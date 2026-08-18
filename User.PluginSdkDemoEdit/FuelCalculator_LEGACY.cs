// -------------------------------------------------------------------------
// FILE: FuelCalculator.cs
// VERSION: V0.11.47 Log version
// -------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;

namespace SimRIG
{
    public struct PredictiveRaceState
    {
        public bool IsRace;
        public bool IsLapLimited;
        public bool IsTimeLimited;
        public int SessionState;
        public bool PlayerCheckeredFlag;
        public bool GlobalCheckeredFlag;
        public double TimeLeftSeconds;
        public double RawSessionTime;
        public int TotalLaps;
        public int MyCurrentLap;
        public double MyTrackPosition;
        public double MyPace;
        public double TrackRecordTime;
        public double TrackLength;
        public bool IsRaceLeader;
        public int LeaderCurrentLap;
        public double LeaderTrackPosition;
        public double LeaderLastLap;
        public double LeaderBestLap;
        public bool IsInPitLane;
        public bool IsInPitBox;
    }

    public class FuelCalculator
    {
        private const int HISTORY_SIZE = 5;
        private List<double> _fuelHistory = new List<double>();
        public double AverageFuelPerLap { get; private set; } = 0.0;
        public double LastLapFuelUsed { get; private set; } = 0.0;
        public double FuelToAdd { get; private set; } = 0.0;
        public double PitRequiredNumber { get; private set; } = 0.0;
        public double FuelDelta { get; private set; } = 0.0;
        public double TankLapsRemaining { get; private set; } = 99.0;
        public double LeaderRaceTotalLaps { get; private set; } = 0.0;
        public int LeaderRaceLapsCompleted { get; private set; } = 0;
        public double LeaderRaceLapsRemaining { get; private set; } = 99.0;
        public double RaceTotalLaps { get; private set; } = 0.0;
        public int RaceLapsCompleted { get; private set; } = 0;
        public double RaceLapsRemaining { get; private set; } = 99.0;
        public bool IsLapped { get; private set; } = false;
        public double SessionTimeLeftSec { get; private set; } = 0.0;
        public double RaceLifeTimeLeftSec { get; private set; } = 0.0;
        public double LeaderEstimatedPace { get; private set; } = 0.0;
        private double _userFuelOffset = 0.0;
        public bool TargetManuallySet = false;
        public double FuelPerLapTarget { get; private set; } = 3.0;
        public bool UseTargetOverride = false;
        private bool _isRaceFinished = false;
        private bool _leaderHasFinished = false;

        private double _latchedLeaderTotalLaps = 0.0;
        private double _latchedPlayerTotalReality = 0.0;
        private double _latchedPlayerTotalUtopia = 0.0;

        private double _smoothedLeaderPace = 0.0;
        private double _smoothedPlayerPace = 0.0;

        private int _lastEvaluatedLap = -1;

        private bool _isLatchedForPit = false;
        private double _latchedRaceLapsRemaining = 0.0;
        private double _latchedPitRequiredNumber = 0.0;
        private double _latchedFuelDelta = 0.0;

        public bool IsPredictionValid { get; private set; } = false;

        public void RecordLap(double lapTimeSec, double fuelUsed, bool wasInPit, bool isInPit, int blackFlag)
        {
            if (wasInPit || isInPit || lapTimeSec <= 0 || fuelUsed <= 0 || blackFlag > 0) return;
            LastLapFuelUsed = fuelUsed;
            _fuelHistory.Add(fuelUsed);
            if (_fuelHistory.Count > HISTORY_SIZE) _fuelHistory.RemoveAt(0);
            AverageFuelPerLap = _fuelHistory.Average();
            if (!TargetManuallySet && AverageFuelPerLap > 0) FuelPerLapTarget = AverageFuelPerLap;
        }

        public void AddUserOffset(double offset) { _userFuelOffset = Math.Round(_userFuelOffset + offset, 1); }
        public void ResetUserOffset() { _userFuelOffset = 0.0; }
        public double GetUserOffset() { return _userFuelOffset; }
        public void SetFuelTarget(double val) { FuelPerLapTarget = Math.Round(val, 2); TargetManuallySet = true; }
        public double GetEffectiveConsumption() { return UseTargetOverride ? FuelPerLapTarget : AverageFuelPerLap; }

