// -------------------------------------------------------------------------
// FILE: CalibrationCascadeRunner.cs
// Y-28 fase 3: decide QUANDO l'ingegnere parla.
// CalibrationCascade sa quale passo manca; questo sa se annunciarlo adesso.
// Nessuna dipendenza SimHub: la decisione e' verificabile dai test.
// -------------------------------------------------------------------------

using System;

namespace SimRIG
{
    /// <summary>
    /// Tiene il filo della cascata fra un giro e l'altro, e decide quando l'ingegnere deve parlare.
    ///
    /// **Perche' l'insistenza e' legata al progresso e non al tempo.** Ripetere l'istruzione ogni
    /// N secondi, o ogni giro a prescindere, significherebbe parlare sopra al pilota mentre sta
    /// gia' eseguendo quello che gli e' stato chiesto. Qui si ripete **solo** se un giro e' passato
    /// senza che la cascata sia avanzata: se stai eseguendo l'ingegnere tace, se ti sei distratto
    /// ti richiama.
    ///
    /// **Perche' un limite alle ripetizioni.** Un pilota che ignora deliberatamente la calibrazione
    /// non deve sentirsi ripetere la stessa frase per venti giri. Dopo alcune ripetizioni si tace
    /// fino alla sessione successiva — dove si ricomincia, perche' entrare in una Practice nuova
    /// e' di per se' il segnale che si e' li' per lavorare.
    /// </summary>
    public class CalibrationCascadeRunner
    {
        /// <summary>
        /// Quante volte ripetere la stessa istruzione prima di rinunciare per questa sessione.
        /// Tre: abbastanza da coprire una distrazione, poco da non diventare assillante.
        /// </summary>
        public const int MaxRepeatsPerStep = 3;

        private CalibrationStep _announcedStep = CalibrationStep.None;
        private int _announcedOnLap = -1;
        private int _repeatsForStep;

        /// <summary>Il passo che la cascata sta chiedendo adesso. <c>None</c> se non c'e' nulla da fare.</summary>
        public CalibrationStep CurrentStep { get; private set; }

        /// <summary>La cascata si e' appena completata: va dato l'annuncio di fine, una volta sola.</summary>
        public bool JustCompleted { get; private set; }

        private bool _wasWorking;

        /// <summary>
        /// Un tick. Restituisce <c>true</c> **solo** nell'istante in cui va pronunciato un annuncio,
        /// cosi' il chiamante non deve tenere conto di cosa ha gia' detto.
        /// </summary>
        public bool Update(bool isCalibrationSession, CalibrationNeeds needs,
                           bool hasGenuineLap, int currentLap)
        {
            JustCompleted = false;

            if (!isCalibrationSession)
            {
                // In gara o qualifica la cascata non esiste: si azzera, cosi' rientrando in
                // Practice si riparte pulito.
                Reset();
                return false;
            }

            CurrentStep = CalibrationCascade.NextStep(needs, hasGenuineLap);

            if (CurrentStep == CalibrationStep.None)
            {
                // Transizione da "stavo lavorando" a "non manca piu' nulla": e' la fine della
                // cascata, e va annunciata una volta sola.
                if (_wasWorking)
                {
                    _wasWorking = false;
                    _announcedStep = CalibrationStep.None;
                    JustCompleted = true;
                    return true;
                }
                return false;
            }

            _wasWorking = true;

            // Passo nuovo: si annuncia subito, e riparte il conteggio delle ripetizioni.
            if (CurrentStep != _announcedStep)
            {
                _announcedStep = CurrentStep;
                _announcedOnLap = currentLap;
                _repeatsForStep = 0;
                return true;
            }

            // Stesso passo di prima: si ripete solo se un giro e' passato senza avanzamento, e
            // solo per un numero limitato di volte.
            if (currentLap > _announcedOnLap && _repeatsForStep < MaxRepeatsPerStep)
            {
                _announcedOnLap = currentLap;
                _repeatsForStep++;
                return true;
            }

            return false;
        }

        public void Reset()
        {
            CurrentStep = CalibrationStep.None;
            _announcedStep = CalibrationStep.None;
            _announcedOnLap = -1;
            _repeatsForStep = 0;
            _wasWorking = false;
            JustCompleted = false;
        }
    }
}
