// -------------------------------------------------------------------------
// FILE: PlayerTotalLapsRatchetUnitTests.cs
// Y-31: il totale giri del Player saliva di uno e non tornava piu' indietro.
//
// Caso di regressione dal replay Road Atlanta del 2026-08-28
// (Logs/Serata Test Pirpi/SimRIG_DebugLog_20260828_205434.csv). La gara e'
// durata 35 giri; il totale stimato e' salito a 36 alle 21:06:17.915 — un
// singolo tick, un secondo dopo che valeva 35 — e ci e' rimasto fino alla
// bandiera, portandosi dietro un giro di carburante di troppo (~2.26 L).
//
// La causa non era il filtro di stabilita' ma il suo *chiamante*, che
// arrotondava la posizione all'intero prima di passargliela: con un intero in
// ingresso un calo di un giro vale esattamente 1.00 e non supera mai la soglia
// di 1.05 richiesta per scendere.
// -------------------------------------------------------------------------

using System;
using SimRIG;

namespace User.PluginSdkDemo.Tests
{
    public class PlayerTotalLapsRatchetUnitTests
    {
        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception("[PlayerTotalLapsRatchet] " + message);
        }

        private static void Pass(string name)
        {
            Console.WriteLine("  [PASS] " + name);
        }

        public static void RunAllTests()
        {
            Console.WriteLine("[TEST] Running Player Total Laps Ratchet Tests...");

            Test_Regression_RoadAtlantaTotalComesBackDown();
            Test_QuantizedInputCannotComeDown_WhyTheCeilingWasRemoved();
            Test_TotalStillRisesImmediately();
            Test_NoiseWithinTheBandDoesNotMoveTheTotal();
            Test_InPitLaneTheTotalDoesNotDecrease();
            Test_LeaderCapAppliesToTheContinuousPosition();
            Test_PlayerIsLeaderTakesTheLeaderTotal();

            Console.WriteLine("[TEST SUCCESS] All Player Total Laps Ratchet Tests Passed!");
        }

        /// <summary>
        /// Il caso vero. Il totale e' congelato a 36; la proiezione dice 34.80 (il valore che il
        /// software di riferimento mostrava in pista). Lo scarto e' 1.20, sopra la soglia di 1.05:
        /// il totale **deve** scendere a 35, che e' il numero di giri realmente completati.
        ///
        /// Neutralizzando la correzione (rimettendo Math.Ceiling sulla posizione dentro
        /// ProjectPlayerTotalLaps) questo test diventa rosso: 34.80 arrotondato da' 35, lo scarto
        /// da 36 diventa 1.00 e il filtro non lascia scendere nulla.
        /// </summary>
        private static void Test_Regression_RoadAtlantaTotalComesBackDown()
        {
            double result = RaceAnalyzer.ProjectPlayerTotalLaps(
                posAtCheckered: 34.80,
                currentLatched: 36.0,
                allowDecrease: true,
                leaderTotalCap: 0.0,      // multiclasse: nessun tetto sul leader assoluto
                playerIsLeader: false);

            Assert(Math.Abs(result - 35.0) < 0.0001,
                   $"con proiezione 34.80 e totale bloccato a 36 il totale deve tornare a 35, ottenuto {result}");
            Pass("Regressione Road Atlanta: da 36 si torna a 35 con proiezione 34.80");
        }

        /// <summary>
        /// La dimostrazione diretta del difetto: lo **stesso** valore, arrotondato prima di entrare
        /// nel filtro, resta incastrato. E' il motivo per cui la posizione va passata continua.
        /// </summary>
        private static void Test_QuantizedInputCannotComeDown_WhyTheCeilingWasRemoved()
        {
            double continuous = RaceAnalyzer.UpdateLatchedLaps(34.80, 36.0, true);
            double quantized = RaceAnalyzer.UpdateLatchedLaps(Math.Ceiling(34.80), 36.0, true);

            Assert(Math.Abs(continuous - 35.0) < 0.0001,
                   $"la posizione continua deve far scendere il totale a 35, ottenuto {continuous}");
            Assert(Math.Abs(quantized - 36.0) < 0.0001,
                   $"la posizione arrotondata resta incastrata a 36, ottenuto {quantized}");
            Pass("Ingresso quantizzato: il filtro non scende (difetto), continuo: scende (corretto)");
        }

