// -------------------------------------------------------------------------
// FILE: LeaderPositionMissingUnitTests.cs
// Y-35: la posizione del leader non arriva, e viene letta come "leader
// esattamente sul traguardo".
//
// Caso di regressione dal replay Road Atlanta del 2026-08-29
// (Logs/Road Atlanta/SimRIG_DebugLog_20260829_140004.csv), giro 30: tutti e
// 13 i tick riportano il leader a PosPct=0.0000 con LapsComp=32.
// Nella stessa gara succede nel 26% dei tick.
//
// Il danno non e' un numero assurdo ma un numero *stabile e sbagliato*: con la
// posizione del leader ferma, il tempo alla bandiera diventa esattamente
// costante, e la proiezione del Player sale 1:1 con lui per tutto il giro.
// -------------------------------------------------------------------------

using System;
using SimRIG;

namespace User.PluginSdkDemo.Tests
{
    public class LeaderPositionMissingUnitTests
    {
        // Numeri veri del giro 30, replay Road Atlanta 2026-08-29.
        private const double LeaderPaceSec = 61.336;
        private const int LeaderLapsCompleted = 32;
        private const double TimeLeftAtLapStart = 382.1;
        private const double TimeLeftAtLapEnd = 309.4;

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception("[LeaderPositionMissing] " + message);
        }

        private static void Pass(string name)
        {
            Console.WriteLine("  [PASS] " + name);
        }

        public static void RunAllTests()
        {
            Console.WriteLine("[TEST] Running Leader Position Missing Tests...");

            Test_Regression_ZeroWithLapsIsAMissingPosition();
            Test_BlankRecordGuardStillDistinguishesItsOwnCase();
            Test_Regression_FrozenLeaderFreezesTimeToFlag();
            Test_DeadReckoningKeepsTimeToFlagDecreasing();
            Test_DeadReckonIsBoundedByTheKnownLapCount();
            Test_DeadReckonWithoutPaceHoldsInsteadOfInventing();
            Test_RealPositionIsNeverOverridden();
            Test_Regression_ResolverUsesTheEstimateWhenPositionIsMissing();
            Test_ResolverPrefersTheLivePositionWhenItArrives();

            Console.WriteLine("[TEST SUCCESS] All Leader Position Missing Tests Passed!");
        }

        /// <summary>
        /// Il caso vero: giri popolati, posizione a zero esatto. Va riconosciuto come posizione
        /// mancante, non come leader sulla linea.
        /// </summary>
        private static void Test_Regression_ZeroWithLapsIsAMissingPosition()
        {
            Assert(RaceAnalyzer.IsLeaderPositionMissing(LeaderLapsCompleted, 0.0),
                   "leader con 32 giri e posizione 0.0000: la posizione non e' arrivata");
            Assert(!RaceAnalyzer.IsLeaderPositionMissing(LeaderLapsCompleted, 0.4231),
                   "una posizione vera non deve mai risultare mancante");
            Pass("Posizione a zero con giri popolati = dato mancante");
        }

        /// <summary>
        /// Y-24 resta com'era: la sua domanda ("il record e' vuoto?") e' diversa. Con i giri
        /// popolati il record **non** e' vuoto, ed e' esattamente il motivo per cui lasciava
        /// passare il caso di Y-35.
        /// </summary>
        private static void Test_BlankRecordGuardStillDistinguishesItsOwnCase()
        {
            Assert(RaceAnalyzer.IsLeaderSampleUsable(LeaderLapsCompleted, 0.0),
                   "per Y-24 il record con 32 giri non e' vuoto: comportamento invariato");
            Assert(!RaceAnalyzer.IsLeaderSampleUsable(0, 0.0),
                   "record davvero vuoto (zero giri, zero posizione) resta inutilizzabile");
            Pass("Il guard di Y-24 conserva la sua semantica");
        }

