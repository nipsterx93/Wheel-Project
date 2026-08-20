// -------------------------------------------------------------------------
// FILE: WarmupDoubleCountUnitTests.cs
// Y-11: i giri post-pit su gomma fredda non devono entrare nella media da cui
// si ricava PaceDropDueToTyres, perché la stessa lentezza viene poi ri-sommata
// come warmup esplicito. ActiveWarmupLaps() è la funzione che decide quali.
// -------------------------------------------------------------------------

using System;
using SimRIG;

namespace User.PluginSdkDemo.Tests
{
    public class WarmupDoubleCountUnitTests
    {
        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception("[WarmupDoubleCount] " + message);
        }

        private static void Pass(string name)
        {
            Console.WriteLine("  [PASS] " + name);
        }

        /// <summary>Popola le penalità di warmup come farebbe una sosta reale.</summary>
        private static RaceAnalyzer WithPenalties(double p0, double p1, double p2)
        {
            var ra = new RaceAnalyzer();
            ra.PostPitWarmupPenalties[0] = p0;
            ra.PostPitWarmupPenalties[1] = p1;
            ra.PostPitWarmupPenalties[2] = p2;
            return ra;
        }

        public static void RunAllTests()
        {
            Console.WriteLine("[TEST] Running Warmup Double-Count Tests...");

            Test_NoWarmupBeforeAnyPit();
            Test_CountsOnlyContiguousPrefix();
            Test_ThresholdIsInclusive();
            Test_AllThreeLapsCanBeWarmup();
            Test_ThresholdIsSharedNotDuplicated();
            Test_ExclusionWindowShrinksAsTyresComeIn();

            Console.WriteLine("[TEST SUCCESS] All Warmup Double-Count Tests Passed!");
        }

        /// <summary>
        /// A inizio gara le penalità sono tutte a zero: nessun giro va escluso, altrimenti
        /// si perderebbero i primi giri buoni che servono a costruire la baseline.
        /// </summary>
        private static void Test_NoWarmupBeforeAnyPit()
        {
            var ra = new RaceAnalyzer();
            Assert(ra.ActiveWarmupLaps() == 0,
                   $"senza soste non si esclude nulla, ottenuti {ra.ActiveWarmupLaps()} giri");

            Pass("Test_NoWarmupBeforeAnyPit");
        }

        /// <summary>
        /// Il conteggio si ferma al primo giro sotto soglia. Un giro lento più avanti nello
        /// stint è degrado, non warmup, e deve restare nella media.
        /// </summary>
        private static void Test_CountsOnlyContiguousPrefix()
        {
            Assert(WithPenalties(0.80, 0.30, 0.05).ActiveWarmupLaps() == 2,
                   "0.80/0.30/0.05 deve dare 2 giri di warmup");
            Assert(WithPenalties(0.80, 0.02, 0.90).ActiveWarmupLaps() == 1,
                   "un buco interrompe il conteggio: 0.80/0.02/0.90 deve dare 1, non 2");
            Assert(WithPenalties(0.01, 0.90, 0.90).ActiveWarmupLaps() == 0,
                   "se il primo giro e' gia' a regime non c'e' warmup da escludere");

            Pass("Test_CountsOnlyContiguousPrefix");
        }

        private static void Test_ThresholdIsInclusive()
        {
            Assert(WithPenalties(RaceAnalyzer.WarmupThreshold, 0.0, 0.0).ActiveWarmupLaps() == 1,
                   "una penalita' esattamente pari alla soglia conta come warmup");
            Assert(WithPenalties(RaceAnalyzer.WarmupThreshold - 0.001, 0.0, 0.0).ActiveWarmupLaps() == 0,
                   "appena sotto la soglia non conta");

            Pass("Test_ThresholdIsInclusive");
        }

        private static void Test_AllThreeLapsCanBeWarmup()
        {
            var ra = WithPenalties(1.20, 0.60, 0.25);
            Assert(ra.ActiveWarmupLaps() == 3,
                   $"tre giri sopra soglia devono contare tutti, ottenuti {ra.ActiveWarmupLaps()}");
            Assert(ra.ActiveWarmupLaps() <= ra.PostPitWarmupPenalties.Length,
                   "non si puo' escludere piu' giri di quanti il modello ne misuri");

            Pass("Test_AllThreeLapsCanBeWarmup");
        }

        /// <summary>
        /// La soglia era ricopiata a mano in tre punti. Se il gate overcut e l'esclusione dalla
        /// media usassero valori diversi, si riaprirebbe il buco che Y-11 chiude: un giro
        /// escluso da una parte e contato dall'altra, o contato due volte.
        /// </summary>
        private static void Test_ThresholdIsSharedNotDuplicated()
        {
            Assert(Math.Abs(RaceAnalyzer.WarmupThreshold - 0.10) < 1e-9,
                   $"la soglia condivisa deve valere 0.10, vale {RaceAnalyzer.WarmupThreshold}");

            string header = LogManager.BuildModelParamsHeader();
            Assert(header.Contains("# WarmupThreshold=0.10"),
                   "l'header del log deve riportare la soglia reale, non una copia scollegata");

            Pass("Test_ThresholdIsSharedNotDuplicated");
        }

        /// <summary>
        /// Il caso che conta davvero: la finestra di esclusione si stringe man mano che le gomme
        /// entrano in temperatura. Con 2 giri di warmup attivi, i giri 0 e 1 dopo la sosta sono
        /// esclusi e dal 2 in poi si torna a misurare il degrado.
        /// </summary>
        private static void Test_ExclusionWindowShrinksAsTyresComeIn()
        {
            var ra = WithPenalties(0.90, 0.35, 0.04);
            Assert(ra.ActiveWarmupLaps() == 2, $"precondizione: finestra attesa 2, ottenuta {ra.ActiveWarmupLaps()}");

            // Questa e' la decisione che AnalyzePlayerLap prende davvero per ogni giro.
            Assert(ra.IsLapExcludedFromDegradation(0), "il giro subito dopo la sosta deve essere escluso");
            Assert(ra.IsLapExcludedFromDegradation(1), "anche il secondo giro deve essere escluso");
            Assert(!ra.IsLapExcludedFromDegradation(2),
                   "il terzo giro deve tornare nella media: li' e' degrado, non warmup");
            Assert(!ra.IsLapExcludedFromDegradation(5), "a meta' stint non si esclude piu' nulla");

            // Senza soste nulla va escluso, nemmeno il primo giro di gara.
            var fresh = new RaceAnalyzer();
            Assert(!fresh.IsLapExcludedFromDegradation(0),
                   "REGRESSIONE: a inizio gara nessun giro deve essere escluso dalla media");

            Pass("Test_ExclusionWindowShrinksAsTyresComeIn");
        }
    }
}
