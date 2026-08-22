// -------------------------------------------------------------------------
// FILE: TrackPositionValidatorUnitTests.cs
// Guard anti-teletrasporto per l'autorizzazione della calibrazione geofence.
// Il caso da escludere: uscita dai box, ESC a meta' giro, rientro istantaneo.
// -------------------------------------------------------------------------

using System;
using SimRIG;

namespace User.PluginSdkDemo.Tests
{
    public class TrackPositionValidatorUnitTests
    {
        // Misano ~4200 m. A 4200 m e dt=0.5 s il margine e' 120*0.5/4200 = 0.0143 di giro.
        private const double MisanoMeters = 4200.0;

        // Daytona road course ~5730 m: margine piu' stretto in frazione di giro.
        private const double DaytonaMeters = 5730.0;

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception("[TrackPositionValidator] " + message);
        }

        private static void AssertClose(double actual, double expected, string message)
        {
            if (Math.Abs(actual - expected) > 1e-9)
                throw new Exception($"[TrackPositionValidator] {message} — atteso {expected:F6}, ottenuto {actual:F6}");
        }

        private static void Pass(string name)
        {
            Console.WriteLine("  [PASS] " + name);
        }

        public static void RunAllTests()
        {
            Console.WriteLine("[TEST] Running Track Position Validator Tests...");

            Test_WrappedDelta_CrossesStartLine();
            Test_FirstSampleIsUnknown();
            Test_NormalDrivingIsContinuous();
            Test_Regression_TeleportIsDiscontinuous();
            Test_ZeroPositionResetsState();
            Test_LongGapIsNotEvaluable();
            Test_UnknownTrackLengthDisablesGuard();
            Test_MarginScalesWithTrackLength();
            Test_ResetClearsHistory();

            Console.WriteLine("[TEST SUCCESS] All Track Position Validator Tests Passed!");
        }

        private static void Test_WrappedDelta_CrossesStartLine()
        {
            AssertClose(TrackPositionValidator.WrappedDelta(0.01, 0.99), 0.02,
                        "0.99 -> 0.01 e' un avanzamento di 0.02, non un salto all'indietro");
            AssertClose(TrackPositionValidator.WrappedDelta(0.99, 0.01), -0.02,
                        "e specularmente all'indietro");
            AssertClose(TrackPositionValidator.WrappedDelta(0.52, 0.50), 0.02,
                        "a meta' giro nessun wrap da applicare");

            Pass("Test_WrappedDelta_CrossesStartLine");
        }

        private static void Test_FirstSampleIsUnknown()
        {
            var v = new TrackPositionValidator();
            Assert(v.Update(0.5, 1000.0, MisanoMeters) == PositionContinuity.Unknown,
                   "senza un campione precedente non si puo' giudicare la continuita'");

            Pass("Test_FirstSampleIsUnknown");
        }

        private static void Test_NormalDrivingIsContinuous()
        {
            var v = new TrackPositionValidator();
            v.Update(0.500, 1000.0, MisanoMeters);

            // ~50 m in 0.5 s = 360 km/h: veloce ma plausibile su un rettilineo.
            Assert(v.Update(0.512, 999.5, MisanoMeters) == PositionContinuity.Continuous,
                   "un avanzamento da rettilineo deve risultare continuo");

            // Andatura lenta.
            Assert(v.Update(0.514, 999.0, MisanoMeters) == PositionContinuity.Continuous,
                   "un avanzamento lento deve risultare continuo");

            Pass("Test_NormalDrivingIsContinuous");
        }

        /// <summary>
        /// Il caso per cui esiste questo guard: il pilota esce dai box, guida fino a meta' giro,
        /// preme ESC e viene riportato ai box. La posizione salta di mezzo tracciato in un tick.
        /// </summary>
        private static void Test_Regression_TeleportIsDiscontinuous()
        {
            var v = new TrackPositionValidator();
            v.Update(0.450, 1000.0, MisanoMeters);

            // ESC: da meta' giro all'ingresso box in un solo campione.
            Assert(v.Update(0.950, 999.5, MisanoMeters) == PositionContinuity.Discontinuous,
                   "REGRESSIONE: un salto di mezzo giro in 0.5 s non e' guida, e' un teletrasporto");

            // Anche un salto piu' piccolo ma ancora impossibile deve essere respinto.
            var v2 = new TrackPositionValidator();
            v2.Update(0.500, 1000.0, MisanoMeters);
            Assert(v2.Update(0.560, 999.5, MisanoMeters) == PositionContinuity.Discontinuous,
                   "252 m in 0.5 s sono oltre 1800 km/h: impossibile");

            Pass("Test_Regression_TeleportIsDiscontinuous");
        }

