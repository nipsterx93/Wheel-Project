// -------------------------------------------------------------------------
// FILE: CalibrationCascadeUnitTests.cs
// Y-28: cascata di calibrazione guidata.
// Fase 0a  — riconoscere la sosta senza pretendere 20 L esatti
// Fase 5   — moltiplicatori gomme misurati invece che assunti
// -------------------------------------------------------------------------

using System;
using SimRIG;

namespace User.PluginSdkDemo.Tests
{
    public class CalibrationCascadeUnitTests
    {
        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception("[CalibrationCascade] " + message);
        }

        private static void AssertClose(double actual, double expected, double tolerance, string message)
        {
            if (Math.Abs(actual - expected) > tolerance)
                throw new Exception($"[CalibrationCascade] {message} — atteso {expected:F3}, ottenuto {actual:F3}");
        }

        private static void Pass(string name)
        {
            Console.WriteLine("  [PASS] " + name);
        }

        public static void RunAllTests()
        {
            Console.WriteLine("[TEST] Running Calibration Cascade Tests...");

            // Fase 0a
            Test_Regression_FuelStopNoLongerNeedsExactly20Litres();
            Test_FuelStopRequiresTyresUntouched();
            Test_TinyFuelRequestIsNotACalibration();
            Test_TyreStopAcceptsAnyScope();

            // Fase 5
            Test_Regression_MeasuredMultiplierReplacesTheAssumedHalf();
            Test_MultiplierRejectsUnusableMeasurements();
            Test_FallbackHoldsUntilCalibrated();
            Test_HalfSetScopesAreOneCategory();

            Console.WriteLine("[TEST SUCCESS] All Calibration Cascade Tests Passed!");
        }

        // ------------------------------------------------- Fase 0a

        private static void Test_Regression_FuelStopNoLongerNeedsExactly20Litres()
        {
            // La soglia storica era `fuelToAdd == 20.0`: un numero preso dalla procedura guidata
            // originale, che non serve al calcolo (il rate e' litri/secondi, funziona con qualunque
            // quantita' significativa). Chi ne impostava 18 vedeva fallire la calibrazione senza
            // capire perche'.
            Assert(PitRadar.ClassifyCalibrationMode(18.0, TyreSelectionScope.None) == CalibrationMode.SplashAndDash,
                   "18 litri devono valere come calibrazione carburante");
            Assert(PitRadar.ClassifyCalibrationMode(12.5, TyreSelectionScope.None) == CalibrationMode.SplashAndDash,
                   "e anche 12.5");

            // I 20 esatti devono continuare a funzionare: non si rompe cio' che gia' funzionava.
            Assert(PitRadar.ClassifyCalibrationMode(20.0, TyreSelectionScope.None) == CalibrationMode.SplashAndDash,
                   "i 20 litri storici continuano a valere");
            Pass("Regressione: la sosta carburante non richiede piu' 20 litri esatti");
        }

        private static void Test_FuelStopRequiresTyresUntouched()
        {
            // Su questo non si transige: in una sosta mista il tempo fermo non si separa fra
            // benzina e gomme senza conoscerne gia' una delle due.
            Assert(PitRadar.ClassifyCalibrationMode(20.0, TyreSelectionScope.All4) != CalibrationMode.SplashAndDash,
                   "con le gomme richieste non e' una calibrazione carburante");
            Assert(PitRadar.ClassifyCalibrationMode(20.0, TyreSelectionScope.Fronts) != CalibrationMode.SplashAndDash,
                   "nemmeno con due gomme");
            Pass("La sosta carburante pretende lo scope gomme su NONE");
        }

        private static void Test_TinyFuelRequestIsNotACalibration()
        {
            // Sotto la soglia minima il rapporto litri/secondi non e' significativo.
            Assert(PitRadar.ClassifyCalibrationMode(2.0, TyreSelectionScope.None) != CalibrationMode.SplashAndDash,
                   "due litri non bastano per misurare una portata");
            Assert(PitRadar.ClassifyCalibrationMode(0.0, TyreSelectionScope.None) != CalibrationMode.SplashAndDash,
                   "e zero nemmeno");
            Pass("Una richiesta di carburante troppo piccola non e' una calibrazione");
        }

        private static void Test_TyreStopAcceptsAnyScope()
        {
            // Serve per i moltiplicatori: prima solo All4 attivava la modalita' gomme, quindi una
            // sosta a 2 o 1 gomma non veniva nemmeno riconosciuta come tale.
            Assert(PitRadar.ClassifyCalibrationMode(0.0, TyreSelectionScope.All4) == CalibrationMode.TyreChange,
                   "quattro gomme");
            Assert(PitRadar.ClassifyCalibrationMode(0.0, TyreSelectionScope.Fronts) == CalibrationMode.TyreChange,
                   "due gomme");
            Assert(PitRadar.ClassifyCalibrationMode(0.0, TyreSelectionScope.FL) == CalibrationMode.TyreChange,
                   "una gomma");
            Pass("La sosta gomme riconosce qualunque scope, non solo All4");
        }

