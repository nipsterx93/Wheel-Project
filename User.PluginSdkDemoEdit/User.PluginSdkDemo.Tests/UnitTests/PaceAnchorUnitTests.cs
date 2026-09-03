// -------------------------------------------------------------------------
// FILE: PaceAnchorUnitTests.cs
// Y-49: l'ancora del passo sapeva migliorare, ma non ci arrivava mai.
//
// Il codice abbassava l'ancora a ogni giro piu' veloce. Il controllo di
// validita' pero' girava PRIMA, e scartava i giri che deviassero dall'ancora
// oltre il 2% sul lato veloce. Un'ancora sbagliata rendeva quindi invalidi
// proprio i giri che l'avrebbero corretta, e il ramo che li avrebbe promossi
// non veniva mai raggiunto.
//
// Misurato su Road Atlanta 20260901_211532, 803 tick di gara:
//
//   Baseline Established           48 volte
//   Baseline Updated (Better)      55 volte
//   Baseline Reset (Improvement)    0 volte
//
//   Alessandro Barbagallo: ancora fissata a 278.563 s al giro 7, mai piu'
//   aggiornata. Finestra che ne derivava: [273.0 , 288.3]. I suoi giri veri
//   valevano ~69 s: 209 secondi sotto, tutti rifiutati.
//
// E c'era un secondo blocco, aritmetico: il ramo "Reset (Improvement)"
// pretendeva un miglioramento superiore a 1.5 s, ma la finestra ne concedeva
// al massimo il 2% dell'ancora. Sotto i 75 s di giro le due condizioni sono
// incompatibili (2% di 69 = 1.38 < 1.5): su una GTP non poteva scattare mai.
// Gli zero casi contati in gara lo confermano.
// -------------------------------------------------------------------------

using System;
using SimRIG;

namespace User.PluginSdkDemo.Tests
{
    public class PaceAnchorUnitTests
    {
        /// <summary>L'ancora sbagliata di Alessandro Barbagallo, dal log.</summary>
        private const double PoisonedAnchor = 278.563;

        /// <summary>Il suo passo vero, misurato da passaggi consecutivi.</summary>
        private const double RealPace = 69.0;

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception("[PaceAnchor] " + message);
        }

        private static void Pass(string name)
        {
            Console.WriteLine("  [PASS] " + name);
        }

        public static void RunAllTests()
        {
            Console.WriteLine("[TEST] Running Pace Anchor Tests...");

            Test_Regression_APoisonedAnchorIsCorrectedByTwoRealLaps();
            Test_Regression_TheOldWindowWouldHaveRejectedTheCorrection();
            Test_ASingleBigImprovementIsHeldNotTaken();
            Test_TwoBigImprovementsThatDisagreeAreBothHeld();
            Test_AnOrdinaryImprovementIsTakenImmediately();
            Test_TheFirstLapBecomesTheAnchor();
            Test_ASlowerLapNeverMovesTheAnchor();
            Test_TheOldResetBranchWasArithmeticallyUnreachable();

            Console.WriteLine("[TEST SUCCESS] All Pace Anchor Tests Passed!");
        }

        /// <summary>
        /// Il caso vero. Ancora avvelenata a 278.563; arrivano due giri veri da 69 s. Il primo va in
        /// attesa, il secondo lo conferma e l'ancora si sposta.
        ///
        /// Neutralizzazione (ADR-004): rimettendo il rifiuto dei giri piu' veloci — cioe' facendo
        /// tornare falso il ramo del miglioramento — il test diventa rosso con l'ancora ferma a
        /// 278,563.
        /// </summary>
        private static void Test_Regression_APoisonedAnchorIsCorrectedByTwoRealLaps()
        {
            double anchor = PoisonedAnchor;
            double pending = 0.0;

            bool first = OpponentTracker.UpdatePaceAnchor(RealPace, ref anchor, ref pending);
            Assert(!first, "il primo giro vero va tenuto in attesa, non preso alla cieca");
            Assert(Math.Abs(anchor - PoisonedAnchor) < 0.0001,
                   $"e l'ancora non si muove ancora, ottenuto {anchor:F3}");
            Assert(Math.Abs(pending - RealPace) < 0.0001,
                   $"ma il giro resta in attesa, ottenuto {pending:F3}");

            bool second = OpponentTracker.UpdatePaceAnchor(69.2, ref anchor, ref pending);
            Assert(second, "il secondo giro conferma e va accettato");
            Assert(Math.Abs(anchor - 69.2) < 0.0001,
                   $"e l'ancora si sposta sul passo vero, ottenuto {anchor:F3}");
            Assert(Math.Abs(pending) < 0.0001, "l'attesa si chiude");

            Pass("Regressione Barbagallo: da 278.563 a 69.2 in due giri, invece che mai");
        }

