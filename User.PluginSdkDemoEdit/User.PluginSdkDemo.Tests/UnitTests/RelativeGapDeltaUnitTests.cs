// -------------------------------------------------------------------------
// FILE: RelativeGapDeltaUnitTests.cs
// RelativeGapDelta = SignedGap_current - SignedGap_previous, in secondi per
// MACROSETTORE (non secondi/giro). Grezzo: niente EMA, niente clamp, niente
// normalizzazione temporale. Valido solo sul macrosettore pulito in cui il
// tracker ha davvero prodotto un delta.
// -------------------------------------------------------------------------

using System;
using SimRIG;

namespace User.PluginSdkDemo.Tests
{
    public class RelativeGapDeltaUnitTests
    {
        private const double RefLapTime = 100.0;

        private static double Clock(int step) { return 3600.0 - (step * 5.0); }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception("[RelativeGapDelta] " + message);
        }

        private static void AssertClose(double actual, double expected, string message)
        {
            if (Math.Abs(actual - expected) > 1e-9)
                throw new Exception($"[RelativeGapDelta] {message} — atteso {expected:F4}, ottenuto {actual:F4}");
        }

        public static void RunAllTests()
        {
            Console.WriteLine("[TEST] Running Relative Gap Delta Tests...");

            Test_SignConvention_MandatoryExamples();
            Test_RawValue_NoEmaNoClamp();
            Test_InvalidDuringPitAndPostPitSeed();
            Test_InvalidOnBrokenSequenceAndSmallDeltaTime();
            Test_FormatString_UsesSectorUnit();

            Console.WriteLine("[TEST SUCCESS] All Relative Gap Delta Tests Passed!");
        }

        /// <summary>
        /// Esempi obbligatori della specifica:
        ///   gap +0.7 -> +0.5  =>  -0.2  (Player guadagna)
        ///   gap +0.5 -> +0.8  =>  +0.3  (Player perde)
        /// </summary>
        private static void Test_SignConvention_MandatoryExamples()
        {
            var t = new RelativePaceTracker();

            // Seed a gap +0.7
            t.ProcessSample(1, Clock(0), 0.7, false, false, RefLapTime);

            // +0.7 -> +0.5 : il Player recupera 0.2s
            var s = t.ProcessSample(2, Clock(1), 0.5, false, false, RefLapTime);
            Assert(s.RateComputed, "il campione pulito deve produrre un delta");
            AssertClose(s.DeltaGap, -0.2, "gap +0.7 -> +0.5 deve dare -0.2 (Player guadagna)");
            Assert(s.DeltaGap < 0.0, "guadagnare terreno deve dare segno negativo");

            // +0.5 -> +0.8 : il Player perde 0.3s
            s = t.ProcessSample(3, Clock(2), 0.8, false, false, RefLapTime);
            AssertClose(s.DeltaGap, 0.3, "gap +0.5 -> +0.8 deve dare +0.3 (Player perde)");
            Assert(s.DeltaGap > 0.0, "perdere terreno deve dare segno positivo");

            // Vale anche con il Player davanti (gap negativo): da -2.0 a -2.5 il Player allunga
            var t2 = new RelativePaceTracker();
            t2.ProcessSample(1, Clock(0), -2.0, false, false, RefLapTime);
            s = t2.ProcessSample(2, Clock(1), -2.5, false, false, RefLapTime);
            AssertClose(s.DeltaGap, -0.5, "gap -2.0 -> -2.5 deve dare -0.5 (Player allunga)");

            Console.WriteLine("  [PASS] Test_SignConvention_MandatoryExamples");
        }

        /// <summary>
        /// Il delta è grezzo: non deve subire né EMA né clamp, a differenza di RelativePace
        /// che sullo stesso campione viene invece limitato a ±10.
        /// </summary>
        private static void Test_RawValue_NoEmaNoClamp()
        {
            var t = new RelativePaceTracker();
            t.ProcessSample(1, 1000.0, 0.0, false, false, RefLapTime);

            // deltaGap = +25s su dt = 10s con refLap 100s -> instantRate = 250 s/giro, clampato a 10
            var s = t.ProcessSample(2, 990.0, 25.0, false, false, RefLapTime);

            AssertClose(s.DeltaGap, 25.0, "il delta grezzo non deve essere clampato");
            AssertClose(t.RelativePace, 10.0, "RelativePace invece deve restare clampato a +10");
            Assert(s.Clamped, "il clamp deve risultare applicato al solo RelativePace");

            // Secondo campione: RelativePace applica l'EMA, il delta no
            s = t.ProcessSample(3, 980.0, 25.4, false, false, RefLapTime);
            AssertClose(s.DeltaGap, 0.4, "il delta deve essere la differenza pura fra i due gap");
            Assert(Math.Abs(t.RelativePace - 0.4) > 1.0, "RelativePace deve essere mediato, non uguale al delta");

            Console.WriteLine("  [PASS] Test_RawValue_NoEmaNoClamp");
        }

