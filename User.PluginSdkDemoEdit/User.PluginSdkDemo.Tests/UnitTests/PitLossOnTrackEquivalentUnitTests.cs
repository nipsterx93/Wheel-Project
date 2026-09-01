// -------------------------------------------------------------------------
// FILE: PitLossOnTrackEquivalentUnitTests.cs
// Y-44: il costo della sosta contava l'intera traversata della corsia box come
// tempo perso, senza sottrarre quello che ci avresti messo a percorrere quella
// porzione di tracciato in pista.
//
// La perdita reale non e' stata stimata: e' stata MISURATA dai tempi sul giro
// del replay Road Atlanta del 2026-09-01
// (Logs/Road Atlanta/SimRIG_DebugLog_20260901_211532.csv), che e' la
// definizione stessa della grandezza:
//
//   giri 14+15+16 (in-lap, out-lap, primo giro pieno)   268.20 s
//   tre giri normali (77.45 s l'uno)                    232.35 s
//   ------------------------------------------------------------
//   perdita reale                                        35.85 s
//     di cui da fermo                                    14.75 s
//     quindi perdita non-ferma                           21.05 s
//
// Componenti che il plugin usava:
//   PitTransitTime        26.38 s   (gia' al netto del tempo da fermo)
//   PitInOutAccDecTime     9.72 s
//   totale non-fermo      36.10 s   contro 21.05 reali -> eccesso 15.05 s
//
// E 15.05 s a 77.45 s/giro fanno 0.194 di giro, che e' **esattamente** quanto
// misura la zona box: 0.131 di corsia stretta (ingresso 0.957, uscita 0.088)
// piu' due volte il margine di esclusione. La coincidenza fra l'eccesso di
// tempo e la geometria della zona e' la dimostrazione della causa.
// -------------------------------------------------------------------------

using System;
using SimRIG;

namespace User.PluginSdkDemo.Tests
{
    public class PitLossOnTrackEquivalentUnitTests
    {
        // Road Atlanta, replay 20260901_211532.
        private const double TransitSec = 26.38;
        private const double AccelDecelSec = 9.72;
        private const double StationarySec = 14.75;
        private const double PaceSec = 77.45;
        private const double EntryPct = 0.957;
        private const double ExitPct = 0.088;
        private const double Margin = 0.05;

        /// <summary>Perdita misurata dai tempi sul giro: 268.20 - 3 x 77.45.</summary>
        private const double MeasuredLossSec = 35.85;

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception("[PitLossOnTrack] " + message);
        }

        private static void Pass(string name)
        {
            Console.WriteLine("  [PASS] " + name);
        }

        public static void RunAllTests()
        {
            Console.WriteLine("[TEST] Running Pit Loss On-Track Equivalent Tests...");

            Test_Regression_TheOnTrackEquivalentIsSubtracted();
            Test_Regression_TheOldFormulaOverestimatedByHalf();
            Test_TheZoneFractionHandlesTheStartLineWrap();
            Test_UncalibratedGeofenceFallsBackInsteadOfInventing();
            Test_ImplausibleGeometryIsRefused();
            Test_TheCorrectionCanNeverZeroTheCost();
            Test_OnlyTheStationaryPartScalesWithFuel();

            Console.WriteLine("[TEST SUCCESS] All Pit Loss On-Track Equivalent Tests Passed!");
        }

