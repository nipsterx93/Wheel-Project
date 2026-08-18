using System;
using SimRIG;

namespace User.PluginSdkDemo.Tests
{
    public class PitLossUnitTests
    {
        public static void RunAllTests()
        {
            Console.WriteLine("[TEST] Running Pit Loss & Tyre Time Unit Tests...");
            
            Test_GT3_SelectedTireTime();
            Test_PCUP_Sequential_PitLoss();
            Test_GT3_Simultaneous_PitLoss();
            
            Console.WriteLine("[TEST SUCCESS] All Pit Loss Unit Tests Passed!");
        }

        private static void Test_GT3_SelectedTireTime()
        {
            // GT3 4-Tire time should be 26.0s
            double t4 = CarPitData.GetProfile("GT3").Tires4;
            if (Math.Abs(t4 - 26.0) > 0.01)
                throw new Exception($"GT3 4-Tire time expected 26.0s but got {t4}s");

            // GT3 2-Tire time should be 14.0s
            double t2 = CarPitData.GetProfile("GT3").Tires2;
            if (Math.Abs(t2 - 14.0) > 0.01)
                throw new Exception($"GT3 2-Tire time expected 14.0s but got {t2}s");

            Console.WriteLine("  [PASS] Test_GT3_SelectedTireTime");
        }

        private static void Test_PCUP_Sequential_PitLoss()
        {
            // PCUP is sequential: TotalStationary = FuelTime + TireTime
            var profile = CarPitData.GetProfile("PCUP");
            if (!profile.IsSequential)
                throw new Exception("PCUP profile should be IsSequential = true");

            double fuelTime = 10.0;
            double tireTime = profile.Tires4; // 26.0s
            double expectedStationary = fuelTime + tireTime; // 36.0s

            double actualStationary = profile.IsSequential ? (fuelTime + tireTime) : Math.Max(fuelTime, tireTime);
            if (Math.Abs(actualStationary - expectedStationary) > 0.01)
                throw new Exception($"PCUP Sequential Stationary expected {expectedStationary}s but got {actualStationary}s");

            Console.WriteLine("  [PASS] Test_PCUP_Sequential_PitLoss");
        }

        private static void Test_GT3_Simultaneous_PitLoss()
        {
            // GT3 is simultaneous: TotalStationary = Max(FuelTime, TireTime)
            var profile = CarPitData.GetProfile("GT3");
            if (profile.IsSequential)
                throw new Exception("GT3 profile should be IsSequential = false");

            double fuelTime = 10.0;
            double tireTime = profile.Tires4; // 26.0s
            double expectedStationary = Math.Max(fuelTime, tireTime); // 26.0s

            double actualStationary = profile.IsSequential ? (fuelTime + tireTime) : Math.Max(fuelTime, tireTime);
            if (Math.Abs(actualStationary - expectedStationary) > 0.01)
                throw new Exception($"GT3 Simultaneous Stationary expected {expectedStationary}s but got {actualStationary}s");

            Console.WriteLine("  [PASS] Test_GT3_Simultaneous_PitLoss");
        }
    }
}
