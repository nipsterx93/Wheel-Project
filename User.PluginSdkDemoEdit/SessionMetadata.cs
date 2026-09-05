// -------------------------------------------------------------------------
// FILE: SessionMetadata.cs
// Contenitore AGNOSTICO dei metadati di sessione.
// -------------------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace SimRIG
{
    /// <summary>
    /// Quello che il gioco dichiara di sapere su questa sessione, prima ancora che si giri.
    ///
    /// <para><b>Perche' e' un contenitore e non un parser.</b> Su iRacing questi dati arrivano
    /// dal SessionInfo YAML; su altri titoli arriveranno per altre strade, o non arriveranno
    /// affatto. I consumatori (<c>RaceTimeProjection</c>, <c>FuelManager</c>, <c>PitRadar</c>)
    /// leggono <b>solo</b> da qui e chiedono "questo campo c'e'?". Aggiungere un gioco diventa
    /// cosi' un fornitore nuovo, non una riscrittura dei consumatori.</para>
    ///
    /// <para><b>Perche' i campi sono nullable e non zero.</b> Zero non e' un valore neutro:
    /// un tempo alla bandiera pari a zero significa "gara finita adesso" per tutto cio' che sta
    /// a valle, e un consumo pari a zero significa "non serve benzina". Un campo assente deve
    /// poter dire <i>non lo so</i>, che e' un'affermazione diversa da entrambe. Y-22 si e' gia'
    /// bruciato una volta su un sentinella numerico (<c>== -1.0</c>) scambiato per un dato vero.</para>
    ///
    /// <para><b>Prior, non autorita'.</b> Questi valori sono un punto di partenza ad alta
    /// confidenza, non una verita' che scavalca la misura. Appena esiste un dato misurato in
    /// pista, quello vince — sempre. E' la stessa regola di ADR-005: un campione singolo non
    /// consolidato non scalza un dato appreso.</para>
    /// </summary>
    public class SessionMetadata
    {
        /// <summary>Chi ha popolato questo contenitore. Vuoto = nessuno, tutto assente.</summary>
        public string SourceName { get; set; } = "";

        /// <summary>Vero se almeno un campo e' stato popolato da un fornitore.</summary>
        public bool IsPopulated { get { return !string.IsNullOrEmpty(SourceName); } }

        // ------------------------------------------------------------------
        // Passo stimato — il seme che sostituisce i ripieghi cablati
        // ------------------------------------------------------------------

        /// <summary>Passo stimato per classe, in secondi. Chiave = identificativo di classe.</summary>
        public Dictionary<string, double> ClassEstimatedPaceSec { get; }
            = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Passo stimato per pilota, in secondi. Deriva dalla classe della sua vettura.</summary>
        public Dictionary<string, double> DriverEstimatedPaceSec { get; }
            = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Passo stimato della vettura del Player, in secondi.</summary>
        public double? PlayerEstimatedPaceSec { get; set; }

        // ------------------------------------------------------------------
        // Carburante
        // ------------------------------------------------------------------

        /// <summary>
        /// Percentuale massima di serbatoio per pilota (BoP). Sostituisce il vecchio
        /// <c>ParseOpponentMaxFuelPct</c>, che rifaceva questo lavoro a ogni tick.
        /// </summary>
        public Dictionary<string, double> DriverMaxFuelPct { get; }
            = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Densita' del carburante di questa vettura, in kg per litro. Serve a convertire i litri
        /// in chilogrammi, perche' la penalita' di peso e' in secondi <b>per chilogrammo</b>
        /// (Y-43). Assente = si usa il valore convenzionale cablato.
        /// </summary>
        public double? FuelDensityKgPerLitre { get; set; }

        /// <summary>Capienza del serbatoio della vettura del Player, in litri.</summary>
        public double? PlayerMaxFuelLitres { get; set; }

        // ------------------------------------------------------------------
        // Corsia box
        // ------------------------------------------------------------------

        /// <summary>
        /// Limite di velocita' in corsia box, in km/h. E' il valore <b>ufficiale</b>: non e' la
        /// stessa grandezza che il consenso di Y-20 impara dalle velocita' osservate, e non lo
        /// sostituisce. Vale come valore iniziale ad alta confidenza.
        /// </summary>
        public double? PitSpeedLimitKmh { get; set; }

        /// <summary>Posizione della piazzola assegnata al Player, in frazione di giro.</summary>
        public double? PlayerPitStallPct { get; set; }

        // ------------------------------------------------------------------
        // Regolamento della sessione
        // ------------------------------------------------------------------

        /// <summary>Tetto di incidenti prima della squalifica.</summary>
        public int? IncidentLimit { get; set; }

        /// <summary>Riparazioni rapide disponibili. Negativo = illimitate.</summary>
        public int? FastRepairsAvailable { get; set; }

        /// <summary>Treni di gomme da asciutto disponibili per la sessione.</summary>
        public int? DryTireSetLimit { get; set; }

        /// <summary>Vero = partenza da fermo, falso = lanciata.</summary>
        public bool? IsStandingStart { get; set; }

        // ------------------------------------------------------------------
        // Identita' della sessione — serve alla deduplicazione del dump
        // ------------------------------------------------------------------

        public string TrackName { get; set; } = "";
        public string PlayerCarClass { get; set; } = "";

        /// <summary>Riporta il contenitore allo stato "nessun fornitore ha parlato".</summary>
        public void Clear()
        {
            SourceName = "";
            ClassEstimatedPaceSec.Clear();
            DriverEstimatedPaceSec.Clear();
            DriverMaxFuelPct.Clear();
            PlayerEstimatedPaceSec = null;
            FuelDensityKgPerLitre = null;
            PlayerMaxFuelLitres = null;
            PitSpeedLimitKmh = null;
            PlayerPitStallPct = null;
            IncidentLimit = null;
            FastRepairsAvailable = null;
            DryTireSetLimit = null;
            IsStandingStart = null;
            TrackName = "";
            PlayerCarClass = "";
        }

        /// <summary>
        /// Passo stimato per un pilota: prima il suo, poi quello della sua classe. Null se non
        /// se ne sa nulla — e chi chiama deve trattare il null come "non lo so", non come zero.
        /// </summary>
        public double? EstimatedPaceFor(string driverName, string carClass)
        {
            double pace;
            if (!string.IsNullOrEmpty(driverName) &&
                DriverEstimatedPaceSec.TryGetValue(driverName, out pace) && pace > 0.0)
            {
                return pace;
            }
            if (!string.IsNullOrEmpty(carClass) &&
                ClassEstimatedPaceSec.TryGetValue(carClass, out pace) && pace > 0.0)
            {
                return pace;
            }
            return null;
        }
    }
}
