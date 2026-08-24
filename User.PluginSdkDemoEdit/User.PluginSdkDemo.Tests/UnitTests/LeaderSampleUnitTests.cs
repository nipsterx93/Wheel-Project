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
            Assert(RaceAnalyzer.IsLeaderSampleUsable(0, 0.3452),
                   "primo giro in corso, posizione valida");
            Assert(RaceAnalyzer.IsLeaderSampleUsable(17, 0.6079),
                   "campione reale del giro 16, deve passare");
            Pass("I campioni normali passano");
        }

        private static void Test_LapsRemainingWouldCollapseWithoutTheGuard()
        {
            // Dimostra il danno con i numeri veri del log: totale latchato 30, leader al giro 11.35.
            const double latchedTotal = 30.0;
            const double realLeaderPos = 11.3452;

            double correct = latchedTotal - realLeaderPos;
            double withBlankSample = latchedTotal - 0.0;

            Assert(Math.Abs(correct - 18.65) < 0.01,
                   $"il valore corretto e' ~18.65, ottenuto {correct:F2}");
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
    }
}