        public void ResetSession()
        {
            _fuelHistory.Clear();
            AverageFuelPerLap = 0.0;
            LastLapFuelUsed = 0.0;
            FuelToAdd = 0.0;
            PitRequiredNumber = 0.0;
            FuelDelta = 0.0;
            TankLapsRemaining = 99.0;
            LeaderRaceTotalLaps = 0.0;
            LeaderRaceLapsCompleted = 0;
            LeaderRaceLapsRemaining = 99.0;
            RaceTotalLaps = 0.0;
            RaceLapsCompleted = 0;
            RaceLapsRemaining = 99.0;
            IsLapped = false;
            RaceLifeTimeLeftSec = 0.0;
            _isRaceFinished = false;
            _leaderHasFinished = false;
            IsPredictionValid = false;

            _latchedLeaderTotalLaps = 0.0;
            _latchedPlayerTotalReality = 0.0;
            _latchedPlayerTotalUtopia = 0.0;
            _smoothedLeaderPace = 0.0;
            _smoothedPlayerPace = 0.0;

            _lastEvaluatedLap = -1;
            _isLatchedForPit = false;
        }

        private double UpdateLatchedLaps(double rawProjectedPos, double currentLatchedLaps)
        {
            if (currentLatchedLaps == 0.0) return Math.Ceiling(rawProjectedPos);

            if (rawProjectedPos > currentLatchedLaps + 0.05)
                return Math.Ceiling(rawProjectedPos - 0.05);

            if (rawProjectedPos < currentLatchedLaps - 1.05)
                return Math.Ceiling(rawProjectedPos + 0.05);

            return currentLatchedLaps;
        }

