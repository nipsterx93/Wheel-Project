// -------------------------------------------------------------------------

// FILE: TargetStrategyManager.cs

// VERSION: Fix errori 41

// -------------------------------------------------------------------------

using System;

using System.Globalization;

using System.Linq;



namespace SimRIG

{

    public enum RelativePaceInvalidationReason
    {
        None,
        PlayerInPit,
        TargetInPit,
        DuplicateSector,
        InvalidSequence,
        MissingSector,
        DeltaTimeTooSmall,
        TargetChanged,
        NoPreviousSeed
    }

    public enum StrategyRejectReason
    {
        None,
        Position,
        TargetNotPitting,
        Fuel,
        Traffic,
        InsufficientStayLaps,
        InsufficientCaptureMargin,
        RaceTooLate,
        Multiple
    }

    public enum StrategyDecision
    {
        None,
        Undercut,
        Overcut,
        Neutral
    }

    public class TargetState

    {

        public string ModeLabel { get; set; } = "AHEAD";

        public string Name { get; set; } = "NO TARGET";

        public int ClassPosition { get; set; } = 0;

        public double GapSeconds { get; set; } = 0.0;

        public string GapString { get; set; } = "";

        public double SignedGapSeconds { get; set; } = 0.0;

        public string Diagnosis { get; set; } = "ANALYZING";



        /// <summary>
        /// LEGACY / DIAGNOSTICO. Passo relativo in secondi/giro, con EMA (α=0.30) e clamp ±10.
        /// Mantenuto invariato finché la dashboard non passa a <see cref="RelativeGapDelta"/>.
        /// </summary>
        public double RelativePace { get; set; } = 0.0;

        /// <summary>
        /// Variazione grezza del gap tra due macrosettori consecutivi puliti, in
        /// <b>secondi per macrosettore</b> (non secondi/giro).
        /// Negativo = il Player guadagna tempo sul Target; positivo = lo perde.
        /// Nessuna EMA, nessun clamp, nessuna normalizzazione temporale.
        /// Da leggere solo quando <see cref="RelativeGapDeltaValid"/> è true: fuori da quei
        /// momenti conserva l'ultima misura buona, che è stantia.
        /// </summary>
        public double RelativeGapDelta { get; set; } = 0.0;

        /// <summary>
        /// True solo sul macrosettore in cui il tracker ha prodotto un delta reale.
        /// False durante il pit, sul seed post-pit, su sequenza invalida, con dt &lt; 1s,
        /// al cambio target e prima del primo seed.
        /// </summary>
        public bool RelativeGapDeltaValid { get; set; } = false;

        public double TargetCurrentSpeed { get; set; } = 0.0;

        public double TargetTopSpeed { get; set; } = 0.0;

        public int CurrentMicrosector { get; set; } = 0;



        public double NormalizedRaceStartPace { get; set; } = 0.0;

        public double PaceDeficit { get; set; } = 0.0;

        public double RelativeDegradation { get; set; } = 0.0;

        public double TargetPaceDropDueToTyres { get; set; } = 0.0;

        public double TargetSectorPaceDropDueToTyres { get; set; } = 0.0;
        public double TargetSectorPaceDropDueToTyresRaw { get; set; } = 0.0;

        public double TargetLapBaselineNormalized { get; set; } = 0.0;
        public double TargetLapBaselineRaw { get; set; } = 0.0;
        public double TargetLapMovingAverageNormalized { get; set; } = 0.0;
        public double TargetLapMovingAverageRaw { get; set; } = 0.0;

        public double TargetSectorBaselineNormalized { get; set; } = 0.0;
        public double TargetSectorBaselineRaw { get; set; } = 0.0;
        public double TargetSectorMovingAverageNormalized { get; set; } = 0.0;
        public double TargetSectorMovingAverageRaw { get; set; } = 0.0;



        public bool UndercutViable { get; set; } = false;
        public bool OvercutViable { get; set; } = false;
        public double ProjectedMergeGap { get; set; } = 0.0;
        public bool TrafficAlert { get; set; } = false;

        public double UndercutAdvantage { get; set; } = 0.0;
        public double UndercutCaptureMargin { get; set; } = 0.0;
        public double OvercutAdvantage { get; set; } = 0.0;
        public double OvercutCaptureMargin { get; set; } = 0.0;

        public double TargetLapsUntilPit { get; set; } = 0.0;
        public double OvercutStayLaps { get; set; } = 0.0;
        public bool UndercutPositionOK { get; set; } = false;
        public bool UndercutFuelOK { get; set; } = false;
        public bool UndercutTrafficOK { get; set; } = false;
        public bool OvercutFuelOK { get; set; } = false;
        public bool OvercutTrafficOK { get; set; } = false;
        public bool TargetPittedRecently { get; set; } = false;

        public StrategyRejectReason UndercutRejectReason { get; set; } = StrategyRejectReason.None;
        public StrategyRejectReason OvercutRejectReason { get; set; } = StrategyRejectReason.None;
        public StrategyDecision Decision { get; set; } = StrategyDecision.None;

        public string TargetMode { get; set; } = "UNKNOWN";

        public int PitCount { get; set; } = 0;
        public double CurrentTank { get; set; } = 0.0;
        public double TankLapsRemaining { get; set; } = 0.0;

        public double SpeedDrop { get; set; } = 0.0;
        public double ReactionDeltaLaps { get; set; } = 0.0;



        public double PitLaneZoneRacingTime { get; set; } = 0.0;

        public double ProjectedStationaryTime { get; set; } = 0.0;

        public double CalculatedStationaryTime { get; set; } = 0.0;

        public double InOutPitAccDecTime { get; set; } = 0.0;

        public double EstimatedFuelToAdd { get; set; } = 0.0;
        public double FuelToAddTime { get; set; } = 0.0;

        public double EstimatedFuelAdded { get; set; } = 0.0;

        public double EstimatedStationaryTime { get; set; } = 0.0;

        public double EstimatedFuelTank { get; set; } = 0.0;

        public int LapCount { get; set; } = 0;

        public double EstimatedPitWindow { get; set; } = 0.0;

        public double EstimatedPitWindowTargetLap { get; set; } = 0.0;

        public double PrePitNormalizedAverage { get; set; } = 0.0;
        public double[] PostPitNormalizedDeltas { get; set; } = new double[3] { 99.0, 99.0, 99.0 };
        public double[] PostPitWarmupPenalties { get; set; } = new double[3] { 0.0, 0.0, 0.0 };
    }



    public class TargetStrategyManager

    {

        public TargetState CurrentTarget { get; private set; } = new TargetState();

        public string LatchedTargetName { get; set; } = null;

        private StrategyDecision _lastStrategyDecision = StrategyDecision.None;
        private bool _lastUndercutViable = false;
        private bool _lastOvercutViable = false;
        private int _snapshotTickCounter = 0;
        private bool _snapshotWidthWarned = false;





        private int _myLastMacroSector = -1;
        private int _lastLogSector = -1;
        private DateTime _lastMergeGapLogWallTime = DateTime.MinValue;

        private readonly RelativePaceTracker _relativePace = new RelativePaceTracker();

        // Ultimo campione processato, conservato per lo snapshot (spec §32).
        private RelativePaceSample _lastPaceSample;

        public TargetStrategyManager() { }

        /// <summary>
        /// Rilevamento pit del Player. Unica definizione condivisa: prima era triplicata,
        /// con il rischio che snapshot e gate divergessero.
        /// </summary>
        private static bool IsPlayerInPitLane(SessionState state)
        {
            return state.IsInPitLane
                || (state.TrackPositionPercent > 0.85 && state.SpeedKmh < 100.0 && state.SpeedKmh > 10.0);
        }

        // Formattatori dello snapshot CSV: cultura invariante, niente separatori decimali locali.
        private static string F1(double v) { return v.ToString("F1", CultureInfo.InvariantCulture); }
        private static string F3(double v) { return v.ToString("F3", CultureInfo.InvariantCulture); }
        private static string I(int v) { return v.ToString(CultureInfo.InvariantCulture); }
        private static string B(bool v) { return v ? "True" : "False"; }

