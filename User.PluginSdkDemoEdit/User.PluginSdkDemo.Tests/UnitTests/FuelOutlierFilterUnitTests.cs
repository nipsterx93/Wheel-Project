// -------------------------------------------------------------------------
// FILE: FuelOutlierFilterUnitTests.cs
// Verifica dell'algoritmo Interquartile Range (IQR) e della tolleranza del 15%
// derivata da irdashies (fuelCalculations.ts), con indicizzazione intera esatta
// e latch di stato (giro verde, in-lap, out-lap).
// -------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using SimRIG;

namespace User.PluginSdkDemo.Tests
{
    public class FuelOutlierFilterUnitTests
    {
        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception("[FuelOutlierFilter] " + message);
        }

        private static void Pass(string name)
        {
            Console.WriteLine("  [PASS] " + name);
        }

        public static void RunAllTests()
        {
            Console.WriteLine("[TEST] Running Fuel Outlier Filter & Latch Tests...");

            Test_FewSamples_AlwaysAccepted();
            Test_Outlier_Rejected_By_IQR_And_Tolerance();
            Test_Tolerance15Percent_Saves_ConsistentDriver();
            Test_Exact_Indexing_Matches_IrDashies();
            Test_Update_StateLatching_YellowAndPit();
            Test_CleanHistory_MaxCapacity_10();
            Test_ResetSession_ClearsAll();

            Console.WriteLine("[TEST SUCCESS] All Fuel Outlier Filter Tests Passed!");
        }

        private static void Test_FewSamples_AlwaysAccepted()
        {
            // Sotto i 3 campioni, qualsiasi consumo positivo è accettato per costruire la baseline
            var emptyHistory = new List<double>();
            Assert(FuelManager.ValidateFuelConsumptionIQR(2.5, emptyHistory), "0 campioni: deve accettare");

            var oneLap = new List<double> { 2.5 };
            Assert(FuelManager.ValidateFuelConsumptionIQR(2.4, oneLap), "1 campione: deve accettare");

            var twoLaps = new List<double> { 2.5, 2.6 };
            Assert(FuelManager.ValidateFuelConsumptionIQR(1.1, twoLaps), "2 campioni: deve accettare");

            // Zero o negativo sempre scartato
            Assert(!FuelManager.ValidateFuelConsumptionIQR(0.0, twoLaps), "0.0 L deve essere rifiutato");
            Assert(!FuelManager.ValidateFuelConsumptionIQR(-1.0, twoLaps), "Negativo deve essere rifiutato");

            Pass("Con meno di 3 campioni la baseline accetta i consumi positivi");
        }

        private static void Test_Outlier_Rejected_By_IQR_And_Tolerance()
        {
            // Storico reale di 4 giri: [2.23, 2.25, 2.26, 2.27]
            var history = new List<double> { 2.23, 2.25, 2.26, 2.27 };

            // Giro di caution / safety car a 1.40 L (troppo basso)
            bool cautionLap = FuelManager.ValidateFuelConsumptionIQR(1.40, history);
            Assert(!cautionLap, "Giro da 1.40 L sotto caution deve essere scartato come outlier");

            // Giro anomalo a 3.50 L (troppo alto)
            bool spillLap = FuelManager.ValidateFuelConsumptionIQR(3.50, history);
            Assert(!spillLap, "Giro da 3.50 L deve essere scartato come outlier");

            // Giro di gara pulito a 2.24 L (perfettamente nel range)
            bool normalLap = FuelManager.ValidateFuelConsumptionIQR(2.24, history);
            Assert(normalLap, "Giro da 2.24 L in gara deve essere accettato");

            Pass("Giri anomali (caution da 1.4L o picchi da 3.5L) scartati da IQR");
        }

