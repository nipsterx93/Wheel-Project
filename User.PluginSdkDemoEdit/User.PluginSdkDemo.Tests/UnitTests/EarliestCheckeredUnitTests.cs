// -------------------------------------------------------------------------
// FILE: EarliestCheckeredUnitTests.cs
// Punto 4: il momento della bandiera come minimo del tempo di attraversamento
// su tutte le vetture, invece che sul solo P1 assoluto istantaneo.
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
// NOTA: il minimo gira in MODALITA' OMBRA — calcolato e scritto a log, non usato
// da nessun calcolo. Questi test verificano la funzione, non che il plugin la
// stia usando.
//
// AGGIORNAMENTO 2026-09-01: il minimo e' ristretto alle vetture IN LOTTA per la
// bandiera (entro un giro dal massimo proiettato). La prima versione lo prendeva
// su tutte, e la modalita' ombra ha dimostrato che era sbagliato: su
// 20260831_222417, 867 campioni su 910 sotto il valore in uso, mediana -28.9 s,
// vincitore che ruotava fra 40 vetture, Player compreso. Con 43 vetture in pista
// c'e' sempre qualcuno a pochi metri dalla linea. Una vettura DOPPIATA che taglia
// il traguardo non fa uscire la bandiera.
// -------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using SimRIG;

namespace User.PluginSdkDemo.Tests
{
    public class EarliestCheckeredUnitTests
    {
        private const double CountdownSec = 922.5;
        private const double PlayerAbsolutePos = 22.0675;
        private const double PlayerPaceSec = 76.524;

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception("[EarliestCheckered] " + message);
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
            Console.WriteLine("[TEST] Running Earliest Checkered Tests...");

            Test_Regression_TheSlowOutlierLosesTheMinimum();
            Test_Regression_TheSameNumbersGive37WithP1And35WithTheMinimum();
            Test_PaceFloorRejectsAnImplausiblyFastCar();
            Test_NoFloorMeansNoRejection();
            Test_UnusableCarsAreSkippedNotCounted();
            Test_EmptyInputHasNoResult();
            Test_TheMinimumIsBlindToClass();
            Test_AFasterPaceDoesNotAutomaticallyWinTheMinimum();
            Test_Regression_ALappedCarNearTheLineDoesNotEndTheRace();
            Test_TheSlowOutlierIsExcludedByLapCountBeforeAnyComparison();
            Test_ContendersCountsOnlyTheCarsOnTheLeadLap();

            Console.WriteLine("[TEST SUCCESS] All Earliest Checkered Tests Passed!");
        }

        /// <summary>
        /// Il punto centrale: un passo assurdamente **lento** sposta l'attraversamento in avanti,
        /// quindi quella vettura perde il minimo e non puo' avvelenare niente. Oggi invece bastava
        /// che fosse P1 per un istante.
        ///
        /// Neutralizzazione (ADR-004): sostituendo il minimo col massimo — o riducendo la lista
        /// alla sola vettura che in quell'istante era P1 — il test diventa rosso con 1114.3 s.
        /// </summary>
        private static void Test_Regression_TheSlowOutlierLosesTheMinimum()
        {
            var result = RaceTimeProjection.EarliestCheckeredTime(FromTheLog(), CountdownSec, 0.0);

            Assert(result.HasResult, "con tre vetture valutabili il minimo deve esistere");
            Assert(result.WinnerName == "Sven Neiss",
                   $"deve vincere il leader vero, non chi ha il passo assurdo: ha vinto {result.WinnerName}");
            Assert(Math.Abs(result.TimeSec - 934.931) < 0.01,
                   $"il minimo deve valere 934.93 s, ottenuto {result.TimeSec:F3}");

            // La vettura col passo sballato, da sola, dava questo:
            double poisoned = RaceTimeProjection.TimeUntilLeaderCheckered(CountdownSec, 24.0, 278.563);
            Assert(Math.Abs(poisoned - 1114.252) < 0.01,
                   $"la vettura col passo a 278 s dava 1114.25 s, ottenuto {poisoned:F3}");
            Assert(poisoned > result.TimeSec,
                   "e attraversa piu' tardi del minimo, quindi il minimo la esclude da solo");

            Pass("Regressione: il passo a 278.563 s perde il minimo (934.93 s contro 1114.25 s)");
        }