        /// <summary>
        /// La dimostrazione del difetto vecchio, sugli stessi numeri. La finestra rifiutava tutto
        /// cio' che stesse oltre il 2% sotto l'ancora: con ancora 278.563 il limite inferiore era
        /// **273.0 s**, e un giro da 69 s ne stava 204 sotto.
        ///
        /// Non e' un margine stretto: e' un ordine di grandezza. Nessun giro reale di quella vettura
        /// poteva rientrare, quindi l'ancora era inchiodata per il resto della gara.
        /// </summary>
        private static void Test_Regression_TheOldWindowWouldHaveRejectedTheCorrection()
        {
            double lowerBound = PoisonedAnchor - (PoisonedAnchor * 0.020);

            Assert(Math.Abs(lowerBound - 273.0) < 0.1,
                   $"il limite inferiore della vecchia finestra era 273.0 s, ottenuto {lowerBound:F1}");
            Assert(RealPace < lowerBound,
                   "e il passo vero ci stava sotto, quindi veniva rifiutato");
            Assert(lowerBound - RealPace > 200.0,
                   $"di oltre duecento secondi, ottenuto {lowerBound - RealPace:F1}");

            Pass($"Vecchia finestra: limite {lowerBound:F1} s contro un passo vero di {RealPace} s");
        }

        /// <summary>
        /// **La protezione nell'altra direzione, e conta piu' di prima.** Col criterio del massimo
        /// (punto 4) un passo falsamente **veloce** porta quella vettura al comando e anticipa la
        /// bandiera per tutti. Quindi un solo giro molto piu' veloce non basta: si tiene in attesa.
        ///
        /// E finche' e' in attesa il giro **non e' valido**, cosi' non finisce nemmeno nella media
        /// mobile: un errore di misura non deve inquinare il passo neanche senza diventare ancora.
        /// </summary>
        private static void Test_ASingleBigImprovementIsHeldNotTaken()
        {
            double anchor = 69.0;
            double pending = 0.0;

            // Un giro da 45 s per una GTP a Road Atlanta e' impossibile: errore di misura.
            bool accepted = OpponentTracker.UpdatePaceAnchor(45.0, ref anchor, ref pending);

            Assert(!accepted, "un solo giro molto piu' veloce non va accettato");
            Assert(Math.Abs(anchor - 69.0) < 0.0001,
                   $"l'ancora non si muove, ottenuto {anchor:F3}");
            Assert(Math.Abs(pending - 45.0) < 0.0001, "ma il valore resta in attesa");

            Pass("Miglioramento grande isolato: tenuto in attesa, fuori dalla media");
        }

        /// <summary>
        /// Due giri molto piu' veloci ma **in disaccordo fra loro** non si confermano a vicenda: se
        /// fossero reali si somiglierebbero. Restano entrambi fuori, e l'ancora non si muove.
        /// </summary>
        private static void Test_TwoBigImprovementsThatDisagreeAreBothHeld()
        {
            double anchor = 69.0;
            double pending = 0.0;

            OpponentTracker.UpdatePaceAnchor(45.0, ref anchor, ref pending);
            bool second = OpponentTracker.UpdatePaceAnchor(58.0, ref anchor, ref pending);

            Assert(!second, "due valori distanti non si confermano");
            Assert(Math.Abs(anchor - 69.0) < 0.0001,
                   $"l'ancora resta dov'era, ottenuto {anchor:F3}");
            Assert(Math.Abs(pending - 58.0) < 0.0001,
                   "e in attesa resta il piu' recente, non il primo");

            Pass("Due miglioramenti grandi discordi: nessuno dei due passa");
        }

