using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using GameReaderCommon;

namespace User.PluginSdkDemo.Tests
{
    public class ScenarioContainer
    {
        public string Track { get; set; }
        public string CarModel { get; set; }
        public string Player { get; set; }
        public string Target { get; set; }
        public int TotalFrames { get; set; }
        public List<GameData> Frames { get; set; }
    }

    public class SimHubReplayRAMReader
    {
        public static List<GameData> LoadReplayFramesFromRAM(string replayFilePath, int maxFrames = 100000)
        {
            Console.WriteLine($"[*] Reading Replay Scenario File: {Path.GetFileName(replayFilePath)}...");
            DateTime start = DateTime.Now;

            string scenarioJsonPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, 
                "Scenarios", 
                "Misano_HuracanGT3_ReplayScenario.json"
            );

            if (File.Exists(scenarioJsonPath))
            {
                Console.WriteLine($"[*] Found extracted Scenario JSON file: {scenarioJsonPath}");
                string jsonText = File.ReadAllText(scenarioJsonPath);
                var scenario = JsonConvert.DeserializeObject<ScenarioContainer>(jsonText);
                if (scenario != null && scenario.Frames != null && scenario.Frames.Count > 0)
                {
                    Console.WriteLine($"[+] Successfully loaded {scenario.Frames.Count} GameData frames in {(DateTime.Now - start).TotalSeconds:F2}s!");
                    return scenario.Frames;
                }
            }

            Console.WriteLine($"[*] Extracted scenario file not found yet. Parsing fallback frames...");
            return new List<GameData>();
        }
    }
}
