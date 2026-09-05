// -------------------------------------------------------------------------

// FILE: FuelManager.cs

// VERSION: Fix errori 26

// -------------------------------------------------------------------------

using System;

using System.Collections.Generic;

using System.Linq;



namespace SimRIG

{

    public enum FuelStrategyMode { Manual, Normal, Safe, Aggressive }



    public class FuelCalculations

    {

        public double AverageFuelPerLap { get; set; } = 0.0;

        public double LastLapFuelUsed { get; set; } = 0.0;

        public double FuelToAdd { get; set; } = 0.0;



        public double TankLapsRemaining { get; set; } = 99.0;

        public double FuelDelta { get; set; } = 0.0;

        public double PitRequiredNumber { get; set; } = 0.0;

        public bool IsPredictionValid { get; set; } = false;



        public double FuelPerLapTarget { get; set; } = 3.0;

        public bool IsTargetModeEnabled { get; set; } = false;

        public bool TargetManuallySet { get; set; } = false;



        /// <summary>
        /// Consumo per giro necessario ad arrivare in fondo con quello che si ha a bordo,
        /// **senza** un'altra sosta. Da non confondere con FuelPerLapTarget, che è il target
        /// impostato a mano dal pilota per il rifornimento (Y-1).
        /// Vale 0.0 quando la domanda non ha senso: previsione non valida o gara finita.
        /// </summary>
        public double FuelSaveTarget { get; set; } = 0.0;

        /// <summary>Frazione di consumo da tagliare per centrare FuelSaveTarget. Negativa se si è già a posto.</summary>
        public double FuelSavingRequired { get; set; } = 0.0;

        /// <summary>
        /// Se quel risparmio è realisticamente ottenibile guidando, invece che con una sosta.
        /// È il filtro che tiene fuori i casi assurdi: 100 giri alla fine con 25 litri a bordo
        /// non è un problema di stile di guida, è una sosta obbligata.
        /// </summary>
        public bool IsFuelSavingAchievable { get; set; } = false;



        public double UserFuelOffset { get; set; } = 0.0;

        public double FuelStep { get; set; } = 0.1;



        public FuelStrategyMode StrategyMode { get; set; } = FuelStrategyMode.Manual;

        public bool IsSyncModeEnabled { get; set; } = false;

    }



    public class FuelManager

    {

        /// <summary>
        /// Ampiezza della finestra **cronologica** su cui si misura la statistica. Ci entrano
        /// tutti i giri che hanno passato i controlli di contesto, accettati **e rifiutati**:
        /// e' l'accumularsi dei rifiuti che espelle i vecchi accettati e fa riaprire il filtro
        /// quando il consumo reale e' cambiato davvero (Y-50).
        /// </summary>
        public const int MAX_CLEAN_HISTORY_LAPS = 10;

        /// <summary>
        /// Convalida un valore di consumo carburante per giro utilizzando l'algoritmo
        /// Interquartile Range (IQR) e la tolleranza del 15% sulla media di irdashies (fuelCalculations.ts).
        /// </summary>
        public static bool ValidateFuelConsumptionIQR(double fuelUsed, IReadOnlyList<double> cleanHistory)
        {
            if (fuelUsed <= 0.0) return false;

            // Servono almeno 3 giri validi per la validazione statistica
            if (cleanHistory == null || cleanHistory.Count < 3) return true;

            var sorted = cleanHistory.OrderBy(x => x).ToList();
            int count = sorted.Count;

            // Indicizzazione intera esatta di irdashies: Math.Floor(length * 0.25) e Math.Floor(length * 0.75)
            int q1Index = (int)Math.Floor(count * 0.25);
            int q3Index = (int)Math.Floor(count * 0.75);

            double q1 = sorted[q1Index];
            double q3 = sorted[q3Index];
            double iqr = q3 - q1;

            const double factor = 2.0;
            double lowerBound = q1 - (factor * iqr);
            double upperBound = q3 + (factor * iqr);

            double mean = sorted.Average();
            double tolerance = mean * 0.15;

            bool isWithinIQR = fuelUsed >= lowerBound && fuelUsed <= upperBound;
            bool isWithinTolerance = Math.Abs(fuelUsed - mean) <= tolerance;

            return isWithinIQR || isWithinTolerance;
        }

