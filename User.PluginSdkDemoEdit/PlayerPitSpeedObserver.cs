// -------------------------------------------------------------------------
// FILE: PlayerPitSpeedObserver.cs
// Y-28 fase 0c: il limite di velocita' della corsia box si legge dal
// limitatore del Player, non si deduce piu' solo dagli avversari.
// Nessuna dipendenza SimHub: la decisione e' verificabile dai test.
// -------------------------------------------------------------------------

using System;

namespace SimRIG
{
    /// <summary>
    /// Osserva la velocita' del Player mentre il limitatore e' inserito, per ricavarne il limite
    /// della corsia box.
    ///
    /// **Il buco che chiude.** Fino a Y-28 il limite si imparava **solo** osservando un avversario
    /// entrare ai box (<c>OpponentTracker</c> -> <c>UpdatePitLaneSpeedLimit</c>). In una sessione di
    /// Practice da soli in pista — cioe' esattamente la sessione in cui si calibra — quel dato
    /// restava a zero per sempre, e con lui due cose che ne dipendono: il pavimento di plausibilita'
    /// sui tempi di transito (<c>PitRadar.MinimumCredibleTransitSec</c>) e la soglia adattiva di
    /// rilevamento pit (<c>PitLaneDetector.SpeedThresholdFor</c>).
    ///
    /// **Perche' leggere invece di dedurre.** Quando il pilota inserisce il limitatore, il gioco
    /// taglia la velocita' da solo: quella *e'* il limite, non una stima. Non serve misurare la
    /// velocita' massima tenuta lungo un drive-through e sperare che il pilota fosse al limite —
    /// approccio che era stato considerato e scartato proprio per questo.
    ///
    /// **Perche' serve comunque una persistenza.** Subito dopo l'inserimento la vettura sta ancora
    /// decelerando, quindi i primi campioni sono piu' alti del limite. Si aspetta che la velocita'
    /// si sia stabilizzata prima di considerarla buona.
    /// </summary>
    public class PlayerPitSpeedObserver
    {
        /// <summary>
        /// Per quanto la velocita' deve restare stabile prima di valere come misura del limite.
        ///
        /// Copre la decelerazione iniziale: inserendo il limitatore a velocita' di pista servono
        /// alcuni secondi perche' il taglio faccia effetto. Sotto questa soglia si leggerebbe una
        /// velocita' ancora in discesa, sopra il limite vero.
        /// </summary>
        public const double StableSpeedPersistenceSec = 1.5;

        /// <summary>
        /// Quanto puo' oscillare la velocita' e restare "stabile", in km/h. Il taglio del gioco non
        /// e' perfettamente piatto: la vettura ondeggia di poco attorno al limite.
        /// </summary>
        public const double StableSpeedToleranceKmh = 2.0;

        /// <summary>Sotto questa velocita' non si sta percorrendo la corsia, si e' fermi o quasi.</summary>
        public const double MinimumPlausibleLimitKmh = 30.0;

        /// <summary>Sopra questa velocita' non e' un limite di corsia box, e' pista.</summary>
        public const double MaximumPlausibleLimitKmh = 120.0;

        private double? _stableSinceClock;
        private double _stableReference;

        /// <summary>Ultimo limite osservato e ritenuto valido. Zero se non ancora osservato.</summary>
        public double ObservedLimitKmh { get; private set; }

        /// <summary>
        /// Un campione. Restituisce <c>true</c> **solo** nel momento in cui una nuova osservazione
        /// valida si e' completata, cosi' il chiamante scrive una volta sola invece che a ogni tick.
        /// </summary>
        public bool Update(bool limiterOn, double speedKmh, double sessionClock)
        {
            if (!limiterOn)
            {
                _stableSinceClock = null;
                return false;
            }

            if (speedKmh < MinimumPlausibleLimitKmh || speedKmh > MaximumPlausibleLimitKmh)
            {
                _stableSinceClock = null;
                return false;
            }

            // Velocita' cambiata piu' della tolleranza: si sta ancora assestando, si riparte.
            if (_stableSinceClock == null || Math.Abs(speedKmh - _stableReference) > StableSpeedToleranceKmh)
            {
                _stableSinceClock = sessionClock;
                _stableReference = speedKmh;
                return false;
            }

            if (Math.Abs(sessionClock - _stableSinceClock.Value) < StableSpeedPersistenceSec)
            {
                return false;
            }

            // Stabile abbastanza a lungo: e' il limite. Si riparte da capo, cosi' una permanenza
            // lunga in corsia non produce una raffica di scritture identiche.
            ObservedLimitKmh = _stableReference;
            _stableSinceClock = null;
            return true;
        }

        public void Reset()
        {
            _stableSinceClock = null;
            ObservedLimitKmh = 0.0;
        }
    }
}
