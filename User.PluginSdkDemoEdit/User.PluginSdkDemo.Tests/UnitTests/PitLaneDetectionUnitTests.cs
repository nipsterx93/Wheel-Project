// -------------------------------------------------------------------------
// FILE: PitLaneDetectionUnitTests.cs
// Y-9: il rilevamento pit del Player usa la stessa cascata degli avversari.
// L'euristica precedente (TrackPositionPercent > 0.85 && 10 < SpeedKmh < 100)
// scattava su un tornante lento di fine giro: nessuna geofence, nessuna
// persistenza. Il caso del tornante e' il test che conta.
// -------------------------------------------------------------------------

using System;
using SimRIG;

namespace User.PluginSdkDemo.Tests
{
    public class PitLaneDetectionUnitTests
    {
        private const double NoLimitLearned = 0.0;

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception("[PitLaneDetection] " + message);
        }

        private static void Pass(string name)
        {
            Console.WriteLine("  [PASS] " + name);
        }

        public static void RunAllTests()
        {
            Console.WriteLine("[TEST] Running Pit Lane Detection Tests...");

            Test_Regression_SlowHairpinIsNotAPitStop();
            Test_TelemetryIsTrusted();
            Test_StoppedInsideZone();
            Test_SpeedPersistenceNeedsTime();
            Test_SpeedResetInterruptsPersistence();
            Test_ThresholdAdaptsToLearnedLimit();
            Test_ThresholdFallsBackWhenNothingLearned();
            Test_ResetClearsState();

            Console.WriteLine("[TEST SUCCESS] All Pit Lane Detection Tests Passed!");
        }

        /// <summary>
        /// Il difetto di Y-9. Un tornante lento nell'ultimo 15% del giro: velocità bassa a lungo,
        /// ma FUORI dalla zona box. L'euristica vecchia diceva "in pit"; la cascata deve dire no.
        /// </summary>
        private static void Test_Regression_SlowHairpinIsNotAPitStop()
        {
            var d = new PitLaneDetector();

            // 10 secondi a 45 km/h, fuori dalla geofence: e' un tornante, non un pit stop.
            double clock = 1000.0;
            for (int i = 0; i < 20; i++)
            {
                bool inPit = d.Update(false, false, 45.0, clock, NoLimitLearned);
                Assert(!inPit,
                       $"REGRESSIONE Y-9: un tratto lento FUORI dalla zona box non e' un pit " +
                       $"(campione {i}, 45 km/h)");
                clock -= 0.5;
            }

            Assert(d.LastTrigger == PitDetectionTrigger.None, "nessun criterio deve essere scattato");

            Pass("Test_Regression_SlowHairpinIsNotAPitStop");
        }

        private static void Test_TelemetryIsTrusted()
        {
            var d = new PitLaneDetector();

            // La telemetria vince subito, senza attendere persistenza e senza geofence.
            Assert(d.Update(true, false, 200.0, 1000.0, NoLimitLearned),
                   "quando la telemetria dichiara la pit lane le si crede");
            Assert(d.LastTrigger == PitDetectionTrigger.Telemetry,
                   $"trigger atteso Telemetry, ottenuto {d.LastTrigger}");

            Pass("Test_TelemetryIsTrusted");
        }

        private static void Test_StoppedInsideZone()
        {
            var d = new PitLaneDetector();

            Assert(d.Update(false, true, 0.0, 1000.0, NoLimitLearned),
                   "fermo dentro la zona box: e' un pit, senza aspettare");
            Assert(d.LastTrigger == PitDetectionTrigger.Stopped,
                   $"trigger atteso Stopped, ottenuto {d.LastTrigger}");

            // Fermo FUORI dalla zona box e' un testacoda, non un pit stop.
            var d2 = new PitLaneDetector();
            Assert(!d2.Update(false, false, 0.0, 1000.0, NoLimitLearned),
                   "fermo fuori dalla zona box non e' un pit: puo' essere un incidente");

            Pass("Test_StoppedInsideZone");
        }

        /// <summary>
        /// La persistenza e' cio' che l'euristica vecchia non aveva: un singolo campione lento
        /// non basta, altrimenti una staccata dentro la zona box farebbe scattare tutto.
        /// </summary>
        private static void Test_SpeedPersistenceNeedsTime()
        {
            var d = new PitLaneDetector();

            Assert(!d.Update(false, true, 50.0, 1000.0, NoLimitLearned),
                   "il primo campione lento non basta");
            Assert(!d.Update(false, true, 50.0, 998.5, NoLimitLearned),
                   "dopo 1.5 s ancora non basta");
            Assert(!d.Update(false, true, 50.0, 997.5, NoLimitLearned),
                   "dopo 2.5 s ancora no");
            Assert(d.Update(false, true, 50.0, 997.0, NoLimitLearned),
                   "a 3 s esatti la persistenza e' raggiunta");
            Assert(d.LastTrigger == PitDetectionTrigger.SpeedPersistence,
                   $"trigger atteso SpeedPersistence, ottenuto {d.LastTrigger}");

            Pass("Test_SpeedPersistenceNeedsTime");
        }