        private static void Test_Tolerance15Percent_Saves_ConsistentDriver()
        {
            // Pilota ultra-costante: 5 giri identici da 2.00 L
            // Q1 = 2.0, Q3 = 2.0 => IQR = 0.0!
            var history = new List<double> { 2.0, 2.0, 2.0, 2.0, 2.0 };

            // Un giro con traffico o scia a 2.10 L (+5%) verrebbe bocciato dall'IQR puro
            // ma DEVE essere salvato dalla tolleranza del 15% sulla media (2.0 * 0.15 = 0.30 L => range [1.70, 2.30])
            Assert(FuelManager.ValidateFuelConsumptionIQR(2.10, history), "Giro a 2.10L (+5%) deve essere salvato dalla tolleranza 15%");
            Assert(FuelManager.ValidateFuelConsumptionIQR(1.85, history), "Giro a 1.85L (-7.5%) deve essere salvato dalla tolleranza 15%");

            // Un giro oltre il 15% (es. 2.35 L, +17.5%) deve essere scartato
            Assert(!FuelManager.ValidateFuelConsumptionIQR(2.35, history), "Giro a 2.35L (+17.5%) fuori da IQR e tolleranza deve essere scartato");

            Pass("La tolleranza del 15% protegge il pilota costante dal falso positivo");
        }

        private static void Test_Exact_Indexing_Matches_IrDashies()
        {
            // Verifica che l'indicizzazione Math.Floor(count * 0.25) e Math.Floor(count * 0.75)
            // corrisponda esattamente a irdashies su 4 elementi
            var history = new List<double> { 2.23, 2.25, 2.26, 2.27 };

            // In irdashies:
            // q1Index = floor(4 * 0.25) = 1 => 2.25
            // q3Index = floor(4 * 0.75) = 3 => 2.27
            // iqr = 0.02
            // lowerBound = 2.25 - 2.0 * 0.02 = 2.21
            // upperBound = 2.27 + 2.0 * 0.02 = 2.31
            // mean = 2.2525, tolerance = 0.3378 => [1.9146, 2.5903]

            // 2.20 è < 2.21 (fuori IQR) ma è > 1.9146 (dentro la tolleranza 15%) => ACCETTATO
            Assert(FuelManager.ValidateFuelConsumptionIQR(2.20, history), "2.20 L rientra nella tolleranza 15%");

            // 1.90 è < 1.9146 e < 2.21 => SCARTATO
            Assert(!FuelManager.ValidateFuelConsumptionIQR(1.90, history), "1.90 L è fuori sia da IQR che da tolleranza 15%");

            Pass("Indicizzazione esatta Math.Floor e limiti compositi verificati");
        }

