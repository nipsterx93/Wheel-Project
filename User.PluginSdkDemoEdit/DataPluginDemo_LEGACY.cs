// -------------------------------------------------------------------------
// FILE VERSION: V0.11.54
// -------------------------------------------------------------------------
using GameReaderCommon;
using SimHub.Plugins;
using System;
using System.IO.Ports;
using System.Threading.Tasks;
using System.Linq;
using SharpDX.DirectInput;
using System.Windows.Media;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Speech.Synthesis;
using System.Media;
using System.Reflection;
using System.IO;

namespace SimRIG
{
    public enum FuelStrategyMode { Manual, Normal, Safe, Aggressive }
    public enum TyreSelectionScope { None, All4, Fronts, Rears, Left, Right, FL, FR, RL, RR }

    [PluginDescription("SimRIG Manager V0.11.54")]
    [PluginAuthor("Gemini & Andreas")]
    [PluginName("SimRIG")]
    public class DataPluginDemo : IPlugin, IDataPlugin, IWPFSettingsV2
    {
        public DataPluginDemoSettings Settings;
        public PluginManager PluginManager { get; set; }

        public static readonly string[] FunctionList = new string[] { "Brake Bias", "Traction Control", "ABS", "Engine Map", "Engine Brake", "Diff Entry", "Diff Mid", "Diff Exit", "MGU-K / Rec", "Aux A", "Aux B", "Aux C" };

        public bool[] RawButtons = new bool[128];
        public int[] RawAxes = new int[8];
        private bool[] _prevEncoderState = new bool[128];
        private CancellationTokenSource[] _pushTokens = new CancellationTokenSource[4];
        private bool[] _longPressExecuted = new bool[4];

        private CancellationTokenSource _msgTokenTL, _msgTokenTR, _msgTokenBL, _msgTokenBR, _msgTokenSource;

        private SerialPort _serialInput = null, _serialLeds = null;
        private string _inputBuffer = "";
        private readonly object _bufferLock = new object();
        private DirectInput directInput;
        private Joystick joystick;
        private bool _deviceAcquired = false;

        public FuelCalculator FuelCalc = new FuelCalculator();
        public PitStrategyManager StrategyManager = new PitStrategyManager();

        private SpeechSynthesizer _speechSynth;
        private SoundPlayer _beepPlayer, _noisePlayer;
        private int _radioSequenceId = 0;
        private Prompt _currentPrompt = null;

        private int _lastSessionLap = -1;
        private bool _wasInPit = false, _autoPitTriggered = false;
        private string _latchedDropZoneStatus = "CLEAR";

        private double _currentTankLevel = 0.0;
        public double RaceStartingFuel = 0.0;
        private bool _raceStartingFuelLatched = false;

        private bool _lastGameRunning = false;
        private int _lastRpmPercent = -1, _lastFlagState = -1;

        private double _fuelStep = 0.1;
        private FuelStrategyMode _fuelStrat = FuelStrategyMode.Manual;
        private TyreSelectionScope _tyreScope = TyreSelectionScope.None;
        private int _pressureOffsetKpa = 0;
        private bool _windshieldActive = true, _fastRepairActive = false;
        private bool _isTargetModeEnabled = false, _isSyncModeEnabled = false;
        private int _targetSelectionIndex = 0;
        private double _lastTrackPos = 0.0;
        private bool _boxNowPlayed = false;

        private string[] _encoderLabels = new string[4] { "N/A", "N/A", "N/A", "N/A" };
        public double PersoSteeringWheelLiveBitePoint => _liveBitePoint;
        private double _liveBitePoint = 50.0;
        private string _steeringWheelMode = "NORMAL", _steeringWheelMessage = "READY", _msgTL = "", _msgTR = "", _msgBL = "", _msgBR = "";

        public SimRigProfile CurrentProfile = new SimRigProfile();
        public Color[] ButtonColors = new Color[12];
        public bool IsGlobalTesting = false;
        public bool IsInputConnected => _serialInput != null && _serialInput.IsOpen;
        public bool IsLedsConnected => _serialLeds != null && _serialLeds.IsOpen;
        public string InputPortName => IsInputConnected ? _serialInput.PortName : "N/A";
        public string LedsPortName => IsLedsConnected ? _serialLeds.PortName : "N/A";

        private const string SH_MODE_PREFIX = "WMODE:", SH_MSG_PREFIX = "WMSG:", SH_VAL_PREFIX = "WVAL:", SH_IDX_PREFIX = "WIDX:", SH_WENC_PREFIX = "WENC:", SH_WPUSH_PREFIX = "WPUSH:";

        public ImageSource PictureIcon => null;
        public string LeftMenuTitle => "SimRIG Manager";

        private string _currentLogFile = null;
        private readonly object _logLock = new object();
        private string _diagnosticLogFile = null;
        private double _lastLoggedRaceLapsRem = -1;
        private int _lastLoggedLap = -1;
        private bool _lastLoggedTraffic = false;

        public List<Opponent> _lastOpponents = null;
        public double _lastSpeedKmh = 0.0;

        public string GetBottomLeftLabel() => _encoderLabels[2];
        public string GetBottomRightLabel() => _encoderLabels[3];
        public string GetPitStratLabel()
        {
            switch (_fuelStrat)
            {
                case FuelStrategyMode.Manual: return "MANUAL";
                case FuelStrategyMode.Normal: return "NORM";
                case FuelStrategyMode.Safe: return "SAFE";
                case FuelStrategyMode.Aggressive: return "AGGR";
                default: return "UNKNOWN";
            }
        }
        public string GetCurrentStrategyModeString()
        {
            if (_isSyncModeEnabled) return "SYNC (MAX)";
            if (_isTargetModeEnabled) return $"TGT: {FuelCalc.FuelPerLapTarget:0.0}";
            switch (_fuelStrat)
            {
                case FuelStrategyMode.Manual: return "MANUAL";
                case FuelStrategyMode.Normal: return "NORM (+0.3)";
                case FuelStrategyMode.Safe: return "SAFE (+1.0)";
                case FuelStrategyMode.Aggressive: return "AGGR (+0.0)";
                default: return "UNKNOWN";
            }
        }
        public string GetPitPressLabel() => $"{(_pressureOffsetKpa >= 0 ? "+" : "")}{_pressureOffsetKpa} kPa";
        public string GetPitFuelLabel() { double val = FuelCalc.GetUserOffset(); return $"{(val > 0 ? "+" : "")}{val.ToString("F1", CultureInfo.InvariantCulture)} L"; }
        public string GetTyreScopeLabel() => _tyreScope.ToString().ToUpper().Replace("ALL4", "ALL 4");
        private string GetTargetModeName(int index) { if (index == 0) return "AHEAD"; if (index == 1) return "BEHIND"; return $"P{index - 1}"; }
        public string GetCurrentTargetModeName() { return GetTargetModeName(_targetSelectionIndex); }

        public string GetEncoderActionLabel(int index)
        {
            if (_steeringWheelMode == "PIT") { if (index == 0) return $"FUEL {GetPitFuelLabel()}"; if (index == 1) return $"TYRES {GetTyreScopeLabel()}"; if (index == 2) return $"MODE {GetPitStratLabel()}"; if (index == 3) return $"PRESS {GetPitPressLabel()}"; }
            else if (_steeringWheelMode == "PIT2") { if (index == 0) return $"TGT {FuelCalc.FuelPerLapTarget:F2}L" + (_isTargetModeEnabled ? " [ON]" : " [OFF]"); if (index == 1) return $"SYNC" + (_isSyncModeEnabled ? " [ON]" : " [OFF]"); if (index == 2) return $"STRAT {GetPitStratLabel()}"; if (index == 3) return $"PRESS {GetPitPressLabel()}"; }
            else if (_steeringWheelMode == "STRAT") { if (index == 0) return $"TGT: {StrategyManager.GetTargetName()}"; if (index == 1) return $"DZ: {StrategyManager.DropZoneStatus}"; }
            if (index == 0) return Settings.TopLeftEncoderMode >= 0 && Settings.TopLeftEncoderMode < FunctionList.Length ? FunctionList[Settings.TopLeftEncoderMode] : "N/A";
            if (index == 1) return Settings.TopRightEncoderMode >= 0 && Settings.TopRightEncoderMode < FunctionList.Length ? FunctionList[Settings.TopRightEncoderMode] : "N/A";
            if (index == 2) return !string.IsNullOrEmpty(_encoderLabels[2]) ? _encoderLabels[2] : "N/A";
            if (index == 3) return !string.IsNullOrEmpty(_encoderLabels[3]) ? _encoderLabels[3] : "N/A";
            return "N/A";
        }

