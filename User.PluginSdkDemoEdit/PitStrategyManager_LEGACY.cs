// -------------------------------------------------------------------------
// FILE: PitStrategyManager.cs
// VERSION: V0.11.54
// -------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using GameReaderCommon;
using System.Globalization;
using System.Collections.ObjectModel;
using System.Text;

namespace SimRIG
{
    public class TrackRecord
    {
        public string TrackID { get; set; }
        public double TransitTime { get; set; } = 0.0;
        public double TransitDriveThrough { get; set; } = 0.0;
        public bool PlayerRecordSet { get; set; } = false;

        public double PitEntryPct { get; set; } = -1.0;
        public double PitExitPct { get; set; } = -1.0;

        public bool IsInGeofence(double pos)
        {
            if (PitEntryPct == -1.0 || PitExitPct == -1.0) return false;

            if (PitEntryPct < PitExitPct) return pos >= PitEntryPct && pos <= PitExitPct;
            return pos >= PitEntryPct || pos <= PitExitPct;
        }

        public bool HasValidCleanSectorBounds() => PitEntryPct != -1.0 && PitExitPct != -1.0;

        public bool IsInCleanSector(double pos)
        {
            if (!HasValidCleanSectorBounds()) return false;

            double start = PitExitPct + 0.10;
            if (start >= 1.0) start -= 1.0;

            double end = PitEntryPct - 0.05;
            if (end < 0.0) end += 1.0;

            if (start < end) return pos >= start && pos <= end;
            return pos >= start || pos <= end;
        }

        public double GetSectorWeight()
        {
            if (!HasValidCleanSectorBounds()) return 1.0;
            double start = PitExitPct + 0.10;
            if (start >= 1.0) start -= 1.0;
            double end = PitEntryPct - 0.05;
            if (end < 0.0) end += 1.0;

            double weight = end - start;
            if (weight <= 0) weight += 1.0;
            if (weight < 0.1) weight = 0.1;
            return weight;
        }
    }

    public class ClassRecord
    {
        public string CarClass { get; set; }
        public double FuelFillRate { get; set; } = 0.0;
        public double TyreChangeTime { get; set; } = 0.0;
    }

    public class SimRigDatabase
    {
        public List<TrackRecord> Tracks { get; set; } = new List<TrackRecord>();
        public List<ClassRecord> Classes { get; set; } = new List<ClassRecord>();
    }

    public class TargetState
    {
        public string ModeLabel;
        public string Name;
        public int ClassPosition;
        public double GapSeconds;
        public string GapString;
        public double SignedGapSeconds;
        public double TrueCurrentPace;
        public string Diagnosis;
        public double RelativePace;
        public double TargetCurrentSpeed;
        public double TargetTopSpeed;
        public double TargetPaceDropDueToTyres;
        public double TargetSpeedDrop;
        public double PaceDeficit;
        public double RelativeDegradation;
        public bool UndercutViable;
        public bool OvercutViable;
        public double ProjectedMergeGap;
        public double MergeGapWorstCase;
        public int PitCount;

        public double CurrentTank;
        public double TankLapsRemaining;

        public int ReactionDeltaLaps;
        public double NetPaceAdvantageTotal;

        public string PlayerTyreScope;
        public double PlayerStatLoss;
        public double PlayerOutLoss;
        public double TargetStatLoss;
        public double TargetOutLoss;
        public bool TargetTireInferred;
        public string TargetDropZoneStatus;

        public double TargetTrueRawPace;
        public double TargetTrueBaselinePace;
        public double TargetTrueCurrentPace;

        public double LapsRemainingAtExit;
    }

    public class LogEntry
    {
        public double SessionTime { get; set; }
        public string Type { get; set; }
        public string DriverName { get; set; }
        public int LapNumber { get; set; }
        public double RawLapTime { get; set; }
        public double NormalizedLapTime { get; set; }
        public double Baseline { get; set; }
        public double PaceDropDueToTyres { get; set; }
        public double FuelData { get; set; }
        public int LapsSincePit { get; set; }
        public double TrackTemp { get; set; }
        public double BestFreshTime { get; set; }
        public double WornTime { get; set; }
        public bool TiresChanged { get; set; }
    }

    public class OpponentPitTracker
    {
        public double EntryTimeSec;
        public double ExitTimeSec = 0.0;
        public double LapsRemainingAtExit = -1.0;
        public double? StopStartTimeSec;
        public double StationaryTimeSec;
        public bool IsInsideGeofence;
        public int PitCount = 0;
        public bool HasCountedPitThisTransit = false;
        public bool TiresChanged = true;
    }

    public class CleanSectorData
    {
        public bool IsInside = false;
        public double EntryTime = 0.0;
        public double LastNormalTime = 0.0;
        public double LastOutlapTime = 0.0;
        public double LastInlapTime = 0.0;
        public string CurrentState = "NORMAL";
        public bool PendingOutlap = false;

        public double BestFreshNormalTime = 0.0;
        public List<double> WornNormalHistory = new List<double>();
        public double WornNormalTime = 0.0;
    }

    public class OpponentTelemetryTracker
    {
        public double LastPosPct;
        public double LastTimeSec;
        public double LastLapStartTimeSec = -1.0;
        public double LastValidSpeedKmh;
        public double PersonalTopSpeed;
        public double SpeedAtFastestSector;
        public List<double> LapHistory = new List<double>();
        public List<double> NormalizedLapHistory = new List<double>();
        public List<double> RawLapHistory = new List<double>();
        public double BaselinePace = 0.0;
        public double PaceDropDueToTyres = 0.0;
        public int LastLap = -1;
        public int LastPitLap = 1;
        public int HighestLapSeen = 0;
        public double EstimatedFuel = 0.0;
        public bool LastPitTiresChanged = true;
        public CleanSectorData CleanSector = new CleanSectorData();
    }

    public class PitStrategyManager
    {
        private SimRigDatabase _database = new SimRigDatabase();
        private string _dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SimRIG_Data.json");

        public double DropZoneEstimatedLoss { get; private set; } = 0.0;
        public string DropZoneStatus { get; private set; } = "ANALYZING";
        public string CalibrationStatus { get; private set; } = "ANALYZING";
        public double PitStationaryTimeLoss { get; private set; } = 0.0;
        public double TrueCurrentPace { get; private set; } = 0.0;

        public double MeasuredFuelFillRate => _currentClass != null ? _currentClass.FuelFillRate : 0.0;
        public double PitTransitTime => _currentTrack != null ? _currentTrack.TransitTime : 0.0;
        public double PitTransitDriveThrough => _currentTrack != null ? _currentTrack.TransitDriveThrough : 0.0;
        public double DbTireChangeTime => _currentClass != null ? _currentClass.TyreChangeTime : 0.0;

        public int TotalOpponentsInSession { get; private set; } = 0;

        public double DebugLastOpponentTransit { get; private set; } = 0.0;
        public double DebugLastOpponentStationary { get; private set; } = 0.0;
        public double DebugLastOpponentTotalTime { get; private set; } = 0.0;

        private double[] _lastLapGaps = new double[20];
        private int _myLastMacroSector = -1;
        private string _currentTargetName = "NO TARGET";
        private int _targetPosition = 0;

        private double _targetGapSeconds = 0.0;
        private double _targetSignedGapSeconds = 0.0;
        private string _targetGapString = "";
        private double _targetRelativePace = 0.0;
        private double _targetExposedCurrentSpeed = 0.0;
        private double _targetExposedTopSpeed = 0.0;

        public double FuelCapacityInTireTime { get; private set; } = 0.0;
        public double SelectedTireTime { get; private set; } = 0.0;
        public double PaceDeficit { get; private set; } = 0.0;
        public string Diagnosis { get; private set; } = "ANALYZING";
        public double RelativeDegradation { get; private set; } = 0.0;
        public double TargetPaceDropDueToTyres { get; private set; } = 0.0;
        public double TargetSpeedDrop { get; private set; } = 0.0;

        public int MyCurrentSector { get; private set; } = 0;
        public double MyLastMicroSectorSpeed { get; private set; } = 0.0;

