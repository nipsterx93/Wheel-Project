// -------------------------------------------------------------------------
// FILE VERSION: V0.11.58
// -------------------------------------------------------------------------
using GameReaderCommon;
using SimHub.Plugins;
using System;
using System.Windows.Media;
using System.Globalization;
using System.Threading.Tasks;
using System.Threading;
using System.Speech.Synthesis;
using System.Media;
using System.Reflection;
using System.Linq;

namespace SimRIG
{
    [PluginDescription("SimRIG Manager V0.11.58 - Modulare")]
    [PluginAuthor("Gemini & Andreas")]
    [PluginName("SimRIG")]
    public class DataPluginDemo : IPlugin, IDataPlugin, IWPFSettingsV2
    {
        public DataPluginDemoSettings Settings;
        public PluginManager PluginManager { get; set; }

        public static readonly string[] FunctionList = new string[] { "Brake Bias", "Traction Control", "ABS", "Engine Map", "Engine Brake", "Diff Entry", "Diff Mid", "Diff Exit", "MGU-K / Rec", "Aux A", "Aux B", "Aux C" };

        public SessionState CurrentState { get; private set; }
        public TelemetryReader TelemetryReader { get; private set; }
        public FuelManager FuelManager { get; private set; }
        public RaceAnalyzer RaceAnalyzer { get; private set; }
        public PitRadar PitRadar { get; private set; }
        public OpponentTracker OpponentTracker { get; private set; }
        public TargetStrategyManager TargetStrategyManager { get; private set; }
        public TyreManager TyreManager { get; private set; }
        public SimRigHardwareManager HardwareManager { get; private set; }
        public LogManager LogManager { get; private set; }

        private string _steeringWheelMode = "NORMAL";
        private string _steeringWheelMessage = "READY";
        private double _liveBitePoint = 50.0;
        private string[] _encoderLabels = new string[4] { "N/A", "N/A", "N/A", "N/A" };
        private int _targetSelectionIndex = 0;
        private string _msgTL = "", _msgTR = "", _msgBL = "", _msgBR = "";
        private bool _windshieldActive = true;
        private bool _fastRepairActive = false;
        private double _manualCrossoverOffset = 0.0;
        private double _alertThreshold = 10.0;
        private int _leftWidgetPage = 0;
        private int _bottomLeftWidgetPage = 0;
        private int _bottomRightWidgetPage = 0;

        private bool _boxNowPlayed = false;
        private readonly CalibrationCascadeRunner _calibrationRunner = new CalibrationCascadeRunner();
        private string _calibrationStepText = "";
        private bool _autoUndercutPlayed = false;
        private bool _pitWindowAlertPlayed = false;
        private bool _interactivePitDialogueActive = false;
        private bool _sanityCheckPitPlayed = false;
        private bool _autoPitMacroSent = false;
        private bool _formationStartPlayed = false;
        private bool _formationGridPlayed = false;
        private bool _formationMidwayCleared = false;
        private bool _pitRecapPlayed = false;
        private bool _fuelWarningEndGamePlayed = false;
        private bool _wetOpponentsPitAlertPlayed = false;
        private bool _rainAlertPlayed = false;
        private bool _crossoverWetsPlayed = false;
        private bool _crossoverSlicksPlayed = false;


        private int _goodFuelLaps = 0;
        private int _lastVoiceEvalLap = -1;
        private readonly Random _rng = new Random();
        private bool _isLedGameModeActive = false;
        private int _lastLedRpm = -1;
        private readonly int[] _lastLedEvents = new int[7] { -1, -1, -1, -1, -1, -1, -1 };
        private double _fuelCapacityInTireTimeCache = 0.0;
        private int _lastSessionStateStatus = -1;
        private bool _lastTrackWet = false;
        private string _lastWeatherAlertState = "ASCIUTTO";
        private double _lastWeatherLogTime = -999.0;
        private string _lastSessionTypeName = null;
        private bool _lastTargetIsInsideGeofence = false;

        private CancellationTokenSource[] _pushTokens = new CancellationTokenSource[4];
        private bool[] _longPressExecuted = new bool[4];
        private CancellationTokenSource _msgTokenTL, _msgTokenTR, _msgTokenBL, _msgTokenBR;

        public SimRigProfile CurrentProfile = new SimRigProfile();
        public Color[] ButtonColors = new Color[12];
        public bool IsGlobalTesting = false;

        public bool IsInputConnected => HardwareManager != null && HardwareManager.IsInputConnected;
        public bool IsLedsConnected => HardwareManager != null && HardwareManager.IsLedsConnected;
        public string InputPortName => HardwareManager?.InputPortName ?? "N/A";
        public string LedsPortName => HardwareManager?.LedsPortName ?? "N/A";
        public double PersoSteeringWheelLiveBitePoint => _liveBitePoint;

        public ImageSource PictureIcon => null;
        public string LeftMenuTitle => "SimRIG Manager";

        public string GetBottomLeftLabel() => _encoderLabels[2];
        public string GetBottomRightLabel() => _encoderLabels[3];
        public string GetPitFuelLabel() { double val = FuelManager.Calculations.UserFuelOffset; return $"{(val > 0 ? "+" : "")}{val.ToString("F1", CultureInfo.InvariantCulture)} L"; }
        public string GetTyreScopeLabel() => TyreManager.GetScopeLabel();
        public string GetPitStratLabel()
        {
            switch (FuelManager.Calculations.StrategyMode)
            {
                case FuelStrategyMode.Manual: return "MANUAL";
                case FuelStrategyMode.Normal: return "NORM";
                case FuelStrategyMode.Safe: return "SAFE";
                case FuelStrategyMode.Aggressive: return "AGGR";
                default: return "UNKNOWN";
            }
        }
        public string GetPitPressLabel() => TyreManager.GetPressureLabel();
        private string GetTargetModeName(int index)
        {
            if (index == 0) return "AHEAD_CLASS";
            if (index == 1) return "BEHIND_CLASS";
            if (index == 2) return "AHEAD_OVERALL";
            if (index == 3) return "BEHIND_OVERALL";
            if (index == 4) return "LEADER_CLASS";
            if (index == 5) return "LEADER_OVERALL";
            if (index == 6) return "PLAYER";
            return $"P{index - 6}";
        }
        public string GetCurrentTargetModeName() { return GetTargetModeName(_targetSelectionIndex); }

        public string GetEncoderActionLabel(int index)
        {
            if (_steeringWheelMode == "PIT")
            {
                if (index == 0) return $"FUEL {GetPitFuelLabel()}";
                if (index == 1) return $"TYRES {GetTyreScopeLabel()}";
                if (index == 2) return $"MODE {GetPitStratLabel()}";
                if (index == 3) return $"PRESS {GetPitPressLabel()}";
            }
            else if (_steeringWheelMode == "PIT2")
            {
                if (index == 0) return $"TGT {FuelManager.Calculations.FuelPerLapTarget:F2}L" + (FuelManager.Calculations.IsTargetModeEnabled ? " [ON]" : " [OFF]");
                if (index == 1) return $"SYNC" + (FuelManager.Calculations.IsSyncModeEnabled ? " [ON]" : " [OFF]");
                if (index == 2) return $"STRAT {GetPitStratLabel()}";
                if (index == 3) return $"PRESS {GetPitPressLabel()}";
            }
            else if (_steeringWheelMode == "STRAT")
            {
                if (index == 0) return $"TGT: {(!string.IsNullOrEmpty(TargetStrategyManager.LatchedTargetName) ? "*" : "")}{TargetStrategyManager.CurrentTarget.Name}";
                if (index == 1) return $"DZ: {TargetStrategyManager.CurrentTarget.Diagnosis}";
            }
            else if (_steeringWheelMode == "FORECAST")
            {
                if (index == 0) return $"ALERT: {_alertThreshold:0}%";
                if (index == 1) return $"CROSS: {(_manualCrossoverOffset > 0 ? "+" : "")}{_manualCrossoverOffset:0.0}s";
            }

            if (index == 0) return Settings.TopLeftEncoderMode >= 0 && Settings.TopLeftEncoderMode < FunctionList.Length ? FunctionList[Settings.TopLeftEncoderMode] : "N/A";
            if (index == 1) return Settings.TopRightEncoderMode >= 0 && Settings.TopRightEncoderMode < FunctionList.Length ? FunctionList[Settings.TopRightEncoderMode] : "N/A";
            if (index == 2) return !string.IsNullOrEmpty(_encoderLabels[2]) ? _encoderLabels[2] : "N/A";
            if (index == 3) return !string.IsNullOrEmpty(_encoderLabels[3]) ? _encoderLabels[3] : "N/A";
            return "N/A";
        }