        /// <summary>
        /// Formato HUD del delta gap. L'unità è <b>s/sector</b>, mai s/lap: confonderla con
        /// RelativePace significherebbe leggere un numero per macrosettore come se fosse per giro.
        /// Quando la misura non è valida non si stampa uno zero, che verrebbe letto come
        /// "nessuna variazione": si stampa un segnaposto.
        /// </summary>
        public static string FormatGapDelta(double delta, bool valid)
        {
            if (!valid) return "--.--s/sector";
            // Solo ASCII: la dash può usare font senza glifi estesi.
            string sign = delta > 0.0 ? "+" : string.Empty;
            return sign + delta.ToString("F2", CultureInfo.InvariantCulture) + "s/sector";
        }

        /// <summary>Un nome pilota con la virgola ("Rossi, Mario") sfonderebbe le colonne del CSV.</summary>
        private static string Csv(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Replace(',', ' ').Replace('\r', ' ').Replace('\n', ' ');
        }
        private static double W(double[] array, int index)
        {
            return (array != null && index < array.Length) ? array[index] : 0.0;
        }

        public double ComputeInterpolatedWarmup(double[] penalties, double laps)
        {
            if (penalties == null) return 0.0;
            int L = penalties.Length;
            double nPrime = Math.Max(0.0, Math.Min(laps, L));
            int I = (int)Math.Floor(nPrime);
            double F = nPrime - I;

            double sum = 0.0;
            for (int k = 0; k < I; k++)
            {
                sum += penalties[k];
            }
            if (I < L)
            {
                sum += F * penalties[I];
            }
            return sum;
        }



        public void Update(SessionState state, OpponentTracker tracker, PitRadar radar, FuelCalculations fuel, RaceAnalyzer raceAnalyzer, TyreManager tyres, string targetModeString, LogManager log, double fuelWeightCoef = 0.03)

