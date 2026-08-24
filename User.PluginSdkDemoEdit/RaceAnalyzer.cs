// -------------------------------------------------------------------------

// FILE: RaceAnalyzer.cs

// VERSION: Fix errori 37

// -------------------------------------------------------------------------

using System;

using System.Collections.Generic;

using System.Linq;



namespace SimRIG

{

    public class RaceAnalysisResult

    {

        public double LeaderRaceTotalLaps { get; set; } = 0.0;

        public int LeaderRaceLapsCompleted { get; set; } = 0;

        public double LeaderRaceLapsRemaining { get; set; } = 99.0;



        public double RaceTotalLaps { get; set; } = 0.0;

        public int RaceLapsCompleted { get; set; } = 0;

        public double RaceLapsRemaining { get; set; } = 99.0;



        public bool IsLapped { get; set; } = false;

        public double LeaderEstimatedPace { get; set; } = 0.0;

        public double RaceLifeTimeLeftSec { get; set; } = 0.0;



        public double NormalizedRaceStartPace { get; set; } = 0.0;

        public double EstimatedCurrentPace { get; set; } = 0.0;

        public double PaceDropDueToTyres { get; set; } = 0.0;

        public double SectorPaceDropDueToTyres { get; set; } = 0.0;

        public double SectorPaceDropDueToTyresRaw { get; set; } = 0.0;



        public int PlayerPitCount { get; set; } = 0;

        public int LeaderPitCount { get; set; } = 0;

        public double RemainingPitsPlayer { get; set; } = 0.0;
        public double RemainingPitsLeader { get; set; } = 0.0;
        public double LeaderStintLaps { get; set; } = 0.0;
        public double LeaderAveragePace { get; set; } = 0.0;
        public string LeaderDataSource { get; set; } = "NONE";
        public double LeaderPitLossTime { get; set; } = 0.0;
    }



    public class RaceAnalyzer

    {

        public RaceAnalysisResult Results { get; private set; } = new RaceAnalysisResult();



        private bool _isRaceFinished = false;

        private bool _leaderHasFinished = false;



        private double _latchedLeaderTotalLaps = 0.0;

        private double _latchedPlayerTotalReality = 0.0;



        private double _smoothedLeaderPace = 0.0;

        /// <summary>
        /// Ultima posizione assoluta credibile del leader (giri completati + frazione di giro).
        /// Serve a non ricalcolare la proiezione su un campione vuoto: vedi
        /// <see cref="IsLeaderSampleUsable"/>.
        /// </summary>
        private double _lastGoodLeaderAbsolutePos = -1.0;

        /// <summary>
        /// Ultimo conteggio giri credibile del leader. Come
        /// <see cref="_lastGoodLeaderAbsolutePos"/>, ma alla sorgente: qui protegge la proprieta'
        /// esposta alla dashboard, non solo il calcolo derivato.
        /// </summary>
        private int _lastGoodLeaderLapsCompleted = -1;

        /// <summary>
        /// Protegge la media del passo del leader dai campioni raccolti mentre l'identita' del P1
        /// assoluto sfarfalla — in multiclasse succede di continuo.
        /// </summary>
        private readonly LeaderPaceFilter _leaderPaceFilter = new LeaderPaceFilter();



        public LapSectorTimeContainer RawTimes { get; } = new LapSectorTimeContainer();
        public LapSectorTimeContainer NormalizedTimes { get; } = new LapSectorTimeContainer();

        private int _lastEvaluatedLap = -1;

        private bool _wasInPitLane = false;
        private int _playerLastPitLap = 1;
        private bool _playerTiresChangedThisStop = false;
        private bool _playerLastPitTiresChanged = false;



        private bool _isLatchedForPit = false;

        private double _latchedRaceLapsRemaining = 0.0;
        private DateTime _lastDiagnosticsLogTime = DateTime.MinValue;
        private double _pendingLeaderTotalLaps = 0.0;
        private DateTime _leaderLapsDecreaseStartTime = DateTime.MinValue;
        private double _pendingPlayerTotalReality = 0.0;
        private DateTime _playerLapsDecreaseStartTime = DateTime.MinValue;

        public SectorTracker PlayerExtendedSectorRacingZone { get; private set; } = new SectorTracker { Name = "PlayerExtendedSectorRacingZone" };
        public SectorTracker PlayerExtendedPitZone { get; private set; } = new SectorTracker { Name = "PlayerExtendedPitZone" };

        public List<double> RecentPrePitSectors { get; } = new List<double>();
        public double PrePitNormalizedAverage { get; set; } = 0.0;
        public double[] PostPitNormalizedTimes { get; } = new double[3] { 0.0, 0.0, 0.0 };
        public double[] PostPitNormalizedDeltas { get; } = new double[3] { 99.0, 99.0, 99.0 };
        public double[] PostPitWarmupPenalties { get; } = new double[3] { 0.0, 0.0, 0.0 };
        public int PostPitTransitCount { get; set; } = -1;

        /// <summary>
        /// Penalità minima perché un giro post-pit conti come warmup (spec §29).
        /// Era ricopiata a mano in tre punti: qui, nel gate overcut di TargetStrategyManager
        /// e nell'header dei parametri del log.
        /// </summary>
        public const double WarmupThreshold = 0.10;

        /// <summary>
        /// Quanti giri post-pit il modello di warmup sta contabilizzando **separatamente**.
        ///
        /// Serve a non contare due volte lo stesso effetto (debito Y-11): quei giri, corsi su
        /// gomma fredda, gonfiano la media mobile di LapMovingAverage e quindi PaceDropDueToTyres,
        /// che rappresenta il degrado da usura — poi la stessa lentezza viene ri-sommata come
        /// warmup esplicito. Il prefisso contiguo è lo stesso criterio usato dal gate overcut,
        /// così i due lati restano allineati per costruzione.
        /// </summary>
        public int ActiveWarmupLaps()
        {
            int count = 0;
            for (int i = 0; i < PostPitWarmupPenalties.Length; i++)
            {
                if (PostPitWarmupPenalties[i] >= WarmupThreshold) count++;
                else break;
            }
            return count;
        }

        /// <summary>
        /// Se questo giro va tenuto fuori dalla storia che alimenta il degrado (Y-11).
        /// Estratto come metodo pubblico perché la decisione sia verificabile dai test:
        /// dentro AnalyzePlayerLap sarebbe sepolta in uno stato che i test non possono costruire.
        /// </summary>
        public bool IsLapExcludedFromDegradation(int playerLapsOnTyres)
        {
            return playerLapsOnTyres < ActiveWarmupLaps();
        }

        public RaceAnalyzer() { }

        public void Update(SessionState state, PitRadar radar, OpponentTracker tracker, FuelCalculations fuel, LogManager log, TyreSelectionScope tyreScope, double fuelWeightCoef = 0.03, double tempCoef = 0.05)

