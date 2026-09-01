// -------------------------------------------------------------------------
// FILE: RaceTimeProjection.cs
// Quanto manca alla bandiera a scacchi in una gara a tempo, e quanti giri
// ci stanno dentro. Nessuna dipendenza SimHub: la matematica e' verificabile
// dai test.
// -------------------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace SimRIG
{
    /// <summary>
    /// Proiezioni temporali di una gara a tempo.
    ///
    /// **Il difetto che questa classe chiude.** Prima, il tempo rimanente veniva *ricostruito*
    /// dal leader invece che letto dal cronometro di sessione:
    /// <code>RaceLifeTimeLeftSec = leaderLapsRem * leaderPace + leaderRemainingPitTime</code>
    /// dove <c>leaderLapsRem</c> derivava a sua volta dal tempo diviso per lo stesso passo, ed era
    /// per giunta *latchato* (appiccicoso). Il giro completo tempo -> giri -> tempo e' l'identita'
    /// solo finche' il passo resta lo stesso fra andata e ritorno: quando il passo del leader
    /// cambiava di colpo — in multiclasse succede a ogni cambio di identita' del P1 assoluto — il
    /// conteggio dei giri restava latchato al valore vecchio e veniva rimoltiplicato per il passo
    /// nuovo. Misurato sul replay Daytona del 2026-08-23: ai giri 12-15 il tempo residuo risultava
    /// 2368 s contro i ~1400 s reali, e i giri rimanenti del Player ne uscivano quasi raddoppiati
    /// (22 invece di ~10). Da li' passava dritto in <c>FuelToAdd</c>.
    ///
    /// **Il criterio adottato.** Il cronometro di sessione e' un dato di telemetria, non una stima:
    /// si parte da quello. Il leader serve solo per la regola del giro extra — a tempo scaduto la
    /// bandiera esce quando il leader taglia il traguardo, quindi la gara dura ancora la frazione
    /// di giro che gli manca. Cosi' l'errore sul passo del leader non scala piu' con la durata
    /// della gara: e' **limitato a un giro del leader**, sempre.
    /// </summary>
    public static class RaceTimeProjection
    {
        /// <summary>
        /// Velocita' media su giro oltre la quale un tempo sul giro non e' credibile, in m/s.
        /// 110 m/s sono 396 km/h *di media sul giro intero*: nessuna vettura da circuito ci arriva,
        /// nemmeno su un ovale veloce. Volutamente generosa — serve a scartare la spazzatura
        /// (un giro parziale contato come intero), non a giudicare il passo.
        ///
        /// Distinta da <see cref="TrackPositionValidator.MaxPlausibleSpeedMs"/> (120 m/s): li' e' un
        /// tetto sulla velocita' *istantanea* fra due campioni, qui e' una media su un giro intero.
        /// </summary>
        public const double MaxPlausibleLapAverageSpeedMs = 110.0;

        /// <summary>
        /// Tempo che manca alla bandiera a scacchi, in secondi.
        ///
        /// Ancorato al cronometro reale: allo scadere del tempo il leader si trova a
        /// <c>leaderAbsolutePos + sessionTimeLeftSec / leaderPaceSec</c>, e per prendere la bandiera
        /// deve completare quel giro. Il supplemento vale quindi
        /// <c>(1 - frazione percorsa) * leaderPaceSec</c> ed e' per costruzione dentro
        /// <c>[0, leaderPaceSec)</c>.
        /// </summary>
        /// <param name="sessionTimeLeftSec">Countdown di sessione. Negativo o zero = gia' scaduto.</param>
        /// <param name="leaderAbsolutePos">Posizione assoluta del leader: giri completati + frazione di giro.</param>
        /// <param name="leaderPaceSec">Passo del leader. Se non credibile, il supplemento non si applica.</param>
        public static double TimeUntilLeaderCheckered(double sessionTimeLeftSec,
                                                      double leaderAbsolutePos,
                                                      double leaderPaceSec)
        {
            double timeLeft = Math.Max(0.0, sessionTimeLeftSec);

            // Senza un passo utilizzabile il supplemento non e' calcolabile. Si restituisce il
            // countdown nudo invece di inventare una frazione di giro: sbagliare per difetto di
            // meno di un giro e' preferibile a moltiplicare per un numero a caso.
            if (leaderPaceSec <= 0.0) return timeLeft;

            double posAtExpiry = Math.Max(0.0, leaderAbsolutePos) + timeLeft / leaderPaceSec;

            // Frazione di giro che manca al leader per tagliare il traguardo dopo lo scadere.
            double lapFractionToComplete = Math.Ceiling(posAtExpiry) - posAtExpiry;

            // Esattamente sul traguardo: il giro e' gia' chiuso, nessun supplemento.
            if (lapFractionToComplete >= 1.0) lapFractionToComplete = 0.0;

            return timeLeft + lapFractionToComplete * leaderPaceSec;
        }

        /// <summary>
        /// Densita' della benzina da competizione, in kg per litro.
        ///
        /// Esiste come costante dichiarata perche' la sua **assenza** era un difetto (Y-43): la
        /// penalita' di peso si applicava come <c>litri × coefficiente</c>, ma il coefficiente
        /// standard del motorsport e' in **secondi per chilogrammo**, non per litro. Mancando la
        /// conversione, la penalita' risultava sovrastimata del 33% ovunque venisse applicata.
        ///
        /// Il valore e' quello convenzionale per la benzina da competizione. Non e' una costante
        /// universale — varia con la temperatura e la formulazione — ma un errore del 2-3% qui e'
        /// irrilevante rispetto al 33% che si correggeva.
        /// </summary>
        public const double FuelDensityKgPerLitre = 0.75;

        /// <summary>
        /// Quanto rallenta un giro il carburante a bordo, in secondi.
        ///
        /// **L'unita' del coefficiente e' secondi per chilogrammo**, che e' la convenzione del
        /// motorsport (~0.03 s/kg, cioe' 0.3 s per giro ogni 10 kg). Il carburante lo misuriamo
        /// in litri, quindi la conversione va fatta qui e non puo' restare implicita: e' esattamente
        /// l'omissione che ha prodotto Y-43.
        ///
        /// Tenere il coefficiente in s/kg significa anche che il valore esposto nelle impostazioni
        /// coincide con quello che si trova in qualunque fonte di ingegneria di pista, invece di
        /// essere un numero che vale solo dentro questo plugin.
        /// </summary>
        /// <param name="fuelLitres">Carburante a bordo, in litri.</param>
        /// <param name="coefSecPerKg">Sensibilita' alla massa, in secondi per chilogrammo.</param>
        public static double FuelWeightPenaltySec(double fuelLitres, double coefSecPerKg)
        {
            if (fuelLitres <= 0.0 || coefSecPerKg <= 0.0) return 0.0;
            return fuelLitres * FuelDensityKgPerLitre * coefSecPerKg;
        }

        /// <summary>
        /// Tempo sul giro piu' breve fisicamente possibile su questo tracciato.
        /// Lunghezza non nota (zero o negativa) = nessun giudizio possibile: si restituisce 0,
        /// cioe' un limite che non scarta nulla. Stessa scelta del fallback di MaxSectorFraction.
        /// </summary>
        public static double MinimumPlausibleLapSec(double trackLengthMeters)
        {
            if (trackLengthMeters <= 0.0) return 0.0;
            return trackLengthMeters / MaxPlausibleLapAverageSpeedMs;
        }

        /// <summary>
        /// Un tempo sul giro e' fisicamente possibile su questo tracciato?
        /// Con lunghezza pista non nota risponde sempre true: meglio nessun giudizio che uno
        /// basato su una lunghezza inventata.
        /// </summary>
        public static bool IsPhysicallyPlausibleLap(double paceSec, double trackLengthMeters)
        {
            if (paceSec <= 0.0) return false;
            double floor = MinimumPlausibleLapSec(trackLengthMeters);
            if (floor <= 0.0) return true;
            return paceSec >= floor;
        }

        /// <summary>
        /// Quanto indietro puo' stare una vettura, in giri, e restare in lotta per la bandiera.
        ///
        /// Un giro: chi e' doppiato non fa uscire la bandiera. Il margine non e' stretto perche' non
        /// deve discriminare fra contendenti — deve solo escludere i doppiati, e una sosta ancora da
        /// fare costa ~0.5 giri, che deve restare dentro.
        /// </summary>
        public const double LeadLapMarginLaps = 1.0;

        /// <summary>Una vettura come la vede il calcolo del momento della bandiera.</summary>
        public struct CrossingCandidate
        {
            /// <summary>Nome, solo per poter dire a log **chi** ha vinto il minimo.</summary>
            public string Name;

            /// <summary>Posizione assoluta: giri completati piu' frazione di giro in corso.</summary>
            public double AbsolutePos;

            /// <summary>Passo stimato della vettura, in secondi.</summary>
            public double PaceSec;
        }

        /// <summary>Esito di <see cref="EarliestCheckeredTime"/>.</summary>
        public struct EarliestCheckered
        {
            /// <summary>Secondi che mancano alla bandiera. Zero se nessuna vettura era valutabile.</summary>
            public double TimeSec;

            /// <summary>Chi taglia per primo dopo lo scadere del cronometro.</summary>
            public string WinnerName;

            /// <summary>Passo della vettura che ha vinto il minimo, per riconoscerne uno assurdo.</summary>
            public double WinnerPaceSec;

            /// <summary>Quante vetture sono entrate nel confronto.</summary>
            public int Considered;

            /// <summary>Quante sono state scartate dal limite di plausibilita' sul passo.</summary>
            public int RejectedByFloor;

            /// <summary>
            /// Quante, fra quelle valutate, sono in lotta per la bandiera — cioe' entro un giro dal
            /// massimo proiettato. Solo fra queste si prende il minimo.
            /// </summary>
            public int Contenders;

            /// <summary>La posizione proiettata piu' alta allo scadere del cronometro.</summary>
            public double MaxProjectedPos;

            /// <summary>Vero se almeno una vettura era valutabile.</summary>
            public bool HasResult;
        }

        /// <summary>
        /// Il momento della bandiera come **minimo del tempo di attraversamento fra le vetture in
        /// lotta**: chi taglia per primo dopo lo scadere del cronometro *e'* il leader, per
        /// definizione, qualunque classe abbia — purche' sia sul giro del leader.
        ///
        /// **Perche' non basta il P1 di adesso** (Y-38, e la causa misurata del totale a 37).
        /// Oggi si proietta la sola vettura che in questo istante e' prima. Quando l'identita'
        /// cambia — per un sorpasso, per una sosta, o per lo sfarfallio del dato — la vettura di
        /// riferimento cambia **di colpo**, e con lei cambiano di colpo passo e posizione dentro
        /// una formula che contiene un arrotondamento all'intero. Road Atlanta `20260831_195300`,
        /// ore 20:04:08: il P1 passa a una vettura con passo registrato **278.563 s** invece di
        /// ~68, e il supplemento di fine gara — che vale al massimo un giro del leader — passa da
        /// ~68 a fino a 278 secondi. Sul Player sono <c>278.563 / 76.524 = 3.6 giri</c> in un solo
        /// fotogramma.
        ///
        /// Col minimo quella vettura non viene semplicemente scelta: un passo di 278 s significa
        /// attraversare **tardi**, e chi attraversa tardi perde il minimo. Sugli stessi numeri —
        /// countdown 922.5 s — la vettura lenta da' 1112.1 s e il vero leader 934.3 s: il minimo e'
        /// 934.3, e la proiezione del Player resta 34.28 invece di saltare a 37.
        ///
        /// **Il minimo va preso solo fra chi e' in lotta per la bandiera, e questa non era l'idea
        /// iniziale: l'ha imposta la misura.** La prima versione prendeva il minimo su *tutte* le
        /// vetture. Girata in modalita' ombra su Road Atlanta `20260831_222417` ha dato, su 910
        /// campioni, un valore **piu' basso di quello in uso 867 volte**, mediana **-28.9 s** (circa
        /// -0.4 giri) con code fino a -169 s. E il vincitore ruotava fra una quarantina di vetture,
        /// **Player compreso**.
        ///
        /// La ragione e' semplice e non dipende dal circuito: con 43 vetture in pista, in ogni
        /// istante *qualcuna* si trova a pochi metri dal traguardo. Il minimo su tutte collassa
        /// quindi sul countdown nudo, e il giro finale del leader — la parte che fa uscire la
        /// bandiera **dopo** lo scadere — sparisce del tutto.
        ///
        /// L'errore di ragionamento era su cosa distingue le vetture. Non e' la classe: e' il
        /// **conteggio giri**. Una vettura **doppiata** che taglia il traguardo non fa finire la
        /// gara. Da qui la restrizione: si guarda dove sara' ciascuno allo scadere, si prende il
        /// massimo, e si tengono solo quelli entro <see cref="LeadLapMarginLaps"/> da li'.
        ///
        /// La restrizione **non toglie nulla** al motivo per cui il minimo serve: fra i contendenti
        /// il passaggio resta continuo, perche' chi deve ancora fermarsi vede il proprio
        /// attraversamento spostarsi in avanti e cede il minimo prima del sorpasso fisico. E copre
        /// il caso che l'ha motivata: una vettura con passo 278 s proietta ~28 giri contro i 39 del
        /// massimo, quindi **esce da sola** dall'insieme senza bisogno di riconoscerla come anomala.
        ///
        /// **L'asimmetria che rende il criterio sicuro, e il suo rovescio.** Un passo sbagliato per
        /// eccesso di lentezza sposta la vettura in fondo alla classifica del minimo, quindi e'
        /// innocuo. Un passo sbagliato per eccesso di **velocita'** puo' vincere il minimo e
        /// anticipare la bandiera per tutti. Misurato: su 877 campioni e 42 vetture, zero passi
        /// sotto i 66 s (implausibili qui) e sei sopra i 100 s — **tutti gli errori sono nella
        /// direzione innocua**; e nel replay `222417` il limite di plausibilita' non ha scartato
        /// nessuno, **0 volte su 910**. Ma "tutti" vale per un circuito e due replay: da qui
        /// <paramref name="paceFloorSec"/>, che scarta chi dichiara un passo piu' veloce del piu'
        /// veloce giro realmente girato nella sessione. E' un limite osservato, non un parametro
        /// da tarare.
        ///
        /// Le soste ancora da fare **non** entrano qui. La formula corretta le somma al tempo di
        /// attraversamento di chi deve ancora fermarsi, ma il nostro tempo di sosta e' noto essere
        /// sovrastimato del 49% (Y-44): aggiungerlo adesso significherebbe rimpiazzare un errore
        /// misurato con uno altrettanto grande e non misurato. Va aggiunto dopo Y-44, e il difetto
        /// per cui il minimo serve adesso — la vettura col passo assurdo — non ne ha bisogno.
        /// </summary>
        /// <param name="candidates">Tutte le vetture in pista, Player compreso.</param>
        /// <param name="sessionTimeLeftSec">Countdown di sessione.</param>
        /// <param name="paceFloorSec">
        /// Passo piu' veloce ammissibile. Zero = nessun limite (nessun giro di riferimento ancora
        /// osservato): meglio nessun giudizio che uno basato su un limite inventato — stessa scelta
        /// di <see cref="IsPhysicallyPlausibleLap"/>.
        /// </param>
        public static EarliestCheckered EarliestCheckeredTime(IEnumerable<CrossingCandidate> candidates,
                                                              double sessionTimeLeftSec,
                                                              double paceFloorSec)
        {
            var result = new EarliestCheckered();
            result.WinnerName = "";
            if (candidates == null) return result;

            // Prima passata: chi e' valutabile, e dove sara' ciascuno allo scadere del cronometro.
            var usable = new List<CrossingCandidate>();
            var projectedPos = new List<double>();
            double timeLeft = Math.Max(0.0, sessionTimeLeftSec);

            foreach (var car in candidates)
            {
                if (car.PaceSec <= 0.0 || double.IsNaN(car.PaceSec)) continue;
                if (car.AbsolutePos < 0.0 || double.IsNaN(car.AbsolutePos)) continue;

                if (paceFloorSec > 0.0 && car.PaceSec < paceFloorSec)
                {
                    result.RejectedByFloor++;
                    continue;
                }

                double pos = car.AbsolutePos + timeLeft / car.PaceSec;
                usable.Add(car);
                projectedPos.Add(pos);
                result.Considered++;

                if (usable.Count == 1 || pos > result.MaxProjectedPos) result.MaxProjectedPos = pos;
            }

            if (usable.Count == 0) return result;

            // Seconda passata: il minimo, ma **solo fra chi e' in lotta per la bandiera**.
            double leadLapThreshold = result.MaxProjectedPos - LeadLapMarginLaps;

            for (int i = 0; i < usable.Count; i++)
            {
                if (projectedPos[i] <= leadLapThreshold) continue;
                result.Contenders++;

                double crossing = TimeUntilLeaderCheckered(sessionTimeLeftSec,
                                                           usable[i].AbsolutePos,
                                                           usable[i].PaceSec);

                if (!result.HasResult || crossing < result.TimeSec)
                {
                    result.HasResult = true;
                    result.TimeSec = crossing;
                    result.WinnerName = usable[i].Name ?? "";
                    result.WinnerPaceSec = usable[i].PaceSec;
                }
            }

            return result;
        }

        /// <summary>
        /// Giri totali che una vettura avra' completato quando esce la bandiera, dato il tempo che
        /// manca alla bandiera stessa (uguale per tutti: la gara finisce quando il leader assoluto
        /// taglia) e il passo di *quella* vettura.
        ///
        /// Si arrotonda per eccesso perche' il giro in corso allo scadere va comunque completato.
        /// </summary>
        public static double ProjectedTotalLaps(double timeUntilCheckeredSec,
                                                double absolutePos,
                                                double paceSec)
        {
            if (paceSec <= 0.0) return Math.Ceiling(Math.Max(0.0, absolutePos));

            double lapsLeft = Math.Max(0.0, timeUntilCheckeredSec) / paceSec;
            return Math.Ceiling(Math.Max(0.0, absolutePos) + lapsLeft);
        }
    }
}
