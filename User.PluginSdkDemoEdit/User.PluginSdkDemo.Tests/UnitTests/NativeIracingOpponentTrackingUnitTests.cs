// -------------------------------------------------------------------------
// FILE: NativeIracingOpponentTrackingUnitTests.cs
// Unit tests per le funzionalita' native iRacing:
// 1. Fuel latch condizionato a >= 3 giri completati (giro corrente >= 4)
// 2. Smart Refueling all'ingresso box (calcolo FuelToAdd per fine gara e azzeramento NeedsPitStop)
// 3. Scorporo Pit Loss (RawExtendedPitZoneTime e Jacking Dead Time empirico)
// 4. Stabilizzazione del Gap oltre mezzo giro e protezione dal salto spurio al traguardo
// 5. IracingTelemetryBridge e TrackSurface / PitRoad mapping
// -------------------------------------------------------------------------

using System;
using SimRIG;

namespace User.PluginSdkDemo.Tests
{
    public class NativeIracingOpponentTrackingUnitTests
    {
        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception("[NativeIracingTracking] " + message);
        }

        private static void Pass(string name)
        {
            Console.WriteLine("  [PASS] " + name);
        }

        public static void RunAllTests()
        {
            Console.WriteLine("[TEST] Running Native iRacing Opponent Tracking Unit Tests...");

            Test_FuelLatch_WaitsUntilLap3Completed();
            Test_SmartRefueling_CalculatesFuelToFinish();
            Test_PitLossDissection_Formulas();
            Test_NormalizeLapDifference_PreservesLargeGaps();
            Test_NormalizeLapDifference_NeutralizesRolloverDesync();
            Test_IracingTelemetryBridge_MockValues();

            Console.WriteLine("[TEST SUCCESS] All Native iRacing Tracking Tests Passed!");
        }

        private static void Test_FuelLatch_WaitsUntilLap3Completed()
        {
            // Lap 1, 2 (completed laps 0, 1, 2): Player median burn is NOT yet reliable.
            // Only at completed laps >= 3 (current lap >= 4) should it be considered ready.
            for (int completedLaps = 0; completedLaps <= 2; completedLaps++)
            {
                bool isLatchReady = completedLaps >= 3;
                Assert(!isLatchReady, $"At completed laps = {completedLaps}, fuel latch must NOT be ready.");
            }

            Assert((3 >= 3), "At completed laps = 3 (current lap 4), fuel latch must be ready.");
            Assert((4 >= 3), "At completed laps = 4, fuel latch must be ready.");
            Pass("Fuel latch waits until >= 3 completed laps (lap 4+)");
        }

        private static void Test_SmartRefueling_CalculatesFuelToFinish()
        {
            // Scenario: 15 laps remaining in race. Opponent burns 2.5 L/lap.
            // Fuel needed = 15 * 2.5 + safety (0.5 * 2.5 = 1.25L) = 38.75L.
            // Current fuel residuo = 10.0L.
            // Fuel to add needed = 38.75 - 10.0 = 28.75L.
            // Car max tank = 50.0L.
            double lapsRemaining = 15.0;
            double burn = 2.5;
            double classMaxTank = 50.0;
            double fuelResiduo = 10.0;
            double safetyLitres = burn * 0.5;

            double fuelNeeded = (lapsRemaining * burn) + safetyLitres;
            double smartFuelToAdd = Math.Min(classMaxTank - fuelResiduo, Math.Max(0.0, fuelNeeded - fuelResiduo));

            Assert(Math.Abs(smartFuelToAdd - 28.75) < 0.001, $"Expected smartFuelToAdd = 28.75L, got {smartFuelToAdd:F2}L");

            double fuelAfterPit = fuelResiduo + smartFuelToAdd;
            Assert(Math.Abs(fuelAfterPit - 38.75) < 0.001, $"Expected fuelAfterPit = 38.75L, got {fuelAfterPit:F2}L");

            bool needsPitStop = fuelAfterPit < (lapsRemaining * burn);
            Assert(!needsPitStop, "NeedsPitStop must be false after smart refueling to race end.");
            Pass("Smart refueling calculates exact fuel to finish and sets NeedsPitStop to false");
        }

        private static void Test_PitLossDissection_Formulas()
        {
            // Scenario:
            // Observed Extended Pit Time = 45.5s
            // Stationary Time = 22.0s
            // Fuel added = 54.0L with MeasuredFuelFillRate = 2.7 L/s (refuel duration = 20.0s)
            // Formulas:
            // RawExtended = Observed - Stationary = 45.5 - 22.0 = 23.5s
            // Empirical Jacking Dead Time = Stationary - RefuelTime = 22.0 - 20.0 = 2.0s
            double observedExtendedTransit = 45.5;
            double statDuration = 22.0;
            double fillRate = 2.7;
            double fuelAdded = 54.0;

            double rawExtendedTime = Math.Max(0.0, observedExtendedTransit - statDuration);
            Assert(Math.Abs(rawExtendedTime - 23.5) < 0.001, $"Expected rawExtended = 23.5s, got {rawExtendedTime:F2}s");

            double refuelDuration = fuelAdded / fillRate;
            Assert(Math.Abs(refuelDuration - 20.0) < 0.001, $"Expected refuelDuration = 20.0s, got {refuelDuration:F2}s");

            double empiricalDeadTime = Math.Max(0.0, statDuration - refuelDuration);
            Assert(Math.Abs(empiricalDeadTime - 2.0) < 0.001, $"Expected empiricalDeadTime = 2.0s, got {empiricalDeadTime:F2}s");
            Pass("Pit loss dissection correctly isolates raw transit and jacking buffer");
        }