        public double TrueBaselinePace { get; private set; } = 0.0;
        public double DriverTrueRawPace { get; private set; } = 0.0;
        public bool MyPendingStateReset { get; private set; } = false;
        public double ClassTopSpeed { get; private set; } = 0.0;
        public int TopSpeedMicroSector { get; private set; } = -1;
        public bool MyIsInTraffic { get; private set; } = false;

        public double DriverPaceDropDueToTyres { get; private set; } = 0.0;
        public double ClassPaceDropDueToTyres { get; private set; } = 0.0;
        public double LeaderPaceDropDueToTyres { get; private set; } = 0.0;
        public double RelativeDegradationToClass { get; private set; } = 0.0;
        private double _driverBaselineTemp = 0.0;

        public CleanSectorData PlayerCleanSector = new CleanSectorData();
        public double LeaderCleanSectorLastTime = 0.0;
        public string LeaderCleanSectorState = "NORMAL";
        public bool PlayerWasInPitLane = false;

        public TargetState LastTargetState { get; private set; }

        public List<LogEntry> PendingLogs = new List<LogEntry>();
        public double CurrentProjectedMergeGap { get; private set; } = 0.0;

        public int PlayerPitCount { get; private set; } = 0;
        public int LeaderPitCount { get; private set; } = 0;
        public int CurrentTargetPitCount { get; private set; } = 0;

        public double GlobalColdTyrePenalty { get; private set; } = 2.5;
        private List<double> _coldTyrePenalties = new List<double>();

        private TrackRecord _currentTrack;
        private ClassRecord _currentClass;
        private string _currentCarClassString;

        private double? _pitEntryTime;
        private double? _stopStartTime;
        public double LastStationaryTime { get; private set; } = 0.0;
        public double LastSessionClock { get; private set; } = 0.0;

        public Dictionary<string, OpponentPitTracker> _pitRadar = new Dictionary<string, OpponentPitTracker>();
        public Dictionary<string, OpponentTelemetryTracker> _oppTelemetry = new Dictionary<string, OpponentTelemetryTracker>();

        public Dictionary<int, double> StintPeakFuels = new Dictionary<int, double>();

        private bool _isFueling = false;
        private double _fuelStartTime = 0.0;
        private double _fuelStartAmount = 0.0;
        private double _lastFuelLevel = 0.0;
        private double _lastFuelIncreaseTime = 0.0;
        private double _maxFuelObserved = 0.0;
        private bool _fuelAddedDuringCurrentPitStop = false;

        private List<double> _myLapHistory = new List<double>();
        private List<double> _myRawLapHistory = new List<double>();
        private TyreSelectionScope _currentTyreScope = TyreSelectionScope.None;

        private double _lastRaceLapsRemaining = 0.0;

        public PitStrategyManager()
        {
            LoadDatabase();
        }

        private void LoadDatabase()
        {
            try
            {
                if (File.Exists(_dbPath))
                {
                    string json = File.ReadAllText(_dbPath);
                    _database = JsonConvert.DeserializeObject<SimRigDatabase>(json) ?? new SimRigDatabase();
                }
            }
            catch { }
        }

        private void SaveDatabase()
        {
            try
            {
                string json = JsonConvert.SerializeObject(_database, Formatting.Indented);
                File.WriteAllText(_dbPath, json);
            }
            catch { }
        }

        public double GetPitEntryPct()
        {
            if (_currentTrack != null && _currentTrack.PitEntryPct != -1.0)
            {
                return _currentTrack.PitEntryPct;
            }
            return 0.95;
        }

        private double GetTireMultiplier(TyreSelectionScope scope)
        {
            switch (scope)
            {
                case TyreSelectionScope.All4: return 1.0;
                case TyreSelectionScope.Fronts:
                case TyreSelectionScope.Rears:
                case TyreSelectionScope.Left:
                case TyreSelectionScope.Right: return 0.5;
                case TyreSelectionScope.FL:
                case TyreSelectionScope.FR:
                case TyreSelectionScope.RL:
                case TyreSelectionScope.RR: return 0.25;
                default: return 0.0;
            }
        }

        public int GetLatchedOpponentLap(string name, int fallback)
        {
            if (_oppTelemetry.ContainsKey(name)) return _oppTelemetry[name].HighestLapSeen;
            return fallback;
        }

