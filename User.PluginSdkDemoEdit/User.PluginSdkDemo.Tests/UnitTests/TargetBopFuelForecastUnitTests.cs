// -------------------------------------------------------------------------
// FILE: TargetBopFuelForecastUnitTests.cs
// Y-61, passo 1 di .ai/plans/2026-09-13-daytona-piano-correzioni.md
//
// Il calcolo MergeGap/undercut prevedeva la sosta del Target col consumo e col
// serbatoio del Player. Il modello avversari conosce gia' il consumo del Target
// proporzionato al BoP ("GreenBurn" in OpponentTracker), ma lo teneva in una
// variabile locale.
//
// Numeri veri dal replay Daytona 20260913_163743, giro 15:
//   Target Lamborghini Huracan GT3 EVO: 60 L col BoP, GreenBurn 3.60 L/giro
//   Player Porsche 911 GT3 R (992): 50 L col BoP, 3.0 L/giro
//   EstimatedFuel del Target 6.9 L, giri rimanenti 11.1, fill rate 2.50 L/s
//   MergeGapLog:1079  Target (+34.40s) : Staz: 12.79s   (col consumo del Player)
//   MergeGapLog:1081  ProjectedMergeGap: 0.43s          (reale dopo le soste: -2.7 s)
// -------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using SimRIG;

namespace User.PluginSdkDemo.Tests
{
    public class TargetBopFuelForecastUnitTests
    {
        private const double DaytonaTargetTank = 60.0;
        private const double DaytonaTargetBopBurn = 3.60;
        private const double DaytonaPlayerTankBoP = 50.0;
        private const double DaytonaPlayerBurn = 3.0;
        private const double DaytonaFillRate = 2.50;

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception("[TargetBopFuel] " + message);
        }

        private static void Pass(string name)
        {
            Console.WriteLine("  [PASS] " + name);
        }

        public static void RunAllTests()
        {
            Console.WriteLine("[TEST] Running Target BoP Fuel Forecast Tests (Y-61)...");

            // I test di questa suite girano tutti anche se uno fallisce: neutralizzando le correzioni
            // (ADR-004) un solo giro mostra quali diventano rossi, invece di fermarsi al primo.
            var failures = new List<string>();
            RunCollecting(Test_Regression_TargetForecastUsesTargetBopBurn_Daytona, failures);
            RunCollecting(Test_Regression_FuelToAddCappedByTargetTankCapacity, failures);
            RunCollecting(Test_WithoutOpponentFuelModelFallsBackToPlayer, failures);
            RunCollecting(Test_Regression_OnlyBopScaledBurnIsExposedForStrategy, failures);
            if (failures.Count > 0) throw new Exception(string.Join(" || ", failures));

            Console.WriteLine("[TEST SUCCESS] All Target BoP Fuel Forecast Tests Passed!");
        }

        private static void RunCollecting(Action test, List<string> failures)
        {
            try { test(); }
            catch (Exception ex) { failures.Add(ex.Message); }
        }

