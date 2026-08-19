// -------------------------------------------------------------------------
// FILE: StrategyGateHysteresis.cs
// Isteresi dei gate strategici (debito Y-12), estratta come RelativePaceTracker
// per essere verificabile senza dipendenze SimHub.
// -------------------------------------------------------------------------

using System;

namespace SimRIG
{
    /// <summary>
    /// Comparatore con banda morta attorno a una soglia: lo stato commuta solo quando il
    /// segnale esce dalla banda, e dentro la banda **conserva** lo stato precedente.
    ///
    /// Serve perché i gate strategici confrontavano con zero un segnale rumoroso: nel replay
    /// 20260819_205004 il gap variava di 0.217 s al p90 fra campioni consecutivi, mentre le due
    /// soglie in gioco stavano proprio dentro quel rumore. Risultato: 375 STRATEGY_CHANGED in
    /// ~40 minuti, di cui 371 sullo stesso target — cioè un gate che oscillava su un bersaglio fermo.
    /// </summary>
    public class HysteresisLatch
    {
        private readonly double _threshold;
        private readonly double _band;
        private bool _state;
        private bool _initialized;

        public HysteresisLatch(double threshold, double band)
        {
            _threshold = threshold;
            _band = Math.Abs(band);
        }

        public bool State { get { return _state; } }
        public bool IsInitialized { get { return _initialized; } }

        /// <summary>Semiampiezza della banda morta attorno alla soglia.</summary>
        public double Band { get { return _band; } }

        /// <summary>
        /// Aggiorna il latch. Il primo campione inizializza lo stato con il confronto secco:
        /// senza uno stato precedente non c'è nulla da conservare, e partire sempre da false
        /// introdurrebbe un ritardo artificiale a inizio sessione.
        /// </summary>
        public bool Update(double value)
        {
            if (!_initialized)
            {
                _state = value >= _threshold;
                _initialized = true;
                return _state;
            }

            if (value >= _threshold + _band) _state = true;
            else if (value < _threshold - _band) _state = false;
            // dentro la banda: lo stato resta quello che era

            return _state;
        }

        public void Reset()
        {
            _state = false;
            _initialized = false;
        }
    }

    /// <summary>
    /// Impone una permanenza minima allo stato prima di accettarne uno nuovo.
    ///
    /// Complementare alla banda morta e non ridondante: la banda taglia le oscillazioni per
    /// **ampiezza**, il dwell quelle per **frequenza**. Nel replay 20260819_205004 metà delle
    /// oscillazioni durava meno di 0.6 s — invisibili nello snapshot (1 campione ogni 25 tick,
    /// ~0.9 s) ma ben presenti nella traccia degli eventi, che è a piena risoluzione.
    /// </summary>
    public class DwellFilter
    {
        private readonly double _minimumDwell;
        private StrategyDecision _state;
        private double _stateSince;
        private bool _initialized;

        public DwellFilter(double minimumDwellSeconds)
        {
            _minimumDwell = Math.Max(0.0, minimumDwellSeconds);
        }

        public StrategyDecision State { get { return _state; } }
        public bool IsInitialized { get { return _initialized; } }
        public double MinimumDwell { get { return _minimumDwell; } }

        /// <summary>Secondi trascorsi nello stato corrente all'istante dato.</summary>
        public double TimeInState(double sessionClock)
        {
            if (!_initialized) return 0.0;
            return Math.Abs(_stateSince - sessionClock);
        }

        /// <summary>
        /// Restituisce lo stato accettato. Il clock di sessione è un conto alla rovescia,
        /// quindi la durata si misura in valore assoluto: la stessa convenzione di
        /// RelativePaceTracker.ProcessSample.
        /// </summary>
        public StrategyDecision Update(StrategyDecision candidate, double sessionClock)
        {
            if (!_initialized)
            {
                _state = candidate;
                _stateSince = sessionClock;
                _initialized = true;
                return _state;
            }

            if (candidate == _state) return _state;

            if (Math.Abs(_stateSince - sessionClock) >= _minimumDwell)
            {
                _state = candidate;
                _stateSince = sessionClock;
            }

            return _state;
        }

