using System;

namespace User.PluginSdkDemo.Tests
{
    public class TestRunner
    {
        public static int Main(string[] args)
        {
            Console.WriteLine("=================================================");
            Console.WriteLine("  SIMRIG PLUGIN AUTOMATED TEST SUITE RUNNER  ");
            Console.WriteLine("=================================================");

            try
            {
                PaceAnchorUnitTests.RunAllTests();
                PitLossUnitTests.RunAllTests();
                PitLossOnTrackEquivalentUnitTests.RunAllTests();
                MergeGapUnitTests.RunAllTests();
                RelativePaceUnitTests.RunAllTests();
                StrategyLoggingUnitTests.RunAllTests();
                RelativeGapDeltaUnitTests.RunAllTests();
                StrategyHysteresisUnitTests.RunAllTests();
                WarmupDoubleCountUnitTests.RunAllTests();
                OvercutTrafficUnitTests.RunAllTests();
                PitLaneDetectionUnitTests.RunAllTests();
                FuelSavingUnitTests.RunAllTests();
                FuelOutlierFilterUnitTests.RunAllTests();
                GapWrapAndResolutionUnitTests.RunAllTests();
                TrackPositionValidatorUnitTests.RunAllTests();
                CalibrationConfidenceUnitTests.RunAllTests();
                GeofenceCalibrationGateUnitTests.RunAllTests();
                NaturalPitLearningUnitTests.RunAllTests();
                RaceTimeProjectionUnitTests.RunAllTests();
                CalibrationConsensusUnitTests.RunAllTests();
                OpponentLapAnchorUnitTests.RunAllTests();
                PhantomPitVisitUnitTests.RunAllTests();
                LeaderSampleUnitTests.RunAllTests();
                CalibrationCascadeUnitTests.RunAllTests();
                PlayerTotalLapsRatchetUnitTests.RunAllTests();
                ProjectionStabilizerUnitTests.RunAllTests();
                FlagMomentUnitTests.RunAllTests();
                FlagTimeWiringUnitTests.RunAllTests();
                LeaderTotalCapUnitTests.RunAllTests();
                LeaderTotalFromCommandUnitTests.RunAllTests();
                LeaderPositionMissingUnitTests.RunAllTests();
                OpponentLapTimeSourceUnitTests.RunAllTests();
                OpponentMaxTankUnitTests.RunAllTests();
                FuelWeightAndPitLossUnitTests.RunAllTests();
                ReplayBacktestIntegrationTest.RunMisanoGt3Backtest();
                MisanoHuracanGT3ReplayTest.RunFullReplayMergeGapValidation();

                Console.WriteLine("=================================================");
                Console.WriteLine("  ALL UNIT TESTS PASSED SUCCESSFULLY! (100%)    ");
                Console.WriteLine("=================================================");
                return 0;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n[TEST FAILED] {ex.Message}");
                Console.ResetColor();
                return 1;
            }
        }
    }
}
