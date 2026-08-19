// -------------------------------------------------------------------------
// FILE: StrategyHysteresisUnitTests.cs
// Y-12: isteresi dei gate strategici, e finestra di plausibilità del DeltaTime.
// I casi di regressione usano i valori misurati nel replay 20260819_205004,
// non numeri inventati: se il fix regredisce, questi test lo dicono.
// -------------------------------------------------------------------------

using System;
using SimRIG;

namespace User.PluginSdkDemo.Tests
{
    public class StrategyHysteresisUnitTests
    {
        private const double RefLapTime = 100.0;   // macrosettore nominale 5.0 s

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception("[StrategyHysteresis] " + message);
        }

        private static void Pass(string name)
        {
            Console.WriteLine("  [PASS] " + name);
        }

        public static void RunAllTests()
        {
            Console.WriteLine("[TEST] Running Strategy Hysteresis Tests...");

            Test_Latch_HoldsStateInsideBand();
            Test_Latch_FirstSampleIsPlainComparison();
            Test_Latch_PositionThresholdBoundaries();
            Test_Latch_ResetClearsState();
            Test_Dwell_RejectsChangeBeforeMinimum();
            Test_Dwell_AcceptsChangeAfterMinimum();
            Test_Dwell_SurvivesCountdownClock();
            Test_Dwell_ReturningToSameStateCostsNothing();
            Test_Regression_ChurnCollapses();
            Test_DeltaTime_RejectsFragmentOfSector();
            Test_DeltaTime_RejectsSwallowedSectors();
            Test_DeltaTime_WindowScalesWithLapTime();
            Test_DeltaTime_FallsBackWhenLapTimeUnknown();
            Test_GapJump_RejectsLapRolloverWrap();
            Test_GapJump_AllowsRealRacingIncident();
            Test_GapJump_RecoversOnNextCleanSample();

            Console.WriteLine("[TEST SUCCESS] All Strategy Hysteresis Tests Passed!");
        }

        // ------------------------------------------------------------------
        // HysteresisLatch
        // ------------------------------------------------------------------

        private static void Test_Latch_HoldsStateInsideBand()
        {
            var latch = new HysteresisLatch(0.0, 0.15);

            Assert(latch.Update(1.0), "sopra soglia + banda deve essere true");
            Assert(latch.Update(0.10), "dentro la banda deve CONSERVARE true, non ricadere sul confronto secco");
            Assert(latch.Update(-0.10), "dentro la banda deve conservare true anche sotto zero");
            Assert(!latch.Update(-0.30), "sotto soglia - banda deve commutare a false");
            Assert(!latch.Update(0.10), "dentro la banda deve ora conservare false");
            Assert(latch.Update(0.30), "sopra soglia + banda deve tornare true");

            Pass("Test_Latch_HoldsStateInsideBand");
        }

        private static void Test_Latch_FirstSampleIsPlainComparison()
        {
            // Senza stato precedente non c'è nulla da conservare: partire sempre da false
            // introdurrebbe un ritardo artificiale a inizio sessione.
            var a = new HysteresisLatch(0.0, 0.15);
            Assert(a.Update(0.05), "il primo campione sopra soglia deve dare true anche se dentro la banda");

            var b = new HysteresisLatch(0.0, 0.15);
            Assert(!b.Update(-0.05), "il primo campione sotto soglia deve dare false anche se dentro la banda");

            Pass("Test_Latch_FirstSampleIsPlainComparison");
        }

        private static void Test_Latch_PositionThresholdBoundaries()
        {
            // Il gate reale: soglia -0.5, banda 0.25 => entra a >= -0.25, esce sotto -0.75
            var latch = new HysteresisLatch(StrategyGateHysteresis.UndercutPositionThreshold,
                                            StrategyGateHysteresis.PositionHysteresis);

            Assert(!latch.Update(-2.0), "gap molto negativo: gate chiuso");
            Assert(!latch.Update(-0.5), "esattamente sulla soglia non basta piu' ad aprire il gate");
            Assert(!latch.Update(-0.26), "appena dentro la banda non deve aprire");
            Assert(latch.Update(-0.25), "a soglia + banda il gate si apre");
            Assert(latch.Update(-0.74), "appena dentro la banda il gate resta aperto");
            Assert(!latch.Update(-0.76), "oltre soglia - banda il gate si chiude");

            Pass("Test_Latch_PositionThresholdBoundaries");
        }