        {

            if (!state.IsGameRunning) return;

            bool tiresChanged = (tyreScope == TyreSelectionScope.All4 ||
                                 tyreScope == TyreSelectionScope.Fronts ||
                                 tyreScope == TyreSelectionScope.Rears ||
                                 tyreScope == TyreSelectionScope.Left ||
                                 tyreScope == TyreSelectionScope.Right);

            if (state.IsSessionActive)
            {
                if (radar.HasValidCleanSectorBounds())
                {
                    int playerLapsOnTyres = Math.Max(0, state.CurrentLap - _playerLastPitLap);
                    if (PlayerExtendedSectorRacingZone.Update(
                        state.TrackPositionPercent,
                        state.SessionTimeLeftSec,
                        state.IsInPitLane,
                        radar.IsInExtendedSectorRacingZone(state.TrackPositionPercent),
                        radar.GetExtendedSectorRacingZoneWeight(),
                        state.CurrentFuelLevel,
                        state.TrackTemperature,
                        state.GlobalBaselineTemp,
                        playerLapsOnTyres,
                        state.TrackLengthMeters,
                        _playerLastPitTiresChanged,
                        log,
                        fuelWeightCoef,
                        tempCoef
                    ))
                    {
                        RawTimes.LastSectorTime = PlayerExtendedSectorRacingZone.LastTransitTime;
                        RawTimes.SectorHistory.Add(PlayerExtendedSectorRacingZone.LastTransitTime);
                        NormalizedTimes.LastSectorTime = PlayerExtendedSectorRacingZone.LastNormalizedTime;
                        NormalizedTimes.SectorHistory.Add(PlayerExtendedSectorRacingZone.LastNormalizedTime);

                        RawTimes.BestSectorTime = PlayerExtendedSectorRacingZone.BestRawTime;
                        RawTimes.BestSectorLapCount = PlayerExtendedSectorRacingZone.BestRawTimeLapCount;
                        NormalizedTimes.BestSectorTime = PlayerExtendedSectorRacingZone.BestFreshNormalTime;

                        NormalizedTimes.SectorBaseline = PlayerExtendedSectorRacingZone.BestFreshNormalTime;
                        RawTimes.SectorBaseline = PlayerExtendedSectorRacingZone.BestRawTime;

                        double normalizedSector = PlayerExtendedSectorRacingZone.LastNormalizedTime;
                        if (PostPitTransitCount >= 0 && PostPitTransitCount < 3)
                        {
                            PostPitNormalizedTimes[PostPitTransitCount] = normalizedSector;
                            PostPitNormalizedDeltas[PostPitTransitCount] = normalizedSector - PrePitNormalizedAverage;
                            
                            log.Log(LogModule.SYSTEM, LogType.EVENT, "Player Post-Pit Sector Transit",
                                $"Index: {PostPitTransitCount} | Time: {normalizedSector:F3}s | PrePitAvg: {PrePitNormalizedAverage:F3}s | Delta: {PostPitNormalizedDeltas[PostPitTransitCount]:F3}s");

                            PostPitTransitCount++;
                        }

                        RecentPrePitSectors.Add(normalizedSector);
                        if (RecentPrePitSectors.Count > 3)
                        {
                            RecentPrePitSectors.RemoveAt(0);
                        }

                        if (PlayerExtendedSectorRacingZone.BestFreshNormalTime > 0.0)
                        {
                            int activeTransits = Math.Max(0, Math.Min(3, PostPitTransitCount >= 0 ? PostPitTransitCount : 0));
                            for (int i = 0; i < activeTransits; i++)
                            {
                                double oldPenalty = PostPitWarmupPenalties[i];
                                PostPitWarmupPenalties[i] = Math.Max(0.0, PostPitNormalizedTimes[i] - PlayerExtendedSectorRacingZone.BestFreshNormalTime);
                                if (Math.Abs(PostPitWarmupPenalties[i] - oldPenalty) > 0.001)
                                {
                                    log.Log(LogModule.SYSTEM, LogType.EVENT, "Player Post-Pit Warmup Penalty Update",
                                        $"Index: {i} | Time: {PostPitNormalizedTimes[i]:F3}s | BestFresh: {PlayerExtendedSectorRacingZone.BestFreshNormalTime:F3}s | Penalty: {PostPitWarmupPenalties[i]:F3}s");
                                }
                            }
                        }

                        if (NormalizedTimes.SectorHistory.Count > 0)
                        {
                            NormalizedTimes.SectorMovingAverage = NormalizedTimes.SectorHistory.Skip(Math.Max(0, NormalizedTimes.SectorHistory.Count - 4)).Average();
                        }
                        if (RawTimes.SectorHistory.Count > 0)
                        {
                            RawTimes.SectorMovingAverage = RawTimes.SectorHistory.Skip(Math.Max(0, RawTimes.SectorHistory.Count - 4)).Average();
                        }

                        if (NormalizedTimes.SectorBaseline > 0.0)
                        {
                            NormalizedTimes.SectorPaceDrop = Math.Max(0.0, NormalizedTimes.SectorMovingAverage - NormalizedTimes.SectorBaseline);
                        }
                        if (RawTimes.SectorBaseline > 0.0)
                        {
                            RawTimes.SectorPaceDrop = Math.Max(0.0, RawTimes.SectorMovingAverage - RawTimes.SectorBaseline);
                        }

                        Results.SectorPaceDropDueToTyres = NormalizedTimes.SectorPaceDrop;
                        Results.SectorPaceDropDueToTyresRaw = RawTimes.SectorPaceDrop;

                        if (!PlayerExtendedSectorRacingZone.PendingOutlap)
                        {
                            _playerLastPitTiresChanged = false;
                        }
                    }

                    bool wasInsideExtendedPit = PlayerExtendedPitZone.IsInside;

                    PlayerExtendedPitZone.Update(
                        state.TrackPositionPercent,
                        state.SessionTimeLeftSec,
                        false, // isCarInPit - purely raw stopwatch
                        radar.IsInExtendedPitLaneZone(state.TrackPositionPercent),
                        1.0 - radar.GetExtendedSectorRacingZoneWeight(),
                        0.0, // currentFuel
                        0.0, // trackTemp
                        0.0, // baselineTemp
                        state.CurrentLap,
                        state.TrackLengthMeters,
                        false, // tiresChanged
                        log
                    );

                    if (wasInsideExtendedPit && !PlayerExtendedPitZone.IsInside)
                    {
                        if (radar.LastPlayerStrictPitLaneTime > 0.0)
                        {
                            double playerAccDecTime = PlayerExtendedPitZone.LastTransitTime - radar.LastPlayerStrictPitLaneTime;
                            
                            // Registriamo i dettagli del pit stop per il giocatore nel logger radar per debug e verifica
                            log.Log(LogModule.RADAR, LogType.EVENT, "Player Pit AccDec Details", 
                                $"ExtendedPitZoneTime={PlayerExtendedPitZone.LastTransitTime:F2}s, StrictPitLaneTime={radar.LastPlayerStrictPitLaneTime:F2}s, InOutPitAccDecTime={playerAccDecTime:F2}s, StationaryTime={radar.LastPlayerStationaryTime:F2}s, GlobalDbAccDec={radar.PitInOutAccDecTime:F2}s");

                            if (playerAccDecTime > 0.0)
                            {
                                radar.UpdatePitInOutAccDecTime(playerAccDecTime);
                                log.Log(LogModule.STRATEGY, LogType.EVENT, "AccDec Time Calibrated from Player", $"{playerAccDecTime:F2}s");
                            }
                            radar.ResetLastPlayerStrictPitLaneTime();
                        }
                    }
                }
            }
            else
            {
                PlayerExtendedSectorRacingZone.Reset();
                PlayerExtendedPitZone.Reset();
            }



            if (state.IsInPitLane && !_wasInPitLane)
            {
                Results.PlayerPitCount++;
                if (state.Position == 1) Results.LeaderPitCount = Results.PlayerPitCount;
                _playerTiresChangedThisStop = false;
            }

            if (state.IsInPitLane)
            {
                bool currentScopeChangesTires = (tyreScope == TyreSelectionScope.All4 ||
                                                 tyreScope == TyreSelectionScope.Fronts ||
                                                 tyreScope == TyreSelectionScope.Rears ||
                                                 tyreScope == TyreSelectionScope.Left ||
                                                 tyreScope == TyreSelectionScope.Right);
                if (currentScopeChangesTires)
                {
                    _playerTiresChangedThisStop = true;
                }
            }

            if (!state.IsInPitLane && _wasInPitLane)
            {
                if (_playerTiresChangedThisStop)
                {
                    _playerLastPitLap = state.CurrentLap;
                    _playerLastPitTiresChanged = true;
                    NormalizedTimes.LapBaseline = 0.0;
                    NormalizedTimes.SectorBaseline = 0.0;
                    RawTimes.LapBaseline = 0.0;
                    RawTimes.SectorBaseline = 0.0;
                    Results.NormalizedRaceStartPace = 0.0;
                    PlayerExtendedSectorRacingZone.BestFreshNormalTime = 0.0;
                    PlayerExtendedSectorRacingZone.BestRawTime = 0.0;

                    PrePitNormalizedAverage = RecentPrePitSectors.Count > 0 ? RecentPrePitSectors.Average() : NormalizedTimes.SectorBaseline;
                    PostPitTransitCount = 0;
                    Array.Clear(PostPitNormalizedTimes, 0, 3);
                    for (int i = 0; i < 3; i++) PostPitNormalizedDeltas[i] = 99.0;
                    for (int i = 0; i < 3; i++) PostPitWarmupPenalties[i] = 0.0;

                    log.Log(LogModule.SYSTEM, LogType.EVENT, "Player Pit Stop Reset (Exit)",
                        $"TotalPits: {Results.PlayerPitCount} | TiresChanged: True | ResetLap: {_playerLastPitLap} | PrePitAvg: {PrePitNormalizedAverage:F3}");
                }
                else
                {
                    log.Log(LogModule.SYSTEM, LogType.EVENT, "Player Pit Stop No Reset (Exit)",
                        $"TotalPits: {Results.PlayerPitCount} | TiresChanged: False | CurrentLap: {state.CurrentLap}");
                }
            }

            _wasInPitLane = state.IsInPitLane;



            Results.RaceLapsCompleted = Math.Max(0, state.CurrentLap - 1);

            int leaderCurrentLap = state.CurrentLap;
            // Se il Player e' lui stesso il leader, la posizione del leader e' la sua.
            double leaderTrackPosPct = state.TrackPositionPercent;
            if (state.Position != 1)
            {
                var overallLeader = state.Opponents.FirstOrDefault(o => o.Position == 1);
                if (overallLeader != null)
                {
                    leaderCurrentLap = overallLeader.CurrentLap ?? state.CurrentLap;
                    leaderTrackPosPct = overallLeader.TrackPositionPercent ?? 0.0;
                }
                else
                {
                    leaderCurrentLap = state.CurrentLap + (Results.IsLapped ? 1 : 0);
                }
            }

            int resolvedLeaderLaps = Math.Max(0, leaderCurrentLap - 1);

            // Stesso record vuoto che falsava leaderAbsolutePos (Y-24), qui alla sorgente: il
            // conteggio giri del leader va a zero per circa il 43% dei tick nel replay Daytona.
            // Il calcolo derivato era gia' protetto, ma questa proprieta' finisce **direttamente**
            // sulla dashboard (SimRIG.Session.LeaderRaceLapsCompleted), dove lampeggiava a zero.
            // Si tiene l'ultimo conteggio credibile finche' il record non torna popolato.
            Results.LeaderRaceLapsCompleted = HoldLeaderLapsCompleted(
                resolvedLeaderLaps, leaderTrackPosPct, ref _lastGoodLeaderLapsCompleted);



            if (state.CurrentLap != _lastEvaluatedLap)

            {

                if (_lastEvaluatedLap > 0 && state.CurrentLap > 1)

                {

                    Results.IsLapped = (Results.LeaderRaceLapsCompleted > Results.RaceLapsCompleted);



                    int playerLapsOnTyres = Math.Max(0, Results.RaceLapsCompleted - _playerLastPitLap);
                    double distanceOnTyres = playerLapsOnTyres * state.TrackLengthMeters;
                    AnalyzePlayerLap(state.LastLapTimeSec, state.CurrentFuelLevel, state.TrackTemperature, state.IsInPitLane, state.Flag_Black, Results.RaceLapsCompleted, state.GlobalBaselineTemp, fuelWeightCoef, tempCoef, distanceOnTyres, playerLapsOnTyres, log);



                    log.Log(LogModule.STRATEGY, LogType.EVENT, "Race Projections Update",

                        $"L_Rem: {Results.LeaderRaceLapsRemaining:F2} | P_Rem: {Results.RaceLapsRemaining:F2} | L_Pace: {Results.LeaderEstimatedPace:F3}");

                }

                _lastEvaluatedLap = state.CurrentLap;

            }



            double effSessionTimeLeft = state.SessionTimeLeftSec;

            double effTrackPos = state.SessionStateStatus < 4 ? 0.0 : state.TrackPositionPercent;



            if (state.SessionStateStatus < 4 || state.CurrentLap == 0)

            {

                effSessionTimeLeft = state.RawSessionTime;

                effTrackPos = 0.0;

            }



            if (state.SessionStateStatus >= 4)

            {

                if (state.GlobalCheckeredFlag || state.SessionStateStatus >= 5) _leaderHasFinished = true;

            }

            else _leaderHasFinished = false;



            if (state.PlayerCheckeredFlag)

            {

                if ((effTrackPos > 0.05 && effTrackPos < 0.15) || (state.CurrentLapTimeSec > 5.0 && state.CurrentLapTimeSec < 15.0))

                {

                    _isRaceFinished = true;

                }

            }



            if (!state.IsRaceSession || _isRaceFinished)

            {

                Results.RaceLapsRemaining = 0.0;

                Results.RaceTotalLaps = 0.0;

                Results.LeaderRaceLapsRemaining = 0.0;

                Results.LeaderRaceTotalLaps = 0.0;

                Results.RaceLifeTimeLeftSec = 0.0;

                _isLatchedForPit = false;

                return;

            }



            double rawLeaderPace = 120.0;
            // Identita' a cui appartiene il campione di passo: serve al filtro per accorgersi
            // che il P1 assoluto sta sfarfallando fra piu' piloti (tipico del multiclasse).
            string rawLeaderName = "PLAYER";

            if (state.Position == 1)
            {
                if (Results.NormalizedRaceStartPace > 0)
                {
                    rawLeaderPace = Results.NormalizedRaceStartPace;
                }
                else if (state.BestLapTimeSec > 0)
                {
                    rawLeaderPace = state.BestLapTimeSec;
                }
            }
            else
            {
                var overallLeader = state.Opponents.FirstOrDefault(o => o.Position == 1);
                if (overallLeader != null) rawLeaderName = overallLeader.Name ?? "";
                if (overallLeader != null && tracker.TrackedOpponents.TryGetValue(overallLeader.Name, out var leaderData))
                {
                    if (leaderData.NormalizedTimes.LapMovingAverage > 0.0)
                    {
                        rawLeaderPace = leaderData.NormalizedTimes.LapMovingAverage;
                    }
                    else if (leaderData.NormalizedTimes.BestLapTime > 0.0)
                    {
                        rawLeaderPace = leaderData.NormalizedTimes.BestLapTime;
                    }
                    else if (state.BestLapTimeSec > 0)
                    {
                        rawLeaderPace = state.BestLapTimeSec;
                    }
                }
                else if (state.BestLapTimeSec > 0)
                {
                    rawLeaderPace = state.BestLapTimeSec + 0.5;
                }
            }



            // La media mobile e' la stessa di prima (alpha 0.10): cambia quali campioni ci entrano.
            // Vengono scartati quelli fisicamente impossibili e quelli raccolti mentre l'identita'
            // del leader sta ancora sfarfallando. Vedi LeaderPaceFilter.
            _smoothedLeaderPace = _leaderPaceFilter.Update(rawLeaderPace, rawLeaderName,
                                                           state.SessionTimeLeftSec, state.TrackLengthMeters);

            Results.LeaderEstimatedPace = _smoothedLeaderPace;



            double activePlayerPace = Results.NormalizedRaceStartPace > 0 ? Results.NormalizedRaceStartPace : 120.0;



            double leaderAbsolutePos = Results.LeaderRaceLapsCompleted + 0.0;
            if (state.Position == 1)
            {
                leaderAbsolutePos = Results.RaceLapsCompleted + effTrackPos;
                _lastGoodLeaderAbsolutePos = leaderAbsolutePos;
            }
            else
            {
                var overallLeader = state.Opponents.FirstOrDefault(o => o.Position == 1);
                if (overallLeader != null && overallLeader.TrackPositionPercent.HasValue)
                {
                    leaderAbsolutePos = Results.LeaderRaceLapsCompleted + overallLeader.TrackPositionPercent.Value;
                }

                // Un record del leader momentaneamente vuoto (posizione e giri a zero) non e' il
                // leader sul traguardo: e' un buco nella telemetria. Preso per buono azzerava
                // leaderAbsolutePos, e LeaderRaceLapsRemaining diventava il totale latchato intero
                // — 30.00 invece dei ~18 reali, ai giri 12-15 del replay Daytona del 2026-08-23.
                // Si tiene l'ultima posizione credibile finche' il record non torna popolato.
                if (!IsLeaderSampleUsable(Results.LeaderRaceLapsCompleted,
                                          overallLeader != null && overallLeader.TrackPositionPercent.HasValue
                                              ? overallLeader.TrackPositionPercent.Value
                                              : 0.0))
                {
                    if (_lastGoodLeaderAbsolutePos >= 0.0)
                    {
                        leaderAbsolutePos = _lastGoodLeaderAbsolutePos;
                    }
                }
                else
                {
                    _lastGoodLeaderAbsolutePos = leaderAbsolutePos;
                }
            }

            double playerAbsolutePos = Results.RaceLapsCompleted + effTrackPos;



            double leaderLapsRem = 99.0;

            double rawPlayerRemaining = 0.0;



            bool isApproachingPit = (effTrackPos > 0.85 && state.SpeedKmh < 100.0 && state.SpeedKmh > 10.0);

            bool shouldLatch = state.IsInPitLane || isApproachingPit;



            if (shouldLatch && !_isLatchedForPit)

            {

                _isLatchedForPit = true;

                _latchedRaceLapsRemaining = Results.RaceLapsRemaining;

            }

            else if (!shouldLatch && !state.IsInPitLane && _isLatchedForPit)

            {

                if (state.SpeedKmh > 120.0) _isLatchedForPit = false;

            }



            if (_leaderHasFinished)

            {

                leaderLapsRem = 0.0;

                double projectedPlayerTotal = Math.Ceiling(playerAbsolutePos + (1.0 - effTrackPos));

                _latchedPlayerTotalReality = UpdateLatchedLaps(projectedPlayerTotal, _latchedPlayerTotalReality, !state.IsInPitLane);

            }

            else

            {

                if (state.IsLapLimited && !state.IsTimeLimited && state.TotalLaps > 0)

                {

                    _latchedLeaderTotalLaps = state.TotalLaps;

                    _latchedPlayerTotalReality = state.TotalLaps;

                    leaderLapsRem = Math.Max(0, state.TotalLaps - leaderAbsolutePos);

                    Results.RaceLifeTimeLeftSec = leaderLapsRem * _smoothedLeaderPace;

                }
                else
                {
                    double timeUntilZero = Math.Max(0, effSessionTimeLeft);
                    
                    // 1. Identificazione del Leader e reperimento parametri
                    string leaderName = "";
                    string leaderClass = state.CarClassId;
                    if (state.Position != 1)
                    {
                        var overallLeader = state.Opponents.FirstOrDefault(o => o.Position == 1);
                        if (overallLeader != null)
                        {
                            leaderName = overallLeader.Name;
                            leaderClass = overallLeader.CarClass;
                        }
                    }
                    else
                    {
                        leaderName = "PLAYER";
                    }

                    bool isMultiClass = state.Opponents.Select(o => o.CarClass).Distinct().Count() > 1;

                    double leaderStintLaps = 0.0;
                    double leaderPace = _smoothedLeaderPace > 0.0 ? _smoothedLeaderPace : (state.BestLapTimeSec > 0.0 ? state.BestLapTimeSec : 120.0);
                    double leaderPitLoss = 0.0;
                    double leaderTankLapsRemaining = 99.0;
                    string leaderSource = "NONE";

                    if (!isMultiClass)
                    {
                        // Gara Single-Class: usiamo i parametri ricavati dal Player nel database
                        var dbPlayerRecord = radar.CurrentTrack;
                        if (dbPlayerRecord != null)
                        {
                            leaderStintLaps = dbPlayerRecord.AverageStintLaps;
                            leaderPace = dbPlayerRecord.AverageLapPace > 0.0 ? dbPlayerRecord.AverageLapPace : leaderPace;
                            leaderPitLoss = dbPlayerRecord.PitTransitTime + dbPlayerRecord.PitInOutAccDecTime + 25.0; // transito + sosta media
                            leaderSource = "SINGLE_CLASS_DB";
                        }
                        
                        if (leaderStintLaps <= 0.0)
                        {
                            double pFuelPerLap = fuel.AverageFuelPerLap > 0.0 ? fuel.AverageFuelPerLap : 3.0;
                            leaderStintLaps = state.MaxFuelCapacity / pFuelPerLap;
                            leaderPitLoss = radar.PitTransitTime + radar.PitInOutAccDecTime + 25.0;
                            leaderSource = "SINGLE_CLASS_LIVE";
                        }
                        
                        leaderTankLapsRemaining = fuel.TankLapsRemaining;
                    }
                    else
                    {
                        // Gara Multi-Class: cascata di priorità per il leader
                        // Priorità 1: Database JSON
                        var dbLeaderRecord = radar.Database.Tracks.FirstOrDefault(t => t.TrackID == state.TrackId && t.CarClass == leaderClass);
                        if (dbLeaderRecord != null && dbLeaderRecord.AverageStintLaps > 0.0)
                        {
                            leaderStintLaps = dbLeaderRecord.AverageStintLaps;
                            leaderPace = dbLeaderRecord.AverageLapPace > 0.0 ? dbLeaderRecord.AverageLapPace : leaderPace;
                            leaderPitLoss = dbLeaderRecord.PitTransitTime + dbLeaderRecord.PitInOutAccDecTime + 25.0;
                            leaderSource = "DATABASE";
                        }
                        
                        // Priorità 2: Apprendimento Live
                        if (leaderSource == "NONE" && state.Position != 1)
                        {
                            if (tracker.TrackedOpponents.TryGetValue(leaderName, out var leaderData))
                            {
                                leaderStintLaps = leaderData.LastStintLaps;
                                leaderPace = leaderData.NormalizedTimes.LapMovingAverage > 0.0 ? leaderData.NormalizedTimes.LapMovingAverage : leaderPace;
                                leaderPitLoss = leaderData.LastPitStationaryTimeSec + leaderData.LastPitInOutAccDecTimeSec;
                                if (leaderPitLoss <= 0.0 && tracker.ClassBestExtendedPitZoneTime > 0.0)
                                {
                                    leaderPitLoss = tracker.ClassBestExtendedPitZoneTime + 25.0;
                                }
                                if (leaderStintLaps > 0.0)
                                {
                                    leaderSource = "LIVE";
                                }
                            }
                        }
                        
                        // Calcoliamo il serbatoio rimanente del leader
                        if (state.Position == 1)
                        {
                            leaderTankLapsRemaining = fuel.TankLapsRemaining;
                        }
                        else if (tracker.TrackedOpponents.TryGetValue(leaderName, out var leaderData))
                        {
                            double dbLeaderFuelPerLap = (dbLeaderRecord != null && dbLeaderRecord.FuelPerLap > 0.0) ? dbLeaderRecord.FuelPerLap : 3.0;
                            leaderTankLapsRemaining = leaderData.EstimatedFuelTank / dbLeaderFuelPerLap;
                        }
                    }

                    // Log della sorgente del leader (solo in caso di variazione significativa o ad intervalli)
                    if (state.CurrentLap % 5 == 0 && state.CurrentLap != _lastEvaluatedLap)
                    {
                        log.Log(LogModule.STRATEGY, LogType.FLOW, "Leader Strategy Parameters",
                            $"Source: {leaderSource} | Class: {leaderClass} | StintLaps: {leaderStintLaps:F1} | Pace: {leaderPace:F2} | PitLoss: {leaderPitLoss:F1}s");
                    }

                    // 2. Calcolo dei giri netti rimanenti del Leader tramite formula analitica
                    double leaderL_left = timeUntilZero / leaderPace;
                    // Le soste residue del leader restano un dato diagnostico (RemainingPitsLeader).
                    // Non entrano piu' nel tempo alla bandiera: quello ora viene dal countdown, che
                    // il tempo perso ai box lo contiene gia' per costruzione.
                    int leaderRemainingStops = 0;

                    bool leaderIsInPit = false;
                    if (state.Position == 1)
                    {
                        leaderIsInPit = state.IsInPitLane;
                    }
                    else if (tracker.TrackedOpponents.TryGetValue(leaderName, out var leaderData))
                    {
                        leaderIsInPit = leaderData.IsInsideGeofence;
                    }

                    double effectiveLeaderTank = leaderTankLapsRemaining;
                    if (leaderIsInPit)
                    {
                        effectiveLeaderTank = leaderStintLaps;
                    }

                    if (leaderSource != "NONE" && leaderStintLaps > 0.0 && leaderPitLoss > 0.0)
                    {
                        if (leaderL_left > effectiveLeaderTank)
                        {
                            double J_leader = leaderPitLoss / leaderStintLaps;
                            leaderL_left = (timeUntilZero - leaderPitLoss + effectiveLeaderTank * J_leader) / (leaderPace + J_leader);
                            
                            leaderRemainingStops = 1 + (int)Math.Max(0, Math.Ceiling((leaderL_left - effectiveLeaderTank) / leaderStintLaps) - 1);
                        }
                    }

                    double leaderPosAtZero = leaderAbsolutePos + leaderL_left;
                    double targetLeaderTotal = UpdateLatchedLaps(leaderPosAtZero, _latchedLeaderTotalLaps, !leaderIsInPit);
                    if (targetLeaderTotal < _latchedLeaderTotalLaps)
                    {
                        if (_pendingLeaderTotalLaps != targetLeaderTotal)
                        {
                            _pendingLeaderTotalLaps = targetLeaderTotal;
                            _leaderLapsDecreaseStartTime = DateTime.Now;
                        }
                        else if ((DateTime.Now - _leaderLapsDecreaseStartTime).TotalSeconds >= 30.0)
                        {
                            _latchedLeaderTotalLaps = targetLeaderTotal;
                            _pendingLeaderTotalLaps = 0.0;
                        }
                    }
                    else
                    {
                        _latchedLeaderTotalLaps = targetLeaderTotal;
                        _pendingLeaderTotalLaps = 0.0;
                    }

                    leaderLapsRem = Math.Max(0, _latchedLeaderTotalLaps - leaderAbsolutePos);

                    // Il tempo che manca alla bandiera si legge dal cronometro di sessione, non si
                    // ricostruisce dai giri del leader. Prima era
                    //     leaderLapsRem * leaderPace + leaderRemainingPitTime
                    // cioe' un conteggio *latchato* di giri rimoltiplicato per un passo che nel
                    // frattempo poteva essere cambiato: quando il P1 assoluto cambiava classe, il
                    // risultato divergeva senza limite (Daytona 2026-08-23, giri 12-15: 2368 s
                    // stimati contro ~1400 s reali). Ancorando al countdown, il passo del leader
                    // pesa solo sulla frazione di giro che gli manca per tagliare: errore limitato
                    // a un giro, non piu' proporzionale alla durata della gara.
                    Results.RaceLifeTimeLeftSec = RaceTimeProjection.TimeUntilLeaderCheckered(
                        timeUntilZero, leaderAbsolutePos, leaderPace);

                    // Salviamo i risultati del leader nei campi di debug
                    Results.RemainingPitsLeader = leaderRemainingStops;
                    Results.LeaderStintLaps = leaderStintLaps;
                    Results.LeaderAveragePace = leaderPace;
                    Results.LeaderDataSource = leaderSource;
                    Results.LeaderPitLossTime = leaderPitLoss;

                    // 3. Calcolo dei giri netti rimanenti del Player tramite formula analitica
                    if (!_isLatchedForPit)
                    {
                        double playerL_left = Results.RaceLifeTimeLeftSec / activePlayerPace;
                        
                        double playerFuelPerLap = fuel.AverageFuelPerLap > 0.0 ? fuel.AverageFuelPerLap : 3.0;
                        double playerStintLaps = state.MaxFuelCapacity / playerFuelPerLap;
                        double playerPitLoss = radar.PitTransitTime + radar.PitInOutAccDecTime + Math.Max(fuel.FuelToAdd / (radar.MeasuredFuelFillRate > 0 ? radar.MeasuredFuelFillRate : 2.7), radar.DbTireChangeTime);
                        int playerRemainingStops = 0;

                        double effectivePlayerTank = fuel.TankLapsRemaining;
                        if (shouldLatch) // Se stiamo entrando ai box o siamo già in pitlane, consideriamo il serbatoio pieno per la proiezione futura
                        {
                            effectivePlayerTank = playerStintLaps;
                        }

                        if (playerL_left > effectivePlayerTank && playerStintLaps > 0.0 && playerPitLoss > 0.0)
                        {
                            double J_player = playerPitLoss / playerStintLaps;
                            playerL_left = (Results.RaceLifeTimeLeftSec - playerPitLoss + effectivePlayerTank * J_player) / (activePlayerPace + J_player);
                            playerRemainingStops = 1 + (int)Math.Max(0, Math.Ceiling((playerL_left - effectivePlayerTank) / playerStintLaps) - 1);
                        }

                        Results.RemainingPitsPlayer = playerRemainingStops;

                        double playerPosWhenLeaderFinishes = playerAbsolutePos + playerL_left;
                        double projectedPlayerTotal = Math.Ceiling(playerPosWhenLeaderFinishes);

                        if (state.Position == 1)
                        {
                            projectedPlayerTotal = _latchedLeaderTotalLaps;
                        }
                        else if (!isMultiClass)
                        {
                            // Monoclasse: "non puoi completare piu' giri del leader" e' un tetto
                            // sensato, il leader gira nella stessa vettura.
                            projectedPlayerTotal = Math.Min(projectedPlayerTotal, _latchedLeaderTotalLaps);
                        }
                        // Multiclasse: nessun tetto sul leader **assoluto**. Una GT3 e una GTP non
                        // fanno lo stesso numero di giri, quindi il confronto non dice nulla — e se
                        // il totale del leader e' sovrastimato il tetto non protegge comunque.
                        // Il tetto giusto sarebbe il leader **di classe**: vedi Y-19, va tarato sui
                        // log Daytona prima di cablarlo (serve la posizione assoluta degli avversari,
                        // che ha convenzioni di conteggio giri tutte da verificare).

                        double targetPlayerTotal = UpdateLatchedLaps(projectedPlayerTotal, _latchedPlayerTotalReality, !state.IsInPitLane);
                        if (targetPlayerTotal < _latchedPlayerTotalReality)
                        {
                            if (_pendingPlayerTotalReality != targetPlayerTotal)
                            {
                                _pendingPlayerTotalReality = targetPlayerTotal;
                                _playerLapsDecreaseStartTime = DateTime.Now;
                            }
                            else if ((DateTime.Now - _playerLapsDecreaseStartTime).TotalSeconds >= 30.0)
                            {
                                _latchedPlayerTotalReality = targetPlayerTotal;
                                _pendingPlayerTotalReality = 0.0;
                            }
                        }
                        else
                        {
                            _latchedPlayerTotalReality = targetPlayerTotal;
                            _pendingPlayerTotalReality = 0.0;
                        }
                        if (_latchedLeaderTotalLaps > 0.0)
                        {
                            _latchedPlayerTotalReality = Math.Min(_latchedPlayerTotalReality, _latchedLeaderTotalLaps);
                        }
                    }
                }
            }



            rawPlayerRemaining = Math.Max(0, _latchedPlayerTotalReality - playerAbsolutePos);

            if (state.SessionStateStatus < 4 && effTrackPos == 0.0 && state.IsLapLimited && !state.IsTimeLimited)

                rawPlayerRemaining -= 1.0;



            Results.LeaderRaceTotalLaps = _latchedLeaderTotalLaps;

            Results.LeaderRaceLapsRemaining = Math.Truncate(leaderLapsRem * 100) / 100.0;

            Results.RaceTotalLaps = _latchedPlayerTotalReality;



            if (_isLatchedForPit && !_leaderHasFinished)
            {
                Results.RaceLapsRemaining = _latchedRaceLapsRemaining;
            }
            else
            {
                Results.RaceLapsRemaining = Math.Truncate(rawPlayerRemaining * 100) / 100.0;
            }

            if (state.IsSessionActive && (DateTime.Now - _lastDiagnosticsLogTime).TotalSeconds >= 1.0)
            {
                _lastDiagnosticsLogTime = DateTime.Now;

                string leaderName = "PLAYER";
                double leaderTrackPos = state.TrackPositionPercent;
                int leaderLapsCompleted = Results.LeaderRaceLapsCompleted;
                double leaderFuelToAdd = 0.0;
                bool leaderIsInPit = false;

                if (state.Position != 1)
                {
                    var overallLeader = state.Opponents.FirstOrDefault(o => o.Position == 1);
                    if (overallLeader != null)
                    {
                        leaderName = overallLeader.Name;
                        leaderTrackPos = overallLeader.TrackPositionPercent ?? 0.0;

                        if (tracker.TrackedOpponents.TryGetValue(leaderName, out var leaderData))
                        {
                            leaderIsInPit = leaderData.IsInsideGeofence;
                            double fuelPerLap = fuel.AverageFuelPerLap > 0 ? fuel.AverageFuelPerLap : 3.0;
                            leaderFuelToAdd = (Results.LeaderRaceLapsRemaining * fuelPerLap) + (0.3 * fuelPerLap) - leaderData.EstimatedFuel;
                            if (leaderFuelToAdd < 0.0) leaderFuelToAdd = 0.0;
                            leaderFuelToAdd = Math.Min(state.MaxFuelCapacity, leaderFuelToAdd);
                        }
                    }
                }
                else
                {
                    leaderIsInPit = state.IsInPitLane;
                    leaderFuelToAdd = fuel.FuelToAdd;
                }

                log.Log(LogModule.STRATEGY, LogType.FLOW, "RaceProjectionsDiagnostics",
                    $"TimeLeft: {state.SessionTimeLeftSec:F1}s | " +
                    $"Player: Lap={state.CurrentLap}, PosPct={state.TrackPositionPercent:F4}, LapsComp={Results.RaceLapsCompleted}, LapsRem={Results.RaceLapsRemaining:F2}, FuelToAdd={fuel.FuelToAdd:F2}L, IsInPit={state.IsInPitLane}, Latched={_isLatchedForPit}, LatchedVal={_latchedRaceLapsRemaining:F2}, LatchedReality={_latchedPlayerTotalReality:F2} | " +
                    $"Leader ({leaderName}): PosPct={leaderTrackPos:F4}, LapsComp={leaderLapsCompleted}, LapsRem={Results.LeaderRaceLapsRemaining:F2}, FuelToAdd={leaderFuelToAdd:F2}L, IsInPit={leaderIsInPit}, LatchedTotal={_latchedLeaderTotalLaps:F2}");
            }
        }



