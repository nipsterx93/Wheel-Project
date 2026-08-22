// -------------------------------------------------------------------------
// FILE: TrackPositionValidator.cs
// Distingue un avanzamento guidato da un salto di posizione (ESC/teletrasporto),
// usando la sola TrackPositionPercent e la lunghezza della pista.
// Nessuna dipendenza SimHub: la decisione è verificabile dai test.
// -------------------------------------------------------------------------

using System;

namespace SimRIG
{
    /// <summary>Esito del confronto fra due campioni di posizione consecutivi.</summary>
    public enum PositionContinuity
    {
        /// <summary>Non c'è ancora abbastanza storia, o l'intervallo è troppo lungo per giudicare.</summary>
        Unknown,

        /// <summary>Avanzamento compatibile con una vettura che guida.</summary>
        Continuous,

        /// <summary>Salto che il tempo trascorso non giustifica: teletrasporto o reset.</summary>
        Discontinuous
    }

    /// <summary>
    /// Verifica che la posizione in pista avanzi in modo fisicamente plausibile.
    ///
    /// Serve ad autorizzare la calibrazione delle geofence solo dopo un tragitto realmente
    /// percorso. Il caso da escludere è il pilota che esce dai box, preme ESC a metà giro e
    /// viene teletrasportato: la transizione di <c>IsInPitLane</c> sarebbe identica a quella di
    /// un rientro vero, e la posizione registrata sarebbe quella del box invece dell'ingresso.
    ///
    /// Alternative valutate e scartate:
    ///   - **guard temporale** (permanenza minima fuori dai box): aggirabile per costruzione,
    ///     basta attendere oltre la soglia; e su una pista mai vista non esiste un tempo di
    ///     riferimento da cui derivarla;
    ///   - **soglia di percorso** (es. TrackPct >= 0.8): assume che l'ingresso box sia tardi nel
    ///     giro — vero a Misano (0.9498) ma è una proprietà del tracciato, non una regola; e non
    ///     protegge da un ESC premuto *dopo* la soglia.
    ///
    /// Il criterio adottato non fa assunzioni sul circuito ed è lo stesso principio già usato da
    /// <see cref="RelativePaceTracker"/> con MaxSectorFraction e dal gate GapJump: un salto che il
    /// tempo trascorso non giustifica è un artefatto, non un evento di gara.
    /// </summary>
    public class TrackPositionValidator
    {
        /// <summary>
        /// Velocità massima plausibile in m/s, usata come tetto per l'avanzamento per campione.
        /// 120 m/s sono 432 km/h: volutamente generosa, perché lo scopo è separare la guida dal
        /// teletrasporto (che sposta di frazioni di giro in un tick), non misurare la velocità.
        /// </summary>
        public const double MaxPlausibleSpeedMs = 120.0;

        /// <summary>
        /// Oltre questo intervallo fra campioni il confronto non è più significativo: una pausa o
        /// un salto del replay produrrebbero un margine così ampio da validare qualunque cosa.
        /// In quel caso si risponde Unknown e si riparte, invece di validare a vuoto.
        /// </summary>
        public const double MaxEvaluableGapSec = 2.0;

        private double? _lastPosition;
        private double _lastClock;

        public PositionContinuity LastResult { get; private set; } = PositionContinuity.Unknown;

        /// <summary>Ultimo avanzamento osservato, con segno. Per diagnostica.</summary>
        public double LastDelta { get; private set; }

        /// <summary>Avanzamento massimo ammesso all'ultimo campione. Per diagnostica.</summary>
        public double LastMaxDelta { get; private set; }

        /// <summary>
        /// Differenza fra due posizioni normalizzate tenendo conto del giro: da 0.99 a 0.01
        /// sono +0.02, non -0.98.
        /// </summary>
        public static double WrappedDelta(double current, double previous)
        {
            double delta = current - previous;
            if (delta > 0.5) delta -= 1.0;
            else if (delta < -0.5) delta += 1.0;
            return delta;
        }

        /// <summary>
        /// Valuta un campione di posizione.
        ///
        /// <paramref name="trackLengthMeters"/> non nota (0 o negativa) disattiva il guard e
        /// restituisce sempre Unknown: meglio nessun giudizio che uno basato su una lunghezza
        /// inventata. Stessa scelta del fallback di MaxSectorFraction.
        /// </summary>
        public PositionContinuity Update(double position, double sessionClock, double trackLengthMeters)
        {
            // Convenzione già in uso nel progetto (SessionState.Reset, PitRadar.cs:855):
            // posizione a zero significa stato azzerato, non inizio giro.
            if (position == 0.0 || trackLengthMeters <= 0.0)
            {
                Reset();
                LastResult = PositionContinuity.Unknown;
                return LastResult;
            }

            if (_lastPosition == null)
            {
                _lastPosition = position;
                _lastClock = sessionClock;
                LastResult = PositionContinuity.Unknown;
                return LastResult;
            }

            double dt = Math.Abs(_lastClock - sessionClock); // il clock di sessione è a scalare
            double delta = WrappedDelta(position, _lastPosition.Value);

            _lastPosition = position;
            _lastClock = sessionClock;

            if (dt <= 0.0 || dt > MaxEvaluableGapSec)
            {
                LastDelta = delta;
                LastMaxDelta = 0.0;
                LastResult = PositionContinuity.Unknown;
                return LastResult;
            }

            double maxDelta = (MaxPlausibleSpeedMs * dt) / trackLengthMeters;

            LastDelta = delta;
            LastMaxDelta = maxDelta;
            LastResult = Math.Abs(delta) <= maxDelta
                ? PositionContinuity.Continuous
                : PositionContinuity.Discontinuous;

            return LastResult;
        }

        public void Reset()
        {
            _lastPosition = null;
            _lastClock = 0.0;
            LastDelta = 0.0;
            LastMaxDelta = 0.0;
            LastResult = PositionContinuity.Unknown;
        }
    }
}
