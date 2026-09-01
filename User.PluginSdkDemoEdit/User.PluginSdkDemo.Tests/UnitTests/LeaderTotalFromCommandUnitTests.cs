// -------------------------------------------------------------------------
// FILE: LeaderTotalFromCommandUnitTests.cs
// Y-48: il totale giri del leader seguiva ancora il P1 di questo istante, e
// crollava con lui, benche' il punto 4 fosse gia' acceso e gia' corretto.
//
// Caso di regressione dal replay Road Atlanta del 2026-09-01
// (Logs/Road Atlanta/SimRIG_DebugLog_20260901_202537.csv), ore 20:36:33.
// Nello stesso identico tick:
//
//   VECCHIO: P1=Alessandro Barbagallo  proiezione=27.886  -> totale 30, 29, 28
//   PUNTO 4: comanda=Aleix Nogue  passo=68.18  proiezione=38.27
//
// La risposta giusta era calcolata e non collegata. Nessuna formula era
// sbagliata: mancava un filo. E' la lezione di Y-31 per la terza volta —
// il difetto sta nel chiamante, e un test sulla sola aritmetica resta verde.
//
// Verita' di terreno dello stesso replay, misurata dal gioco nell'istante in
// cui il cronometro e' andato a zero: il leader era a 38.061, quindi ha
// completato 39 giri.
// -------------------------------------------------------------------------

using System;
using SimRIG;

namespace User.PluginSdkDemo.Tests
{
    public class LeaderTotalFromCommandUnitTests
    {
        /// <summary>Proiezione del P1 istantaneo nel tick del crollo, letta dal log.</summary>
        private const double PoisonedP1Projection = 27.886;

        /// <summary>Proiezione di chi comandava davvero nello stesso tick.</summary>
        private const double CommandingCarProjection = 38.27;

        /// <summary>Verita' di terreno: dove era il leader allo scadere del cronometro.</summary>
        private const double GroundTruthAtExpiry = 38.061;

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception("[LeaderTotalFromCommand] " + message);
        }

        private static void Pass(string name)
        {
            Console.WriteLine("  [PASS] " + name);
        }

        public static void RunAllTests()
        {
            Console.WriteLine("[TEST] Running Leader Total From Command Tests...");

            Test_Regression_TheLeaderTotalDoesNotFollowThePoisonedP1();
            Test_Regression_TheWholeChainEndsAt39NotAt28();
            Test_NoCommandingCarFallsBackToTheCurrentP1();
            Test_AZeroFromTheCommandingCarIsRefused();
            Test_TheGroundTruthRoundsToTheLapsActuallyCompleted();

            Console.WriteLine("[TEST SUCCESS] All Leader Total From Command Tests Passed!");
        }

        /// <summary>
        /// La scelta, isolata. Con una vettura al comando si usa la sua proiezione, non quella del
        /// P1 di questo istante — anche quando quest'ultima e' dieci giri piu' bassa.
        ///
        /// Neutralizzazione (ADR-004): rimettendo la proiezione del P1 come sorgente, il test
        /// diventa rosso con 27,886.
        /// </summary>
        private static void Test_Regression_TheLeaderTotalDoesNotFollowThePoisonedP1()
        {
            double chosen = RaceAnalyzer.ResolveLeaderPosAtZero(
                hasCommandingCar: true,
                fromCommandingCar: CommandingCarProjection,
                fromCurrentP1: PoisonedP1Projection);

            Assert(Math.Abs(chosen - CommandingCarProjection) < 0.0001,
                   $"deve vincere la proiezione di chi comanda, ottenuto {chosen}");
            Assert(chosen - PoisonedP1Projection > 10.0,
                   "e lo scarto fra le due era di oltre dieci giri: non e' una sfumatura");
            Pass("Regressione: si usa 38.27 di chi comanda, non il 27.886 del P1 avvelenato");
        }