        private void AnalyzePlayerLap(double lapTime, double currentFuel, double trackTemp, bool isInPit, int blackFlag, int raceLapsCompleted, double globalBaselineTemp, double fuelWeightCoef, double tempCoef, double distanceOnTyres, int playerLapsOnTyres, LogManager log)

        {

            if (isInPit || lapTime <= 0 || blackFlag > 0) return;



            double fuelPenalty = currentFuel * fuelWeightCoef;

            double tempPenalty = globalBaselineTemp > 0 ? ((trackTemp - globalBaselineTemp) * tempCoef) : 0.0;



            double normalizedLap = lapTime - fuelPenalty - tempPenalty;



            RawTimes.LastLapTime = lapTime;

            NormalizedTimes.LastLapTime = normalizedLap;



            bool isValidForBaseline = true;



            if (raceLapsCompleted < 2) isValidForBaseline = false;



            if (isValidForBaseline)

            {

                if (distanceOnTyres <= 40000.0)

                {

                    if (NormalizedTimes.LapBaseline == 0.0)

                    {

                        NormalizedTimes.LapBaseline = normalizedLap;

                        log.Log(LogModule.STRATEGY, LogType.EVENT, "Player Normalized Lap Baseline Established", $"{NormalizedTimes.LapBaseline:F3}");

                    }

                    else if (normalizedLap < NormalizedTimes.LapBaseline)

                    {

                        if (NormalizedTimes.LapBaseline - normalizedLap > 1.5)

                        {

                            NormalizedTimes.LapBaseline = normalizedLap;

                            NormalizedTimes.LapHistory.Clear();

                            RawTimes.LapHistory.Clear();

                            log.Log(LogModule.STRATEGY, LogType.EVENT, "Player Normalized Lap Baseline Reset", $"New Base: {NormalizedTimes.LapBaseline:F3} (Drastic Improvement)");

                        }

                        else

                        {

                            NormalizedTimes.LapBaseline = normalizedLap;

                            log.Log(LogModule.STRATEGY, LogType.EVENT, "Player Normalized Lap Baseline Updated (Better)", $"{NormalizedTimes.LapBaseline:F3}");

                        }

                    }



                    if (RawTimes.LapBaseline == 0.0)

                    {

                        RawTimes.LapBaseline = lapTime;

                        log.Log(LogModule.STRATEGY, LogType.EVENT, "Player Raw Lap Baseline Established", $"{RawTimes.LapBaseline:F3}");

                    }

                    else if (lapTime < RawTimes.LapBaseline)

                    {

                        if (RawTimes.LapBaseline - lapTime > 1.5)

                        {

                            RawTimes.LapBaseline = lapTime;

                            log.Log(LogModule.STRATEGY, LogType.EVENT, "Player Raw Lap Baseline Reset", $"New Base: {RawTimes.LapBaseline:F3} (Drastic Improvement)");

                        }

                        else

                        {

                            RawTimes.LapBaseline = lapTime;

                            log.Log(LogModule.STRATEGY, LogType.EVENT, "Player Raw Lap Baseline Updated (Better)", $"{RawTimes.LapBaseline:F3}");

                        }

                    }

                }

            }



            // Y-11: i giri ancora nella finestra di warmup non entrano nella storia da cui si
            // ricava il degrado. Sono lenti per la gomma fredda, non per l'usura, e quella
            // lentezza è già contabilizzata a parte come warmup: sommarla anche qui la conterebbe
            // due volte. Fuori dalla finestra il giro rientra normalmente.
            int warmupLapsActive = ActiveWarmupLaps();
            bool isWarmupLap = IsLapExcludedFromDegradation(playerLapsOnTyres);

            if (isWarmupLap)
            {
                log.Log(LogModule.STRATEGY, LogType.EVENT, "Player Lap Excluded From Degradation",
                    $"lapsOnTyres={playerLapsOnTyres} | warmupLapsActive={warmupLapsActive} | normalizedLap={normalizedLap:F3} | reason=ancora in warmup, gia' contato dal modello");
            }
            else
            {
                NormalizedTimes.LapHistory.Add(normalizedLap);

                RawTimes.LapHistory.Add(lapTime);
            }



            if (RawTimes.BestLapTime == 0.0 || lapTime < RawTimes.BestLapTime)

            {

                RawTimes.BestLapTime = lapTime;

                RawTimes.BestLapLapCount = raceLapsCompleted;

            }

            if (NormalizedTimes.BestLapTime == 0.0 || normalizedLap < NormalizedTimes.BestLapTime)

            {

                NormalizedTimes.BestLapTime = normalizedLap;

                NormalizedTimes.BestLapLapCount = raceLapsCompleted;

            }



            if (NormalizedTimes.LapHistory.Count > 0)

            {

                NormalizedTimes.LapMovingAverage = NormalizedTimes.LapHistory.Skip(Math.Max(0, NormalizedTimes.LapHistory.Count - 4)).Average();

            }

            if (RawTimes.LapHistory.Count > 0)

            {

                RawTimes.LapMovingAverage = RawTimes.LapHistory.Skip(Math.Max(0, RawTimes.LapHistory.Count - 4)).Average();

            }



            if (NormalizedTimes.LapBaseline > 0.0)

            {

                NormalizedTimes.LapPaceDrop = Math.Max(0.0, NormalizedTimes.LapMovingAverage - NormalizedTimes.LapBaseline);

            }

            if (RawTimes.LapBaseline > 0.0)

            {

                RawTimes.LapPaceDrop = Math.Max(0.0, RawTimes.LapMovingAverage - RawTimes.LapBaseline);

            }



            Results.NormalizedRaceStartPace = NormalizedTimes.LapBaseline;

            Results.PaceDropDueToTyres = NormalizedTimes.LapPaceDrop;



            if (NormalizedTimes.LapHistory.Count > 0)

            {

                Results.EstimatedCurrentPace = Results.NormalizedRaceStartPace + Results.PaceDropDueToTyres + fuelPenalty + tempPenalty;

            }

            else

            {

                Results.EstimatedCurrentPace = lapTime;

            }



            log.Log(LogModule.STRATEGY, LogType.EVENT, "Player Pace Analysis",

                $"EstimatedCurrent: {Results.EstimatedCurrentPace:F3} | Baseline: {Results.NormalizedRaceStartPace:F3} | Deg: {Results.PaceDropDueToTyres:F2}");

        }



