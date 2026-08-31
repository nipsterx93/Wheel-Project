// -------------------------------------------------------------------------
// FILE: ProjectionStabilizerUnitTests.cs
// Y-45: il totale giri restava bloccato in alto per tre giri.
//
// Caso di regressione dal replay Road Atlanta del 2026-08-31
// (Logs/Road Atlanta/SimRIG_DebugLog_20260831_195300.csv). Il totale e' salito
// a 37 alle 20:04:09.525 ed e' tornato a 35 solo alle 20:05:28.695: 79 secondi
// di orologio, cioe' 237 secondi di gara, cioe' tre giri interi, con 4.2-4.4
// litri di rifornimento chiesti e non necessari.
//
// Due meccanismi distinti, misurati separatamente:
//
//   1) la SALITA. Un fotogramma non registrato a log in cui la posizione
//      proiettata ha superato 36.05 (serve quello per arrivare a 37; il massimo
//      scritto a log e' 35.435). Origine: il P1 assoluto e' passato a una
//      vettura con passo registrato 278.563 s invece di ~68.
//
//   2) la PERMANENZA. Un ritardo di 30 s applicato alla sola discesa, che
//      ripartiva da zero a ogni cambio del bersaglio. Il bersaglio oscillava
//      fra 35 e 36 perche' la proiezione ballava di nove millesimi di giro a
//      cavallo dello scalino dell'arrotondamento.
//
// I numeri usati qui sono quelli letti dal log, non valori inventati.
// -------------------------------------------------------------------------

using System;
using SimRIG;

namespace User.PluginSdkDemo.Tests
{
    public class ProjectionStabilizerUnitTests
    {
        /// <summary>Passo del tempo tipico del replay a 3x: un tick di orologio vale 3 s di gara.</summary>
        private const double ReplayStepRaceSec = 3.0;

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception("[ProjectionStabilizer] " + message);
        }

        private static void Pass(string name)
        {
            Console.WriteLine("  [PASS] " + name);
        }

        /// <summary>Porta il filtro a regime su un valore, come a meta' gara.</summary>
        private static ProjectionStabilizer SettledAt(double value)
        {
            var filter = new ProjectionStabilizer();
            for (int i = 0; i < 20; i++) filter.Update(value, ReplayStepRaceSec);
            return filter;
        }

        public static void RunAllTests()
        {
            Console.WriteLine("[TEST] Running Projection Stabilizer Tests...");

            Test_Regression_SpikeDoesNotReachTheTotal();
            Test_Regression_DitherAtTheRoundingStepNoLongerFlipsTheTarget();
            Test_SustainedChangeIsStillTracked();
            Test_LegitimateGhostLapSawtoothIsNotRejected();
            Test_FilterIsSymmetric_NoAbsorbingState();
            Test_FrozenCountdownFreezesTheFilter();
            Test_FirstMeasurementIsTakenAsIs();
            Test_SuspicionResetsWhenTheJumpChangesDirection();

            Console.WriteLine("[TEST SUCCESS] All Projection Stabilizer Tests Passed!");
        }

        /// <summary>
        /// Il caso vero, ricostruito. Il filtro e' a regime su 34.83 — il valore reale del Player in
        /// quella gara, confermato dal software di riferimento dell'utente. Arriva il fotogramma
        /// sballato a 36.63, che e' quanto produce un passo del leader di 278.563 s al posto di 68
        /// (calcolo in EarliestCheckeredUnitTests). Dura un tick e sparisce.
        ///
        /// Senza filtro quel singolo campione basta: <c>Ceiling(36.63 - 0.05) = 37</c>.
        /// Col filtro non deve muovere nulla, e il totale deve restare 35.
        ///
        /// Neutralizzazione (ADR-004): alzando SuspectJumpLaps sopra il salto — cioe' togliendo il
        /// riconoscimento dell'artefatto — il test diventa rosso, perche' il campione entra e con
        /// dt=3 s e tau=4 s se ne assorbe il 53%: 34.83 + 0.53*1.80 = 35.78, che arrotondato da' 36.
        /// </summary>
        private static void Test_Regression_SpikeDoesNotReachTheTotal()
        {
            var filter = SettledAt(34.83);

            double before = filter.Estimate;
            double afterSpike = filter.Update(36.628, ReplayStepRaceSec);

            Assert(Math.Abs(afterSpike - before) < 0.0001,
                   $"il fotogramma sballato a 36.628 non deve muovere la stima, era {before:F3} ora {afterSpike:F3}");

            double total = RaceAnalyzer.UpdateLatchedLaps(afterSpike, 35.0, true);
            Assert(Math.Abs(total - 35.0) < 0.0001,
                   $"il totale deve restare 35, ottenuto {total}");

            // E il valore grezzo, senza filtro, dimostra il difetto: e' proprio 37.
            double unfiltered = RaceAnalyzer.UpdateLatchedLaps(36.628, 35.0, true);
            Assert(Math.Abs(unfiltered - 37.0) < 0.0001,
                   $"senza filtro lo stesso campione produce 37, ottenuto {unfiltered}");

            Pass("Regressione Road Atlanta: il picco a 36.628 non arriva al totale (35), senza filtro darebbe 37");
        }