        // ------------------------------------------------- Fase 5

        private static void Test_Regression_MeasuredMultiplierReplacesTheAssumedHalf()
        {
            // I numeri sono quelli dell'esempio dell'utente: 27 s per quattro gomme, 13 s per due.
            // Il valore assunto oggi e' esattamente 0.5, quello misurato e' 0.481 — la differenza
            // esiste perche' il tempo dei martinetti e' fisso, quindi dimezzare le gomme non
            // dimezza la sosta.
            double measured = PitRadar.TyreMultiplierFromMeasurement(13.0, 27.0);
            AssertClose(measured, 0.481, 0.001, "13 s su 27 s");

            Assert(Math.Abs(measured - TargetStrategyManager.FallbackHalfSetMultiplier) > 0.01,
                   "il valore misurato deve differire da quello assunto, altrimenti non serviva misurarlo");

            // E una volta misurato, deve essere quello a vincere.
            double used = TargetStrategyManager.GetTireMultiplier(TyreSelectionScope.Fronts, measured, 0.0);
            AssertClose(used, 0.481, 0.001, "il moltiplicatore calibrato sostituisce il cablato");
            Pass("Regressione: 13s/27s da 0.481, e sostituisce lo 0.5 assunto");
        }

        private static void Test_MultiplierRejectsUnusableMeasurements()
        {
            Assert(PitRadar.TyreMultiplierFromMeasurement(13.0, 0.0) == 0.0,
                   "senza riferimento All4 non c'e' rapporto da calcolare");
            Assert(PitRadar.TyreMultiplierFromMeasurement(0.0, 27.0) == 0.0,
                   "una misura nulla non e' una misura");

            // Un cambio parziale piu' lento di quello completo e' un dato sporco (sosta
            // interrotta, riparazione danni), non una misura da conservare.
            Assert(PitRadar.TyreMultiplierFromMeasurement(30.0, 27.0) == 0.0,
                   "un parziale piu' lento dell'intero va rifiutato");
            Pass("Le misure inutilizzabili non producono un moltiplicatore");
        }

        private static void Test_FallbackHoldsUntilCalibrated()
        {
            // Zero significa "mai misurato", non "istantaneo": deve valere il cablato.
            AssertClose(TargetStrategyManager.GetTireMultiplier(TyreSelectionScope.Fronts, 0.0, 0.0),
                        TargetStrategyManager.FallbackHalfSetMultiplier, 1e-9,
                        "due gomme non calibrate usano il fallback");
            AssertClose(TargetStrategyManager.GetTireMultiplier(TyreSelectionScope.RL, 0.0, 0.0),
                        TargetStrategyManager.FallbackSingleTyreMultiplier, 1e-9,
                        "una gomma non calibrata usa il fallback");

            // All4 e' sempre 1.0 per definizione: e' il riferimento, non un rapporto.
            AssertClose(TargetStrategyManager.GetTireMultiplier(TyreSelectionScope.All4, 0.481, 0.259), 1.0, 1e-9,
                        "quattro gomme restano il riferimento");
            AssertClose(TargetStrategyManager.GetTireMultiplier(TyreSelectionScope.None, 0.481, 0.259), 0.0, 1e-9,
                        "nessuna gomma, nessun tempo");
            Pass("Il fallback cablato regge finche' non arriva una misura vera");
        }

        private static void Test_HalfSetScopesAreOneCategory()
        {
            // Le quattro combinazioni da due gomme sono trattate come una categoria sola, com'e'
            // gia' oggi: si misura un rappresentante, non tutte e quattro.
            Assert(PitRadar.IsHalfSetScope(TyreSelectionScope.Fronts), "Fronts");
            Assert(PitRadar.IsHalfSetScope(TyreSelectionScope.Rears), "Rears");
            Assert(PitRadar.IsHalfSetScope(TyreSelectionScope.Left), "Left");
            Assert(PitRadar.IsHalfSetScope(TyreSelectionScope.Right), "Right");

            Assert(!PitRadar.IsHalfSetScope(TyreSelectionScope.All4), "All4 non e' un mezzo set");
            Assert(!PitRadar.IsHalfSetScope(TyreSelectionScope.FL), "una gomma sola nemmeno");
            Assert(!PitRadar.IsHalfSetScope(TyreSelectionScope.None), "e None tantomeno");
            Pass("Gli scope da due gomme sono una categoria sola");
        }
    }
}
