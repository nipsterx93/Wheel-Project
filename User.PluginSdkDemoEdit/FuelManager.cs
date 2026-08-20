// -------------------------------------------------------------------------

// FILE: FuelManager.cs

// VERSION: Fix errori 26

// -------------------------------------------------------------------------

using System;

using System.Collections.Generic;

using System.Linq;



namespace SimRIG

{

    public enum FuelStrategyMode { Manual, Normal, Safe, Aggressive }



    public class FuelCalculations

    {

        public double AverageFuelPerLap { get; set; } = 0.0;

        public double LastLapFuelUsed { get; set; } = 0.0;

        public double FuelToAdd { get; set; } = 0.0;



        public double TankLapsRemaining { get; set; } = 99.0;

        public double FuelDelta { get; set; } = 0.0;

        public double PitRequiredNumber { get; set; } = 0.0;

        public bool IsPredictionValid { get; set; } = false;



        public double FuelPerLapTarget { get; set; } = 3.0;

        public bool IsTargetModeEnabled { get; set; } = false;

        public bool TargetManuallySet { get; set; } = false;



        /// <summary>
        /// Consumo per giro necessario ad arrivare in fondo con quello che si ha a bordo,
        /// **senza** un'altra sosta. Da non confondere con FuelPerLapTarget, che è il target
        /// impostato a mano dal pilota per il rifornimento (Y-1).
        /// Vale 0.0 quando la domanda non ha senso: previsione non valida o gara finita.
        /// </summary>
        public double FuelSaveTarget { get; set; } = 0.0;

        /// <summary>Frazione di consumo da tagliare per centrare FuelSaveTarget. Negativa se si è già a posto.</summary>
        public double FuelSavingRequired { get; set; } = 0.0;

        /// <summary>
        /// Se quel risparmio è realisticamente ottenibile guidando, invece che con una sosta.
        /// È il filtro che tiene fuori i casi assurdi: 100 giri alla fine con 25 litri a bordo
        /// non è un problema di stile di guida, è una sosta obbligata.
        /// </summary>
        public bool IsFuelSavingAchievable { get; set; } = false;



        public double UserFuelOffset { get; set; } = 0.0;

        public double FuelStep { get; set; } = 0.1;



        public FuelStrategyMode StrategyMode { get; set; } = FuelStrategyMode.Manual;

        public bool IsSyncModeEnabled { get; set; } = false;

    }



    public class FuelManager

    {

        private const int HISTORY_SIZE = 4;

        /// <summary>
        /// Massimo risparmio di carburante ottenibile guidando, come frazione del consumo.
        /// Oltre questa soglia il divario non si colma alzando il piede: si colma solo con una
        /// sosta, e proporre il fuel saving sarebbe un consiglio impossibile da eseguire.
        ///
        /// Il 15% è la stima consueta di quanto si recupera con lift-and-coast e short-shifting
        /// prima che il tempo perso superi quello di un rifornimento. Non viene da un replay:
        /// nessuna sessione finora ha una fase di fuel saving misurabile.
        /// </summary>
        public const double MaxAchievableFuelSaving = 0.15;

        /// <summary>Esito del calcolo di fuel saving (Y-1).</summary>
        public struct FuelSavingPlan
        {
            /// <summary>Litri per giro necessari ad arrivare in fondo senza un'altra sosta.</summary>
            public double Target;

            /// <summary>Frazione di consumo da tagliare. Negativa se si è già sotto il necessario.</summary>
            public double RequiredFraction;

            /// <summary>Se quel taglio è ottenibile guidando invece che con una sosta.</summary>
            public bool Achievable;
        }

        /// <summary>
        /// Quanto bisogna consumare per arrivare in fondo con quello che si ha a bordo, e se
        /// ha senso proporlo al pilota (Y-1).
        ///
        /// La divisione secca <c>carburante / giri</c> da sola produce consigli assurdi: su una
        /// endurance con 100 giri da fare e 25 litri a bordo darebbe 0.25 L/giro, un numero
        /// aritmeticamente vero e praticamente inutile — quella non è una scelta di guida, è una
        /// sosta obbligata. Due filtri lo rendono azionabile:
        ///   1. il taglio richiesto deve stare entro ciò che si ottiene alzando il piede;
        ///   2. deve mancare **una sola** sosta: con più rifornimenti davanti, risparmiare
        ///      carburante non evita nulla, sposta solo il problema.
        /// </summary>
        public static FuelSavingPlan ComputeFuelSaving(double currentFuel, double raceLapsRemaining,
                                                       double consumption, double pitsRequired)
        {
            var plan = new FuelSavingPlan();

            if (raceLapsRemaining <= 0.0 || consumption <= 0.0) return plan;

            plan.Target = currentFuel / raceLapsRemaining;
            plan.RequiredFraction = (consumption - plan.Target) / consumption;
            plan.Achievable = plan.RequiredFraction > 0.0
                              && plan.RequiredFraction <= MaxAchievableFuelSaving
                              && pitsRequired <= 1.0;
            return plan;
        }

        private List<double> _fuelHistory = new List<double>();

        private int _lastEvaluatedLap = -1;

        private double _fuelAtLapStart = 0.0;



        public FuelCalculations Calculations { get; private set; } = new FuelCalculations();



        public FuelManager() { }



        // Aggiunto LogManager

        public void Update(SessionState state, double raceLapsRemaining, double fuelCapacityInTireTime, LogManager log)

