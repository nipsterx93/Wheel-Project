// -------------------------------------------------------------------------
// FILE: TelemetryReader.cs
// VERSION: Fix errori 17
// -------------------------------------------------------------------------
using System;
using System.Linq;
using GameReaderCommon;
using SimHub.Plugins;

namespace SimRIG
{
    public class TelemetryReader
    {
        private PluginManager _pluginManager;
        private double _lastPressureSampleTime = 0.0;
        private System.Collections.Generic.List<Tuple<double, double>> _pressureHistory = new System.Collections.Generic.List<Tuple<double, double>>(); // list of Tuple<sessionTime, airPressure>

        public TelemetryReader(PluginManager pluginManager)
        {
            _pluginManager = pluginManager;
        }

        public void UpdateState(GameData data, SessionState state, double alertThreshold)
        {
            state.IsGameRunning = data.GameRunning || data.GameReplay;
            state.GameName = data.GameName ?? "";

            if (!(data.GameRunning || data.GameReplay) || data.NewData == null) return;

            int sessionStateStatus = 4;
            var rawState = _pluginManager.GetPropertyValue("DataCorePlugin.GameRawData.Telemetry.SessionState");
            if (rawState != null) sessionStateStatus = Convert.ToInt32(rawState);
            state.SessionStateStatus = sessionStateStatus;

            bool isLapLimited = false;
            var propIsLapLimit = _pluginManager.GetPropertyValue("DataCorePlugin.GameRawData.CurrentSessionInfo.IsLimitedSessionLaps");
            if (propIsLapLimit != null) bool.TryParse(propIsLapLimit.ToString(), out isLapLimited);
            state.IsLapLimited = isLapLimited;

            bool isTimeLimited = false;
            var propIsTimeLimit = _pluginManager.GetPropertyValue("DataCorePlugin.GameRawData.CurrentSessionInfo.IsLimitedTime");
            if (propIsTimeLimit != null) bool.TryParse(propIsTimeLimit.ToString(), out isTimeLimited);
            state.IsTimeLimited = isTimeLimited;

            int rawSessionLaps = 0;
            var propSessLaps = _pluginManager.GetPropertyValue("DataCorePlugin.GameRawData.CurrentSessionInfo._SessionLaps");
            if (propSessLaps != null) int.TryParse(propSessLaps.ToString(), out rawSessionLaps);
            state.TotalLaps = rawSessionLaps;

            double rawSessionTime = 0.0;
            var propSessTime = _pluginManager.GetPropertyValue("DataCorePlugin.GameRawData.CurrentSessionInfo._SessionTime");
            if (propSessTime != null) double.TryParse(propSessTime.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out rawSessionTime);
            state.RawSessionTime = rawSessionTime;

            bool globalCheckered = false;
            var rawCheckered = _pluginManager.GetPropertyValue("DataCorePlugin.GameRawData.Telemetry.SessionFlagsDetails.Ischeckered");
            if (rawCheckered != null) bool.TryParse(rawCheckered.ToString(), out globalCheckered);
            state.GlobalCheckeredFlag = globalCheckered;

            int isInPitBox = 0;
            var pitBoxProp = _pluginManager.GetPropertyValue("DataCorePlugin.GameData.IsInPit");
            if (pitBoxProp != null) isInPitBox = Convert.ToInt32(pitBoxProp);
            state.IsInPitBox = (isInPitBox == 1);

            state.IsRaceSession = data.NewData.SessionTypeName != null && data.NewData.SessionTypeName.IndexOf("Race", StringComparison.OrdinalIgnoreCase) >= 0;
            state.IsQualySession = data.NewData.SessionTypeName != null && (data.NewData.SessionTypeName.IndexOf("Qualify", StringComparison.OrdinalIgnoreCase) >= 0 || data.NewData.SessionTypeName.IndexOf("Qualifying", StringComparison.OrdinalIgnoreCase) >= 0);

            state.SessionTimeLeftSec = data.NewData.SessionTimeLeft.TotalSeconds;
            state.PlayerCheckeredFlag = data.NewData.Flag_Checkered > 0;

            state.TrackId = data.NewData.TrackId ?? "DEFAULT";
            state.TrackLengthMeters = data.NewData.TrackLength > 0 ? data.NewData.TrackLength : 5000.0;
            state.CarClassId = data.NewData.CarClass ?? "DEFAULT";
            string telemetryCar = data.NewData.CarModel ?? "DEFAULT";
            if (telemetryCar != "DEFAULT" && telemetryCar != state.CarModel)
            {
                state.CarModel = telemetryCar;
                state.DeducedStartingFuelLimit = 0.0;
            }

            state.SpeedKmh = data.NewData.SpeedKmh;

            state.Rpm = (int)Math.Round(data.NewData.Rpms);
            state.MaxRpm = data.NewData.CarSettings_MaxRPM > 0 ? (int)Math.Round(data.NewData.CarSettings_MaxRPM) : 10000;

            state.CurrentFuelLevel = data.NewData.Fuel;
            state.MaxFuelCapacity = data.NewData.MaxFuel > 0 ? data.NewData.MaxFuel : 100.0;
            string yaml = _pluginManager.GetPropertyValue("DataCorePlugin.GameRawData.SessionInfo") as string;
            if (string.IsNullOrEmpty(yaml)) yaml = _pluginManager.GetPropertyValue("DataCorePlugin.GameRawData.SessionInfoYAML") as string;
            if (string.IsNullOrEmpty(yaml)) yaml = _pluginManager.GetPropertyValue("DataCorePlugin.GameRawData.SessionInfo.YAML") as string;
            if (string.IsNullOrEmpty(yaml)) yaml = _pluginManager.GetPropertyValue("DataCorePlugin.GameRawData.SessionInfo_YAML") as string;
            state.RawSessionInfoYaml = yaml ?? "";
            var rawPlayerCarIdx = _pluginManager.GetPropertyValue("DataCorePlugin.GameRawData.Telemetry.PlayerCarIdx");
            if (rawPlayerCarIdx != null) state.PlayerCarIdx = Convert.ToInt32(rawPlayerCarIdx);

            state.CurrentLap = data.NewData.CurrentLap > 0 ? data.NewData.CurrentLap : 0;
            state.TrackPositionPercent = data.NewData.TrackPositionPercent;
            state.CurrentLapTimeSec = data.NewData.CurrentLapTime.TotalSeconds;
            state.LastLapTimeSec = data.NewData.LastLapTime.TotalSeconds;
            state.BestLapTimeSec = data.NewData.BestLapTime.TotalSeconds;

            state.Position = data.NewData.Position;
            state.IsInPitLane = (data.NewData.IsInPitLane == 1);

            state.Flag_Yellow = data.NewData.Flag_Yellow;
            state.Flag_Blue = data.NewData.Flag_Blue;
            state.Flag_Green = data.NewData.Flag_Green;
            state.Flag_Black = data.NewData.Flag_Black;

            state.TrackTemperature = data.NewData.RoadTemperature;
            state.AverageTireTemp = CalculateAverageTireTemp(data);

            // Wet track detection logic (iRacing & ACC raw properties)
            int wetLevel = 0;
            if (state.GameName == "iRacing")
            {
                var irTrackWetness = _pluginManager.GetPropertyValue("DataCorePlugin.GameRawData.Telemetry.TrackWetness");
                if (irTrackWetness != null)
                {
                    try
                    {
                        double wetVal = Convert.ToDouble(irTrackWetness);
                        if (wetVal == 1.0) wetLevel = 1;
                        else if (wetVal == 2.0) wetLevel = 2;
                        else if (wetVal >= 3.0) wetLevel = 3;
                    }
                    catch { }
                }

                var irPrecipitation = _pluginManager.GetPropertyValue("DataCorePlugin.GameRawData.Telemetry.Precipitation");
                if (irPrecipitation != null)
                {
                    try
                    {
                        double precipVal = Convert.ToDouble(irPrecipitation);
                        if (precipVal > 0.0 && wetLevel == 0) wetLevel = 1;
                    }
                    catch { }
                }
            }
            else if (state.GameName == "AssettoCorsaCompetizione")
            {
                var accGripStatus = _pluginManager.GetPropertyValue("DataCorePlugin.GameRawData.Graphics.trackGripStatus");
                if (accGripStatus != null)
                {
                    try
                    {
                        double gripVal = Convert.ToDouble(accGripStatus);
                        if (gripVal == 4.0) wetLevel = 1;
                        else if (gripVal == 5.0) wetLevel = 2;
                        else if (gripVal >= 6.0) wetLevel = 3;
                    }
                    catch { }
                }

                var accRainIntensity = _pluginManager.GetPropertyValue("DataCorePlugin.GameRawData.Graphics.rainIntensity");
                if (accRainIntensity != null)
                {
                    try
                    {
                        double rainVal = Convert.ToDouble(accRainIntensity);
                        if (rainVal > 0.0 && wetLevel == 0) wetLevel = 1;
                    }
                    catch { }
                }
            }
            state.TrackWetnessLevel = wetLevel;
            state.IsTrackWet = (wetLevel >= 1);

            // Forecast Engine (Fase 3.2) - Dynamic Feature Detection (Immune to GameName empty/replay issues)
            double humidity = 0.50;
            double pressure = 1013.25;
            double rain10 = 0.0;
            double rain30 = 0.0;
            double windSpeed = 0.0;
            double airTemp = 20.0;

            // 1. Humidity (fractional 0.0 to 1.0 or percentage 0 to 100)
            var irHumid = _pluginManager.GetPropertyValue("DataCorePlugin.GameRawData.Telemetry.RelativeHumidity");
            var weekendHumid = _pluginManager.GetPropertyValue("DataCorePlugin.GameRawData.SessionData.WeekendInfo.TrackRelativeHumidity");
            if (irHumid != null)
            {
                try { humidity = Convert.ToDouble(irHumid); } catch { }
            }
            else if (weekendHumid != null)
            {
                try
                {
                    string rawH = weekendHumid.ToString().Replace("%", "").Trim();
                    humidity = Convert.ToDouble(rawH) / 100.0;
                }
                catch { }
            }

            // 2. Air Pressure (Pascal or hPa or Inches of Mercury)
            var irPress = _pluginManager.GetPropertyValue("DataCorePlugin.GameRawData.Telemetry.AirPressure");
            var weekendPress = _pluginManager.GetPropertyValue("DataCorePlugin.GameRawData.SessionData.WeekendInfo.TrackAirPressure");
            if (irPress != null)
            {
                try
                {
                    double pRaw = Convert.ToDouble(irPress);
                    if (pRaw > 50000.0) pressure = pRaw / 100.0; // convert Pa to hPa
                    else pressure = pRaw;
                }
                catch { }
            }
            else if (weekendPress != null)
            {
                try
                {
                    // convert inches of Mercury to hPa: 1 inHg = 33.8639 hPa
                    string rawP = weekendPress.ToString().Replace("Hg", "").Trim();
                    pressure = Convert.ToDouble(rawP) * 33.8639;
                }
                catch { }
            }

            // 3. ACC Rain Predictions
            var accRain10 = _pluginManager.GetPropertyValue("DataCorePlugin.GameRawData.Graphics.rainIntensityIn10Min");
            if (accRain10 != null)
            {
                try { rain10 = Convert.ToDouble(accRain10); } catch { }
            }
            var accRain30 = _pluginManager.GetPropertyValue("DataCorePlugin.GameRawData.Graphics.rainIntensityIn30Min");
            if (accRain30 != null)
            {
                try { rain30 = Convert.ToDouble(accRain30); } catch { }
            }

            // 4. Wind Speed (m/s)
            var irWind = _pluginManager.GetPropertyValue("DataCorePlugin.GameRawData.Telemetry.WindVel");
            var weekendWind = _pluginManager.GetPropertyValue("DataCorePlugin.GameRawData.SessionData.WeekendInfo.TrackWindVel");
            if (irWind != null)
            {
                try { windSpeed = Convert.ToDouble(irWind); } catch { }
            }
            else if (weekendWind != null)
            {
                try
                {
                    string rawW = weekendWind.ToString().Replace("m/s", "").Trim();
                    windSpeed = Convert.ToDouble(rawW);
                }
                catch { }
            }
            else
            {
                var accWind = _pluginManager.GetPropertyValue("DataCorePlugin.GameRawData.Graphics.windSpeed");
                if (accWind != null)
                {
                    try { windSpeed = Convert.ToDouble(accWind); } catch { }
                }
                else
                {
                    var shWind = _pluginManager.GetPropertyValue("DataCorePlugin.GameData.WindSpeed");
                    if (shWind != null)
                    {
                        try { windSpeed = Convert.ToDouble(shWind) / 3.6; } catch { }
                    }
                }
            }

            // 5. Air Temp
            var irAir = _pluginManager.GetPropertyValue("DataCorePlugin.GameRawData.Telemetry.AirTemp");
            if (irAir != null)
            {
                try { airTemp = Convert.ToDouble(irAir); } catch { }
            }
            else
            {
                var accAir = _pluginManager.GetPropertyValue("DataCorePlugin.GameRawData.Graphics.airTemp");
                if (accAir != null)
                {
                    try { airTemp = Convert.ToDouble(accAir); } catch { }
                }
                else
                {
                    var shAir = _pluginManager.GetPropertyValue("DataCorePlugin.GameData.AirTemperature");
                    if (shAir != null)
                    {
                        try { airTemp = Convert.ToDouble(shAir); } catch { }
                    }
                }
            }

            state.RelativeHumidity = humidity;
            state.AirPressure = pressure;
            state.RainIntensity10Min = rain10;
            state.RainIntensity30Min = rain30;
            state.WindSpeed = windSpeed;
            state.AirTemperature = airTemp;

            double relWindDir = 0.0;
            double absWindDir = 0.0;
            var shRelWindDir = _pluginManager.GetPropertyValue("DataCorePlugin.GameData.WindDirectionRelative");
            if (shRelWindDir != null)
            {
                try { relWindDir = Convert.ToDouble(shRelWindDir); } catch { }
            }
            var shAbsWindDir = _pluginManager.GetPropertyValue("DataCorePlugin.GameData.WindDirection");
            if (shAbsWindDir != null)
            {
                try { absWindDir = Convert.ToDouble(shAbsWindDir); } catch { }
            }
            state.RelativeWindDirection = relWindDir;
            state.AbsoluteWindDirection = absWindDir;

            // Read tyre compound to determine if Player is on Slick or Wet
            bool isPlayerOnSlick = true;
            var irDryTire = _pluginManager.GetPropertyValue("DataCorePlugin.GameRawData.Telemetry.PlayerCarDryTire");
            if (irDryTire != null)
            {
                try { isPlayerOnSlick = Convert.ToInt32(irDryTire) == 1; } catch { }
            }
            else
            {
                var accCompound = _pluginManager.GetPropertyValue("DataCorePlugin.GameRawData.Graphics.tyreCompound");
                if (accCompound != null)
                {
                    string compStr = accCompound.ToString().ToLower();
                    if (compStr.Contains("wet") || compStr.Contains("rain") || compStr.Contains("pneu_pluie"))
                    {
                        isPlayerOnSlick = false;
                    }
                }
                else
                {
                    var shCompound = _pluginManager.GetPropertyValue("DataCorePlugin.GameData.CarSettings_TyreCompound");
                    if (shCompound != null)
                    {
                        string compStr = shCompound.ToString().ToLower();
                        if (compStr.Contains("wet") || compStr.Contains("rain"))
                        {
                            isPlayerOnSlick = false;
                        }
                    }
                }
            }
            state.IsPlayerOnSlick = isPlayerOnSlick;

            // Pressure tracking history (sample once per second)
            if (state.RawSessionTime - _lastPressureSampleTime >= 1.0)
            {
                _pressureHistory.Add(Tuple.Create(state.RawSessionTime, pressure));
                _pressureHistory.RemoveAll(x => x.Item1 < state.RawSessionTime - 60.0);
                _lastPressureSampleTime = state.RawSessionTime;
            }

            int trend = 0;
            if (_pressureHistory.Count >= 10)
            {
                var earliest = _pressureHistory.First();
                var latest = _pressureHistory.Last();
                double deltaP = latest.Item2 - earliest.Item2;
                if (deltaP < -0.05) trend = -1;
                else if (deltaP > 0.05) trend = 1;
            }
            state.PressureTrend = trend;

            // WeatherAlertState logic
            if (state.TrackWetnessLevel >= 1)
            {
                state.WeatherAlertState = (state.TrackWetnessLevel == 1) ? "UMIDO" : "BAGNATO";
                state.TimeToImpactMinutes = 0.0;
                state.TimeToImpactLaps = 0.0;
            }
            else
            {
                bool rainImminent = false;
                double minutesToImpact = 99.0;

                // Feature detection for iRacing (either Telemetry or SessionData present)
                if (irPress != null || irHumid != null || weekendPress != null || weekendHumid != null)
                {
                    // Compute pressure drop over the last minute
                    if (_pressureHistory.Count >= 15)
                    {
                        var earliest = _pressureHistory.First();
                        var latest = _pressureHistory.Last();
                        double timeSpan = latest.Item1 - earliest.Item1;
                        if (timeSpan >= 30.0)
                        {
                            double deltaP = latest.Item2 - earliest.Item2;
                            double irHumidThreshold = alertThreshold / 100.0;
                            if (humidity >= irHumidThreshold && deltaP < -0.15)
                            {
                                rainImminent = true;
                                // Estimate Time to Impact based on WindSpeed (default Cap 1.5 m/s)
                                minutesToImpact = 2000.0 / Math.Max(1.5, state.WindSpeed) / 60.0;
                            }
                        }
                    }
                }
                else if (accRain10 != null || accRain30 != null) // ACC
                {
                    double threshold = alertThreshold / 100.0;
                    if (rain10 >= threshold)
                    {
                        rainImminent = true;
                        minutesToImpact = 10.0;
                    }
                    else if (rain30 >= threshold)
                    {
                        rainImminent = true;
                        minutesToImpact = 30.0;
                    }
                }

                if (rainImminent)
                {
                    state.WeatherAlertState = "ALERT_PIOGGIA";
                    state.TimeToImpactMinutes = minutesToImpact;

                    double refLapTime = data.NewData.BestLapTime.TotalSeconds > 0.0 ? data.NewData.BestLapTime.TotalSeconds : (data.NewData.LastLapTime.TotalSeconds > 0.0 ? data.NewData.LastLapTime.TotalSeconds : 90.0);
                    state.TimeToImpactLaps = (minutesToImpact * 60.0) / refLapTime;
                }
                else
                {
                    state.WeatherAlertState = "ASCIUTTO";
                    state.TimeToImpactMinutes = 99.0;
                    state.TimeToImpactLaps = 99.0;
                }
            }

            if (data.NewData.Opponents != null)
                state.Opponents = data.NewData.Opponents.ToList();
            else
                state.Opponents.Clear();

            if (state.SessionStateStatus >= 1 && state.SessionStateStatus <= 3 && state.CurrentFuelLevel > state.DeducedStartingFuelLimit)
            {
                state.DeducedStartingFuelLimit = state.CurrentFuelLevel;
            }

            if (state.DeducedStartingFuelLimit > 0.0)
            {
                state.MaxFuelCapacity = state.DeducedStartingFuelLimit;
            }

            ManageStartingFuelLatch(data, state);
            ManageRaceStart(data, state);
            ManageGlobalBaselineTemp(data, state);
        }

