// -------------------------------------------------------------------------
// FILE: RaceTimeProjectionUnitTests.cs
// Tempo alla bandiera ancorato al countdown reale, e filtro sul passo del
// leader. I casi di regressione vengono dai numeri veri del replay Daytona
// del 2026-08-23 (SimRIG_DebugLog_20260823_104939.csv).
// -------------------------------------------------------------------------

using System;
using SimRIG;

namespace User.PluginSdkDemo.Tests
{
    public class RaceTimeProjectionUnitTests
    {
        // Daytona 2011 road course. Lunghezza usata anche da TrackPositionValidatorUnitTests.
        private const double DaytonaMeters = 5730.0;
        private const double MisanoMeters = 4200.0;

        // Passi reali letti dal database (SimRIG_Data.json) dopo il replay.
        private const double DaytonaGt3Pace = 104.22;
        private const double MisanoGt3Pace = 91.82;

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception("[RaceTimeProjection] " + message);
        }

        private static void AssertClose(double actual, double expected, double tolerance, string message)
        {
            if (Math.Abs(actual - expected) > tolerance)
                throw new Exception($"[RaceTimeProjection] {message} — atteso {expected:F3}, ottenuto {actual:F3}");
        }

        private static void Pass(string name)
        {
            Console.WriteLine("  [PASS] " + name);
        }

        public static void RunAllTests()
        {
            Console.WriteLine("[TEST] Running Race Time Projection Tests...");

            Test_ExtraLapNeverExceedsOneLeaderLap();
            Test_LeaderExactlyOnLineGetsNoSupplement();
            Test_CountdownExpiredStillFinishesCurrentLap();
            Test_UnusableLeaderPaceFallsBackToBareCountdown();
            Test_Regression_DaytonaLap12_NoLongerDoublesTheRace();
            Test_Regression_BogusLeaderPaceStaysBoundedWithinOneLap();
            Test_ProjectedTotalLapsRoundsUpTheLapInProgress();
            Test_MisanoSingleClassStillProjects26Laps();
            Test_PhysicalPlausibilityFloor();

            Test_Filter_SeedsOnFirstSample();
            Test_Filter_RejectsPhysicallyImpossibleLap();
            Test_Filter_IgnoresFlickeringLeaderIdentity();
            Test_Filter_AdoptsGenuineLeaderChangeAfterDwell();
            Test_Filter_ResetClearsState();

            Console.WriteLine("[TEST SUCCESS] All Race Time Projection Tests Passed!");
        }

        // ---------------------------------------------------------------- proiezione

        private static void Test_ExtraLapNeverExceedsOneLeaderLap()
        {
            // Il leader e' appena passato sul traguardo (posizione .01): allo scadere gli manchera'
            // quasi un giro intero da completare, ma mai piu' di uno.
            for (double frac = 0.0; frac < 1.0; frac += 0.05)
            {
                double t = RaceTimeProjection.TimeUntilLeaderCheckered(600.0, 10.0 + frac, DaytonaGt3Pace);
                Assert(t >= 600.0, $"il supplemento non puo' essere negativo (frac={frac:F2})");
                Assert(t < 600.0 + DaytonaGt3Pace + 1e-9,
                       $"il supplemento non puo' superare un giro del leader (frac={frac:F2}, t={t:F1})");
            }
            Pass("Il giro extra e' sempre dentro [0, un giro del leader)");
        }

        private static void Test_LeaderExactlyOnLineGetsNoSupplement()
        {
            // 600 s a 100 s/giro = 6 giri tondi: allo scadere il leader taglia esattamente il
            // traguardo, quindi nessun giro da completare.
            double t = RaceTimeProjection.TimeUntilLeaderCheckered(600.0, 4.0, 100.0);
            AssertClose(t, 600.0, 1e-9, "sul traguardo esatto non si aggiunge nulla");
            Pass("Leader esattamente sul traguardo allo scadere: nessun supplemento");
        }

        private static void Test_CountdownExpiredStillFinishesCurrentLap()
        {
            // Countdown gia' a zero, leader a meta' giro: manca ancora mezzo giro di gara.
            double t = RaceTimeProjection.TimeUntilLeaderCheckered(0.0, 12.5, 100.0);
            AssertClose(t, 50.0, 1e-9, "a meta' giro manca mezzo giro");

            // Countdown negativo trattato come scaduto, non come tempo che si somma.
            double tNeg = RaceTimeProjection.TimeUntilLeaderCheckered(-30.0, 12.5, 100.0);
            AssertClose(tNeg, 50.0, 1e-9, "un countdown negativo non aggiunge tempo");
            Pass("Tempo scaduto: resta da completare il solo giro in corso");
        }

