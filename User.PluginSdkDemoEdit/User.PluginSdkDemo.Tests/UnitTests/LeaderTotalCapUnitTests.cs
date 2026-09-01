// -------------------------------------------------------------------------
// FILE: LeaderTotalCapUnitTests.cs
// Y-46: il tetto "non puoi fare piu' giri del leader" veniva disattivato in
// multiclasse e poi riapplicato tre righe dopo senza alcuna condizione.
//
// Caso di regressione dal replay Road Atlanta del 2026-09-01
// (Logs/Road Atlanta/SimRIG_DebugLog_20260831_222417.csv), ore 22:35:25.
// Il passo fasullo di Alessandro Barbagallo (278 s, difetto Y-38) fa crollare
// il totale del LEADER da 39 a 28. Il tetto lo trasferisce di peso al Player,
// che per 103 secondi di gara legge 28-30 mentre la sua proiezione e' ferma e
// corretta a 34.53:
//
//   t=22:35:24  posAtFlag=34.537  TOT=35   leaderTot=39
//   t=22:35:25  posAtFlag=34.530  TOT=30   leaderTot=30
//   t=22:35:29  posAtFlag=34.527  TOT=28   leaderTot=28
//
// Sette giri di sottostima: ~15 L di carburante in meno del necessario, cioe'
// la direzione pericolosa. Road Atlanta e' multiclasse (GTP, LMP2, GT3) e il
// Player e' una GT3: il confronto col leader assoluto non dice nulla.
// -------------------------------------------------------------------------

using System;
using SimRIG;

namespace User.PluginSdkDemo.Tests
{
    public class LeaderTotalCapUnitTests
    {
        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception("[LeaderTotalCap] " + message);
        }

        private static void Pass(string name)
        {
            Console.WriteLine("  [PASS] " + name);
        }

        public static void RunAllTests()
        {
            Console.WriteLine("[TEST] Running Leader Total Cap Tests...");

            Test_Regression_MultiClassPlayerIsNotDraggedDownByTheLeader();
            Test_SingleClassTheCapStillApplies();
            Test_PlayerIsLeaderTakesTheCapEvenInMultiClass();
            Test_NoLeaderTotalMeansNoCap();
            Test_TheCapNeverRaisesTheTotal();

            Console.WriteLine("[TEST SUCCESS] All Leader Total Cap Tests Passed!");
        }

        /// <summary>
        /// Il caso vero. Multiclasse, Player non leader, totale del leader crollato a 28 per un
        /// difetto suo. Il totale del Player **non deve muoversi**.
        ///
        /// Neutralizzazione (ADR-004): togliendo la condizione — cioe' rimettendo il
        /// <c>Math.Min</c> incondizionato — il test diventa rosso con 28.
        /// </summary>
        private static void Test_Regression_MultiClassPlayerIsNotDraggedDownByTheLeader()
        {
            double result = RaceAnalyzer.ApplyLeaderTotalCap(
                playerTotal: 35.0,
                leaderTotal: 28.0,       // crollato per il passo fasullo di Y-38
                isMultiClass: true,      // Road Atlanta: GTP + LMP2 + GT3
                playerIsLeader: false);

            Assert(Math.Abs(result - 35.0) < 0.0001,
                   $"in multiclasse il totale del Player non segue quello del leader, ottenuto {result}");
            Pass("Regressione Road Atlanta: leader a 28, Player resta 35 (multiclasse)");
        }

        /// <summary>
        /// In monoclasse il tetto ha una funzione reale e va conservato: tutti fanno lo stesso
        /// numero di giri, quindi "non puoi finirne piu' del leader" e' vero.
        ///
        /// **E non e' ridondante** rispetto al tetto applicato a monte, che agisce sulla posizione
        /// *continua* prima della banda di stabilita': se il totale memorizzato e' gia' sopra il
        /// tetto, la banda puo' tenercelo, perche' per scendere serve un calo di piu' di un giro.
        /// Qui invece si agisce sul totale gia' formato.
        /// </summary>
        private static void Test_SingleClassTheCapStillApplies()
        {
            double result = RaceAnalyzer.ApplyLeaderTotalCap(
                playerTotal: 36.0,
                leaderTotal: 35.0,
                isMultiClass: false,
                playerIsLeader: false);

            Assert(Math.Abs(result - 35.0) < 0.0001,
                   $"in monoclasse il tetto deve applicarsi, ottenuto {result}");
            Pass("Monoclasse: il tetto resta e riporta 36 a 35");
        }

        /// <summary>
        /// Se il Player **e'** il leader, il suo totale *e'* quello del leader — qualunque sia la
        /// struttura delle classi. E' la stessa logica gia' presente a monte
        /// (<c>state.Position == 1 ? _latchedLeaderTotalLaps : leaderTotalCap</c>), e va mantenuta
        /// coerente qui: altrimenti il P1 potrebbe esporre un totale diverso dal proprio.
        /// </summary>
        private static void Test_PlayerIsLeaderTakesTheCapEvenInMultiClass()
        {
            double result = RaceAnalyzer.ApplyLeaderTotalCap(
                playerTotal: 40.0,
                leaderTotal: 39.0,
                isMultiClass: true,
                playerIsLeader: true);

            Assert(Math.Abs(result - 39.0) < 0.0001,
                   $"il P1 non puo' avere un totale diverso da quello del leader, ottenuto {result}");
            Pass("Player leader in multiclasse: il tetto si applica lo stesso");
        }

        /// <summary>
        /// Totale del leader a zero = non ancora stimato. Un tetto a zero azzererebbe il totale del
        /// Player, che a valle diventerebbe "gara finita": si lascia passare tutto.
        /// </summary>
        private static void Test_NoLeaderTotalMeansNoCap()
        {
            double notEstimated = RaceAnalyzer.ApplyLeaderTotalCap(35.0, 0.0, false, false);
            double negative = RaceAnalyzer.ApplyLeaderTotalCap(35.0, -1.0, false, false);

            Assert(Math.Abs(notEstimated - 35.0) < 0.0001,
                   $"senza stima del leader non si applica nessun tetto, ottenuto {notEstimated}");
            Assert(Math.Abs(negative - 35.0) < 0.0001,
                   $"idem per un valore assurdo, ottenuto {negative}");
            Pass("Leader non ancora stimato: nessun tetto");
        }

        /// <summary>
        /// E' un tetto, non un ancoraggio: puo' solo abbassare. Se il leader ha un totale piu' alto
        /// del Player — normale, e' il leader — il Player resta dov'e'.
        /// </summary>
        private static void Test_TheCapNeverRaisesTheTotal()
        {
            double result = RaceAnalyzer.ApplyLeaderTotalCap(35.0, 39.0, false, false);

            Assert(Math.Abs(result - 35.0) < 0.0001,
                   $"un leader piu' avanti non deve alzare il totale del Player, ottenuto {result}");
            Pass("Il tetto abbassa e basta: leader a 39, Player resta 35");
        }
    }
}
