// -------------------------------------------------------------------------
// FILE: LogManager.cs
// VERSION: Fix errori 42 (Dedicated MergeGap Log Monitor)
// -------------------------------------------------------------------------
using System;
using System.IO;
using System.Linq;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;

namespace SimRIG
{
    public enum LogModule
    {
        SYSTEM,
        FUEL,
        STRATEGY,
        RADAR,
        OPPONENTS,
        HARDWARE,
        VOICE,
        MICROSECTOR,
        WEATHER,
        MERGEGAP,
        STRATEGY_SNAPSHOT,
        STRATEGY_EVENT
    }

    public enum LogType
    {
        EVENT,
        FLOW
    }

    /// <summary>
    /// Gestisce la scrittura asincrona dei log in formato CSV e del file dedicato MergeGap.
    /// Salva i file nella sottocartella Logs\SimRig Logs.
    /// </summary>
    public class LogManager
    {
        /// <summary>
        /// Intestazione dello snapshot strategico. Quest'ordine è vincolante: deve corrispondere
        /// esattamente all'array snapFields costruito in TargetStrategyManager.Update().
        /// Il confronto è verificato a runtime (guardia in Update) e dai test.
        /// </summary>
        public static readonly string SnapshotHeader =
            "SessionTime,Lap,MacroSector,Target,SignedGap,PlayerPit,TargetPit,RaceLapsRem," +
            "PrevGap,DeltaGap,GapDeltaValid,DeltaTime,InstantPace,RelativePace,SeqValid,InvalidReason,PitSeedPending,PostPitSeed," +
            "PlayerFuel,TargetFuel,TargetLapsUntilPit,ReactionLaps,TargetDegPace,PlayerFreshPace,PrePitGain," +
            "TargetPitLoss,PlayerPitLoss,NetPitAdv,PlayerWarmup,PositiveGap,UndercutAdv,UndercutMargin," +
            "UndercutPosOK,TargetNeedsPit,UndercutFuelOK,UndercutTrafOK,UndercutMarginOK,UndercutRaceLapsOK,UndercutViable,UndercutRejectReason," +
            "WarmupW0,WarmupW1,WarmupW2,WarmupFallback,WarmupAvailable,MaxStayLaps,NEffective," +
            "TargetRawPace,PlayerTrackPace,StayOutsideGain,TotalWarmupGain,OvercutAdv,OvercutMargin," +
            "TargetIsInPit,TargetPittedRecently,OvercutFuelOK,OvercutTrafOK,OvercutStayOK,OvercutMarginOK,OvercutRaceLapsOK,OvercutViable,OvercutRejectReason," +
            "StrategyDecision," +
            "CandidateDecision,TimeInDecision,MinDeltaTime,MaxDeltaTime";

        /// <summary>Numero di colonne dello snapshot, derivato dall'header stesso.</summary>
        public static int SnapshotColumnCount { get { return SnapshotHeader.Split(',').Length; } }

        private string _logFilePath;
        private string _mergeGapLogFilePath;
        private ConcurrentQueue<string> _logQueue = new ConcurrentQueue<string>();
        private ConcurrentQueue<string> _mergeGapQueue = new ConcurrentQueue<string>();
        private ConcurrentQueue<string> _strategySnapshotQueue = new ConcurrentQueue<string>();
        private ConcurrentQueue<string> _strategyEventQueue = new ConcurrentQueue<string>();
        private string _strategySnapshotLogFilePath;
        private string _strategyEventLogFilePath;
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private Task _writerTask;
        private SessionState _sessionState;

        public const string StrategyEngineVersion = "1.2.0";

        // Stato degli header: se una scrittura fallisce all'avvio si riprova, ma solo finché
        // il file è ancora vuoto (vedi TryWriteHeader).
        private bool _debugHeaderOk;
        private bool _mergeGapHeaderOk;
        private bool _snapshotHeaderOk;
        private bool _eventHeaderOk;

        private readonly ConcurrentDictionary<string, int> _failureCounts = new ConcurrentDictionary<string, int>();

        /// <summary>Ultimo errore di scrittura registrato, o null. Diagnostica leggibile dai test.</summary>
        public string LastLogFailure { get; private set; }

        public bool StrategyHeadersWritten { get { return _snapshotHeaderOk && _eventHeaderOk; } }

        /// <summary>Stato di sessione che corrisponde alla gara in corso (bandiera verde).</summary>
        public const int RaceSessionStateStatus = 4;