        private static void Test_UnusableLeaderPaceFallsBackToBareCountdown()
        {
            // Senza un passo credibile non si inventa una frazione di giro: si restituisce il
            // countdown nudo. Sbagliare per difetto di meno di un giro e' preferibile a
            // moltiplicare per un numero a caso — che e' esattamente il difetto che chiudiamo.
            AssertClose(RaceTimeProjection.TimeUntilLeaderCheckered(600.0, 10.4, 0.0), 600.0, 1e-9,
                        "passo zero");
            AssertClose(RaceTimeProjection.TimeUntilLeaderCheckered(600.0, 10.4, -5.0), 600.0, 1e-9,
                        "passo negativo");
            Pass("Passo del leader inutilizzabile: si restituisce il countdown nudo");
        }

        private static void Test_Regression_DaytonaLap12_NoLongerDoublesTheRace()
        {
            // Caso reale, replay Daytona 2026-08-23 giro 12-15 (SimRIG_DebugLog_20260823_104939):
            //   L_Rem latchato a 42.00 giri, L_Pace scivolato a 56.391 s
            //   vecchia formula: 42 * 56.391 = 2368 s di gara residua
            //   cronometro reale a quel punto: ~1400 s
            // La vecchia formula sovrastimava del 69%, e da li' passava dritta in FuelToAdd.
            const double realCountdown = 1400.0;
            const double latchedLeaderLaps = 42.0;
            const double bogusLeaderPace = 56.391;

            double oldWay = latchedLeaderLaps * bogusLeaderPace;
            Assert(oldWay > 2300.0, "il caso di partenza deve davvero essere quello patologico");

            double newWay = RaceTimeProjection.TimeUntilLeaderCheckered(realCountdown, 12.3, bogusLeaderPace);

            Assert(newWay >= realCountdown, "non si puo' finire prima dello scadere del tempo");
            Assert(newWay < realCountdown + bogusLeaderPace,
                   $"l'errore resta dentro un giro del leader, ottenuto {newWay:F1}");
            Assert(newWay < 1500.0,
                   $"nessuna traccia della sovrastima del 69%, ottenuto {newWay:F1}");
            Pass("Regressione Daytona giro 12: 2368 s -> entro un giro dai 1400 s reali");
        }

        private static void Test_Regression_BogusLeaderPaceStaysBoundedWithinOneLap()
        {
            // Lo stesso istante di gara visto con il passo giusto e con quello corrotto: la
            // differenza fra le due proiezioni non puo' superare un giro, comunque sia sbagliato
            // il passo. E' questa la proprieta' che rende il difetto non piu' pericoloso.
            const double countdown = 1088.0; // giro 16 del replay
            const double pos = 16.4;

            double withGoodPace = RaceTimeProjection.TimeUntilLeaderCheckered(countdown, pos, DaytonaGt3Pace);
            double withBogusPace = RaceTimeProjection.TimeUntilLeaderCheckered(countdown, pos, 56.391);

            double spread = Math.Abs(withGoodPace - withBogusPace);
            Assert(spread <= DaytonaGt3Pace,
                   $"lo scarto fra passo buono e passo corrotto deve stare in un giro, ottenuto {spread:F1}s");
            Pass("Un passo del leader corrotto sposta il risultato di meno di un giro");
        }

        private static void Test_ProjectedTotalLapsRoundsUpTheLapInProgress()
        {
            // 3.2 giri di tempo residuo a partire dal giro 10.4 => taglia durante il giro 13,
            // che va comunque completato: 14.
            double total = RaceTimeProjection.ProjectedTotalLaps(320.0, 10.4, 100.0);
            AssertClose(total, 14.0, 1e-9, "il giro in corso allo scadere va completato");

            // Passo inutilizzabile: si arrotonda la sola posizione, senza inventare giri.
            AssertClose(RaceTimeProjection.ProjectedTotalLaps(320.0, 10.4, 0.0), 11.0, 1e-9,
                        "senza passo non si proiettano giri");
            Pass("Giri totali arrotondati per eccesso sul giro in corso");
        }

        private static void Test_MisanoSingleClassStillProjects26Laps()
        {
            // Misano, monoclasse, il caso noto: 26 giri. Da inizio gara con il passo di database
            // la proiezione deve cadere sul valore atteso, non su 34.
            // 26 giri * 91.82 s = 2387 s di gara.
            const double raceDuration = 26 * MisanoGt3Pace;

            double timeToFlag = RaceTimeProjection.TimeUntilLeaderCheckered(raceDuration, 0.0, MisanoGt3Pace);
            double total = RaceTimeProjection.ProjectedTotalLaps(timeToFlag, 0.0, MisanoGt3Pace);

            AssertClose(total, 26.0, 1e-6, "Misano deve proiettare 26 giri tondi");

            // E a meta' gara il numero non deve muoversi.
            double halfway = RaceTimeProjection.TimeUntilLeaderCheckered(raceDuration / 2.0, 13.0, MisanoGt3Pace);
            double totalHalfway = RaceTimeProjection.ProjectedTotalLaps(halfway, 13.0, MisanoGt3Pace);
            AssertClose(totalHalfway, 26.0, 1e-6, "a meta' gara la proiezione resta 26");
            Pass("Misano monoclasse: 26 giri a inizio gara e a meta' gara");
        }

