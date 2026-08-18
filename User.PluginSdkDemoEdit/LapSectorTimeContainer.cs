using System;
using System.Collections.Generic;

namespace SimRIG
{
    public class LapSectorTimeContainer
    {
        // Ultimi tempi registrati
        public double LastLapTime { get; set; } = 0.0;
        public double LastSectorTime { get; set; } = 0.0;

        // Storico completo della sessione (non limitato)
        public List<double> LapHistory { get; } = new List<double>();
        public List<double> SectorHistory { get; } = new List<double>();

        // Record e statistiche
        public double BestLapTime { get; set; } = 0.0;
        public int BestLapLapCount { get; set; } = 0;

        public double BestSectorTime { get; set; } = 0.0;
        public int BestSectorLapCount { get; set; } = 0;

        // Baselines (con gomma nuova)
        public double LapBaseline { get; set; } = 0.0;
        public double SectorBaseline { get; set; } = 0.0;

        // Medie mobili (ultimi N passaggi)
        public double LapMovingAverage { get; set; } = 0.0;
        public double SectorMovingAverage { get; set; } = 0.0;

        // Calo prestazionale (degrado)
        public double LapPaceDrop { get; set; } = 0.0;
        public double SectorPaceDrop { get; set; } = 0.0;

        public void Reset()
        {
            LastLapTime = 0.0;
            LastSectorTime = 0.0;
            LapHistory.Clear();
            SectorHistory.Clear();
            BestLapTime = 0.0;
            BestLapLapCount = 0;
            BestSectorTime = 0.0;
            BestSectorLapCount = 0;
            LapBaseline = 0.0;
            SectorBaseline = 0.0;
            LapMovingAverage = 0.0;
            SectorMovingAverage = 0.0;
            LapPaceDrop = 0.0;
            SectorPaceDrop = 0.0;
        }
    }
}