        private static void Test_Latch_ResetClearsState()
        {
            var latch = new HysteresisLatch(0.0, 0.15);
            latch.Update(5.0);
            Assert(latch.State, "precondizione: stato true");

            latch.Reset();
            Assert(!latch.State, "Reset deve azzerare lo stato");
            Assert(!latch.IsInitialized, "Reset deve togliere l'inizializzazione");
            Assert(!latch.Update(-0.05), "dopo il reset il primo campione torna al confronto secco");

            Pass("Test_Latch_ResetClearsState");
        }

        // ------------------------------------------------------------------
        // DwellFilter
        // ------------------------------------------------------------------

        private static void Test_Dwell_RejectsChangeBeforeMinimum()
        {
            var dwell = new DwellFilter(5.0);

            Assert(dwell.Update(StrategyDecision.Neutral, 1000.0) == StrategyDecision.Neutral,
                   "il primo campione inizializza lo stato");
            Assert(dwell.Update(StrategyDecision.Undercut, 998.0) == StrategyDecision.Neutral,
                   "dopo 2 s il cambio deve essere rifiutato");
            Assert(dwell.Update(StrategyDecision.Undercut, 996.5) == StrategyDecision.Neutral,
                   "dopo 3.5 s ancora rifiutato");

            Pass("Test_Dwell_RejectsChangeBeforeMinimum");
        }

        private static void Test_Dwell_AcceptsChangeAfterMinimum()
        {
            var dwell = new DwellFilter(5.0);
            dwell.Update(StrategyDecision.Neutral, 1000.0);

            Assert(dwell.Update(StrategyDecision.Undercut, 995.0) == StrategyDecision.Undercut,
                   "a esattamente 5 s il cambio deve passare");
            Assert(dwell.Update(StrategyDecision.Neutral, 994.0) == StrategyDecision.Undercut,
                   "il contatore riparte dal cambio accettato");
            Assert(dwell.Update(StrategyDecision.Neutral, 990.0) == StrategyDecision.Neutral,
                   "trascorso il dwell dal nuovo stato, il ritorno passa");

            Pass("Test_Dwell_AcceptsChangeAfterMinimum");
        }

        private static void Test_Dwell_SurvivesCountdownClock()
        {
            // Il session time e' un conto alla rovescia: la durata si misura in valore assoluto.
            var dwell = new DwellFilter(5.0);
            dwell.Update(StrategyDecision.Neutral, 100.0);
            Assert(dwell.TimeInState(94.0) == 6.0, "TimeInState deve essere positivo su clock decrescente");
            Assert(dwell.Update(StrategyDecision.Undercut, 94.0) == StrategyDecision.Undercut,
                   "il cambio deve passare con clock decrescente");

            Pass("Test_Dwell_SurvivesCountdownClock");
        }

        private static void Test_Dwell_ReturningToSameStateCostsNothing()
        {
            var dwell = new DwellFilter(5.0);
            dwell.Update(StrategyDecision.Neutral, 1000.0);

            // Un candidato che coincide con lo stato non deve far ripartire il contatore,
            // altrimenti un'oscillazione rapida bloccherebbe il filtro per sempre.
            dwell.Update(StrategyDecision.Neutral, 999.0);
            dwell.Update(StrategyDecision.Undercut, 998.0);   // rifiutato
            dwell.Update(StrategyDecision.Neutral, 997.0);    // gia' nello stato
            Assert(dwell.Update(StrategyDecision.Undercut, 995.0) == StrategyDecision.Undercut,
                   "il dwell si misura dall'ultimo cambio ACCETTATO, non dall'ultimo candidato");

            Pass("Test_Dwell_ReturningToSameStateCostsNothing");
        }

        // ------------------------------------------------------------------
        // Regressione sul churn
        // ------------------------------------------------------------------

