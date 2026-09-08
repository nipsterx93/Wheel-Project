// -------------------------------------------------------------------------

// FILE: CarPitData.cs

// VERSION: V0.11.48

// -------------------------------------------------------------------------

using System.Collections.Generic;



namespace SimRIG

{

    public struct CarPitProfile

    {

        public double Tires4; // Tempo sosta per 4 gomme

        public double Tires2; // Tempo sosta per 2 gomme

        public double Tires1; // Tempo sosta per 1 gomma

        public double RefuelRate; // Litri al secondo (Stimato per la classe)

        public bool IsSequential; // True se Gomme e Benzina NON si possono fare contemporaneamente

    }



    public static class CarPitData

    {

        // DATABASE CENTRALE PROFILI

        private static readonly Dictionary<string, CarPitProfile> _database = new Dictionary<string, CarPitProfile>

        {

            // DEFAULT / FALLBACK

            { "DEFAULT", new CarPitProfile { Tires4 = 26.0, Tires2 = 14.0, Tires1 = 9.0, RefuelRate = 2.7, IsSequential = false } },



            // GT3 (Standard iRacing IMSA/VRS: Simultaneo, vincolato da gomme se > fuel time)

            { "GT3", new CarPitProfile { Tires4 = 26.0, Tires2 = 14.0, Tires1 = 9.0, RefuelRate = 2.8, IsSequential = false } },



            // LMP2 (Dallara P217)

            { "LMP2", new CarPitProfile { Tires4 = 22.0, Tires2 = 12.0, Tires1 = 7.0, RefuelRate = 3.2, IsSequential = false } },

            

            // LMDh / GTP (Hypercars)

            { "GTP", new CarPitProfile { Tires4 = 20.0, Tires2 = 11.0, Tires1 = 6.0, RefuelRate = 3.5, IsSequential = false } },



            // PORSCHE CUP (Generalmente non riforniscono, se lo fanno è spesso Sequenziale o senza cambio gomme)

            { "PCUP", new CarPitProfile { Tires4 = 26.0, Tires2 = 14.0, Tires1 = 9.0, RefuelRate = 2.7, IsSequential = true } },



            // OPEN WHEEL (F3/F4/F1) - Gomme velocissime, Sequenziale se riforniscono

            { "OPENWHEEL", new CarPitProfile { Tires4 = 8.0, Tires2 = 5.0, Tires1 = 3.0, RefuelRate = 2.0, IsSequential = true } },

            

            // NASCAR / OVAL (Sequenziale Lato dx -> Lato sx)

            { "NASCAR", new CarPitProfile { Tires4 = 14.0, Tires2 = 7.0, Tires1 = 4.0, RefuelRate = 1.5, IsSequential = true } }

        };



        public static CarPitProfile GetProfile(string carClassId)

        {

            if (string.IsNullOrEmpty(carClassId)) return _database["DEFAULT"];



            string key = carClassId.ToUpper();



            foreach (var kvp in _database)

            {

                if (key.Contains(kvp.Key)) return kvp.Value;

            }



            return _database["DEFAULT"];

        }



        /// <summary>

        /// Calcola il tempo previsto di sosta da fermo (StationaryTime) in base al profilo della classe vettura,

        /// gestendo sia le soste simultanee (es. GT3/LMP2/GTP: max(fuel, tyres)) sia quelle sequenziali

        /// (es. Porsche Cup / Open Wheel: fuel + tyres) e includendo il buffer di accoppiamento martinetti (default 2.0s).

        /// </summary>

        public static double CalculateStationaryTime(string carClassId, double fuelToAdd, double measuredFuelFillRate, double tireTime, double jackBufferSec = 2.0)

        {

            var profile = GetProfile(carClassId);

            double refuelRate = measuredFuelFillRate > 0.0 ? measuredFuelFillRate : profile.RefuelRate;

            double fuelTime = refuelRate > 0.0 ? (fuelToAdd / refuelRate) : 0.0;

            double stationary = profile.IsSequential ? (fuelTime + tireTime) : System.Math.Max(fuelTime, tireTime);

            if (stationary > 0.0) stationary += jackBufferSec;

            return stationary;
        }

        /// <summary>
        /// Calcola il tempo di percorrenza in pista della zona esterna parallela ai box (ExtendedRacingTime).
        /// Se disponibile, usa la misura reale cronometrata dalla classe (<paramref name="classBestExtendedPitZoneTime"/>);
        /// in alternativa calcola la stima da distanza/velocità o frazione di giro.
        /// </summary>
        public static double CalculateExtendedRacingTime(double classBestExtendedPitZoneTime, double pitDistanceMeters, double trackLengthMeters, double classTopSpeedKmh)
        {
            if (classBestExtendedPitZoneTime > 0.0)
            {
                return classBestExtendedPitZoneTime;
            }

            double racingSpeedMs = classTopSpeedKmh > 0.0 ? (classTopSpeedKmh / 3.6) : (250.0 / 3.6);
            double extendedDistance = pitDistanceMeters + (0.10 * trackLengthMeters);
            return (racingSpeedMs > 0.0 && extendedDistance > 0.0) ? (extendedDistance / racingSpeedMs) : 0.0;
        }

        /// <summary>
        /// Calcola il tempo di percorrenza in pista della zona box usando la frazione geometrica del tracciato e il passo sul giro.
        /// Se disponibile la misura reale cronometrata (<paramref name="classBestExtendedPitZoneTime"/>), ha priorità assoluta.
        /// </summary>
        public static double CalculateExtendedRacingTime(double classBestExtendedPitZoneTime, double pitZoneLapFraction, double lapPace)
        {
            if (classBestExtendedPitZoneTime > 0.0)
            {
                return classBestExtendedPitZoneTime;
            }

            if (pitZoneLapFraction > 0.0 && lapPace > 0.0)
            {
                return pitZoneLapFraction * lapPace;
            }

            return 0.0;
        }

        /// <summary>
        /// Calcola la perdita totale netta ai box (TotalPitLoss):
        /// (StationaryTime + PitTransitTime + AccDecTime) - ExtendedRacingTime.
        /// </summary>
        public static double CalculateTotalPitLoss(double stationaryTime, double pitTransitTime, double accDecTime, double extendedRacingTime)
        {
            double stationary = System.Math.Max(0.0, stationaryTime);
            double timeInZone = System.Math.Max(0.0, pitTransitTime) + System.Math.Max(0.0, accDecTime);
            double extended = System.Math.Max(0.0, extendedRacingTime);

            if (extended >= timeInZone)
            {
                return stationary + timeInZone;
            }

            return System.Math.Max(0.0, stationary + (timeInZone - extended));
        }
    }
}
