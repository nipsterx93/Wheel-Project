// -------------------------------------------------------------------------
// FILE: FlagMomentUnitTests.cs
// Punto 4: il momento della bandiera dalla vettura che sara' AL COMANDO allo
// scadere del cronometro, invece che dal P1 di questo istante.
//
// Caso di regressione dal replay Road Atlanta del 2026-08-31
// (Logs/Road Atlanta/SimRIG_DebugLog_20260831_195300.csv), ore 20:04:08.520:
// il P1 assoluto passa da Sven Neiss ad Alessandro Barbagallo, che porta con
// se' un passo registrato di 278.563 s contro i ~68 reali. Un tick dopo il
// totale giri del Player sale a 37.
//
// Numeri letti dal log:
//   countdown            922.5 s
//   Barbagallo           posizione assoluta 24.0    passo 278.563 s
//   Sven Neiss           posizione assoluta 24.34   passo  68.443 s
//   Player               posizione assoluta 22.0675 passo  76.524 s
//
// STORIA DEL CRITERIO, perche' non si ripercorra la stessa strada:
//   v1  minimo del tempo di attraversamento su TUTTE le vetture.
//       Bocciato da 20260831_222417: con 43 vetture in pista ce n'e' sempre
//       una a pochi metri dalla linea, quindi il minimo collassava sul
//       countdown. 867 campioni su 910 sotto il valore in uso.
//   v2  stesso minimo, ristretto alle vetture entro un giro dal massimo.
//       Bocciato da 20260901_175019 con una misura che non dipende dal codice:
//       il supplemento (di quanto la bandiera esce DOPO lo scadere) deve
//       valere ~35 s su un giro da 69, e dava mediana 5.2 s. Impossibile.
//       "Chi taglia per primo" coincide col leader solo fra vetture sullo
//       STESSO giro; a cavallo del confine si inverte.
//   v3  massimo della posizione proiettata: chi comanda quando esce la
//       bandiera. E' quello verificato qui.
//
// NOTA: v3 e' ATTIVA dal 2026-09-01 (commit 718cd8b) e alimenta il tempo alla
// bandiera; dal 2026-09-02 (Y-48, commit 0fd8e21) anche il totale e la
// proiezione del leader. Questo file verifica la formula; il cablaggio sta in
// FlagTimeWiringUnitTests.cs e LeaderTotalFromCommandUnitTests.cs — distinzione
// che questo repository ha pagato tre volte (Y-31, Y-46, Y-48: formula giusta,
// collegata al posto sbagliato).
// -------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using SimRIG;

namespace User.PluginSdkDemo.Tests
{
    public class FlagMomentUnitTests
    {
        private const double CountdownSec = 922.5;
        private const double PlayerAbsolutePos = 22.0675;
        private const double PlayerPaceSec = 76.524;

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception("[FlagMoment] " + message);
        }

        private static void Pass(string name)
        {
            Console.WriteLine("  [PASS] " + name);
        }

        private static RaceTimeProjection.CrossingCandidate Car(string name, double pos, double pace)
        {
            return new RaceTimeProjection.CrossingCandidate { Name = name, AbsolutePos = pos, PaceSec = pace };
        }

        /// <summary>Le due vetture in gioco al momento del difetto, piu' il Player.</summary>
        private static List<RaceTimeProjection.CrossingCandidate> FromTheLog()
        {
            return new List<RaceTimeProjection.CrossingCandidate>
            {
                Car("Alessandro Barbagallo", 24.0, 278.563),
                Car("Sven Neiss", 24.34, 68.443),
                Car("PLAYER", PlayerAbsolutePos, PlayerPaceSec)
            };
        }

        public static void RunAllTests()
        {
            Console.WriteLine("[TEST] Running Flag Moment Tests...");

            Test_Regression_ThePoisonedPaceIsNeverInCommand();
            Test_Regression_TheSameNumbersGive37WithP1And35WithTheLeader();
            Test_Regression_CrossingFirstIsNotLeading();
            Test_TheOldMinimumIsNeverLaterThanTheLeader();
            Test_PaceFloorRejectsAnImplausiblyFastCar();
            Test_NoFloorMeansNoRejection();
            Test_UnusableCarsAreSkippedNotCounted();
            Test_EmptyInputHasNoResult();
            Test_CommandIsBlindToClass();
            Test_ContendersIsDiagnosticOnly();

            Console.WriteLine("[TEST SUCCESS] All Flag Moment Tests Passed!");
        }

