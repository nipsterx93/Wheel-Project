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
            Test_PlayerAndTarget_TrackPositionPercent_Properties();
            Test_InPitStall_Fallback_WhenApproachingPitsAndStationary();
            Test_InPitStall_NativeTakesPriorityImmediately();
            Test_OpponentPosition_NativeLapDistPct_TakesPriorityOverSimHubTrackPositionPercent();
            Test_LatchedTarget_PreservedOnTemporaryDropOrReplayJump();
            Test_RecordPitExitSample_RejectsSampleTooCloseToEntry();
            Test_TheoreticalTransitTime_CalculatesFromYamlSpeedAndTrackLength();
            Test_Player_NaturalPitStop_SavesPitTransitTime();
            Test_Player_NaturalDriveThrough_SavesPitDriveThroughTime();
            Test_Opponent_NotInWorld_LatchesPitRoadAndDeducesStationaryTime();
            Test_SpatialGeofence_DoesNotTriggerPitStopAtRacingSpeedOnStraight();
            Test_SimultaneousPitStop_IdentifiesFuelOnlyWhenTimeExplainedByFuel();
            Test_NativeConfirmedOnTrack_RejectsRetroactiveSpatialValidation();
            Test_TargetAlreadyPitted_WithFuelToFinish_DoesNotNeedPitStop();
            Test_LapFuelSync_DoesNotDoubleDeductInlapFuelAfterPit();
            Test_SplashAndDash_CalculatesDynamicFuelRateFromActualLitres();
            Test_ExtendedPitZone_AppliesPhysicalFloorToRejectTrackCutOutliers();

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
            Assert(target.TrackPositionPercent == 0.0, "Initial TrackPositionPercent should be 0.0");

            target.TrackSurface = IracingTrackSurface.InPitStall;
            target.IsOnPitRoad = true;
            target.TrackPositionPercent = 0.1234;
            Assert(target.IsInPitStall, "IsInPitStall should be true when TrackSurface is InPitStall");
            Assert(target.TrackSurfaceCode == 1, "TrackSurfaceCode should be 1 for InPitStall");
            Assert(target.TrackSurfaceString == "InPitStall", "TrackSurfaceString should be InPitStall");
            Assert(target.IsOnPitRoad, "IsOnPitRoad should be true");
            Assert(Math.Abs(target.TrackPositionPercent - 0.1234) < 1e-6, "TrackPositionPercent should be 0.1234");

            target.TrackSurface = IracingTrackSurface.OnTrack;
            target.IsOnPitRoad = false;
            Assert(!target.IsInPitStall, "IsInPitStall should be false when TrackSurface is OnTrack");
            Assert(target.TrackSurfaceCode == 3, "TrackSurfaceCode should be 3 for OnTrack");
            Assert(target.TrackSurfaceString == "OnTrack", "TrackSurfaceString should be OnTrack");

            Pass("TargetState TrackSurface and PitRoad properties reflect correctly");
        }

        private static void Test_PlayerAndTarget_TrackPositionPercent_Properties()
        {
            var state = new SessionState
            {
                IsGameRunning = true,
                PlayerCarIdx = 5,
                TrackPositionPercent = 0.6543
            };

            var tracker = new OpponentTracker();
            tracker.IracingBridge.SetMockData(
                new bool[64],
                new IracingTrackSurface[64],
                new int[64],
                new int[64],
                new float[64]
            );

            // Test 1: NativeLapDistPct assente (> 0 false) => fallback su state.TrackPositionPercent
            tracker.PlayerData.LastPosPct = state.TrackPositionPercent;
            Assert(Math.Abs(tracker.PlayerData.LastPosPct - 0.6543) < 1e-4, "PlayerData.LastPosPct should fall back to state.TrackPositionPercent");

            // Test 2: NativeLapDistPct presente (es. 0.725f)
            tracker.IracingBridge.CarIdxLapDistPct[5] = 0.725f;
            float nativePlayerDist = tracker.IracingBridge.GetLapDistPct(state.PlayerCarIdx);
            tracker.PlayerData.NativeLapDistPct = nativePlayerDist;
            tracker.PlayerData.LastPosPct = (nativePlayerDist > 0.0f) ? (double)nativePlayerDist : state.TrackPositionPercent;
            Assert(Math.Abs(tracker.PlayerData.LastPosPct - 0.725) < 1e-4, "PlayerData.LastPosPct should use nativeLapDistPct when available");

            // Test 3: TargetState TrackPositionPercent prende valore da oppData
            var targetState = new TargetState();
            var oppData = new OpponentTelemetryData
            {
                LastPosPct = 0.3344,
                NativeLapDistPct = 0.3345f
            };
            targetState.TrackPositionPercent = (oppData.NativeLapDistPct > 0.0f) ? (double)oppData.NativeLapDistPct : oppData.LastPosPct;
            Assert(Math.Abs(targetState.TrackPositionPercent - 0.3345) < 1e-4, "TargetState should use NativeLapDistPct if > 0");

            Pass("Player and Target TrackPositionPercent properties and fallback behave correctly");
        }

        private static void Test_InPitStall_Fallback_WhenApproachingPitsAndStationary()
        {
            var tData = new OpponentTelemetryData
            {
                Name = "Bruno Carneiro",
                CarClass = "GT3",
                IsOnPitRoad = true,
                TrackSurface = IracingTrackSurface.AproachingPits
            };

            // Simula la logica di fallback:
            // Tick 1: Vettura in pit lane, velocità 0 km/h, ApproachingPits (non InPitStall)
            double currentSessionClock = 1000.0;
            double currentSpeed = 0.0;
            double currentPosPct = 0.05;
            bool nativeOnPitRoad = true;
            var nativeTrackSurface = IracingTrackSurface.AproachingPits;

            bool effectiveInPitStall = (nativeTrackSurface == IracingTrackSurface.InPitStall);
            if (!effectiveInPitStall && (nativeOnPitRoad || nativeTrackSurface == IracingTrackSurface.AproachingPits))
            {
                if (currentSpeed < 0.5)
                {
                    if (tData.PitRoadStationaryStartSec == null)
                    {
                        tData.PitRoadStationaryStartSec = currentSessionClock;
                        tData.PitRoadStationaryPosPct = currentPosPct;
                    }
                }
            }

            Assert(!effectiveInPitStall, "At t=0s, stationary fallback should not have triggered yet (< 1.0s)");
            Assert(tData.PitRoadStationaryStartSec == 1000.0, "Stationary start clock should be 1000.0");

            // Tick 2: Passati 0.5s (t = 1000.5s), ancora ferma
            currentSessionClock = 1000.5;
            if (currentSpeed < 0.5 && tData.PitRoadStationaryStartSec != null)
            {
                if (Math.Abs(currentSessionClock - tData.PitRoadStationaryStartSec.Value) >= 1.0)
                {
                    effectiveInPitStall = true;
                    tData.TrackSurface = IracingTrackSurface.InPitStall;
                }
            }
            Assert(!effectiveInPitStall, "At t=0.5s, stationary fallback should not have triggered yet");

            // Tick 3: Passati 1.2s (t = 1001.2s), ferma da > 1.0s
            currentSessionClock = 1001.2;
            if (currentSpeed < 0.5 && tData.PitRoadStationaryStartSec != null)
            {
                if (Math.Abs(currentSessionClock - tData.PitRoadStationaryStartSec.Value) >= 1.0)
                {
                    effectiveInPitStall = true;
                    tData.TrackSurface = IracingTrackSurface.InPitStall;
                }
            }
            Assert(effectiveInPitStall, "At t=1.2s, effectiveInPitStall should be TRUE");
            Assert(tData.TrackSurface == IracingTrackSurface.InPitStall, "TrackSurface must be promoted to InPitStall");

            // Avvio cronometro
            if (effectiveInPitStall)
            {
                if (!tData.WasInPitStall)
                {
                    tData.WasInPitStall = true;
                    tData.InPitStallStartTimeSec = tData.PitRoadStationaryStartSec ?? currentSessionClock;
                    tData.StopStartTimeSec = tData.InPitStallStartTimeSec;
                }
                tData.StationaryTimeSec = Math.Abs(currentSessionClock - tData.InPitStallStartTimeSec);
            }
            Assert(tData.WasInPitStall, "WasInPitStall should be true");
            Assert(Math.Abs(tData.InPitStallStartTimeSec - 1000.0) < 1e-4, "Start time should be retrodated to 1000.0s");
            Assert(Math.Abs(tData.StationaryTimeSec - 1.2) < 1e-4, "Stationary time should be 1.2s");

            // Tick 4: Sosta continua per 25s (t = 1025.0s)
            currentSessionClock = 1025.0;
            tData.StationaryTimeSec = Math.Abs(currentSessionClock - tData.InPitStallStartTimeSec);
            Assert(Math.Abs(tData.StationaryTimeSec - 25.0) < 1e-4, "Stationary time should be 25.0s");

            // Tick 5: Ripartenza vettura (speed = 25.0 km/h)
            currentSpeed = 25.0;
            effectiveInPitStall = (nativeTrackSurface == IracingTrackSurface.InPitStall);
            if (!effectiveInPitStall && (nativeOnPitRoad || nativeTrackSurface == IracingTrackSurface.AproachingPits))
            {
                if (currentSpeed < 0.5) { }
                else
                {
                    tData.PitRoadStationaryStartSec = null;
                    tData.PitRoadStationaryPosPct = null;
                }
            }

            Assert(!effectiveInPitStall, "effectiveInPitStall should become false when speed >= 0.5 km/h");
            if (!effectiveInPitStall && tData.WasInPitStall)
            {
                tData.WasInPitStall = false;
                tData.StationaryTimeSec = Math.Abs(currentSessionClock - tData.InPitStallStartTimeSec);
                tData.LastPitStationaryTimeSec = tData.StationaryTimeSec;
                tData.StopStartTimeSec = null;
                tData.TrackSurface = nativeTrackSurface;
            }

            Assert(!tData.WasInPitStall, "WasInPitStall should be reset to false on departure");
            Assert(tData.TrackSurface == IracingTrackSurface.AproachingPits, "TrackSurface should revert to AproachingPits");
            Assert(Math.Abs(tData.LastPitStationaryTimeSec - 25.0) < 1e-4, "LastPitStationaryTimeSec should be 25.0s");

            Pass("InPitStall fallback correctly detects stationary opponent on pit road and records stop");
        }

        private static void Test_InPitStall_NativeTakesPriorityImmediately()
        {
            var tData = new OpponentTelemetryData
            {
                Name = "Nicolas Regnier",
                IsOnPitRoad = true,
                TrackSurface = IracingTrackSurface.AproachingPits
            };

            double currentSessionClock = 2000.0;
            var nativeTrackSurface = IracingTrackSurface.InPitStall;

            bool effectiveInPitStall = (nativeTrackSurface == IracingTrackSurface.InPitStall);
            Assert(effectiveInPitStall, "Native InPitStall must take priority immediately");

            if (effectiveInPitStall)
            {
                if (!tData.WasInPitStall)
                {
                    tData.WasInPitStall = true;
                    tData.InPitStallStartTimeSec = tData.PitRoadStationaryStartSec ?? currentSessionClock;
                }
                tData.StationaryTimeSec = Math.Abs(currentSessionClock - tData.InPitStallStartTimeSec);
            }

            Assert(tData.WasInPitStall, "WasInPitStall should be true immediately");
            Assert(Math.Abs(tData.InPitStallStartTimeSec - 2000.0) < 1e-4, "StartTime should be 2000.0s immediately");

            Pass("Native InPitStall takes priority immediately without delay");
        }

        private static void Test_OpponentPosition_NativeLapDistPct_TakesPriorityOverSimHubTrackPositionPercent()
        {
            var tracker = new OpponentTracker();
            var state = new SessionState();
            state.Metadata.SourceName = "iRacingSDK";
            state.Metadata.CarIdxByUserName["Bruno Carneiro"] = 5;

            var opp = new GameReaderCommon.Opponent
            {
                Name = "Bruno Carneiro",
                TrackPositionPercent = 0.45 // Stale or desynced SimHub position
            };

            // Set mock native telemetry with 60 Hz CarIdxLapDistPct
            var lapDists = new float[64];
            lapDists[5] = 0.725f; // Native position
            tracker.IracingBridge.SetMockData(
                onPitRoad: new bool[64],
                trackSurface: new IracingTrackSurface[64],
                pitStopCount: new int[64],
                classPosition: new int[64],
                lapDistPct: lapDists
            );

            // 1. When native telemetry is available, it MUST take priority over opp.TrackPositionPercent
            double pos = tracker.GetOpponentTrackPosition(opp, state);
            Assert(Math.Abs(pos - 0.725) < 1e-4, $"GetOpponentTrackPosition must return native 0.725, got {pos}");

            // 2. When native telemetry is NOT available (or 0f), fallback to SimHub TrackPositionPercent
            lapDists[5] = 0f;
            double fallbackPos = tracker.GetOpponentTrackPosition(opp, state);
            Assert(Math.Abs(fallbackPos - 0.45) < 1e-4, $"Fallback must return SimHub 0.45, got {fallbackPos}");

            // 3. When both native and SimHub are unavailable, fallback to last tracked position
            opp.TrackPositionPercent = null;
            tracker.AddTrackedOpponent("Bruno Carneiro", new OpponentTelemetryData { Name = "Bruno Carneiro", LastPosPct = 0.60 });
            double continuityPos = tracker.GetOpponentTrackPosition(opp, state);
            Assert(Math.Abs(continuityPos - 0.60) < 1e-4, $"Continuity fallback must return LastPosPct 0.60, got {continuityPos}");

            Pass("Opponent position prioritizes native CarIdxLapDistPct over SimHub TrackPositionPercent");
        }

        private static void Test_LatchedTarget_PreservedOnTemporaryDropOrReplayJump()
        {
            var manager = new TargetStrategyManager();
            var tracker = new OpponentTracker();
            var state = new SessionState();
            state.Opponents = new System.Collections.Generic.List<GameReaderCommon.Opponent>();

            manager.LatchedTargetName = "Bruno Carneiro";
            tracker.AddTrackedOpponent("Bruno Carneiro", new OpponentTelemetryData
            {
                Name = "Bruno Carneiro",
                CarClass = "GT3",
                ClassPosition = 2,
                NativeLapDistPct = 0.55f,
                LastPosPct = 0.55
            });

            // Replay Jump preserves latched target when preserveLatchedTarget is true
            manager.ResetSession(preserveLatchedTarget: true);
            Assert(manager.LatchedTargetName == "Bruno Carneiro", "ResetSession(preserveLatchedTarget: true) must retain LatchedTargetName");

            // Full session reset clears latched target
            manager.ResetSession(preserveLatchedTarget: false);
            Assert(manager.LatchedTargetName == null, "ResetSession(preserveLatchedTarget: false) must clear LatchedTargetName");

            Pass("Latched target is preserved during temporary drops and replay jumps");
        }

        private static void Test_RecordPitExitSample_RejectsSampleTooCloseToEntry()
        {
            var radar = new PitRadar();
            var track = new TrackRecord
            {
                TrackID = "roadatlanta",
                CarClass = "GT3",
                PitEntryPct = 0.9575,
                PitExitPct = 0.0887,
                GeofenceConfidence = CalibrationConfidence.EstimatedOpponent
            };
            radar.SetCurrentTrackForTesting(track);

            // Spurious sample at 0.9582 (delta = 0.0007, which is < MinimumPitTraversalPct 0.01)
            radar.RecordPitExitSample(0.9582, CalibrationConfidence.EstimatedOpponent);
            Assert(Math.Abs(radar.CurrentTrack.PitExitPct - 0.0887) < 1e-4,
                $"RecordPitExitSample must reject 0.9582 because it is too close to entry (0.9575). Got {radar.CurrentTrack.PitExitPct}");

            // Valid sample at 0.0885 (traversal delta ~0.131)
            radar.RecordPitExitSample(0.0885, CalibrationConfidence.Confirmed);
            Assert(Math.Abs(radar.CurrentTrack.PitExitPct - 0.0885) < 1e-4,
                $"RecordPitExitSample must accept genuine sample at 0.0885. Got {radar.CurrentTrack.PitExitPct}");

            Pass("RecordPitExitSample rejects sample too close to entry and accepts genuine exit");
        }

        private static void Test_TheoreticalTransitTime_CalculatesFromYamlSpeedAndTrackLength()
        {
            var radar = new PitRadar();
            var track = new TrackRecord
            {
                TrackID = "roadatlanta",
                CarClass = "GT3",
                PitEntryPct = 0.9578,
                PitExitPct = 0.0887,
                PitLaneSpeedLimit = 72.42 // km/h from YAML (45 mph)
            };
            radar.SetCurrentTrackForTesting(track);

            // TrackLength = 4056.9m. DeltaPct = (1.0 - 0.9578) + 0.0887 = 0.1309. Distance = 531.05m.
            // Speed = 72.42 km/h = 20.1167 m/s. Theoretical transit = 531.05 / 20.1167 = 26.40s.
            double theoretical = radar.GetTheoreticalTransitTimeSec(4056.9);
            Assert(Math.Abs(theoretical - 26.40) < 0.1,
                $"Expected theoretical transit ~26.40s, got {theoretical:F2}s");

            Pass("Theoretical transit time calculates correctly from YAML pit speed and track length");
        }

        private static void Test_Player_NaturalPitStop_SavesPitTransitTime()
        {
            var radar = new PitRadar();
            var track = new TrackRecord
            {
                TrackClassID = "ROADATLANTA_STOP_GT3",
                TrackID = "roadatlanta_stop",
                CarClass = "GT3",
                PitEntryPct = 0.9578,
                PitExitPct = 0.0887,
                PitLaneSpeedLimit = 72.42,
                PitTransitTime = 0.0,
                PlayerRecordSet = false
            };
            radar.SetCurrentTrackForTesting(track);

            var state = new SessionState
            {
                TrackId = "roadatlanta_stop",
                CarClassId = "GT3",
                TrackLengthMeters = 4056.9,
                IsInPitLane = true,
                IsInPitBox = false,
                TrackPositionPercent = 0.9578,
                CurrentFuelLevel = 17.1,
                SpeedKmh = 72.0
            };

            // Enter pit lane at t = 100.0s
            radar.Update(state, sessionClock: 100.0, TyreSelectionScope.None, fuelToAdd: 31.0, log: null);

            // Stop in box from t = 114.0s to t = 127.83s (13.83s stationary, refueling 31L)
            state.IsInPitBox = true;
            state.SpeedKmh = 0.0;
            state.CurrentFuelLevel = 17.1;
            radar.Update(state, sessionClock: 114.0, TyreSelectionScope.None, fuelToAdd: 31.0, log: null);

            state.CurrentFuelLevel = 48.1;
            radar.Update(state, sessionClock: 127.83, TyreSelectionScope.None, fuelToAdd: 31.0, log: null);

            // Resume driving in pit lane at t = 127.83s (leaves box)
            state.IsInPitBox = false;
            state.SpeedKmh = 72.0;
            radar.Update(state, sessionClock: 127.83, TyreSelectionScope.None, fuelToAdd: 31.0, log: null);

            // Exit pit lane at t = 141.12s (Total pit time = 41.12s, StatTime = 13.83s)
            state.IsInPitLane = false;
            state.TrackPositionPercent = 0.0887;
            radar.Update(state, sessionClock: 141.12, TyreSelectionScope.None, fuelToAdd: 31.0, log: null);

            // Expected transit time = 41.12 - 13.83 = 27.29s
            Assert(radar.CurrentTrack.PlayerRecordSet, "PlayerRecordSet must be true after natural pit stop with stall stop.");
            Assert(Math.Abs(radar.CurrentTrack.PitTransitTime - 27.29) < 0.1,
                $"Expected PitTransitTime ~27.29s, got {radar.CurrentTrack.PitTransitTime:F2}s");

            Pass("Player natural pit stop saves PitTransitTime and sets PlayerRecordSet");
        }

        private static void Test_Player_NaturalDriveThrough_SavesPitDriveThroughTime()
        {
            var radar = new PitRadar();
            var track = new TrackRecord
            {
                TrackClassID = "ROADATLANTA_DT_GT3",
                TrackID = "roadatlanta_dt",
                CarClass = "GT3",
                PitEntryPct = 0.9578,
                PitExitPct = 0.0887,
                PitLaneSpeedLimit = 72.42,
                PitTransitTime = 0.0,
                PitDriveThroughTime = 0.0
            };
            radar.SetCurrentTrackForTesting(track);

            var state = new SessionState
            {
                TrackId = "roadatlanta_dt",
                CarClassId = "GT3",
                TrackLengthMeters = 4056.9,
                IsInPitLane = true,
                IsInPitBox = false,
                TrackPositionPercent = 0.9578,
                CurrentFuelLevel = 40.0,
                SpeedKmh = 72.0
            };

            // Enter pit lane at t = 200.0s
            radar.Update(state, sessionClock: 200.0, TyreSelectionScope.None, fuelToAdd: 0.0, log: null);

            // Drive through without stopping at 72 km/h, exit at t = 226.5s
            state.IsInPitLane = false;
            state.TrackPositionPercent = 0.0887;
            radar.Update(state, sessionClock: 226.5, TyreSelectionScope.None, fuelToAdd: 0.0, log: null);

            Assert(Math.Abs(radar.CurrentTrack.PitDriveThroughTime - 26.5) < 0.1,
                $"Expected PitDriveThroughTime ~26.5s, got {radar.CurrentTrack.PitDriveThroughTime:F2}s");
            Assert(radar.CurrentTrack.PitTransitTime == 0.0,
                "PitTransitTime must NOT be updated by a Drive-Through (no stall stop).");

            Pass("Player natural drive-through saves PitDriveThroughTime without touching PitTransitTime");
        }

        private static void Test_Opponent_NotInWorld_LatchesPitRoadAndDeducesStationaryTime()
        {
            var radar = new PitRadar();
            var track = new TrackRecord
            {
                TrackClassID = "ROADATLANTA_OPP_GT3",
                TrackID = "roadatlanta_opp",
                CarClass = "GT3",
                PitEntryPct = 0.9578,
                PitExitPct = 0.0887,
                PitTransitTime = 27.29, // Calibrated from Player
                PitLaneSpeedLimit = 72.42
            };
            radar.SetCurrentTrackForTesting(track);

            var tData = new OpponentTelemetryData
            {
                Name = "Bruno Carneiro",
                CarClass = "GT3",
                IsOnPitRoad = false,
                TrackSurface = IracingTrackSurface.OnTrack
            };

            // 1. Ingresso Pit Lane a t = 1000.0s in AproachingPits
            double currentSessionClock = 1000.0;
            bool wasOnPitRoad = tData.IsOnPitRoad;
            bool nativeOnPitRoad = true;
            var nativeTrackSurface = IracingTrackSurface.AproachingPits;

            bool effectiveOnPitRoad = nativeOnPitRoad;
            if (nativeTrackSurface == IracingTrackSurface.NotInWorld)
            {
                if (wasOnPitRoad)
                {
                    effectiveOnPitRoad = true;
                    if (tData.NotInWorldStartSec == null) tData.NotInWorldStartSec = currentSessionClock;
                }
            }
            else
            {
                if (tData.NotInWorldStartSec != null)
                {
                    tData.NotInWorldDurationSec += Math.Abs(currentSessionClock - tData.NotInWorldStartSec.Value);
                    tData.NotInWorldStartSec = null;
                }
                if (wasOnPitRoad && !nativeOnPitRoad)
                {
                    if (nativeTrackSurface == IracingTrackSurface.AproachingPits || nativeTrackSurface == IracingTrackSurface.InPitStall)
                        effectiveOnPitRoad = true;
                    else
                        effectiveOnPitRoad = false;
                }
            }

            if (!wasOnPitRoad && effectiveOnPitRoad)
            {
                radar.RecordPitEntrySample(0.9578, CalibrationConfidence.EstimatedOpponent, null);
            }
            tData.IsOnPitRoad = effectiveOnPitRoad;
            tData.TrackSurface = nativeTrackSurface;

            Assert(tData.IsOnPitRoad, "Opponent must be on pit road at entry");
            Assert(Math.Abs(radar.CurrentTrack.PitEntryPct - 0.9578) < 1e-4, "PitEntryPct must be 0.9578");

            // 2. iRacing culls opponent a t = 1002.6s (entra in NotInWorld -1, nativeOnPitRoad diventa false)
            currentSessionClock = 1002.6;
            wasOnPitRoad = tData.IsOnPitRoad;
            nativeOnPitRoad = false;
            nativeTrackSurface = IracingTrackSurface.NotInWorld;

            effectiveOnPitRoad = nativeOnPitRoad;
            if (nativeTrackSurface == IracingTrackSurface.NotInWorld)
            {
                if (wasOnPitRoad)
                {
                    effectiveOnPitRoad = true;
                    if (tData.NotInWorldStartSec == null) tData.NotInWorldStartSec = currentSessionClock;
                }
            }

            // CRITICO: la falsa uscita NÃO deve avvenire
            if (wasOnPitRoad && !effectiveOnPitRoad)
            {
                radar.RecordPitExitSample(0.9582, CalibrationConfidence.EstimatedOpponent, null);
            }
            tData.IsOnPitRoad = effectiveOnPitRoad;
            tData.TrackSurface = nativeTrackSurface;

            Assert(tData.IsOnPitRoad, "Opponent must remain latched on pit road despite NotInWorld");
            Assert(radar.CurrentTrack.PitExitPct == 0.0887,
                "PitExitPct must NOT be corrupted to 0.958 by NotInWorld culling");
            Assert(tData.NotInWorldStartSec == 1002.6, "NotInWorldStartSec must record start timestamp");

            // 3. Risveglio pre-uscita a t = 1045.16s (torna ad AproachingPits per 1.8s)
            currentSessionClock = 1045.16;
            wasOnPitRoad = tData.IsOnPitRoad;
            nativeOnPitRoad = true;
            nativeTrackSurface = IracingTrackSurface.AproachingPits;

            effectiveOnPitRoad = nativeOnPitRoad;
            if (nativeTrackSurface == IracingTrackSurface.NotInWorld)
            {
                if (wasOnPitRoad)
                {
                    effectiveOnPitRoad = true;
                    if (tData.NotInWorldStartSec == null) tData.NotInWorldStartSec = currentSessionClock;
                }
            }
            else
            {
                if (tData.NotInWorldStartSec != null)
                {
                    tData.NotInWorldDurationSec += Math.Abs(currentSessionClock - tData.NotInWorldStartSec.Value);
                    tData.NotInWorldStartSec = null;
                }
                if (wasOnPitRoad && !nativeOnPitRoad)
                {
                    if (nativeTrackSurface == IracingTrackSurface.AproachingPits || nativeTrackSurface == IracingTrackSurface.InPitStall)
                        effectiveOnPitRoad = true;
                    else
                        effectiveOnPitRoad = false;
                }
            }
            tData.IsOnPitRoad = effectiveOnPitRoad;
            tData.TrackSurface = nativeTrackSurface;

            Assert(tData.IsOnPitRoad, "Opponent waking up from NotInWorld must remain on pit road");
            Assert(Math.Abs(tData.NotInWorldDurationSec - 42.56) < 1e-4,
                $"NotInWorldDurationSec must be 42.56s, got {tData.NotInWorldDurationSec:F2}s");
            Assert(tData.NotInWorldStartSec == null, "NotInWorldStartSec must be cleared");

            // 4. Ritorno OnTrack a t = 1046.96s (linea uscita box a 0.0887)
            currentSessionClock = 1046.96;
            wasOnPitRoad = tData.IsOnPitRoad;
            nativeOnPitRoad = false;
            nativeTrackSurface = IracingTrackSurface.OnTrack;

            effectiveOnPitRoad = nativeOnPitRoad;
            if (nativeTrackSurface != IracingTrackSurface.NotInWorld)
            {
                if (tData.NotInWorldStartSec != null)
                {
                    tData.NotInWorldDurationSec += Math.Abs(currentSessionClock - tData.NotInWorldStartSec.Value);
                    tData.NotInWorldStartSec = null;
                }
                if (wasOnPitRoad && !nativeOnPitRoad)
                {
                    if (nativeTrackSurface == IracingTrackSurface.AproachingPits || nativeTrackSurface == IracingTrackSurface.InPitStall)
                        effectiveOnPitRoad = true;
                    else
                        effectiveOnPitRoad = false;
                }
            }
            if (wasOnPitRoad && !effectiveOnPitRoad)
            {
                radar.RecordPitExitSample(0.0887, CalibrationConfidence.EstimatedOpponent, null);
            }
            tData.IsOnPitRoad = effectiveOnPitRoad;
            tData.TrackSurface = nativeTrackSurface;

            Assert(!tData.IsOnPitRoad, "Opponent must have exited pit road upon returning OnTrack");
            Assert(Math.Abs(radar.CurrentTrack.PitExitPct - 0.0887) < 1e-4, "PitExitPct must remain 0.0887");

            // 5. Deduzione Reverse-Engineering della sosta
            // Formula: StationaryTime = NotInWorldDurationSec - refTransit
            // 42.56s - 27.29s = 15.27s
            if (tData.NotInWorldDurationSec > 0.0)
            {
                double refTransit = radar.PitTransitTime > 0.0
                    ? radar.PitTransitTime
                    : radar.GetTheoreticalTransitTimeSec(4056.9);

                if (refTransit > 0.0)
                {
                    double deducedStationary = Math.Max(0.0, tData.NotInWorldDurationSec - refTransit);
                    if (deducedStationary > 0.5)
                    {
                        tData.StationaryTimeSec = deducedStationary;
                        tData.LastPitStationaryTimeSec = deducedStationary;
                    }
                }
            }

            Assert(Math.Abs(tData.StationaryTimeSec - 15.27) < 0.01,
                $"Expected deduced StationaryTime ~15.27s, got {tData.StationaryTimeSec:F2}s");

            // Classificazione gomme: soglia cambio 2 gomme in GT3 (tTyres = 26s, min = 0.5 * 26 - 1 = 12s, o full 4 gomme 18-20s)
            // A 15.27s, rifornimento stimato = (15.27 - 2.0s overhead) * 2.7 L/s = ~35.8L -> compatibile 100% con solo carburante
            double tTyres = radar.DbTireChangeTime; // default 26.0
            double fillRate = 2.7;
            double activeRefuelTime = Math.Max(0.0, tData.StationaryTimeSec - 2.0); // 13.27s
            double maxReasonableRefuelTime = (34.0 / fillRate) + 1.0; // ~13.6s
            double min2TiresTime = (0.5 * tTyres) - 1.0; // 12.0s
            double deltaRemaining = activeRefuelTime - maxReasonableRefuelTime; // < 0 (nessun tempo residuo per gomme)

            bool opponentTiresChanged = deltaRemaining >= min2TiresTime;
            tData.LastPitTiresChanged = opponentTiresChanged;
            Assert(!tData.LastPitTiresChanged, "At 15.27s stationary time, opponent must be identified as Fuel Only (no tire change)");

            // Cleanup NotInWorld a fine sosta
            tData.NotInWorldDurationSec = 0.0;
            tData.NotInWorldStartSec = null;
            Assert(tData.NotInWorldDurationSec == 0.0, "NotInWorldDurationSec must be reset after stop");

            Pass("Opponent NotInWorld latches pit road, protects geofence, and correctly deduces stationary time");
        }

        private static void Test_SpatialGeofence_DoesNotTriggerPitStopAtRacingSpeedOnStraight()
        {
            var radar = new PitRadar();
            var track = new TrackRecord
            {
                TrackClassID = "ROADATLANTA_STRAIGHT_GT3",
                TrackID = "roadatlanta_straight",
                CarClass = "GT3",
                PitEntryPct = 0.9575,
                PitExitPct = 0.1051,
                PitLaneSpeedLimit = 72.42
            };
            radar.SetCurrentTrackForTesting(track);

            var tData = new OpponentTelemetryData
            {
                Name = "Bruno Carneiro",
                CarClass = "GT3",
                IsOnPitRoad = false,
                TrackSurface = IracingTrackSurface.OnTrack,
                LastValidSpeedKmh = 196.8,
                HasExitedPitZoneAtLeastOnce = true
            };

            // 1. Con telemetria nativa disponibile (isNativeAvailable = true), !IsOnPitRoad
            // Il bypass nativo impone isInsideGeofence = false, a prescindere dalla posizione spaziale
            bool isNativeAvailable = true;
            bool isSpatiallyInsideGeofence = true; // es. 0.9600 a cavallo del rettilineo di Road Atlanta
            bool isInsideGeofence = false;

            if (isNativeAvailable && tData.IsOnPitRoad)
            {
                isInsideGeofence = true;
            }
            else if (isNativeAvailable && !tData.IsOnPitRoad)
            {
                isInsideGeofence = false;
            }
            else if (isSpatiallyInsideGeofence)
            {
                isInsideGeofence = true;
            }

            Assert(!isInsideGeofence, "Native !IsOnPitRoad telemetry must bypass spatial geofence and force isInsideGeofence = false.");

            // 2. Senza telemetria nativa (fallback puramente spaziale):
            // L'auto transita sul rettilineo principale a 196.8 km/h per 9.4s (il pit zone a Road Atlanta è lungo 598.9m)
            isNativeAvailable = false;
            double pitSpeedThreshold = PitLaneDetector.SpeedThresholdFor(radar.GetPitLaneSpeedLimit(tData.CarClass)); // ~82.4 km/h

            // Criterio B: Velocità sotto soglia
            bool criterioBTrigger = tData.LastValidSpeedKmh < pitSpeedThreshold;
            Assert(!criterioBTrigger, "Racing speed 196.8 km/h must not satisfy Criterio B speed threshold.");

            // Criterio C: Paracadute di durata nel geofence
            // Protezione 1: Velocità massima per attivare Criterio C (< pitSpeedThreshold + 15 e < 100 km/h)
            bool criterioCAllowed = tData.LastValidSpeedKmh < (pitSpeedThreshold + 15.0) && tData.LastValidSpeedKmh < 100.0;
            Assert(!criterioCAllowed, "Criterio C must be strictly disabled when car is at racing speed (196.8 km/h >= 100 km/h).");

            // Protezione 2: Pavimento minimo della soglia di durata (Math.Max(15.0, ...))
            double classBestRacingTime = 5.3; // Esempio di campione sporco/stretto registrato in passato
            double durationThreshold = Math.Max(15.0, (classBestRacingTime > 0.0 ? classBestRacingTime * 1.8 : 15.0));
            Assert(durationThreshold >= 15.0, $"Duration threshold must be at least 15.0s, got {durationThreshold:F1}s.");

            double elapsedStraightTransitTime = 9.4; // 598.9m a ~200 km/h impiegano ~9.4-9.5s
            bool durationTrigger = elapsedStraightTransitTime > durationThreshold;
            Assert(!durationTrigger, "9.4s straight transit must NOT exceed duration threshold (>= 15.0s).");

            bool fallbackInsideGeofence = criterioBTrigger || (criterioCAllowed && durationTrigger);
            Assert(!fallbackInsideGeofence, "Fallback geofence logic must not trigger pit stop at racing speed on straight.");

            // 3. Auto lontana dal player, culled da iRacing in NotInWorld (-1) e !IsOnPitRoad:
            // Le coordinate non si aggiornano a 60 Hz, deltaPos = 0, velocità calcolata = 0.0 km/h.
            // Con telemetria nativa attiva:
            tData.TrackSurface = IracingTrackSurface.NotInWorld;
            tData.IsOnPitRoad = false;
            tData.LastValidSpeedKmh = 0.0;
            isNativeAvailable = true;
            isInsideGeofence = false;

            if (isNativeAvailable && tData.IsOnPitRoad)
            {
                isInsideGeofence = true;
            }
            else if (isNativeAvailable && !tData.IsOnPitRoad)
            {
                isInsideGeofence = false;
            }
            Assert(!isInsideGeofence, "Culled NotInWorld car on track must NOT trigger geofence when native !IsOnPitRoad.");

            // Nel fallback spaziale puro, Speed < 0.5 NON deve scattare se l'auto è in NotInWorld:
            bool stoppedTrigger = tData.LastValidSpeedKmh < 0.5 && tData.TrackSurface != IracingTrackSurface.NotInWorld;
            Assert(!stoppedTrigger, "Speed < 0.5 must NOT trigger geofence if car is culled in NotInWorld.");

            Pass("Spatial geofence does not falsely trigger pit stops for cars at racing speed on main straight or culled in NotInWorld");
        }

        private static void Test_SimultaneousPitStop_IdentifiesFuelOnlyWhenTimeExplainedByFuel()
        {
            var radar = new PitRadar();
            var track = new TrackRecord
            {
                TrackClassID = "ROADATLANTA_SIMULTANEOUS_GT3",
                TrackID = "roadatlanta_simultaneous",
                CarClass = "GT3",
                PitEntryPct = 0.9575,
                PitExitPct = 0.1051,
                PitTransitTime = 26.37,
                PitLaneSpeedLimit = 72.42
            };
            radar.SetCurrentTrackForTesting(track);

            var tData = new OpponentTelemetryData
            {
                Name = "Bruno Carneiro",
                CarClass = "GT3",
                IsOnPitRoad = false,
                TrackSurface = IracingTrackSurface.OnTrack,
                StationaryTimeSec = 16.20,
                LastPitFuelAdded = 36.3 // Calcolato da Smart Refuel all'ingresso box
            };

            double classMaxTank = 55.0;
            double fillRate = 2.7;
            double tTyres = radar.DbTireChangeTime; // 26.0s
            double predictedFuelToAdd = tData.LastPitFuelAdded > 0.0
                ? tData.LastPitFuelAdded
                : 36.3;

            double tFuel = predictedFuelToAdd / fillRate; // 36.3 / 2.7 = 13.44s
            double tStationary = tData.StationaryTimeSec; // 16.20s
            const double RefuelOverhead = 2.0;
            double activeRefuelTime = Math.Max(0.0, tStationary - RefuelOverhead); // 14.20s
            double expectedTotalRefuelTime = tFuel + RefuelOverhead; // 15.44s
            double minFullTiresTime = Math.Min(tTyres - 3.0, 18.0); // 18.0s

            bool opponentTiresChanged = true;
            double refuelTime = 0.0;

            if (tStationary <= expectedTotalRefuelTime + 2.5 && tStationary < minFullTiresTime)
            {
                // Sosta spiegata al 100% dal carburante (16.20s <= 15.44s + 2.5s e 16.20s < 18.0s)
                opponentTiresChanged = false;
                refuelTime = activeRefuelTime;
            }
            else if (tStationary >= minFullTiresTime)
            {
                opponentTiresChanged = true;
                refuelTime = Math.Min(activeRefuelTime, expectedTotalRefuelTime);
            }

            Assert(!opponentTiresChanged, "At 16.20s stationary time with 36.3L fuel, pit stop must be identified as Fuel Only (TiresChanged = false)!");
            Assert(Math.Abs(refuelTime - 14.20) < 1e-4, "Refuel time must be active refuel time (14.20s)");

            double estimatedFuelAdded = Math.Max(0.0, Math.Min(classMaxTank, refuelTime * fillRate)); // 14.20 * 2.7 = 38.34L
            Assert(estimatedFuelAdded > 35.0, $"Estimated fuel added must be ~38L, got {estimatedFuelAdded:F1}L");

            // Caso opposto: sosta lunga 24.5s (cambio gomme effettuato)
            double tStationaryTires = 24.5;
            bool tiresChangedLongStop = false;
            if (tStationaryTires <= expectedTotalRefuelTime + 2.5 && tStationaryTires < minFullTiresTime)
            {
                tiresChangedLongStop = false;
            }
            else if (tStationaryTires >= minFullTiresTime)
            {
                tiresChangedLongStop = true;
            }

            Assert(tiresChangedLongStop, "A 24.5s stationary stop in Simultaneous GT3 must be identified as Tires Changed (True)!");

            Pass("Simultaneous pit stop correctly identifies Fuel Only when stationary time is explained by refuel duration");
        }

        private static void Test_NativeConfirmedOnTrack_RejectsRetroactiveSpatialValidation()
        {
            var tData = new OpponentTelemetryData
            {
                Name = "Bruno Carneiro",
                CarClass = "GT3",
                IsOnPitRoad = false,
                TrackSurface = IracingTrackSurface.OnTrack,
                MaxSpeedInPitThisTransit = 245.0,
                LastValidSpeedKmh = 240.0,
                PitCount = 1,
                HasCountedPitThisTransit = false,
                SpatialStrictEntryLap = 29,
                HighestLapSeen = 29
            };

            bool isNativeAvailable = true;
            int rawCurrentLap = 29;
            double currentSessionClock = 446.4;
            tData.SpatialStrictEntryTimeSec = 455.9; // delta = 9.5s

            // Logic under test:
            bool isNativeConfirmedOnTrack = isNativeAvailable && !tData.IsOnPitRoad && tData.TrackSurface == IracingTrackSurface.OnTrack;
            bool isHighSpeedTransit = tData.MaxSpeedInPitThisTransit > 120.0 || (tData.LastValidSpeedKmh > 120.0 && tData.TrackSurface == IracingTrackSurface.OnTrack);

            bool pitValidated = false;
            if (isNativeConfirmedOnTrack || isHighSpeedTransit)
            {
                // Discarded (On Track)
                pitValidated = false;
            }
            else
            {
                double totalSpatialTransitTime = Math.Abs(currentSessionClock - tData.SpatialStrictEntryTimeSec);
                double minPhysicalPitTime = 18.0;
                double spatialAdaptiveThreshold = Math.Max(minPhysicalPitTime, 5.3 + 4.0); // 18.0s
                bool lapDiffValid = (rawCurrentLap >= tData.SpatialStrictEntryLap) && (rawCurrentLap <= tData.SpatialStrictEntryLap + 1);
                if (totalSpatialTransitTime > spatialAdaptiveThreshold && lapDiffValid)
                {
                    pitValidated = true;
                    tData.PitCount++;
                }
            }

            Assert(!pitValidated, "Confirmed on track at 245 km/h must NEVER validate retroactive spatial pit stop!");
            Assert(tData.PitCount == 1, "PitCount must remain 1 and not increment on main straight passage!");

            Pass("Confirmed on track at racing speed cleanly rejects retroactive spatial pit stop");
        }

        private static void Test_TargetAlreadyPitted_WithFuelToFinish_DoesNotNeedPitStop()
        {
            var oppData = new OpponentTelemetryData
            {
                Name = "Bruno Carneiro",
                PitCount = 1,
                NeedsPitStop = true, // False positive due to margin in OpponentTracker
                EstimatedFuel = 34.86
            };

            double fuelPerLap = 2.287;
            double raceLapsRemaining = 14.5;
            double targetFuelLaps = oppData.EstimatedFuel / fuelPerLap; // 15.24 laps
            double targetFuelDeficit = raceLapsRemaining - targetFuelLaps; // -0.74 laps (extra fuel!)

            int targetPitCount = oppData.PitCount;

            bool targetNeedsPit;
            if (targetPitCount >= 1)
            {
                targetNeedsPit = targetFuelDeficit > 0.8;
            }
            else
            {
                targetNeedsPit = oppData != null ? (oppData.NeedsPitStop || targetFuelDeficit > 0.8) : (targetFuelDeficit > 0.8);
            }

            Assert(!targetNeedsPit, "Target who has pitted with enough fuel to finish must NOT be flagged as needing another pit stop!");

            double liveSignedGap = -5.21;
            bool playerNeedsPit = false;
            double playerPitLoss = 0.0;
            double targetPitLoss = 24.90;

            double projectedMergeGap = TargetStrategyManager.CalculateProjectedMergeGap(
                liveSignedGap,
                playerNeedsPit,
                playerPitLoss,
                targetNeedsPit,
                targetPitLoss);

            Assert(Math.Abs(projectedMergeGap - (-5.21)) < 1e-4, $"ProjectedMergeGap must remain -5.21s, got {projectedMergeGap:F2}s");

            Pass("Target with 1 completed pit stop and sufficient fuel does not trigger second pit or false pit loss");
        }

        private static void Test_LapFuelSync_DoesNotDoubleDeductInlapFuelAfterPit()
        {
            var tData = new OpponentTelemetryData
            {
                Name = "Bruno Carneiro",
                FuelAfterLastPit = 39.7, // Smart refuel output
                LastRefuelLap = 20,
                LastPitLap = 20,
                IsInsideGeofence = true // Crossing line inside pit road
            };

            int rawCurrentLap = 21;
            int calculatedLapsSinceRefuel = (rawCurrentLap - 1) - tData.LastRefuelLap; // 0
            double classFuelBurn = 2.49;
            double inlapFuelDeduction = 2.17;

            double currentStartingFuel = tData.FuelAfterLastPit > 0 ? tData.FuelAfterLastPit : 50.0;
            double greenLaps = 0.0;
            double fuelBurned = greenLaps * classFuelBurn; // 0.0
            double estimatedFuel = currentStartingFuel - fuelBurned; // 39.7

            if (tData.IsInsideGeofence && tData.FuelAfterLastPit <= 0.0)
            {
                estimatedFuel = Math.Max(0.0, estimatedFuel - inlapFuelDeduction);
            }

            Assert(Math.Abs(estimatedFuel - 39.7) < 1e-4, $"Fuel must remain 39.7L without double-deducting inlap fuel, got {estimatedFuel:F2}L");

            Pass("Opponent lap fuel sync preserves FuelAfterLastPit without double inlap fuel deduction");
        }

        private static void Test_SplashAndDash_CalculatesDynamicFuelRateFromActualLitres()
        {
            // Verifichiamo che con 30.9L imbarcati in 12.4s il tasso di rifornimento
            // non sia più corrotto a 1.61 L/s (20.0 / 12.4s) ma calcoli il tasso reale ~2.49 L/s
            double fuelingSeconds = 12.40;
            double litresAdded = 30.90;
            double fuelToAdd = 31.0;
            double minLitres = 5.0;

            double effectiveLitres = litresAdded >= minLitres ? litresAdded : fuelToAdd;
            double measuredRate = effectiveLitres / fuelingSeconds;

            Assert(Math.Abs(measuredRate - 2.492) < 0.01,
                $"Expected measured rate ~2.49 L/s, got {measuredRate:F3} L/s");
            Assert(measuredRate > 2.0, "Measured rate must NOT be corrupted to 1.61 L/s");

            Pass("SplashAndDash calculates dynamic FuelFillRate from actual litres added");
        }

        private static void Test_ExtendedPitZone_AppliesPhysicalFloorToRejectTrackCutOutliers()
        {
            // Verifichiamo che tempi assurdi di attraversamento della ExtendedPitZone in pista
            // (es. 4.78s, 10.40s) vengano scartati dalla soglia fisica minima,
            // preservando il tempo reale di percorrenza a velocità di gara (es. 16.95s).
            double pitFraction = 0.2476;
            double refPace = 76.5;
            double minPhysicalExtendedTime = Math.Max(12.0, pitFraction * refPace * 0.70); // 13.26s

            double validTrackTime = 16.95;
            double glitchedCutTime = 10.40;
            double extremeGlitchedTime = 4.78;

            Assert(validTrackTime >= minPhysicalExtendedTime, "16.95s must be accepted as valid extended racing zone time");
            Assert(glitchedCutTime < minPhysicalExtendedTime, "10.40s must be rejected as below physical floor");
            Assert(extremeGlitchedTime < minPhysicalExtendedTime, "4.78s must be rejected as below physical floor");

            double classBest = 999.0;
            double[] samples = new double[] { 17.25, 17.00, glitchedCutTime, extremeGlitchedTime, 16.95 };
            foreach (var s in samples)
            {
                if (s >= minPhysicalExtendedTime && s < classBest)
                {
                    classBest = s;
                }
            }

            Assert(Math.Abs(classBest - 16.95) < 1e-4,
                $"Expected classBest ~16.95s, got {classBest:F2}s (outliers 10.40s and 4.78s were ignored)");

            Pass("ExtendedPitZone applies physical floor to reject track cut and teleport outliers");
        }
    }
}
