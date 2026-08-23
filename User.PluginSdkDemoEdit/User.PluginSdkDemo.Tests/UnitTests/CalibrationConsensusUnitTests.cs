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

            Test_Regression_SeedMakesSingleStopTracksConsolidate();
            Test_SeededValueResistsOneDivergentSample();
            Test_SeedIsCappedSoRealityCanStillWin();
            Test_SeedIgnoresNonPositiveCount();
            Test_LegacyRecordGetsSeedCountOnMigration();

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

        // ------------------------------------------------- persistenza fra sessioni

        private static void Test_Regression_SeedMakesSingleStopTracksConsolidate()
        {
            // Caso reale, tre replay Misano del 2026-08-23: una sosta per gara, SimHub riavviato
            // ogni volta. Senza seme il consenso ripartiva da zero e la confidenza restava
            // EstimatedPlayer in tutti e tre i run — cioe' di fatto "l'ultimo che scrive vince",
            // il difetto che il consenso doveva chiudere.
            const double misanoExit = 0.0738605;

            var withoutSeed = new CalibrationConsensus(PitRadar.GeofenceAgreementPct);
            withoutSeed.Add(misanoExit);
            Assert(!withoutSeed.HasConsensus,
                   "senza seme, una sosta sola non consolida — e' il comportamento osservato");

            // Con il seme, la terza gara con una sosta a testa consolida come tre soste in una.
            var session3 = new CalibrationConsensus(PitRadar.GeofenceAgreementPct);
            session3.Seed(misanoExit, 2);          // due gare precedenti
            session3.Add(0.0737054);               // la terza sosta, dal run 3 reale
            Assert(session3.HasConsensus,
                   $"tre osservazioni concordi consolidano (agreeing={session3.AgreeingCount})");
            Pass("Regressione Misano: il seme fa consolidare i circuiti con una sosta a gara");
        }

        private static void Test_SeededValueResistsOneDivergentSample()
        {
            // Il valore consolidato e' 0.0737. Arriva un campione a 0.1088 — i due valori storici
            // di Misano, distanti ~148 m. Il consolidato deve reggere.
            var consensus = new CalibrationConsensus(PitRadar.GeofenceAgreementPct);
            consensus.Seed(0.0737054, CalibrationConsensus.MinimumForConsensus);
            consensus.Add(0.1088);

            AssertClose(consensus.Value, 0.0737054, "un campione divergente isolato non scalza il consolidato");
            Assert(consensus.HasConsensus, "e il dato resta consolidato");
            Pass("Un campione divergente isolato non sposta un valore consolidato");
        }

        private static void Test_SeedIsCappedSoRealityCanStillWin()
        {
            // Il seme non deve rendere il valore inamovibile: se la realta' cambia davvero e in
            // modo duraturo, abbastanza campioni nuovi devono poter vincere.
            var consensus = new CalibrationConsensus(PitRadar.GeofenceAgreementPct);
            consensus.Seed(0.0737, 999);   // storico enorme
            Assert(consensus.SampleCount == CalibrationConsensus.MinimumForConsensus,
                   $"il seme e' limitato a {CalibrationConsensus.MinimumForConsensus}, ottenuto {consensus.SampleCount}");

            for (int i = 0; i < 4; i++) consensus.Add(0.1500);
            AssertClose(consensus.Value, 0.1500, "quattro campioni concordi nuovi ribaltano il consolidato");
            Pass("Il seme e' limitato: una realta' cambiata puo' ancora vincere");
        }

        private static void Test_SeedIgnoresNonPositiveCount()
        {
            var consensus = new CalibrationConsensus(PitRadar.GeofenceAgreementPct);
            consensus.Seed(0.95, 0);
            Assert(!consensus.HasSamples, "contatore a zero non semina nulla");

            consensus.Seed(0.95, -3);
            Assert(!consensus.HasSamples, "ne' un contatore negativo");
            Pass("Un contatore assente o negativo non semina");
        }

        private static void Test_LegacyRecordGetsSeedCountOnMigration()
        {
            // Un record scritto prima che il consenso esistesse ha il valore ma non il contatore.
            // Degradarlo lo esporrebbe a essere sovrascritto da un campione singolo: e' l'opposto
            // di quello che serve. La migrazione lo considera consolidato.
            var db = new SimRigDatabase
            {
                Tracks = new List<TrackRecord>
                {
                    new TrackRecord
                    {
                        TrackClassID = "MISANO GP_GT3 2025",
                        PitEntryPct = 0.9495,
                        PitExitPct = 0.0737,
                        GeofenceConfidence = CalibrationConfidence.Confirmed,
                        GeofenceSampleCount = 0
                    }
                }
            };

            PitRadar.MigrateLegacyConfidence(db);

            Assert(db.Tracks[0].GeofenceSampleCount == CalibrationConsensus.MinimumForConsensus,
                   $"il record legacy va considerato consolidato, ottenuto {db.Tracks[0].GeofenceSampleCount}");

            // Idempotente: rigirarla non gonfia il contatore.
            PitRadar.MigrateLegacyConfidence(db);
            Assert(db.Tracks[0].GeofenceSampleCount == CalibrationConsensus.MinimumForConsensus,
                   "la migrazione resta idempotente");

            // Un record senza geofence non deve ricevere nessun contatore.
            var empty = new SimRigDatabase
            {
                Tracks = new List<TrackRecord> { new TrackRecord { TrackClassID = "NUOVA" } }
            };
            PitRadar.MigrateLegacyConfidence(empty);
            Assert(empty.Tracks[0].GeofenceSampleCount == 0,
                   "una pista mai calibrata resta a zero");
            Pass("Migrazione: i record legacy nascono consolidati e la migrazione e' idempotente");
        }
    }
}