        public void Init(PluginManager pluginManager)
        {
            this.PluginManager = pluginManager;
            Settings = this.ReadCommonSettings<DataPluginDemoSettings>("GeneralSettings", () => new DataPluginDemoSettings());
            FuelCalc = new FuelCalculator();
            StrategyManager = new PitStrategyManager();
            InitVoiceEngine();

            this.AttachDelegate("PersoSteeringWheelMode", () => _steeringWheelMode);
            this.AttachDelegate("PersoSteeringWheelMessage", () => _steeringWheelMessage);
            this.AttachDelegate("PersoSteeringWheelLiveBitePoint", () => _liveBitePoint);
            this.AttachDelegate("Enc_TopLeft_Label", () => _encoderLabels[0]);
            this.AttachDelegate("Enc_TopRight_Label", () => _encoderLabels[1]);
            this.AttachDelegate("Enc_BotLeft_Label", () => _encoderLabels[2]);
            this.AttachDelegate("Enc_BotRight_Label", () => _encoderLabels[3]);

            pluginManager.AddProperty("SimRIG.Input.TopLeftEncoder", this.GetType(), ""); pluginManager.AddProperty("SimRIG.Input.TopRightEncoder", this.GetType(), ""); pluginManager.AddProperty("SimRIG.Input.BottomLeftEncoder", this.GetType(), ""); pluginManager.AddProperty("SimRIG.Input.BottomRightEncoder", this.GetType(), "");
            pluginManager.AddProperty("SimRIG.Fuel.UserOffset", this.GetType(), 0.0); pluginManager.AddProperty("SimRIG.Fuel.Step", this.GetType(), 0.1); pluginManager.AddProperty("SimRIG.Fuel.ActionMessage", this.GetType(), ""); pluginManager.AddProperty("SimRIG.Fuel.FuelToAdd", this.GetType(), 0.0); pluginManager.AddProperty("SimRIG.Fuel.TankLapsRemaining", this.GetType(), 0.0); pluginManager.AddProperty("SimRIG.Fuel.PitRequiredNumber", this.GetType(), 0.0); pluginManager.AddProperty("SimRIG.Fuel.FuelDelta", this.GetType(), 0.0); pluginManager.AddProperty("SimRIG.Fuel.HistoricalPerLap", this.GetType(), 0.0); pluginManager.AddProperty("SimRIG.Fuel.LastLapFuelUsed", this.GetType(), 0.0); pluginManager.AddProperty("SimRIG.Fuel.CurrentTankLevel", this.GetType(), 0.0);
            pluginManager.AddProperty("SimRIG.Fuel.TargetFuel", this.GetType(), 0.0); pluginManager.AddProperty("SimRIG.Fuel.TargetEnabled", this.GetType(), false); pluginManager.AddProperty("SimRIG.Fuel.SyncEnabled", this.GetType(), false);
            pluginManager.AddProperty("SimRIG.Fuel.RaceStartingFuel", this.GetType(), 0.0);

            pluginManager.AddProperty("SimRIG.Session.LeaderRaceTotalLaps", this.GetType(), 0.0); pluginManager.AddProperty("SimRIG.Session.LeaderRaceLapsCompleted", this.GetType(), 0); pluginManager.AddProperty("SimRIG.Session.LeaderRaceLapsRemaining", this.GetType(), 0.0); pluginManager.AddProperty("SimRIG.Session.RaceTotalLaps", this.GetType(), 0.0); pluginManager.AddProperty("SimRIG.Session.RaceLapsCompleted", this.GetType(), 0); pluginManager.AddProperty("SimRIG.Session.RaceLapsRemaining", this.GetType(), 0.0); pluginManager.AddProperty("SimRIG.Session.IsLapped", this.GetType(), false); pluginManager.AddProperty("SimRIG.Session.TimeLeftStr", this.GetType(), "00:00.000"); pluginManager.AddProperty("SimRIG.Session.RaceLifeTimeLeftStr", this.GetType(), "00:00.000"); pluginManager.AddProperty("SimRIG.Session.ClassTopSpeed", this.GetType(), 0.0);
            pluginManager.AddProperty("SimRIG.Session.ClassPaceDropDueToTyres", this.GetType(), 0.0); pluginManager.AddProperty("SimRIG.Session.LeaderPaceDropDueToTyres", this.GetType(), 0.0);

            pluginManager.AddProperty("SimRIG.Strategy.IsPredictionValid", this.GetType(), false);
            pluginManager.AddProperty("SimRIG.Strategy.GlobalColdTyrePenalty", this.GetType(), 2.5);

            pluginManager.AddProperty("SimRIG.Strategy.LeaderPaceStr", this.GetType(), "00:00.000"); pluginManager.AddProperty("SimRIG.Strategy.MyPaceStr", this.GetType(), "00:00.000"); pluginManager.AddProperty("SimRIG.Strategy.MyPace", this.GetType(), 0.0); pluginManager.AddProperty("SimRIG.Strategy.LeaderPace", this.GetType(), 0.0); pluginManager.AddProperty("SimRIG.Strategy.Mode", this.GetType(), "MANUAL"); pluginManager.AddProperty("SimRIG.Strategy.FuelCalculatorEnabled", this.GetType(), Settings.EnableFuelCalculatorSystem); pluginManager.AddProperty("SimRIG.Strategy.AutoPitEnabled", this.GetType(), Settings.EnableAutoPitStrategy);
            pluginManager.AddProperty("SimRIG.Tyres.SelectionScope", this.GetType(), "NONE"); pluginManager.AddProperty("SimRIG.Tyres.ActionMessage", this.GetType(), ""); pluginManager.AddProperty("SimRIG.Pressure.OffsetKpa", this.GetType(), 0); pluginManager.AddProperty("SimRIG.Pressure.ActionMessage", this.GetType(), "");
            pluginManager.AddProperty("SimRIG.Services.WindshieldActive", this.GetType(), true); pluginManager.AddProperty("SimRIG.Services.FastRepairActive", this.GetType(), false); pluginManager.AddProperty("SimRIG.Services.ActionMessage", this.GetType(), "");

            pluginManager.AddProperty("SimRIG.Driver.TrueRawPace", this.GetType(), 0.0);
            pluginManager.AddProperty("SimRIG.Driver.TrueBaselinePace", this.GetType(), 0.0);
            pluginManager.AddProperty("SimRIG.Driver.TrueBaselinePaceStr", this.GetType(), "00:00.000");
            pluginManager.AddProperty("SimRIG.Driver.TrueCurrentPace", this.GetType(), 0.0);
            pluginManager.AddProperty("SimRIG.Driver.TrueCurrentPaceStr", this.GetType(), "00:00.000");
            pluginManager.AddProperty("SimRIG.Driver.CurrentMicroSector", this.GetType(), 0);
            pluginManager.AddProperty("SimRIG.Driver.LastMicroSectorSpeed", this.GetType(), 0.0);
            pluginManager.AddProperty("SimRIG.Driver.PendingReset", this.GetType(), false);
            pluginManager.AddProperty("SimRIG.Driver.IsInTraffic", this.GetType(), false);
            pluginManager.AddProperty("SimRIG.Driver.RelativeDegradationToClass", this.GetType(), 0.0);
            pluginManager.AddProperty("SimRIG.Driver.PaceDropDueToTyres", this.GetType(), 0.0);

            pluginManager.AddProperty("SimRIG.Target.Mode", this.GetType(), "AHEAD"); pluginManager.AddProperty("SimRIG.Target.Name", this.GetType(), "--"); pluginManager.AddProperty("SimRIG.Target.Position", this.GetType(), 0); pluginManager.AddProperty("SimRIG.Target.GapSeconds", this.GetType(), 0.0); pluginManager.AddProperty("SimRIG.Target.GapString", this.GetType(), ""); pluginManager.AddProperty("SimRIG.Target.RelativePace", this.GetType(), 0.0); pluginManager.AddProperty("SimRIG.Target.TopSpeed", this.GetType(), 0.0); pluginManager.AddProperty("SimRIG.Target.CurrentSpeed", this.GetType(), 0.0); pluginManager.AddProperty("SimRIG.Target.SpeedDrop", this.GetType(), 0.0); pluginManager.AddProperty("SimRIG.Target.Diagnosis", this.GetType(), "ANALYZING"); pluginManager.AddProperty("SimRIG.Target.PaceDeficit", this.GetType(), 0.0); pluginManager.AddProperty("SimRIG.Target.RelativeDegradation", this.GetType(), 0.0); pluginManager.AddProperty("SimRIG.Target.UndercutViable", this.GetType(), false); pluginManager.AddProperty("SimRIG.Target.PaceDropDueToTyres", this.GetType(), 0.0);
            pluginManager.AddProperty("SimRIG.Target.ProjectedMergeGap", this.GetType(), 0.0);

            pluginManager.AddProperty("SimRIG.Target.CurrentTank", this.GetType(), 0.0);
            pluginManager.AddProperty("SimRIG.Target.TankLapsRemaining", this.GetType(), 0.0);
            pluginManager.AddProperty("SimRIG.Target.ReactionDeltaLaps", this.GetType(), 0);
            pluginManager.AddProperty("SimRIG.Target.NetPaceAdvantageTotal", this.GetType(), 0.0);
            pluginManager.AddProperty("SimRIG.Target.OvercutViable", this.GetType(), false);
            pluginManager.AddProperty("SimRIG.Target.MergeGapWorstCase", this.GetType(), 0.0);

            pluginManager.AddProperty("SimRIG.Target.TrueRawPace", this.GetType(), 0.0);
            pluginManager.AddProperty("SimRIG.Target.TrueBaselinePace", this.GetType(), 0.0);
            pluginManager.AddProperty("SimRIG.Target.TrueCurrentPace", this.GetType(), 0.0);

            pluginManager.AddProperty("SimRIG.Target.PlayerTyreScope", this.GetType(), "NONE");
            pluginManager.AddProperty("SimRIG.Target.PlayerStatLoss", this.GetType(), 0.0);
            pluginManager.AddProperty("SimRIG.Target.PlayerOutLoss", this.GetType(), 0.0);
            pluginManager.AddProperty("SimRIG.Target.TargetStatLoss", this.GetType(), 0.0);
            pluginManager.AddProperty("SimRIG.Target.TargetOutLoss", this.GetType(), 0.0);
            pluginManager.AddProperty("SimRIG.Target.TargetTireInferred", this.GetType(), false);
            pluginManager.AddProperty("SimRIG.Target.TargetDropZoneStatus", this.GetType(), "CLEAR");

            pluginManager.AddProperty("SimRIG.Pit.SelectedTireTime", this.GetType(), 0.0); pluginManager.AddProperty("SimRIG.Pit.FuelCapacityInTireTime", this.GetType(), 0.0); pluginManager.AddProperty("SimRIG.Pit.StationaryTimeLoss", this.GetType(), 0.0); pluginManager.AddProperty("SimRIG.Pit.TransitTime", this.GetType(), 0.0); pluginManager.AddProperty("SimRIG.Pit.TransitDriveThrough", this.GetType(), 0.0); pluginManager.AddProperty("SimRIG.Pit.DropZoneLossSec", this.GetType(), 0.0); pluginManager.AddProperty("SimRIG.Pit.DropZoneStatus", this.GetType(), "ANALYZING"); pluginManager.AddProperty("SimRIG.Pit.MeasuredFuelFillRate", this.GetType(), 0.0); pluginManager.AddProperty("SimRIG.Pit.CalibrationStatus", this.GetType(), "ANALYZING");
            pluginManager.AddProperty("SimRIG.Debug.LastOpponentTransit", this.GetType(), 0.0); pluginManager.AddProperty("SimRIG.Debug.LastOpponentStationary", this.GetType(), 0.0); pluginManager.AddProperty("SimRIG.Debug.LastOpponentTotalTime", this.GetType(), 0.0); pluginManager.AddProperty("SimRIG.Debug.LatchedDropZone", this.GetType(), "CLEAR");

            pluginManager.AddProperty("SimRIG.CleanSector.MyState", this.GetType(), "NORMAL");
            pluginManager.AddProperty("SimRIG.CleanSector.MyTime", this.GetType(), 0.0);
            pluginManager.AddProperty("SimRIG.CleanSector.LeaderState", this.GetType(), "NORMAL");
            pluginManager.AddProperty("SimRIG.CleanSector.LeaderTime", this.GetType(), 0.0);

            pluginManager.AddProperty("SimRIG.Pit.PlayerPitCount", this.GetType(), 0);
            pluginManager.AddProperty("SimRIG.Pit.TargetPitCount", this.GetType(), 0);
            pluginManager.AddProperty("SimRIG.Pit.LeaderPitCount", this.GetType(), 0);

            ProfileManager.Init();
            for (int i = 0; i < 12; i++) ButtonColors[i] = Colors.White;
            if (!string.IsNullOrEmpty(Settings.LastProfileUsed)) { SimRigProfile lastProfile = ProfileManager.LoadProfile(Settings.LastProfileUsed); if (lastProfile != null) ApplyProfile(lastProfile); else ApplyProfile(new SimRigProfile()); } else ApplyProfile(new SimRigProfile());

            InitializeDirectInput();
            Task.Run(() => RunDiscovery());
            UpdateAllProperties();
        }

        public void ReloadVoiceEngine() { InitVoiceEngine(); }

        private void InitVoiceEngine() { try { if (_speechSynth != null) { _speechSynth.SpeakCompleted -= SpeechSynth_SpeakCompleted; _speechSynth.Dispose(); } _speechSynth = new SpeechSynthesizer(); _speechSynth.Rate = 2; _speechSynth.SpeakCompleted += SpeechSynth_SpeakCompleted; string accentCode = "en"; if (Settings.EngineerNationality == "IT") accentCode = "it"; else if (Settings.EngineerNationality == "DE") accentCode = "de"; else if (Settings.EngineerNationality == "ES") accentCode = "es"; else if (Settings.EngineerNationality == "FR") accentCode = "fr"; var installedVoices = _speechSynth.GetInstalledVoices(); var candidateVoices = installedVoices.Where(v => v.VoiceInfo.Culture.Name.StartsWith(accentCode, StringComparison.OrdinalIgnoreCase)).ToList(); if (!candidateVoices.Any()) { accentCode = "en"; candidateVoices = installedVoices.Where(v => v.VoiceInfo.Culture.Name.StartsWith(accentCode, StringComparison.OrdinalIgnoreCase)).ToList(); } InstalledVoice bestVoice = null; int bestScore = -999; foreach (var voice in candidateVoices) { int score = 0; string name = voice.VoiceInfo.Name.ToLower(); if (name.Contains("natural") || name.Contains("online") || name.Contains("premium") || name.Contains("cereproc") || name.Contains("ivona")) score += 100; else if (!name.Contains("desktop")) score += 50; if (name.Contains("desktop") || name.Contains("david") || name.Contains("zira")) score -= 50; if (score > bestScore) { bestScore = score; bestVoice = voice; } } if (bestVoice != null) _speechSynth.SelectVoice(bestVoice.VoiceInfo.Name); else _speechSynth.SelectVoiceByHints(VoiceGender.NotSet, VoiceAge.NotSet, 0, new CultureInfo("en-US")); LoadEmbeddedAudio(); } catch (Exception ex) { SimHub.Logging.Current.Error($"[SimRIG] Errore avvio TTS: {ex.Message}"); } }
        private void LoadEmbeddedAudio() { try { if (_beepPlayer != null || _noisePlayer != null) return; var assembly = Assembly.GetExecutingAssembly(); string[] resources = assembly.GetManifestResourceNames(); string beepName = resources.FirstOrDefault(r => r.EndsWith("beep.wav", StringComparison.OrdinalIgnoreCase)); string noiseName = resources.FirstOrDefault(r => r.EndsWith("noise.wav", StringComparison.OrdinalIgnoreCase)); if (beepName != null) { _beepPlayer = new SoundPlayer(assembly.GetManifestResourceStream(beepName)); _beepPlayer.LoadAsync(); } if (noiseName != null) { _noisePlayer = new SoundPlayer(assembly.GetManifestResourceStream(noiseName)); _noisePlayer.LoadAsync(); } } catch { } }
        private void SpeechSynth_SpeakCompleted(object sender, SpeakCompletedEventArgs e) { if (e.Prompt != null && e.Prompt == _currentPrompt) { Task.Run(() => { try { if (!e.Cancelled) { Thread.Sleep(1000); if (e.Prompt == _currentPrompt) { if (_beepPlayer != null) _beepPlayer.PlaySync(); else if (_noisePlayer != null) _noisePlayer.Stop(); } } } catch { } }); } }
        private void TriggerRadioVoice(string phraseKey, params object[] args) { if (!Settings.EnableVoiceEngineer) return; string textToSpeak = PitWallLanguage.GetPhrase(Settings.VoiceLanguage, phraseKey, args); if (string.IsNullOrEmpty(textToSpeak)) return; int currentSeqId = Interlocked.Increment(ref _radioSequenceId); Task.Run(() => { try { _speechSynth.SpeakAsyncCancelAll(); Thread.Sleep(100); if (_radioSequenceId != currentSeqId) return; if (_beepPlayer != null) _beepPlayer.PlaySync(); if (_radioSequenceId != currentSeqId) return; if (_noisePlayer != null) _noisePlayer.PlayLooping(); if (_radioSequenceId != currentSeqId) return; Thread.Sleep(1000); if (_radioSequenceId != currentSeqId) return; _currentPrompt = _speechSynth.SpeakAsync(textToSpeak); } catch { } }); }

