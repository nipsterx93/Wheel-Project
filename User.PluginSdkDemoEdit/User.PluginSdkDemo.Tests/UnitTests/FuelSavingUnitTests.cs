// -------------------------------------------------------------------------
// FILE: FuelSavingUnitTests.cs
// Y-1, opzione 1: invece di silenziare tutti gli avvisi a fine gara, si
// distingue "non ti serve piu' fermarti" da "ti servirebbe risparmiare".
// Il filtro chiave e' la fattibilita': un target di 0.25 L/giro e' vero in
// aritmetica e inutile in pista.
// -------------------------------------------------------------------------

using System;
using SimRIG;

namespace User.PluginSdkDemo.Tests
{
    public class FuelSavingUnitTests
    {
        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception("[FuelSaving] " + message);
        }

        private static void AssertClose(double actual, double expected, string message)
        {
            if (Math.Abs(actual - expected) > 1e-6)
                throw new Exception($"[FuelSaving] {message} — atteso {expected:F4}, ottenuto {actual:F4}");
        }

        private static void Pass(string name)
        {
            Console.WriteLine("  [PASS] " + name);
        }

        public static void RunAllTests()
        {
            Console.WriteLine("[TEST] Running Fuel Saving Tests...");

            Test_Regression_EnduranceDoesNotGetAbsurdAdvice();
            Test_AchievableSavingNearTheEnd();
            Test_NoSavingNeededWhenFuelIsEnough();
            Test_MultiplePitsMakeSavingPointless();
            Test_BoundaryOfAchievableSaving();
            Test_DegenerateInputs();
            Test_NoPitNeededIsItsOwnReason();

            Console.WriteLine("[TEST SUCCESS] All Fuel Saving Tests Passed!");
        }

        /// <summary>
        /// Il caso che rende necessario il filtro: 100 giri alla fine con 25 litri a bordo.
        /// La divisione secca dice 0.25 L/giro contro un consumo di 3.0 — un taglio del 92%.
        /// Non e' fuel saving, e' una sosta obbligata, e proporlo sarebbe un consiglio
        /// impossibile da eseguire.
        /// </summary>
        private static void Test_Regression_EnduranceDoesNotGetAbsurdAdvice()
        {
            var plan = FuelManager.ComputeFuelSaving(25.0, 100.0, 3.0, 3.0);

            AssertClose(plan.Target, 0.25, "il target aritmetico resta calcolato");
            Assert(plan.RequiredFraction > 0.90, "il taglio richiesto deve risultare enorme");
            Assert(!plan.Achievable,
                   "REGRESSIONE Y-1: un taglio del 92% non e' ottenibile guidando, non va proposto");

            Pass("Test_Regression_EnduranceDoesNotGetAbsurdAdvice");
        }

        /// <summary>
        /// Il caso utile, quello per cui Y-1 e' stato aperto: mancano 10 giri, a bordo ci sono
        /// 28 litri e ne servirebbero 30. Due litri si recuperano alzando il piede, e il
        /// pilota va avvisato invece di essere mandato ai box.
        /// </summary>
        private static void Test_AchievableSavingNearTheEnd()
        {
            var plan = FuelManager.ComputeFuelSaving(28.0, 10.0, 3.0, 1.0);

            AssertClose(plan.Target, 2.8, "servono 2.8 L/giro per arrivare in fondo");
            AssertClose(plan.RequiredFraction, (3.0 - 2.8) / 3.0, "taglio richiesto ~6.7%");
            Assert(plan.Achievable,
                   "un taglio del 6.7% e' esattamente cio' che il fuel saving ottiene: va proposto");

            Pass("Test_AchievableSavingNearTheEnd");
        }