        /// <summary>
        /// Riproduce il meccanismo misurato nel replay 20260819_205004: un gap che oscilla
        /// attorno alla soglia di posizione con l'ampiezza di rumore realmente osservata
        /// (p90 = 0.217 s fra campioni consecutivi), su un target fermo.
        ///
        /// Senza isteresi ogni attraversamento produce un STRATEGY_CHANGED: erano 375 in ~40
        /// minuti, 371 dei quali sullo stesso avversario. La sequenza qui e' deterministica,
        /// non casuale: un test che dipende da un seed non e' una rete di sicurezza.
        /// </summary>
        private static void Test_Regression_ChurnCollapses()
        {
            double[] noise = { 0.00, 0.12, -0.09, 0.18, -0.15, 0.06, -0.21, 0.14, -0.05, 0.19,
                               -0.17, 0.08, -0.12, 0.21, -0.06, 0.11, -0.19, 0.04, -0.14, 0.16 };

            var plain = new HysteresisLatch(StrategyGateHysteresis.UndercutPositionThreshold, 0.0);
            var withHysteresis = new HysteresisLatch(StrategyGateHysteresis.UndercutPositionThreshold,
                                                    StrategyGateHysteresis.PositionHysteresis);

            int plainFlips = 0, hystFlips = 0;
            bool prevPlain = false, prevHyst = false, first = true;

            // Il gap si assesta proprio sulla soglia: il caso peggiore, ed e' quello reale.
            for (int cycle = 0; cycle < 5; cycle++)
            {
                for (int i = 0; i < noise.Length; i++)
                {
                    double gap = StrategyGateHysteresis.UndercutPositionThreshold + noise[i];

                    bool p = plain.Update(gap);
                    bool h = withHysteresis.Update(gap);

                    if (!first)
                    {
                        if (p != prevPlain) plainFlips++;
                        if (h != prevHyst) hystFlips++;
                    }
                    prevPlain = p; prevHyst = h; first = false;
                }
            }

            Assert(plainFlips > 30,
                   $"precondizione: senza isteresi il gate deve oscillare molto (ottenuti {plainFlips})");
            Assert(hystFlips == 0,
                   $"REGRESSIONE Y-12: con banda 0.25 un rumore di ampiezza 0.21 non deve produrre " +
                   $"nessun cambio di stato (ottenuti {hystFlips} su {plainFlips} senza isteresi)");

            // La banda non deve pero' rendere il gate sordo a un movimento vero.
            bool opened = withHysteresis.Update(StrategyGateHysteresis.UndercutPositionThreshold + 0.30);
            Assert(opened, "un movimento oltre la banda deve comunque aprire il gate");
            bool closed = withHysteresis.Update(StrategyGateHysteresis.UndercutPositionThreshold - 0.30);
            Assert(!closed, "un movimento oltre la banda in senso opposto deve chiudere il gate");

            Pass("Test_Regression_ChurnCollapses");
        }

        // ------------------------------------------------------------------
        // Finestra di plausibilità del DeltaTime
        // ------------------------------------------------------------------

        /// <summary>
        /// Caso reale dal replay: dt=1.600 s con gapDelta=3.642 dava instantRate 212.4 s/giro,
        /// e lasciava l'EMA incollata al clamp per le ultime 16 righe della gara.
        /// </summary>
        private static void Test_DeltaTime_RejectsFragmentOfSector()
        {
            var t = new RelativePaceTracker();
            t.ProcessSample(4, 1000.0, -88.750, false, false, RefLapTime);

            var s = t.ProcessSample(5, 998.4, -85.108, false, false, RefLapTime);   // dt = 1.6 s

            Assert(!s.RateComputed,
                   "REGRESSIONE: un dt di 1.6 s su un macrosettore nominale di 5 s non deve produrre un rate");
            Assert(s.Reason == RelativePaceInvalidationReason.DeltaTimeTooSmall,
                   $"il motivo deve essere DeltaTimeTooSmall, ottenuto {s.Reason}");
            Assert(t.RelativePace == 0.0, "un campione degenere non deve muovere l'EMA");

            Pass("Test_DeltaTime_RejectsFragmentOfSector");
        }

        /// <summary>
        /// Caso reale dal replay: dt=20.266 s con gapDelta=13.038 dava instantRate 60.3 s/giro.
        /// Quattro macrosettori persi, trattati come uno solo: la sequenza risultava valida
        /// perche' il numero di settore era avanzato, ma il tempo dice che manca roba.
        /// </summary>
        private static void Test_DeltaTime_RejectsSwallowedSectors()
        {
            var t = new RelativePaceTracker();
            t.ProcessSample(17, 920.0, 1.771, false, false, RefLapTime);

            var s = t.ProcessSample(18, 899.734, 14.809, false, false, RefLapTime);  // dt = 20.266 s

            Assert(!s.RateComputed,
                   "REGRESSIONE: un dt di 20.3 s su un macrosettore nominale di 5 s non deve produrre un rate");
            Assert(s.Reason == RelativePaceInvalidationReason.DeltaTimeTooLarge,
                   $"il motivo deve essere DeltaTimeTooLarge, ottenuto {s.Reason}");
            Assert(t.RelativePace == 0.0, "un campione degenere non deve muovere l'EMA");

            Pass("Test_DeltaTime_RejectsSwallowedSectors");
        }

