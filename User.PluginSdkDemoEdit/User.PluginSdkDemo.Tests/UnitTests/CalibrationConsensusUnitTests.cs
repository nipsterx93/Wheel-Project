// -------------------------------------------------------------------------
// FILE: CalibrationConsensusUnitTests.cs
// Consenso sui dati calibrati: un campione singolo non e' una misura.
// I casi di regressione vengono dai numeri veri del replay Misano del
// 2026-08-23 (SimRIG_DebugLog_20260823_133904.csv).
// -------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using SimRIG;

namespace User.PluginSdkDemo.Tests
{
    public class CalibrationConsensusUnitTests
    {
        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception("[CalibrationConsensus] " + message);
        }

        private static void AssertClose(double actual, double expected, string message)
        {
            if (Math.Abs(actual - expected) > 1e-9)
                throw new Exception($"[CalibrationConsensus] {message} — atteso {expected:F4}, ottenuto {actual:F4}");
        }

        private static void Pass(string name)
        {
            Console.WriteLine("  [PASS] " + name);
        }

        public static void RunAllTests()
        {
            Console.WriteLine("[TEST] Running Calibration Consensus Tests...");

            Test_Regression_MisanoPitSpeedLimitOutlier();
            Test_MedianReturnsAnObservedValue();
            Test_SingleSampleIsUsableButNotConsensus();
            Test_ThreeAgreeingSamplesReachConsensus();
            Test_DisagreeingSamplesDoNotReachConsensus();
            Test_GeofenceToleranceSeparatesTheTwoHistoricValues();
            Test_RollingWindowFollowsAGenuineChange();
            Test_EmptyAndResetBehaviour();

            Console.WriteLine("[TEST SUCCESS] All Calibration Consensus Tests Passed!");
        }

        private static void Test_Regression_MisanoPitSpeedLimitOutlier()
        {
            // Caso reale, replay Misano 2026-08-23: dodici osservazioni del limite di pit lane.
            // Undici a 60 km/h, una a 80 (Adrian Olschar, MaxSpeed 79.6 — ancora in decelerazione).
            // Con "l'ultimo che scrive vince" l'80 sovrascriveva il 60, e il database e' tornato
            // corretto solo perche' altri avversari hanno scritto dopo: fortuna, non protezione.
            var consensus = new CalibrationConsensus(PitRadar.SpeedLimitAgreementKmh);

            consensus.Add(60.0);
            consensus.Add(80.0);   // l'outlier, che prima avrebbe vinto se arrivato per ultimo
            for (int i = 0; i < 7; i++) consensus.Add(60.0);

            AssertClose(consensus.Value, 60.0, "l'outlier non deve spostare la mediana");
            Assert(consensus.HasConsensus, "nove osservazioni concordi sono un consenso");

            // E la stessa serie con l'outlier **per ultimo**: e' il caso che prima rompeva.
            var worstCase = new CalibrationConsensus(PitRadar.SpeedLimitAgreementKmh);
            for (int i = 0; i < 8; i++) worstCase.Add(60.0);
            worstCase.Add(80.0);
            AssertClose(worstCase.Value, 60.0, "nemmeno arrivando per ultimo l'outlier vince");
            Pass("Regressione Misano: l'80 km/h isolato non scalza gli undici 60");
        }

        private static void Test_MedianReturnsAnObservedValue()
        {
            // A parita' di campioni si prende il centrale basso, non la media dei due centrali:
            // fra un 60 e un 80 la media darebbe 70, che non e' un limite di pit lane esistente.
            var consensus = new CalibrationConsensus(PitRadar.SpeedLimitAgreementKmh);
            consensus.Add(60.0);
            consensus.Add(80.0);

            AssertClose(consensus.Value, 60.0, "mai inventare un valore mai osservato");

            AssertClose(CalibrationConsensus.Median(new List<double> { 80.0, 60.0 }), 60.0,
                        "la mediana non dipende dall'ordine di inserimento");
            Pass("La mediana restituisce sempre un valore realmente osservato");
        }