        private static void Test_ZeroPositionResetsState()
        {
            var v = new TrackPositionValidator();
            v.Update(0.500, 1000.0, MisanoMeters);

            // Convenzione gia' in uso nel progetto: 0.0 = stato azzerato, non inizio giro.
            Assert(v.Update(0.0, 999.5, MisanoMeters) == PositionContinuity.Unknown,
                   "posizione a zero significa stato azzerato, non una posizione da giudicare");

            // Dopo lo zero la storia riparte: il campione successivo non puo' essere giudicato.
            Assert(v.Update(0.500, 999.0, MisanoMeters) == PositionContinuity.Unknown,
                   "dopo un azzeramento serve un nuovo campione di riferimento");

            Pass("Test_ZeroPositionResetsState");
        }

        private static void Test_LongGapIsNotEvaluable()
        {
            var v = new TrackPositionValidator();
            v.Update(0.500, 1000.0, MisanoMeters);

            // Pausa o salto del replay: il margine diventerebbe cosi' ampio da validare tutto.
            Assert(v.Update(0.900, 990.0, MisanoMeters) == PositionContinuity.Unknown,
                   "con 10 s fra i campioni il confronto non e' significativo");

            Pass("Test_LongGapIsNotEvaluable");
        }

        private static void Test_UnknownTrackLengthDisablesGuard()
        {
            var v = new TrackPositionValidator();
            Assert(v.Update(0.500, 1000.0, 0.0) == PositionContinuity.Unknown,
                   "senza lunghezza pista non si giudica: meglio nessun verdetto che uno inventato");
            Assert(v.Update(0.900, 999.5, 0.0) == PositionContinuity.Unknown,
                   "il guard resta disattivato finche' la lunghezza non e' nota");

            Pass("Test_UnknownTrackLengthDisablesGuard");
        }

        /// <summary>
        /// Lo stesso avanzamento in frazione di giro e' piu' sospetto su una pista lunga:
        /// l'1% di Daytona sono piu' metri dell'1% di Misano.
        /// </summary>
        private static void Test_MarginScalesWithTrackLength()
        {
            var shortTrack = new TrackPositionValidator();
            shortTrack.Update(0.500, 1000.0, MisanoMeters);
            var shortResult = shortTrack.Update(0.5125, 999.5, MisanoMeters);

            var longTrack = new TrackPositionValidator();
            longTrack.Update(0.500, 1000.0, DaytonaMeters);
            var longResult = longTrack.Update(0.5125, 999.5, DaytonaMeters);

            Assert(shortResult == PositionContinuity.Continuous,
                   "52.5 m in 0.5 s su Misano sono plausibili");
            Assert(longResult == PositionContinuity.Discontinuous,
                   "gli stessi 1.25 punti percentuali su Daytona sono 71.6 m in 0.5 s: oltre il tetto");

            Pass("Test_MarginScalesWithTrackLength");
        }

        private static void Test_ResetClearsHistory()
        {
            var v = new TrackPositionValidator();
            v.Update(0.500, 1000.0, MisanoMeters);
            v.Update(0.510, 999.5, MisanoMeters);
            Assert(v.LastResult == PositionContinuity.Continuous, "precondizione: continuo");

            v.Reset();
            Assert(v.LastResult == PositionContinuity.Unknown, "Reset deve azzerare l'esito");
            Assert(v.Update(0.900, 999.0, MisanoMeters) == PositionContinuity.Unknown,
                   "dopo il reset serve un nuovo riferimento prima di poter giudicare");

            Pass("Test_ResetClearsHistory");
        }
    }
}
