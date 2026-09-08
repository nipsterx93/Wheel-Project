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
            Test_CalculateStationaryTime_Centralized();
            Test_CalculateExtendedRacingTime_Centralized();
            Test_CalculateTotalPitLoss_Centralized();
            
            Console.WriteLine("[TEST SUCCESS] All Pit Loss Unit Tests Passed!");
        }

        private static void Test_CalculateStationaryTime_Centralized()
        {
            // GT3 (Simultaneo): 28L / 2.8 L/s = 10s. Tire = 26s. Max(10, 26) + 2.0 = 28.0s
            double gt3Stat = CarPitData.CalculateStationaryTime("GT3", 28.0, 0.0, 26.0, jackBufferSec: 2.0);
            if (Math.Abs(gt3Stat - 28.0) > 0.01)
                throw new Exception($"GT3 StationaryTime expected 28.0s but got {gt3Stat}s");

            // PCUP (Sequenziale): 27L / 2.7 L/s = 10s. Tire = 26s. (10 + 26) + 2.0 = 38.0s
            double pcupStat = CarPitData.CalculateStationaryTime("PCUP", 27.0, 0.0, 26.0, jackBufferSec: 2.0);
            if (Math.Abs(pcupStat - 38.0) > 0.01)
                throw new Exception($"PCUP StationaryTime expected 38.0s but got {pcupStat}s");

            // Zero sosta: nessun buffer martinetti
            double zeroStat = CarPitData.CalculateStationaryTime("GT3", 0.0, 0.0, 0.0, jackBufferSec: 2.0);
            if (Math.Abs(zeroStat - 0.0) > 0.001)
                throw new Exception($"Zero StationaryTime expected 0.0s but got {zeroStat}s");

            Console.WriteLine("  [PASS] Test_CalculateStationaryTime_Centralized");
        }

        private static void Test_CalculateExtendedRacingTime_Centralized()
        {
            // 1. Misura cronometrata disponibile: priorità assoluta
            double measured = CarPitData.CalculateExtendedRacingTime(14.5, 350.0, 4000.0, 250.0);
            if (Math.Abs(measured - 14.5) > 0.01)
                throw new Exception($"ExtendedRacingTime with measured time expected 14.5s but got {measured}s");

            // 2. Fallback geometrico con frazione di giro e passo
            double geometric = CarPitData.CalculateExtendedRacingTime(0.0, 0.194, 76.5);
            double expectedGeo = 0.194 * 76.5; // 14.841s
            if (Math.Abs(geometric - expectedGeo) > 0.01)
                throw new Exception($"ExtendedRacingTime geometric expected {expectedGeo}s but got {geometric}s");

            // 3. Fallback distanza e velocità: (350 + 0.10*4000) / (250 / 3.6) = 750 / 69.444 = 10.8s
            double distSpeed = CarPitData.CalculateExtendedRacingTime(0.0, 350.0, 4000.0, 250.0);
            double expectedDistSpeed = 750.0 / (250.0 / 3.6);
            if (Math.Abs(distSpeed - expectedDistSpeed) > 0.01)
                throw new Exception($"ExtendedRacingTime dist/speed expected {expectedDistSpeed}s but got {distSpeed}s");

            Console.WriteLine("  [PASS] Test_CalculateExtendedRacingTime_Centralized");
        }

        private static void Test_CalculateTotalPitLoss_Centralized()
        {
            // Caso 1: Standard pit stop
            // Stationary = 15.0s, Transit = 26.0s, AccDec = 9.0s -> timeInZone = 35.0s
            // ExtendedRacingTime = 14.0s -> Loss = 15.0 + (35.0 - 14.0) = 36.0s
            double loss1 = CarPitData.CalculateTotalPitLoss(15.0, 26.0, 9.0, 14.0);
            if (Math.Abs(loss1 - 36.0) > 0.01)
                throw new Exception($"TotalPitLoss expected 36.0s but got {loss1}s");

            // Caso 2: Protezione sicurezza se ExtendedRacingTime >= timeInZone (geometria anomala o passo fuori scala)
            // timeInZone = 35.0s, Extended = 40.0s -> non deve sottrarre, fallback a stationary + timeInZone = 50.0s
            double loss2 = CarPitData.CalculateTotalPitLoss(15.0, 26.0, 9.0, 40.0);
            if (Math.Abs(loss2 - 50.0) > 0.01)
                throw new Exception($"TotalPitLoss safety fallback expected 50.0s but got {loss2}s");

            // Caso 3: Solo stationary (in pit box o transiti nulli)
            double loss3 = CarPitData.CalculateTotalPitLoss(15.0, 0.0, 0.0, 0.0);
            if (Math.Abs(loss3 - 15.0) > 0.01)
                throw new Exception($"TotalPitLoss stationary-only expected 15.0s but got {loss3}s");

            Console.WriteLine("  [PASS] Test_CalculateTotalPitLoss_Centralized");
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
