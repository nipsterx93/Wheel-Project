// -------------------------------------------------------------------------
// FILE: SessionState.cs
// VERSION: Fix errori 2
// -------------------------------------------------------------------------
using System;
using System.Collections.Generic;

namespace SimRIG
{
    /// <summary>
    /// Questa classe contiene la "fotografia" dei dati di telemetria.
    /// Viene aggiornata una sola volta per frame dal TelemetryReader.
    /// Nessun calcolo complesso deve risiedere qui dentro.
    /// </summary>
    public class SessionState
    {
        // -------------------------------------------------------------------------
        // DATI DI BASE DELLA SESSIONE E GIOCO
        // -------------------------------------------------------------------------
        public bool IsGameRunning { get; set; } = false;
        public int SessionStateStatus { get; set; } = 0; // 0=Offline, 4=GreenFlag ecc.
        public bool IsRaceSession { get; set; } = false;
        public bool IsQualySession { get; set; } = false;
        public string GameName { get; set; } = "";

        public bool IsSessionActive
        {
            get
            {
                if (SessionStateStatus == 1) return false; // Replay is not active
                if (IsRaceSession) return SessionStateStatus == 4;
                if (IsQualySession) return SessionStateStatus == 3;
                return SessionStateStatus == 2 || SessionStateStatus == 4; // Practice or fallback
            }
        }

        public double SessionTimeLeftSec { get; set; } = 0.0;
        public double RawSessionTime { get; set; } = 0.0;
        public int TotalLaps { get; set; } = 0;

        public bool IsLapLimited { get; set; } = false;
        public bool IsTimeLimited { get; set; } = false;

        public bool GlobalCheckeredFlag { get; set; } = false;
        public bool PlayerCheckeredFlag { get; set; } = false;
        public bool RaceStarted { get; set; } = false;

        // -------------------------------------------------------------------------
        // DATI DEL VEICOLO / CIRCUITO (PLAYER)
        // -------------------------------------------------------------------------
        public string TrackId { get; set; } = "DEFAULT";
        public double TrackLengthMeters { get; set; } = 5000.0;
        public string CarClassId { get; set; } = "DEFAULT";
        public string CarModel { get; set; } = "DEFAULT";
        public int PlayerCarIdx { get; set; } = -1;

        public double SpeedKmh { get; set; } = 0.0;
        public int Rpm { get; set; } = 0;
        public int MaxRpm { get; set; } = 0;

        public double CurrentFuelLevel { get; set; } = 0.0;
        public double MaxFuelCapacity { get; set; } = 100.0;

        public int CurrentLap { get; set; } = 0;
        public double TrackPositionPercent { get; set; } = 0.0;
        public double CurrentLapTimeSec { get; set; } = 0.0;
        public double LastLapTimeSec { get; set; } = 0.0;
        public double BestLapTimeSec { get; set; } = 0.0;

        public int Position { get; set; } = 0;
        public bool IsInPitLane { get; set; } = false;
        public bool IsInPitBox { get; set; } = false;

        /// <summary>
        /// Limitatore di velocità inserito. Il gioco taglia la velocità da solo, quindi mentre è
        /// attivo la velocità della vettura **è** il limite della corsia box: non serve dedurlo.
        ///
        /// Era già letto dalla telemetria per il colore del LED, ma mai esposto qui — vedi Y-28.
        /// </summary>
        public bool IsPitLimiterOn { get; set; } = false;

        // -------------------------------------------------------------------------
        // BANDIERE (FLAGS)
        // -------------------------------------------------------------------------
        public int Flag_Yellow { get; set; } = 0;
        public int Flag_Blue { get; set; } = 0;
        public int Flag_Green { get; set; } = 0;
        public int Flag_Black { get; set; } = 0;

        // -------------------------------------------------------------------------
        // TEMPERATURE GOMME E PISTA
        // -------------------------------------------------------------------------
        public double TrackTemperature { get; set; } = 0.0;
        public double AverageTireTemp { get; set; } = 0.0;
        public double GlobalBaselineTemp { get; set; } = 0.0;
        public bool IsTrackWet { get; set; } = false;
        public int TrackWetnessLevel { get; set; } = 0;
        public double AirPressure { get; set; } = 1013.25; // in hPa (millibars) or equivalent
        public int PressureTrend { get; set; } = 0; // -1 = falling, 0 = stable, 1 = rising
        public double RelativeHumidity { get; set; } = 0.50; // fraction 0.0 to 1.0
        public double RainIntensity10Min { get; set; } = 0.0;
        public double RainIntensity30Min { get; set; } = 0.0;
        public double WindSpeed { get; set; } = 0.0; // m/s
        public double RelativeWindDirection { get; set; } = 0.0; // relative wind direction in degrees (0 = headwind, 180 = tailwind)
        public double AbsoluteWindDirection { get; set; } = 0.0; // absolute wind direction in degrees (0 = north)
        public double AirTemperature { get; set; } = 0.0; // °C
        public double TimeToImpactMinutes { get; set; } = 99.0;
        public double TimeToImpactLaps { get; set; } = 99.0;
        public string WeatherAlertState { get; set; } = "ASCIUTTO";
        public bool IsPlayerOnSlick { get; set; } = true;
        public string CrossoverAlertState { get; set; } = "NONE";
        public double CrossoverDeltaSeconds { get; set; } = 0.0;
        public string RawSessionInfoYaml { get; set; } = "";
        public double DeducedStartingFuelLimit { get; set; } = 0.0;

        // -------------------------------------------------------------------------
        // ELENCO AVVERSARI (Riferimento diretto dalla telemetria)
        // -------------------------------------------------------------------------
        public List<GameReaderCommon.Opponent> Opponents { get; set; } = new List<GameReaderCommon.Opponent>();

        // -------------------------------------------------------------------------
        // EVENTI "LATCHED" (Eventi registrati all'inizio gara/sessione)
        // -------------------------------------------------------------------------
        public double RaceStartingFuel { get; set; } = 0.0;
        public bool RaceStartingFuelLatched { get; set; } = false;

        /// <summary>
        /// Resetta lo stato di base. Utile quando il gioco viene chiuso.
        /// </summary>
        public void Reset()
        {
            IsGameRunning = false;
            SessionStateStatus = 0;
            IsRaceSession = false;
            IsQualySession = false;
            GameName = "";
            PlayerCarIdx = -1;
            SpeedKmh = 0.0;
            Rpm = 0;
            CurrentFuelLevel = 0.0;
            CurrentLap = 0;
            TrackPositionPercent = 0.0;
            IsInPitLane = false;
            IsInPitBox = false;
            IsPitLimiterOn = false;
            Opponents.Clear();
            GlobalBaselineTemp = 0.0;
            IsTrackWet = false;
            TrackWetnessLevel = 0;
            AirPressure = 1013.25;
            RelativeHumidity = 0.50;
            RainIntensity10Min = 0.0;
            RainIntensity30Min = 0.0;
            WindSpeed = 0.0;
            AirTemperature = 0.0;
            TimeToImpactMinutes = 99.0;
            TimeToImpactLaps = 99.0;
            WeatherAlertState = "ASCIUTTO";
            IsPlayerOnSlick = true;
            CrossoverAlertState = "NONE";
            CrossoverDeltaSeconds = 0.0;
            RawSessionInfoYaml = "";
        }
    }
}