        private double CalculateAverageTireTemp(GameData data)
        {
            try
            {
                double GetIracingTireTemp(string tire)
                {
                    var l = _pluginManager.GetPropertyValue($"DataCorePlugin.GameRawData.Telemetry.{tire}tempCL");
                    var m = _pluginManager.GetPropertyValue($"DataCorePlugin.GameRawData.Telemetry.{tire}tempCM");
                    var r = _pluginManager.GetPropertyValue($"DataCorePlugin.GameRawData.Telemetry.{tire}tempCR");
                    if (l != null && m != null && r != null)
                        return (Convert.ToDouble(l) + Convert.ToDouble(m) + Convert.ToDouble(r)) / 3.0;
                    return -1.0;
                }

                double lf = GetIracingTireTemp("LF"); if (lf < 0) lf = data.NewData.TyreTemperatureFrontLeft;
                double rf = GetIracingTireTemp("RF"); if (rf < 0) rf = data.NewData.TyreTemperatureFrontRight;
                double lr = GetIracingTireTemp("LR"); if (lr < 0) lr = data.NewData.TyreTemperatureRearLeft;
                double rr = GetIracingTireTemp("RR"); if (rr < 0) rr = data.NewData.TyreTemperatureRearRight;

                return (lf + rf + lr + rr) / 4.0;
            }
            catch { return data.NewData.RoadTemperature; }
        }

