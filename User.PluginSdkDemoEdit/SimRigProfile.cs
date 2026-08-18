using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.Windows.Media;

namespace SimRIG
{
    [Serializable]
    public class LedEventConfig
    {
        public bool Enabled { get; set; } = true;
        public string ColorHex { get; set; } = "#FFFFFF"; // Colore Primario
        public string ColorHexSecondary { get; set; } = "#000000"; // Colore Secondario (es. Pit Limiter)

        public bool IsBlinking { get; set; } = true;
        public int BlinkIntervalMs { get; set; } = 300;

        public int ZoneA_Start { get; set; } = 0;
        public int ZoneA_Count { get; set; } = 3;

        public bool ZoneB_Enabled { get; set; } = true;
        public int ZoneB_Start { get; set; } = 19;
        public int ZoneB_Count { get; set; } = 3;

        public LedEventConfig() { }
    }

    [Serializable]
    public class SimRigProfile
    {
        public string ProfileName { get; set; } = "Default";

        // INPUT
        public double BitePoint { get; set; } = 50.0;
        public int EncoderTopLeft_Mode { get; set; } = 0;
        public int EncoderTopRight_Mode { get; set; } = 0;

        // LED GLOBAL
        public int Brightness_Backlight { get; set; } = 100;
        public int Brightness_RPM { get; set; } = 80;
        public string Idle_Mode { get; set; } = "Rainbow";
        public string Idle_Color { get; set; } = "#FFFFFF";

        // RPM LOGIC
        public int Rpm_StartLed { get; set; } = 0;
        public int Rpm_LedCount { get; set; } = 22;
        public string Rpm_Style { get; set; } = "LeftToRight";
        public bool Rpm_UseGradient { get; set; } = true;
        public string Rpm_Color_Start { get; set; } = "#00FF00";
        public string Rpm_Color_End { get; set; } = "#FF0000";

        // RPM ZONES
        public int Rpm_ZoneLow_Count { get; set; } = 5;
        public string Rpm_ZoneLow_Color { get; set; } = "#00FF00";
        public int Rpm_ZoneMed_Count { get; set; } = 5;
        public string Rpm_ZoneMed_Color { get; set; } = "#FFFF00";
        public int Rpm_ZoneHigh_Count { get; set; } = 5;
        public string Rpm_ZoneHigh_Color { get; set; } = "#FF8000";
        public int Rpm_ZoneMax_Count { get; set; } = 7;
        public string Rpm_ZoneMax_Color { get; set; } = "#FF0000";

        // FLAGS
        public LedEventConfig Flag_Yellow { get; set; } = new LedEventConfig { ColorHex = "#FFFF00" };
        public LedEventConfig Flag_Blue { get; set; } = new LedEventConfig { ColorHex = "#0000FF" };
        public LedEventConfig Flag_Green { get; set; } = new LedEventConfig { ColorHex = "#00FF00" };

        // CAR SYSTEMS
        public LedEventConfig Car_ABS { get; set; } = new LedEventConfig { ColorHex = "#00FFFF", IsBlinking = false };
        public LedEventConfig Car_TC { get; set; } = new LedEventConfig { ColorHex = "#FFA500", IsBlinking = false };
        // V0.9.22: Pit Limiter (Dual Color)
        public LedEventConfig Car_Pit { get; set; } = new LedEventConfig { ColorHex = "#FF0000", ColorHexSecondary = "#0000FF", IsBlinking = true, BlinkIntervalMs = 150 };

        // BUTTONS
        public List<string> ButtonColorsHex { get; set; } = new List<string>();

        public SimRigProfile() { }
    }
}