        private static void Test_DeltaTime_WindowScalesWithLapTime()
        {
            // La finestra e' una frazione del macrosettore nominale, non una costante:
            // 4.7 s a Misano non sono i 6.5 s di un circuito lungo.
            var shortTrack = new RelativePaceTracker();
            shortTrack.ProcessSample(1, 1000.0, 0.0, false, false, 60.0);           // settore 3.0 s
            var a = shortTrack.ProcessSample(2, 997.0, 0.05, false, false, 60.0);   // dt = 3.0 s
            Assert(a.RateComputed, "dt pari al settore nominale deve essere accettato sul circuito corto");

            var longTrack = new RelativePaceTracker();
            longTrack.ProcessSample(1, 1000.0, 0.0, false, false, 200.0);           // settore 10.0 s
            var b = longTrack.ProcessSample(2, 997.0, 0.05, false, false, 200.0);   // dt = 3.0 s
            Assert(!b.RateComputed,
                   "lo stesso dt di 3 s deve essere rifiutato dove il settore nominale e' 10 s");
            Assert(b.Reason == RelativePaceInvalidationReason.DeltaTimeTooSmall,
                   $"atteso DeltaTimeTooSmall, ottenuto {b.Reason}");

            Assert(a.MinDeltaTime == 1.5 && a.MaxDeltaTime == 6.0,
                   $"finestra attesa [1.5, 6.0] sul circuito corto, ottenuta [{a.MinDeltaTime}, {a.MaxDeltaTime}]");
            Assert(b.MinDeltaTime == 5.0 && b.MaxDeltaTime == 20.0,
                   $"finestra attesa [5.0, 20.0] sul circuito lungo, ottenuta [{b.MinDeltaTime}, {b.MaxDeltaTime}]");

            Pass("Test_DeltaTime_WindowScalesWithLapTime");
        }

        private static void Test_DeltaTime_FallsBackWhenLapTimeUnknown()
        {
            // Senza un tempo di giro utilizzabile si ricade sul solo pavimento assoluto:
            // meglio un gate debole che una finestra calcolata su un tempo inventato.
            var t = new RelativePaceTracker();
            t.ProcessSample(1, 1000.0, 0.0, false, false, 0.0);
            var s = t.ProcessSample(2, 985.0, 0.05, false, false, 0.0);   // dt = 15 s

            Assert(s.RateComputed, "senza refLapTime nessun tetto deve essere applicato");
            Assert(s.MinDeltaTime == RelativePaceTracker.MinimumDeltaTime,
                   "il pavimento deve restare MinimumDeltaTime");

            var t2 = new RelativePaceTracker();
            t2.ProcessSample(1, 1000.0, 0.0, false, false, 0.0);
            var s2 = t2.ProcessSample(2, 999.5, 0.05, false, false, 0.0);  // dt = 0.5 s
            Assert(!s2.RateComputed, "il pavimento assoluto deve comunque rifiutare dt < 1 s");

            Pass("Test_DeltaTime_FallsBackWhenLapTimeUnknown");
        }

        // ------------------------------------------------------------------
        // Ampiezza del DeltaGap
        // ------------------------------------------------------------------