        /// <summary>
        /// I log strategici hanno senso solo a gara in corso. Griglia, formazione, pausa e
        /// post-bandiera producono telemetria che non descrive nulla di strategico: gap congelati,
        /// clock di sessione a −1 o a 0, campioni duplicati. Nel replay del 19/08 erano 71 righe
        /// di rumore su 1021, e generavano un delta di −28.8 s calcolato su una finestra di 146 s
        /// in cui la sessione era di fatto ferma.
        ///
        /// Con <c>_sessionState</c> nullo (test) non si filtra nulla: senza telemetria non c'è
        /// stato da valutare.
        /// </summary>
        private bool IsRaceRunning
        {
            get { return _sessionState == null || _sessionState.SessionStateStatus == RaceSessionStateStatus; }
        }

        /// <summary>Righe strategiche scartate perché fuori dalla finestra di gara.</summary>
        public int StrategyLinesSkippedOutsideRace { get; private set; }

        // Interruttori di debug collegati alla UI
        public bool EnableLogFuel { get; set; } = false;
        public bool EnableLogStrategy { get; set; } = false;
        public bool EnableLogRadar { get; set; } = false;
        public bool EnableLogOpponents { get; set; } = false;
        public bool EnableLogMicrosector { get; set; } = false;
        public bool EnableLogSystem { get; set; } = true;
        public bool EnableLogWeather { get; set; } = false;
        public bool EnableLogHardware { get; set; } = false;
        public bool EnableLogVoice { get; set; } = true;
        public bool EnableLogMergeGap { get; set; } = true;

        /// <param name="logDirectoryOverride">
        /// Solo per i test: reindirizza i log in una cartella temporanea. In produzione resta null
        /// e si usa <c>{BaseDirectory}\Logs\SimRig Logs</c>.
        /// </param>
        public LogManager(SessionState state, string logDirectoryOverride = null)
        {
            _sessionState = state;

            string logDir = logDirectoryOverride
                ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs", "SimRig Logs");

            if (!Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }

            string dateStr = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            _logFilePath = Path.Combine(logDir, $"SimRIG_DebugLog_{dateStr}.csv");
            _mergeGapLogFilePath = Path.Combine(logDir, $"SimRIG_MergeGapLog_{dateStr}.txt");
            _strategySnapshotLogFilePath = Path.Combine(logDir, $"SimRIG_StrategySnapshot_{dateStr}.csv");
            _strategyEventLogFilePath = Path.Combine(logDir, $"SimRIG_StrategyEvent_{dateStr}.txt");

            WriteHeaders();
            StartWriterTask();
        }

        /// <summary>
        /// Parametri di modello in testa a entrambi i file strategy. Le costanti del RelativePace
        /// sono lette dalle definizioni reali: se qualcuno cambia Alpha nel tracker, l'header lo segue.
        /// Funzione pura, senza I/O, così i test possono verificarla senza toccare il filesystem.
        /// </summary>
        public static string BuildModelParamsHeader()
        {
            var c = System.Globalization.CultureInfo.InvariantCulture;
            return "# StrategyEngineVersion=" + StrategyEngineVersion + "\n" +
                   "# RelativePaceAlpha=" + RelativePaceTracker.Alpha.ToString(c) + "\n" +
                   "# RelativePaceBeta=" + RelativePaceTracker.Beta.ToString(c) + "\n" +
                   "# RelativePaceClamp=" + RelativePaceTracker.ClampLimit.ToString(c) + "\n" +
                   "# MinimumDeltaTime=" + RelativePaceTracker.MinimumDeltaTime.ToString(c) + "\n" +
                   "# MinSectorFraction=" + RelativePaceTracker.MinSectorFraction.ToString(c) + "\n" +
                   "# MaxSectorFraction=" + RelativePaceTracker.MaxSectorFraction.ToString(c) + "\n" +
                   "# PostPitSettlingSectors=" + RelativePaceTracker.PostPitSettlingSectors.ToString(c) + "\n" +
                   "# PitDecisionBuffer=0.8\n" +
                   "# MaxUndercutReactionWindow=1.0\n" +
                   "# WarmupThreshold=0.10\n" +
                   "# FuelReserve=0.4\n" +
                   "# UndercutPositionThreshold=" + StrategyGateHysteresis.UndercutPositionThreshold.ToString(c) + "\n" +
                   "# PositionHysteresis=" + StrategyGateHysteresis.PositionHysteresis.ToString(c) + "\n" +
                   "# MarginHysteresis=" + StrategyGateHysteresis.MarginHysteresis.ToString(c) + "\n" +
                   "# MinimumStateDwell=" + StrategyGateHysteresis.MinimumStateDwell.ToString(c) + "\n" +
                   "# TargetPittedRecentlyThreshold=2.0\n" +
                   "# MinimumOvercutStay=0.5\n" +
                   "# MinimumRaceLapsRemaining=2.0\n";
        }