        /// <summary>
        /// Massimo risparmio di carburante ottenibile guidando, come frazione del consumo.
        /// Oltre questa soglia il divario non si colma alzando il piede: si colma solo con una
        /// sosta, e proporre il fuel saving sarebbe un consiglio impossibile da eseguire.
        ///
        /// Il 15% è la stima consueta di quanto si recupera con lift-and-coast e short-shifting
        /// prima che il tempo perso superi quello di un rifornimento. Non viene da un replay:
        /// nessuna sessione finora ha una fase di fuel saving misurabile.
        /// </summary>
        public const double MaxAchievableFuelSaving = 0.15;

        /// <summary>
        /// Margine fisso della modalita' Normal, in litri.
        ///
        /// Prima era <c>consumo * 0.3</c>, cioe' proporzionale. Il valore fisso e' una scelta
        /// dell'utente e cambia il comportamento in due versi opposti: su una vettura da 4 L/giro
        /// il vecchio margine valeva 1.2 L e ora ne vale 0.6 (**meno** cuscinetto), su una da
        /// 1.5 L/giro valeva 0.45 e ora 0.6 (**piu'** cuscinetto). A Road Atlanta, dove il consumo
        /// misurato e' 2.25 L/giro, i due quasi coincidono (0.675 contro 0.6).
        /// </summary>
        public const double NormalMarginLitres = 0.6;

        /// <summary>
        /// Margine di sicurezza da sommare al fabbisogno, in litri, per modalita'.
        ///
        /// <c>Aggressive</c> non ne ha: il cuscinetto glielo da' gia' l'arrotondamento per
        /// eccesso, che vale fra 0 e 1 litro. <c>Safe</c> imbarca un giro intero.
        /// </summary>
        public static double MarginForMode(FuelStrategyMode mode, double consumption)
        {
            if (mode == FuelStrategyMode.Safe) return Math.Max(0.0, consumption);
            if (mode == FuelStrategyMode.Normal) return NormalMarginLitres;
            return 0.0;
        }

        /// <summary>
        /// Porta i litri da imbarcare a un intero, **sempre per eccesso** (Y-34).
        ///
        /// iRacing non accetta decimali nel rifornimento: finora il plugin mandava
        /// <c>#fuel 30.5l</c> e lasciava arrotondare al gioco, con una regola che non conosciamo.
        /// Il verso non e' simmetrico — un litro di troppo costa una frazione di secondo di
        /// sosta, un litro in meno significa restare a piedi — quindi si arrotonda sempre in su,
        /// in **tutte** le modalita'. Con questo, la rete di sicurezza su
        /// <see cref="MaxAchievableFuelSaving"/> prevista dallo schema originale di Y-34 non
        /// serve piu': non esiste piu' un percorso che arrotondi in difetto.
        ///
        /// Il tetto del serbatoio si applica **dopo** l'arrotondamento e sul suo intero inferiore:
        /// <c>MaxFuel</c> arriva dal gioco come frazionario (una GT3 puo' avere 63.7 L), e
        /// limitare a 63.7 dopo aver arrotondato a 64 rimetterebbe in circolo un decimale.
        /// </summary>
        /// <param name="rawLitres">Fabbisogno gia' comprensivo di margine e offset utente.</param>
        /// <param name="maxFuelCapacity">Capacita' del serbatoio, anche frazionaria.</param>
        public static double RoundFuelToAdd(double rawLitres, double maxFuelCapacity)
        {
            if (double.IsNaN(rawLitres) || double.IsInfinity(rawLitres)) return 0.0;

            double whole = Math.Ceiling(Math.Max(0.0, rawLitres));
            double capacityLimit = Math.Floor(Math.Max(0.0, maxFuelCapacity));
            return Math.Min(capacityLimit, whole);
        }

