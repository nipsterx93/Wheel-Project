// -------------------------------------------------------------------------
// FILE: ExtendedRacingReferenceUnitTests.cs
// Y-61, passo 2 di .ai/plans/2026-09-13-daytona-piano-correzioni.md
//
// La perdita ai box sottrae il tempo che la vettura avrebbe impiegato a percorrere
// in pista la zona estesa dei box. Finora quel tempo era il MINIMO di classe
// (ClassBestExtendedPitZoneTime): a Daytona 21.93 s, contro un transito di corsa
// tipico del Player di ~23.7 s. Il minimo pesca un passaggio eccezionale e gonfia
// di ~1.8 s la perdita ai box di tutte le vetture.
//
// Numeri veri, replay Daytona:
//   transiti di corsa del Player (140133, zona 0.9086 -> 0.1516): 23.76 23.70 23.73
//   23.66 23.92 24.24 23.97 23.74 23.69 23.69 23.76 23.57 23.59 23.53; 28.12 nel
//   giro col fuori pista
//   MergeGapLog 163743:1117  Player (+35.73s): Staz 14.01 | Transit 31.95 | AccDec 11.70 | ExtZone 21.93
//   MergeGapLog 163743:1120  LiveSignedGap -37.18 -> ProjectedMergeGap -1.45 (reale dopo le soste -2.7)
// -------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using SimRIG;

namespace User.PluginSdkDemo.Tests
{
    public class ExtendedRacingReferenceUnitTests
    {
        private static readonly double[] PlayerTransits140133 =
        {
            23.76, 23.70, 23.73, 23.66, 23.92, 24.24, 23.97, 23.74, 23.69, 23.69, 23.76, 23.57, 23.59, 23.53
        };
        private const double OffTrackTransit140133 = 28.12;

        private const double ClassMinimumDaytona = 21.93;
        // Zona estesa 0.9086 -> 0.1516 = 0.243 giri; pavimento fisico indicativo 0.243 x ~105 s x 0.70
        private const double ExtendedZoneWeight = 0.243;
        private const double PhysicalFloorDaytona = 17.89;
        // Lunghezza indicativa: conta solo per la regola dei 40 km di SectorTracker sui tempi "freschi"
        private const double TrackLengthMeters = 5729.0;

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception("[ExtendedRacingReference] " + message);
        }

        private static void Pass(string name)
        {
            Console.WriteLine("  [PASS] " + name);
        }

        public static void RunAllTests()
        {
            Console.WriteLine("[TEST] Running Extended Racing Reference Tests (Y-61, passo 2)...");

            // Come in TargetBopFuelForecastUnitTests: si raccolgono i fallimenti, cosi' una
            // neutralizzazione (ADR-004) mostra in un giro solo tutti i test che diventano rossi.
            var failures = new List<string>();
            RunCollecting(Test_Regression_TypicalTransitIsMedianOfLastSeven_Daytona140133, failures);
            RunCollecting(Test_RecordedTransitsFeedTheMedian, failures);
            RunCollecting(Test_Regression_ExtendedRacingReferenceIsPlayerTypicalTransit, failures);
            RunCollecting(Test_FewerThanThreeTransitsFallBackToClassMinimum, failures);
            if (failures.Count > 0) throw new Exception(string.Join(" || ", failures));

            Console.WriteLine("[TEST SUCCESS] All Extended Racing Reference Tests Passed!");
        }

        private static void RunCollecting(Action test, List<string> failures)
        {
            try { test(); }
            catch (Exception ex) { failures.Add(ex.Message); }
        }