        private static void Test_SpeedResetInterruptsPersistence()
        {
            var d = new PitLaneDetector();

            d.Update(false, true, 50.0, 1000.0, NoLimitLearned);
            d.Update(false, true, 50.0, 998.0, NoLimitLearned);   // 2 s accumulati

            // Riaccelera: il contatore deve ripartire da zero.
            Assert(!d.Update(false, true, 150.0, 997.5, NoLimitLearned),
                   "a 150 km/h non si e' in pit");
            Assert(d.LowSpeedElapsed(997.5) == 0.0, "il contatore di persistenza deve azzerarsi");

            Assert(!d.Update(false, true, 50.0, 997.0, NoLimitLearned),
                   "riparte da zero: 0.5 s non bastano");
            Assert(!d.Update(false, true, 50.0, 995.0, NoLimitLearned),
                   "2 s dal nuovo inizio non bastano ancora");
            Assert(d.Update(false, true, 50.0, 994.0, NoLimitLearned),
                   "3 s dal nuovo inizio: ora si");

            Pass("Test_SpeedResetInterruptsPersistence");
        }

        /// <summary>
        /// Il punto dell'apprendimento: su un circuito con limite 80 km/h, una vettura a 85
        /// e' plausibilmente in corsia box. Con la soglia cablata a 80 non lo sarebbe mai stata.
        /// </summary>
        private static void Test_ThresholdAdaptsToLearnedLimit()
        {
            Assert(PitLaneDetector.SpeedThresholdFor(60.0) == 80.0,
                   "limite 60 -> soglia 80, cioe' esattamente il valore storico cablato");
            Assert(PitLaneDetector.SpeedThresholdFor(80.0) == 100.0,
                   "limite 80 -> soglia 100");
            Assert(PitLaneDetector.SpeedThresholdFor(50.0) == 70.0,
                   "limite 50 -> soglia 70");

            // Con limite 80 appreso, 85 km/h persistenti dentro la zona sono un pit.
            var d = new PitLaneDetector();
            d.Update(false, true, 85.0, 1000.0, 80.0);
            d.Update(false, true, 85.0, 998.0, 80.0);
            Assert(d.Update(false, true, 85.0, 997.0, 80.0),
                   "con limite 80 appreso, 85 km/h persistenti devono contare come pit");

            // Lo stesso campione con la vecchia soglia fissa non sarebbe mai scattato.
            var d2 = new PitLaneDetector();
            d2.Update(false, true, 85.0, 1000.0, NoLimitLearned);
            d2.Update(false, true, 85.0, 998.0, NoLimitLearned);
            Assert(!d2.Update(false, true, 85.0, 997.0, NoLimitLearned),
                   "senza limite appreso 85 km/h resta sopra la soglia di fallback");

            Pass("Test_ThresholdAdaptsToLearnedLimit");
        }

        private static void Test_ThresholdFallsBackWhenNothingLearned()
        {
            Assert(PitLaneDetector.SpeedThresholdFor(0.0) == PitLaneDetector.DefaultSpeedThresholdKmh,
                   "senza nulla di appreso si usa il fallback storico");
            Assert(PitLaneDetector.SpeedThresholdFor(-5.0) == PitLaneDetector.DefaultSpeedThresholdKmh,
                   "un valore assurdo non deve produrre una soglia assurda");
            Assert(PitLaneDetector.DefaultSpeedThresholdKmh == 80.0,
                   "il fallback deve restare il valore storico, per non cambiare comportamento");

            Pass("Test_ThresholdFallsBackWhenNothingLearned");
        }

        private static void Test_ResetClearsState()
        {
            var d = new PitLaneDetector();
            d.Update(true, true, 0.0, 1000.0, NoLimitLearned);
            Assert(d.IsInPitLane, "precondizione: rilevato in pit");

            d.Reset();
            Assert(!d.IsInPitLane, "Reset deve azzerare lo stato");
            Assert(d.LastTrigger == PitDetectionTrigger.None, "Reset deve azzerare il trigger");
            Assert(d.LowSpeedElapsed(1000.0) == 0.0, "Reset deve azzerare la persistenza");

            Pass("Test_ResetClearsState");
        }
    }
}
