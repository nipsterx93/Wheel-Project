// -------------------------------------------------------------------------
// FILE: StrategyLoggingUnitTests.cs
// Regressioni del logging strategico emerse dal replay reale del 2026-08-18:
//   A) l'event log conteneva solo i nomi evento, il payload `data` andava perso;
//   B) snapshot ed event log non avevano header perché un unico try/catch globale
//      veniva abortito dal fallimento della prima scrittura.
// I test scrivono in una cartella temporanea: nessuna dipendenza da E:\SimHub.
// -------------------------------------------------------------------------

using System;
using System.IO;
using System.Linq;
using SimRIG;

namespace User.PluginSdkDemo.Tests
{
    public class StrategyLoggingUnitTests
    {
        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception("[StrategyLogging] " + message);
        }

        public static void RunAllTests()
        {
            Console.WriteLine("[TEST] Running Strategy Logging Tests...");

            Test_EventLine_ContainsPayload();
            Test_EventLine_IsSingleLine();
            Test_ModelParamsHeader_ContainsRequiredParameters();
            Test_SnapshotHeader_ColumnCount();
            Test_HeadersAreWrittenToDisk();
            Test_HeadersNotDuplicatedOnSecondCall();
            Test_EventFile_ContainsPayloadOnDisk();
            Test_PostPitSequence_IsReconstructableFromEventLog();
            Test_CleanLifecycle_ReportsNoFailure();
            Test_HeadersSurviveFolderWipedMidSession();
            Test_StrategyLogsOnlyDuringRace();

            Console.WriteLine("[TEST SUCCESS] All Strategy Logging Tests Passed!");
        }

        // ===============================================================
        // A) payload dell'evento
        // ===============================================================

        private static void Test_EventLine_ContainsPayload()
        {
            string line = LogManager.FormatStrategyEventLine(
                "21:32:14.501", "1234.5", "7",
                "RELATIVE_PACE_INVALIDATION",
                "reason=PlayerInPit | sector=9 | seqValid=True | deltaTime=4.812 | frozenPace=-0.412");

            Assert(line.Contains("RELATIVE_PACE_INVALIDATION"), "manca il nome evento");
            Assert(line.Contains("reason=PlayerInPit"), "REGRESSIONE: il payload `data` è stato perso");
            Assert(line.Contains("sector=9"), "manca il settore nel payload");
            Assert(line.Contains("frozenPace=-0.412"), "manca il valore congelato nel payload");
            Assert(line.Contains("21:32:14.501"), "manca il wall clock");
            Assert(line.Contains("1234.5"), "manca il session time");
            Assert(line.Contains("| 7 |"), "manca il lap");

            // Il session time deve GUIDARE la riga: è l'unica base temporale coerente in un
            // replay accelerato, ed è la chiave che incrocia con la prima colonna dello snapshot.
            Assert(line.StartsWith("1234.5 | 7 | "),
                $"il session time deve essere il primo campo, non il wall clock: '{line}'");

            // Un evento senza payload non deve lasciare un separatore penzolante
            string bare = LogManager.FormatStrategyEventLine("21:32:14.501", "1234.5", "7", "STRATEGY_CHANGED", "");
            Assert(!bare.TrimEnd().EndsWith("|"), "evento senza payload: separatore finale spurio");

            Console.WriteLine("  [PASS] Test_EventLine_ContainsPayload");
        }

        private static void Test_EventLine_IsSingleLine()
        {
            string line = LogManager.FormatStrategyEventLine(
                "10:00:00.000", "10.0", "1", "EVT", "a=1\nb=2\r\nc=3");

            Assert(!line.Contains("\n") && !line.Contains("\r"),
                "una riga di log deve restare su una riga sola");
            Assert(line.Contains("a=1") && line.Contains("b=2") && line.Contains("c=3"),
                "l'appiattimento non deve perdere contenuto");

            Console.WriteLine("  [PASS] Test_EventLine_IsSingleLine");
        }

