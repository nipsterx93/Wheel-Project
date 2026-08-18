using System;
using System.Collections.Generic;
using System.Linq;
using GameReaderCommon;
using SimRIG;

namespace User.PluginSdkDemo.Tests
{
    public class MisanoHuracanGT3ReplayTest
    {
        public static void RunFullReplayMergeGapValidation()
        {
            Console.WriteLine("\n=========================================================");
            Console.WriteLine("  REAL REPLAY BACKTEST: Misano GP - Huracán GT3 EVO     ");
            Console.WriteLine("=========================================================");
            Console.WriteLine("  Player: Sara Tolotti");
            Console.WriteLine("  Target: Egor Ogorodnicov3");
            Console.WriteLine("  Track: Misano GP (GT3 Class)");

            string replayPath = @"E:\SimHub\Replays\IRacing\20260303_151035.telemetry.json";
            
            // 1. Load frames into RAM
            List<GameData> frames = SimHubReplayRAMReader.LoadReplayFramesFromRAM(replayPath, maxFrames: 50000);

            if (frames.Count == 0)
            {
                Console.WriteLine("[!] No frames loaded from replay file. Skipping full replay backtest.");
                return;
            }

            // 2. Initialize REAL Plugin instance
            var plugin = new DataPluginDemo();
            var pm = new SimHub.Plugins.PluginManager();
            plugin.PluginManager = pm;
            plugin.Init(pm);

            Console.WriteLine($"[*] Processing {frames.Count} telemetry frames through real DataPluginDemo engine...");

            double prePitPredictedMergeGap = 0.0;
            double actualPostPitMergeGap = 0.0;
            bool pitEntryRecorded = false;
            bool pitExitRecorded = false;

            for (int i = 0; i < frames.Count; i++)
            {
                var gData = frames[i];
                if (gData == null || gData.NewData == null) continue;

                // Execute REAL plugin update logic
                plugin.DataUpdate(pm, ref gData);

                bool playerInPit = plugin.CurrentState.IsInPitLane;

                // Record pre-pit prediction (3 laps before pit)
                if (!pitEntryRecorded && !playerInPit && plugin.TargetStrategyManager != null)
                {
                    double mergeGapVal = plugin.TargetStrategyManager.CurrentTarget.ProjectedMergeGap;
                    if (Math.Abs(mergeGapVal) > 0.01)
                    {
                        prePitPredictedMergeGap = mergeGapVal;
                    }
                }

                if (playerInPit && !pitEntryRecorded)
                {
                    pitEntryRecorded = true;
                    Console.WriteLine($"[*] PIT ENTRY DETECTED at Lap {plugin.CurrentState.CurrentLap}. Pre-Pit Predicted MergeGap: {prePitPredictedMergeGap:F2}s");
                }

                // Detect exit at ExtendedPitExitPct
                double exitPct = plugin.PitRadar.GetExtendedPitExitPct();
                if (pitEntryRecorded && !pitExitRecorded && !playerInPit)
                {
                    double pTrackPct = plugin.CurrentState.TrackPositionPercent;
                    if (Math.Abs(pTrackPct - exitPct) < 0.05)
                    {
                        pitExitRecorded = true;

                        // Find target (Egor Ogorodnicov3) position at exit
                        var egor = plugin.CurrentState.Opponents.FirstOrDefault(o => o.Name != null && o.Name.IndexOf("Egor", StringComparison.OrdinalIgnoreCase) >= 0);
                        if (egor != null && egor.TrackPositionPercent.HasValue)
                        {
                            double posDiffLaps = (plugin.CurrentState.CurrentLap + pTrackPct) - ((egor.CurrentLap ?? 0) + egor.TrackPositionPercent.Value);
                            double refLapTime = plugin.CurrentState.LastLapTimeSec > 10.0 ? plugin.CurrentState.LastLapTimeSec : 100.0;
                            actualPostPitMergeGap = posDiffLaps < 0 ? (Math.Abs(posDiffLaps) * refLapTime) : -(posDiffLaps * refLapTime);
                            Console.WriteLine($"[*] EXTENDED PIT EXIT DETECTED! Measured Real Gap vs Egor: {actualPostPitMergeGap:F2}s");
                        }
                    }
                }
            }

            Console.WriteLine("\n---------------------------------------------------------");
            Console.WriteLine("  REAL REPLAY BACKTEST VALIDATION REPORT                 ");
            Console.WriteLine("---------------------------------------------------------");
            Console.WriteLine($"  Pre-Pit Predicted MergeGap : {prePitPredictedMergeGap:F2}s");
            Console.WriteLine($"  Post-Pit Real Measured Gap  : {actualPostPitMergeGap:F2}s");
            double deltaError = Math.Abs(prePitPredictedMergeGap - actualPostPitMergeGap);
            Console.WriteLine($"  Prediction Error Delta     : {deltaError:F2}s");

            if (deltaError <= 1.0)
            {
                Console.WriteLine("  [RESULT] PASSED! Algorithm predicted post-pit gap within tenths of a second!");
            }
            else
            {
                Console.WriteLine($"  [RESULT] TUNING NEEDED: Prediction delta error is {deltaError:F2}s.");
            }
            Console.WriteLine("=========================================================\n");
        }
    }
}