        private void ManageStartingFuelLatch(GameData data, SessionState state)
        {
            if (state.SessionStateStatus == 4 && state.IsRaceSession && !state.RaceStartingFuelLatched && data.NewData.Fuel > 0)
            {
                state.RaceStartingFuel = data.NewData.Fuel;
                state.RaceStartingFuelLatched = true;
            }
            if (!state.IsRaceSession || state.SessionStateStatus < 4)
            {
                state.RaceStartingFuelLatched = false;
                state.RaceStartingFuel = 0.0;
            }
        }

        private void ManageRaceStart(GameData data, SessionState state)
        {
            if (state.IsRaceSession)
            {
                if (state.SessionStateStatus < 4)
                {
                    state.RaceStarted = false;
                }
                else if (!state.RaceStarted)
                {
                    if (state.Flag_Green > 0 || state.SpeedKmh > 80.0 || state.CurrentLap > 1)
                    {
                        state.RaceStarted = true;
                    }
                }
            }
            else
            {
                state.RaceStarted = false;
            }
        }

        private void ManageGlobalBaselineTemp(GameData data, SessionState state)
        {
            if (state.IsSessionActive && state.GlobalBaselineTemp == 0.0 && state.TrackTemperature > 0.0)
            {
                state.GlobalBaselineTemp = state.TrackTemperature;
            }
        }
    }
}