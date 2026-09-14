// -------------------------------------------------------------------------
// FILE: PitExitTrafficDiagnosticsUnitTests.cs
// Y-62 — diagnostica del controllo traffico al rientro (TargetStrategyManager, ciclo traffico)
//
// Il controllo di oggi stima il distacco di ogni avversario come differenza di posizione x
// passo del Player, e chiede che la vettura nella bolla di +-3 s sia ADESSO vicina all'uscita
// box estesa. Sul replay Daytona 094551 scatta una volta per giro, col Player fra i
// macrosettori 8 e 11, e i log non dicono quale vettura sia. Due ipotesi:
//   H1 - vettura vera al bordo della bolla, che la stima posizione x passo fa entrare a meta' giro;
//   H2 - vettura NotInWorld con la posizione ferma all'ultimo valore memorizzato, il cui distacco
//        scorre attraverso la bolla una volta per giro.
// La diagnostica scrive, per ogni vettura vicina alla bolla, la sorgente della posizione e il
// distacco vero letto dai timestamp del Player. Qui si provano i pezzi puri.
//
// Numeri veri, DebugLog 094551 (RaceProjectionsDiagnostics, giro 2):
//   TL 2507.6 PosPct 0.4393 | 2505.6 0.4602 | 2503.5 0.4829 | 2501.5 0.5065 | 2499.5 0.5310 | 2497.4 0.5575
//   primo UndercutTrafOK=False del run a TL 2501.9; passo normalizzato del Player dal giro 4: 105.675 s
//   uscita box 0.102 + ExclusionMargin 0.05 = uscita estesa 0.152
// -------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using SimRIG;

namespace User.PluginSdkDemo.Tests
{
    public class PitExitTrafficDiagnosticsUnitTests
    {
        private static readonly double[] PlayerClock094551 = { 2507.6, 2505.6, 2503.5, 2501.5, 2499.5, 2497.4 };
        private static readonly double[] PlayerPos094551 = { 0.4393, 0.4602, 0.4829, 0.5065, 0.5310, 0.5575 };
        private const double RefLapTime094551 = 105.675;
        private const double ExtendedPitExitDaytona = 0.152;

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception("[PitExitTrafficDiagnostics] " + message);
        }

        private static void Pass(string name)
        {
            Console.WriteLine("  [PASS] " + name);
        }

        public static void RunAllTests()
        {
            Console.WriteLine("[TEST] Running Pit Exit Traffic Diagnostics Tests (Y-62)...");

            // Come in ExtendedRacingReferenceUnitTests: si raccolgono i fallimenti, cosi' una
            // neutralizzazione (ADR-004) mostra in un giro solo tutti i test che diventano rossi.
            var failures = new List<string>();
            RunCollecting(Test_GapBehind_IsTimeSincePlayerPassedOpponentPosition_Daytona094551, failures);
            RunCollecting(Test_GapBehind_WithoutPlayerTimestampsIsNaN, failures);
            RunCollecting(Test_GapBehind_OlderThanOneAndAHalfLapsIsNaN, failures);
            RunCollecting(Test_PositionSource_ReportsNativeSimHubOrFrozenMemory, failures);
            RunCollecting(Test_CandidateLog_OnEnterExitConflictChangeAndOncePerSecondInside, failures);
            if (failures.Count > 0) throw new Exception(string.Join(" || ", failures));

            Console.WriteLine("[TEST SUCCESS] All Pit Exit Traffic Diagnostics Tests Passed!");
        }

        private static void RunCollecting(Action test, List<string> failures)
        {
            try { test(); }
            catch (Exception ex) { failures.Add(ex.Message); }
        }

        /// <summary>
        /// Timestamp del Player come li scrive OpponentTracker.Update: all'ingresso in ogni bucket,
        /// l'orologio di sessione (tempo rimanente). Fra due campioni del log si interpola.
        /// </summary>
        private static double[] PlayerTimestampsFrom094551()
        {
            var timestamps = new double[OpponentTracker.TimestampBucketCount];
            for (int b = 0; b < timestamps.Length; b++)
            {
                double edge = (double)b / OpponentTracker.TimestampBucketCount;
                for (int i = 0; i + 1 < PlayerPos094551.Length; i++)
                {
                    if (edge >= PlayerPos094551[i] && edge <= PlayerPos094551[i + 1])
                    {
                        double f = (edge - PlayerPos094551[i]) / (PlayerPos094551[i + 1] - PlayerPos094551[i]);
                        timestamps[b] = PlayerClock094551[i] + f * (PlayerClock094551[i + 1] - PlayerClock094551[i]);
                        break;
                    }
                }
            }
            return timestamps;
        }

        /// <summary>
        /// Il distacco vero dietro al Player e' il tempo trascorso da quando il Player e' passato dove
        /// l'avversario si trova adesso. Il Player e' passato da 0.4829 a TL 2503.5: a TL 2468.5 un
        /// avversario in quel punto sta 35.0 s dietro. Se la sua posizione resta ferma (NotInWorld, H2),
        /// cinque secondi dopo il distacco vale 40.0 s.
        /// </summary>
        private static void Test_GapBehind_IsTimeSincePlayerPassedOpponentPosition_Daytona094551()
        {
            double[] timestamps = PlayerTimestampsFrom094551();

            double gap = TargetStrategyManager.TimestampGapBehindSeconds(timestamps, 0.4829, 2468.5, RefLapTime094551);
            Assert(Math.Abs(gap - 35.0) < 0.05,
                $"Opponent where the Player was at TL 2503.5 must be 35.0 s behind at TL 2468.5, got {gap:F3}");

            double frozen = TargetStrategyManager.TimestampGapBehindSeconds(timestamps, 0.4829, 2463.5, RefLapTime094551);
            Assert(Math.Abs(frozen - 40.0) < 0.05,
                $"A frozen opponent position must drift to 40.0 s five seconds later, got {frozen:F3}");

            Pass("Gap behind = time since the Player passed the opponent position (094551, lap 2)");
        }

