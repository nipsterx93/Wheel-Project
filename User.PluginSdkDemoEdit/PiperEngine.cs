// -------------------------------------------------------------------------
// FILE: PiperEngine.cs
// VERSION: V1.1.00
// -------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Globalization;

namespace SimRIG
{
    public class PiperVoice
    {
        public string Name { get; set; }
        public string Language { get; set; } // e.g. "EN", "IT"
        public string Quality { get; set; } // e.g. "low", "medium", "x_low"
        public string Url { get; set; }
        public bool IsCustom { get; set; }
        public string CustomPath { get; set; }
        
        public string DisplayName => IsCustom 
            ? $"[Custom] {Path.GetFileNameWithoutExtension(CustomPath)}" 
            : $"{Name} [{Quality}]";
            
        public string Id => IsCustom 
            ? Path.GetFileNameWithoutExtension(CustomPath) 
            : Path.GetFileNameWithoutExtension(Url);
            
        public string Filename => IsCustom 
            ? Path.GetFileName(CustomPath) 
            : Path.GetFileName(Url);
    }

    public class PiperEngine
    {
        private static PiperEngine _instance;
        public static PiperEngine Instance => _instance ?? (_instance = new PiperEngine());

        private const string PiperZipUrl = "https://github.com/rhasspy/piper/releases/download/2023.11.14-2/piper_windows_amd64.zip";
        
        // Database dei modelli consigliati ufficiali
        private static readonly List<PiperVoice> RecommendedVoices = new List<PiperVoice>
        {
            new PiperVoice { Name = "riccardo", Language = "IT", Quality = "x_low", Url = "https://huggingface.co/rhasspy/piper-voices/resolve/main/it/it_IT/riccardo/x_low/it_IT-riccardo-x_low.onnx" },
            new PiperVoice { Name = "alan", Language = "EN", Quality = "low", Url = "https://huggingface.co/rhasspy/piper-voices/resolve/main/en/en_GB/alan/low/en_GB-alan-low.onnx" },
            new PiperVoice { Name = "alba", Language = "EN", Quality = "medium", Url = "https://huggingface.co/rhasspy/piper-voices/resolve/main/en/en_GB/alba/medium/en_GB-alba-medium.onnx" },
            new PiperVoice { Name = "northern_english_male", Language = "EN", Quality = "medium", Url = "https://huggingface.co/rhasspy/piper-voices/resolve/main/en/en_GB/northern_english_male/medium/en_GB-northern_english_male-medium.onnx" },
            new PiperVoice { Name = "hfc_male", Language = "EN", Quality = "medium", Url = "https://huggingface.co/rhasspy/piper-voices/resolve/main/en/en_US/hfc_male/medium/en_US-hfc_male-medium.onnx" },
            new PiperVoice { Name = "lessac", Language = "EN", Quality = "high", Url = "https://huggingface.co/rhasspy/piper-voices/resolve/main/en/en_US/lessac/high/en_US-lessac-high.onnx" },
            new PiperVoice { Name = "ryan", Language = "EN", Quality = "medium", Url = "https://huggingface.co/rhasspy/piper-voices/resolve/main/en/en_US/ryan/medium/en_US-ryan-medium.onnx" },
            new PiperVoice { Name = "ryan", Language = "EN", Quality = "high", Url = "https://huggingface.co/rhasspy/piper-voices/resolve/main/en/en_US/ryan/high/en_US-ryan-high.onnx" },
            new PiperVoice { Name = "thorsten", Language = "DE", Quality = "medium", Url = "https://huggingface.co/rhasspy/piper-voices/resolve/main/de/de_DE/thorsten/medium/de_DE-thorsten-medium.onnx" },
            new PiperVoice { Name = "carlfm", Language = "ES", Quality = "x_low", Url = "https://huggingface.co/rhasspy/piper-voices/resolve/main/es/es_ES/carlfm/x_low/es_ES-carlfm-x_low.onnx" },
            new PiperVoice { Name = "tom", Language = "FR", Quality = "medium", Url = "https://huggingface.co/rhasspy/piper-voices/resolve/main/fr/fr_FR/tom/medium/fr_FR-tom-medium.onnx" },
            new PiperVoice { Name = "pim", Language = "NL", Quality = "medium", Url = "https://huggingface.co/rhasspy/piper-voices/resolve/main/nl/nl_NL/pim/medium/nl_NL-pim-medium.onnx" },
            new PiperVoice { Name = "faber", Language = "PT", Quality = "medium", Url = "https://huggingface.co/rhasspy/piper-voices/resolve/main/pt/pt_BR/faber/medium/pt_BR-faber-medium.onnx" }
        };