        {
            var raceResult = raceAnalyzer.Results;
            if (!state.IsGameRunning || state.Opponents == null) return;



            double myPos = state.TrackPositionPercent;

            int myLap = state.CurrentLap;

            double trackLen = state.TrackLengthMeters;



            int macroSector = Math.Max(0, Math.Min(19, (int)(myPos * 20.0)));

            bool sectorChanged = (macroSector != _myLastMacroSector && _myLastMacroSector != -1);



            GameReaderCommon.Opponent targetOpp = null;
            bool targetIsPlayer = false;

            if (!string.IsNullOrEmpty(LatchedTargetName))
            {
                targetOpp = state.Opponents.FirstOrDefault(o => o.Name.Equals(LatchedTargetName, StringComparison.OrdinalIgnoreCase));
                if (targetOpp == null)
                {
                    LatchedTargetName = null;
                    targetOpp = SelectTarget(state, tracker, targetModeString, out targetIsPlayer);
                }
                else if (targetOpp.IsPlayer)
                {
                    targetIsPlayer = true;
                }
            }
            else
            {
                targetOpp = SelectTarget(state, tracker, targetModeString, out targetIsPlayer);
            }



            if (targetIsPlayer)

            {

                SetPlayerAsTarget(state, raceAnalyzer, radar, fuel);

                return;

            }



            if (targetOpp != null)

            {

                if (targetOpp.Name != CurrentTarget.Name)

                {

                    log.Log(LogModule.STRATEGY, LogType.EVENT, "Target Changed", $"Old: {CurrentTarget.Name} | New: {targetOpp.Name} | Mode: {targetModeString}");

                    // Anche nel canale STRATEGY_EVENT: chi analizza solo quel file deve poter
                    // vedere la causa del reset del RelativePace (spec §11).
                    log.Log(LogModule.STRATEGY_EVENT, LogType.EVENT, "RELATIVE_PACE_INVALIDATION",
                        $"reason={RelativePaceInvalidationReason.TargetChanged} | old={CurrentTarget.Name} | new={targetOpp.Name}");

                    CurrentTarget.Name = targetOpp.Name;

                    CurrentTarget.ClassPosition = targetOpp.Position;

                    // Reset completo, non invalidazione temporanea: il valore torna a 0.0 (spec §10).
                    _relativePace.Reset();
                    CurrentTarget.RelativePace = 0.0;
                    CurrentTarget.RelativeGapDelta = 0.0;
                    CurrentTarget.RelativeGapDeltaValid = false;
                    _lastPaceSample = default(RelativePaceSample);
                    _myLastMacroSector = -1;
                    CurrentTarget.Diagnosis = "ANALYZING";

                    _lastLogSector = -1;

                    sectorChanged = false;

                }



                CurrentTarget.ClassPosition = targetOpp.Position;



                double oppPos = targetOpp.TrackPositionPercent ?? 0.0;

                int targetLatchedLap = targetOpp.CurrentLap ?? state.CurrentLap;



                if (tracker.TrackedOpponents.ContainsKey(targetOpp.Name))

                {

                    targetLatchedLap = tracker.TrackedOpponents[targetOpp.Name].HighestLapSeen;

                }



                double posDiff = (myLap + myPos) - (targetLatchedLap + oppPos);
                double currentSessionClock = state.SessionTimeLeftSec;
                double currentFluidGap = 0.0;
                bool gapCalculated = false;

                if (tracker.TrackedOpponents.ContainsKey(targetOpp.Name))
                {
                    var oppData = tracker.TrackedOpponents[targetOpp.Name];
                    
                    if (posDiff < 0) // Target davanti (Player dietro)
                    {
                        double myPos100 = myPos * 100.0;
                        int s1 = (int)Math.Floor(myPos100);
                        int s2 = (s1 + 1) % 100;
                        double t1 = oppData.MicrosectorTimestamps[s1];
                        double t2 = oppData.MicrosectorTimestamps[s2];
                        
                        if (t1 > 0.0 && t2 > 0.0 && Math.Abs(t2 - t1) < 10.0)
                        {
                            double t_opp_at_myPos = t1 + (myPos100 - s1) * (t2 - t1);
                            currentFluidGap = Math.Abs(currentSessionClock - t_opp_at_myPos);
                            gapCalculated = true;
                        }
                    }
                    else // Target dietro (Player davanti)
                    {
                        double oppPos100 = oppPos * 100.0;
                        int s1 = (int)Math.Floor(oppPos100);
                        int s2 = (s1 + 1) % 100;
                        double t1 = tracker.PlayerMicrosectorTimestamps[s1];
                        double t2 = tracker.PlayerMicrosectorTimestamps[s2];
                        
                        if (t1 > 0.0 && t2 > 0.0 && Math.Abs(t2 - t1) < 10.0)
                        {
                            double t_player_at_oppPos = t1 + (oppPos100 - s1) * (t2 - t1);
                            currentFluidGap = Math.Abs(currentSessionClock - t_player_at_oppPos);
                            gapCalculated = true;
                        }
                    }
                }

                double refLapTime = 100.0;
                if (state.LastLapTimeSec > 10.0) refLapTime = state.LastLapTimeSec;
                else if (state.BestLapTimeSec > 10.0) refLapTime = state.BestLapTimeSec;
                else if (raceResult.NormalizedRaceStartPace > 10.0) refLapTime = raceResult.NormalizedRaceStartPace;
                else if (state.TrackLengthMeters > 0.0) refLapTime = state.TrackLengthMeters / 50.0;

                if (!gapCalculated)
                {
                    currentFluidGap = Math.Abs(posDiff * refLapTime);
                }



                CurrentTarget.GapSeconds = currentFluidGap;

                CurrentTarget.SignedGapSeconds = posDiff < 0 ? currentFluidGap : -currentFluidGap;

                CurrentTarget.GapString = FormatGap(CurrentTarget.SignedGapSeconds);



                if (sectorChanged)
                {
                    double currentSignedGap = posDiff < 0 ? currentFluidGap : -currentFluidGap;

                    bool playerInPit = IsPlayerInPitLane(state);
                    bool targetInPit = false;
                    if (tracker.TrackedOpponents.ContainsKey(CurrentTarget.Name))
                    {
                        var oData = tracker.TrackedOpponents[CurrentTarget.Name];
                        targetInPit = oData.IsInsideGeofence || (targetOpp != null && targetOpp.IsCarInPit);
                    }

                    var paceSample = _relativePace.ProcessSample(macroSector, currentSessionClock, currentSignedGap,
                                                                 playerInPit, targetInPit, refLapTime);
                    _lastPaceSample = paceSample;
                    CurrentTarget.RelativePace = _relativePace.RelativePace;

                    // Delta grezzo del gap per macrosettore. Vale solo quando il tracker ha
                    // davvero prodotto un rate: negli altri casi (pit, seed post-pit, sequenza
                    // rotta, dt troppo piccolo) resta l'ultima misura buona, marcata non valida.
                    // Scriverci uno zero significherebbe dichiarare "nessuna variazione", che è
                    // un'affermazione falsa, non un'assenza di dato.
                    CurrentTarget.RelativeGapDeltaValid = paceSample.RateComputed;
                    if (paceSample.RateComputed)
                    {
                        CurrentTarget.RelativeGapDelta = paceSample.DeltaGap;
                    }

                    if (paceSample.Reason != RelativePaceInvalidationReason.None)
                    {
                        log.Log(LogModule.STRATEGY_EVENT, LogType.EVENT, "RELATIVE_PACE_INVALIDATION",
                            $"reason={paceSample.Reason} | sector={macroSector} | seqValid={paceSample.SequenceValid} | deltaTime={paceSample.DeltaTime:F3} | frozenPace={CurrentTarget.RelativePace:F3}");
                    }
                    else if (paceSample.WasPostPitSeed)
                    {
                        log.Log(LogModule.STRATEGY_EVENT, LogType.EVENT, "RELATIVE_PACE_POST_PIT_SEED",
                            $"sector={macroSector} | gap={currentSignedGap:F3} | frozenPace={CurrentTarget.RelativePace:F3} | sectorsRemaining={paceSample.PostPitSectorsRemaining} | note=assestamento post-pit, nessun rate calcolato");
                    }
                    else if (paceSample.EmaSeeded)
                    {
                        log.Log(LogModule.STRATEGY_EVENT, LogType.EVENT, "RELATIVE_PACE_SEED",
                            $"sector={macroSector} | gap={currentSignedGap:F3} | prevGap={paceSample.PreviousGap:F3} | gapDelta={paceSample.DeltaGap:F3} | deltaTime={paceSample.DeltaTime:F3} | instantRate={paceSample.InstantRate:F3} | emaAfter={paceSample.EmaAfter:F3} | clamped={paceSample.Clamped}");
                    }
                    else if (paceSample.RateComputed)
                    {
                        log.Log(LogModule.STRATEGY_EVENT, LogType.EVENT, "RELATIVE_PACE_UPDATE",
                            $"sector={macroSector} | prevGap={paceSample.PreviousGap:F3} | gapDelta={paceSample.DeltaGap:F3} | deltaTime={paceSample.DeltaTime:F3} | instantRate={paceSample.InstantRate:F3} | emaBefore={paceSample.EmaBefore:F3} | emaAfter={paceSample.EmaAfterRaw:F3} | clamped={paceSample.EmaAfter:F3} | wasClamped={paceSample.Clamped}");
                    }

                    log.Log(LogModule.STRATEGY, LogType.FLOW, "Gap Update", $"Gap: {currentSignedGap:F2}s | RelPace: {CurrentTarget.RelativePace:F3}s | Sector: {macroSector}");
                }

                _myLastMacroSector = macroSector;



                if (tracker.TrackedOpponents.ContainsKey(CurrentTarget.Name))

                {

                    var oppData = tracker.TrackedOpponents[CurrentTarget.Name];



                    CurrentTarget.TargetCurrentSpeed = oppData.LastValidSpeedKmh;

                    CurrentTarget.TargetTopSpeed = oppData.PersonalTopSpeed;

                    CurrentTarget.CurrentMicrosector = oppData.CurrentMicrosector;

                    CurrentTarget.SpeedDrop = oppData.CurrentSpeedDrop;



                    CurrentTarget.NormalizedRaceStartPace = oppData.NormalizedRaceStartPace;

                    CurrentTarget.TargetPaceDropDueToTyres = oppData.PaceDropDueToTyres;

                    CurrentTarget.TargetSectorPaceDropDueToTyres = oppData.SectorPaceDropDueToTyres;
                    CurrentTarget.TargetSectorPaceDropDueToTyresRaw = oppData.SectorPaceDropDueToTyresRaw;

                    CurrentTarget.PrePitNormalizedAverage = oppData.PrePitNormalizedAverage;
                    Array.Copy(oppData.PostPitNormalizedDeltas, CurrentTarget.PostPitNormalizedDeltas, 3);
                    Array.Copy(oppData.PostPitWarmupPenalties, CurrentTarget.PostPitWarmupPenalties, 3);

                    CurrentTarget.TargetLapBaselineNormalized = oppData.NormalizedTimes.LapBaseline;
                    CurrentTarget.TargetLapBaselineRaw = oppData.RawTimes.LapBaseline;
                    CurrentTarget.TargetLapMovingAverageNormalized = oppData.NormalizedTimes.LapMovingAverage;
                    CurrentTarget.TargetLapMovingAverageRaw = oppData.RawTimes.LapMovingAverage;

                    CurrentTarget.TargetSectorBaselineNormalized = oppData.NormalizedTimes.SectorBaseline;
                    CurrentTarget.TargetSectorBaselineRaw = oppData.RawTimes.SectorBaseline;
                    CurrentTarget.TargetSectorMovingAverageNormalized = oppData.NormalizedTimes.SectorMovingAverage;
                    CurrentTarget.TargetSectorMovingAverageRaw = oppData.RawTimes.SectorMovingAverage;

                    CurrentTarget.PitCount = oppData.PitCount;

                    CurrentTarget.CurrentTank = oppData.EstimatedFuel;
                    CurrentTarget.EstimatedFuelTank = oppData.EstimatedFuelTank;
                    CurrentTarget.LapCount = oppData.LapCount;
                    CurrentTarget.EstimatedPitWindow = oppData.EstimatedPitWindow;
                    CurrentTarget.EstimatedPitWindowTargetLap = oppData.EstimatedPitWindowTargetLap;

                    CurrentTarget.TankLapsRemaining = fuel.AverageFuelPerLap > 0 ? (oppData.EstimatedFuelTank / fuel.AverageFuelPerLap) : 99.0;

                    CurrentTarget.CalculatedStationaryTime = oppData.LastPitStationaryTimeSec;
                    CurrentTarget.FuelToAddTime = oppData.FuelToAddTime;

                    CurrentTarget.InOutPitAccDecTime = oppData.LastPitInOutAccDecTimeSec > 0.0 ? oppData.LastPitInOutAccDecTimeSec : radar.PitInOutAccDecTime;



                    double playerRawPace = raceResult.NormalizedRaceStartPace > 0 ? raceResult.NormalizedRaceStartPace + raceResult.PaceDropDueToTyres : 0.0;

                    double targetRawPace = oppData.NormalizedRaceStartPace > 0 ? oppData.NormalizedRaceStartPace + oppData.PaceDropDueToTyres : 0.0;



                    if (playerRawPace > 0 && targetRawPace > 0)

                    {

                        CurrentTarget.PaceDeficit = playerRawPace - targetRawPace;

                        CurrentTarget.RelativeDegradation = oppData.PaceDropDueToTyres - raceResult.PaceDropDueToTyres;

                    }



                    double profileRefuelRate = CarPitData.GetProfile(state.CarClassId).RefuelRate;
                    double refuelRate = radar.MeasuredFuelFillRate > 0 ? radar.MeasuredFuelFillRate : profileRefuelRate;
                    double fuelTime = refuelRate > 0 ? (fuel.FuelToAdd / refuelRate) : 0.0;

                    double tireMult = GetTireMultiplier(tyres.CurrentScope);
                    double tireTime = radar.DbTireChangeTime * tireMult;

                    double pitDistance = radar.PitDistanceMeters;
                    double racingSpeedMs = tracker.ClassTopSpeed > 0 ? (tracker.ClassTopSpeed / 3.6) : (250.0 / 3.6);
                    double pitLaneZoneRacingTime = pitDistance > 0 ? (pitDistance / racingSpeedMs) : 0.0;
                    CurrentTarget.PitLaneZoneRacingTime = pitLaneZoneRacingTime;

                    // Calcolo raffinato basato sulla Extended Pit Zone
                    double extendedDistance = pitDistance + (0.10 * trackLen);
                    double fallbackExtendedRacingTime = extendedDistance / racingSpeedMs;
                    double extendedRacingTime = tracker.ClassBestExtendedPitZoneTime > 0.0 ? tracker.ClassBestExtendedPitZoneTime : fallbackExtendedRacingTime;

                    double accDecTime = CurrentTarget.InOutPitAccDecTime;

                    bool isSeqPit = CarPitData.GetProfile(state.CarClassId).IsSequential;
                    double playerStationaryTime = isSeqPit ? (fuelTime + tireTime) : Math.Max(fuelTime, tireTime);
                    if (playerStationaryTime > 0.0) playerStationaryTime += 2.0; // +2.0s tempo morto martinetti

                    double playerTotalPitLaneTime = playerStationaryTime + radar.PitTransitTime;
                    double playerProjectedExtendedTime = playerTotalPitLaneTime + accDecTime;
                    double playerTotalPitLoss = Math.Max(0.0, playerProjectedExtendedTime - extendedRacingTime);

                    // Previsione carburante da aggiungere e sosta stazionaria per il target
                    double fuelPerLap = fuel.AverageFuelPerLap > 0 ? fuel.AverageFuelPerLap : 3.0;
                    double targetFuelLaps = fuelPerLap > 0 ? (oppData.EstimatedFuel / fuelPerLap) : 99.0;
                    double targetFuelDeficit = raceResult.RaceLapsRemaining - targetFuelLaps;

                    // Regola d'Oro: Il target DEVE pittare solo se il deficit supera la soglia di 0.8 giri
                    bool targetNeedsPit = targetFuelDeficit > 0.8;

                    double targetFuelToAdd = (raceResult.RaceLapsRemaining * fuelPerLap) + (0.3 * fuelPerLap) - oppData.EstimatedFuel;
                    if (targetFuelToAdd < 0.0) targetFuelToAdd = 0.0;
                    targetFuelToAdd = Math.Min(state.MaxFuelCapacity, targetFuelToAdd);

                    CurrentTarget.EstimatedFuelToAdd = targetFuelToAdd;
                    CurrentTarget.EstimatedFuelAdded = oppData.LastPitFuelAdded;

                    double targetStationaryTime = 0.0;
                    double targetTotalPitLoss = 0.0;

                    if (targetNeedsPit)
                    {
                        targetStationaryTime = refuelRate > 0 ? (targetFuelToAdd / refuelRate) : 0.0;
                        if (targetStationaryTime > 0.0) targetStationaryTime += 2.0; // +2.0s tempo morto martinetti

                        double targetTotalPitLaneTime = targetStationaryTime + radar.PitTransitTime;
                        double targetProjectedExtendedTime = targetTotalPitLaneTime + accDecTime;
                        targetTotalPitLoss = Math.Max(0.0, targetProjectedExtendedTime - extendedRacingTime);
                    }

                    CurrentTarget.EstimatedStationaryTime = targetStationaryTime;
                    CurrentTarget.ProjectedStationaryTime = targetStationaryTime;

                    double netPitAdvantage = targetTotalPitLoss - playerTotalPitLoss;

                    // 1. Stima del Warmup del Player e del Target (Media delle penalità post-pit passate, default 1.0s / 1.2s)
                    double playerWarmup = 1.0;
                    if (raceAnalyzer.PostPitWarmupPenalties[0] > 0.1 && raceAnalyzer.PostPitWarmupPenalties[0] < 10.0)
                    {
                        var playerWarms = raceAnalyzer.PostPitWarmupPenalties.Where(p => p > 0.0).ToList();
                        if (playerWarms.Count > 0) playerWarmup = playerWarms.Average();
                    }

                    double targetWarmup = 1.2;
                    if (oppData.PostPitWarmupPenalties[0] > 0.1 && oppData.PostPitWarmupPenalties[0] < 10.0)
                    {
                        var targetWarms = oppData.PostPitWarmupPenalties.Where(p => p > 0.0).ToList();
                        if (targetWarms.Count > 0) targetWarmup = targetWarms.Average();
                    }

                    // 2. Controllo del Traffico all'Uscita Box (Drop-Zone)
                    CurrentTarget.TargetMode = targetModeString;
                    refLapTime = 100.0;
                    if (state.LastLapTimeSec > 10.0) refLapTime = state.LastLapTimeSec;
                    else if (state.BestLapTimeSec > 10.0) refLapTime = state.BestLapTimeSec;
                    else if (raceResult.NormalizedRaceStartPace > 10.0) refLapTime = raceResult.NormalizedRaceStartPace;

                    bool pitExitTrafficConflict = false;
                    double minMergeGap = 999.0;

                    foreach (var opp in state.Opponents)
                    {
                        if (opp.IsPlayer || !opp.TrackPositionPercent.HasValue) continue;

                        // Escludiamo vetture già nei box (IsInsideGeofence == true)
                        if (tracker.TrackedOpponents.TryGetValue(opp.Name, out var oData))
                        {
                            if (oData.IsInsideGeofence) continue;
                        }

                        // Calcolo del distacco temporale continuo tra noi e l'avversario
                        int oppLap = opp.CurrentLap ?? 0;
                        if (tracker.TrackedOpponents.TryGetValue(opp.Name, out var trackedOpp))
                        {
                            oppLap = trackedOpp.HighestLapSeen;
                        }
                        double posDiffLaps = (myLap + myPos) - (oppLap + opp.TrackPositionPercent.Value);
                        double timeGap = posDiffLaps * refLapTime; // positivo se l'avversario è dietro, negativo se davanti

                        // Distacco temporale proiettato all'uscita dai box del Player
                        double projectedExitGap = timeGap - playerTotalPitLoss;

                        // Applicazione del modulo per il distacco fisico reale in pista
                        double physicalMergeGap = projectedExitGap % refLapTime;
                        if (physicalMergeGap < 0.0) physicalMergeGap += refLapTime;

                        if (physicalMergeGap > refLapTime / 2.0)
                        {
                            physicalMergeGap -= refLapTime; // negativo: davanti; positivo: dietro
                        }

                        // Se l'avversario si trova nella bolla di merge di ±3 secondi
                        if (physicalMergeGap >= -3.0 && physicalMergeGap <= 3.0)
                        {
                            if (Math.Abs(physicalMergeGap) < Math.Abs(minMergeGap))
                            {
                                minMergeGap = physicalMergeGap;
                            }

                            bool isThreat = (opp.CarClass == state.CarClassId);
                            if (!isThreat && oData != null)
                            {
                                double oppPace = oData.NormalizedTimes.SectorBaseline * 3.0;
                                double playerNewPace = raceResult.NormalizedRaceStartPace + (fuel.FuelToAdd * fuelWeightCoef);
                                if (oppPace > playerNewPace + 1.5)
                                {
                                    isThreat = true;
                                }
                            }

                            // Sanity check spaziale: coordinata dell'avversario vicina a ExtendedPitExitPct
                            double exitPct = radar.GetExtendedPitExitPct();
                            double tolerancePct = 0.05;
                            double deltaPct = opp.TrackPositionPercent.Value - exitPct;
                            if (deltaPct < -0.5) deltaPct += 1.0;
                            else if (deltaPct > 0.5) deltaPct -= 1.0;

                            if (isThreat && Math.Abs(deltaPct) < tolerancePct)
                            {
                                pitExitTrafficConflict = true;
                            }
                        }
                    }

                    CurrentTarget.TrafficAlert = pitExitTrafficConflict;                    bool canFinishWithoutPitting = fuel.IsPredictionValid && (fuel.TankLapsRemaining > raceResult.RaceLapsRemaining);

                    // ==========================================
                    // ⚡ UNDERCUT ENGINE (Anticipare la Sosta)
                    // ==========================================
                    double maxUndercutReactionWindow = 1.0;
                    double pitDecisionBuffer = 0.8;
                    CurrentTarget.TargetLapsUntilPit = Math.Max(0.0, targetFuelLaps - pitDecisionBuffer);
                    CurrentTarget.ReactionDeltaLaps = Math.Min(CurrentTarget.TargetLapsUntilPit, maxUndercutReactionWindow);

                    double targetPaceDegraded = targetRawPace; // NormalizedRaceStartPace + PaceDropDueToTyres
                    double playerPaceFresh = raceResult.NormalizedRaceStartPace + (fuel.FuelToAdd * fuelWeightCoef);

                    double prePitPaceGain = (targetPaceDegraded - playerPaceFresh) * CurrentTarget.ReactionDeltaLaps;
                    CurrentTarget.UndercutAdvantage = prePitPaceGain + netPitAdvantage - playerWarmup;

                    double positiveGapToTarget = Math.Max(0.0, CurrentTarget.SignedGapSeconds);
                    CurrentTarget.UndercutCaptureMargin = CurrentTarget.UndercutAdvantage - positiveGapToTarget;

                    CurrentTarget.UndercutPositionOK = CurrentTarget.SignedGapSeconds >= -0.5;
                    CurrentTarget.UndercutFuelOK = fuel.TankLapsRemaining >= 1.0;
                    CurrentTarget.UndercutTrafficOK = !pitExitTrafficConflict;
                    bool undercutMarginOK = CurrentTarget.UndercutCaptureMargin > 0.0;
                    bool undercutRaceLapsOK = raceResult.RaceLapsRemaining > 2.0;

                    CurrentTarget.UndercutRejectReason = StrategyRejectReason.None;
                    int undercutFails = 0;
                    string uFailedStr = "";
                    if (!CurrentTarget.UndercutPositionOK) { CurrentTarget.UndercutRejectReason = StrategyRejectReason.Position; undercutFails++; uFailedStr += "Position,"; }
                    if (!targetNeedsPit) { CurrentTarget.UndercutRejectReason = StrategyRejectReason.TargetNotPitting; undercutFails++; uFailedStr += "TargetNotPitting,"; }
                    if (!CurrentTarget.UndercutFuelOK) { CurrentTarget.UndercutRejectReason = StrategyRejectReason.Fuel; undercutFails++; uFailedStr += "Fuel,"; }
                    if (!CurrentTarget.UndercutTrafficOK) { CurrentTarget.UndercutRejectReason = StrategyRejectReason.Traffic; undercutFails++; uFailedStr += "Traffic,"; }
                    if (!undercutMarginOK) { CurrentTarget.UndercutRejectReason = StrategyRejectReason.InsufficientCaptureMargin; undercutFails++; uFailedStr += "Margin,"; }
                    if (!undercutRaceLapsOK || canFinishWithoutPitting) { CurrentTarget.UndercutRejectReason = StrategyRejectReason.RaceTooLate; undercutFails++; uFailedStr += "RaceLaps,"; }
                    
                    if (undercutFails > 1) CurrentTarget.UndercutRejectReason = StrategyRejectReason.Multiple;

                    CurrentTarget.UndercutViable = undercutFails == 0;

                    // ==========================================
                    // 🛡️ OVERCUT ENGINE (Posticipare la Sosta)
                    // ==========================================
                    // 1. Determine Contiguous Warmup Prefix
                    double targetWarmupLapsAvailable = 2.0; // Fallback missing/invalid
                    if (oppData.PostPitWarmupPenalties != null && oppData.PostPitWarmupPenalties.Length > 0)
                    {
                        targetWarmupLapsAvailable = 0.0;
                        for (int i = 0; i < oppData.PostPitWarmupPenalties.Length; i++)
                        {
                            if (oppData.PostPitWarmupPenalties[i] >= 0.10)
                                targetWarmupLapsAvailable += 1.0;
                            else
                                break;
                        }
                    }

                    double maxOvercutStayLaps = Math.Max(0.0, fuel.TankLapsRemaining - 0.4);
                    double nEffective = Math.Min(maxOvercutStayLaps, targetWarmupLapsAvailable);
                    CurrentTarget.OvercutStayLaps = nEffective;

                    double totalWarmupGain = ComputeInterpolatedWarmup(oppData.PostPitWarmupPenalties, nEffective);
                    double targetPacePreWarmupPenalty = targetRawPace;
                    double stayOutsideGain = (targetPacePreWarmupPenalty - playerRawPace) * nEffective;

                    CurrentTarget.OvercutAdvantage = stayOutsideGain + totalWarmupGain + netPitAdvantage;
                    CurrentTarget.OvercutCaptureMargin = CurrentTarget.OvercutAdvantage - positiveGapToTarget;

                    bool targetIsInPit = oppData.IsInsideGeofence || (targetOpp != null && targetOpp.IsCarInPit);
                    double lapsSinceLastPit = oppData.LastPitLap > 0 ? (double)(oppData.HighestLapSeen - oppData.LastPitLap) : 999.0;
                    CurrentTarget.TargetPittedRecently = lapsSinceLastPit <= 2.0;
                    CurrentTarget.OvercutFuelOK = fuel.TankLapsRemaining >= nEffective + 0.4;
                    CurrentTarget.OvercutTrafficOK = true; // In future: check player sector traffic

                    bool overcutTargetPitting = targetIsInPit || CurrentTarget.TargetPittedRecently;
                    bool overcutStayOK = nEffective >= 0.5;
                    bool overcutMarginOK = CurrentTarget.OvercutCaptureMargin > 0.0;
                    bool overcutRaceLapsOK = raceResult.RaceLapsRemaining > 2.0;

                    CurrentTarget.OvercutRejectReason = StrategyRejectReason.None;
                    int overcutFails = 0;
                    string oFailedStr = "";
                    if (!overcutTargetPitting) { CurrentTarget.OvercutRejectReason = StrategyRejectReason.TargetNotPitting; overcutFails++; oFailedStr += "TargetNotPitting,"; }
                    if (!overcutStayOK) { CurrentTarget.OvercutRejectReason = StrategyRejectReason.InsufficientStayLaps; overcutFails++; oFailedStr += "StayLaps,"; }
                    if (!CurrentTarget.OvercutFuelOK) { CurrentTarget.OvercutRejectReason = StrategyRejectReason.Fuel; overcutFails++; oFailedStr += "Fuel,"; }
                    if (!CurrentTarget.OvercutTrafficOK) { CurrentTarget.OvercutRejectReason = StrategyRejectReason.Traffic; overcutFails++; oFailedStr += "Traffic,"; }
                    if (!overcutMarginOK) { CurrentTarget.OvercutRejectReason = StrategyRejectReason.InsufficientCaptureMargin; overcutFails++; oFailedStr += "Margin,"; }
                    if (!overcutRaceLapsOK || canFinishWithoutPitting) { CurrentTarget.OvercutRejectReason = StrategyRejectReason.RaceTooLate; overcutFails++; oFailedStr += "RaceLaps,"; }

                    if (overcutFails > 1) CurrentTarget.OvercutRejectReason = StrategyRejectReason.Multiple;

                    CurrentTarget.OvercutViable = overcutFails == 0;

                    // ==========================================
                    // 🧠 DECISION ENGINE
                    // ==========================================
                    StrategyDecision newDecision = StrategyDecision.Neutral;
                    if (CurrentTarget.UndercutViable && CurrentTarget.OvercutViable)
                    {
                        if (CurrentTarget.UndercutCaptureMargin > CurrentTarget.OvercutCaptureMargin)
                            newDecision = StrategyDecision.Undercut;
                        else
                            newDecision = StrategyDecision.Overcut;
                    }
                    else if (CurrentTarget.UndercutViable) newDecision = StrategyDecision.Undercut;
                    else if (CurrentTarget.OvercutViable) newDecision = StrategyDecision.Overcut;

                    CurrentTarget.Decision = newDecision;

                    // ==========================================
                    // 📡 LOGGING: EVENTS AND SNAPSHOTS
                    // ==========================================
                    if (newDecision != _lastStrategyDecision)
                    {
                        log.Log(LogModule.STRATEGY_EVENT, LogType.EVENT, "STRATEGY_CHANGED", $"previous={_lastStrategyDecision} | new={newDecision} | target={CurrentTarget.Name}");
                        _lastStrategyDecision = newDecision;
                    }

                    if (CurrentTarget.UndercutViable != _lastUndercutViable)
                    {
                        if (CurrentTarget.UndercutViable) log.Log(LogModule.STRATEGY_EVENT, LogType.EVENT, "UNDERCUT_VIABLE", $"margin={CurrentTarget.UndercutCaptureMargin:F3} | reaction={CurrentTarget.ReactionDeltaLaps:F1}");
                        else log.Log(LogModule.STRATEGY_EVENT, LogType.EVENT, "UNDERCUT_NONVIABLE", $"reason={CurrentTarget.UndercutRejectReason} | failed={uFailedStr.TrimEnd(',')}");
                        _lastUndercutViable = CurrentTarget.UndercutViable;
                    }

                    if (CurrentTarget.OvercutViable != _lastOvercutViable)
                    {
                        if (CurrentTarget.OvercutViable) log.Log(LogModule.STRATEGY_EVENT, LogType.EVENT, "OVERCUT_VIABLE", $"margin={CurrentTarget.OvercutCaptureMargin:F3} | stayLaps={nEffective:F1}");
                        else log.Log(LogModule.STRATEGY_EVENT, LogType.EVENT, "OVERCUT_NONVIABLE", $"reason={CurrentTarget.OvercutRejectReason} | failed={oFailedStr.TrimEnd(',')}");
                        _lastOvercutViable = CurrentTarget.OvercutViable;
                    }

                    _snapshotTickCounter++;
                    if (_snapshotTickCounter >= 25)
                    {
                        _snapshotTickCounter = 0;
                        bool pInPit = IsPlayerInPitLane(state);

                        double[] warmupArray = oppData.PostPitWarmupPenalties;
                        bool warmupFallbackUsed = warmupArray == null || warmupArray.Length == 0;

                        // Campi in array anziché string.Format numerato: l'ordine qui è l'unica
                        // fonte di verità e deve corrispondere a LogManager.WriteHeader().
                        string[] snapFields =
                        {
                            // --- contesto ---
                            F3(state.SessionTimeLeftSec), I(myLap), I(macroSector), Csv(CurrentTarget.Name),
                            F3(CurrentTarget.SignedGapSeconds), B(pInPit), B(targetIsInPit), F1(raceResult.RaceLapsRemaining),
                            // --- RelativePace: intermedi ricostruibili (spec §32) ---
                            F3(_lastPaceSample.PreviousGap), F3(_lastPaceSample.DeltaGap),
                            B(CurrentTarget.RelativeGapDeltaValid), F3(_lastPaceSample.DeltaTime),
                            F3(_lastPaceSample.InstantRate), F3(CurrentTarget.RelativePace),
                            B(_lastPaceSample.SequenceValid), _lastPaceSample.Reason.ToString(),
                            B(_relativePace.PitSeedPending), B(_lastPaceSample.WasPostPitSeed),
                            // --- Undercut ---
                            F1(fuel.TankLapsRemaining), F1(targetFuelLaps), F1(CurrentTarget.TargetLapsUntilPit),
                            F1(CurrentTarget.ReactionDeltaLaps), F3(targetPaceDegraded), F3(playerPaceFresh),
                            F3(prePitPaceGain), F3(targetTotalPitLoss), F3(playerTotalPitLoss), F3(netPitAdvantage),
                            F3(playerWarmup), F3(positiveGapToTarget), F3(CurrentTarget.UndercutAdvantage),
                            F3(CurrentTarget.UndercutCaptureMargin),
                            B(CurrentTarget.UndercutPositionOK), B(targetNeedsPit), B(CurrentTarget.UndercutFuelOK),
                            B(CurrentTarget.UndercutTrafficOK), B(undercutMarginOK), B(undercutRaceLapsOK),
                            B(CurrentTarget.UndercutViable), CurrentTarget.UndercutRejectReason.ToString(),
                            // --- Overcut ---
                            F3(W(warmupArray, 0)), F3(W(warmupArray, 1)), F3(W(warmupArray, 2)), B(warmupFallbackUsed),
                            F1(targetWarmupLapsAvailable), F1(maxOvercutStayLaps), F1(nEffective),
                            F3(targetRawPace), F3(playerRawPace), F3(stayOutsideGain), F3(totalWarmupGain),
                            F3(CurrentTarget.OvercutAdvantage), F3(CurrentTarget.OvercutCaptureMargin),
                            B(targetIsInPit), B(CurrentTarget.TargetPittedRecently), B(CurrentTarget.OvercutFuelOK),
                            B(CurrentTarget.OvercutTrafficOK), B(overcutStayOK), B(overcutMarginOK),
                            B(overcutRaceLapsOK), B(CurrentTarget.OvercutViable), CurrentTarget.OvercutRejectReason.ToString(),
                            // --- esito ---
                            newDecision.ToString()
                        };

                        // Un disallineamento header/dati rende il CSV muto senza errori: meglio urlarlo.
                        if (snapFields.Length != LogManager.SnapshotColumnCount && !_snapshotWidthWarned)
                        {
                            _snapshotWidthWarned = true;
                            log.Log(LogModule.SYSTEM, LogType.EVENT, "SNAPSHOT_COLUMN_MISMATCH",
                                $"fields={snapFields.Length} | headerColumns={LogManager.SnapshotColumnCount}");
                        }

                        log.Log(LogModule.STRATEGY_SNAPSHOT, LogType.FLOW, string.Join(",", snapFields));
                    }
                    
                    // La proiezione del merge gap sul target è il distacco fisico di rientro in pista (positivo: dietro, negativo: davanti)
                    double projectedPhysicalMergeGap = CurrentTarget.SignedGapSeconds + playerTotalPitLoss - targetTotalPitLoss;
                    CurrentTarget.ProjectedMergeGap = projectedPhysicalMergeGap;

                    // Log dedicato di monitoraggio MergeGap aggiornato ogni 10 secondi (attivo solo quando SessionState == 4)
                    // Mantiene sempre il focus su Egor nel Log anche se l'utente naviga su altri target a schermo
                    bool timeElapsed = (DateTime.UtcNow - _lastMergeGapLogWallTime).TotalSeconds >= 10.0 || _lastMergeGapLogWallTime == DateTime.MinValue;
                    if (state.SessionStateStatus == 4 && timeElapsed)
                    {
                        _lastMergeGapLogWallTime = DateTime.UtcNow;

                        var logTargetOpp = state.Opponents.FirstOrDefault(o => !o.IsPlayer && (o.Name.IndexOf("Egor", StringComparison.OrdinalIgnoreCase) >= 0 || o.Name.IndexOf("Ogorodnicov", StringComparison.OrdinalIgnoreCase) >= 0 || o.Name.IndexOf("Ogorodnikov", StringComparison.OrdinalIgnoreCase) >= 0)) ?? targetOpp;

                        if (logTargetOpp != null && tracker.TrackedOpponents.TryGetValue(logTargetOpp.Name, out var logOppData))
                        {
                            double logOppPos = logTargetOpp.TrackPositionPercent ?? 0.0;
                            int logLatchedLap = logOppData.HighestLapSeen;
                            double logPosDiff = (myLap + myPos) - (logLatchedLap + logOppPos);
                            double logSessionClock = state.SessionTimeLeftSec;
                            double logTargetFluidGap = 0.0;
                            bool gapFound = false;

                            if (logPosDiff < 0)
                            {
                                double myPos100 = myPos * 100.0;
                                int s1 = (int)Math.Floor(myPos100);
                                int s2 = (s1 + 1) % 100;
                                double t1 = logOppData.MicrosectorTimestamps[s1];
                                double t2 = logOppData.MicrosectorTimestamps[s2];
                                if (t1 > 0.0 && t2 > 0.0 && Math.Abs(t2 - t1) < 10.0)
                                {
                                    double t_opp_at_myPos = t1 + (myPos100 - s1) * (t2 - t1);
                                    logTargetFluidGap = Math.Abs(logSessionClock - t_opp_at_myPos);
                                    gapFound = true;
                                }
                            }
                            else
                            {
                                double oppPos100 = logOppPos * 100.0;
                                int s1 = (int)Math.Floor(oppPos100);
                                int s2 = (s1 + 1) % 100;
                                double t1 = tracker.PlayerMicrosectorTimestamps[s1];
                                double t2 = tracker.PlayerMicrosectorTimestamps[s2];
                                if (t1 > 0.0 && t2 > 0.0 && Math.Abs(t2 - t1) < 10.0)
                                {
                                    double t_player_at_oppPos = t1 + (oppPos100 - s1) * (t2 - t1);
                                    logTargetFluidGap = Math.Abs(logSessionClock - t_player_at_oppPos);
                                    gapFound = true;
                                }
                            }

                            if (!gapFound)
                            {
                                double refLap = 100.0;
                                if (state.LastLapTimeSec > 10.0) refLap = state.LastLapTimeSec;
                                else if (state.BestLapTimeSec > 10.0) refLap = state.BestLapTimeSec;
                                else if (raceResult.NormalizedRaceStartPace > 10.0) refLap = raceResult.NormalizedRaceStartPace;
                                else if (state.TrackLengthMeters > 0.0) refLap = state.TrackLengthMeters / 50.0;
                                logTargetFluidGap = Math.Abs(logPosDiff * refLap);
                            }

                            double logTargetSignedGap = logPosDiff < 0 ? logTargetFluidGap : -logTargetFluidGap;

                            double logTargetFuelLaps = fuelPerLap > 0 ? (logOppData.EstimatedFuel / fuelPerLap) : 99.0;
                            double logTargetFuelDeficit = raceResult.RaceLapsRemaining - logTargetFuelLaps;
                            bool logTargetNeedsPit = logTargetFuelDeficit > 0.8;

                            double logTargetFuelToAdd = (raceResult.RaceLapsRemaining * fuelPerLap) + (0.3 * fuelPerLap) - logOppData.EstimatedFuel;
                            if (logTargetFuelToAdd < 0.0) logTargetFuelToAdd = 0.0;
                            logTargetFuelToAdd = Math.Min(state.MaxFuelCapacity, logTargetFuelToAdd);

                            double logTargetStationaryTime = 0.0;
                            double logTargetTotalPitLoss = 0.0;

                            if (logTargetNeedsPit)
                            {
                                logTargetStationaryTime = refuelRate > 0 ? (logTargetFuelToAdd / refuelRate) : 0.0;
                                if (logTargetStationaryTime > 0.0) logTargetStationaryTime += 2.0;

                                double logTargetTotalPitLaneTime = logTargetStationaryTime + radar.PitTransitTime;
                                double logTargetProjectedExtendedTime = logTargetTotalPitLaneTime + accDecTime;
                                logTargetTotalPitLoss = Math.Max(0.0, logTargetProjectedExtendedTime - extendedRacingTime);
                            }

                            double logProjectedMergeGap = logTargetSignedGap + playerTotalPitLoss - logTargetTotalPitLoss;

                            int playerPitCount = raceResult.PlayerPitCount;
                            int targetPitCount = (logTargetOpp.PitCount.HasValue) ? logTargetOpp.PitCount.Value : 0;
                            int playerPos = state.Position;
                            int targetPos = logTargetOpp.Position;
                            double playerFuelLaps = fuel.TankLapsRemaining;

                            string logMsg =
                                $"========================================================================================\n" +
                                $"[MERGE_GAP_MONITOR] SessionTimeLeft: {state.SessionTimeLeftSec:F1}s | Lap: {state.CurrentLap} | Target: {logTargetOpp.Name}\n" +
                                $"----------------------------------------------------------------------------------------\n" +
                                $"DRIVERS:\n" +
                                $"  Player: Pos: P{playerPos} | Pits: {playerPitCount} | FuelLaps: {playerFuelLaps:F1} | FuelFillRate: {refuelRate:F2} L/s\n" +
                                $"  Target: Pos: P{targetPos} | Pits: {targetPitCount} | FuelLaps: {logTargetFuelLaps:F1} | RemLaps: {raceResult.RaceLapsRemaining:F1} | TargetNeedsPit: {logTargetNeedsPit}\n" +
                                $"PIT LOSS TIMINGS:\n" +
                                $"  Player (+{playerTotalPitLoss:F2}s): Staz: {playerStationaryTime:F2}s (incl. 2s) | Transit: {radar.PitTransitTime:F2}s | AccDec: {accDecTime:F2}s | ExtZone: {extendedRacingTime:F2}s\n" +
                                $"  Target (+{logTargetTotalPitLoss:F2}s) : Staz: {logTargetStationaryTime:F2}s | Transit: {radar.PitTransitTime:F2}s | AccDec: {accDecTime:F2}s | ExtZone: {extendedRacingTime:F2}s\n" +
                                $"RESULT:\n" +
                                $"  LiveSignedGap: {logTargetSignedGap:F2}s -> ProjectedMergeGap: {logProjectedMergeGap:F2}s ({logTargetSignedGap:F2} + {playerTotalPitLoss:F2} - {logTargetTotalPitLoss:F2})\n" +
                                $"========================================================================================\n";

                            log.Log(LogModule.MERGEGAP, LogType.FLOW, logMsg);
                        }
                    }

                    if (sectorChanged)
                    {
                        log.Log(LogModule.STRATEGY, LogType.FLOW, "Undercut Math",
                            $"AccDec:{accDecTime:F1} | P_PitLoss:{playerTotalPitLoss:F1} | ReactLaps:{CurrentTarget.ReactionDeltaLaps} | P_AfterPitPace:{playerPaceFresh:F2} | T_RawPace:{targetRawPace:F2} | MergeGap:{projectedPhysicalMergeGap:F2}");
                    }



                    double lastLap = oppData.RawTimes.LastLapTime;

                    double bestLap = oppData.NormalizedRaceStartPace;



                    GenerateDiagnosis(oppData, lastLap, bestLap);



                    if (oppData.CurrentMicrosector != _lastLogSector)

                    {

                        string diagLog = string.Format(CultureInfo.InvariantCulture,

                            "Lap:{0:F3} | Best:{1:F3} | DropL:{2:F1} | DropM:{3:F1} | DropH:{4:F1} | {5}",

                            lastLap, bestLap, oppData.ZoneDropLow, oppData.ZoneDropMid, oppData.ZoneDropHigh, CurrentTarget.Diagnosis);



                        log.Log(LogModule.STRATEGY, LogType.FLOW, "Diagnosis Analysis", diagLog);

                        _lastLogSector = oppData.CurrentMicrosector;

                    }

                }

            }

            else

            {

                SetNoTarget();

            }



            CurrentTarget.ModeLabel = !string.IsNullOrEmpty(LatchedTargetName) ? ("LOCK:" + targetModeString) : targetModeString;

        }