        /// <summary>
        /// Mediana degli ultimi 7 transiti validi, robusta a un passaggio anomalo.
        ///
        /// Neutralizzando la correzione (nessuna mediana) diventa rosso.
        /// </summary>
        private static void Test_Regression_TypicalTransitIsMedianOfLastSeven_Daytona140133()
        {
            var zone = new SectorTracker { Name = "PlayerExtendedPitZone" };
            zone.RawNormalHistory.AddRange(PlayerTransits140133);

            // ultimi 7: 23.74 23.69 23.69 23.76 23.57 23.59 23.53 -> ordinati 23.53 23.57 23.59 23.69 23.69 23.74 23.76
            Assert(Math.Abs(zone.RecentRawTimeMedian() - 23.69) < 1e-6,
                $"Typical transit must be the median of the last 7 valid transits 23.69 s, got {zone.RecentRawTimeMedian():F2}");

            // un giro col fuori pista entra nella finestra: 23.69 23.69 23.76 23.57 23.59 23.53 28.12 -> mediana 23.69
            zone.RawNormalHistory.Add(OffTrackTransit140133);
            Assert(Math.Abs(zone.RecentRawTimeMedian() - 23.69) < 1e-6,
                $"An off-track transit (28.12 s) must not move the median from 23.69 s, got {zone.RecentRawTimeMedian():F2}");

            Pass("Typical extended-zone transit is the median of the last 7 valid transits (Daytona 140133: 23.69 s)");
        }

        /// <summary>
        /// Lo stesso percorso passando dal vero SectorTracker.Update: i transiti di corsa entrano nello
        /// storico, il giro col fuori pista (oltre il 115% del migliore) no, e la mediana e' quella attesa.
        /// </summary>
        private static void Test_RecordedTransitsFeedTheMedian()
        {
            var zone = new SectorTracker { Name = "PlayerExtendedPitZone" };
            double clock = 1000.0;
            for (int i = 0; i < PlayerTransits140133.Length; i++)
            {
                DriveTransit(zone, clock, PlayerTransits140133[i], i + 2);
                clock += 110.0;
            }

            Assert(zone.RawNormalHistory.Count == PlayerTransits140133.Length,
                $"All 14 racing transits must be recorded, got {zone.RawNormalHistory.Count}");
            Assert(Math.Abs(zone.RecentRawTimeMedian() - 23.69) < 1e-6,
                $"Median of the recorded transits must be 23.69 s, got {zone.RecentRawTimeMedian():F2}");

            // 28.12 s supera 1.15 x 23.66 (migliore fresco) = 27.21 s: SectorTracker lo scarta
            DriveTransit(zone, clock, OffTrackTransit140133, 16);
            Assert(zone.RawNormalHistory.Count == PlayerTransits140133.Length,
                $"The off-track transit must not be recorded, history has {zone.RawNormalHistory.Count} entries");
            Assert(Math.Abs(zone.RecentRawTimeMedian() - 23.69) < 1e-6,
                $"Median must stay 23.69 s after the off-track lap, got {zone.RecentRawTimeMedian():F2}");

            Pass("Racing transits recorded by SectorTracker.Update feed the median; the off-track lap is left out");
        }