        // ===============================================================
        // B) header
        // ===============================================================

        private static void Test_ModelParamsHeader_ContainsRequiredParameters()
        {
            string header = LogManager.BuildModelParamsHeader();

            string[] required =
            {
                "# StrategyEngineVersion=", "# RelativePaceAlpha=0.3", "# RelativePaceBeta=0.7",
                "# RelativePaceClamp=10", "# MinimumDeltaTime=1", "# PitDecisionBuffer=0.8",
                "# MaxUndercutReactionWindow=1.0", "# WarmupThreshold=0.10", "# FuelReserve=0.4",
                "# UndercutPositionThreshold=-0.5", "# TargetPittedRecentlyThreshold=2.0",
                "# MinimumOvercutStay=0.5", "# MinimumRaceLapsRemaining=2.0"
            };

            foreach (string token in required)
            {
                Assert(header.Contains(token), $"parametro di modello mancante nell'header: {token}");
            }

            // I valori devono venire dalle costanti reali, non da stringhe ricopiate
            Assert(RelativePaceTracker.Alpha == 0.30 && RelativePaceTracker.Beta == 0.70,
                "le costanti del tracker sono cambiate: aggiornare il test, non solo l'header");

            Console.WriteLine("  [PASS] Test_ModelParamsHeader_ContainsRequiredParameters");
        }

        private static void Test_SnapshotHeader_ColumnCount()
        {
            Assert(LogManager.SnapshotColumnCount == 68,
                $"snapshot: attese 68 colonne, trovate {LogManager.SnapshotColumnCount}");

            string[] columns = LogManager.SnapshotHeader.Split(',');
            Assert(columns.Distinct().Count() == columns.Length, "l'header snapshot contiene nomi duplicati");
            Assert(columns.All(c => c.Trim().Length > 0), "l'header snapshot contiene una colonna vuota");

            Console.WriteLine("  [PASS] Test_SnapshotHeader_ColumnCount");
        }

        private static void Test_HeadersAreWrittenToDisk()
        {
            string dir = CreateTempDir();
            try
            {
                var log = new LogManager(null, dir);
                log.Shutdown();

                Assert(log.LastLogFailure == null, $"diagnostica inattesa all'avvio: {log.LastLogFailure}");

                string snapshot = ReadSingle(dir, "SimRIG_StrategySnapshot_*.csv");
                string events = ReadSingle(dir, "SimRIG_StrategyEvent_*.txt");
                string debug = ReadSingle(dir, "SimRIG_DebugLog_*.csv");

                Assert(snapshot.StartsWith("# StrategyEngineVersion="),
                    "REGRESSIONE: lo snapshot non inizia con i model params");
                Assert(snapshot.Contains(LogManager.SnapshotHeader),
                    "REGRESSIONE: lo snapshot non contiene SnapshotHeader");
                Assert(events.StartsWith("# StrategyEngineVersion="),
                    "REGRESSIONE: l'event log non inizia con i model params");
                Assert(debug.Length > 0, "il DebugLog non è stato creato");

                // La prima riga non commentata dello snapshot deve essere l'header delle colonne
                string firstDataLine = snapshot.Split('\n').First(l => !l.StartsWith("#") && l.Trim().Length > 0);
                Assert(firstDataLine.TrimEnd('\r') == LogManager.SnapshotHeader,
                    "la prima riga non commentata dello snapshot non è l'header delle colonne");

                Console.WriteLine("  [PASS] Test_HeadersAreWrittenToDisk");
            }
            finally { Cleanup(dir); }
        }