        private GameReaderCommon.Opponent SelectTarget(SessionState state, OpponentTracker tracker, string mode, out bool isPlayer)
        {
            isPlayer = false;
            if (mode == "PLAYER")
            {
                isPlayer = true;
                return null;
            }

            if (mode == "LEADER_OVERALL")
            {
                if (state.Position == 1)
                {
                    isPlayer = true;
                    return null;
                }
                return state.Opponents.FirstOrDefault(o => o.Position == 1 && !o.IsPlayer);
            }

            if (mode == "LEADER_CLASS")
            {
                double playerProgress = state.CurrentLap + state.TrackPositionPercent;
                GameReaderCommon.Opponent classLeaderOpp = null;
                double maxClassOppProgress = -1.0;
                foreach (var opp in state.Opponents)
                {
                    if (opp.IsPlayer || opp.CarClass != state.CarClassId) continue;
                    int oppLap = opp.CurrentLap ?? 0;
                    if (tracker.TrackedOpponents.ContainsKey(opp.Name))
                        oppLap = tracker.TrackedOpponents[opp.Name].HighestLapSeen;
                    double progress = oppLap + (opp.TrackPositionPercent ?? 0.0);
                    if (progress > maxClassOppProgress)
                    {
                        maxClassOppProgress = progress;
                        classLeaderOpp = opp;
                    }
                }
                if (playerProgress > maxClassOppProgress)
                {
                    isPlayer = true;
                    return null;
                }
                return classLeaderOpp;
            }

            GameReaderCommon.Opponent found = null;
            double minGapAhead = 999.0;
            double minGapBehind = 999.0;

            double myPos = state.TrackPositionPercent;
            int myLap = state.CurrentLap;

            foreach (var opp in state.Opponents)
            {
                if (opp.IsPlayer) continue;

                int oppLap = opp.CurrentLap ?? 0;
                if (tracker.TrackedOpponents.ContainsKey(opp.Name)) 
                    oppLap = tracker.TrackedOpponents[opp.Name].HighestLapSeen;

                double posDiff = (myLap + myPos) - (oppLap + (opp.TrackPositionPercent ?? 0));

                if (mode == "AHEAD_CLASS" || mode == "AHEAD_OVERALL" || mode == "AHEAD")
                {
                    if (mode == "AHEAD_CLASS" && opp.CarClass != state.CarClassId) continue;

                    if (posDiff < 0)
                    {
                        double gap = Math.Abs(posDiff);
                        if (gap < minGapAhead)
                        {
                            minGapAhead = gap;
                            found = opp;
                        }
                    }
                }
                else if (mode == "BEHIND_CLASS" || mode == "BEHIND_OVERALL" || mode == "BEHIND")
                {
                    if (mode == "BEHIND_CLASS" && opp.CarClass != state.CarClassId) continue;

                    if (posDiff > 0)
                    {
                        double gap = posDiff;
                        if (gap < minGapBehind)
                        {
                            minGapBehind = gap;
                            found = opp;
                        }
                    }
                }
                else if (mode.StartsWith("P"))
                {
                    if (int.TryParse(mode.Substring(1), out int pPos))
                    {
                        if (pPos == state.Position) isPlayer = true;
                        else if (opp.Position == pPos) found = opp;
                    }
                }
            }

            return found;
        }



