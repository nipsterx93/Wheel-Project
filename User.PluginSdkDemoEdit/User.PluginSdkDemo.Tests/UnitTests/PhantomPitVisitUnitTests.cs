// -------------------------------------------------------------------------
// FILE: PhantomPitVisitUnitTests.cs
// Y-23: una permanenza in corsia box che non ha percorso strada non e' una
// sosta. Il caso di regressione viene dallo sfarfallio di IsInPitLane
// osservato nel replay Daytona del 2026-08-23 (Logs/Daytona Run, riga 4546).
// -------------------------------------------------------------------------

using System;
using SimRIG;

namespace User.PluginSdkDemo.Tests
{
    public class PhantomPitVisitUnitTests
    {
        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception("[PhantomPitVisit] " + message);
        }

        private static void Pass(string name)
        {
            Console.WriteLine("  [PASS] " + name);
        }

        public static void RunAllTests()
        {
            Console.WriteLine("[TEST] Running Phantom Pit Visit Tests...");

            Test_Regression_DaytonaFlickerIsDiscarded();
            Test_RealPitLaneTraversalIsAccepted();
            Test_TraversalAcrossTheStartLineCountsCorrectly();
            Test_UnknownEntryPositionDisablesTheGuard();
            Test_ThresholdBoundary();

            Console.WriteLine("[TEST SUCCESS] All Phantom Pit Visit Tests Passed!");
        }

        private static void Test_Regression_DaytonaFlickerIsDiscarded()
        {
            // Caso reale, replay Daytona 2026-08-23 giro 16. In due decimi di secondo:
            //   22:17:43.975  Entry  sample=0.959   IsInPitLane -> true
            //   22:17:44.109  Exit   sample=0.963   IsInPitLane -> false, "Pit Complete 0.7s"
            //   22:17:44.178  Entry  sample=0.965   IsInPitLane -> true
            // Il Player era sul rettilineo principale, non in corsia box. Quel finto transito ha
            // scritto PitDriveThroughTime = 0.666 nel database, e da li' ha spento per sempre la
            // richiesta vocale di calibrazione (IsDriveThroughTimeMissing verifica == 0.0).
            Assert(!PitRadar.HasTraversedPitLane(0.959, 0.963),
                   "lo sfarfallio da 0.004 di giro non e' una traversata");

            // E nemmeno il secondo lobo dello stesso sfarfallio.
            Assert(!PitRadar.HasTraversedPitLane(0.963, 0.965),
                   "nemmeno il secondo lobo");
            Pass("Regressione Daytona: lo sfarfallio di IsInPitLane non conta come sosta");
        }

        private static void Test_RealPitLaneTraversalIsAccepted()
        {
            // Le geofence vere dal database, dopo il consenso.
            // Daytona: entry 0.9588 -> exit 0.0987, attraversando il traguardo.
            Assert(PitRadar.HasTraversedPitLane(0.9588, 0.0987),
                   "il transito reale di Daytona deve passare");

            // Misano: entry 0.9495 -> exit 0.0737.
            Assert(PitRadar.HasTraversedPitLane(0.9495, 0.0737),
                   "il transito reale di Misano deve passare");
            Pass("I transiti reali di Misano e Daytona vengono accettati");
        }

        private static void Test_TraversalAcrossTheStartLineCountsCorrectly()
        {
            // Il wrap del traguardo non deve far sembrare enorme un percorso minimo, ne' viceversa.
            // Da 0.999 a 0.001 sono 0.002 di giro, non 0.998.
            Assert(!PitRadar.HasTraversedPitLane(0.999, 0.001),
                   "attraversare il traguardo di un soffio non e' una traversata");

            // Da 0.99 a 0.05 sono 0.06 di giro: sopra soglia.
            Assert(PitRadar.HasTraversedPitLane(0.99, 0.05),
                   "0.06 di giro attraverso il traguardo e' una traversata");
            Pass("Il wrap del traguardo viene contato correttamente");
        }

        private static void Test_UnknownEntryPositionDisablesTheGuard()
        {
            // Senza posizione d'ingresso non si puo' giudicare: si accetta, invece di scartare una
            // sosta vera per mancanza di dati.
            Assert(PitRadar.HasTraversedPitLane(-1.0, 0.5),
                   "posizione d'ingresso non nota disattiva il guard");
            Pass("Posizione d'ingresso non nota: nessun giudizio, si accetta");
        }

        private static void Test_ThresholdBoundary()
        {
            // Esattamente sulla soglia si accetta: il guard esclude cio' che e' *sotto*.
            Assert(PitRadar.HasTraversedPitLane(0.50, 0.50 + PitRadar.MinimumPitTraversalPct),
                   "esattamente sulla soglia si accetta");
            Assert(!PitRadar.HasTraversedPitLane(0.50, 0.50 + PitRadar.MinimumPitTraversalPct / 2.0),
                   "sotto la soglia si scarta");

            // La direzione non conta: un'uscita "all'indietro" e' comunque uno spostamento.
            Assert(PitRadar.HasTraversedPitLane(0.50, 0.50 - 0.05),
                   "il verso dello spostamento non cambia il giudizio");
            Pass("Il comportamento sulla soglia e' quello atteso");
        }
    }
}
