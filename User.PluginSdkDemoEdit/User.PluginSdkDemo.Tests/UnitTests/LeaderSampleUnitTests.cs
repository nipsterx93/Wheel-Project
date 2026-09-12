// -------------------------------------------------------------------------
// FILE: LeaderSampleUnitTests.cs
// Y-24: un record del leader vuoto non e' il leader sul traguardo.
// Il caso di regressione viene dal replay Daytona del 2026-08-23
// (Logs/Daytona Run), giri 12-15: LapsComp=0, PosPct=0.0000.
// -------------------------------------------------------------------------

using System;
using SimRIG;

namespace User.PluginSdkDemo.Tests
{
    public class LeaderSampleUnitTests
    {
        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception("[LeaderSample] " + message);
        }

        private static void Pass(string name)
        {
            Console.WriteLine("  [PASS] " + name);
        }

        public static void RunAllTests()
        {
            Console.WriteLine("[TEST] Running Leader Sample Tests...");

            Test_Regression_BlankLeaderRecordIsRejected();
            Test_LeaderOnTheLineIsAccepted();
            Test_NormalSamplesAreAccepted();
            Test_LapsRemainingWouldCollapseWithoutTheGuard();
            Test_HeldLapCountSurvivesABlankRun();
            Test_OpponentLeaderLapsCompleted_MatchesCurrentLapMinusOne();
            Test_LeaderTrackPct_HoldsLastGoodWhenTelemetryDrops();
            Test_LastLapInLapProjections_ActiveAfterTimeExpiry();

            Console.WriteLine("[TEST SUCCESS] All Leader Sample Tests Passed!");
        }

        private static void Test_Regression_BlankLeaderRecordIsRejected()
        {
            // Caso reale, replay Daytona 2026-08-23 giri 12-15. Il diagnostico riportava
            //   Leader (Sam Kuitert): PosPct=0.0000, LapsComp=0, LapsRem=30.00, LatchedTotal=30.00
            // mentre il leader era in realta' intorno al giro 12-14. Con posizione assoluta a zero,
            // LeaderRaceLapsRemaining diventava il totale latchato intero.
            Assert(!RaceAnalyzer.IsLeaderSampleUsable(0, 0.0),
                   "giri a zero e posizione a zero insieme sono un record vuoto");
            Pass("Regressione Daytona: il record vuoto del leader viene rifiutato");
        }

        private static void Test_LeaderOnTheLineIsAccepted()
        {
            // Posizione esattamente a zero e' legittima se il conteggio giri dice che la gara e'
            // in corso: il leader ha appena tagliato il traguardo. Non si deve scartare quel caso.
            Assert(RaceAnalyzer.IsLeaderSampleUsable(12, 0.0),
                   "sul traguardo al giro 12 il campione e' buono");
            Assert(RaceAnalyzer.IsLeaderSampleUsable(1, 0.0),
                   "e anche dopo il primo giro completato");
            Pass("Il leader esattamente sul traguardo non viene scambiato per un buco");
        }

        private static void Test_NormalSamplesAreAccepted()
        {
            Assert(RaceAnalyzer.IsLeaderSampleUsable(1, 0.34), "campione normale");
            Assert(RaceAnalyzer.IsLeaderSampleUsable(25, 0.88), "campione inoltrato");
            Pass("I campioni ordinari restano tutti utilizzabili");
        }

        private static void Test_LapsRemainingWouldCollapseWithoutTheGuard()
        {
            // Verifica che il danno descritto nel log Daytona fosse reale.
            // Con totale latchato = 30 e leader al giro 11 a meta' pista (11.35):
            //   corretto:   30.00 - 11.35 = 18.65 giri rimanenti
            //   senza guard (pos = 0.0): 30.00 - 0.00 = 30.00 giri rimanenti (+11.35)
            double latchedTotal = 30.0;
            double correctPos = 11.35;
            double blankPos = 0.0; // quello che SimHub passava nel buco

            double correct = latchedTotal - correctPos;
            double withBlankSample = latchedTotal - blankPos;

            Assert(Math.Abs(withBlankSample - 30.0) < 0.01,
                   "con il campione vuoto diventa il totale intero, cioe' i 30.00 visti nel log");
            Assert(withBlankSample - correct > 11.0,
                   "lo scarto e' di oltre undici giri: non e' un arrotondamento");
            Pass("Senza il guard, L_Rem passa da 18.65 a 30.00");
        }