        private void GenerateDiagnosis(OpponentTelemetryData opp, double lastLap, double bestLap)
        {
            CurrentTarget.Diagnosis = opp.Diagnosis;
        }



        private string FormatGap(double gapSeconds)

        {

            string sign = gapSeconds < 0 ? "-" : "+";

            double absGap = Math.Abs(gapSeconds);

            TimeSpan t = TimeSpan.FromSeconds(absGap);



            if (t.TotalHours >= 1.0) return sign + (int)t.TotalHours + ":" + t.ToString(@"mm\:ss\.f");

            else if (t.TotalMinutes >= 1.0) return sign + t.ToString(@"mm\:ss\.f");

            else return sign + absGap.ToString("0.0", CultureInfo.InvariantCulture);

        }



        private double GetTireMultiplier(TyreSelectionScope scope)

        {

            switch (scope)

            {

                case TyreSelectionScope.All4: return 1.0;

                case TyreSelectionScope.Fronts:

                case TyreSelectionScope.Rears:

                case TyreSelectionScope.Left:

                case TyreSelectionScope.Right: return 0.5;

                case TyreSelectionScope.FL:

                case TyreSelectionScope.FR:

                case TyreSelectionScope.RL:

                case TyreSelectionScope.RR: return 0.25;

                default: return 0.0;

            }

        }