        /// <summary>
        /// Il campione del leader è utilizzabile, o è un buco nella telemetria?
        ///
        /// Un avversario a <c>TrackPositionPercent == 0</c> e <c>CurrentLap</c> a zero non è sul
        /// traguardo al primo giro: è un record momentaneamente vuoto. È la stessa convenzione
        /// "posizione zero = stato azzerato" già usata da <see cref="TrackPositionValidator"/> e da
        /// <c>PitRadar</c>, e <c>OpponentTracker</c> scarta quei tick già oggi
        /// (<c>if (currentPos == 0.0) continue;</c>) — mancava solo qui.
        ///
        /// Misurato sul replay Daytona del 2026-08-23: ai giri 12-15 il leader risultava a
        /// <c>LapsComp=0, PosPct=0.0000</c>, quindi <c>leaderAbsolutePos</c> valeva 0 e
        /// <c>LeaderRaceLapsRemaining</c> diventava il totale latchato intero (30.00) invece dei
        /// ~18 reali.
        /// </summary>
        public static bool IsLeaderSampleUsable(int leaderLapsCompleted, double leaderTrackPositionPct)
        {
            if (leaderTrackPositionPct > 0.0) return true;

            // Posizione a zero è accettabile solo se il conteggio giri dice che la gara è
            // davvero iniziata: altrimenti il record è vuoto.
            return leaderLapsCompleted > 0;
        }

