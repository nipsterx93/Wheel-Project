// -------------------------------------------------------------------------
// FILE: FuelRoundingUnitTests.cs
// Y-34: iRacing non accetta decimali nel rifornimento. FuelToAdd deve uscire
// intero e sempre arrotondato PER ECCESSO, in tutte le modalita'. Il verso non
// e' simmetrico: un litro di troppo costa una frazione di secondo di sosta, un
// litro in meno significa restare a piedi.
// -------------------------------------------------------------------------

using System;
using SimRIG;

namespace User.PluginSdkDemo.Tests
{
    public class FuelRoundingUnitTests
    {
        /// <summary>Consumo misurato a Road Atlanta nella gara di riferimento.</summary>
        private const double RoadAtlantaBurn = 2.25;

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception("[FuelRounding] " + message);
        }

        private static void Pass(string name)
        {
            Console.WriteLine("  [PASS] " + name);
        }

        private static bool IsWhole(double v)
        {
            return Math.Abs(v - Math.Floor(v)) < 1e-9;
        }

        public static void RunAllTests()
        {
            Console.WriteLine("[TEST] Running Fuel Rounding (Y-34) Tests...");

            Test_RealCase_RoadAtlanta_MatchesReferenceSoftware();
            Test_EveryModeProducesWholeLitres();
            Test_NeverRoundsDown();
            Test_MarginForMode_MatchesAgreedScheme();
            Test_TankLimitIsWholeAndNeverExceeded();
            Test_DegenerateInputsAreNeutralised();

            Console.WriteLine("[TEST SUCCESS] All Fuel Rounding Tests Passed!");
        }

        /// <summary>
        /// Caso reale annotato dall'utente dopo la gara di riferimento a Road Atlanta
        /// (Correzioni Post Test Pirpi/Test.txt): il software di riferimento chiedeva 31 L,
        /// il plugin ne mandava 30.5 in modalita' AGGR e lasciava arrotondare al gioco.
        /// Con l'arrotondamento per eccesso i due coincidono.
        /// </summary>
        private static void Test_RealCase_RoadAtlanta_MatchesReferenceSoftware()
        {
            double nostroGrezzo = 30.5;
            double arrotondato = FuelManager.RoundFuelToAdd(nostroGrezzo, 100.0);

            Assert(arrotondato == 31.0,
                   "il caso reale da 30.5 L deve dare 31 L come il software di riferimento, da' "
                   + arrotondato.ToString("F1"));
            Assert(IsWhole(arrotondato), "e deve essere un intero");

            Pass("il caso reale di Road Atlanta (30.5 -> 31) coincide col software di riferimento");
        }

        /// <summary>
        /// La regola dell'utente e' categorica: nessun valore puo' essere decimale, in nessuna
        /// modalita'. Si spazza il fabbisogno grezzo a passi da un decimo su tutte e quattro.
        /// </summary>
        private static void Test_EveryModeProducesWholeLitres()
        {
            FuelStrategyMode[] modi =
            {
                FuelStrategyMode.Aggressive,
                FuelStrategyMode.Normal,
                FuelStrategyMode.Safe,
                FuelStrategyMode.Manual
            };

            foreach (FuelStrategyMode modo in modi)
            {
                for (double grezzo = 0.0; grezzo <= 60.0; grezzo += 0.1)
                {
                    double conMargine = grezzo + FuelManager.MarginForMode(modo, RoadAtlantaBurn);
                    double uscita = FuelManager.RoundFuelToAdd(conMargine, 100.0);

                    Assert(IsWhole(uscita),
                           "modalita' " + modo + " con grezzo " + grezzo.ToString("F1")
                           + " produce il decimale " + uscita.ToString("F3"));
                }
            }

            Pass("nessuna modalita' produce un valore decimale");
        }

        /// <summary>
        /// La proprieta' di sicurezza: qualunque sia il fabbisogno, non si imbarca mai meno del
        /// necessario. E' il verso che conta — sbagliare in meno significa restare a piedi.
        /// </summary>
        private static void Test_NeverRoundsDown()
        {
            for (double grezzo = 0.05; grezzo <= 60.0; grezzo += 0.07)
            {
                double uscita = FuelManager.RoundFuelToAdd(grezzo, 100.0);

                Assert(uscita >= grezzo - 1e-9,
                       "con grezzo " + grezzo.ToString("F2") + " l'uscita " + uscita.ToString("F1")
                       + " e' inferiore al fabbisogno: e' la direzione che lascia a piedi");
                Assert(uscita < grezzo + 1.0 + 1e-9,
                       "con grezzo " + grezzo.ToString("F2") + " l'eccesso supera il litro: "
                       + uscita.ToString("F1"));
            }

            Pass("l'arrotondamento non scende mai sotto il fabbisogno, e non eccede il litro");
        }