        /// <summary>
        /// Il caso vero del giro 15. Col consumo del Player (3.0) il Target risultava con 27.3 L da
        /// imbarcare e 12.9 s di stazionario; col suo (3.60) sono 34.1 L e 15.7 s, e il MergeGap passa
        /// da positivo a -2.4 s (reale -2.7 s).
        ///
        /// Neutralizzando la correzione (consumo del Player al posto di quello BoP) diventa rosso.
        /// </summary>
        private static void Test_Regression_TargetForecastUsesTargetBopBurn_Daytona()
        {
            var target = new OpponentTelemetryData
            {
                CarClass = "GT3",
                EstimatedFuel = 6.9,
                EstimatedFuelTank = 6.9,
                BopFuelPerLap = DaytonaTargetBopBurn,
                FuelTankCapacity = DaytonaTargetTank
            };

            var forecast = TargetStrategyManager.ForecastTargetPit(target, 0, DaytonaPlayerBurn, DaytonaPlayerTankBoP, 11.1, DaytonaFillRate);

            Assert(Math.Abs(forecast.FuelPerLap - 3.60) < 1e-6,
                $"Target burn must be its BoP burn 3.60 L/lap, got {forecast.FuelPerLap:F2}");
            // 11.1 x 3.60 + 0.3 x 3.60 - 6.9 = 34.14 L
            Assert(Math.Abs(forecast.FuelToAdd - 34.14) < 0.01,
                $"Expected fuel to add 34.14 L, got {forecast.FuelToAdd:F2}");
            // 34.14 / 2.50 + 2.0 = 15.656 s
            Assert(Math.Abs(forecast.StationaryTime - 15.656) < 0.01,
                $"Expected Target stationary 15.66 s, got {forecast.StationaryTime:F2}");
            // 6.9 / 3.60 = 1.917 giri
            Assert(Math.Abs(forecast.FuelLaps - 1.9167) < 0.001,
                $"Expected Target fuel laps 1.92, got {forecast.FuelLaps:F2}");
            Assert(Math.Abs(forecast.TankLapsRemaining - 1.9167) < 0.001,
                $"Expected Target.TankLapsRemaining 1.92, got {forecast.TankLapsRemaining:F2}");
            Assert(forecast.NeedsPit, "Target with 1.9 laps of fuel and 11.1 laps to go must need a pit stop");

            // Ricaduta sul blocco del giro 15 (MergeGapLog:1078-1081): LiveSignedGap -0.80, perdita
            // Player 35.63, transito 31.95, AccDec 11.60, ExtZone 21.93.
            // Perdita Target = 15.656 + 31.95 + 11.60 - 21.93 = 37.276 -> MergeGap = -0.80 + 35.63 - 37.276 = -2.446
            double targetLoss = CarPitData.CalculateTotalPitLoss(forecast.StationaryTime, 31.95, 11.60, 21.93);
            double mergeGap = TargetStrategyManager.CalculateProjectedMergeGap(-0.80, true, 35.63, forecast.NeedsPit, targetLoss);
            Assert(Math.Abs(mergeGap - (-2.446)) < 0.02,
                $"Expected MergeGap -2.45 s at lap 15 (log: +0.43, real: -2.7), got {mergeGap:F2}");

            Pass("Target pit forecast uses the Target BoP burn (Daytona lap 15: 34.1 L, 15.7 s, MergeGap -2.4 s)");
        }

        /// <summary>
        /// Il tetto sul carburante da imbarcare e' la capienza del serbatoio del Target: non quella del
        /// Player, e nemmeno lo spazio libero di adesso, perche' la sosta prevista arriva giri dopo, a
        /// serbatoio quasi vuoto.
        ///
        /// Regressione vista nel replay Daytona 20260914_070557, primo blocco del MergeGapLog sul Target
        /// (TL 2652.5, giro 1): FuelLaps 16.3 x 3.57 = 58.19 L a bordo, 26.8 giri alla fine. Col tetto sullo
        /// spazio libero (60 - 58.19 = 1.81 L) lo stazionario previsto era 2.7 s (loggato 2.67) e l'errore del
        /// MergeGap arrivava a +15.5 s nei giri 2-7. Con la capienza sono 38.56 L e 17.4 s; all'ingresso box
        /// il modello avversari prevedera' 37.3 L (DebugLog 070557:6361).
        ///
        /// Neutralizzando la correzione (spazio libero, oppure capienza del Player) diventa rosso.
        /// </summary>
        private static void Test_Regression_FuelToAddCappedByTargetTankCapacity()
        {
            // Inizio stint: serbatoio quasi pieno, sosta ancora lontana
            var fullTank = new OpponentTelemetryData
            {
                CarClass = "GT3",
                EstimatedFuel = 58.19,
                EstimatedFuelTank = 58.19,
                BopFuelPerLap = 3.57,
                FuelTankCapacity = DaytonaTargetTank
            };

            var early = TargetStrategyManager.ForecastTargetPit(fullTank, 0, DaytonaPlayerBurn, DaytonaPlayerTankBoP, 26.8, DaytonaFillRate);

            // 26.8 x 3.57 + 0.3 x 3.57 - 58.19 = 38.557 L, sotto i 60 L del serbatoio
            Assert(Math.Abs(early.FuelToAdd - 38.557) < 0.01,
                $"Early in the stint fuel to add must be the fuel deficit 38.56 L, not the current free tank space 1.81 L, got {early.FuelToAdd:F2}");
            // 38.557 / 2.50 + 2.0 = 17.423 s
            Assert(Math.Abs(early.StationaryTime - 17.423) < 0.01,
                $"Expected early-stint Target stationary 17.42 s, got {early.StationaryTime:F2}");

            // Serbatoio quasi vuoto e tanta gara ancora (ipotetico coi parametri di Daytona): a 20 giri dalla
            // fine con 4 L a bordo servono 69.1 L, ma ne entrano al massimo 60 (il serbatoio del Player ne ha 50)
            var nearlyEmpty = new OpponentTelemetryData
            {
                CarClass = "GT3",
                EstimatedFuel = 4.0,
                EstimatedFuelTank = 4.0,
                BopFuelPerLap = DaytonaTargetBopBurn,
                FuelTankCapacity = DaytonaTargetTank
            };

            var late = TargetStrategyManager.ForecastTargetPit(nearlyEmpty, 0, DaytonaPlayerBurn, DaytonaPlayerTankBoP, 20.0, DaytonaFillRate);

            // richiesti 20 x 3.60 + 0.3 x 3.60 - 4.0 = 69.08 L -> tetto 60 L del Target
            Assert(Math.Abs(late.FuelToAdd - 60.0) < 0.01,
                $"Fuel to add must be capped at the Target tank capacity 60.00 L, got {late.FuelToAdd:F2}");
            // 60 / 2.50 + 2.0 = 26.0 s
            Assert(Math.Abs(late.StationaryTime - 26.0) < 0.01,
                $"Expected Target stationary 26.00 s, got {late.StationaryTime:F2}");

            Pass("Target fuel to add is capped by the Target tank capacity, not by its current free space or the Player tank");
        }