        /// <summary>
        /// Caso reale dal replay 20260819_221922, al rollover del contatore giri
        /// (lap 24 sec=19 -> lap 25 sec=0): il gap salta di un giro intero e torna indietro.
        ///   prevGap=-88.767 -> gap=4.267   gapDelta= 93.033  dt=4.000 -> instantRate= 2160.4
        ///   prevGap=  4.267 -> gap=-88.709 gapDelta=-92.976  dt=3.800 -> instantRate=-2279.8
        /// I gate temporali non possono vederlo: dt e' del tutto plausibile.
        /// </summary>
        private static void Test_GapJump_RejectsLapRolloverWrap()
        {
            const double MisanoLap = 93.0;

            var t = new RelativePaceTracker();
            t.ProcessSample(19, 113.2, -88.767, false, false, MisanoLap);

            // il wrap in avanti
            var up = t.ProcessSample(0, 109.2, 4.267, false, false, MisanoLap);
            Assert(!up.RateComputed,
                   "REGRESSIONE: un salto di gap di un giro intero non deve produrre un rate");
            Assert(up.Reason == RelativePaceInvalidationReason.GapJump,
                   $"il motivo deve essere GapJump, ottenuto {up.Reason}");
            Assert(t.RelativePace == 0.0, "il wrap non deve muovere l'EMA");

            // e il ritorno, che senza il gate produceva il secondo clamp
            var down = t.ProcessSample(1, 105.4, -88.709, false, false, MisanoLap);
            Assert(!down.RateComputed, "REGRESSIONE: anche il rientro dal wrap non deve produrre un rate");
            Assert(down.Reason == RelativePaceInvalidationReason.GapJump,
                   $"il motivo deve essere GapJump anche al rientro, ottenuto {down.Reason}");
            Assert(t.RelativePace == 0.0, "nemmeno il rientro deve muovere l'EMA");

            // il DeltaGap resta leggibile per la diagnostica, ma non e' consumabile:
            // RelativeGapDeltaValid deriva da RateComputed, che qui e' false.
            Assert(Math.Abs(up.DeltaGap - 93.034) < 0.01,
                   $"il DeltaGap deve restare nel sample per il log, ottenuto {up.DeltaGap:F3}");

            Pass("Test_GapJump_RejectsLapRolloverWrap");
        }

        /// <summary>
        /// La soglia sta a mezzo giro proprio per non confondere un artefatto con un episodio
        /// di gara: un testacoda o un'uscita di pista del target costano una manciata di secondi
        /// e devono continuare a contare come ritmo, per quanto brutto.
        /// </summary>
        private static void Test_GapJump_AllowsRealRacingIncident()
        {
            const double MisanoLap = 93.0;   // soglia = 46.5 s

            var t = new RelativePaceTracker();
            t.ProcessSample(5, 1000.0, 2.0, false, false, MisanoLap);

            // il target va in testacoda e perde 15 s in un macrosettore: brutto ma vero
            var s = t.ProcessSample(6, 995.0, -13.0, false, false, MisanoLap);
            Assert(s.RateComputed,
                   "un incidente di gara da 15 s deve restare un campione valido, non un GapJump");
            Assert(s.Reason == RelativePaceInvalidationReason.None,
                   $"nessuna invalidazione attesa, ottenuta {s.Reason}");
            Assert(Math.Abs(s.MaxGapDelta - 46.5) < 1e-9,
                   $"soglia attesa 46.5 s su un giro da 93 s, ottenuta {s.MaxGapDelta}");

            Pass("Test_GapJump_AllowsRealRacingIncident");
        }

        /// <summary>
        /// Dopo un wrap il campione diventa la nuova reference e l'EMA e' invalidata:
        /// il primo campione pulito successivo deve ripartire come seed, non trascinarsi dietro
        /// il valore precedente ne' saltare del tutto la misura.
        /// </summary>
        private static void Test_GapJump_RecoversOnNextCleanSample()
        {
            const double MisanoLap = 93.0;

            var t = new RelativePaceTracker();
            t.ProcessSample(10, 1000.0, 1.000, false, false, MisanoLap);
            t.ProcessSample(11, 995.0, 1.100, false, false, MisanoLap);   // rate normale
            Assert(t.RelativePace != 0.0, "precondizione: l'EMA deve essersi mossa");

            var jump = t.ProcessSample(12, 990.0, 94.100, false, false, MisanoLap);
            Assert(jump.Reason == RelativePaceInvalidationReason.GapJump, "precondizione: GapJump");

            var clean = t.ProcessSample(13, 985.0, 94.200, false, false, MisanoLap);
            Assert(clean.RateComputed, "il campione pulito dopo il wrap deve tornare a misurare");
            Assert(clean.EmaSeeded, "e deve ri-inizializzare l'EMA, non aggiornarla");
            Assert(!clean.Clamped, "il primo rate dopo il wrap non deve saturare il clamp");

            Pass("Test_GapJump_RecoversOnNextCleanSample");
        }
    }
}