        public void Update(GameData data, string carClass, string trackId, TyreSelectionScope tyreScope, double projectedFuelToAdd, string targetMode, double sessionClock, double averageFuelPerLap, double sessionBestLap, double currentTrackTemp, int sessionState, bool isPlayerInPitBox, double pitSpeedLimitKmh, double maxTank, double raceLapsRemaining, double raceStartingFuel)
        {
            if (string.IsNullOrEmpty(carClass) || string.IsNullOrEmpty(trackId)) return;

            LastSessionClock = sessionClock;
            _currentTyreScope = tyreScope;
            _currentCarClassString = carClass;

            double validLapsRem = raceLapsRemaining > 0 && raceLapsRemaining < 500 ? raceLapsRemaining : (_lastRaceLapsRemaining > 0 ? _lastRaceLapsRemaining : 20.0);
            _lastRaceLapsRemaining = validLapsRem;

            bool dbChanged = false;

            if (_currentTrack == null || _currentTrack.TrackID != trackId)
            {
                _currentTrack = _database.Tracks.FirstOrDefault(t => t.TrackID == trackId);
                if (_currentTrack == null)
                {
                    _currentTrack = new TrackRecord { TrackID = trackId, TransitTime = 0.0, TransitDriveThrough = 0.0, PlayerRecordSet = false, PitEntryPct = -1.0, PitExitPct = -1.0 };
                    _database.Tracks.Add(_currentTrack);
                    dbChanged = true;
                }
            }

            if (_currentClass == null || _currentClass.CarClass != carClass)
            {
                _currentClass = _database.Classes.FirstOrDefault(c => c.CarClass == carClass);
                if (_currentClass == null)
                {
                    _currentClass = new ClassRecord { CarClass = carClass, FuelFillRate = 0.0, TyreChangeTime = 0.0 };
                    _database.Classes.Add(_currentClass);
                    dbChanged = true;
                }
            }

            if (dbChanged) SaveDatabase();

            if (_currentTrack.TransitTime == 0.0 && _currentClass.FuelFillRate == 0.0) CalibrationStatus = "NEEDS FULL CALIBRATION";
            else if (_currentTrack.TransitTime == 0.0) CalibrationStatus = "NEEDS PIT TRANSIT";
            else if (_currentClass.FuelFillRate == 0.0) CalibrationStatus = "NEEDS FUEL CALIBRATION";
            else CalibrationStatus = "READY";

            if (data.NewData == null) return;

            TotalOpponentsInSession = data.NewData.Opponents?.Count ?? 0;
            double trackLen = data.NewData.TrackLength > 0 ? data.NewData.TrackLength : 5000.0;

            double mySpeed = data.NewData.SpeedKmh;
            int myMicro = (int)(data.NewData.TrackPositionPercent * 100.0);
            myMicro = Math.Max(0, Math.Min(99, myMicro));

            if (mySpeed > ClassTopSpeed)
            {
                ClassTopSpeed = mySpeed;
                TopSpeedMicroSector = myMicro;
            }

            if (data.NewData.Position == 1) LeaderPitCount = PlayerPitCount;

            bool isInPitLane = data.NewData.IsInPitLane == 1;
            double currentFuel = data.NewData.Fuel;

            if (!isInPitLane)
            {
                if (!StintPeakFuels.ContainsKey(PlayerPitCount))
                {
                    StintPeakFuels[PlayerPitCount] = currentFuel;
                }
                else if (currentFuel > StintPeakFuels[PlayerPitCount])
                {
                    StintPeakFuels[PlayerPitCount] = currentFuel;
                }
            }

            List<double> validClassPaceDrops = new List<double>();
            double effectiveClassFuelBurn = averageFuelPerLap > 0.1 ? averageFuelPerLap : 3.0;

            if (data.NewData.Opponents != null)
            {
                foreach (var opp in data.NewData.Opponents)
                {
                    if (opp.IsPlayer || string.IsNullOrEmpty(opp.Name)) continue;

                    if (!_oppTelemetry.ContainsKey(opp.Name))
                    {
                        _oppTelemetry[opp.Name] = new OpponentTelemetryTracker { LastPosPct = opp.TrackPositionPercent ?? 0, LastTimeSec = sessionClock };
                    }

                    var t = _oppTelemetry[opp.Name];

                    int rawCurrentLap = opp.CurrentLap ?? 0;
                    if (sessionState >= 3)
                    {
                        if (rawCurrentLap > t.HighestLapSeen) t.HighestLapSeen = rawCurrentLap;
                        else if (rawCurrentLap < t.HighestLapSeen) rawCurrentLap = t.HighestLapSeen;
                    }
                    else
                    {
                        t.HighestLapSeen = rawCurrentLap;
                    }
                    int currentLap = rawCurrentLap;

                    double currentPos = opp.TrackPositionPercent ?? 0;
                    double deltaPos = currentPos - t.LastPosPct;

                    if (deltaPos < -0.5) deltaPos += 1.0;
                    else if (deltaPos > 0.5) deltaPos -= 1.0;

                    double deltaTime = sessionClock - t.LastTimeSec;

                    if (deltaTime > 0.001 && deltaTime < 5.0)
                    {
                        double speedKmh = (Math.Abs(deltaPos) * trackLen / deltaTime) * 3.6;
                        if (speedKmh >= 0 && speedKmh <= 360)
                        {
                            t.LastValidSpeedKmh = speedKmh;
                            if (speedKmh > t.PersonalTopSpeed) t.PersonalTopSpeed = speedKmh;

                            if (speedKmh > ClassTopSpeed)
                            {
                                ClassTopSpeed = speedKmh;
                                TopSpeedMicroSector = Math.Max(0, Math.Min(99, (int)(currentPos * 100.0)));
                            }

                            int oppMicro = Math.Max(0, Math.Min(99, (int)(currentPos * 100.0)));
                            if (oppMicro == TopSpeedMicroSector)
                            {
                                t.SpeedAtFastestSector = speedKmh;
                            }
                        }
                    }

                    t.LastPosPct = currentPos;
                    t.LastTimeSec = sessionClock;

                    if (opp.IsCarInPit) t.LastPitLap = currentLap;

                    if (currentLap != t.LastLap)
                    {
                        if (t.LastLap != -1 && t.LastLapStartTimeSec > 0 && sessionClock > t.LastLapStartTimeSec)
                        {
                            int completedLap = currentLap - 1;
                            int calculatedLapsSincePit = 0;
                            if (sessionState >= 4)
                            {
                                calculatedLapsSincePit = completedLap - t.LastPitLap;
                                if (calculatedLapsSincePit < 0) calculatedLapsSincePit = 0;
                            }

                            double rawLapTime = sessionClock - t.LastLapStartTimeSec;

                            if (rawLapTime > 20.0 && rawLapTime < 600.0)
                            {
                                int oppPitCount = _pitRadar.ContainsKey(opp.Name) ? _pitRadar[opp.Name].PitCount : 0;

                                double virtualLapsSincePit = calculatedLapsSincePit;
                                double maxStintLapsForCap = maxTank / effectiveClassFuelBurn;

                                if (oppPitCount > 0 && _pitRadar.ContainsKey(opp.Name))
                                {
                                    var radar = _pitRadar[opp.Name];
                                    if (radar.ExitTimeSec > 0 && sessionClock > radar.ExitTimeSec)
                                    {
                                        double timeSincePit = sessionClock - radar.ExitTimeSec;
                                        if (timeSincePit >= 0 && timeSincePit < (maxStintLapsForCap * 300.0))
                                        {
                                            double calcBase = t.BaselinePace > 0 ? t.BaselinePace : 90.0;
                                            double calcVirtual = timeSincePit / calcBase;
                                            if (calcVirtual >= 0 && calcVirtual <= maxStintLapsForCap + 2.0)
                                            {
                                                virtualLapsSincePit = calcVirtual;
                                            }
                                        }
                                    }
                                }

                                if (double.IsNaN(virtualLapsSincePit) || double.IsInfinity(virtualLapsSincePit)) virtualLapsSincePit = calculatedLapsSincePit;
                                double virtualFuelBurned = virtualLapsSincePit * effectiveClassFuelBurn;

                                double currentStartingFuel = raceStartingFuel > 0 ? raceStartingFuel : (validLapsRem + virtualLapsSincePit) * effectiveClassFuelBurn;
                                if (oppPitCount > 0 && _pitRadar.ContainsKey(opp.Name))
                                {
                                    var radar = _pitRadar[opp.Name];
                                    double lapsRemAtPitExit = radar.LapsRemainingAtExit > 0 ? radar.LapsRemainingAtExit : (validLapsRem + virtualLapsSincePit);
                                    double fuelNeededToFinish = lapsRemAtPitExit * effectiveClassFuelBurn;
                                    currentStartingFuel = Math.Min(maxTank, fuelNeededToFinish);
                                }
                                if (currentStartingFuel > maxTank) currentStartingFuel = maxTank;

                                double estimatedOppFuel = currentStartingFuel - virtualFuelBurned;
                                if (double.IsNaN(estimatedOppFuel) || double.IsInfinity(estimatedOppFuel) || estimatedOppFuel < 0) estimatedOppFuel = 0.0;

                                t.EstimatedFuel = estimatedOppFuel;

                                double oppFuelPenalty = estimatedOppFuel * 0.03;
                                double tempPenalty = _driverBaselineTemp > 0 ? ((currentTrackTemp - _driverBaselineTemp) * 0.05) : 0.0;

                                double normalizedOppLap = rawLapTime - oppFuelPenalty - tempPenalty;

                                bool isValidOppLap = true;
                                if (calculatedLapsSincePit <= 1) isValidOppLap = false;

                                if (t.BaselinePace > 0 && isValidOppLap)
                                {
                                    double deviation = normalizedOppLap - t.BaselinePace;
                                    if (deviation > (t.BaselinePace * 0.035) || deviation < -(t.BaselinePace * 0.020))
                                    {
                                        isValidOppLap = false;
                                    }
                                }

                                if (isValidOppLap)
                                {
                                    if (t.BaselinePace == 0.0)
                                    {
                                        t.BaselinePace = normalizedOppLap;
                                    }
                                    else if (t.BaselinePace - normalizedOppLap > 1.5)
                                    {
                                        t.BaselinePace = normalizedOppLap;
                                        t.NormalizedLapHistory.Clear();
                                        t.RawLapHistory.Clear();
                                    }
                                    else if (normalizedOppLap <= t.BaselinePace)
                                    {
                                        t.BaselinePace = (t.BaselinePace * 0.90) + (normalizedOppLap * 0.10);
                                    }

                                    t.NormalizedLapHistory.Add(normalizedOppLap);
                                    if (t.NormalizedLapHistory.Count > 5) t.NormalizedLapHistory.RemoveAt(0);

                                    t.RawLapHistory.Add(rawLapTime);
                                    if (t.RawLapHistory.Count > 5) t.RawLapHistory.RemoveAt(0);

                                    t.PaceDropDueToTyres = t.NormalizedLapHistory.Average() - t.BaselinePace;
                                    if (t.PaceDropDueToTyres < 0) t.PaceDropDueToTyres = 0.0;

                                    if (opp.Position == 1) LeaderPaceDropDueToTyres = t.PaceDropDueToTyres;

                                    PendingLogs.Add(new LogEntry
                                    {
                                        SessionTime = sessionClock,
                                        Type = "OPPONENT",
                                        DriverName = opp.Name,
                                        LapNumber = completedLap,
                                        RawLapTime = rawLapTime,
                                        NormalizedLapTime = normalizedOppLap,
                                        Baseline = t.BaselinePace,
                                        PaceDropDueToTyres = t.PaceDropDueToTyres,
                                        FuelData = estimatedOppFuel,
                                        LapsSincePit = calculatedLapsSincePit,
                                        TrackTemp = currentTrackTemp,
                                        BestFreshTime = t.CleanSector.BestFreshNormalTime,
                                        WornTime = t.CleanSector.WornNormalTime,
                                        TiresChanged = t.LastPitTiresChanged
                                    });
                                }
                            }
                        }
                        t.LastLapStartTimeSec = sessionClock;
                        t.LastLap = currentLap;
                    }

                    if (t.NormalizedLapHistory.Count > 0 && t.BaselinePace > 0)
                    {
                        validClassPaceDrops.Add(t.PaceDropDueToTyres);
                    }

                    if (t.EstimatedFuel == 0) t.EstimatedFuel = raceStartingFuel > 0 ? raceStartingFuel : maxTank;
                    UpdateCleanSector(opp.TrackPositionPercent ?? 0, sessionClock, opp.IsCarInPit, t.CleanSector, t.EstimatedFuel, currentTrackTemp, _driverBaselineTemp, currentLap, trackLen, t.LastPitTiresChanged);

                    if (opp.Position == 1)
                    {
                        LeaderCleanSectorLastTime = GetActiveCleanSectorTime(t.CleanSector);
                        LeaderCleanSectorState = t.CleanSector.CurrentState;
                        if (_pitRadar.ContainsKey(opp.Name)) LeaderPitCount = _pitRadar[opp.Name].PitCount;
                    }
                }
            }

            if (validClassPaceDrops.Count > 0)
            {
                validClassPaceDrops.Sort();
                int mid = validClassPaceDrops.Count / 2;
                if (validClassPaceDrops.Count % 2 == 0)
                {
                    ClassPaceDropDueToTyres = (validClassPaceDrops[mid - 1] + validClassPaceDrops[mid]) / 2.0;
                }
                else
                {
                    ClassPaceDropDueToTyres = validClassPaceDrops[mid];
                }
            }
            else
            {
                ClassPaceDropDueToTyres = 0.0;
            }

            RelativeDegradationToClass = DriverPaceDropDueToTyres - ClassPaceDropDueToTyres;

            UpdateTimingGates(data, targetMode, trackLen);
            UpdateOpponentRadar(data, sessionClock, maxTank, effectiveClassFuelBurn);

            UpdateCleanSector(data.NewData.TrackPositionPercent, sessionClock, isInPitLane, PlayerCleanSector, currentFuel, currentTrackTemp, _driverBaselineTemp, data.NewData.CurrentLap, trackLen, true);

            if (isInPitLane && !PlayerWasInPitLane)
            {
                PlayerPitCount++;

                if (PlayerCleanSector.LastNormalTime > 0)
                {
                    PlayerCleanSector.LastInlapTime = PlayerCleanSector.LastNormalTime;
                    PlayerCleanSector.CurrentState = "INLAP (RETROACTIVE)";
                }
            }
            PlayerWasInPitLane = isInPitLane;

            if (isInPitLane && _pitEntryTime == null)
            {
                _pitEntryTime = sessionClock;
                _fuelAddedDuringCurrentPitStop = false;

                if (_currentTrack.PitEntryPct == -1.0 || Math.Abs(_currentTrack.PitEntryPct - data.NewData.TrackPositionPercent) > 0.05)
                {
                    _currentTrack.PitEntryPct = data.NewData.TrackPositionPercent;
                    SaveDatabase();
                }
            }

            if (isInPitLane && isPlayerInPitBox)
            {
                if (currentFuel > _lastFuelLevel + 0.05)
                {
                    _fuelAddedDuringCurrentPitStop = true;
                    if (!_isFueling)
                    {
                        _isFueling = true;
                        _fuelStartTime = sessionClock;
                        _fuelStartAmount = _lastFuelLevel;
                    }
                    _lastFuelIncreaseTime = sessionClock;
                    _maxFuelObserved = currentFuel;
                }
            }

            if (_isFueling && (sessionClock - _lastFuelIncreaseTime > 2.0 || !isPlayerInPitBox))
            {
                _isFueling = false;
                double fuelAdded = _maxFuelObserved - _fuelStartAmount;
                double timeTaken = _lastFuelIncreaseTime - _fuelStartTime;

                if (fuelAdded > 0.5 && timeTaken > 0.5)
                {
                    _currentClass.FuelFillRate = fuelAdded / timeTaken;
                    SaveDatabase();
                }
            }
            _lastFuelLevel = currentFuel;

            if (isInPitLane)
            {
                if (isPlayerInPitBox && _stopStartTime == null)
                {
                    _stopStartTime = sessionClock;
                }
                else if (!isPlayerInPitBox && _stopStartTime != null)
                {
                    LastStationaryTime = sessionClock - _stopStartTime.Value;

                    if (!_fuelAddedDuringCurrentPitStop && LastStationaryTime > 3.0 && tyreScope != TyreSelectionScope.None)
                    {
                        double mult = GetTireMultiplier(tyreScope);
                        if (mult > 0)
                        {
                            _currentClass.TyreChangeTime = LastStationaryTime / mult;
                            SaveDatabase();
                        }
                    }

                    _stopStartTime = null;
                }
            }
            else if (_pitEntryTime != null)
            {
                if (_currentTrack.PitExitPct == -1.0 || Math.Abs(_currentTrack.PitExitPct - data.NewData.TrackPositionPercent) > 0.05)
                {
                    _currentTrack.PitExitPct = data.NewData.TrackPositionPercent;
                    SaveDatabase();
                }

                double lastTotalPitTime = sessionClock - _pitEntryTime.Value;
                double measuredTransit = lastTotalPitTime - LastStationaryTime;

                if (measuredTransit > 15.0 && measuredTransit < 120.0)
                {
                    if (_currentTrack.TransitTime == 0.0 || (!_currentTrack.PlayerRecordSet && measuredTransit < _currentTrack.TransitTime))
                    {
                        _currentTrack.TransitTime = measuredTransit;
                        _currentTrack.PlayerRecordSet = true;
                        SaveDatabase();
                    }
                }

                _pitEntryTime = null;
                LastStationaryTime = 0;
            }

            if (_currentTrack != null && _currentTrack.PitEntryPct != -1.0 && _currentTrack.PitExitPct != -1.0)
            {
                double pitDistanceMeters = 0;
                if (_currentTrack.PitEntryPct > _currentTrack.PitExitPct)
                {
                    pitDistanceMeters = ((1.0 - _currentTrack.PitEntryPct) + _currentTrack.PitExitPct) * trackLen;
                }
                else
                {
                    pitDistanceMeters = (_currentTrack.PitExitPct - _currentTrack.PitEntryPct) * trackLen;
                }

                double speedMs = pitSpeedLimitKmh / 3.6;
                if (speedMs > 0 && pitDistanceMeters > 0)
                {
                    double calculatedDt = pitDistanceMeters / speedMs;
                    if (calculatedDt > 10.0 && calculatedDt < 180.0)
                    {
                        _currentTrack.TransitDriveThrough = calculatedDt;
                    }
                }
            }

            double calcFillRate = _currentClass != null && _currentClass.FuelFillRate > 0 ? _currentClass.FuelFillRate : 0.0;
            if (calcFillRate == 0.0) calcFillRate = CarPitData.GetProfile(_currentCarClassString).RefuelRate;

            double calcTireTime = _currentClass != null && _currentClass.TyreChangeTime > 0 ? _currentClass.TyreChangeTime : 0.0;
            if (calcTireTime == 0.0) calcTireTime = CarPitData.GetProfile(_currentCarClassString).Tires4;

            double tireMult = GetTireMultiplier(tyreScope);
            double currentSelectedTireTime = calcTireTime * tireMult;
            SelectedTireTime = currentSelectedTireTime;

            FuelCapacityInTireTime = (calcFillRate > 0 && currentSelectedTireTime > 0) ? (currentSelectedTireTime * calcFillRate) : 0.0;

            double fuelTime = (calcFillRate > 0) ? (projectedFuelToAdd / calcFillRate) : 0.0;
            PitStationaryTimeLoss = Math.Max(fuelTime, currentSelectedTireTime);

            if (_currentTrack.TransitTime == 0.0)
            {
                DropZoneEstimatedLoss = 0.0;
                DropZoneStatus = "ANALYZING";
            }
            else
            {
                DropZoneEstimatedLoss = _currentTrack.TransitTime + PitStationaryTimeLoss;
                DropZoneStatus = EvaluateDropZoneByTime(data.NewData.CurrentLap, data.NewData.TrackPositionPercent, trackLen, data.NewData.SpeedKmh, DropZoneEstimatedLoss, data.NewData.Opponents?.ToList());
            }
        }