        /// <summary>
        /// Senza dati dal modello avversari (vettura di un'altra classe, consumo del Player non ancora
        /// disponibile) resta il comportamento di prima: consumo e capienza del Player. Il consumo del
        /// Player vale 2.975 L/giro (DebugLog:6437 `Burn: 3.57` del Target = 2.975 x 60 / 50), diverso
        /// dal ripiego fisso di 3.0: un ripiego sulla costante al posto del Player farebbe rosso il test.
        /// </summary>
        private static void Test_WithoutOpponentFuelModelFallsBackToPlayer()
        {
            var target = new OpponentTelemetryData
            {
                CarClass = "GT3",
                EstimatedFuel = 6.9,
                EstimatedFuelTank = 6.9
            };

            var forecast = TargetStrategyManager.ForecastTargetPit(target, 0, 2.975, DaytonaPlayerTankBoP, 11.1, DaytonaFillRate);

            Assert(Math.Abs(forecast.FuelPerLap - 2.975) < 1e-6,
                $"Without a BoP burn the Target must use the Player burn 2.975 L/lap, got {forecast.FuelPerLap:F3}");
            // 11.1 x 2.975 + 0.3 x 2.975 - 6.9 = 27.015 L
            Assert(Math.Abs(forecast.FuelToAdd - 27.015) < 0.01,
                $"Expected fuel to add 27.02 L, got {forecast.FuelToAdd:F2}");
            // 27.015 / 2.50 + 2.0 = 12.806 s
            Assert(Math.Abs(forecast.StationaryTime - 12.806) < 0.01,
                $"Expected Target stationary 12.81 s, got {forecast.StationaryTime:F2}");
            // 6.9 / 2.975 = 2.319 giri
            Assert(Math.Abs(forecast.TankLapsRemaining - 2.3193) < 0.001,
                $"Expected Target.TankLapsRemaining 2.32, got {forecast.TankLapsRemaining:F2}");

            // Nemmeno il consumo del Player: 3.0 L/giro per la sosta, autonomia sconosciuta (99)
            var noBurn = TargetStrategyManager.ForecastTargetPit(target, 0, 0.0, DaytonaPlayerTankBoP, 11.1, DaytonaFillRate);
            Assert(Math.Abs(noBurn.FuelPerLap - 3.0) < 1e-6,
                $"Without any burn the stop forecast must use 3.0 L/lap, got {noBurn.FuelPerLap:F2}");
            Assert(Math.Abs(noBurn.TankLapsRemaining - 99.0) < 1e-6,
                $"Without any burn Target.TankLapsRemaining must stay 99, got {noBurn.TankLapsRemaining:F2}");

            Pass("Without opponent fuel data the Target forecast falls back to the Player burn and tank");
        }

