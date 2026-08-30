// -------------------------------------------------------------------------
// FILE: FuelWeightAndPitLossUnitTests.cs
//
// Y-43: la penalita' di peso del carburante si applicava come litri x coef,
//       ma il coefficiente del motorsport e' in secondi per CHILOGRAMMO.
//       Mancando la conversione, la penalita' era sovrastimata del 33%.
//
// Y-42: la sosta veniva sottratta con una formula che alterava anche il passo,
//       togliendo 1.25 giri dove la sosta reale ne costava 0.53.
//
// Numeri veri dal replay Road Atlanta 20260830_140721.
// -------------------------------------------------------------------------

using System;
using SimRIG;

namespace User.PluginSdkDemo.Tests
{
    public class FuelWeightAndPitLossUnitTests
    {
        private const double CoefSecPerKg = 0.03;      // valore nelle impostazioni, invariato

        // Player: BMW M4 GT3, giri reali 77.13-77.68 s, sosta da 41.1 s.
        private const double PlayerPaceSec = 77.50;
        private const double PitLossSec = 41.10;

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception("[FuelWeightAndPitLoss] " + message);
        }

        private static void Pass(string name)
        {
            Console.WriteLine("  [PASS] " + name);
        }

        public static void RunAllTests()
        {
            Console.WriteLine("[TEST] Running Fuel Weight And Pit Loss Tests...");

            Test_Regression_PenaltyIsPerKilogramNotPerLitre();
            Test_PenaltyMatchesTheMotorsportRuleOfThumb();
            Test_NoFuelOrNoCoefficientMeansNoPenalty();
            Test_Regression_PitLossCostsTheTimeItReallyCosts();
            Test_NoStopNeededMeansNoSubtraction();
            Test_StopsScaleWithTheDistanceToCover();
            Test_WithoutPitDataTheProjectionStaysBare();

            Console.WriteLine("[TEST SUCCESS] All Fuel Weight And Pit Loss Tests Passed!");
        }

        /// <summary>
        /// Il caso vero: il leader viaggiava con ~80 L stimati.
        ///
        /// Neutralizzando la correzione (togliendo la densita' dalla conversione) questo test
        /// diventa rosso con 2.40 s — il valore che il plugin usava davvero, e che gonfiava del 33%
        /// la differenza fra tempo grezzo e normalizzato misurata nei log.
        /// </summary>
        private static void Test_Regression_PenaltyIsPerKilogramNotPerLitre()
        {
            double penalty = RaceTimeProjection.FuelWeightPenaltySec(80.0, CoefSecPerKg);

            // 80 L * 0.75 kg/l = 60 kg -> 60 * 0.03 = 1.80 s
            Assert(Math.Abs(penalty - 1.80) < 0.001,
                   $"80 L pesano 60 kg e costano 1.80 s, ottenuto {penalty:F3}");
            Assert(penalty < 2.40 - 0.5,
                   $"deve essere nettamente sotto i 2.40 s applicati prima, ottenuto {penalty:F3}");
            Pass("Regressione: 80 L costano 1.80 s (erano 2.40, +33%)");
        }

        /// <summary>
        /// La regola pratica del motorsport: ~0.3 s al giro ogni 10 kg. Se la conversione e' giusta,
        /// deve venire fuori da sola.
        /// </summary>
        private static void Test_PenaltyMatchesTheMotorsportRuleOfThumb()
        {
            double tenKilosInLitres = 10.0 / RaceTimeProjection.FuelDensityKgPerLitre;
            double penalty = RaceTimeProjection.FuelWeightPenaltySec(tenKilosInLitres, CoefSecPerKg);

            Assert(Math.Abs(penalty - 0.30) < 0.001,
                   $"10 kg devono costare 0.30 s al giro, ottenuto {penalty:F3}");
            Pass("10 kg = 0.30 s al giro, la regola pratica torna");
        }

        /// <summary>Senza carburante o senza coefficiente non si inventa una penalita'.</summary>
        private static void Test_NoFuelOrNoCoefficientMeansNoPenalty()
        {
            Assert(RaceTimeProjection.FuelWeightPenaltySec(0.0, CoefSecPerKg) == 0.0, "zero litri, zero penalita'");
            Assert(RaceTimeProjection.FuelWeightPenaltySec(80.0, 0.0) == 0.0, "coefficiente nullo, nessuna penalita'");
            Pass("Senza dati la penalita' resta zero");
        }