        private void UpdateCleanSector(double trackPos, double now, bool isCarInPit, CleanSectorData csData, double currentFuel, double trackTemp, double baselineTemp, int currentLap, double trackLength, bool tiresChanged)
        {
            if (_currentTrack == null || !_currentTrack.HasValidCleanSectorBounds()) return;

            bool isInsideNow = _currentTrack.IsInCleanSector(trackPos);

            if (isCarInPit)
            {
                if (!csData.PendingOutlap) csData.PendingOutlap = true;

                if (csData.LastNormalTime > 0)
                {
                    csData.LastInlapTime = csData.LastNormalTime;
                    csData.CurrentState = "INLAP (RETROACTIVE)";
                }
            }

            if (isInsideNow && !csData.IsInside)
            {
                csData.IsInside = true;
                csData.EntryTime = now;
            }
            else if (!isInsideNow && csData.IsInside)
            {
                csData.IsInside = false;
                double sectorTime = now - csData.EntryTime;

                if (sectorTime > 10.0 && sectorTime < 300.0)
                {
                    double sectorWeight = _currentTrack.GetSectorWeight();
                    double fuelPenalty = (currentFuel * 0.03) * sectorWeight;
                    double tempPen = baselineTemp > 0 ? ((trackTemp - baselineTemp) * 0.05) * sectorWeight : 0.0;
                    double normalizedSectorTime = sectorTime - fuelPenalty - tempPen;

                    if (csData.PendingOutlap)
                    {
                        csData.LastOutlapTime = sectorTime;
                        csData.PendingOutlap = false;
                        csData.CurrentState = "OUTLAP";

                        double referenceTime = tiresChanged && csData.BestFreshNormalTime > 0 ? csData.BestFreshNormalTime : csData.WornNormalTime;
                        if (referenceTime > 0)
                        {
                            double localDelta = normalizedSectorTime - referenceTime;

                            if (localDelta > 0.5 && localDelta < 20.0)
                            {
                                _coldTyrePenalties.Add(localDelta);
                                if (_coldTyrePenalties.Count > 10) _coldTyrePenalties.RemoveAt(0);
                                GlobalColdTyrePenalty = _coldTyrePenalties.Average();
                            }
                        }
                    }
                    else
                    {
                        bool isValid = true;
                        if (csData.BestFreshNormalTime > 0 && normalizedSectorTime > csData.BestFreshNormalTime * 1.05)
                        {
                            isValid = false;
                        }

                        if (isValid)
                        {
                            csData.LastNormalTime = sectorTime;
                            csData.CurrentState = "NORMAL";

                            double distanceDriven = currentLap * trackLength;
                            if (currentLap >= 2 && distanceDriven <= 30000.0)
                            {
                                if (csData.BestFreshNormalTime == 0.0 || normalizedSectorTime < csData.BestFreshNormalTime)
                                {
                                    csData.BestFreshNormalTime = normalizedSectorTime;
                                }
                            }

                            csData.WornNormalHistory.Add(normalizedSectorTime);
                            if (csData.WornNormalHistory.Count > 3) csData.WornNormalHistory.RemoveAt(0);
                            csData.WornNormalTime = csData.WornNormalHistory.Average();
                        }
                    }
                }
            }
        }