        /// <summary>
        /// Il punto centrale. Un passo assurdamente lento **abbassa** la posizione proiettata, quindi
        /// quella vettura non puo' essere al comando e non entra nel calcolo. Non serve riconoscere
        /// il passo come anomalo: se ne occupa il conteggio giri.
        ///
        /// Barbagallo proietta **27.312**, Sven **37.818**. Dieci giri di distanza.
        ///
        /// Neutralizzazione (ADR-004): cercando il **minimo** della posizione proiettata invece del
        /// massimo, il test diventa rosso e comanda Barbagallo con 1114.3 s.
        /// </summary>
        private static void Test_Regression_ThePoisonedPaceIsNeverInCommand()
        {
            var result = RaceTimeProjection.ProjectFlagMoment(FromTheLog(), CountdownSec, 0.0);

            Assert(result.HasResult, "con tre vetture valutabili il risultato deve esistere");
            Assert(result.LeaderName == "Sven Neiss",
                   $"deve comandare il leader vero, non chi ha il passo assurdo: comanda {result.LeaderName}");
            Assert(Math.Abs(result.MaxProjectedPos - 37.818) < 0.01,
                   $"la posizione proiettata al comando deve essere 37.82, ottenuto {result.MaxProjectedPos:F3}");
            Assert(Math.Abs(result.TimeSec - 934.931) < 0.01,
                   $"la bandiera deve cadere a 934.93 s, ottenuto {result.TimeSec:F3}");

            double poisoned = 24.0 + CountdownSec / 278.563;
            Assert(Math.Abs(poisoned - 27.312) < 0.01,
                   $"la vettura col passo a 278 s proietta 27.31, ottenuto {poisoned:F3}");
            Assert(poisoned < result.MaxProjectedPos - 1.0,
                   "ed e' oltre un giro sotto il comando: non ci arriva nemmeno vicino");

            Pass("Regressione: il passo a 278 s proietta 27.3 contro 37.8, non comanda mai");
        }

        /// <summary>
        /// Lo stesso difetto nell'unita' che l'utente vede: **giri**. Gli stessi numeri danno 37 col
        /// criterio di oggi (P1 di questo istante) e 35 col comando. La gara ne e' durati 35.
        /// </summary>
        private static void Test_Regression_TheSameNumbersGive37WithP1And35WithTheLeader()
        {
            // Criterio di oggi: si proietta chi e' P1 in questo istante, cioe' Barbagallo.
            double withP1 = RaceTimeProjection.TimeUntilLeaderCheckered(CountdownSec, 24.0, 278.563);
            double posWithP1 = PlayerAbsolutePos + withP1 / PlayerPaceSec;
            double totalWithP1 = RaceAnalyzer.UpdateLatchedLaps(posWithP1, 35.0, true);

            // Criterio del punto 4.
            var flag = RaceTimeProjection.ProjectFlagMoment(FromTheLog(), CountdownSec, 0.0);
            double posWithLeader = PlayerAbsolutePos + flag.TimeSec / PlayerPaceSec;
            double totalWithLeader = RaceAnalyzer.UpdateLatchedLaps(posWithLeader, 35.0, true);

            Assert(Math.Abs(totalWithP1 - 37.0) < 0.0001,
                   $"col P1 di adesso il totale diventa 37 (il difetto), ottenuto {totalWithP1}");
            Assert(Math.Abs(totalWithLeader - 35.0) < 0.0001,
                   $"col comando resta 35 (il valore vero), ottenuto {totalWithLeader}");
            Assert(Math.Abs(posWithLeader - 34.285) < 0.01,
                   $"e la posizione proiettata torna a 34.29, ottenuto {posWithLeader:F3}");

            Pass($"Stessi numeri: P1 -> {totalWithP1:F0} giri (sbagliato), comando -> {totalWithLeader:F0} giri (giusto)");
        }