        /// <summary>Esito del calcolo di fuel saving (Y-1).</summary>
        public struct FuelSavingPlan
        {
            /// <summary>Litri per giro necessari ad arrivare in fondo senza un'altra sosta.</summary>
            public double Target;

            /// <summary>Frazione di consumo da tagliare. Negativa se si è già sotto il necessario.</summary>
            public double RequiredFraction;

            /// <summary>Se quel taglio è ottenibile guidando invece che con una sosta.</summary>
            public bool Achievable;
        }

        /// <summary>
        /// Quanto bisogna consumare per arrivare in fondo con quello che si ha a bordo, e se
        /// ha senso proporlo al pilota (Y-1).
        ///
        /// La divisione secca <c>carburante / giri</c> da sola produce consigli assurdi: su una
        /// endurance con 100 giri da fare e 25 litri a bordo darebbe 0.25 L/giro, un numero
        /// aritmeticamente vero e praticamente inutile — quella non è una scelta di guida, è una
        /// sosta obbligata. Due filtri lo rendono azionabile:
        ///   1. il taglio richiesto deve stare entro ciò che si ottiene alzando il piede;
        ///   2. deve mancare **una sola** sosta: con più rifornimenti davanti, risparmiare
        ///      carburante non evita nulla, sposta solo il problema.
        /// </summary>
        public static FuelSavingPlan ComputeFuelSaving(double currentFuel, double raceLapsRemaining,
                                                       double consumption, double pitsRequired)
        {
            var plan = new FuelSavingPlan();

            if (raceLapsRemaining <= 0.0 || consumption <= 0.0) return plan;

            plan.Target = currentFuel / raceLapsRemaining;
            plan.RequiredFraction = (consumption - plan.Target) / consumption;
            plan.Achievable = plan.RequiredFraction > 0.0
                              && plan.RequiredFraction <= MaxAchievableFuelSaving
                              && pitsRequired <= 1.0;
            return plan;
        }

        /// <summary>Un giro gia' passato dai controlli di contesto, con l'esito del filtro.</summary>
        private struct FuelLapSample
        {
            public double FuelUsed;
            public bool Accepted;
        }

        private readonly List<FuelLapSample> _recentLaps = new List<FuelLapSample>();

        /// <summary>Consumi dei soli giri accettati presenti nella finestra.</summary>
        public IReadOnlyList<double> FuelHistory => AcceptedSamples();

        /// <summary>Numero di giri nella finestra, accettati e rifiutati. Diagnostico.</summary>
        public int RecentLapCount => _recentLaps.Count;

        private List<double> AcceptedSamples()
        {
            var accepted = new List<double>(_recentLaps.Count);
            for (int i = 0; i < _recentLaps.Count; i++)
            {
                if (_recentLaps[i].Accepted) accepted.Add(_recentLaps[i].FuelUsed);
            }
            return accepted;
        }

        private void RecordLap(double fuelUsed, bool accepted)
        {
            _recentLaps.Add(new FuelLapSample { FuelUsed = fuelUsed, Accepted = accepted });
            if (_recentLaps.Count > MAX_CLEAN_HISTORY_LAPS) _recentLaps.RemoveAt(0);
        }

        private int _lastEvaluatedLap = -1;
        private double _fuelAtLapStart = 0.0;

        private bool _wasInPitLaneDuringLap = false;
        private bool _wasPreviousLapPit = false;
        private bool _isLapFullyGreen = true;

        public bool WasInPitLaneDuringLap => _wasInPitLaneDuringLap;
        public bool WasPreviousLapPit => _wasPreviousLapPit;
        public bool IsLapFullyGreen => _isLapFullyGreen;

        public FuelCalculations Calculations { get; private set; } = new FuelCalculations();

        public FuelManager() { }