        private double GetActiveCleanSectorTime(CleanSectorData cs)
        {
            if (cs.CurrentState == "OUTLAP") return cs.LastOutlapTime;
            if (cs.CurrentState.Contains("INLAP")) return cs.LastInlapTime;
            return cs.LastNormalTime;
        }

        private string EvaluateDropZoneByTime(double subjectLap, double subjectPos, double trackLen, double speedKmh, double lossSec, List<Opponent> opponents)
        {
            if (opponents == null || trackLen <= 0 || speedKmh <= 0) return "CLEAR";

            double minGap = lossSec - 2.0;
            double maxGap = lossSec + 2.0;

            double calcSpeed = speedKmh / 3.6 > 5.0 ? speedKmh / 3.6 : 5.0;

            List<string> carsInZone = new List<string>();

            foreach (var opp in opponents)
            {
                if (opp.IsPlayer || string.IsNullOrEmpty(opp.Name)) continue;

                int latchedOppLap = opp.CurrentLap ?? 0;
                if (_oppTelemetry.ContainsKey(opp.Name)) latchedOppLap = _oppTelemetry[opp.Name].HighestLapSeen;

                double oppTotalPos = latchedOppLap + (opp.TrackPositionPercent ?? 0);
                double posDiff = (subjectLap + subjectPos) - oppTotalPos;

                if (posDiff > 0)
                {
                    double gap = (posDiff * trackLen) / calcSpeed;
                    if (gap >= minGap && gap <= maxGap)
                    {
                        double projectedGap = lossSec - gap;
                        carsInZone.Add($"{opp.Name.Split(' ').Last()} {projectedGap:+#.0;-#.0}s");
                    }
                }
            }

            if (carsInZone.Count == 0) return "CLEAR";
            if (carsInZone.Count == 1) return $"1 CAR ({carsInZone[0]})";

            string names = string.Join(", ", carsInZone.Take(2));
            if (carsInZone.Count > 2) names += ", ...";
            return $"TRAFFIC ({names})";
        }