        /// <summary>
        /// La salita non deve rallentare: se la gara si allunga davvero, il totale segue subito.
        /// La banda e' asimmetrica di proposito, la correzione non la tocca.
        /// </summary>
        private static void Test_TotalStillRisesImmediately()
        {
            double result = RaceAnalyzer.ProjectPlayerTotalLaps(
                posAtCheckered: 35.40,
                currentLatched: 35.0,
                allowDecrease: true,
                leaderTotalCap: 0.0,
                playerIsLeader: false);

            Assert(Math.Abs(result - 36.0) < 0.0001,
                   $"una proiezione a 35.40 con totale 35 deve salire a 36, ottenuto {result}");
            Pass("La salita resta immediata");
        }

        /// <summary>
        /// Lo scopo del filtro: una proiezione che oscilla dentro la banda non muove il totale.
        /// 35.30 sta 0.70 sotto 36 — meno della soglia di 1.05 — quindi non e' un errore di un giro,
        /// e' rumore.
        /// </summary>
        private static void Test_NoiseWithinTheBandDoesNotMoveTheTotal()
        {
            double result = RaceAnalyzer.ProjectPlayerTotalLaps(
                posAtCheckered: 35.30,
                currentLatched: 36.0,
                allowDecrease: true,
                leaderTotalCap: 0.0,
                playerIsLeader: false);

            Assert(Math.Abs(result - 36.0) < 0.0001,
                   $"35.30 e' rumore dentro la banda, il totale deve restare 36, ottenuto {result}");
            Pass("Il rumore dentro la banda non muove il totale");
        }

        /// <summary>
        /// In corsia box la proiezione non e' confrontabile (il passo e' quello del transito), quindi
        /// la discesa e' vietata anche quando lo scarto supererebbe la soglia.
        /// </summary>
        private static void Test_InPitLaneTheTotalDoesNotDecrease()
        {
            double result = RaceAnalyzer.ProjectPlayerTotalLaps(
                posAtCheckered: 34.80,
                currentLatched: 36.0,
                allowDecrease: false,     // IsInPitLane
                leaderTotalCap: 0.0,
                playerIsLeader: false);

            Assert(Math.Abs(result - 36.0) < 0.0001,
                   $"in corsia box il totale non deve scendere, ottenuto {result}");
            Pass("In corsia box la discesa resta vietata");
        }

        /// <summary>
        /// Monoclasse: il tetto del leader si applica alla posizione **continua**, cosi' il
        /// troncamento avviene una volta sola. Con proiezione 35.60 e leader a 35 il totale e' 35,
        /// non 36.
        /// </summary>
        private static void Test_LeaderCapAppliesToTheContinuousPosition()
        {
            double result = RaceAnalyzer.ProjectPlayerTotalLaps(
                posAtCheckered: 35.60,
                currentLatched: 0.0,      // primo campione della sessione
                allowDecrease: true,
                leaderTotalCap: 35.0,     // monoclasse
                playerIsLeader: false);

            Assert(Math.Abs(result - 35.0) < 0.0001,
                   $"il tetto del leader monoclasse deve limitare il totale a 35, ottenuto {result}");
            Pass("Il tetto monoclasse si applica alla posizione continua");
        }

        /// <summary>
        /// Se il Player e' P1, il suo totale e' per definizione quello del leader.
        /// </summary>
        private static void Test_PlayerIsLeaderTakesTheLeaderTotal()
        {
            double result = RaceAnalyzer.ProjectPlayerTotalLaps(
                posAtCheckered: 34.10,
                currentLatched: 0.0,
                allowDecrease: true,
                leaderTotalCap: 35.0,
                playerIsLeader: true);

            Assert(Math.Abs(result - 35.0) < 0.0001,
                   $"da P1 il totale del Player e' quello del leader (35), ottenuto {result}");
            Pass("Da P1 il totale coincide con quello del leader");
        }
    }
}
