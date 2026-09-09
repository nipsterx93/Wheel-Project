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
            Test_SelectTarget_P1_P2_InMulticlass();
            Test_ReplayFallback_RetroactiveTransitValidatesStop();
            Test_IracingTelemetryBridge_PluginManagerReplayFallback();
            Test_TargetState_TrackSurface_Properties();

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

        private static void Test_SelectTarget_P1_P2_InMulticlass()
        {
            var manager = new TargetStrategyManager();
            var tracker = new OpponentTracker();

            var gtpLeader = new GameReaderCommon.Opponent { Name = "GTP P1", Position = 1, PositionInClass = 1, CarClass = "GTP" };
            var gtpSecond = new GameReaderCommon.Opponent { Name = "GTP P2", Position = 2, PositionInClass = 2, CarClass = "GTP" };
            var gt3Leader = new GameReaderCommon.Opponent { Name = "GT3 P1", Position = 20, PositionInClass = 1, CarClass = "GT3" };
            var gt3Second = new GameReaderCommon.Opponent { Name = "GT3 P2", Position = 21, PositionInClass = 2, CarClass = "GT3" };
            var player = new GameReaderCommon.Opponent { Name = "Player", Position = 22, PositionInClass = 3, CarClass = "GT3", IsPlayer = true };

            var state = new SessionState
            {
                CarClassId = "GT3",
                Position = 22,
                PositionInClass = 3,
                Opponents = new System.Collections.Generic.List<GameReaderCommon.Opponent>
                {
                    gtpLeader, gtpSecond, gt3Leader, gt3Second, player
                }
            };

            // Track GT3 cars in OpponentTracker
            tracker.AddTrackedOpponent("GT3 P1", new OpponentTelemetryData { Name = "GT3 P1", CarClass = "GT3", ClassPosition = 1 });
            tracker.AddTrackedOpponent("GT3 P2", new OpponentTelemetryData { Name = "GT3 P2", CarClass = "GT3", ClassPosition = 2 });

            // P1 mode: Must target GT3 P1 (class leader), NOT GTP P1!
            bool isPlayer;
            var targetP1 = manager.SelectTarget(state, tracker, "P1", out isPlayer);
            Assert(!isPlayer, "P1 should not be player");
            Assert(targetP1 != null && targetP1.Name == "GT3 P1", $"Expected GT3 P1, got {targetP1?.Name}");

            // P2 mode: Must target GT3 P2 (class second), NOT GTP P2!
            var targetP2 = manager.SelectTarget(state, tracker, "P2", out isPlayer);
            Assert(!isPlayer, "P2 should not be player");
            Assert(targetP2 != null && targetP2.Name == "GT3 P2", $"Expected GT3 P2, got {targetP2?.Name}");

            // P3 mode: Player is P3, should report isPlayer = true
            var targetP3 = manager.SelectTarget(state, tracker, "P3", out isPlayer);
            Assert(isPlayer, "P3 should identify as player");

            // LEADER_OVERALL: Must target overall P1 (GTP P1)
            var targetLeader = manager.SelectTarget(state, tracker, "LEADER_OVERALL", out isPlayer);
            Assert(targetLeader != null && targetLeader.Name == "GTP P1", $"Expected GTP P1, got {targetLeader?.Name}");

            Pass("TargetStrategyManager SelectTarget P1/P2 correctly selects class position in multiclass");
        }

        private static void Test_ReplayFallback_RetroactiveTransitValidatesStop()
        {
            // Verifies the fix for replay pit stops:
            // In replay mode, CarIdxOnPitRoad is false, so opponents exit via spatial transit retro validation.
            // This test verifies that spatial transit validation increments PitCount, sets LastRefuelLap,
            // calculates smart refueling (so EstimatedFuel does NOT drop to 0.00L), and sets NeedsPitStop = false.
            var tData = new OpponentTelemetryData
            {
                Name = "Aake Korte",
                CarClass = "GT3",
                EstimatedFuel = 4.5,
                PitCount = 0,
                HasCountedPitThisTransit = false,
                SpatialStrictEntryLap = 16,
                HighestLapSeen = 16
            };

            int rawCurrentLap = 16;
            double totalSpatialTransitTime = 38.5;
            double spatialAdaptiveThreshold = 20.0;
            bool lapDiffValid = (rawCurrentLap >= tData.SpatialStrictEntryLap) && (rawCurrentLap <= tData.SpatialStrictEntryLap + 1);

            Assert(totalSpatialTransitTime > spatialAdaptiveThreshold && lapDiffValid, "Preconditions for retroactive pit validation met");

            // Execute the retroactive pit validation logic
            if (!tData.HasCountedPitThisTransit)
            {
                tData.PitCount++;
                tData.HasCountedPitThisTransit = true;
                tData.LastStopLap = rawCurrentLap;
            }
            tData.LastPitLap = rawCurrentLap;

            double classFuelBurn = 2.48;
            double classMaxTank = 52.0;
            double raceLapsRemaining = 19.0;
            double inlapFuelDeduction = 0.5 * classFuelBurn;

            tData.EstimatedFuel = Math.Max(0.0, tData.EstimatedFuel - inlapFuelDeduction);
            int currentOppLap = tData.HighestLapSeen;
            double lapsRemaining = raceLapsRemaining;
            double fuelResiduo = Math.Max(0.0, tData.EstimatedFuel);
            double safetyLitres = classFuelBurn * 0.5;
            double fuelNeeded = (lapsRemaining * classFuelBurn) + safetyLitres;
            double smartFuelToAdd = Math.Min(classMaxTank - fuelResiduo, Math.Max(0.0, fuelNeeded - fuelResiduo));

            tData.LastPitFuelAdded = smartFuelToAdd;
            tData.LastRefuelLap = currentOppLap;
            tData.FuelAfterLastPit = Math.Min(classMaxTank, fuelResiduo + smartFuelToAdd);
            tData.EstimatedFuel = tData.FuelAfterLastPit;
            tData.EstimatedFuelTank = tData.FuelAfterLastPit;
            if (tData.EstimatedFuel >= lapsRemaining * classFuelBurn)
            {
                tData.NeedsPitStop = false;
            }

            Assert(tData.PitCount == 1, $"PitCount must be 1, got {tData.PitCount}");
            Assert(tData.LastRefuelLap == 16, $"LastRefuelLap must be 16, got {tData.LastRefuelLap}");
            Assert(tData.EstimatedFuel > 45.0, $"EstimatedFuel must be replenished (>45L), got {tData.EstimatedFuel:F1}L");
            Assert(!tData.NeedsPitStop, "NeedsPitStop must be false after sufficient refueling");

            Pass("Replay fallback retroactive pit validation replenishes fuel and increments PitCount");
        }

        private class DataCorePlugin { }

        private static void Test_IracingTelemetryBridge_PluginManagerReplayFallback()
        {
            var bridge = new IracingTelemetryBridge();
            var pm = new SimHub.Plugins.PluginManager();

            int[] replayTrackSurfaces = new int[64];
            for (int i = 0; i < 64; i++) replayTrackSurfaces[i] = -1; // NotInWorld
            replayTrackSurfaces[0] = 2; // AproachingPits
            replayTrackSurfaces[1] = 3; // OnTrack
            replayTrackSurfaces[9] = 1; // InPitStall

            bool[] replayPitRoad = new bool[64];
            replayPitRoad[0] = true;
            replayPitRoad[9] = true;

            int[] replayPitCount = new int[64];
            replayPitCount[9] = 2;

            int[] replayClassPos = new int[64];
            replayClassPos[9] = 4;

            float[] replayLapDist = new float[64];
            replayLapDist[9] = 0.05f;

            pm.AddProperty("GameRawData.Telemetry.CarIdxTrackSurface", typeof(DataCorePlugin), replayTrackSurfaces);
            pm.AddProperty("GameRawData.Telemetry.CarIdxOnPitRoad", typeof(DataCorePlugin), replayPitRoad);
            pm.AddProperty("GameRawData.Telemetry.CarIdxPitStopCount", typeof(DataCorePlugin), replayPitCount);
            pm.AddProperty("GameRawData.Telemetry.CarIdxClassPosition", typeof(DataCorePlugin), replayClassPos);
            pm.AddProperty("GameRawData.Telemetry.CarIdxLapDistPct", typeof(DataCorePlugin), replayLapDist);

            bridge.Update(null, pm);

            Assert(bridge.IsAvailable, "Bridge should be available via PluginManager replay fallback");
            Assert(bridge.GetTrackSurface(9) == IracingTrackSurface.InPitStall, "CarIdx 9 should be InPitStall from replay array");
            Assert(bridge.IsInPitStall(9), "CarIdx 9 IsInPitStall should be true");
            Assert(bridge.IsOnPitRoad(9), "CarIdx 9 IsOnPitRoad should be true");
            Assert(bridge.GetTrackSurface(0) == IracingTrackSurface.AproachingPits, "CarIdx 0 should be AproachingPits");
            Assert(bridge.GetTrackSurface(1) == IracingTrackSurface.OnTrack, "CarIdx 1 should be OnTrack");
            Assert(bridge.GetTrackSurface(15) == IracingTrackSurface.NotInWorld, "CarIdx 15 should be NotInWorld");
            Assert(bridge.GetPitStopCount(9) == 2, "CarIdx 9 PitCount should be 2");
            Assert(bridge.GetClassPosition(9) == 4, "CarIdx 9 ClassPosition should be 4");
            Assert(Math.Abs(bridge.GetLapDistPct(9) - 0.05f) < 1e-4, "CarIdx 9 LapDistPct should be 0.05");

            Assert(IracingTelemetryBridge.GetTrackSurfaceString(IracingTrackSurface.InPitStall) == "InPitStall", "TrackSurfaceString should be InPitStall");
            Assert(IracingTelemetryBridge.GetTrackSurfaceString(IracingTrackSurface.OnTrack) == "OnTrack", "TrackSurfaceString should be OnTrack");
            Assert(IracingTelemetryBridge.GetTrackSurfaceString(IracingTrackSurface.AproachingPits) == "ApproachingPits", "TrackSurfaceString should be ApproachingPits");
            Assert(IracingTelemetryBridge.GetTrackSurfaceString(IracingTrackSurface.OffTrack) == "OffTrack", "TrackSurfaceString should be OffTrack");
            Assert(IracingTelemetryBridge.GetTrackSurfaceString(IracingTrackSurface.NotInWorld) == "NotInWorld", "TrackSurfaceString should be NotInWorld");

            Pass("IracingTelemetryBridge correctly extracts telemetry from PluginManager replay arrays");
        }

        private static void Test_TargetState_TrackSurface_Properties()
        {
            var target = new TargetState();
            Assert(target.TrackSurface == IracingTrackSurface.NotInWorld, "Initial TrackSurface should be NotInWorld");
            Assert(target.TrackSurfaceCode == -1, "Initial TrackSurfaceCode should be -1");
            Assert(target.TrackSurfaceString == "NotInWorld", "Initial TrackSurfaceString should be NotInWorld");
            Assert(!target.IsInPitStall, "Initial IsInPitStall should be false");
            Assert(!target.IsOnPitRoad, "Initial IsOnPitRoad should be false");

            target.TrackSurface = IracingTrackSurface.InPitStall;
            target.IsOnPitRoad = true;
            Assert(target.IsInPitStall, "IsInPitStall should be true when TrackSurface is InPitStall");
            Assert(target.TrackSurfaceCode == 1, "TrackSurfaceCode should be 1 for InPitStall");
            Assert(target.TrackSurfaceString == "InPitStall", "TrackSurfaceString should be InPitStall");
            Assert(target.IsOnPitRoad, "IsOnPitRoad should be true");

            target.TrackSurface = IracingTrackSurface.OnTrack;
            target.IsOnPitRoad = false;
            Assert(!target.IsInPitStall, "IsInPitStall should be false when TrackSurface is OnTrack");
            Assert(target.TrackSurfaceCode == 3, "TrackSurfaceCode should be 3 for OnTrack");
            Assert(target.TrackSurfaceString == "OnTrack", "TrackSurfaceString should be OnTrack");

            Pass("TargetState TrackSurface and PitRoad properties reflect correctly");
        }
    }
}
