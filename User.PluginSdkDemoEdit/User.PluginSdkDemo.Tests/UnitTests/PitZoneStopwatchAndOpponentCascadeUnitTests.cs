using System;
using SimRIG;

namespace User.PluginSdkDemo.Tests
{
    public class PitZoneStopwatchAndOpponentCascadeUnitTests
    {
        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception("[PitZoneStopwatchAndOpponentCascade] " + message);
        }

        private static void Pass(string name)
        {
            Console.WriteLine("  [PASS] " + name);
        }

        public static void RunAllTests()
        {
            Console.WriteLine("[TEST] Running Pit Zone Stopwatch & Opponent Cascade Tests...");

            Test_SectorTracker_FastTransitSetsBestRawTime();
            Test_SectorTracker_SlowPitTransitDoesNotCorruptBestRawTime();
            Test_SectorTracker_LapsOnTyresZeroDoesNotRecordRawTime();
            Test_PitZoneWeight_CalculatesCorrectFraction();

            Console.WriteLine("[TEST SUCCESS] All Pit Zone Stopwatch & Opponent Cascade Tests Passed!");
        }

        private static void Test_SectorTracker_FastTransitSetsBestRawTime()
        {
            var tracker = new SectorTracker { Name = "PitZone" };

            // Start outside the sector
            tracker.Update(0.85, 995.0, false, false, 0.20, 50.0, 25.0, 25.0, 1, 4000.0, false, null);

            // Simulate entering the pit zone at lap 1 at racing speed (takes 14.0s)
            // Track pos enters at 0.90, exits at 0.10. sectorWeight = 0.20
            // Entry
            tracker.Update(0.90, 1000.0, false, true, 0.20, 50.0, 25.0, 25.0, 1, 4000.0, false, null);
            Assert(tracker.IsInside, "Tracker should be inside sector");

            // Exit at 1014.0 (14s elapsed)
            tracker.Update(0.10, 1014.0, false, false, 0.20, 50.0, 25.0, 25.0, 1, 4000.0, false, null);
            Assert(!tracker.IsInside, "Tracker should have exited sector");
            Assert(Math.Abs(tracker.BestRawTime - 14.0) < 0.001, $"BestRawTime expected 14.0s, got {tracker.BestRawTime}s");

            Pass("Test_SectorTracker_FastTransitSetsBestRawTime");
        }

        private static void Test_SectorTracker_SlowPitTransitDoesNotCorruptBestRawTime()
        {
            var tracker = new SectorTracker { Name = "PitZone" };

            // Start outside the sector
            tracker.Update(0.85, 995.0, false, false, 0.20, 50.0, 25.0, 25.0, 1, 4000.0, false, null);

            // First fast lap at 14.0s
            tracker.Update(0.90, 1000.0, false, true, 0.20, 50.0, 25.0, 25.0, 1, 4000.0, false, null);
            tracker.Update(0.10, 1014.0, false, false, 0.20, 50.0, 25.0, 25.0, 1, 4000.0, false, null);
            Assert(Math.Abs(tracker.BestRawTime - 14.0) < 0.001, "Precondition: fast lap set to 14.0s");

            // Next lap is a pit stop (takes 35.0s)
            tracker.Update(0.90, 1100.0, false, true, 0.20, 30.0, 25.0, 25.0, 2, 4000.0, false, null);
            tracker.Update(0.10, 1135.0, false, false, 0.20, 30.0, 25.0, 25.0, 2, 4000.0, false, null);

            // BestRawTime must remain 14.0s!
            Assert(Math.Abs(tracker.BestRawTime - 14.0) < 0.001, $"BestRawTime should remain 14.0s, but got {tracker.BestRawTime}s");

            Pass("Test_SectorTracker_SlowPitTransitDoesNotCorruptBestRawTime");
        }

        private static void Test_SectorTracker_LapsOnTyresZeroDoesNotRecordRawTime()
        {
            var tracker = new SectorTracker { Name = "PitZone" };

            // Outlap before race green flag / grid exit (lapsOnTyres = 0)
            tracker.Update(0.90, 1000.0, false, true, 0.20, 50.0, 25.0, 25.0, 0, 4000.0, false, null);
            tracker.Update(0.10, 1015.0, false, false, 0.20, 50.0, 25.0, 25.0, 0, 4000.0, false, null);

            Assert(tracker.BestRawTime == 0.0, $"Lap 0 transit should not record BestRawTime, got {tracker.BestRawTime}s");

            Pass("Test_SectorTracker_LapsOnTyresZeroDoesNotRecordRawTime");
        }

        private static void Test_PitZoneWeight_CalculatesCorrectFraction()
        {
            var tr = new TrackRecord
            {
                TrackID = "TestTrack",
                PitEntryPct = 0.90,
                PitExitPct = 0.10
            };

            // (1.0 - 0.90) + 0.10 = 0.20
            double weight = tr.GetPitZoneWeight();
            Assert(Math.Abs(weight - 0.20) < 0.001, $"PitZoneWeight expected 0.20, got {weight}");

            Pass("Test_PitZoneWeight_CalculatesCorrectFraction");
        }
    }
}