        /// <summary>
        /// **Il difetto che ha ucciso il criterio precedente, in due righe.** "Chi taglia per primo
        /// dopo lo scadere" coincide col leader **solo fra vetture sullo stesso giro**. A cavallo
        /// del confine si inverte.
        ///
        /// Due vetture con lo stesso passo: <c>A</c> ha appena iniziato il giro 39 (proiettata
        /// 39.05), <c>B</c> sta chiudendo il 38 (proiettata 38.95). <c>B</c> taglia dopo **3.45 s**
        /// dallo scadere, <c>A</c> dopo **65.55 s** — ma il traguardo di <c>B</c> chiude il *suo*
        /// giro 38 e non fa finire la gara.
        ///
        /// Il supplemento e' la firma: 3.45 s significherebbe un leader sempre a un passo dalla
        /// linea proprio allo scadere. Misurato sul replay, il vecchio criterio dava mediana 5.2 s.
        ///
        /// Neutralizzazione: prendendo il minimo del tempo di attraversamento invece del massimo
        /// della posizione, il test diventa rosso e la bandiera cade a 925.95 s.
        /// </summary>
        private static void Test_Regression_CrossingFirstIsNotLeading()
        {
            // Posizioni scelte perche' le proiettate cadano a 39.05 e 38.95 con countdown 922.5.
            var cars = new List<RaceTimeProjection.CrossingCandidate>
            {
                Car("A al comando", 25.680, 69.0),
                Car("B dietro di un soffio", 25.580, 69.0)
            };

            var result = RaceTimeProjection.ProjectFlagMoment(cars, CountdownSec, 0.0);

            double crossingA = RaceTimeProjection.TimeUntilLeaderCheckered(CountdownSec, 25.680, 69.0);
            double crossingB = RaceTimeProjection.TimeUntilLeaderCheckered(CountdownSec, 25.580, 69.0);

            // Premesse sui soli ingressi: restano vere anche neutralizzando la correzione.
            Assert(crossingB < crossingA,
                   $"B taglia prima di A ({crossingB:F2} contro {crossingA:F2}): e' la premessa del caso");
            Assert(Math.Abs((crossingB - CountdownSec) - 3.45) < 0.05,
                   $"e lo fa 3.45 s dopo lo scadere, ottenuto {crossingB - CountdownSec:F2}");

            Assert(result.LeaderName == "A al comando",
                   $"ma comanda A, che ha la proiettata piu' alta: comanda {result.LeaderName}");
            Assert(Math.Abs(result.TimeSec - crossingA) < 0.0001,
                   $"e la bandiera cade sul suo attraversamento, ottenuto {result.TimeSec:F2}");
            Assert(Math.Abs((result.TimeSec - CountdownSec) - 65.55) < 0.05,
                   $"con un supplemento di 65.55 s, plausibile per un leader a fine giro, ottenuto {result.TimeSec - CountdownSec:F2}");

            Pass("Tagliare per primo non e' comandare: B taglia a +3.45 s, ma la bandiera e' quella di A a +65.55 s");
        }

        /// <summary>
        /// Invariante che tiene onesto il confronto a log: chi comanda **e'** fra i contendenti (dista
        /// zero dal massimo), quindi il vecchio minimo non puo' mai essere piu' tardi del nuovo
        /// valore. Se un giorno lo fosse, il calcolo dei contendenti e' rotto.
        /// </summary>
        private static void Test_TheOldMinimumIsNeverLaterThanTheLeader()
        {
            var cars = FromTheLog();
            cars.Add(Car("B dietro di un soffio", 25.580, 69.0));
            cars.Add(Car("A al comando", 25.680, 69.0));

            var result = RaceTimeProjection.ProjectFlagMoment(cars, CountdownSec, 0.0);

            Assert(result.EarliestCrossingSec <= result.TimeSec + 0.0001,
                   $"il vecchio minimo ({result.EarliestCrossingSec:F2}) non puo' superare il comando ({result.TimeSec:F2})");
            Assert(result.Contenders >= 1, "e chi comanda conta sempre come contendente");

            Pass($"Invariante: vecchio minimo {result.EarliestCrossingSec:F1} s <= comando {result.TimeSec:F1} s");
        }

