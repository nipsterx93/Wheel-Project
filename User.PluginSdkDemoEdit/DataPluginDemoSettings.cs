using System.Collections.Generic;

namespace SimRIG
{
    public class VoiceModelSettings
    {
        public double VoiceVolume { get; set; } = 100.0;
        public double RadioNoiseVolume { get; set; } = 30.0;
        public double SpeechSpeed { get; set; } = 0.0;
    }

    public class DataPluginDemoSettings
    {

        public string LastProfileUsed { get; set; } = "Default";

        public int TopLeftEncoderMode { get; set; } = 0;

        public int TopRightEncoderMode { get; set; } = 1;

        public string SpeedUnit { get; set; } = "kmh";
        public string TempUnit { get; set; } = "C";
        public string PressureUnit { get; set; } = "bar";



        public bool EnableFuelCalculatorSystem { get; set; } = true;

        public bool EnableAutoPitStrategy { get; set; } = false;

        public double FuelWeightCoef { get; set; } = 0.03;

        public double TempCoef { get; set; } = 0.05;

        public double FuelSpeedCoef { get; set; } = 0.05;

        public double SpeedDropThresholdKmh { get; set; } = 2.0;

        public double DampSpeedDropOffsetKmh { get; set; } = 1.5;

        public double DampPaceOffsetSeconds { get; set; } = 2.0;



        // STEP 5: TTS Voice Engineer Settings

        public bool EnableVoiceEngineer { get; set; } = true;

        public string VoiceLanguage { get; set; } = "EN";

        public string CustomPlayerName { get; set; } = "";



        public string SelectedCustomModelPath { get; set; } = "";
        public string SelectedVoiceModel { get; set; } = "alan";
        public double VoiceVolume { get; set; } = 100.0;
        public double RadioNoiseVolume { get; set; } = 30.0;
        public double SpeechSpeed { get; set; } = 0.0;

        public Dictionary<string, VoiceModelSettings> ModelSettings { get; set; } = new Dictionary<string, VoiceModelSettings>(System.StringComparer.OrdinalIgnoreCase);

        public VoiceModelSettings GetSettingsForVoice(string voiceId)
        {
            if (ModelSettings == null)
            {
                ModelSettings = new Dictionary<string, VoiceModelSettings>(System.StringComparer.OrdinalIgnoreCase);
            }
            if (string.IsNullOrEmpty(voiceId)) voiceId = "alan";
            if (!ModelSettings.ContainsKey(voiceId))
            {
                ModelSettings[voiceId] = new VoiceModelSettings
                {
                    VoiceVolume = VoiceVolume,
                    RadioNoiseVolume = RadioNoiseVolume,
                    SpeechSpeed = SpeechSpeed
                };
            }
            return ModelSettings[voiceId];
        }


        public bool EnableLogVoice { get; set; } = true;

        public bool EnableLogFuel { get; set; } = false;

        public bool EnableLogStrategy { get; set; } = false;

        public bool EnableLogRadar { get; set; } = false;

        public bool EnableLogOpponents { get; set; } = false;

        public bool EnableLogMicrosector { get; set; } = false;

        public bool EnableLogSystem { get; set; } = true;

        public bool EnableLogWeather { get; set; } = false;

        public bool EnableLogHardware { get; set; } = false;

        public bool EnableLogMergeGap { get; set; } = true;

    }

}