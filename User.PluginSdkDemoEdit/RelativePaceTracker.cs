// -------------------------------------------------------------------------
// FILE: RelativePaceTracker.cs
// Macchina di stato del RelativePace, estratta da TargetStrategyManager per
// renderla verificabile senza dipendenze SimHub (spec §3-§11).
// -------------------------------------------------------------------------

using System;

namespace SimRIG
{
    /// <summary>
    /// Esito del processamento di un singolo campione macrosettoriale.
    /// Espone tutti gli intermedi richiesti da spec §32/§33 per logging e test.
    /// </summary>
    public struct RelativePaceSample
    {
        public RelativePaceInvalidationReason Reason;
        public bool SequenceValid;

        /// <summary>Il campione è diventato la nuova reference.</summary>
        public bool WasReseeded;

        /// <summary>Primo campione pulito dopo una fase di pit: seed obbligatorio, mai un rate.</summary>
        public bool WasPostPitSeed;

        public bool RateComputed;

        /// <summary>Il rate ha inizializzato l'EMA invece di aggiornarla (spec §9.3).</summary>
        public bool EmaSeeded;

        public double PreviousGap;
        public double DeltaGap;
        public double DeltaTime;
        public double InstantRate;
        public double EmaBefore;

        /// <summary>Valore EMA prima del clamp, per distinguere saturazione da valore reale.</summary>
        public double EmaAfterRaw;

        public double EmaAfter;
        public bool Clamped;
    }

    /// <summary>
    /// Calcola il passo relativo al target in secondi/giro a partire dalla variazione
    /// del gap tra macrosettori consecutivi.
    ///
    /// Regola centrale: la sequenzialità dei macrosettori e l'usabilità del campione sono
    /// due gate **indipendenti**. Il TrackPercentage continua a progredire nella pit lane,
    /// quindi una sequenza può restare numericamente valida mentre il campione è inutilizzabile.
    /// Nessun DeltaGap deve mai attraversare una sosta (spec §9.1, §9.2).
    /// </summary>
    public class RelativePaceTracker
    {
        public const double Alpha = 0.30;
        public const double Beta = 0.70;
        public const double ClampLimit = 10.0;
        public const double MinimumDeltaTime = 1.0;
        public const int MacroSectorCount = 20;

        public double RelativePace { get; private set; } = 0.0;

        /// <summary>
        /// True quando la reference corrente nasce da una fase di pit e non può ancora
        /// essere usata per calcolare un rate.
        /// </summary>
        public bool PitSeedPending { get { return _pitContaminatedSeed; } }

        public int LastValidMacroSector { get { return _lastValidMacroSector; } }
        public bool EmaInitialized { get { return _emaInitialized; } }

        private int _lastValidMacroSector = -1;
        private double _lastMacroSectorTime = 0.0;
        private double _lastMacroSectorGap = 0.0;
        private bool _emaInitialized = false;
        private bool _pitContaminatedSeed = false;

        /// <summary>
        /// Reset completo: azzera il valore, non solo la reference. Da usare al cambio target
        /// e a inizio sessione (spec §10). Diverso dall'invalidazione temporanea, che
        /// **preserva** il valore corrente di RelativePace.
        /// </summary>
        public void Reset()
        {
            RelativePace = 0.0;
            _lastValidMacroSector = -1;
            _lastMacroSectorTime = 0.0;
            _lastMacroSectorGap = 0.0;
            _emaInitialized = false;
            _pitContaminatedSeed = false;
        }

