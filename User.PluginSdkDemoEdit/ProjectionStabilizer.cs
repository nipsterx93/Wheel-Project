// -------------------------------------------------------------------------
// FILE: ProjectionStabilizer.cs
// Stabilizzazione della proiezione di fine gara (punto 6 del piano).
//
// Sostituisce il ritardo di 30 secondi sulla discesa che teneva il totale
// giri bloccato in alto. Vedi Y-45 in PROJECT_STATE.md per la misura.
// -------------------------------------------------------------------------

using System;

namespace SimRIG
{
    /// <summary>
    /// Filtro di stabilizzazione della posizione proiettata alla bandiera.
    ///
    /// **Il difetto che questa classe chiude (Y-45).** Il totale giri veniva stabilizzato da due
    /// meccanismi sovrapposti: la banda di <see cref="RaceAnalyzer.UpdateLatchedLaps"/> — che e'
    /// corretta e resta — e, nel chiamante, un **ritardo di 30 secondi applicato alla sola
    /// discesa**, che ripartiva da zero ogni volta che il valore bersaglio cambiava. Quella era
    /// l'asimmetria vera: le salite erano immediate, le discese dovevano attendere 30 secondi di
    /// quiete che una stima rumorosa non concede mai.
    ///
    /// Misurato sul replay Road Atlanta `20260831_195300`, giri 23-26. Il totale era bloccato a 37
    /// mentre la proiezione grezza diceva ~34.95, cioe' 35. Il bersaglio oscillava fra 35 e 36
    /// perche' la proiezione ballava di **nove millesimi di giro** (34.946 / 34.959) a cavallo
    /// dello scalino dell'arrotondamento, e ogni oscillazione riarmava il conto alla rovescia:
    ///
    /// <code>
    ///   20:04:37  raw=34.959 -> bersaglio 36   timer riarmato
    ///   20:04:39  raw=34.946 -> bersaglio 35   timer riarmato
    ///   20:04:40  raw=34.955 -> bersaglio 36   timer riarmato
    ///   20:04:47  raw=34.950 -> bersaglio 35   timer riarmato
    ///   20:04:50  raw=34.956 -> bersaglio 36   timer riarmato
    /// </code>
    ///
    /// Risultato: 79 secondi di blocco invece dei 30 previsti — **tre giri interi** di gara — con
    /// 4.2-4.4 litri di rifornimento chiesti e non necessari. Rumore da meno di un secondo di gara
    /// ha tenuto in ostaggio la proiezione.
    ///
    /// **Il criterio adottato.** Il ritardo sparisce. Al suo posto la misura viene *lisciata* prima
    /// di arrivare alla banda: il rumore non produce piu' scalini da riarmare, e la banda —
    /// simmetrica: 0.05 sopra il confine in salita, 0.05 sotto il confine in discesa — torna a fare
    /// il solo lavoro per cui era stata scritta.
    ///
    /// **Perche' un filtro di sola posizione e non un Alpha-Beta completo.** Il report esterno
    /// raccomanda l'Alpha-Beta, che stima *posizione e velocita'*. Il termine di velocita' qui non
    /// ha un significato fisico: la posizione proiettata alla bandiera e' la previsione di un
    /// valore **finale e fisso**, non una grandezza in movimento. Misurata sul replay, deriva di
    /// ~0.5 giri in 900 secondi; con la costante di tempo scelta qui il ritardo che ne consegue
    /// vale <c>4 s x 0.5/900 = 0.002 giri</c>, cioe' due millesimi di giro — quattro ordini di
    /// grandezza sotto la banda di 0.05 che deve superare per contare. Un termine di velocita'
    /// non correggerebbe nulla di misurabile e in compenso estrapolerebbe il rumore. Si e'
    /// quindi implementato il caso <c>beta = 0</c>, che e' un Alpha-Beta degenere, e lo si dichiara
    /// invece di lasciarlo intendere. Se un replay futuro mostrera' un ritardo osservabile, il
    /// termine va aggiunto **con quella misura in mano**, non per completezza formale.
    /// </summary>
    public class ProjectionStabilizer
    {
        /// <summary>
        /// Costante di tempo del lisciamento, in secondi **di gara**.
        ///
        /// Il tempo si misura sul cronometro di sessione e non sull'orologio di sistema: un replay
        /// a 3x comprime tre secondi di gara in uno reale, e un filtro tarato sull'orologio si
        /// comporterebbe in modo diverso a ogni velocita' di riproduzione. Tarare su una grandezza
        /// che cambia con la velocita' del replay significa non poter confrontare due run.
        /// </summary>
        public const double SmoothingTauRaceSec = 4.0;

        /// <summary>
        /// Costante di tempo dopo che un cambio e' stato **confermato**: si aggancia in fretta.
        /// </summary>
        public const double CatchUpTauRaceSec = 1.0;