        public void Reset()
        {
            _state = StrategyDecision.None;
            _stateSince = 0.0;
            _initialized = false;
        }
    }

    /// <summary>
    /// Isteresi applicata ai due gate che causavano il churn, più il dwell sulla decisione finale.
    ///
    /// I valori vengono da una simulazione sul replay 20260819_205004 con un modello validato
    /// campione per campione (1958/1958 su UndercutViable e StrategyDecision). Le cause misurate
    /// a piena risoluzione sui 188 UNDERCUT_NONVIABLE erano: Position 113 (60.1%),
    /// Margin 74 (39.4%), Traffic 1 (0.5%).
    /// </summary>
    public class StrategyGateHysteresis
    {
        /// <summary>
        /// Banda morta sul gate di posizione (soglia UndercutPositionThreshold = -0.5 s).
        /// Sweep fine: 0.20 -> 4.09% di disaccordo, 0.25 -> 2.38%, 0.30 -> 2.59%,
        /// 0.35 -> 11.69% con il ritardo p90 che salta da 5 s a 199 s. Il cliff è a 0.35
        /// perché la banda inizia a inghiottire un cambio genuino: 0.25 è l'ottimo con margine.
        /// </summary>
        public const double PositionHysteresis = 0.25;

        /// <summary>
        /// Banda morta sui capture margin, confrontati con zero.
        /// Con la posizione fissata: 0.10 -> 3.40%, 0.15 -> 2.55%, 0.20 -> 2.72%,
        /// 0.25 -> 5.30% e ritardo p90 da 5 s a 17 s.
        /// </summary>
        public const double MarginHysteresis = 0.15;

        /// <summary>
        /// Permanenza minima della decisione. Misurato esattamente sulla traccia a piena
        /// risoluzione: 1 s -> -64%, 2 s -> -72%, 5 s -> -82%, 10 s -> -87%. Oltre i 5 s il
        /// guadagno si appiattisce mentre il ritardo cresce, e una raccomandazione che cambia
        /// più spesso di così non è comunque azionabile da chi guida.
        /// </summary>
        public const double MinimumStateDwell = 5.0;

        /// <summary>Soglia di posizione per l'undercut (spec §26), invariata: la banda le sta attorno.</summary>
        public const double UndercutPositionThreshold = -0.5;

        private readonly HysteresisLatch _position;
        private readonly HysteresisLatch _undercutMargin;
        private readonly HysteresisLatch _overcutMargin;
        private readonly DwellFilter _dwell;

        public StrategyGateHysteresis()
        {
            _position = new HysteresisLatch(UndercutPositionThreshold, PositionHysteresis);
            _undercutMargin = new HysteresisLatch(0.0, MarginHysteresis);
            _overcutMargin = new HysteresisLatch(0.0, MarginHysteresis);
            _dwell = new DwellFilter(MinimumStateDwell);
        }

        public bool PositionOK { get { return _position.State; } }
        public bool UndercutMarginOK { get { return _undercutMargin.State; } }
        public bool OvercutMarginOK { get { return _overcutMargin.State; } }
        public StrategyDecision Decision { get { return _dwell.State; } }
        public double TimeInDecision(double sessionClock) { return _dwell.TimeInState(sessionClock); }

        public bool UpdatePosition(double signedGap) { return _position.Update(signedGap); }
        public bool UpdateUndercutMargin(double margin) { return _undercutMargin.Update(margin); }
        public bool UpdateOvercutMargin(double margin) { return _overcutMargin.Update(margin); }
        public StrategyDecision UpdateDecision(StrategyDecision candidate, double sessionClock)
        {
            return _dwell.Update(candidate, sessionClock);
        }

        /// <summary>
        /// Reset completo. Da chiamare al cambio target e a inizio sessione, per lo stesso
        /// motivo per cui si resetta RelativePaceTracker: gli stati latched si riferiscono a un
        /// confronto con un avversario specifico e non hanno senso trasferiti a un altro.
        /// </summary>
        public void Reset()
        {
            _position.Reset();
            _undercutMargin.Reset();
            _overcutMargin.Reset();
            _dwell.Reset();
        }
    }
}