        /// <summary>
        /// Il caso vero. Coi numeri misurati la stima deve avvicinarsi alla perdita reale di 35.85 s,
        /// non superarla del cinquanta per cento.
        ///
        /// Resta un residuo di qualche secondo — la corsia box non e' esattamente parallela al
        /// tracciato e gli estremi della geofence hanno la loro incertezza — ma il segno e' quello
        /// **sicuro**: sottostimare la perdita significa proiettare qualche giro in piu' e imbarcare
        /// qualche decilitro di troppo, non restare a piedi.
        ///
        /// Neutralizzazione (ADR-004): togliendo la sottrazione dell'equivalente in pista il test
        /// diventa rosso con 50,85.
        /// </summary>
        private static void Test_Regression_TheOnTrackEquivalentIsSubtracted()
        {
            double fraction = RaceTimeProjection.PitZoneLapFraction(EntryPct, ExitPct, Margin);
            double loss = RaceTimeProjection.PitLossSec(TransitSec, AccelDecelSec, StationarySec,
                                                        fraction, PaceSec);

            Assert(Math.Abs(loss - MeasuredLossSec) < 5.0,
                   $"la stima deve stare entro cinque secondi dai {MeasuredLossSec} s misurati, ottenuto {loss:F2}");
            Assert(loss < MeasuredLossSec,
                   $"e deve sbagliare per difetto, che e' la direzione sicura: ottenuto {loss:F2}");

            Pass($"Regressione Road Atlanta: {loss:F1} s stimati contro {MeasuredLossSec} s misurati");
        }

        /// <summary>
        /// La dimostrazione diretta del difetto: gli stessi ingressi, senza la correzione, davano
        /// **50.85 s** contro i 35.85 reali. Il quaranta per cento in piu' — e su una proiezione da
        /// 77 secondi al giro sono quasi 0.2 giri sbagliati a ogni sosta prevista.
        /// </summary>
        private static void Test_Regression_TheOldFormulaOverestimatedByHalf()
        {
            double oldWay = TransitSec + AccelDecelSec + StationarySec;
            double fraction = RaceTimeProjection.PitZoneLapFraction(EntryPct, ExitPct, Margin);
            double newWay = RaceTimeProjection.PitLossSec(TransitSec, AccelDecelSec, StationarySec,
                                                          fraction, PaceSec);

            Assert(Math.Abs(oldWay - 50.85) < 0.01,
                   $"la formula vecchia dava 50.85 s, ottenuto {oldWay:F2}");
            Assert(oldWay - MeasuredLossSec > 14.0,
                   $"cioe' oltre quattordici secondi di troppo, ottenuto {oldWay - MeasuredLossSec:F2}");
            Assert(newWay < oldWay - 10.0,
                   $"la nuova deve toglierne almeno dieci, ottenuto {oldWay - newWay:F2}");

            Pass($"Formula vecchia {oldWay:F1} s, nuova {newWay:F1} s, reale {MeasuredLossSec} s");
        }

        /// <summary>
        /// A Road Atlanta la corsia box **scavalca il traguardo**: ingresso a 0.957, uscita a 0.088.
        /// La sottrazione diretta darebbe un numero negativo, e con un numero negativo la correzione
        /// sparirebbe in silenzio — il difetto tornerebbe senza che nulla lo segnali.
        /// </summary>
        private static void Test_TheZoneFractionHandlesTheStartLineWrap()
        {
            double wrapping = RaceTimeProjection.PitZoneLapFraction(0.957, 0.088, 0.05);
            double normal = RaceTimeProjection.PitZoneLapFraction(0.20, 0.33, 0.05);

            Assert(Math.Abs(wrapping - 0.231) < 0.001,
                   $"0.957 -> 0.088 fa 0.131 di corsia piu' 0.10 di margine = 0.231, ottenuto {wrapping:F3}");
            Assert(Math.Abs(normal - 0.230) < 0.001,
                   $"e una corsia che non scavalca deve dare lo stesso genere di risultato, ottenuto {normal:F3}");

            Pass($"Scavalco del traguardo: 0.957 -> 0.088 = {wrapping:F3} di giro");
        }