        // Aggiunto LogManager
        public void Update(SessionState state, double raceLapsRemaining, double fuelCapacityInTireTime, LogManager log)
        {
            if (!state.IsGameRunning) return;

            if (state.CurrentLap != _lastEvaluatedLap)
            {
                if (_lastEvaluatedLap > 0 && state.CurrentLap > 1)
                {
                    double fuelUsed = _fuelAtLapStart - state.CurrentFuelLevel;
                    bool isOutLap = _wasPreviousLapPit;
                    bool isInLap = _wasInPitLaneDuringLap;
                    bool isGreen = _isLapFullyGreen;

                    bool isSanityOk = fuelUsed > 0.1 && fuelUsed < state.MaxFuelCapacity && state.Flag_Black == 0;

                    // `LastLapFuelUsed` e' una **misura**, non una statistica: va aggiornata anche
                    // quando il giro non e' rappresentativo (gialla, out-lap). L'unico caso in cui
                    // il numero non significa nulla e' l'in-lap: con un rifornimento parziale
                    // `fuelAtLapStart - fuelLevel` sottostima il consumo vero, e da li' finirebbe
                    // nell'allarme vocale FUEL_TARGET_ALERT (DataPluginDemo.cs:1152).
                    if (isSanityOk && !isInLap)
                    {
                        Calculations.LastLapFuelUsed = fuelUsed;
                    }

                    if (isSanityOk && !isInLap && !isOutLap && isGreen)
                    {
                        // La baseline sono i soli accettati **dentro la finestra**, non tutti gli
                        // accettati di sempre: quando il consumo reale cambia, i rifiuti riempiono
                        // la finestra ed espellono i vecchi accettati, finche' sotto i tre campioni
                        // ValidateFuelConsumptionIQR riapre da sola (Y-50).
                        bool accepted = ValidateFuelConsumptionIQR(fuelUsed, AcceptedSamples());
                        RecordLap(fuelUsed, accepted);

                        var acceptedNow = AcceptedSamples();

                        if (accepted && acceptedNow.Count > 0)
                        {
                            Calculations.AverageFuelPerLap = acceptedNow.Average();

                            if (!Calculations.TargetManuallySet && Calculations.AverageFuelPerLap > 0)
                            {
                                Calculations.FuelPerLapTarget = Calculations.AverageFuelPerLap;
                            }

                            log?.Log(LogModule.FUEL, LogType.EVENT, "Lap Fuel Consumption (Accepted)",
                                $"Lap {_lastEvaluatedLap} | Used: {fuelUsed:F2}L | Avg: {Calculations.AverageFuelPerLap:F2}L | Accepted: {acceptedNow.Count}/{_recentLaps.Count}");
                        }
                        else
                        {
                            log?.Log(LogModule.FUEL, LogType.EVENT, "Lap Fuel Outlier Rejected (IQR)",
                                $"Lap {_lastEvaluatedLap} | Used: {fuelUsed:F2}L | Avg: {(acceptedNow.Count > 0 ? acceptedNow.Average() : 0):F2}L | Accepted: {acceptedNow.Count}/{_recentLaps.Count}");
                        }
                    }
                    else
                    {
                        string reason = !isSanityOk ? "Sanity Failed" :
                                        isInLap ? "In-Lap / Pit Active" :
                                        isOutLap ? "Out-Lap" : "!Green Flag";

                        log?.Log(LogModule.FUEL, LogType.EVENT, "Lap Fuel Ignored",
                            $"Lap {_lastEvaluatedLap} | Used: {fuelUsed:F2}L | Reason: {reason}");
                    }

                    // Reset esplicito e pulito per il nuovo giro (come concordato con Claude)
                    _wasPreviousLapPit = _wasInPitLaneDuringLap;
                    _wasInPitLaneDuringLap = false;
                    _isLapFullyGreen = true;
                }

                _fuelAtLapStart = state.CurrentFuelLevel;
                _lastEvaluatedLap = state.CurrentLap;
            }

            // Accumulatori di stato ad ogni tick del frame (dopo eventuale cambio giro)
            if (state.IsInPitLane)
            {
                _wasInPitLaneDuringLap = true;
            }
            if (state.Flag_Yellow > 0)
            {
                _isLapFullyGreen = false;
            }



            double consumption = GetEffectiveConsumption();



            if (consumption > 0) Calculations.TankLapsRemaining = state.CurrentFuelLevel / consumption;

            else Calculations.TankLapsRemaining = 99.0;



            Calculations.IsPredictionValid = (state.CurrentLap > 1) && (consumption > 0);



            if (raceLapsRemaining > 0 && consumption > 0 && Calculations.IsPredictionValid)

            {

                double rawFuelNeededForRace = raceLapsRemaining * consumption;

                double rawFuelToAdd = rawFuelNeededForRace - state.CurrentFuelLevel;



                Calculations.FuelDelta = -rawFuelToAdd;

                Calculations.PitRequiredNumber = rawFuelToAdd > 0 ? Math.Ceiling(rawFuelToAdd / state.MaxFuelCapacity) : 0.0;

                var plan = ComputeFuelSaving(state.CurrentFuelLevel, raceLapsRemaining,
                                             consumption, Calculations.PitRequiredNumber);
                Calculations.FuelSaveTarget = plan.Target;
                Calculations.FuelSavingRequired = plan.RequiredFraction;
                Calculations.IsFuelSavingAchievable = plan.Achievable;



                double finalFuelToAdd = 0.0;



                if (Calculations.StrategyMode == FuelStrategyMode.Manual)

                {

                    finalFuelToAdd = Calculations.UserFuelOffset;

                }

                else

                {

                    double margin = MarginForMode(Calculations.StrategyMode, consumption);



                    finalFuelToAdd = rawFuelToAdd + margin + Calculations.UserFuelOffset;



                    if (Calculations.IsSyncModeEnabled && fuelCapacityInTireTime > 0)

                    {

                        double antiBallastLimit = finalFuelToAdd + (consumption * 2.0);

                        if (fuelCapacityInTireTime > antiBallastLimit) finalFuelToAdd = antiBallastLimit;

                        else finalFuelToAdd = fuelCapacityInTireTime;

                    }

                }



                // Y-34: intero, sempre per eccesso, in ogni modalita' — Manual compresa, perche'
                // il vincolo e' del gioco e non della strategia. Con FuelStep a 0.1 L un colpo di
                // encoder ora spesso non muove il risultato: e' il prezzo dichiarato dell'intero.
                Calculations.FuelToAdd = RoundFuelToAdd(finalFuelToAdd, state.MaxFuelCapacity);

            }

            else

            {

                Calculations.FuelToAdd = 0.0;

                Calculations.FuelDelta = 0.0;

                Calculations.PitRequiredNumber = 0.0;

                Calculations.FuelSaveTarget = 0.0;

                Calculations.FuelSavingRequired = 0.0;

                Calculations.IsFuelSavingAchievable = false;

            }

        }