        public void Init(PluginManager pluginManager)
        {
            this.PluginManager = pluginManager;
            try
            {
                var names = pluginManager.GetAllPropertiesNames();
                foreach (var name in names)
                {
                    if (name.IndexOf("SessionInfo", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("MaxFuel", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        SimHub.Logging.Current.Info($"SimRIG Property Name: {name}");
                    }
                }
            }
            catch (Exception ex)
            {
                SimHub.Logging.Current.Info($"SimRIG Property Name Check Error: {ex.Message}");
            }
            Settings = this.ReadCommonSettings<DataPluginDemoSettings>("GeneralSettings", () => new DataPluginDemoSettings());

            CurrentState = new SessionState();
            LogManager = new LogManager(CurrentState);
            LogManager.EnableLogSystem = Settings.EnableLogSystem;
            LogManager.EnableLogFuel = Settings.EnableLogFuel;
            LogManager.EnableLogStrategy = Settings.EnableLogStrategy;
            LogManager.EnableLogRadar = Settings.EnableLogRadar;
            LogManager.EnableLogOpponents = Settings.EnableLogOpponents;
            LogManager.EnableLogMicrosector = Settings.EnableLogMicrosector;
            LogManager.EnableLogWeather = Settings.EnableLogWeather;
            LogManager.EnableLogHardware = Settings.EnableLogHardware;
            LogManager.EnableLogVoice = Settings.EnableLogVoice;

            TelemetryReader = new TelemetryReader(pluginManager);
            FuelManager = new FuelManager();
            RaceAnalyzer = new RaceAnalyzer();
            PitRadar = new PitRadar();
            OpponentTracker = new OpponentTracker();
            TargetStrategyManager = new TargetStrategyManager();
            TyreManager = new TyreManager();
            HardwareManager = new SimRigHardwareManager();

            InitVoiceEngine();

            HardwareManager.OnHardwareInputReceived += HardwareManager_OnHardwareInputReceived;
            HardwareManager.OnHardwareConnected += HardwareManager_OnHardwareConnected;

            this.AttachDelegate("PersoSteeringWheelMode", () => _steeringWheelMode);
            this.AttachDelegate("PersoSteeringWheelMessage", () => _steeringWheelMessage);
            this.AttachDelegate("PersoSteeringWheelLiveBitePoint", () => _liveBitePoint);
            this.AttachDelegate("Enc_TopLeft_Label", () => _encoderLabels[0]);
            this.AttachDelegate("Enc_TopRight_Label", () => _encoderLabels[1]);
            this.AttachDelegate("Enc_BotLeft_Label", () => _encoderLabels[2]);
            this.AttachDelegate("Enc_BotRight_Label", () => _encoderLabels[3]);
            this.AttachDelegate("LeftWidgetPage", () => _leftWidgetPage);
            this.AttachDelegate("BottomLeftWidgetPage", () => _bottomLeftWidgetPage);
            this.AttachDelegate("BottomRightWidgetPage", () => _bottomRightWidgetPage);

            pluginManager.AddAction("LeftWidget_NextPage", this.GetType(), (a, b) => {
                _leftWidgetPage = (_leftWidgetPage + 1) % 2;
            });
            pluginManager.AddAction("LeftWidget_PrevPage", this.GetType(), (a, b) => {
                _leftWidgetPage = _leftWidgetPage == 0 ? 1 : 0;
            });

            pluginManager.AddAction("BottomLeftWidget_NextPage", this.GetType(), (a, b) => {
                _bottomLeftWidgetPage = (_bottomLeftWidgetPage + 1) % 4;
            });
            pluginManager.AddAction("BottomLeftWidget_PrevPage", this.GetType(), (a, b) => {
                _bottomLeftWidgetPage = (_bottomLeftWidgetPage + 3) % 4;
            });

            pluginManager.AddAction("BottomRightWidget_NextPage", this.GetType(), (a, b) => {
                _bottomRightWidgetPage = (_bottomRightWidgetPage + 1) % 4;
            });
            pluginManager.AddAction("BottomRightWidget_PrevPage", this.GetType(), (a, b) => {
                _bottomRightWidgetPage = (_bottomRightWidgetPage + 3) % 4;
            });

            pluginManager.AddAction("LeftLeaderboard_NextPage", this.GetType(), (a, b) => {
                _leftLeaderboardPageOffset = (_leftLeaderboardPageOffset + 1) % 4;
            });
            pluginManager.AddAction("LeftLeaderboard_PrevPage", this.GetType(), (a, b) => {
                _leftLeaderboardPageOffset = _leftLeaderboardPageOffset > 0 ? _leftLeaderboardPageOffset - 1 : 3;
            });

            pluginManager.AddAction("RightLeaderboard_NextPage", this.GetType(), (a, b) => {
                _rightLeaderboardPageOffset = (_rightLeaderboardPageOffset + 1) % 4;
            });
            pluginManager.AddAction("RightLeaderboard_PrevPage", this.GetType(), (a, b) => {
                _rightLeaderboardPageOffset = _rightLeaderboardPageOffset > 0 ? _rightLeaderboardPageOffset - 1 : 3;
            });

            RegisterSimHubProperties(pluginManager);

            ProfileManager.Init();
            for (int i = 0; i < 12; i++) ButtonColors[i] = Colors.White;
            if (!string.IsNullOrEmpty(Settings.LastProfileUsed))
            {
                SimRigProfile lastProfile = ProfileManager.LoadProfile(Settings.LastProfileUsed);
                if (lastProfile != null) ApplyProfile(lastProfile);
                else ApplyProfile(new SimRigProfile());
            }
            else ApplyProfile(new SimRigProfile());

            Task.Run(() => HardwareManager.RunDiscoveryAsync());
            UpdateSimHubProperties();
        }

        private void RegisterSimHubProperties(PluginManager pm)
        {
            Type t = this.GetType();
            pm.AddProperty("SimRIG.Input.TopLeftEncoder", t, "");
            pm.AddProperty("SimRIG.Input.TopRightEncoder", t, "");
            pm.AddProperty("SimRIG.Input.BottomLeftEncoder", t, "");
            pm.AddProperty("SimRIG.Input.BottomRightEncoder", t, "");

            pm.AddProperty("SimRIG.Fuel.UserOffset", t, 0.0);
            pm.AddProperty("SimRIG.Fuel.Step", t, 0.1);
            pm.AddProperty("SimRIG.Fuel.ActionMessage", t, "");
            pm.AddProperty("SimRIG.Fuel.FuelToAdd", t, 0.0);
            pm.AddProperty("SimRIG.Fuel.TankLapsRemaining", t, 0.0);
            pm.AddProperty("SimRIG.Fuel.EstimatedPitWindow", t, 0.0);
            pm.AddProperty("SimRIG.Fuel.EstimatedPitWindowTargetLap", t, 0.0);
            pm.AddProperty("SimRIG.Fuel.PitRequiredNumber", t, 0.0);
            // Y-1: consumo per giro necessario ad arrivare in fondo senza un'altra sosta.
            // Leggere SEMPRE FuelSavingAchievable prima: quando è false il target esiste ma
            // non è ottenibile guidando, e mostrarlo sarebbe un consiglio impossibile.
            pm.AddProperty("SimRIG.Fuel.FuelSaveTarget", t, 0.0);
            pm.AddProperty("SimRIG.Fuel.FuelSaveTargetStr", t, "--.-");
            pm.AddProperty("SimRIG.Fuel.FuelSavingRequiredPct", t, 0.0);
            pm.AddProperty("SimRIG.Fuel.FuelSavingAchievable", t, false);
            pm.AddProperty("SimRIG.Fuel.FuelDelta", t, 0.0);
            pm.AddProperty("SimRIG.Fuel.HistoricalPerLap", t, 0.0);
            pm.AddProperty("SimRIG.Fuel.LastLapFuelUsed", t, 0.0);
            pm.AddProperty("SimRIG.Fuel.CurrentTankLevel", t, 0.0);
            pm.AddProperty("SimRIG.Fuel.TargetFuel", t, 0.0);
            pm.AddProperty("SimRIG.Fuel.TargetEnabled", t, false);
            pm.AddProperty("SimRIG.Fuel.SyncEnabled", t, false);
            pm.AddProperty("SimRIG.Fuel.RaceStartingFuel", t, 0.0);

            pm.AddProperty("SimRIG.Session.LeaderRaceTotalLaps", t, 0.0);
            pm.AddProperty("SimRIG.Session.LeaderRaceLapsCompleted", t, 0);
            pm.AddProperty("SimRIG.Session.LeaderRaceLapsRemaining", t, 0.0);
            pm.AddProperty("SimRIG.Session.RaceTotalLaps", t, 0.0);
            // Dove sara' il Player quando esce la bandiera, col decimale (es. 34.80). La parte
            // decimale dice quanto manca a essere costretti a un giro in piu'.
            pm.AddProperty("SimRIG.Session.ProjectedPosAtCheckered", t, 0.0);
            // Dove sara' il LEADER ASSOLUTO quando scade il cronometro, col decimale (es. 38.85).
            pm.AddProperty("SimRIG.Session.LeaderProjectedPosAtCheckered", t, 0.0);
            // Punto 4: chi decide il momento della bandiera, e dove sara' allo scadere.
            pm.AddProperty("SimRIG.Session.FlagLeaderName", t, "");
            pm.AddProperty("SimRIG.Session.FlagLeaderProjectedPos", t, 0.0);
            // Posizione grezza del leader adesso, e la fotografia allo scadere del cronometro:
            // e' la verita' di terreno contro cui si verifica la proiezione (vedi RaceAnalyzer).
            pm.AddProperty("SimRIG.Session.LeaderTrackPct", t, 0.0);
            pm.AddProperty("SimRIG.Session.LeaderPosAtExpiry", t, -1.0);
            pm.AddProperty("SimRIG.Session.LeaderTrackPctAtExpiry", t, -1.0);
            pm.AddProperty("SimRIG.Session.LeaderNameAtExpiry", t, "");
            pm.AddProperty("SimRIG.Session.RaceLapsCompleted", t, 0);
            pm.AddProperty("SimRIG.Session.RaceLapsRemaining", t, 0.0);
            pm.AddProperty("SimRIG.Session.IsLapped", t, false);
            pm.AddProperty("SimRIG.Session.TimeLeftStr", t, "00:00.000");
            pm.AddProperty("SimRIG.Session.RaceLifeTimeLeftStr", t, "00:00.000");
            pm.AddProperty("SimRIG.Session.ClassTopSpeed", t, 0.0);
            pm.AddProperty("SimRIG.Session.ClassPaceDropDueToTyres", t, 0.0);
            pm.AddProperty("SimRIG.Session.ClassSectorPaceDropDueToTyres", t, 0.0);
            pm.AddProperty("SimRIG.Session.ClassSectorPaceDropDueToTyresRaw", t, 0.0);
            pm.AddProperty("SimRIG.Session.PlayerMicrosector", t, 0);
            pm.AddProperty("SimRIG.Session.ClassBestExtendedPitZoneTime", t, 0.0);
            // Y-9: limite di pit lane appreso osservando le vetture in corsia, per traccia+classe.
            // 0.0 finché non è stato imparato: la dash deve trattare lo zero come "non noto".
            pm.AddProperty("SimRIG.Session.PitLaneSpeedLimit", t, 0.0);
            pm.AddProperty("SimRIG.Session.PitLaneSpeedLimitKnown", t, false);

            pm.AddProperty("SimRIG.Strategy.IsPredictionValid", t, false);
            pm.AddProperty("SimRIG.Strategy.LeaderPaceStr", t, "00:00.000");
            pm.AddProperty("SimRIG.Strategy.LeaderPace", t, 0.0);
            pm.AddProperty("SimRIG.Strategy.Mode", t, "MANUAL");
            pm.AddProperty("SimRIG.Strategy.FuelCalculatorEnabled", t, Settings.EnableFuelCalculatorSystem);
            pm.AddProperty("SimRIG.Strategy.AutoPitEnabled", t, Settings.EnableAutoPitStrategy);

            pm.AddProperty("SimRIG.Strategy.RemainingPitsPlayer", t, 0.0);
            pm.AddProperty("SimRIG.Strategy.RemainingPitsLeader", t, 0.0);
            pm.AddProperty("SimRIG.Strategy.LeaderStintLaps", t, 0.0);
            pm.AddProperty("SimRIG.Strategy.LeaderAveragePace", t, 0.0);
            pm.AddProperty("SimRIG.Strategy.LeaderDataSource", t, "NONE");
            pm.AddProperty("SimRIG.Strategy.LeaderPitLossTime", t, 0.0);

            pm.AddProperty("SimRIG.Driver.NormalizedRaceStartPace", t, 0.0);
            pm.AddProperty("SimRIG.Driver.NormalizedRaceStartPaceStr", t, "00:00.000");
            pm.AddProperty("SimRIG.Driver.EstimatedCurrentPace", t, 0.0);
            pm.AddProperty("SimRIG.Driver.EstimatedCurrentPaceStr", t, "00:00.000");
            pm.AddProperty("SimRIG.Driver.PaceDropDueToTyres", t, 0.0);
            pm.AddProperty("SimRIG.Driver.RelativeDegradationToClass", t, 0.0);
            pm.AddProperty("SimRIG.Driver.SectorPaceDropDueToTyres", t, 0.0);
            pm.AddProperty("SimRIG.Driver.SectorPaceDropDueToTyresRaw", t, 0.0);
            pm.AddProperty("SimRIG.Driver.RelativeSectorDegradationToClass", t, 0.0);

            pm.AddProperty("SimRIG.Driver.LapBaselineNormalized", t, 0.0);
            pm.AddProperty("SimRIG.Driver.LapBaselineRaw", t, 0.0);
            pm.AddProperty("SimRIG.Driver.LapMovingAverageNormalized", t, 0.0);
            pm.AddProperty("SimRIG.Driver.LapMovingAverageRaw", t, 0.0);

            pm.AddProperty("SimRIG.Driver.SectorBaselineNormalized", t, 0.0);
            pm.AddProperty("SimRIG.Driver.SectorBaselineRaw", t, 0.0);
            pm.AddProperty("SimRIG.Driver.SectorMovingAverageNormalized", t, 0.0);
            pm.AddProperty("SimRIG.Driver.SectorMovingAverageRaw", t, 0.0);
            pm.AddProperty("SimRIG.Driver.LastLapTime", t, 0.0);
            pm.AddProperty("SimRIG.Driver.LastLapTimeStr", t, "00:00.000");

            pm.AddProperty("SimRIG.Driver.PrePitNormalizedAverage", t, 0.0);
            pm.AddProperty("SimRIG.Driver.FuelToAddTime", t, 0.0);
            pm.AddProperty("SimRIG.Driver.PostPitDelta0", t, 99.0);
            pm.AddProperty("SimRIG.Driver.PostPitDelta1", t, 99.0);
            pm.AddProperty("SimRIG.Driver.PostPitDelta2", t, 99.0);
            pm.AddProperty("SimRIG.Driver.PostPitPenalty0", t, 0.0);
            pm.AddProperty("SimRIG.Driver.PostPitPenalty1", t, 0.0);
            pm.AddProperty("SimRIG.Driver.PostPitPenalty2", t, 0.0);

            pm.AddProperty("SimRIG.Tyres.SelectionScope", t, "NONE");
            pm.AddProperty("SimRIG.Tyres.SelectedTireTime", t, 0.0);
            pm.AddProperty("SimRIG.Tyres.SelectedTireTimeStr", t, "0.0s");
            pm.AddProperty("SimRIG.Tyres.ActionMessage", t, "");
            pm.AddProperty("SimRIG.Pit.SelectedTireTime", t, 0.0);
            pm.AddProperty("SimRIG.Pressure.UserOffset", t, 0.0);
            pm.AddProperty("SimRIG.Pressure.UserOffsetStr", t, "+0.00 bar");
            pm.AddProperty("SimRIG.Pressure.OffsetKpa", t, 0);
            pm.AddProperty("SimRIG.Pressure.ActionMessage", t, "");

            pm.AddProperty("SimRIG.Services.WindshieldActive", t, true);
            pm.AddProperty("SimRIG.Services.FastRepairActive", t, false);
            pm.AddProperty("SimRIG.Services.ActionMessage", t, "");

            pm.AddProperty("SimRIG.Target.Mode", t, "AHEAD");
            pm.AddProperty("SimRIG.Target.Name", t, "--");
            pm.AddProperty("SimRIG.Target.Position", t, 0);
            pm.AddProperty("SimRIG.Target.GapSeconds", t, 0.0);
            pm.AddProperty("SimRIG.Target.GapString", t, "");
            pm.AddProperty("SimRIG.Target.RelativePace", t, 0.0);
            pm.AddProperty("SimRIG.Target.RelativePaceStr", t, "0.0s/lap");
            // Delta grezzo del gap per macrosettore. Affianca RelativePace senza sostituirlo:
            // unità diversa (s/settore, non s/giro), quindi la dash va aggiornata esplicitamente.
            pm.AddProperty("SimRIG.Target.RelativeGapDelta", t, 0.0);
            pm.AddProperty("SimRIG.Target.RelativeGapDeltaStr", t, "--.--s/sector");
            pm.AddProperty("SimRIG.Target.RelativeGapDeltaValid", t, false);
            pm.AddProperty("SimRIG.Target.TopSpeed", t, 0.0);
            pm.AddProperty("SimRIG.Target.CurrentSpeed", t, 0.0);
            pm.AddProperty("SimRIG.Target.Diagnosis", t, "ANALYZING");
            pm.AddProperty("SimRIG.Target.NormalizedRaceStartPace", t, 0.0);
            pm.AddProperty("SimRIG.Target.NormalizedRaceStartPaceStr", t, "00:00.000");
            pm.AddProperty("SimRIG.Target.PaceDeficit", t, 0.0);
            pm.AddProperty("SimRIG.Target.PaceDeficitStr", t, "0.0s/lap");
            pm.AddProperty("SimRIG.Target.RelativeDegradation", t, 0.0);
            pm.AddProperty("SimRIG.Target.SectorPaceDropDueToTyres", t, 0.0);
            pm.AddProperty("SimRIG.Target.SectorPaceDropDueToTyresRaw", t, 0.0);
            pm.AddProperty("SimRIG.Target.RelativeSectorDegradation", t, 0.0);

            pm.AddProperty("SimRIG.Target.LapBaselineNormalized", t, 0.0);
            pm.AddProperty("SimRIG.Target.LapBaselineRaw", t, 0.0);
            pm.AddProperty("SimRIG.Target.LapMovingAverageNormalized", t, 0.0);
            pm.AddProperty("SimRIG.Target.LapMovingAverageRaw", t, 0.0);

            pm.AddProperty("SimRIG.Target.SectorBaselineNormalized", t, 0.0);
            pm.AddProperty("SimRIG.Target.SectorBaselineRaw", t, 0.0);
            pm.AddProperty("SimRIG.Target.SectorMovingAverageNormalized", t, 0.0);
            pm.AddProperty("SimRIG.Target.SectorMovingAverageRaw", t, 0.0);
            pm.AddProperty("SimRIG.Target.UndercutAdvantage", t, 0.0);
            pm.AddProperty("SimRIG.Target.UndercutCaptureMargin", t, 0.0);
            pm.AddProperty("SimRIG.Target.UndercutCaptureMarginStr", t, "+0.0s");
            pm.AddProperty("SimRIG.Target.UndercutViable", t, false);
            
            pm.AddProperty("SimRIG.Target.OvercutAdvantage", t, 0.0);
            pm.AddProperty("SimRIG.Target.OvercutCaptureMargin", t, 0.0);
            pm.AddProperty("SimRIG.Target.OvercutCaptureMarginStr", t, "+0.0s");
            pm.AddProperty("SimRIG.Target.OvercutViable", t, false);

            pm.AddProperty("SimRIG.Target.TargetLapsUntilPit", t, 0.0);
            pm.AddProperty("SimRIG.Target.ReactionDeltaLaps", t, 0.0);
            pm.AddProperty("SimRIG.Target.OvercutStayLaps", t, 0.0);
            pm.AddProperty("SimRIG.Target.UndercutPositionOK", t, false);
            pm.AddProperty("SimRIG.Target.UndercutFuelOK", t, false);
            pm.AddProperty("SimRIG.Target.UndercutTrafficOK", t, false);
            pm.AddProperty("SimRIG.Target.OvercutFuelOK", t, false);
            pm.AddProperty("SimRIG.Target.OvercutTrafficOK", t, false);
            pm.AddProperty("SimRIG.Target.TargetPittedRecently", t, false);

            pm.AddProperty("SimRIG.Target.ProjectedMergeGap", t, 0.0);
            pm.AddProperty("SimRIG.Target.TrafficAlert", t, false);
            pm.AddProperty("SimRIG.Target.TargetMode", t, "UNKNOWN");
            pm.AddProperty("SimRIG.Target.CurrentTank", t, 0.0);
            pm.AddProperty("SimRIG.Target.TankLapsRemaining", t, 0.0);
            pm.AddProperty("SimRIG.Target.SpeedDrop", t, 0.0);
            pm.AddProperty("SimRIG.Target.CurrentMicrosector", t, 0);
            pm.AddProperty("SimRIG.Target.ProjectedStationaryTime", t, 0.0);
            pm.AddProperty("SimRIG.Target.PitCount", t, 0);
            pm.AddProperty("SimRIG.Target.CalculatedStationaryTime", t, 0.0);
            pm.AddProperty("SimRIG.Target.InOutPitAccDecTime", t, 0.0);
            pm.AddProperty("SimRIG.Target.EstimatedFuelToAdd", t, 0.0);
            pm.AddProperty("SimRIG.Target.EstimatedFuelAdded", t, 0.0);
            pm.AddProperty("SimRIG.Target.EstimatedStationaryTime", t, 0.0);
            pm.AddProperty("SimRIG.Target.EstimatedFuelTank", t, 0.0);
            pm.AddProperty("SimRIG.Target.LapCount", t, 0);
            pm.AddProperty("SimRIG.Target.EstimatedPitWindow", t, 0.0);
            pm.AddProperty("SimRIG.Target.EstimatedPitWindowTargetLap", t, 0.0);
            pm.AddProperty("SimRIG.Session.ClassRaceStartingFuel", t, 0.0);
            pm.AddProperty("SimRIG.Session.PitLayoutMode", t, "CALIBRATING");
            pm.AddProperty("SimRIG.Session.IsTrackWet", t, false);
            pm.AddProperty("SimRIG.Session.TrackWetnessLevel", t, 0);
            pm.AddProperty("SimRIG.Session.WindSpeed", t, 0.0);
            pm.AddProperty("SimRIG.Session.AirTemperature", t, 0.0);
            pm.AddProperty("SimRIG.Session.TimeToImpactMinutes", t, 99.0);
            pm.AddProperty("SimRIG.Session.TimeToImpactLaps", t, 99.0);
            pm.AddProperty("SimRIG.Session.WetPaceLoss", t, 0.0);
            pm.AddProperty("SimRIG.Session.WeatherAlert", t, "ASCIUTTO");
            pm.AddProperty("SimRIG.Session.IsPlayerOnSlick", t, true);
            pm.AddProperty("SimRIG.Session.CrossoverAlertState", t, "NONE");
            pm.AddProperty("SimRIG.Session.CrossoverDeltaSeconds", t, 0.0);

            pm.AddProperty("SimRIG.Session.RelativeHumidity", t, 0.0);
            pm.AddProperty("SimRIG.Session.BarometricPressure", t, 0.0);
            pm.AddProperty("SimRIG.Session.PressureTrend", t, 0);
            pm.AddProperty("SimRIG.Session.TrackTemperature", t, 0.0);
            pm.AddProperty("SimRIG.Session.WindDirectionRelative", t, 0.0);
            pm.AddProperty("SimRIG.Session.AbsoluteWindDirection", t, 0.0);

            pm.AddProperty("SimRIG.BottomLeftWidgetPage", t, 0);
            pm.AddProperty("SimRIG.BottomRightWidgetPage", t, 0);

            // Formatted string values (based on user settings)
            pm.AddProperty("SimRIG.Units.SpeedUnit", t, "km/h");
            pm.AddProperty("SimRIG.Units.TempUnit", t, "°C");
            pm.AddProperty("SimRIG.Units.PressureUnit", t, "bar");

            pm.AddProperty("SimRIG.Vehicle.Speed", t, 0.0);
            pm.AddProperty("SimRIG.Vehicle.SpeedStr", t, "0 km/h");

            pm.AddProperty("SimRIG.Session.AirTemperatureStr", t, "0.0 °C");
            pm.AddProperty("SimRIG.Session.TrackTemperatureStr", t, "0.0 °C");
            pm.AddProperty("SimRIG.Session.WindSpeedStr", t, "0.0 km/h");
            pm.AddProperty("SimRIG.Session.AirPressureStr", t, "0.0 hPa");
            pm.AddProperty("SimRIG.Session.RelativeHumidityStr", t, "0%");
            pm.AddProperty("SimRIG.Session.TrackWetnessStr", t, "0%");
            pm.AddProperty("SimRIG.Session.WindDirectionLabel", t, "HEADWIND");
            pm.AddProperty("SimRIG.Session.PressureTrendStr", t, "STABILE");
            pm.AddProperty("SimRIG.Forecast.ManualCrossoverOffset", t, 0.0);
            pm.AddProperty("SimRIG.Forecast.ManualCrossoverOffsetStr", t, "AUTO");
            pm.AddProperty("SimRIG.Forecast.AlertThreshold", t, 10.0);
            pm.AddProperty("SimRIG.Forecast.AlertThresholdStr", t, "10%");

            pm.AddProperty("SimRIG.Target.PrePitNormalizedAverage", t, 0.0);
            pm.AddProperty("SimRIG.Target.FuelToAddTime", t, 0.0);
            pm.AddProperty("SimRIG.Target.PostPitDelta0", t, 99.0);
            pm.AddProperty("SimRIG.Target.PostPitDelta1", t, 99.0);
            pm.AddProperty("SimRIG.Target.PostPitDelta2", t, 99.0);
            pm.AddProperty("SimRIG.Target.PostPitPenalty0", t, 0.0);
            pm.AddProperty("SimRIG.Target.PostPitPenalty1", t, 0.0);
            pm.AddProperty("SimRIG.Target.PostPitPenalty2", t, 0.0);

            pm.AddProperty("SimRIG.Target.DropLow", t, 0.0);
            pm.AddProperty("SimRIG.Target.DropMid", t, 0.0);
            pm.AddProperty("SimRIG.Target.DropHigh", t, 0.0);
            pm.AddProperty("SimRIG.Target.BestLow", t, 0.0);
            pm.AddProperty("SimRIG.Target.BestMid", t, 0.0);
            pm.AddProperty("SimRIG.Target.BestHigh", t, 0.0);
            pm.AddProperty("SimRIG.Target.BestLapNumber", t, 0);

            pm.AddProperty("SimRIG.Player.DropLow", t, 0.0);
            pm.AddProperty("SimRIG.Player.DropMid", t, 0.0);
            pm.AddProperty("SimRIG.Player.DropHigh", t, 0.0);
            pm.AddProperty("SimRIG.Player.BestLow", t, 0.0);
            pm.AddProperty("SimRIG.Player.BestMid", t, 0.0);
            pm.AddProperty("SimRIG.Player.BestHigh", t, 0.0);
            pm.AddProperty("SimRIG.Player.BestLapNumber", t, 0);
            pm.AddProperty("SimRIG.Player.Diagnosis", t, "ANALYZING");
            pm.AddProperty("SimRIG.Player.SelectedTyreCompound", t, "DRY");

            pm.AddProperty("SimRIG.Pit.StationaryTimeLoss", t, 0.0);
            pm.AddProperty("SimRIG.Pit.TransitTime", t, 0.0);
            pm.AddProperty("SimRIG.Pit.TransitDriveThrough", t, 0.0);
            pm.AddProperty("SimRIG.Pit.MeasuredFuelFillRate", t, 0.0);
            pm.AddProperty("SimRIG.Pit.CalibrationStatus", t, "ANALYZING");
            pm.AddProperty("SimRIG.Pit.CalibrationStep", t, "");
            // Cosa manca ancora, in chiaro. "READY" da solo non dice al pilota cosa fare.
            pm.AddProperty("SimRIG.Pit.CalibrationMissing", t, "");
            pm.AddProperty("SimRIG.Pit.GeofenceCalibrated", t, false);
            // Confidenza dei tre dati: un valore stimato funziona, ma una calibrazione vera
            // lo migliorerebbe, e il pilota deve poterlo sapere.
            pm.AddProperty("SimRIG.Pit.GeofenceConfidence", t, "Unknown");
            pm.AddProperty("SimRIG.Pit.FuelRateConfidence", t, "Unknown");
            pm.AddProperty("SimRIG.Pit.TyreTimeConfidence", t, "Unknown");
            pm.AddProperty("SimRIG.Pit.PlayerPitCount", t, 0);
            pm.AddProperty("SimRIG.Pit.LeaderPitCount", t, 0);
            pm.AddProperty("SimRIG.Pit.PitLaneZoneRacingTime", t, 0.0);
            pm.AddProperty("SimRIG.Pit.FuelCapacityInTireTime", t, 0.0);
            pm.AddProperty("SimRIG.Pit.InOutPitAccDecTime", t, 0.0);
            pm.AddProperty("SimRIG.Pit.TotalStationaryTime", t, 0.0);
            pm.AddProperty("SimRIG.Pit.TotalStationaryTimeStr", t, "0.0s");
            pm.AddProperty("SimRIG.Pit.TotalPitLoss", t, 0.0);
            pm.AddProperty("SimRIG.Pit.TotalPitLossStr", t, "0.0s");
            pm.AddProperty("SimRIG.Pit.IsSequential", t, false);
            pm.AddProperty("SimRIG.Pit.PitLayoutMode", t, "CALIBRATING");

            pm.AddProperty("SimRIG.Driver.ExtendedSectorRacingZoneState", t, "NORMAL");
            pm.AddProperty("SimRIG.Leader.ExtendedSectorRacingZoneState", t, "NORMAL");
            pm.AddProperty("SimRIG.Leader.ExtendedSectorRacingZone", t, 0.0);

            pm.AddProperty("SimRIG.Driver.ExtendedSectorRacingZone", t, 0.0);
            pm.AddProperty("SimRIG.Target.ExtendedSectorRacingZone", t, 0.0);

            pm.AddProperty("SimRIG.Driver.ExtendedPitZoneTime", t, 0.0);
            pm.AddProperty("SimRIG.Target.ExtendedPitZoneTime", t, 0.0);
            pm.AddProperty("SimRIG.Driver.IsInPit", t, false);
            pm.AddProperty("SimRIG.Target.IsInPit", t, false);
            pm.AddProperty("SimRIG.Leader.IsInPit", t, false);

            pm.AddProperty("SimRIG.Driver.BestExtendedSectorRacingZone", t, 0.0);
            pm.AddProperty("SimRIG.Driver.BestExtendedSectorRacingZoneLapCount", t, 0);
            pm.AddProperty("SimRIG.Target.BestExtendedSectorRacingZone", t, 0.0);
            pm.AddProperty("SimRIG.Target.BestExtendedSectorRacingZoneLapCount", t, 0);

            pm.AddProperty("SimRIG.Diagnostics.PitEntryPct", t, -1.0);
            pm.AddProperty("SimRIG.Diagnostics.PitExitPct", t, -1.0);
            pm.AddProperty("SimRIG.Diagnostics.ExtendedPitEntryPct", t, -1.0);
            pm.AddProperty("SimRIG.Diagnostics.ExtendedPitExitPct", t, -1.0);

            // Sectors
            pm.AddProperty("SimRIG.Player.Sector1Str", t, "--.--");
            pm.AddProperty("SimRIG.Player.Sector2Str", t, "--.--");
            pm.AddProperty("SimRIG.Player.Sector3Str", t, "--.--");
            pm.AddProperty("SimRIG.Player.Sector1Color", t, "#FFFFFFFF");
            pm.AddProperty("SimRIG.Player.Sector2Color", t, "#FFFFFFFF");
            pm.AddProperty("SimRIG.Player.Sector3Color", t, "#FFFFFFFF");
            pm.AddProperty("SimRIG.Player.BestSector1Str", t, "--.--");
            pm.AddProperty("SimRIG.Player.BestSector2Str", t, "--.--");
            pm.AddProperty("SimRIG.Player.BestSector3Str", t, "--.--");
            pm.AddProperty("SimRIG.Player.LastSectorStr", t, "0.000");

            // Left Leaderboard
            pm.AddProperty("SimRIG.Left.Class.HeaderStr", t, "CLASS LEADERBOARD");
            pm.AddProperty("SimRIG.Left.Overall.HeaderStr", t, "OVERALL LEADERBOARD");
            pm.AddProperty("SimRIG.Left.Relative.HeaderStr", t, "RELATIVE LEADERBOARD");

            // Right Leaderboard
            pm.AddProperty("SimRIG.Right.Class.HeaderStr", t, "CLASS LEADERBOARD");
            pm.AddProperty("SimRIG.Right.Overall.HeaderStr", t, "OVERALL LEADERBOARD");
            pm.AddProperty("SimRIG.Right.Relative.HeaderStr", t, "RELATIVE LEADERBOARD");

            // Relative (R1 to R7)
            for (int i = 1; i <= 7; i++)
            {
                pm.AddProperty("SimRIG.Relative.R" + i + "_Pos", t, "--");
                pm.AddProperty("SimRIG.Relative.R" + i + "_Name", t, "---");
                pm.AddProperty("SimRIG.Relative.R" + i + "_Gap", t, "---");
                pm.AddProperty("SimRIG.Relative.R" + i + "_LastLap", t, "--:--.---");
                pm.AddProperty("SimRIG.Relative.R" + i + "_Class", t, "");

                // Left Widget Leaderboards
                pm.AddProperty("SimRIG.Left.Class.C" + i + "_Pos", t, "P" + i);
                pm.AddProperty("SimRIG.Left.Class.C" + i + "_Name", t, "---");
                pm.AddProperty("SimRIG.Left.Class.C" + i + "_Gap", t, "---");
                pm.AddProperty("SimRIG.Left.Class.C" + i + "_Class", t, "");
                pm.AddProperty("SimRIG.Left.Class.C" + i + "_Color", t, "#FFFFFFFF");

                pm.AddProperty("SimRIG.Left.Overall.O" + i + "_Pos", t, "P" + i);
                pm.AddProperty("SimRIG.Left.Overall.O" + i + "_Name", t, "---");
                pm.AddProperty("SimRIG.Left.Overall.O" + i + "_Gap", t, "---");
                pm.AddProperty("SimRIG.Left.Overall.O" + i + "_Class", t, "");
                pm.AddProperty("SimRIG.Left.Overall.O" + i + "_Color", t, "#FFFFFFFF");

                pm.AddProperty("SimRIG.Left.Relative.R" + i + "_Pos", t, "--");
                pm.AddProperty("SimRIG.Left.Relative.R" + i + "_Name", t, "---");
                pm.AddProperty("SimRIG.Left.Relative.R" + i + "_Gap", t, "---");
                pm.AddProperty("SimRIG.Left.Relative.R" + i + "_LastLap", t, "--:--.---");
                pm.AddProperty("SimRIG.Left.Relative.R" + i + "_Class", t, "");
                pm.AddProperty("SimRIG.Left.Relative.R" + i + "_Color", t, "#FFFFFFFF");

                // Right Widget Leaderboards
                pm.AddProperty("SimRIG.Right.Class.C" + i + "_Pos", t, "P" + i);
                pm.AddProperty("SimRIG.Right.Class.C" + i + "_Name", t, "---");
                pm.AddProperty("SimRIG.Right.Class.C" + i + "_Gap", t, "---");
                pm.AddProperty("SimRIG.Right.Class.C" + i + "_Class", t, "");
                pm.AddProperty("SimRIG.Right.Class.C" + i + "_Color", t, "#FFFFFFFF");

                pm.AddProperty("SimRIG.Right.Overall.O" + i + "_Pos", t, "P" + i);
                pm.AddProperty("SimRIG.Right.Overall.O" + i + "_Name", t, "---");
                pm.AddProperty("SimRIG.Right.Overall.O" + i + "_Gap", t, "---");
                pm.AddProperty("SimRIG.Right.Overall.O" + i + "_Class", t, "");
                pm.AddProperty("SimRIG.Right.Overall.O" + i + "_Color", t, "#FFFFFFFF");

                pm.AddProperty("SimRIG.Right.Relative.R" + i + "_Pos", t, "--");
                pm.AddProperty("SimRIG.Right.Relative.R" + i + "_Name", t, "---");
                pm.AddProperty("SimRIG.Right.Relative.R" + i + "_Gap", t, "---");
                pm.AddProperty("SimRIG.Right.Relative.R" + i + "_LastLap", t, "--:--.---");
                pm.AddProperty("SimRIG.Right.Relative.R" + i + "_Class", t, "");
                pm.AddProperty("SimRIG.Right.Relative.R" + i + "_Color", t, "#FFFFFFFF");
            }
        }

        private void HardwareManager_OnHardwareConnected(object sender, EventArgs e)
        {
            if (HardwareManager.IsLedsConnected) ApplyProfile(CurrentProfile);
            if (HardwareManager.IsInputConnected)
            {
                HardwareManager.SendEncoderMapping(0, Settings.TopLeftEncoderMode);
                HardwareManager.SendEncoderMapping(1, Settings.TopRightEncoderMode);
                HardwareManager.SendInputSystemState(Settings.EnableFuelCalculatorSystem);
                HardwareManager.SyncInputChip();
            }
        }

        private void HardwareManager_OnHardwareInputReceived(object sender, HardwareInputEventArgs e)
        {
            if (e.CommandType == "MODE")
            {
                _steeringWheelMode = e.RawValue;
                PluginManager.SetPropertyValue("SimRIG.Mode", this.GetType(), _steeringWheelMode);
                UpdateSimHubProperties();
            }
            else if (e.CommandType == "MSG") _steeringWheelMessage = e.RawValue;
            else if (e.CommandType == "VAL")
            {
                if (double.TryParse(e.RawValue, NumberStyles.Any, CultureInfo.InvariantCulture, out double val))
                    _liveBitePoint = val;
            }
            else if (e.CommandType == "IDX")
            {
                string[] parts = e.RawValue.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[0], out int encIdx) && int.TryParse(parts[1], out int modeIdx))
                {
                    if (encIdx >= 2 && encIdx < 4 && modeIdx >= 0 && modeIdx < FunctionList.Length)
                    {
                        _encoderLabels[encIdx] = FunctionList[modeIdx];
                        UpdateSimHubProperties();
                    }
                }
            }
            else if (e.CommandType == "ENC") HandleEncoderRotation(e.EncoderIndex, e.DirectionOrState);
            else if (e.CommandType == "PUSH") HandleEncoderPush(e.EncoderIndex, e.DirectionOrState == 1);
        }

