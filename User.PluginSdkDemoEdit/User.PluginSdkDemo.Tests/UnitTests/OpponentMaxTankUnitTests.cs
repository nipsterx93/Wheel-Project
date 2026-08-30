// -------------------------------------------------------------------------
// FILE: OpponentMaxTankUnitTests.cs
// Fase 1 del piano .ai/plans/2026-08-30-proiezioni-su-tempi-misurati.md
//
// La capienza del serbatoio veniva calcolata correttamente per **modello di
// vettura** (dal database, col BoP di sessione applicato) e poi sovrascritta
// tre righe dopo dal record di traccia+classe, che e' un valore unico per
// tutta la classe.
//
// Numeri veri dal replay Road Atlanta 20260830_121813, tutti in classe 4011:
//   BMW M4 GT3 EVO     100.0 L
//   Ferrari 296 GT3    104.0 L
//   Ford Mustang GT3   110.0 L
//   McLaren 720S GT3   110.0 L
//
// Errore massimo evitato: 10 L su una vettura. Il log mostrava il valore per
// vettura mentre il codice ne usava un altro: per questo non si notava.
// -------------------------------------------------------------------------

using System;
using SimRIG;

namespace User.PluginSdkDemo.Tests
{
    public class OpponentMaxTankUnitTests
    {
        // Le quattro GT3 del replay, stessa classe, capienze diverse.
        private const double BmwM4Gt3 = 100.0;
        private const double Ferrari296Gt3 = 104.0;
        private const double FordMustangGt3 = 110.0;
        private const double McLaren720sGt3 = 110.0;

        // Un ipotetico record di traccia+classe: un solo numero per tutta la GT3.
        private const double ClassRecordTank = 100.0;

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception("[OpponentMaxTank] " + message);
        }

        private static void Pass(string name)
        {
            Console.WriteLine("  [PASS] " + name);
        }

        public static void RunAllTests()
        {
            Console.WriteLine("[TEST] Running Opponent Max Tank Tests...");

            Test_Regression_CarsInTheSameClassKeepTheirOwnTank();
            Test_UnknownCarFallsBackToTheClassRecord();
            Test_UnknownCarWithoutClassRecordKeepsTheFamilyConstant();
            Test_KnownCarIgnoresTheClassRecordEvenWhenItExists();

            Console.WriteLine("[TEST SUCCESS] All Opponent Max Tank Tests Passed!");
        }

        /// <summary>
        /// Il caso vero: quattro GT3 della stessa classe devono restare distinte.
        ///
        /// Neutralizzando la correzione (facendo vincere sempre il record di classe) questo test
        /// diventa rosso: Ferrari e Mustang collassano su 100 L, cioe' 4 e 10 litri di errore.
        /// </summary>
        private static void Test_Regression_CarsInTheSameClassKeepTheirOwnTank()
        {
            double bmw = OpponentTracker.ResolveOpponentMaxTank(BmwM4Gt3, ClassRecordTank, true);
            double ferrari = OpponentTracker.ResolveOpponentMaxTank(Ferrari296Gt3, ClassRecordTank, true);
            double mustang = OpponentTracker.ResolveOpponentMaxTank(FordMustangGt3, ClassRecordTank, true);
            double mclaren = OpponentTracker.ResolveOpponentMaxTank(McLaren720sGt3, ClassRecordTank, true);

            Assert(Math.Abs(bmw - BmwM4Gt3) < 0.001, $"BMW deve restare a 100 L, ottenuto {bmw:F1}");
            Assert(Math.Abs(ferrari - Ferrari296Gt3) < 0.001, $"Ferrari deve restare a 104 L, ottenuto {ferrari:F1}");
            Assert(Math.Abs(mustang - FordMustangGt3) < 0.001, $"Mustang deve restare a 110 L, ottenuto {mustang:F1}");
            Assert(Math.Abs(mclaren - McLaren720sGt3) < 0.001, $"McLaren deve restare a 110 L, ottenuto {mclaren:F1}");

            Assert(Math.Abs(mustang - bmw) > 9.0,
                   "fra Mustang e BMW ci devono restare i 10 L di differenza reale");
            Pass("Regressione: quattro GT3 della stessa classe conservano capienze diverse (100/104/110/110)");
        }

        /// <summary>
        /// Vettura non riconosciuta nel database: li' la capienza per modello non esiste e il valore
        /// per vettura e' solo una costante di famiglia. Un dato di traccia misurato e' meglio.
        /// </summary>
        private static void Test_UnknownCarFallsBackToTheClassRecord()
        {
            const double familyConstantGt3 = 120.0;   // il fallback cablato per una GT3 sconosciuta
            double resolved = OpponentTracker.ResolveOpponentMaxTank(familyConstantGt3, ClassRecordTank, false);

            Assert(Math.Abs(resolved - ClassRecordTank) < 0.001,
                   $"vettura sconosciuta: deve vincere il record di classe (100 L), ottenuto {resolved:F1}");
            Pass("Vettura non riconosciuta: il record di classe resta come ripiego");
        }

        /// <summary>Sconosciuta e senza record di classe: si tiene quello che c'e'.</summary>
        private static void Test_UnknownCarWithoutClassRecordKeepsTheFamilyConstant()
        {
            const double familyConstantGtp = 89.0;
            double resolved = OpponentTracker.ResolveOpponentMaxTank(familyConstantGtp, 0.0, false);

            Assert(Math.Abs(resolved - familyConstantGtp) < 0.001,
                   $"senza record di classe si tiene la costante di famiglia, ottenuto {resolved:F1}");
            Pass("Senza record di classe si tiene la costante di famiglia");
        }

        /// <summary>
        /// Il punto della correzione: se la vettura e' nel database, il record di classe **non deve
        /// entrare**, nemmeno quando esiste ed e' popolato.
        /// </summary>
        private static void Test_KnownCarIgnoresTheClassRecordEvenWhenItExists()
        {
            double resolved = OpponentTracker.ResolveOpponentMaxTank(FordMustangGt3, ClassRecordTank, true);
            Assert(Math.Abs(resolved - FordMustangGt3) < 0.001,
                   $"vettura nota: il record di classe non deve sovrascrivere i 110 L, ottenuto {resolved:F1}");
            Pass("Vettura riconosciuta: il record di classe non la sovrascrive");
        }
    }
}