        /// <summary>
        /// Restituisce il conteggio giri del leader da esporre, tenendo l'ultimo credibile quando
        /// il campione e' vuoto. Statico e con lo stato passato per riferimento perche' i test
        /// esercitino **questa** logica invece di riprodurla: la prima versione del test la
        /// ricopiava, e restava verde anche neutralizzando il guard nel codice di produzione.
        /// </summary>
        public static int HoldLeaderLapsCompleted(int rawLapsCompleted, double leaderTrackPositionPct,
                                                  ref int lastGoodLapsCompleted)
        {
            if (!IsLeaderSampleUsable(rawLapsCompleted, leaderTrackPositionPct)
                && lastGoodLapsCompleted >= 0)
            {
                return lastGoodLapsCompleted;
            }

            lastGoodLapsCompleted = rawLapsCompleted;
            return rawLapsCompleted;
        }

        private double UpdateLatchedLaps(double rawProjectedPos, double currentLatchedLaps, bool allowDecrease = true)
        {
            if (currentLatchedLaps == 0.0) return Math.Ceiling(rawProjectedPos);
            if (rawProjectedPos > currentLatchedLaps + 0.05) return Math.Ceiling(rawProjectedPos - 0.05);
            if (allowDecrease && rawProjectedPos < currentLatchedLaps - 1.05) return Math.Ceiling(rawProjectedPos + 0.05);
            return currentLatchedLaps;
        }