        private void UpdateTimingGates(GameData data, string targetMode, double trackLen)
        {
            double myPos = data.NewData.TrackPositionPercent;
            double myLap = data.NewData.CurrentLap;

            int microSector = (int)(myPos * 100.0);
            MyCurrentSector = Math.Max(0, Math.Min(99, microSector));
            MyLastMicroSectorSpeed = data.NewData.SpeedKmh;

            int macroSector = (int)(myPos * 20.0);
            macroSector = Math.Max(0, Math.Min(19, macroSector));

            if (data.NewData.Opponents == null) return;

            Opponent targetOpp = null;
            bool targetIsPlayer = false;

            double minGapAhead = 999.0;
            double calcSpeed = data.NewData.SpeedKmh / 3.6 > 5.0 ? data.NewData.SpeedKmh / 3.6 : 5.0;

            foreach (var opp in data.NewData.Opponents)
            {
                if (opp.IsPlayer) continue;

                int latchedOppLap = opp.CurrentLap ?? 0;
                if (_oppTelemetry.ContainsKey(opp.Name)) latchedOppLap = _oppTelemetry[opp.Name].HighestLapSeen;

                double oppTotalPos = latchedOppLap + (opp.TrackPositionPercent ?? 0);
                double posDiff = (myLap + myPos) - oppTotalPos;

                if (posDiff < 0)
                {
                    double gap = Math.Abs((posDiff * trackLen) / calcSpeed);
                    if (gap < minGapAhead) minGapAhead = gap;
                }

                if (targetMode == "AHEAD")
                {
                    if (targetOpp == null || oppTotalPos < (GetLatchedOpponentLap(targetOpp.Name, targetOpp.CurrentLap ?? 0) + (targetOpp.TrackPositionPercent ?? 0)) && posDiff < 0)
                    {
                        if (posDiff < 0) targetOpp = opp;
                    }
                }
                else if (targetMode == "BEHIND")
                {
                    if (targetOpp == null || oppTotalPos > (GetLatchedOpponentLap(targetOpp.Name, targetOpp.CurrentLap ?? 0) + (targetOpp.TrackPositionPercent ?? 0)) && posDiff > 0)
                    {
                        if (posDiff > 0) targetOpp = opp;
                    }
                }
                else if (targetMode.StartsWith("P"))
                {
                    if (int.TryParse(targetMode.Substring(1), out int pPos))
                    {
                        if (pPos == data.NewData.Position) targetIsPlayer = true;
                        else if (opp.Position == pPos) targetOpp = opp;
                    }
                }
            }

            MyIsInTraffic = minGapAhead < 1.5;

            if (targetIsPlayer)
            {
                _currentTargetName = "PLAYER";
                _targetPosition = data.NewData.Position;
                _targetGapSeconds = 0.0;
                _targetSignedGapSeconds = 0.0;
                _targetGapString = "";
                _targetRelativePace = 0.0;
                _myLastMacroSector = -1;
                _targetExposedCurrentSpeed = 0.0;
                _targetExposedTopSpeed = 0.0;
                PaceDeficit = 0.0;
                RelativeDegradation = 0.0;
                TargetPaceDropDueToTyres = 0.0;
                TargetSpeedDrop = 0.0;
                Diagnosis = "TARGET IS YOU";
            }
            else if (targetOpp != null)
            {
                if (targetOpp.Name != _currentTargetName)
                {
                    _currentTargetName = targetOpp.Name;
                    _targetPosition = targetOpp.Position;
                    Array.Clear(_lastLapGaps, 0, 20);
                    _targetGapSeconds = 0.0;
                    _targetSignedGapSeconds = 0.0;
                    _targetGapString = "";
                    _targetRelativePace = 0.0;
                    _myLastMacroSector = -1;
                    PaceDeficit = 0.0;
                    RelativeDegradation = 0.0;
                    TargetPaceDropDueToTyres = 0.0;
                    TargetSpeedDrop = 0.0;
                    Diagnosis = "ANALYZING";
                }

                _targetPosition = targetOpp.Position;

                double oppPos = targetOpp.TrackPositionPercent ?? 0.0;
                int targetLatchedLap = targetOpp.CurrentLap ?? data.NewData.CurrentLap;
                if (_oppTelemetry.ContainsKey(targetOpp.Name)) targetLatchedLap = _oppTelemetry[targetOpp.Name].HighestLapSeen;

                double oppLap = targetLatchedLap;

                double posDiff = (myLap + myPos) - (oppLap + oppPos);
                double currentFluidGap = Math.Abs((posDiff * trackLen) / calcSpeed);

                _targetGapSeconds = currentFluidGap;
                _targetSignedGapSeconds = posDiff < 0 ? currentFluidGap : -currentFluidGap;

                string sign = posDiff < 0 ? "-" : "+";
                TimeSpan t = TimeSpan.FromSeconds(currentFluidGap);
                if (t.TotalHours >= 1.0) _targetGapString = sign + (int)t.TotalHours + ":" + t.ToString(@"mm\:ss\.f");
                else if (t.TotalMinutes >= 1.0) _targetGapString = sign + t.ToString(@"mm\:ss\.f");
                else _targetGapString = sign + currentFluidGap.ToString("0.0", CultureInfo.InvariantCulture);

                if (macroSector != _myLastMacroSector && _myLastMacroSector != -1)
                {
                    if (_lastLapGaps[macroSector] != 0.0)
                    {
                        bool targetIsAhead = posDiff < 0;
                        double deltaAbsoluteGap = currentFluidGap - Math.Abs(_lastLapGaps[macroSector]);

                        if (targetIsAhead) _targetRelativePace = deltaAbsoluteGap;
                        else _targetRelativePace = -deltaAbsoluteGap;
                    }

                    _lastLapGaps[macroSector] = currentFluidGap * (posDiff < 0 ? -1 : 1);
                }

                _myLastMacroSector = macroSector;

                if (_oppTelemetry.ContainsKey(_currentTargetName))
                {
                    var oppData = _oppTelemetry[_currentTargetName];
                    _targetExposedCurrentSpeed = oppData.LastValidSpeedKmh;
                    _targetExposedTopSpeed = oppData.PersonalTopSpeed;
                    CalculateAnalytics(oppData);
                }
            }
            else
            {
                _currentTargetName = "NO TARGET";
                _targetPosition = 0;
                _targetGapSeconds = 0.0;
                _targetSignedGapSeconds = 0.0;
                _targetGapString = "";
                _targetRelativePace = 0.0;
                _myLastMacroSector = -1;
                _targetExposedCurrentSpeed = 0.0;
                _targetExposedTopSpeed = 0.0;
                PaceDeficit = 0.0;
                RelativeDegradation = 0.0;
                TargetPaceDropDueToTyres = 0.0;
                TargetSpeedDrop = 0.0;
                Diagnosis = "ANALYZING";
            }
        }

        private void CalculateAnalytics(OpponentTelemetryTracker targetData)
        {
            if (_myRawLapHistory.Count > 0 && targetData.RawLapHistory.Count > 0)
            {
                TargetPaceDropDueToTyres = targetData.PaceDropDueToTyres;
                RelativeDegradation = TargetPaceDropDueToTyres - DriverPaceDropDueToTyres;
                TargetSpeedDrop = targetData.PersonalTopSpeed - targetData.SpeedAtFastestSector;

                if (TargetPaceDropDueToTyres > 0.5)
                {
                    if (TargetSpeedDrop > 5.0) Diagnosis = "TARGET FUEL SAVING";
                    else Diagnosis = "TARGET DEG HIGH";
                }
                else
                {
                    Diagnosis = "TARGET PUSHING";
                }
            }
            else
            {
                TargetPaceDropDueToTyres = 0.0;
                RelativeDegradation = 0.0;
                TargetSpeedDrop = 0.0;
                Diagnosis = "ANALYZING";
            }
        }

        public string GetTargetName() => _currentTargetName;

        private void UpdateOpponentRadar(GameData data, double now, double maxTank, double effectiveClassFuelBurn)
        {
            if (data.NewData.Opponents == null) return;

            foreach (var opp in data.NewData.Opponents)
            {
                if (opp.IsPlayer || string.IsNullOrEmpty(opp.Name)) continue;

                if (_currentTrack != null && _currentTrack.PitEntryPct != -1.0 && _currentTrack.PitExitPct != -1.0)
                {
                    bool isInside = _currentTrack.IsInGeofence(opp.TrackPositionPercent ?? 0);

                    if (!_pitRadar.ContainsKey(opp.Name))
                    {
                        _pitRadar[opp.Name] = new OpponentPitTracker { IsInsideGeofence = false };
                    }

                    var tracker = _pitRadar[opp.Name];

                    if (isInside && !tracker.IsInsideGeofence)
                    {
                        tracker.IsInsideGeofence = true;
                        tracker.HasCountedPitThisTransit = false;
                        tracker.EntryTimeSec = now;
                        tracker.StationaryTimeSec = 0;
                        tracker.StopStartTimeSec = null;
                    }
                    else if (isInside && tracker.IsInsideGeofence)
                    {
                        if (opp.IsCarInPit)
                        {
                            if (!tracker.HasCountedPitThisTransit)
                            {
                                tracker.PitCount++;
                                tracker.HasCountedPitThisTransit = true;
                            }

                            if (tracker.StopStartTimeSec == null) tracker.StopStartTimeSec = now;
                        }
                        else
                        {
                            if (tracker.StopStartTimeSec != null)
                            {
                                tracker.StationaryTimeSec += (now - tracker.StopStartTimeSec.Value);
                                tracker.StopStartTimeSec = null;
                            }
                        }
                    }
                    else if (!isInside && tracker.IsInsideGeofence)
                    {
                        tracker.IsInsideGeofence = false;

                        if (tracker.StopStartTimeSec != null)
                        {
                            tracker.StationaryTimeSec += (now - tracker.StopStartTimeSec.Value);
                            tracker.StopStartTimeSec = null;
                        }

                        if (tracker.HasCountedPitThisTransit)
                        {
                            tracker.ExitTimeSec = now;
                            tracker.LapsRemainingAtExit = _lastRaceLapsRemaining;

                            double totalTime = now - tracker.EntryTimeSec;
                            double transit = totalTime - tracker.StationaryTimeSec;

                            DebugLastOpponentTotalTime = totalTime;
                            DebugLastOpponentStationary = tracker.StationaryTimeSec;
                            DebugLastOpponentTransit = transit;

                            if (transit > 15.0 && transit < 120.0)
                            {
                                if (_currentTrack.TransitTime == 0.0 || (!_currentTrack.PlayerRecordSet && transit < _currentTrack.TransitTime))
                                {
                                    _currentTrack.TransitTime = transit;
                                    SaveDatabase();
                                }
                            }

                            if (_oppTelemetry.ContainsKey(opp.Name))
                            {
                                var tData = _oppTelemetry[opp.Name];
                                double fuelNeeded = tracker.LapsRemainingAtExit * effectiveClassFuelBurn;
                                double fuelToAdd = Math.Min(maxTank, Math.Max(0, fuelNeeded - tData.EstimatedFuel));

                                double calcFillRate = _currentClass != null && _currentClass.FuelFillRate > 0 ? _currentClass.FuelFillRate : 0.0;
                                if (calcFillRate == 0.0) calcFillRate = CarPitData.GetProfile(_currentCarClassString).RefuelRate;

                                double refuelTime = calcFillRate > 0 ? (fuelToAdd / calcFillRate) : 0.0;

                                double tireTime = _currentClass != null && _currentClass.TyreChangeTime > 0 ? _currentClass.TyreChangeTime : 0.0;
                                if (tireTime == 0.0) tireTime = CarPitData.GetProfile(_currentCarClassString).Tires4;

                                bool isSequential = CarPitData.GetProfile(_currentCarClassString).IsSequential;

                                if (isSequential)
                                {
                                    tracker.TiresChanged = tracker.StationaryTimeSec > (refuelTime + 5.0);
                                }
                                else
                                {
                                    if (refuelTime >= tireTime) tracker.TiresChanged = true;
                                    else tracker.TiresChanged = tracker.StationaryTimeSec >= (tireTime - 2.0);
                                }
                                tData.LastPitTiresChanged = tracker.TiresChanged;
                            }
                        }
                    }
                }
            }
        }