        /// <summary>
        /// Salto istantaneo oltre il quale la misura e' sospetta, in giri.
        ///
        /// Il limite non e' arbitrario: la formula del tempo alla bandiera contiene un dente di
        /// sega **legittimo** di ampiezza un giro del leader — quando il momento della bandiera
        /// scivola oltre un giro intero, la gara dura davvero un giro del leader in piu'. A Road
        /// Atlanta quel dente vale <c>68.4 / 76.5 = 0.89</c> giri del Player, ed e' un dato vero
        /// che non va filtrato. La soglia sta sopra: 1.5 giri lascia passare il dente di sega e
        /// intercetta l'artefatto, che nel replay valeva <b>oltre 2.2 giri in un fotogramma</b>.
        /// </summary>
        public const double SuspectJumpLaps = 1.5;

        /// <summary>
        /// Per quanti secondi **di gara** un salto sospetto deve persistere, nella stessa
        /// direzione, prima di essere accettato come cambio vero invece che come artefatto.
        ///
        /// Quindici secondi su una gara da 45 minuti. Il costo, se il cambio e' vero, e' di
        /// riconoscerlo quindici secondi piu' tardi su un numero che serve a decidere il
        /// carburante e che si muove su scala di minuti: nullo in pratica. Il beneficio e' che
        /// **qualunque** artefatto piu' corto di quindici secondi viene ignorato per intero,
        /// invece che assorbito in parte.
        /// </summary>
        public const double ChangeConfirmRaceSec = 15.0;

        /// <summary>
        /// Tetto sul passo del tempo, in secondi di gara. Un intervallo piu' lungo non e' un tick
        /// lento: e' un salto di sessione, una pausa, o un replay riavvolto. Lisciare attraverso un
        /// buco simile darebbe alla misura successiva un peso quasi pieno senza motivo.
        /// </summary>
        public const double MaxStepRaceSec = 10.0;

        private double _estimate;
        private bool _hasEstimate;

        // Stato del riconoscimento dei cambi: da quanti secondi di gara dura il sospetto, e in che
        // direzione. Il segno serve perche' un rumore che salta avanti e indietro non e' un cambio:
        // solo una deviazione **coerente** lo e'.
        private double _suspicionRaceSec;
        private int _suspicionSign;

        /// <summary>La stima lisciata. Zero finche' non e' arrivata la prima misura.</summary>
        public double Estimate { get { return _estimate; } }

        /// <summary>Falso finche' non e' arrivata la prima misura utilizzabile.</summary>
        public bool HasEstimate { get { return _hasEstimate; } }

        /// <summary>
        /// Da quanti secondi di gara e' in corso un sospetto non ancora confermato. Zero quando la
        /// misura e' nella norma. Esposto perche' finisca a log: e' il campo che dice se un salto
        /// e' stato scartato o assorbito, e senza di lui la decisione del filtro non e' osservabile.
        /// </summary>
        public double SuspicionRaceSec { get { return _suspicionRaceSec; } }

        /// <summary>Azzera tutto. Da chiamare al cambio di sessione.</summary>
        public void Reset()
        {
            _estimate = 0.0;
            _hasEstimate = false;
            _suspicionRaceSec = 0.0;
            _suspicionSign = 0;
        }

        /// <summary>
        /// Assorbe una misura e restituisce la stima aggiornata.
        ///
        /// La prima misura viene presa cosi' com'e': senza uno stato precedente non esiste
        /// innovazione da pesare, e partire da zero introdurrebbe un transitorio che non
        /// corrisponde a nulla.
        /// </summary>
        /// <param name="measuredPosAtFlag">Posizione proiettata alla bandiera, continua.</param>
        /// <param name="dtRaceSec">
        /// Secondi **di gara** trascorsi dalla misura precedente. Zero o negativo = il cronometro
        /// non e' avanzato, quindi non c'e' informazione nuova: la stima resta dov'e'.
        /// </param>
        public double Update(double measuredPosAtFlag, double dtRaceSec)
        {
            if (measuredPosAtFlag <= 0.0 || double.IsNaN(measuredPosAtFlag)) return _estimate;

            if (!_hasEstimate)
            {
                _estimate = measuredPosAtFlag;
                _hasEstimate = true;
                _suspicionRaceSec = 0.0;
                _suspicionSign = 0;
                return _estimate;
            }

            if (dtRaceSec <= 0.0) return _estimate;
            double dt = Math.Min(dtRaceSec, MaxStepRaceSec);

            double innovation = measuredPosAtFlag - _estimate;
            double tau = SmoothingTauRaceSec;

            if (Math.Abs(innovation) > SuspectJumpLaps)
            {
                int sign = innovation > 0.0 ? 1 : -1;

                if (_suspicionSign == sign) _suspicionRaceSec += dt;
                else { _suspicionSign = sign; _suspicionRaceSec = dt; }

                // Sospetto ancora aperto: la misura non entra affatto. Assorbirne una frazione
                // significherebbe farsi spostare comunque da un artefatto, solo piu' lentamente —
                // ed e' precisamente il modo in cui il difetto vecchio arrivava a 37.
                if (_suspicionRaceSec < ChangeConfirmRaceSec) return _estimate;

                // Persistente e coerente: non e' rumore, e' un cambio. Ci si aggancia in fretta.
                tau = CatchUpTauRaceSec;
            }
            else
            {
                _suspicionRaceSec = 0.0;
                _suspicionSign = 0;
            }

            double alpha = 1.0 - Math.Exp(-dt / tau);
            _estimate += alpha * innovation;
            return _estimate;
        }
    }
}
