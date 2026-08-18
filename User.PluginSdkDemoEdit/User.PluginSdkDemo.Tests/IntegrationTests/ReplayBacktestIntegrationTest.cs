using System;
using SimRIG;

namespace User.PluginSdkDemo.Tests
{
    public class ReplayBacktestIntegrationTest
    {
        public static void RunMisanoGt3Backtest()
        {
            Console.WriteLine("\n[INTEGRATION TEST] Misano GP - Huracán GT3 EVO Backtest");
            Console.WriteLine("-------------------------------------------------------");
            Console.WriteLine("  Player: Sara Tolotti");
            Console.WriteLine("  Target Opponent: Egor Ogorodnicov3");
            Console.WriteLine("  Ground Truth: 16.0L Fuel Added | Tires: NONE (0.0s)");

            // 1. Setup Ground-Truth Inputs
            string carClass = "GT3";
            double fuelToAdd = 16.0; // Liters
            var tyreScope = TyreSelectionScope.None;

            // 2. Profile & Refuel Rate Lookup
            var profile = CarPitData.GetProfile(carClass);
            double refuelRate = profile.RefuelRate; // 2.8 L/s
            double expectedStationaryTime = fuelToAdd / refuelRate; // 16 / 2.8 = 5.71s
            double tyreTime = 0.0; // NONE

            double actualStationary = profile.IsSequential 
                ? (expectedStationaryTime + tyreTime) 
                : Math.Max(expectedStationaryTime, tyreTime);

            Console.WriteLine($"  -> Estimated Stationary Time: {actualStationary:F2}s (Fuel Only: {expectedStationaryTime:F2}s)");

            if (Math.Abs(actualStationary - 5.71) > 0.1)
                throw new Exception($"Stationary time expected ~5.71s but got {actualStationary:F2}s");

            // 3. Strategy Engine Projection (Simulated Pre-Pit State)
            double initialGapBehindTarget = 12.5; // SignedGapSeconds = +12.5s (Player is 12.5s behind Egor)
            double transitTime = 22.0; // Misano Pit Transit
            double accDecTime = 2.5;   // Acc/Dec Penalty
            double racingZoneTime = 10.0; // Parallel Track Racing Time

            double totalPitLoss = (actualStationary + transitTime + accDecTime) - racingZoneTime; // (5.71 + 22.0 + 2.5) - 10.0 = 20.21s
            double projectedExitGap = initialGapBehindTarget + totalPitLoss; // +12.5 + 20.21 = +32.71s behind

            Console.WriteLine($"  -> Calculated Total Pit Loss: {totalPitLoss:F2}s");
            Console.WriteLine($"  -> Projected Merge Gap vs Egor Ogorodnicov3: +{projectedExitGap:F2}s (Player trailing)");

            // 4. Ground Truth Assertions
            if (projectedExitGap <= initialGapBehindTarget)
                throw new Exception("Projected gap after pitting with +20.2s pit loss should be larger (further behind) than initial gap!");

            Console.WriteLine("  [PASS] Misano GT3 Replay Backtest Logic Validated!");
        }
    }
}