        /// <summary>
        /// Lo stesso difetto raccontato nell'unita' che l'utente vede: **giri**.
        /// Gli stessi numeri danno 37 col criterio di oggi e 35 col minimo. La gara ne e' durati 35.
        /// </summary>
        private static void Test_Regression_TheSameNumbersGive37WithP1And35WithTheMinimum()
        {
            // Criterio di oggi: si proietta chi e' P1 in questo istante.
            double withP1 = RaceTimeProjection.TimeUntilLeaderCheckered(CountdownSec, 24.0, 278.563);
            double posWithP1 = PlayerAbsolutePos + withP1 / PlayerPaceSec;
            double totalWithP1 = RaceAnalyzer.UpdateLatchedLaps(posWithP1, 35.0, true);

            // Criterio del punto 4.
            var minimum = RaceTimeProjection.EarliestCheckeredTime(FromTheLog(), CountdownSec, 0.0);
            double posWithMinimum = PlayerAbsolutePos + minimum.TimeSec / PlayerPaceSec;
            double totalWithMinimum = RaceAnalyzer.UpdateLatchedLaps(posWithMinimum, 35.0, true);

            Assert(Math.Abs(totalWithP1 - 37.0) < 0.0001,
                   $"col P1 di adesso il totale diventa 37 (il difetto), ottenuto {totalWithP1}");
            Assert(Math.Abs(totalWithMinimum - 35.0) < 0.0001,
                   $"col minimo resta 35 (il valore vero), ottenuto {totalWithMinimum}");
            Assert(Math.Abs(posWithMinimum - 34.285) < 0.01,
                   $"e la posizione proiettata torna a 34.29, ottenuto {posWithMinimum:F3}");

            Pass($"Stessi numeri: P1 -> {totalWithP1:F0} giri (sbagliato), minimo -> {totalWithMinimum:F0} giri (giusto)");
        }

        /// <summary>
        /// Il rovescio dell'asimmetria. Un passo sbagliato per eccesso di **velocita'** puo' vincere
        /// il minimo e anticipare la bandiera per tutti: e' l'unico modo in cui il criterio puo'
        /// sbagliare. Il limite e' il giro piu' veloce realmente osservato — un dato, non un
        /// parametro da tarare.
        ///
        /// Il valore 58.810 non e' inventato: e' una delle baseline anomale registrate in Y-39.
        /// La posizione 24.2 non e' scelta a caso: vedi il test qui sotto sul perche' un passo piu'
        /// veloce **non** vinca automaticamente.
        /// </summary>
        private static void Test_PaceFloorRejectsAnImplausiblyFastCar()
        {
            var cars = FromTheLog();
            cars.Add(Car("baseline anomala (Y-39)", 24.2, 58.810));

            double fastestLapSeen = 68.443;

            var unguarded = RaceTimeProjection.EarliestCheckeredTime(cars, CountdownSec, 0.0);
            var guarded = RaceTimeProjection.EarliestCheckeredTime(cars, CountdownSec, fastestLapSeen);

            Assert(unguarded.WinnerName == "baseline anomala (Y-39)",
                   $"senza limite la baseline anomala vince il minimo, ha vinto {unguarded.WinnerName}");
            Assert(guarded.WinnerName == "Sven Neiss",
                   $"col limite deve tornare a vincere il leader vero, ha vinto {guarded.WinnerName}");
            Assert(guarded.RejectedByFloor == 1,
                   $"e la vettura scartata deve essere contata, contate {guarded.RejectedByFloor}");

            Pass("Limite di plausibilita': la baseline a 58.810 s viene scartata, non vince il minimo");
        }

        /// <summary>
        /// Limite a zero = nessun giro di riferimento ancora osservato. Meglio nessun giudizio che
        /// uno basato su un limite inventato: stessa scelta di IsPhysicallyPlausibleLap.
        /// </summary>
        private static void Test_NoFloorMeansNoRejection()
        {
            var result = RaceTimeProjection.EarliestCheckeredTime(FromTheLog(), CountdownSec, 0.0);

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

            var result = RaceTimeProjection.EarliestCheckeredTime(cars, CountdownSec, 60.0);

            Assert(result.Considered == 3, $"le due vetture senza dato non si contano, valutate {result.Considered}");
            Assert(result.RejectedByFloor == 0,
                   $"e non vanno confuse con quelle scartate dal limite, scartate {result.RejectedByFloor}");
            Assert(result.WinnerName == "Sven Neiss", $"il minimo non cambia, ha vinto {result.WinnerName}");

            Pass("Dati mancanti: saltati, e distinti dagli scarti per implausibilita'");
        }

        /// <summary>
        /// Nessuna vettura valutabile: si dichiara di non sapere invece di restituire uno zero che
        /// a valle sembrerebbe "bandiera adesso".
        /// </summary>
        private static void Test_EmptyInputHasNoResult()
        {
            var empty = RaceTimeProjection.EarliestCheckeredTime(new List<RaceTimeProjection.CrossingCandidate>(),
                                                                 CountdownSec, 0.0);
            var nothing = RaceTimeProjection.EarliestCheckeredTime(null, CountdownSec, 0.0);

            Assert(!empty.HasResult, "lista vuota: nessun risultato");
            Assert(!nothing.HasResult, "lista assente: nessun risultato");
            Assert(empty.Considered == 0, "e nessuna vettura valutata");

            Pass("Nessuna vettura valutabile: si dichiara l'assenza di risultato");
        }