        private void HandleEncoderRotation(int encIdx, int dir)
        {
            if (!Settings.EnableFuelCalculatorSystem) return;

            if (_steeringWheelMode == "PIT")
            {
                if (encIdx == 0) FuelManager.AddUserOffset(dir);
                else if (encIdx == 1) TyreManager.CycleTyreScope(dir);
                else if (encIdx == 2)
                {
                    if (dir > 0) FuelManager.CycleStrategyMode();
                    else
                    {
                        _windshieldActive = !_windshieldActive;
                        SetCornerMessage(2, _windshieldActive ? "WS: ON" : "WS: OFF", 1500);
                    }
                }
                else if (encIdx == 3) TyreManager.AdjustPressure(dir, Settings.PressureUnit);
            }
            else if (_steeringWheelMode == "PIT2")
            {
                if (encIdx == 0)
                {
                    double step = 0.05;
                    double current = FuelManager.Calculations.FuelPerLapTarget;
                    current += (dir * step);
                    if (current < 0.1) current = 0.1;
                    FuelManager.SetFuelTarget(current);
                    SetCornerMessage(0, $"TGT: {FuelManager.Calculations.FuelPerLapTarget:0.00}", 1500);
                }
                else if (encIdx == 1)
                {
                    FuelManager.Calculations.IsSyncModeEnabled = !FuelManager.Calculations.IsSyncModeEnabled;
                    SetCornerMessage(1, FuelManager.Calculations.IsSyncModeEnabled ? "SYNC: ON" : "SYNC: OFF", 1500);
                }
                else if (encIdx == 2)
                {
                    _fastRepairActive = !_fastRepairActive;
                    if (_fastRepairActive) MacroManager.SendChatCommand("#fastrepair");
                    else MacroManager.SendChatCommand("#clear fr");
                    SetCornerMessage(2, _fastRepairActive ? "FAST REP: ON" : "FAST REP: OFF", 2000);
                }
            }
            else if (_steeringWheelMode == "STRAT")
            {
                if (encIdx == 0)
                {
                    TargetStrategyManager.LatchedTargetName = null;
                    _targetSelectionIndex += dir;
                    int maxIndex = 6 + (CurrentState.Opponents?.Count ?? 0);
                    if (_targetSelectionIndex < 0) _targetSelectionIndex = maxIndex;
                    else if (_targetSelectionIndex > maxIndex) _targetSelectionIndex = 0;
                    SetCornerMessage(0, $"TGT: {GetTargetModeName(_targetSelectionIndex)}", 1500);
                }
            }
            else if (_steeringWheelMode == "FORECAST")
            {
                if (encIdx == 0)
                {
                    _alertThreshold += dir * 5;
                    if (_alertThreshold < 5) _alertThreshold = 5;
                    if (_alertThreshold > 100) _alertThreshold = 100;
                    SetCornerMessage(0, $"ALERT: {_alertThreshold:0}%", 1500);
                }
                else if (encIdx == 1)
                {
                    _manualCrossoverOffset += dir * 0.1;
                    _manualCrossoverOffset = Math.Round(_manualCrossoverOffset, 1);
                    if (_manualCrossoverOffset < -2.0) _manualCrossoverOffset = -2.0;
                    if (_manualCrossoverOffset > 2.0) _manualCrossoverOffset = 2.0;
                    string sign = _manualCrossoverOffset > 0 ? "+" : "";
                    SetCornerMessage(1, $"CROSS: {sign}{_manualCrossoverOffset:0.0}s", 1500);
                }
            }
            UpdateSimHubProperties();
        }

        private void HandleEncoderPush(int encIdx, bool isPressed)
        {
            if (!Settings.EnableFuelCalculatorSystem) return;
            if (encIdx < 0 || encIdx > 3) return;

            if (isPressed)
            {
                if (_pushTokens[encIdx] != null) _pushTokens[encIdx].Dispose();
                _pushTokens[encIdx] = new CancellationTokenSource();
                var token = _pushTokens[encIdx].Token;
                _longPressExecuted[encIdx] = false;
                Task.Run(async () => {
                    try
                    {
                        await Task.Delay(1000, token);
                        if (!token.IsCancellationRequested) { _longPressExecuted[encIdx] = true; ExecuteLongPressAction(encIdx); }
                    }
                    catch { }
                });
            }
            else
            {
                if (_pushTokens[encIdx] != null) { _pushTokens[encIdx].Cancel(); _pushTokens[encIdx].Dispose(); }
                if (!_longPressExecuted[encIdx]) ExecuteShortPressAction(encIdx);
            }
        }

        private void ExecuteShortPressAction(int encIdx)
        {
            if (_steeringWheelMode == "PIT")
            {
                if (encIdx == 0) { FuelManager.CycleFuelStep(); SetCornerMessage(0, $"STEP: {FuelManager.Calculations.FuelStep}L", 1500); }
                else if (encIdx == 1)
                {
                    TyreManager.ToggleCompound();
                    string compLabel = TyreManager.SelectedWetCompound ? "WET" : "DRY";
                    SetCornerMessage(1, $"{compLabel} SELECTED", 2000);
                }
                else if (encIdx == 2)
                {
                    _fastRepairActive = !_fastRepairActive;
                    if (_fastRepairActive) MacroManager.SendChatCommand("#fastrepair");
                    else MacroManager.SendChatCommand("#clear fr");
                    SetCornerMessage(2, _fastRepairActive ? "FAST REP: ON" : "FAST REP: OFF", 2000);
                }
                else if (encIdx == 3) { TyreManager.ResetPressureOnly(); MacroManager.SendChatCommand("#lf 0kpa #rf 0kpa #lr 0kpa #rr 0kpa"); SetCornerMessage(3, "RESET (SENT 0)", 1500); }
            }
            else if (_steeringWheelMode == "PIT2")
            {
                if (encIdx == 0)
                {
                    FuelManager.Calculations.IsTargetModeEnabled = !FuelManager.Calculations.IsTargetModeEnabled;
                    SetCornerMessage(0, FuelManager.Calculations.IsTargetModeEnabled ? "TARGET: ON" : "TARGET: OFF", 1500);
                }
                else if (encIdx == 1)
                {
                    FuelManager.Calculations.IsSyncModeEnabled = !FuelManager.Calculations.IsSyncModeEnabled;
                    SetCornerMessage(1, FuelManager.Calculations.IsSyncModeEnabled ? "SYNC: ON" : "SYNC: OFF", 1500);
                }
                else if (encIdx == 2)
                {
                    _fastRepairActive = !_fastRepairActive;
                    if (_fastRepairActive) MacroManager.SendChatCommand("#fastrepair");
                    else MacroManager.SendChatCommand("#clear fr");
                    SetCornerMessage(2, _fastRepairActive ? "FAST REP: ON" : "FAST REP: OFF", 2000);
                }
            }
            else if (_steeringWheelMode == "STRAT")
            {
                if (encIdx == 0)
                {
                    if (!string.IsNullOrEmpty(TargetStrategyManager.LatchedTargetName))
                    {
                        TargetStrategyManager.LatchedTargetName = null;
                        SetCornerMessage(0, "TGT UNLOCKED", 1500);
                    }
                    else
                    {
                        var current = TargetStrategyManager.CurrentTarget;
                        if (current != null && !string.IsNullOrEmpty(current.Name) && current.Name != "NO TARGET" && current.Name != "PLAYER")
                        {
                            TargetStrategyManager.LatchedTargetName = current.Name;
                            SetCornerMessage(0, $"LOCK: {current.Name}", 1500);
                        }
                        else
                        {
                            SetCornerMessage(0, "NO CAR TO LOCK", 1500);
                        }
                    }
                }
            }
            else if (_steeringWheelMode == "FORECAST")
            {
                if (encIdx == 0)
                {
                    _alertThreshold = 10.0;
                    _manualCrossoverOffset = 0.0;
                    SetCornerMessage(0, "RESET AUTO", 1500);
                }
                else if (encIdx == 1)
                {
                    TriggerWeatherReportSpeech();
                }
            }
            UpdateSimHubProperties();
        }

        public double GetPlayerTireChangeTime()
        {
            double baseTime = PitRadar != null ? PitRadar.DbTireChangeTime : 26.0;
            switch (TyreManager.CurrentScope)
            {
                case TyreSelectionScope.None: return 0.0;
                case TyreSelectionScope.All4: return baseTime;
                case TyreSelectionScope.Fronts:
                case TyreSelectionScope.Rears:
                case TyreSelectionScope.Left:
                case TyreSelectionScope.Right: return baseTime * 0.5;
                case TyreSelectionScope.FL:
                case TyreSelectionScope.FR:
                case TyreSelectionScope.RL:
                case TyreSelectionScope.RR: return baseTime * 0.25;
                default: return baseTime;
            }
        }

        private void SpeakTyreConfirmation(TyreSelectionScope scope)
        {
            if (scope == TyreSelectionScope.None)
            {
                TriggerRadioVoice("CONFIRM_JUST_TYRES_NONE");
                return;
            }

            int variant = _rng.Next(0, 2);

            if (scope == TyreSelectionScope.FL || scope == TyreSelectionScope.FR || 
                scope == TyreSelectionScope.RL || scope == TyreSelectionScope.RR)
            {
                string cornerName = PitWallLanguage.GetPhrase(Settings.VoiceLanguage, $"CORNER_{scope.ToString().ToUpper()}");
                TriggerRadioVoice($"CONFIRM_TYRES_SINGLE_{variant}", cornerName);
            }
            else
            {
                string key = $"CONFIRM_TYRES_{scope.ToString().ToUpper()}_{variant}";
                TriggerRadioVoice(key);
            }
        }

        private string GetConfirmFuelTyreKey(TyreSelectionScope scope)
        {
            if (scope == TyreSelectionScope.All4) return "CONFIRM_FULL";
            if (scope == TyreSelectionScope.Fronts || scope == TyreSelectionScope.Rears || scope == TyreSelectionScope.Left || scope == TyreSelectionScope.Right) return "CONFIRM_2TYRES";
            if (scope != TyreSelectionScope.None) return "CONFIRM_1TYRE";
            return "CONFIRM_FUEL";
        }

        private string CleanNameForSpeech(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            string cleaned = name.Replace("_", " ");
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\d+", "");
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s+", " ").Trim();
            return cleaned;
        }

        private void ExecuteLongPressAction(int encIdx)
        {
            if (_steeringWheelMode == "PIT" || _steeringWheelMode == "PIT2")
            {
                if (encIdx == 0)
                {
                    double val = FuelManager.Calculations.FuelToAdd;
                    string cmd = string.Format(CultureInfo.InvariantCulture, "#fuel {0}l", val);
                    if (_windshieldActive) cmd += " #ws";
                    MacroManager.SendChatCommand(cmd);
                    SetCornerMessage(0, $"SENT: {val}L", 2500);

                    if (_interactivePitDialogueActive)
                    {
                        if (TyreManager.CurrentScope != TyreSelectionScope.None)
                            TriggerRadioVoice("CONFIRM_INTERACTIVE_BOTH");
                        else
                            TriggerRadioVoice("CONFIRM_INTERACTIVE_FUEL");
                        _interactivePitDialogueActive = false;
                    }
                    else
                    {
                        if (TyreManager.CurrentScope != TyreSelectionScope.None)
                            TriggerRadioVoice(GetConfirmFuelTyreKey(TyreManager.CurrentScope), val.ToString("F1", CultureInfo.InvariantCulture));
                        else
                            TriggerRadioVoice("CONFIRM_FUEL", val.ToString("F1", CultureInfo.InvariantCulture));
                    }
                }
                else if (encIdx == 1)
                {
                    string tyreCmd = TyreManager.GetTyreCommandString();
                    tyreCmd += TyreManager.SelectedWetCompound ? " #tc 2" : " #tc 1";
                    MacroManager.SendChatCommand(tyreCmd);
                    string compLabel = TyreManager.SelectedWetCompound ? "WET" : "DRY";
                    SetCornerMessage(1, $"SENT: {TyreManager.CurrentScope} ({compLabel})", 2500);

                    if (_interactivePitDialogueActive)
                    {
                        if (TyreManager.CurrentScope == TyreSelectionScope.None)
                            SpeakTyreConfirmation(TyreSelectionScope.None);
                        else
                            TriggerRadioVoice("CONFIRM_INTERACTIVE_TYRES");
                        _interactivePitDialogueActive = false;
                    }
                    else
                    {
                        SpeakTyreConfirmation(TyreManager.CurrentScope);
                    }
                }
                else if (encIdx == 2)
                {
                    FuelManager.ResetUserOffset();
                    TyreManager.ResetAll();
                    MacroManager.SendChatCommand("#clear");
                    SetCornerMessage(2, "ALL CLEARED & RESET", 2500);
                    _interactivePitDialogueActive = false;
                    TriggerRadioVoice("CONFIRM_CLEAR");
                }
                else if (encIdx == 3)
                {
                    MacroManager.SendChatCommand(TyreManager.GetPressureCommandString());
                    SetCornerMessage(3, $"SENT: {TyreManager.GetPressureLabel()}", 2500);
                    TriggerRadioVoice("CONFIRM_PRESS");
                }
            }
            else if (_steeringWheelMode == "STRAT")
            {
                if (encIdx == 0)
                {
                    var state = TargetStrategyManager.CurrentTarget;
                    if (state.Name.Contains("NO TARGET") || state.Name.Contains("PLAYER"))
                    {
                        // Rimanere in silenzio
                    }
                    else
                    {
                        string rawDiag = state.Diagnosis;
                        string diagKey = "DIAG_ANALYZING";

                        if (rawDiag == "TARGET PUSHING" || rawDiag == "TARGET WET PUSHING") diagKey = "DIAG_PUSHING";
                        else if (rawDiag == "TARGET FUEL SAVING") diagKey = "DIAG_FUELSAVING";
                        else if (rawDiag == "TARGET DEG HIGH") diagKey = "DIAG_TIREWEAR";
                        else if (rawDiag == "TARGET CONSERVING" || rawDiag == "TARGET WET CONSERVING") diagKey = "DIAG_CONSERVING";
                        else if (rawDiag == "TARGET STEADY" || rawDiag == "TARGET WET STEADY") diagKey = "DIAG_STABLE";

                        string cleanedTargetName = CleanNameForSpeech(state.Name);
                        string gapString = state.GapSeconds.ToString("F1", CultureInfo.InvariantCulture);
                        int variant = _rng.Next(0, 4);

                        string textToSpeak = "";
                        if (rawDiag == "TARGET ANALYZING" || rawDiag == "ANALYZING" || diagKey == "DIAG_ANALYZING")
                        {
                            textToSpeak = PitWallLanguage.GetPhrase(Settings.VoiceLanguage, $"REPORT_GAP_{variant}", cleanedTargetName, gapString);
                        }
                        else
                        {
                            string diagLoc = PitWallLanguage.GetPhrase(Settings.VoiceLanguage, diagKey);
                            textToSpeak = PitWallLanguage.GetPhrase(Settings.VoiceLanguage, $"REPORT_FULL_{variant}", cleanedTargetName, gapString, diagLoc);
                        }

                        if (state.UndercutViable)
                        {
                            string undercutText = PitWallLanguage.GetPhrase(Settings.VoiceLanguage, "REPORT_UNDERCUT");
                            if (!string.IsNullOrEmpty(undercutText))
                            {
                                textToSpeak += " " + undercutText;
                            }
                        }

                        TriggerRadioVoice("RAW_SPEECH", textToSpeak);
                    }
                }
                else if (encIdx == 1)
                {
                    TriggerRadioVoice("DROP_ZONE_INFO", (PitRadar.PitTransitTime + PitRadar.DbTireChangeTime).ToString("F0"), "libera");
                }
            }
            UpdateSimHubProperties();
        }