        /// <summary>
        /// Una riga di Strategy Event autoesplicativa:
        /// <c>sessionTimeLeft | lap | wallClock | EVENT_NAME | payload</c>.
        ///
        /// Il <b>session time guida</b>, non l'orologio di sistema: è l'unica base temporale
        /// coerente quando un replay viene riprodotto accelerato, ed è la stessa chiave della
        /// prima colonna dello snapshot CSV, quindi i due file si incrociano riga per riga.
        /// Il wall clock resta in terza posizione per correlare con il log di SimHub.
        ///
        /// Funzione pura per renderla testabile senza filesystem né SessionState.
        /// </summary>
        public static string FormatStrategyEventLine(string wallClock, string sessionTime, string lap,
                                                     string message, string data)
        {
            string line = sessionTime + " | " + lap + " | " + wallClock + " | " + Flatten(message);
            string payload = Flatten(data);
            if (payload.Length > 0) line += " | " + payload;
            return line;
        }

        private string CurrentSessionTimeText()
        {
            return _sessionState != null
                ? _sessionState.SessionTimeLeftSec.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)
                : "0.0";
        }

        private string CurrentLapText()
        {
            return _sessionState != null
                ? _sessionState.CurrentLap.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : "0";
        }

        /// <summary>Rimuove i ritorni a capo: una riga di log deve restare una riga sola.</summary>
        private static string Flatten(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Replace("\r", string.Empty).Replace("\n", " ").Trim();
        }

        /// <summary>
        /// Scrive gli header. Ogni file è isolato: il fallimento di uno non può impedire agli altri
        /// di essere scritti. Era esattamente il difetto che lasciava snapshot ed event senza header
        /// perché la prima WriteAllText falliva dentro un unico try globale.
        /// </summary>
        private void WriteHeaders()
        {
            EnsureDebugHeader();
            EnsureMergeGapHeader();
            EnsureStrategySnapshotHeader();
            EnsureStrategyEventHeader();
        }

        /// <summary>
        /// Idempotente per costruzione: scrive solo se il file è assente o vuoto. Non può quindi
        /// duplicare un header né troncare dati già accodati, anche se richiamata più volte.
        /// </summary>
        private bool TryWriteHeader(string path, string content, string label)
        {
            try
            {
                if (File.Exists(path) && new FileInfo(path).Length > 0) return true;
                File.WriteAllText(path, content);
                return true;
            }
            catch (Exception ex)
            {
                ReportLogFailure(label + " header", ex);
                return false;
            }
        }

        /// <summary>
        /// Riscrive l'header se il file è assente o vuoto. Va invocata **prima di ogni append**,
        /// non una volta sola all'avvio: fra la costruzione del LogManager e la prima scrittura
        /// di dati passano decine di secondi, durante i quali il file può sparire — basta che
        /// qualcuno svuoti la cartella dei log per tenerla in ordine. Con un flag "scritto una
        /// volta" il file tornerebbe senza intestazione e nessuno se ne accorgerebbe.
        ///
        /// Il costo è un File.Exists + FileInfo.Length per ciclo da 500 ms, solo quando c'è
        /// qualcosa da scrivere. Nulla di tutto questo tocca il percorso a 60 Hz.
        /// </summary>
        private void EnsureStrategySnapshotHeader()
        {
            _snapshotHeaderOk = TryWriteHeader(_strategySnapshotLogFilePath,
                BuildModelParamsHeader() + SnapshotHeader + "\n", "StrategySnapshot");
        }

        private void EnsureStrategyEventHeader()
        {
            _eventHeaderOk = TryWriteHeader(_strategyEventLogFilePath,
                BuildModelParamsHeader() +
                "# Formato: sessionTimeLeft | lap | wallClock | EVENT | payload\n" +
                "========================================================================================\n\n", "StrategyEvent");
        }

