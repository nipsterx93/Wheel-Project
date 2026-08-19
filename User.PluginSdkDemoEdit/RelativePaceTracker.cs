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

        /// <summary>Macrosettori di assestamento ancora da scartare dopo questo campione.</summary>
        public int PostPitSectorsRemaining;

        /// <summary>Estremi della finestra di plausibilità applicata a DeltaTime, per il log.</summary>
        public double MinDeltaTime;
        public double MaxDeltaTime;

        /// <summary>Ampiezza massima ammessa per DeltaGap in questo campione, per il log.</summary>
        public double MaxGapDelta;
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

        /// <summary>
        /// Un campione vale solo se il tempo trascorso somiglia a un macrosettore vero.
        /// La finestra è una frazione del macrosettore nominale (refLapTime / 20), non una
        /// costante: 4.7 s a Misano non sono i 6.5 s di un circuito lungo.
        ///
        /// Il solo pavimento a 1 s non bastava. Nel replay 20260819_205004 sono passati due
        /// campioni degeneri che hanno saturato il clamp:
        ///   dt=1.600 s  con gapDelta=3.642  -> instantRate 212.4 s/giro  (frammento di settore)
        ///   dt=20.266 s con gapDelta=13.038 -> instantRate  60.3 s/giro  (4 settori persi,
        ///                                                                 trattati come uno)
        /// Il secondo caso lasciava l'EMA incollata a +10.0 per le ultime 16 righe della gara.
        /// </summary>
        public const double MinSectorFraction = 0.5;
        public const double MaxSectorFraction = 2.0;

        /// <summary>
        /// Ampiezza massima plausibile per un DeltaGap, come frazione del tempo di giro.
        ///
        /// Al rollover del contatore giri il gap calcolato da <c>posDiffLaps * refLapTime</c> salta
        /// di **un giro intero** e torna subito indietro: nel replay 20260819_221922, un gapDelta di
        /// 93.033 s con un deltaTime di 4.000 s del tutto plausibile, cioè un instantRate di
        /// 2160 s/giro. I gate temporali non possono vederlo — lì il difetto è l'ampiezza.
        ///
        /// Sullo stesso replay abs(gapDelta) valeva p50=0.073, p90=0.313, p99=0.540, e poi
        /// direttamente 93.033: fra segnale e artefatto ci sono due ordini di grandezza vuoti,
        /// quindi la soglia esatta conta poco. Mezzo giro è il punto di mezzo naturale — oltre
        /// metà giro un salto è più plausibilmente un wrap che un evento di gara — ed è la stessa
        /// convenzione già usata per il wrap del MergeGap in TargetStrategyManager.
        ///
        /// Il margine è ampio da entrambi i lati: a Misano la soglia vale ~47 s, cioè 87 volte il
        /// p99 del segnale legittimo e la metà dell'artefatto. Un testacoda o un'uscita di pista
        /// del target, che costano una manciata di secondi, restano dentro.
        /// </summary>
        public const double MaxGapDeltaFraction = 0.5;

        /// <summary>
        /// Macrosettori puliti da scartare dopo una fase di pit prima di riprendere a misurare.
        /// Non basta saltarne uno: il primo campione fuori dai box viene preso mentre la vettura
        /// sta ancora rientrando in pista e accelerando, e il rate che ne esce satura il clamp.
        /// Nel replay del 2026-08-18 tutte e tre le soste producevano un primo rate a ±10 s/giro.
        /// A ~4.7 s per macrosettore, 3 corrispondono a ~14 s di assestamento.
        /// </summary>
        public const int PostPitSettlingSectors = 3;

        public double RelativePace { get; private set; } = 0.0;

        /// <summary>
        /// True finché la finestra di assestamento post-pit non è esaurita: la reference corrente
        /// non può ancora essere usata per calcolare un rate.
        /// </summary>
        public bool PitSeedPending { get { return _postPitSectorsToSkip > 0; } }

        /// <summary>Macrosettori puliti ancora da scartare prima di riprendere a misurare.</summary>
        public int PostPitSectorsRemaining { get { return _postPitSectorsToSkip; } }

        public int LastValidMacroSector { get { return _lastValidMacroSector; } }
        public bool EmaInitialized { get { return _emaInitialized; } }

        private int _lastValidMacroSector = -1;
        private double _lastMacroSectorTime = 0.0;
        private double _lastMacroSectorGap = 0.0;
        private bool _emaInitialized = false;
        private int _postPitSectorsToSkip = 0;

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
            _postPitSectorsToSkip = 0;
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

            // Gate pit: indipendente dalla sequenza. Il contatore sopravvive all'uscita dai box
            // e impone una finestra di assestamento prima di riprendere i calcoli.
            bool inPit = playerInPit || targetInPit;
            if (inPit) _postPitSectorsToSkip = PostPitSettlingSectors;

            double dt = sessionClock - _lastMacroSectorTime;
            if (dt < 0.0) dt = -dt; // il clock di sessione è un conto alla rovescia
            sample.DeltaTime = dt;

            // Finestra di plausibilità del campione, ancorata al macrosettore nominale.
            // Con refLapTime non utilizzabile si ricade sul solo pavimento assoluto: meglio un
            // gate debole che una finestra calcolata su un tempo di giro inventato.
            double nominalSector = refLapTime > 0.0 ? refLapTime / MacroSectorCount : 0.0;
            double minDeltaTime = MinimumDeltaTime;
            double maxDeltaTime = double.MaxValue;
            if (nominalSector > 0.0)
            {
                minDeltaTime = Math.Max(MinimumDeltaTime, nominalSector * MinSectorFraction);
                maxDeltaTime = nominalSector * MaxSectorFraction;
            }
            sample.MinDeltaTime = minDeltaTime;
            sample.MaxDeltaTime = maxDeltaTime;

            sample.SequenceValid = (_lastValidMacroSector != -1) && (ForwardDistance(macroSector) == 1);

            if (inPit || !sample.SequenceValid || dt < minDeltaTime || dt > maxDeltaTime)
            {
                sample.Reason = ClassifyInvalidation(macroSector, playerInPit, targetInPit,
                                                     sample.SequenceValid, dt, minDeltaTime, maxDeltaTime);
                Reseed(macroSector, sessionClock, signedGap);
                _emaInitialized = false; // il prossimo rate pulito ri-inizializza l'EMA
                sample.WasReseeded = true;
                return sample;
            }

            // Finestra di assestamento post-pit. La sequenza qui è numericamente valida
            // (i macrosettori sono avanzati regolarmente in pit lane) ma i primi campioni fuori
            // dai box sono inutilizzabili per due motivi distinti:
            //   1. la reference precedente è stata presa dentro la sosta, quindi il DeltaGap
            //      attraverserebbe il pit;
            //   2. anche dopo averla scartata, la vettura sta ancora rientrando e accelerando,
            //      e il delta che ne esce satura il clamp.
            // Si scartano PostPitSettlingSectors macrosettori, ri-seminando ogni volta.
            if (_postPitSectorsToSkip > 0)
            {
                _postPitSectorsToSkip--;
                Reseed(macroSector, sessionClock, signedGap);
                _emaInitialized = false;
                sample.WasReseeded = true;
                sample.WasPostPitSeed = true;
                sample.PostPitSectorsRemaining = _postPitSectorsToSkip;
                return sample;
            }

            sample.DeltaGap = signedGap - _lastMacroSectorGap;

            // Gate di ampiezza, indipendente dai due gate temporali: qui il tempo trascorso è
            // plausibile ed è il salto del gap a non esserlo. Il valore resta in DeltaGap per la
            // diagnostica — non può essere consumato per errore, perché RelativeGapDeltaValid
            // deriva da RateComputed, che qui non viene mai alzato.
            double maxGapDelta = refLapTime > 0.0 ? refLapTime * MaxGapDeltaFraction : 0.0;
            sample.MaxGapDelta = maxGapDelta;
            if (maxGapDelta > 0.0 && Math.Abs(sample.DeltaGap) > maxGapDelta)
            {
                sample.Reason = RelativePaceInvalidationReason.GapJump;
                Reseed(macroSector, sessionClock, signedGap);
                _emaInitialized = false; // il prossimo rate pulito ri-inizializza l'EMA
                sample.WasReseeded = true;
                return sample;
            }

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
                                                                    bool targetInPit, bool sequenceValid,
                                                                    double dt, double minDeltaTime,
                                                                    double maxDeltaTime)
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

            if (dt > maxDeltaTime) return RelativePaceInvalidationReason.DeltaTimeTooLarge;
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