        /// <summary>
        /// Il secondo meccanismo. Questi cinque valori sono presi tali e quali dal log fra le
        /// 20:04:37 e le 20:04:55: oscillano di **nove millesimi di giro** e cadono a cavallo dello
        /// scalino di <c>Ceiling(x + 0.05)</c>, che sta esattamente a 34.95. Ogni attraversamento
        /// cambiava il bersaglio da 35 a 36 e riarmava il conto alla rovescia dei 30 secondi.
        ///
        /// Dopo il lisciamento la stima non deve piu' attraversare quello scalino avanti e indietro.
        /// </summary>
        private static void Test_Regression_DitherAtTheRoundingStepNoLongerFlipsTheTarget()
        {
            double[] fromLog = { 34.959, 34.946, 34.955, 34.950, 34.956, 34.950, 34.959 };

            // Senza filtro il bersaglio balla: la dimostrazione del difetto.
            int rawFlips = 0;
            double rawPrevious = Math.Ceiling(fromLog[0] + 0.05);
            for (int i = 1; i < fromLog.Length; i++)
            {
                double target = Math.Ceiling(fromLog[i] + 0.05);
                if (Math.Abs(target - rawPrevious) > 0.001) rawFlips++;
                rawPrevious = target;
            }
            Assert(rawFlips >= 2,
                   $"i valori grezzi del log devono far ballare il bersaglio (difetto), cambi contati {rawFlips}");

            // Col filtro no.
            var filter = SettledAt(34.952);
            int smoothedFlips = 0;
            double smoothedPrevious = Math.Ceiling(filter.Estimate + 0.05);
            foreach (double sample in fromLog)
            {
                double target = Math.Ceiling(filter.Update(sample, ReplayStepRaceSec) + 0.05);
                if (Math.Abs(target - smoothedPrevious) > 0.001) smoothedFlips++;
                smoothedPrevious = target;
            }
            Assert(smoothedFlips == 0,
                   $"dopo il lisciamento il bersaglio non deve piu' cambiare, cambi contati {smoothedFlips}");

            Pass($"Sfarfallio sullo scalino: {rawFlips} cambi grezzi -> 0 dopo il filtro");
        }

        /// <summary>
        /// Il filtro non deve diventare cieco: se la gara cambia davvero — un salto grande che
        /// **persiste** — va inseguito. Quindici secondi di gara di conferma, poi si aggancia.
        ///
        /// Neutralizzazione: se il ramo di conferma non riabbassasse tau, la stima resterebbe ferma
        /// e il test diventerebbe rosso.
        /// </summary>
        private static void Test_SustainedChangeIsStillTracked()
        {
            var filter = SettledAt(34.83);

            double elapsed = 0.0;
            double estimate = filter.Estimate;
            for (int i = 0; i < 20; i++)
            {
                estimate = filter.Update(37.50, ReplayStepRaceSec);
                elapsed += ReplayStepRaceSec;
                if (estimate > 37.0) break;
            }

            Assert(estimate > 37.0,
                   $"un cambio vero e persistente deve essere agganciato, stima {estimate:F3}");
            Assert(elapsed <= 30.0,
                   $"e va agganciato in fretta una volta confermato, servivano {elapsed:F0} s di gara");

            Pass($"Cambio vero e persistente: agganciato in {elapsed:F0} s di gara");
        }

        /// <summary>
        /// Il dente di sega del tempo alla bandiera e' **fisico**, non rumore: quando il momento
        /// della bandiera scivola oltre un giro intero, la gara dura davvero un giro del leader in
        /// piu'. A Road Atlanta vale 68.443/76.524 = 0.894 giri del Player. Non deve essere
        /// scambiato per un artefatto: la soglia sta a 1.5 giri proprio per lasciarlo passare.
        /// </summary>
        private static void Test_LegitimateGhostLapSawtoothIsNotRejected()
        {
            var filter = SettledAt(34.00);

            double moved = filter.Update(34.894, ReplayStepRaceSec);

            Assert(moved > 34.30,
                   $"il dente di sega da 0.894 giri deve entrare subito nel filtro, stima {moved:F3}");
            Assert(Math.Abs(filter.SuspicionRaceSec) < 0.0001,
                   "e non deve essere registrato come sospetto");

            Pass("Dente di sega legittimo (0.894 giri): assorbito, non scartato");
        }