        public void DataUpdate(PluginManager pluginManager, ref GameData data)
        {
            if ((data.GameRunning || data.GameReplay) && data.NewData != null)
            {
                double prevTime = CurrentState.SessionTimeLeftSec;
                double newTime = data.NewData.SessionTimeLeft.TotalSeconds;

                if (prevTime > 0.0 && newTime > 0.0)
                {
                    double timeDiff = newTime - prevTime;
                    if (timeDiff > 2.0 || timeDiff < -30.0)
                    {
                        LogManager.Log(LogModule.RADAR, LogType.EVENT, "Replay Time Jump Detected", $"Session time jumped from {prevTime:F1}s to {newTime:F1}s. Resetting session.");
                        CurrentState.Reset();
                        OpponentTracker.ResetSession();
                        RaceAnalyzer.ResetSession();
                    }
                }

                int prevStatus = _lastSessionStateStatus;
                string prevType = _lastSessionTypeName;

                TelemetryReader.UpdateState(data, CurrentState, _alertThreshold);

                int currentStatus = CurrentState.SessionStateStatus;
                string currentType = data.NewData.SessionTypeName;

                bool isSameEventTransition = (prevStatus >= 4 && currentStatus >= 4);
                if (prevStatus != -1 && (currentStatus != prevStatus || currentType != prevType) && !isSameEventTransition)
                {
                    LogManager.Log(LogModule.RADAR, LogType.EVENT, "Session State/Type Change Detected", 
                        $"Status: {prevStatus} -> {currentStatus} | Type: {prevType} -> {currentType}. Resetting trackers.");
                    CurrentState.Reset();
                    OpponentTracker.ResetSession();
                    RaceAnalyzer.ResetSession();
                    TelemetryReader.UpdateState(data, CurrentState, _alertThreshold);
                }

                _lastSessionStateStatus = CurrentState.SessionStateStatus;
                _lastSessionTypeName = data.NewData.SessionTypeName;

                PitRadar.Update(CurrentState, data.NewData.SessionTimeLeft.TotalSeconds, TyreManager.CurrentScope, FuelManager.Calculations.FuelToAdd, LogManager);

                RaceAnalyzer.Update(CurrentState, PitRadar, OpponentTracker, FuelManager.Calculations, LogManager, TyreManager.CurrentScope, Settings.FuelWeightCoef, Settings.TempCoef);

                double fuelCapacityInTireTime = (PitRadar.MeasuredFuelFillRate > 0) ? (PitRadar.DbTireChangeTime * PitRadar.MeasuredFuelFillRate) : 0.0;
                _fuelCapacityInTireTimeCache = fuelCapacityInTireTime;

                FuelManager.Update(CurrentState, RaceAnalyzer.Results.RaceLapsRemaining, fuelCapacityInTireTime, LogManager);

                UpdateCustomSectors(data);
                UpdateCustomLeaderboards(data);

                // Fuel Target Monitoring (Tolleranza estesa e respiro radio)
                if (CurrentState.CurrentLap != _lastVoiceEvalLap)
                {
                    if (_lastVoiceEvalLap > 0 && CurrentState.CurrentLap > 1)
                    {
                        if (!CurrentState.IsInPitLane)
                        {
                            if (CurrentState.IsRaceSession && 
                                RaceAnalyzer.Results.RaceLapsRemaining > 0 && 
                                RaceAnalyzer.Results.RaceLapsRemaining <= 2.2 && 
                                FuelManager.Calculations.TankLapsRemaining < RaceAnalyzer.Results.RaceLapsRemaining && 
                                !_fuelWarningEndGamePlayed)
                            {
                                TriggerRadioVoice("FUEL_WARNING_LIFT_COAST");
                                _fuelWarningEndGamePlayed = true;
                            }

                            bool canFinishWithoutPitting = false;
                            if (CurrentState.IsRaceSession)
                            {
                                canFinishWithoutPitting = RaceAnalyzer.Results.RaceLapsRemaining <= 0 || 
                                                          FuelManager.Calculations.TankLapsRemaining > RaceAnalyzer.Results.RaceLapsRemaining;
                            }
                            else
                            {
                                canFinishWithoutPitting = true;
                            }

                            if (FuelManager.Calculations.IsTargetModeEnabled && !canFinishWithoutPitting)
                            {
                                double delta = FuelManager.Calculations.LastLapFuelUsed - FuelManager.Calculations.FuelPerLapTarget;
                                if (delta > 0.1)
                                {
                                    TriggerRadioVoice("FUEL_TARGET_ALERT");
                                    _goodFuelLaps = 0;
                                }
                                else if (delta < -0.1)
                                {
                                    _goodFuelLaps++;
                                    if (_goodFuelLaps >= 3)
                                    {
                                        TriggerRadioVoice("FUEL_TARGET_OK");
                                        _goodFuelLaps = 0;
                                    }
                                }
                                else
                                {
                                    _goodFuelLaps = 0;
                                }
                            }
                        }
                    }
                    _lastVoiceEvalLap = CurrentState.CurrentLap;

                    // Aggiorniamo i dati storici del Player nel database JSON (solo se i valori sono consolidati)
                    double pFuel = FuelManager.Calculations.AverageFuelPerLap;
                    if (pFuel > 0.0)
                    {
                        double pStint = CurrentState.MaxFuelCapacity / pFuel;
                        double pPace = RaceAnalyzer.Results.NormalizedRaceStartPace;
                        PitRadar.UpdatePlayerTrackRecord(pStint, pPace, pFuel, CurrentState.MaxFuelCapacity);
                    }
                }

                if (CurrentState.IsInPitLane) _autoUndercutPlayed = false;

                if (CurrentState.IsTrackWet != _lastTrackWet)
                {
                    LogManager.Log(LogModule.WEATHER, LogType.EVENT, "Track Wet Status Changed",
                        $"IsTrackWet: {CurrentState.IsTrackWet} | TrackTemp: {CurrentState.TrackTemperature:F1}C | BaselineTemp: {CurrentState.GlobalBaselineTemp:F1}C");
                    _lastTrackWet = CurrentState.IsTrackWet;
                }

                bool shouldLogWeather = false;
                if (CurrentState.WeatherAlertState != _lastWeatherAlertState)
                {
                    shouldLogWeather = true;
                    _lastWeatherAlertState = CurrentState.WeatherAlertState;
                }

                double rawTime = data.NewData.SessionTimeLeft.TotalSeconds;
                if (Math.Abs(rawTime - _lastWeatherLogTime) >= 30.0)
                {
                    shouldLogWeather = true;
                    _lastWeatherLogTime = rawTime;
                }

                if (shouldLogWeather)
                {
                    LogManager.Log(LogModule.WEATHER, LogType.EVENT, "Weather Telemetry Update",
                        $"Alert: {CurrentState.WeatherAlertState} | TrackWetnessLevel: {CurrentState.TrackWetnessLevel} | " +
                        $"Humidity: {CurrentState.RelativeHumidity:P1} | Pressure: {CurrentState.AirPressure:F2} hPa | " +
                        $"AirTemp: {CurrentState.AirTemperature:F1}C | TrackTemp: {CurrentState.TrackTemperature:F1}C | " +
                        $"WindSpeed: {CurrentState.WindSpeed:F1} m/s | " +
                        $"TimeToImpactMinutes: {CurrentState.TimeToImpactMinutes:F1} min | TimeToImpactLaps: {CurrentState.TimeToImpactLaps:F1} laps | " +
                        $"Rain10m: {CurrentState.RainIntensity10Min:F2} | Rain30m: {CurrentState.RainIntensity30Min:F2}");
                }

                OpponentTracker.Update(data, Settings, TyreManager.CurrentScope, CurrentState, PitRadar, data.NewData.SessionTimeLeft.TotalSeconds, CurrentState.RaceStartingFuel, CurrentState.MaxFuelCapacity, FuelManager.Calculations.AverageFuelPerLap, RaceAnalyzer.PlayerExtendedPitZone.BestRawTime, RaceAnalyzer.Results.RaceLapsRemaining, LogManager, Settings.FuelWeightCoef, Settings.TempCoef);
                CurrentState.CrossoverAlertState = OpponentTracker.CrossoverAlertState;
                CurrentState.CrossoverDeltaSeconds = OpponentTracker.CrossoverDeltaSeconds;

                TargetStrategyManager.Update(CurrentState, OpponentTracker, PitRadar, FuelManager.Calculations, RaceAnalyzer, TyreManager, GetTargetModeName(_targetSelectionIndex), LogManager, Settings.FuelWeightCoef);



                CheckAutoPitTriggers();
                CheckVoiceEngineerTriggers();
                CheckCalibrationTriggers();

                if (!IsGlobalTesting && IsLedsConnected)
                {
                    if (!_isLedGameModeActive)
                    {
                        HardwareManager?.SendRawLedCommand("GAME:1");
                        _isLedGameModeActive = true;
                        SendLedConfig();
                        Task.Run(() => { System.Threading.Thread.Sleep(50); SyncAllColorsToHardware(); });
                    }

                    int currentRpm = 0;
                    if (data.NewData != null && data.NewData.Rpms > 0 && data.NewData.CarSettings_MaxRPM > 0)
                    {
                        currentRpm = (int)Math.Round((data.NewData.Rpms / (double)data.NewData.CarSettings_MaxRPM) * 100.0);
                        if (currentRpm > 100) currentRpm = 100;
                        if (currentRpm < 0) currentRpm = 0;
                    }
                    if (currentRpm != _lastLedRpm)
                    {
                        HardwareManager?.SendRawLedCommand($"RPM:{currentRpm}");
                        _lastLedRpm = currentRpm;
                    }

                    if (data.NewData != null)
                    {
                        int yellow = (data.NewData.Flag_Yellow > 0) ? 1 : 0;
                        int blue = (data.NewData.Flag_Blue > 0) ? 1 : 0;
                        int green = (data.NewData.Flag_Green > 0) ? 1 : 0;
                        int abs = (data.NewData.ABSActive > 0) ? 1 : 0;
                        int tc = (data.NewData.TCActive > 0) ? 1 : 0;
                        int pit = (data.NewData.PitLimiterOn > 0 || (CurrentState != null && CurrentState.IsInPitLane)) ? 1 : 0;

                        void SendEventIfChanged(int evId, int val)
                        {
                            if (evId >= 0 && evId < _lastLedEvents.Length && _lastLedEvents[evId] != val)
                            {
                                HardwareManager?.SendRawLedCommand($"EVT:{evId}:{val}");
                                _lastLedEvents[evId] = val;
                            }
                        }

                        SendEventIfChanged(0, yellow);
                        SendEventIfChanged(2, blue);
                        SendEventIfChanged(3, green);
                        SendEventIfChanged(4, abs);
                        SendEventIfChanged(5, tc);
                        SendEventIfChanged(6, pit);
                    }
                }

                UpdateSimHubProperties();
            }
            else
            {
                if (!IsGlobalTesting)
                {
                    if (_isLedGameModeActive)
                    {
                        HardwareManager?.SendRawLedCommand("GAME:0");
                        HardwareManager?.SendRawLedCommand("RPM:0");
                        _isLedGameModeActive = false;
                        _lastLedRpm = -1;
                        for (int i = 0; i < _lastLedEvents.Length; i++) _lastLedEvents[i] = -1;
                    }
                }

                if (CurrentState != null && CurrentState.IsGameRunning)
                {
                    CurrentState.Reset();
                    FuelManager.ResetSession();
                    RaceAnalyzer.ResetSession();
                    PitRadar.ResetSession();
                    OpponentTracker.ResetSession();
                    TargetStrategyManager.ResetSession();
                    _calibrationRunner.Reset();
                    _autoUndercutPlayed = false;
                    _goodFuelLaps = 0;
                    _lastVoiceEvalLap = -1;
                    _fuelCapacityInTireTimeCache = 0.0;
                }
                _lastSessionStateStatus = -1;
                _lastSessionTypeName = null;
            }
        }

        /// <summary>
        /// La cascata di calibrazione guidata (Y-28).
        ///
        /// Sostituisce la vecchia catena di `if` che annunciava una calibrazione alla volta, solo
        /// da fermi in piazzola e una volta sola per sosta. Quella non guidava: diceva cosa mancava
        /// e taceva. Adesso c'è una regia che sa quale passo si aspetta, riconosce quando è stato
        /// eseguito, e prosegue — anche se il pilota fa le cose in un ordine diverso.
        ///
        /// Le due decisioni sono separate apposta e vivono in moduli puri, testabili senza SimHub:
        /// <see cref="CalibrationCascade"/> decide **cosa** chiedere,
        /// <see cref="CalibrationCascadeRunner"/> decide **quando** dirlo.
        /// </summary>
        private void CheckCalibrationTriggers()
        {
            if (!Settings.EnableVoiceEngineer)
            {
                _calibrationRunner.Reset();
                return;
            }

            bool isCalibrationSession = CalibrationCascade.IsCalibrationSession(
                CurrentState.IsRaceSession, CurrentState.IsQualySession);

            bool shouldAnnounce = _calibrationRunner.Update(
                isCalibrationSession,
                PitRadar.GetCalibrationNeeds(),
                PitRadar.HasGenuineTrackSample,
                CurrentState.CurrentLap);

            CalibrationStep step = _calibrationRunner.CurrentStep;

            // La dash mostra sempre il passo corrente, anche fra un annuncio e l'altro: il pilota
            // deve poter guardare cosa gli è stato chiesto senza riascoltare la radio.
            _calibrationStepText = _calibrationRunner.JustCompleted
                ? "CALIBRATION COMPLETE"
                : CalibrationStepLabel(step);

            if (!shouldAnnounce) return;

            string voiceKey = _calibrationRunner.JustCompleted
                ? "CALIB_COMPLETE"
                : CalibrationCascade.VoiceKeyFor(step, _calibrationRunner.RepeatIndex);

            if (string.IsNullOrEmpty(voiceKey)) return;

            // Al primo annuncio della sessione la frase del passo viene incastonata nell'apertura,
            // in un'unica battuta radio. Serve perché la cascata salta i passi già noti: senza
            // apertura, arrivando su un circuito nuovo con la classe già calibrata, l'ingegnere
            // attaccherebbe da metà elenco senza contesto.
            if (_calibrationRunner.IsFirstOfSession)
            {
                string stepText = PitWallLanguage.GetPhrase(Settings.VoiceLanguage, voiceKey);
                TriggerRadioVoice("CALIB_INTRO", stepText);
            }
            else
            {
                TriggerRadioVoice(voiceKey);
            }

            LogManager.Log(LogModule.RADAR, LogType.EVENT, "Calibration Step Announced",
                $"step={step} | key={voiceKey} | repeat={_calibrationRunner.RepeatIndex} | " +
                $"intro={_calibrationRunner.IsFirstOfSession} | lap={CurrentState.CurrentLap}");
        }

        /// <summary>Etichetta breve del passo per la dash. Vuota se non c'è nulla in corso.</summary>
        private static string CalibrationStepLabel(CalibrationStep step)
        {
            switch (step)
            {
                case CalibrationStep.NeedGenuineLap: return "CALIBRATION: COMPLETE A LAP";
                case CalibrationStep.DriveThrough: return "CALIBRATION: DRIVE-THROUGH";
                case CalibrationStep.FuelOnlyStop: return "CALIBRATION: FUEL ONLY STOP";
                case CalibrationStep.TyreStopAll4: return "CALIBRATION: 4 TYRES, NO FUEL";
                case CalibrationStep.TyreStopHalf: return "CALIBRATION: 2 TYRES, NO FUEL";
                case CalibrationStep.TyreStopSingle: return "CALIBRATION: 1 TYRE, NO FUEL";
                default: return "";
            }
        }

        private void CheckAutoPitTriggers()
        {
            if (Settings.EnableAutoPitStrategy && Settings.EnableFuelCalculatorSystem)
            {
                if (CurrentState.IsInPitLane && !_autoPitMacroSent && FuelManager.Calculations.IsPredictionValid)
                {
                    _autoPitMacroSent = true;
                    Task.Delay(2000).ContinueWith(t => {
                        double val = FuelManager.Calculations.FuelToAdd;
                        string cmd = string.Format(CultureInfo.InvariantCulture, "#fuel {0}l", val);

                        if (TyreManager.CurrentScope != TyreSelectionScope.None)
                        {
                            cmd += " " + TyreManager.GetTyreCommandString();
                            cmd += TyreManager.SelectedWetCompound ? " #tc 2" : " #tc 1";

                            if (TyreManager.UserPressureOffset != 0.0)
                            {
                                cmd += " " + TyreManager.GetPressureCommandString();
                            }
                        }
                        else
                        {
                            cmd += " #clear tires";
                        }

                        if (_windshieldActive) cmd += " #ws";

                        MacroManager.SendChatCommand(cmd);
                        SetCornerMessage(0, "AUTO PIT SENT", 3000);
                        if (LogManager != null) LogManager.Log(LogModule.STRATEGY, LogType.EVENT, "AutoPit Triggered (Unified)", cmd);
                    });
                }
                if (!CurrentState.IsInPitLane) _autoPitMacroSent = false;
            }
        }

        private void CheckVoiceEngineerTriggers()
        {
            if (CurrentState.IsInPitLane)
            {
                _boxNowPlayed = false;
                _pitWindowAlertPlayed = false;
                _autoUndercutPlayed = false;
                _interactivePitDialogueActive = false;
                _fuelWarningEndGamePlayed = false;
                _wetOpponentsPitAlertPlayed = false;
                _crossoverWetsPlayed = false;
                _crossoverSlicksPlayed = false;
            }
            else
            {
                _sanityCheckPitPlayed = false;
                _pitRecapPlayed = false;
            }

            if (Settings.EnableVoiceEngineer)
            {
                // Formation Lap triggers
                if (CurrentState.SessionStateStatus == 3 && CurrentState.IsRaceSession)
                {
                    if (CurrentState.TrackPositionPercent > 0.10 && CurrentState.TrackPositionPercent < 0.70)
                    {
                        _formationMidwayCleared = true;
                    }

                    if (!_formationStartPlayed)
                    {
                        string speakName = Settings.CustomPlayerName;
                        if (string.IsNullOrWhiteSpace(speakName))
                        {
                            string rawPlayerName = PluginManager.GetPropertyValue("PlayerName")?.ToString() ?? "";
                            speakName = CleanNameForSpeech(rawPlayerName);
                        }
                        else
                        {
                            speakName = CleanNameForSpeech(speakName);
                        }

                        TriggerRadioVoice("FORMATION_LAP_START", speakName);
                        _formationStartPlayed = true;
                    }

                    if (!_formationGridPlayed && _formationMidwayCleared && CurrentState.TrackPositionPercent > 0.90)
                    {
                        TriggerRadioVoice("FORMATION_LAP_GRID");
                        _formationGridPlayed = true;
                    }
                }
                else
                {
                    _formationStartPlayed = false;
                    _formationGridPlayed = false;
                    _formationMidwayCleared = false;
                }
                if (CurrentState.IsInPitLane && !_sanityCheckPitPlayed)
                {
                    _sanityCheckPitPlayed = true;
                    if (FuelManager.Calculations.FuelToAdd <= 0 && TyreManager.CurrentScope == TyreSelectionScope.None)
                    {
                        TriggerRadioVoice("SANITY_CHECK_PIT_NOT_SET");
                    }
                    else
                    {
                        if (!_pitRecapPlayed && FuelManager.Calculations.IsPredictionValid)
                        {
                            double val = FuelManager.Calculations.FuelToAdd;
                            if (TyreManager.CurrentScope != TyreSelectionScope.None)
                                TriggerRadioVoice(GetConfirmFuelTyreKey(TyreManager.CurrentScope), val.ToString("F1", CultureInfo.InvariantCulture));
                            else
                                TriggerRadioVoice("CONFIRM_FUEL", val.ToString("F1", CultureInfo.InvariantCulture));
                            _pitRecapPlayed = true;
                        }
                    }
                }

                if (!CurrentState.IsInPitLane)
                {
                    bool canFinishWithoutPitting = false;
                    if (CurrentState.IsRaceSession)
                    {
                        canFinishWithoutPitting = RaceAnalyzer.Results.RaceLapsRemaining <= 0 || 
                                                  FuelManager.Calculations.TankLapsRemaining > RaceAnalyzer.Results.RaceLapsRemaining;
                    }
                    else
                    {
                        canFinishWithoutPitting = true;
                    }

                    if (!canFinishWithoutPitting)
                    {
                        if (!_boxNowPlayed && FuelManager.Calculations.TankLapsRemaining > 0 && FuelManager.Calculations.TankLapsRemaining <= 1.5)
                        {
                            TriggerRadioVoice("BOX_NOW");
                            _boxNowPlayed = true;
                            _interactivePitDialogueActive = false;
                        }

                        if (!_pitWindowAlertPlayed && FuelManager.Calculations.TankLapsRemaining > 1.5 && FuelManager.Calculations.TankLapsRemaining <= 2.5)
                        {
                            if (TyreManager.CurrentScope == TyreSelectionScope.None)
                                TriggerRadioVoice("PIT_WINDOW_ASK_TYRES");
                            else
                                TriggerRadioVoice("PIT_WINDOW_ALERT");

                            _pitWindowAlertPlayed = true;
                            _interactivePitDialogueActive = true;
                        }
                    }

                    if (TargetStrategyManager.CurrentTarget.UndercutViable && FuelManager.Calculations.TankLapsRemaining > 0 && FuelManager.Calculations.TankLapsRemaining < 3.0 && !_autoUndercutPlayed)
                    {
                        TriggerRadioVoice("AUTO_UNDERCUT_ALERT", CleanNameForSpeech(TargetStrategyManager.CurrentTarget.Name));
                        _autoUndercutPlayed = true;
                    }

                    if (OpponentTracker.OpponentPittedInWet && !_wetOpponentsPitAlertPlayed)
                    {
                        TriggerRadioVoice("WET_OPPONENTS_ENTERING_PITS");
                        _wetOpponentsPitAlertPlayed = true;
                        OpponentTracker.OpponentPittedInWet = false;
                    }

                    if (CurrentState.TrackWetnessLevel == 0 && CurrentState.WeatherAlertState != "ALERT_PIOGGIA")
                    {
                        _rainAlertPlayed = false;
                    }

                    if (CurrentState.WeatherAlertState == "ALERT_PIOGGIA" && !_rainAlertPlayed)
                    {
                        string humidStr = Math.Round(CurrentState.RelativeHumidity * 100.0, 0).ToString(CultureInfo.InvariantCulture);
                        string windStr = Math.Round(CurrentState.WindSpeed, 1).ToString("F1", CultureInfo.InvariantCulture);
                        string lapsStr = Math.Round(CurrentState.TimeToImpactLaps, 1).ToString("F1", CultureInfo.InvariantCulture);
                        TriggerRadioVoice("RAIN_ALERT", humidStr, windStr, lapsStr);
                        _rainAlertPlayed = true;
                    }

                    if (CurrentState.CrossoverAlertState == "BOX_WETS" && !_crossoverWetsPlayed)
                    {
                        string secondsStr = Math.Round(CurrentState.CrossoverDeltaSeconds, 1).ToString("F1", CultureInfo.InvariantCulture);
                        TriggerRadioVoice("CROSSOVER_BOX_WETS", secondsStr);
                        _crossoverWetsPlayed = true;
                    }

                    if (CurrentState.CrossoverAlertState == "BOX_SLICKS" && !_crossoverSlicksPlayed)
                    {
                        string secondsStr = Math.Round(CurrentState.CrossoverDeltaSeconds, 1).ToString("F1", CultureInfo.InvariantCulture);
                        TriggerRadioVoice("CROSSOVER_BOX_SLICKS", secondsStr);
                        _crossoverSlicksPlayed = true;
                    }

                    // Overcut / Target Pit Entry Check (Fase 4)
                    var tgt = TargetStrategyManager.CurrentTarget;
                    if (tgt != null && !string.IsNullOrEmpty(tgt.Name) && tgt.Name != "PLAYER" && !tgt.Name.Contains("NO TARGET"))
                    {
                        if (OpponentTracker.TrackedOpponents.ContainsKey(tgt.Name))
                        {
                            bool isInside = OpponentTracker.TrackedOpponents[tgt.Name].IsInsideGeofence;
                            if (isInside && !_lastTargetIsInsideGeofence)
                            {
                                string cleanedName = CleanNameForSpeech(tgt.Name);
                                if (tgt.OvercutViable)
                                {
                                    TriggerRadioVoice("TARGET_ENTERING_PITS_OVERCUT", cleanedName);
                                }
                                else
                                {
                                    TriggerRadioVoice("TARGET_ENTERING_PITS", cleanedName);
                                }
                            }
                            _lastTargetIsInsideGeofence = isInside;
                        }
                        else
                        {
                            _lastTargetIsInsideGeofence = false;
                        }
                    }
                    else
                    {
                        _lastTargetIsInsideGeofence = false;
                    }
                }
            }
        }

