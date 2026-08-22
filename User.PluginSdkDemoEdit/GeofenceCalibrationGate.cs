// -------------------------------------------------------------------------
// FILE: GeofenceCalibrationGate.cs
// Y-9 / calibrazione: decide QUANDO è lecito registrare PitEntryPct e
// PitExitPct. Nessuna dipendenza SimHub: la decisione è verificabile.
// -------------------------------------------------------------------------

using System;

namespace SimRIG
{
    /// <summary>
    /// Autorizza la scrittura delle geofence solo dopo un tragitto realmente percorso in pista.
    ///
    /// Il difetto che chiude: <c>PitRadar.Update</c> registrava <c>PitEntryPct</c> alla prima
    /// transizione utile di <c>IsInPitLane</c>, senza sapere da dove si arrivasse. Partendo dai
    /// box la posizione registrata era quella del **pit box**, non dell'ingresso corsia — e non
    /// essendoci modo di correggerla, restava sbagliata per sempre.
    ///
    /// **Perché una condizione e non una sequenza.** La formulazione naturale sarebbe pretendere
    /// <c>True → False → True</c> su <c>IsInPitLane</c>. Ma partendo dalla griglia quella sequenza
    /// non parte mai: manca il <c>True</c> iniziale, e in gara non si calibrerebbe più nulla.
    /// Il requisito reale è più debole: prima di fidarsi di un ingresso, dev'esserci stato almeno
    /// un campione genuino **in pista** — posizione valida, movimento continuo — in questa sessione.
    ///
    /// La stessa condizione copre entrambi i casi senza bisogno di due regole:
    /// <list type="bullet">
    /// <item>partenza dai box: <c>IsInPitLane</c> parte true, si autorizza solo dopo l'uscita vera;</item>
    /// <item>partenza in griglia: si è già in pista, il primo pit stop è genuino e autorizza subito.</item>
    /// </list>
    ///
    /// Il movimento continuo è verificato da <see cref="TrackPositionValidator"/>: senza quello,
    /// un ESC premuto a metà giro produrrebbe una transizione indistinguibile da un rientro vero.
    /// </summary>
    public class GeofenceCalibrationGate
    {
        private readonly TrackPositionValidator _positionValidator = new TrackPositionValidator();

        private bool _seenGenuineOnTrack;
        private bool _wasInPitLane;
        private bool _hasPreviousPitState;

        /// <summary>
        /// True quando si è osservato almeno un campione credibile in pista in questa sessione.
        /// È il prerequisito per fidarsi di qualunque transizione successiva.
        /// </summary>
        public bool HasGenuineTrackSample { get { return _seenGenuineOnTrack; } }

        /// <summary>Esito dell'ultima valutazione di continuità, per diagnostica.</summary>
        public PositionContinuity LastContinuity { get { return _positionValidator.LastResult; } }

        /// <summary>Ingresso in corsia box osservato in questo campione (transizione false → true).</summary>
        public bool PitLaneEntered { get; private set; }

        /// <summary>Uscita dalla corsia box osservata in questo campione (transizione true → false).</summary>
        public bool PitLaneExited { get; private set; }

        /// <summary>
        /// Autorizzato a registrare <c>PitEntryPct</c> in questo istante: stiamo entrando in
        /// corsia box e ci arriviamo da un tragitto genuino.
        /// </summary>
        public bool CanCalibrateEntry { get; private set; }

        /// <summary>Autorizzato a registrare <c>PitExitPct</c>: stiamo uscendo dopo un ingresso autorizzato.</summary>
        public bool CanCalibrateExit { get; private set; }

        private bool _entryWasAuthorised;

        /// <summary>
        /// Valuta un campione di telemetria. Da chiamare a ogni tick, prima di decidere se
        /// scrivere le geofence.
        /// </summary>
        public void Update(bool isInPitLane, double trackPositionPercent, double sessionClock,
                           double trackLengthMeters)
        {
            PitLaneEntered = false;
            PitLaneExited = false;
            CanCalibrateEntry = false;
            CanCalibrateExit = false;

            PositionContinuity continuity =
                _positionValidator.Update(trackPositionPercent, sessionClock, trackLengthMeters);

            // Una discontinuità **invalida il tragitto accumulato**. Senza questo il flag sarebbe
            // un latch che non si abbassa mai: basterebbe guidare due campioni dopo l'uscita dai
            // box per autorizzare qualunque rientro successivo, ESC compreso. Il flag non
            // significa "ho guidato prima o poi", ma "il tragitto fino a qui è credibile".
            if (continuity == PositionContinuity.Discontinuous)
            {
                _seenGenuineOnTrack = false;
            }

            // Un campione conta come "genuino in pista" solo se siamo fuori dalla corsia, la
            // posizione è utilizzabile e il movimento è compatibile con la guida. La continuità
            // Unknown non basta: all'avvio, dopo una pausa o dopo un azzeramento non sappiamo
            // ancora nulla, e dare per buono quel campione riaprirebbe il caso teletrasporto.
            else if (!isInPitLane && continuity == PositionContinuity.Continuous)
            {
                _seenGenuineOnTrack = true;
            }

            if (!_hasPreviousPitState)
            {
                _hasPreviousPitState = true;
                _wasInPitLane = isInPitLane;
                return;
            }

            if (isInPitLane && !_wasInPitLane)
            {
                PitLaneEntered = true;
                // Ci si fida dell'ingresso solo se qualcosa di credibile è successo prima.
                CanCalibrateEntry = _seenGenuineOnTrack;
                _entryWasAuthorised = CanCalibrateEntry;
            }
            else if (!isInPitLane && _wasInPitLane)
            {
                PitLaneExited = true;
                // L'uscita si registra solo se il relativo ingresso era autorizzato: una coppia
                // entry+exit deve venire dallo stesso transito credibile, altrimenti si otterrebbe
                // una zona composta da due osservazioni scollegate.
                CanCalibrateExit = _entryWasAuthorised;
                _entryWasAuthorised = false;
            }

            _wasInPitLane = isInPitLane;
        }

        /// <summary>Da chiamare a inizio sessione: l'autorizzazione non sopravvive a un cambio di sessione.</summary>
        public void Reset()
        {
            _positionValidator.Reset();
            _seenGenuineOnTrack = false;
            _wasInPitLane = false;
            _hasPreviousPitState = false;
            _entryWasAuthorised = false;
            PitLaneEntered = false;
            PitLaneExited = false;
            CanCalibrateEntry = false;
            CanCalibrateExit = false;
        }
    }
}