        private void SetPlayerAsTarget(SessionState state, RaceAnalyzer raceAnalyzer, PitRadar radar, FuelCalculations fuel)

        {

            CurrentTarget.Name = "PLAYER";

            CurrentTarget.ClassPosition = state.Position;

            CurrentTarget.GapSeconds = 0.0;

            CurrentTarget.SignedGapSeconds = 0.0;

            CurrentTarget.GapString = "";

            CurrentTarget.RelativePace = 0.0;

            _relativePace.Reset();
            _lastPaceSample = default(RelativePaceSample);
            CurrentTarget.RelativeGapDelta = 0.0;
            CurrentTarget.RelativeGapDeltaValid = false;
            _myLastMacroSector = -1;

            CurrentTarget.Diagnosis = "TARGET IS YOU";

            CurrentTarget.UndercutViable = false;
            CurrentTarget.OvercutViable = false;
            CurrentTarget.TrafficAlert = false;
            CurrentTarget.UndercutAdvantage = 0.0;
            CurrentTarget.OvercutAdvantage = 0.0;
            CurrentTarget.CurrentMicrosector = 0;

            CurrentTarget.NormalizedRaceStartPace = 0.0;

            double playerEffectiveFuel = state.CurrentFuelLevel;
            double playerFuelPerLap = fuel.AverageFuelPerLap > 0 ? fuel.AverageFuelPerLap : 3.0;
            double playerStintLaps = state.MaxFuelCapacity / playerFuelPerLap;
            double playerTankLaps = fuel.TankLapsRemaining;

            bool playerIsInPit = IsPlayerInPitLane(state);

            if (playerIsInPit)
            {
                playerEffectiveFuel = state.MaxFuelCapacity;
                playerTankLaps = playerStintLaps;
            }

            CurrentTarget.CurrentTank = playerEffectiveFuel;
            CurrentTarget.EstimatedFuelTank = playerEffectiveFuel;
            CurrentTarget.TankLapsRemaining = playerTankLaps;
            CurrentTarget.LapCount = state.CurrentLap;
            CurrentTarget.EstimatedPitWindow = playerTankLaps;
            CurrentTarget.EstimatedPitWindowTargetLap = state.CurrentLap + playerTankLaps;

            CurrentTarget.FuelToAddTime = radar.MeasuredFuelFillRate > 0 ? (fuel.FuelToAdd / radar.MeasuredFuelFillRate) : 0.0;
            CurrentTarget.ProjectedStationaryTime = 0.0;
            CurrentTarget.EstimatedStationaryTime = 0.0;
            CurrentTarget.EstimatedFuelToAdd = 0.0;
            CurrentTarget.EstimatedFuelAdded = 0.0;

            CurrentTarget.PitCount = 0;

            CurrentTarget.PitLaneZoneRacingTime = 0.0;

            CurrentTarget.TargetSectorPaceDropDueToTyres = 0.0;
            CurrentTarget.TargetSectorPaceDropDueToTyresRaw = 0.0;
            CurrentTarget.TargetLapBaselineNormalized = 0.0;
            CurrentTarget.TargetLapBaselineRaw = 0.0;
            CurrentTarget.TargetLapMovingAverageNormalized = 0.0;
            CurrentTarget.TargetLapMovingAverageRaw = 0.0;
            CurrentTarget.TargetSectorBaselineNormalized = 0.0;
            CurrentTarget.TargetSectorBaselineRaw = 0.0;
            CurrentTarget.TargetSectorMovingAverageNormalized = 0.0;
            CurrentTarget.TargetSectorMovingAverageRaw = 0.0;

            CurrentTarget.PrePitNormalizedAverage = raceAnalyzer.PrePitNormalizedAverage;
            Array.Copy(raceAnalyzer.PostPitNormalizedDeltas, CurrentTarget.PostPitNormalizedDeltas, 3);
            Array.Copy(raceAnalyzer.PostPitWarmupPenalties, CurrentTarget.PostPitWarmupPenalties, 3);

            _lastLogSector = -1;

        }



