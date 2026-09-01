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

        /// <summary>Esito di <see cref="ProjectFlagMoment"/>.</summary>
        public struct FlagMoment
        {
            /// <summary>
            /// Secondi che mancano alla bandiera: il tempo di attraversamento della vettura che
            /// sara' **al comando** allo scadere. Zero se nessuna vettura era valutabile.
            /// </summary>
            public double TimeSec;

            /// <summary>Chi sara' al comando quando esce la bandiera.</summary>
            public string LeaderName;

            /// <summary>Passo di quella vettura, per riconoscerne uno assurdo a log.</summary>
            public double LeaderPaceSec;

            /// <summary>Dove sara' quella vettura allo scadere del cronometro: e' il massimo.</summary>
            public double MaxProjectedPos;

            /// <summary>Quante vetture sono state valutate.</summary>
            public int Considered;

            /// <summary>Quante sono state scartate dal limite di plausibilita' sul passo.</summary>
            public int RejectedByFloor;

            /// <summary>
            /// Quante sono entro un giro dal massimo. **Diagnostico**: dice quanto e' contesa la
            /// testa della corsa, non entra nel calcolo.
            /// </summary>
            public int Contenders;

            /// <summary>
            /// Il vecchio criterio — minimo del tempo di attraversamento fra i contendenti —
            /// tenuto come **diagnostica di confronto**. Non usato. E' il valore che la misura del
            /// 2026-09-01 ha dimostrato impossibile: vedi <see cref="ProjectFlagMoment"/>.
            /// </summary>
            public double EarliestCrossingSec;

            /// <summary>Vero se almeno una vettura era valutabile.</summary>
            public bool HasResult;
        }

        /// <summary>
        /// Il momento in cui esce la bandiera: **il tempo di attraversamento della vettura che sara'
        /// al comando allo scadere del cronometro**, cioe' quella con la posizione proiettata piu'
        /// alta.
        ///
        /// **Perche' non il P1 di adesso.** Oggi si proietta la vettura che in questo istante e'
        /// prima. Quando l'identita' cambia — per un sorpasso, per una sosta, o per lo sfarfallio
        /// del dato (Y-38) — la vettura di riferimento cambia **di colpo**, e con lei cambiano di
        /// colpo passo e posizione dentro una formula che contiene un arrotondamento all'intero.
        /// Road Atlanta `20260831_195300`, ore 20:04:08: il P1 passa a una vettura con passo
        /// registrato **278.563 s** invece di ~68, e il supplemento — che vale al massimo un giro
        /// del leader — passa da ~68 a fino a 278 secondi: sul Player sono `278.563 / 76.524 =`
        /// **3.64 giri in un fotogramma**.
        ///
        /// Col massimo quella vettura non viene neppure considerata: con un passo di 278 s proietta
        /// **27.3 giri contro i 39** del massimo. **Non e' mai il massimo**, e non serve
        /// riconoscerne il passo come anomalo perche' se ne occupa il conteggio giri.
        ///
        /// **Perche' non il MINIMO del tempo di attraversamento, che era il disegno precedente.**
        /// Sembra equivalente — "chi taglia per primo dopo lo scadere e' il leader" — e non lo e'.
        /// Vale solo fra vetture sullo **stesso giro**. Se una vettura e' a inizio del giro 39 e
        /// un'altra a fine del giro 38, la seconda taglia prima, ma sta chiudendo il *suo* giro 38:
        /// non fa finire la gara.
        ///
        /// La modalita' ombra lo ha dimostrato con una grandezza che non dipende dal codice: **di
        /// quanto la bandiera esce dopo lo scadere del cronometro**. Allo scadere il leader e' a
        /// meta' giro in media, quindi su un giro da 69 s quel supplemento deve valere circa 35 s e
        /// non puo' quasi mai essere zero. Misurato su `20260901_175019`, 758 campioni di gara:
        ///
        /// <code>
        ///   criterio P1 di adesso        mediana 50.6 s   (da  6.1 a 67.1)
        ///   criterio minimo contendenti  mediana  5.2 s   (da  0.5 a 19.6)
        /// </code>
        ///
        /// Cinque secondi: la bandiera uscirebbe sullo scadere del cronometro, sempre. Con 5-8
        /// contendenti dentro una finestra di un giro ci sono sempre vetture a cavallo del confine,
        /// e il minimo pesca sistematicamente quella che sta chiudendo il giro precedente. Il
        /// vecchio valore resta calcolato in <see cref="FlagMoment.EarliestCrossingSec"/> come
        /// diagnostica di confronto.
        ///
        /// **Il limite di plausibilita' conta piu' di prima, non meno.** Col minimo, un passo
        /// sbagliato per eccesso di lentezza era innocuo. Col massimo si inverte il verso del
        /// pericolo: un passo falsamente **veloce** gonfia la posizione proiettata
        /// (<c>pos + T/passo</c>) e vincerebbe il massimo, anticipando la bandiera per tutti. Il
        /// massimo e' per sua natura sensibile a un solo campione sbagliato — e' il difetto di
        /// famiglia di questo repository (ADR-005) — quindi
        /// <paramref name="paceFloorSec"/> e' la sua unica protezione. Nel replay `20260901_175019`
        /// non ha mai scartato nessuno, 0 volte su 910: la rete c'e' e non ha ancora dovuto reggere
        /// nulla.
        ///
        /// Le soste ancora da fare **non** entrano. La formula del report esterno le somma al tempo
        /// di attraversamento di chi deve ancora fermarsi, ma il nostro tempo di sosta e' noto
        /// essere sovrastimato del 49% (Y-44): applicarlo a 43 avversari sostituirebbe
        /// un'ignoranza onesta con un errore sistematico moltiplicato per 43. Il prezzo di non
        /// farlo, detto esplicitamente: il passaggio di consegne fra un leader che deve ancora
        /// fermarsi e chi ha gia' finito avviene al sorpasso fisico invece che prima. E' la meta'
        /// della promessa del punto 4 che resta non mantenuta finche' Y-44 e' aperto.
        /// </summary>
        /// <param name="candidates">Tutte le vetture in pista, Player compreso.</param>
        /// <param name="sessionTimeLeftSec">Countdown di sessione.</param>
        /// <param name="paceFloorSec">
        /// Passo piu' veloce ammissibile. Zero = nessun giro di riferimento ancora osservato: meglio
        /// nessun giudizio che uno basato su un limite inventato — stessa scelta di
        /// <see cref="IsPhysicallyPlausibleLap"/>.
        /// </param>
        public static FlagMoment ProjectFlagMoment(IEnumerable<CrossingCandidate> candidates,
                                                   double sessionTimeLeftSec,
                                                   double paceFloorSec)
        {
            var result = new FlagMoment();
            result.LeaderName = "";
            if (candidates == null) return result;

            // Prima passata: chi e' valutabile, e dove sara' ciascuno allo scadere del cronometro.
            var usable = new List<CrossingCandidate>();
            var projectedPos = new List<double>();
            double timeLeft = Math.Max(0.0, sessionTimeLeftSec);
            int leaderIndex = -1;

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

                if (leaderIndex < 0 || pos > result.MaxProjectedPos)
                {
                    result.MaxProjectedPos = pos;
                    leaderIndex = usable.Count - 1;
                }
            }

            if (leaderIndex < 0) return result;

            // La bandiera esce quando **chi e' al comando** chiude il giro in corso allo scadere.
            result.HasResult = true;
            result.LeaderName = usable[leaderIndex].Name ?? "";
            result.LeaderPaceSec = usable[leaderIndex].PaceSec;
            result.TimeSec = TimeUntilLeaderCheckered(sessionTimeLeftSec,
                                                      usable[leaderIndex].AbsolutePos,
                                                      usable[leaderIndex].PaceSec);

            // Seconda passata, **solo diagnostica**: quanto e' contesa la testa, e cosa avrebbe
            // dato il vecchio criterio del minimo. Nessuno dei due entra nel risultato.
            double leadLapThreshold = result.MaxProjectedPos - LeadLapMarginLaps;
            bool anyContender = false;

            for (int i = 0; i < usable.Count; i++)
            {
                if (projectedPos[i] <= leadLapThreshold) continue;
                result.Contenders++;

                double crossing = TimeUntilLeaderCheckered(sessionTimeLeftSec,
                                                           usable[i].AbsolutePos,
                                                           usable[i].PaceSec);
                if (!anyContender || crossing < result.EarliestCrossingSec)
                {
                    anyContender = true;
                    result.EarliestCrossingSec = crossing;
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