        /// <summary>Dove il Player non ha timestamp (bucket a 0) non c'e' un distacco vero: NaN, non 0.</summary>
        private static void Test_GapBehind_WithoutPlayerTimestampsIsNaN()
        {
            double gap = TargetStrategyManager.TimestampGapBehindSeconds(PlayerTimestampsFrom094551(), ExtendedPitExitDaytona, 2468.5, RefLapTime094551);
            Assert(double.IsNaN(gap), $"Without Player timestamps at 0.152 the gap must be NaN, got {gap:F3}");

            Pass("No Player timestamps at the opponent position -> NaN");
        }

        /// <summary>
        /// Un timestamp di oltre un giro e mezzo fa (salto del replay, sessione ripartita) non e' un
        /// distacco: stessa soglia del gap del Target.
        /// </summary>
        private static void Test_GapBehind_OlderThanOneAndAHalfLapsIsNaN()
        {
            double gap = TargetStrategyManager.TimestampGapBehindSeconds(PlayerTimestampsFrom094551(), 0.4829, 2503.5 - 170.0, RefLapTime094551);
            Assert(double.IsNaN(gap), $"A Player timestamp 170 s old (more than 1.5 x 105.675) must give NaN, got {gap:F3}");

            Pass("Player timestamp older than 1.5 laps -> NaN");
        }

        /// <summary>
        /// La diagnostica deve dire da dove arriva la posizione usata dal controllo traffico. Con l'auto
        /// NotInWorld il nativo vale 0 e SimHub non ha posizione: GetOpponentTrackPosition restituisce
        /// l'ultima posizione memorizzata, che resta ferma.
        /// </summary>
        private static void Test_PositionSource_ReportsNativeSimHubOrFrozenMemory()
        {
            var tracker = new OpponentTracker();
            var state = new SessionState();
            state.Metadata.SourceName = "iRacingSDK";
            state.Metadata.CarIdxByUserName["GT3 Car"] = 5;
            var opp = new GameReaderCommon.Opponent { Name = "GT3 Car", TrackPositionPercent = 0.45 };

            var lapDists = new float[64];
            lapDists[5] = 0.725f;
            tracker.IracingBridge.SetMockData(new bool[64], new IracingTrackSurface[64], new int[64], new int[64], lapDists);

            double pos = tracker.GetOpponentTrackPosition(opp, state, out OpponentTracker.PositionSource source);
            Assert(source == OpponentTracker.PositionSource.Native && Math.Abs(pos - 0.725) < 1e-4,
                $"Native 0.725 expected, got {pos:F4} from {source}");

            lapDists[5] = 0f;
            pos = tracker.GetOpponentTrackPosition(opp, state, out source);
            Assert(source == OpponentTracker.PositionSource.SimHub && Math.Abs(pos - 0.45) < 1e-4,
                $"SimHub 0.45 expected, got {pos:F4} from {source}");

            opp.TrackPositionPercent = null;
            tracker.AddTrackedOpponent("GT3 Car", new OpponentTelemetryData { Name = "GT3 Car", LastPosPct = ExtendedPitExitDaytona });
            pos = tracker.GetOpponentTrackPosition(opp, state, out source);
            Assert(source == OpponentTracker.PositionSource.Memory && Math.Abs(pos - ExtendedPitExitDaytona) < 1e-4,
                $"Frozen memory 0.152 expected, got {pos:F4} from {source}");

            Pass("Opponent position source: native, SimHub, or frozen memory");
        }

        /// <summary>
        /// Una riga per vettura quando entra nella bolla, quando esce, quando cambia il suo contributo
        /// al conflitto, e al massimo una al secondo mentre resta dentro. L'orologio e' il tempo
        /// rimanente (decresce), attorno al primo conflitto di 094551 (TL 2501.9).
        /// </summary>
        private static void Test_CandidateLog_OnEnterExitConflictChangeAndOncePerSecondInside()
        {
            Assert(TargetStrategyManager.ShouldLogTrafficCandidate(false, false, double.NaN, true, false, 2501.9, 1.0),
                "entering the bubble must log");
            Assert(!TargetStrategyManager.ShouldLogTrafficCandidate(true, false, 2501.9, true, false, 2501.5, 1.0),
                "0.4 s after the last line, still inside, must not log");
            Assert(TargetStrategyManager.ShouldLogTrafficCandidate(true, false, 2501.9, true, true, 2501.7, 1.0),
                "a change of the conflict must log at once");
            Assert(TargetStrategyManager.ShouldLogTrafficCandidate(true, true, 2501.7, true, true, 2500.6, 1.0),
                "1.1 s after the last line, still inside, must log");
            Assert(TargetStrategyManager.ShouldLogTrafficCandidate(true, true, 2500.6, false, false, 2500.4, 1.0),
                "leaving the bubble must log");
            Assert(!TargetStrategyManager.ShouldLogTrafficCandidate(false, false, 2500.4, false, false, 2499.0, 1.0),
                "outside the bubble must not log");

            Pass("Traffic candidate log: enter, exit, conflict change, once per second inside");
        }
    }
}
