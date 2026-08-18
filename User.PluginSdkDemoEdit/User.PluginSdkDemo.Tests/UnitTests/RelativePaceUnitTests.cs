// -------------------------------------------------------------------------
// FILE: RelativePaceUnitTests.cs
// Test obbligatori 6-9 della Technical Verification Specification.
// Il caso critico è il Test 8/9: la progressione dei macrosettori continua
// dentro la pit lane, quindi una sequenza numericamente valida può nascondere
// un DeltaGap che attraversa la sosta.
// -------------------------------------------------------------------------

using System;
using SimRIG;

namespace User.PluginSdkDemo.Tests
{
    public class RelativePaceUnitTests
    {
        private const double RefLapTime = 100.0;

        /// <summary>Clock di sessione: conta alla rovescia, come in gara.</summary>
        private static double Clock(int step) { return 3600.0 - (step * 5.0); }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception("[RelativePace] " + message);
        }

        private static void AssertClose(double actual, double expected, double tolerance, string message)
        {
            if (Math.Abs(actual - expected) > tolerance)
                throw new Exception($"[RelativePace] {message} — atteso {expected:F4}, ottenuto {actual:F4}");
        }

        public static void RunAllTests()
        {
            Console.WriteLine("[TEST] Running RelativePace State Machine Tests...");

            Test_SectorSequence_And_Wrap();
            Test_InvalidSequence_WithoutPit();
            Test_DeltaTimeTooSmall();
            Test_PlayerPit_NoDeltaAcrossPit();
            Test_TargetPit_NoDeltaAcrossPit();
            Test_PitContamination_SurvivesMultiSectorStop();
            Test_TargetChange_ResetsToZero();
            Test_EmaAndClamp();
            Test_SnapshotHeaderMatchesFieldCount();

            Console.WriteLine("[TEST SUCCESS] All RelativePace Tests Passed!");
        }

        // ---------------------------------------------------------------
        // Test 6 — sequenzialità e wrap 19 -> 0
        // ---------------------------------------------------------------
        private static void Test_SectorSequence_And_Wrap()
        {
            var t = new RelativePaceTracker();

            // Primo campione: nessun seed precedente
            var s = t.ProcessSample(18, Clock(0), 10.0, false, false, RefLapTime);
            Assert(s.Reason == RelativePaceInvalidationReason.NoPreviousSeed, "primo campione deve dare NoPreviousSeed");
            Assert(!s.RateComputed, "primo campione non deve produrre un rate");

            // 18 -> 19 valido
            s = t.ProcessSample(19, Clock(1), 10.5, false, false, RefLapTime);
            Assert(s.SequenceValid, "18 -> 19 deve essere una sequenza valida");
            Assert(s.RateComputed, "18 -> 19 deve produrre un rate");

            // 19 -> 0 valido (wrap)
            s = t.ProcessSample(0, Clock(2), 11.0, false, false, RefLapTime);
            Assert(s.SequenceValid, "19 -> 0 deve essere valido (wrap)");
            Assert(s.RateComputed, "19 -> 0 deve produrre un rate");

            // 0 -> 3 invalido: salto
            s = t.ProcessSample(3, Clock(3), 11.5, false, false, RefLapTime);
            Assert(!s.SequenceValid, "0 -> 3 deve essere invalido");
            Assert(s.Reason == RelativePaceInvalidationReason.MissingSector, "0 -> 3 deve dare MissingSector");
            Assert(!s.RateComputed, "0 -> 3 non deve produrre un rate");

            // 3 diventa seed, 3 -> 4 produce il primo rate
            s = t.ProcessSample(4, Clock(4), 12.0, false, false, RefLapTime);
            Assert(s.RateComputed, "3 -> 4 deve produrre il primo rate dopo il salto");

            // Settore duplicato
            var t2 = new RelativePaceTracker();
            t2.ProcessSample(5, Clock(0), 1.0, false, false, RefLapTime);
            s = t2.ProcessSample(5, Clock(1), 1.2, false, false, RefLapTime);
            Assert(s.Reason == RelativePaceInvalidationReason.DuplicateSector, "5 -> 5 deve dare DuplicateSector");

            // Salto all'indietro
            var t3 = new RelativePaceTracker();
            t3.ProcessSample(10, Clock(0), 1.0, false, false, RefLapTime);
            s = t3.ProcessSample(8, Clock(1), 1.2, false, false, RefLapTime);
            Assert(s.Reason == RelativePaceInvalidationReason.InvalidSequence, "10 -> 8 deve dare InvalidSequence");

            Console.WriteLine("  [PASS] Test6_SectorSequence_And_Wrap");
        }

