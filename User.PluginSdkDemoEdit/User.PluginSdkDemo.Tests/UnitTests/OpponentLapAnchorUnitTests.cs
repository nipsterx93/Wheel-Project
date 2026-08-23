// -------------------------------------------------------------------------
// FILE: OpponentLapAnchorUnitTests.cs
// Y-17: il primo giro osservato di un avversario non e' un giro.
// Il caso di regressione viene dai numeri veri del replay Daytona del
// 2026-08-23 (Logs/Daytona Run), dove le GTP entrate a gara in corso hanno
// preso baseline di 56.4 s contro i ~100 s reali.
// -------------------------------------------------------------------------

using System;
using SimRIG;

namespace User.PluginSdkDemo.Tests
{
    public class OpponentLapAnchorUnitTests
    {
        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception("[OpponentLapAnchor] " + message);
        }

        private static void Pass(string name)
        {
            Console.WriteLine("  [PASS] " + name);
        }

        /// <summary>
        /// Riproduce la macchina a stati di OpponentTracker per un avversario, usando gli stessi
        /// predicati del codice di produzione. Restituisce i tempi sul giro accettati.
        /// </summary>
        private class LapObserver
        {
            private int _lastLap = -1;
            private double _anchor = -1.0;
            private bool _witnessed = false;

            /// <summary>Un cambio di contatore giri. Restituisce il tempo misurato, o null se scartato.</summary>
            public double? ObserveLapCounter(int currentLap, double sessionClock)
            {
                if (currentLap == _lastLap) return null;

                double? measured = null;
                if (OpponentTracker.CanMeasureLap(_lastLap, _anchor, _witnessed))
                {
                    measured = Math.Abs(sessionClock - _anchor);
                }

                _anchor = sessionClock;
                _witnessed = OpponentTracker.AnchorIsGenuine(_lastLap);
                _lastLap = currentLap;

                return measured;
            }
        }

        public static void RunAllTests()
        {
            Console.WriteLine("[TEST] Running Opponent Lap Anchor Tests...");

            Test_Regression_MidRaceArrivalPartialLapIsDiscarded();
            Test_CarPresentFromTheStartSkipsTheStandingStartLap();
            Test_SteadyStateMeasuresEveryLap();
            Test_PredicatesInIsolation();

            Console.WriteLine("[TEST SUCCESS] All Opponent Lap Anchor Tests Passed!");
        }

        private static void Test_Regression_MidRaceArrivalPartialLapIsDiscarded()
        {
            // Caso reale: Sam Kuitert (Porsche 963 GTP) compare al giro 5 del Player, a meta'
            // tracciato. Il tempo dalla prima comparsa al traguardo successivo e' 56.4 s — una
            // frazione di giro, non un giro. Prima del fix diventava la sua baseline, e siccome
            // la baseline si sostituisce solo al ribasso, i giri veri da ~100 s non l'hanno mai
            // potuta correggere.
            var observer = new LapObserver();

            // t=2124: prima comparsa, gia' in corso il giro 6.
            Assert(observer.ObserveLapCounter(6, 2124.0) == null,
                   "la prima comparsa non produce un tempo");

            // t=2180.4: taglia il traguardo. Sono passati 56.4 s dalla comparsa: NON e' un giro.
            double? partial = observer.ObserveLapCounter(7, 2180.409);
            Assert(partial == null,
                   $"il giro parziale da 56.4 s va scartato, invece e' stato accettato ({partial})");

            // t=2280.4: primo giro davvero completo, 100 s. Questo si', e' un giro.
            double? firstReal = observer.ObserveLapCounter(8, 2280.409);
            Assert(firstReal.HasValue && Math.Abs(firstReal.Value - 100.0) < 1e-6,
                   $"il primo giro intero va accettato a 100 s, ottenuto {firstReal}");
            Pass("Regressione Daytona: il giro parziale di una GTP entrata tardi viene scartato");
        }

        private static void Test_CarPresentFromTheStartSkipsTheStandingStartLap()
        {
            // Stesso meccanismo, sintomo opposto: il primo giro di chi parte dalla griglia include
            // la partenza da fermo. Nel log reale valeva 176.6 s (Jesse Telkkala), 197.9 (Pablo
            // Espes), 220.7 (Michal Zajac2), 339.8 (Christoph Maliszewski) — contro ~100 s veri.
            var observer = new LapObserver();

            Assert(observer.ObserveLapCounter(1, 0.0) == null, "prima comparsa in griglia");
            Assert(observer.ObserveLapCounter(2, 220.717) == null,
                   "il giro con la partenza da fermo va scartato");

            double? clean = observer.ObserveLapCounter(3, 320.717);
            Assert(clean.HasValue && Math.Abs(clean.Value - 100.0) < 1e-6,
                   $"dal secondo giro in poi si misura, ottenuto {clean}");
            Pass("Anche il giro con partenza da fermo viene scartato");
        }

        private static void Test_SteadyStateMeasuresEveryLap()
        {
            // Dopo l'avvio non si deve perdere nessun giro: il costo del fix e' esattamente
            // un giro per vettura, non uno ogni tanto.
            var observer = new LapObserver();
            observer.ObserveLapCounter(1, 0.0);
            observer.ObserveLapCounter(2, 100.0);

            for (int lap = 3; lap <= 12; lap++)
            {
                double clock = (lap - 1) * 100.0;
                double? measured = observer.ObserveLapCounter(lap, clock);
                Assert(measured.HasValue && Math.Abs(measured.Value - 100.0) < 1e-6,
                       $"il giro {lap} deve essere misurato, ottenuto {measured}");
            }

            // Un tick senza cambio di contatore non produce nulla e non sposta l'ancoraggio.
            Assert(observer.ObserveLapCounter(12, 1250.0) == null,
                   "senza cambio di giro non si misura niente");
            Pass("A regime ogni giro viene misurato, il costo e' un giro solo");
        }

        private static void Test_PredicatesInIsolation()
        {
            Assert(!OpponentTracker.CanMeasureLap(-1, 100.0, true),
                   "senza un giro precedente non si misura");
            Assert(!OpponentTracker.CanMeasureLap(5, -1.0, true),
                   "senza ancoraggio non si misura");
            Assert(!OpponentTracker.CanMeasureLap(5, 100.0, false),
                   "con un ancoraggio non genuino non si misura — e' il difetto di Y-17");
            Assert(OpponentTracker.CanMeasureLap(5, 100.0, true),
                   "con tutto a posto si misura");

            Assert(!OpponentTracker.AnchorIsGenuine(-1),
                   "la prima comparsa non e' un attraversamento");
            Assert(OpponentTracker.AnchorIsGenuine(0),
                   "un cambio di giro vero lo e', anche dal giro zero");
            Assert(OpponentTracker.AnchorIsGenuine(7), "e a maggior ragione a gara in corso");
            Pass("I predicati si comportano correttamente in isolamento");
        }
    }
}