        private static void Test_PhysicalPlausibilityFloor()
        {
            // 110 m/s di media su giro intero e' il tetto. A Daytona sono 52.1 s.
            AssertClose(RaceTimeProjection.MinimumPlausibleLapSec(DaytonaMeters), 5730.0 / 110.0, 1e-9,
                        "limite fisico a Daytona");

            Assert(RaceTimeProjection.IsPhysicallyPlausibleLap(DaytonaGt3Pace, DaytonaMeters),
                   "il passo reale GT3 e' ovviamente plausibile");
            Assert(!RaceTimeProjection.IsPhysicallyPlausibleLap(30.0, DaytonaMeters),
                   "30 s a Daytona sono impossibili");
            Assert(!RaceTimeProjection.IsPhysicallyPlausibleLap(0.0, DaytonaMeters),
                   "zero non e' un tempo sul giro");

            // Lunghezza pista non nota: nessun giudizio, si accetta.
            Assert(RaceTimeProjection.IsPhysicallyPlausibleLap(30.0, 0.0),
                   "senza lunghezza pista il guard e' disattivato");
            Pass("Limite di plausibilita' fisica sul tempo sul giro");
        }

        // ---------------------------------------------------------------- filtro sul passo

        private static void Test_Filter_SeedsOnFirstSample()
        {
            var filter = new LeaderPaceFilter();
            double pace = filter.Update(DaytonaGt3Pace, "Sara Tolotti", 2600.0, DaytonaMeters);
            AssertClose(pace, DaytonaGt3Pace, 1e-9, "il primo campione buono diventa la media");
            Pass("Il primo campione plausibile inizializza la media");
        }

        private static void Test_Filter_RejectsPhysicallyImpossibleLap()
        {
            var filter = new LeaderPaceFilter();
            filter.Update(DaytonaGt3Pace, "Leader", 2600.0, DaytonaMeters);

            // 30 s a Daytona: impossibile. La media non si muove.
            double after = filter.Update(30.0, "Leader", 2500.0, DaytonaMeters);
            AssertClose(after, DaytonaGt3Pace, 1e-9, "un giro impossibile non entra nella media");
            Assert(filter.RejectedImplausible == 1, "il campione va contato come scartato");
            Pass("Un tempo sul giro fisicamente impossibile viene scartato");
        }

        private static void Test_Filter_IgnoresFlickeringLeaderIdentity()
        {
            var filter = new LeaderPaceFilter();
            filter.Update(DaytonaGt3Pace, "Leader A", 2600.0, DaytonaMeters);
            double seeded = filter.SmoothedPace;

            // Raffica di cambi identita' a decine di millisecondi l'uno dall'altro, come nel log
            // reale: nessuno regge i 2 s di dwell, quindi nessuno entra nella media.
            double clock = 2600.0;
            for (int i = 0; i < 20; i++)
            {
                clock -= 0.05;
                filter.Update(56.391, (i % 2 == 0) ? "Leader B" : "Leader C", clock, DaytonaMeters);
            }

            AssertClose(filter.SmoothedPace, seeded, 1e-9,
                        "lo sfarfallio di identita' non deve spostare la media");
            Assert(filter.RejectedUnstableIdentity == 20, "tutti i campioni sfarfallanti vanno scartati");
            Pass("Regressione: raffica di TargetChanged non contamina il passo del leader");
        }

        private static void Test_Filter_AdoptsGenuineLeaderChangeAfterDwell()
        {
            var filter = new LeaderPaceFilter();
            filter.Update(100.0, "Leader A", 2600.0, DaytonaMeters);

            // Un leader nuovo che regge oltre il dwell e' un cambio vero: deve entrare.
            filter.Update(110.0, "Leader B", 2599.0, DaytonaMeters);   // dwell non ancora scaduto
            AssertClose(filter.SmoothedPace, 100.0, 1e-9, "prima del dwell non entra");

            double after = filter.Update(110.0, "Leader B", 2597.0, DaytonaMeters); // 3 s: dwell ok
            Assert(after > 100.0, "dopo il dwell il campione entra nella media");
            AssertClose(after, 100.0 + (110.0 - 100.0) * LeaderPaceFilter.SmoothingAlpha, 1e-9,
                        "e ci entra con l'alpha storico 0.10");
            Pass("Un cambio di leader vero viene adottato dopo il dwell");
        }

        private static void Test_Filter_ResetClearsState()
        {
            var filter = new LeaderPaceFilter();
            filter.Update(DaytonaGt3Pace, "Leader", 2600.0, DaytonaMeters);
            filter.Update(30.0, "Leader", 2599.0, DaytonaMeters);

            filter.Reset();

            Assert(filter.SmoothedPace == 0.0, "reset azzera la media");
            Assert(filter.RejectedImplausible == 0, "reset azzera i contatori");
            Assert(filter.RejectedUnstableIdentity == 0, "reset azzera i contatori");
            Pass("Reset riporta il filtro allo stato iniziale");
        }
    }
}
