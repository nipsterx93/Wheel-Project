//--------------------------------------------------------------------------------------
//FILE: ProfileManager.cs
//VERSION: Unknown
//--------------------------------------------------------------------------------------


using System;

using System.Collections.Generic;

using System.IO;

using System.Xml.Serialization;

using System.Windows.Media;

using System.Linq;



namespace SimRIG

{

    public static class ProfileManager

    {

        private static string BasePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins", "Data", "SimRIG", "Profiles");



        public static readonly List<string> FactoryProfiles = new List<string> { "Default", "Red_Race", "Blue_Chill" };



        public static void Init()

        {

            try

            {

                if (!Directory.Exists(BasePath)) Directory.CreateDirectory(BasePath);



                // --- 1. DEFAULT PROFILE ---

                CheckAndCreateFactoryProfile("Default", p => {

                    p.BitePoint = 50;

                    p.Brightness_Backlight = 25; p.Brightness_RPM = 25;

                    p.Idle_Mode = "Rainbow"; p.Idle_Color = "#FFFFFF";

                    p.Rpm_StartLed = 3; p.Rpm_LedCount = 16; p.Rpm_Style = "LeftToRight"; p.Rpm_UseGradient = true;

                    p.Rpm_Color_Start = "#FFFF0000"; p.Rpm_Color_End = "#FF0000FF";

                    p.Rpm_ZoneLow_Count = 2; p.Rpm_ZoneLow_Color = "#00FF00";

                    p.Rpm_ZoneMed_Count = 3; p.Rpm_ZoneMed_Color = "#FFFF00";

                    p.Rpm_ZoneHigh_Count = 2; p.Rpm_ZoneHigh_Color = "#FF8000";

                    p.Rpm_ZoneMax_Count = 1; p.Rpm_ZoneMax_Color = "#FF0000";



                    // Buttons

                    p.ButtonColorsHex.Clear();

                    for (int i = 0; i < 12; i++) p.ButtonColorsHex.Add("#FFFFFFFF"); // White

                });



                // --- 2. RED_RACE PROFILE ---

                CheckAndCreateFactoryProfile("Red_Race", p => {

                    p.BitePoint = 50;

                    p.Brightness_Backlight = 51; p.Brightness_RPM = 25;

                    p.Idle_Mode = "Breath"; p.Idle_Color = "#FF0000";

                    p.Rpm_StartLed = 3; p.Rpm_LedCount = 16; p.Rpm_Style = "SideToCenter"; p.Rpm_UseGradient = true;

                    p.Rpm_Color_Start = "#00FF00"; p.Rpm_Color_End = "#FF0000";



                    // Buttons

                    p.ButtonColorsHex.Clear();

                    for (int i = 0; i < 12; i++) p.ButtonColorsHex.Add("#FFFF0000"); // Red

                });



                // --- 3. BLUE_CHILL PROFILE ---

                CheckAndCreateFactoryProfile("Blue_Chill", p => {

                    p.BitePoint = 50;

                    p.Brightness_Backlight = 25; p.Brightness_RPM = 25;

                    p.Idle_Mode = "Breath"; p.Idle_Color = "#0000FF";

                    p.Rpm_StartLed = 3; p.Rpm_LedCount = 16; p.Rpm_Style = "SideToCenter"; p.Rpm_UseGradient = true;

                    p.Rpm_Color_Start = "#FFFFFF00"; p.Rpm_Color_End = "#FF0000FF";



                    // Buttons

                    p.ButtonColorsHex.Clear();

                    for (int i = 0; i < 12; i++) p.ButtonColorsHex.Add("#FF00BFFF"); // DeepSkyBlue

                });

            }

            catch (Exception ex) { SimHub.Logging.Current.Error($"[SimRIG] Init Error: {ex.Message}"); }

        }



        private static void CheckAndCreateFactoryProfile(string name, Action<SimRigProfile> customConfig)

        {

            string path = Path.Combine(BasePath, name + ".xml");

            if (!File.Exists(path))

            {

                SimRigProfile p = new SimRigProfile();

                p.ProfileName = name;

                // Default init lists to avoid nulls

                p.ButtonColorsHex = new List<string>();

                if (customConfig != null) customConfig(p);

                SaveProfile(p, name);

            }

        }



        public static void SaveProfile(SimRigProfile profile, string fileName)

        {

            try

            {

                if (!Directory.Exists(BasePath)) Directory.CreateDirectory(BasePath);

                if (!fileName.EndsWith(".xml")) fileName += ".xml";

                string fullPath = Path.Combine(BasePath, fileName);

                XmlSerializer serializer = new XmlSerializer(typeof(SimRigProfile));

                using (TextWriter writer = new StreamWriter(fullPath)) { serializer.Serialize(writer, profile); }

            }

            catch (Exception ex) { SimHub.Logging.Current.Error($"[SimRIG] Error saving: {ex.Message}"); }

        }



        public static SimRigProfile LoadProfile(string fileName)

        {

            try

            {

                if (!fileName.EndsWith(".xml")) fileName += ".xml";

                string fullPath = Path.Combine(BasePath, fileName);

                if (!File.Exists(fullPath)) return null;

                XmlSerializer serializer = new XmlSerializer(typeof(SimRigProfile));

                using (TextReader reader = new StreamReader(fullPath)) { return (SimRigProfile)serializer.Deserialize(reader); }

            }

            catch { return null; }

        }



        public static bool DeleteProfile(string fileName)

        {

            try

            {

                if (FactoryProfiles.Contains(fileName)) return false;

                if (!fileName.EndsWith(".xml")) fileName += ".xml";

                string fullPath = Path.Combine(BasePath, fileName);

                if (File.Exists(fullPath)) { File.Delete(fullPath); return true; }

                return false;

            }

            catch { return false; }

        }



        public static List<string> GetAvailableProfiles()

        {

            if (!Directory.Exists(BasePath)) Directory.CreateDirectory(BasePath);

            List<string> profiles = new List<string>();

            string[] files = Directory.GetFiles(BasePath, "*.xml");

            foreach (string f in files) profiles.Add(Path.GetFileNameWithoutExtension(f));

            profiles.Sort();

            return profiles;

        }



        public static string ColorToHex(Color c) => $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";

        public static Color HexToColor(string hex) { try { return (Color)ColorConverter.ConvertFromString(hex); } catch { return Colors.White; } }

    }

}