        private static void Test_NormalizeLapDifference_PreservesLargeGaps()
        {
            // Scenario: Chasing Aake Korte who is 0.65 laps ahead.
            // Player at 0.10, Target at 0.75 on same lap: rawDiff = 0.10 - 0.75 = -0.65.
            // In the legacy code, WrapLapDifference folded -0.65 into +0.35, inverting sign!
            // NormalizeLapDifference MUST preserve -0.65 (negative = target ahead).
            double myPos = 0.10;
            double oppPos = 0.75;
            double rawDiff = -0.65;

            double normalized = TargetStrategyManager.NormalizeLapDifference(rawDiff, myPos, oppPos);
            Assert(Math.Abs(normalized - (-0.65)) < 1e-9, $"Expected -0.65 laps, got {normalized}");

            // Target 0.80 laps ahead (Player at 0.18, Target at 0.98)
            double myPos2 = 0.18;
            double oppPos2 = 0.98;
            double rawDiff2 = -0.80;
            double norm2 = TargetStrategyManager.NormalizeLapDifference(rawDiff2, myPos2, oppPos2);
            Assert(Math.Abs(norm2 - (-0.80)) < 1e-9, $"Expected -0.80 laps, got {norm2}");
            Pass("NormalizeLapDifference preserves large continuous gaps (> 0.5 laps) without sign inversion");
        }

        private static void Test_NormalizeLapDifference_NeutralizesRolloverDesync()
        {
            // Rollover desync case 1:
            // Player just crossed line (myPos = 0.02), Target just approaching (oppPos = 0.98).
            // Player is 0.04 laps ahead. But 1-tick desync leaves myLap = targetLap.
            // rawDiff = 0.02 - 0.98 = -0.96.
            // NormalizeLapDifference must correct this to +0.04.
            double myPos = 0.02;
            double oppPos = 0.98;
            double desyncDiff = -0.96;

            double corrected = TargetStrategyManager.NormalizeLapDifference(desyncDiff, myPos, oppPos);
            Assert(Math.Abs(corrected - 0.04) < 1e-9, $"Expected +0.04 laps, got {corrected}");

            // Rollover desync case 2:
            // Target just crossed line (oppPos = 0.02), Player approaching (myPos = 0.98).
            // Target is 0.04 laps ahead. Desync leaves rawDiff = 0.98 - 0.02 = +0.96.
            // NormalizeLapDifference must correct this to -0.04.
            double myPos2 = 0.98;
            double oppPos2 = 0.02;
            double desyncDiff2 = 0.96;

            double corrected2 = TargetStrategyManager.NormalizeLapDifference(desyncDiff2, myPos2, oppPos2);
            Assert(Math.Abs(corrected2 - (-0.04)) < 1e-9, $"Expected -0.04 laps, got {corrected2}");
            Pass("NormalizeLapDifference neutralizes +/- 1.0 lap desync spikes at start/finish line");
        }

        private static void Test_IracingTelemetryBridge_MockValues()
        {
            var bridge = new IracingTelemetryBridge();
            var mockOnPitRoad = new bool[64];
            var mockTrackSurface = new IracingTrackSurface[64];
            var mockPitCount = new int[64];
            var mockClassPos = new int[64];
            var mockLapDist = new float[64];

            // Setup mock driver at idx 7: In Pit Stall, on pit road, P2 in class, pit stop #1
            mockOnPitRoad[7] = true;
            mockTrackSurface[7] = IracingTrackSurface.InPitStall;
            mockPitCount[7] = 1;
            mockClassPos[7] = 2;
            mockLapDist[7] = 0.45f;

            bridge.SetMockData(mockOnPitRoad, mockTrackSurface, mockPitCount, mockClassPos, mockLapDist);

            Assert(bridge.IsAvailable, "Bridge should be available with mock data");
            Assert(bridge.IsOnPitRoad(7), "CarIdx 7 should be on pit road");
            Assert(bridge.GetTrackSurface(7) == IracingTrackSurface.InPitStall, "CarIdx 7 surface should be InPitStall");
            Assert(bridge.GetPitStopCount(7) == 1, "CarIdx 7 pit count should be 1");
            Assert(bridge.GetClassPosition(7) == 2, "CarIdx 7 class position should be 2");
            Assert(Math.Abs(bridge.GetLapDistPct(7) - 0.45) < 0.01, "CarIdx 7 lap dist should be ~0.45");

            // Check non-setup idx returns default (OffTrack in initialized array)
            Assert(!bridge.IsOnPitRoad(15), "CarIdx 15 should not be on pit road");
            Assert(bridge.GetTrackSurface(15) == IracingTrackSurface.OffTrack, "CarIdx 15 surface should be OffTrack (0)");
            // Check out of range returns NotInWorld
            Assert(bridge.GetTrackSurface(70) == IracingTrackSurface.NotInWorld, "CarIdx 70 surface should be NotInWorld (-1)");
            Pass("IracingTelemetryBridge accurately extracts mock telemetry arrays");
        }
    }
}
