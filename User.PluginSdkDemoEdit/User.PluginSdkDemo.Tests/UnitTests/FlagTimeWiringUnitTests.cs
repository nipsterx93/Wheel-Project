// -------------------------------------------------------------------------
// FILE: FlagTimeWiringUnitTests.cs
// L'accensione del punto 4: il tempo alla bandiera viene dalla vettura che
// sara' AL COMANDO allo scadere, non dal P1 di questo istante.
//
// Questo file copre il CABLAGGIO, non la formula (quella sta in
// FlagMomentUnitTests.cs). E' una distinzione che questo repository ha pagato:
// il difetto Y-31 non era nella formula ma nel chiamante, e un test sulla sola
// aritmetica restava verde col difetto al suo posto.
//
// Copre anche la fotografia della posizione del leader allo scadere del
// cronometro — la verita' di terreno con cui verificare la proiezione contro un
// dato misurato invece che contro il numero mostrato da un altro software.
// -------------------------------------------------------------------------

using System;
using SimRIG;

namespace User.PluginSdkDemo.Tests
{
    public class FlagTimeWiringUnitTests
    {
        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception("[FlagTimeWiring] " + message);
        }

        private static void Pass(string name)
        {
            Console.WriteLine("  [PASS] " + name);
        }

        public static void RunAllTests()
        {
            Console.WriteLine("[TEST] Running Flag Time Wiring Tests...");

            Test_TheCommandingCarWinsWhenAvailable();
            Test_Regression_NoCommandingCarFallsBackInsteadOfReturningZero();
            Test_AZeroFromTheCommandingCarIsAlsoRefused();
            Test_SnapshotOnlyAfterTheCountdownHasBeenPositive();
            Test_SnapshotFiresExactlyOnceAtExpiry();
            Test_SnapshotDoesNotFireWhileTheClockIsRunning();

            Console.WriteLine("[TEST SUCCESS] All Flag Time Wiring Tests Passed!");
        }

        /// <summary>
        /// Il caso normale: c'e' una vettura al comando, si usa il suo tempo.
        ///
        /// I due numeri sono quelli veri della finestra Barbagallo del replay
        /// `20260901_184453`: il P1 di quell'istante aveva passo 278.60 s e produceva un tempo alla
        /// bandiera di **1030.9 s** (supplemento 154.7 s, cioe' piu' di due giri del leader), mentre
        /// la vettura davvero al comando dava **927.3 s** (supplemento 51.1 s).
        ///
        /// Neutralizzazione (ADR-004): rimettendo il P1 di questo istante come sorgente, il test
        /// diventa rosso con 1030,9.
        /// </summary>
        private static void Test_TheCommandingCarWinsWhenAvailable()
        {
            double result = RaceAnalyzer.ResolveFlagTime(
                hasCommandingCar: true,
                flagFromCommandingCar: 927.3,
                flagFromCurrentP1: 1030.9);

            Assert(Math.Abs(result - 927.3) < 0.0001,
                   $"deve vincere la vettura al comando, ottenuto {result}");
            Pass("Vettura al comando: 927.3 s invece dei 1030.9 s del P1 istantaneo");
        }

        /// <summary>
        /// **Il modo grave di sbagliare.** Se nessuna vettura era valutabile, il punto 4 restituisce
        /// zero — e uno zero, a valle, si legge come "la bandiera esce adesso": i giri rimanenti
        /// vanno a zero e il carburante da imbarcare crolla a meta' gara.
        ///
        /// Succede realmente nei primi secondi di sessione, prima che gli avversari abbiano un
        /// passo: nel replay `20260901_184453` la prima riga di log ha `vetture=1`.
        ///
        /// Neutralizzazione: togliendo il ripiego — cioe' restituendo sempre il valore del punto 4 —
        /// il test diventa rosso con 0.
        /// </summary>
        private static void Test_Regression_NoCommandingCarFallsBackInsteadOfReturningZero()
        {
            double result = RaceAnalyzer.ResolveFlagTime(
                hasCommandingCar: false,
                flagFromCommandingCar: 0.0,
                flagFromCurrentP1: 934.9);

            Assert(Math.Abs(result - 934.9) < 0.0001,
                   $"senza vettura al comando si ripiega sul P1, ottenuto {result}");
            Assert(result > 0.0,
                   "e soprattutto non si espone zero, che a valle significa 'gara finita adesso'");

            // Il caso sopra passa **anche** grazie al guard sullo zero, quindi da solo non
            // verifica nulla del guard su hasCommandingCar: neutralizzando quest'ultimo il test
            // restava verde. Trovato eseguendo la neutralizzazione, che e' il motivo per cui
            // ADR-004 la richiede. Qui il valore e' diverso da zero, cosi' l'unica cosa che puo'
            // far scattare il ripiego e' la dichiarazione di assenza di risultato.
            double garbageButNonZero = RaceAnalyzer.ResolveFlagTime(
                hasCommandingCar: false,
                flagFromCommandingCar: 500.0,
                flagFromCurrentP1: 934.9);

            Assert(Math.Abs(garbageButNonZero - 934.9) < 0.0001,
                   $"un risultato dichiarato assente non si usa nemmeno se il numero sembra sano, ottenuto {garbageButNonZero}");

            Pass("Nessuna vettura al comando: ripiego sul P1, anche con un valore non nullo");
        }