        /// <summary>
        /// Il retry degli header deve essere idempotente: nessuna duplicazione, e soprattutto
        /// nessun troncamento dei dati già scritti.
        /// </summary>
        private static void Test_HeadersNotDuplicatedOnSecondCall()
        {
            string dir = CreateTempDir();
            try
            {
                var log = new LogManager(null, dir);
                log.Log(LogModule.STRATEGY_EVENT, LogType.EVENT, "EVT_UNO", "k=1");
                log.Shutdown();

                string events = ReadSingle(dir, "SimRIG_StrategyEvent_*.txt");
                int occurrences = CountOccurrences(events, "# StrategyEngineVersion=");

                Assert(occurrences == 1, $"header duplicato nell'event log: {occurrences} occorrenze");
                Assert(events.Contains("EVT_UNO"), "il retry header ha troncato i dati già scritti");
                Assert(events.Contains("k=1"), "il payload è andato perso");

                Console.WriteLine("  [PASS] Test_HeadersNotDuplicatedOnSecondCall");
            }
            finally { Cleanup(dir); }
        }

        private static void Test_EventFile_ContainsPayloadOnDisk()
        {
            string dir = CreateTempDir();
            try
            {
                var log = new LogManager(null, dir);
                log.Log(LogModule.STRATEGY_EVENT, LogType.EVENT, "RELATIVE_PACE_INVALIDATION",
                    "reason=TargetInPit | sector=12 | seqValid=True | deltaTime=5.000 | frozenPace=0.250");
                log.Shutdown();

                string events = ReadSingle(dir, "SimRIG_StrategyEvent_*.txt");
                string line = events.Split('\n')
                                    .FirstOrDefault(l => l.Contains("RELATIVE_PACE_INVALIDATION"));

                Assert(line != null, "l'evento non è finito sul file");
                Assert(line.Contains("reason=TargetInPit"),
                    "REGRESSIONE A: il payload `data` non arriva su disco");
                Assert(line.Contains("sector=12"), "settore assente su disco");

                // Formato: timestamp | sessionTime | lap | EVENT | payload
                string[] parts = line.Split('|');
                Assert(parts.Length >= 5, $"formato riga inatteso, {parts.Length} campi: {line}");
                Assert(parts[3].Trim() == "RELATIVE_PACE_INVALIDATION",
                    $"il nome evento non è nel quarto campo: '{parts[3].Trim()}'");

                Console.WriteLine("  [PASS] Test_EventFile_ContainsPayloadOnDisk");
            }
            finally { Cleanup(dir); }
        }

        /// <summary>
        /// La sequenza post-pit deve essere ricostruibile leggendo il solo event log:
        /// è il criterio con cui si verifica il fix RED-1 in un replay reale.
        /// </summary>
        private static void Test_PostPitSequence_IsReconstructableFromEventLog()
        {
            string dir = CreateTempDir();
            try
            {
                var log = new LogManager(null, dir);

                log.Log(LogModule.STRATEGY_EVENT, LogType.EVENT, "RELATIVE_PACE_INVALIDATION",
                    "reason=PlayerInPit | sector=9 | seqValid=False | deltaTime=4.500 | frozenPace=-0.380");
                log.Log(LogModule.STRATEGY_EVENT, LogType.EVENT, "RELATIVE_PACE_POST_PIT_SEED",
                    "sector=10 | gap=28.000 | frozenPace=-0.380 | note=primo campione pulito post-pit, nessun rate calcolato");
                log.Log(LogModule.STRATEGY_EVENT, LogType.EVENT, "RELATIVE_PACE_SEED",
                    "sector=11 | gap=28.300 | instantRate=0.600 | emaAfter=0.600 | clamped=False");
                log.Shutdown();

                string events = ReadSingle(dir, "SimRIG_StrategyEvent_*.txt");

                Assert(events.Contains("reason=PlayerInPit"), "fase pit non distinguibile");
                Assert(events.Contains("RELATIVE_PACE_POST_PIT_SEED"), "seed post-pit assente");
                Assert(events.Contains("gap=28.000"), "il gap del seed post-pit non è ricostruibile");
                Assert(events.Contains("instantRate=0.600"), "gli intermedi del primo rate pulito sono persi");

                // L'ordine deve essere preservato: invalidazione -> seed post-pit -> primo rate
                int idxPit = events.IndexOf("reason=PlayerInPit", StringComparison.Ordinal);
                int idxSeed = events.IndexOf("RELATIVE_PACE_POST_PIT_SEED", StringComparison.Ordinal);
                int idxRate = events.IndexOf("instantRate=0.600", StringComparison.Ordinal);
                Assert(idxPit < idxSeed && idxSeed < idxRate,
                    "l'ordine degli eventi post-pit non è preservato nel file");

                Console.WriteLine("  [PASS] Test_PostPitSequence_IsReconstructableFromEventLog");
            }
            finally { Cleanup(dir); }
        }