        public double GetEffectiveConsumption() => Calculations.IsTargetModeEnabled ? Calculations.FuelPerLapTarget : Calculations.AverageFuelPerLap;



        public void AddUserOffset(double direction)

        {

            Calculations.UserFuelOffset = Math.Round(Calculations.UserFuelOffset + (direction * Calculations.FuelStep), 1);

        }



        public void ResetUserOffset() { Calculations.UserFuelOffset = 0.0; }



        public void SetFuelTarget(double val)

        {

            Calculations.FuelPerLapTarget = Math.Round(val, 2);

            Calculations.TargetManuallySet = true;

        }



        public void CycleStrategyMode()

        {

            int current = (int)Calculations.StrategyMode;

            current++;

            if (current > 3) current = 0;

            Calculations.StrategyMode = (FuelStrategyMode)current;

            ResetUserOffset();

        }



        public void CycleFuelStep()

        {

            if (Calculations.FuelStep == 0.1) Calculations.FuelStep = 1.0;

            else if (Calculations.FuelStep == 1.0) Calculations.FuelStep = 5.0;

            else Calculations.FuelStep = 0.1;

        }



        public void ResetSession()
        {
            _recentLaps.Clear();
            _lastEvaluatedLap = -1;
            _fuelAtLapStart = 0.0;
            _wasInPitLaneDuringLap = false;
            _wasPreviousLapPit = false;
            _isLapFullyGreen = true;
            Calculations = new FuelCalculations();
        }

    }

}