        // ---------------------------------------------------------------
        // Test 7 — invalidazione senza pit: 8 -> 10
        // ---------------------------------------------------------------
        private static void Test_InvalidSequence_WithoutPit()
        {
            var t = new RelativePaceTracker();

            t.ProcessSample(7, Clock(0), 5.0, false, false, RefLapTime);
            var s = t.ProcessSample(8, Clock(1), 5.5, false, false, RefLapTime);
            Assert(s.RateComputed, "7 -> 8 deve produrre un rate");
            double paceAfterValid = t.RelativePace;

            // 8 -> 10: salto, RelativePace congelato
            s = t.ProcessSample(10, Clock(2), 9.0, false, false, RefLapTime);
            Assert(!s.RateComputed, "8 -> 10 non deve produrre un rate");
            AssertClose(t.RelativePace, paceAfterValid, 1e-9, "RelativePace deve restare congelato sul salto");

            // 10 è il nuovo seed: il rate arriva solo su 10 -> 11 e usa gap@10, non gap@8
            s = t.ProcessSample(11, Clock(3), 9.5, false, false, RefLapTime);
            Assert(s.RateComputed, "10 -> 11 deve produrre il primo rate valido");
            AssertClose(s.PreviousGap, 9.0, 1e-9, "la reference deve essere gap@10, non gap@8");
            AssertClose(s.DeltaGap, 0.5, 1e-9, "DeltaGap deve essere 9.5 - 9.0");

            Console.WriteLine("  [PASS] Test7_InvalidSequence_WithoutPit");
        }

        private static void Test_DeltaTimeTooSmall()
        {
            var t = new RelativePaceTracker();
            t.ProcessSample(4, 3600.0, 2.0, false, false, RefLapTime);

            // dt = 0.4s < 1.0s
            var s = t.ProcessSample(5, 3599.6, 2.1, false, false, RefLapTime);
            Assert(s.Reason == RelativePaceInvalidationReason.DeltaTimeTooSmall, "dt < 1.0 deve dare DeltaTimeTooSmall");
            Assert(!s.RateComputed, "dt < 1.0 non deve produrre un rate");

            // Il campione corrente è il nuovo seed
            s = t.ProcessSample(6, 3595.0, 2.6, false, false, RefLapTime);
            Assert(s.RateComputed, "il campione successivo con dt valido deve produrre un rate");
            AssertClose(s.PreviousGap, 2.1, 1e-9, "la reference deve essere il campione scartato per dt");

            Console.WriteLine("  [PASS] Test_DeltaTimeTooSmall");
        }

        // ---------------------------------------------------------------
        // Test 8 — OBBLIGATORIO. Pit del Player con TrackPercentage continuo.
        // Regressione diretta di RED-1.
        // ---------------------------------------------------------------
        private static void Test_PlayerPit_NoDeltaAcrossPit()
        {
            var t = new RelativePaceTracker();

            // Regime pulito: costruiamo un RelativePace non nullo
            t.ProcessSample(6, Clock(0), 5.0, false, false, RefLapTime);
            t.ProcessSample(7, Clock(1), 5.2, false, false, RefLapTime);
            var s = t.ProcessSample(8, Clock(2), 5.4, false, false, RefLapTime);
            Assert(s.RateComputed, "il regime pulito deve produrre rate");
            double frozenPace = t.RelativePace;
            Assert(Math.Abs(frozenPace) > 1e-9, "il RelativePace di partenza non deve essere zero");

            // Settore 9: Player entra ai box. Il gap esplode perché la sosta costa ~22s.
            s = t.ProcessSample(9, Clock(3), 27.0, true, false, RefLapTime);
            Assert(s.Reason == RelativePaceInvalidationReason.PlayerInPit, "in pit la reason deve essere PlayerInPit");
            Assert(!s.RateComputed, "nessun rate durante il pit");
            AssertClose(t.RelativePace, frozenPace, 1e-9, "RelativePace deve restare congelato durante il pit");
            Assert(t.PitSeedPending, "il flag di contaminazione deve essere alzato");

            // Settore 10: Player esce. La sequenza 9 -> 10 è NUMERICAMENTE VALIDA
            // (il macrosettore avanza anche in pit lane) ma il campione 9 è contaminato.
            s = t.ProcessSample(10, Clock(4), 28.0, false, false, RefLapTime);
            Assert(s.SequenceValid, "9 -> 10 è numericamente valida: è proprio il caso insidioso");
            Assert(!s.RateComputed, "REGRESSIONE RED-1: nessun rate deve attraversare il pit");
            Assert(s.WasPostPitSeed, "il primo campione pulito post-pit deve essere un seed");
            AssertClose(t.RelativePace, frozenPace, 1e-9, "RelativePace deve essere ancora congelato");
            Assert(!t.PitSeedPending, "il flag deve essere consumato dal seed post-pit");

            // Settore 11: primo rate pulito, reference = gap@10, non gap@9
            s = t.ProcessSample(11, Clock(5), 28.3, false, false, RefLapTime);
            Assert(s.RateComputed, "10 -> 11 deve produrre il primo rate pulito");
            AssertClose(s.PreviousGap, 28.0, 1e-9, "la reference deve essere gap@10 (fuori pit)");
            AssertClose(s.DeltaGap, 0.3, 1e-9, "DeltaGap non deve contenere il salto della sosta");
            Assert(s.EmaSeeded, "l'EMA era invalidata: il primo rate pulito deve inizializzarla");
            AssertClose(t.RelativePace, s.InstantRate, 1e-9, "EMA = InstantRate al primo campione post-pit");
            Assert(Math.Abs(t.RelativePace) < 10.0, "il rate pulito non deve saturare il clamp");

            Console.WriteLine("  [PASS] Test8_PlayerPit_NoDeltaAcrossPit");
        }

