// -------------------------------------------------------------------------
// FILE: SessionMetadataDump.cs
// Riversa su disco cio' che e' stato letto dallo YAML. Diagnostica e fixture.
// -------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace SimRIG
{
    /// <summary>Una sessione osservata, come finisce nel file.</summary>
    public class SessionMetadataRecord
    {
        public string Source { get; set; }
        public string Track { get; set; }
        public string PlayerCarClass { get; set; }
        public string FirstSeenUtc { get; set; }

        public double? PlayerEstimatedPaceSec { get; set; }
        public double? PitSpeedLimitKmh { get; set; }
        public double? PlayerPitStallPct { get; set; }
        public double? FuelDensityKgPerLitre { get; set; }
        public double? PlayerMaxFuelLitres { get; set; }
        public int? IncidentLimit { get; set; }
        public int? FastRepairsAvailable { get; set; }
        public int? DryTireSetLimit { get; set; }
        public bool? IsStandingStart { get; set; }

        public Dictionary<string, double> ClassEstimatedPaceSec { get; set; }
        public int DriverCount { get; set; }
    }

    /// <summary>
    /// Scrive un file leggibile con quello che il fornitore YAML ha estratto.
    ///
    /// <para><b>Si scrive, non si rilegge mai.</b> Il plugin non deve dipendere da questo file
    /// a runtime: rileggerlo significherebbe far vincere una copia vecchia su una realta' fresca,
    /// che e' esattamente il difetto contro cui questo progetto ha combattuto in Y-20, Y-21,
    /// Y-22 e Y-50. Lo YAML e' disponibile gratis a ogni sessione. Questo file serve a un umano
    /// che vuole controllare, e a chi scrive i test che ha bisogno di un ingresso vero.</para>
    ///
    /// <para><b>Perche' salva anche lo YAML grezzo.</b> Senza, i test del parser sarebbero
    /// scritti contro uno YAML inventato — cioe' verificherebbero che il parser fa quello che
    /// chi lo ha scritto immaginava, non quello che iRacing manda davvero.</para>
    /// </summary>
    public static class SessionMetadataDump
    {
        public const string FileName = "SimRigMetadata.json";

        /// <summary>
        /// La cartella dove scrivere. Vuota = accanto alla DLL, cioe' la cartella di SimHub,
        /// dove vive gia' <c>SimRIG_Data.json</c>.
        ///
        /// Il plugin gira dentro SimHub e non ha modo di sapere dove sia il repository: quel
        /// percorso esiste solo sulla macchina di chi sviluppa. Per questo e' un'impostazione
        /// e non una costante — cablarlo romperebbe su qualunque altra installazione.
        /// </summary>
        public static string ResolveFolder(string configuredFolder)
        {
            if (!string.IsNullOrEmpty(configuredFolder))
            {
                string trimmed = configuredFolder.Trim();
                if (trimmed.Length > 0) return trimmed;
            }
            return AppDomain.CurrentDomain.BaseDirectory;
        }

        /// <summary>
        /// Chiave di una voce: traccia + classe del Player. E' la stessa granularita' del
        /// database di calibrazione, perche' il passo stimato **e'** per traccia e classe.
        /// Rigiocare venti volte lo stesso replay produce una voce sola.
        /// </summary>
        public static string EntryKey(string track, string playerCarClass)
        {
            string t = string.IsNullOrEmpty(track) ? "?" : track.Trim();
            string c = string.IsNullOrEmpty(playerCarClass) ? "?" : playerCarClass.Trim();
            return t + " | " + c;
        }

        public static SessionMetadataRecord ToRecord(SessionMetadata meta, string nowUtcIso)
        {
            var rec = new SessionMetadataRecord
            {
                Source = meta.SourceName,
                Track = meta.TrackName,
                PlayerCarClass = meta.PlayerCarClass,
                FirstSeenUtc = nowUtcIso,
                PlayerEstimatedPaceSec = meta.PlayerEstimatedPaceSec,
                PitSpeedLimitKmh = meta.PitSpeedLimitKmh,
                PlayerPitStallPct = meta.PlayerPitStallPct,
                FuelDensityKgPerLitre = meta.FuelDensityKgPerLitre,
                PlayerMaxFuelLitres = meta.PlayerMaxFuelLitres,
                IncidentLimit = meta.IncidentLimit,
                FastRepairsAvailable = meta.FastRepairsAvailable,
                DryTireSetLimit = meta.DryTireSetLimit,
                IsStandingStart = meta.IsStandingStart,
                ClassEstimatedPaceSec = new Dictionary<string, double>(meta.ClassEstimatedPaceSec),
                DriverCount = meta.DriverEstimatedPaceSec.Count
            };
            return rec;
        }

        /// <summary>
        /// Va riscritto? Solo se la voce manca, o se il contenuto e' cambiato davvero. Il
        /// confronto ignora <c>FirstSeenUtc</c>: altrimenti ogni replay sembrerebbe diverso.
        /// </summary>
        public static bool NeedsWrite(SessionMetadataRecord existing, SessionMetadataRecord candidate)
        {
            if (existing == null) return true;
            if (candidate == null) return false;

            string a = Comparable(existing);
            string b = Comparable(candidate);
            return !string.Equals(a, b, StringComparison.Ordinal);
        }

        private static string Comparable(SessionMetadataRecord r)
        {
            string keep = r.FirstSeenUtc;
            r.FirstSeenUtc = null;
            string json = JsonConvert.SerializeObject(r);
            r.FirstSeenUtc = keep;
            return json;
        }

        /// <summary>Un nome di file che non faccia arrabbiare Windows.</summary>
        public static string SafeFileName(string key)
        {
            var sb = new StringBuilder(key.Length);
            foreach (char c in key)
            {
                sb.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_');
            }
            return sb.ToString();
        }

        /// <summary>
        /// Scrive la voce se serve. Non solleva mai: una diagnostica che fa cadere il plugin
        /// in gara sarebbe peggio del problema che documenta.
        /// </summary>
        public static void Write(SessionMetadata meta, string rawYaml,
                                 string configuredFolder, LogManager log)
        {
            if (meta == null || !meta.IsPopulated) return;

            try
            {
                string folder = ResolveFolder(configuredFolder);
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                string path = Path.Combine(folder, FileName);
                var all = new Dictionary<string, SessionMetadataRecord>(StringComparer.OrdinalIgnoreCase);

                if (File.Exists(path))
                {
                    try
                    {
                        var loaded = JsonConvert.DeserializeObject<Dictionary<string, SessionMetadataRecord>>(
                            File.ReadAllText(path));
                        if (loaded != null) all = new Dictionary<string, SessionMetadataRecord>(
                            loaded, StringComparer.OrdinalIgnoreCase);
                    }
                    catch { /* file illeggibile: si riparte da zero invece di perdere la sessione */ }
                }

                string key = EntryKey(meta.TrackName, meta.PlayerCarClass);
                var candidate = ToRecord(meta, DateTime.UtcNow.ToString("o"));

                SessionMetadataRecord existing;
                all.TryGetValue(key, out existing);
                if (!NeedsWrite(existing, candidate)) return;

                if (existing != null) candidate.FirstSeenUtc = existing.FirstSeenUtc;
                all[key] = candidate;

                File.WriteAllText(path, JsonConvert.SerializeObject(all, Formatting.Indented));

                // Lo YAML grezzo, una volta per chiave: e' la fixture dei test del parser.
                if (!string.IsNullOrEmpty(rawYaml))
                {
                    string yamlPath = Path.Combine(folder, "SimRigSessionYaml_" + SafeFileName(key) + ".txt");
                    if (!File.Exists(yamlPath)) File.WriteAllText(yamlPath, rawYaml);
                }

                if (log != null)
                {
                    log.Log(LogModule.SYSTEM, LogType.EVENT, "Session Metadata Dumped",
                            $"{key} | pace={Show(meta.PlayerEstimatedPaceSec)} | pit={Show(meta.PitSpeedLimitKmh)} km/h | " +
                            $"stall={Show(meta.PlayerPitStallPct)} | classi={meta.ClassEstimatedPaceSec.Count} | " +
                            $"piloti={meta.DriverEstimatedPaceSec.Count} | file={path}");
                }
            }
            catch (Exception ex)
            {
                if (log != null)
                {
                    log.Log(LogModule.SYSTEM, LogType.EVENT, "Session Metadata Dump Failed", ex.Message);
                }
            }
        }

        private static string Show(double? v)
        {
            return v.HasValue ? v.Value.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) : "N/A";
        }
    }
}