        {

            if (!state.IsGameRunning) return;



            if (state.CurrentLap != _lastEvaluatedLap)

            {

                if (_lastEvaluatedLap > 0 && state.CurrentLap > 1)

                {

                    double fuelUsed = _fuelAtLapStart - state.CurrentFuelLevel;



                    if (!state.IsInPitLane && state.Flag_Black == 0 && fuelUsed > 0.1 && fuelUsed < state.MaxFuelCapacity)

                    {

                        Calculations.LastLapFuelUsed = fuelUsed;

                        _fuelHistory.Add(fuelUsed);

                        if (_fuelHistory.Count > HISTORY_SIZE) _fuelHistory.RemoveAt(0);



                        Calculations.AverageFuelPerLap = _fuelHistory.Average();



                        if (!Calculations.TargetManuallySet && Calculations.AverageFuelPerLap > 0)

                        {

                            Calculations.FuelPerLapTarget = Calculations.AverageFuelPerLap;

                        }



                        // LOG EVENTO: Consumo al termine del giro

                        log.Log(LogModule.FUEL, LogType.EVENT, "Lap Fuel Consumption", $"Lap {_lastEvaluatedLap} | Used: {fuelUsed:F2}L | Avg: {Calculations.AverageFuelPerLap:F2}L");

                    }

                    else

                    {

                        log.Log(LogModule.FUEL, LogType.EVENT, "Lap Fuel Ignored", $"Lap {_lastEvaluatedLap} | Used: {fuelUsed:F2}L | Pit/Flag active");

                    }

                }



                _fuelAtLapStart = state.CurrentFuelLevel;

                _lastEvaluatedLap = state.CurrentLap;

            }



            double consumption = GetEffectiveConsumption();



            if (consumption > 0) Calculations.TankLapsRemaining = state.CurrentFuelLevel / consumption;

            else Calculations.TankLapsRemaining = 99.0;



            Calculations.IsPredictionValid = (state.CurrentLap > 1) && (consumption > 0);



            if (raceLapsRemaining > 0 && consumption > 0 && Calculations.IsPredictionValid)

            {

                double rawFuelNeededForRace = raceLapsRemaining * consumption;

                double rawFuelToAdd = rawFuelNeededForRace - state.CurrentFuelLevel;



                Calculations.FuelDelta = -rawFuelToAdd;

                Calculations.PitRequiredNumber = rawFuelToAdd > 0 ? Math.Ceiling(rawFuelToAdd / state.MaxFuelCapacity) : 0.0;

                var plan = ComputeFuelSaving(state.CurrentFuelLevel, raceLapsRemaining,
                                             consumption, Calculations.PitRequiredNumber);
                Calculations.FuelSaveTarget = plan.Target;
                Calculations.FuelSavingRequired = plan.RequiredFraction;
                Calculations.IsFuelSavingAchievable = plan.Achievable;



                double finalFuelToAdd = 0.0;



                if (Calculations.StrategyMode == FuelStrategyMode.Manual)

                {

                    finalFuelToAdd = Calculations.UserFuelOffset;

                }

                else

                {

                    double margin = 0.0;

                    if (Calculations.StrategyMode == FuelStrategyMode.Safe) margin = consumption;

                    else if (Calculations.StrategyMode == FuelStrategyMode.Normal) margin = consumption * 0.3;



                    finalFuelToAdd = rawFuelToAdd + margin + Calculations.UserFuelOffset;



                    if (Calculations.IsSyncModeEnabled && fuelCapacityInTireTime > 0)

                    {

                        double antiBallastLimit = finalFuelToAdd + (consumption * 2.0);

                        if (fuelCapacityInTireTime > antiBallastLimit) finalFuelToAdd = antiBallastLimit;

                        else finalFuelToAdd = fuelCapacityInTireTime;

                    }

                }



                if (finalFuelToAdd < 0) finalFuelToAdd = 0;

                Calculations.FuelToAdd = Math.Round(Math.Min(state.MaxFuelCapacity, finalFuelToAdd), 1);

            }

            else

            {

                Calculations.FuelToAdd = 0.0;

                Calculations.FuelDelta = 0.0;

                Calculations.PitRequiredNumber = 0.0;

                Calculations.FuelSaveTarget = 0.0;

                Calculations.FuelSavingRequired = 0.0;

                Calculations.IsFuelSavingAchievable = false;

            }

        }



        public double GetEffectiveConsumption() => Calculations.IsTargetModeEnabled ? Calculations.FuelPerLapTarget : Calculations.AverageFuelPerLap;



        public void AddUserOffset(double direction)

        {

            Calculations.UserFuelOffset = Math.Round(Calculations.UserFuelOffset + (direction * Calculations.FuelStep), 1);

        }



        public void ResetUserOffset() { Calculations.UserFuelOffset = 0.0; }



        public void SetFuelTarget(double val)

        {

            Calculations.FuelPerLapTarget = Math.Round(val, 2);

            Calculations.TargetManuallySet = true;

        }



        public void CycleStrategyMode()

        {

            int current = (int)Calculations.StrategyMode;

            current++;

            if (current > 3) current = 0;

            Calculations.StrategyMode = (FuelStrategyMode)current;

            ResetUserOffset();

        }



        public void CycleFuelStep()

        {

            if (Calculations.FuelStep == 0.1) Calculations.FuelStep = 1.0;

            else if (Calculations.FuelStep == 1.0) Calculations.FuelStep = 5.0;

            else Calculations.FuelStep = 0.1;

        }



        public void ResetSession()

        {

            _fuelHistory.Clear();

            _lastEvaluatedLap = -1;

            _fuelAtLapStart = 0.0;

            Calculations = new FuelCalculations();

        }

    }

}

