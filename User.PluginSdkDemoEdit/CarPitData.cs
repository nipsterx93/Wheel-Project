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

    }

}

