// -------------------------------------------------------------------------
// FILE: LeaderPaceFilter.cs
// Media mobile del passo del leader, protetta dai campioni che arrivano
// mentre l'identita' del leader sta sfarfallando.
// Nessuna dipendenza SimHub: la decisione e' verificabile dai test.
// -------------------------------------------------------------------------

using System;

namespace SimRIG
{
    /// <summary>
    /// Filtra il passo del leader assoluto prima di lasciarlo entrare nella media mobile.
    ///
    /// **Il problema osservato.** In multiclasse il P1 assoluto cambia identita' di continuo (soste
    /// sfalsate fra classi con passi molto diversi). Il log del replay Daytona del 2026-08-23
    /// mostra raffiche di <c>TargetChanged</c> a distanza di decine di millisecondi l'uno
    /// dall'altro, e a ogni sfarfallio veniva letto il <c>LapMovingAverage</c> di un pilota
    /// diverso — spesso acerbo — e mescolato subito nella media.
    ///
    /// **Cosa fa e cosa non fa.** Questo filtro toglie il rumore di sfarfallio e la spazzatura
    /// fisicamente impossibile. Non pretende di indovinare quale sia il passo "giusto": la vera
    /// protezione contro un passo del leader sbagliato e' l'ancoraggio al cronometro reale in
    /// <see cref="RaceTimeProjection.TimeUntilLeaderCheckered"/>, che limita l'errore a un giro
    /// del leader qualunque cosa arrivi qui dentro. Questo e' un secondo strato, non il primo.
    /// </summary>
    public class LeaderPaceFilter
    {
        /// <summary>
        /// Peso del campione nuovo nella media mobile. 0.10 e' il valore gia' in uso in
        /// <c>RaceAnalyzer</c> prima di questa classe: il filtro non cambia la reattivita', cambia
        /// solo *quali* campioni entrano.
        /// </summary>
        public const double SmoothingAlpha = 0.10;

        /// <summary>
        /// Per quanto un'identita' di leader deve reggere prima che il suo passo entri nella media.
        /// Gli sfarfallii osservati durano decine di millisecondi; due secondi li tagliano tutti
        /// senza ritardare in modo percepibile un cambio di leader vero.
        /// </summary>
        public const double MinimumIdentityDwellSec = 2.0;

        private string _stableLeader;
        private string _pendingLeader;
        private double _pendingSinceClock;
        private bool _hasClock;

        /// <summary>Passo corrente, media mobile. Zero finche' non e' entrato almeno un campione.</summary>
        public double SmoothedPace { get; private set; }

        /// <summary>Numero di campioni scartati perche' fisicamente impossibili. Per diagnostica.</summary>
        public int RejectedImplausible { get; private set; }

        /// <summary>Numero di campioni scartati perche' l'identita' non si era ancora stabilizzata.</summary>
        public int RejectedUnstableIdentity { get; private set; }

        /// <summary>
        /// Offre un campione al filtro e restituisce il passo corrente.
        /// </summary>
        /// <param name="rawPaceSec">Passo grezzo letto per il leader corrente.</param>
        /// <param name="leaderName">Identita' del leader a cui appartiene il campione.</param>
        /// <param name="sessionClock">Orologio di sessione (in questo progetto scala a scendere).</param>
        /// <param name="trackLengthMeters">Lunghezza pista, per il limite di plausibilita' fisica.</param>
        public double Update(double rawPaceSec, string leaderName, double sessionClock, double trackLengthMeters)
        {
            if (!RaceTimeProjection.IsPhysicallyPlausibleLap(rawPaceSec, trackLengthMeters))
            {
                RejectedImplausible++;
                return SmoothedPace;
            }

            string incoming = leaderName ?? "";

            if (!_hasClock)
            {
                _hasClock = true;
                _stableLeader = incoming;
                _pendingLeader = incoming;
                _pendingSinceClock = sessionClock;
            }
            else if (incoming != _stableLeader)
            {
                // Identita' diversa da quella consolidata: parte (o prosegue) l'attesa.
                if (incoming != _pendingLeader)
                {
                    _pendingLeader = incoming;
                    _pendingSinceClock = sessionClock;
                }

                double heldFor = Math.Abs(_pendingSinceClock - sessionClock);
                if (heldFor < MinimumIdentityDwellSec)
                {
                    RejectedUnstableIdentity++;
                    return SmoothedPace;
                }

                // Ha retto abbastanza: diventa la nuova identita' consolidata.
                _stableLeader = incoming;
            }
            else
            {
                // Il leader consolidato e' tornato a essere quello corrente: l'attesa decade.
                _pendingLeader = incoming;
                _pendingSinceClock = sessionClock;
            }

            if (SmoothedPace <= 0.0) SmoothedPace = rawPaceSec;
            else SmoothedPace += (rawPaceSec - SmoothedPace) * SmoothingAlpha;

            return SmoothedPace;
        }

        public void Reset()
        {
            _stableLeader = null;
            _pendingLeader = null;
            _pendingSinceClock = 0.0;
            _hasClock = false;
            SmoothedPace = 0.0;
            RejectedImplausible = 0;
            RejectedUnstableIdentity = 0;
        }
    }
}