        public void SendSystemStateToFirmware() { if (IsInputConnected) { try { string cmd = $"SYS:PIT:{(Settings.EnableFuelCalculatorSystem ? 1 : 0)}"; _serialInput.WriteLine(cmd); } catch { } } }

        private void ProcessPendingLogs()
        {
            if (StrategyManager.PendingLogs.Count == 0) return;

            List<LogEntry> logsToProcess = new List<LogEntry>(StrategyManager.PendingLogs);
            StrategyManager.PendingLogs.Clear();

            Task.Run(() => {
                try
                {
                    if (_currentLogFile == null)
                    {
                        string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs", "SimRIG_Logs");
                        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                        _currentLogFile = Path.Combine(dir, $"Session_{DateTime.Now:yyyyMMdd_HHmm}.txt");
                        string header = "SessionTime;Type;Driver;Lap;RawTime;NormTime;Baseline;PaceDropDueToTyres;FuelData;LapsSincePit;TrackTemp;BestFreshTime;WornTime;TiresChanged";
                        lock (_logLock) { File.AppendAllText(_currentLogFile, header + Environment.NewLine); }
                    }

                    List<string> lines = new List<string>();
                    foreach (var l in logsToProcess)
                    {
                        string timeStr = TimeSpan.FromSeconds(l.SessionTime).ToString(@"hh\:mm\:ss\.fff");
                        lines.Add($"{timeStr};{l.Type};{l.DriverName};{l.LapNumber};{l.RawLapTime.ToString(CultureInfo.InvariantCulture)};{l.NormalizedLapTime.ToString(CultureInfo.InvariantCulture)};{l.Baseline.ToString(CultureInfo.InvariantCulture)};{l.PaceDropDueToTyres.ToString(CultureInfo.InvariantCulture)};{l.FuelData.ToString(CultureInfo.InvariantCulture)};{l.LapsSincePit};{l.TrackTemp.ToString(CultureInfo.InvariantCulture)};{l.BestFreshTime.ToString(CultureInfo.InvariantCulture)};{l.WornTime.ToString(CultureInfo.InvariantCulture)};{l.TiresChanged}");
                    }
                    lock (_logLock) { File.AppendAllLines(_currentLogFile, lines); }
                }
                catch { }
            });
        }

        private void LogDiagnosticData(GameData data, string triggerReason, int sessionState, double raceTotalLaps, int raceLapsCompleted, double raceLapsRemaining, double myTankLapsRemaining, double avgTireTemp, double projectedFuelToAdd, double projectedEndOfLapFuelTank, bool isInPitLane)
        {
            Task.Run(() => {
                try
                {
                    if (_diagnosticLogFile == null)
                    {
                        string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs", "SimRIG_Logs");
                        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                        _diagnosticLogFile = Path.Combine(dir, $"SimRIG_DriverPaceTest_{DateTime.Now:yyyyMMdd_HHmm}.csv");

                        string header = "Timestamp;Trigger;SessionState;SessionTime;Lap;RaceTotalLaps;RaceLapsCompleted;RaceLapsRemaining;PlayerTank;PlayerTankLapsRem;LastLapTimeRaw;DriverRawPace;TrueBaselinePace;TrueCurrentPace;TargetName;TargetTank;TargetTankLapsRem;TargetRawPace;TargetBaseline;TargetCurrentPace;PaceDeficit;RelDegradation;MyPaceDrop;TgtPaceDrop;PendingReset;SelTireTime;FuelCapTireTime;StatTimeLoss;TransitTime;TransitDT;MeasFuelRate;PlayerDropZone;TargetDropZone;DropZoneLossSec;PlayerPits;TargetPits;LeaderPits;ReactionLaps;NetPaceAdvTot;MergeGapWorst;UndercutViable;OvercutViable;RaceStartingFuel;TgtLapsRemAtExit;GlobalColdTyrePen;PlayerBestFresh;PlayerWorn;TargetTireInferred;AvgTireTemp;ProjectedFuelToAdd;TargetActualStatTime;ProjectedEndOfLapFuelTank;IsInPitLane";
                        lock (_logLock) { File.AppendAllText(_diagnosticLogFile, header + Environment.NewLine); }
                    }

                    string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                    double sessionClock = 0.0;
                    var rawSessTime = PluginManager.GetPropertyValue("DataCorePlugin.GameRawData.Telemetry.SessionTime");
                    if (rawSessTime != null) sessionClock = Convert.ToDouble(rawSessTime);
                    string sessionTimeStr = TimeSpan.FromSeconds(sessionClock).ToString(@"hh\:mm\:ss\.fff");

                    double lastRawLap = data.NewData.LastLapTime.TotalSeconds;

                    var tState = StrategyManager.LastTargetState ?? new TargetState();
                    double targetActualStat = StrategyManager._pitRadar.ContainsKey(tState.Name) ? StrategyManager._pitRadar[tState.Name].StationaryTimeSec : 0.0;

                    string line = $"{timestamp};{triggerReason};{sessionState};{sessionTimeStr};{data.NewData.CurrentLap};{raceTotalLaps:F1};{raceLapsCompleted};{raceLapsRemaining:F2};{data.NewData.Fuel:F2};{myTankLapsRemaining:F2};{lastRawLap:F3};{StrategyManager.DriverTrueRawPace:F3};{StrategyManager.TrueBaselinePace:F3};{StrategyManager.TrueCurrentPace:F3};{tState.Name};{tState.CurrentTank:F2};{tState.TankLapsRemaining:F2};{tState.TargetTrueRawPace:F3};{tState.TargetTrueBaselinePace:F3};{tState.TargetTrueCurrentPace:F3};{tState.PaceDeficit:F2};{tState.RelativeDegradation:F2};{StrategyManager.DriverPaceDropDueToTyres:F2};{tState.TargetPaceDropDueToTyres:F2};{StrategyManager.MyPendingStateReset};{StrategyManager.SelectedTireTime:F2};{StrategyManager.FuelCapacityInTireTime:F2};{StrategyManager.PitStationaryTimeLoss:F2};{StrategyManager.PitTransitTime:F2};{StrategyManager.PitTransitDriveThrough:F2};{StrategyManager.MeasuredFuelFillRate:F2};{StrategyManager.DropZoneStatus};{tState.TargetDropZoneStatus};{StrategyManager.DropZoneEstimatedLoss:F2};{StrategyManager.PlayerPitCount};{tState.PitCount};{StrategyManager.LeaderPitCount};{tState.ReactionDeltaLaps};{tState.NetPaceAdvantageTotal:F2};{tState.MergeGapWorstCase:F2};{tState.UndercutViable};{tState.OvercutViable};{RaceStartingFuel:F2};{tState.LapsRemainingAtExit:F2};{StrategyManager.GlobalColdTyrePenalty:F2};{StrategyManager.PlayerCleanSector.BestFreshNormalTime:F3};{StrategyManager.PlayerCleanSector.WornNormalTime:F3};{tState.TargetTireInferred};{avgTireTemp:F1};{projectedFuelToAdd:F1};{targetActualStat:F2};{projectedEndOfLapFuelTank:F2};{isInPitLane}";

                    lock (_logLock) { File.AppendAllText(_diagnosticLogFile, line + Environment.NewLine); }
                }
                catch { }
            });
        }

        public double CalculateProjectedFuelToAdd(double currentLapPos)
        {
            double pitEntry = StrategyManager.GetPitEntryPct();
            double distanceToPit = pitEntry - currentLapPos;
            if (distanceToPit < 0) distanceToPit = 0;

            double consumption = FuelCalc.GetEffectiveConsumption();
            double projectedTank = Math.Max(0, _currentTankLevel - (distanceToPit * consumption));

            double projectedRaceLapsRem = Math.Max(0, FuelCalc.RaceLapsRemaining - distanceToPit);

            double baseNeeded = projectedRaceLapsRem * consumption;
            double rawNeeded = Math.Max(0, baseNeeded - projectedTank);

            double fuelToSend = 0.0;
            if (_fuelStrat == FuelStrategyMode.Manual)
            {
                fuelToSend = FuelCalc.GetUserOffset();
            }
            else
            {
                double margin = 0.0;
                if (_fuelStrat == FuelStrategyMode.Safe) margin = consumption;
                else if (_fuelStrat == FuelStrategyMode.Normal) margin = consumption * 0.3;

                fuelToSend = rawNeeded + margin + FuelCalc.GetUserOffset();

                if (_isSyncModeEnabled)
                {
                    double fuelTires = StrategyManager.FuelCapacityInTireTime;
                    double antiBallastLimit = fuelToSend + (consumption * 2.0);
                    if (fuelTires > antiBallastLimit) fuelToSend = antiBallastLimit;
                    else fuelToSend = fuelTires;
                }
            }
            if (fuelToSend < 0) fuelToSend = 0;

            double maxTank = PluginManager.GetPropertyValue("DataCorePlugin.GameData.NewData.MaxFuel") != null ? Convert.ToDouble(PluginManager.GetPropertyValue("DataCorePlugin.GameData.NewData.MaxFuel")) : 100.0;
            return Math.Round(Math.Min(maxTank, fuelToSend), 1);
        }

        public double CalculateFinalFuelToAdd()
        {
            double fuelToSend = 0.0;
            if (_fuelStrat == FuelStrategyMode.Manual)
            {
                fuelToSend = FuelCalc.GetUserOffset();
            }
            else
            {
                double baseNeeded = FuelCalc.FuelToAdd;
                double margin = 0.0;

                if (_fuelStrat == FuelStrategyMode.Safe)
                {
                    margin = FuelCalc.GetEffectiveConsumption();
                }
                else if (_fuelStrat == FuelStrategyMode.Normal)
                {
                    margin = FuelCalc.GetEffectiveConsumption() * 0.3;
                }

                fuelToSend = baseNeeded + margin + FuelCalc.GetUserOffset();

                if (_isSyncModeEnabled)
                {
                    double fuelTires = StrategyManager.FuelCapacityInTireTime;
                    double currentConsumption = FuelCalc.GetEffectiveConsumption();
                    double antiBallastLimit = fuelToSend + (currentConsumption * 2.0);
                    if (fuelTires > antiBallastLimit)
                    {
                        fuelToSend = antiBallastLimit;
                    }
                    else
                    {
                        fuelToSend = fuelTires;
                    }
                }
            }
            if (fuelToSend < 0) fuelToSend = 0;
            return Math.Round(fuelToSend, 1);
        }