        private static void Test_SingleSampleIsUsableButNotConsensus()
        {
            // Su una pista mai vista il primo campione deve essere subito utilizzabile, altrimenti
            // non si calibrerebbe piu' nulla — ma non basta a dichiarare il dato consolidato.
            var consensus = new CalibrationConsensus(PitRadar.GeofenceAgreementPct);
            consensus.Add(0.9494);

            AssertClose(consensus.Value, 0.9494, "il primo campione e' gia' il valore corrente");
            Assert(consensus.HasSamples, "e risulta presente");
            Assert(!consensus.HasConsensus, "ma una sola osservazione non e' un consenso");
            Pass("Un campione solo e' utilizzabile ma non consolidato");
        }

        private static void Test_ThreeAgreeingSamplesReachConsensus()
        {
            var consensus = new CalibrationConsensus(PitRadar.GeofenceAgreementPct);
            consensus.Add(0.9494);
            consensus.Add(0.9491);
            Assert(!consensus.HasConsensus, "due non bastano: un anomalo peserebbe meta' del totale");

            consensus.Add(0.9497);
            Assert(consensus.HasConsensus, "tre osservazioni concordi consolidano il dato");
            Assert(consensus.AgreeingCount == 3, "e concordano tutte e tre");
            Pass("Tre osservazioni concordi consolidano la geofence");
        }

        private static void Test_DisagreeingSamplesDoNotReachConsensus()
        {
            // Tre campioni sparsi non sono un consenso solo perche' sono tre.
            var consensus = new CalibrationConsensus(PitRadar.GeofenceAgreementPct);
            consensus.Add(0.0737);
            consensus.Add(0.1088);
            consensus.Add(0.0500);

            Assert(!consensus.HasConsensus,
                   $"campioni sparsi non consolidano nulla (agreeing={consensus.AgreeingCount})");
            Pass("Tre campioni discordi non fanno consenso");
        }

        private static void Test_GeofenceToleranceSeparatesTheTwoHistoricValues()
        {
            // I due valori storici di PitExitPct a Misano: 0.1088 (vecchio, congelato dal guard
            // "== -1.0") e 0.0737 (misurato il 2026-08-23). Distano 0.035, cioe' ~148 m: la
            // tolleranza deve considerarli discordi, altrimenti il consenso li fonderebbe.
            var consensus = new CalibrationConsensus(PitRadar.GeofenceAgreementPct);
            consensus.Add(0.1088);
            consensus.Add(0.0737);

            Assert(consensus.AgreeingCount == 1,
                   $"i due valori storici non parlano dello stesso punto (agreeing={consensus.AgreeingCount})");
            Pass("La tolleranza geofence tiene separati 0.1088 e 0.0737");
        }

        private static void Test_RollingWindowFollowsAGenuineChange()
        {
            // Se il dato reale cambia davvero e in modo duraturo, la finestra scorrevole deve
            // seguirlo: la protezione e' contro il campione isolato, non contro la realta'.
            var consensus = new CalibrationConsensus(PitRadar.SpeedLimitAgreementKmh);
            for (int i = 0; i < CalibrationConsensus.DefaultWindow; i++) consensus.Add(60.0);
            AssertClose(consensus.Value, 60.0, "si parte consolidati su 60");

            for (int i = 0; i < CalibrationConsensus.DefaultWindow; i++) consensus.Add(80.0);
            AssertClose(consensus.Value, 80.0, "dopo una finestra piena di 80 la mediana si sposta");
            Assert(consensus.SampleCount == CalibrationConsensus.DefaultWindow,
                   "la finestra non cresce all'infinito");
            Pass("La finestra scorrevole segue un cambiamento reale e duraturo");
        }

        private static void Test_EmptyAndResetBehaviour()
        {
            var consensus = new CalibrationConsensus(PitRadar.GeofenceAgreementPct);
            Assert(!consensus.HasSamples, "nasce vuoto");
            AssertClose(consensus.Value, 0.0, "senza campioni non c'e' valore");
            Assert(consensus.AgreeingCount == 0, "ne' accordo");
            AssertClose(CalibrationConsensus.Median(null), 0.0, "mediana di null e' zero, non un crash");

            consensus.Add(0.95);
            consensus.Reset();
            Assert(!consensus.HasSamples, "reset svuota");
            Pass("Consenso vuoto e reset si comportano bene");
        }
    }
}
