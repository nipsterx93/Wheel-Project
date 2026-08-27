// -------------------------------------------------------------------------
// FILE: CalibrationCascade.cs
// Y-28: la regia che guida il pilota nelle calibrazioni.
// Sa sempre quale passo manca; non deduce cosa stia facendo l'utente, lo
// osserva. Nessuna dipendenza SimHub: la decisione e' verificabile dai test.
// -------------------------------------------------------------------------

using System;

namespace SimRIG
{
    /// <summary>Il passo che la cascata sta chiedendo in questo momento.</summary>
    public enum CalibrationStep
    {
        /// <summary>Niente da chiedere: tutto calibrato, oppure la sessione non e' adatta.</summary>
        None = 0,

        /// <summary>Serve prima un giro vero in pista: vedi <see cref="CalibrationCascade"/>.</summary>
        NeedGenuineLap,

        /// <summary>Un passaggio in corsia box senza fermarsi.</summary>
        DriveThrough,

        /// <summary>Una sosta con solo carburante, gomme su NONE.</summary>
        FuelOnlyStop,

        /// <summary>Una sosta con solo gomme, tutte e quattro.</summary>
        TyreStopAll4,

        /// <summary>Una sosta con solo gomme, due (un asse o un lato).</summary>
        TyreStopHalf,

        /// <summary>Una sosta con solo gomme, una sola.</summary>
        TyreStopSingle
    }

    /// <summary>
    /// Cosa manca per la combinazione Track+Class corrente. Fotografia del database, non stato.
    /// </summary>
    public struct CalibrationNeeds
    {
        /// <summary>Geofence della corsia box e tempo di drive-through: dati di **Track+Class**.</summary>
        public bool NeedsGeofence;

        /// <summary>Tempo di transito e acc/dec: dati di **Track+Class**.</summary>
        public bool NeedsTransit;

        /// <summary>Velocita' di erogazione del carburante: dato di **Classe**.</summary>
        public bool NeedsFuelRate;

        /// <summary>Tempo di cambio delle quattro gomme: dato di **Classe**.</summary>
        public bool NeedsTyreTime;

        /// <summary>Moltiplicatore per due gomme: dato di **Classe**, opzionale.</summary>
        public bool NeedsTyreHalfMultiplier;

        /// <summary>Moltiplicatore per una gomma: dato di **Classe**, opzionale.</summary>
        public bool NeedsTyreSingleMultiplier;
    }

    /// <summary>
    /// Decide quale calibrazione chiedere, e in che ordine.
    ///
    /// **Perche' una cascata e non un pulsante.** Un pulsante direbbe al plugin "sto per calibrare",
    /// ma poi il plugin dovrebbe comunque dedurre *quale* calibrazione stai facendo dalla firma
    /// della richiesta (i 20 litri esatti, le quattro gomme). La cascata invece sa in ogni momento
    /// quale passo si aspetta: non deve dedurre niente. E' anche piu' robusto — una richiesta di
    /// 20 litri capitata per caso in gara non puo' piu' essere scambiata per una calibrazione.
    ///
    /// **L'ordine non e' arbitrario.** Il drive-through viene per primo perche' ogni passo
    /// successivo attraversa comunque la corsia box: a cascata finita la geofence avra' ricevuto
    /// almeno un'osservazione per passo, raggiungendo da sola il consenso a tre
    /// (<see cref="CalibrationConsensus"/>) senza chiedere al pilota un solo giro in piu'.
    /// Le gomme vengono per ultime perche' All4 e' il denominatore dei moltiplicatori parziali:
    /// senza quello, 2 e 1 gomma non hanno un rapporto da calcolare.
    ///
    /// **Track+Class e Class si verificano separatamente**, perche' cosi' sono organizzati i dati:
    /// arrivando a un circuito nuovo con una classe gia' calibrata, la cascata chiede solo geofence
    /// e tempi del circuito e salta del tutto carburante e gomme.
    /// </summary>
    public static class CalibrationCascade
    {
        /// <summary>
        /// La sessione e' adatta a guidare una calibrazione?
        ///
        /// Non e' una domanda sulla **validita'** del dato — quella e' gia' coperta dai guard
        /// (tragitto genuino, traversata, plausibilita'), e vale in qualunque sessione: infatti
        /// l'apprendimento passivo da una sosta di gara continua a funzionare sempre.
        ///
        /// E' una domanda sull'**opportunita'**: se manca un dato a meta' gara, l'ultima cosa che
        /// serve al pilota e' l'ingegnere che gli chiede un drive-through mentre lotta ruota a
        /// ruota. Practice e Test sono semplicemente "ne' gara ne' qualifica": nessuna stringa da
        /// confrontare, nessun rischio di sbagliare le maiuscole.
        /// </summary>
        public static bool IsCalibrationSession(bool isRaceSession, bool isQualySession)
        {
            return !isRaceSession && !isQualySession;
        }