        public void UpdateSimHubProperties()
        {
            Type t = this.GetType();

            PluginManager.SetPropertyValue("SimRIG.Input.TopLeftEncoder", t, GetEncoderActionLabel(0));
            PluginManager.SetPropertyValue("SimRIG.Input.TopRightEncoder", t, GetEncoderActionLabel(1));
            PluginManager.SetPropertyValue("SimRIG.Input.BottomLeftEncoder", t, GetEncoderActionLabel(2));
            PluginManager.SetPropertyValue("SimRIG.Input.BottomRightEncoder", t, GetEncoderActionLabel(3));
            PluginManager.SetPropertyValue("SimRIG.BottomLeftWidgetPage", t, _bottomLeftWidgetPage);
            PluginManager.SetPropertyValue("SimRIG.BottomRightWidgetPage", t, _bottomRightWidgetPage);

            PluginManager.SetPropertyValue("SimRIG.Fuel.UserOffset", t, FuelManager.Calculations.UserFuelOffset);
            PluginManager.SetPropertyValue("SimRIG.Fuel.Step", t, FuelManager.Calculations.FuelStep);
            PluginManager.SetPropertyValue("SimRIG.Fuel.ActionMessage", t, _msgTL);
            PluginManager.SetPropertyValue("SimRIG.Fuel.FuelToAdd", t, FuelManager.Calculations.FuelToAdd);
            PluginManager.SetPropertyValue("SimRIG.Fuel.PitRequiredNumber", t, FuelManager.Calculations.PitRequiredNumber);

            bool fuelSaveAchievable = FuelManager.Calculations.IsFuelSavingAchievable;
            PluginManager.SetPropertyValue("SimRIG.Fuel.FuelSaveTarget", t, Math.Round(FuelManager.Calculations.FuelSaveTarget, 2));
            PluginManager.SetPropertyValue("SimRIG.Fuel.FuelSavingRequiredPct", t, Math.Round(FuelManager.Calculations.FuelSavingRequired * 100.0, 1));
            PluginManager.SetPropertyValue("SimRIG.Fuel.FuelSavingAchievable", t, fuelSaveAchievable);
            // Segnaposto quando il risparmio non è ottenibile: un numero mostrato lì verrebbe
            // letto come un obiettivo da inseguire, mentre la risposta corretta è "fermati".
            PluginManager.SetPropertyValue("SimRIG.Fuel.FuelSaveTargetStr", t,
                fuelSaveAchievable
                    ? FuelManager.Calculations.FuelSaveTarget.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                    : "--.-");
            PluginManager.SetPropertyValue("SimRIG.Fuel.FuelDelta", t, Math.Round(FuelManager.Calculations.FuelDelta, 2));
            PluginManager.SetPropertyValue("SimRIG.Fuel.TankLapsRemaining", t, Math.Round(FuelManager.Calculations.TankLapsRemaining, 2));
            double playerPitWindowTargetLap = CurrentState.CurrentLap + FuelManager.Calculations.TankLapsRemaining;
            PluginManager.SetPropertyValue("SimRIG.Fuel.EstimatedPitWindow", t, Math.Round(FuelManager.Calculations.TankLapsRemaining, 1));
            PluginManager.SetPropertyValue("SimRIG.Fuel.EstimatedPitWindowTargetLap", t, Math.Round(playerPitWindowTargetLap, 1));
            PluginManager.SetPropertyValue("SimRIG.Fuel.CurrentTankLevel", t, Math.Round(CurrentState.CurrentFuelLevel, 2));
            PluginManager.SetPropertyValue("SimRIG.Fuel.TargetFuel", t, FuelManager.Calculations.FuelPerLapTarget);
            PluginManager.SetPropertyValue("SimRIG.Fuel.TargetEnabled", t, FuelManager.Calculations.IsTargetModeEnabled);
            PluginManager.SetPropertyValue("SimRIG.Fuel.SyncEnabled", t, FuelManager.Calculations.IsSyncModeEnabled);
            PluginManager.SetPropertyValue("SimRIG.Fuel.RaceStartingFuel", t, Math.Round(CurrentState.RaceStartingFuel, 2));
            PluginManager.SetPropertyValue("SimRIG.Fuel.HistoricalPerLap", t, Math.Round(FuelManager.Calculations.AverageFuelPerLap, 2));
            PluginManager.SetPropertyValue("SimRIG.Fuel.LastLapFuelUsed", t, Math.Round(FuelManager.Calculations.LastLapFuelUsed, 2));

            PluginManager.SetPropertyValue("SimRIG.Tyres.SelectionScope", t, GetTyreScopeLabel());
            PluginManager.SetPropertyValue("SimRIG.Tyres.SelectedTireTime", t, TyreManager.GetSelectedTireTime(CurrentState.CarClassId));
            PluginManager.SetPropertyValue("SimRIG.Tyres.SelectedTireTimeStr", t, $"{TyreManager.GetSelectedTireTime(CurrentState.CarClassId):F1}s");
            PluginManager.SetPropertyValue("SimRIG.Pit.SelectedTireTime", t, TyreManager.GetSelectedTireTime(CurrentState.CarClassId));
            PluginManager.SetPropertyValue("SimRIG.Tyres.ActionMessage", t, _msgTR);
            PluginManager.SetPropertyValue("SimRIG.Pressure.UserOffset", t, TyreManager.UserPressureOffset);
            PluginManager.SetPropertyValue("SimRIG.Pressure.UserOffsetStr", t, TyreManager.GetPressureLabel());
            PluginManager.SetPropertyValue("SimRIG.Pressure.OffsetKpa", t, TyreManager.PressureOffsetKpa);
            PluginManager.SetPropertyValue("SimRIG.Pressure.ActionMessage", t, _msgBR);
            PluginManager.SetPropertyValue("SimRIG.Services.WindshieldActive", t, _windshieldActive);
            PluginManager.SetPropertyValue("SimRIG.Services.FastRepairActive", t, _fastRepairActive);
            PluginManager.SetPropertyValue("SimRIG.Services.ActionMessage", t, _msgBL);

            PluginManager.SetPropertyValue("SimRIG.Strategy.Mode", t, GetPitStratLabel());
            PluginManager.SetPropertyValue("SimRIG.Strategy.FuelCalculatorEnabled", t, Settings.EnableFuelCalculatorSystem);
            PluginManager.SetPropertyValue("SimRIG.Strategy.AutoPitEnabled", t, Settings.EnableAutoPitStrategy && Settings.EnableFuelCalculatorSystem);
            PluginManager.SetPropertyValue("SimRIG.Strategy.IsPredictionValid", t, FuelManager.Calculations.IsPredictionValid);
            PluginManager.SetPropertyValue("SimRIG.Strategy.LeaderPace", t, Math.Round(RaceAnalyzer.Results.LeaderEstimatedPace, 3));
            PluginManager.SetPropertyValue("SimRIG.Strategy.LeaderPaceStr", t, FormatTime(RaceAnalyzer.Results.LeaderEstimatedPace));

            PluginManager.SetPropertyValue("SimRIG.Strategy.RemainingPitsPlayer", t, Math.Round(RaceAnalyzer.Results.RemainingPitsPlayer, 2));
            PluginManager.SetPropertyValue("SimRIG.Strategy.RemainingPitsLeader", t, Math.Round(RaceAnalyzer.Results.RemainingPitsLeader, 2));
            PluginManager.SetPropertyValue("SimRIG.Strategy.LeaderStintLaps", t, Math.Round(RaceAnalyzer.Results.LeaderStintLaps, 1));
            PluginManager.SetPropertyValue("SimRIG.Strategy.LeaderAveragePace", t, Math.Round(RaceAnalyzer.Results.LeaderAveragePace, 3));
            PluginManager.SetPropertyValue("SimRIG.Strategy.LeaderDataSource", t, RaceAnalyzer.Results.LeaderDataSource);
            PluginManager.SetPropertyValue("SimRIG.Strategy.LeaderPitLossTime", t, Math.Round(RaceAnalyzer.Results.LeaderPitLossTime, 1));

            PluginManager.SetPropertyValue("SimRIG.Session.LeaderRaceTotalLaps", t, RaceAnalyzer.Results.LeaderRaceTotalLaps);
            PluginManager.SetPropertyValue("SimRIG.Session.LeaderRaceLapsCompleted", t, RaceAnalyzer.Results.LeaderRaceLapsCompleted);
            PluginManager.SetPropertyValue("SimRIG.Session.LeaderRaceLapsRemaining", t, RaceAnalyzer.Results.LeaderRaceLapsRemaining);
            PluginManager.SetPropertyValue("SimRIG.Session.RaceTotalLaps", t, RaceAnalyzer.Results.RaceTotalLaps);
            PluginManager.SetPropertyValue("SimRIG.Session.ProjectedPosAtCheckered", t, Math.Round(RaceAnalyzer.Results.ProjectedPosAtCheckered, 2));
            PluginManager.SetPropertyValue("SimRIG.Session.LeaderProjectedPosAtCheckered", t, Math.Round(RaceAnalyzer.Results.LeaderProjectedPosAtCheckered, 2));
            PluginManager.SetPropertyValue("SimRIG.Session.FlagLeaderName", t, RaceAnalyzer.Results.FlagLeaderName);
            PluginManager.SetPropertyValue("SimRIG.Session.FlagLeaderProjectedPos", t, Math.Round(RaceAnalyzer.Results.FlagLeaderProjectedPos, 2));
            PluginManager.SetPropertyValue("SimRIG.Session.LeaderTrackPct", t, Math.Round(RaceAnalyzer.Results.LeaderTrackPct, 4));
            PluginManager.SetPropertyValue("SimRIG.Session.LeaderPosAtExpiry", t, Math.Round(RaceAnalyzer.Results.LeaderPosAtExpiry, 3));
            PluginManager.SetPropertyValue("SimRIG.Session.LeaderTrackPctAtExpiry", t, Math.Round(RaceAnalyzer.Results.LeaderTrackPctAtExpiry, 4));
            PluginManager.SetPropertyValue("SimRIG.Session.LeaderNameAtExpiry", t, RaceAnalyzer.Results.LeaderNameAtExpiry);
            PluginManager.SetPropertyValue("SimRIG.Session.RaceLapsCompleted", t, RaceAnalyzer.Results.RaceLapsCompleted);
            PluginManager.SetPropertyValue("SimRIG.Session.RaceLapsRemaining", t, RaceAnalyzer.Results.RaceLapsRemaining);
            PluginManager.SetPropertyValue("SimRIG.Session.IsLapped", t, RaceAnalyzer.Results.IsLapped);
            PluginManager.SetPropertyValue("SimRIG.Session.TimeLeftStr", t, FormatTime(CurrentState.SessionTimeLeftSec));
            PluginManager.SetPropertyValue("SimRIG.Session.RaceLifeTimeLeftStr", t, FormatTime(RaceAnalyzer.Results.RaceLifeTimeLeftSec));
            PluginManager.SetPropertyValue("SimRIG.Session.ClassTopSpeed", t, Math.Round(OpponentTracker.ClassTopSpeed, 1));
            PluginManager.SetPropertyValue("SimRIG.Session.ClassPaceDropDueToTyres", t, Math.Round(OpponentTracker.ClassAveragePaceDrop, 2));
            PluginManager.SetPropertyValue("SimRIG.Session.ClassSectorPaceDropDueToTyres", t, Math.Round(OpponentTracker.ClassAverageSectorPaceDrop, 2));
            PluginManager.SetPropertyValue("SimRIG.Session.ClassSectorPaceDropDueToTyresRaw", t, Math.Round(OpponentTracker.ClassAverageSectorPaceDropRaw, 2));
            PluginManager.SetPropertyValue("SimRIG.Session.PlayerMicrosector", t, (int)(CurrentState.TrackPositionPercent * 100));
            PluginManager.SetPropertyValue("SimRIG.Session.ClassBestExtendedPitZoneTime", t, Math.Round(OpponentTracker.ClassBestExtendedPitZoneTime, 3));

            double learnedPitLimit = PitRadar.GetPitLaneSpeedLimit(CurrentState.CarClassId);
            PluginManager.SetPropertyValue("SimRIG.Session.PitLaneSpeedLimit", t, Math.Round(learnedPitLimit, 0));
            PluginManager.SetPropertyValue("SimRIG.Session.PitLaneSpeedLimitKnown", t, learnedPitLimit > 0.0);

            PluginManager.SetPropertyValue("SimRIG.Driver.NormalizedRaceStartPace", t, Math.Round(RaceAnalyzer.Results.NormalizedRaceStartPace, 3));
            PluginManager.SetPropertyValue("SimRIG.Driver.NormalizedRaceStartPaceStr", t, FormatTime(RaceAnalyzer.Results.NormalizedRaceStartPace));
            PluginManager.SetPropertyValue("SimRIG.Driver.EstimatedCurrentPace", t, Math.Round(RaceAnalyzer.Results.EstimatedCurrentPace, 3));
            PluginManager.SetPropertyValue("SimRIG.Driver.EstimatedCurrentPaceStr", t, FormatTime(RaceAnalyzer.Results.EstimatedCurrentPace));
            PluginManager.SetPropertyValue("SimRIG.Driver.PaceDropDueToTyres", t, Math.Round(RaceAnalyzer.Results.PaceDropDueToTyres, 2));
            PluginManager.SetPropertyValue("SimRIG.Driver.RelativeDegradationToClass", t, Math.Round(RaceAnalyzer.Results.PaceDropDueToTyres - OpponentTracker.ClassAveragePaceDrop, 2));
            PluginManager.SetPropertyValue("SimRIG.Driver.SectorPaceDropDueToTyres", t, Math.Round(RaceAnalyzer.Results.SectorPaceDropDueToTyres, 2));
            PluginManager.SetPropertyValue("SimRIG.Driver.SectorPaceDropDueToTyresRaw", t, Math.Round(RaceAnalyzer.Results.SectorPaceDropDueToTyresRaw, 2));
            PluginManager.SetPropertyValue("SimRIG.Driver.RelativeSectorDegradationToClass", t, Math.Round(RaceAnalyzer.Results.SectorPaceDropDueToTyres - OpponentTracker.ClassAverageSectorPaceDrop, 2));

            PluginManager.SetPropertyValue("SimRIG.Driver.LapBaselineNormalized", t, Math.Round(RaceAnalyzer.NormalizedTimes.LapBaseline, 3));
            PluginManager.SetPropertyValue("SimRIG.Driver.LapBaselineRaw", t, Math.Round(RaceAnalyzer.RawTimes.LapBaseline, 3));
            PluginManager.SetPropertyValue("SimRIG.Driver.LapMovingAverageNormalized", t, Math.Round(RaceAnalyzer.NormalizedTimes.LapMovingAverage, 3));
            PluginManager.SetPropertyValue("SimRIG.Driver.LapMovingAverageRaw", t, Math.Round(RaceAnalyzer.RawTimes.LapMovingAverage, 3));

            PluginManager.SetPropertyValue("SimRIG.Driver.SectorBaselineNormalized", t, Math.Round(RaceAnalyzer.NormalizedTimes.SectorBaseline, 3));
            PluginManager.SetPropertyValue("SimRIG.Driver.SectorBaselineRaw", t, Math.Round(RaceAnalyzer.RawTimes.SectorBaseline, 3));
            PluginManager.SetPropertyValue("SimRIG.Driver.SectorMovingAverageNormalized", t, Math.Round(RaceAnalyzer.NormalizedTimes.SectorMovingAverage, 3));
            PluginManager.SetPropertyValue("SimRIG.Driver.SectorMovingAverageRaw", t, Math.Round(RaceAnalyzer.RawTimes.SectorMovingAverage, 3));
            PluginManager.SetPropertyValue("SimRIG.Driver.LastLapTime", t, Math.Round(CurrentState.LastLapTimeSec, 3));
            PluginManager.SetPropertyValue("SimRIG.Driver.LastLapTimeStr", t, FormatTime(CurrentState.LastLapTimeSec));

            PluginManager.SetPropertyValue("SimRIG.Driver.PrePitNormalizedAverage", t, Math.Round(RaceAnalyzer.PrePitNormalizedAverage, 3));
            double driverFuelTime = PitRadar.MeasuredFuelFillRate > 0 ? (FuelManager.Calculations.FuelToAdd / PitRadar.MeasuredFuelFillRate) : 0.0;
            PluginManager.SetPropertyValue("SimRIG.Driver.FuelToAddTime", t, Math.Round(driverFuelTime, 1));
            PluginManager.SetPropertyValue("SimRIG.Driver.PostPitDelta0", t, Math.Round(RaceAnalyzer.PostPitNormalizedDeltas[0], 3));
            PluginManager.SetPropertyValue("SimRIG.Driver.PostPitDelta1", t, Math.Round(RaceAnalyzer.PostPitNormalizedDeltas[1], 3));
            PluginManager.SetPropertyValue("SimRIG.Driver.PostPitDelta2", t, Math.Round(RaceAnalyzer.PostPitNormalizedDeltas[2], 3));
            PluginManager.SetPropertyValue("SimRIG.Driver.PostPitPenalty0", t, Math.Round(RaceAnalyzer.PostPitWarmupPenalties[0], 3));
            PluginManager.SetPropertyValue("SimRIG.Driver.PostPitPenalty1", t, Math.Round(RaceAnalyzer.PostPitWarmupPenalties[1], 3));
            PluginManager.SetPropertyValue("SimRIG.Driver.PostPitPenalty2", t, Math.Round(RaceAnalyzer.PostPitWarmupPenalties[2], 3));

            var tgt = TargetStrategyManager.CurrentTarget;
            PluginManager.SetPropertyValue("SimRIG.Target.Mode", t, tgt.ModeLabel);
            PluginManager.SetPropertyValue("SimRIG.Target.Name", t, tgt.Name);
            PluginManager.SetPropertyValue("SimRIG.Target.Position", t, tgt.ClassPosition);
            PluginManager.SetPropertyValue("SimRIG.Target.GapSeconds", t, Math.Round(tgt.GapSeconds, 1));
            PluginManager.SetPropertyValue("SimRIG.Target.GapString", t, tgt.GapString);
            PluginManager.SetPropertyValue("SimRIG.Target.RelativePace", t, Math.Round(tgt.RelativePace, 3));
            PluginManager.SetPropertyValue("SimRIG.Target.RelativePaceStr", t, $"{(tgt.RelativePace > 0 ? "+" : "")}{tgt.RelativePace:F2}s/lap");
            PluginManager.SetPropertyValue("SimRIG.Target.RelativeGapDelta", t, Math.Round(tgt.RelativeGapDelta, 3));
            PluginManager.SetPropertyValue("SimRIG.Target.RelativeGapDeltaValid", t, tgt.RelativeGapDeltaValid);
            PluginManager.SetPropertyValue("SimRIG.Target.RelativeGapDeltaStr", t, TargetStrategyManager.FormatGapDelta(tgt.RelativeGapDelta, tgt.RelativeGapDeltaValid));
            PluginManager.SetPropertyValue("SimRIG.Target.TopSpeed", t, Math.Round(tgt.TargetTopSpeed, 1));
            PluginManager.SetPropertyValue("SimRIG.Target.CurrentSpeed", t, Math.Round(tgt.TargetCurrentSpeed, 1));
            PluginManager.SetPropertyValue("SimRIG.Target.Diagnosis", t, tgt.Diagnosis);
            PluginManager.SetPropertyValue("SimRIG.Target.NormalizedRaceStartPace", t, Math.Round(tgt.NormalizedRaceStartPace, 3));
            PluginManager.SetPropertyValue("SimRIG.Target.NormalizedRaceStartPaceStr", t, FormatTime(tgt.NormalizedRaceStartPace));
            PluginManager.SetPropertyValue("SimRIG.Target.PaceDeficit", t, Math.Round(tgt.PaceDeficit, 2));
            PluginManager.SetPropertyValue("SimRIG.Target.PaceDeficitStr", t, $"{(tgt.PaceDeficit > 0 ? "+" : "")}{tgt.PaceDeficit:F2}s/lap");
            PluginManager.SetPropertyValue("SimRIG.Target.RelativeDegradation", t, Math.Round(tgt.RelativeDegradation, 2));
            PluginManager.SetPropertyValue("SimRIG.Target.SectorPaceDropDueToTyres", t, Math.Round(tgt.TargetSectorPaceDropDueToTyres, 2));
            PluginManager.SetPropertyValue("SimRIG.Target.SectorPaceDropDueToTyresRaw", t, Math.Round(tgt.TargetSectorPaceDropDueToTyresRaw, 2));
            PluginManager.SetPropertyValue("SimRIG.Target.RelativeSectorDegradation", t, Math.Round(tgt.TargetSectorPaceDropDueToTyres - RaceAnalyzer.Results.SectorPaceDropDueToTyres, 2));

            PluginManager.SetPropertyValue("SimRIG.Target.LapBaselineNormalized", t, Math.Round(tgt.TargetLapBaselineNormalized, 3));
            PluginManager.SetPropertyValue("SimRIG.Target.LapBaselineRaw", t, Math.Round(tgt.TargetLapBaselineRaw, 3));
            PluginManager.SetPropertyValue("SimRIG.Target.LapMovingAverageNormalized", t, Math.Round(tgt.TargetLapMovingAverageNormalized, 3));
            PluginManager.SetPropertyValue("SimRIG.Target.LapMovingAverageRaw", t, Math.Round(tgt.TargetLapMovingAverageRaw, 3));

            PluginManager.SetPropertyValue("SimRIG.Target.SectorBaselineNormalized", t, Math.Round(tgt.TargetSectorBaselineNormalized, 3));
            PluginManager.SetPropertyValue("SimRIG.Target.SectorBaselineRaw", t, Math.Round(tgt.TargetSectorBaselineRaw, 3));
            PluginManager.SetPropertyValue("SimRIG.Target.SectorMovingAverageNormalized", t, Math.Round(tgt.TargetSectorMovingAverageNormalized, 3));
            PluginManager.SetPropertyValue("SimRIG.Target.SectorMovingAverageRaw", t, Math.Round(tgt.TargetSectorMovingAverageRaw, 3));
            PluginManager.SetPropertyValue("SimRIG.Target.UndercutAdvantage", t, Math.Round(tgt.UndercutAdvantage, 2));
            PluginManager.SetPropertyValue("SimRIG.Target.UndercutCaptureMargin", t, Math.Round(tgt.UndercutCaptureMargin, 2));
            PluginManager.SetPropertyValue("SimRIG.Target.UndercutCaptureMarginStr", t, $"{(tgt.UndercutCaptureMargin > 0 ? "+" : "")}{tgt.UndercutCaptureMargin:F1}s");
            PluginManager.SetPropertyValue("SimRIG.Target.UndercutViable", t, tgt.UndercutViable);

            PluginManager.SetPropertyValue("SimRIG.Target.OvercutAdvantage", t, Math.Round(tgt.OvercutAdvantage, 2));
            PluginManager.SetPropertyValue("SimRIG.Target.OvercutCaptureMargin", t, Math.Round(tgt.OvercutCaptureMargin, 2));
            PluginManager.SetPropertyValue("SimRIG.Target.OvercutCaptureMarginStr", t, $"{(tgt.OvercutCaptureMargin > 0 ? "+" : "")}{tgt.OvercutCaptureMargin:F1}s");
            PluginManager.SetPropertyValue("SimRIG.Target.OvercutViable", t, tgt.OvercutViable);

            PluginManager.SetPropertyValue("SimRIG.Target.TargetLapsUntilPit", t, Math.Round(tgt.TargetLapsUntilPit, 1));
            PluginManager.SetPropertyValue("SimRIG.Target.ReactionDeltaLaps", t, Math.Round(tgt.ReactionDeltaLaps, 1));
            PluginManager.SetPropertyValue("SimRIG.Target.OvercutStayLaps", t, Math.Round(tgt.OvercutStayLaps, 1));
            PluginManager.SetPropertyValue("SimRIG.Target.UndercutPositionOK", t, tgt.UndercutPositionOK);
            PluginManager.SetPropertyValue("SimRIG.Target.UndercutFuelOK", t, tgt.UndercutFuelOK);
            PluginManager.SetPropertyValue("SimRIG.Target.UndercutTrafficOK", t, tgt.UndercutTrafficOK);
            PluginManager.SetPropertyValue("SimRIG.Target.OvercutFuelOK", t, tgt.OvercutFuelOK);
            PluginManager.SetPropertyValue("SimRIG.Target.OvercutTrafficOK", t, tgt.OvercutTrafficOK);
            PluginManager.SetPropertyValue("SimRIG.Target.TargetPittedRecently", t, tgt.TargetPittedRecently);

            PluginManager.SetPropertyValue("SimRIG.Target.ProjectedMergeGap", t, Math.Round(tgt.ProjectedMergeGap, 2));
            PluginManager.SetPropertyValue("SimRIG.Target.TrafficAlert", t, tgt.TrafficAlert);
            PluginManager.SetPropertyValue("SimRIG.Target.TargetMode", t, tgt.TargetMode);
            PluginManager.SetPropertyValue("SimRIG.Target.CurrentTank", t, Math.Round(tgt.CurrentTank, 2));
            PluginManager.SetPropertyValue("SimRIG.Target.TankLapsRemaining", t, Math.Round(tgt.TankLapsRemaining, 1));
            PluginManager.SetPropertyValue("SimRIG.Target.SpeedDrop", t, Math.Round(tgt.SpeedDrop, 1));
            PluginManager.SetPropertyValue("SimRIG.Target.CurrentMicrosector", t, tgt.CurrentMicrosector);
            PluginManager.SetPropertyValue("SimRIG.Target.ProjectedStationaryTime", t, Math.Round(tgt.ProjectedStationaryTime, 1));
            PluginManager.SetPropertyValue("SimRIG.Target.PitCount", t, tgt.PitCount);
            PluginManager.SetPropertyValue("SimRIG.Target.CalculatedStationaryTime", t, Math.Round(tgt.CalculatedStationaryTime, 1));
            PluginManager.SetPropertyValue("SimRIG.Target.InOutPitAccDecTime", t, Math.Round(tgt.InOutPitAccDecTime, 2));
            PluginManager.SetPropertyValue("SimRIG.Target.EstimatedFuelToAdd", t, Math.Round(tgt.EstimatedFuelToAdd, 2));
            PluginManager.SetPropertyValue("SimRIG.Target.EstimatedFuelAdded", t, Math.Round(tgt.EstimatedFuelAdded, 2));
            PluginManager.SetPropertyValue("SimRIG.Target.EstimatedStationaryTime", t, Math.Round(tgt.EstimatedStationaryTime, 1));
            PluginManager.SetPropertyValue("SimRIG.Target.EstimatedFuelTank", t, Math.Round(tgt.EstimatedFuelTank, 2));
            PluginManager.SetPropertyValue("SimRIG.Target.LapCount", t, tgt.LapCount);
            PluginManager.SetPropertyValue("SimRIG.Target.EstimatedPitWindow", t, Math.Round(tgt.EstimatedPitWindow, 1));
            PluginManager.SetPropertyValue("SimRIG.Target.EstimatedPitWindowTargetLap", t, Math.Round(tgt.EstimatedPitWindowTargetLap, 1));
            PluginManager.SetPropertyValue("SimRIG.Session.ClassRaceStartingFuel", t, Math.Round(OpponentTracker.ClassRaceStartingFuel, 2));
            PluginManager.SetPropertyValue("SimRIG.Session.PitLayoutMode", t, PitRadar.PitLayoutMode);

            PluginManager.SetPropertyValue("SimRIG.Session.IsTrackWet", t, CurrentState.IsTrackWet);
            PluginManager.SetPropertyValue("SimRIG.Session.TrackWetnessLevel", t, CurrentState.TrackWetnessLevel);
            PluginManager.SetPropertyValue("SimRIG.Session.WindSpeed", t, Math.Round(CurrentState.WindSpeed, 1));
            PluginManager.SetPropertyValue("SimRIG.Session.AirTemperature", t, Math.Round(CurrentState.AirTemperature, 1));
            PluginManager.SetPropertyValue("SimRIG.Session.TimeToImpactMinutes", t, Math.Round(CurrentState.TimeToImpactMinutes, 1));
            PluginManager.SetPropertyValue("SimRIG.Session.TimeToImpactLaps", t, Math.Round(CurrentState.TimeToImpactLaps, 1));
            double wetLossVal = 0.0;
            if (OpponentTracker.PlayerData.BestNormalizedLapTimeWet > 0.0 && OpponentTracker.PlayerData.BestNormalizedLapTime > 0.0)
            {
                wetLossVal = OpponentTracker.PlayerData.BestNormalizedLapTimeWet - OpponentTracker.PlayerData.BestNormalizedLapTime;
            }
            PluginManager.SetPropertyValue("SimRIG.Session.WetPaceLoss", t, Math.Round(wetLossVal, 3));
            
            PluginManager.SetPropertyValue("SimRIG.Session.WeatherAlert", t, CurrentState.WeatherAlertState);
            PluginManager.SetPropertyValue("SimRIG.Session.IsPlayerOnSlick", t, CurrentState.IsPlayerOnSlick);
            PluginManager.SetPropertyValue("SimRIG.Session.CrossoverAlertState", t, CurrentState.CrossoverAlertState);
            PluginManager.SetPropertyValue("SimRIG.Session.CrossoverDeltaSeconds", t, Math.Round(CurrentState.CrossoverDeltaSeconds, 3));

            // Set new raw forecast properties
            PluginManager.SetPropertyValue("SimRIG.Session.RelativeHumidity", t, Math.Round(CurrentState.RelativeHumidity, 3));
            PluginManager.SetPropertyValue("SimRIG.Session.BarometricPressure", t, Math.Round(CurrentState.AirPressure, 1));
            PluginManager.SetPropertyValue("SimRIG.Session.PressureTrend", t, CurrentState.PressureTrend);
            PluginManager.SetPropertyValue("SimRIG.Session.TrackTemperature", t, Math.Round(CurrentState.TrackTemperature, 1));
            PluginManager.SetPropertyValue("SimRIG.Session.WindDirectionRelative", t, Math.Round(CurrentState.RelativeWindDirection, 1));
            PluginManager.SetPropertyValue("SimRIG.Session.AbsoluteWindDirection", t, Math.Round(CurrentState.AbsoluteWindDirection, 1));

            // Active Units labels
            string speedUnitTag = (Settings.SpeedUnit ?? "kmh").ToLowerInvariant();
            string speedUnitDisplay = speedUnitTag == "mph" ? "mph" : "km/h";

            string tempUnitTag = Settings.TempUnit ?? "C";
            string tempUnitDisplay = tempUnitTag == "F" ? "°F" : "°C";

            string pressUnitTag = (Settings.PressureUnit ?? "bar").ToLowerInvariant();
            string pressUnitDisplay = pressUnitTag == "psi" ? "psi" : (pressUnitTag == "kpa" || pressUnitTag == "hpa" ? "kPa" : "bar");

            PluginManager.SetPropertyValue("SimRIG.Units.SpeedUnit", t, speedUnitDisplay);
            PluginManager.SetPropertyValue("SimRIG.Units.TempUnit", t, tempUnitDisplay);
            PluginManager.SetPropertyValue("SimRIG.Units.PressureUnit", t, pressUnitDisplay);

            // Converted Vehicle Speed
            double vehicleSpeedConverted = CurrentState.SpeedKmh;
            if (speedUnitTag == "mph") vehicleSpeedConverted = CurrentState.SpeedKmh * 0.621371;
            PluginManager.SetPropertyValue("SimRIG.Vehicle.Speed", t, Math.Round(vehicleSpeedConverted, 1));
            PluginManager.SetPropertyValue("SimRIG.Vehicle.SpeedStr", t, $"{Math.Round(vehicleSpeedConverted, 0)} {speedUnitDisplay}");

            // Compute unit conversions for Session Weather & Environment
            double windSpeedConverted = CurrentState.WindSpeed;
            string windUnitLabel = "m/s";
            if (speedUnitTag == "kmh") { windSpeedConverted = CurrentState.WindSpeed * 3.6; windUnitLabel = "km/h"; }
            else if (speedUnitTag == "mph") { windSpeedConverted = CurrentState.WindSpeed * 2.23694; windUnitLabel = "mph"; }
            
            double airTempConverted = CurrentState.AirTemperature;
            double trackTempConverted = CurrentState.TrackTemperature;
            if (tempUnitTag == "F")
            {
                airTempConverted = (CurrentState.AirTemperature * 9.0 / 5.0) + 32.0;
                trackTempConverted = (CurrentState.TrackTemperature * 9.0 / 5.0) + 32.0;
            }

            double pressureConverted = CurrentState.AirPressure;
            string pressUnitLabel = "kPa";
            int pressDecimals = 1;
            if (pressUnitTag == "bar") { pressureConverted = CurrentState.AirPressure / 1000.0; pressUnitLabel = "bar"; pressDecimals = 3; }
            else if (pressUnitTag == "psi") { pressureConverted = CurrentState.AirPressure * 0.0145038; pressUnitLabel = "psi"; pressDecimals = 1; }
            else if (pressUnitTag == "kpa" || pressUnitTag == "hpa") { pressureConverted = CurrentState.AirPressure / 10.0; pressUnitLabel = "kPa"; pressDecimals = 1; }

            // Set Formatted Strings & Converted Values
            PluginManager.SetPropertyValue("SimRIG.Session.AirTemperature", t, Math.Round(airTempConverted, 1));
            PluginManager.SetPropertyValue("SimRIG.Session.TrackTemperature", t, Math.Round(trackTempConverted, 1));
            PluginManager.SetPropertyValue("SimRIG.Session.AirTemperatureStr", t, $"{Math.Round(airTempConverted, 1)} {tempUnitDisplay}");
            PluginManager.SetPropertyValue("SimRIG.Session.TrackTemperatureStr", t, $"{Math.Round(trackTempConverted, 1)} {tempUnitDisplay}");
            PluginManager.SetPropertyValue("SimRIG.Session.WindSpeedStr", t, $"{Math.Round(windSpeedConverted, 1)} {windUnitLabel}");
            PluginManager.SetPropertyValue("SimRIG.Session.AirPressureStr", t, $"{Math.Round(pressureConverted, pressDecimals).ToString(pressDecimals == 3 ? "0.000" : (pressDecimals == 2 ? "0.00" : "0.0"))} {pressUnitLabel}");
            PluginManager.SetPropertyValue("SimRIG.Session.RelativeHumidityStr", t, $"{Math.Round(CurrentState.RelativeHumidity * 100.0, 0)}%");
            PluginManager.SetPropertyValue("SimRIG.Session.TrackWetnessStr", t, $"{CurrentState.TrackWetnessLevel}%");

            // Wind relative direction label
            double rAngle = (CurrentState.RelativeWindDirection + 360.0) % 360.0;
            string windDirLabel = "HEADWIND";
            if (rAngle >= 22.5 && rAngle < 67.5) windDirLabel = "HEAD-R";
            else if (rAngle >= 67.5 && rAngle < 112.5) windDirLabel = "CROSS-R";
            else if (rAngle >= 112.5 && rAngle < 157.5) windDirLabel = "TAIL-R";
            else if (rAngle >= 157.5 && rAngle < 202.5) windDirLabel = "TAILWIND";
            else if (rAngle >= 202.5 && rAngle < 247.5) windDirLabel = "TAIL-L";
            else if (rAngle >= 247.5 && rAngle < 292.5) windDirLabel = "CROSS-L";
            else if (rAngle >= 292.5 && rAngle < 337.5) windDirLabel = "HEAD-L";
            PluginManager.SetPropertyValue("SimRIG.Session.WindDirectionLabel", t, windDirLabel);

            // Pressure trend string
            string pressTrendStr = "STABILE";
            if (CurrentState.PressureTrend == -1) pressTrendStr = "IN CALO";
            else if (CurrentState.PressureTrend == 1) pressTrendStr = "IN AUMENTO";
            PluginManager.SetPropertyValue("SimRIG.Session.PressureTrendStr", t, pressTrendStr);

            // Forecast settings
            PluginManager.SetPropertyValue("SimRIG.Forecast.ManualCrossoverOffset", t, _manualCrossoverOffset);
            string crossoverSign = _manualCrossoverOffset > 0 ? "+" : "";
            PluginManager.SetPropertyValue("SimRIG.Forecast.ManualCrossoverOffsetStr", t, _manualCrossoverOffset == 0.0 ? "AUTO" : $"{crossoverSign}{_manualCrossoverOffset:0.0}s");
            PluginManager.SetPropertyValue("SimRIG.Forecast.AlertThreshold", t, _alertThreshold);
            PluginManager.SetPropertyValue("SimRIG.Forecast.AlertThresholdStr", t, $"{_alertThreshold:0}%");

            PluginManager.SetPropertyValue("SimRIG.Target.PrePitNormalizedAverage", t, Math.Round(tgt.PrePitNormalizedAverage, 3));
            PluginManager.SetPropertyValue("SimRIG.Target.FuelToAddTime", t, Math.Round(tgt.FuelToAddTime, 1));
            PluginManager.SetPropertyValue("SimRIG.Target.PostPitDelta0", t, Math.Round(tgt.PostPitNormalizedDeltas[0], 3));
            PluginManager.SetPropertyValue("SimRIG.Target.PostPitDelta1", t, Math.Round(tgt.PostPitNormalizedDeltas[1], 3));
            PluginManager.SetPropertyValue("SimRIG.Target.PostPitDelta2", t, Math.Round(tgt.PostPitNormalizedDeltas[2], 3));
            PluginManager.SetPropertyValue("SimRIG.Target.PostPitPenalty0", t, Math.Round(tgt.PostPitWarmupPenalties[0], 3));
            PluginManager.SetPropertyValue("SimRIG.Target.PostPitPenalty1", t, Math.Round(tgt.PostPitWarmupPenalties[1], 3));
            PluginManager.SetPropertyValue("SimRIG.Target.PostPitPenalty2", t, Math.Round(tgt.PostPitWarmupPenalties[2], 3));

            if (OpponentTracker.TrackedOpponents.ContainsKey(tgt.Name))
            {
                var oppData = OpponentTracker.TrackedOpponents[tgt.Name];
                PluginManager.SetPropertyValue("SimRIG.Target.DropLow", t, Math.Round(oppData.ZoneDropLow, 1));
                PluginManager.SetPropertyValue("SimRIG.Target.DropMid", t, Math.Round(oppData.ZoneDropMid, 1));
                PluginManager.SetPropertyValue("SimRIG.Target.DropHigh", t, Math.Round(oppData.ZoneDropHigh, 1));
                PluginManager.SetPropertyValue("SimRIG.Target.BestLow", t, Math.Round(oppData.BestLapAvgSpeedLow, 1));
                PluginManager.SetPropertyValue("SimRIG.Target.BestMid", t, Math.Round(oppData.BestLapAvgSpeedMid, 1));
                PluginManager.SetPropertyValue("SimRIG.Target.BestHigh", t, Math.Round(oppData.BestLapAvgSpeedHigh, 1));
                PluginManager.SetPropertyValue("SimRIG.Target.BestLapNumber", t, oppData.BestMicrosectorSpeedLapCount);
            }
            else
            {
                PluginManager.SetPropertyValue("SimRIG.Target.DropLow", t, 0.0);
                PluginManager.SetPropertyValue("SimRIG.Target.DropMid", t, 0.0);
                PluginManager.SetPropertyValue("SimRIG.Target.DropHigh", t, 0.0);
                PluginManager.SetPropertyValue("SimRIG.Target.BestLow", t, 0.0);
                PluginManager.SetPropertyValue("SimRIG.Target.BestMid", t, 0.0);
                PluginManager.SetPropertyValue("SimRIG.Target.BestHigh", t, 0.0);
                PluginManager.SetPropertyValue("SimRIG.Target.BestLapNumber", t, 0);
            }

            // Expose Player properties
            PluginManager.SetPropertyValue("SimRIG.Player.DropLow", t, Math.Round(OpponentTracker.PlayerData.ZoneDropLow, 1));
            PluginManager.SetPropertyValue("SimRIG.Player.DropMid", t, Math.Round(OpponentTracker.PlayerData.ZoneDropMid, 1));
            PluginManager.SetPropertyValue("SimRIG.Player.DropHigh", t, Math.Round(OpponentTracker.PlayerData.ZoneDropHigh, 1));
            PluginManager.SetPropertyValue("SimRIG.Player.BestLow", t, Math.Round(OpponentTracker.PlayerData.BestLapAvgSpeedLow, 1));
            PluginManager.SetPropertyValue("SimRIG.Player.BestMid", t, Math.Round(OpponentTracker.PlayerData.BestLapAvgSpeedMid, 1));
            PluginManager.SetPropertyValue("SimRIG.Player.BestHigh", t, Math.Round(OpponentTracker.PlayerData.BestLapAvgSpeedHigh, 1));
            PluginManager.SetPropertyValue("SimRIG.Player.BestLapNumber", t, OpponentTracker.PlayerData.BestMicrosectorSpeedLapCount);
            PluginManager.SetPropertyValue("SimRIG.Player.Diagnosis", t, OpponentTracker.PlayerData.Diagnosis);
            PluginManager.SetPropertyValue("SimRIG.Player.SelectedTyreCompound", t, TyreManager.SelectedWetCompound ? "WET" : "DRY");

            PluginManager.SetPropertyValue("SimRIG.Pit.StationaryTimeLoss", t, Math.Round(PitRadar.LastStationaryTime, 1));
            PluginManager.SetPropertyValue("SimRIG.Pit.TransitTime", t, Math.Round(PitRadar.PitTransitTime, 2));
            PluginManager.SetPropertyValue("SimRIG.Pit.TransitDriveThrough", t, Math.Round(PitRadar.PitDriveThroughTime, 2));
            PluginManager.SetPropertyValue("SimRIG.Pit.MeasuredFuelFillRate", t, Math.Round(PitRadar.MeasuredFuelFillRate, 2));
            PluginManager.SetPropertyValue("SimRIG.Pit.CalibrationStatus", t, PitRadar.CalibrationStatus);
            PluginManager.SetPropertyValue("SimRIG.Pit.CalibrationStep", t, _calibrationStepText);
            PluginManager.SetPropertyValue("SimRIG.Pit.CalibrationMissing", t, PitRadar.CalibrationMissing);
            PluginManager.SetPropertyValue("SimRIG.Pit.GeofenceCalibrated", t, PitRadar.IsGeofenceCalibrated);
            PluginManager.SetPropertyValue("SimRIG.Pit.GeofenceConfidence", t, PitRadar.GeofenceConfidence.ToString());
            PluginManager.SetPropertyValue("SimRIG.Pit.FuelRateConfidence", t, PitRadar.FuelFillRateConfidence.ToString());
            PluginManager.SetPropertyValue("SimRIG.Pit.TyreTimeConfidence", t, PitRadar.TyreChangeTimeConfidence.ToString());
            PluginManager.SetPropertyValue("SimRIG.Pit.PlayerPitCount", t, RaceAnalyzer.Results.PlayerPitCount);
            PluginManager.SetPropertyValue("SimRIG.Pit.LeaderPitCount", t, RaceAnalyzer.Results.LeaderPitCount);
            PluginManager.SetPropertyValue("SimRIG.Pit.PitLaneZoneRacingTime", t, Math.Round(tgt.PitLaneZoneRacingTime, 2));
            PluginManager.SetPropertyValue("SimRIG.Pit.FuelCapacityInTireTime", t, Math.Round(_fuelCapacityInTireTimeCache, 1));
            PluginManager.SetPropertyValue("SimRIG.Pit.InOutPitAccDecTime", t, Math.Round(PitRadar.PitInOutAccDecTime, 2));

            double profileRefuelRate = CarPitData.GetProfile(CurrentState.CarClassId).RefuelRate;
            double refuelRate = PitRadar.MeasuredFuelFillRate > 0 ? PitRadar.MeasuredFuelFillRate : profileRefuelRate;
            double projectedFuelTime = refuelRate > 0 ? (FuelManager.Calculations.FuelToAdd / refuelRate) : 0.0;
            double projectedTireTime = TyreManager.GetSelectedTireTime(CurrentState.CarClassId);
            bool isSeqPit = CarPitData.GetProfile(CurrentState.CarClassId).IsSequential;
            double totalStationaryTimeVal = isSeqPit ? (projectedFuelTime + projectedTireTime) : Math.Max(projectedFuelTime, projectedTireTime);
            if (totalStationaryTimeVal > 0.0) totalStationaryTimeVal += 2.0; // +2.0s tempo morto martinetti

            double extRacingTimeVal = OpponentTracker.ClassBestExtendedPitZoneTime > 0.0
                ? OpponentTracker.ClassBestExtendedPitZoneTime
                : (tgt.PitLaneZoneRacingTime > 0.0 ? tgt.PitLaneZoneRacingTime : PitRadar.PitTransitTime);

            double totalPitLaneTimeVal = totalStationaryTimeVal + PitRadar.PitTransitTime;
            double projExtendedTimeVal = totalPitLaneTimeVal + PitRadar.PitInOutAccDecTime;
            double totalPitLossVal = Math.Max(0.0, projExtendedTimeVal - extRacingTimeVal);

            PluginManager.SetPropertyValue("SimRIG.Pit.TotalStationaryTime", t, Math.Round(totalStationaryTimeVal, 1));
            PluginManager.SetPropertyValue("SimRIG.Pit.TotalStationaryTimeStr", t, $"{totalStationaryTimeVal:F1}s");
            PluginManager.SetPropertyValue("SimRIG.Pit.TotalPitLoss", t, Math.Round(totalPitLossVal, 1));
            PluginManager.SetPropertyValue("SimRIG.Pit.TotalPitLossStr", t, $"{totalPitLossVal:F1}s");
            PluginManager.SetPropertyValue("SimRIG.Pit.IsSequential", t, isSeqPit);
            PluginManager.SetPropertyValue("SimRIG.Pit.PitLayoutMode", t, PitRadar.PitLayoutMode);

            // ExtendedSectorRacingZone and Diagnostics properties
            PluginManager.SetPropertyValue("SimRIG.Driver.ExtendedSectorRacingZoneState", t, RaceAnalyzer.PlayerExtendedSectorRacingZone.CurrentState);

            PluginManager.SetPropertyValue("SimRIG.Driver.ExtendedSectorRacingZone", t, Math.Round(RaceAnalyzer.PlayerExtendedSectorRacingZone.LastNormalTime, 3));
            PluginManager.SetPropertyValue("SimRIG.Driver.BestExtendedSectorRacingZone", t, Math.Round(RaceAnalyzer.PlayerExtendedSectorRacingZone.BestRawTime, 3));
            PluginManager.SetPropertyValue("SimRIG.Driver.BestExtendedSectorRacingZoneLapCount", t, RaceAnalyzer.PlayerExtendedSectorRacingZone.BestRawTimeLapCount);

            double driverPitZoneTime = 0.0;
            if (CurrentState.IsSessionActive && (!CurrentState.IsRaceSession || CurrentState.SessionStateStatus == 4))
            {
                driverPitZoneTime = RaceAnalyzer.PlayerExtendedPitZone.GetCurrentTime(CurrentState.SessionTimeLeftSec);
            }
            PluginManager.SetPropertyValue("SimRIG.Driver.ExtendedPitZoneTime", t, Math.Round(driverPitZoneTime, 3));
            PluginManager.SetPropertyValue("SimRIG.Driver.IsInPit", t, CurrentState.IsInPitLane);

            double targetSectorTime = 0.0;
            double targetBestSectorTime = 0.0;
            int targetBestSectorLapCount = 0;
            double targetPitZoneTime = 0.0;
            bool targetIsInPit = false;

            if (tgt != null && !string.IsNullOrEmpty(tgt.Name) && OpponentTracker.TrackedOpponents.ContainsKey(tgt.Name))
            {
                var oppData = OpponentTracker.TrackedOpponents[tgt.Name];
                targetSectorTime = oppData.ExtendedSectorRacingZone.LastNormalTime;
                targetBestSectorTime = oppData.ExtendedSectorRacingZone.BestRawTime;
                targetBestSectorLapCount = oppData.ExtendedSectorRacingZone.BestRawTimeLapCount;
                if (CurrentState.IsSessionActive && (!CurrentState.IsRaceSession || CurrentState.SessionStateStatus == 4))
                {
                    targetPitZoneTime = oppData.ExtendedPitZone.GetCurrentTime(CurrentState.SessionTimeLeftSec);
                }
                targetIsInPit = oppData.IsInsideGeofence;
            }
            PluginManager.SetPropertyValue("SimRIG.Target.ExtendedSectorRacingZone", t, Math.Round(targetSectorTime, 3));
            PluginManager.SetPropertyValue("SimRIG.Target.BestExtendedSectorRacingZone", t, Math.Round(targetBestSectorTime, 3));
            PluginManager.SetPropertyValue("SimRIG.Target.BestExtendedSectorRacingZoneLapCount", t, targetBestSectorLapCount);

            PluginManager.SetPropertyValue("SimRIG.Target.ExtendedPitZoneTime", t, Math.Round(targetPitZoneTime, 3));
            PluginManager.SetPropertyValue("SimRIG.Target.IsInPit", t, targetIsInPit);

            // Find leader's state and pit status
            string leaderState = "NORMAL";
            double leaderTime = 0.0;
            bool leaderIsInPit = false;

            if (CurrentState.Position == 1)
            {
                leaderState = RaceAnalyzer.PlayerExtendedSectorRacingZone.CurrentState;
                leaderTime = RaceAnalyzer.PlayerExtendedSectorRacingZone.LastNormalTime;
                leaderIsInPit = CurrentState.IsInPitLane;
            }
            else
            {
                var p1Opponent = CurrentState.Opponents.FirstOrDefault(o => o.Position == 1);
                if (p1Opponent != null && OpponentTracker.TrackedOpponents.ContainsKey(p1Opponent.Name))
                {
                    var oppData = OpponentTracker.TrackedOpponents[p1Opponent.Name];
                    leaderState = oppData.ExtendedSectorRacingZone.CurrentState;
                    leaderTime = oppData.ExtendedSectorRacingZone.LastNormalTime;
                    leaderIsInPit = oppData.IsInsideGeofence;
                }
            }

            PluginManager.SetPropertyValue("SimRIG.Leader.ExtendedSectorRacingZoneState", t, leaderState);
            PluginManager.SetPropertyValue("SimRIG.Leader.ExtendedSectorRacingZone", t, Math.Round(leaderTime, 3));
            PluginManager.SetPropertyValue("SimRIG.Leader.IsInPit", t, leaderIsInPit);

            // Diagnostic properties
            PluginManager.SetPropertyValue("SimRIG.Diagnostics.PitEntryPct", t, PitRadar.GetPitEntryPctValue());
            PluginManager.SetPropertyValue("SimRIG.Diagnostics.PitExitPct", t, PitRadar.GetPitExitPctValue());
            PluginManager.SetPropertyValue("SimRIG.Diagnostics.ExtendedPitEntryPct", t, PitRadar.GetExtendedPitEntryPct());
            PluginManager.SetPropertyValue("SimRIG.Diagnostics.ExtendedPitExitPct", t, PitRadar.GetExtendedPitExitPct());
        }