        /// <summary>
        /// Chi taglia per primo dopo lo scadere **e'** il leader, qualunque classe abbia. Qui una
        /// GT3 molto avanti nei giri batte una GTP piu' veloce ma piu' indietro: il criterio guarda
        /// il tempo di attraversamento, non la velocita' e non la categoria.
        ///
        /// Serve anche a fissare per iscritto che questo NON e' un leader di classe: quello non
        /// esiste da nessuna parte nel progetto e non serve per il carburante.
        /// </summary>
        private static void Test_TheMinimumIsBlindToClass()
        {
            var cars = new List<RaceTimeProjection.CrossingCandidate>
            {
                Car("GTP piu' veloce ma indietro", 20.00, 68.0),
                Car("GT3 piu' lenta ma avanti", 24.90, 77.5)
            };

            var result = RaceTimeProjection.EarliestCheckeredTime(cars, CountdownSec, 0.0);

            double gtp = RaceTimeProjection.TimeUntilLeaderCheckered(CountdownSec, 20.00, 68.0);
            double gt3 = RaceTimeProjection.TimeUntilLeaderCheckered(CountdownSec, 24.90, 77.5);

            Assert(result.WinnerName == (gt3 < gtp ? "GT3 piu' lenta ma avanti" : "GTP piu' veloce ma indietro"),
                   $"deve vincere chi attraversa prima ({gt3:F1} contro {gtp:F1}), ha vinto {result.WinnerName}");
            Assert(Math.Abs(result.TimeSec - Math.Min(gtp, gt3)) < 0.0001,
                   $"e il tempo deve essere il minimo dei due, ottenuto {result.TimeSec:F3}");

            Pass($"Il minimo guarda il tempo di attraversamento, non la classe (vince {result.WinnerName})");
        }

        /// <summary>
        /// **Proprieta' controintuitiva, fissata qui perche' e' facile sbagliarla.** Il minimo non
        /// premia il passo piu' veloce: premia chi **attraversa prima**. Il tempo di attraversamento
        /// vale <c>T + frazione x passo</c> con la frazione in [0,1), quindi un passo piu' veloce
        /// abbassa il *tetto* del supplemento ma non il supplemento di adesso.
        ///
        /// Conseguenza pratica, che va capita prima di accendere il punto 4: una vettura con un
        /// passo falsamente veloce **non** falsa la bandiera in continuazione — la falsa a
        /// intermittenza, ogni volta che la sua frazione capita bassa. Un errore che va e viene
        /// e' piu' difficile da riconoscere a occhio di uno costante, non meno grave.
        ///
        /// Stessa vettura, stesso passo di 58.810 s, due posizioni distanti 20 centesimi di giro:
        /// da una perde il minimo, dall'altra lo vince.
        /// </summary>
        private static void Test_AFasterPaceDoesNotAutomaticallyWinTheMinimum()
        {
            double leader = RaceTimeProjection.TimeUntilLeaderCheckered(CountdownSec, 24.34, 68.443);

            double fastButLoses = RaceTimeProjection.TimeUntilLeaderCheckered(CountdownSec, 24.0, 58.810);
            double fastAndWins = RaceTimeProjection.TimeUntilLeaderCheckered(CountdownSec, 24.2, 58.810);

            Assert(fastButLoses > leader,
                   $"a posizione 24.0 il passo veloce attraversa DOPO il leader ({fastButLoses:F1} contro {leader:F1})");
            Assert(fastAndWins < leader,
                   $"a posizione 24.2 lo stesso passo attraversa PRIMA ({fastAndWins:F1} contro {leader:F1})");

            Pass($"Passo piu' veloce non vince da solo: stesso passo, 940.96 s da una posizione e 929.20 s dall'altra");
        }