        public void UpdateAllProperties()
        {
            PluginManager.SetPropertyValue("SimRIG.Input.TopLeftEncoder", this.GetType(), GetEncoderActionLabel(0));
            PluginManager.SetPropertyValue("SimRIG.Input.TopRightEncoder", this.GetType(), GetEncoderActionLabel(1));
            PluginManager.SetPropertyValue("SimRIG.Input.BottomLeftEncoder", this.GetType(), GetEncoderActionLabel(2));
            PluginManager.SetPropertyValue("SimRIG.Input.BottomRightEncoder", this.GetType(), GetEncoderActionLabel(3));

            PluginManager.SetPropertyValue("SimRIG.Fuel.UserOffset", this.GetType(), FuelCalc.GetUserOffset());
            PluginManager.SetPropertyValue("SimRIG.Fuel.Step", this.GetType(), _fuelStep);
            PluginManager.SetPropertyValue("SimRIG.Fuel.ActionMessage", this.GetType(), _msgTL);
            PluginManager.SetPropertyValue("SimRIG.Fuel.FuelToAdd", this.GetType(), CalculateFinalFuelToAdd());
            PluginManager.SetPropertyValue("SimRIG.Fuel.PitRequiredNumber", this.GetType(), FuelCalc.PitRequiredNumber);
            PluginManager.SetPropertyValue("SimRIG.Fuel.FuelDelta", this.GetType(), Math.Round(FuelCalc.FuelDelta, 2));
            PluginManager.SetPropertyValue("SimRIG.Fuel.TankLapsRemaining", this.GetType(), Math.Round(FuelCalc.TankLapsRemaining, 2));
            PluginManager.SetPropertyValue("SimRIG.Fuel.CurrentTankLevel", this.GetType(), Math.Round(_currentTankLevel, 2));
            PluginManager.SetPropertyValue("SimRIG.Fuel.TargetFuel", this.GetType(), FuelCalc.FuelPerLapTarget);
            PluginManager.SetPropertyValue("SimRIG.Fuel.TargetEnabled", this.GetType(), _isTargetModeEnabled);
            PluginManager.SetPropertyValue("SimRIG.Fuel.SyncEnabled", this.GetType(), _isSyncModeEnabled);
            PluginManager.SetPropertyValue("SimRIG.Fuel.RaceStartingFuel", this.GetType(), Math.Round(RaceStartingFuel, 2));

            PluginManager.SetPropertyValue("SimRIG.Tyres.SelectionScope", this.GetType(), _tyreScope.ToString().ToUpper().Replace("ALL4", "ALL 4"));
            PluginManager.SetPropertyValue("SimRIG.Tyres.ActionMessage", this.GetType(), _msgTR);
            PluginManager.SetPropertyValue("SimRIG.Pressure.OffsetKpa", this.GetType(), _pressureOffsetKpa);
            PluginManager.SetPropertyValue("SimRIG.Pressure.ActionMessage", this.GetType(), _msgBR);

            PluginManager.SetPropertyValue("SimRIG.Strategy.Mode", this.GetType(), GetCurrentStrategyModeString());
            PluginManager.SetPropertyValue("SimRIG.Strategy.FuelCalculatorEnabled", this.GetType(), Settings.EnableFuelCalculatorSystem);
            bool effectiveAutoPit = Settings.EnableAutoPitStrategy && Settings.EnableFuelCalculatorSystem;
            PluginManager.SetPropertyValue("SimRIG.Strategy.AutoPitEnabled", this.GetType(), effectiveAutoPit);

            PluginManager.SetPropertyValue("SimRIG.Strategy.IsPredictionValid", this.GetType(), FuelCalc.IsPredictionValid);
            PluginManager.SetPropertyValue("SimRIG.Strategy.GlobalColdTyrePenalty", this.GetType(), Math.Round(StrategyManager.GlobalColdTyrePenalty, 2));

            PluginManager.SetPropertyValue("SimRIG.Services.WindshieldActive", this.GetType(), _windshieldActive);
            PluginManager.SetPropertyValue("SimRIG.Services.FastRepairActive", this.GetType(), _fastRepairActive);
            PluginManager.SetPropertyValue("SimRIG.Services.ActionMessage", this.GetType(), _msgBL);

            PluginManager.SetPropertyValue("SimRIG.Session.LeaderRaceTotalLaps", this.GetType(), FuelCalc.LeaderRaceTotalLaps);
            PluginManager.SetPropertyValue("SimRIG.Session.LeaderRaceLapsCompleted", this.GetType(), FuelCalc.LeaderRaceLapsCompleted);
            PluginManager.SetPropertyValue("SimRIG.Session.LeaderRaceLapsRemaining", this.GetType(), FuelCalc.LeaderRaceLapsRemaining);
            PluginManager.SetPropertyValue("SimRIG.Session.RaceTotalLaps", this.GetType(), FuelCalc.RaceTotalLaps);
            PluginManager.SetPropertyValue("SimRIG.Session.RaceLapsCompleted", this.GetType(), FuelCalc.RaceLapsCompleted);
            PluginManager.SetPropertyValue("SimRIG.Session.RaceLapsRemaining", this.GetType(), FuelCalc.RaceLapsRemaining);
            PluginManager.SetPropertyValue("SimRIG.Session.IsLapped", this.GetType(), FuelCalc.IsLapped);
            PluginManager.SetPropertyValue("SimRIG.Session.TimeLeftStr", this.GetType(), FuelCalculator.FormatTime(FuelCalc.SessionTimeLeftSec));
            PluginManager.SetPropertyValue("SimRIG.Session.RaceLifeTimeLeftStr", this.GetType(), FuelCalculator.FormatTime(FuelCalc.RaceLifeTimeLeftSec));
            PluginManager.SetPropertyValue("SimRIG.Session.ClassTopSpeed", this.GetType(), Math.Round(StrategyManager.ClassTopSpeed, 1));

            PluginManager.SetPropertyValue("SimRIG.Session.ClassPaceDropDueToTyres", this.GetType(), Math.Round(StrategyManager.ClassPaceDropDueToTyres, 2));
            PluginManager.SetPropertyValue("SimRIG.Session.LeaderPaceDropDueToTyres", this.GetType(), Math.Round(StrategyManager.LeaderPaceDropDueToTyres, 2));

            PluginManager.SetPropertyValue("SimRIG.Strategy.LeaderPaceStr", this.GetType(), FuelCalculator.FormatTime(FuelCalc.LeaderEstimatedPace));
            PluginManager.SetPropertyValue("SimRIG.Strategy.MyPaceStr", this.GetType(), FuelCalculator.FormatTime(StrategyManager.TrueCurrentPace));
            PluginManager.SetPropertyValue("SimRIG.Fuel.HistoricalPerLap", this.GetType(), Math.Round(FuelCalc.AverageFuelPerLap, 2));
            PluginManager.SetPropertyValue("SimRIG.Fuel.LastLapFuelUsed", this.GetType(), Math.Round(FuelCalc.LastLapFuelUsed, 2));
            PluginManager.SetPropertyValue("SimRIG.Strategy.MyPace", this.GetType(), Math.Round(StrategyManager.TrueCurrentPace, 3));
            PluginManager.SetPropertyValue("SimRIG.Strategy.LeaderPace", this.GetType(), Math.Round(FuelCalc.LeaderEstimatedPace, 3));

            PluginManager.SetPropertyValue("SimRIG.Driver.TrueRawPace", this.GetType(), Math.Round(StrategyManager.DriverTrueRawPace, 3));
            PluginManager.SetPropertyValue("SimRIG.Driver.TrueBaselinePace", this.GetType(), Math.Round(StrategyManager.TrueBaselinePace, 3));
            PluginManager.SetPropertyValue("SimRIG.Driver.TrueBaselinePaceStr", this.GetType(), FuelCalculator.FormatTime(StrategyManager.TrueBaselinePace));
            PluginManager.SetPropertyValue("SimRIG.Driver.TrueCurrentPace", this.GetType(), Math.Round(StrategyManager.TrueCurrentPace, 3));
            PluginManager.SetPropertyValue("SimRIG.Driver.TrueCurrentPaceStr", this.GetType(), FuelCalculator.FormatTime(StrategyManager.TrueCurrentPace));
            PluginManager.SetPropertyValue("SimRIG.Driver.CurrentMicroSector", this.GetType(), StrategyManager.MyCurrentSector);
            PluginManager.SetPropertyValue("SimRIG.Driver.LastMicroSectorSpeed", this.GetType(), Math.Round(StrategyManager.MyLastMicroSectorSpeed, 1));
            PluginManager.SetPropertyValue("SimRIG.Driver.PendingReset", this.GetType(), StrategyManager.MyPendingStateReset);
            PluginManager.SetPropertyValue("SimRIG.Driver.IsInTraffic", this.GetType(), StrategyManager.MyIsInTraffic);
            PluginManager.SetPropertyValue("SimRIG.Driver.RelativeDegradationToClass", this.GetType(), Math.Round(StrategyManager.RelativeDegradationToClass, 2));
            PluginManager.SetPropertyValue("SimRIG.Driver.PaceDropDueToTyres", this.GetType(), Math.Round(StrategyManager.DriverPaceDropDueToTyres, 2));

            string currentTargetMode = GetTargetModeName(_targetSelectionIndex);
            double trackLen = PluginManager.GetPropertyValue("DataCorePlugin.GameData.NewData.TrackLength") != null ? Convert.ToDouble(PluginManager.GetPropertyValue("DataCorePlugin.GameData.NewData.TrackLength")) : 5000.0;
            double myPos = PluginManager.GetPropertyValue("DataCorePlugin.GameData.NewData.TrackPositionPercent") != null ? Convert.ToDouble(PluginManager.GetPropertyValue("DataCorePlugin.GameData.NewData.TrackPositionPercent")) : 0.0;
            double maxFuel = PluginManager.GetPropertyValue("DataCorePlugin.GameData.NewData.MaxFuel") != null ? Convert.ToDouble(PluginManager.GetPropertyValue("DataCorePlugin.GameData.NewData.MaxFuel")) : 100.0;
            int myCurrentLap = PluginManager.GetPropertyValue("DataCorePlugin.GameData.NewData.CurrentLap") != null ? Convert.ToInt32(PluginManager.GetPropertyValue("DataCorePlugin.GameData.NewData.CurrentLap")) : 1;

            double projectedFuelToAdd = CalculateProjectedFuelToAdd(myPos);

            var targetState = StrategyManager.GetTargetState(currentTargetMode, StrategyManager.TrueCurrentPace, myPos, trackLen, projectedFuelToAdd, maxFuel, FuelCalc.AverageFuelPerLap, FuelCalc.RaceTotalLaps, FuelCalc.RaceLapsRemaining, FuelCalc.TankLapsRemaining, RaceStartingFuel, myCurrentLap, _lastSpeedKmh, _lastOpponents);

            PluginManager.SetPropertyValue("SimRIG.Target.Mode", this.GetType(), targetState.ModeLabel);
            PluginManager.SetPropertyValue("SimRIG.Target.Name", this.GetType(), targetState.Name);
            PluginManager.SetPropertyValue("SimRIG.Target.Position", this.GetType(), targetState.ClassPosition);
            PluginManager.SetPropertyValue("SimRIG.Target.GapSeconds", this.GetType(), Math.Round(targetState.GapSeconds, 1));
            PluginManager.SetPropertyValue("SimRIG.Target.GapString", this.GetType(), targetState.GapString);
            PluginManager.SetPropertyValue("SimRIG.Target.RelativePace", this.GetType(), Math.Round(targetState.RelativePace, 3));
            PluginManager.SetPropertyValue("SimRIG.Target.TopSpeed", this.GetType(), Math.Round(targetState.TargetTopSpeed, 1));
            PluginManager.SetPropertyValue("SimRIG.Target.CurrentSpeed", this.GetType(), Math.Round(targetState.TargetCurrentSpeed, 1));
            PluginManager.SetPropertyValue("SimRIG.Target.SpeedDrop", this.GetType(), Math.Round(targetState.TargetSpeedDrop, 1));
            PluginManager.SetPropertyValue("SimRIG.Target.Diagnosis", this.GetType(), targetState.Diagnosis);
            PluginManager.SetPropertyValue("SimRIG.Target.PaceDeficit", this.GetType(), Math.Round(targetState.PaceDeficit, 2));
            PluginManager.SetPropertyValue("SimRIG.Target.RelativeDegradation", this.GetType(), Math.Round(targetState.RelativeDegradation, 2));
            PluginManager.SetPropertyValue("SimRIG.Target.UndercutViable", this.GetType(), targetState.UndercutViable);
            PluginManager.SetPropertyValue("SimRIG.Target.PaceDropDueToTyres", this.GetType(), Math.Round(targetState.TargetPaceDropDueToTyres, 2));
            PluginManager.SetPropertyValue("SimRIG.Target.ProjectedMergeGap", this.GetType(), Math.Round(targetState.ProjectedMergeGap, 2));

            PluginManager.SetPropertyValue("SimRIG.Target.CurrentTank", this.GetType(), Math.Round(targetState.CurrentTank, 2));
            PluginManager.SetPropertyValue("SimRIG.Target.TankLapsRemaining", this.GetType(), Math.Round(targetState.TankLapsRemaining, 1));
            PluginManager.SetPropertyValue("SimRIG.Target.ReactionDeltaLaps", this.GetType(), targetState.ReactionDeltaLaps);
            PluginManager.SetPropertyValue("SimRIG.Target.NetPaceAdvantageTotal", this.GetType(), Math.Round(targetState.NetPaceAdvantageTotal, 2));
            PluginManager.SetPropertyValue("SimRIG.Target.OvercutViable", this.GetType(), targetState.OvercutViable);
            PluginManager.SetPropertyValue("SimRIG.Target.MergeGapWorstCase", this.GetType(), Math.Round(targetState.MergeGapWorstCase, 2));

            PluginManager.SetPropertyValue("SimRIG.Target.TrueRawPace", this.GetType(), Math.Round(targetState.TargetTrueRawPace, 3));
            PluginManager.SetPropertyValue("SimRIG.Target.TrueBaselinePace", this.GetType(), Math.Round(targetState.TargetTrueBaselinePace, 3));
            PluginManager.SetPropertyValue("SimRIG.Target.TrueCurrentPace", this.GetType(), Math.Round(targetState.TargetTrueCurrentPace, 3));

            PluginManager.SetPropertyValue("SimRIG.Target.PlayerTyreScope", this.GetType(), targetState.PlayerTyreScope);
            PluginManager.SetPropertyValue("SimRIG.Target.PlayerStatLoss", this.GetType(), Math.Round(targetState.PlayerStatLoss, 2));
            PluginManager.SetPropertyValue("SimRIG.Target.PlayerOutLoss", this.GetType(), Math.Round(targetState.PlayerOutLoss, 2));
            PluginManager.SetPropertyValue("SimRIG.Target.TargetStatLoss", this.GetType(), Math.Round(targetState.TargetStatLoss, 2));
            PluginManager.SetPropertyValue("SimRIG.Target.TargetOutLoss", this.GetType(), Math.Round(targetState.TargetOutLoss, 2));
            PluginManager.SetPropertyValue("SimRIG.Target.TargetTireInferred", this.GetType(), targetState.TargetTireInferred);
            PluginManager.SetPropertyValue("SimRIG.Target.TargetDropZoneStatus", this.GetType(), targetState.TargetDropZoneStatus);

            PluginManager.SetPropertyValue("SimRIG.Pit.SelectedTireTime", this.GetType(), Math.Round(StrategyManager.SelectedTireTime, 2));
            PluginManager.SetPropertyValue("SimRIG.Pit.FuelCapacityInTireTime", this.GetType(), Math.Round(StrategyManager.FuelCapacityInTireTime, 2));
            PluginManager.SetPropertyValue("SimRIG.Pit.DropZoneStatus", this.GetType(), StrategyManager.DropZoneStatus);
            PluginManager.SetPropertyValue("SimRIG.Pit.DropZoneLossSec", this.GetType(), Math.Round(StrategyManager.DropZoneEstimatedLoss, 1));
            PluginManager.SetPropertyValue("SimRIG.Pit.StationaryTimeLoss", this.GetType(), Math.Round(StrategyManager.PitStationaryTimeLoss, 1));
            PluginManager.SetPropertyValue("SimRIG.Pit.MeasuredFuelFillRate", this.GetType(), Math.Round(StrategyManager.MeasuredFuelFillRate, 2));
            PluginManager.SetPropertyValue("SimRIG.Pit.TransitTime", this.GetType(), Math.Round(StrategyManager.PitTransitTime, 2));
            PluginManager.SetPropertyValue("SimRIG.Pit.TransitDriveThrough", this.GetType(), Math.Round(StrategyManager.PitTransitDriveThrough, 2));
            PluginManager.SetPropertyValue("SimRIG.Pit.CalibrationStatus", this.GetType(), StrategyManager.CalibrationStatus);

            PluginManager.SetPropertyValue("SimRIG.Debug.LastOpponentTransit", this.GetType(), Math.Round(StrategyManager.DebugLastOpponentTransit, 2));
            PluginManager.SetPropertyValue("SimRIG.Debug.LastOpponentStationary", this.GetType(), Math.Round(StrategyManager.DebugLastOpponentStationary, 2));
            PluginManager.SetPropertyValue("SimRIG.Debug.LastOpponentTotalTime", this.GetType(), Math.Round(StrategyManager.DebugLastOpponentTotalTime, 2));
            PluginManager.SetPropertyValue("SimRIG.Debug.LatchedDropZone", this.GetType(), _latchedDropZoneStatus);

            double myClTime = 0.0;
            if (StrategyManager.PlayerCleanSector.CurrentState == "NORMAL") myClTime = StrategyManager.PlayerCleanSector.LastNormalTime;
            else if (StrategyManager.PlayerCleanSector.CurrentState == "OUTLAP") myClTime = StrategyManager.PlayerCleanSector.LastOutlapTime;
            else myClTime = StrategyManager.PlayerCleanSector.LastInlapTime;

            PluginManager.SetPropertyValue("SimRIG.CleanSector.MyState", this.GetType(), StrategyManager.PlayerCleanSector.CurrentState);
            PluginManager.SetPropertyValue("SimRIG.CleanSector.MyTime", this.GetType(), Math.Round(myClTime, 3));
            PluginManager.SetPropertyValue("SimRIG.CleanSector.LeaderState", this.GetType(), StrategyManager.LeaderCleanSectorState);
            PluginManager.SetPropertyValue("SimRIG.CleanSector.LeaderTime", this.GetType(), Math.Round(StrategyManager.LeaderCleanSectorLastTime, 3));

            PluginManager.SetPropertyValue("SimRIG.Pit.PlayerPitCount", this.GetType(), StrategyManager.PlayerPitCount);
            PluginManager.SetPropertyValue("SimRIG.Pit.TargetPitCount", this.GetType(), targetState.PitCount);
            PluginManager.SetPropertyValue("SimRIG.Pit.LeaderPitCount", this.GetType(), StrategyManager.LeaderPitCount);
        }