        private void SetCornerMessage(int cornerIdx, string msg, int durationMs = 2000)
        {
            CancellationTokenSource cts = null;
            if (cornerIdx == 0) { if (_msgTokenTL != null) _msgTokenTL.Cancel(); _msgTokenTL = new CancellationTokenSource(); cts = _msgTokenTL; _msgTL = msg; }
            else if (cornerIdx == 1) { if (_msgTokenTR != null) _msgTokenTR.Cancel(); _msgTokenTR = new CancellationTokenSource(); cts = _msgTokenTR; _msgTR = msg; }
            else if (cornerIdx == 2) { if (_msgTokenBL != null) _msgTokenBL.Cancel(); _msgTokenBL = new CancellationTokenSource(); cts = _msgTokenBL; _msgBL = msg; }
            else if (cornerIdx == 3) { if (_msgTokenBR != null) _msgTokenBR.Cancel(); _msgTokenBR = new CancellationTokenSource(); cts = _msgTokenBR; _msgBR = msg; }
            UpdateSimHubProperties();
            Task.Run(async () => {
                try
                {
                    await Task.Delay(durationMs, cts.Token);
                    if (!cts.Token.IsCancellationRequested)
                    {
                        if (cornerIdx == 0) _msgTL = ""; else if (cornerIdx == 1) _msgTR = ""; else if (cornerIdx == 2) _msgBL = ""; else if (cornerIdx == 3) _msgBR = "";
                        UpdateSimHubProperties();
                    }
                }
                catch { }
            });
        }