        /// <summary>
        /// Il riferimento per la perdita ai box e' il transito tipico del Player, non il minimo di classe.
        /// Sul primo blocco del MergeGapLog 163743 dopo la sosta del Target la perdita del Player passa da
        /// 35.73 a 33.97 s e il MergeGap da -1.45 a -3.21 s (reale -2.7).
        ///
        /// Neutralizzando la correzione (minimo di classe) diventa rosso.
        /// </summary>
        private static void Test_Regression_ExtendedRacingReferenceIsPlayerTypicalTransit()
        {
            double reference = OpponentTracker.ResolveExtendedRacingReference(23.69, ClassMinimumDaytona, PhysicalFloorDaytona);
            Assert(Math.Abs(reference - 23.69) < 1e-6,
                $"Extended racing reference must be the Player typical transit 23.69 s, not the class minimum 21.93 s, got {reference:F2}");

            // Mediana sotto il pavimento fisico (dato corrotto): si resta sul minimo di classe
            double belowFloor = OpponentTracker.ResolveExtendedRacingReference(10.40, ClassMinimumDaytona, PhysicalFloorDaytona);
            Assert(Math.Abs(belowFloor - ClassMinimumDaytona) < 1e-6,
                $"A typical transit below the physical floor must fall back to the class minimum 21.93 s, got {belowFloor:F2}");

            // Nessun dato: 0, e CalculateExtendedRacingTime passa alla stima geometrica come oggi
            double none = OpponentTracker.ResolveExtendedRacingReference(0.0, 0.0, PhysicalFloorDaytona);
            Assert(none == 0.0, $"Without any transit the reference must be 0, got {none:F2}");

            // Ricaduta (MergeGapLog 163743:1117-1120): 14.01 + 31.95 + 11.70 - 21.93 = 35.73 s
            double lossClassMinimum = CarPitData.CalculateTotalPitLoss(14.01, 31.95, 11.70, ClassMinimumDaytona);
            Assert(Math.Abs(lossClassMinimum - 35.73) < 0.01, $"Precondition: logged Player pit loss 35.73 s, got {lossClassMinimum:F2}");

            // 14.01 + 31.95 + 11.70 - 23.69 = 33.97 s; MergeGap -37.18 + 33.97 = -3.21 s
            double loss = CarPitData.CalculateTotalPitLoss(14.01, 31.95, 11.70, reference);
            Assert(Math.Abs(loss - 33.97) < 0.01, $"Expected Player pit loss 33.97 s, got {loss:F2}");
            double mergeGap = TargetStrategyManager.CalculateProjectedMergeGap(-37.18, true, loss, false, 0.0);
            Assert(Math.Abs(mergeGap - (-3.21)) < 0.01, $"Expected MergeGap -3.21 s after the Target stop (log -1.45, real -2.7), got {mergeGap:F2}");

            Pass("Extended racing reference is the Player typical transit (Daytona: 23.69 s instead of 21.93 s)");
        }

        /// <summary>
        /// Finche' il Player non ha 3 transiti validi non c'e' mediana e resta il minimo di classe di oggi.
        /// </summary>
        private static void Test_FewerThanThreeTransitsFallBackToClassMinimum()
        {
            var zone = new SectorTracker { Name = "PlayerExtendedPitZone" };
            zone.RawNormalHistory.AddRange(new[] { 23.76, 23.70 });

            Assert(zone.RecentRawTimeMedian() == 0.0,
                $"With 2 transits there is no typical transit yet (0), got {zone.RecentRawTimeMedian():F2}");
            double reference = OpponentTracker.ResolveExtendedRacingReference(zone.RecentRawTimeMedian(), ClassMinimumDaytona, PhysicalFloorDaytona);
            Assert(Math.Abs(reference - ClassMinimumDaytona) < 1e-6,
                $"With 2 transits the reference must stay the class minimum 21.93 s, got {reference:F2}");

            // terzo transito: mediana di 23.76 23.70 23.73 = 23.73
            zone.RawNormalHistory.Add(23.73);
            Assert(Math.Abs(zone.RecentRawTimeMedian() - 23.73) < 1e-6,
                $"With 3 transits the median must be 23.73 s, got {zone.RecentRawTimeMedian():F2}");

            Pass("Fewer than 3 valid transits fall back to the class minimum");
        }

        /// <summary>Un passaggio completo nella zona estesa: fuori, ingresso a 0.9086, uscita a 0.1516.</summary>
        private static void DriveTransit(SectorTracker zone, double entryTime, double duration, int lap)
        {
            zone.Update(0.85, entryTime - 5.0, false, false, ExtendedZoneWeight, 0.0, 0.0, 0.0, lap, TrackLengthMeters, false, null);
            zone.Update(0.9086, entryTime, false, true, ExtendedZoneWeight, 0.0, 0.0, 0.0, lap, TrackLengthMeters, false, null);
            zone.Update(0.1516, entryTime + duration, false, false, ExtendedZoneWeight, 0.0, 0.0, 0.0, lap, TrackLengthMeters, false, null);
        }
    }
}
