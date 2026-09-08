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
            Test_ConsumptionShift_ReopensFilter();
            Test_LastLapFuelUsed_IsAMeasureNotAStatistic();
            Test_FuelManager_Lap1_FreezeFuelToAdd();
            Test_FuelManager_Grid_ParadeLap_IgnoredAndGreenFlagLatched();
            Test_RaceStartLineCrossed_FuelAndLapSync();
            Test_ComputeMedian_RobustStatistics();
            Test_RoadAtlanta_MedianFuelEstimation_AntiDraft();

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

        /// <summary>
        /// Y-50 — regressione. Con la finestra dei soli accettati (commit c0be69d) un cambio
        /// reale del consumo oltre il 15% veniva rifiutato **per sempre**: la lista non si
        /// accorciava mai sui rifiuti, quindi `cleanHistory.Count < 3` non poteva piu' scattare.
        /// Simulato: 10 giri a ~2.51 L, poi 15 giri a 3.10 L (danno, pioggia, mappa aggressiva).
        /// Senza il fix la media resta a 2.51 e `FuelToAdd` sottorifornisce fino alla bandiera.
        /// </summary>
        private static void Test_ConsumptionShift_ReopensFilter()
        {
            var fm = new FuelManager();
            var state = new SessionState
            {
                IsGameRunning = true,
                CurrentLap = 1,
                CurrentFuelLevel = 200.0,
                MaxFuelCapacity = 999.0,
                IsInPitLane = false,
                Flag_Yellow = 0,
                Flag_Black = 0
            };
            double fuel = 200.0;
            fm.Update(state, 30.0, 0.0, null);

            int lap = 2;
            // Dieci giri stabili: 2.50 / 2.51 / 2.52 a rotazione. IQR stretto, media ~2.51.
            double[] baseline = { 2.50, 2.51, 2.52 };
            for (int i = 0; i < 10; i++)
            {
                fuel -= baseline[i % 3];
                state.CurrentLap = lap++;
                state.CurrentFuelLevel = fuel;
                fm.Update(state, 30.0, 0.0, null);
            }
            Assert(fm.FuelHistory.Count == 10, $"baseline: attesi 10 giri accettati, trovati {fm.FuelHistory.Count}");
            Assert(Math.Abs(fm.Calculations.AverageFuelPerLap - 2.51) < 0.02,
                   $"baseline: media attesa ~2.51, trovata {fm.Calculations.AverageFuelPerLap:F3}");

            // Il consumo reale sale a 3.10 L/giro (+23.5%): il primo giro DEVE essere rifiutato,
            // altrimenti il filtro non sta filtrando nulla.
            fuel -= 3.10;
            state.CurrentLap = lap++;
            state.CurrentFuelLevel = fuel;
            fm.Update(state, 30.0, 0.0, null);
            Assert(Math.Abs(fm.Calculations.AverageFuelPerLap - 2.51) < 0.02,
                   "il primo giro fuori scala deve essere rifiutato, la media non deve muoversi");

            // Altri 14 giri alla nuova realta'. La finestra si riempie di rifiuti, i vecchi
            // accettati vengono espulsi e sotto i tre campioni il filtro riapre da solo.
            for (int i = 0; i < 14; i++)
            {
                fuel -= 3.10;
                state.CurrentLap = lap++;
                state.CurrentFuelLevel = fuel;
                fm.Update(state, 30.0, 0.0, null);
            }

            Assert(Math.Abs(fm.Calculations.AverageFuelPerLap - 3.10) < 0.01,
                   $"dopo 15 giri a 3.10 la media deve riancorarsi a 3.10, trovata {fm.Calculations.AverageFuelPerLap:F3}");

            Pass("Y-50: un cambio reale di consumo oltre il 15% riapre il filtro invece di bloccarlo");
        }

        /// <summary>
        /// `LastLapFuelUsed` alimenta l'allarme vocale FUEL_TARGET_ALERT (DataPluginDemo.cs:1152):
        /// e' una misura del giro appena concluso, non una statistica. Deve aggiornarsi anche sui
        /// giri non rappresentativi (gialla), ma **non** sull'in-lap, dove un rifornimento
        /// parziale rende il delta di serbatoio una sottostima del consumo vero.
        /// </summary>
        private static void Test_LastLapFuelUsed_IsAMeasureNotAStatistic()
        {
            var fm = new FuelManager();
            var state = new SessionState
            {
                IsGameRunning = true,
                CurrentLap = 1,
                CurrentFuelLevel = 100.0,
                MaxFuelCapacity = 100.0,
                IsInPitLane = false,
                Flag_Yellow = 0,
                Flag_Black = 0
            };
            fm.Update(state, 30.0, 0.0, null);

            // Giro 1 pulito: 2.50 L
            state.CurrentLap = 2;
            state.CurrentFuelLevel = 97.5;
            fm.Update(state, 30.0, 0.0, null);
            Assert(Math.Abs(fm.Calculations.LastLapFuelUsed - 2.50) < 1e-6, "giro pulito: LastLapFuelUsed = 2.50");
            Assert(fm.FuelHistory.Count == 1, "giro pulito entra in cronologia");

            // Giro 2 sotto gialla: 1.80 L. E' una misura vera, va mostrata; ma non e'
            // rappresentativa, quindi resta fuori dalla statistica.
            state.Flag_Yellow = 1;
            fm.Update(state, 30.0, 0.0, null);
            state.Flag_Yellow = 0;
            state.CurrentLap = 3;
            state.CurrentFuelLevel = 95.7;
            fm.Update(state, 30.0, 0.0, null);
            Assert(Math.Abs(fm.Calculations.LastLapFuelUsed - 1.80) < 1e-6,
                   $"giro con gialla: LastLapFuelUsed deve valere 1.80, vale {fm.Calculations.LastLapFuelUsed:F2}");
            Assert(fm.FuelHistory.Count == 1, "il giro con gialla non entra in cronologia");

            // Giro 3 in-lap con rifornimento parziale: il serbatoio scende solo di 0.40 L perche'
            // nel frattempo sono stati aggiunti litri. Il numero non descrive il consumo reale.
            state.IsInPitLane = true;
            fm.Update(state, 30.0, 0.0, null);
            state.IsInPitLane = false;
            state.CurrentLap = 4;
            state.CurrentFuelLevel = 95.3;
            fm.Update(state, 30.0, 0.0, null);
            Assert(Math.Abs(fm.Calculations.LastLapFuelUsed - 1.80) < 1e-6,
                   $"in-lap: LastLapFuelUsed non deve aggiornarsi, vale {fm.Calculations.LastLapFuelUsed:F2}");
            Assert(fm.FuelHistory.Count == 1, "l'in-lap non entra in cronologia");

            Pass("LastLapFuelUsed segue la gialla ma non l'in-lap contaminato dal rifornimento");
        }

        /// <summary>
        /// Nel tratto pre-via (sprint bandiera verde verso il traguardo): FuelToAdd deve rimanere 0.0 e IsPredictionValid false.
        /// Al primo passaggio sul traguardo, il serbatoio viene fotografato sul via (RaceStartingFuel).
        /// Al termine del primo giro reale di gara (ingresso in Giro 3 in iRacing), il consumo del primo giro
        /// entra direttamente in cronologia pulita, IsPredictionValid diventa true e FuelToAdd si calcola regolarmente.
        /// </summary>
        private static void Test_FuelManager_Lap1_FreezeFuelToAdd()
        {
            var fm = new FuelManager();
            fm.Calculations.StrategyMode = FuelStrategyMode.Normal;
            var state = new SessionState
            {
                IsGameRunning = true,
                IsRaceSession = true,
                SessionStateStatus = 4, // Semaforo verde
                CurrentLap = 1,
                CurrentFuelLevel = 50.0,
                MaxFuelCapacity = 100.0,
                IsInPitLane = false,
                Flag_Yellow = 0,
                Flag_Black = 0,
                RaceStartLineCrossed = false // ancora nello sprint verso il traguardo
            };

            // Sprint pre-traguardo verso il via
            fm.Update(state, 20.0, 0.0, null);
            Assert(!fm.Calculations.IsPredictionValid, "Nel tratto pre-via IsPredictionValid deve essere false");
            Assert(fm.Calculations.FuelToAdd == 0.0, "Nel tratto pre-via FuelToAdd deve essere congelato a 0.0");
            Assert(fm.Calculations.AverageFuelPerLap == 0.0, "Nel tratto pre-via non c'e' ancora consumo medio");

            // Taglio del traguardo al via (CurrentLap passa a 2, serbatoio latched a 47.5 L)
            state.RaceStartLineCrossed = true;
            state.RaceStartLap = 2;
            state.CurrentLap = 2;
            state.CurrentFuelLevel = 47.5;
            state.RaceStartingFuel = 47.5;
            state.RaceStartingFuelLatched = true;
            fm.Update(state, 19.0, 0.0, null);

            Assert(fm.FuelHistory.Count == 0, "Lo sprint pre-via non deve entrare in cronologia");
            Assert(!fm.Calculations.IsPredictionValid, "Al via del primo giro IsPredictionValid e' ancora false");
            Assert(fm.Calculations.FuelToAdd == 0.0, "Al via del primo giro FuelToAdd resta 0.0");

            // Fine del primo giro reale di gara (CurrentLap passa a 3, consumati 2.4 L)
            state.CurrentLap = 3;
            state.CurrentFuelLevel = 45.1;
            fm.Update(state, 18.0, 0.0, null);

            Assert(Math.Abs(fm.Calculations.LastLapFuelUsed - 2.4) < 1e-6, "LastLapFuelUsed misura il consumo del primo giro reale (2.4L)");
            Assert(fm.FuelHistory.Count == 1, "Il primo giro reale di gara entra subito in cronologia");
            Assert(fm.Calculations.IsPredictionValid, "Al termine del primo giro reale IsPredictionValid diventa true");
            Assert(Math.Abs(fm.Calculations.AverageFuelPerLap - 2.4) < 1e-6, "AverageFuelPerLap deve essere 2.4L");
            // Con 25 giri mancanti: 25 * 2.4 = 60.0 L necessari. A bordo 45.1 L -> rawFuelToAdd = 14.9 L.
            fm.Update(state, 25.0, 0.0, null);
            Assert(fm.Calculations.FuelToAdd > 0.0, "Con fabbisogno superiore al serbatoio FuelToAdd deve essere > 0");
            Pass("Sprint pre-via scartato; al passaggio linea si azzera; al termine del primo giro reale entra 2.4L e popola FuelToAdd");
        }

        /// <summary>
        /// In griglia di partenza o giro di ricognizione (SessionStateStatus = 3), l'idling o le bandiere
        /// gialle non devono sporcare il consumo del Giro 1.
        /// Alla bandiera verde (SessionStateStatus = 4), il carburante di partenza viene agganciato
        /// e il consumo del Giro 1 viene calcolato pulito.
        /// </summary>
        private static void Test_FuelManager_Grid_ParadeLap_IgnoredAndGreenFlagLatched()
        {
            var fm = new FuelManager();
            var state = new SessionState
            {
                IsGameRunning = true,
                IsRaceSession = true,
                SessionStateStatus = 3, // Griglia / Parade lap
                CurrentLap = 1,
                CurrentFuelLevel = 100.0,
                MaxFuelCapacity = 100.0,
                IsInPitLane = false,
                Flag_Yellow = 1, // Bandiera gialla di ricognizione/pace car
                Flag_Black = 0
            };

            // In griglia: motore acceso che consuma carburante (100.0 -> 99.5)
            fm.Update(state, 20.0, 0.0, null);
            state.CurrentFuelLevel = 99.5;
            fm.Update(state, 20.0, 0.0, null);

            Assert(!fm.Calculations.IsPredictionValid, "In griglia IsPredictionValid deve essere false");
            Assert(fm.Calculations.FuelToAdd == 0.0, "In griglia FuelToAdd deve essere 0.0");
            Assert(!state.RaceStartLineCrossed, "In formazione la linea di partenza non e' ancora attraversata");

            // Bandiera Verde a metà pista (pos 0.62)! SessionStateStatus passa a 4
            // Ma la macchina non ha ancora tagliato il traguardo (RaceStartLineCrossed = false)
            state.SessionStateStatus = 4;
            state.Flag_Yellow = 0;
            state.CurrentFuelLevel = 99.0;
            fm.Update(state, 20.0, 0.0, null);

            Assert(!state.RaceStartLineCrossed, "Prima del traguardo RaceStartLineCrossed e' false");
            Assert(fm.FuelHistory.Count == 0, "Nessun dato di fuel durante lo sprint pre-via");

            // La vettura taglia per la prima volta il traguardo sotto bandiera verde (AbsolutePos == 0.00)
            // Latch di RaceStartingFuel = 97.0L
            state.RaceStartLineCrossed = true;
            state.RaceStartLap = 2; // in iRacing i lap salgono a 2 al via
            state.CurrentLap = 2;
            state.CurrentFuelLevel = 97.0;
            state.RaceStartingFuel = 97.0;
            state.RaceStartingFuelLatched = true;
            fm.Update(state, 20.0, 0.0, null);

            // Lo sprint pre-traguardo NON deve essere entrato in cronologia
            Assert(fm.FuelHistory.Count == 0, "Lo sprint pre-via non deve entrare in cronologia");

            // Fine giro 1 di gara reale (97.0 -> 94.6 = 2.4L)
            state.CurrentLap = 3;
            state.CurrentFuelLevel = 94.6;
            fm.Update(state, 18.0, 0.0, null);

            Assert(fm.FuelHistory.Count == 1, "Il primo giro reale di gara verde deve entrare in cronologia");
            Assert(Math.Abs(fm.Calculations.AverageFuelPerLap - 2.4) < 1e-6,
                   $"Il consumo medio deve basarsi direttamente sul primo giro pulito (2.4L), ottenuto {fm.Calculations.AverageFuelPerLap:F2}");
            Pass("Sprint pre-via escluso dal traguardo; primo giro reale di gara entra pulito a 2.4L");
        }

        private static void Test_RaceStartLineCrossed_FuelAndLapSync()
        {
            var ra = new RaceAnalyzer();
            var fm = new FuelManager();
            var state = new SessionState
            {
                IsGameRunning = true,
                IsRaceSession = true,
                SessionStateStatus = 3, // Formation lap
                CurrentLap = 1,
                TrackPositionPercent = 0.50,
                CurrentFuelLevel = 50.0,
                MaxFuelCapacity = 50.0,
                Position = 1, // Player è leader
                TrackLengthMeters = 4088.0,
                SpeedKmh = 90.0
            };

            // 1. In formazione: LapsCompleted deve essere 0, Fuel non conteggiato
            ra.Update(state, null, null, fm.Calculations, null, TyreSelectionScope.None, 0.0, 0.0);
            fm.Update(state, 20.0, 0.0, null);
            Assert(ra.Results.RaceLapsCompleted == 0, "In formazione RaceLapsCompleted deve essere 0");
            Assert(fm.FuelHistory.Count == 0, "In formazione nessun consumo deve essere registrato");

            // 2. Bandiera verde a pos 0.62 (Rolling start sprint verso il traguardo)
            state.SessionStateStatus = 4;
            state.TrackPositionPercent = 0.62;
            state.CurrentFuelLevel = 49.04;
            state.RaceStartLineCrossed = false; // non ha ancora tagliato la linea del via
            ra.Update(state, null, null, fm.Calculations, null, TyreSelectionScope.None, 0.0, 0.0);
            fm.Update(state, 20.0, 0.0, null);

            Assert(ra.Results.RaceLapsCompleted == 0, "Durante lo sprint pre-via RaceLapsCompleted deve essere 0");
            Assert(!state.RaceStartingFuelLatched, "Durante lo sprint pre-via RaceStartingFuel non deve essere ancora agganciato");
            Assert(fm.FuelHistory.Count == 0, "Durante lo sprint pre-via nessun consumo deve essere registrato");

            // 3. Primo passaggio sul traguardo sotto bandiera verde (AbsolutePos == 0.00)
            // Latch di RaceStartingFuel = 46.07L, CurrentLap passa a 2 in iRacing
            state.CurrentLap = 2;
            state.TrackPositionPercent = 0.001;
            state.CurrentFuelLevel = 46.07;
            state.RaceStartingFuel = 46.07;
            state.RaceStartingFuelLatched = true;
            state.RaceStartLineCrossed = true;
            state.RaceStartLap = 2;

            ra.Update(state, null, null, fm.Calculations, null, TyreSelectionScope.None, 0.0, 0.0);
            fm.Update(state, 20.0, 0.0, null);

            Assert(ra.Results.RaceLapsCompleted == 0, "Al primo passaggio del via i giri completati devono essere 0 (si inizia il Giro 1!)");
            Assert(fm.FuelHistory.Count == 0, "Al passaggio del via lo sprint pre-traguardo viene scartato");

            // 4. Completamento del primo giro reale di gara (CurrentLap passa a 3, fuel = 43.86L)
            state.CurrentLap = 3;
            state.TrackPositionPercent = 0.002;
            state.CurrentFuelLevel = 43.86;

            ra.Update(state, null, null, fm.Calculations, null, TyreSelectionScope.None, 0.0, 0.0);
            fm.Update(state, 19.0, 0.0, null);

            Assert(ra.Results.RaceLapsCompleted == 1, "Dopo il primo giro completo RaceLapsCompleted deve essere 1");
            Assert(fm.FuelHistory.Count == 1, "Dopo il primo giro completo deve entrare esattamente 1 campione di fuel");
            Assert(Math.Abs(fm.Calculations.AverageFuelPerLap - 2.21) < 1e-6,
                   $"Il consumo del primo giro deve essere 46.07 - 43.86 = 2.21L, ottenuto {fm.Calculations.AverageFuelPerLap:F2}");
            Pass("Sincronizzazione completa: start line crossing latch, giri completati 0->1, fuel 2.21L");
        }

        private static void Test_ComputeMedian_RobustStatistics()
        {
            // Null ed empty
            Assert(FuelManager.ComputeMedian(null) == 0.0, "null deve restituire 0.0");
            Assert(FuelManager.ComputeMedian(new List<double>()) == 0.0, "empty deve restituire 0.0");

            // 1 campione
            Assert(FuelManager.ComputeMedian(new List<double> { 2.5 }) == 2.5, "1 campione restituisce se stesso");

            // 2 campioni (media dei due)
            Assert(Math.Abs(FuelManager.ComputeMedian(new List<double> { 2.4, 2.6 }) - 2.5) < 1e-6, "2 campioni restituisce la media dei due");

            // 3 campioni
            Assert(FuelManager.ComputeMedian(new List<double> { 2.9, 2.2, 2.5 }) == 2.5, "3 campioni restituisce il valore centrale");

            // 5 campioni: caso Road Atlanta con 1 calo isolato da scia/fuel saving (2.14L su giro 12)
            var roadAtlantaWindow = new List<double> { 2.28, 2.27, 2.23, 2.14, 2.26 };
            double median = FuelManager.ComputeMedian(roadAtlantaWindow);
            Assert(Math.Abs(median - 2.26) < 1e-6, $"Road Atlanta: la mediana deve essere 2.26L, ottenuta {median:F3}");

            // 5 campioni con 2 cali (ancora non persistenti, servono almeno 3 per spostare la maggioranza)
            var twoDrops = new List<double> { 2.28, 2.27, 2.14, 2.14, 2.26 };
            Assert(Math.Abs(FuelManager.ComputeMedian(twoDrops) - 2.26) < 1e-6, "2 cali isolati non spostano la mediana da 2.26L");

            // 5 campioni con 3 cali (persistenza confermata: il pilota sta gestendo davvero!)
            var threeDrops = new List<double> { 2.14, 2.14, 2.14, 2.27, 2.28 };
            Assert(Math.Abs(FuelManager.ComputeMedian(threeDrops) - 2.14) < 1e-6, "3 cali consecutivi spostano la mediana al nuovo valore (2.14L)");

            // 5 campioni con 1 picco isolato verso l'alto (errore/pattinamento a 2.45L)
            var oneHighPeak = new List<double> { 2.26, 2.26, 2.27, 2.28, 2.45 };
            Assert(Math.Abs(FuelManager.ComputeMedian(oneHighPeak) - 2.27) < 1e-6, "1 picco isolato alto non sposta la mediana oltre 2.27L");

            Pass("ComputeMedian: statistica robusta, immunità a scia singola (1 calo) e transizione solo a 3 cali");
        }

        private static void Test_RoadAtlanta_MedianFuelEstimation_AntiDraft()
        {
            var fm = new FuelManager();
            var state = new SessionState
            {
                IsGameRunning = true,
                IsRaceSession = true,
                SessionStateStatus = 4,
                CurrentLap = 2,
                RaceStartLap = 2,
                RaceStartLineCrossed = true,
                RaceStartingFuelLatched = true,
                RaceStartingFuel = 48.19,
                CurrentFuelLevel = 48.19,
                MaxFuelCapacity = 52.0,
                TrackPositionPercent = 0.001
            };

            // Inizializza stato iniziale di inizio stint
            fm.Update(state, 21.0, 0.0, null);

            // Simula i 5 giri puliti prima del pit stop (Giri 8, 9, 11, 12, 13)
            // con il Giro 12 anomalo a 2.14L
            double[] lapsFuel = { 2.28, 2.27, 2.23, 2.14, 2.26 };
            double fuel = 48.19;
            int lap = 2;
            foreach (double burn in lapsFuel)
            {
                fuel -= burn;
                state.CurrentLap = ++lap;
                state.CurrentFuelLevel = fuel;
                fm.Update(state, 21.0, 0.0, null);
            }

            // La stima mediana deve essere 2.26L (mentre la vecchia media dava 2.236L)
            Assert(Math.Abs(fm.Calculations.AverageFuelPerLap - 2.26) < 1e-6,
                $"La stima di consumo deve essere la mediana 2.26L, ottenuta {fm.Calculations.AverageFuelPerLap:F3}");

            // All'ingresso box (giro 14, 21.0458 giri rimanenti, 17.16L nel serbatoio):
            state.CurrentFuelLevel = 17.16;
            fm.Calculations.StrategyMode = FuelStrategyMode.Aggressive;
            fm.Update(state, 21.0458, 0.0, null);

            // In AGGRESSIVE: (21.0458 * 2.26) - 17.16 = 30.404L -> Math.Ceiling(30.404) = 31L!
            Assert(fm.Calculations.FuelToAdd == 31.0,
                $"In AGGRESSIVE con la mediana il rifornimento deve essere 31L (salva la gara!), ottenuto {fm.Calculations.FuelToAdd:F1}L");

            // In NORMAL: margine fisso +0.6L -> 30.404 + 0.6 = 31.004L -> Math.Ceiling(31.004) = 32L
            fm.Calculations.StrategyMode = FuelStrategyMode.Normal;
            fm.Update(state, 21.0458, 0.0, null);
            Assert(fm.Calculations.FuelToAdd == 32.0,
                $"In NORMAL il rifornimento con margine +0.6L deve essere 32L, ottenuto {fm.Calculations.FuelToAdd:F1}L");

            Pass("Road Atlanta Replay Pit Stop: la mediana a 2.26L evita la sottostima e consiglia 31L in AGGR e 32L in NORM");
        }
    }
}