        /// <summary>
        /// **Il verso del pericolo si e' invertito rispetto al criterio del minimo, e va detto.**
        /// Col minimo era pericoloso un passo troppo veloce solo di rado; col massimo lo e' sempre,
        /// perche' un passo falsamente veloce **gonfia** la posizione proiettata
        /// (<c>pos + T/passo</c>) e porta quella vettura al comando.
        ///
        /// Il valore 58.810 non e' inventato: e' una delle baseline anomale registrate in Y-39. A
        /// posizione 24.2 proietta **39.886**, sopra il 37.818 del leader vero.
        ///
        /// Il massimo e' per sua natura sensibile a un solo campione sbagliato — il difetto di
        /// famiglia di questo repository (ADR-005) — e questo limite e' la sua unica protezione.
        /// </summary>
        private static void Test_PaceFloorRejectsAnImplausiblyFastCar()
        {
            var cars = FromTheLog();
            cars.Add(Car("baseline anomala (Y-39)", 24.2, 58.810));

            double fastestLapSeen = 68.443;

            var unguarded = RaceTimeProjection.ProjectFlagMoment(cars, CountdownSec, 0.0);
            var guarded = RaceTimeProjection.ProjectFlagMoment(cars, CountdownSec, fastestLapSeen);

            Assert(unguarded.LeaderName == "baseline anomala (Y-39)",
                   $"senza limite la baseline anomala va al comando, comanda {unguarded.LeaderName}");
            Assert(Math.Abs(unguarded.MaxProjectedPos - 39.886) < 0.01,
                   $"perche' proietta 39.89, ottenuto {unguarded.MaxProjectedPos:F3}");

            Assert(guarded.LeaderName == "Sven Neiss",
                   $"col limite deve tornare a comandare il leader vero, comanda {guarded.LeaderName}");
            Assert(guarded.RejectedByFloor == 1,
                   $"e la vettura scartata deve essere contata, contate {guarded.RejectedByFloor}");

            Pass("Limite di plausibilita': la baseline a 58.810 s proietta 39.89 e verrebbe al comando, viene scartata");
        }

        /// <summary>
        /// Limite a zero = nessun giro di riferimento ancora osservato. Meglio nessun giudizio che
        /// uno basato su un limite inventato: stessa scelta di IsPhysicallyPlausibleLap.
        /// </summary>
        private static void Test_NoFloorMeansNoRejection()
        {
            var result = RaceTimeProjection.ProjectFlagMoment(FromTheLog(), CountdownSec, 0.0);

            Assert(result.RejectedByFloor == 0, $"senza limite non si scarta nulla, scartate {result.RejectedByFloor}");
            Assert(result.Considered == 3, $"e si valutano tutte e tre le vetture, valutate {result.Considered}");

            Pass("Nessun limite osservato: nessuno scarto");
        }

        /// <summary>
        /// Una vettura senza passo non e' una vettura lenta: e' un dato mancante. Non deve entrare
        /// nel confronto ne' essere contata fra quelle valutate — e nemmeno fra le scartate dal
        /// limite, che e' un motivo diverso e va potuto distinguere a log.
        /// </summary>
        private static void Test_UnusableCarsAreSkippedNotCounted()
        {
            var cars = FromTheLog();
            cars.Add(Car("appena entrata, nessun giro", 0.0, 0.0));
            cars.Add(Car("posizione non arrivata", -1.0, 70.0));

            var result = RaceTimeProjection.ProjectFlagMoment(cars, CountdownSec, 60.0);

            Assert(result.Considered == 3, $"le due vetture senza dato non si contano, valutate {result.Considered}");
            Assert(result.RejectedByFloor == 0,
                   $"e non vanno confuse con quelle scartate dal limite, scartate {result.RejectedByFloor}");
            Assert(result.LeaderName == "Sven Neiss", $"il comando non cambia, comanda {result.LeaderName}");

            Pass("Dati mancanti: saltati, e distinti dagli scarti per implausibilita'");
        }

        /// <summary>
        /// Nessuna vettura valutabile: si dichiara di non sapere invece di restituire uno zero che a
        /// valle sembrerebbe "bandiera adesso".
        /// </summary>
        private static void Test_EmptyInputHasNoResult()
        {
            var empty = RaceTimeProjection.ProjectFlagMoment(new List<RaceTimeProjection.CrossingCandidate>(),
                                                             CountdownSec, 0.0);
            var nothing = RaceTimeProjection.ProjectFlagMoment(null, CountdownSec, 0.0);

            Assert(!empty.HasResult, "lista vuota: nessun risultato");
            Assert(!nothing.HasResult, "lista assente: nessun risultato");
            Assert(empty.Considered == 0, "e nessuna vettura valutata");

            Pass("Nessuna vettura valutabile: si dichiara l'assenza di risultato");
        }