        public void AnalyzeLap(double lapTime, double fuelUsed, double trackTemp, bool wasInPit, bool isInPit, double trackLength, double currentFuel, int raceLapsCompleted, double sessionClock)
        {
            if (wasInPit || isInPit || lapTime <= 0) return;

            if (_driverBaselineTemp == 0.0 && trackTemp > 0)
            {
                _driverBaselineTemp = trackTemp;
            }

            double fuelPenalty = currentFuel * 0.03;
            double tempPenalty = _driverBaselineTemp > 0 ? ((trackTemp - _driverBaselineTemp) * 0.05) : 0.0;

            double normalizedLap = lapTime - fuelPenalty - tempPenalty;

            bool isValidForBaseline = true;

            if (raceLapsCompleted < 2)
            {
                isValidForBaseline = false;
            }

            if (TrueBaselinePace > 0 && isValidForBaseline)
            {
                double deviation = normalizedLap - TrueBaselinePace;

                if (deviation > (TrueBaselinePace * 0.035) || deviation < -(TrueBaselinePace * 0.020))
                {
                    isValidForBaseline = false;
                }
            }

            if (isValidForBaseline)
            {
                if (TrueBaselinePace == 0.0)
                {
                    TrueBaselinePace = normalizedLap;
                }
                else if (TrueBaselinePace - normalizedLap > 1.5)
                {
                    TrueBaselinePace = normalizedLap;
                    _myLapHistory.Clear();
                    _myRawLapHistory.Clear();
                }
                else if (normalizedLap <= TrueBaselinePace)
                {
                    TrueBaselinePace = (TrueBaselinePace * 0.90) + (normalizedLap * 0.10);
                }

                _myLapHistory.Add(normalizedLap);
                if (_myLapHistory.Count > 5) _myLapHistory.RemoveAt(0);

                _myRawLapHistory.Add(lapTime);
                if (_myRawLapHistory.Count > 5) _myRawLapHistory.RemoveAt(0);

                DriverTrueRawPace = _myRawLapHistory.Average();
                DriverPaceDropDueToTyres = _myLapHistory.Average() - TrueBaselinePace;
                if (DriverPaceDropDueToTyres < 0) DriverPaceDropDueToTyres = 0.0;
            }

            if (_myLapHistory.Count > 0)
            {
                TrueCurrentPace = TrueBaselinePace + DriverPaceDropDueToTyres + fuelPenalty + tempPenalty;
            }
            else
            {
                TrueCurrentPace = lapTime;
                DriverTrueRawPace = lapTime;
            }
        }

