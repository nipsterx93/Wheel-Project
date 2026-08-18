// -------------------------------------------------------------------------
// FILE: LogManager.cs
// VERSION: Fix errori 42 (Dedicated MergeGap Log Monitor)
// -------------------------------------------------------------------------
using System;
using System.IO;
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
            "PrevGap,DeltaGap,DeltaTime,InstantPace,RelativePace,SeqValid,InvalidReason,PitSeedPending,PostPitSeed," +
            "PlayerFuel,TargetFuel,TargetLapsUntilPit,ReactionLaps,TargetDegPace,PlayerFreshPace,PrePitGain," +
            "TargetPitLoss,PlayerPitLoss,NetPitAdv,PlayerWarmup,PositiveGap,UndercutAdv,UndercutMargin," +
            "UndercutPosOK,TargetNeedsPit,UndercutFuelOK,UndercutTrafOK,UndercutMarginOK,UndercutRaceLapsOK,UndercutViable,UndercutRejectReason," +
            "WarmupW0,WarmupW1,WarmupW2,WarmupFallback,WarmupAvailable,MaxStayLaps,NEffective," +
            "TargetRawPace,PlayerTrackPace,StayOutsideGain,TotalWarmupGain,OvercutAdv,OvercutMargin," +
            "TargetIsInPit,TargetPittedRecently,OvercutFuelOK,OvercutTrafOK,OvercutStayOK,OvercutMarginOK,OvercutRaceLapsOK,OvercutViable,OvercutRejectReason," +
            "StrategyDecision";

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

        public LogManager(SessionState state)
        {
            _sessionState = state;
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            // Creazione della cartella Logs/SimRig Logs se non esiste
            string logDir = Path.Combine(baseDir, "Logs", "SimRig Logs");
            if (!Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }

            string dateStr = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            _logFilePath = Path.Combine(logDir, $"SimRIG_DebugLog_{dateStr}.csv");
            _mergeGapLogFilePath = Path.Combine(logDir, $"SimRIG_MergeGapLog_{dateStr}.txt");
            _strategySnapshotLogFilePath = Path.Combine(logDir, $"SimRIG_StrategySnapshot_{dateStr}.csv");
            _strategyEventLogFilePath = Path.Combine(logDir, $"SimRIG_StrategyEvent_{dateStr}.txt");

            WriteHeader();
            StartWriterTask();
        }

        private void WriteHeader()
        {
            try
            {
                File.WriteAllText(_logFilePath, "Timestamp;SessionTime;Lap;Type;Module;Message;Data\n");
                File.WriteAllText(_mergeGapLogFilePath, "========================================================================================\n" +
                                                        "                 SIMRIG MERGEGAP STRATEGY MONITOR (10s DEDICATED LOG)                  \n" +
                                                        "========================================================================================\n\n");

                // Costanti lette dalle definizioni reali, non ricopiate a mano: se qualcuno
                // cambia Alpha nel tracker, l'header lo segue.
                string inv(double v) { return v.ToString(System.Globalization.CultureInfo.InvariantCulture); }

                string modelParams = "# StrategyEngineVersion=1.1.0\n" +
                                     "# RelativePaceAlpha=" + inv(RelativePaceTracker.Alpha) + "\n" +
                                     "# RelativePaceBeta=" + inv(RelativePaceTracker.Beta) + "\n" +
                                     "# RelativePaceClamp=" + inv(RelativePaceTracker.ClampLimit) + "\n" +
                                     "# MinimumDeltaTime=" + inv(RelativePaceTracker.MinimumDeltaTime) + "\n" +
                                     "# PitDecisionBuffer=0.8\n" +
                                     "# MaxUndercutReactionWindow=1.0\n" +
                                     "# WarmupThreshold=0.10\n" +
                                     "# FuelReserve=0.4\n" +
                                     "# UndercutPositionThreshold=-0.5\n" +
                                     "# TargetPittedRecentlyThreshold=2.0\n" +
                                     "# MinimumOvercutStay=0.5\n" +
                                     "# MinimumRaceLapsRemaining=2.0\n";

                File.WriteAllText(_strategySnapshotLogFilePath, modelParams + SnapshotHeader + "\n");
                File.WriteAllText(_strategyEventLogFilePath, modelParams + "========================================================================================\n\n");
            }
            catch { }
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
                        catch { /* Ignoriamo lock temporanei */ }
                    }

                    // 2. Scrittura log dedicato MergeGap
                    if (!_mergeGapQueue.IsEmpty)
                    {
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
                        catch { /* Ignoriamo lock temporanei */ }
                    }

                    // 3. Scrittura Strategy Snapshot
                    if (!_strategySnapshotQueue.IsEmpty)
                    {
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
                        catch { }
                    }

                    // 4. Scrittura Strategy Event
                    if (!_strategyEventQueue.IsEmpty)
                    {
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
                        catch { }
                    }

                    await Task.Delay(500, _cancellationTokenSource.Token).ConfigureAwait(false);
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

            if (module == LogModule.STRATEGY_SNAPSHOT)
            {
                _strategySnapshotQueue.Enqueue(message);
                return;
            }

            if (module == LogModule.STRATEGY_EVENT)
            {
                _strategyEventQueue.Enqueue(message);
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
            string sessTime = _sessionState != null ? _sessionState.SessionTimeLeftSec.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) : "0.0";
            string lap = _sessionState != null ? _sessionState.CurrentLap.ToString() : "0";

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
            catch { }

            while (_logQueue.TryDequeue(out string logLine))
            {
                try
                {
                    using (StreamWriter sw = File.AppendText(_logFilePath))
                    {
                        sw.WriteLine(logLine);
                    }
                }
                catch { }
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
                catch { }
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
                catch { }
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
                catch { }
            }
        }
    }
}