        public void UpdateStrategy(double currentFuel, double maxFuel, PredictiveRaceState state, double currentLapTimeSec, double transitTime, double fuelFillRate, double tireChangeTime)
        {
            SessionTimeLeftSec = state.TimeLeftSeconds;
            RaceLapsCompleted = Math.Max(0, state.MyCurrentLap - 1);
            LeaderRaceLapsCompleted = Math.Max(0, state.LeaderCurrentLap - 1);

            if (state.MyCurrentLap != _lastEvaluatedLap && state.MyCurrentLap > 1)
            {
                IsLapped = (LeaderRaceLapsCompleted > RaceLapsCompleted);
                _lastEvaluatedLap = state.MyCurrentLap;
            }

            double effSessionTimeLeft = state.TimeLeftSeconds;
            double effTrackPos = state.SessionState < 4 ? 0.0 : state.MyTrackPosition;

            if (state.SessionState < 4 || state.MyCurrentLap == 0)
            {
                effSessionTimeLeft = state.RawSessionTime;
                effTrackPos = 0.0;
            }

            if (state.SessionState >= 4)
            {
                if (state.GlobalCheckeredFlag || state.SessionState >= 5) _leaderHasFinished = true;
            }
            else _leaderHasFinished = false;

            if (state.PlayerCheckeredFlag)
            {
                if ((effTrackPos > 0.05 && effTrackPos < 0.15) || (currentLapTimeSec > 5.0 && currentLapTimeSec < 15.0))
                {
                    _isRaceFinished = true;
                }
            }

            double consumption = GetEffectiveConsumption();
            if (consumption > 0) TankLapsRemaining = currentFuel / consumption;
            else TankLapsRemaining = 99.0;

            bool hasValidConsumption = GetEffectiveConsumption() > 0.0 || TargetManuallySet;
            IsPredictionValid = (state.MyCurrentLap > 1) && hasValidConsumption;

            if (!state.IsRace || _isRaceFinished || !IsPredictionValid)
            {
                if (!state.IsRace || _isRaceFinished)
                {
                    RaceLapsRemaining = 0.0;
                    RaceTotalLaps = 0.0;
                }
                else
                {
                    if (state.IsLapLimited && !state.IsTimeLimited && state.TotalLaps > 0)
                    {
                        RaceTotalLaps = state.TotalLaps;
                        double rawRem = state.TotalLaps - (RaceLapsCompleted + effTrackPos);
                        RaceLapsRemaining = Math.Truncate(Math.Max(0, rawRem) * 100) / 100.0;
                    }
                    else
                    {
                        RaceLapsRemaining = 0.0;
                        RaceTotalLaps = 0.0;
                    }
                }

                LeaderRaceLapsRemaining = 0.0;
                FuelToAdd = 0.0;
                FuelDelta = currentFuel;
                PitRequiredNumber = 0;
                RaceLifeTimeLeftSec = 0.0;
                _isLatchedForPit = false;
                return;
            }

            if (state.IsInPitLane && !_isLatchedForPit)
            {
                _isLatchedForPit = true;
                _latchedRaceLapsRemaining = RaceLapsRemaining;
                _latchedPitRequiredNumber = PitRequiredNumber;
                _latchedFuelDelta = FuelDelta;
            }
            else if (!state.IsInPitLane && _isLatchedForPit)
            {
                _isLatchedForPit = false;
            }

            double rawLeaderPace = state.LeaderBestLap > 0 ? state.LeaderBestLap * 1.02 : 120.0;
            if (state.LeaderLastLap > 0 && state.LeaderLastLap < state.LeaderBestLap * 1.15)
                rawLeaderPace = (state.LeaderLastLap * 2.0 + state.LeaderBestLap) / 3.0;

            if (_smoothedLeaderPace == 0.0) _smoothedLeaderPace = rawLeaderPace;
            else _smoothedLeaderPace += (rawLeaderPace - _smoothedLeaderPace) * 0.05;

            double rawPlayerPace = state.MyPace > 10.0 ? state.MyPace : (state.TrackRecordTime > 10.0 ? state.TrackRecordTime * 1.03 : 120.0);

            if (_smoothedPlayerPace == 0.0) _smoothedPlayerPace = rawPlayerPace;
            else _smoothedPlayerPace += (rawPlayerPace - _smoothedPlayerPace) * 0.05;

            LeaderEstimatedPace = _smoothedLeaderPace;

            double leaderAbsolutePos = LeaderRaceLapsCompleted + state.LeaderTrackPosition;
            double playerAbsolutePos = RaceLapsCompleted + effTrackPos;

            double leaderLapsRem = 99.0;
            double rawPlayerRemaining = 0.0;

            if (_leaderHasFinished)
            {
                leaderLapsRem = 0.0;
                double projectedReality = playerAbsolutePos + (1.0 - effTrackPos);
                _latchedPlayerTotalReality = UpdateLatchedLaps(projectedReality, _latchedPlayerTotalReality);
                rawPlayerRemaining = Math.Max(0, _latchedPlayerTotalReality - playerAbsolutePos);
            }
            else
            {
                if (state.IsLapLimited && !state.IsTimeLimited && state.TotalLaps > 0)
                {
                    _latchedLeaderTotalLaps = state.TotalLaps;
                    _latchedPlayerTotalReality = state.TotalLaps;
                    _latchedPlayerTotalUtopia = state.TotalLaps;
                    leaderLapsRem = Math.Max(0, state.TotalLaps - leaderAbsolutePos);
                    RaceLifeTimeLeftSec = leaderLapsRem * _smoothedLeaderPace;
                    rawPlayerRemaining = Math.Max(0, state.TotalLaps - playerAbsolutePos);
                }
                else
                {
                    double timeUntilZero = Math.Max(0, effSessionTimeLeft);
                    double leaderPosAtZero = leaderAbsolutePos + (timeUntilZero / _smoothedLeaderPace);

                    _latchedLeaderTotalLaps = UpdateLatchedLaps(leaderPosAtZero, _latchedLeaderTotalLaps);
                    leaderLapsRem = Math.Max(0, _latchedLeaderTotalLaps - leaderAbsolutePos);

                    double timeUntilRaceEnds = leaderLapsRem * _smoothedLeaderPace;
                    RaceLifeTimeLeftSec = timeUntilRaceEnds;

                    if (!_isLatchedForPit)
                    {
                        double playerPosWhenLeaderFinishesUtopia = playerAbsolutePos + (timeUntilRaceEnds / _smoothedPlayerPace);
                        _latchedPlayerTotalUtopia = UpdateLatchedLaps(playerPosWhenLeaderFinishesUtopia, _latchedPlayerTotalUtopia);

                        double rawPlayerRemainingUtopia = _latchedPlayerTotalUtopia - playerAbsolutePos;
                        if (state.SessionState < 4 && effTrackPos == 0.0 && state.IsLapLimited && !state.IsTimeLimited) rawPlayerRemainingUtopia -= 1.0;
                        if (rawPlayerRemainingUtopia < 0) rawPlayerRemainingUtopia = 0;

                        double pass1FuelNeeded = rawPlayerRemainingUtopia * consumption;
                        double pass1RawFuelToAdd = pass1FuelNeeded - currentFuel;
                        double pass1Pits = pass1RawFuelToAdd > 0 ? Math.Ceiling(pass1RawFuelToAdd / maxFuel) : 0;

                        double estimatedPitLoss = 0.0;
                        if (pass1Pits > 0)
                        {
                            double fuelTime = (fuelFillRate > 0 && pass1RawFuelToAdd > 0) ? (pass1RawFuelToAdd / fuelFillRate) : 0.0;
                            double stationaryTime = Math.Max(fuelTime, tireChangeTime);
                            estimatedPitLoss = transitTime + stationaryTime;
                        }

                        double timeAvailableForDriving = timeUntilRaceEnds;
                        if (pass1Pits > 0 && estimatedPitLoss > 0 && !state.IsInPitLane)
                        {
                            timeAvailableForDriving = Math.Max(0, timeUntilRaceEnds - (pass1Pits * estimatedPitLoss));
                        }

                        double playerPosWhenLeaderFinishesReality = playerAbsolutePos + (timeAvailableForDriving / _smoothedPlayerPace);
                        _latchedPlayerTotalReality = UpdateLatchedLaps(playerPosWhenLeaderFinishesReality, _latchedPlayerTotalReality);
                    }

                    rawPlayerRemaining = Math.Max(0, _latchedPlayerTotalReality - playerAbsolutePos);
                    if (state.SessionState < 4 && effTrackPos == 0.0 && state.IsLapLimited && !state.IsTimeLimited) rawPlayerRemaining -= 1.0;
                }
            }

            LeaderRaceTotalLaps = _latchedLeaderTotalLaps;
            LeaderRaceLapsRemaining = Math.Truncate(leaderLapsRem * 100) / 100.0;
            RaceTotalLaps = _latchedPlayerTotalReality;

            if (_isLatchedForPit && !_leaderHasFinished)
            {
                RaceLapsRemaining = _latchedRaceLapsRemaining;
                PitRequiredNumber = _latchedPitRequiredNumber;
            }
            else
            {
                RaceLapsRemaining = Math.Truncate(rawPlayerRemaining * 100) / 100.0;
            }

            if (state.SessionState < 4 || RaceLapsRemaining >= 98.0 || state.MyCurrentLap == 0)
            {
                FuelToAdd = 0.0;
                FuelDelta = 0.0;
                if (!_isLatchedForPit || _leaderHasFinished) PitRequiredNumber = 0;
            }
            else
            {
                double fuelNeededForRace = RaceLapsRemaining * consumption;
                double rawFuelToAdd = fuelNeededForRace - currentFuel;

                if (rawFuelToAdd > 0)
                {
                    FuelToAdd = rawFuelToAdd;
                    FuelDelta = -rawFuelToAdd;
                    if (!_isLatchedForPit || _leaderHasFinished) PitRequiredNumber = Math.Ceiling(rawFuelToAdd / maxFuel);
                }
                else
                {
                    FuelToAdd = 0.0;
                    FuelDelta = Math.Abs(rawFuelToAdd);
                    if (!_isLatchedForPit || _leaderHasFinished) PitRequiredNumber = 0;
                }
            }
        }

        public static string FormatTime(double totalSeconds)
        {
            if (totalSeconds <= 0) return "00:00.000";
            TimeSpan t = TimeSpan.FromSeconds(totalSeconds);
            if (t.TotalHours >= 1.0) return t.ToString(@"hh\:mm\:ss\.fff");
            return t.ToString(@"mm\:ss\.fff");
        }
    }
}