        private void EnsureDebugHeader()
        {
            _debugHeaderOk = TryWriteHeader(_logFilePath,
                "Timestamp;SessionTime;Lap;Type;Module;Message;Data\n", "DebugLog");
        }

        private void EnsureMergeGapHeader()
        {
            _mergeGapHeaderOk = TryWriteHeader(_mergeGapLogFilePath, MergeGapBanner, "MergeGapLog");
        }

        private const string MergeGapBanner =
            "========================================================================================\n" +
            "                 SIMRIG MERGEGAP STRATEGY MONITOR (10s DEDICATED LOG)                  \n" +
            "========================================================================================\n\n";

        /// <summary>
        /// Diagnostica dei fallimenti di scrittura. Non solleva mai: un errore di logging non deve
        /// propagarsi nel runtime SimHub. Segnala la prima occorrenza e poi una ogni 100, per non
        /// inondare il log di SimHub se il problema è persistente.
        /// </summary>
        private void ReportLogFailure(string label, Exception ex)
        {
            int count = _failureCounts.AddOrUpdate(label, 1, (k, v) => v + 1);
            LastLogFailure = $"{label}: {ex.GetType().Name} - {ex.Message}";

            if (count != 1 && count % 100 != 0) return;

            try
            {
                SimHub.Logging.Current.Error($"[SimRIG][LogManager] {LastLogFailure} (occorrenza {count})");
            }
            catch
            {
                // Nessun host SimHub (es. test runner): la diagnostica resta in LastLogFailure.
            }
        }