        /// <summary>
        /// La dimostrazione del meccanismo, coi numeri del log.
        ///
        /// Con la posizione del leader ferma, nel tempo alla bandiera il countdown **si semplifica
        /// algebricamente**:
        /// <code>
        ///   tempo = timeLeft + (Ceiling(pos + timeLeft/pace) - pos - timeLeft/pace) * pace
        ///         = pace * (Ceiling(posAtExpiry) - pos)          // timeLeft sparisce
        /// </code>
        /// Il risultato non dipende piu' dal cronometro: diventa una scala a gradini di giri interi
        /// del leader. Nel log del giro 30 vale <c>429.352</c> per tre tick consecutivi — 12.2
        /// secondi di gara in cui il tempo residuo non si muove di un millesimo — e poi scende di
        /// colpo a <c>368.016</c>, esattamente un giro del leader piu' in basso.
        ///
        /// E' il tipo di errore peggiore da trovare a occhio: non un numero assurdo, un numero
        /// **immobile e plausibile**.
        /// </summary>
        private static void Test_Regression_FrozenLeaderFreezesTimeToFlag()
        {
            double frozenPos = LeaderLapsCompleted; // 32.000, la posizione a zero presa per buona

            // Tre tick reali del giro 30, dentro lo stesso gradino.
            double t1 = RaceTimeProjection.TimeUntilLeaderCheckered(382.1, frozenPos, LeaderPaceSec);
            double t2 = RaceTimeProjection.TimeUntilLeaderCheckered(376.1, frozenPos, LeaderPaceSec);
            double t3 = RaceTimeProjection.TimeUntilLeaderCheckered(369.9, frozenPos, LeaderPaceSec);

            Assert(Math.Abs(t1 - t2) < 0.001 && Math.Abs(t2 - t3) < 0.001,
                   $"12.2s di gara e il tempo alla bandiera non si muove: {t1:F3} / {t2:F3} / {t3:F3}");

            // E il valore e' un multiplo esatto del giro del leader: la firma della patologia.
            double inLeaderLaps = t1 / LeaderPaceSec;
            Assert(Math.Abs(inLeaderLaps - Math.Round(inLeaderLaps)) < 0.001,
                   $"il tempo residuo e' un multiplo intero del giro del leader, vale {inLeaderLaps:F3}");

            Pass($"Regressione: leader fermo => tempo alla bandiera immobile a {t1:F3}s (= {inLeaderLaps:F0} giri leader)");
        }

        /// <summary>
        /// Con la posizione fatta avanzare il countdown torna a contare: sui 12.2 secondi che prima
        /// non muovevano nulla, adesso il tempo alla bandiera scende di altrettanto.
        /// </summary>
        private static void Test_DeadReckoningKeepsTimeToFlagDecreasing()
        {
            double posAtStart = LeaderLapsCompleted + 0.0;
            double elapsed = 382.1 - 369.9;   // 12.2 s di gara, gli stessi tick di sopra

            double posAtEnd = RaceAnalyzer.DeadReckonLeaderPos(posAtStart, elapsed,
                                                               LeaderPaceSec, LeaderLapsCompleted);

            double before = RaceTimeProjection.TimeUntilLeaderCheckered(382.1, posAtStart, LeaderPaceSec);
            double after = RaceTimeProjection.TimeUntilLeaderCheckered(369.9, posAtEnd, LeaderPaceSec);

            double drop = before - after;
            Assert(Math.Abs(drop - elapsed) < 0.5,
                   $"il tempo alla bandiera deve scendere dei {elapsed:F1}s trascorsi, sceso di {drop:F2}s");
            Pass($"Con la posizione avanzata il tempo alla bandiera segue il cronometro ({drop:F2}s su {elapsed:F1}s)");
        }

        /// <summary>
        /// Il conteggio giri continua ad arrivare anche quando la posizione no: la stima ci resta
        /// dentro, quindi l'errore non puo' superare il giro nemmeno con un passo sbagliato.
        /// </summary>
        private static void Test_DeadReckonIsBoundedByTheKnownLapCount()
        {
            // Passo assurdamente veloce: senza vincolo avanzerebbe di oltre 7 giri.
            double runaway = RaceAnalyzer.DeadReckonLeaderPos(32.0, 72.7, 10.0, LeaderLapsCompleted);
            Assert(runaway >= 32.0 && runaway < 33.0,
                   $"la stima deve restare nel giro 32, ottenuto {runaway}");

            // Posizione tenuta rimasta indietro rispetto ai giri gia' completati.
            double stale = RaceAnalyzer.DeadReckonLeaderPos(30.2, 0.0, LeaderPaceSec, LeaderLapsCompleted);
            Assert(Math.Abs(stale - 32.0) < 0.0001,
                   $"una posizione arretrata va portata al giro noto, ottenuto {stale}");
            Pass("La stima resta dentro il giro che il conteggio dichiara in corso");
        }