        /// <summary>
        /// Il caso ordinario, che deve restare com'era: un giro un po' piu' veloce e' semplicemente
        /// un giro migliore, e l'ancora lo prende subito. E' il comportamento che il codice aveva
        /// gia' e che funzionava — Y-49 non lo tocca.
        /// </summary>
        private static void Test_AnOrdinaryImprovementIsTakenImmediately()
        {
            double anchor = 69.0;
            double pending = 0.0;

            // Un secondo di miglioramento: sotto il 2% di 69, quindi ordinario.
            bool accepted = OpponentTracker.UpdatePaceAnchor(68.0, ref anchor, ref pending);

            Assert(accepted, "un miglioramento ordinario si accetta subito");
            Assert(Math.Abs(anchor - 68.0) < 0.0001, $"e l'ancora si sposta, ottenuto {anchor:F3}");
            Assert(Math.Abs(pending) < 0.0001, "senza lasciare niente in attesa");

            Pass("Miglioramento ordinario (1 s su 69): preso subito, come prima");
        }

        /// <summary>
        /// Prima misura: diventa l'ancora, non c'e' niente con cui confrontarla. Se la si rifiutasse
        /// in attesa di conferma, la vettura resterebbe senza passo per due giri.
        /// </summary>
        private static void Test_TheFirstLapBecomesTheAnchor()
        {
            double anchor = 0.0;
            double pending = 0.0;

            bool accepted = OpponentTracker.UpdatePaceAnchor(70.5, ref anchor, ref pending);

            Assert(accepted, "la prima misura si accetta");
            Assert(Math.Abs(anchor - 70.5) < 0.0001, $"e diventa l'ancora, ottenuto {anchor:F3}");

            Pass("Prima misura: diventa l'ancora senza attese");
        }

        /// <summary>
        /// Un giro piu' lento non tocca l'ancora — l'ancora e' il **migliore**, non l'ultimo. Il
        /// giudizio sui giri lenti resta alla finestra del chiamante, che li scarta se deviano oltre
        /// il 3.5%: qui si dice solo che l'ancora non si alza mai.
        /// </summary>
        private static void Test_ASlowerLapNeverMovesTheAnchor()
        {
            double anchor = 69.0;
            double pending = 0.0;

            bool accepted = OpponentTracker.UpdatePaceAnchor(71.0, ref anchor, ref pending);

            Assert(accepted, "un giro piu' lento e' comunque un dato valido");
            Assert(Math.Abs(anchor - 69.0) < 0.0001,
                   $"ma l'ancora non si alza mai, ottenuto {anchor:F3}");

            Pass("Giro piu' lento: l'ancora resta il migliore");
        }

        /// <summary>
        /// **Il secondo blocco, quello aritmetico.** Il ramo vecchio che azzerava la storia chiedeva
        /// un miglioramento superiore a **1.5 s**; la finestra ne concedeva al massimo il **2%**
        /// dell'ancora. Sotto i 75 s di giro le due condizioni non possono essere vere insieme.
        ///
        /// Questo test non esercita codice di produzione: fissa per iscritto un fatto aritmetico che
        /// spiega perche' quel ramo non sia mai scattato in gara, nemmeno una volta su 803 tick.
        /// Senza, la prossima sessione potrebbe rimettere una soglia fissa e rifare l'errore.
        /// </summary>
        private static void Test_TheOldResetBranchWasArithmeticallyUnreachable()
        {
            const double oldResetThresholdSec = 1.5;
            const double oldWindowFrac = 0.020;

            double gtpAnchor = 69.0;
            double maxImprovementAllowed = gtpAnchor * oldWindowFrac;

            Assert(maxImprovementAllowed < oldResetThresholdSec,
                   $"su una GTP la finestra concedeva {maxImprovementAllowed:F2} s e il ramo ne chiedeva {oldResetThresholdSec}");

            // Sopra i 75 s di giro il ramo diventa raggiungibile: e' il confine.
            double breakEvenAnchor = oldResetThresholdSec / oldWindowFrac;
            Assert(Math.Abs(breakEvenAnchor - 75.0) < 0.001,
                   $"il confine sta a 75 s di giro, ottenuto {breakEvenAnchor:F1}");

            Pass($"Ramo vecchio irraggiungibile sotto i {breakEvenAnchor:F0} s di giro: 1.38 s concessi contro 1.5 richiesti");
        }
    }
}