        private static void Test_HeldLapCountSurvivesABlankRun()
        {
            // Y-25: la stessa regola applicata alla sorgente, non solo al calcolo derivato.
            // Nel replay Daytona 231 tick su 534 (43%) avevano LapsComp=0: la proprieta'
            // SimRIG.Session.LeaderRaceLapsCompleted, che finisce direttamente sulla dashboard,
            // lampeggiava a zero quasi meta' del tempo.
            //
            // Riproduce la regola di tenuta: campione buono -> si aggiorna; campione vuoto -> si
            // tiene l'ultimo buono.
            int lastGood = -1;
            int[] rawLapsCompleted = { 11, 0, 0, 0, 12, 0, 13 };
            double[] rawPositions = { 0.34, 0.0, 0.0, 0.0, 0.51, 0.0, 0.09 };
            int[] expected = { 11, 11, 11, 11, 12, 12, 13 };

            for (int i = 0; i < rawLapsCompleted.Length; i++)
            {
                int shown = RaceAnalyzer.HoldLeaderLapsCompleted(
                    rawLapsCompleted[i], rawPositions[i], ref lastGood);

                Assert(shown == expected[i],
                       $"tick {i}: atteso {expected[i]}, ottenuto {shown}");
            }

            // Il conteggio non deve mai tornare indietro durante la raffica di record vuoti.
            Assert(expected[3] >= expected[0], "il conteggio tenuto non regredisce");
            Pass("Il conteggio giri del leader non lampeggia a zero sui record vuoti");
        }

        private static void Test_OpponentLeaderLapsCompleted_MatchesCurrentLapMinusOne()
        {
            // Replay Daytona 20260912_142223:
            // Al giro 9 del Player (Leader in corso al giro 10, CurrentLap == 10),
            // LeaderRaceLapsCompleted deve valere esattamente 9 (CurrentLap - 1).
            // Con il vecchio bug di _leaderRaceStartLap agganciato a 6, riportava 10 - 6 = 4!
            var analyzer = new RaceAnalyzer();
            var state = new SessionState
            {
                IsGameRunning = true,
                IsRaceSession = true,
                SessionStateStatus = 4,
                RaceStartLineCrossed = true,
                Position = 2,
                CurrentLap = 9,
                TrackPositionPercent = 0.5,
                TrackLengthMeters = 5729,
                BestLapTimeSec = 105.0
            };

            var leaderOpponent = new GameReaderCommon.Opponent
            {
                Name = "Leader Driver",
                Position = 1,
                CurrentLap = 10,
                TrackPositionPercent = 0.65
            };
            state.Opponents = new System.Collections.Generic.List<GameReaderCommon.Opponent> { leaderOpponent };

            analyzer.Update(state, null, null, null, null, TyreSelectionScope.None);

            Assert(analyzer.Results.LeaderRaceLapsCompleted == 9,
                   $"Al giro 10 il leader ha completato 9 giri, ottenuto {analyzer.Results.LeaderRaceLapsCompleted}");
            Assert(Math.Abs(analyzer.Results.LeaderTrackPct - 0.65) < 0.001,
                   $"LeaderTrackPct deve valere 0.65, ottenuto {analyzer.Results.LeaderTrackPct}");
            Pass("Leader laps completed si allinea a CurrentLap - 1 senza offset di _leaderRaceStartLap");
        }