        public RelativePaceSample ProcessSample(int macroSector, double sessionClock, double signedGap,
                                                bool playerInPit, bool targetInPit, double refLapTime)
        {
            var sample = new RelativePaceSample
            {
                Reason = RelativePaceInvalidationReason.None,
                PreviousGap = _lastMacroSectorGap,
                EmaBefore = RelativePace,
                EmaAfterRaw = RelativePace,
                EmaAfter = RelativePace
            };

            // Gate pit: indipendente dalla sequenza. Una volta alzato, il flag sopravvive
            // all'uscita dai box e forza un seed pulito prima di riprendere i calcoli.
            bool inPit = playerInPit || targetInPit;
            if (inPit) _pitContaminatedSeed = true;

            double dt = sessionClock - _lastMacroSectorTime;
            if (dt < 0.0) dt = -dt; // il clock di sessione è un conto alla rovescia
            sample.DeltaTime = dt;

            sample.SequenceValid = (_lastValidMacroSector != -1) && (ForwardDistance(macroSector) == 1);

            if (inPit || !sample.SequenceValid || dt < MinimumDeltaTime)
            {
                sample.Reason = ClassifyInvalidation(macroSector, playerInPit, targetInPit, sample.SequenceValid);
                Reseed(macroSector, sessionClock, signedGap);
                _emaInitialized = false; // il prossimo rate pulito ri-inizializza l'EMA
                sample.WasReseeded = true;
                return sample;
            }

            // Primo campione pulito dopo il pit. La sequenza qui è numericamente valida
            // (i macrosettori sono avanzati regolarmente in pit lane) ma la reference
            // precedente è stata campionata dentro la sosta: usarla farebbe attraversare
            // il pit al DeltaGap. Si rimanda di un macrosettore.
            if (_pitContaminatedSeed)
            {
                _pitContaminatedSeed = false;
                Reseed(macroSector, sessionClock, signedGap);
                _emaInitialized = false;
                sample.WasReseeded = true;
                sample.WasPostPitSeed = true;
                return sample;
            }

            sample.DeltaGap = signedGap - _lastMacroSectorGap;
            sample.InstantRate = (sample.DeltaGap / dt) * refLapTime;
            sample.RateComputed = true;

            if (!_emaInitialized)
            {
                // EMA invalidata a monte: il primo rate pulito la inizializza direttamente (spec §9.3)
                sample.EmaAfterRaw = sample.InstantRate;
                _emaInitialized = true;
                sample.EmaSeeded = true;
            }
            else
            {
                sample.EmaAfterRaw = (Alpha * sample.InstantRate) + (Beta * RelativePace);
            }

            sample.EmaAfter = Math.Max(-ClampLimit, Math.Min(ClampLimit, sample.EmaAfterRaw));
            sample.Clamped = sample.EmaAfter != sample.EmaAfterRaw;
            RelativePace = sample.EmaAfter;

            Reseed(macroSector, sessionClock, signedGap);
            sample.WasReseeded = true;
            return sample;
        }

        /// <summary>Distanza in avanti dal seed corrente, con wrap 19 -> 0.</summary>
        private int ForwardDistance(int macroSector)
        {
            return ((macroSector - _lastValidMacroSector) % MacroSectorCount + MacroSectorCount) % MacroSectorCount;
        }

        private RelativePaceInvalidationReason ClassifyInvalidation(int macroSector, bool playerInPit,
                                                                    bool targetInPit, bool sequenceValid)
        {
            if (playerInPit) return RelativePaceInvalidationReason.PlayerInPit;
            if (targetInPit) return RelativePaceInvalidationReason.TargetInPit;
            if (_lastValidMacroSector == -1) return RelativePaceInvalidationReason.NoPreviousSeed;

            if (!sequenceValid)
            {
                int forward = ForwardDistance(macroSector);
                if (forward == 0) return RelativePaceInvalidationReason.DuplicateSector;
                if (forward <= MacroSectorCount / 2) return RelativePaceInvalidationReason.MissingSector;
                return RelativePaceInvalidationReason.InvalidSequence; // salto all'indietro
            }

            return RelativePaceInvalidationReason.DeltaTimeTooSmall;
        }

        private void Reseed(int macroSector, double sessionClock, double signedGap)
        {
            _lastValidMacroSector = macroSector;
            _lastMacroSectorTime = sessionClock;
            _lastMacroSectorGap = signedGap;
        }
    }
}