        /// <summary>
        /// Cintura e bretelle: anche se il punto 4 dichiarasse un risultato ma con tempo zero o
        /// negativo, non lo si espone. La conseguenza di sbagliare qui e' asimmetrica — un tempo
        /// leggermente impreciso costa qualche decimo di litro, uno zero fa annunciare di non
        /// rifornire affatto.
        /// </summary>
        private static void Test_AZeroFromTheCommandingCarIsAlsoRefused()
        {
            double zero = RaceAnalyzer.ResolveFlagTime(true, 0.0, 934.9);
            double negative = RaceAnalyzer.ResolveFlagTime(true, -12.0, 934.9);

            Assert(Math.Abs(zero - 934.9) < 0.0001, $"uno zero va rifiutato, ottenuto {zero}");
            Assert(Math.Abs(negative - 934.9) < 0.0001, $"e anche un negativo, ottenuto {negative}");
            Pass("Tempo alla bandiera nullo o negativo: si ripiega comunque");
        }

        /// <summary>
        /// **Il guard che rende utile la fotografia.** Prima del via il cronometro di sessione vale
        /// <c>-1</c>: senza il controllo "ho gia' visto un countdown positivo", lo scatto partirebbe
        /// sulla griglia di partenza, su una posizione che non significa nulla — e resterebbe li'
        /// per tutta la gara, perche' si scatta una volta sola.
        /// </summary>
        private static void Test_SnapshotOnlyAfterTheCountdownHasBeenPositive()
        {
            bool onTheGrid = RaceAnalyzer.ShouldCaptureExpirySnapshot(
                sessionTimeLeftSec: -1.0, hasSeenPositiveCountdown: false, alreadyCaptured: false);

            Assert(!onTheGrid,
                   "in griglia il cronometro vale -1 ma la gara non e' scaduta: non si fotografa");
            Pass("Pre-gara: nessuna fotografia, il countdown a -1 non e' uno scadere");
        }

        /// <summary>
        /// Scatta all'istante in cui il cronometro va a zero, e **una volta sola**: il valore e' una
        /// fotografia, non una misura continua. Se si riscattasse a ogni tick successivo, dopo la
        /// bandiera registrerebbe la posizione di una vettura che nel frattempo ha continuato a
        /// girare, e non sarebbe piu' la verita' di terreno cercata.
        /// </summary>
        private static void Test_SnapshotFiresExactlyOnceAtExpiry()
        {
            bool first = RaceAnalyzer.ShouldCaptureExpirySnapshot(0.0, true, false);
            bool afterwards = RaceAnalyzer.ShouldCaptureExpirySnapshot(0.0, true, true);
            bool wellAfterwards = RaceAnalyzer.ShouldCaptureExpirySnapshot(-30.0, true, true);

            Assert(first, "allo scadere si fotografa");
            Assert(!afterwards, "ma non una seconda volta");
            Assert(!wellAfterwards, "ne' trenta secondi dopo");
            Pass("Fotografia allo scadere: una volta sola");
        }

        /// <summary>
        /// Col cronometro ancora in corsa non si fotografa niente, nemmeno a un secondo dalla fine:
        /// il dato che serve e' dove sta il leader **allo zero**, non poco prima.
        /// </summary>
        private static void Test_SnapshotDoesNotFireWhileTheClockIsRunning()
        {
            Assert(!RaceAnalyzer.ShouldCaptureExpirySnapshot(900.0, true, false), "a meta' gara no");
            Assert(!RaceAnalyzer.ShouldCaptureExpirySnapshot(1.0, true, false), "e nemmeno a un secondo dalla fine");
            Pass("Cronometro in corsa: nessuna fotografia");
        }
    }
}