        /// <summary>Lo schema concordato: AGGR nulla, NORM 0.6 L fissi, SAFE un giro intero.</summary>
        private static void Test_MarginForMode_MatchesAgreedScheme()
        {
            Assert(FuelManager.MarginForMode(FuelStrategyMode.Aggressive, RoadAtlantaBurn) == 0.0,
                   "AGGR non ha margine: glielo da' gia' l'arrotondamento per eccesso");

            Assert(FuelManager.MarginForMode(FuelStrategyMode.Normal, RoadAtlantaBurn) == 0.6,
                   "NORM ha 0.6 L fissi, non piu' proporzionali al consumo");

            Assert(FuelManager.MarginForMode(FuelStrategyMode.Safe, RoadAtlantaBurn) == RoadAtlantaBurn,
                   "SAFE imbarca un giro intero");

            // Il margine di NORM ora e' indipendente dal consumo: e' la conseguenza dichiarata
            // del passaggio da proporzionale a fisso.
            Assert(FuelManager.MarginForMode(FuelStrategyMode.Normal, 4.0)
                   == FuelManager.MarginForMode(FuelStrategyMode.Normal, 1.5),
                   "il margine di NORM non deve piu' dipendere dal consumo");

            // SAFE invece resta un giro, quindi segue il consumo.
            Assert(FuelManager.MarginForMode(FuelStrategyMode.Safe, 4.0) == 4.0,
                   "SAFE resta legato al consumo perche' e' un giro intero");

            Pass("i margini per modalita' seguono lo schema concordato");
        }

        /// <summary>
        /// MaxFuel arriva dal gioco come frazionario (una GT3 puo' avere 63.7 L). Limitare a 63.7
        /// dopo aver arrotondato a 64 rimetterebbe in circolo un decimale, quindi il tetto e'
        /// l'intero inferiore della capacita'.
        /// </summary>
        private static void Test_TankLimitIsWholeAndNeverExceeded()
        {
            double capacitaFrazionaria = 63.7;

            double oltreIlTetto = FuelManager.RoundFuelToAdd(70.0, capacitaFrazionaria);
            Assert(oltreIlTetto == 63.0,
                   "il tetto deve essere l'intero inferiore della capacita' (63), e' "
                   + oltreIlTetto.ToString("F1"));
            Assert(IsWhole(oltreIlTetto), "il tetto stesso deve essere intero");

            // Anche appena sotto il tetto frazionario non deve uscire un decimale.
            double appenaSotto = FuelManager.RoundFuelToAdd(63.2, capacitaFrazionaria);
            Assert(IsWhole(appenaSotto),
                   "vicino al tetto l'uscita deve restare intera, e' " + appenaSotto.ToString("F3"));
            Assert(appenaSotto <= capacitaFrazionaria,
                   "e non deve chiedere piu' di quanto entra nel serbatoio");

            Pass("il tetto del serbatoio resta intero e non viene mai superato");
        }

        private static void Test_DegenerateInputsAreNeutralised()
        {
            Assert(FuelManager.RoundFuelToAdd(-5.0, 100.0) == 0.0, "un fabbisogno negativo da' 0");
            Assert(FuelManager.RoundFuelToAdd(0.0, 100.0) == 0.0, "zero resta zero");
            Assert(FuelManager.RoundFuelToAdd(double.NaN, 100.0) == 0.0, "NaN non deve propagarsi");
            Assert(FuelManager.RoundFuelToAdd(10.0, 0.0) == 0.0, "senza serbatoio non si imbarca nulla");

            // Un fabbisogno minimo deve comunque produrre un litro: mandare 0 quando serve
            // qualcosa e' la direzione pericolosa.
            Assert(FuelManager.RoundFuelToAdd(0.01, 100.0) == 1.0,
                   "un fabbisogno di un centilitro deve comunque chiedere un litro");

            Pass("gli ingressi degeneri non producono valori pericolosi");
        }
    }
}