        public void ApplyProfile(SimRigProfile profile) { if (profile == null) return; CurrentProfile = profile; IsGlobalTesting = false; SendRawCommand("GAME:0"); SendRawCommand("RPM:0"); for (int i = 0; i < 12; i++) ButtonColors[i] = Colors.White; if (CurrentProfile.ButtonColorsHex != null && CurrentProfile.ButtonColorsHex.Count == 12) { for (int i = 0; i < 12; i++) { this.ButtonColors[i] = ProfileManager.HexToColor(CurrentProfile.ButtonColorsHex[i]); } } else { CurrentProfile.ButtonColorsHex = new List<string>(); for (int i = 0; i < 12; i++) CurrentProfile.ButtonColorsHex.Add(ProfileManager.ColorToHex(Colors.White)); } SendLedConfig(); Task.Run(() => { System.Threading.Thread.Sleep(50); SyncAllColorsToHardware(); }); Settings.TopLeftEncoderMode = CurrentProfile.EncoderTopLeft_Mode; Settings.TopRightEncoderMode = CurrentProfile.EncoderTopRight_Mode; Settings.LastProfileUsed = CurrentProfile.ProfileName; this.SaveCommonSettings("GeneralSettings", Settings); SendEncoderCommand(0, Settings.TopLeftEncoderMode); SendEncoderCommand(1, Settings.TopRightEncoderMode); }
        public void SaveCurrentProfileToDisk(string name) { CurrentProfile.ProfileName = name; CurrentProfile.ButtonColorsHex.Clear(); for (int i = 0; i < 12; i++) CurrentProfile.ButtonColorsHex.Add(ProfileManager.ColorToHex(ButtonColors[i])); CurrentProfile.EncoderTopLeft_Mode = Settings.TopLeftEncoderMode; CurrentProfile.EncoderTopRight_Mode = Settings.TopRightEncoderMode; CurrentProfile.BitePoint = _liveBitePoint; ProfileManager.SaveProfile(CurrentProfile, name); Settings.LastProfileUsed = name; this.SaveCommonSettings("GeneralSettings", Settings); SendLedConfig(); }
        private void SetCornerMessage(int cornerIdx, string msg, int durationMs = 2000) { CancellationTokenSource cts = null; if (cornerIdx == 0) { if (_msgTokenTL != null) _msgTokenTL.Cancel(); _msgTokenTL = new CancellationTokenSource(); cts = _msgTokenTL; _msgTL = msg; } else if (cornerIdx == 1) { if (_msgTokenTR != null) _msgTokenTR.Cancel(); _msgTokenTR = new CancellationTokenSource(); cts = _msgTokenTR; _msgTR = msg; } else if (cornerIdx == 2) { if (_msgTokenBL != null) _msgTokenBL.Cancel(); _msgTokenBL = new CancellationTokenSource(); cts = _msgTokenBL; _msgBL = msg; } else if (cornerIdx == 3) { if (_msgTokenBR != null) _msgTokenBR.Cancel(); _msgTokenBR = new CancellationTokenSource(); cts = _msgTokenBR; _msgBR = msg; } UpdateAllProperties(); Task.Run(async () => { try { await Task.Delay(durationMs, cts.Token); if (!cts.Token.IsCancellationRequested) { if (cornerIdx == 0) _msgTL = ""; else if (cornerIdx == 1) _msgTR = ""; else if (cornerIdx == 2) _msgBL = ""; else if (cornerIdx == 3) _msgBR = ""; UpdateAllProperties(); } } catch { } }); }
        private void SetTempMessage(string msg, int durationMs = 2000) { _steeringWheelMessage = msg; PluginManager.SetPropertyValue("SimRIG.PersoSteeringWheelMessage", this.GetType(), _steeringWheelMessage); if (_msgTokenSource != null) { _msgTokenSource.Cancel(); _msgTokenSource.Dispose(); } _msgTokenSource = new CancellationTokenSource(); var token = _msgTokenSource.Token; Task.Run(async () => { try { await Task.Delay(durationMs, token); if (!token.IsCancellationRequested) { _steeringWheelMessage = "READY"; PluginManager.SetPropertyValue("SimRIG.PersoSteeringWheelMessage", this.GetType(), "READY"); } } catch { } }); }

        private void ProcessInputLine(string line)
        {
            line = line.Trim(); if (string.IsNullOrEmpty(line)) return;
            if (line.StartsWith(SH_MODE_PREFIX))
            {
                _steeringWheelMode = line.Substring(SH_MODE_PREFIX.Length).Trim();
                PluginManager.SetPropertyValue("SimRIG.Mode", this.GetType(), _steeringWheelMode);
                UpdateAllProperties();
            }
            else if (line.StartsWith(SH_MSG_PREFIX)) _steeringWheelMessage = line.Substring(SH_MSG_PREFIX.Length);
            else if (line.StartsWith(SH_VAL_PREFIX)) { if (double.TryParse(line.Substring(SH_VAL_PREFIX.Length), NumberStyles.Any, CultureInfo.InvariantCulture, out double val)) _liveBitePoint = val; }
            else if (line.StartsWith(SH_IDX_PREFIX))
            {
                string[] parts = line.Substring(SH_IDX_PREFIX.Length).Split(':');
                if (parts.Length == 2 && int.TryParse(parts[0], out int encIdx) && int.TryParse(parts[1], out int modeIdx))
                {
                    if (encIdx >= 2 && encIdx < 4 && modeIdx >= 0 && modeIdx < FunctionList.Length)
                    {
                        _encoderLabels[encIdx] = FunctionList[modeIdx];
                        UpdateAllProperties();
                    }
                }
            }
            else if (line.StartsWith(SH_WENC_PREFIX)) { string[] parts = line.Substring(SH_WENC_PREFIX.Length).Split(':'); if (parts.Length == 2 && int.TryParse(parts[0], out int encIdx) && int.TryParse(parts[1], out int dir)) HandleSerialEncoderRotation(encIdx, dir); }
            else if (line.StartsWith(SH_WPUSH_PREFIX)) { string[] parts = line.Substring(SH_WPUSH_PREFIX.Length).Split(':'); if (parts.Length == 2 && int.TryParse(parts[0], out int encIdx) && int.TryParse(parts[1], out int state)) HandleSerialEncoderPush(encIdx, state == 1); }
        }

        private void HandleSerialEncoderRotation(int encIdx, int dir)
        {
            if (!Settings.EnableFuelCalculatorSystem) return;
            if (_steeringWheelMode == "PIT") { if (encIdx == 0) { FuelCalc.AddUserOffset(dir * _fuelStep); UpdateAllProperties(); } else if (encIdx == 1) { int current = (int)_tyreScope; int max = Enum.GetNames(typeof(TyreSelectionScope)).Length; current += dir; if (current < 0) current = max - 1; else if (current >= max) current = 0; _tyreScope = (TyreSelectionScope)current; UpdateAllProperties(); } else if (encIdx == 2) { if (dir > 0) { int current = (int)_fuelStrat; current++; if (current > 3) current = 0; _fuelStrat = (FuelStrategyMode)current; FuelCalc.ResetUserOffset(); } else { _windshieldActive = !_windshieldActive; SetCornerMessage(2, _windshieldActive ? "WS: ON" : "WS: OFF", 1500); } UpdateAllProperties(); } else if (encIdx == 3) { _pressureOffsetKpa += dir; UpdateAllProperties(); } }
            else if (_steeringWheelMode == "PIT2") { if (encIdx == 0) { double step = 0.05; FuelCalc.TargetManuallySet = true; double current = FuelCalc.FuelPerLapTarget; current += (dir * step); if (current < 0.1) current = 0.1; FuelCalc.SetFuelTarget(current); SetCornerMessage(0, $"TGT: {FuelCalc.FuelPerLapTarget:0.00}", 1500); } else if (encIdx == 1) { _isSyncModeEnabled = !_isSyncModeEnabled; SetCornerMessage(1, _isSyncModeEnabled ? "SYNC: ON" : "SYNC: OFF", 1500); } else if (encIdx == 2) { _fastRepairActive = !_fastRepairActive; if (_fastRepairActive) MacroManager.SendChatCommand("#fastrepair"); else MacroManager.SendChatCommand("#clear fr"); SetCornerMessage(2, _fastRepairActive ? "FAST REP: ON" : "FAST REP: OFF", 2000); } UpdateAllProperties(); }
            else if (_steeringWheelMode == "STRAT") { if (encIdx == 0) { _targetSelectionIndex += dir; int maxIndex = 1 + StrategyManager.TotalOpponentsInSession; if (_targetSelectionIndex < 0) _targetSelectionIndex = maxIndex; else if (_targetSelectionIndex > maxIndex) _targetSelectionIndex = 0; string tName = GetTargetModeName(_targetSelectionIndex); SetCornerMessage(0, $"TGT: {tName}", 1500); UpdateAllProperties(); } }
        }