        /// <summary>
        /// Il modello avversari espone per la strategia solo il consumo che viene dalla regola BoP
        /// (consumo del Player, o del database, x capienza avversario / capienza BoP del Player). Dove usa
        /// una costante (2.5 L/giro senza dati, 3.0 per le altre classi) espone 0, e il calcolo del Target
        /// ricade sul Player come prima invece di adottare la costante.
        ///
        /// Neutralizzando la correzione (non esporre nulla, come prima) i casi BoP diventano rossi.
        /// I valori verdi e gialli fissano il comportamento del modello avversari, estratto da
        /// OpponentTracker.Update senza modifiche.
        /// </summary>
        private static void Test_Regression_OnlyBopScaledBurnIsExposedForStrategy()
        {
            // Stessa classe, consumo del Player pronto (Daytona: GreenBurn 3.60, giallo 3.60 x 0.6)
            var live = OpponentTracker.ResolveOpponentFuelBurn("GT3", "GT3", true, DaytonaPlayerBurn, 0.0, 0.0, DaytonaTargetTank, DaytonaPlayerTankBoP);
            AssertBurn(live, 3.60, 2.16, 3.60, "same class, live Player burn");

            // Consumo della formation lap noto (ipotetico 1.5 L): giallo = 60 x 1.5 / 50
            var formation = OpponentTracker.ResolveOpponentFuelBurn("GT3", "GT3", true, DaytonaPlayerBurn, 1.5, 0.0, DaytonaTargetTank, DaytonaPlayerTankBoP);
            AssertBurn(formation, 3.60, 1.80, 3.60, "same class, formation lap burn known");

            // Primi giri: consumo dal database (ipotetico 3.0) scalato col BoP
            var fromDb = OpponentTracker.ResolveOpponentFuelBurn("GT3", "GT3", false, DaytonaPlayerBurn, 0.0, 3.0, DaytonaTargetTank, DaytonaPlayerTankBoP);
            AssertBurn(fromDb, 3.60, 2.16, 3.60, "same class, database burn before the Player latch");

            // Primi giri senza database: costante 2.5, non esposta
            var constant = OpponentTracker.ResolveOpponentFuelBurn("GT3", "GT3", false, DaytonaPlayerBurn, 0.0, 0.0, DaytonaTargetTank, DaytonaPlayerTankBoP);
            AssertBurn(constant, 2.5, 1.5, 0.0, "same class, no burn data");

            // Capienza BoP del Player sconosciuta: consumo del database non scalato, non esposto
            var unscaled = OpponentTracker.ResolveOpponentFuelBurn("GT3", "GT3", true, DaytonaPlayerBurn, 0.0, 3.0, DaytonaTargetTank, 0.0);
            AssertBurn(unscaled, 3.0, 1.8, 0.0, "same class, Player BoP tank unknown");

            // Altra classe (Daytona: leader GTP con 78 L e GreenBurn 3.00): costante, non esposta
            var otherClass = OpponentTracker.ResolveOpponentFuelBurn("GT3", "GTP", true, DaytonaPlayerBurn, 0.0, 0.0, 78.0, DaytonaPlayerTankBoP);
            AssertBurn(otherClass, 3.0, 1.8, 0.0, "other class");

            // Classe del Player sconosciuta: nessun consumo
            var noClass = OpponentTracker.ResolveOpponentFuelBurn("DEFAULT", "GT3", true, DaytonaPlayerBurn, 0.0, 0.0, DaytonaTargetTank, DaytonaPlayerTankBoP);
            AssertBurn(noClass, 0.0, 0.0, 0.0, "Player class unknown");

            Pass("Opponent fuel model exposes only the BoP-scaled burn to the Target strategy");
        }

        private static void AssertBurn(OpponentTracker.OpponentFuelBurn burn, double green, double yellow, double exposed, string scenario)
        {
            Assert(Math.Abs(burn.GreenPerLap - green) < 1e-6,
                $"{scenario}: expected green burn {green:F2}, got {burn.GreenPerLap:F2}");
            Assert(Math.Abs(burn.YellowPerLap - yellow) < 1e-6,
                $"{scenario}: expected yellow burn {yellow:F2}, got {burn.YellowPerLap:F2}");
            Assert(Math.Abs(burn.BopScaledGreenPerLap - exposed) < 1e-6,
                $"{scenario}: expected exposed BoP burn {exposed:F2}, got {burn.BopScaledGreenPerLap:F2}");
        }
    }
}