        /// <summary>
        /// Il prossimo passo da chiedere.
        ///
        /// <paramref name="hasGenuineLap"/> viene da <see cref="GeofenceCalibrationGate"/>: in
        /// Practice si parte fermi in piazzola, e un ingresso ai box che non arrivi da un tragitto
        /// vero registrerebbe la posizione della piazzola invece dell'ingresso corsia. Finche' non
        /// e' soddisfatto, l'unica cosa da chiedere e' un giro.
        /// </summary>
        public static CalibrationStep NextStep(CalibrationNeeds needs, bool hasGenuineLap)
        {
            if (!NeedsAnything(needs)) return CalibrationStep.None;

            if (!hasGenuineLap) return CalibrationStep.NeedGenuineLap;

            if (needs.NeedsGeofence) return CalibrationStep.DriveThrough;

            // Transito e carburante si misurano nella stessa sosta: una sola fermata a benzina
            // produce entrambi, quindi basta che ne manchi uno per chiederla.
            if (needs.NeedsTransit || needs.NeedsFuelRate) return CalibrationStep.FuelOnlyStop;

            if (needs.NeedsTyreTime) return CalibrationStep.TyreStopAll4;

            // I moltiplicatori vengono dopo il tempo All4, che e' il loro denominatore.
            if (needs.NeedsTyreHalfMultiplier) return CalibrationStep.TyreStopHalf;
            if (needs.NeedsTyreSingleMultiplier) return CalibrationStep.TyreStopSingle;

            return CalibrationStep.None;
        }

        /// <summary>C'e' almeno un dato da raccogliere?</summary>
        public static bool NeedsAnything(CalibrationNeeds needs)
        {
            return needs.NeedsGeofence
                || needs.NeedsTransit
                || needs.NeedsFuelRate
                || needs.NeedsTyreTime
                || needs.NeedsTyreHalfMultiplier
                || needs.NeedsTyreSingleMultiplier;
        }

        /// <summary>
        /// Il passo <paramref name="step"/> risulta soddisfatto dopo l'aggiornamento del database?
        ///
        /// Si guarda **cosa manca adesso**, non cosa l'utente dice di aver fatto: e' il criterio che
        /// rende la cascata tollerante alle deviazioni. Se l'ingegnere chiede un drive-through e il
        /// pilota fa invece il pieno, il passo del carburante risulta spuntato e la cascata prosegue
        /// da li' — nessun rifiuto, nessuna richiesta ripetuta a vuoto.
        /// </summary>
        public static bool IsStepSatisfied(CalibrationStep step, CalibrationNeeds needs)
        {
            switch (step)
            {
                case CalibrationStep.DriveThrough: return !needs.NeedsGeofence;
                case CalibrationStep.FuelOnlyStop: return !needs.NeedsTransit && !needs.NeedsFuelRate;
                case CalibrationStep.TyreStopAll4: return !needs.NeedsTyreTime;
                case CalibrationStep.TyreStopHalf: return !needs.NeedsTyreHalfMultiplier;
                case CalibrationStep.TyreStopSingle: return !needs.NeedsTyreSingleMultiplier;
                case CalibrationStep.NeedGenuineLap: return false;
                default: return true;
            }
        }

        /// <summary>
        /// Chiave vocale per il passo, o stringa vuota se non c'e' niente da annunciare.
        ///
        /// <paramref name="repeatIndex"/> distingue la **prima** richiesta dai solleciti: 0 e' la
        /// richiesta piena, 1 e 2 sono richiami progressivamente piu' asciutti. Un ingegnere vero
        /// non ripete la stessa frase parola per parola — la accorcia, perche' il contesto ormai
        /// e' condiviso. Ripeterla identica e' cio' che fa suonare il tutto come un automa.
        ///
        /// Oltre l'ultima variante si continua a restituire la piu' corta, invece di tornare alla
        /// frase lunga: se il chiamante decidesse di insistere ancora, deve farlo sottovoce.
        /// </summary>
        public static string VoiceKeyFor(CalibrationStep step, int repeatIndex = 0)
        {
            string baseKey;
            switch (step)
            {
                case CalibrationStep.NeedGenuineLap: baseKey = "CALIB_NEED_LAP"; break;
                case CalibrationStep.DriveThrough: baseKey = "CALIB_DT"; break;
                case CalibrationStep.FuelOnlyStop: baseKey = "CALIB_FUEL"; break;
                case CalibrationStep.TyreStopAll4: baseKey = "CALIB_TYRE"; break;
                case CalibrationStep.TyreStopHalf: baseKey = "CALIB_TYRE_HALF"; break;
                case CalibrationStep.TyreStopSingle: baseKey = "CALIB_TYRE_SINGLE"; break;
                default: return "";
            }

            if (repeatIndex <= 0) return baseKey + "_REQ";

            int variant = repeatIndex < MaxVoiceVariants ? repeatIndex : MaxVoiceVariants;
            return baseKey + "_R" + variant.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>Quante varianti di sollecito esistono per ogni passo, oltre alla richiesta piena.</summary>
        public const int MaxVoiceVariants = 2;
    }
}