        public void ResetSession()

        {

            Results = new RaceAnalysisResult();

            _isRaceFinished = false;

            _leaderHasFinished = false;

            _latchedLeaderTotalLaps = 0.0;

            _latchedPlayerTotalReality = 0.0;
            _pendingLeaderTotalLaps = 0.0;
            _leaderLapsDecreaseStartTime = DateTime.MinValue;
            _pendingPlayerTotalReality = 0.0;
            _playerLapsDecreaseStartTime = DateTime.MinValue;

            _smoothedLeaderPace = 0.0;
            _leaderPaceFilter.Reset();
            _lastGoodLeaderAbsolutePos = -1.0;
            _lastGoodLeaderLapsCompleted = -1;

            _lastEvaluatedLap = -1;

            _wasInPitLane = false;

            _isLatchedForPit = false;

            _playerLastPitLap = 1;
            _playerTiresChangedThisStop = false;
            _playerLastPitTiresChanged = false;

            RawTimes.Reset();
            NormalizedTimes.Reset();
            PlayerExtendedSectorRacingZone.Reset();
            PlayerExtendedPitZone.Reset();

            RecentPrePitSectors.Clear();
            PrePitNormalizedAverage = 0.0;
            Array.Clear(PostPitNormalizedTimes, 0, 3);
            for (int i = 0; i < 3; i++) PostPitNormalizedDeltas[i] = 99.0;
            for (int i = 0; i < 3; i++) PostPitWarmupPenalties[i] = 0.0;
            PostPitTransitCount = -1;
        }

    }

}