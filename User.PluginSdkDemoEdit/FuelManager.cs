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



        public double UserFuelOffset { get; set; } = 0.0;

        public double FuelStep { get; set; } = 0.1;



        public FuelStrategyMode StrategyMode { get; set; } = FuelStrategyMode.Manual;

        public bool IsSyncModeEnabled { get; set; } = false;

    }



    public class FuelManager

    {

        private const int HISTORY_SIZE = 4;

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