        private void StartWriterTask()
        {
            _writerTask = Task.Run(async () =>
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    // 1. Scrittura log CSV generale
                    if (!_logQueue.IsEmpty)
                    {
                        EnsureDebugHeader();
                        try
                        {
                            using (StreamWriter sw = File.AppendText(_logFilePath))
                            {
                                while (_logQueue.TryDequeue(out string logLine))
                                {
                                    sw.WriteLine(logLine);
                                }
                            }
                        }
                        catch (Exception ex) { ReportLogFailure("DebugLog write", ex); }
                    }

                    // 2. Scrittura log dedicato MergeGap
                    if (!_mergeGapQueue.IsEmpty)
                    {
                        EnsureMergeGapHeader();
                        try
                        {
                            using (StreamWriter sw = File.AppendText(_mergeGapLogFilePath))
                            {
                                while (_mergeGapQueue.TryDequeue(out string mergeLine))
                                {
                                    sw.WriteLine(mergeLine);
                                }
                            }
                        }
                        catch (Exception ex) { ReportLogFailure("MergeGapLog write", ex); }
                    }

                    // 3. Scrittura Strategy Snapshot
                    if (!_strategySnapshotQueue.IsEmpty)
                    {
                        EnsureStrategySnapshotHeader();
                        try
                        {
                            using (StreamWriter sw = File.AppendText(_strategySnapshotLogFilePath))
                            {
                                while (_strategySnapshotQueue.TryDequeue(out string line))
                                {
                                    sw.WriteLine(line);
                                }
                            }
                        }
                        catch (Exception ex) { ReportLogFailure("StrategySnapshot write", ex); }
                    }

                    // 4. Scrittura Strategy Event
                    if (!_strategyEventQueue.IsEmpty)
                    {
                        EnsureStrategyEventHeader();
                        try
                        {
                            using (StreamWriter sw = File.AppendText(_strategyEventLogFilePath))
                            {
                                while (_strategyEventQueue.TryDequeue(out string line))
                                {
                                    sw.WriteLine(line);
                                }
                            }
                        }
                        catch (Exception ex) { ReportLogFailure("StrategyEvent write", ex); }
                    }

                    try
                    {
                        await Task.Delay(500, _cancellationTokenSource.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // Shutdown richiesto: uscita pulita. Lasciar propagare l'eccezione
                        // farebbe fallire il task e trasformerebbe ogni chiusura normale
                        // in un errore diagnostico.
                        break;
                    }
                }
            }, _cancellationTokenSource.Token);
        }

        public void Log(LogModule module, LogType type, string message, string data = "")
        {
            if (module == LogModule.MERGEGAP)
            {
                if (!EnableLogMergeGap) return;
                _mergeGapQueue.Enqueue(message);
                return;
            }

            if (module == LogModule.STRATEGY_SNAPSHOT || module == LogModule.STRATEGY_EVENT)
            {
                // Fuori dalla gara non si scrive: vedi IsRaceRunning.
                if (!IsRaceRunning)
                {
                    StrategyLinesSkippedOutsideRace++;
                    return;
                }
            }

            if (module == LogModule.STRATEGY_SNAPSHOT)
            {
                // Il messaggio È già la riga CSV completa: accodarci altro romperebbe
                // l'invariante delle colonne. Se arriva un data, è un errore del chiamante.
                if (!string.IsNullOrEmpty(data))
                {
                    ReportLogFailure("StrategySnapshot payload",
                        new ArgumentException("STRATEGY_SNAPSHOT non accetta un payload data: la riga è già completa."));
                }
                _strategySnapshotQueue.Enqueue(message);
                return;
            }

            if (module == LogModule.STRATEGY_EVENT)
            {
                // Il payload `data` porta reason, settore e tutti gli intermedi del tracker:
                // senza, l'event log si riduce a un elenco di nomi evento.
                _strategyEventQueue.Enqueue(FormatStrategyEventLine(
                    DateTime.Now.ToString("HH:mm:ss.fff"),
                    CurrentSessionTimeText(),
                    CurrentLapText(),
                    message,
                    data));
                return;
            }

            if (module == LogModule.FUEL && !EnableLogFuel) return;
            if (module == LogModule.STRATEGY && !EnableLogStrategy) return;
            if (module == LogModule.RADAR && !EnableLogRadar) return;
            if (module == LogModule.OPPONENTS && !EnableLogOpponents) return;
            if (module == LogModule.MICROSECTOR && !EnableLogMicrosector) return;
            if (module == LogModule.SYSTEM && !EnableLogSystem) return;
            if (module == LogModule.WEATHER && !EnableLogWeather) return;
            if (module == LogModule.HARDWARE && !EnableLogHardware) return;
            if (module == LogModule.VOICE && !EnableLogVoice) return;

            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            string sessTime = CurrentSessionTimeText();
            string lap = CurrentLapText();

            string safeMessage = message.Replace(";", ",").Replace("\n", " ").Replace("\r", "");
            string safeData = data.Replace(";", ",").Replace("\n", " ").Replace("\r", "");

            string csvLine = $"{timestamp};{sessTime};{lap};{type};{module};{safeMessage};{safeData}";
            _logQueue.Enqueue(csvLine);
        }

        public void Shutdown()
        {
            _cancellationTokenSource.Cancel();
            try
            {
                _writerTask?.Wait(1000);
            }
            catch (AggregateException ex) when (ex.InnerExceptions.All(e => e is OperationCanceledException))
            {
                // Chiusura normale: il writer task è stato cancellato. Non è un errore, e
                // segnalarlo inonderebbe il log di SimHub a ogni sessione.
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { ReportLogFailure("writer shutdown", ex); }

            // Una sessione brevissima potrebbe non aver mai raggiunto un ciclo del writer task.
            WriteHeaders();

            while (_logQueue.TryDequeue(out string logLine))
            {
                try
                {
                    using (StreamWriter sw = File.AppendText(_logFilePath))
                    {
                        sw.WriteLine(logLine);
                    }
                }
                catch (Exception ex) { ReportLogFailure("DebugLog flush", ex); }
            }

            while (_mergeGapQueue.TryDequeue(out string mergeLine))
            {
                try
                {
                    using (StreamWriter sw = File.AppendText(_mergeGapLogFilePath))
                    {
                        sw.WriteLine(mergeLine);
                    }
                }
                catch (Exception ex) { ReportLogFailure("MergeGapLog flush", ex); }
            }

            while (_strategySnapshotQueue.TryDequeue(out string line))
            {
                try
                {
                    using (StreamWriter sw = File.AppendText(_strategySnapshotLogFilePath))
                    {
                        sw.WriteLine(line);
                    }
                }
                catch (Exception ex) { ReportLogFailure("StrategySnapshot flush", ex); }
            }

            while (_strategyEventQueue.TryDequeue(out string evtLine))
            {
                try
                {
                    using (StreamWriter sw = File.AppendText(_strategyEventLogFilePath))
                    {
                        sw.WriteLine(evtLine);
                    }
                }
                catch (Exception ex) { ReportLogFailure("StrategyEvent flush", ex); }
            }
        }
    }
}