        /// <summary>
        /// **Il difetto che la modalita' ombra ha intercettato.** Una vettura doppiata a pochi metri
        /// dal traguardo taglia prima di chiunque altro dopo lo scadere del cronometro — ma non fa
        /// finire la gara, perche' non e' in lotta per la bandiera.
        ///
        /// Numeri: la doppiata (passo GT3 77.20 s, posizione 22.0) proietta **33.949** contro il
        /// **37.818** del leader, quindi e' quasi quattro giri indietro. Il suo attraversamento cade
        /// a **926.4 s**, prima dei **934.9 s** del leader: col minimo ingenuo vincerebbe lei.
        ///
        /// Neutralizzazione (ADR-004): alzando LeadLapMarginLaps a un valore grande — cioe'
        /// togliendo la restrizione — il test diventa rosso, e vince la doppiata con 926.4 s.
        /// </summary>
        private static void Test_Regression_ALappedCarNearTheLineDoesNotEndTheRace()
        {
            var cars = FromTheLog();
            cars.Add(Car("doppiato vicino al traguardo", 22.0, 77.20));

            var result = RaceTimeProjection.EarliestCheckeredTime(cars, CountdownSec, 0.0);

            // Premesse sui soli ingressi, indipendenti dalla funzione in prova: restano vere anche
            // neutralizzando la restrizione, cosi' il rosso cade sull'asserzione che conta.
            double lappedCrossing = RaceTimeProjection.TimeUntilLeaderCheckered(CountdownSec, 22.0, 77.20);
            double leaderCrossing = RaceTimeProjection.TimeUntilLeaderCheckered(CountdownSec, 24.34, 68.443);
            Assert(Math.Abs(lappedCrossing - 926.400) < 0.01,
                   $"la doppiata attraversa a 926.40 s, ottenuto {lappedCrossing:F3}");
            Assert(lappedCrossing < leaderCrossing,
                   $"e attraversa PRIMA del leader ({lappedCrossing:F1} contro {leaderCrossing:F1}): col minimo ingenuo vincerebbe lei");

            Assert(result.WinnerName == "Sven Neiss",
                   $"ma la bandiera la fa uscire il leader, ha vinto {result.WinnerName}");
            Assert(Math.Abs(result.TimeSec - 934.931) < 0.01,
                   $"e il momento resta 934.93 s, ottenuto {result.TimeSec:F3}");

            Pass("Regressione: la doppiata attraversa a 926.4 s ma non fa uscire la bandiera (934.9 s)");
        }

        /// <summary>
        /// Proprieta' piu' forte di quella verificata sopra sul passo lento. La vettura col passo a
        /// 278 s non "perde" il confronto: **non ci arriva proprio**. Proietta 27.312 contro il
        /// 37.818 del massimo, cioe' dieci giri indietro, quindi esce con i doppiati.
        ///
        /// Conta perche' rende il criterio indipendente dal riconoscere il passo come anomalo: non
        /// serve una soglia sul passo per escluderla, ci pensa il conteggio giri.
        /// </summary>
        private static void Test_TheSlowOutlierIsExcludedByLapCountBeforeAnyComparison()
        {
            var onlyTheOutlierAndTheLeader = new List<RaceTimeProjection.CrossingCandidate>
            {
                Car("Alessandro Barbagallo", 24.0, 278.563),
                Car("Sven Neiss", 24.34, 68.443)
            };

            var result = RaceTimeProjection.EarliestCheckeredTime(onlyTheOutlierAndTheLeader, CountdownSec, 0.0);

            Assert(result.Considered == 2, $"entrambe le vetture sono valutabili, valutate {result.Considered}");
            Assert(result.Contenders == 1, $"ma una sola e' in lotta, in lotta {result.Contenders}");
            Assert(result.WinnerName == "Sven Neiss", $"ha vinto {result.WinnerName}");
            Assert(Math.Abs(result.MaxProjectedPos - 37.818) < 0.01,
                   $"il massimo proiettato deve essere 37.82, ottenuto {result.MaxProjectedPos:F3}");

            Pass("Il passo a 278 s proietta 27.3 contro 37.8: escluso dal conteggio giri, non dal confronto");
        }

        /// <summary>
        /// Il conteggio dei contendenti e' il numero da leggere nel log per capire se la restrizione
        /// sta lavorando: se vale 1 per tutta la gara il minimo sta degenerando nel leader corrente e
        /// il punto 4 non porta niente; se vale quanto le vetture in pista non sta filtrando nulla.
        /// </summary>
        private static void Test_ContendersCountsOnlyTheCarsOnTheLeadLap()
        {
            var cars = new List<RaceTimeProjection.CrossingCandidate>
            {
                Car("in lotta A", 24.34, 68.443),   // proietta 37.818, e' il massimo
                Car("in lotta B", 24.00, 68.443),   // proietta 37.478, dentro il margine
                Car("doppiato", 22.00, 77.200),     // proietta 33.949, fuori
                Car("passo assurdo", 24.00, 278.563) // proietta 27.312, fuori
            };

            var result = RaceTimeProjection.EarliestCheckeredTime(cars, CountdownSec, 0.0);

            Assert(result.Considered == 4, $"tutte e quattro sono valutabili, valutate {result.Considered}");
            Assert(result.Contenders == 2, $"due sono in lotta, in lotta {result.Contenders}");

            Pass("Contendenti: 4 vetture valutate, 2 sul giro del leader");
        }
    }
}