        private readonly string _ttsDir;
        private readonly string _piperExePath;
        private readonly string _voicesDir;
        private readonly string _tempWavPath;

        private readonly string _soundSquelchOnPath;
        private readonly string _soundSquelchOffPath;
        private readonly string _soundNoisePath;
        private readonly string _soundBeepPath;

        private WebClient _webClient;
        private MediaPlayer _playerSquelch;
        private MediaPlayer _playerNoise;
        private MediaPlayer _playerSpeech;

        public bool IsDownloading { get; private set; }
        public double DownloadProgress { get; private set; }
        public string DownloadStatus { get; private set; } = "IDLE";

        public event Action<double, string> OnDownloadProgress;
        public event Action OnDownloadCompleted;
        public event Action<string> OnDownloadFailed;

        private PiperEngine()
        {
            string baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppDomain.CurrentDomain.BaseDirectory;
            _ttsDir = Path.Combine(baseDir, "SimRIG_TTS");
            _piperExePath = Path.Combine(_ttsDir, "piper", "piper.exe");
            _voicesDir = Path.Combine(_ttsDir, "voices");
            _tempWavPath = Path.Combine(_ttsDir, "temp_speech.wav");

            _soundSquelchOnPath = Path.Combine(_ttsDir, "sounds", "squelch_on.wav");
            _soundSquelchOffPath = Path.Combine(_ttsDir, "sounds", "squelch_off.wav");
            _soundNoisePath = Path.Combine(_ttsDir, "sounds", "noise.wav");
            _soundBeepPath = Path.Combine(_ttsDir, "sounds", "beep.wav");

            ExtractEmbeddedAudio();
        }

        private void ExtractEmbeddedAudio()
        {
            try
            {
                string soundsDir = Path.Combine(_ttsDir, "sounds");
                Directory.CreateDirectory(soundsDir);

                var assembly = Assembly.GetExecutingAssembly();
                string[] resources = assembly.GetManifestResourceNames();

                ExtractResource(assembly, resources, "beep.wav", _soundBeepPath);
                ExtractResource(assembly, resources, "noise.wav", _soundNoisePath);
                ExtractResource(assembly, resources, "squelch_on.wav", _soundSquelchOnPath);
                ExtractResource(assembly, resources, "squelch_off.wav", _soundSquelchOffPath);
            }
            catch (Exception ex)
            {
                SimHub.Logging.Current.Error("Error extracting embedded audio in PiperEngine: " + ex.Message);
            }
        }

        private void ExtractResource(Assembly assembly, string[] resources, string resNameEnding, string destPath)
        {
            if (File.Exists(destPath)) return;
            string resFullName = resources.FirstOrDefault(r => r.EndsWith(resNameEnding, StringComparison.OrdinalIgnoreCase));
            if (resFullName != null)
            {
                using (var stream = assembly.GetManifestResourceStream(resFullName))
                using (var destStream = File.Create(destPath))
                {
                    stream.CopyTo(destStream);
                }
            }
        }