        private void SetNoTarget()

        {

            CurrentTarget.Name = "NO TARGET";

            CurrentTarget.ClassPosition = 0;

            CurrentTarget.GapSeconds = 0.0;

            CurrentTarget.SignedGapSeconds = 0.0;

            CurrentTarget.GapString = "";

            CurrentTarget.RelativePace = 0.0;

            _relativePace.Reset();
            _lastPaceSample = default(RelativePaceSample);
            CurrentTarget.RelativeGapDelta = 0.0;
            CurrentTarget.RelativeGapDeltaValid = false;
            _myLastMacroSector = -1;

            CurrentTarget.Diagnosis = "ANALYZING";

            CurrentTarget.UndercutViable = false;
            CurrentTarget.OvercutViable = false;
            CurrentTarget.TrafficAlert = false;
            CurrentTarget.UndercutAdvantage = 0.0;
            CurrentTarget.OvercutAdvantage = 0.0;
            CurrentTarget.CurrentMicrosector = 0;

            CurrentTarget.NormalizedRaceStartPace = 0.0;

            CurrentTarget.FuelToAddTime = 0.0;
            CurrentTarget.ProjectedStationaryTime = 0.0;
            CurrentTarget.EstimatedStationaryTime = 0.0;
            CurrentTarget.EstimatedFuelToAdd = 0.0;
            CurrentTarget.EstimatedFuelAdded = 0.0;
            CurrentTarget.EstimatedFuelTank = 0.0;
            CurrentTarget.LapCount = 0;
            CurrentTarget.EstimatedPitWindow = 0.0;
            CurrentTarget.EstimatedPitWindowTargetLap = 0.0;

            CurrentTarget.PitCount = 0;

            CurrentTarget.PitLaneZoneRacingTime = 0.0;

            CurrentTarget.TargetSectorPaceDropDueToTyres = 0.0;
            CurrentTarget.TargetSectorPaceDropDueToTyresRaw = 0.0;
            CurrentTarget.TargetLapBaselineNormalized = 0.0;
            CurrentTarget.TargetLapBaselineRaw = 0.0;
            CurrentTarget.TargetLapMovingAverageNormalized = 0.0;
            CurrentTarget.TargetLapMovingAverageRaw = 0.0;
            CurrentTarget.TargetSectorBaselineNormalized = 0.0;
            CurrentTarget.TargetSectorBaselineRaw = 0.0;
            CurrentTarget.TargetSectorMovingAverageNormalized = 0.0;
            CurrentTarget.TargetSectorMovingAverageRaw = 0.0;

            CurrentTarget.PrePitNormalizedAverage = 0.0;
            for (int i = 0; i < 3; i++) CurrentTarget.PostPitNormalizedDeltas[i] = 99.0;
            for (int i = 0; i < 3; i++) CurrentTarget.PostPitWarmupPenalties[i] = 0.0;

            _lastLogSector = -1;

        }



        public void ResetSession()
        {
            LatchedTargetName = null;
            SetNoTarget();
            _relativePace.Reset();
            _lastPaceSample = default(RelativePaceSample);
            CurrentTarget.RelativeGapDelta = 0.0;
            CurrentTarget.RelativeGapDeltaValid = false;
        }

    }

}