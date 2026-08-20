// -------------------------------------------------------------------------
// FILE: PitLaneDetector.cs
// Y-9: rilevamento della corsia box per il Player, con la stessa cascata di
// criteri già usata per gli avversari, e soglie di velocità derivate dal
// limite di pit lane appreso invece che cablate.
// Nessuna dipendenza SimHub: la decisione è verificabile dai test.
// -------------------------------------------------------------------------

using System;

namespace SimRIG
{
    /// <summary>Quale criterio ha fatto scattare il rilevamento, per il log.</summary>
    public enum PitDetectionTrigger
    {
        None,
        Telemetry,
        Stopped,
        SpeedPersistence
    }

    /// <summary>
    /// Decide se il Player è in corsia box.
    ///
    /// Sostituisce l'euristica precedente — <c>TrackPositionPercent &gt; 0.85 &amp;&amp;
    /// 10 &lt; SpeedKmh &lt; 100</c> — che classificava come "in pit" anche un tornante lento
    /// di fine giro: bastava trovarsi nell'ultimo 15% del giro a velocità ridotta.
    ///
    /// La cascata è la stessa già in uso per gli avversari in OpponentTracker:
    ///   1. telemetria esplicita (per il Player è affidabile, e serve anche a calibrare le geofence)
    ///   2. fuori dalla zona box spaziale non si è mai in pit, qualunque cosa dica la velocità
    ///   3. vettura ferma
    ///   4. velocità sotto soglia **persistente** per qualche secondo
    /// Il criterio 2 è quello che chiude il difetto: un tornante non è dentro la geofence.
    /// Il criterio 4 aggiunge la persistenza, che l'euristica non aveva affatto.
    /// </summary>
    public class PitLaneDetector
    {
        /// <summary>
        /// Soglia di velocità usata finché il limite di pit lane non è stato appreso.
        /// Corrisponde al valore storico della cascata avversari (60 km/h di limite + margine).
        /// </summary>
        public const double DefaultSpeedThresholdKmh = 80.0;

        /// <summary>
        /// Margine sopra il limite di pit lane appreso. Serve perché in corsia si viaggia
        /// **al** limite, con oscillazioni, e l'ingresso avviene in decelerazione.
        /// Con il limite più diffuso (60 km/h) la soglia risultante è 80, cioè esattamente
        /// il valore cablato finora: la formula generalizza il comportamento esistente
        /// invece di cambiarlo.
        /// </summary>
        public const double PitSpeedMarginKmh = 20.0;

        /// <summary>Quanto a lungo la velocità deve restare bassa prima di concludere che è un pit.</summary>
        public const double LowSpeedPersistenceSec = 3.0;

        /// <summary>Sotto questa velocità la vettura è considerata ferma.</summary>
        public const double StoppedSpeedKmh = 0.5;

        private double? _lowSpeedStartSec;

        public bool IsInPitLane { get; private set; }
        public PitDetectionTrigger LastTrigger { get; private set; } = PitDetectionTrigger.None;

        /// <summary>Secondi accumulati sotto soglia, per diagnostica.</summary>
        public double LowSpeedElapsed(double sessionClock)
        {
            if (_lowSpeedStartSec == null) return 0.0;
            return Math.Abs(sessionClock - _lowSpeedStartSec.Value);
        }

        /// <summary>
        /// Soglia di velocità effettiva per un limite di pit lane appreso.
        /// Con limite non ancora appreso (0 o negativo) si ricade sul valore storico: meglio
        /// una soglia ragionevole che una calcolata su un dato inventato.
        /// </summary>
        public static double SpeedThresholdFor(double learnedPitLimitKmh)
        {
            if (learnedPitLimitKmh <= 0.0) return DefaultSpeedThresholdKmh;
            return learnedPitLimitKmh + PitSpeedMarginKmh;
        }

        /// <summary>
        /// Valuta un campione. <paramref name="spatiallyInsidePitZone"/> viene dalla geofence
        /// del circuito; quando non è disponibile passare <c>true</c> disattiva quel filtro
        /// e la cascata si comporta come prima dell'introduzione delle geofence.
        /// </summary>
        public bool Update(bool telemetryInPitLane, bool spatiallyInsidePitZone, double speedKmh,
                           double sessionClock, double learnedPitLimitKmh)
        {
            // 1. La telemetria, quando lo dichiara, è la fonte più affidabile per il Player.
            if (telemetryInPitLane)
            {
                _lowSpeedStartSec = null;
                LastTrigger = PitDetectionTrigger.Telemetry;
                IsInPitLane = true;
                return true;
            }

            // 2. Fuori dalla zona box non si è in pit, per quanto piano si vada.
            //    È il criterio che scarta il tornante lento di fine giro.
            if (!spatiallyInsidePitZone)
            {
                _lowSpeedStartSec = null;
                LastTrigger = PitDetectionTrigger.None;
                IsInPitLane = false;
                return false;
            }

            // 3. Vettura ferma dentro la zona box.
            if (speedKmh < StoppedSpeedKmh)
            {
                LastTrigger = PitDetectionTrigger.Stopped;
                IsInPitLane = true;
                return true;
            }

            // 4. Velocità bassa e *persistente*: un singolo campione lento non basta.
            double threshold = SpeedThresholdFor(learnedPitLimitKmh);
            if (speedKmh < threshold)
            {
                if (_lowSpeedStartSec == null) _lowSpeedStartSec = sessionClock;

                if (Math.Abs(sessionClock - _lowSpeedStartSec.Value) >= LowSpeedPersistenceSec)
                {
                    LastTrigger = PitDetectionTrigger.SpeedPersistence;
                    IsInPitLane = true;
                    return true;
                }
            }
            else
            {
                _lowSpeedStartSec = null;
            }

            LastTrigger = PitDetectionTrigger.None;
            IsInPitLane = false;
            return false;
        }

        public void Reset()
        {
            _lowSpeedStartSec = null;
            IsInPitLane = false;
            LastTrigger = PitDetectionTrigger.None;
        }
    }
}