        // ---------------------------------------------------------------
        // Test 9 — OBBLIGATORIO. Target ai box, Player in pista.
        // ---------------------------------------------------------------
        private static void Test_TargetPit_NoDeltaAcrossPit()
        {
            var t = new RelativePaceTracker();

            t.ProcessSample(2, Clock(0), -3.0, false, false, RefLapTime);
            t.ProcessSample(3, Clock(1), -3.1, false, false, RefLapTime);
            var s = t.ProcessSample(4, Clock(2), -3.2, false, false, RefLapTime);
            Assert(s.RateComputed, "il regime pulito deve produrre rate");
            double frozenPace = t.RelativePace;

            // Target ai box: il gap cambia di 20s per la sosta dell'avversario
            s = t.ProcessSample(5, Clock(3), -24.0, false, true, RefLapTime);
            Assert(s.Reason == RelativePaceInvalidationReason.TargetInPit, "reason deve essere TargetInPit");
            Assert(!s.RateComputed, "nessun rate con il target ai box");
            AssertClose(t.RelativePace, frozenPace, 1e-9, "RelativePace congelato");

            // Target rientra in pista
            s = t.ProcessSample(6, Clock(4), -24.5, false, false, RefLapTime);
            Assert(s.SequenceValid, "5 -> 6 è numericamente valida");
            Assert(!s.RateComputed, "REGRESSIONE RED-1: nessun rate con reference presa durante il pit del target");
            Assert(s.WasPostPitSeed, "primo campione pulito = seed post-pit");

            s = t.ProcessSample(7, Clock(5), -24.7, false, false, RefLapTime);
            Assert(s.RateComputed, "6 -> 7 deve produrre il primo rate pulito");
            AssertClose(s.PreviousGap, -24.5, 1e-9, "reference = gap@6");
            AssertClose(s.DeltaGap, -0.2, 1e-9, "DeltaGap pulito");

            Console.WriteLine("  [PASS] Test9_TargetPit_NoDeltaAcrossPit");
        }

        /// <summary>
        /// Sosta lunga su più macrosettori: il flag di contaminazione deve sopravvivere
        /// a tutta la permanenza ai box, non solo all'ultimo campione.
        /// </summary>
        private static void Test_PitContamination_SurvivesMultiSectorStop()
        {
            var t = new RelativePaceTracker();

            t.ProcessSample(1, Clock(0), 4.0, false, false, RefLapTime);
            t.ProcessSample(2, Clock(1), 4.1, false, false, RefLapTime);
            double frozenPace = t.RelativePace;

            // Quattro macrosettori consecutivi ai box
            for (int sector = 3; sector <= 6; sector++)
            {
                var inPit = t.ProcessSample(sector, Clock(sector - 2), 4.1 + (sector * 5.0), true, false, RefLapTime);
                Assert(!inPit.RateComputed, $"nessun rate al settore {sector} (in pit)");
                Assert(t.PitSeedPending, $"flag ancora alzato al settore {sector}");
            }
            AssertClose(t.RelativePace, frozenPace, 1e-9, "RelativePace congelato per tutta la sosta");

            var seed = t.ProcessSample(7, Clock(6), 34.0, false, false, RefLapTime);
            Assert(seed.WasPostPitSeed, "uscita dai box: seed obbligatorio");
            Assert(!seed.RateComputed, "uscita dai box: nessun rate");

            var rate = t.ProcessSample(8, Clock(7), 34.4, false, false, RefLapTime);
            Assert(rate.RateComputed, "il campione dopo il seed post-pit deve produrre il rate");
            AssertClose(rate.PreviousGap, 34.0, 1e-9, "reference = ultimo campione fuori pit");

            Console.WriteLine("  [PASS] Test_PitContamination_SurvivesMultiSectorStop");
        }

