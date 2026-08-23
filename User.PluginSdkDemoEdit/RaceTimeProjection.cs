// -------------------------------------------------------------------------
// FILE: RaceTimeProjection.cs
// Quanto manca alla bandiera a scacchi in una gara a tempo, e quanti giri
// ci stanno dentro. Nessuna dipendenza SimHub: la matematica e' verificabile
// dai test.
// -------------------------------------------------------------------------

using System;

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