        /// <summary>
        /// **La catena intera**, che e' il punto: la scelta piu' la banda di stabilita', come nel
        /// codice di produzione. Il totale del leader deve arrivare a **39** — il numero di giri che
        /// il leader ha davvero completato, secondo la verita' di terreno misurata dal gioco — e non
        /// ai 28 che il replay mostrava.
        ///
        /// Un test sulla sola <c>UpdateLatchedLaps</c> non intercetterebbe nulla: quella funzione
        /// faceva gia' il suo lavoro correttamente, le si dava in pasto il numero sbagliato.
        /// </summary>
        private static void Test_Regression_TheWholeChainEndsAt39NotAt28()
        {
            // Col difetto: la proiezione avvelenata entra nella banda.
            double broken = RaceAnalyzer.UpdateLatchedLaps(PoisonedP1Projection, 39.0, true);

            // Corretto: entra quella di chi comanda.
            double chosen = RaceAnalyzer.ResolveLeaderPosAtZero(true, CommandingCarProjection, PoisonedP1Projection);
            double fixedTotal = RaceAnalyzer.UpdateLatchedLaps(chosen, 39.0, true);

            Assert(Math.Abs(broken - 28.0) < 0.0001,
                   $"col difetto il totale del leader crolla a 28, ottenuto {broken}");
            Assert(Math.Abs(fixedTotal - 39.0) < 0.0001,
                   $"col cablaggio corretto resta 39, ottenuto {fixedTotal}");
            Assert(Math.Abs(Math.Ceiling(GroundTruthAtExpiry) - fixedTotal) < 0.0001,
                   "e 39 e' anche quello che dice la verita' di terreno misurata dal gioco");

            Pass("Catena intera: 28 col difetto, 39 col cablaggio — e 39 e' il valore vero");
        }

        /// <summary>
        /// Se nessuna vettura era valutabile — succede nei primi secondi, prima che gli avversari
        /// abbiano un passo — si ripiega sul P1 di questo istante. Una stima imperfetta vale piu' di
        /// uno zero, che a valle si legge come "il leader e' sulla linea di partenza".
        /// </summary>
        private static void Test_NoCommandingCarFallsBackToTheCurrentP1()
        {
            double fromZero = RaceAnalyzer.ResolveLeaderPosAtZero(false, 0.0, 38.7);
            double fromSaneLooking = RaceAnalyzer.ResolveLeaderPosAtZero(false, 500.0, 38.7);

            Assert(Math.Abs(fromZero - 38.7) < 0.0001,
                   $"senza vettura al comando si ripiega sul P1, ottenuto {fromZero}");
            Assert(Math.Abs(fromSaneLooking - 38.7) < 0.0001,
                   $"anche se il numero sembra sano: e' dichiarato assente, ottenuto {fromSaneLooking}");

            Pass("Nessuna vettura al comando: ripiego sul P1, qualunque sia il numero");
        }

        /// <summary>
        /// Cintura e bretelle, come per il tempo alla bandiera: una proiezione a zero o negativa non
        /// si espone nemmeno se dichiarata valida.
        /// </summary>
        private static void Test_AZeroFromTheCommandingCarIsRefused()
        {
            Assert(Math.Abs(RaceAnalyzer.ResolveLeaderPosAtZero(true, 0.0, 38.7) - 38.7) < 0.0001,
                   "uno zero va rifiutato");
            Assert(Math.Abs(RaceAnalyzer.ResolveLeaderPosAtZero(true, -3.0, 38.7) - 38.7) < 0.0001,
                   "e anche un negativo");
            Pass("Proiezione nulla o negativa: si ripiega comunque");
        }

        /// <summary>
        /// Fissa il significato della verita' di terreno, perche' non venga frainteso da chi legge
        /// dopo: <c>LeaderPosAtExpiry</c> e' **dove si trovava** il leader allo scadere, e il numero
        /// di giri che completera' e' il suo arrotondamento **per eccesso** — il giro in corso allo
        /// scadere va comunque finito prima della bandiera.
        ///
        /// Col dato vero del replay: `38.061` -> 39 giri. E se il leader fosse stato appena prima
        /// della linea invece che appena dopo — `38.999` — sarebbero stati comunque 39. E' la
        /// differenza di un soffio che vale un giro intero, cioe' il giro fantasma.
        /// </summary>
        private static void Test_TheGroundTruthRoundsToTheLapsActuallyCompleted()
        {
            Assert(Math.Abs(Math.Ceiling(GroundTruthAtExpiry) - 39.0) < 0.0001,
                   $"38.061 allo scadere significa 39 giri completati, ottenuto {Math.Ceiling(GroundTruthAtExpiry)}");
            Assert(Math.Abs(Math.Ceiling(38.999) - 39.0) < 0.0001,
                   "e anche 38.999 significa 39: un soffio prima della linea non cambia il conto");
            Assert(Math.Abs(Math.Ceiling(39.001) - 40.0) < 0.0001,
                   "ma un soffio dopo la linea ne vale 40: e' il giro fantasma, e vale un giro intero");

            Pass("Verita' di terreno: 38.061 -> 39 giri, e il salto sta a cavallo dell'intero");
        }
    }
}