        private static void Test_Update_StateLatching_YellowAndPit()
        {
            var fm = new FuelManager();
            var state = new SessionState
            {
                IsGameRunning = true,
                CurrentLap = 1,
                CurrentFuelLevel = 50.0,
                MaxFuelCapacity = 100.0,
                IsInPitLane = false,
                Flag_Yellow = 0,
                Flag_Black = 0
            };

            // Frame iniziale giro 1
            fm.Update(state, 20.0, 0.0, null);

            // Fine giro 1 -> inizio giro 2 (consumati 2.5 L)
            state.CurrentLap = 2;
            state.CurrentFuelLevel = 47.5;
            fm.Update(state, 19.0, 0.0, null);
            Assert(fm.FuelHistory.Count == 1, "Giro 1 pulito deve entrare in cronologia");
            Assert(Math.Abs(fm.Calculations.AverageFuelPerLap - 2.5) < 1e-6, "Media deve essere 2.5L");

            // Giro 2: durante il giro la vettura entra in pit lane
            state.IsInPitLane = true;
            fm.Update(state, 18.0, 0.0, null); // tick intermedio ai box

            // Fine giro 2 -> inizio giro 3 (In-lap)
            state.CurrentLap = 3;
            state.CurrentFuelLevel = 45.0;
            fm.Update(state, 18.0, 0.0, null);
            Assert(fm.FuelHistory.Count == 1, "In-lap non deve entrare in cronologia");

            // Giro 3: la vettura esce dai box e torna in pista
            state.IsInPitLane = false;
            fm.Update(state, 17.0, 0.0, null); // tick in pista

            // Fine giro 3 -> inizio giro 4 (Out-lap)
            state.CurrentLap = 4;
            state.CurrentFuelLevel = 42.5;
            fm.Update(state, 17.0, 0.0, null);
            Assert(fm.FuelHistory.Count == 1, "Out-lap non deve entrare in cronologia");

            // Giro 4: la vettura corre in pista, ma a metà giro esce bandiera gialla
            state.Flag_Yellow = 1;
            fm.Update(state, 16.0, 0.0, null); // tick con gialla
            state.Flag_Yellow = 0; // torna verde prima del traguardo
            fm.Update(state, 16.0, 0.0, null);

            // Fine giro 4 -> inizio giro 5 (Giro con gialla dentro)
            state.CurrentLap = 5;
            state.CurrentFuelLevel = 41.0;
            fm.Update(state, 16.0, 0.0, null);
            Assert(fm.FuelHistory.Count == 1, "Giro con gialla intermedia non deve entrare in cronologia");

            // Giro 5: giro 100% verde in pista (consumati 2.4 L)
            fm.Update(state, 15.0, 0.0, null);
            state.CurrentLap = 6;
            state.CurrentFuelLevel = 38.6;
            fm.Update(state, 15.0, 0.0, null);
            Assert(fm.FuelHistory.Count == 2, "Giro 100% verde deve entrare in cronologia");
            Assert(Math.Abs(fm.Calculations.AverageFuelPerLap - 2.45) < 1e-6, "Media aggiornata correttamente a 2.45L");

            Pass("Latch di stato: in-lap, out-lap e gialla intermedia scartati correttamente");
        }

        private static void Test_CleanHistory_MaxCapacity_10()
        {
            var fm = new FuelManager();
            var state = new SessionState
            {
                IsGameRunning = true,
                CurrentLap = 1,
                CurrentFuelLevel = 100.0,
                MaxFuelCapacity = 100.0,
                IsInPitLane = false
            };
            fm.Update(state, 50.0, 0.0, null);

            // Simula 15 giri puliti consecutivi da 2.0 L
            for (int i = 2; i <= 16; i++)
            {
                state.CurrentLap = i;
                state.CurrentFuelLevel -= 2.0;
                fm.Update(state, 50.0 - i, 0.0, null);
            }

            Assert(fm.FuelHistory.Count == FuelManager.MAX_CLEAN_HISTORY_LAPS,
                   $"Capacità massima deve essere {FuelManager.MAX_CLEAN_HISTORY_LAPS}, ottenuto {fm.FuelHistory.Count}");

            Pass($"Il buffer rispetta il limite massimo di {FuelManager.MAX_CLEAN_HISTORY_LAPS} giri puliti");
        }

        private static void Test_ResetSession_ClearsAll()
        {
            var fm = new FuelManager();
            var state = new SessionState
            {
                IsGameRunning = true,
                CurrentLap = 1,
                CurrentFuelLevel = 50.0,
                MaxFuelCapacity = 100.0
            };
            fm.Update(state, 20.0, 0.0, null);
            state.CurrentLap = 2;
            state.CurrentFuelLevel = 48.0;
            fm.Update(state, 19.0, 0.0, null);
            Assert(fm.FuelHistory.Count == 1, "Dovrebbe avere 1 giro");

            fm.ResetSession();
            Assert(fm.FuelHistory.Count == 0, "ResetSession deve svuotare FuelHistory");
            Assert(!fm.WasInPitLaneDuringLap, "WasInPitLaneDuringLap deve essere false");
            Assert(!fm.WasPreviousLapPit, "WasPreviousLapPit deve essere false");
            Assert(fm.IsLapFullyGreen, "IsLapFullyGreen deve essere true");

            Pass("ResetSession ripristina tutti gli accumulatori e la cronologia");
        }
    }
}