        private void HandleSerialEncoderPush(int encIdx, bool isPressed)
        {
            if (!Settings.EnableFuelCalculatorSystem) return;
            if (encIdx < 0 || encIdx > 3) return;
            if (isPressed) { if (_pushTokens[encIdx] != null) _pushTokens[encIdx].Dispose(); _pushTokens[encIdx] = new CancellationTokenSource(); var token = _pushTokens[encIdx].Token; _longPressExecuted[encIdx] = false; Task.Run(async () => { try { await Task.Delay(1000, token); if (!token.IsCancellationRequested) { _longPressExecuted[encIdx] = true; ExecuteLongPressAction(encIdx); } } catch { } }); }
            else { if (_pushTokens[encIdx] != null) { _pushTokens[encIdx].Cancel(); _pushTokens[encIdx].Dispose(); } if (!_longPressExecuted[encIdx]) ExecuteShortPressAction(encIdx); }
        }

        private void ExecuteShortPressAction(int encIdx)
        {
            if (_steeringWheelMode == "PIT") { if (encIdx == 0) { if (_fuelStep == 0.1) _fuelStep = 1.0; else if (_fuelStep == 1.0) _fuelStep = 5.0; else _fuelStep = 0.1; SetCornerMessage(0, $"STEP: {_fuelStep}L", 1500); } else if (encIdx == 1) { MacroManager.SendChatCommand("#clear tires"); SetCornerMessage(1, "CLEARED", 2000); } else if (encIdx == 2) { _fastRepairActive = !_fastRepairActive; if (_fastRepairActive) MacroManager.SendChatCommand("#fastrepair"); else MacroManager.SendChatCommand("#clear fr"); SetCornerMessage(2, _fastRepairActive ? "FAST REP: ON" : "FAST REP: OFF", 2000); } else if (encIdx == 3) { _pressureOffsetKpa = 0; MacroManager.SendChatCommand("#lf 0kpa #rf 0kpa #lr 0kpa #rr 0kpa"); SetCornerMessage(3, "RESET (SENT 0)", 1500); } UpdateAllProperties(); }
            else if (_steeringWheelMode == "PIT2") { if (encIdx == 0) { _isTargetModeEnabled = !_isTargetModeEnabled; SetCornerMessage(0, _isTargetModeEnabled ? "TARGET: ON" : "TARGET: OFF", 1500); } else if (encIdx == 1) { _isSyncModeEnabled = !_isSyncModeEnabled; SetCornerMessage(1, _isSyncModeEnabled ? "SYNC: ON" : "SYNC: OFF", 1500); } else if (encIdx == 2) { _fastRepairActive = !_fastRepairActive; if (_fastRepairActive) MacroManager.SendChatCommand("#fastrepair"); else MacroManager.SendChatCommand("#clear fr"); SetCornerMessage(2, _fastRepairActive ? "FAST REP: ON" : "FAST REP: OFF", 2000); } UpdateAllProperties(); }
            else if (_steeringWheelMode == "STRAT") { if (encIdx == 0) { _targetSelectionIndex = 0; SetCornerMessage(0, "TGT: AHEAD", 1500); UpdateAllProperties(); } }
        }

        private void ExecuteLongPressAction(int encIdx)
        {
            if (_steeringWheelMode == "PIT" || _steeringWheelMode == "PIT2")
            {
                if (encIdx == 0)
                {
                    double val = CalculateFinalFuelToAdd();
                    string cmd = string.Format(CultureInfo.InvariantCulture, "#fuel {0}l", val);
                    if (_windshieldActive) cmd += " #ws";
                    MacroManager.SendChatCommand(cmd);
                    SetCornerMessage(0, $"SENT: {val}L", 2500);

                    int voiceLaps = (int)Math.Floor(FuelCalc.TankLapsRemaining);
                    if (voiceLaps < 0 || voiceLaps > 500) voiceLaps = 99;
                    TriggerRadioVoice("FUEL_INFO", val.ToString("F0"), voiceLaps);
                }
                else if (encIdx == 1)
                {
                    if (_tyreScope == TyreSelectionScope.None) MacroManager.SendChatCommand("#clear tires");
                    else MacroManager.SendChatCommand(GetTyreCommandString());
                    SetCornerMessage(1, $"SENT: {_tyreScope}", 2500);
                }
                else if (encIdx == 2) { FuelCalc.ResetUserOffset(); _pressureOffsetKpa = 0; MacroManager.SendChatCommand("#clear"); SetCornerMessage(2, "ALL CLEARED & RESET", 2500); UpdateAllProperties(); }
                else if (encIdx == 3) { string cmd = GetPressureCommandString(); MacroManager.SendChatCommand(cmd); string sign = _pressureOffsetKpa >= 0 ? "+" : ""; SetCornerMessage(3, $"SENT: {sign}{_pressureOffsetKpa} kPa", 2500); }
            }
            else if (_steeringWheelMode == "STRAT")
            {
                if (encIdx == 0)
                {
                    string currentTargetMode = GetTargetModeName(_targetSelectionIndex);
                    double trackLen = PluginManager.GetPropertyValue("DataCorePlugin.GameData.NewData.TrackLength") != null ? Convert.ToDouble(PluginManager.GetPropertyValue("DataCorePlugin.GameData.NewData.TrackLength")) : 5000.0;
                    double myPos = PluginManager.GetPropertyValue("DataCorePlugin.GameData.NewData.TrackPositionPercent") != null ? Convert.ToDouble(PluginManager.GetPropertyValue("DataCorePlugin.GameData.NewData.TrackPositionPercent")) : 0.0;
                    double maxFuel = PluginManager.GetPropertyValue("DataCorePlugin.GameData.NewData.MaxFuel") != null ? Convert.ToDouble(PluginManager.GetPropertyValue("DataCorePlugin.GameData.NewData.MaxFuel")) : 100.0;
                    int myCurrentLap = PluginManager.GetPropertyValue("DataCorePlugin.GameData.NewData.CurrentLap") != null ? Convert.ToInt32(PluginManager.GetPropertyValue("DataCorePlugin.GameData.NewData.CurrentLap")) : 1;

                    double projectedFuelToAdd = CalculateProjectedFuelToAdd(myPos);

                    var state = StrategyManager.GetTargetState(currentTargetMode, StrategyManager.TrueCurrentPace, myPos, trackLen, projectedFuelToAdd, maxFuel, FuelCalc.AverageFuelPerLap, FuelCalc.RaceTotalLaps, FuelCalc.RaceLapsRemaining, FuelCalc.TankLapsRemaining, RaceStartingFuel, myCurrentLap, _lastSpeedKmh, _lastOpponents);

                    if (state.Name.Contains("NO TARGET")) TriggerRadioVoice("TARGET_NO_CAR");
                    else
                    {
                        string diagCode = "DIAG_" + state.Diagnosis.ToUpper();
                        string diagLoc = PitWallLanguage.GetPhrase(Settings.VoiceLanguage, diagCode);
                        TriggerRadioVoice("TARGET_INFO", state.Name, state.GapSeconds.ToString("F1", CultureInfo.InvariantCulture), diagLoc);

                        Task.Delay(3500).ContinueWith(t => {
                            if (state.UndercutViable) TriggerRadioVoice("TARGET_UNDERCUT_YES");
                            else TriggerRadioVoice("TARGET_UNDERCUT_NO");
                        });
                    }
                }
                else if (encIdx == 1)
                {
                    string statusLocKey = "ZONE_CLEAR";
                    if (StrategyManager.DropZoneStatus.StartsWith("1 CAR")) statusLocKey = "ZONE_1CAR";
                    else if (StrategyManager.DropZoneStatus.StartsWith("TRAFFIC")) statusLocKey = "ZONE_TRAFFIC";

                    string zoneLoc = PitWallLanguage.GetPhrase(Settings.VoiceLanguage, statusLocKey);
                    TriggerRadioVoice("DROP_ZONE_INFO", StrategyManager.DropZoneEstimatedLoss.ToString("F0"), zoneLoc);
                }
            }
        }

        private string GetTyreCommandString() { switch (_tyreScope) { case TyreSelectionScope.Fronts: return "#lf #rf"; case TyreSelectionScope.Rears: return "#lr #rr"; case TyreSelectionScope.Left: return "#lf #lr"; case TyreSelectionScope.Right: return "#rf #rr"; case TyreSelectionScope.FL: return "#lf"; case TyreSelectionScope.FR: return "#rf"; case TyreSelectionScope.RL: return "#lr"; case TyreSelectionScope.RR: return "#rr"; case TyreSelectionScope.None: return "#clear tires"; default: return "#lf #rf #lr #rr"; } }
        private string GetPressureCommandString() { string val = $"{(_pressureOffsetKpa >= 0 ? "+" : "")}{_pressureOffsetKpa}kpa"; string cmdBase = ""; switch (_tyreScope) { case TyreSelectionScope.Fronts: cmdBase = "#lf {0} #rf {0}"; break; case TyreSelectionScope.Rears: cmdBase = "#lr {0} #rr {0}"; break; case TyreSelectionScope.Left: cmdBase = "#lf {0} #lr {0}"; break; case TyreSelectionScope.Right: cmdBase = "#rf {0} #rr {0}"; break; case TyreSelectionScope.FL: cmdBase = "#lf {0}"; break; case TyreSelectionScope.FR: cmdBase = "#rf {0}"; break; case TyreSelectionScope.RL: cmdBase = "#lr {0}"; break; case TyreSelectionScope.RR: cmdBase = "#rr {0}"; break; default: cmdBase = "#lf {0} #rf {0} #lr {0} #rr {0}"; break; } return string.Format(CultureInfo.InvariantCulture, cmdBase, val); }

