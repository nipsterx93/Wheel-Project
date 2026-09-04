// -------------------------------------------------------------------------
// FILE: GapWrapAndResolutionUnitTests.cs
// Y-13: il gap non deve saltare di un giro esatto quando i contatori dei giri
// si disallineano per un tick. E risoluzione dei timestamp di passaggio, che
// governa la precisione del gap sul percorso principale a microsettori.
// -------------------------------------------------------------------------

using System;
using SimRIG;

namespace User.PluginSdkDemo.Tests
{
    public class GapWrapAndResolutionUnitTests
    {
        /// <summary>Giro di Misano: il gapDelta di 93.033 s del replay 20260819_221922 e' esattamente questo.</summary>
        private const double MisanoLapSec = 93.033;

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception("[GapWrapAndResolution] " + message);
        }

        private static void Pass(string name)
        {
            Console.WriteLine("  [PASS] " + name);
        }

        public static void RunAllTests()
        {
            Console.WriteLine("[TEST] Running Gap Wrap & Timestamp Resolution Tests...");

            Test_Wrap_NeutralizesOneLapRollover();
            Test_Wrap_LeavesNormalGapsUntouched();
            Test_Wrap_HalfLapBoundary();
            Test_Wrap_LappedTargetFoldsToNearest();
            Test_TimestampResolution_IsFinerThanSpeedMicrosectors();
            Test_TimestampBucketOf_ClampsInsteadOfOverflowing();

            Console.WriteLine("[TEST SUCCESS] All Gap Wrap & Resolution Tests Passed!");
        }

        /// <summary>
        /// Y-13 - regressione. Al rollover i due contatori si disallineano di un giro esatto per
        /// un tick: posDiff passa da ~0 a ~1.0 e il gap, che e' posDiff * refLapTime, schizza a
        /// un giro intero. Sul replay 20260819_221922 valeva 93.033 s contro un p99 di 0.540.
        /// </summary>
        private static void Test_Wrap_NeutralizesOneLapRollover()
        {
            double artefatto = 1.0;   // i contatori disallineati di un giro esatto

            double senzaPiega = Math.Abs(artefatto * MisanoLapSec);
            Assert(senzaPiega > 90.0,
                   "senza piega l'artefatto deve valere un giro intero, vale " + senzaPiega.ToString("F3"));

            double conPiega = Math.Abs(TargetStrategyManager.WrapLapDifference(artefatto) * MisanoLapSec);
            Assert(conPiega < 0.001,
                   "con la piega il gap deve tornare a ~0, vale " + conPiega.ToString("F3") + " s");

            Assert(Math.Abs(TargetStrategyManager.WrapLapDifference(-1.0) * MisanoLapSec) < 0.001,
                   "un rollover negativo deve annullarsi allo stesso modo");

            double quasiUnGiro = TargetStrategyManager.WrapLapDifference(1.004) * MisanoLapSec;
            Assert(Math.Abs(quasiUnGiro) < 0.5,
                   "un rollover con 0.004 di gap reale deve dare ~0.37 s, da' " + quasiUnGiro.ToString("F3"));

            Pass("Y-13: il salto di un giro esatto al rollover viene annullato");
        }

        /// <summary>
        /// La piega non deve toccare l'esercizio normale: se cambiasse i gap veri sarebbe una
        /// cura peggiore del male.
        /// </summary>
        private static void Test_Wrap_LeavesNormalGapsUntouched()
        {
            double[] normali = { 0.0, 0.002, 0.02, -0.03, 0.15, -0.22, 0.4, -0.49 };
            foreach (double v in normali)
            {
                double w = TargetStrategyManager.WrapLapDifference(v);
                Assert(Math.Abs(w - v) < 1e-9,
                       "un gap normale di " + v.ToString("F3") + " giri non deve essere alterato, e' diventato " + w.ToString("F3"));
            }
            Pass("i gap dentro mezzo giro passano invariati");
        }