        private void InitPlayers()
        {
            if (_playerSquelch != null) return;

            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null)
            {
                dispatcher.Invoke(() =>
                {
                    _playerSquelch = new MediaPlayer();
                    _playerNoise = new MediaPlayer();
                    _playerSpeech = new MediaPlayer();

                    // Loop del rumore bianco
                    _playerNoise.MediaEnded += (s, e) =>
                    {
                        _playerNoise.Position = TimeSpan.Zero;
                        _playerNoise.Play();
                    };
                });
            }
            else
            {
                _playerSquelch = new MediaPlayer();
                _playerNoise = new MediaPlayer();
                _playerSpeech = new MediaPlayer();

                _playerNoise.MediaEnded += (s, e) =>
                {
                    _playerNoise.Position = TimeSpan.Zero;
                    _playerNoise.Play();
                };
            }
        }

        public bool CheckEngineInstalled()
        {
            return File.Exists(_piperExePath);
        }

        public List<PiperVoice> GetAvailableVoices(string lang)
        {
            var list = new List<PiperVoice>();
            
            // 1. Aggiungi le voci consigliate della lingua
            foreach (var v in RecommendedVoices)
            {
                if (v.Language.Equals(lang, StringComparison.OrdinalIgnoreCase))
                {
                    list.Add(v);
                }
            }

            // 2. Scansiona i file locali per trovare file personalizzati
            if (Directory.Exists(_voicesDir))
            {
                var files = Directory.GetFiles(_voicesDir, "*.onnx");
                foreach (var file in files)
                {
                    string filename = Path.GetFileName(file);
                    if (list.Any(v => v.Filename.Equals(filename, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    string fileLang = GetLanguageFromFilename(filename);
                    if (fileLang.Equals(lang, StringComparison.OrdinalIgnoreCase))
                    {
                        list.Add(new PiperVoice
                        {
                            Name = Path.GetFileNameWithoutExtension(file),
                            Language = lang,
                            Quality = "custom",
                            Url = "",
                            IsCustom = true,
                            CustomPath = file
                        });
                    }
                }
            }

            return list;
        }

        private string GetLanguageFromFilename(string filename)
        {
            string lower = filename.ToLowerInvariant();
            if (lower.StartsWith("it")) return "IT";
            if (lower.StartsWith("en")) return "EN";
            if (lower.StartsWith("de")) return "DE";
            if (lower.StartsWith("es")) return "ES";
            if (lower.StartsWith("fr")) return "FR";
            if (lower.StartsWith("nl")) return "NL";
            if (lower.StartsWith("pt")) return "PT";
            return "";
        }

        public bool CheckModelInstalled(string lang, string voiceId)
        {
            var voices = GetAvailableVoices(lang);
            var selected = voices.FirstOrDefault(v => v.Id.Equals(voiceId, StringComparison.OrdinalIgnoreCase))
                           ?? voices.FirstOrDefault();
                           
            if (selected == null) return false;
            if (selected.IsCustom) return File.Exists(selected.CustomPath);
            
            return File.Exists(Path.Combine(_voicesDir, selected.Filename));
        }

        public string GetModelPath(string lang, string voiceId)
        {
            var voices = GetAvailableVoices(lang);
            var selected = voices.FirstOrDefault(v => v.Id.Equals(voiceId, StringComparison.OrdinalIgnoreCase))
                           ?? voices.FirstOrDefault();
            
            if (selected == null) return null;
            if (selected.IsCustom) return selected.CustomPath;
            
            return Path.Combine(_voicesDir, selected.Filename);
        }

        public string GetModelDownloadUrl(string lang, string voiceId)
        {
            var voices = GetAvailableVoices(lang);
            var selected = voices.FirstOrDefault(v => v.Id.Equals(voiceId, StringComparison.OrdinalIgnoreCase))
                           ?? voices.FirstOrDefault();
            return selected?.Url;
        }

        public void ImportCustomModel(string onnxPath, string currentLang)
        {
            try
            {
                if (!File.Exists(onnxPath)) return;
                Directory.CreateDirectory(_voicesDir);

                string originalFilename = Path.GetFileName(onnxPath);
                string fileLang = GetLanguageFromFilename(originalFilename);
                string destFilename = originalFilename;

                if (string.IsNullOrEmpty(fileLang))
                {
                    destFilename = $"{currentLang.ToLowerInvariant()}_{originalFilename}";
                }

                string destOnnx = Path.Combine(_voicesDir, destFilename);
                File.Copy(onnxPath, destOnnx, true);

                string jsonPath = onnxPath + ".json";
                if (File.Exists(jsonPath))
                {
                    string destJson = destOnnx + ".json";
                    File.Copy(jsonPath, destJson, true);
                }
            }
            catch (Exception ex)
            {
                SimHub.Logging.Current.Error("Error importing custom model: " + ex.Message);
            }
        }

        public void CancelDownload()
        {
            if (IsDownloading && _webClient != null)
            {
                _webClient.CancelAsync();
            }
        }

        public void StartSetupAsync(string lang, string voiceId)
        {
            if (IsDownloading) return;

            IsDownloading = true;
            DownloadProgress = 0.0;
            DownloadStatus = "PREPARING";
            OnDownloadProgress?.Invoke(0.0, "Preparing setup...");

            Task.Run(() =>
            {
                try
                {
                    Directory.CreateDirectory(_ttsDir);
                    Directory.CreateDirectory(_voicesDir);

                    if (!CheckEngineInstalled())
                    {
                        DownloadEngine(lang, voiceId);
                    }
                    else
                    {
                        DownloadModel(lang, voiceId);
                    }
                }
                catch (Exception ex)
                {
                    ResetDownloadState();
                    OnDownloadFailed?.Invoke(ex.Message);
                }
            });
        }

        private void DownloadEngine(string lang, string voiceId)
        {
            DownloadStatus = "DOWNLOADING_ENGINE";
            string zipPath = Path.Combine(_ttsDir, "piper_temp.zip");

            _webClient = new WebClient();
            _webClient.DownloadProgressChanged += (s, e) =>
            {
                DownloadProgress = e.ProgressPercentage * 0.5;
                OnDownloadProgress?.Invoke(DownloadProgress, $"Downloading voice engine ({e.ProgressPercentage}%)...");
            };

            _webClient.DownloadFileCompleted += (s, e) =>
            {
                if (e.Cancelled)
                {
                    ResetDownloadState();
                    OnDownloadFailed?.Invoke("Download cancelled.");
                    return;
                }
                if (e.Error != null)
                {
                    ResetDownloadState();
                    OnDownloadFailed?.Invoke(e.Error.Message);
                    return;
                }

                try
                {
                    DownloadStatus = "EXTRACTING_ENGINE";
                    OnDownloadProgress?.Invoke(50.0, "Extracting voice engine...");
                    
                    if (File.Exists(zipPath))
                    {
                        string extractPath = _ttsDir;
                        ZipFile.ExtractToDirectory(zipPath, extractPath);
                        File.Delete(zipPath);
                    }

                    DownloadModel(lang, voiceId);
                }
                catch (Exception ex)
                {
                    ResetDownloadState();
                    OnDownloadFailed?.Invoke("Extraction failed: " + ex.Message);
                }
            };

            _webClient.DownloadFileAsync(new Uri(PiperZipUrl), zipPath);
        }

        private void DownloadModel(string lang, string voiceId)
        {
            string onnxUrl = GetModelDownloadUrl(lang, voiceId);
            if (string.IsNullOrEmpty(onnxUrl))
            {
                ResetDownloadState();
                OnDownloadFailed?.Invoke("Voice model download URL not found for voice: " + voiceId);
                return;
            }

            DownloadStatus = "DOWNLOADING_MODEL";
            string modelFilename = Path.GetFileName(onnxUrl);
            string modelDestPath = Path.Combine(_voicesDir, modelFilename);

            _webClient = new WebClient();
            _webClient.DownloadProgressChanged += (s, e) =>
            {
                DownloadProgress = 50.0 + (e.ProgressPercentage * 0.4);
                OnDownloadProgress?.Invoke(DownloadProgress, $"Downloading voice model ({e.ProgressPercentage}%)...");
            };

            _webClient.DownloadFileCompleted += (s, e) =>
            {
                if (e.Cancelled)
                {
                    ResetDownloadState();
                    OnDownloadFailed?.Invoke("Download cancelled.");
                    return;
                }
                if (e.Error != null)
                {
                    ResetDownloadState();
                    OnDownloadFailed?.Invoke(e.Error.Message);
                    return;
                }

                DownloadModelConfig(lang, onnxUrl, modelDestPath);
            };

            _webClient.DownloadFileAsync(new Uri(onnxUrl), modelDestPath);
        }

        private void DownloadModelConfig(string lang, string onnxUrl, string modelDestPath)
        {
            DownloadStatus = "DOWNLOADING_CONFIG";
            OnDownloadProgress?.Invoke(90.0, "Downloading configuration file...");

            string jsonUrl = onnxUrl + ".json";
            string jsonDestPath = modelDestPath + ".json";

            _webClient = new WebClient();
            _webClient.DownloadProgressChanged += (s, e) =>
            {
                DownloadProgress = 90.0 + (e.ProgressPercentage * 0.1);
                OnDownloadProgress?.Invoke(DownloadProgress, "Downloading configuration...");
            };

            _webClient.DownloadFileCompleted += (s, e) =>
            {
                ResetDownloadState();
                if (e.Error != null)
                {
                    OnDownloadFailed?.Invoke("Failed downloading config file: " + e.Error.Message);
                }
                else
                {
                    OnDownloadCompleted?.Invoke();
                }
            };

            _webClient.DownloadFileAsync(new Uri(jsonUrl), jsonDestPath);
        }

        private void ResetDownloadState()
        {
            IsDownloading = false;
            DownloadProgress = 0.0;
            DownloadStatus = "IDLE";
            if (_webClient != null)
            {
                _webClient.Dispose();
                _webClient = null;
            }
        }

        public void Speak(string text, string lang, string voiceId, double voiceVol, double noiseVol, double speechSpeed = 0.0)
        {
            if (!CheckEngineInstalled() || !CheckModelInstalled(lang, voiceId)) return;

            string modelPath = GetModelPath(lang, voiceId);
            if (string.IsNullOrEmpty(modelPath)) return;

            // Helper to run action on UI Dispatcher if available, or directly on the current thread
            Action<Action> runOnUI = (action) =>
            {
                var disp = System.Windows.Application.Current?.Dispatcher;
                if (disp != null)
                {
                    if (disp.CheckAccess())
                    {
                        try { action(); } catch (Exception ex) { SimHub.Logging.Current.Error("PiperEngine runOnUI direct error: " + ex.Message); }
                    }
                    else
                    {
                        try { disp.Invoke(action); } catch (Exception ex) { SimHub.Logging.Current.Error("PiperEngine runOnUI invoke error: " + ex.Message); }
                    }
                }
                else
                {
                    try { action(); } catch (Exception ex) { SimHub.Logging.Current.Error("PiperEngine runOnUI thread fallback error: " + ex.Message); }
                }
            };

            Task.Run(async () =>
            {
                try
                {
                    // Close players to release any existing file locks on temp_speech.wav
                    runOnUI(() =>
                    {
                        try { _playerSpeech?.Close(); } catch { }
                        try { _playerNoise?.Close(); } catch { }
                        try { _playerSquelch?.Close(); } catch { }
                    });

                    // Small delay to ensure Windows OS frees the file handle
                    await Task.Delay(100);

                    if (File.Exists(_tempWavPath))
                    {
                        try { File.Delete(_tempWavPath); } catch { }
                    }

                    double lengthScale = 1.0;
                    if (speechSpeed >= 0.0)
                    {
                        lengthScale = 1.0 / (1.0 + (speechSpeed / 100.0));
                    }
                    else
                    {
                        lengthScale = 1.0 + (-speechSpeed / 100.0);
                    }

                    var startInfo = new ProcessStartInfo
                    {
                        FileName = _piperExePath,
                        Arguments = $"-m \"{modelPath}\" -f \"{_tempWavPath}\" --length_scale {lengthScale.ToString("F2", CultureInfo.InvariantCulture)}",
                        UseShellExecute = false,
                        RedirectStandardInput = true,
                        CreateNoWindow = true
                    };

                    using (var process = new Process { StartInfo = startInfo })
                    {
                        process.Start();
                        using (var writer = process.StandardInput)
                        {
                            writer.WriteLine(text);
                        }
                        process.WaitForExit();
                    }

                    if (!File.Exists(_tempWavPath))
                    {
                        SimHub.Logging.Current.Error("PiperEngine: Failed to generate temporary speech WAV file.");
                        return;
                    }

                    InitPlayers();

                    // Squelch ON
                    runOnUI(() =>
                    {
                        _playerSquelch.Volume = noiseVol / 100.0;
                        _playerSquelch.Open(new Uri(Path.GetFullPath(_soundSquelchOnPath)));
                        _playerSquelch.Play();
                    });

                    await Task.Delay(250);

                    // Rumore bianco in loop
                    runOnUI(() =>
                    {
                        _playerNoise.Volume = (noiseVol / 100.0) * 0.4;
                        _playerNoise.Open(new Uri(Path.GetFullPath(_soundNoisePath)));
                        _playerNoise.Play();
                    });

                    await Task.Delay(100);

                    // Riproduzione voce
                    var speechFinished = new TaskCompletionSource<bool>();
                    EventHandler onEnded = (s, e) => speechFinished.TrySetResult(true);
                    EventHandler<ExceptionEventArgs> onFailed = (s, e) =>
                    {
                        SimHub.Logging.Current.Error("PiperEngine MediaPlayer MediaFailed: " + (e.ErrorException?.Message ?? "Unknown error"));
                        speechFinished.TrySetResult(false);
                    };

                    runOnUI(() =>
                    {
                        _playerSpeech.Volume = voiceVol / 100.0;
                        _playerSpeech.MediaEnded += onEnded;
                        _playerSpeech.MediaFailed += onFailed;
                        _playerSpeech.Open(new Uri(Path.GetFullPath(_tempWavPath)));
                        _playerSpeech.Play();
                    });

                    // Wait for speech to finish, but with a 15-second safety timeout to prevent hanging
                    var completedTask = await Task.WhenAny(speechFinished.Task, Task.Delay(15000));
                    if (completedTask != speechFinished.Task)
                    {
                        SimHub.Logging.Current.Warn("PiperEngine: Speech playback timed out after 15 seconds.");
                    }

                    runOnUI(() =>
                    {
                        _playerSpeech.MediaEnded -= onEnded;
                        _playerSpeech.MediaFailed -= onFailed;
                        _playerSpeech.Stop();
                        _playerSpeech.Close(); // RILASCIA IL LOCK!
                        
                        _playerNoise.Stop();
                        _playerNoise.Close(); // RILASCIA IL LOCK!

                        // Squelch OFF
                        _playerSquelch.Volume = noiseVol / 100.0;
                        _playerSquelch.Open(new Uri(Path.GetFullPath(_soundSquelchOffPath)));
                        _playerSquelch.Play();
                    });
                }
                catch (Exception ex)
                {
                    SimHub.Logging.Current.Error("Error in PiperEngine Speech Thread: " + ex.Message);
                    runOnUI(() =>
                    {
                        try { _playerNoise?.Close(); } catch { }
                        try { _playerSpeech?.Close(); } catch { }
                        try { _playerSquelch?.Close(); } catch { }
                    });
                }
            });
        }
    }
}