        /// <summary>
        /// Durante il pit e sul seed post-pit non esiste una misura: il consumatore non deve
        /// ricevere uno zero spacciato per "nessuna variazione".
        /// Copre anche la non-regressione di RED-1 dal lato del nuovo valore.
        /// </summary>
        private static void Test_InvalidDuringPitAndPostPitSeed()
        {
            var t = new RelativePaceTracker();

            t.ProcessSample(6, Clock(0), 5.0, false, false, RefLapTime);
            var s = t.ProcessSample(7, Clock(1), 5.2, false, false, RefLapTime);
            Assert(s.RateComputed, "regime pulito: delta valido");
            AssertClose(s.DeltaGap, 0.2, "delta del regime pulito");

            // Player ai box: il gap esplode per la sosta
            s = t.ProcessSample(8, Clock(2), 27.0, true, false, RefLapTime);
            Assert(!s.RateComputed, "durante il pit il delta non è valido");

            // Uscita dai box: sequenza numericamente valida, ma il riferimento è contaminato
            s = t.ProcessSample(9, Clock(3), 28.0, false, false, RefLapTime);
            Assert(s.WasPostPitSeed, "primo campione pulito post-pit = seed");
            Assert(!s.RateComputed, "sul seed post-pit il delta non è valido");

            // Primo delta valido solo al macrosettore pulito successivo
            s = t.ProcessSample(10, Clock(4), 28.3, false, false, RefLapTime);
            Assert(s.RateComputed, "il macrosettore successivo deve produrre il primo delta valido");
            AssertClose(s.DeltaGap, 0.3, "il delta non deve contenere il salto della sosta");
            Assert(Math.Abs(s.DeltaGap) < 1.0, "REGRESSIONE RED-1: il delta ha attraversato il pit");

            // Stesso comportamento con il Target ai box
            var t2 = new RelativePaceTracker();
            t2.ProcessSample(1, Clock(0), -3.0, false, false, RefLapTime);
            t2.ProcessSample(2, Clock(1), -3.1, false, false, RefLapTime);
            s = t2.ProcessSample(3, Clock(2), -24.0, false, true, RefLapTime);
            Assert(!s.RateComputed, "target ai box: delta non valido");
            s = t2.ProcessSample(4, Clock(3), -24.5, false, false, RefLapTime);
            Assert(!s.RateComputed, "seed post-pit del target: delta non valido");
            s = t2.ProcessSample(5, Clock(4), -24.7, false, false, RefLapTime);
            Assert(s.RateComputed, "primo delta valido dopo il rientro del target");
            AssertClose(s.DeltaGap, -0.2, "delta pulito dopo il pit del target");

            Console.WriteLine("  [PASS] Test_InvalidDuringPitAndPostPitSeed");
        }

        private static void Test_InvalidOnBrokenSequenceAndSmallDeltaTime()
        {
            // Sequenza rotta
            var t = new RelativePaceTracker();
            t.ProcessSample(4, Clock(0), 1.0, false, false, RefLapTime);
            var s = t.ProcessSample(7, Clock(1), 1.4, false, false, RefLapTime);
            Assert(!s.RateComputed, "salto di macrosettore: delta non valido");

            s = t.ProcessSample(8, Clock(2), 1.6, false, false, RefLapTime);
            Assert(s.RateComputed, "delta valido al macrosettore pulito successivo");
            AssertClose(s.DeltaGap, 0.2, "reference = campione che ha ri-seminato, non quello pre-salto");

            // dt troppo piccolo
            var t2 = new RelativePaceTracker();
            t2.ProcessSample(4, 3600.0, 2.0, false, false, RefLapTime);
            s = t2.ProcessSample(5, 3599.6, 2.1, false, false, RefLapTime);
            Assert(!s.RateComputed, "dt < 1s: delta non valido");

            // Nessun seed precedente
            var t3 = new RelativePaceTracker();
            s = t3.ProcessSample(3, Clock(0), 4.0, false, false, RefLapTime);
            Assert(!s.RateComputed, "primo campione assoluto: delta non valido");

            // Cambio target
            var t4 = new RelativePaceTracker();
            t4.ProcessSample(1, Clock(0), 1.0, false, false, RefLapTime);
            t4.ProcessSample(2, Clock(1), 1.5, false, false, RefLapTime);
            t4.Reset();
            s = t4.ProcessSample(3, Clock(2), 9.0, false, false, RefLapTime);
            Assert(!s.RateComputed, "dopo il cambio target il delta non è valido finché non c'è un seed");

            Console.WriteLine("  [PASS] Test_InvalidOnBrokenSequenceAndSmallDeltaTime");
        }

        private static void Test_FormatString_UsesSectorUnit()
        {
            string gaining = TargetStrategyManager.FormatGapDelta(-0.20, true);
            string losing = TargetStrategyManager.FormatGapDelta(0.30, true);
            string invalid = TargetStrategyManager.FormatGapDelta(0.30, false);

            Assert(gaining == "-0.20s/sector", $"formato guadagno errato: '{gaining}'");
            Assert(losing == "+0.30s/sector", $"formato perdita errato: '{losing}'");

            foreach (string s in new[] { gaining, losing, invalid })
            {
                Assert(s.Contains("s/sector"), $"unità mancante in '{s}'");
                Assert(!s.Contains("s/lap"), $"REGRESSIONE: unità s/lap in un valore per macrosettore: '{s}'");
            }

            Assert(!invalid.Contains("0.30"), "un valore non valido non deve esporre la misura stantia");
            Assert(invalid.Contains("--"), $"un valore non valido deve usare un segnaposto: '{invalid}'");

            // Zero misurato davvero è diverso da "nessuna misura"
            string measuredZero = TargetStrategyManager.FormatGapDelta(0.0, true);
            Assert(measuredZero == "0.00s/sector", $"formato zero misurato errato: '{measuredZero}'");
            Assert(measuredZero != invalid, "uno zero misurato non deve essere indistinguibile da un dato assente");

            // Solo ASCII: la dash può usare font senza glifi estesi
            foreach (string s in new[] { gaining, losing, invalid, measuredZero })
            {
                foreach (char ch in s)
                {
                    Assert(ch < 128, $"carattere non ASCII '{ch}' in '{s}'");
                }
            }

            Console.WriteLine("  [PASS] Test_FormatString_UsesSectorUnit");
        }
    }
}
