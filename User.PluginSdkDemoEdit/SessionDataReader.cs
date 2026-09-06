// -------------------------------------------------------------------------
// FILE: SessionDataReader.cs
// Estrae metadati di sessione dall'oggetto nativo iRacing (SessionData/DataSample)
// o dalle proprieta' pubblicate da SimHub sotto DataCorePlugin.GameRawData.SessionData.*
// -------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace SimRIG
{
    public static class SessionDataReader
    {
        public const string SourceLabelObject = "iRacing SessionData Object";
        public const string SourceLabelProperties = "SimHub Properties SessionData";

        /// <summary>
        /// Popola SessionMetadata a partire dall'oggetto raw restituito da SimHub
        /// (GameData.NewData.GetRawDataObject() o property DataCorePlugin.GameRawData).
        /// </summary>
        public static SessionMetadata ReadFromRawObject(object rawObject)
        {
            var meta = new SessionMetadata();
            if (rawObject == null) return meta;

            try
            {
                // rawObject puo' essere DataSample (con proprieta' SessionData) o SessionData direttamente
                object sessionData = GetPropValue(rawObject, "SessionData")
                                  ?? GetPropValue(rawObject, "sessionData")
                                  ?? rawObject;

                if (sessionData == null) return meta;

                object driverInfo = GetPropValue(sessionData, "DriverInfo");
                object weekendInfo = GetPropValue(sessionData, "WeekendInfo");

                int playerCarIdx = -1;
                var paceByCarIdx = new Dictionary<int, double>();

                // 1. WeekendInfo
                if (weekendInfo != null)
                {
                    string trackName = GetStringProp(weekendInfo, "TrackDisplayName");
                    if (!string.IsNullOrEmpty(trackName)) meta.TrackName = trackName;

                    string pitSpeed = GetStringProp(weekendInfo, "TrackPitSpeedLimit");
                    if (!string.IsNullOrEmpty(pitSpeed))
                    {
                        meta.PitSpeedLimitKmh = SpeedKmh(pitSpeed);
                    }

                    object weekendOptions = GetPropValue(weekendInfo, "WeekendOptions");
                    if (weekendOptions != null)
                    {
                        long? standing = GetLongProp(weekendOptions, "StandingStart");
                        if (standing.HasValue) meta.IsStandingStart = standing.Value != 0;

                        long? incLimit = GetLongProp(weekendOptions, "IncidentLimit");
                        if (incLimit.HasValue) meta.IncidentLimit = (int)incLimit.Value;

                        long? fastRep = GetLongProp(weekendOptions, "FastRepairsLimit");
                        if (fastRep.HasValue) meta.FastRepairsAvailable = (int)fastRep.Value;
                    }
                }

                // 2. DriverInfo
                if (driverInfo != null)
                {
                    long? pCarIdx = GetLongProp(driverInfo, "DriverCarIdx");
                    if (pCarIdx.HasValue) playerCarIdx = (int)pCarIdx.Value;

                    double? estPace = GetDoubleProp(driverInfo, "DriverCarEstLapTime");
                    if (estPace.HasValue && estPace.Value >= 10.0) meta.PlayerEstimatedPaceSec = estPace.Value;

                    double? pitStall = GetDoubleProp(driverInfo, "DriverPitTrkPct");
                    if (pitStall.HasValue && pitStall.Value >= 0.0 && pitStall.Value <= 1.0)
                        meta.PlayerPitStallPct = pitStall.Value;

                    double? fuelDensity = GetDoubleProp(driverInfo, "DriverCarFuelKgPerLtr");
                    if (fuelDensity.HasValue && fuelDensity.Value > 0.0)
                        meta.FuelDensityKgPerLitre = fuelDensity.Value;

                    double? maxFuel = GetDoubleProp(driverInfo, "DriverCarFuelMaxLtr");
                    if (maxFuel.HasValue && maxFuel.Value > 0.0)
                        meta.PlayerMaxFuelLitres = maxFuel.Value;

                    // Drivers list
                    object driversObj = GetPropValue(driverInfo, "Drivers");
                    if (driversObj is IEnumerable driversList)
                    {
                        foreach (var d in driversList)
                        {
                            if (d == null) continue;
                            long? carIdx = GetLongProp(d, "CarIdx");
                            string userName = GetStringProp(d, "UserName");
                            string carClass = GetStringProp(d, "CarClassShortName");
                            double? classEstLap = GetDoubleProp(d, "CarClassEstLapTime");
                            object bopObj = GetPropValue(d, "CarClassMaxFuelPct");
                            double? bopPct = ParsePercentage(bopObj);
                            long? tireSets = GetLongProp(d, "CarClassDryTireSetLimit");

                            int cIdx = carIdx.HasValue ? (int)carIdx.Value : -1;

                            if (!string.IsNullOrEmpty(userName))
                            {
                                if (classEstLap.HasValue && classEstLap.Value >= 10.0)
                                    meta.DriverEstimatedPaceSec[userName] = classEstLap.Value;
                                if (bopPct.HasValue && bopPct.Value > 0.0)
                                    meta.DriverMaxFuelPct[userName] = bopPct.Value;
                            }

                            if (!string.IsNullOrEmpty(carClass) && classEstLap.HasValue && classEstLap.Value >= 10.0)
                            {
                                meta.ClassEstimatedPaceSec[carClass] = classEstLap.Value;
                            }

                            if (cIdx >= 0 && classEstLap.HasValue && classEstLap.Value >= 10.0)
                            {
                                paceByCarIdx[cIdx] = classEstLap.Value;
                            }

                            if (tireSets.HasValue && !meta.DryTireSetLimit.HasValue)
                            {
                                meta.DryTireSetLimit = (int)tireSets.Value;
                            }
                        }
                    }
                }

                // Risolvi il player pace per CarIdx se mancante
                if (!meta.PlayerEstimatedPaceSec.HasValue && playerCarIdx >= 0 && paceByCarIdx.TryGetValue(playerCarIdx, out double pPace))
                {
                    meta.PlayerEstimatedPaceSec = pPace;
                }

                if (meta.DriverEstimatedPaceSec.Count > 0 || meta.DriverMaxFuelPct.Count > 0 ||
                    meta.PlayerEstimatedPaceSec.HasValue || meta.PitSpeedLimitKmh.HasValue ||
                    meta.IncidentLimit.HasValue || meta.PlayerPitStallPct.HasValue)
                {
                    meta.SourceName = SourceLabelObject;
                }
            }
            catch
            {
                // Nessuna eccezione propagata verso l'esterno
            }

            return meta;
        }

        /// <summary>
        /// Popola SessionMetadata leggendo le proprieta' pubblicate da SimHub in PluginManager.
        /// </summary>
        public static SessionMetadata ReadFromPluginManager(SimHub.Plugins.PluginManager pm)
        {
            var meta = new SessionMetadata();
            if (pm == null) return meta;

            try
            {
                // Prima prova: l'oggetto SessionData intero esposto come proprieta'
                object sessionData = pm.GetPropertyValue("DataCorePlugin.GameRawData.SessionData");
                if (sessionData != null)
                {
                    meta = ReadFromRawObject(sessionData);
                    if (meta.IsPopulated) return meta;
                }

                // Seconda prova: lettura proprieta' individuali SimHub
                int playerCarIdx = -1;
                var paceByCarIdx = new Dictionary<int, double>();

                var trackName = pm.GetPropertyValue("DataCorePlugin.GameRawData.SessionData.WeekendInfo.TrackDisplayName") as string;
                if (!string.IsNullOrEmpty(trackName)) meta.TrackName = trackName;

                var pitSpeedStr = pm.GetPropertyValue("DataCorePlugin.GameRawData.SessionData.WeekendInfo.TrackPitSpeedLimit")?.ToString();
                if (!string.IsNullOrEmpty(pitSpeedStr)) meta.PitSpeedLimitKmh = SpeedKmh(pitSpeedStr);

                var standingObj = pm.GetPropertyValue("DataCorePlugin.GameRawData.SessionData.WeekendInfo.WeekendOptions.StandingStart");
                if (standingObj != null && long.TryParse(standingObj.ToString(), out long standing))
                    meta.IsStandingStart = standing != 0;

                var incLimitObj = pm.GetPropertyValue("DataCorePlugin.GameRawData.SessionData.WeekendInfo.WeekendOptions.IncidentLimit");
                if (incLimitObj != null && int.TryParse(incLimitObj.ToString(), out int incLimit))
                    meta.IncidentLimit = incLimit;

                var fastRepObj = pm.GetPropertyValue("DataCorePlugin.GameRawData.SessionData.WeekendInfo.WeekendOptions.FastRepairsLimit");
                if (fastRepObj != null && int.TryParse(fastRepObj.ToString(), out int fastRep))
                    meta.FastRepairsAvailable = fastRep;

                var pCarIdxObj = pm.GetPropertyValue("DataCorePlugin.GameRawData.SessionData.DriverInfo.DriverCarIdx");
                if (pCarIdxObj != null && int.TryParse(pCarIdxObj.ToString(), out int pIdx))
                    playerCarIdx = pIdx;

                var estPaceObj = pm.GetPropertyValue("DataCorePlugin.GameRawData.SessionData.DriverInfo.DriverCarEstLapTime");
                if (estPaceObj != null && double.TryParse(estPaceObj.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double pPace) && pPace >= 10.0)
                    meta.PlayerEstimatedPaceSec = pPace;

                var pitStallObj = pm.GetPropertyValue("DataCorePlugin.GameRawData.SessionData.DriverInfo.DriverPitTrkPct");
                if (pitStallObj != null && double.TryParse(pitStallObj.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double stall) && stall >= 0.0 && stall <= 1.0)
                    meta.PlayerPitStallPct = stall;

                var densityObj = pm.GetPropertyValue("DataCorePlugin.GameRawData.SessionData.DriverInfo.DriverCarFuelKgPerLtr");
                if (densityObj != null && double.TryParse(densityObj.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double dens) && dens > 0.0)
                    meta.FuelDensityKgPerLitre = dens;

                var maxFuelObj = pm.GetPropertyValue("DataCorePlugin.GameRawData.SessionData.DriverInfo.DriverCarFuelMaxLtr");
                if (maxFuelObj != null && double.TryParse(maxFuelObj.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double mFuel) && mFuel > 0.0)
                    meta.PlayerMaxFuelLitres = mFuel;

                // Loop Drivers00 .. Drivers63
                for (int i = 0; i < 64; i++)
                {
                    string prefix = $"DataCorePlugin.GameRawData.SessionData.DriverInfo.Drivers{i:D2}.";
                    var nameObj = pm.GetPropertyValue(prefix + "UserName");
                    if (nameObj == null) continue;
                    string name = nameObj.ToString();
                    if (string.IsNullOrEmpty(name)) continue;

                    var carClass = pm.GetPropertyValue(prefix + "CarClassShortName")?.ToString();
                    var cIdxObj = pm.GetPropertyValue(prefix + "CarIdx");
                    int cIdx = (cIdxObj != null && int.TryParse(cIdxObj.ToString(), out int ci)) ? ci : -1;

                    var estLapObj = pm.GetPropertyValue(prefix + "CarClassEstLapTime");
                    if (estLapObj != null && double.TryParse(estLapObj.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double cp) && cp >= 10.0)
                    {
                        meta.DriverEstimatedPaceSec[name] = cp;
                        if (!string.IsNullOrEmpty(carClass)) meta.ClassEstimatedPaceSec[carClass] = cp;
                        if (cIdx >= 0) paceByCarIdx[cIdx] = cp;
                    }

                    var bopObj = pm.GetPropertyValue(prefix + "CarClassMaxFuelPct");
                    double? bopPct = ParsePercentage(bopObj);
                    if (bopPct.HasValue && bopPct.Value > 0.0)
                    {
                        meta.DriverMaxFuelPct[name] = bopPct.Value;
                    }

                    var tiresObj = pm.GetPropertyValue(prefix + "CarClassDryTireSetLimit");
                    if (tiresObj != null && int.TryParse(tiresObj.ToString(), out int tLimit) && !meta.DryTireSetLimit.HasValue)
                    {
                        meta.DryTireSetLimit = tLimit;
                    }
                }

                if (!meta.PlayerEstimatedPaceSec.HasValue && playerCarIdx >= 0 && paceByCarIdx.TryGetValue(playerCarIdx, out double resolvedPlayerPace))
                {
                    meta.PlayerEstimatedPaceSec = resolvedPlayerPace;
                }

                if (meta.DriverEstimatedPaceSec.Count > 0 || meta.DriverMaxFuelPct.Count > 0 ||
                    meta.PlayerEstimatedPaceSec.HasValue || meta.PitSpeedLimitKmh.HasValue ||
                    meta.IncidentLimit.HasValue || meta.PlayerPitStallPct.HasValue)
                {
                    meta.SourceName = SourceLabelProperties;
                }
            }
            catch
            {
            }

            return meta;
        }

        public static double? ParsePercentage(object val)
        {
            if (val == null) return null;
            if (val is double d)
            {
                return d > 1.05 ? d / 100.0 : d;
            }
            if (val is float f)
            {
                return f > 1.05f ? f / 100.0 : f;
            }

            string s = val.ToString().Trim();
            if (string.IsNullOrEmpty(s)) return null;

            s = s.Replace('%', ' ').Trim();
            if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed))
            {
                // iRacing manda "1.000 %" per 100% e "0.500 %" per 50%.
                // Se un valore e' > 1.05 significa che era espresso in scala 0-100 (es. "50 %").
                return parsed > 1.05 ? parsed / 100.0 : parsed;
            }
            return null;
        }

        public static double? SpeedKmh(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;

            int end = 0;
            string vTrim = value.Trim();
            while (end < vTrim.Length &&
                   (char.IsDigit(vTrim[end]) || vTrim[end] == '.' ||
                    vTrim[end] == '-' || vTrim[end] == '+'))
            {
                end++;
            }
            if (end == 0) return null;

            if (double.TryParse(vTrim.Substring(0, end), NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed))
            {
                if (parsed <= 0.0) return null;
                if (vTrim.IndexOf("mph", StringComparison.OrdinalIgnoreCase) >= 0)
                    return parsed * 1.609344;
                return parsed;
            }
            return null;
        }

        private static object GetPropValue(object obj, string propName)
        {
            if (obj == null) return null;
            var prop = obj.GetType().GetProperty(propName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop != null) return prop.GetValue(obj, null);
            var field = obj.GetType().GetField(propName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (field != null) return field.GetValue(obj);
            return null;
        }

        private static string GetStringProp(object obj, string propName)
        {
            var val = GetPropValue(obj, propName);
            return val?.ToString();
        }

        private static double? GetDoubleProp(object obj, string propName)
        {
            var val = GetPropValue(obj, propName);
            if (val == null) return null;
            if (val is double d) return d;
            if (val is float f) return f;
            if (double.TryParse(val.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed))
                return parsed;
            return null;
        }

        private static long? GetLongProp(object obj, string propName)
        {
            var val = GetPropValue(obj, propName);
            if (val == null) return null;
            if (val is long l) return l;
            if (val is int i) return i;
            if (long.TryParse(val.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out long parsed))
                return parsed;
            return null;
        }
    }
}
