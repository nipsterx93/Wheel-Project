// -------------------------------------------------------------------------
// FILE: OvercutTrafficUnitTests.cs
// Y-2: OvercutTrafficOK non e' piu' cablato a true. Overcut e undercut fanno
// due domande diverse sul traffico e vanno misurate diversamente:
//   undercut -> dove mi ritrovo all'USCITA dai box (gap proiettato)
//   overcut  -> chi ho davanti ADESSO mentre spingo (gap istantaneo)
// -------------------------------------------------------------------------

using System;
using SimRIG;

namespace User.PluginSdkDemo.Tests
{
    public class OvercutTrafficUnitTests
    {
        private const double MisanoLap = 93.0;

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception("[OvercutTraffic] " + message);
        }

        private static void AssertClose(double actual, double expected, string message)
        {
            if (Math.Abs(actual - expected) > 1e-9)
                throw new Exception($"[OvercutTraffic] {message} — atteso {expected:F4}, ottenuto {actual:F4}");
        }

        private static void Pass(string name)
        {
            Console.WriteLine("  [PASS] " + name);
        }

        public static void RunAllTests()
        {
            Console.WriteLine("[TEST] Running Overcut Traffic Tests...");

            Test_PhysicalGap_SignConvention();
            Test_PhysicalGap_UnwrapsLappedCars();
            Test_PhysicalGap_HandlesUnknownLapTime();
            Test_BlocksOvercut_OnlyCarsAhead();
            Test_BlocksOvercut_WindowBoundaries();
            Test_BlocksOvercut_RealisticScenarios();

            Console.WriteLine("[TEST SUCCESS] All Overcut Traffic Tests Passed!");
        }

        private static void Test_PhysicalGap_SignConvention()
        {
            // Convenzione: negativo = davanti a me, positivo = dietro.
            Assert(TargetStrategyManager.PhysicalGapSeconds(-1.5, MisanoLap) < 0.0,
                   "un avversario davanti deve dare un gap negativo");
            Assert(TargetStrategyManager.PhysicalGapSeconds(1.5, MisanoLap) > 0.0,
                   "un avversario dietro deve dare un gap positivo");
            AssertClose(TargetStrategyManager.PhysicalGapSeconds(-1.5, MisanoLap), -1.5,
                        "entro mezzo giro il gap non deve essere alterato");

            Pass("Test_PhysicalGap_SignConvention");
        }

        /// <summary>
        /// Il caso che rende necessario il modulo: un doppiato che il conteggio a giri mette a
        /// 88 secondi e' fisicamente 5 secondi davanti. Senza questo, il traffico piu'
        /// pericoloso — quello che ti trovi davanti mentre spingi — risulterebbe lontanissimo.
        /// </summary>
        private static void Test_PhysicalGap_UnwrapsLappedCars()
        {
            AssertClose(TargetStrategyManager.PhysicalGapSeconds(88.0, MisanoLap), -5.0,
                        "88 s su un giro da 93 s significa 5 s DAVANTI, non 88 s dietro");
            AssertClose(TargetStrategyManager.PhysicalGapSeconds(-88.0, MisanoLap), 5.0,
                        "e specularmente dall'altro lato");
            AssertClose(TargetStrategyManager.PhysicalGapSeconds(MisanoLap, MisanoLap), 0.0,
                        "un giro esatto di distacco e' distanza fisica zero");

            // Due giri di distacco: stesso ragionamento, deve restare stabile.
            AssertClose(TargetStrategyManager.PhysicalGapSeconds(2 * MisanoLap - 5.0, MisanoLap), -5.0,
                        "il modulo deve reggere anche oltre il giro singolo");

            Pass("Test_PhysicalGap_UnwrapsLappedCars");
        }

        private static void Test_PhysicalGap_HandlesUnknownLapTime()
        {
            AssertClose(TargetStrategyManager.PhysicalGapSeconds(12.0, 0.0), 12.0,
                        "senza un tempo di giro utilizzabile il valore passa inalterato");

            Pass("Test_PhysicalGap_HandlesUnknownLapTime");
        }

        private static void Test_BlocksOvercut_OnlyCarsAhead()
        {
            double w = TargetStrategyManager.OvercutTrafficWindowSeconds;

            Assert(TargetStrategyManager.BlocksOvercut(-1.0, w),
                   "una vettura 1 s davanti blocca l'overcut");
            Assert(!TargetStrategyManager.BlocksOvercut(1.0, w),
                   "una vettura 1 s DIETRO non mi rallenta: non deve bloccare nulla");
            Assert(!TargetStrategyManager.BlocksOvercut(0.5, w),
                   "chi mi segue da vicino non e' traffico");

            Pass("Test_BlocksOvercut_OnlyCarsAhead");
        }

        private static void Test_BlocksOvercut_WindowBoundaries()
        {
            Assert(TargetStrategyManager.BlocksOvercut(-2.0, 2.0),
                   "al limite esatto della finestra la vettura conta ancora");
            Assert(!TargetStrategyManager.BlocksOvercut(-2.001, 2.0),
                   "appena oltre la finestra non conta piu'");
            Assert(!TargetStrategyManager.BlocksOvercut(0.0, 2.0),
                   "gap zero non e' 'davanti': niente da bloccare");

            Pass("Test_BlocksOvercut_WindowBoundaries");
        }

        /// <summary>
        /// Il caso completo, come si presenta in gara: parto dal distacco a giri e arrivo alla
        /// decisione, passando per il modulo.
        /// </summary>
        private static void Test_BlocksOvercut_RealisticScenarios()
        {
            double w = TargetStrategyManager.OvercutTrafficWindowSeconds;

            // Un doppiato che il conteggio mette a +91.5 s: fisicamente 1.5 s davanti. Blocca.
            double lapped = TargetStrategyManager.PhysicalGapSeconds(91.5, MisanoLap);
            AssertClose(lapped, -1.5, "il doppiato deve risultare 1.5 s davanti");
            Assert(TargetStrategyManager.BlocksOvercut(lapped, w),
                   "un doppiato a 1.5 s davanti rovina l'overcut esattamente come un rivale");

            // Pista libera: il piu' vicino davanti e' a 8 s.
            double clear = TargetStrategyManager.PhysicalGapSeconds(-8.0, MisanoLap);
            Assert(!TargetStrategyManager.BlocksOvercut(clear, w),
                   "a 8 s di pista libera l'overcut non e' ostacolato");

            // Chi mi insegue a mezzo secondo: non e' traffico.
            double behind = TargetStrategyManager.PhysicalGapSeconds(0.5, MisanoLap);
            Assert(!TargetStrategyManager.BlocksOvercut(behind, w),
                   "chi mi segue non deve mai bloccare l'overcut");

            Pass("Test_BlocksOvercut_RealisticScenarios");
        }
    }
}