        private static void Test_NoSavingNeededWhenFuelIsEnough()
        {
            // 40 litri per 10 giri a 3.0 L/giro: ne avanzano. Nessun consiglio da dare.
            var plan = FuelManager.ComputeFuelSaving(40.0, 10.0, 3.0, 0.0);

            Assert(plan.RequiredFraction < 0.0, "con carburante in eccesso il taglio richiesto e' negativo");
            Assert(!plan.Achievable,
                   "non si propone fuel saving a chi ha gia' abbastanza carburante");

            Pass("Test_NoSavingNeededWhenFuelIsEnough");
        }

        /// <summary>
        /// Con piu' di una sosta ancora davanti, risparmiare non evita nulla: sposta solo il
        /// problema al rifornimento successivo.
        /// </summary>
        private static void Test_MultiplePitsMakeSavingPointless()
        {
            // Taglio piccolo e ottenibile, ma restano due soste da fare.
            var plan = FuelManager.ComputeFuelSaving(28.0, 10.0, 3.0, 2.0);

            Assert(plan.RequiredFraction <= FuelManager.MaxAchievableFuelSaving,
                   "precondizione: il taglio sarebbe ottenibile");
            Assert(!plan.Achievable,
                   "con due soste ancora da fare il fuel saving non evita la sosta: non va proposto");

            Pass("Test_MultiplePitsMakeSavingPointless");
        }

        private static void Test_BoundaryOfAchievableSaving()
        {
            double consumption = 3.0;

            // Il confronto avviene in virgola mobile, quindi il comportamento all'uguaglianza
            // esatta non e' un requisito: si verifica che il taglio sia accettato appena sotto
            // il limite e rifiutato appena sopra.
            double targetAtLimit = consumption * (1.0 - FuelManager.MaxAchievableFuelSaving);

            var justInside = FuelManager.ComputeFuelSaving((targetAtLimit + 0.01) * 10.0, 10.0, consumption, 1.0);
            Assert(justInside.RequiredFraction < FuelManager.MaxAchievableFuelSaving,
                   "precondizione: taglio appena sotto il tetto");
            Assert(justInside.Achievable, "appena sotto il tetto il consiglio e' valido");

            var justOutside = FuelManager.ComputeFuelSaving((targetAtLimit - 0.05) * 10.0, 10.0, consumption, 1.0);
            Assert(justOutside.RequiredFraction > FuelManager.MaxAchievableFuelSaving,
                   "precondizione: taglio appena sopra il tetto");
            Assert(!justOutside.Achievable, "appena oltre il tetto il consiglio non e' piu' eseguibile");

            Assert(Math.Abs(FuelManager.MaxAchievableFuelSaving - 0.15) < 1e-9,
                   "il tetto deve restare il 15% documentato");

            Pass("Test_BoundaryOfAchievableSaving");
        }

        private static void Test_DegenerateInputs()
        {
            var noLaps = FuelManager.ComputeFuelSaving(30.0, 0.0, 3.0, 0.0);
            Assert(noLaps.Target == 0.0 && !noLaps.Achievable,
                   "a gara finita non si divide per zero e non si consiglia nulla");

            var noConsumption = FuelManager.ComputeFuelSaving(30.0, 10.0, 0.0, 0.0);
            Assert(noConsumption.Target == 0.0 && !noConsumption.Achievable,
                   "senza un consumo misurato non si puo' proporre un target");

            Pass("Test_DegenerateInputs");
        }

        /// <summary>
        /// L'altra meta' di Y-1: "non mi serve fermarmi" aveva lo stesso RejectReason di
        /// "la gara sta finendo", quindi il log non permetteva di distinguerle.
        /// </summary>
        private static void Test_NoPitNeededIsItsOwnReason()
        {
            Assert(StrategyRejectReason.NoPitNeeded != StrategyRejectReason.RaceTooLate,
                   "NoPitNeeded non deve piu' mascherarsi da RaceTooLate");
            Assert(StrategyRejectReason.NoPitNeeded.ToString() == "NoPitNeeded",
                   "il motivo deve comparire con il proprio nome nei log");

            Pass("Test_NoPitNeededIsItsOwnReason");
        }
    }
}