        public TargetState GetTargetState(string targetMode, double myPace, double myPos, double trackLen, double projectedFuelToAdd, double maxTank, double averageFuelPerLap, double raceTotalLaps, double raceLapsRemaining, double playerTankLapsRemaining, double raceStartingFuel, int myCurrentLap, double mySpeedKmh, List<Opponent> opponents)
        {
            double playerInlapLoss = 0.0;
            if (PlayerCleanSector.LastInlapTime > 0 && PlayerCleanSector.LastNormalTime > 0)
            {
                playerInlapLoss = PlayerCleanSector.LastInlapTime - PlayerCleanSector.LastNormalTime;
            }
            else
            {
                playerInlapLoss = DriverPaceDropDueToTyres;
            }

            double playerOutlapLoss = 0.0;

            double calcFillRate = _currentClass != null && _currentClass.FuelFillRate > 0 ? _currentClass.FuelFillRate : 0.0;
            if (calcFillRate == 0.0) calcFillRate = CarPitData.GetProfile(_currentCarClassString).RefuelRate;

            double fuelTime = calcFillRate > 0 ? (projectedFuelToAdd / calcFillRate) : 0.0;

            double calcTireTime = _currentClass != null && _currentClass.TyreChangeTime > 0 ? _currentClass.TyreChangeTime : 0.0;
            if (calcTireTime == 0.0) calcTireTime = CarPitData.GetProfile(_currentCarClassString).Tires4;

            double tireMult = GetTireMultiplier(_currentTyreScope);
            double currentSelectedTireTime = calcTireTime * tireMult;

            double playerStationary = Math.Max(fuelTime, currentSelectedTireTime);

            if (PlayerCleanSector.LastOutlapTime > 0 && PlayerCleanSector.LastNormalTime > 0)
            {
                playerOutlapLoss = PlayerCleanSector.LastOutlapTime - PlayerCleanSector.LastNormalTime;
            }
            else
            {
                if (_currentTyreScope == TyreSelectionScope.None)
                {
                    playerOutlapLoss = (projectedFuelToAdd * 0.03) + GlobalColdTyrePenalty;
                }
                else
                {
                    playerOutlapLoss = (projectedFuelToAdd * 0.03) + GlobalColdTyrePenalty + ((playerStationary / 15.0) * 0.5);
                }
            }

            double playerTruePitLoss = PitTransitTime + playerStationary + playerInlapLoss + playerOutlapLoss;

            string pDz = EvaluateDropZoneByTime(myCurrentLap, myPos, trackLen, mySpeedKmh, playerTruePitLoss, opponents);
            double pDzPenalty = pDz.StartsWith("TRAFFIC") ? 2.0 : (pDz.StartsWith("1 CAR") ? 0.5 : 0.0);
            playerOutlapLoss += pDzPenalty;
            playerTruePitLoss += pDzPenalty;

            double targetStationary = fuelTime;
            double targetInlapLoss = 0.0;
            double targetOutlapLoss = 0.0;
            double targetTruePace = _targetRelativePace > 0 ? myPace - 0.1 : myPace + 0.1;
            bool targetTireInferred = false;
            string targetDropZoneStr = "CLEAR";
            double targetOppPos = 0.0;
            int targetOppLap = 1;

            double targetRawPace = 0.0;
            double targetBaseline = 0.0;
            double targetCurrentPace = 0.0;
            double paceDeficit = 0.0;

            int targetPitCount = 0;
            if (_currentTargetName == "PLAYER")
            {
                targetPitCount = PlayerPitCount;
            }
            else if (_pitRadar.ContainsKey(_currentTargetName))
            {
                targetPitCount = _pitRadar[_currentTargetName].PitCount;
                if (_pitRadar[_currentTargetName].StationaryTimeSec > 3.0)
                {
                    targetStationary = _pitRadar[_currentTargetName].StationaryTimeSec;
                }
            }

            double effectiveClassFuelBurn = averageFuelPerLap > 0.1 ? averageFuelPerLap : 3.0;
            double validLapsRem = raceLapsRemaining > 0 && raceLapsRemaining < 500 ? raceLapsRemaining : (_lastRaceLapsRemaining > 0 ? _lastRaceLapsRemaining : 20.0);

            double targetLapsRemAtExit = validLapsRem;
            if (_pitRadar.ContainsKey(_currentTargetName))
            {
                targetLapsRemAtExit = _pitRadar[_currentTargetName].LapsRemainingAtExit;
            }

            double oppDistanceLaps = 0.0;

            if (_oppTelemetry.ContainsKey(_currentTargetName))
            {
                var oppData = _oppTelemetry[_currentTargetName];

                targetOppPos = Math.Max(0, Math.Min(0.999, oppData.LastPosPct));
                targetOppLap = oppData.HighestLapSeen;
                int baseLap = targetPitCount > 0 ? oppData.LastPitLap : 1;
                double lapsDone = Math.Max(0, oppData.HighestLapSeen - baseLap);
                oppDistanceLaps = lapsDone + targetOppPos;

                if (oppData.NormalizedLapHistory.Count > 0 && oppData.RawLapHistory.Count > 0)
                {
                    targetTruePace = oppData.NormalizedLapHistory.Average();
                    targetRawPace = oppData.RawLapHistory.Average();
                    targetBaseline = oppData.BaselinePace;
                    paceDeficit = DriverTrueRawPace - targetRawPace;
                }

                if (oppData.CleanSector.LastInlapTime > 0 && oppData.CleanSector.LastNormalTime > 0)
                {
                    targetInlapLoss = oppData.CleanSector.LastInlapTime - oppData.CleanSector.LastNormalTime;
                }
                else
                {
                    targetInlapLoss = TargetPaceDropDueToTyres;
                }

                if (oppData.CleanSector.LastOutlapTime > 0 && oppData.CleanSector.LastNormalTime > 0)
                {
                    targetOutlapLoss = oppData.CleanSector.LastOutlapTime - oppData.CleanSector.LastNormalTime;
                    targetTireInferred = oppData.LastPitTiresChanged;
                }
                else
                {
                    targetOutlapLoss = (projectedFuelToAdd * 0.03) + GlobalColdTyrePenalty;
                }
            }
            else
            {
                targetOutlapLoss = (projectedFuelToAdd * 0.03) + GlobalColdTyrePenalty;
                oppDistanceLaps = Math.Max(0, myCurrentLap - 1) + myPos;
            }

            double currentStartingFuel = raceStartingFuel > 0 ? raceStartingFuel : (raceTotalLaps * effectiveClassFuelBurn);
            if (currentStartingFuel > maxTank) currentStartingFuel = maxTank;

            if (targetPitCount > 0 && _pitRadar.ContainsKey(_currentTargetName))
            {
                var radar = _pitRadar[_currentTargetName];
                double lapsRemAtPitExit = radar.LapsRemainingAtExit > 0 ? radar.LapsRemainingAtExit : validLapsRem;
                double fuelNeededToFinish = lapsRemAtPitExit * effectiveClassFuelBurn;
                currentStartingFuel = Math.Min(maxTank, fuelNeededToFinish);
            }

            double targetFuelBurned = oppDistanceLaps * effectiveClassFuelBurn;
            double targetCurrentTank = currentStartingFuel - targetFuelBurned;
            if (targetCurrentTank < 0) targetCurrentTank = 0.0;

            double targetTankLapsRemaining = targetCurrentTank / effectiveClassFuelBurn;

            if (_oppTelemetry.ContainsKey(_currentTargetName))
            {
                var oppData = _oppTelemetry[_currentTargetName];
                if (oppData.NormalizedLapHistory.Count > 0 && oppData.RawLapHistory.Count > 0)
                {
                    targetCurrentPace = targetBaseline + oppData.PaceDropDueToTyres + (targetCurrentTank * 0.03);
                }
            }

            double targetTruePitLoss = PitTransitTime + targetStationary + targetInlapLoss + targetOutlapLoss;

            targetDropZoneStr = EvaluateDropZoneByTime(targetOppLap, targetOppPos, trackLen, _targetExposedCurrentSpeed > 0 ? _targetExposedCurrentSpeed : 200.0, targetTruePitLoss, opponents);
            double tDzPenalty = targetDropZoneStr.StartsWith("TRAFFIC") ? 2.0 : (targetDropZoneStr.StartsWith("1 CAR") ? 0.5 : 0.0);
            targetOutlapLoss += tDzPenalty;
            targetTruePitLoss += tDzPenalty;

            double netPitAdvantage = targetTruePitLoss - playerTruePitLoss;

            int reactionDeltaLaps = 1;
            if (targetTankLapsRemaining < 1.5)
            {
                reactionDeltaLaps = 0;
            }
            else if (_targetSignedGapSeconds < 0)
            {
                reactionDeltaLaps = (int)Math.Max(1, Math.Floor(playerTankLapsRemaining));
            }
            else
            {
                reactionDeltaLaps = 1;
            }

            double playerFreshPace = TrueBaselinePace + (projectedFuelToAdd * 0.03);
            double targetOldPace = targetCurrentPace > 0 ? targetCurrentPace : (targetBaseline + TargetPaceDropDueToTyres + (targetCurrentTank * 0.03));
            if (targetOldPace == 0) targetOldPace = myPace;

            double undercutPaceAdvantage = targetOldPace - playerFreshPace;
            double totalUndercutPaceAdvantage = undercutPaceAdvantage * reactionDeltaLaps;

            double undercutMergeGap = _targetSignedGapSeconds - netPitAdvantage - totalUndercutPaceAdvantage;
            bool isUndercutViable = undercutMergeGap < 0;

            double playerOldPace = TrueCurrentPace;
            double targetFreshPace = targetBaseline + (projectedFuelToAdd * 0.03);
            double overcutPaceAdvantage = playerOldPace - targetFreshPace;
            double totalOvercutPaceAdvantage = overcutPaceAdvantage * reactionDeltaLaps;

            double overcutMergeGap = _targetSignedGapSeconds - netPitAdvantage + totalOvercutPaceAdvantage;
            bool isOvercutViable = overcutMergeGap < 0;

            double activeNetPaceAdvTotal = 0.0;
            double activeMergeGap = 0.0;

            if (_targetSignedGapSeconds >= 0)
            {
                activeNetPaceAdvTotal = totalUndercutPaceAdvantage;
                activeMergeGap = undercutMergeGap;
            }
            else
            {
                activeNetPaceAdvTotal = totalOvercutPaceAdvantage;
                activeMergeGap = overcutMergeGap;
            }

            CurrentTargetPitCount = targetPitCount;
            CurrentProjectedMergeGap = activeMergeGap;

            LastTargetState = new TargetState
            {
                ModeLabel = targetMode,
                Name = _currentTargetName,
                ClassPosition = _targetPosition,
                GapSeconds = _targetGapSeconds,
                GapString = _targetGapString,
                SignedGapSeconds = _targetSignedGapSeconds,
                TrueCurrentPace = 0.0,
                Diagnosis = Diagnosis,
                RelativePace = _targetRelativePace,
                TargetCurrentSpeed = _targetExposedCurrentSpeed,
                TargetTopSpeed = _targetExposedTopSpeed,
                TargetPaceDropDueToTyres = TargetPaceDropDueToTyres,
                TargetSpeedDrop = TargetSpeedDrop,
                PaceDeficit = paceDeficit,
                RelativeDegradation = RelativeDegradation,

                UndercutViable = isUndercutViable,
                OvercutViable = isOvercutViable,
                ProjectedMergeGap = activeMergeGap,
                MergeGapWorstCase = activeMergeGap,
                PitCount = targetPitCount,

                CurrentTank = targetCurrentTank,
                TankLapsRemaining = targetTankLapsRemaining,
                ReactionDeltaLaps = reactionDeltaLaps,
                NetPaceAdvantageTotal = activeNetPaceAdvTotal,

                PlayerTyreScope = _currentTyreScope.ToString().ToUpper(),
                PlayerStatLoss = playerStationary,
                PlayerOutLoss = playerOutlapLoss,
                TargetStatLoss = targetStationary,
                TargetOutLoss = targetOutlapLoss,
                TargetTireInferred = targetTireInferred,
                TargetDropZoneStatus = targetDropZoneStr,
                TargetTrueRawPace = targetRawPace,
                TargetTrueBaselinePace = targetBaseline,
                TargetTrueCurrentPace = targetCurrentPace,
                LapsRemainingAtExit = targetLapsRemAtExit
            };

            return LastTargetState;
        }
    }
}