        /// <summary>
        /// Geofence non calibrata: gli estremi valgono <c>-1</c>. Senza geometria non si corregge
        /// nulla e si torna al comportamento vecchio — sbagliato ma noto — invece di sottrarre un
        /// numero inventato. E' la stessa scelta di IsPhysicallyPlausibleLap: meglio nessun giudizio
        /// che uno basato su un dato che non esiste.
        /// </summary>
        private static void Test_UncalibratedGeofenceFallsBackInsteadOfInventing()
        {
            double fraction = RaceTimeProjection.PitZoneLapFraction(-1.0, -1.0, 0.05);
            double loss = RaceTimeProjection.PitLossSec(TransitSec, AccelDecelSec, StationarySec,
                                                        fraction, PaceSec);

            Assert(Math.Abs(fraction) < 0.0001, $"senza geofence la frazione e' zero, ottenuto {fraction}");
            Assert(Math.Abs(loss - 50.85) < 0.01,
                   $"e il costo torna quello vecchio, ottenuto {loss:F2}");

            Pass("Geofence non calibrata: nessuna correzione, comportamento vecchio");
        }

        /// <summary>
        /// Una zona box piu' lunga di mezzo tracciato non esiste: se il conto la produce, gli estremi
        /// sono scambiati o la calibrazione e' andata male. Si rinuncia alla correzione.
        /// </summary>
        private static void Test_ImplausibleGeometryIsRefused()
        {
            double tooLong = RaceTimeProjection.PitZoneLapFraction(0.10, 0.80, 0.05);
            double outOfRange = RaceTimeProjection.PitZoneLapFraction(1.5, 0.088, 0.05);

            Assert(Math.Abs(tooLong) < 0.0001,
                   $"0.70 di giro di corsia box non e' credibile, ottenuto {tooLong:F3}");
            Assert(Math.Abs(outOfRange) < 0.0001,
                   $"e un estremo fuori da [0,1] nemmeno, ottenuto {outOfRange:F3}");

            Pass("Geometria implausibile: correzione rifiutata");
        }

        /// <summary>
        /// **Il guard che protegge dal difetto opposto.** Se l'equivalente in pista risultasse
        /// maggiore del tempo passato nella zona, la sottrazione azzererebbe il costo della sosta —
        /// e una sosta che non costa niente e' un consiglio pericoloso quanto una che costa il
        /// doppio. In corsia box si va sempre piu' piano che in pista: se il conto dice il
        /// contrario, il dato e' rotto e si rinuncia.
        /// </summary>
        private static void Test_TheCorrectionCanNeverZeroTheCost()
        {
            // Passo assurdo (600 s/giro): l'equivalente in pista supererebbe il tempo in zona.
            double loss = RaceTimeProjection.PitLossSec(TransitSec, AccelDecelSec, StationarySec,
                                                        0.231, 600.0);

            Assert(Math.Abs(loss - 50.85) < 0.01,
                   $"con un passo assurdo si rinuncia alla correzione, ottenuto {loss:F2}");
            Assert(loss > StationarySec,
                   "e il costo resta comunque sopra il solo tempo da fermo");

            Pass("Equivalente in pista maggiore del tempo in zona: correzione rifiutata, costo non azzerato");
        }

        /// <summary>
        /// Solo il tempo da fermo dipende da quanto carburante si imbarca: il transito e le
        /// accelerazioni costano uguale con dieci litri o con quaranta. Serve a fissare che il
        /// tempo da fermo **si somma** e non e' gia' dentro il transito — e' il secondo sospetto
        /// registrato in Y-44, escluso guardando PitRadar, che lo sottrae quando calibra.
        /// </summary>
        private static void Test_OnlyTheStationaryPartScalesWithFuel()
        {
            double fraction = RaceTimeProjection.PitZoneLapFraction(EntryPct, ExitPct, Margin);
            double shortStop = RaceTimeProjection.PitLossSec(TransitSec, AccelDecelSec, 6.0, fraction, PaceSec);
            double longStop = RaceTimeProjection.PitLossSec(TransitSec, AccelDecelSec, 26.0, fraction, PaceSec);

            Assert(Math.Abs((longStop - shortStop) - 20.0) < 0.0001,
                   $"venti secondi di rifornimento in piu' devono costare venti secondi, ottenuto {longStop - shortStop:F2}");

            Pass("Solo il tempo da fermo scala col carburante: +20 s di rifornimento = +20 s di perdita");
        }
    }
}
