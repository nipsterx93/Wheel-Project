// -------------------------------------------------------------------------
// FILE: SessionYamlParser.cs
// Fornitore iRacing per SessionMetadata: legge il SessionInfo YAML.
// -------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Globalization;

namespace SimRIG
{
    /// <summary>
    /// Estrae i metadati di sessione dal SessionInfo YAML di iRacing.
    ///
    /// <para><b>Funzione pura.</b> Riceve una stringa, restituisce un contenitore. Nessun I/O,
    /// nessuno stato: cosi' e' verificabile con un dump reale come ingresso.</para>
    ///
    /// <para><b>Perche' un parser a righe e non una libreria YAML.</b> Perche' funziona gia':
    /// il vecchio <c>ParseOpponentMaxFuelPct</c> leggeva il BoP con questo stesso schema, e i
    /// log di gara lo confermano (<c>BoP Pct: 0.500</c> non puo' venire da altro). Serve leggere
    /// una dozzina di campi noti, non interpretare YAML arbitrario.</para>
    ///
    /// <para><b>Va chiamato una volta per sessione, non a ogni tick.</b> Un SessionInfo reale
    /// con 41 piloti pesa ~150 KB: riscansionarlo 60 volte al secondo significa ~8.7 MB/s di
    /// stringhe piu' una Dictionary nuova ogni frame. La cache sta in <c>TelemetryReader</c>.</para>
    /// </summary>
    public static class SessionYamlParser
    {
        public const string SourceLabel = "iRacing SessionInfo YAML";

        /// <summary>Un giro sotto questa soglia non e' un passo, e' un errore di lettura.</summary>
        private const double MinPlausiblePaceSec = 10.0;

        /// <summary>
        /// Legge lo YAML e restituisce quello che ha trovato. Un contenitore vuoto (con
        /// <c>SourceName</c> vuoto) significa "non c'era niente da leggere" — non e' un errore,
        /// e' il caso normale su un gioco che non pubblica uno YAML.
        /// </summary>
        public static SessionMetadata Parse(string yaml)
        {
            var meta = new SessionMetadata();
            if (string.IsNullOrEmpty(yaml)) return meta;

            string[] lines = yaml.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            // Sezione corrente. Le sezioni di primo livello iniziano a colonna zero.
            const int None = 0, WeekendInfo = 1, DriverInfo = 2;
            int section = None;
            bool inWeekendOptions = false;
            bool inDrivers = false;

            int playerCarIdx = -1;

            // Un pilota alla volta, mentre si scorre la lista.
            string curName = null;
            int curCarIdx = -1;
            string curClass = null;
            double curClassPace = 0.0;
            double curMaxFuelPct = 0.0;
            bool curHasAny = false;

            // I dati del pilota che risultera' essere il Player, risolti alla fine.
            var byCarIdx = new Dictionary<int, string>();
            var paceByCarIdx = new Dictionary<int, double>();

            Action flushDriver = () =>
            {
                if (!curHasAny) return;

                if (!string.IsNullOrEmpty(curName))
                {
                    if (curClassPace >= MinPlausiblePaceSec)
                        meta.DriverEstimatedPaceSec[curName] = curClassPace;
                    if (curMaxFuelPct > 0.0)
                        meta.DriverMaxFuelPct[curName] = curMaxFuelPct;
                    if (curCarIdx >= 0)
                        byCarIdx[curCarIdx] = curName;
                }
                if (!string.IsNullOrEmpty(curClass) && curClassPace >= MinPlausiblePaceSec)
                    meta.ClassEstimatedPaceSec[curClass] = curClassPace;
                if (curCarIdx >= 0 && curClassPace >= MinPlausiblePaceSec)
                    paceByCarIdx[curCarIdx] = curClassPace;

                curName = null; curCarIdx = -1; curClass = null;
                curClassPace = 0.0; curMaxFuelPct = 0.0; curHasAny = false;
            };

            foreach (string line in lines)
            {
                if (line.Length == 0) continue;

                bool topLevel = !char.IsWhiteSpace(line[0]);
                string trimmed = line.Trim();

                if (topLevel)
                {
                    flushDriver();
                    inWeekendOptions = false;
                    inDrivers = false;

                    if (trimmed.StartsWith("WeekendInfo:")) section = WeekendInfo;
                    else if (trimmed.StartsWith("DriverInfo:")) section = DriverInfo;
                    else section = None;
                    continue;
                }

                if (section == WeekendInfo)
                {
                    if (trimmed.StartsWith("WeekendOptions:")) { inWeekendOptions = true; continue; }

                    if (!inWeekendOptions)
                    {
                        if (trimmed.StartsWith("TrackDisplayName:"))
                            meta.TrackName = Text(trimmed);
                        else if (trimmed.StartsWith("TrackPitSpeedLimit:"))
                            meta.PitSpeedLimitKmh = SpeedKmh(Text(trimmed));
                    }
                    else
                    {
                        if (trimmed.StartsWith("IncidentLimit:"))
                            meta.IncidentLimit = Integer(Text(trimmed));
                        else if (trimmed.StartsWith("FastRepairsLimit:"))
                            meta.FastRepairsAvailable = Integer(Text(trimmed));
                        else if (trimmed.StartsWith("StandingStart:"))
                        {
                            int? v = Integer(Text(trimmed));
                            if (v.HasValue) meta.IsStandingStart = v.Value != 0;
                        }
                    }
                    continue;
                }

                if (section != DriverInfo) continue;

                if (trimmed.StartsWith("Drivers:")) { flushDriver(); inDrivers = true; continue; }

                if (!inDrivers)
                {
                    if (trimmed.StartsWith("DriverCarIdx:"))
                    {
                        int? v = Integer(Text(trimmed));
                        if (v.HasValue) playerCarIdx = v.Value;
                    }
                    else if (trimmed.StartsWith("DriverCarEstLapTime:"))
                    {
                        double? v = Number(Text(trimmed));
                        if (v.HasValue && v.Value >= MinPlausiblePaceSec)
                            meta.PlayerEstimatedPaceSec = v.Value;
                    }
                    else if (trimmed.StartsWith("DriverPitTrkPct:"))
                    {
                        double? v = Number(Text(trimmed));
                        if (v.HasValue && v.Value >= 0.0 && v.Value <= 1.0)
                            meta.PlayerPitStallPct = v.Value;
                    }
                    else if (trimmed.StartsWith("DriverCarFuelKgPerLtr:"))
                    {
                        double? v = Number(Text(trimmed));
                        if (v.HasValue && v.Value > 0.0) meta.FuelDensityKgPerLitre = v.Value;
                    }
                    else if (trimmed.StartsWith("DriverCarFuelMaxLtr:"))
                    {
                        double? v = Number(Text(trimmed));
                        if (v.HasValue && v.Value > 0.0) meta.PlayerMaxFuelLitres = v.Value;
                    }
                    continue;
                }

                // Dentro la lista dei piloti. Ogni voce comincia con un trattino.
                if (trimmed.StartsWith("-")) flushDriver();

                if (Field(trimmed, "CarIdx:"))
                {
                    int? v = Integer(Text(trimmed));
                    if (v.HasValue) { curCarIdx = v.Value; curHasAny = true; }
                }
                else if (Field(trimmed, "UserName:"))
                {
                    curName = Text(trimmed); curHasAny = true;
                }
                else if (Field(trimmed, "CarClassShortName:"))
                {
                    curClass = Text(trimmed); curHasAny = true;
                }
                else if (Field(trimmed, "CarClassEstLapTime:"))
                {
                    double? v = Number(Text(trimmed));
                    if (v.HasValue) { curClassPace = v.Value; curHasAny = true; }
                }
                else if (Field(trimmed, "CarClassMaxFuelPct:"))
                {
                    double? v = Number(Text(trimmed));
                    if (v.HasValue) { curMaxFuelPct = v.Value; curHasAny = true; }
                }
                else if (Field(trimmed, "CarClassDryTireSetLimit:"))
                {
                    int? v = Integer(Text(trimmed));
                    if (v.HasValue && !meta.DryTireSetLimit.HasValue) meta.DryTireSetLimit = v.Value;
                }
            }

            flushDriver();

            // Il Player si risolve per indice, non per nome: il nome non e' univoco.
            if (playerCarIdx >= 0)
            {
                double pace;
                if (!meta.PlayerEstimatedPaceSec.HasValue &&
                    paceByCarIdx.TryGetValue(playerCarIdx, out pace))
                {
                    meta.PlayerEstimatedPaceSec = pace;
                }
            }

            if (meta.DriverEstimatedPaceSec.Count > 0 || meta.DriverMaxFuelPct.Count > 0 ||
                meta.PlayerEstimatedPaceSec.HasValue || meta.PitSpeedLimitKmh.HasValue ||
                meta.IncidentLimit.HasValue || meta.PlayerPitStallPct.HasValue)
            {
                meta.SourceName = SourceLabel;
            }

            return meta;
        }