        // ---------------------------------------------------------------
        // Spec §10 — il cambio target azzera, non congela
        // ---------------------------------------------------------------
        private static void Test_TargetChange_ResetsToZero()
        {
            var t = new RelativePaceTracker();

            t.ProcessSample(1, Clock(0), 5.0, false, false, RefLapTime);
            t.ProcessSample(2, Clock(1), 6.0, false, false, RefLapTime);
            Assert(Math.Abs(t.RelativePace) > 1e-9, "serve un RelativePace non nullo per il test");

            t.Reset();
            AssertClose(t.RelativePace, 0.0, 1e-12, "il cambio target deve azzerare RelativePace");
            Assert(t.LastValidMacroSector == -1, "il cambio target deve invalidare la reference");
            Assert(!t.PitSeedPending, "il cambio target deve azzerare anche il flag di contaminazione");

            var s = t.ProcessSample(3, Clock(2), 1.0, false, false, RefLapTime);
            Assert(s.Reason == RelativePaceInvalidationReason.NoPreviousSeed, "dopo il reset serve un nuovo seed");

            Console.WriteLine("  [PASS] Test10_TargetChange_ResetsToZero");
        }

        // ---------------------------------------------------------------
        // Spec §3.1 e §4 — formula istantanea, EMA, clamp
        // ---------------------------------------------------------------
        private static void Test_EmaAndClamp()
        {
            var t = new RelativePaceTracker();

            // Seed
            t.ProcessSample(1, 1000.0, 0.0, false, false, RefLapTime);

            // dt = 10s, deltaGap = +0.5s, refLap = 100s -> instantRate = 0.5/10*100 = 5.0
            var s = t.ProcessSample(2, 990.0, 0.5, false, false, RefLapTime);
            AssertClose(s.DeltaTime, 10.0, 1e-9, "DeltaTime su clock a scalare");
            AssertClose(s.InstantRate, 5.0, 1e-9, "InstantRate = (deltaGap/dt) * refLapTime");
            Assert(s.EmaSeeded, "il primo rate deve inizializzare l'EMA");
            AssertClose(t.RelativePace, 5.0, 1e-9, "EMA inizializzata al valore istantaneo");

            // Secondo campione: instantRate = 1.0/10*100 = 10.0
            // EMA = 0.30*10.0 + 0.70*5.0 = 3.0 + 3.5 = 6.5
            s = t.ProcessSample(3, 980.0, 1.5, false, false, RefLapTime);
            Assert(!s.EmaSeeded, "il secondo rate deve aggiornare l'EMA, non inizializzarla");
            AssertClose(s.InstantRate, 10.0, 1e-9, "InstantRate secondo campione");
            AssertClose(t.RelativePace, 6.5, 1e-9, "EMA = 0.30*instant + 0.70*old");

            // Clamp: deltaGap enorme
            s = t.ProcessSample(4, 970.0, 101.5, false, false, RefLapTime);
            AssertClose(s.InstantRate, 1000.0, 1e-6, "InstantRate saturante");
            AssertClose(t.RelativePace, 10.0, 1e-9, "il clamp deve limitare a +10.0");
            Assert(s.Clamped, "il flag Clamped deve segnalare la saturazione");

            // Clamp negativo
            var t2 = new RelativePaceTracker();
            t2.ProcessSample(1, 1000.0, 0.0, false, false, RefLapTime);
            s = t2.ProcessSample(2, 990.0, -100.0, false, false, RefLapTime);
            AssertClose(t2.RelativePace, -10.0, 1e-9, "il clamp deve limitare a -10.0");

            Console.WriteLine("  [PASS] Test_EmaAndClamp");
        }

        /// <summary>
        /// L'header dello snapshot e l'array di campi sono definiti in file diversi:
        /// se divergono, il CSV diventa illeggibile senza che nulla fallisca.
        /// </summary>
        private static void Test_SnapshotHeaderMatchesFieldCount()
        {
            int columns = LogManager.SnapshotColumnCount;
            Assert(columns == 62, $"header snapshot: attese 62 colonne, trovate {columns}");

            string header = LogManager.SnapshotHeader;
            foreach (string required in new[] { "DeltaGap", "DeltaTime", "InstantPace", "PrevGap",
                                                "SeqValid", "InvalidReason", "PitSeedPending", "PostPitSeed",
                                                "MaxStayLaps", "PlayerTrackPace", "WarmupFallback", "PositiveGap" })
            {
                Assert(header.Contains(required), $"colonna mancante nell'header snapshot: {required}");
            }

            Console.WriteLine("  [PASS] Test_SnapshotHeaderMatchesFieldCount");
        }
    }
}