        private static string FormatTime(double totalSeconds)
        {
            if (totalSeconds <= 0) return "00:00.000";
            TimeSpan t = TimeSpan.FromSeconds(totalSeconds);
            if (t.TotalHours >= 1.0) return t.ToString(@"hh\:mm\:ss\.fff");
            return t.ToString(@"mm\:ss\.fff");
        }

        public void ApplyProfile(SimRigProfile profile)
        {
            if (profile == null) return;
            CurrentProfile = profile;
            IsGlobalTesting = false;
            HardwareManager?.SendRawLedCommand("GAME:0");
            HardwareManager?.SendRawLedCommand("RPM:0");
            for (int i = 0; i < 12; i++) ButtonColors[i] = Colors.White;
            if (CurrentProfile.ButtonColorsHex != null && CurrentProfile.ButtonColorsHex.Count == 12)
            {
                for (int i = 0; i < 12; i++) ButtonColors[i] = ProfileManager.HexToColor(CurrentProfile.ButtonColorsHex[i]);
            }
            SendLedConfig();
            Task.Run(() => { System.Threading.Thread.Sleep(50); SyncAllColorsToHardware(); });
            Settings.TopLeftEncoderMode = CurrentProfile.EncoderTopLeft_Mode;
            Settings.TopRightEncoderMode = CurrentProfile.EncoderTopRight_Mode;
            Settings.LastProfileUsed = CurrentProfile.ProfileName;
            this.SaveCommonSettings("GeneralSettings", Settings);
            HardwareManager?.SendEncoderMapping(0, Settings.TopLeftEncoderMode);
            HardwareManager?.SendEncoderMapping(1, Settings.TopRightEncoderMode);
        }

        public void SaveCurrentProfileToDisk(string name)
        {
            CurrentProfile.ProfileName = name;
            CurrentProfile.ButtonColorsHex.Clear();
            for (int i = 0; i < 12; i++) CurrentProfile.ButtonColorsHex.Add(ProfileManager.ColorToHex(ButtonColors[i]));
            CurrentProfile.EncoderTopLeft_Mode = Settings.TopLeftEncoderMode;
            CurrentProfile.EncoderTopRight_Mode = Settings.TopRightEncoderMode;
            CurrentProfile.BitePoint = _liveBitePoint;
            ProfileManager.SaveProfile(CurrentProfile, name);
            Settings.LastProfileUsed = name;
            this.SaveCommonSettings("GeneralSettings", Settings);
            SendLedConfig();
        }

        public void SendLedConfig()
        {
            if (!IsLedsConnected) return;
            try
            {
                HardwareManager.SendRawLedCommand($"BRT:{CurrentProfile.Brightness_Backlight}:{CurrentProfile.Brightness_RPM}");
                HardwareManager.SendRawLedCommand($"RPMCFG:{CurrentProfile.Rpm_StartLed}:{CurrentProfile.Rpm_LedCount}");
                HardwareManager.SendRawLedCommand($"IDLE:{CurrentProfile.Idle_Mode}");
                Color cIdle = ProfileManager.HexToColor(CurrentProfile.Idle_Color);
                HardwareManager.SendRawLedCommand($"IDLECOL:{cIdle.R},{cIdle.G},{cIdle.B}");
                int styleIdx = 0;
                if (CurrentProfile.Rpm_Style == "RightToLeft") styleIdx = 1; else if (CurrentProfile.Rpm_Style == "CenterToSide") styleIdx = 2; else if (CurrentProfile.Rpm_Style == "SideToCenter") styleIdx = 3;
                HardwareManager.SendRawLedCommand($"RPMSTYLE:{styleIdx}");
                if (CurrentProfile.Rpm_UseGradient)
                {
                    HardwareManager.SendRawLedCommand("RPMGRAD:1");
                    Color cStart = ProfileManager.HexToColor(CurrentProfile.Rpm_Color_Start);
                    Color cEnd = ProfileManager.HexToColor(CurrentProfile.Rpm_Color_End);
                    HardwareManager.SendRawLedCommand($"RPMCOLS:{cStart.R},{cStart.G},{cStart.B}:{cEnd.R},{cEnd.G},{cEnd.B}");
                }
                else
                {
                    HardwareManager.SendRawLedCommand("RPMGRAD:0");
                    Color c1 = ProfileManager.HexToColor(CurrentProfile.Rpm_ZoneLow_Color); int cnt1 = CurrentProfile.Rpm_ZoneLow_Count; HardwareManager.SendRawLedCommand($"RPMSEG:0:{cnt1}:{c1.R},{c1.G},{c1.B}");
                    Color c2 = ProfileManager.HexToColor(CurrentProfile.Rpm_ZoneMed_Color); int cnt2 = CurrentProfile.Rpm_ZoneMed_Count; HardwareManager.SendRawLedCommand($"RPMSEG:{cnt1}:{cnt2}:{c2.R},{c2.G},{c2.B}");
                    Color c3 = ProfileManager.HexToColor(CurrentProfile.Rpm_ZoneHigh_Color); int cnt3 = CurrentProfile.Rpm_ZoneHigh_Count; HardwareManager.SendRawLedCommand($"RPMSEG:{cnt1 + cnt2}:{cnt3}:{c3.R},{c3.G},{c3.B}");
                    Color c4 = ProfileManager.HexToColor(CurrentProfile.Rpm_ZoneMax_Color); int cnt4 = CurrentProfile.Rpm_ZoneMax_Count; HardwareManager.SendRawLedCommand($"RPMSEG:{cnt1 + cnt2 + cnt3}:{cnt4}:{c4.R},{c4.G},{c4.B}");
                }
                SendEventConfig(0, CurrentProfile.Flag_Yellow); SendEventConfig(2, CurrentProfile.Flag_Blue); SendEventConfig(3, CurrentProfile.Flag_Green);
                SendEventConfig(4, CurrentProfile.Car_ABS); SendEventConfig(5, CurrentProfile.Car_TC); SendEventConfig(6, CurrentProfile.Car_Pit);
            }
            catch { }
        }

        private void SendEventConfig(int id, LedEventConfig cfg)
        {
            if (!IsLedsConnected || cfg == null) return;
            Color c1 = ProfileManager.HexToColor(cfg.ColorHex); Color c2 = ProfileManager.HexToColor(cfg.ColorHexSecondary);
            string cmd = $"EVTCFG:{id}:{cfg.ZoneA_Start}:{cfg.ZoneA_Count}:{(cfg.ZoneB_Enabled ? 1 : 0)}:{cfg.ZoneB_Start}:{cfg.ZoneB_Count}:{(cfg.IsBlinking ? 1 : 0)}:{cfg.BlinkIntervalMs}:{c1.R},{c1.G},{c1.B}:{c2.R},{c2.G},{c2.B}";
            HardwareManager.SendRawLedCommand(cmd);
        }

        public void SyncAllColorsToHardware()
        {
            if (!IsLedsConnected) return;
            for (int i = 0; i < 12; i++) { SetButtonColor(i, ButtonColors[i]); System.Threading.Thread.Sleep(10); }
        }

        public void SetButtonColor(int logicalId, Color c)
        {
            if (logicalId < 0 || logicalId > 11) return;
            ButtonColors[logicalId] = c;
            if (IsLedsConnected)
            {
                string cmdPrefix; int ledIndex;
                if (logicalId < 6) { cmdPrefix = "BL"; ledIndex = logicalId; }
                else { cmdPrefix = "BR"; int relativeIndex = logicalId - 6; ledIndex = 5 - relativeIndex; }
                HardwareManager.SendRawLedCommand($"{cmdPrefix}:{ledIndex}:{c.R},{c.G},{c.B}");
            }
        }

        public void ReloadVoiceEngine() { InitVoiceEngine(); }

        private void InitVoiceEngine()
        {
            try
            {
                string lang = Settings.VoiceLanguage;
                string voiceId = Settings.SelectedVoiceModel;
                if (!PiperEngine.Instance.CheckEngineInstalled() || !PiperEngine.Instance.CheckModelInstalled(lang, voiceId))
                {
                    PiperEngine.Instance.StartSetupAsync(lang, voiceId);
                }
            }
            catch (Exception ex)
            {
                SimHub.Logging.Current.Error("Error in InitVoiceEngine: " + ex.Message);
            }
        }

        private void TriggerWeatherReportSpeech()
        {
            string lang = Settings.VoiceLanguage ?? "EN";
            bool isIt = lang.Equals("IT", StringComparison.OrdinalIgnoreCase);

            int hVal = (int)Math.Round(CurrentState.RelativeHumidity * 100.0, 0);
            
            double wVal = CurrentState.WindSpeed;
            string wUnit = isIt ? "metri al secondo" : "meters per second";
            if (Settings.SpeedUnit == "kmh")
            {
                wVal = CurrentState.WindSpeed * 3.6;
                wUnit = isIt ? "chilometri orari" : "kilometers per hour";
            }
            else if (Settings.SpeedUnit == "mph")
            {
                wVal = CurrentState.WindSpeed * 2.23694;
                wUnit = isIt ? "miglia orarie" : "miles per hour";
            }

            string windSpeech = $"{Math.Round(wVal, 0)} {wUnit}";
            string textToSpeak = "";

            if (CurrentState.TrackWetnessLevel >= 1)
            {
                textToSpeak = isIt 
                    ? "Pista bagnata. Gomme da bagnato consigliate."
                    : "Wet track. Wet tires recommended.";
            }
            else if (CurrentState.TimeToImpactMinutes < 90.0)
            {
                int min = (int)Math.Round(CurrentState.TimeToImpactMinutes, 0);
                textToSpeak = isIt
                    ? $"Pioggia attesa tra circa {min} minuti. Umidità al {hVal}%. Vento a {windSpeech}."
                    : $"Rain expected in about {min} minutes. Humidity at {hVal}%. Wind at {windSpeech}.";
            }
            else
            {
                textToSpeak = isIt
                    ? $"Nessuna pioggia prevista. Pista asciutta. Umidità al {hVal}%. Vento a {windSpeech}."
                    : $"No rain expected. Dry track. Humidity at {hVal}%. Wind at {windSpeech}.";
            }

            TriggerRadioVoice("RAW_SPEECH", textToSpeak);
        }

        private void TriggerRadioVoice(string phraseKey, params object[] args)
        {
            if (!Settings.EnableVoiceEngineer) return;
            string textToSpeak = PitWallLanguage.GetPhrase(Settings.VoiceLanguage, phraseKey, args);
            if (string.IsNullOrEmpty(textToSpeak)) return;

            string lang = Settings.VoiceLanguage;
            string voiceId = Settings.SelectedVoiceModel;
            if (PiperEngine.Instance.CheckEngineInstalled() && PiperEngine.Instance.CheckModelInstalled(lang, voiceId))
            {
                var voiceSettings = Settings.GetSettingsForVoice(voiceId);

                if (LogManager != null)
                {
                    string dataStr = $"VoiceId: {voiceId} | Lang: {lang} | PhraseKey: {phraseKey} | Args: {(args != null ? string.Join(", ", args) : "")}";
                    LogManager.Log(LogModule.VOICE, LogType.EVENT, $"Speaking: \"{textToSpeak}\"", dataStr);
                }

                PiperEngine.Instance.Speak(textToSpeak, lang, voiceId, voiceSettings.VoiceVolume, voiceSettings.RadioNoiseVolume, voiceSettings.SpeechSpeed);
            }
        }

