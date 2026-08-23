// -------------------------------------------------------------------------
// FILE: CalibrationConsensus.cs
// Un campione singolo non e' una misura. Accumula osservazioni e restituisce
// la mediana, cosi' un valore anomalo non sposta il dato calibrato.
// Nessuna dipendenza SimHub: la decisione e' verificabile dai test.
// -------------------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace SimRIG
{
    /// <summary>
    /// Consenso su un dato calibrato osservato piu' volte.
    ///
    /// **Il difetto che chiude.** La calibrazione ha attraversato due regimi, sbagliati per motivi
    /// opposti. Prima delle fasi 1-5 valeva *il primo campione vince per sempre*
    /// (<c>if (_currentTrack.PitExitPct == -1.0)</c>): un dato scritto male restava per sempre e un
    /// dato migliore non poteva sostituirlo. Le fasi 2-3 hanno tolto quel blocco, ma la regola
    /// <c>CanOverwrite(Confirmed, Confirmed)</c> e' vera, quindi si e' passati a *l'ultimo campione
    /// vince* — e su <c>PitLaneSpeedLimit</c> non c'e' mai stata nemmeno la confidenza a filtrare.
    ///
    /// Misurato sul replay Misano del 2026-08-23: dodici osservazioni del limite di pit lane, undici
    /// a 60 km/h e una a 80 (una vettura ancora in decelerazione all'ingresso). L'80 ha sovrascritto
    /// il 60 corretto; il database e' tornato a 60 solo perche' altri avversari hanno scritto dopo.
    /// Fortuna, non protezione.
    ///
    /// **Il criterio adottato: la mediana.** Resiste agli outlier per costruzione — undici 60 e un
    /// 80 danno 60, mentre la media darebbe 61.7 e "l'ultimo che scrive" darebbe 80. Su una finestra
    /// scorrevole segue comunque un cambiamento reale e duraturo, senza bisogno di regole di
    /// adozione separate: bastano abbastanza campioni nuovi e la mediana si sposta da sola.
    ///
    /// **Perche' non la media.** Un solo campione fuori scala la trascina, ed e' esattamente il caso
    /// da cui ci si vuole difendere. Stesso motivo per cui a parita' di campioni si restituisce un
    /// valore **osservato** e non interpolato: vedi <see cref="Median"/>.
    /// </summary>
    public class CalibrationConsensus
    {
        /// <summary>
        /// Quanti campioni concordi bastano per dichiarare il dato consolidato.
        ///
        /// Tre e' il minimo che abbia senso: con due, un campione anomalo pesa la meta' del totale e
        /// la mediana non ha modo di isolarlo. Va tenuto basso perche' per le geofence i campioni
        /// sono rari — una sosta del Player ne produce uno solo, e ci sono gare con una sosta sola.
        /// </summary>
        public const int MinimumForConsensus = 3;

        /// <summary>
        /// Quanti campioni si tengono. Oltre, i piu' vecchi escono.
        ///
        /// Finestra scorrevole e non memoria infinita: se il dato reale cambia (altra configurazione
        /// del circuito, altra classe) la mediana deve poterlo seguire invece di restare ancorata a
        /// una storia che non vale piu'.
        /// </summary>
        public const int DefaultWindow = 9;

        private readonly List<double> _samples = new List<double>();
        private readonly double _agreementTolerance;
        private readonly int _window;

        /// <param name="agreementTolerance">
        /// Entro quale scarto due campioni parlano dello stesso valore. Dipende dalla grandezza:
        /// vedi le costanti in <see cref="PitRadar"/>.
        /// </param>
        public CalibrationConsensus(double agreementTolerance, int window = DefaultWindow)
        {
            _agreementTolerance = Math.Abs(agreementTolerance);
            _window = Math.Max(1, window);
        }

        public int SampleCount { get { return _samples.Count; } }

        public bool HasSamples { get { return _samples.Count > 0; } }

        /// <summary>Valore consolidato: la mediana dei campioni in finestra. Zero se non ce ne sono.</summary>
        public double Value
        {
            get { return _samples.Count == 0 ? 0.0 : Median(_samples); }
        }

        /// <summary>Quanti campioni concordano con il valore consolidato, entro la tolleranza.</summary>
        public int AgreeingCount
        {
            get
            {
                if (_samples.Count == 0) return 0;
                double median = Median(_samples);
                int count = 0;
                for (int i = 0; i < _samples.Count; i++)
                {
                    if (Math.Abs(_samples[i] - median) <= _agreementTolerance) count++;
                }
                return count;
            }
        }

        /// <summary>Il dato e' consolidato, cioe' abbastanza campioni concordano fra loro.</summary>
        public bool HasConsensus { get { return AgreeingCount >= MinimumForConsensus; } }

        public void Add(double sample)
        {
            _samples.Add(sample);
            while (_samples.Count > _window) _samples.RemoveAt(0);
        }

        /// <summary>
        /// Mediana. A parita' di campioni si restituisce l'elemento **centrale basso**, non la media
        /// dei due centrali: cosi' il risultato e' sempre un valore realmente osservato.
        ///
        /// Conta per i dati discreti — fra un 60 e un 80 la media darebbe 70, che non e' un limite
        /// di pit lane esistente — e non fa danno su quelli continui, dove la differenza fra i due
        /// centrali e' comunque dentro la tolleranza di accordo.
        /// </summary>
        public static double Median(IList<double> values)
        {
            if (values == null || values.Count == 0) return 0.0;

            var sorted = new List<double>(values);
            sorted.Sort();

            return sorted[(sorted.Count - 1) / 2];
        }

        public void Reset()
        {
            _samples.Clear();
        }
    }
}
