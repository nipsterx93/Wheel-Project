// -------------------------------------------------------------------------
// FILE: NaturalPitLearningUnitTests.cs
// Cosa si puo' imparare da una sosta NON guidata. Serve al pilota che salta
// la practice e va diretto in gara: la sua prima sosta vera e' il dato
// migliore disponibile, e prima veniva buttata via.
// -------------------------------------------------------------------------

using System;
using SimRIG;

namespace User.PluginSdkDemo.Tests
{
    public class NaturalPitLearningUnitTests
    {
        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception("[NaturalPitLearning] " + message);
        }

        private static void AssertClose(double actual, double expected, double tol, string message)
        {
            if (Math.Abs(actual - expected) > tol)
                throw new Exception($"[NaturalPitLearning] {message} — atteso {expected:F3}, ottenuto {actual:F3}");
        }

        private static void Pass(string name)
        {
            Console.WriteLine("  [PASS] " + name);
        }

        public static void RunAllTests()
        {
            Console.WriteLine("[TEST] Running Natural Pit Learning Tests...");

            Test_Regression_MisanoFuelOnlyStop();
            Test_TyreOnlyStop();
            Test_MixedStopTeachesNothing();
            Test_TooLittleFuelIsNotUsable();
            Test_DegenerateTimingsAreRejected();
            Test_LearnedRateCannotOverwriteConfirmed();

            Console.WriteLine("[TEST SUCCESS] All Natural Pit Learning Tests Passed!");
        }

        /// <summary>
        /// Il caso reale citato dall'utente, dal replay di Misano: 16 litri erogati in 6.1 s,
        /// nessuna gomma toccata. Deve produrre 2.62 L/s — lo stesso valore che il database
        /// contiene gia' per la GT3, ottenuto per altra via.
        /// </summary>
        private static void Test_Regression_MisanoFuelOnlyStop()
        {
            var observed = PitRadar.ObserveNaturalPitStop(
                fuelRequested: 16.0,
                tyres: TyreSelectionScope.None,
                litresAdded: 16.0,
                fuelingSeconds: 6.1,
                stationarySeconds: 6.1);

            Assert(observed.FuelRateUsable,
                   "REGRESSIONE: una sosta di solo carburante deve insegnare il fill rate");
            AssertClose(observed.FuelFillRate, 2.62, 0.01,
                        "16 L in 6.1 s devono dare 2.62 L/s");
            Assert(!observed.TyreTimeUsable,
                   "senza gomme toccate non si puo' dedurre un tempo di cambio gomme");

            Pass("Test_Regression_MisanoFuelOnlyStop");
        }

        private static void Test_TyreOnlyStop()
        {
            var observed = PitRadar.ObserveNaturalPitStop(
                fuelRequested: 0.0,
                tyres: TyreSelectionScope.All4,
                litresAdded: 0.0,
                fuelingSeconds: 0.0,
                stationarySeconds: 27.0);

            Assert(observed.TyreTimeUsable,
                   "una sosta di sole gomme deve insegnare il tempo di cambio");
            AssertClose(observed.TyreChangeTime, 27.0, 1e-9,
                        "tutto il tempo fermo e' attribuibile alle gomme");
            Assert(!observed.FuelRateUsable,
                   "senza carburante erogato non si puo' dedurre un fill rate");

            Pass("Test_TyreOnlyStop");
        }

        /// <summary>
        /// Il limite dichiarato nel piano: in una sosta mista il tempo fermo non si separa fra
        /// benzina e gomme senza conoscerne gia' una. Meglio non imparare nulla che imparare
        /// un valore inquinato.
        /// </summary>
        private static void Test_MixedStopTeachesNothing()
        {
            var observed = PitRadar.ObserveNaturalPitStop(
                fuelRequested: 40.0,
                tyres: TyreSelectionScope.All4,
                litresAdded: 40.0,
                fuelingSeconds: 15.0,
                stationarySeconds: 30.0);

            Assert(!observed.FuelRateUsable,
                   "in una sosta mista il tempo di erogazione non e' isolabile: niente fill rate");
            Assert(!observed.TyreTimeUsable,
                   "e nemmeno il tempo gomme e' isolabile");

            Pass("Test_MixedStopTeachesNothing");
        }

        private static void Test_TooLittleFuelIsNotUsable()
        {
            // Uno splash da 2 litri: l'errore relativo sul tempo e' troppo grande.
            var observed = PitRadar.ObserveNaturalPitStop(
                fuelRequested: 2.0,
                tyres: TyreSelectionScope.None,
                litresAdded: 2.0,
                fuelingSeconds: 0.8,
                stationarySeconds: 3.0);

            Assert(!observed.FuelRateUsable,
                   "un rifornimento troppo piccolo non e' una base affidabile per il fill rate");

            Pass("Test_TooLittleFuelIsNotUsable");
        }

        private static void Test_DegenerateTimingsAreRejected()
        {
            var zeroTime = PitRadar.ObserveNaturalPitStop(20.0, TyreSelectionScope.None, 20.0, 0.0, 5.0);
            Assert(!zeroTime.FuelRateUsable, "tempo di erogazione nullo: nessuna divisione da fare");

            var instantTyres = PitRadar.ObserveNaturalPitStop(0.0, TyreSelectionScope.All4, 0.0, 0.0, 1.0);
            Assert(!instantTyres.TyreTimeUsable, "un secondo fermo non e' un cambio gomme");

            Pass("Test_DegenerateTimingsAreRejected");
        }

        /// <summary>
        /// L'osservazione naturale vale EstimatedPlayer, quindi non deve poter cancellare una
        /// calibrazione guidata gia' presente.
        /// </summary>
        private static void Test_LearnedRateCannotOverwriteConfirmed()
        {
            Assert(!PitRadar.CanOverwrite(CalibrationConfidence.Confirmed,
                                          CalibrationConfidence.EstimatedPlayer),
                   "una sosta naturale non deve sovrascrivere una calibrazione guidata");

            Assert(PitRadar.CanOverwrite(CalibrationConfidence.EstimatedOpponent,
                                         CalibrationConfidence.EstimatedPlayer),
                   "ma deve sostituire una stima dedotta dagli avversari");

            Assert(PitRadar.CanOverwrite(CalibrationConfidence.Unknown,
                                         CalibrationConfidence.EstimatedPlayer),
                   "e ovviamente vale piu' di nulla");

            Pass("Test_LearnedRateCannotOverwriteConfirmed");
        }
    }
}
