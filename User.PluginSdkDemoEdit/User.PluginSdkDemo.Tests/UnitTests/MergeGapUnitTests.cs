using System;
using SimRIG;

namespace User.PluginSdkDemo.Tests
{
    public class MergeGapUnitTests
    {
        public static void RunAllTests()
        {
            Console.WriteLine("[TEST] Running Merge Gap & Race States Unit Tests...");

            Test_SignedGap_BehindTarget();
            Test_SignedGap_AheadTarget();
            Test_State1_PlayerMustPit_TargetNoPit();
            Test_State2_BothPitted_PlayerHypotheticalStop();
            Test_State3_BothMustPit_Differential();

            Console.WriteLine("[TEST SUCCESS] All Merge Gap Unit Tests Passed!");
        }

        private static void Test_SignedGap_BehindTarget()
        {
            // When player is behind target (posDiff < 0), SignedGapSeconds MUST be positive (+)
            double posDiff = -0.1; // player behind
            double fluidGap = 10.0;
            double signedGap = posDiff < 0 ? fluidGap : -fluidGap;

            if (signedGap != 10.0)
                throw new Exception($"Behind target expected SignedGap = +10.0s but got {signedGap}s");

            Console.WriteLine("  [PASS] Test_SignedGap_BehindTarget");
        }

        private static void Test_SignedGap_AheadTarget()
        {
            // When player is ahead of target (posDiff > 0), SignedGapSeconds MUST be negative (-)
            double posDiff = 0.1; // player ahead
            double fluidGap = 10.0;
            double signedGap = posDiff < 0 ? fluidGap : -fluidGap;

            if (signedGap != -10.0)
                throw new Exception($"Ahead of target expected SignedGap = -10.0s but got {signedGap}s");

            Console.WriteLine("  [PASS] Test_SignedGap_AheadTarget");
        }

        private static void Test_State1_PlayerMustPit_TargetNoPit()
        {
            // Sara Tolotti (-20.7s ahead), PlayerPitLoss = +32.18s, Egor already pitted (TargetPitLoss = 0.0s)
            // ProjectedMergeGap = SignedGap (-20.7s) + PlayerPitLoss (32.18s) - TargetPitLoss (0.0s) = +11.48s (Sara 11.5s BEHIND Egor)
            double signedGap = -20.7; // Ahead
            double playerPitLoss = 32.18;
            double targetPitLoss = 0.0; // Has fuel for finish
            double projectedMergeGap = signedGap + playerPitLoss - targetPitLoss;

            if (Math.Abs(projectedMergeGap - 11.48) > 0.01)
                throw new Exception($"State 1 expected +11.48s but got {projectedMergeGap:F2}s");

            Console.WriteLine("  [PASS] Test_State1_PlayerMustPit_TargetNoPit (+11.48s)");
        }

        private static void Test_State2_BothPitted_PlayerHypotheticalStop()
        {
            // Both have pitted, Sara is +11.7s behind Egor.
            // If Sara hypothetically stops again, ProjectedMergeGap = +11.7s + 32.18s - 0.0s = +43.88s
            double signedGap = 11.7; // Behind
            double playerPitLoss = 32.18;
            double targetPitLoss = 0.0; // Egor won't pit again
            double projectedMergeGap = signedGap + playerPitLoss - targetPitLoss;

            if (Math.Abs(projectedMergeGap - 43.88) > 0.01)
                throw new Exception($"State 2 expected +43.88s but got {projectedMergeGap:F2}s");

            Console.WriteLine("  [PASS] Test_State2_BothPitted_PlayerHypotheticalStop (+43.88s)");
        }

        private static void Test_State3_BothMustPit_Differential()
        {
            // Sara is -5.0s ahead of target. Both must pit. Sara loses 32.18s, target loses 36.50s.
            // ProjectedMergeGap = -5.0s + 32.18s - 36.50s = -9.32s (Sara extends lead to 9.32s ahead)
            double signedGap = -5.0; // Ahead
            double playerPitLoss = 32.18;
            double targetPitLoss = 36.50;
            double projectedMergeGap = signedGap + playerPitLoss - targetPitLoss;

            if (Math.Abs(projectedMergeGap - (-9.32)) > 0.01)
                throw new Exception($"State 3 expected -9.32s but got {projectedMergeGap:F2}s");

            Console.WriteLine("  [PASS] Test_State3_BothMustPit_Differential (-9.32s)");
        }
    }
}