        public void End(PluginManager pluginManager)
        {
            HardwareManager?.Shutdown();
            LogManager?.Shutdown();
            try
            {
                PiperEngine.Instance.CancelDownload();
            }
            catch { }
            this.SaveCommonSettings("GeneralSettings", Settings);
        }

        private double _sec1Time = 0.0;
        private double _sec2Time = 0.0;
        private double _sec3Time = 0.0;

        private double _lastLapSec1 = 0.0;
        private double _lastLapSec2 = 0.0;
        private double _lastLapSec3 = 0.0;

        private double _bestSec1 = 0.0;
        private double _bestSec2 = 0.0;
        private double _bestSec3 = 0.0;

        private double _lastSecTime = 0.0;
        private double _holdUntilClock = 0.0;
        private int _lastLapCount = -1;
        private int _lastSectorIdx = -1;

        private int _leftLeaderboardPageOffset = 0;
        private int _rightLeaderboardPageOffset = 0;

        private void UpdateCustomSectors(GameData data)
        {
            Type t = this.GetType();
            if (data?.NewData == null) return;

            double currentClock = data.NewData.SessionTimeLeft.TotalSeconds;
            int currentLap = data.NewData.CompletedLaps;
            int sectorIdx = data.NewData.CurrentSectorIndex;
            double currentLapTime = data.NewData.CurrentLapTime.TotalSeconds;

            // Detect lap completion
            if (_lastLapCount != -1 && currentLap != _lastLapCount)
            {
                _holdUntilClock = currentClock - 5.0; // 5 seconds hold (SessionTimeLeft counts down)
                
                if (data.NewData.LastLapTime.TotalSeconds > 0 && _sec1Time > 0 && _sec2Time > 0)
                {
                    double calc = data.NewData.LastLapTime.TotalSeconds - (_sec1Time + _sec2Time);
                    if (calc > 0) _sec3Time = calc;
                }

                if (_sec3Time > 0)
                {
                    if (_bestSec3 == 0.0 || _sec3Time < _bestSec3) _bestSec3 = _sec3Time;
                    _lastSecTime = _sec3Time;
                }

                _lastLapSec1 = _sec1Time;
                _lastLapSec2 = _sec2Time;
                _lastLapSec3 = _sec3Time;

                _sec1Time = 0.0;
                _sec2Time = 0.0;
                _sec3Time = 0.0;
            }
            _lastLapCount = currentLap;

            // Detect sector transitions
            if (_lastSectorIdx != -1 && sectorIdx != _lastSectorIdx)
            {
                if (_lastSectorIdx == 1 && sectorIdx == 2)
                {
                    _sec1Time = currentLapTime;
                    if (_bestSec1 == 0.0 || _sec1Time < _bestSec1) _bestSec1 = _sec1Time;
                    _lastSecTime = _sec1Time;
                }
                else if (_lastSectorIdx == 2 && sectorIdx == 3)
                {
                    _sec2Time = currentLapTime - _sec1Time;
                    if (_sec2Time > 0)
                    {
                        if (_bestSec2 == 0.0 || _sec2Time < _bestSec2) _bestSec2 = _sec2Time;
                        _lastSecTime = _sec2Time;
                    }
                }
            }
            _lastSectorIdx = sectorIdx;

            // 5-second post-finish line hold window
            bool isHolding = (currentClock > _holdUntilClock) && currentLapTime <= 5.0 && currentLap > 0;

            double activeS1 = isHolding ? _lastLapSec1 : _sec1Time;
            double activeS2 = isHolding ? _lastLapSec2 : _sec2Time;
            double activeS3 = isHolding ? _lastLapSec3 : _sec3Time;

            string s1Str = activeS1 > 0 ? string.Format("{0:F3}", activeS1) : "--.--";
            string s2Str = activeS2 > 0 ? string.Format("{0:F3}", activeS2) : "--.--";
            string s3Str = activeS3 > 0 ? string.Format("{0:F3}", activeS3) : "--.--";

            string bestS1Str = _bestSec1 > 0 ? string.Format("{0:F3}", _bestSec1) : "--.--";
            string bestS2Str = _bestSec2 > 0 ? string.Format("{0:F3}", _bestSec2) : "--.--";
            string bestS3Str = _bestSec3 > 0 ? string.Format("{0:F3}", _bestSec3) : "--.--";

            // Colors: Purple (#FFA020F0) if Session Best, Green (#FF00FF7F) if completed, White (#FFFFFFFF) default
            string s1Color = (_bestSec1 > 0 && activeS1 > 0 && Math.Abs(activeS1 - _bestSec1) < 0.001) ? "#FFA020F0" : (activeS1 > 0 ? "#FF00FF7F" : "#FFFFFFFF");
            string s2Color = (_bestSec2 > 0 && activeS2 > 0 && Math.Abs(activeS2 - _bestSec2) < 0.001) ? "#FFA020F0" : (activeS2 > 0 ? "#FF00FF7F" : "#FFFFFFFF");
            string s3Color = (_bestSec3 > 0 && activeS3 > 0 && Math.Abs(activeS3 - _bestSec3) < 0.001) ? "#FFA020F0" : (activeS3 > 0 ? "#FF00FF7F" : "#FFFFFFFF");

            string lastSecStr = _lastSecTime > 0 ? string.Format("{0:F3}", _lastSecTime) : "0.000";

            PluginManager.SetPropertyValue("SimRIG.Player.Sector1Str", t, s1Str);
            PluginManager.SetPropertyValue("SimRIG.Player.Sector2Str", t, s2Str);
            PluginManager.SetPropertyValue("SimRIG.Player.Sector3Str", t, s3Str);

            PluginManager.SetPropertyValue("SimRIG.Player.Sector1Color", t, s1Color);
            PluginManager.SetPropertyValue("SimRIG.Player.Sector2Color", t, s2Color);
            PluginManager.SetPropertyValue("SimRIG.Player.Sector3Color", t, s3Color);

            PluginManager.SetPropertyValue("SimRIG.Player.BestSector1Str", t, bestS1Str);
            PluginManager.SetPropertyValue("SimRIG.Player.BestSector2Str", t, bestS2Str);
            PluginManager.SetPropertyValue("SimRIG.Player.BestSector3Str", t, bestS3Str);

            PluginManager.SetPropertyValue("SimRIG.Player.LastSectorStr", t, lastSecStr);
        }

        private void UpdateCustomLeaderboards(GameData data)
        {
            Type t = this.GetType();

            // Populate LEFT leaderboard properties
            PopulateLeaderboardForSide(data, "Left", _leftLeaderboardPageOffset);

            // Populate RIGHT leaderboard properties
            PopulateLeaderboardForSide(data, "Right", _rightLeaderboardPageOffset);
        }

        private void PopulateLeaderboardForSide(GameData data, string sidePrefix, int pageOffset)
        {
            Type t = this.GetType();
            int startRank = pageOffset * 7 + 1;
            int endRank = startRank + 6;

            bool isClassRelative = (pageOffset % 2) != 0;
            string classHeader = pageOffset == 0 ? "CLASS LEADERBOARD" : string.Format("CLASS LEADERBOARD (P{0}-P{1})", startRank, endRank);
            string overallHeader = pageOffset == 0 ? "OVERALL LEADERBOARD" : string.Format("OVERALL LEADERBOARD (P{0}-P{1})", startRank, endRank);
            string relativeHeader = isClassRelative ? "CLASS RELATIVE LEADERBOARD" : "OVERALL RELATIVE LEADERBOARD";

            PluginManager.SetPropertyValue("SimRIG." + sidePrefix + ".Class.HeaderStr", t, classHeader);
            PluginManager.SetPropertyValue("SimRIG." + sidePrefix + ".Overall.HeaderStr", t, overallHeader);
            PluginManager.SetPropertyValue("SimRIG." + sidePrefix + ".Relative.HeaderStr", t, relativeHeader);

            if (data?.NewData?.Opponents == null || data.NewData.Opponents.Count == 0)
            {
                for (int i = 1; i <= 7; i++)
                {
                    PluginManager.SetPropertyValue("SimRIG." + sidePrefix + ".Class.C" + i + "_Pos", t, "P" + (startRank + i - 1));
                    PluginManager.SetPropertyValue("SimRIG." + sidePrefix + ".Class.C" + i + "_Name", t, "---");
                    PluginManager.SetPropertyValue("SimRIG." + sidePrefix + ".Class.C" + i + "_Gap", t, "---");
                    PluginManager.SetPropertyValue("SimRIG." + sidePrefix + ".Class.C" + i + "_Class", t, "");
                    PluginManager.SetPropertyValue("SimRIG." + sidePrefix + ".Class.C" + i + "_Color", t, "#FFFFFFFF");

                    PluginManager.SetPropertyValue("SimRIG." + sidePrefix + ".Overall.O" + i + "_Pos", t, "P" + (startRank + i - 1));
                    PluginManager.SetPropertyValue("SimRIG." + sidePrefix + ".Overall.O" + i + "_Name", t, "---");
                    PluginManager.SetPropertyValue("SimRIG." + sidePrefix + ".Overall.O" + i + "_Gap", t, "---");
                    PluginManager.SetPropertyValue("SimRIG." + sidePrefix + ".Overall.O" + i + "_Class", t, "");
                    PluginManager.SetPropertyValue("SimRIG." + sidePrefix + ".Overall.O" + i + "_Color", t, "#FFFFFFFF");

                    PluginManager.SetPropertyValue("SimRIG." + sidePrefix + ".Relative.R" + i + "_Pos", t, "--");
                    PluginManager.SetPropertyValue("SimRIG." + sidePrefix + ".Relative.R" + i + "_Name", t, "---");
                    PluginManager.SetPropertyValue("SimRIG." + sidePrefix + ".Relative.R" + i + "_Gap", t, "---");
                    PluginManager.SetPropertyValue("SimRIG." + sidePrefix + ".Relative.R" + i + "_LastLap", t, "--:--.---");
                    PluginManager.SetPropertyValue("SimRIG." + sidePrefix + ".Relative.R" + i + "_Class", t, "");
                    PluginManager.SetPropertyValue("SimRIG." + sidePrefix + ".Relative.R" + i + "_Color", t, "#FFFFFFFF");
                }
                return;
            }

            var opponents = data.NewData.Opponents.Where(o => o != null && o.TrackPositionPercent.HasValue).ToList();
            var player = opponents.FirstOrDefault(o => o.IsPlayer);
            string playerClass = player != null ? (player.CarClass ?? "") : "";
            double playerProgress = player != null ? ((player.CurrentLap ?? 1) + player.TrackPositionPercent.Value) : 0.0;
            double trackLen = CurrentState.TrackLengthMeters > 0 ? CurrentState.TrackLengthMeters : 5000.0;
            
            double lapPaceSec = 100.0;
            if (data.NewData.LastLapTime.TotalSeconds > 30.0)
                lapPaceSec = data.NewData.LastLapTime.TotalSeconds;
            else if (player != null && player.BestLapTime.TotalSeconds > 30.0)
                lapPaceSec = player.BestLapTime.TotalSeconds;
            else if (trackLen > 1000.0)
                lapPaceSec = trackLen / 45.0;

            // 1. OVERALL LEADERBOARD (Paged for side)
            var overallSorted = opponents.OrderBy(o => o.Position > 0 ? o.Position : 999)
                                         .ThenByDescending(o => (o.CurrentLap ?? 1) + o.TrackPositionPercent.Value)
                                         .ToList();
            for (int i = 1; i <= 7; i++)
            {
                int oppIndex = (startRank - 1) + (i - 1);
                if (oppIndex < overallSorted.Count)
                {
                    var opp = overallSorted[oppIndex];
                    int pos = opp.Position > 0 ? opp.Position : (oppIndex + 1);
                    PluginManager.SetPropertyValue("SimRIG." + sidePrefix + ".Overall.O" + i + "_Pos", t, "P" + pos);
                    PluginManager.SetPropertyValue("SimRIG." + sidePrefix + ".Overall.O" + i + "_Name", t, opp.Name ?? "---");
                    
                    string gapStr = "LEADER";
                    if (oppIndex > 0)
                    {
                        var carAhead = overallSorted[oppIndex - 1];
                        double progressAhead = (carAhead.CurrentLap ?? 1) + carAhead.TrackPositionPercent.Value;
                        double progressCurr = (opp.CurrentLap ?? 1) + opp.TrackPositionPercent.Value;
                        double deltaProgress = progressAhead - progressCurr;
                        int lapDiff = (carAhead.CurrentLap ?? 1) - (opp.CurrentLap ?? 1);

                        if (lapDiff > 1)
                        {
                            gapStr = string.Format("+{0} LAPS", lapDiff);
                        }
                        else if (lapDiff == 1 && deltaProgress >= 1.0)
                        {
                            gapStr = "+1 LAP";
                        }
                        else
                        {
                            if (deltaProgress < 0) deltaProgress = 0;
                            double gapSec = deltaProgress * lapPaceSec;
                            gapStr = string.Format("+{0:F1}s", Math.Abs(gapSec));
                        }
                    }

                    string colorStr = opp.IsPlayer ? "#FF00E5FF" : "#FFFFFFFF";

                    PluginManager.SetPropertyValue("SimRIG." + sidePrefix + ".Overall.O" + i + "_Gap", t, gapStr);
                    PluginManager.SetPropertyValue("SimRIG." + sidePrefix + ".Overall.O" + i + "_Class", t, opp.CarClass ?? "");
                    PluginManager.SetPropertyValue("SimRIG." + sidePrefix + ".Overall.O" + i + "_Color", t, colorStr);
                }
                else
                {
                    PluginManager.SetPropertyValue("SimRIG." + sidePrefix + ".Overall.O" + i + "_Pos", t, "P" + (startRank + i - 1));
                    PluginManager.SetPropertyValue("SimRIG." + sidePrefix + ".Overall.O" + i + "_Name", t, "---");
                    PluginManager.SetPropertyValue("SimRIG." + sidePrefix + ".Overall.O" + i + "_Gap", t, "---");
                    PluginManager.SetPropertyValue("SimRIG." + sidePrefix + ".Overall.O" + i + "_Class", t, "");
                    PluginManager.SetPropertyValue("SimRIG." + sidePrefix + ".Overall.O" + i + "_Color", t, "#FFFFFFFF");
                }
            }

            // 2. CLASS LEADERBOARD (Paged for side)
            var classSorted = opponents.Where(o => string.IsNullOrEmpty(playerClass) || string.Equals(o.CarClass, playerClass, StringComparison.OrdinalIgnoreCase))
                                       .OrderBy(o => o.Position > 0 ? o.Position : 999)
                                       .ThenByDescending(o => (o.CurrentLap ?? 1) + o.TrackPositionPercent.Value)
                                       .ToList();
            for (int i = 1; i <= 7; i++)
            {
                int oppIndex = (startRank - 1) + (i - 1);
                if (oppIndex < classSorted.Count)
                {
                    var opp = classSorted[oppIndex];
                    int cPos = oppIndex + 1;
                    PluginManager.SetPropertyValue("SimRIG." + sidePrefix + ".Class.C" + i + "_Pos", t, "P" + cPos);
                    PluginManager.SetPropertyValue("SimRIG." + sidePrefix + ".Class.C" + i + "_Name", t, opp.Name ?? "---");
                    
                    string gapStr = "LEADER";
                    if (oppIndex > 0)
                    {
                        var carAhead = classSorted[oppIndex - 1];
                        double progressAhead = (carAhead.CurrentLap ?? 1) + carAhead.TrackPositionPercent.Value;
                        double progressCurr = (opp.CurrentLap ?? 1) + opp.TrackPositionPercent.Value;
                        double deltaProgress = progressAhead - progressCurr;
                        int lapDiff = (carAhead.CurrentLap ?? 1) - (opp.CurrentLap ?? 1);

                        if (lapDiff > 1)
                        {
                            gapStr = string.Format("+{0} LAPS", lapDiff);
                        }
                        else if (lapDiff == 1 && deltaProgress >= 1.0)
                        {
                            gapStr = "+1 LAP";
                        }
                        else
                        {
                            if (deltaProgress < 0) deltaProgress = 0;
                            double gapSec = deltaProgress * lapPaceSec;
                            gapStr = string.Format("+{0:F1}s", Math.Abs(gapSec));
                        }
                    }

                    string colorStr = opp.IsPlayer ? "#FF00E5FF" : "#FFFFFFFF";

                    PluginManager.SetPropertyValue("SimRIG." + sidePrefix + ".Class.C" + i + "_Gap", t, gapStr);
                    PluginManager.SetPropertyValue("SimRIG." + sidePrefix + ".Class.C" + i + "_Class", t, opp.CarClass ?? "");
                    PluginManager.SetPropertyValue("SimRIG." + sidePrefix + ".Class.C" + i + "_Color", t, colorStr);
                }
                else
                {
                    PluginManager.SetPropertyValue("SimRIG." + sidePrefix + ".Class.C" + i + "_Pos", t, "P" + (startRank + i - 1));
                    PluginManager.SetPropertyValue("SimRIG." + sidePrefix + ".Class.C" + i + "_Name", t, "---");
                    PluginManager.SetPropertyValue("SimRIG." + sidePrefix + ".Class.C" + i + "_Gap", t, "---");
                    PluginManager.SetPropertyValue("SimRIG." + sidePrefix + ".Class.C" + i + "_Class", t, "");
                    PluginManager.SetPropertyValue("SimRIG." + sidePrefix + ".Class.C" + i + "_Color", t, "#FFFFFFFF");
                }
            }

            // 3. RELATIVE LEADERBOARD (Overall vs Class Toggle based on pageOffset)
            var relativeSource = isClassRelative
                ? opponents.Where(o => string.IsNullOrEmpty(playerClass) || string.Equals(o.CarClass, playerClass, StringComparison.OrdinalIgnoreCase)).ToList()
                : opponents;

            var relativeSorted = relativeSource.OrderByDescending(o => (o.CurrentLap ?? 1) + o.TrackPositionPercent.Value).ToList();
            int pIndex = relativeSorted.FindIndex(o => o.IsPlayer);
            if (pIndex < 0) pIndex = 0;

            for (int r = 1; r <= 7; r++)
            {
                int offset = r - 4; // -3, -2, -1, 0, +1, +2, +3
                int targetIdx = pIndex + offset;

                if (targetIdx >= 0 && targetIdx < relativeSorted.Count)
                {
                    var opp = relativeSorted[targetIdx];
                    double oppProgress = (opp.CurrentLap ?? 1) + opp.TrackPositionPercent.Value;
                    string gapStr = "";
                    if (opp.IsPlayer)
                    {
                        gapStr = "SELF";
                    }
                    else if (oppProgress > playerProgress) // Driver AHEAD of player -> NEGATIVE sign (-)
                    {
                        double deltaProgress = oppProgress - playerProgress;
                        if (deltaProgress < 0) deltaProgress = 0;
                        double gapSec = deltaProgress * lapPaceSec;
                        gapStr = string.Format("-{0:F1}s", Math.Abs(gapSec));
                    }
                    else // Driver BEHIND player -> POSITIVE sign (+)
                    {
                        double deltaProgress = playerProgress - oppProgress;
                        if (deltaProgress < 0) deltaProgress = 0;
                        double gapSec = deltaProgress * lapPaceSec;
                        gapStr = string.Format("+{0:F1}s", Math.Abs(gapSec));
                    }

                    string lastLapStr = opp.LastLapTime.TotalSeconds > 0 ? string.Format("{0:mm\\:ss\\.fff}", opp.LastLapTime) : "--:--.---";
                    int pos = isClassRelative ? (targetIdx + 1) : (opp.Position > 0 ? opp.Position : (targetIdx + 1));
                    string colorStr = opp.IsPlayer ? "#FF00E5FF" : "#FFFFFFFF";

                    PluginManager.SetPropertyValue("SimRIG." + sidePrefix + ".Relative.R" + r + "_Pos", t, "P" + pos);
                    PluginManager.SetPropertyValue("SimRIG." + sidePrefix + ".Relative.R" + r + "_Name", t, opp.Name ?? "---");
                    PluginManager.SetPropertyValue("SimRIG." + sidePrefix + ".Relative.R" + r + "_Gap", t, gapStr);
                    PluginManager.SetPropertyValue("SimRIG." + sidePrefix + ".Relative.R" + r + "_LastLap", t, lastLapStr);
                    PluginManager.SetPropertyValue("SimRIG." + sidePrefix + ".Relative.R" + r + "_Class", t, opp.CarClass ?? "");
                    PluginManager.SetPropertyValue("SimRIG." + sidePrefix + ".Relative.R" + r + "_Color", t, colorStr);
                }
                else
                {
                    PluginManager.SetPropertyValue("SimRIG." + sidePrefix + ".Relative.R" + r + "_Pos", t, "--");
                    PluginManager.SetPropertyValue("SimRIG." + sidePrefix + ".Relative.R" + r + "_Name", t, "---");
                    PluginManager.SetPropertyValue("SimRIG." + sidePrefix + ".Relative.R" + r + "_Gap", t, "---");
                    PluginManager.SetPropertyValue("SimRIG." + sidePrefix + ".Relative.R" + r + "_LastLap", t, "--:--.---");
                    PluginManager.SetPropertyValue("SimRIG." + sidePrefix + ".Relative.R" + r + "_Class", t, "");
                    PluginManager.SetPropertyValue("SimRIG." + sidePrefix + ".Relative.R" + r + "_Color", t, "#FFFFFFFF");
                }
            }
        }

        public System.Windows.Controls.Control GetWPFSettingsControl(PluginManager pluginManager) { return new SettingsControlDemo(this); }
    }
}