        private static void Test_LeaderTrackPct_HoldsLastGoodWhenTelemetryDrops()
        {
            var analyzer = new RaceAnalyzer();
            var state = new SessionState
            {
                IsGameRunning = true,
                IsRaceSession = true,
                SessionStateStatus = 4,
                RaceStartLineCrossed = true,
                Position = 2,
                CurrentLap = 9,
                TrackPositionPercent = 0.5,
                TrackLengthMeters = 5729,
                BestLapTimeSec = 105.0
            };

            var leaderOpponent = new GameReaderCommon.Opponent
            {
                Name = "Leader Driver",
                Position = 1,
                CurrentLap = 10,
                TrackPositionPercent = 0.72
            };
            state.Opponents = new System.Collections.Generic.List<GameReaderCommon.Opponent> { leaderOpponent };

            analyzer.Update(state, null, null, null, null, TyreSelectionScope.None);
            Assert(Math.Abs(analyzer.Results.LeaderTrackPct - 0.72) < 0.001, "primo campione valido");

            // Tick successivo con drop di telemetria (TrackPositionPercent null o 0.0)
            leaderOpponent.TrackPositionPercent = null;
            analyzer.Update(state, null, null, null, null, TyreSelectionScope.None);

            Assert(Math.Abs(analyzer.Results.LeaderTrackPct - 0.72) < 0.001,
                   $"Sul drop di telemetria LeaderTrackPct tiene 0.72 invece di andare a 0.0, ottenuto {analyzer.Results.LeaderTrackPct}");
            Pass("LeaderTrackPct mantiene l'ultimo valore valido sui drop a zero");
        }

        private static void Test_LastLapInLapProjections_ActiveAfterTimeExpiry()
        {
            // Replay Daytona: il countdown scende sotto zero (tempo scaduto, bandiera a scacchi sventolata al leader).
            // Il Player non ha ancora tagliato il traguardo (_isRaceFinished = false).
            // Le proiezioni NON devono azzerarsi / freezarsi: devono continuare il countdown fino alla linea.
            var analyzer = new RaceAnalyzer();
            var state = new SessionState
            {
                IsGameRunning = true,
                IsRaceSession = true,
                IsTimeLimited = true,
                SessionStateStatus = 4,
                RaceStartLineCrossed = true,
                Position = 1,
                CurrentLap = 26,
                TrackPositionPercent = 0.75, // Player a 3/4 dell'ultimo giro
                TrackLengthMeters = 5729,
                BestLapTimeSec = 100.0,
                SessionTimeLeftSec = 10.0 // countdown positivo prima dello scadere
            };

            // Aggiornamento prima dello zero per stabilizzare il totale e segnare _hasSeenPositiveCountdown
            analyzer.Update(state, null, null, null, null, TyreSelectionScope.None);
            double initialTotal = analyzer.Results.RaceTotalLaps;
            Assert(initialTotal > 0.0, "Totale iniziale calcolato");

            // Tempo di sessione scaduto (< 0) e leader ha tagliato la linea (GlobalCheckeredFlag)
            state.SessionTimeLeftSec = -5.0;
            state.GlobalCheckeredFlag = true;
            state.TrackPositionPercent = 0.80; // Player a 80% dell'ultimo giro (manca 0.20 giri)

            analyzer.Update(state, null, null, null, null, TyreSelectionScope.None);

            Assert(analyzer.Results.RaceTotalLaps == initialTotal,
                   $"Il totale non si azzera allo scadere del tempo, ottenuto {analyzer.Results.RaceTotalLaps}");
            Assert(analyzer.Results.RaceLapsRemaining > 0.0 && analyzer.Results.RaceLapsRemaining <= 0.25,
                   $"RaceLapsRemaining conta la frazione residua del giro in corso, ottenuto {analyzer.Results.RaceLapsRemaining}");
            Assert(analyzer.Results.RaceLifeTimeLeftSec > 0.0,
                   $"RaceLifeTimeLeftSec conta i secondi residui fino al traguardo del Player, ottenuto {analyzer.Results.RaceLifeTimeLeftSec}");
            Assert(analyzer.Results.IsLapsPredictionValid,
                   "IsLapsPredictionValid deve rimanere true durante l'in-lap finale");
            Pass("Ultimo giro: le proiezioni non si freezano allo scadere del cronometro ma accompagnano il Player al traguardo");
        }
    }
}