        /// <summary>
        /// Senza un passo utilizzabile non si inventa un avanzamento: si tiene quello che si ha.
        /// Stessa scelta di TimeUntilLeaderCheckered quando il passo manca.
        /// </summary>
        private static void Test_DeadReckonWithoutPaceHoldsInsteadOfInventing()
        {
            double held = RaceAnalyzer.DeadReckonLeaderPos(32.4, 72.7, 0.0, LeaderLapsCompleted);
            Assert(Math.Abs(held - 32.4) < 0.0001,
                   $"senza passo si tiene la posizione, ottenuto {held}");
            Pass("Senza passo utilizzabile la posizione si tiene, non si inventa");
        }

        /// <summary>
        /// Quando la posizione c'e' davvero non si tocca nulla: la stima serve solo a coprire i
        /// buchi.
        /// </summary>
        private static void Test_RealPositionIsNeverOverridden()
        {
            Assert(!RaceAnalyzer.IsLeaderPositionMissing(LeaderLapsCompleted, 0.8797),
                   "0.8797 e' una posizione vera (ultimo tick della gara nel log)");
            Assert(!RaceAnalyzer.IsLeaderPositionMissing(LeaderLapsCompleted, 0.0001),
                   "anche una posizione minuscola ma non nulla e' un dato arrivato");
            Pass("Una posizione reale non viene mai sostituita dalla stima");
        }

        /// <summary>
        /// La scelta del chiamante, non solo l'aritmetica: col campione live a zero il risolutore
        /// **deve** restituire la stima avanzata, non <c>32 + 0.0</c>.
        ///
        /// E' il test che diventa rosso se qualcuno riporta la posizione grezza dentro
        /// <see cref="RaceAnalyzer.ResolveLeaderAbsolutePos"/>. Y-31 aveva insegnato che un test
        /// sulla sola funzione di calcolo non basta: il difetto stava nel chiamante.
        /// </summary>
        private static void Test_Regression_ResolverUsesTheEstimateWhenPositionIsMissing()
        {
            double resolved = RaceAnalyzer.ResolveLeaderAbsolutePos(
                leaderLapsCompleted: LeaderLapsCompleted,
                rawTrackPos: 0.0,                 // il campione del giro 30, per 13 tick di fila
                lastGoodPos: 32.0,
                elapsedSec: 12.2,
                leaderPaceSec: LeaderPaceSec);

            Assert(resolved > 32.0 + 0.0001,
                   $"con la posizione assente il leader non puo' restare inchiodato a 32.000, ottenuto {resolved:F4}");
            Assert(Math.Abs(resolved - (32.0 + 12.2 / LeaderPaceSec)) < 0.0001,
                   $"la stima deve avanzare di 12.2s di passo, ottenuto {resolved:F4}");
            Pass($"Regressione: posizione assente => il risolutore avanza a {resolved:F4} invece di 32.0000");
        }

        /// <summary>
        /// E quando la posizione c'e', si usa quella: la stima non deve mai prendere il sopravvento
        /// su un dato vero.
        /// </summary>
        private static void Test_ResolverPrefersTheLivePositionWhenItArrives()
        {
            double resolved = RaceAnalyzer.ResolveLeaderAbsolutePos(
                leaderLapsCompleted: LeaderLapsCompleted,
                rawTrackPos: 0.8797,              // ultimo tick reale della gara nel log
                lastGoodPos: 32.0,
                elapsedSec: 999.0,                // stima volutamente lontana: non deve contare
                leaderPaceSec: LeaderPaceSec);

            Assert(Math.Abs(resolved - 32.8797) < 0.0001,
                   $"con la posizione live presente il risolutore deve usarla, ottenuto {resolved:F4}");
            Pass("Con la posizione live presente la stima non entra in gioco");
        }
    }
}
