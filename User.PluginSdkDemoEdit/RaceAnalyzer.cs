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

        /// <summary>
        /// Dove si trovera' il **leader assoluto** quando scade il cronometro, in giri completati
        /// piu' frazione di giro (es. <c>38.85</c>). E' il gemello di
        /// <see cref="ProjectedPosAtCheckered"/> per il leader, ed e' il numero da cui
        /// <see cref="LeaderRaceTotalLaps"/> deriva per arrotondamento all'intero superiore.
        ///
        /// Esposto per lo stesso motivo del suo gemello: senza il decimale non si distingue una
        /// proiezione a 39.05 da una a 39.95 — entrambe mostrano 40 — e quindi non si puo' dire se
        /// un totale sbagliato di un giro venga da un errore piccolo appena sopra la soglia o da uno
        /// grande. Il software di riferimento dell'utente mostra proprio questo valore.
        /// </summary>
        public double LeaderProjectedPosAtCheckered { get; set; } = 0.0;

        /// <summary>Fase 0a — il passo effettivamente usato per proiettare il Player.</summary>
        public double PlayerPaceUsed { get; set; } = 0.0;

        /// <summary>Fase 0a — giri proiettati **prima** della correzione per la sosta.</summary>
        public double PlayerLapsLeftBeforePitLoss { get; set; } = 0.0;

        /// <summary>Fase 0a — giri proiettati **dopo** la correzione per la sosta.</summary>
        public double PlayerLapsLeftAfterPitLoss { get; set; } = 0.0;

        public int LeaderRaceLapsCompleted { get; set; } = 0;

        public double LeaderRaceLapsRemaining { get; set; } = 99.0;



        public double RaceTotalLaps { get; set; } = 0.0;

        public int RaceLapsCompleted { get; set; } = 0;

        public double RaceLapsRemaining { get; set; } = 99.0;

        /// <summary>
        /// Dove si trovera' il Player **nell'istante in cui esce la bandiera**, in giri completati
        /// piu' frazione di giro (es. <c>34.80</c>). E' il numero da cui <see cref="RaceTotalLaps"/>
        /// deriva per arrotondamento all'intero superiore: la parte decimale dice quanto manca a
        /// essere costretti a un giro in piu'.
        ///
        /// Esposto perche' finora non era osservabile da nessuna parte: la proiezione veniva
        /// arrotondata un'istruzione dopo essere stata calcolata, quindi nei log si vedeva solo il
        /// risultato gia' quantizzato e gia' filtrato. Senza questo valore non si puo' distinguere
        /// una proiezione a 34.10 da una a 34.95 — entrambe mostrano 35.
        /// </summary>
        public double ProjectedPosAtCheckered { get; set; } = 0.0;



        /// <summary>
        /// La stessa posizione, **lisciata** dal filtro di stabilizzazione: e' questa che decide il
        /// totale giri, mentre <see cref="ProjectedPosAtCheckered"/> resta la misura grezza.
        ///
        /// Le due restano separate di proposito. Il valore grezzo e' quello che l'utente confronta
        /// tick per tick col software di riferimento che usa in pista: lisciarlo al suo posto
        /// avrebbe cambiato il significato di un numero gia' validato contro una fonte esterna.
        /// Averle entrambe a log permette anche di leggere direttamente quanto lavoro sta facendo
        /// il filtro, che altrimenti si potrebbe solo dedurre.
        /// </summary>
        public double SmoothedPosAtCheckered { get; set; } = 0.0;



        /// <summary>
        /// **Modalita' ombra (punto 4).** Tempo alla bandiera calcolato come minimo del tempo di
        /// attraversamento su tutte le vetture, invece che sul solo P1 assoluto istantaneo.
        /// Calcolato e scritto a log, **non usato** da nessun calcolo: serve a misurare cosa
        /// darebbe prima di dipenderne. Vedi RaceTimeProjection.EarliestCheckeredTime.
        /// </summary>
        public double ShadowTimeToFlagSec { get; set; } = 0.0;

        /// <summary>Chi vince il minimo in modalita' ombra. Vuoto se nessuna vettura valutabile.</summary>
        public string ShadowFlagWinner { get; set; } = "";



        /// <summary>
        /// Chi sara' **al comando** quando esce la bandiera: la vettura che decide il momento della
        /// fine gara. Non e' necessariamente chi e' P1 in questo istante.
        /// </summary>
        public string FlagLeaderName { get; set; } = "";

        /// <summary>
        /// Dove sara' quella vettura allo scadere del cronometro, col decimale. E' il numero
        /// direttamente confrontabile con quello del software di riferimento (es. `38.84`).
        /// </summary>
        public double FlagLeaderProjectedPos { get; set; } = 0.0;

        /// <summary>
        /// Posizione sul giro del leader assoluto **adesso**, in frazione di giro. Cambia vettura
        /// quando cambia il leader: e' il dato grezzo da cui nasce tutto il resto, esposto perche'
        /// finora non era osservabile a schermo.
        /// </summary>
        public double LeaderTrackPct { get; set; } = 0.0;

        /// <summary>
        /// **Verita' di terreno.** Dove si trovava il leader nell'istante esatto in cui il cronometro
        /// e' andato a zero — giri completati piu' frazione di giro — fotografato una volta sola e
        /// non piu' toccato.
        ///
        /// Esiste perche' finora l'unico metro di paragone per la proiezione del leader era il
        /// numero mostrato da un altro software (`38.8`), trattato come esatto senza averlo mai
        /// verificato. Questo invece e' misurato dal gioco: <c>Math.Ceiling</c> di questo valore
        /// **e'** il numero di giri che il leader ha davvero completato. Da confrontare con
        /// <see cref="FlagLeaderProjectedPos"/> a fine gara.
        ///
        /// Vale <c>-1</c> finche' il cronometro non e' andato a zero.
        /// </summary>
        public double LeaderPosAtExpiry { get; set; } = -1.0;

        /// <summary>Chi era il leader in quell'istante, e la sua frazione di giro grezza.</summary>
        public string LeaderNameAtExpiry { get; set; } = "";

        /// <summary>Frazione di giro del leader allo scadere. <c>-1</c> finche' non e' scaduto.</summary>
        public double LeaderTrackPctAtExpiry { get; set; } = -1.0;



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
        /// Countdown di sessione all'istante di <see cref="_lastGoodLeaderAbsolutePos"/>, per sapere
        /// **quanto tempo fa** e' stata presa. Negativo = nessuna posizione credibile finora.
        ///
        /// Si usa il tempo di *sessione* e non l'orologio di sistema perche' i replay girano a
        /// velocita' multipla: il passo del leader e' in secondi di sessione, e l'avanzamento va
        /// misurato con lo stesso metro.
        /// </summary>
        private double _lastGoodLeaderPosSessionTimeLeft = -1.0;

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
        // Y-45: i due ritardi di 30 s sulla sola discesa sono stati rimossi. Riarmandosi a ogni
        // cambio del bersaglio tenevano il totale bloccato ben oltre i 30 s previsti (misurati 79
        // su Road Atlanta 20260831_195300). Al loro posto la misura viene lisciata prima di
        // arrivare alla banda: vedi ProjectionStabilizer.
        private readonly ProjectionStabilizer _playerPosStabilizer = new ProjectionStabilizer();
        private readonly ProjectionStabilizer _leaderPosStabilizer = new ProjectionStabilizer();

        // Cronometro di sessione all'aggiornamento precedente: serve a dare al filtro un passo del
        // tempo in secondi **di gara**, non di orologio (un replay a 3x ne comprime tre in uno).
        private double _lastStabilizerTimeLeft = -1.0;

        // Ultimo totale annunciato, per far scattare la riga di log solo quando cambia davvero.
        private double _lastLoggedPlayerTotal = -1.0;
        private double _lastLoggedLeaderTotal = -1.0;

        private DateTime _lastShadowLogTime = DateTime.MinValue;

        // Appoggio fra ComputeFlagMoment e LogFlagMoment: il calcolo gira a ogni tick, il log una
        // volta al secondo, e la variante col limite stretto serve solo a quest'ultimo.
        private RaceTimeProjection.FlagMoment _lastStrictFlagMoment;
        private double _lastFastestLapSeen = 0.0;
        private double _lastPhysicalFloor = 0.0;

        // Verita' di terreno sulla posizione del leader allo scadere del cronometro. Vedi
        // CaptureLeaderPositionAtExpiry: e' l'unico modo che abbiamo di verificare la proiezione
        // contro un dato misurato invece che contro un altro software.
        // Fotografie della proiezione a distanze fisse dalla bandiera. Servono a confrontare la
        // previsione con la verita' di terreno: il confronto **allo scadere** non dimostra nulla,
        // perche' li' il countdown vale zero e la proiezione coincide per costruzione con la
        // posizione misurata (era il difetto della prima versione di questa strumentazione).
        private readonly double[] _projectionAtHorizon = new double[ValidationHorizonsSec.Length];
        private readonly double[] _p1ProjectionAtHorizon = new double[ValidationHorizonsSec.Length];

        private bool _hasSeenPositiveCountdown = false;
        private double _leaderPosAtExpiry = -1.0;
        private string _leaderNameAtExpiry = "";
        private double _leaderTrackPctAtExpiry = -1.0;

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

            // Posizione grezza del leader sul giro. Cambia vettura quando cambia il leader: e' il
            // dato da cui nasce tutta la proiezione, e finora non era osservabile a schermo.
            Results.LeaderTrackPct = leaderTrackPosPct;



            if (state.CurrentLap != _lastEvaluatedLap)

            {

                if (_lastEvaluatedLap > 0 && state.CurrentLap > 1)

                {

                    Results.IsLapped = (Results.LeaderRaceLapsCompleted > Results.RaceLapsCompleted);



                    int playerLapsOnTyres = Math.Max(0, Results.RaceLapsCompleted - _playerLastPitLap);
                    double distanceOnTyres = playerLapsOnTyres * state.TrackLengthMeters;
                    AnalyzePlayerLap(state.LastLapTimeSec, state.CurrentFuelLevel, state.TrackTemperature, state.IsInPitLane, state.Flag_Black, Results.RaceLapsCompleted, state.GlobalBaselineTemp, fuelWeightCoef, tempCoef, distanceOnTyres, playerLapsOnTyres, log);



                    log.Log(LogModule.STRATEGY, LogType.EVENT, "Race Projections Update",

                        $"L_Rem: {Results.LeaderRaceLapsRemaining:F2} | P_Rem: {Results.RaceLapsRemaining:F2} | L_Pace: {Results.LeaderEstimatedPace:F3} | P_PosAtFlag: {Results.ProjectedPosAtCheckered:F3} | P_Total: {Results.RaceTotalLaps:F2} | L_PosAtFlag: {Results.LeaderProjectedPosAtCheckered:F3} | L_Total: {Results.LeaderRaceTotalLaps:F2} | P_Pace: {Results.PlayerPaceUsed:F3} | P_LeftPre: {Results.PlayerLapsLeftBeforePitLoss:F3} | P_LeftPost: {Results.PlayerLapsLeftAfterPitLoss:F3} | RaceLife: {Results.RaceLifeTimeLeftSec:F1}");

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



            CaptureLeaderPositionAtExpiry(state, effSessionTimeLeft, log);

            // Passo del tempo per i filtri di stabilizzazione, in secondi **di gara**.
            // Si legge dal countdown di sessione e non dall'orologio di sistema: un replay a 3x
            // comprime tre secondi di gara in uno reale, e un filtro tarato sull'orologio si
            // comporterebbe in modo diverso a ogni velocita' di riproduzione — due run della
            // stessa gara non sarebbero piu' confrontabili, che e' il metodo con cui si verifica
            // qui (ADR-004). Countdown fermo = nessuna informazione nuova = filtro fermo.
            double stabilizerDtRaceSec = 0.0;
            if (_lastStabilizerTimeLeft >= 0.0 && effSessionTimeLeft >= 0.0)
            {
                stabilizerDtRaceSec = _lastStabilizerTimeLeft - effSessionTimeLeft;
                // Countdown che risale = cambio di sessione o replay riavvolto: si riparte puliti
                // invece di lisciare attraverso un salto che non e' tempo di gara.
                if (stabilizerDtRaceSec < 0.0)
                {
                    _playerPosStabilizer.Reset();
                    _leaderPosStabilizer.Reset();
                    stabilizerDtRaceSec = 0.0;
                }
            }
            if (effSessionTimeLeft >= 0.0) _lastStabilizerTimeLeft = effSessionTimeLeft;



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

                Results.ProjectedPosAtCheckered = 0.0;
                Results.LeaderProjectedPosAtCheckered = 0.0;

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
                _lastGoodLeaderPosSessionTimeLeft = effSessionTimeLeft;
            }
            else
            {
                var overallLeader = state.Opponents.FirstOrDefault(o => o.Position == 1);
                double rawLeaderTrackPos = overallLeader != null && overallLeader.TrackPositionPercent.HasValue
                    ? overallLeader.TrackPositionPercent.Value
                    : 0.0;

                // Due buchi diversi nello stesso dato, entrambi gestiti da ResolveLeaderAbsolutePos:
                //  - record del leader momentaneamente vuoto, posizione **e** giri a zero (Y-24);
                //  - record popolato ma con la sola **posizione** mai arrivata (Y-35), che il guard
                //    di Y-24 lascia passare perche' i giri ci sono. Li' non basta *tenere* l'ultima
                //    posizione buona: una posizione tenuta e' ferma, e una posizione ferma congela
                //    il tempo alla bandiera invece di sbagliarlo in modo visibile.
                double elapsedSinceGoodSample = _lastGoodLeaderPosSessionTimeLeft >= 0.0
                    ? _lastGoodLeaderPosSessionTimeLeft - effSessionTimeLeft
                    : 0.0;

                leaderAbsolutePos = ResolveLeaderAbsolutePos(Results.LeaderRaceLapsCompleted,
                                                             rawLeaderTrackPos,
                                                             _lastGoodLeaderAbsolutePos,
                                                             elapsedSinceGoodSample,
                                                             _smoothedLeaderPace);

                if (IsLeaderSampleUsable(Results.LeaderRaceLapsCompleted, rawLeaderTrackPos)
                    && !IsLeaderPositionMissing(Results.LeaderRaceLapsCompleted, rawLeaderTrackPos))
                {
                    _lastGoodLeaderAbsolutePos = leaderAbsolutePos;
                    _lastGoodLeaderPosSessionTimeLeft = effSessionTimeLeft;
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

                    // Dove sara' allo scadere il **P1 di questo istante**. Resta calcolato come
                    // ripiego e come termine di paragone a log, ma non e' piu' il valore in uso.
                    double leaderPosAtZeroFromP1 = leaderAbsolutePos + leaderL_left;

                    // --- Punto 4: chi comanda ---------------------------------------------
                    // Va calcolato **qui**, prima del totale del leader, perche' e' da qui che
                    // discendono sia il totale sia il tempo alla bandiera. Nel turno precedente era
                    // piu' in basso e alimentava solo il tempo: il totale continuava a seguire il
                    // P1 istantaneo e crollava con lui (Y-48).
                    var flagMoment = ComputeFlagMoment(state, tracker, timeUntilZero,
                                                       playerAbsolutePos, activePlayerPace);

                    // Y-48: il totale del leader viene da **chi comanda**, non da chi e' primo
                    // adesso. Road Atlanta 20260901_202537, ore 20:36:33: il P1 istantaneo era una
                    // vettura con passo registrato 278 s che proiettava 27.886, e il totale del
                    // leader crollava a 30, poi 29, poi 28 — mentre nello stesso identico tick il
                    // punto 4 gia' diceva `comanda=Aleix Nogue, proiezione=38.27`. La risposta
                    // giusta era calcolata e non collegata.
                    double leaderPosAtZero = ResolveLeaderPosAtZero(flagMoment.HasResult,
                                                                    flagMoment.MaxProjectedPos,
                                                                    leaderPosAtZeroFromP1);
                    double leaderPosNow = ResolveLeaderPosAtZero(flagMoment.HasResult,
                                                                 flagMoment.LeaderAbsolutePos,
                                                                 leaderAbsolutePos);

                    Results.LeaderProjectedPosAtCheckered = leaderPosAtZero;
                    Results.FlagLeaderName = flagMoment.HasResult ? flagMoment.LeaderName : leaderName;
                    Results.FlagLeaderProjectedPos = leaderPosAtZero;

                    RecordValidationHorizons(timeUntilZero, leaderPosAtZero, leaderPosAtZeroFromP1);

                    // Y-45: la posizione entra nella banda **lisciata**, e la banda decide da sola.
                    // Prima c'era, in aggiunta, un ritardo di 30 s applicato alla sola discesa: e'
                    // quello che rendeva il filtro asimmetrico e lo bloccava in alto.
                    double smoothedLeaderPosAtZero = _leaderPosStabilizer.Update(leaderPosAtZero, stabilizerDtRaceSec);
                    _latchedLeaderTotalLaps = UpdateLatchedLaps(smoothedLeaderPosAtZero, _latchedLeaderTotalLaps, !leaderIsInPit);

                    // I giri che restano al leader si contano sulla posizione di **chi comanda**:
                    // mescolare il totale di una vettura con la posizione di un'altra darebbe un
                    // conteggio che non descrive nessuna vettura reale.
                    leaderLapsRem = Math.Max(0, _latchedLeaderTotalLaps - leaderPosNow);

                    // Il tempo che manca alla bandiera si legge dal cronometro di sessione, non si
                    // ricostruisce dai giri del leader. Prima era
                    //     leaderLapsRem * leaderPace + leaderRemainingPitTime
                    // cioe' un conteggio *latchato* di giri rimoltiplicato per un passo che nel
                    // frattempo poteva essere cambiato: quando il P1 assoluto cambiava classe, il
                    // risultato divergeva senza limite (Daytona 2026-08-23, giri 12-15: 2368 s
                    // stimati contro ~1400 s reali). Ancorando al countdown, il passo del leader
                    // pesa solo sulla frazione di giro che gli manca per tagliare: errore limitato
                    // a un giro, non piu' proporzionale alla durata della gara.
                    // --- Punto 4, ATTIVO --------------------------------------------------
                    // La bandiera esce quando chiude il giro chi sara' **al comando** allo scadere,
                    // non chi e' P1 in questo istante. Vedi RaceTimeProjection.ProjectFlagMoment
                    // per il perche' e per le due versioni precedenti bocciate dalla misura.
                    //
                    // Misurato su 20260901_184453, finestra in cui il P1 istantaneo era una vettura
                    // con passo registrato 278.60 s: il criterio vecchio produceva un supplemento di
                    // fine gara di **154.7 s** — piu' di due giri del leader, impossibile — mentre
                    // questo teneva ~50 s stabili mettendo al comando vetture con passi veri.
                    //
                    // Il momento della bandiera dal P1 di questo istante resta calcolato come
                    // ripiego e come termine di paragone a log.
                    double flagFromCurrentP1 = RaceTimeProjection.TimeUntilLeaderCheckered(
                        timeUntilZero, leaderAbsolutePos, leaderPace);

                    // Ripiego sul criterio vecchio se nessuna vettura era valutabile: meglio una
                    // stima imperfetta che un tempo alla bandiera pari a zero, che a valle
                    // significherebbe "gara finita adesso".
                    Results.RaceLifeTimeLeftSec = ResolveFlagTime(flagMoment.HasResult, flagMoment.TimeSec,
                                                                  flagFromCurrentP1);

                    LogFlagMoment(state, tracker, log, timeUntilZero, flagMoment,
                                  flagFromCurrentP1, leaderName, leaderPace, leaderPosAtZeroFromP1);

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

                        // Fase 0a: il valore prima che la correzione per la sosta lo tocchi. Il
                        // sospetto da verificare e' che il Player risulti corretto **per caso**:
                        // il passo normalizzato lo gonfia (+1.56% = +0.54 giri su questa gara) e la
                        // correzione per la sosta lo sgonfia di altrettanto. Se e' cosi', passare ai
                        // tempi misurati migliorerebbe il leader e **peggiorerebbe il Player**.
                        double playerL_leftBeforePitLoss = playerL_left;
                        
                        double playerFuelPerLap = fuel.AverageFuelPerLap > 0.0 ? fuel.AverageFuelPerLap : 3.0;
                        double playerStintLaps = state.MaxFuelCapacity / playerFuelPerLap;
                        double playerPitLoss = radar.PitTransitTime + radar.PitInOutAccDecTime + Math.Max(fuel.FuelToAdd / (radar.MeasuredFuelFillRate > 0 ? radar.MeasuredFuelFillRate : 2.7), radar.DbTireChangeTime);
                        int playerRemainingStops = 0;

                        double effectivePlayerTank = fuel.TankLapsRemaining;
                        if (shouldLatch) // Se stiamo entrando ai box o siamo già in pitlane, consideriamo il serbatoio pieno per la proiezione futura
                        {
                            effectivePlayerTank = playerStintLaps;
                        }

                        var pitPlan = ProjectLapsLeftWithStops(Results.RaceLifeTimeLeftSec,
                                                               activePlayerPace,
                                                               effectivePlayerTank,
                                                               playerStintLaps,
                                                               playerPitLoss);
                        playerL_left = pitPlan.LapsLeft;
                        playerRemainingStops = pitPlan.StopsNeeded;

                        Results.RemainingPitsPlayer = playerRemainingStops;

                        double playerPosWhenLeaderFinishes = playerAbsolutePos + playerL_left;
                        Results.ProjectedPosAtCheckered = playerPosWhenLeaderFinishes;

                        Results.PlayerPaceUsed = activePlayerPace;
                        Results.PlayerLapsLeftBeforePitLoss = playerL_leftBeforePitLoss;
                        Results.PlayerLapsLeftAfterPitLoss = playerL_left;

                        // Multiclasse: nessun tetto sul leader **assoluto**. Una GT3 e una GTP non
                        // fanno lo stesso numero di giri, quindi il confronto non dice nulla — e se
                        // il totale del leader e' sovrastimato il tetto non protegge comunque.
                        // Il tetto giusto sarebbe il leader **di classe**: vedi Y-19, va tarato sui
                        // log Daytona prima di cablarlo (serve la posizione assoluta degli avversari,
                        // che ha convenzioni di conteggio giri tutte da verificare).
                        // Monoclasse: "non puoi completare piu' giri del leader" e' un tetto sensato,
                        // il leader gira nella stessa vettura.
                        double leaderTotalCap = isMultiClass ? 0.0 : _latchedLeaderTotalLaps;

                        // Y-31: la posizione passa **continua**. Arrotondarla qui la quantizzava a
                        // intero, e un calo di un giro non superava piu' la soglia di 1.05 del filtro.
                        // Y-45: la posizione entra lisciata; il ritardo di 30 s sulla discesa non
                        // c'e' piu'. **ProjectedPosAtCheckered resta la misura grezza**, cosi' il
                        // numero che l'utente confronta col software di riferimento non cambia
                        // significato: il lisciamento serve al totale, non alla proprieta'.
                        double smoothedPlayerPosAtFlag = _playerPosStabilizer.Update(playerPosWhenLeaderFinishes, stabilizerDtRaceSec);
                        Results.SmoothedPosAtCheckered = smoothedPlayerPosAtFlag;

                        _latchedPlayerTotalReality = ProjectPlayerTotalLaps(smoothedPlayerPosAtFlag,
                                                                          _latchedPlayerTotalReality,
                                                                          !state.IsInPitLane,
                                                                          state.Position == 1 ? _latchedLeaderTotalLaps : leaderTotalCap,
                                                                          state.Position == 1);
                        _latchedPlayerTotalReality = ApplyLeaderTotalCap(_latchedPlayerTotalReality,
                                                                        _latchedLeaderTotalLaps,
                                                                        isMultiClass,
                                                                        state.Position == 1);
                    }
                }
            }



            rawPlayerRemaining = Math.Max(0, _latchedPlayerTotalReality - playerAbsolutePos);

            if (state.SessionStateStatus < 4 && effTrackPos == 0.0 && state.IsLapLimited && !state.IsTimeLimited)

                rawPlayerRemaining -= 1.0;



            Results.LeaderRaceTotalLaps = _latchedLeaderTotalLaps;

            Results.LeaderRaceLapsRemaining = Math.Truncate(leaderLapsRem * 100) / 100.0;

            Results.RaceTotalLaps = _latchedPlayerTotalReality;

            // --- La riga che scatta quando il totale CAMBIA ------------------------------
            // Serve a rendere osservabile l'ingresso che ha fatto scattare il filtro. La
            // diagnostica ordinaria scrive **una riga al secondo** mentre questo calcolo gira
            // almeno a 12 Hz (misurato sugli intervalli del log 20260831_195300: massimo 1.085 s
            // fra due righe con la strozzatura a 1.0 s, quindi al peggio un fotogramma su dodici
            // finisce a log). Il salto del totale a 37 di quel replay richiedeva una posizione
            // sopra 36.05 che **in tutto il log non compare**: era in uno dei fotogrammi non
            // scritti. Un evento raro come un cambio di totale puo' permettersi una riga sempre.
            LogTotalLapsTransition(state, log);



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
                    $"Player: Lap={state.CurrentLap}, PosPct={state.TrackPositionPercent:F4}, LapsComp={Results.RaceLapsCompleted}, LapsRem={Results.RaceLapsRemaining:F2}, PosAtFlag={Results.ProjectedPosAtCheckered:F3}, FuelToAdd={fuel.FuelToAdd:F2}L, IsInPit={state.IsInPitLane}, Latched={_isLatchedForPit}, LatchedVal={_latchedRaceLapsRemaining:F2}, LatchedReality={_latchedPlayerTotalReality:F2} | " +
                    $"Leader ({leaderName}): PosPct={leaderTrackPos:F4}, LapsComp={leaderLapsCompleted}, LapsRem={Results.LeaderRaceLapsRemaining:F2}, PosAtFlag={Results.LeaderProjectedPosAtCheckered:F3}, FuelToAdd={leaderFuelToAdd:F2}L, IsInPit={leaderIsInPit}, LatchedTotal={_latchedLeaderTotalLaps:F2}");
            }
        }



        private void AnalyzePlayerLap(double lapTime, double currentFuel, double trackTemp, bool isInPit, int blackFlag, int raceLapsCompleted, double globalBaselineTemp, double fuelWeightCoef, double tempCoef, double distanceOnTyres, int playerLapsOnTyres, LogManager log)

        {

            if (isInPit || lapTime <= 0 || blackFlag > 0) return;



            double fuelPenalty = RaceTimeProjection.FuelWeightPenaltySec(currentFuel, fuelWeightCoef);

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
        /// Il record del leader e' popolato ma **il campo posizione no** (Y-35).
        ///
        /// E' il caso che <see cref="IsLeaderSampleUsable"/> lascia deliberatamente passare: li' la
        /// domanda era "questo record e' vuoto?", e con i giri popolati la risposta e' no. La
        /// domanda diversa a cui risponde questa funzione e' "la **posizione** e' arrivata?".
        ///
        /// Uno zero esatto con la gara in corso non e' un leader fermo sulla linea: e' un campo mai
        /// riempito. Una vettura in movimento attraversa il traguardo in una frazione di tick, e la
        /// probabilita' di campionarla a <c>0.0000</c> esatto e' trascurabile — mentre nel replay
        /// Road Atlanta del 2026-08-29 e' successo nel **26% dei tick**, per giri interi di fila
        /// (giro 30: tutti e 13 i campioni a zero con <c>LapsComp=32</c>).
        ///
        /// **Perche' fa danno.** Con la posizione del leader ferma,
        /// <see cref="RaceTimeProjection.TimeUntilLeaderCheckered"/> restituisce un valore
        /// *esattamente* costante: il countdown che scende e la frazione di giro che manca al leader
        /// crescono della stessa quantita' e si annullano. Il tempo alla bandiera si blocca, e con
        /// lui i giri che il Player puo' ancora fare — mentre la sua posizione avanza. La proiezione
        /// sale allora 1:1 col pilota per tutto il giro, poi crolla di un giro del leader quando
        /// l'arrotondamento scatta. Non e' un errore appariscente: e' un numero *stabile e sbagliato*.
        /// </summary>
        public static bool IsLeaderPositionMissing(int leaderLapsCompleted, double leaderTrackPositionPct)
        {
            return leaderTrackPositionPct == 0.0 && leaderLapsCompleted > 0;
        }

        /// <summary>
        /// Dove sara' il leader adesso, partendo dall'ultima posizione credibile e dal tempo passato
        /// da allora. Serve quando la posizione live non e' arrivata
        /// (<see cref="IsLeaderPositionMissing"/>).
        ///
        /// **Tenere l'ultima posizione buona non basta**, ed e' il punto meno ovvio di Y-35: una
        /// posizione *tenuta* e' comunque una posizione ferma, e produce lo stesso tempo-alla-bandiera
        /// congelato di una posizione a zero. L'unica cosa che spezza il meccanismo e' farla
        /// **avanzare**: il leader non sta fermo, e di quanto si sia spostato lo sappiamo dal suo
        /// passo e dal tempo trascorso.
        ///
        /// Senza un passo utilizzabile si restituisce la posizione tenuta: sbagliare per difetto e'
        /// preferibile a moltiplicare per un numero a caso — stessa scelta di
        /// <see cref="RaceTimeProjection.TimeUntilLeaderCheckered"/> quando il passo manca.
        /// </summary>
        /// Il risultato e' **vincolato al conteggio giri**, che continua ad arrivare anche quando la
        /// posizione no: il leader ha completato <paramref name="knownLapsCompleted"/> giri, quindi
        /// si trova per forza fra quel valore e il successivo. Cosi' l'errore della stima resta
        /// sotto il giro qualunque cosa faccia il passo — la stessa garanzia su cui e' costruita
        /// <see cref="RaceTimeProjection.TimeUntilLeaderCheckered"/>.
        /// </summary>
        /// <param name="lastGoodPos">Ultima posizione assoluta credibile. Negativa = mai avuta.</param>
        /// <param name="elapsedSec">Secondi di **sessione** trascorsi da allora. Negativi = ignorati.</param>
        /// <param name="leaderPaceSec">Passo del leader. Zero o negativo = nessun avanzamento.</param>
        /// <param name="knownLapsCompleted">Giri completati dal leader, dato che continua ad arrivare.</param>
        public static double DeadReckonLeaderPos(double lastGoodPos, double elapsedSec,
                                                 double leaderPaceSec, int knownLapsCompleted)
        {
            double advanced = lastGoodPos;

            if (lastGoodPos >= 0.0 && leaderPaceSec > 0.0 && elapsedSec > 0.0)
            {
                advanced = lastGoodPos + elapsedSec / leaderPaceSec;
            }

            if (knownLapsCompleted < 0) return advanced;

            // Il giro in corso e' noto: la posizione ci sta dentro per costruzione.
            double floor = knownLapsCompleted;
            double ceiling = knownLapsCompleted + 1.0;

            if (advanced < floor) return floor;
            if (advanced >= ceiling) return ceiling - 0.001;
            return advanced;
        }

        /// <summary>
        /// La posizione assoluta del leader da usare nelle proiezioni: quella live se e' arrivata,
        /// altrimenti la stima per avanzamento.
        ///
        /// Sta qui come funzione pura perche' e' **questa scelta** — non l'aritmetica delle due
        /// funzioni che chiama — il punto in cui il difetto puo' tornare. La lezione di Y-31: il
        /// filtro era corretto, sbagliava il chiamante, e un test sul solo filtro restava verde.
        /// </summary>
        /// <param name="leaderLapsCompleted">Giri completati dal leader.</param>
        /// <param name="rawTrackPos">Posizione live dalla telemetria, <c>0.0</c> se non arrivata.</param>
        /// <param name="lastGoodPos">Ultima posizione assoluta credibile. Negativa = mai avuta.</param>
        /// <param name="elapsedSec">Secondi di sessione da quel campione.</param>
        /// <param name="leaderPaceSec">Passo del leader.</param>
        public static double ResolveLeaderAbsolutePos(int leaderLapsCompleted, double rawTrackPos,
                                                      double lastGoodPos, double elapsedSec,
                                                      double leaderPaceSec)
        {
            bool recordBlank = !IsLeaderSampleUsable(leaderLapsCompleted, rawTrackPos);
            bool positionMissing = IsLeaderPositionMissing(leaderLapsCompleted, rawTrackPos);

            if (recordBlank || positionMissing)
            {
                return DeadReckonLeaderPos(lastGoodPos, elapsedSec, leaderPaceSec, leaderLapsCompleted);
            }

            return leaderLapsCompleted + rawTrackPos;
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

        /// <summary>
        /// Filtro di stabilita' sul totale giri: sale facile, scende solo se la proiezione crolla.
        ///
        /// **La banda e' asimmetrica di proposito** (+0.05 in salita, -1.05 in discesa): il totale
        /// deve seguire subito un allungamento della gara, ma non deve sfarfallare a ogni tick su
        /// una stima che oscilla. La soglia di 1.05 significa "scendo solo se ho sbagliato di piu'
        /// di un giro intero".
        ///
        /// **Vincolo su <paramref name="rawProjectedPos"/>: deve essere la posizione _continua_
        /// (giri + frazione), mai un valore gia' arrotondato all'intero.** Con un intero in
        /// ingresso, un calo di un giro vale esattamente 1.00 e non supera mai la soglia di 1.05:
        /// il filtro diventa un dente d'arresto che sale di un giro e non torna piu' indietro.
        /// E' il difetto Y-31, misurato a Road Atlanta il 2026-08-28: il totale e' salito a 36 per
        /// un singolo tick al giro 4 e ci e' rimasto fino alla bandiera, con la gara che di giri
        /// ne e' durati 35.
        /// </summary>
        public static double UpdateLatchedLaps(double rawProjectedPos, double currentLatchedLaps, bool allowDecrease = true)
        {
            if (currentLatchedLaps == 0.0) return Math.Ceiling(rawProjectedPos);
            if (rawProjectedPos > currentLatchedLaps + 0.05) return Math.Ceiling(rawProjectedPos - 0.05);
            if (allowDecrease && rawProjectedPos < currentLatchedLaps - 1.05) return Math.Ceiling(rawProjectedPos + 0.05);
            return currentLatchedLaps;
        }

        /// <summary>
        /// Scrive una riga **solo quando** uno dei due totali cambia, con dentro tutto quello che
        /// serve a spiegare perche' e' cambiato: misura grezza, misura lisciata, stato del
        /// riconoscimento dei cambi, identita' e passo del leader in quell'istante.
        ///
        /// Esiste perche' la diagnostica a 1 Hz non basta a chiudere un difetto come Y-45: il
        /// fotogramma che fa scattare il filtro e' quasi sempre uno di quelli non scritti, e
        /// dedurne il valore dai vicini e' esattamente il genere di ricostruzione che questo
        /// repository non accetta come prova (ADR-004).
        /// </summary>
        private void LogTotalLapsTransition(SessionState state, LogManager log)
        {
            bool playerChanged = Math.Abs(_latchedPlayerTotalReality - _lastLoggedPlayerTotal) > 0.001;
            bool leaderChanged = Math.Abs(_latchedLeaderTotalLaps - _lastLoggedLeaderTotal) > 0.001;
            if (!playerChanged && !leaderChanged) return;

            // Prima transizione della sessione: si registra il punto di partenza senza gridare a
            // un cambio che non e' avvenuto.
            bool isFirst = _lastLoggedPlayerTotal < 0.0 && _lastLoggedLeaderTotal < 0.0;

            double previousPlayer = _lastLoggedPlayerTotal;
            double previousLeader = _lastLoggedLeaderTotal;
            _lastLoggedPlayerTotal = _latchedPlayerTotalReality;
            _lastLoggedLeaderTotal = _latchedLeaderTotalLaps;

            if (!state.IsSessionActive) return;

            string leaderNow = "PLAYER";
            if (state.Position != 1)
            {
                var p1 = state.Opponents.FirstOrDefault(o => o.Position == 1);
                if (p1 != null && !string.IsNullOrEmpty(p1.Name)) leaderNow = p1.Name;
            }

            log.Log(LogModule.STRATEGY, LogType.EVENT, "Total Laps Transition",
                $"{(isFirst ? "INIZIALE" : "CAMBIO")} | " +
                $"Player: {previousPlayer:F0} -> {_latchedPlayerTotalReality:F0} | " +
                $"grezzo={Results.ProjectedPosAtCheckered:F3} lisciato={Results.SmoothedPosAtCheckered:F3} " +
                $"sospetto={_playerPosStabilizer.SuspicionRaceSec:F1}s | " +
                $"Leader: {previousLeader:F0} -> {_latchedLeaderTotalLaps:F0} | " +
                $"grezzo={Results.LeaderProjectedPosAtCheckered:F3} lisciato={_leaderPosStabilizer.Estimate:F3} " +
                $"sospetto={_leaderPosStabilizer.SuspicionRaceSec:F1}s | " +
                $"chiEraP1={leaderNow} passoLeader={Results.LeaderEstimatedPace:F2} | " +
                $"tl={state.SessionTimeLeftSec:F1}s giro={state.CurrentLap} pos={state.TrackPositionPercent:F4} " +
                $"inPit={state.IsInPitLane} | ombra={Results.ShadowTimeToFlagSec:F1}s vince={Results.ShadowFlagWinner}");
        }

        /// <summary>
        /// **Punto 4, modalita' ombra.** Calcola il momento della bandiera come tempo di
        /// attraversamento della vettura che sara' **al comando** allo scadere — quella con la
        /// posizione proiettata piu' alta — e lo scrive a log **senza usarlo**.
        ///
        /// Cosa leggere nella riga, in ordine di importanza:
        /// <list type="bullet">
        /// <item><c>suppl=</c> — di quanto la bandiera esce dopo lo scadere. Deve stare intorno a
        /// meta' giro del leader (~35 s qui) e non deve quasi mai avvicinarsi a zero. E' la
        /// grandezza che ha bocciato il criterio precedente.</item>
        /// <item><c>comanda=</c> — chi risulta al comando. Deve essere una vettura di testa, e non
        /// deve cambiare a ogni tick.</item>
        /// <item><c>vecchioMin=</c> — cosa avrebbe dato il criterio del minimo, per confronto
        /// diretto sullo stesso tick. Non usato.</item>
        /// <item><c>inLotta=</c> — quanto e' contesa la testa. Diagnostico, non entra nel calcolo.</item>
        /// </list>
        ///
        /// Due varianti nella stessa riga, perche' la differenza fra le due e' il dato che serve
        /// per decidere se accendere il punto 4:
        /// <list type="bullet">
        /// <item><c>min</c> — limite di plausibilita' **fisico** (lunghezza pista / 110 m/s):
        /// generoso, scarta solo la spazzatura conclamata.</item>
        /// <item><c>minStretto</c> — limite pari al **giro piu' veloce realmente osservato** nella
        /// sessione: nessuno puo' tagliare prima di quel ritmo.</item>
        /// </list>
        /// Se le due coincidono per tutta la gara, il limite stretto non serve e il punto 4 puo'
        /// accendersi con quello fisico. Se divergono, il replay dice **quando** e **per colpa di
        /// chi** — che e' esattamente l'informazione che oggi non abbiamo.
        ///
        /// I passi usati sono i **normalizzati**, gli stessi da cui esce <c>leaderPace</c>: un
        /// confronto fra vetture ha senso solo se la grandezza confrontata e' la stessa per tutte.
        /// Le soste ancora dovute non entrano: vedi la nota su Y-44 in
        /// <see cref="RaceTimeProjection.EarliestCheckeredTime"/>.
        /// </summary>
        private RaceTimeProjection.FlagMoment ComputeFlagMoment(SessionState state, OpponentTracker tracker,
                                                                double timeUntilZero, double playerAbsolutePos,
                                                                double playerPace)
        {
            var candidates = new List<RaceTimeProjection.CrossingCandidate>();
            double fastestLapSeen = 0.0;

            if (playerPace > 0.0)
            {
                candidates.Add(new RaceTimeProjection.CrossingCandidate
                {
                    Name = "PLAYER",
                    AbsolutePos = playerAbsolutePos,
                    PaceSec = playerPace
                });
            }
            if (state.BestLapTimeSec > 0.0) fastestLapSeen = state.BestLapTimeSec;

            foreach (var opponent in state.Opponents)
            {
                if (opponent == null || string.IsNullOrEmpty(opponent.Name)) continue;

                OpponentTelemetryData data;
                if (!tracker.TrackedOpponents.TryGetValue(opponent.Name, out data) || data == null) continue;

                double pace = data.NormalizedTimes.LapMovingAverage > 0.0
                    ? data.NormalizedTimes.LapMovingAverage
                    : data.NormalizedTimes.BestLapTime;
                if (pace <= 0.0) continue;

                // Stessa convenzione del conteggio giri del leader: CurrentLap e' il giro in corso,
                // quindi i giri completati sono uno di meno.
                int lapsCompleted = Math.Max(0, (opponent.CurrentLap ?? 0) - 1);
                double posPct = opponent.TrackPositionPercent ?? 0.0;

                candidates.Add(new RaceTimeProjection.CrossingCandidate
                {
                    Name = opponent.Name,
                    AbsolutePos = lapsCompleted + posPct,
                    PaceSec = pace
                });

                double best = data.NormalizedTimes.BestLapTime;
                if (best > 0.0 && (fastestLapSeen <= 0.0 || best < fastestLapSeen)) fastestLapSeen = best;
            }

            if (candidates.Count == 0) return new RaceTimeProjection.FlagMoment();

            double physicalFloor = RaceTimeProjection.MinimumPlausibleLapSec(state.TrackLengthMeters);
            _lastFastestLapSeen = fastestLapSeen;
            _lastPhysicalFloor = physicalFloor;

            var loose = RaceTimeProjection.ProjectFlagMoment(candidates, timeUntilZero, physicalFloor);
            _lastStrictFlagMoment = RaceTimeProjection.ProjectFlagMoment(candidates, timeUntilZero, fastestLapSeen);

            Results.ShadowTimeToFlagSec = loose.TimeSec;
            Results.ShadowFlagWinner = loose.LeaderName;
            return loose;
        }

        /// <summary>
        /// Scrive la riga di confronto fra il criterio in uso (punto 4 v3, la vettura al comando) e
        /// quello vecchio (il P1 di questo istante), piu' il minimo bocciato come terzo termine.
        ///
        /// Cosa leggere, in ordine di importanza:
        /// <list type="bullet">
        /// <item><c>suppl=</c> — di quanto la bandiera esce dopo lo scadere. Deve stare intorno a
        /// meta' giro del leader (~35 s qui) e non deve quasi mai avvicinarsi a zero. E' la
        /// grandezza che ha bocciato due disegni precedenti.</item>
        /// <item><c>comanda=</c> — chi risulta al comando. Deve essere una vettura di testa.</item>
        /// <item><c>vecchioP1=</c> — cosa avrebbe dato il criterio precedente sullo stesso tick.</item>
        /// </list>
        /// </summary>
        private void LogFlagMoment(SessionState state, OpponentTracker tracker, LogManager log,
                                   double timeUntilZero, RaceTimeProjection.FlagMoment loose,
                                   double flagFromCurrentP1, string currentLeaderName, double currentLeaderPace,
                                   double leaderPosAtZeroFromP1)
        {
            if (!state.IsSessionActive) return;
            if ((DateTime.Now - _lastShadowLogTime).TotalSeconds < 1.0) return;
            _lastShadowLogTime = DateTime.Now;

            var strict = _lastStrictFlagMoment;
            double fastestLapSeen = _lastFastestLapSeen;
            double physicalFloor = _lastPhysicalFloor;

            // Il supplemento — di quanto la bandiera esce **dopo** lo scadere del cronometro — e' la
            // grandezza che ha bocciato il criterio del minimo, e si legge senza sapere nulla del
            // codice: allo scadere il leader e' a meta' giro in media, quindi deve valere ~35 s su
            // un giro da 69 e non puo' quasi mai essere zero. Va a log per tutti e tre i criteri.
            double overrunUsed = flagFromCurrentP1 - timeUntilZero;
            double overrunMax = loose.TimeSec - timeUntilZero;
            double overrunMin = loose.EarliestCrossingSec - timeUntilZero;

            log.Log(LogModule.STRATEGY, LogType.FLOW, "Flag Moment",
                $"USATO={Results.RaceLifeTimeLeftSec:F1}s (comanda={loose.LeaderName}, passo={loose.LeaderPaceSec:F2}, suppl={overrunMax:F1}s, " +
                $"vetture={loose.Considered}, inLotta={loose.Contenders}, scartate={loose.RejectedByFloor}, maxProiettato={loose.MaxProjectedPos:F2}) | " +
                $"totLeader={_latchedLeaderTotalLaps:F0} | " +
                $"vecchioP1={flagFromCurrentP1:F1}s (P1={currentLeaderName}, passo={currentLeaderPace:F2}, " +
                $"suppl={overrunUsed:F1}s, proiez={leaderPosAtZeroFromP1:F2}) | " +
                $"maxStretto={strict.TimeSec:F1}s (comanda={strict.LeaderName}, passo={strict.LeaderPaceSec:F2}, scartate={strict.RejectedByFloor}) | " +
                $"vecchioMin={loose.EarliestCrossingSec:F1}s (suppl={overrunMin:F1}s) | " +
                $"delta={loose.TimeSec - flagFromCurrentP1:F1}s | limiteFisico={physicalFloor:F2} | giroPiuVeloce={fastestLapSeen:F3}");
        }

        /// <summary>
        /// Applica al totale del Player il tetto "non puoi completare piu' giri del leader".
        ///
        /// **Il difetto che questa funzione chiude (Y-46).** Il tetto veniva disattivato in
        /// multiclasse — correttamente, e con tanto di commento che spiega perche' — e poi
        /// **riapplicato tre righe dopo senza alcuna condizione**. Il codice contraddiceva il
        /// proprio commento.
        ///
        /// Road Atlanta `20260831_222417`, ore 22:35:25: il passo fasullo di `Alessandro Barbagallo`
        /// (278 s, difetto Y-38) fa crollare il totale del **leader** da 39 a 28, e questo tetto lo
        /// trasferisce di peso al Player. Per **103 secondi di gara** il totale del Player ha letto
        /// 28-30 mentre la sua proiezione era ferma e corretta a `34.53`:
        ///
        /// <code>
        ///   t=22:35:24  posAtFlag=34.537  TOT=35   leaderTot=39
        ///   t=22:35:25  posAtFlag=34.530  TOT=30   leaderTot=30
        ///   t=22:35:29  posAtFlag=34.527  TOT=28   leaderTot=28
        /// </code>
        ///
        /// Il totale del Player copiava quello del leader cifra per cifra. Sette giri di
        /// sottostima, cioe' ~15 L di carburante in meno del necessario: la direzione pericolosa.
        ///
        /// **Perche' non basta cancellare la riga.** In monoclasse il tetto ha una funzione reale e
        /// **non** e' ridondante: il tetto applicato a monte agisce sulla posizione *continua*
        /// prima della banda, quindi se il totale memorizzato e' gia' sopra il tetto la banda puo'
        /// tenercelo (serve un calo di piu' di un giro per scendere). Qui invece si agisce sul
        /// totale gia' formato. Le due applicazioni fanno cose diverse e servono entrambe.
        ///
        /// **Le due condizioni in cui il tetto vale.** Monoclasse: tutti fanno lo stesso numero di
        /// giri, quindi il confronto ha senso. Player che **e'** il leader: il suo totale *e'* per
        /// definizione quello del leader, qualunque sia la struttura delle classi. In multiclasse
        /// e non leader il confronto non dice nulla — una GT3 e una GTP non fanno lo stesso numero
        /// di giri — ed e' li' che il tetto faceva danno.
        /// </summary>
        /// <param name="playerTotal">Totale del Player dopo la banda di stabilita'.</param>
        /// <param name="leaderTotal">Totale del leader assoluto. Zero = non ancora stimato.</param>
        /// <param name="isMultiClass">Vero se in pista c'e' piu' di una classe.</param>
        /// <param name="playerIsLeader">Vero se il Player e' P1 assoluto.</param>
        public static double ApplyLeaderTotalCap(double playerTotal, double leaderTotal,
                                                 bool isMultiClass, bool playerIsLeader)
        {
            if (leaderTotal <= 0.0) return playerTotal;
            if (isMultiClass && !playerIsLeader) return playerTotal;
            return Math.Min(playerTotal, leaderTotal);
        }

        /// <summary>
        /// Fotografa dove si trova il leader assoluto nell'**istante esatto** in cui il cronometro
        /// di sessione va a zero, e non tocca piu' quel valore.
        ///
        /// **Perche' esiste.** Finora l'unico metro di paragone per la proiezione del leader era il
        /// numero mostrato da un altro software (`38.8`), che abbiamo trattato come esatto senza
        /// averlo mai verificato — e che puo' avere una sua deviazione. Questo invece e' un dato del
        /// gioco: allo scadere il leader si trova dove si trova, e <c>Math.Ceiling</c> di
        /// <c>giri completati + frazione</c> **e'** il numero di giri che completera' davvero.
        /// Diventa il termine di paragone della proiezione, misurato invece che riferito.
        ///
        /// Si cattura una volta sola, e solo dopo aver visto un countdown positivo: senza quel
        /// guard la pre-gara (dove il cronometro vale <c>-1</c>) farebbe scattare subito la
        /// fotografia su una posizione che non significa nulla.
        /// </summary>
        private void CaptureLeaderPositionAtExpiry(SessionState state, double effSessionTimeLeft, LogManager log)
        {
            if (effSessionTimeLeft > 0.0)
            {
                _hasSeenPositiveCountdown = true;
                return;
            }

            if (!ShouldCaptureExpirySnapshot(effSessionTimeLeft, _hasSeenPositiveCountdown, _leaderPosAtExpiry >= 0.0)) return;
            if (!state.IsSessionActive) return;

            string name = "PLAYER";
            double trackPct = state.TrackPositionPercent;
            int lapsCompleted = Results.RaceLapsCompleted;

            if (state.Position != 1)
            {
                var p1 = state.Opponents.FirstOrDefault(o => o.Position == 1);
                if (p1 == null) return;   // senza leader non si fotografa niente
                name = p1.Name ?? "";
                trackPct = p1.TrackPositionPercent ?? 0.0;
                lapsCompleted = Results.LeaderRaceLapsCompleted;
            }

            _leaderNameAtExpiry = name;
            _leaderTrackPctAtExpiry = trackPct;
            _leaderPosAtExpiry = lapsCompleted + trackPct;

            Results.LeaderNameAtExpiry = _leaderNameAtExpiry;
            Results.LeaderTrackPctAtExpiry = _leaderTrackPctAtExpiry;
            Results.LeaderPosAtExpiry = _leaderPosAtExpiry;

            log.Log(LogModule.STRATEGY, LogType.EVENT, "Leader Position At Expiry",
                $"VERITA' DI TERRENO | leader={_leaderNameAtExpiry} | giriCompletati={lapsCompleted} | " +
                $"posSulGiro={_leaderTrackPctAtExpiry:F4} | posAssoluta={_leaderPosAtExpiry:F3} | " +
                $"giriCheCompletera={Math.Ceiling(_leaderPosAtExpiry):F0}");

            // Il confronto che conta: cosa avevamo previsto **prima**, non allo scadere. Allo
            // scadere il countdown vale zero e la proiezione coincide per costruzione con la
            // posizione misurata — la prima versione di questa riga confrontava proprio quello, e
            // i tre numeri leggevano tutti 38.061. Non dimostrava niente.
            var righe = new System.Text.StringBuilder();
            for (int i = 0; i < ValidationHorizonsSec.Length; i++)
            {
                if (_projectionAtHorizon[i] <= 0.0) continue;
                righe.Append($" | a-{ValidationHorizonsSec[i] / 60.0:F0}min: punto4={_projectionAtHorizon[i]:F3} " +
                             $"(err {_projectionAtHorizon[i] - _leaderPosAtExpiry:+0.000;-0.000}) " +
                             $"vecchioP1={_p1ProjectionAtHorizon[i]:F3} " +
                             $"(err {_p1ProjectionAtHorizon[i] - _leaderPosAtExpiry:+0.000;-0.000})");
            }

            log.Log(LogModule.STRATEGY, LogType.EVENT, "Projection Validation",
                $"vero={_leaderPosAtExpiry:F3}{righe}");
        }

        /// <summary>
        /// Quale tempo alla bandiera esporre: quello della vettura **al comando** (punto 4) se e'
        /// stato possibile calcolarlo, altrimenti il ripiego sul P1 di questo istante.
        ///
        /// Esiste come funzione pura per una ragione precisa, ed e' la lezione di Y-31: il difetto
        /// sta quasi sempre nel *chiamante*, non nella formula. Qui il modo di sbagliare e' uno solo
        /// ma e' grave — restituire il valore del punto 4 quando non c'e' un risultato significa
        /// esporre **zero**, che a valle si legge come "la bandiera esce adesso": il carburante da
        /// imbarcare crollerebbe a zero a meta' gara. Il ripiego non e' una raffinatezza, e' la
        /// differenza fra una stima imperfetta e un ordine sbagliato.
        /// </summary>
        /// <param name="hasCommandingCar">Falso se nessuna vettura era valutabile.</param>
        /// <param name="flagFromCommandingCar">Tempo alla bandiera dalla vettura al comando.</param>
        /// <param name="flagFromCurrentP1">Tempo alla bandiera dal P1 di questo istante.</param>
        public static double ResolveFlagTime(bool hasCommandingCar, double flagFromCommandingCar,
                                             double flagFromCurrentP1)
        {
            if (!hasCommandingCar) return flagFromCurrentP1;
            if (flagFromCommandingCar <= 0.0) return flagFromCurrentP1;
            return flagFromCommandingCar;
        }

        /// <summary>
        /// E' il momento di fotografare dove si trova il leader? Solo quando il cronometro e' andato
        /// a zero, solo se prima lo si era visto **positivo**, e una volta sola.
        ///
        /// Il guard sul countdown positivo non e' teorico: prima del via il cronometro di sessione
        /// vale <c>-1</c>, quindi senza di lui la fotografia scatterebbe immediatamente sulla griglia
        /// di partenza, su una posizione che non significa nulla, e resterebbe li' per tutta la gara
        /// perche' si scatta una volta sola.
        /// </summary>
        public static bool ShouldCaptureExpirySnapshot(double sessionTimeLeftSec,
                                                       bool hasSeenPositiveCountdown,
                                                       bool alreadyCaptured)
        {
            if (sessionTimeLeftSec > 0.0) return false;
            if (!hasSeenPositiveCountdown) return false;
            if (alreadyCaptured) return false;
            return true;
        }

        /// <summary>
        /// Distanze dalla bandiera, in secondi di gara, a cui si fotografa la proiezione per poterla
        /// poi confrontare con la verita' di terreno. Venti, quindici, dieci, cinque e due minuti:
        /// sono gli orizzonti proposti dal report esterno, che chiede anche che l'errore **cali in
        /// modo monotono** — se a dieci minuti si sbaglia piu' che a venti, qualcosa e' rotto.
        /// </summary>
        public static readonly double[] ValidationHorizonsSec = { 1200.0, 900.0, 600.0, 300.0, 120.0 };

        /// <summary>
        /// Quale posizione del leader usare: quella di **chi comanda** (punto 4) se e' stato
        /// possibile calcolarla, altrimenti il ripiego sul P1 di questo istante.
        ///
        /// **Esiste come funzione pura perche' questo difetto era esattamente il chiamante** (Y-48).
        /// Il criterio del punto 4 era gia' acceso e gia' corretto, ma il totale del leader
        /// continuava a leggere dal P1 istantaneo: Road Atlanta `20260901_202537`, ore 20:36:33, il
        /// P1 era una vettura con passo registrato 278 s che proiettava `27.886` e il totale del
        /// leader crollava a 30, 29, 28 — mentre nello stesso tick il punto 4 diceva gia'
        /// `comanda=Aleix Nogue, proiezione=38.27`. Nessuna formula era sbagliata: mancava un filo.
        /// E' la stessa lezione di Y-31, per la terza volta in questo repository.
        /// </summary>
        public static double ResolveLeaderPosAtZero(bool hasCommandingCar, double fromCommandingCar,
                                                    double fromCurrentP1)
        {
            if (!hasCommandingCar) return fromCurrentP1;
            if (fromCommandingCar <= 0.0) return fromCurrentP1;
            return fromCommandingCar;
        }

        /// <summary>
        /// Fotografa la proiezione ogni volta che il countdown scende sotto uno degli orizzonti di
        /// <see cref="ValidationHorizonsSec"/>, una volta per orizzonte. Nessun effetto sul calcolo:
        /// serve solo perche' la riga di fine gara possa mettere in fila previsione ed esito.
        /// </summary>
        private void RecordValidationHorizons(double timeUntilZero, double projection, double p1Projection)
        {
            if (timeUntilZero <= 0.0) return;

            for (int i = 0; i < ValidationHorizonsSec.Length; i++)
            {
                if (_projectionAtHorizon[i] > 0.0) continue;
                if (timeUntilZero > ValidationHorizonsSec[i]) continue;

                _projectionAtHorizon[i] = projection;
                _p1ProjectionAtHorizon[i] = p1Projection;
            }
        }

        /// <summary>Esito di <see cref="ProjectLapsLeftWithStops"/>.</summary>
        public struct LapsLeftPlan
        {
            /// <summary>Giri che il Player percorrera' ancora prima della bandiera.</summary>
            public double LapsLeft;

            /// <summary>Soste ancora necessarie per arrivare in fondo.</summary>
            public int StopsNeeded;
        }

        /// <summary>
        /// Giri che restano da percorrere, tolto il tempo che si perdera' nelle soste ancora dovute.
        ///
        /// **La sosta si sottrae come tempo, e basta** (Y-42):
        /// <code>
        ///   tempo netto = tempo alla bandiera − (numero soste × tempo perso per sosta)
        ///   giri        = tempo netto / passo
        /// </code>
        ///
        /// Prima c'era una formula analitica che, oltre a sottrarre la perdita, alterava anche il
        /// **denominatore** (<c>pace + pitLoss/stintLaps</c>), spalmando cioe' il costo della sosta
        /// su ogni giro. Il risultato era una sottrazione molto piu' grande del dovuto: misurata sul
        /// replay Road Atlanta del 2026-08-30, toglieva **1.25 giri** dove la sosta reale ne era
        /// costati **0.53** (41.1 s su un giro da 77.5 s). L'effetto si vedeva a occhio nel log:
        /// <c>PosAtFlag</c> valeva 33.75-34.37 **prima** della sosta e 34.67-34.85 **dopo**, contro
        /// un valore reale di 34.83. Dopo la sosta — quando la correzione vale zero — la proiezione
        /// era gia' accurata; era la correzione stessa a sbagliare.
        ///
        /// **La direzione dell'errore contava:** sottostimare i giri significa imbarcare meno
        /// carburante del necessario. Si restava a piedi, non si portava peso inutile.
        ///
        /// Il numero di soste si stima dai giri percorribili senza fermarsi, poi si ricontrolla una
        /// volta sola sul risultato: una seconda iterazione non cambia mai il conteggio in una gara
        /// da 45 minuti, e un ciclo aperto qui non e' desiderabile.
        /// </summary>
        /// <param name="timeToFlagSec">Tempo che manca alla bandiera.</param>
        /// <param name="paceSec">Passo del Player.</param>
        /// <param name="tankLapsRemaining">Giri percorribili col carburante a bordo adesso.</param>
        /// <param name="stintLaps">Giri percorribili con un serbatoio pieno.</param>
        /// <param name="pitLossSec">Tempo perso in una sosta: transito piu' tempo da fermi.</param>
        public static LapsLeftPlan ProjectLapsLeftWithStops(double timeToFlagSec, double paceSec,
                                                            double tankLapsRemaining, double stintLaps,
                                                            double pitLossSec)
        {
            var plan = new LapsLeftPlan();
            if (paceSec <= 0.0) return plan;

            double lapsIfNoStop = Math.Max(0.0, timeToFlagSec) / paceSec;
            plan.LapsLeft = lapsIfNoStop;

            // Senza dati sulla sosta non si inventa una penalita': meglio la proiezione nuda.
            if (stintLaps <= 0.0 || pitLossSec <= 0.0) return plan;

            plan.StopsNeeded = StopsRequired(lapsIfNoStop, tankLapsRemaining, stintLaps);
            if (plan.StopsNeeded == 0) return plan;

            plan.LapsLeft = Math.Max(0.0, timeToFlagSec - plan.StopsNeeded * pitLossSec) / paceSec;

            // Le soste tolgono giri, quindi il fabbisogno puo' solo calare: si ricontrolla una volta.
            plan.StopsNeeded = StopsRequired(plan.LapsLeft, tankLapsRemaining, stintLaps);
            return plan;
        }

        /// <summary>
        /// Quante soste servono per coprire <paramref name="lapsToCover"/> partendo dal carburante
        /// che si ha adesso. Zero se il serbatoio attuale basta.
        /// </summary>
        public static int StopsRequired(double lapsToCover, double tankLapsRemaining, double stintLaps)
        {
            if (stintLaps <= 0.0) return 0;
            if (lapsToCover <= tankLapsRemaining) return 0;
            return (int)Math.Ceiling((lapsToCover - tankLapsRemaining) / stintLaps);
        }

        /// <summary>
        /// Il totale giri del Player dopo il filtro di stabilita', a partire da **dove si trovera'
        /// quando esce la bandiera** (<paramref name="posAtCheckered"/>, continuo).
        ///
        /// Esiste come funzione pura per una ragione precisa: il difetto Y-31 non era dentro
        /// <see cref="UpdateLatchedLaps"/> — quello funzionava — ma nel *chiamante*, che arrotondava
        /// la posizione all'intero prima di passarla. Un test sul solo filtro resterebbe verde con
        /// il difetto rimesso al suo posto; questo lo intercetta.
        ///
        /// I due tetti sul leader restano applicati alla posizione **continua**, cosi' il
        /// troncamento avviene una volta sola, dentro il filtro.
        /// </summary>
        /// <param name="posAtCheckered">Posizione del Player alla bandiera: giri completati + frazione.</param>
        /// <param name="currentLatched">Totale attualmente memorizzato.</param>
        /// <param name="allowDecrease">Falso in corsia box: li' la proiezione non e' confrontabile.</param>
        /// <param name="leaderTotalCap">
        /// Tetto sul totale del leader. <c>0</c> = nessun tetto (leader non ancora stimato, oppure
        /// multiclasse: una GT3 e una GTP non fanno lo stesso numero di giri — vedi Y-19).
        /// </param>
        /// <param name="playerIsLeader">Se il Player e' P1 il suo totale **e'** quello del leader.</param>
        public static double ProjectPlayerTotalLaps(double posAtCheckered,
                                                    double currentLatched,
                                                    bool allowDecrease,
                                                    double leaderTotalCap,
                                                    bool playerIsLeader)
        {
            double projected = posAtCheckered;

            if (playerIsLeader) projected = leaderTotalCap;
            else if (leaderTotalCap > 0.0) projected = Math.Min(projected, leaderTotalCap);

            return UpdateLatchedLaps(projected, currentLatched, allowDecrease);
        }



        public void ResetSession()

        {

            Results = new RaceAnalysisResult();

            _isRaceFinished = false;

            _leaderHasFinished = false;

            _latchedLeaderTotalLaps = 0.0;

            _latchedPlayerTotalReality = 0.0;
            Results.ProjectedPosAtCheckered = 0.0;
            Results.LeaderProjectedPosAtCheckered = 0.0;
            _playerPosStabilizer.Reset();
            _leaderPosStabilizer.Reset();
            for (int i = 0; i < ValidationHorizonsSec.Length; i++)
            {
                _projectionAtHorizon[i] = 0.0;
                _p1ProjectionAtHorizon[i] = 0.0;
            }
            _hasSeenPositiveCountdown = false;
            _leaderPosAtExpiry = -1.0;
            _leaderNameAtExpiry = "";
            _leaderTrackPctAtExpiry = -1.0;
            _lastStrictFlagMoment = new RaceTimeProjection.FlagMoment();
            _lastFastestLapSeen = 0.0;
            _lastPhysicalFloor = 0.0;
            _lastStabilizerTimeLeft = -1.0;
            _lastLoggedPlayerTotal = -1.0;
            _lastLoggedLeaderTotal = -1.0;

            _smoothedLeaderPace = 0.0;
            _leaderPaceFilter.Reset();
            _lastGoodLeaderAbsolutePos = -1.0;
            _lastGoodLeaderPosSessionTimeLeft = -1.0;
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