        /// <summary>
        /// Chi comanda allo scadere **e'** il leader, qualunque classe abbia. Il criterio guarda la
        /// posizione proiettata, non la velocita' e non la categoria.
        ///
        /// Serve anche a fissare per iscritto che questo NON e' un leader di classe: quello non
        /// esiste da nessuna parte nel progetto e non serve per il carburante.
        /// </summary>
        private static void Test_CommandIsBlindToClass()
        {
            var cars = new List<RaceTimeProjection.CrossingCandidate>
            {
                Car("GTP piu' veloce ma indietro", 20.00, 68.0),   // proietta 33.566
                Car("GT3 piu' lenta ma avanti", 24.90, 77.5)       // proietta 36.803
            };

            var result = RaceTimeProjection.ProjectFlagMoment(cars, CountdownSec, 0.0);

            Assert(result.LeaderName == "GT3 piu' lenta ma avanti",
                   $"comanda chi e' proiettato piu' avanti, non chi e' piu' veloce: comanda {result.LeaderName}");
            Assert(Math.Abs(result.TimeSec - RaceTimeProjection.TimeUntilLeaderCheckered(CountdownSec, 24.90, 77.5)) < 0.0001,
                   "e la bandiera cade sul suo attraversamento");

            Pass("Il comando guarda la posizione proiettata, non la classe");
        }

        /// <summary>
        /// Il conteggio dei contendenti e' **diagnostico**: dice quanto e' contesa la testa della
        /// corsa e va a log, ma non deve cambiare il risultato. Aggiungere una vettura doppiata
        /// cambia il conteggio e basta.
        /// </summary>
        private static void Test_ContendersIsDiagnosticOnly()
        {
            // A proietta 39.05, B 38.95: distano un decimo di giro, quindi sono entrambi in lotta.
            // Sven proietta 37.82, cioe' 1.23 giri dietro ad A: e' doppiato e non conta.
            var alone = new List<RaceTimeProjection.CrossingCandidate>
            {
                Car("A al comando", 25.680, 69.0)
            };
            var withRival = new List<RaceTimeProjection.CrossingCandidate>
            {
                Car("A al comando", 25.680, 69.0),
                Car("B dietro di un soffio", 25.580, 69.0)
            };
            var withRivalAndLapped = new List<RaceTimeProjection.CrossingCandidate>
            {
                Car("A al comando", 25.680, 69.0),
                Car("B dietro di un soffio", 25.580, 69.0),
                Car("Sven Neiss, un giro dietro", 24.34, 68.443)
            };

            var a = RaceTimeProjection.ProjectFlagMoment(alone, CountdownSec, 0.0);
            var b = RaceTimeProjection.ProjectFlagMoment(withRival, CountdownSec, 0.0);
            var c = RaceTimeProjection.ProjectFlagMoment(withRivalAndLapped, CountdownSec, 0.0);

            Assert(a.Contenders == 1, $"da sola c'e' un contendente, contati {a.Contenders}");
            Assert(b.Contenders == 2, $"col rivale a un decimo di giro ce ne sono due, contati {b.Contenders}");
            Assert(c.Contenders == 2, $"la doppiata non aggiunge un contendente, contati {c.Contenders}");
            Assert(c.Considered == 3, $"ma resta fra le vetture valutate, valutate {c.Considered}");

            Assert(a.LeaderName == "A al comando" && b.LeaderName == "A al comando" && c.LeaderName == "A al comando",
                   $"e il comando non cambia mai: {a.LeaderName} / {b.LeaderName} / {c.LeaderName}");
            Assert(Math.Abs(a.TimeSec - c.TimeSec) < 0.0001,
                   $"ne' cambia il momento della bandiera: {a.TimeSec:F3} contro {c.TimeSec:F3}");

            Pass("Contendenti: solo diagnostica — il conteggio cambia, il comando e la bandiera no");
        }
    }
}
