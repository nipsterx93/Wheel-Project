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

            // Fase 0b
            Test_Regression_LongFlickerCaughtOnlyByDuration();
            Test_RealTransitsSurviveBothGuards();
            Test_DerivedFloorIsStricterWhenAvailable();

            // Fase 0c
            Test_Regression_PlayerLearnsLimitFromItsOwnLimiter();
            Test_DecelerationIsNotTheLimit();
            Test_LimiterOffNeverObserves();
            Test_ImplausibleSpeedsAreIgnored();

            // Fasi 1 e 2
            Test_OnlyPracticeAndTestGuideCalibration();
            Test_GenuineLapComesBeforeEverything();
            Test_CascadeOrderIsDriveThroughFuelThenTyres();
            Test_Regression_SpaCaseKnownClassNewTrack();
            Test_KnownTrackNewClassSkipsStraightToTyres();
            Test_MultipliersWaitForTheirDenominator();
            Test_NothingMissingMeansSilence();
            Test_CascadeToleratesDeviation();

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

        // ------------------------------------------------- Fase 0b

        private static void Test_Regression_LongFlickerCaughtOnlyByDuration()
        {
            // Il caso che giustifica il secondo criterio. A 200 km/h un secondo vale 55 m: a Misano
            // (4226 m) sono 0.013 di giro, **sopra** la soglia di distanza di 0.01 — quindi il
            // guard di Y-23, da solo, lo lascerebbe passare.
            const double entry = 0.9400;
            const double exit = 0.9532;   // 0.0132 di giro piu' avanti

            Assert(PitRadar.HasTraversedPitLane(entry, exit),
                   "premessa del test: questo guizzo supera il guard sulla distanza");

            Assert(!PitRadar.IsPitVisitPlausible(entry, exit, 1.0, 0.0),
                   "ma un secondo di permanenza non e' un transito: va scartato");
            Pass("Regressione: uno sfarfallio lungo, che supera la distanza, viene preso dalla durata");
        }

        private static void Test_RealTransitsSurviveBothGuards()
        {
            // I transiti veri misurati sui replay. Un guard che scarta anche questi sarebbe
            // peggiore del difetto.
            Assert(PitRadar.IsPitVisitPlausible(0.9588, 0.0987, 30.9, 0.0),
                   "transito reale di Daytona, 30.9 s");
            Assert(PitRadar.IsPitVisitPlausible(0.9495, 0.0737, 36.0, 0.0),
                   "transito reale di Misano, 36.0 s");

            // E lo sfarfallio originale di Daytona resta scartato da entrambi i criteri.
            Assert(!PitRadar.IsPitVisitPlausible(0.9594, 0.9632, 0.67, 0.0),
                   "lo sfarfallio originale non passa");
            Pass("I transiti reali sopravvivono a entrambi i guard");
        }

        private static void Test_DerivedFloorIsStricterWhenAvailable()
        {
            // Quando il circuito e' noto, il pavimento calcolato (~16 s a Daytona) sostituisce
            // quello fisso di sicurezza, ed e' molto piu' selettivo.
            const double entry = 0.9588;
            const double exit = 0.0987;

            Assert(PitRadar.IsPitVisitPlausible(entry, exit, 10.0, 0.0),
                   "senza pavimento derivato, 10 s passano il minimo fisso");
            Assert(!PitRadar.IsPitVisitPlausible(entry, exit, 10.0, 16.0),
                   "col pavimento derivato di Daytona, 10 s non bastano");
            Assert(PitRadar.IsPitVisitPlausible(entry, exit, 30.9, 16.0),
                   "il transito vero lo supera comunque");
            Pass("Il pavimento derivato dal circuito e' piu' selettivo di quello fisso");
        }

        // ------------------------------------------------- Fase 0c

        private static void Test_Regression_PlayerLearnsLimitFromItsOwnLimiter()
        {
            // Il buco: fino a Y-28 il limite si imparava SOLO dagli avversari, quindi in una
            // Practice da soli in pista — la sessione in cui si calibra — restava a zero per
            // sempre. Ora il Player lo legge dal proprio limitatore.
            var observer = new PlayerPitSpeedObserver();

            // Velocita' stabile a 60 km/h col limitatore inserito, oltre la persistenza richiesta.
            bool observed = false;
            for (double t = 0.0; t <= 2.0 && !observed; t += 0.25)
            {
                observed = observer.Update(true, 60.0, t);
            }

            Assert(observed, "una velocita' stabile col limitatore inserito deve produrre un'osservazione");
            AssertClose(observer.ObservedLimitKmh, 60.0, 0.01, "il limite osservato");
            Pass("Regressione: il Player impara il limite dal proprio limitatore");
        }

        private static void Test_DecelerationIsNotTheLimit()
        {
            // Subito dopo l'inserimento la vettura sta ancora rallentando: quei campioni sono piu'
            // alti del limite vero e non devono essere presi per buoni.
            var observer = new PlayerPitSpeedObserver();

            Assert(!observer.Update(true, 110.0, 0.00), "primo campione, ancora in decelerazione");
            Assert(!observer.Update(true, 95.0, 0.25), "sta scendendo");
            Assert(!observer.Update(true, 80.0, 0.50), "scende ancora");
            Assert(!observer.Update(true, 68.0, 0.75), "quasi");
            Assert(!observer.Update(true, 60.0, 1.00), "arrivato al limite, ma va confermato nel tempo");

            // Da qui in poi e' stabile: dopo la persistenza si accetta.
            bool observed = observer.Update(true, 60.0, 2.60);
            Assert(observed, "stabile abbastanza a lungo, ora vale");
            AssertClose(observer.ObservedLimitKmh, 60.0, 0.01, "e il valore e' il limite, non la decelerazione");
            Pass("La decelerazione iniziale non viene scambiata per il limite");
        }

        private static void Test_LimiterOffNeverObserves()
        {
            // Senza limitatore la velocita' non dice nulla sul limite: si sta guidando.
            var observer = new PlayerPitSpeedObserver();
            for (double t = 0.0; t <= 5.0; t += 0.25)
            {
                Assert(!observer.Update(false, 60.0, t),
                       "col limitatore spento non si osserva nulla, nemmeno a velocita' da corsia box");
            }
            Pass("Col limitatore spento non si osserva mai");
        }

        private static void Test_ImplausibleSpeedsAreIgnored()
        {
            var observer = new PlayerPitSpeedObserver();

            // Fermi ai box: il limitatore puo' essere inserito, ma zero non e' un limite.
            for (double t = 0.0; t <= 5.0; t += 0.25)
            {
                Assert(!observer.Update(true, 0.0, t), "da fermo non si misura un limite");
            }

            // Velocita' da pista: non e' una corsia box.
            var fast = new PlayerPitSpeedObserver();
            for (double t = 0.0; t <= 5.0; t += 0.25)
            {
                Assert(!fast.Update(true, 200.0, t), "200 km/h non e' un limite di corsia box");
            }
            Pass("Le velocita' implausibili non producono osservazioni");
        }

        // ------------------------------------------------- Fasi 1 e 2

        /// <summary>Tutto da calibrare: il caso di un circuito e una classe mai visti.</summary>
        private static CalibrationNeeds Everything()
        {
            return new CalibrationNeeds
            {
                NeedsGeofence = true,
                NeedsTransit = true,
                NeedsFuelRate = true,
                NeedsTyreTime = true,
                NeedsTyreHalfMultiplier = true,
                NeedsTyreSingleMultiplier = true
            };
        }

        private static void Test_OnlyPracticeAndTestGuideCalibration()
        {
            // Non e' una domanda sulla validita' del dato — quella vale in qualunque sessione — ma
            // sull'opportunita': a meta' gara l'ingegnere non deve chiedere un drive-through.
            Assert(!CalibrationCascade.IsCalibrationSession(true, false), "in gara mai");
            Assert(!CalibrationCascade.IsCalibrationSession(false, true), "in qualifica mai");
            Assert(CalibrationCascade.IsCalibrationSession(false, false),
                   "practice e test sono 'ne' gara ne' qualifica'");
            Pass("La cascata parla solo fuori da gara e qualifica");
        }

        private static void Test_GenuineLapComesBeforeEverything()
        {
            // In Practice si parte fermi in piazzola: un ingresso ai box che non arrivi da un
            // tragitto vero registrerebbe la posizione della piazzola invece dell'ingresso corsia.
            Assert(CalibrationCascade.NextStep(Everything(), false) == CalibrationStep.NeedGenuineLap,
                   "senza un giro vero non si chiede altro");

            Assert(CalibrationCascade.NextStep(Everything(), true) == CalibrationStep.DriveThrough,
                   "fatto il giro, si comincia");
            Pass("Prima di ogni calibrazione serve un giro genuino");
        }

        private static void Test_CascadeOrderIsDriveThroughFuelThenTyres()
        {
            var needs = Everything();

            Assert(CalibrationCascade.NextStep(needs, true) == CalibrationStep.DriveThrough,
                   "prima il drive-through");

            needs.NeedsGeofence = false;
            Assert(CalibrationCascade.NextStep(needs, true) == CalibrationStep.FuelOnlyStop,
                   "poi la sosta carburante");

            needs.NeedsTransit = false;
            needs.NeedsFuelRate = false;
            Assert(CalibrationCascade.NextStep(needs, true) == CalibrationStep.TyreStopAll4,
                   "poi le quattro gomme");

            needs.NeedsTyreTime = false;
            Assert(CalibrationCascade.NextStep(needs, true) == CalibrationStep.TyreStopHalf,
                   "poi due gomme");

            needs.NeedsTyreHalfMultiplier = false;
            Assert(CalibrationCascade.NextStep(needs, true) == CalibrationStep.TyreStopSingle,
                   "infine una gomma");

            needs.NeedsTyreSingleMultiplier = false;
            Assert(CalibrationCascade.NextStep(needs, true) == CalibrationStep.None,
                   "finito, silenzio");
            Pass("L'ordine della cascata e' drive-through, carburante, gomme");
        }

        private static void Test_Regression_SpaCaseKnownClassNewTrack()
        {
            // Il caso descritto dall'utente: GT3 gia' calibrate altrove, si arriva a Spa che e'
            // nuova. Deve chiedere solo i dati del circuito, mai carburante o gomme.
            var needs = new CalibrationNeeds
            {
                NeedsGeofence = true,
                NeedsTransit = true,
                NeedsFuelRate = false,          // dato di classe, gia' noto
                NeedsTyreTime = false,          // dato di classe, gia' noto
                NeedsTyreHalfMultiplier = false,
                NeedsTyreSingleMultiplier = false
            };

            Assert(CalibrationCascade.NextStep(needs, true) == CalibrationStep.DriveThrough,
                   "si parte dal drive-through");

            needs.NeedsGeofence = false;
            Assert(CalibrationCascade.NextStep(needs, true) == CalibrationStep.FuelOnlyStop,
                   "il transito serve comunque: e' un dato del circuito, non della classe");

            needs.NeedsTransit = false;
            Assert(CalibrationCascade.NextStep(needs, true) == CalibrationStep.None,
                   "e finisce li': gomme e portata sono gia' note per questa classe");
            Pass("Caso Spa: classe nota, circuito nuovo -> solo i dati del circuito");
        }

        private static void Test_KnownTrackNewClassSkipsStraightToTyres()
        {
            // Il caso opposto: circuito gia' calibrato, si scende in pista con una classe nuova.
            var needs = new CalibrationNeeds
            {
                NeedsGeofence = false,
                NeedsTransit = false,
                NeedsFuelRate = true,
                NeedsTyreTime = true,
                NeedsTyreHalfMultiplier = true,
                NeedsTyreSingleMultiplier = true
            };

            Assert(CalibrationCascade.NextStep(needs, true) == CalibrationStep.FuelOnlyStop,
                   "serve la portata, che e' un dato di classe");

            needs.NeedsFuelRate = false;
            Assert(CalibrationCascade.NextStep(needs, true) == CalibrationStep.TyreStopAll4,
                   "poi le gomme, anch'esse dato di classe");
            Pass("Circuito noto, classe nuova -> solo i dati della classe");
        }

        private static void Test_MultipliersWaitForTheirDenominator()
        {
            // Senza il tempo All4 non c'e' rapporto da calcolare: chiedere 2 gomme prima sarebbe
            // far perdere tempo al pilota per un dato inutilizzabile.
            var needs = new CalibrationNeeds
            {
                NeedsTyreTime = true,
                NeedsTyreHalfMultiplier = true,
                NeedsTyreSingleMultiplier = true
            };

            Assert(CalibrationCascade.NextStep(needs, true) == CalibrationStep.TyreStopAll4,
                   "prima il denominatore");
            Pass("I moltiplicatori aspettano il tempo delle quattro gomme");
        }

        private static void Test_NothingMissingMeansSilence()
        {
            var nothing = default(CalibrationNeeds);

            Assert(!CalibrationCascade.NeedsAnything(nothing), "niente da chiedere");
            Assert(CalibrationCascade.NextStep(nothing, true) == CalibrationStep.None,
                   "nessun passo");

            // E nemmeno il giro genuino va chiesto se non serve calibrare niente: altrimenti
            // l'ingegnere parlerebbe a ogni sessione su un circuito gia' calibrato.
            Assert(CalibrationCascade.NextStep(nothing, false) == CalibrationStep.None,
                   "silenzio anche senza giro genuino, se non manca nulla");
            Assert(CalibrationCascade.VoiceKeyFor(CalibrationStep.None) == "",
                   "nessuna chiave vocale da annunciare");
            Pass("Con tutto calibrato l'ingegnere tace");
        }

        private static void Test_CascadeToleratesDeviation()
        {
            // L'ingegnere chiede un drive-through, il pilota fa invece il pieno. Il passo del
            // carburante risulta soddisfatto e la cascata prosegue da li': riconosce cio' che e'
            // successo davvero invece di rifiutare e ripetere la richiesta.
            var needs = Everything();
            needs.NeedsTransit = false;
            needs.NeedsFuelRate = false;

            Assert(CalibrationCascade.IsStepSatisfied(CalibrationStep.FuelOnlyStop, needs),
                   "il passo carburante risulta spuntato anche se non era quello chiesto");
            Assert(!CalibrationCascade.IsStepSatisfied(CalibrationStep.DriveThrough, needs),
                   "e il drive-through resta da fare");
            Assert(CalibrationCascade.NextStep(needs, true) == CalibrationStep.DriveThrough,
                   "quindi si torna a chiedere quello che manca ancora");
            Pass("La cascata riconosce cio' che l'utente fa davvero, non lo rifiuta");
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
