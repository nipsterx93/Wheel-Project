using System;
using System.Collections.Generic;
using System.Linq;

namespace SimRIG
{
    public class SectorTracker
    {
        public string Name { get; set; }
        public bool IsInside { get; set; } = false;
        public double EntryTime { get; set; } = 0.0;
        public double EntryPos { get; set; } = -1.0;
        public double LastNormalTime { get; set; } = 0.0;
        public double LastNormalizedTime { get; set; } = 0.0;
        public double LastOutlapTime { get; set; } = 0.0;
        public double LastInlapTime { get; set; } = 0.0;
        public double LastTransitTime { get; set; } = 0.0;
        public string CurrentState { get; set; } = "NORMAL";
        public bool PendingOutlap { get; set; } = false;

        public double BestFreshNormalTime { get; set; } = 0.0;
        public List<double> WornNormalHistory { get; set; } = new List<double>();
        public List<double> RawNormalHistory { get; set; } = new List<double>();
        public double WornNormalTime { get; set; } = 0.0;

        public double BestRawTime { get; set; } = 0.0;
        public int BestRawTimeLapCount { get; set; } = 0;

        public List<double> ColdTyrePenalties { get; set; } = new List<double>();
        public double ColdTyrePenalty { get; set; } = 0.0;

        public bool EntryIsValid { get; set; } = false;
        private bool _wasOutside = false;

        public void Reset()
        {
            IsInside = false;
            EntryTime = 0.0;
            EntryPos = -1.0;
            LastNormalTime = 0.0;
            LastNormalizedTime = 0.0;
            LastOutlapTime = 0.0;
            LastInlapTime = 0.0;
            LastTransitTime = 0.0;
            CurrentState = "NORMAL";
            PendingOutlap = false;
            BestFreshNormalTime = 0.0;
            WornNormalHistory.Clear();
            RawNormalHistory.Clear();
            WornNormalTime = 0.0;
            ColdTyrePenalties.Clear();
            ColdTyrePenalty = 0.0;
            BestRawTime = 0.0;
            BestRawTimeLapCount = 0;
            EntryIsValid = false;
            _wasOutside = false;
        }

        public bool Update(
            double trackPos,
            double now,
            bool isCarInPit,
            bool isInsideSector,
            double sectorWeight,
            double currentFuel,
            double trackTemp,
            double baselineTemp,
            int lapsOnTyres,
            double trackLength,
            bool tiresChanged,
            LogManager log,
            double fuelWeightCoef = 0.03,
            double tempCoef = 0.05)
        {
            if (isCarInPit)
            {
                if (!PendingOutlap) PendingOutlap = true;

                if (LastNormalTime > 0)
                {
                    LastInlapTime = LastNormalTime;
                    CurrentState = "INLAP (RETROACTIVE)";
                }
            }

            if (!isInsideSector)
            {
                _wasOutside = true;
            }

            if (isInsideSector && !IsInside)
            {
                IsInside = true;
                EntryTime = now;
                EntryPos = trackPos;
                EntryIsValid = _wasOutside;
            }
            else if (!isInsideSector && IsInside)
            {
                IsInside = false;
                if (EntryIsValid)
                {
                    double exitPos = trackPos;
                    double dist = exitPos - EntryPos;
                    if (dist < 0.0) dist += 1.0;

                    // Verifichiamo se il transito è completo (almeno il 50% della lunghezza del settore)
                    bool isCompleteTransit = sectorWeight <= 0.0 || dist >= (sectorWeight * 0.50);

                    double sectorTime = Math.Abs(now - EntryTime);

                    if (isCompleteTransit && sectorTime > 5.0 && sectorTime < 300.0)
                    {
                        LastTransitTime = sectorTime;
                        double fuelPenalty = (currentFuel * fuelWeightCoef) * sectorWeight;
                        double tempPen = baselineTemp > 0 ? ((trackTemp - baselineTemp) * tempCoef) * sectorWeight : 0.0;
                        double normalizedSectorTime = sectorTime - fuelPenalty - tempPen;

                        if (PendingOutlap)
                        {
                            LastOutlapTime = sectorTime;
                            CurrentState = "OUTLAP";

                            double referenceTime = tiresChanged && BestFreshNormalTime > 0 ? BestFreshNormalTime : WornNormalTime;
                            if (referenceTime > 0)
                            {
                                double localDelta = normalizedSectorTime - referenceTime;

                                if (localDelta > 0.5 && localDelta < 20.0)
                                {
                                    ColdTyrePenalties.Add(localDelta);
                                    if (ColdTyrePenalties.Count > 10) ColdTyrePenalties.RemoveAt(0);
                                    ColdTyrePenalty = ColdTyrePenalties.Average();
                                }
                            }
                        }
                        else
                        {
                            bool isValid = true;
                            if (BestFreshNormalTime > 0 && normalizedSectorTime > BestFreshNormalTime * 1.15)
                            {
                                isValid = false;
                            }

                            if (isValid)
                            {
                                LastNormalTime = sectorTime;
                                CurrentState = "NORMAL";

                                double distanceDriven = lapsOnTyres * trackLength;
                                if (lapsOnTyres >= 1 && distanceDriven <= 40000.0)
                                {
                                    if (BestFreshNormalTime == 0.0 || normalizedSectorTime < BestFreshNormalTime)
                                    {
                                        BestFreshNormalTime = normalizedSectorTime;
                                    }

                                    if (BestRawTime == 0.0 || sectorTime < BestRawTime)
                                    {
                                        BestRawTime = sectorTime;
                                        BestRawTimeLapCount = lapsOnTyres;
                                    }
                                }

                                WornNormalHistory.Add(normalizedSectorTime);
                                WornNormalTime = WornNormalHistory.Average();

                                RawNormalHistory.Add(sectorTime);
                                LastNormalizedTime = normalizedSectorTime;
                                PendingOutlap = false;
                                return true;
                            }
                        }
                        PendingOutlap = false;
                    }
                }
            }
            return false;
        }

        public double GetCurrentTime(double now)
        {
            if (IsInside && EntryTime > 0.0)
            {
                return Math.Abs(now - EntryTime);
            }
            return LastNormalTime;
        }
    }
}