        /// <summary>
        /// Il caso vero: al giro 14 mancavano ~2700 s alla bandiera, il serbatoio dava ~7.8 giri di
        /// autonomia contro ~21 da percorrere, quindi serviva una sosta.
        ///
        /// La sosta deve costare **il tempo che costa**: 41.1 s su un giro da 77.5 s sono 0.53 giri.
        /// La vecchia formula ne toglieva 1.25.
        ///
        /// Neutralizzando la correzione (rimettendo il termine J sul denominatore) il test diventa
        /// rosso: la differenza sale oltre un giro.
        /// </summary>
        private static void Test_Regression_PitLossCostsTheTimeItReallyCosts()
        {
            const double timeToFlag = 1630.0;    // ~21 giri di gara ancora davanti
            const double tankLaps = 7.80;        // autonomia residua misurata prima della sosta
            const double stintLaps = 22.0;       // serbatoio pieno

            var withStop = RaceAnalyzer.ProjectLapsLeftWithStops(
                timeToFlag, PlayerPaceSec, tankLaps, stintLaps, PitLossSec);

            double lapsIfNoStop = timeToFlag / PlayerPaceSec;
            double cost = lapsIfNoStop - withStop.LapsLeft;

            Assert(withStop.StopsNeeded == 1,
                   $"con 7.8 giri di autonomia e 21 da fare serve una sosta, calcolate {withStop.StopsNeeded}");
            Assert(Math.Abs(cost - (PitLossSec / PlayerPaceSec)) < 0.001,
                   $"la sosta deve costare {PitLossSec / PlayerPaceSec:F3} giri, ne costa {cost:F3}");
            Assert(cost < 0.60,
                   $"il costo deve stare sotto i 0.60 giri, non essere 1.25 come prima: {cost:F3}");
            Pass($"Regressione: una sosta costa {cost:F2} giri (erano 1.25)");
        }

        /// <summary>
        /// Dopo la sosta non c'e' piu' niente da sottrarre, ed e' il caso in cui la proiezione era
        /// gia' accurata anche prima della correzione (34.67-34.85 contro un vero 34.83).
        /// </summary>
        private static void Test_NoStopNeededMeansNoSubtraction()
        {
            const double timeToFlag = 800.0;
            var plan = RaceAnalyzer.ProjectLapsLeftWithStops(
                timeToFlag, PlayerPaceSec, tankLapsRemaining: 21.6, stintLaps: 22.0, pitLossSec: PitLossSec);

            Assert(plan.StopsNeeded == 0, $"col serbatoio pieno non serve fermarsi, calcolate {plan.StopsNeeded}");
            Assert(Math.Abs(plan.LapsLeft - timeToFlag / PlayerPaceSec) < 0.0001,
                   $"senza soste i giri restano {timeToFlag / PlayerPaceSec:F3}, ottenuto {plan.LapsLeft:F3}");
            Pass("Senza soste da fare non si sottrae nulla");
        }

        /// <summary>Piu' strada da coprire del serbatoio, piu' soste — e il costo cresce con loro.</summary>
        private static void Test_StopsScaleWithTheDistanceToCover()
        {
            // Gara lunga: 60 giri da coprire, serbatoio da 22, in tasca 5.
            var plan = RaceAnalyzer.ProjectLapsLeftWithStops(
                60.0 * PlayerPaceSec, PlayerPaceSec, tankLapsRemaining: 5.0, stintLaps: 22.0, pitLossSec: PitLossSec);

            Assert(plan.StopsNeeded >= 2,
                   $"60 giri con 5 di autonomia e stint da 22 richiedono almeno due soste, calcolate {plan.StopsNeeded}");
            Assert(plan.LapsLeft < 60.0,
                   $"le soste devono togliere giri, ottenuti {plan.LapsLeft:F2}");
            Pass($"Con {plan.StopsNeeded} soste i giri scendono a {plan.LapsLeft:F2}");
        }

        /// <summary>
        /// Su una pista mai vista non conosciamo il tempo di sosta: si restituisce la proiezione
        /// nuda invece di inventare una penalita'.
        /// </summary>
        private static void Test_WithoutPitDataTheProjectionStaysBare()
        {
            const double timeToFlag = 1630.0;
            var plan = RaceAnalyzer.ProjectLapsLeftWithStops(
                timeToFlag, PlayerPaceSec, tankLapsRemaining: 7.8, stintLaps: 22.0, pitLossSec: 0.0);

            Assert(Math.Abs(plan.LapsLeft - timeToFlag / PlayerPaceSec) < 0.0001,
                   $"senza tempo di sosta noto la proiezione resta nuda, ottenuto {plan.LapsLeft:F3}");
            Pass("Senza dati sulla sosta non si inventa una penalita'");
        }
    }
}