        public void DataUpdate(PluginManager pluginManager, ref GameData data)
        {
            if (_deviceAcquired && joystick != null) { try { joystick.Poll(); var state = joystick.GetCurrentState(); for (int i = 0; i < state.Buttons.Length && i < RawButtons.Length; i++) RawButtons[i] = state.Buttons[i]; RawAxes[0] = state.X; RawAxes[1] = state.Y; RawAxes[2] = state.Z; RawAxes[3] = state.RotationZ; RawAxes[4] = state.RotationX; RawAxes[5] = state.RotationY; } catch { _deviceAcquired = false; try { joystick.Acquire(); _deviceAcquired = true; } catch { } } }

            if (data.GameRunning && data.NewData != null)
            {
                _lastOpponents = data.NewData.Opponents?.ToList();
                _lastSpeedKmh = data.NewData.SpeedKmh;

                if (data.NewData.IsInPitLane == 1)
                {
                    if (data.NewData.SpeedKmh < 2.0 || StrategyManager.DropZoneStatus.StartsWith("TRAFFIC") || StrategyManager.DropZoneStatus.StartsWith("1 CAR"))
                    {
                        _latchedDropZoneStatus = StrategyManager.DropZoneStatus;
                    }
                    _wasInPit = true;
                    _boxNowPlayed = false;
                }

                if (_wasInPit && data.NewData.IsInPitLane == 0)
                {
                    if (Settings.EnableVoiceEngineer)
                    {
                        if (_latchedDropZoneStatus.StartsWith("TRAFFIC") || _latchedDropZoneStatus.StartsWith("1 CAR"))
                        {
                            TriggerRadioVoice("PIT_EXIT_TRAFFIC");
                        }
                    }
                    _latchedDropZoneStatus = "CLEAR";
                    _wasInPit = false;
                }

                _currentTankLevel = data.NewData.Fuel;
                FuelCalc.UseTargetOverride = _isTargetModeEnabled;
                double trackPos = data.NewData.TrackPositionPercent;
                double currentLapTimeSec = data.NewData.CurrentLapTime.TotalSeconds;

                int sessionState = 4;
                var rawState = PluginManager.GetPropertyValue("DataCorePlugin.GameRawData.Telemetry.SessionState");
                if (rawState != null) sessionState = Convert.ToInt32(rawState);

                int checkeredFlag = data.NewData.Flag_Checkered;
                double timeLeft = data.NewData.SessionTimeLeft.TotalSeconds;
                bool isLapLimited = false;
                bool isTimeLimited = false;
                int rawSessionLaps = 0;
                double rawSessionTime = 0.0;
                bool globalCheckered = false;

                try
                {
                    var propIsLapLimit = PluginManager.GetPropertyValue("DataCorePlugin.GameRawData.CurrentSessionInfo.IsLimitedSessionLaps");
                    if (propIsLapLimit != null) bool.TryParse(propIsLapLimit.ToString(), out isLapLimited);

                    var propIsTimeLimit = PluginManager.GetPropertyValue("DataCorePlugin.GameRawData.CurrentSessionInfo.IsLimitedTime");
                    if (propIsTimeLimit != null) bool.TryParse(propIsTimeLimit.ToString(), out isTimeLimited);

                    var propSessLaps = PluginManager.GetPropertyValue("DataCorePlugin.GameRawData.CurrentSessionInfo._SessionLaps");
                    if (propSessLaps != null) int.TryParse(propSessLaps.ToString(), out rawSessionLaps);

                    var propSessTime = PluginManager.GetPropertyValue("DataCorePlugin.GameRawData.CurrentSessionInfo._SessionTime");
                    if (propSessTime != null) double.TryParse(propSessTime.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out rawSessionTime);

                    var rawCheckered = PluginManager.GetPropertyValue("DataCorePlugin.GameRawData.Telemetry.SessionFlagsDetails.Ischeckered");
                    if (rawCheckered != null) bool.TryParse(rawCheckered.ToString(), out globalCheckered);
                }
                catch { }

                PredictiveRaceState pState = new PredictiveRaceState
                {
                    IsRace = data.NewData.SessionTypeName == "Race",
                    IsLapLimited = isLapLimited,
                    IsTimeLimited = isTimeLimited,
                    SessionState = sessionState,
                    PlayerCheckeredFlag = checkeredFlag > 0,
                    GlobalCheckeredFlag = globalCheckered,
                    TimeLeftSeconds = timeLeft,
                    RawSessionTime = rawSessionTime,
                    TotalLaps = rawSessionLaps,
                    MyCurrentLap = data.NewData.CurrentLap > 0 ? data.NewData.CurrentLap : 0,
                    MyTrackPosition = trackPos,
                    MyPace = StrategyManager.TrueCurrentPace,
                    TrackRecordTime = data.NewData.BestLapTime.TotalSeconds,
                    TrackLength = data.NewData.TrackLength > 0 ? data.NewData.TrackLength : 5000.0,
                    IsRaceLeader = data.NewData.Position == 1,
                    LeaderCurrentLap = 0,
                    LeaderTrackPosition = 0.0,
                    LeaderLastLap = 0.0,
                    LeaderBestLap = 0.0,
                    IsInPitLane = data.NewData.IsInPitLane == 1,
                    IsInPitBox = false
                };

                if (sessionState >= 4 && pState.IsRace && !_raceStartingFuelLatched && data.NewData.Fuel > 0)
                {
                    RaceStartingFuel = data.NewData.Fuel;
                    _raceStartingFuelLatched = true;
                }
                if (!pState.IsRace || sessionState < 3)
                {
                    _raceStartingFuelLatched = false;
                    RaceStartingFuel = 0.0;
                }

                double avgTireTemp = 0.0;
                try
                {
                    double GetIracingTireTemp(string tire)
                    {
                        var l = PluginManager.GetPropertyValue($"DataCorePlugin.GameRawData.Telemetry.{tire}tempCL");
                        var m = PluginManager.GetPropertyValue($"DataCorePlugin.GameRawData.Telemetry.{tire}tempCM");
                        var r = PluginManager.GetPropertyValue($"DataCorePlugin.GameRawData.Telemetry.{tire}tempCR");
                        if (l != null && m != null && r != null) return (Convert.ToDouble(l) + Convert.ToDouble(m) + Convert.ToDouble(r)) / 3.0;
                        return -1.0;
                    }
                    double lf = GetIracingTireTemp("LF"); if (lf < 0) lf = data.NewData.TyreTemperatureFrontLeft;
                    double rf = GetIracingTireTemp("RF"); if (rf < 0) rf = data.NewData.TyreTemperatureFrontRight;
                    double lr = GetIracingTireTemp("LR"); if (lr < 0) lr = data.NewData.TyreTemperatureRearLeft;
                    double rr = GetIracingTireTemp("RR"); if (rr < 0) rr = data.NewData.TyreTemperatureRearRight;
                    avgTireTemp = (lf + rf + lr + rr) / 4.0;
                }
                catch
                {
                    avgTireTemp = data.NewData.RoadTemperature;
                }

                if (data.NewData.CurrentLap != _lastSessionLap)
                {
                    if (_lastSessionLap != -1)
                    {
                        double lapTime = data.NewData.LastLapTime.TotalSeconds;
                        var shFuelProp = PluginManager.GetPropertyValue("DataCorePlugin.Computed.Fuel_LastLapConsumption");
                        double actualFuelUsed = shFuelProp != null ? Convert.ToDouble(shFuelProp) : 0.0;
                        double trackTemp = data.NewData.RoadTemperature;
                        bool isInPit = (data.NewData.IsInPitLane == 1);
                        double trackLengthMtrs = data.NewData.TrackLength > 0 ? data.NewData.TrackLength : 5000.0;

                        double sessionClock = 0.0;
                        var rawSessTime2 = PluginManager.GetPropertyValue("DataCorePlugin.GameRawData.Telemetry.SessionTime");
                        if (rawSessTime2 != null) sessionClock = Convert.ToDouble(rawSessTime2);
                        else sessionClock = (DateTime.Now - DateTime.Today).TotalSeconds;

                        FuelCalc.RecordLap(lapTime, actualFuelUsed, _wasInPit, isInPit, data.NewData.Flag_Black);

                        int lapsCompleted = FuelCalc.RaceLapsCompleted;
                        StrategyManager.AnalyzeLap(lapTime, actualFuelUsed, trackTemp, _wasInPit, isInPit, trackLengthMtrs, data.NewData.Fuel, lapsCompleted, sessionClock);

                        if (Settings.EnableVoiceEngineer)
                        {
                            if (FuelCalc.TankLapsRemaining > 0 && FuelCalc.TankLapsRemaining <= 1.5 && !_boxNowPlayed)
                            {
                                TriggerRadioVoice("BOX_NOW");
                                _boxNowPlayed = true;
                            }
                            else if (Settings.EnableAutoSpotter && !_boxNowPlayed)
                            {
                                double tgtMaxFuel = data.NewData.MaxFuel > 0 ? data.NewData.MaxFuel : 100.0;
                                double projectedFuelToAdd = CalculateProjectedFuelToAdd(trackPos);
                                var tState = StrategyManager.GetTargetState(GetTargetModeName(_targetSelectionIndex), StrategyManager.TrueCurrentPace, data.NewData.TrackPositionPercent, trackLengthMtrs, projectedFuelToAdd, tgtMaxFuel, FuelCalc.AverageFuelPerLap, FuelCalc.RaceTotalLaps, FuelCalc.RaceLapsRemaining, FuelCalc.TankLapsRemaining, RaceStartingFuel, data.NewData.CurrentLap, data.NewData.SpeedKmh, data.NewData.Opponents?.ToList());
                                if (!tState.Name.Contains("NO TARGET") && tState.GapSeconds > 0 && tState.GapSeconds < 3.0)
                                {
                                    string diagLoc = PitWallLanguage.GetPhrase(Settings.VoiceLanguage, "DIAG_" + tState.Diagnosis.ToUpper());
                                    TriggerRadioVoice("TARGET_INFO", tState.Name, tState.GapSeconds.ToString("F1", CultureInfo.InvariantCulture), diagLoc);
                                }
                            }
                        }
                    }
                    _lastSessionLap = data.NewData.CurrentLap;
                }

                if (Settings.EnableVoiceEngineer && !_boxNowPlayed)
                {
                    if (_lastTrackPos < 0.70 && trackPos >= 0.70)
                    {
                        if (FuelCalc.TankLapsRemaining > 0 && FuelCalc.TankLapsRemaining <= 1.40)
                        {
                            TriggerRadioVoice("BOX_NOW");
                            _boxNowPlayed = true;
                        }
                    }
                }
                _lastTrackPos = trackPos;

                double maxTank = data.NewData.MaxFuel > 0 ? data.NewData.MaxFuel : 100.0;
                double bestLap = data.NewData.BestLapTime.TotalSeconds;
                double trackLen = data.NewData.TrackLength > 0 ? data.NewData.TrackLength : 5000.0;

                if (data.NewData.Opponents != null)
                {
                    var leader = data.NewData.Opponents.FirstOrDefault(o => o.Position == 1);
                    if (leader != null)
                    {
                        pState.LeaderLastLap = leader.LastLapTime.TotalSeconds;

                        int leaderLatchedLap = leader.CurrentLap ?? 0;
                        leaderLatchedLap = StrategyManager.GetLatchedOpponentLap(leader.Name, leaderLatchedLap);

                        pState.LeaderCurrentLap = leaderLatchedLap;
                        pState.LeaderTrackPosition = leader.TrackPositionPercent ?? 0.0;
                        pState.LeaderBestLap = pState.LeaderLastLap;
                    }
                }

                int isInPitBox = 0;
                var pitBoxProp = PluginManager.GetPropertyValue("DataCorePlugin.GameData.IsInPit");
                if (pitBoxProp != null) isInPitBox = Convert.ToInt32(pitBoxProp);
                pState.IsInPitBox = isInPitBox == 1;

                string currentCarClass = data.NewData.CarClass ?? "DEFAULT";
                string currentTrackId = data.NewData.TrackId ?? "DEFAULT";
                string tMode = GetTargetModeName(_targetSelectionIndex);

                double sessionClockUpdate = 0.0;
                var rawSessTimeUpdate = PluginManager.GetPropertyValue("DataCorePlugin.GameRawData.Telemetry.SessionTime");
                if (rawSessTimeUpdate != null) sessionClockUpdate = Convert.ToDouble(rawSessTimeUpdate);
                else sessionClockUpdate = (DateTime.Now - DateTime.Today).TotalSeconds;

                double pitSpeedLimitKmh = 60.0;
                try
                {
                    var rawPitSpeed = PluginManager.GetPropertyValue("DataCorePlugin.GameRawData.SessionData.WeekendInfo.TrackPitSpeedLimit");
                    if (rawPitSpeed != null)
                    {
                        string speedStr = rawPitSpeed.ToString().ToLower().Replace("km/h", "").Replace("kph", "").Trim();
                        if (double.TryParse(speedStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsedSpeed))
                        {
                            pitSpeedLimitKmh = parsedSpeed;
                        }
                    }
                }
                catch { }

                double currentProjectedFuelToAdd = CalculateProjectedFuelToAdd(trackPos);
                StrategyManager.Update(data, currentCarClass, currentTrackId, _tyreScope, currentProjectedFuelToAdd, tMode, sessionClockUpdate, FuelCalc.AverageFuelPerLap, bestLap, data.NewData.RoadTemperature, sessionState, pState.IsInPitBox, pitSpeedLimitKmh, maxTank, FuelCalc.RaceLapsRemaining, RaceStartingFuel);

                FuelCalc.UpdateStrategy(data.NewData.Fuel, maxTank, pState, currentLapTimeSec, StrategyManager.PitTransitTime, StrategyManager.MeasuredFuelFillRate, StrategyManager.SelectedTireTime);

                ProcessPendingLogs();

                if (pState.IsRace && FuelCalc.IsPredictionValid)
                {
                    double currentLapsRem = FuelCalc.RaceLapsRemaining;
                    int currentLap = data.NewData.CurrentLap;
                    bool currentTraffic = StrategyManager.MyIsInTraffic;

                    if (_lastLoggedRaceLapsRem == -1) _lastLoggedRaceLapsRem = currentLapsRem;
                    if (_lastLoggedLap == -1) _lastLoggedLap = currentLap;

                    bool lapsRemChanged = Math.Abs(currentLapsRem - _lastLoggedRaceLapsRem) > 0.001;
                    bool lapChanged = currentLap != _lastLoggedLap;
                    bool trafficChanged = currentTraffic != _lastLoggedTraffic;

                    if (lapsRemChanged || lapChanged || trafficChanged)
                    {
                        string triggerReason = lapsRemChanged ? "LAPS_REM_CHANGE" :
                                              (lapChanged ? "NEW_LAP" : "TRAFFIC_CHANGE");

                        double pitEntryPct = StrategyManager.GetPitEntryPct();
                        double distToPit = pitEntryPct - trackPos;
                        if (distToPit < 0) distToPit = 0;
                        double projectedEndOfLapFuelTank = Math.Max(0, data.NewData.Fuel - (distToPit * FuelCalc.GetEffectiveConsumption()));
                        bool isPlayerInPitLane = data.NewData.IsInPitLane == 1;

                        LogDiagnosticData(data, triggerReason, sessionState, FuelCalc.RaceTotalLaps, FuelCalc.RaceLapsCompleted, currentLapsRem, FuelCalc.TankLapsRemaining, avgTireTemp, currentProjectedFuelToAdd, projectedEndOfLapFuelTank, isPlayerInPitLane);

                        _lastLoggedRaceLapsRem = currentLapsRem;
                        _lastLoggedLap = currentLap;
                        _lastLoggedTraffic = currentTraffic;
                    }
                }

                if (Settings.EnableAutoPitStrategy && Settings.EnableFuelCalculatorSystem)
                {
                    if (data.NewData.IsInPitLane == 1 && !_autoPitTriggered && FuelCalc.IsPredictionValid)
                    {
                        _autoPitTriggered = true;
                        Task.Delay(2000).ContinueWith(t => {
                            double val = CalculateFinalFuelToAdd();
                            string cmd = string.Format(CultureInfo.InvariantCulture, "#fuel {0}l", val);
                            if (_windshieldActive) cmd += " #ws";
                            MacroManager.SendChatCommand(cmd);
                            SetCornerMessage(0, $"AUTO: {val}L", 3000);
                        });
                    }
                    if (data.NewData.IsInPitLane == 0) _autoPitTriggered = false;
                }

                UpdateAllProperties();
            }
            else
            {
                if (_lastGameRunning)
                {
                    _currentLogFile = null;
                    _diagnosticLogFile = null;
                    _lastLoggedRaceLapsRem = -1;
                    _lastLoggedLap = -1;
                }
            }

            if (IsLedsConnected) { if (IsGlobalTesting) return; bool gameRunning = data.GameRunning; if (gameRunning != _lastGameRunning) { try { _serialLeds.WriteLine($"GAME:{(gameRunning ? 1 : 0)}"); } catch { } _lastGameRunning = gameRunning; } if (gameRunning) { int currentRpm = 0; if (data.NewData != null && data.NewData.Rpms > 0 && data.NewData.CarSettings_MaxRPM > 0) currentRpm = (int)((data.NewData.Rpms / (double)data.NewData.CarSettings_MaxRPM) * 100); if (currentRpm != _lastRpmPercent) { try { _serialLeds.WriteLine($"RPM:{currentRpm}"); } catch { } _lastRpmPercent = currentRpm; } int currentFlag = 0; if (data.NewData != null) { if (data.NewData.Flag_Yellow > 0) currentFlag = 1; else if (data.NewData.Flag_Blue > 0) currentFlag = 2; else if (data.NewData.Flag_Green > 0) currentFlag = 4; } if (currentFlag != _lastFlagState) { try { _serialLeds.WriteLine($"FLG:{currentFlag}"); } catch { } _lastFlagState = currentFlag; } } }
            _lastGameRunning = data.GameRunning;
        }