        /// <summary>
        /// **Il cuore di Y-45.** Il difetto vecchio era che salire era immediato e scendere no.
        /// Lo stesso salto, stessa ampiezza e stessa durata, deve produrre la stessa risposta nelle
        /// due direzioni. Se ci fosse ancora uno stato assorbente, i due valori differirebbero.
        /// </summary>
        private static void Test_FilterIsSymmetric_NoAbsorbingState()
        {
            var up = SettledAt(35.00);
            var down = SettledAt(35.00);

            double movedUp = 0.0;
            double movedDown = 0.0;
            for (int i = 0; i < 10; i++)
            {
                movedUp = up.Update(37.50, ReplayStepRaceSec) - 35.00;
                movedDown = 35.00 - down.Update(32.50, ReplayStepRaceSec);
            }

            Assert(Math.Abs(movedUp - movedDown) < 0.0001,
                   $"salita e discesa devono essere speculari: su {movedUp:F4}, giu' {movedDown:F4}");
            Assert(movedUp > 0.5,
                   $"e devono entrambe muoversi davvero, spostamento {movedUp:F4}");

            Pass($"Simmetria: stesso spostamento in salita e in discesa ({movedUp:F3} giri)");
        }

        /// <summary>
        /// Il tempo si misura sul cronometro di gara. Se il countdown non avanza — gara in pausa,
        /// menu aperto, fotogrammi ripetuti — non c'e' informazione nuova e la stima non si muove.
        /// Senza questo, a 60 fotogrammi al secondo il filtro assorbirebbe lo stesso campione
        /// sessanta volte e il lisciamento sarebbe solo apparente.
        /// </summary>
        private static void Test_FrozenCountdownFreezesTheFilter()
        {
            var filter = SettledAt(34.83);
            double before = filter.Estimate;

            for (int i = 0; i < 50; i++) filter.Update(40.00, 0.0);

            Assert(Math.Abs(filter.Estimate - before) < 0.0001,
                   $"col countdown fermo la stima non deve muoversi, era {before:F3} ora {filter.Estimate:F3}");

            Pass("Countdown fermo: nessuna informazione nuova, filtro fermo");
        }

        /// <summary>
        /// La prima misura si prende com'e'. Partire da zero introdurrebbe un transitorio che non
        /// corrisponde a niente, e a inizio gara la proiezione servirebbe subito.
        /// </summary>
        private static void Test_FirstMeasurementIsTakenAsIs()
        {
            var filter = new ProjectionStabilizer();

            Assert(!filter.HasEstimate, "prima di ogni misura il filtro non deve dichiarare una stima");

            double first = filter.Update(34.83, ReplayStepRaceSec);

            Assert(Math.Abs(first - 34.83) < 0.0001,
                   $"la prima misura si prende intera, ottenuto {first:F3}");
            Assert(filter.HasEstimate, "dopo la prima misura la stima esiste");

            Pass("Prima misura presa senza transitorio");
        }

        /// <summary>
        /// Un sospetto e' tale solo se **coerente**. Salti grandi che si alternano di segno sono
        /// rumore, non un cambio di regime: il contatore riparte a ogni inversione, cosi' un
        /// disturbo che oscilla non arriva mai a farsi confermare.
        /// </summary>
        private static void Test_SuspicionResetsWhenTheJumpChangesDirection()
        {
            var filter = SettledAt(35.00);
            double before = filter.Estimate;

            // Dieci tick alternati: trenta secondi di gara, ben oltre i quindici di conferma.
            for (int i = 0; i < 10; i++)
            {
                filter.Update(i % 2 == 0 ? 38.00 : 32.00, ReplayStepRaceSec);
            }

            Assert(Math.Abs(filter.Estimate - before) < 0.0001,
                   $"salti alternati non devono confermare nulla, stima {filter.Estimate:F3} contro {before:F3}");
            Assert(filter.SuspicionRaceSec <= ReplayStepRaceSec + 0.0001,
                   $"il contatore del sospetto deve ripartire a ogni inversione, vale {filter.SuspicionRaceSec:F1}s");

            Pass("Salti alternati di segno: mai confermati, la stima non si muove");
        }
    }
}