        /// <summary>
        /// Un ciclo di vita normale non deve produrre alcuna diagnostica di errore. Senza questo
        /// test la cancellazione del writer task veniva segnalata come fallimento a ogni chiusura,
        /// inondando il log di SimHub di errori inesistenti.
        /// </summary>
        private static void Test_CleanLifecycle_ReportsNoFailure()
        {
            string dir = CreateTempDir();
            try
            {
                var log = new LogManager(null, dir);
                log.Log(LogModule.STRATEGY_EVENT, LogType.EVENT, "EVT", "k=1");
                log.Log(LogModule.STRATEGY_SNAPSHOT, LogType.FLOW, "1,2,3");
                Assert(log.StrategyHeadersWritten, "gli header strategy non risultano scritti");

                log.Shutdown();

                Assert(log.LastLogFailure == null,
                    $"ciclo di vita pulito ma diagnostica emessa: {log.LastLogFailure}");

                Console.WriteLine("  [PASS] Test_CleanLifecycle_ReportsNoFailure");
            }
            finally { Cleanup(dir); }
        }

        /// <summary>
        /// Scenario reale del replay 20260818_230037: fra la costruzione del LogManager e la prima
        /// scrittura di dati passano ~28 secondi, e in quella finestra la cartella dei log viene
        /// svuotata a mano. Con un flag "header già scritto" i file tornavano privi di intestazione
        /// e restavano così per tutta la sessione. L'header va garantito prima di ogni append.
        /// </summary>
        private static void Test_HeadersSurviveFolderWipedMidSession()
        {
            string dir = CreateTempDir();
            try
            {
                var log = new LogManager(null, dir);
                Assert(log.StrategyHeadersWritten, "header non scritti alla costruzione");

                // Qualcuno svuota la cartella mentre la sessione è in corso
                foreach (string f in Directory.GetFiles(dir)) File.Delete(f);
                Assert(Directory.GetFiles(dir).Length == 0, "la cartella doveva restare vuota");

                // Poi arrivano i dati
                log.Log(LogModule.STRATEGY_EVENT, LogType.EVENT, "RELATIVE_PACE_UPDATE", "gapDelta=-0.200");
                log.Log(LogModule.STRATEGY_SNAPSHOT, LogType.FLOW, "riga,dati,snapshot");
                log.Shutdown();

                string events = ReadSingle(dir, "SimRIG_StrategyEvent_*.txt");
                string snapshot = ReadSingle(dir, "SimRIG_StrategySnapshot_*.csv");

                Assert(events.StartsWith("# StrategyEngineVersion="),
                    "REGRESSIONE: l'event log ricreato dopo lo svuotamento non ha l'header");
                Assert(snapshot.StartsWith("# StrategyEngineVersion="),
                    "REGRESSIONE: lo snapshot ricreato dopo lo svuotamento non ha l'header");
                Assert(snapshot.Contains(LogManager.SnapshotHeader),
                    "lo snapshot ricreato non contiene l'header delle colonne");

                // I dati arrivati dopo devono esserci comunque
                Assert(events.Contains("gapDelta=-0.200"), "dati persi nell'event log");
                Assert(snapshot.Contains("riga,dati,snapshot"), "dati persi nello snapshot");

                // E l'header non deve essere duplicato
                Assert(CountOccurrences(events, "# StrategyEngineVersion=") == 1,
                    "header duplicato nell'event log dopo il ripristino");

                Console.WriteLine("  [PASS] Test_HeadersSurviveFolderWipedMidSession");
            }
            finally { Cleanup(dir); }
        }

