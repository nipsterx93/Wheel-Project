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
            Test_PostPitSettling_PreventsClampSaturation();

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

            // Uscita dai box. Le sequenze restano NUMERICAMENTE VALIDE (il macrosettore avanza
            // anche in pit lane): è proprio il caso insidioso. Si scartano PostPitSettlingSectors
            // macrosettori perché la vettura sta ancora rientrando e accelerando.
            int settling = RelativePaceTracker.PostPitSettlingSectors;
            double[] gapsWhileSettling = { 28.0, 30.5, 31.2, 31.4, 31.5, 31.6 };

            for (int i = 0; i < settling; i++)
            {
                s = t.ProcessSample(10 + i, Clock(4 + i), gapsWhileSettling[i], false, false, RefLapTime);
                Assert(s.SequenceValid, $"settore {10 + i}: la sequenza è numericamente valida");
                Assert(!s.RateComputed, $"REGRESSIONE: nessun rate durante l'assestamento (settore {10 + i})");
                Assert(s.WasPostPitSeed, $"settore {10 + i} deve essere un seed di assestamento");
                AssertClose(t.RelativePace, frozenPace, 1e-9, "RelativePace congelato per tutto l'assestamento");
                Assert(s.PostPitSectorsRemaining == settling - 1 - i,
                    $"contatore residuo errato al settore {10 + i}: {s.PostPitSectorsRemaining}");
            }
            Assert(!t.PitSeedPending, "la finestra di assestamento deve essere esaurita");

            // Primo rate pulito: reference = ultimo campione dell'assestamento
            double lastSettlingGap = gapsWhileSettling[settling - 1];
            s = t.ProcessSample(10 + settling, Clock(4 + settling), lastSettlingGap + 0.3, false, false, RefLapTime);
            Assert(s.RateComputed, "dopo l'assestamento deve arrivare il primo rate pulito");
            AssertClose(s.PreviousGap, lastSettlingGap, 1e-9, "la reference deve essere l'ultimo seed di assestamento");
            AssertClose(s.DeltaGap, 0.3, 1e-9, "DeltaGap non deve contenere né la sosta né il rientro");
            Assert(s.EmaSeeded, "l'EMA era invalidata: il primo rate pulito deve inizializzarla");
            AssertClose(t.RelativePace, s.InstantRate, 1e-9, "EMA = InstantRate al primo campione post-assestamento");
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

            // Target rientra in pista: stessa finestra di assestamento del caso Player
            int settling = RelativePaceTracker.PostPitSettlingSectors;
            double[] gapsWhileSettling = { -24.5, -25.9, -26.2, -26.3, -26.4, -26.5 };

            for (int i = 0; i < settling; i++)
            {
                s = t.ProcessSample(6 + i, Clock(4 + i), gapsWhileSettling[i], false, false, RefLapTime);
                Assert(s.SequenceValid, $"settore {6 + i}: sequenza numericamente valida");
                Assert(!s.RateComputed, $"REGRESSIONE: nessun rate durante l'assestamento (settore {6 + i})");
                Assert(s.WasPostPitSeed, $"settore {6 + i} deve essere un seed di assestamento");
                AssertClose(t.RelativePace, frozenPace, 1e-9, "RelativePace congelato");
            }

            double lastSettlingGap = gapsWhileSettling[settling - 1];
            s = t.ProcessSample(6 + settling, Clock(4 + settling), lastSettlingGap - 0.2, false, false, RefLapTime);
            Assert(s.RateComputed, "dopo l'assestamento deve arrivare il primo rate pulito");
            AssertClose(s.PreviousGap, lastSettlingGap, 1e-9, "reference = ultimo seed di assestamento");
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

            // Il contatore di assestamento riparte pieno a ogni campione in pit: qui erano quattro,
            // quindi all'uscita restano comunque PostPitSettlingSectors macrosettori da scartare.
            int settling = RelativePaceTracker.PostPitSettlingSectors;
            Assert(t.PostPitSectorsRemaining == settling,
                $"il contatore deve essere pieno all'uscita: {t.PostPitSectorsRemaining}");

            double gap = 34.0;
            for (int i = 0; i < settling; i++)
            {
                var seed = t.ProcessSample(7 + i, Clock(6 + i), gap, false, false, RefLapTime);
                Assert(seed.WasPostPitSeed, $"assestamento in corso al settore {7 + i}");
                Assert(!seed.RateComputed, $"nessun rate durante l'assestamento (settore {7 + i})");
                gap += 0.4;
            }

            var rate = t.ProcessSample(7 + settling, Clock(6 + settling), gap, false, false, RefLapTime);
            Assert(rate.RateComputed, "dopo l'assestamento deve arrivare il rate");
            AssertClose(rate.PreviousGap, gap - 0.4, 1e-9, "reference = ultimo seed di assestamento");

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
        /// Regressione dal replay reale del 2026-08-18: con un solo macrosettore di attesa,
        /// tutte e tre le soste producevano un primo rate che saturava il clamp (±10 s/giro),
        /// perché il campione veniva preso mentre la vettura stava ancora rientrando in pista.
        /// Qui si riproduce quel profilo di gap e si verifica che l'assestamento lo assorba.
        /// </summary>
        private static void Test_PostPitSettling_PreventsClampSaturation()
        {
            var t = new RelativePaceTracker();

            // Regime pulito prima della sosta
            t.ProcessSample(0, Clock(0), 9.0, false, false, RefLapTime);
            t.ProcessSample(1, Clock(1), 9.1, false, false, RefLapTime);
            double paceBeforePit = t.RelativePace;

            // Sosta del Player
            t.ProcessSample(2, Clock(2), 9.2, true, false, RefLapTime);

            // Profilo di rientro misurato nel replay: il gap fa un balzo di +2.5s in un
            // macrosettore (uscita box + accelerazione), poi si stabilizza.
            double[] rejoin = { 9.204, 11.719, 11.661, 11.640, 11.628, 11.615 };
            int settling = RelativePaceTracker.PostPitSettlingSectors;

            RelativePaceSample s = default(RelativePaceSample);
            for (int i = 0; i < settling; i++)
            {
                s = t.ProcessSample(3 + i, Clock(3 + i), rejoin[i], false, false, RefLapTime);
                Assert(!s.RateComputed, $"il balzo del rientro non deve produrre un rate (settore {3 + i})");
            }
            AssertClose(t.RelativePace, paceBeforePit, 1e-9,
                "RelativePace deve restare congelato su tutto il rientro");

            // Primo rate reale, ormai su gap stabilizzati
            s = t.ProcessSample(3 + settling, Clock(3 + settling), rejoin[settling], false, false, RefLapTime);
            Assert(s.RateComputed, "dopo l'assestamento deve arrivare il rate");
            Assert(!s.Clamped,
                $"REGRESSIONE: il primo rate post-pit satura ancora il clamp (instantRate={s.InstantRate:F2})");
            Assert(Math.Abs(t.RelativePace) < RelativePaceTracker.ClampLimit,
                $"RelativePace incollato alla sbarra: {t.RelativePace:F2}");

            Console.WriteLine("  [PASS] Test_PostPitSettling_PreventsClampSaturation");
        }

        /// <summary>
        /// L'header dello snapshot e l'array di campi sono definiti in file diversi:
        /// se divergono, il CSV diventa illeggibile senza che nulla fallisca.
        /// </summary>
        private static void Test_SnapshotHeaderMatchesFieldCount()
        {
            int columns = LogManager.SnapshotColumnCount;
            Assert(columns == 63, $"header snapshot: attese 63 colonne, trovate {columns}");

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