        public async Task RunDiscovery() { CloseSerialPorts(); string[] ports = SerialPort.GetPortNames(); foreach (string port in ports) { try { using (SerialPort tempPort = new SerialPort(port, 115200)) { tempPort.ReadTimeout = 500; tempPort.WriteTimeout = 500; tempPort.DtrEnable = true; tempPort.RtsEnable = true; tempPort.Open(); await Task.Delay(3000); tempPort.DiscardInBuffer(); tempPort.DiscardOutBuffer(); tempPort.WriteLine("WHO"); await Task.Delay(200); string response = tempPort.ReadExisting(); if (string.IsNullOrWhiteSpace(response)) { tempPort.WriteLine("WHO"); await Task.Delay(200); response = tempPort.ReadExisting(); } if (response.Contains("ID:SIMRIG_INPUT")) { tempPort.Close(); await Task.Delay(200); ConnectInputPort(port); } else if (response.Contains("ID:SIMRIG_LEDS")) { tempPort.Close(); await Task.Delay(200); ConnectLedsPort(port); } } } catch { } } }
        private void ConnectInputPort(string portName) { try { _serialInput = new SerialPort(portName, 115200); _serialInput.DtrEnable = true; _serialInput.RtsEnable = true; _serialInput.Open(); _serialInput.DataReceived += Input_DataReceived; Task.Delay(2000).ContinueWith(t => SyncInputChip()); Task.Delay(2500).ContinueWith(t => SendSystemStateToFirmware()); } catch { _serialInput = null; } }
        private void ConnectLedsPort(string portName) { try { _serialLeds = new SerialPort(portName, 115200); _serialLeds.DtrEnable = true; _serialLeds.RtsEnable = true; _serialLeds.Open(); Task.Delay(2000).ContinueWith(t => { ApplyProfile(CurrentProfile); }); } catch { _serialLeds = null; } }
        public void CloseSerialPorts() { if (_serialInput != null) { try { if (_serialInput.IsOpen) { _serialInput.DataReceived -= Input_DataReceived; _serialInput.Close(); } _serialInput.Dispose(); } catch { } _serialInput = null; } if (_serialLeds != null) { try { if (_serialLeds.IsOpen) _serialLeds.Close(); _serialLeds.Dispose(); } catch { } _serialLeds = null; } }
        private void Input_DataReceived(object sender, SerialDataReceivedEventArgs e) { if (_serialInput == null || !_serialInput.IsOpen) return; try { string data = _serialInput.ReadExisting(); lock (_bufferLock) _inputBuffer += data; string line; while (ExtractLineFromInputBuffer(out line)) ProcessInputLine(line); } catch { } }
        private bool ExtractLineFromInputBuffer(out string line) { line = null; int newlineIndex = _inputBuffer.IndexOfAny(new char[] { '\n', '\r' }); if (newlineIndex >= 0) { line = _inputBuffer.Substring(0, newlineIndex); _inputBuffer = _inputBuffer.Substring(newlineIndex + (_inputBuffer[newlineIndex] == '\r' && newlineIndex + 1 < _inputBuffer.Length && _inputBuffer[newlineIndex + 1] == '\n' ? 2 : 1)); return true; } return false; }
        public void SendEncoderCommand(int encIdx, int modeIdx) { if (IsInputConnected) try { _serialInput.WriteLine($"M:{encIdx}:{modeIdx}"); } catch { } }
        private void SyncInputChip() { SendEncoderCommand(0, Settings.TopLeftEncoderMode); System.Threading.Thread.Sleep(50); SendEncoderCommand(1, Settings.TopRightEncoderMode); System.Threading.Thread.Sleep(50); if (IsInputConnected) try { _serialInput.WriteLine("S"); } catch { } }
        private void InitializeDirectInput() { try { directInput = new DirectInput(); var devices = directInput.GetDevices(DeviceClass.GameControl, DeviceEnumerationFlags.AttachedOnly); var deviceInstance = devices.FirstOrDefault(d => d.InstanceName.IndexOf("Arduino", StringComparison.OrdinalIgnoreCase) >= 0 || d.InstanceName.IndexOf("Leonardo", StringComparison.OrdinalIgnoreCase) >= 0); if (deviceInstance != null) { joystick = new Joystick(directInput, deviceInstance.InstanceGuid); joystick.SetCooperativeLevel(IntPtr.Zero, CooperativeLevel.Background | CooperativeLevel.NonExclusive); joystick.Acquire(); _deviceAcquired = true; } } catch (Exception ex) { SimHub.Logging.Current.Error($"[SimRIG_LOG] HID Error: {ex.Message}"); } }

        public void SendRawCommand(string cmd) { if (IsLedsConnected) try { _serialLeds.WriteLine(cmd); } catch { } }
        private void SendEventConfig(int id, LedEventConfig cfg) { if (!IsLedsConnected) return; Color c1 = ProfileManager.HexToColor(cfg.ColorHex); Color c2 = ProfileManager.HexToColor(cfg.ColorHexSecondary); string cmd = $"EVTCFG:{id}:{cfg.ZoneA_Start}:{cfg.ZoneA_Count}:{(cfg.ZoneB_Enabled ? 1 : 0)}:{cfg.ZoneB_Start}:{cfg.ZoneB_Count}:{(cfg.IsBlinking ? 1 : 0)}:{cfg.BlinkIntervalMs}:{c1.R},{c1.G},{c1.B}:{c2.R},{c2.G},{c2.B}"; try { _serialLeds.WriteLine(cmd); } catch { } }
        public void SendLedConfig() { if (!IsLedsConnected) return; try { _serialLeds.WriteLine($"BRT:{CurrentProfile.Brightness_Backlight}:{CurrentProfile.Brightness_RPM}"); _serialLeds.WriteLine($"RPMCFG:{CurrentProfile.Rpm_StartLed}:{CurrentProfile.Rpm_LedCount}"); _serialLeds.WriteLine($"IDLE:{CurrentProfile.Idle_Mode}"); Color cIdle = ProfileManager.HexToColor(CurrentProfile.Idle_Color); _serialLeds.WriteLine($"IDLECOL:{cIdle.R},{cIdle.G},{cIdle.B}"); int styleIdx = 0; if (CurrentProfile.Rpm_Style == "RightToLeft") styleIdx = 1; else if (CurrentProfile.Rpm_Style == "CenterToSide") styleIdx = 2; else if (CurrentProfile.Rpm_Style == "SideToCenter") styleIdx = 3; _serialLeds.WriteLine($"RPMSTYLE:{styleIdx}"); if (CurrentProfile.Rpm_UseGradient) { _serialLeds.WriteLine("RPMGRAD:1"); Color cStart = ProfileManager.HexToColor(CurrentProfile.Rpm_Color_Start); Color cEnd = ProfileManager.HexToColor(CurrentProfile.Rpm_Color_End); _serialLeds.WriteLine($"RPMCOLS:{cStart.R},{cStart.G},{cStart.B}:{cEnd.R},{cEnd.G},{cEnd.B}"); } else { _serialLeds.WriteLine("RPMGRAD:0"); Color c1 = ProfileManager.HexToColor(CurrentProfile.Rpm_ZoneLow_Color); int cnt1 = CurrentProfile.Rpm_ZoneLow_Count; _serialLeds.WriteLine($"RPMSEG:0:{cnt1}:{c1.R},{c1.G},{c1.B}"); Color c2 = ProfileManager.HexToColor(CurrentProfile.Rpm_ZoneMed_Color); int cnt2 = CurrentProfile.Rpm_ZoneMed_Count; _serialLeds.WriteLine($"RPMSEG:{cnt1}:{cnt2}:{c2.R},{c2.G},{c2.B}"); Color c3 = ProfileManager.HexToColor(CurrentProfile.Rpm_ZoneHigh_Color); int cnt3 = CurrentProfile.Rpm_ZoneHigh_Count; _serialLeds.WriteLine($"RPMSEG:{cnt1 + cnt2}:{cnt3}:{c3.R},{c3.G},{c3.B}"); Color c4 = ProfileManager.HexToColor(CurrentProfile.Rpm_ZoneMax_Color); int cnt4 = CurrentProfile.Rpm_ZoneMax_Count; _serialLeds.WriteLine($"RPMSEG:{cnt1 + cnt2 + cnt3}:{cnt4}:{c4.R},{c4.G},{c4.B}"); } SendEventConfig(0, CurrentProfile.Flag_Yellow); SendEventConfig(2, CurrentProfile.Flag_Blue); SendEventConfig(3, CurrentProfile.Flag_Green); SendEventConfig(4, CurrentProfile.Car_ABS); SendEventConfig(5, CurrentProfile.Car_TC); SendEventConfig(6, CurrentProfile.Car_Pit); } catch { } }

        public void SyncAllColorsToHardware() { if (!IsLedsConnected) return; for (int i = 0; i < 12; i++) { SetButtonColor(i, ButtonColors[i]); System.Threading.Thread.Sleep(10); } }
        public void SetButtonColor(int logicalId, Color c) { if (logicalId < 0 || logicalId > 11) return; ButtonColors[logicalId] = c; if (IsLedsConnected) { string cmdPrefix; int ledIndex; if (logicalId < 6) { cmdPrefix = "BL"; ledIndex = logicalId; } else { cmdPrefix = "BR"; int relativeIndex = logicalId - 6; ledIndex = 5 - relativeIndex; } string cmd = $"{cmdPrefix}:{ledIndex}:{c.R},{c.G},{c.B}"; try { _serialLeds.WriteLine(cmd); } catch { } } }
        public void End(PluginManager pluginManager) { CloseSerialPorts(); if (joystick != null) { try { joystick.Unacquire(); joystick.Dispose(); } catch { } } if (directInput != null) directInput.Dispose(); this.SaveCommonSettings("GeneralSettings", Settings); }
        public System.Windows.Controls.Control GetWPFSettingsControl(PluginManager pluginManager) { return new SettingsControlDemo(this); }
    }
}