        /// <summary>
        /// I log strategici devono coprire solo la gara. Nel replay del 19/08 le fasi fuori gara
        /// producevano 71 righe di rumore su 1021: griglia con SessionTime a −1, post-bandiera
        /// con SessionTime a 0, e una coda di campioni identici a sessione ferma. Da quella
        /// finestra nasceva anche un delta di −28.8 s calcolato su 146 s di sessione immobile.
        /// </summary>
        private static void Test_StrategyLogsOnlyDuringRace()
        {
            string dir = CreateTempDir();
            try
            {
                var state = new SessionState();
                var log = new LogManager(state, dir);

                // Pre-gara: griglia / formazione
                state.SessionStateStatus = 3;
                log.Log(LogModule.STRATEGY_EVENT, LogType.EVENT, "EVT_PREGARA", "k=pre");
                log.Log(LogModule.STRATEGY_SNAPSHOT, LogType.FLOW, "riga,pre,gara");

                // Gara
                state.SessionStateStatus = LogManager.RaceSessionStateStatus;
                log.Log(LogModule.STRATEGY_EVENT, LogType.EVENT, "EVT_GARA", "k=race");
                log.Log(LogModule.STRATEGY_SNAPSHOT, LogType.FLOW, "riga,in,gara");

                // Post-bandiera
                state.SessionStateStatus = 5;
                log.Log(LogModule.STRATEGY_EVENT, LogType.EVENT, "EVT_POSTGARA", "k=post");
                log.Log(LogModule.STRATEGY_SNAPSHOT, LogType.FLOW, "riga,post,gara");

                log.Shutdown();

                string events = ReadSingle(dir, "SimRIG_StrategyEvent_*.txt");
                string snapshot = ReadSingle(dir, "SimRIG_StrategySnapshot_*.csv");

                Assert(events.Contains("EVT_GARA"), "l'evento in gara deve essere scritto");
                Assert(snapshot.Contains("riga,in,gara"), "lo snapshot in gara deve essere scritto");

                Assert(!events.Contains("EVT_PREGARA"), "REGRESSIONE: evento pre-gara scritto");
                Assert(!events.Contains("EVT_POSTGARA"), "REGRESSIONE: evento post-gara scritto");
                Assert(!snapshot.Contains("riga,pre,gara"), "REGRESSIONE: snapshot pre-gara scritto");
                Assert(!snapshot.Contains("riga,post,gara"), "REGRESSIONE: snapshot post-gara scritto");

                Assert(log.StrategyLinesSkippedOutsideRace == 4,
                    $"attese 4 righe scartate, contate {log.StrategyLinesSkippedOutsideRace}");

                // Gli header restano: servono anche a un file che conterrà solo la gara
                Assert(events.StartsWith("# StrategyEngineVersion="), "header event perso");
                Assert(snapshot.StartsWith("# StrategyEngineVersion="), "header snapshot perso");

                Console.WriteLine("  [PASS] Test_StrategyLogsOnlyDuringRace");
            }
            finally { Cleanup(dir); }
        }

        // ===============================================================
        // helper
        // ===============================================================

        private static string CreateTempDir()
        {
            string dir = Path.Combine(Path.GetTempPath(), "SimRIGLogTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static void Cleanup(string dir)
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
            catch { /* cartella temporanea: un residuo non deve far fallire il test */ }
        }

        private static string ReadSingle(string dir, string pattern)
        {
            string[] files = Directory.GetFiles(dir, pattern);
            Assert(files.Length == 1, $"attesi 1 file per '{pattern}', trovati {files.Length}");
            using (var fs = new FileStream(files[0], FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sr = new StreamReader(fs))
            {
                return sr.ReadToEnd();
            }
        }

        private static int CountOccurrences(string haystack, string needle)
        {
            int count = 0, index = 0;
            while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += needle.Length;
            }
            return count;
        }
    }
}