        private static void Test_Wrap_HalfLapBoundary()
        {
            Assert(Math.Abs(TargetStrategyManager.WrapLapDifference(0.49) - 0.49) < 1e-9,
                   "0.49 giri resta 0.49");
            // Il confronto in PhysicalGapSeconds e' `>` stretto, quindi mezzo giro esatto resta
            // positivo: e' un pareggio, le due risposte distano un giro e sono entrambe corrette.
            // Il test descrive il comportamento reale invece di imporne uno arbitrario.
            Assert(Math.Abs(TargetStrategyManager.WrapLapDifference(0.5) - 0.5) < 1e-9,
                   "mezzo giro esatto resta +0.5 (pareggio risolto verso il positivo)");
            Assert(TargetStrategyManager.WrapLapDifference(0.5001) < 0.0,
                   "appena oltre mezzo giro si ripiega dall'altro verso");
            Assert(Math.Abs(TargetStrategyManager.WrapLapDifference(-0.49) - (-0.49)) < 1e-9,
                   "-0.49 giri resta -0.49");
            Pass("il confine di mezzo giro si comporta come previsto");
        }

        /// <summary>
        /// Comportamento voluto e dichiarato: un bersaglio realmente doppiato viene riportato
        /// alla sua distanza in pista. E' accettabile perche' il bersaglio si sceglie col minimo
        /// |posDiff| assoluto, quindi in esercizio normale non e' mai a piu' di mezzo giro.
        /// </summary>
        private static void Test_Wrap_LappedTargetFoldsToNearest()
        {
            Assert(Math.Abs(TargetStrategyManager.WrapLapDifference(1.4) - 0.4) < 1e-9,
                   "1.4 giri si ripiega a 0.4, la distanza vera in pista");
            Assert(Math.Abs(TargetStrategyManager.WrapLapDifference(-2.3) - (-0.3)) < 1e-9,
                   "-2.3 giri si ripiega a -0.3");
            Pass("un bersaglio doppiato si ripiega alla distanza fisica in pista");
        }

        /// <summary>
        /// I timestamp sono campionati piu' fitti dei microsettori di velocita', e i due sono
        /// deliberatamente separati: i secondi alimentano ZoneDrop e non vanno cambiati.
        /// </summary>
        private static void Test_TimestampResolution_IsFinerThanSpeedMicrosectors()
        {
            Assert(OpponentTracker.TimestampBucketCount == 400,
                   "la risoluzione dei timestamp deve essere 400, e' " + OpponentTracker.TimestampBucketCount);

            double metriPerBucket = 4226.0 / OpponentTracker.TimestampBucketCount;
            Assert(metriPerBucket > 8.0 && metriPerBucket < 12.0,
                   "a Misano ci si aspettano ~10 m per bucket, sono " + metriPerBucket.ToString("F1") + " m");

            int a = OpponentTracker.TimestampBucketOf(0.4200);
            int b = OpponentTracker.TimestampBucketOf(0.4275);
            Assert(a != b,
                   "due posizioni dentro lo stesso vecchio microsettore da 1/100 devono ora distinguersi");

            Pass("i timestamp sono a 400 bucket, ~10 m, separati dai microsettori di velocita'");
        }

        /// <summary>
        /// Con l'indicizzazione vecchia una posizione di esattamente 1.0 dava indice 100 su un
        /// array lungo 100: IndexOutOfRange. Il clamp lo rende impossibile.
        /// </summary>
        private static void Test_TimestampBucketOf_ClampsInsteadOfOverflowing()
        {
            Assert(OpponentTracker.TimestampBucketOf(0.0) == 0, "posizione 0 -> bucket 0");
            Assert(OpponentTracker.TimestampBucketOf(0.9999) == OpponentTracker.TimestampBucketCount - 1,
                   "fine giro -> ultimo bucket");
            Assert(OpponentTracker.TimestampBucketOf(1.0) == OpponentTracker.TimestampBucketCount - 1,
                   "una posizione di esattamente 1.0 deve essere bloccata sull'ultimo bucket, non sfondare l'array");
            Assert(OpponentTracker.TimestampBucketOf(-0.2) == 0,
                   "una posizione negativa deve essere bloccata sul primo bucket");

            for (double pos = 0.0; pos < 1.0; pos += 0.001)
            {
                int idx = OpponentTracker.TimestampBucketOf(pos);
                Assert(idx >= 0 && idx < OpponentTracker.TimestampBucketCount,
                       "la posizione " + pos.ToString("F3") + " produce l'indice fuori range " + idx);
            }

            Pass("l'indice del bucket e' sempre dentro l'array, agli estremi compresi");
        }
    }
}
