// -------------------------------------------------------------------------
// FILE: OpponentLapTimeSourceUnitTests.cs
// Y-32: il tempo sul giro degli avversari veniva cronometrato per
// campionamento invece che letto dal gioco.
//
// Caso di regressione dal replay Road Atlanta del 2026-08-30
// (Logs/Road Atlanta/SimRIG_DebugLog_20260830_102220.csv).
//
// Il replay girava a 3x, quindi fra due letture scorrevano 3.0 secondi di gara
// (min 2.8, max 3.4 - misurati su 888 intervalli). Il passaggio sul traguardo
// si vede al primo tick dopo che e' avvenuto, quindi la misura sbaglia fino a
// un tick per estremo. Sul leader Kalyann Mey4: 62.95 s misurati contro 69.4 s
// reali, cioe' circa due tick.
//
// Da li' in cascata: baseline normalizzata a 60.550, finestra di validita'
// [59.34, 62.67] s, e ogni giro vero rifiutato come anomalo per il resto della
// gara. Passo del leader fermo a ~61 s, giri totali del leader 42-45 invece
// dei 38.8 reali.
// -------------------------------------------------------------------------

using System;
using SimRIG;

namespace User.PluginSdkDemo.Tests
{
    public class OpponentLapTimeSourceUnitTests
    {
        // Numeri veri del replay Road Atlanta 2026-08-30.
        private const double RoadAtlantaMeters = 4088.0;
        private const double LeaderRealLapSec = 69.40;     // misurato da passaggi consecutivi
        private const double LeaderSelfTimedSec = 62.95;   // quanto ne misurava il cronometro interno

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception("[OpponentLapTimeSource] " + message);
        }

        private static void Pass(string name)
        {
            Console.WriteLine("  [PASS] " + name);
        }

        public static void RunAllTests()
        {
            Console.WriteLine("[TEST] Running Opponent Lap Time Source Tests...");

            Test_Regression_GameTimeWinsOverTheSampledOne();
            Test_Regression_TheSampledErrorLockedTheValidityWindow();
            Test_SelfTimedIsUsedOnlyWhenTheGameGivesNothing();
            Test_NeitherSourceCredibleReturnsZero();
            Test_PhysicallyImpossibleGameValueIsRefused();
            Test_PitLapIsNotMistakenForAFlyingLap();

            Console.WriteLine("[TEST SUCCESS] All Opponent Lap Time Source Tests Passed!");
        }

        /// <summary>
        /// Il caso vero: il gioco dice 69.40, il nostro cronometro 62.95. Vince il gioco.
        ///
        /// Neutralizzando la correzione (tornando a preferire il valore cronometrato) questo test
        /// diventa rosso con 62.95, il numero che ha avvelenato la baseline nel replay.
        /// </summary>
        private static void Test_Regression_GameTimeWinsOverTheSampledOne()
        {
            double resolved = OpponentTracker.ResolveOpponentLapTime(
                LeaderRealLapSec, LeaderSelfTimedSec, RoadAtlantaMeters);

            Assert(Math.Abs(resolved - LeaderRealLapSec) < 0.0001,
                   $"deve vincere il tempo del gioco (69.40), ottenuto {resolved:F2}");
            Pass("Regressione: col tempo del gioco disponibile il cronometro interno non si usa");
        }

        /// <summary>
        /// Perche' quei 6.45 secondi non erano un dettaglio: dimostra che la baseline sbagliata
        /// costruisce una finestra di validita' che esclude la realta' per sempre.
        ///
        /// La finestra e' [-2%, +3.5%] attorno alla baseline. Con la baseline avvelenata i giri veri
        /// restano fuori; con quella corretta ci stanno dentro.
        /// </summary>
        private static void Test_Regression_TheSampledErrorLockedTheValidityWindow()
        {
            const double fuelPenalty = 2.40;   // 80 L * 0.03 s/L, dal log
            double realNormalized = LeaderRealLapSec - fuelPenalty;          // ~67.0
            double poisonedBaseline = LeaderSelfTimedSec - fuelPenalty;      // ~60.55, il valore del log

            double poisonedUpperBound = poisonedBaseline * 1.035;
            Assert(realNormalized > poisonedUpperBound,
                   $"con baseline {poisonedBaseline:F2} il giro vero {realNormalized:F2} deve cadere fuori " +
                   $"dal tetto {poisonedUpperBound:F2} — e' il blocco osservato");

            double healthyUpperBound = realNormalized * 1.035;
            double healthyLowerBound = realNormalized * 0.98;
            Assert(realNormalized <= healthyUpperBound && realNormalized >= healthyLowerBound,
                   "con la baseline corretta il giro vero deve rientrare nella finestra");

            Pass($"Regressione: baseline {poisonedBaseline:F2} esclude i giri veri ({realNormalized:F2} > {poisonedUpperBound:F2})");
        }

        /// <summary>
        /// Il ripiego resta: se il gioco non espone nulla per quella vettura, meglio una misura
        /// grossolana che nessuna misura.
        /// </summary>
        private static void Test_SelfTimedIsUsedOnlyWhenTheGameGivesNothing()
        {
            double resolved = OpponentTracker.ResolveOpponentLapTime(0.0, LeaderSelfTimedSec, RoadAtlantaMeters);
            Assert(Math.Abs(resolved - LeaderSelfTimedSec) < 0.0001,
                   $"senza tempo dal gioco si usa il cronometro interno, ottenuto {resolved:F2}");
            Pass("Il cronometro interno resta come ripiego");
        }

        /// <summary>Nessuna fonte credibile: si restituisce zero e il giro non viene contato.</summary>
        private static void Test_NeitherSourceCredibleReturnsZero()
        {
            double resolved = OpponentTracker.ResolveOpponentLapTime(0.0, 0.0, RoadAtlantaMeters);
            Assert(resolved == 0.0, $"senza fonti utilizzabili si restituisce 0, ottenuto {resolved:F2}");

            // Contatore saltato di due giri: 8 secondi non sono un giro di Road Atlanta.
            double jumped = OpponentTracker.ResolveOpponentLapTime(8.0, 0.0, RoadAtlantaMeters);
            Assert(jumped == 0.0, $"8 s non e' un giro, ottenuto {jumped:F2}");
            Pass("Senza fonti credibili non si inventa un giro");
        }

        /// <summary>
        /// Il limite fisico del tracciato vale anche sul dato del gioco: 30 s a Road Atlanta
        /// sarebbero 490 km/h di media. Si ripiega sul cronometro interno.
        /// </summary>
        private static void Test_PhysicallyImpossibleGameValueIsRefused()
        {
            double resolved = OpponentTracker.ResolveOpponentLapTime(30.0, LeaderRealLapSec, RoadAtlantaMeters);
            Assert(Math.Abs(resolved - LeaderRealLapSec) < 0.0001,
                   $"un tempo fisicamente impossibile va rifiutato anche se viene dal gioco, ottenuto {resolved:F2}");
            Pass("Il limite fisico del tracciato si applica anche alla fonte del gioco");
        }

        /// <summary>Un "giro" con dentro una sosta non e' un giro di riferimento.</summary>
        private static void Test_PitLapIsNotMistakenForAFlyingLap()
        {
            double resolved = OpponentTracker.ResolveOpponentLapTime(720.0, 0.0, RoadAtlantaMeters);
            Assert(resolved == 0.0, $"720 s contengono una sosta, non un giro, ottenuto {resolved:F2}");
            Pass("Un giro con una sosta dentro non entra nel passo");
        }
    }
}