        /// <summary>Il campo, con o senza il trattino di lista davanti.</summary>
        private static bool Field(string trimmed, string name)
        {
            return trimmed.StartsWith(name, StringComparison.Ordinal)
                || trimmed.StartsWith("- " + name, StringComparison.Ordinal);
        }

        /// <summary>Il valore dopo i due punti, senza virgolette.</summary>
        private static string Text(string trimmed)
        {
            int colon = trimmed.IndexOf(':');
            if (colon < 0) return "";
            string v = trimmed.Substring(colon + 1).Trim();

            if (v.Length > 1)
            {
                if ((v[0] == '"' && v[v.Length - 1] == '"') ||
                    (v[0] == '\'' && v[v.Length - 1] == '\''))
                {
                    v = v.Substring(1, v.Length - 2).Trim();
                }
            }
            return v;
        }

        /// <summary>
        /// Un numero, ignorando l'unita' che iRacing appiccica dopo (<c>1.000 %</c>, <c>3 %</c>).
        /// Il valore percentuale si restituisce **com'e' scritto**: il vecchio parser del BoP
        /// faceva cosi', e i consumatori si aspettano <c>0.500</c>, non <c>50</c>.
        /// </summary>
        private static double? Number(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;

            int end = 0;
            while (end < value.Length &&
                   (char.IsDigit(value[end]) || value[end] == '.' ||
                    value[end] == '-' || value[end] == '+'))
            {
                end++;
            }
            if (end == 0) return null;

            double parsed;
            if (double.TryParse(value.Substring(0, end), NumberStyles.Any,
                                CultureInfo.InvariantCulture, out parsed))
            {
                return parsed;
            }
            return null;
        }

        private static int? Integer(string value)
        {
            double? v = Number(value);
            if (!v.HasValue) return null;
            if (v.Value > int.MaxValue || v.Value < int.MinValue) return null;
            return (int)Math.Round(v.Value);
        }

        /// <summary>
        /// Una velocita' con unita' (<c>"60.00 kph"</c>, <c>"37.28 mph"</c>), sempre in km/h.
        /// Senza la conversione un tracciato americano darebbe un limite sbagliato del 61%.
        /// </summary>
        private static double? SpeedKmh(string value)
        {
            double? v = Number(value);
            if (!v.HasValue || v.Value <= 0.0) return null;

            if (value.IndexOf("mph", StringComparison.OrdinalIgnoreCase) >= 0)
                return v.Value * 1.609344;

            return v.Value;
        }
    }
}
