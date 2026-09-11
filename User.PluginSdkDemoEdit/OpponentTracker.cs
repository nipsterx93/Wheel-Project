// -------------------------------------------------------------------------

// FILE: OpponentTracker.cs

// VERSION: Fix errori 41

// -------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using GameReaderCommon;
using SimHub.Plugins;



namespace SimRIG

{

    public class OpponentTelemetryData

    {

        public string Name { get; set; }
        public string CarClass { get; set; }

        public double LastPosPct { get; set; }

        public double LastTimeSec { get; set; }

        public double LastLapStartTimeSec { get; set; } = -1.0;

        /// <summary>
        /// <c>LastLapStartTimeSec</c> è ancorato a un attraversamento del traguardo che abbiamo
        /// davvero visto, e non al primo istante in cui la vettura è comparsa nella lista.
        ///
        /// Serve perché il primo ancoraggio cade a metà giro: una vettura che entra a gara in corso
        /// viene vista per la prima volta in un punto qualunque del tracciato, e il tempo fino al
        /// traguardo successivo è una **frazione di giro**, non un giro. Misurato sul replay Daytona
        /// del 2026-08-23: le GTP (classe 4029) sono entrate al giro 5-7 e hanno preso baseline di
        /// 56.4, 62.3, 64.4, 67.0 s contro i ~100 s reali — mentre LMP2 e GT3, presenti dal via,
        /// avevano baseline corrette di 97-107 s.
        ///
        /// Il danno è permanente perché la baseline si sostituisce **solo con un valore più basso**
        /// (<c>OpponentTracker.cs</c>, ramo "Baseline Reset (Improvement)"): i giri veri, essendo
        /// più lenti di una frazione di giro, non qualificano mai.
        /// </summary>
        public bool HasWitnessedLapStart { get; set; } = false;



        public double LastValidSpeedKmh { get; set; }

        public double PersonalTopSpeed { get; set; }



        public int CurrentMicrosector { get; set; } = 0;

        /// <summary>
        /// Bucket del timestamp di passaggio, a risoluzione piu' fine del microsettore di
        /// velocita'. Sono deliberatamente **separati**: i microsettori di velocita' alimentano
        /// ZoneDrop (diagnosi gomme avversari) e la loro granularita' non va cambiata per un
        /// motivo che riguarda solo la precisione del gap.
        /// </summary>
        public int CurrentTimestampBucket { get; set; } = -1;



        // Nuove variabili per calcolo media microsettore

        public double MicrosectorSumSpeed { get; set; } = 0.0;

        public int MicrosectorCount { get; set; } = 0;

        public bool MicrosectorIsDirty { get; set; } = false;



        public double[] MaxSpeedPerMicrosector { get; set; } = new double[100];

        public double[] CurrentLapMicrosectorSpeeds { get; } = new double[100];
        public double[] BestLapMicrosectorSpeeds { get; } = new double[100];
        public double[] BestLapMicrosectorSpeedsWet { get; } = new double[100];
        public double FuelAtBestLap { get; set; } = 0.0;
        public double FuelAtBestLapWet { get; set; } = 0.0;
        public double LapStartFuel { get; set; } = 0.0;
        public int BestMicrosectorSpeedLapCount { get; set; } = 0;
        public int BestMicrosectorSpeedLapCountWet { get; set; } = 0;
        public double BestLapAvgSpeedLow { get; set; } = 0.0;
        public double BestLapAvgSpeedLowWet { get; set; } = 0.0;
        public double BestLapAvgSpeedMid { get; set; } = 0.0;
        public double BestLapAvgSpeedMidWet { get; set; } = 0.0;
        public double BestLapAvgSpeedHigh { get; set; } = 0.0;
        public double BestLapAvgSpeedHighWet { get; set; } = 0.0;
        public double BestRawLapTime { get; set; } = 0.0;
        public double BestRawLapTimeWet { get; set; } = 0.0;
        public double BestNormalizedLapTime { get; set; } = 0.0;
        public double BestNormalizedLapTimeWet { get; set; } = 0.0;
        public bool WasInsideGeofenceThisLap { get; set; } = false;
        public string Diagnosis { get; set; } = "ANALYZING";

        public double CurrentSpeedDrop { get; set; } = 0.0;



        public double ZoneDropLow { get; set; } = 0.0;

        public double ZoneDropMid { get; set; } = 0.0;

        public double ZoneDropHigh { get; set; } = 0.0;



        public LapSectorTimeContainer RawTimes { get; } = new LapSectorTimeContainer();
        public LapSectorTimeContainer NormalizedTimes { get; } = new LapSectorTimeContainer();

        public double SectorPaceDropDueToTyres { get; set; } = 0.0;
        public double SectorPaceDropDueToTyresRaw { get; set; } = 0.0;



        public double NormalizedRaceStartPace { get; set; } = 0.0;

        /// <summary>
        /// Miglioramento grande dell'ancora in attesa di conferma (Y-49). Zero = nessuno.
        /// Un solo giro molto piu' veloce puo' essere un errore di misura; due consecutivi
        /// d'accordo fra loro no.
        /// </summary>
        public double PendingPaceAnchor { get; set; } = 0.0;

        public double PaceDropDueToTyres { get; set; } = 0.0;



        public int LastLap { get; set; } = -1;

        public int LastPitLap { get; set; } = 0;

        public int LastStopLap { get; set; } = -1;

        public int HighestLapSeen { get; set; } = 0;

        public double EstimatedFuel { get; set; } = 0.0;

        public double FuelAfterLastPit { get; set; } = 0.0;

        public double LastPitFuelAdded { get; set; } = 0.0;

        public int LastRefuelLap { get; set; } = 0;
        public double LastStintLaps { get; set; } = 0.0;
        public double YellowLapsInStint { get; set; } = 0.0;

        public double EstimatedFuelTank { get; set; } = 0.0;
        public double FuelToAddTime { get; set; } = 0.0;

        public int LapCount { get; set; } = 0;

        public double EstimatedPitWindow { get; set; } = 0.0;

        public double EstimatedPitWindowTargetLap { get; set; } = 0.0;



        public double EntryTimeSec { get; set; } = 0.0;

        public double ExitTimeSec { get; set; } = 0.0;

        public double? StopStartTimeSec { get; set; }

        public double StationaryTimeSec { get; set; } = 0.0;

        public bool IsInsideGeofence { get; set; } = false;
        public bool HasExitedPitZoneAtLeastOnce { get; set; } = false;
        public double? LowSpeedStartSec { get; set; } = null;
        public double? HighSpeedStartSec { get; set; } = null;
        public bool IsSpatiallyInsideStrict { get; set; } = false;
        public double SpatialStrictEntryTimeSec { get; set; } = 0.0;
        public int SpatialStrictEntryLap { get; set; } = 0;
        public bool StrictPitValidInTransit { get; set; } = false;
        public double MaxSpeedInPitThisTransit { get; set; } = 0.0;

        public int PitCount { get; set; } = 0;
        public int ClassPosition { get; set; } = 0;

        public bool HasCountedPitThisTransit { get; set; } = false;

        public int CarIdx { get; set; } = -1;
        public bool IsOnPitRoad { get; set; } = false;
        public IracingTrackSurface TrackSurface { get; set; } = IracingTrackSurface.NotInWorld;
        public double? NotInWorldStartSec { get; set; } = null;
        public double NotInWorldDurationSec { get; set; } = 0.0;
        public int NativePitStopCount { get; set; } = 0;
        public int NativeClassPosition { get; set; } = 0;
        public float NativeLapDistPct { get; set; } = 0f;
        public double InPitStallStartTimeSec { get; set; } = 0.0;
        public bool WasInPitStall { get; set; } = false;
        public double? PitRoadStationaryStartSec { get; set; } = null;
        public double? PitRoadStationaryPosPct { get; set; } = null;
        public bool NeedsPitStop { get; set; } = true;

        public bool LastPitTiresChanged { get; set; } = true;
        public double LastPitTransitTimeSec { get; set; } = 0.0;
        public double LastPitStationaryTimeSec { get; set; } = 0.0;
        public double LastPitInOutAccDecTimeSec { get; set; } = 0.0;
        public double LastOpponentStrictPitLaneTime { get; set; } = 0.0;

        public double[] MicrosectorTimestamps { get; } = new double[OpponentTracker.TimestampBucketCount];
        public SectorTracker PitZone { get; set; } = new SectorTracker { Name = "PitZone" };
        public SectorTracker ExtendedSectorRacingZone { get; set; } = new SectorTracker { Name = "ExtendedSectorRacingZone" };
        public SectorTracker ExtendedPitZone { get; set; } = new SectorTracker { Name = "ExtendedPitZone" };

        public List<double> RecentPrePitSectors { get; } = new List<double>();
        public double PrePitNormalizedAverage { get; set; } = 0.0;
        public double[] PostPitNormalizedTimes { get; } = new double[3] { 0.0, 0.0, 0.0 };
        public double[] PostPitNormalizedDeltas { get; } = new double[3] { 99.0, 99.0, 99.0 };
        public double[] PostPitWarmupPenalties { get; } = new double[3] { 0.0, 0.0, 0.0 };
        public int PostPitTransitCount { get; set; } = -1;

        // Prova del Nove / Verification fields
        public bool IsTiresChangedProvisional { get; set; } = false;
        public int BackupLastPitLap { get; set; } = 0;
        public double BackupNormalizedRaceStartPace { get; set; } = 0.0;
        public double BackupLapBaseline { get; set; } = 0.0;
        public double BackupSectorBaseline { get; set; } = 0.0;
        public double BackupRawLapBaseline { get; set; } = 0.0;
        public double BackupRawSectorBaseline { get; set; } = 0.0;
        public double BackupSectorPaceDropDueToTyres { get; set; } = 0.0;
        public double BackupSectorPaceDropDueToTyresRaw { get; set; } = 0.0;
        public double BackupPaceDropDueToTyres { get; set; } = 0.0;
        public double BackupBestFreshNormalTime { get; set; } = 0.0;
        public double BackupBestRawTime { get; set; } = 0.0;
        public int BackupBestRawTimeLapCount { get; set; } = 0;
        public double BackupLapPaceDrop { get; set; } = 0.0;
        public double BackupSectorPaceDrop { get; set; } = 0.0;
        public double BackupRawLapPaceDrop { get; set; } = 0.0;
        public double BackupRawSectorPaceDrop { get; set; } = 0.0;
        public bool IsProvisionalSectorDirty { get; set; } = false;
    }



    public class OpponentTracker

    {

        private Dictionary<string, OpponentTelemetryData> _telemetry = new Dictionary<string, OpponentTelemetryData>();



        public IReadOnlyDictionary<string, OpponentTelemetryData> TrackedOpponents => _telemetry;

        public void AddTrackedOpponent(string name, OpponentTelemetryData data)
        {
            _telemetry[name] = data;
        }

        /// <summary>
        /// Il tempo misurato fra l'ancoraggio e adesso e' un giro vero?
        ///
        /// Lo e' solo se **abbiamo visto anche l'inizio** di quel giro. L'ancoraggio piazzato alla
        /// prima comparsa di una vettura cade in un punto qualunque del tracciato, quindi il tempo
        /// fino al traguardo successivo e' una frazione di giro — piu' breve del vero, e per questo
        /// capace di avvelenare in modo permanente una baseline che si sostituisce solo al ribasso.
        ///
        /// Stesso principio gia' adottato altrove nel progetto: una misura che il tempo trascorso
        /// non giustifica e' un artefatto (<c>TrackPositionValidator</c>, <c>GapJump</c>), e gli
        /// outlap non entrano nelle medie (Y-11).
        /// </summary>
        public static bool CanMeasureLap(int lastLap, double lapStartClock, bool hasWitnessedLapStart)
        {
            return lastLap != -1 && lapStartClock > 0.0 && hasWitnessedLapStart;
        }

        /// <summary>
        /// La capienza del serbatoio da usare per un avversario.
        ///
        /// **La capienza e' una proprieta' della vettura, non della classe.** Nella sola classe GT3
        /// di Road Atlanta convivono 100 L (BMW M4 GT3 EVO), 104 L (Ferrari 296 GT3) e 110 L
        /// (Ford Mustang GT3, McLaren 720S GT3): appiattirle su un unico numero costa fino a 10 L.
        ///
        /// Il difetto corretto qui: <c>opponentMaxTank</c> veniva calcolato bene — capienza del
        /// modello letta dal database, moltiplicata per il BoP di sessione — e poi **sovrascritto**
        /// tre righe dopo dal record di traccia+classe, che e' un valore unico per tutta la classe.
        /// Il log mostrava il valore per vettura mentre il codice ne usava un altro, ed e' il motivo
        /// per cui non si notava.
        ///
        /// Il record di classe resta come ripiego **solo per una vettura non riconosciuta nel
        /// database**: li' la capienza per modello non esiste e <c>opponentMaxTank</c> ricade su una
        /// costante di famiglia (120 GT3 / 89 GTP / 96.9 LMP2 / 40 F4). Un dato di traccia misurato
        /// e' comunque meglio di una costante.
        /// </summary>
        /// <param name="perCarTank">Capienza del modello dal database, gia' scalata col BoP.</param>
        /// <param name="classRecordTank">Capienza del record traccia+classe. Zero = non presente.</param>
        /// <param name="carRecognisedInDb">La vettura e' stata trovata nel database per modello?</param>
        public static double ResolveOpponentMaxTank(double perCarTank, double classRecordTank,
                                                    bool carRecognisedInDb)
        {
            if (!carRecognisedInDb && classRecordTank > 0.0) return classRecordTank;
            return perCarTank;
        }

        /// <summary>Sotto questa soglia non e' un giro: e' un contatore che e' saltato.</summary>
        public const double MinCredibleOpponentLapSec = 20.0;

        /// <summary>Sopra questa soglia il "giro" contiene una sosta, una bandiera o un buco.</summary>
        public const double MaxCredibleOpponentLapSec = 600.0;

        /// <summary>
        /// Il valore e' utilizzabile come tempo sul giro di un avversario?
        /// Oltre alle due soglie di buon senso si applica il limite **fisico** del tracciato
        /// (<see cref="RaceTimeProjection.IsPhysicallyPlausibleLap"/>), che a differenza di una
        /// costante si trasferisce da un circuito all'altro.
        /// </summary>
        /// <summary>
        /// Quanto puo' migliorare l'ancora del passo in un colpo solo, prima che il miglioramento
        /// vada confermato. E' la stessa ampiezza della vecchia tolleranza sul lato veloce: un
        /// miglioramento entro il 2% e' ordinario e si prende subito.
        /// </summary>
        public const double OrdinaryImprovementFrac = 0.020;

        /// <summary>
        /// Quanto due giri devono somigliarsi perche' il secondo confermi il primo. E' la stessa
        /// ampiezza della tolleranza sul lato lento, per non introdurre un terzo numero da tarare.
        /// </summary>
        public const double ConfirmationFrac = 0.035;

        /// <summary>
        /// Aggiorna l'ancora del passo di una vettura con un giro nuovo, e dice se quel giro va
        /// considerato valido.
        ///
        /// **Il difetto che questa funzione chiude (Y-49).** L'ancora sapeva gia' migliorare — il
        /// codice la abbassava a ogni giro piu' veloce. Ma il controllo di validita' girava
        /// **prima**, e scartava i giri che deviassero dall'ancora oltre il 2% sul lato veloce.
        /// Cosi' un'ancora sbagliata rendeva invalidi proprio i giri che l'avrebbero corretta: il
        /// ramo che li avrebbe promossi non veniva mai raggiunto.
        ///
        /// Misurato su Road Atlanta `20260901_211532`, su 803 tick di gara:
        ///
        /// <code>
        ///   Baseline Established          48 volte
        ///   Baseline Updated (Better)     55 volte
        ///   Baseline Reset (Improvement)   0 volte
        ///
        ///   Alessandro Barbagallo: ancora fissata a 278.563 s al giro 7, mai piu' aggiornata.
        ///   Finestra che ne derivava: [273.0 , 288.3]. I suoi giri veri: ~69 s. Tutti rifiutati.
        /// </code>
        ///
        /// **E c'era un secondo blocco, aritmetico.** Il ramo "Reset (Improvement)" pretendeva un
        /// miglioramento superiore a 1.5 s, ma la finestra ne concedeva al massimo il 2% dell'ancora.
        /// Per qualunque ancora sotto i 75 s le due condizioni sono incompatibili — su una GTP a
        /// 69 s la finestra concede 1.38 s e il ramo ne chiede 1.5. **Non poteva scattare mai**, ed
        /// e' coerente con gli zero casi contati in gara.
        ///
        /// **Il criterio adottato.** Un giro piu' veloce non viene piu' giudicato dall'ancora che
        /// deve correggere. Ma non lo si prende nemmeno alla cieca: col criterio del massimo
        /// (punto 4) un passo falsamente **veloce** e' la direzione pericolosa, perche' porta quella
        /// vettura al comando. Quindi:
        ///
        /// <list type="bullet">
        /// <item>miglioramento <b>ordinario</b> (entro il 2%): si prende subito, come prima;</item>
        /// <item>miglioramento <b>grande</b>: si tiene in attesa e serve un secondo giro che lo
        /// confermi. Due giri consecutivi d'accordo non sono un errore di misura.</item>
        /// </list>
        ///
        /// Il giro in attesa **non entra nella storia**: se entrasse, un errore di misura
        /// inquinerebbe la media mobile anche senza diventare ancora.
        ///
        /// La plausibilita' assoluta resta a monte, in <see cref="IsCredibleOpponentLap"/>: questa
        /// funzione presuppone un giro gia' credibile di suo.
        /// </summary>
        /// <param name="lapSec">Il giro appena completato, gia' normalizzato.</param>
        /// <param name="anchor">L'ancora corrente. Zero = non ancora stabilita.</param>
        /// <param name="pending">Il miglioramento grande in attesa di conferma. Zero = nessuno.</param>
        /// <returns>Vero se il giro va trattato come valido e puo' entrare nella storia.</returns>
        public static bool UpdatePaceAnchor(double lapSec, ref double anchor, ref double pending)
        {
            if (lapSec <= 0.0) return false;

            // Prima misura: diventa l'ancora. Non c'e' niente con cui confrontarla.
            if (anchor <= 0.0)
            {
                anchor = lapSec;
                pending = 0.0;
                return true;
            }

            // Non e' un miglioramento: il lato lento lo giudica il chiamante, con la sua finestra.
            if (lapSec >= anchor)
            {
                pending = 0.0;
                return true;
            }

            double improvement = anchor - lapSec;

            // Miglioramento ordinario: si prende subito. E' il comportamento di sempre.
            if (improvement <= anchor * OrdinaryImprovementFrac)
            {
                anchor = lapSec;
                pending = 0.0;
                return true;
            }

            // Miglioramento grande gia' visto una volta, e il secondo giro lo conferma.
            if (pending > 0.0 && Math.Abs(lapSec - pending) <= pending * ConfirmationFrac)
            {
                anchor = lapSec;
                pending = 0.0;
                return true;
            }

            // Prima volta, o due miglioramenti grandi che non si somigliano: si aspetta.
            pending = lapSec;
            return false;
        }

        public static bool IsCredibleOpponentLap(double lapSec, double trackLengthMeters)
        {
            if (lapSec <= MinCredibleOpponentLapSec) return false;
            if (lapSec >= MaxCredibleOpponentLapSec) return false;
            return RaceTimeProjection.IsPhysicallyPlausibleLap(lapSec, trackLengthMeters);
        }

        /// <summary>
        /// Il tempo sul giro di un avversario: **quello dichiarato dal gioco**, col cronometraggio
        /// interno solo come ripiego quando il gioco non lo fornisce.
        ///
        /// **Perche' il cronometro interno non va bene** (Y-32). Il tempo veniva misurato come
        /// <c>clock adesso - clock all'ultimo cambio di giro</c>, cioe' campionando: il passaggio sul
        /// traguardo si vede al primo tick *dopo* che e' avvenuto. L'errore vale quindi fino a un
        /// tick per estremo, e un tick non e' un istante — e' quanto tempo di gara scorre fra due
        /// letture. Misurato sul replay Road Atlanta del 2026-08-30: **3.0 secondi di gara per
        /// tick** (min 2.8, max 3.4), perche' il replay girava a 3x.
        ///
        /// Il risultato e' un'incoerenza logica prima ancora che numerica: la prima misura fissa la
        /// baseline della vettura, e da quella baseline si ricava la finestra entro cui i giri
        /// successivi sono considerati credibili (-2% / +3.5%). Ma l'incertezza dello strumento
        /// (~4.3% di giro a 3x) e' **piu' larga della tolleranza che quello strumento poi impone**.
        /// Su `Kalyann Mey4`: primo giro misurato 62.95 s contro 69.4 s reali — 6.45 s, circa due
        /// tick — baseline normalizzata a 60.550, finestra risultante [59.34, 62.67] s, e tutti i
        /// giri veri (~66.6 s normalizzati) rifiutati come anomali per il resto della gara. Il passo
        /// del leader e' rimasto a ~61 s contro 69.4 reali, e da li' i giri totali del leader
        /// finivano a 42-45 invece di 38.8.
        ///
        /// Il gioco il tempo sul giro ce lo da' gia': e' la stessa fonte che si usa per il Player
        /// (<c>state.LastLapTimeSec</c>), e per gli avversari il plugin la leggeva gia' altrove per
        /// la classifica a schermo. Le normalizzazioni (carburante, temperatura) si applicano dopo,
        /// esattamente come prima: cambia da dove arriva il numero grezzo, non cosa ci si fa.
        ///
        /// **Limite noto e accettato:** il gioco espone l'*ultimo giro completato*, quindi in linea
        /// teorica si puo' leggere il giro precedente invece di quello appena chiuso. E' uno
        /// sfasamento di un giro sulla stessa vettura, non un errore di grandezza — irrilevante per
        /// una media di passo, e comunque preferibile a un errore di quantizzazione che a 3x vale
        /// il 4% del giro.
        /// </summary>
        /// **Il ripiego sul cronometro interno e' stato rimosso** (Y-39), e la misura lo impone.
        /// Sul replay Road Atlanta `20260830_121813`, 46 baseline: 42 lette dal gioco, di cui solo
        /// due fuori scala — e sono due giri lenti **veri** (139.5 s, 191.8 s: out-lap). Le altre 4
        /// venivano dal ripiego, ed erano **sbagliate tutte e quattro**:
        ///
        /// <code>
        ///   Sven Neiss           gioco 0.000   ripiego  63.350   (passo vero 69.15-71.38)
        ///   Ethan Carlton Wong   gioco 0.000   ripiego  65.150
        ///   Fatih Kaya           gioco 0.270   ripiego  75.600
        ///   Alessandro Barbagallo gioco 1.998  ripiego 237.616
        /// </code>
        ///
        /// Il motivo per cui falliscono tutte insieme e' strutturale, non sfortuna: il ripiego si
        /// attiva **esattamente quando il gioco non ha ancora un tempo** per quella vettura, cioe'
        /// quando l'abbiamo appena presa in carico — che e' anche il momento in cui il nostro
        /// ancoraggio e' meno affidabile. Le due condizioni non sono indipendenti: il ripiego entra
        /// in gioco solo nei casi in cui e' peggiore.
        ///
        /// Un dato mancante deve restare mancante. Sostituirlo con una stima produce un passo
        /// plausibile e falso, e quel passo diventa la baseline della vettura — con tutto quello che
        /// ne segue (vedi la finestra di validita' in Y-32). Meglio nessuna baseline che una finta:
        /// a valle i consumatori del passo hanno gia' i loro ripieghi.
        /// </summary>
        /// <param name="gameReportedSec">Tempo dichiarato dal gioco per quella vettura.</param>
        /// <param name="trackLengthMeters">Serve al limite fisico. Zero = nessun limite applicabile.</param>
        /// <returns>Il tempo da usare, oppure <c>0.0</c> se il gioco non ne ha uno credibile.</returns>
        public static double ResolveOpponentLapTime(double gameReportedSec, double trackLengthMeters)
        {
            return IsCredibleOpponentLap(gameReportedSec, trackLengthMeters) ? gameReportedSec : 0.0;
        }

        /// <summary>
        /// Il cambio di giro appena osservato era un attraversamento vero, quindi l'istante attuale
        /// e' un ancoraggio valido per misurare il giro successivo?
        ///
        /// Alla prima comparsa (<paramref name="lastLapBeforeChange"/> == -1) non lo e': il
        /// contatore differisce dal sentinella non perche' la vettura abbia tagliato il traguardo,
        /// ma perche' l'abbiamo appena incontrata.
        /// </summary>
        public static bool AnchorIsGenuine(int lastLapBeforeChange)
        {
            return lastLapBeforeChange != -1;
        }

        /// <summary>
        /// Quanti bucket per giro per i timestamp di passaggio. A 400, su un tracciato da 4.2 km
        /// sono ~10 m l'uno: la stessa densita' del giro di riferimento di irdashies.
        ///
        /// **Misurato** su un profilo di velocita' con 12 curve (Misano, giro 91.3 s): l'errore
        /// massimo nel ricostruire il tempo-alla-posizione passa da **141 ms a 9 ms** restando in
        /// interpolazione lineare. Le bande di isteresi dei gate strategici (Y-12) valgono
        /// 150-250 ms, quindi si passa da "sul bordo della banda" a "17 volte dentro".
        /// La spline cubica di irdashies porterebbe a 0.9 ms: precisione spesa sotto il rumore
        /// della decisione che alimenta, quindi non e' stata adottata.
        ///
        /// **Non e'** il microsettore di velocita', che resta a 100.
        ///
        /// Perche' 400 e' sicuro: per saltare un bucket servirebbe percorrerne piu' di uno fra
        /// due tick, cioe' **458 km/h anche a soli 12 Hz** di aggiornamento. Un bucket saltato
        /// resterebbe col timestamp del giro prima, farebbe fallire la guardia sui timestamp e
        /// manderebbe nel fallback proprio dove volevamo piu' precisione.
        /// </summary>
        public const int TimestampBucketCount = 400;

        /// <summary>Bucket del timestamp per una posizione in frazione di giro, con clamp.</summary>
        public static int TimestampBucketOf(double trackPositionPercent)
        {
            int bucket = (int)(trackPositionPercent * TimestampBucketCount);
            if (bucket < 0) return 0;
            if (bucket > TimestampBucketCount - 1) return TimestampBucketCount - 1;
            return bucket;
        }

        public double[] PlayerMicrosectorTimestamps { get; } = new double[TimestampBucketCount];
        private int _playerLastMicrosector = -1;
        private int _playerLastTimestampBucket = -1;
        private int _playerLastCompletedLaps = -1;
        public OpponentTelemetryData PlayerData { get; } = new OpponentTelemetryData { Name = "PLAYER" };
        private HashSet<string> _loggedOpponentNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> _loggedOpponentFuelDetails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool _liveFuelAvgLogged = false;

        /// <summary>
        /// Scarto entro cui due deduzioni della capacita' base parlano dello stesso serbatoio.
        /// Un litro: sotto, e' rumore di lettura; sopra, e' un'altra vettura o un BoP diverso.
        /// </summary>
        public const double BaseCapacityAgreementLitres = 1.0;

        /// <summary>
        /// Consenso sulla capacita' base dedotta dal Player. Il campione arriva a ogni tick, quindi
        /// ce ne sono molti: la mediana e' il filtro naturale. Vedi Y-27.
        /// </summary>
        private readonly CalibrationConsensus _baseCapacityConsensus =
            new CalibrationConsensus(BaseCapacityAgreementLitres);



        public double ClassRaceStartingFuel { get; private set; } = 0.0;

        public double ClassTopSpeed { get; private set; } = 0.0;

        public double ClassAveragePaceDrop { get; private set; } = 0.0;
        public double ClassAverageSectorPaceDrop { get; private set; } = 0.0;
        public double ClassAverageSectorPaceDropRaw { get; private set; } = 0.0;

        public double ClassBestExtendedPitZoneTime { get; private set; } = 0.0;
        public double ClassBestPitZoneRacingTime { get; private set; } = 0.0;

        public bool OpponentPittedInWet { get; set; } = false;

        public string LastOpponentPittedInWetName { get; set; } = "";
        public string CrossoverAlertState { get; private set; } = "NONE";
        public double CrossoverDeltaSeconds { get; private set; } = 0.0;



        public IracingTelemetryBridge IracingBridge { get; } = new IracingTelemetryBridge();

        /// <summary>
        /// Restituisce la posizione percentuale lungo il tracciato [0.0, 1.0] per un avversario.
        /// Priorità tassativa:
        /// 1. Telemetria nativa CarIdxLapDistPct a 60 Hz via IracingBridge
        /// 2. opp.TrackPositionPercent di SimHub (fallback)
        /// 3. Ultima posizione valida memorizzata in TrackedOpponents (continuità)
        /// </summary>
        public double GetOpponentTrackPosition(GameReaderCommon.Opponent opp, SessionState state)
        {
            if (opp == null) return 0.0;

            int carIdx = -1;
            if (state?.Metadata != null && !string.IsNullOrEmpty(opp.Name))
            {
                carIdx = state.Metadata.GetCarIdxFor(opp.Name);
            }
            if (carIdx < 0 && !string.IsNullOrEmpty(opp.Id) && int.TryParse(opp.Id, out int parsedId))
            {
                carIdx = parsedId;
            }

            // 1. Tassativamente prioritario: telemetria nativa CarIdxLapDistPct a 60 Hz via IracingBridge
            if (IracingBridge != null && IracingBridge.IsAvailable && carIdx >= 0)
            {
                float nativeDist = IracingBridge.GetLapDistPct(carIdx);
                if (nativeDist > 0.0f && nativeDist <= 1.0f)
                {
                    return (double)nativeDist;
                }
            }

            // 2. Fallback subordinato: posizione SimHub
            if (opp.TrackPositionPercent.HasValue && opp.TrackPositionPercent.Value > 0.0)
            {
                return opp.TrackPositionPercent.Value;
            }

            // 3. Fallback di continuità: ultimo valore valido tracciato
            if (!string.IsNullOrEmpty(opp.Name) && TrackedOpponents.TryGetValue(opp.Name, out var tData) && tData.LastPosPct > 0.0)
            {
                return tData.LastPosPct;
            }

            return 0.0;
        }

        public OpponentTracker() { }



        public void Update(
            GameData data,
            DataPluginDemoSettings settings,
            TyreSelectionScope playerTyreScope,
            SessionState state,
            PitRadar radar,
            double currentSessionClock,
            double raceStartingFuel,
            double maxTank,
            double effectiveClassFuelBurn,
            double playerBestStrictPitZoneTime,
            double playerBestExtendedPitZoneTime,
            double raceLapsRemaining,
            LogManager log,
            double fuelWeightCoef = 0.03,
            double tempCoef = 0.05,
            SimHub.Plugins.PluginManager pluginManager = null)

        {
            IracingBridge.Update(data?.NewData?.GetRawDataObject(), pluginManager);

            if (!state.IsGameRunning || state.Opponents == null || state.Opponents.Count == 0) return;
            PlayerData.CarClass = state.CarClassId;
            PlayerData.CarIdx = state.PlayerCarIdx;
            PlayerData.TrackSurface = IracingBridge.GetTrackSurface(state.PlayerCarIdx);
            if (PlayerData.TrackSurface != IracingTrackSurface.InPitStall && state.IsInPitBox)
            {
                PlayerData.TrackSurface = IracingTrackSurface.InPitStall;
            }
            PlayerData.IsOnPitRoad = IracingBridge.IsOnPitRoad(state.PlayerCarIdx);
            float nativePlayerDist = IracingBridge.GetLapDistPct(state.PlayerCarIdx);
            PlayerData.NativeLapDistPct = nativePlayerDist;
            PlayerData.LastPosPct = (nativePlayerDist > 0.0f) ? (double)nativePlayerDist : state.TrackPositionPercent;
            state.PlayerTrackSurface = PlayerData.TrackSurface;
            state.PlayerIsOnPitRoad = PlayerData.IsOnPitRoad;

            // Aggiornamento posizioni di classe dinamiche (robusto per multiclasse)
            var classGroups = state.Opponents
                .Where(o => !string.IsNullOrEmpty(o.CarClass))
                .GroupBy(o => o.CarClass, StringComparer.OrdinalIgnoreCase);

            foreach (var grp in classGroups)
            {
                var sorted = grp.OrderBy(o => o.PositionInClass > 0 ? o.PositionInClass : (o.Position > 0 ? o.Position : 999))
                                .ThenByDescending(o => (o.CurrentLap ?? 1) + (o.IsPlayer ? PlayerData.LastPosPct : GetOpponentTrackPosition(o, state)))
                                .ToList();

                for (int rank = 0; rank < sorted.Count; rank++)
                {
                    var opp = sorted[rank];
                    if (opp.IsPlayer)
                    {
                        state.PositionInClass = rank + 1;
                    }
                    if (TrackedOpponents.TryGetValue(opp.Name, out var tData))
                    {
                        tData.ClassPosition = (tData.NativeClassPosition > 0) ? tData.NativeClassPosition : (rank + 1);
                    }
                }
            }

            // Wet Fallback Check:
            if (!state.IsTrackWet && state.GlobalBaselineTemp > 0.0 && state.TrackTemperature > 0.0)
            {
                double tempDrop = state.GlobalBaselineTemp - state.TrackTemperature;
                if (tempDrop >= 1.5)
                {
                    bool playerTrig = false;
                    int playerStintLaps = Math.Max(0, data.NewData.CompletedLaps - PlayerData.LastPitLap);
                    if (PlayerData.BestNormalizedLapTime > 0.0 && PlayerData.NormalizedTimes.LastLapTime > 0.0 && playerStintLaps > 1)
                    {
                        double pPct = (PlayerData.NormalizedTimes.LastLapTime - PlayerData.BestNormalizedLapTime) / PlayerData.BestNormalizedLapTime;
                        if (pPct > 0.05) playerTrig = true;
                    }

                    if (playerTrig)
                    {
                        state.IsTrackWet = true;
                        log?.Log(LogModule.WEATHER, LogType.EVENT, "Wet Fallback Activated", $"TempDrop: {tempDrop:F1}C | PlayerPaceDrop: +{(PlayerData.NormalizedTimes.LastLapTime - PlayerData.BestNormalizedLapTime):F3}s");
                    }
                }
            }

            // Y-52: il BoP arriva gia' interpretato. Prima si riscansionava l'intero
            // SessionInfo YAML (~150 KB) a ogni tick, senza cache.
            var bopDict = state.Metadata.DriverMaxFuelPct;

            // Calcoliamo la capienza di base del Player per dedurre il BoP della classe come fallback
            double playerBaseCap = 120.0;
            if (!string.IsNullOrEmpty(state.CarModel))
            {
                string pClean = CleanString(state.CarModel);
                var carRec = radar.Database.Cars.FirstOrDefault(c => {
                    string dbClean = CleanString(c.CarModel);
                    if (dbClean == pClean) return true;
                    if (pClean.Contains(dbClean) || dbClean.Contains(pClean)) return true;
                    if (dbClean == "porsche992gt3r" && pClean.Contains("porsche") && (pClean.Contains("992") || pClean.Contains("911"))) return true;
                    if (dbClean == "astonmartinvantagegt3evo" && pClean.Contains("aston") && (pClean.Contains("vantage") || pClean.Contains("valkyrie"))) return true;
                    if (dbClean == "mercedesamggt3" && pClean.Contains("mercedes") && pClean.Contains("gt3")) return true;
                    if (dbClean == "bmwm4gt3" && pClean.Contains("bmw") && pClean.Contains("m4") && pClean.Contains("gt3")) return true;
                    if (dbClean == "audir8evogt3" && pClean.Contains("audi") && pClean.Contains("r8") && pClean.Contains("evo")) return true;
                    if (dbClean == "lamborghinihuracangt3" && pClean.Contains("lamborghini") && pClean.Contains("gt3")) return true;
                    if (dbClean == "mclaren720sgt3" && pClean.Contains("mclaren") && (pClean.Contains("720") || pClean.Contains("gt3"))) return true;
                    if (dbClean == "fordmustanggt3" && pClean.Contains("mustang") && pClean.Contains("gt3")) return true;
                    if (dbClean == "corvettez06gt3" && pClean.Contains("corvette") && pClean.Contains("gt3")) return true;
                    return false;
                });
                if (carRec != null)
                {
                    playerBaseCap = carRec.BaseCapacity;
                }
            }

            double playerClassBopPct = 1.0;
            if (playerBaseCap > 0.0)
            {
                if (state.DeducedStartingFuelLimit > 0.0)
                {
                    playerClassBopPct = Math.Round(state.DeducedStartingFuelLimit / playerBaseCap, 3);
                }
                else if (state.MaxFuelCapacity > 0.0)
                {
                    playerClassBopPct = Math.Round(state.MaxFuelCapacity / playerBaseCap, 3);
                }
            }

            double playerMaxTankBoP = Math.Round(playerBaseCap * playerClassBopPct, 2);
            double formationLapBurn = 0.0;
            if (state.RaceStartingFuel > 0.0 && playerMaxTankBoP > 0.0)
            {
                formationLapBurn = playerMaxTankBoP - state.RaceStartingFuel;
            }

            // Logghiamo la deduzione del BoP una sola volta a inizio sessione
            if (!_loggedOpponentNames.Contains("Player_Bop_Deduction"))
            {
                _loggedOpponentNames.Add("Player_Bop_Deduction");
                log.Log(LogModule.OPPONENTS, LogType.EVENT, "Player BoP Deduced", $"Car: {state.CarModel} | DeducedStartingFuel: {state.DeducedStartingFuelLimit:F1}L | LiveMax: {state.MaxFuelCapacity:F1}L | Base: {playerBaseCap:F1}L | BoP: {playerClassBopPct:F3}");
            }

            if (effectiveClassFuelBurn > 0.0 && !_liveFuelAvgLogged)
            {
                _liveFuelAvgLogged = true;
                log.Log(LogModule.OPPONENTS, LogType.EVENT, "Player Live Fuel Average Available", $"{effectiveClassFuelBurn:F2} L/lap | Starting opponent fuel countdown.");
            }

            // Auto-apprendimento del serbatoio di base del Player (solo se leggiamo il BoP reale dallo YAML)
            string playerName = state.Opponents.FirstOrDefault(o => o.IsPlayer)?.Name ?? "";
            double? playerBopPct = state.Metadata?.MaxFuelPctFor(playerName);
            if (!string.IsNullOrEmpty(playerName) && (playerBopPct.HasValue || (bopDict != null && bopDict.TryGetValue(playerName, out _))))
            {
                double playerPct = playerBopPct ?? bopDict[playerName];
                if (state.MaxFuelCapacity > 0.0 && playerPct > 0.0 && !string.IsNullOrEmpty(state.CarModel) && state.CarModel != "DEFAULT")
                {
                    double calculatedBase = Math.Round(state.MaxFuelCapacity / playerPct, 2);
                    var carRec = radar.Database.Cars.FirstOrDefault(c => string.Equals(c.CarModel, state.CarModel, StringComparison.OrdinalIgnoreCase));
                    if (carRec == null)
                    {
                        carRec = new CarRecord { CarModel = state.CarModel, BaseCapacity = calculatedBase };
                        radar.Database.Cars.Add(carRec);
                        radar.SaveDatabase();
                        log.Log(LogModule.SYSTEM, LogType.EVENT, "Car Added to Database", $"{state.CarModel} | BaseCapacity: {calculatedBase:F2}L (learned from Player)");
                    }
                    else
                    {
                        // Il campione arriva a **ogni tick**, ed e' un rapporto fra due letture di
                        // telemetria (MaxFuelCapacity / BoP): una lettura transitoria sbagliata
                        // riscriveva subito il database. Prima l'unica condizione era uno scarto
                        // > 0.5 L, che non e' un filtro ma una soglia di attivazione — Y-27,
                        // trovato da Antigravity in revisione.
                        _baseCapacityConsensus.Add(calculatedBase);

                        double consolidated = _baseCapacityConsensus.Value;
                        if (_baseCapacityConsensus.HasConsensus
                            && Math.Abs(carRec.BaseCapacity - consolidated) > BaseCapacityAgreementLitres)
                        {
                            carRec.BaseCapacity = consolidated;
                            radar.SaveDatabase();
                            log.Log(LogModule.SYSTEM, LogType.EVENT, "Car Updated in Database", $"{state.CarModel} | BaseCapacity: {consolidated:F2}L (mediana su {_baseCapacityConsensus.AgreeingCount}/{_baseCapacityConsensus.SampleCount} campioni concordi)");
                        }
                    }
                }
            }

            if (raceStartingFuel > 0.0)
            {
                ClassRaceStartingFuel = raceStartingFuel;
            }

            double myPos = state.TrackPositionPercent;
            // Timestamp: risoluzione fine, indipendente dai microsettori di velocita'.
            int myTimestampBucket = TimestampBucketOf(myPos);
            if (myTimestampBucket != _playerLastTimestampBucket)
            {
                _playerLastTimestampBucket = myTimestampBucket;
                PlayerMicrosectorTimestamps[myTimestampBucket] = currentSessionClock;
            }

            int myMicrosector = Math.Max(0, Math.Min(99, (int)(myPos * 100)));
            if (myMicrosector != _playerLastMicrosector)
            {
                if (_playerLastMicrosector != -1 && PlayerData.MicrosectorCount > 0)
                {
                    double avgSpeed = PlayerData.MicrosectorSumSpeed / PlayerData.MicrosectorCount;
                    PlayerData.CurrentLapMicrosectorSpeeds[_playerLastMicrosector] = avgSpeed;
                }

                PlayerData.MicrosectorSumSpeed = 0.0;
                PlayerData.MicrosectorCount = 0;
                _playerLastMicrosector = myMicrosector;
            }

            double playerSpeed = data.NewData.SpeedKmh;
            if (playerSpeed >= 0.0 && playerSpeed <= 400.0)
            {
                PlayerData.MicrosectorSumSpeed += playerSpeed;
                PlayerData.MicrosectorCount++;
            }

            if (state.IsInPitLane)
            {
                PlayerData.WasInsideGeofenceThisLap = true;
            }

            if (_playerLastCompletedLaps == -1)
            {
                _playerLastCompletedLaps = data.NewData.CompletedLaps;
                PlayerData.LapStartFuel = data.NewData.Fuel;
            }
            else if (data.NewData.CompletedLaps != _playerLastCompletedLaps)
            {
                double playerRawLapTime = data.NewData.LastLapTime.TotalSeconds;

                // Calculate normalized lap time for Player
                double fuelPenalty = RaceTimeProjection.FuelWeightPenaltySec(data.NewData.Fuel, fuelWeightCoef);
                double tempPenalty = state.GlobalBaselineTemp > 0 ? ((state.TrackTemperature - state.GlobalBaselineTemp) * tempCoef) : 0.0;
                double normalizedPlayerLap = playerRawLapTime - fuelPenalty - tempPenalty;

                 bool playerTyresChanged = playerTyreScope != TyreSelectionScope.None;
                 ProcessLapEndMicrosectors(PlayerData, playerRawLapTime, normalizedPlayerLap, data.NewData.CompletedLaps, data.NewData.Fuel, playerTyresChanged, log, state.TrackWetnessLevel, settings);

                 double refBest = (state.TrackWetnessLevel >= 2) ? PlayerData.BestNormalizedLapTimeWet : PlayerData.BestNormalizedLapTime;
                 double playerTimeDelta = PlayerData.NormalizedTimes.LastLapTime - refBest;
                 GenerateGeneralDiagnosis(PlayerData, playerTimeDelta, state.IsInPitLane, state.TrackWetnessLevel, settings);

                 LogMicrosectorLapEnd(PlayerData, playerRawLapTime, normalizedPlayerLap, data.NewData.CompletedLaps, data.NewData.Fuel, state.TrackWetnessLevel, log);

                _playerLastCompletedLaps = data.NewData.CompletedLaps;
            }

            var activeOpponents = state.Opponents.Where(o => o.TrackPositionPercent.HasValue || GetOpponentTrackPosition(o, state) > 0.0).ToList();
            if (activeOpponents.Count == 0) return;

            double classBestStrictPitTime = playerBestStrictPitZoneTime > 0.0 ? playerBestStrictPitZoneTime : 999.0;
            double classBestExtendedPitTime = playerBestExtendedPitZoneTime > 0.0 ? playerBestExtendedPitZoneTime : 999.0;
            foreach (var opp in _telemetry.Values)
            {
                if (opp.CarClass == state.CarClassId)
                {
                    if (opp.PitZone.BestRawTime > 0.0 && opp.PitZone.BestRawTime < classBestStrictPitTime)
                    {
                        classBestStrictPitTime = opp.PitZone.BestRawTime;
                    }
                    if (opp.ExtendedPitZone.BestRawTime > 0.0 && opp.ExtendedPitZone.BestRawTime < classBestExtendedPitTime)
                    {
                        classBestExtendedPitTime = opp.ExtendedPitZone.BestRawTime;
                    }
                }
            }
            ClassBestPitZoneRacingTime = classBestStrictPitTime < 999.0 ? classBestStrictPitTime : 0.0;
            ClassBestExtendedPitZoneTime = classBestExtendedPitTime < 999.0 ? classBestExtendedPitTime : 0.0;



            double trackLen = state.TrackLengthMeters;

            double pitEntryPct = radar.GetPitEntryPct();
            double effectivePitEntryPct = pitEntryPct > 0.0 ? pitEntryPct : 0.90;
            double inlapFuelDeduction = effectivePitEntryPct * effectiveClassFuelBurn;

            double pitExitPct = radar.GetPitExitPct();



            if (state.SpeedKmh > ClassTopSpeed) ClassTopSpeed = state.SpeedKmh;



            List<double> validClassPaceDrops = new List<double>();
            List<double> validClassSectorPaceDrops = new List<double>();
            List<double> validClassSectorPaceDropsRaw = new List<double>();



            var sortedOpponents = activeOpponents.OrderByDescending(o => GetOpponentTrackPosition(o, state)).ToList();

            foreach (var opp in activeOpponents)
            {
                if (opp.IsPlayer || string.IsNullOrEmpty(opp.Name)) continue;

                // Calcolo della capienza massima nativa del serbatoio per marchio / BoP
                double bopPct = 1.0;
                bool bopFound = false;
                double? oppBopPct = state.Metadata?.MaxFuelPctFor(opp.Name);
                if (oppBopPct.HasValue)
                {
                    bopPct = oppBopPct.Value;
                    bopFound = true;
                }
                else if (!string.IsNullOrEmpty(opp.Name) && bopDict != null && bopDict.TryGetValue(opp.Name, out double pct))
                {
                    bopPct = pct;
                    bopFound = true;
                }
                if (!bopFound && opp.CarClass == state.CarClassId)
                {
                    bopPct = playerClassBopPct;
                }

                double baseCap = 120.0; // GT3 fallback
                if (!string.IsNullOrEmpty(opp.CarClassID) && opp.CarClassID.IndexOf("GTP", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    baseCap = 89.0;
                }
                else if (!string.IsNullOrEmpty(opp.CarClassID) && opp.CarClassID.IndexOf("LMP2", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    baseCap = 96.9;
                }
                else if (!string.IsNullOrEmpty(opp.CarClassID) && opp.CarClassID.IndexOf("F4", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    baseCap = 40.0;
                }

                bool carRecognisedInDb = false;
                if (!string.IsNullOrEmpty(opp.CarName))
                {
                    string oppClean = CleanString(opp.CarName);
                    var carRec = radar.Database.Cars.FirstOrDefault(c => {
                        string dbClean = CleanString(c.CarModel);
                        if (dbClean == oppClean) return true;
                        if (oppClean.Contains(dbClean) || dbClean.Contains(oppClean)) return true;
                        if (dbClean == "porsche992gt3r" && oppClean.Contains("porsche") && (oppClean.Contains("992") || oppClean.Contains("911"))) return true;
                        if (dbClean == "astonmartinvantagegt3evo" && oppClean.Contains("aston") && (oppClean.Contains("vantage") || oppClean.Contains("valkyrie"))) return true;
                        if (dbClean == "mercedesamggt3" && oppClean.Contains("mercedes") && oppClean.Contains("gt3")) return true;
                        if (dbClean == "bmwm4gt3" && oppClean.Contains("bmw") && oppClean.Contains("m4") && oppClean.Contains("gt3")) return true;
                        if (dbClean == "audir8evogt3" && oppClean.Contains("audi") && oppClean.Contains("r8") && oppClean.Contains("evo")) return true;
                        if (dbClean == "lamborghinihuracangt3" && oppClean.Contains("lamborghini") && oppClean.Contains("gt3")) return true;
                        if (dbClean == "mclaren720sgt3" && oppClean.Contains("mclaren") && (oppClean.Contains("720") || oppClean.Contains("gt3"))) return true;
                        if (dbClean == "fordmustanggt3" && oppClean.Contains("mustang") && oppClean.Contains("gt3")) return true;
                        if (dbClean == "corvettez06gt3" && oppClean.Contains("corvette") && oppClean.Contains("gt3")) return true;
                        return false;
                    });
                    if (carRec != null)
                    {
                        baseCap = carRec.BaseCapacity;
                        carRecognisedInDb = true;
                    }
                }

                double opponentMaxTank = Math.Round(baseCap * bopPct, 2);

                double opponentStartingFuel = opponentMaxTank;
                if (raceStartingFuel > 0.0 && playerMaxTankBoP > 0.0 && opp.CarClass == state.CarClassId)
                {
                    opponentStartingFuel = Math.Round(opponentMaxTank * (raceStartingFuel / playerMaxTankBoP), 2);
                }

                double currentPos = GetOpponentTrackPosition(opp, state);

                if (!_telemetry.ContainsKey(opp.Name))
                {
                    _telemetry[opp.Name] = new OpponentTelemetryData
                    {
                        Name = opp.Name,
                        CarClass = opp.CarClass,
                        LastPosPct = currentPos,
                        LastTimeSec = currentSessionClock,
                        FuelAfterLastPit = opponentStartingFuel,
                        EstimatedFuel = opponentStartingFuel,
                        EstimatedFuelTank = opponentStartingFuel
                    };
                }

                var tData = _telemetry[opp.Name];
                if (string.IsNullOrEmpty(tData.CarClass) && !string.IsNullOrEmpty(opp.CarClass))
                {
                    tData.CarClass = opp.CarClass;
                }

                if (tData.PitCount == 0 && tData.FuelAfterLastPit == 0.0 && opponentStartingFuel > 0.0)
                {
                    tData.FuelAfterLastPit = opponentStartingFuel;
                    if (tData.EstimatedFuel == 0.0)
                    {
                        tData.EstimatedFuel = opponentStartingFuel;
                        tData.EstimatedFuelTank = opponentStartingFuel;
                    }
                }

                if (tData.IsInsideGeofence)
                {
                    tData.WasInsideGeofenceThisLap = true;
                }



                int rawCurrentLap = opp.CurrentLap ?? 0;



                if (state.SessionStateStatus >= 3)

                {

                    if (rawCurrentLap > tData.HighestLapSeen) tData.HighestLapSeen = rawCurrentLap;

                    else if (rawCurrentLap < tData.HighestLapSeen) rawCurrentLap = tData.HighestLapSeen;

                }

                else

                {

                    tData.HighestLapSeen = rawCurrentLap;

                }



                tData.LapCount = Math.Max(0, rawCurrentLap - 1);

                if (currentPos <= 0.0)
                {
                    continue;
                }

                // La capienza e' una proprieta' della **vettura**, non della classe: nella stessa
                // classe GT3 convivono 100 L (BMW M4), 104 L (Ferrari 296) e 110 L (Mustang,
                // McLaren). `opponentMaxTank` la ricava dal database per modello e ci applica il BoP
                // di sessione.
                var dbRecord = radar.Database.Tracks.FirstOrDefault(t => t.TrackID == state.TrackId && t.CarClass == tData.CarClass);
                double classRecordMaxTank = dbRecord != null ? dbRecord.MaxTank : 0.0;
                double classMaxTank = ResolveOpponentMaxTank(opponentMaxTank, classRecordMaxTank, carRecognisedInDb);

                // Calcolo live del carburante a bordo e delle finestre di sosta per tutte le vetture
                double classFuelBurn = 0.0;
                double yellowFuelBurn = 0.0;

                // Vincolo User Step 4376: La mediana del Player richiede almeno 3 giri completati (giro corrente >= 4)
                bool isPlayerFuelLatchReady = (data?.NewData?.CompletedLaps ?? 0) >= 3;

                if (!string.IsNullOrEmpty(state.CarClassId) && state.CarClassId != "DEFAULT")
                {
                    if (tData.CarClass == state.CarClassId)
                    {
                        if (isPlayerFuelLatchReady && effectiveClassFuelBurn > 0.0 && playerMaxTankBoP > 0.0)
                        {
                            classFuelBurn = (opponentMaxTank * effectiveClassFuelBurn) / playerMaxTankBoP;
                            if (formationLapBurn > 0.0)
                            {
                                yellowFuelBurn = (opponentMaxTank * formationLapBurn) / playerMaxTankBoP;
                            }
                            else
                            {
                                yellowFuelBurn = classFuelBurn * 0.6;
                            }
                        }
                        else
                        {
                            double initialEstBurn = (dbRecord != null && dbRecord.FuelPerLap > 0.0) ? dbRecord.FuelPerLap : 0.0;
                            if (initialEstBurn > 0.0 && playerMaxTankBoP > 0.0)
                            {
                                classFuelBurn = (opponentMaxTank * initialEstBurn) / playerMaxTankBoP;
                            }
                            else
                            {
                                classFuelBurn = initialEstBurn > 0.0 ? initialEstBurn : 2.5;
                            }
                            yellowFuelBurn = classFuelBurn * 0.6;
                        }
                    }
                    else
                    {
                        classFuelBurn = 3.0;
                        yellowFuelBurn = 1.8;
                    }
                }

                // Logghiamo i dettagli dell'avversario e dei consumi
                if (!string.IsNullOrEmpty(opp.Name))
                {
                    if (!_loggedOpponentNames.Contains(opp.Name))
                    {
                        _loggedOpponentNames.Add(opp.Name);
                        if (classFuelBurn > 0.0)
                        {
                            _loggedOpponentFuelDetails.Add(opp.Name);
                            log.Log(LogModule.OPPONENTS, LogType.EVENT, "Opponent BoP Loaded", $"{opp.Name} | Car: {opp.CarName} | Class: {opp.CarClassID} | BoP Pct: {bopPct:F3} | MaxTank: {opponentMaxTank:F1}L | usato={classMaxTank:F1}L | recClasse={classRecordMaxTank:F1}L | inDb={carRecognisedInDb} | GreenBurn: {classFuelBurn:F2}L/lap | YellowBurn: {yellowFuelBurn:F2}L/lap");
                        }
                        else
                        {
                            log.Log(LogModule.OPPONENTS, LogType.EVENT, "Opponent BoP Loaded", $"{opp.Name} | Car: {opp.CarName} | Class: {opp.CarClassID} | BoP Pct: {bopPct:F3} | MaxTank: {opponentMaxTank:F1}L | usato={classMaxTank:F1}L | recClasse={classRecordMaxTank:F1}L | inDb={carRecognisedInDb}");
                        }
                    }
                    else if (classFuelBurn > 0.0 && !_loggedOpponentFuelDetails.Contains(opp.Name))
                    {
                        _loggedOpponentFuelDetails.Add(opp.Name);
                        log.Log(LogModule.OPPONENTS, LogType.EVENT, "Opponent Fuel Rates Calculated", $"{opp.Name} | GreenBurn: {classFuelBurn:F2}L/lap | YellowBurn: {yellowFuelBurn:F2}L/lap");
                    }
                }

                double fuelDeltaPos = currentPos - tData.LastPosPct;
                if (fuelDeltaPos < -0.5) // Crossed start/finish line
                {
                    fuelDeltaPos = (1.0 - tData.LastPosPct) + currentPos;
                }
                if (fuelDeltaPos < 0.0 || fuelDeltaPos > 0.05)
                {
                    fuelDeltaPos = 0.0;
                }

                double currentBurn = classFuelBurn;
                if (state.Flag_Yellow == 1)
                {
                    currentBurn = yellowFuelBurn;
                    if (state.IsSessionActive && !tData.IsInsideGeofence)
                    {
                        tData.YellowLapsInStint += fuelDeltaPos;
                    }
                }

                // Sottrazione frame-by-frame del consumo se NON siamo confermati ai box
                if (state.IsSessionActive && !tData.IsInsideGeofence)
                {
                    tData.EstimatedFuel -= fuelDeltaPos * currentBurn;
                    if (tData.EstimatedFuel < 0.0) tData.EstimatedFuel = 0.0;
                }

                tData.EstimatedFuelTank = Math.Max(0.0, tData.EstimatedFuel);
                tData.EstimatedPitWindow = currentBurn > 0.0 ? (tData.EstimatedFuelTank / currentBurn) : 99.0;
                tData.EstimatedPitWindowTargetLap = tData.LapCount + tData.EstimatedPitWindow;

                int oppLapCount = tData.HighestLapSeen > 0 ? tData.HighestLapSeen : (opp.CurrentLap ?? 0);
                double oppLapsRemaining = Math.Max(0.0, (raceLapsRemaining > 0 ? raceLapsRemaining : (state.TotalLaps - oppLapCount)));
                if (currentBurn > 0.0 && state.IsRaceSession)
                {
                    // Se l'avversario ha già effettuato un pit stop (PitCount >= 1), non applichiamo il buffer +0.3
                    // per evitare falsi allarmi post-sosta quando ha rifornito al millimetro con smart refuel
                    double safetyMargin = tData.PitCount >= 1 ? 0.0 : 0.3;
                    tData.NeedsPitStop = tData.EstimatedFuel < ((oppLapsRemaining + safetyMargin) * currentBurn);
                }

                double deltaPos = currentPos - tData.LastPosPct;



                int newMicrosector = Math.Max(0, Math.Min(99, (int)(currentPos * 100)));



                if (deltaPos < -0.5) deltaPos += 1.0;

                else if (deltaPos > 0.5) deltaPos -= 1.0;



                double gapToFront = 99.0;

                var idx = sortedOpponents.FindIndex(o => o.Name == opp.Name);

                if (idx > 0)

                {

                    var ahead = sortedOpponents[idx - 1];
                    double aheadPos = GetOpponentTrackPosition(ahead, state);
                    if (aheadPos > 0.0)
                    {
                        double posDiff = aheadPos - currentPos;
                        if (posDiff < 0) posDiff += 1.0;
                        gapToFront = (posDiff * trackLen) / (Math.Max(tData.LastValidSpeedKmh, 20.0) / 3.6);
                    }

                }

                bool isDirtyData = gapToFront < 0.8;

                if (tData.ExtendedSectorRacingZone.IsInside)
                {
                    if (gapToFront < 1.2)
                    {
                        tData.IsProvisionalSectorDirty = true;
                    }
                }
                else
                {
                    tData.IsProvisionalSectorDirty = false;
                }



                // Timestamp: risoluzione fine, indipendente dai microsettori di velocita'.
                int oppTimestampBucket = TimestampBucketOf(currentPos);
                if (oppTimestampBucket != tData.CurrentTimestampBucket)
                {
                    tData.CurrentTimestampBucket = oppTimestampBucket;
                    tData.MicrosectorTimestamps[oppTimestampBucket] = currentSessionClock;
                }

                // Gestione transizione Microsettore (Calcolo Media)

                if (newMicrosector != tData.CurrentMicrosector)

                {

                    if (tData.MicrosectorCount > 0)

                    {

                        double avgSpeed = tData.MicrosectorSumSpeed / tData.MicrosectorCount;

                        int oldSector = tData.CurrentMicrosector;



                        tData.CurrentLapMicrosectorSpeeds[oldSector] = avgSpeed;

                        if (!tData.MicrosectorIsDirty && avgSpeed > tData.MaxSpeedPerMicrosector[oldSector])
                        {
                            tData.MaxSpeedPerMicrosector[oldSector] = avgSpeed;
                        }

                    }



                    tData.CurrentMicrosector = newMicrosector;

                    tData.MicrosectorSumSpeed = 0.0;

                    tData.MicrosectorCount = 0;

                    tData.MicrosectorIsDirty = false;

                }



                double deltaTime = Math.Abs(currentSessionClock - tData.LastTimeSec);



                if (deltaTime > 0.001 && deltaTime < 5.0)

                {

                    double speedKmh = (Math.Abs(deltaPos) * trackLen / deltaTime) * 3.6;

                    if (speedKmh >= 0 && speedKmh <= 360)

                    {

                        tData.LastValidSpeedKmh = speedKmh;
                        if (tData.IsSpatiallyInsideStrict && speedKmh > tData.MaxSpeedInPitThisTransit)
                        {
                            tData.MaxSpeedInPitThisTransit = speedKmh;
                        }

                        if (speedKmh > tData.PersonalTopSpeed) tData.PersonalTopSpeed = speedKmh;

                        if (speedKmh > ClassTopSpeed) ClassTopSpeed = speedKmh;



                        tData.MicrosectorSumSpeed += speedKmh;

                        tData.MicrosectorCount++;

                        if (isDirtyData) tData.MicrosectorIsDirty = true;

                    }

                }



                tData.LastPosPct = currentPos;

                tData.LastTimeSec = currentSessionClock;



                int carIdx = state.Metadata.GetCarIdxFor(opp.Name);
                if (carIdx < 0 && int.TryParse(opp.Id, out int parsedId))
                {
                    carIdx = parsedId;
                }
                tData.CarIdx = carIdx;

                bool wasOnPitRoad = tData.IsOnPitRoad;
                bool isNativeAvailable = IracingBridge.IsAvailable && carIdx >= 0;
                if (isNativeAvailable)
                {
                    bool nativeOnPitRoad = IracingBridge.IsOnPitRoad(carIdx);
                    var nativeTrackSurface = IracingBridge.GetTrackSurface(carIdx);
                    int nativePitCount = IracingBridge.GetPitStopCount(carIdx);
                    int nativeClassPos = IracingBridge.GetClassPosition(carIdx);
                    float nativeLapDistPct = IracingBridge.GetLapDistPct(carIdx);

                    // Gestione NotInWorld: se l'auto era già in pit road e va in NotInWorld (culling di rete di iRacing),
                    // NON usciamo dalla pit road e accumuliamo la durata trascorsa in NotInWorld per dedurre la sosta.
                    bool effectiveOnPitRoad = nativeOnPitRoad;
                    if (nativeTrackSurface == IracingTrackSurface.NotInWorld)
                    {
                        if (wasOnPitRoad)
                        {
                            effectiveOnPitRoad = true;
                            if (tData.NotInWorldStartSec == null)
                            {
                                tData.NotInWorldStartSec = currentSessionClock;
                            }
                        }
                    }
                    else
                    {
                        if (tData.NotInWorldStartSec != null)
                        {
                            tData.NotInWorldDurationSec += Math.Abs(currentSessionClock - tData.NotInWorldStartSec.Value);
                            tData.NotInWorldStartSec = null;
                        }

                        // Se era in pit road e nativeOnPitRoad diventa false, l'uscita avviene solo se la superficie
                        // non è più di pit lane (AproachingPits o InPitStall)
                        if (wasOnPitRoad && !nativeOnPitRoad)
                        {
                            if (nativeTrackSurface == IracingTrackSurface.AproachingPits || nativeTrackSurface == IracingTrackSurface.InPitStall)
                            {
                                effectiveOnPitRoad = true;
                            }
                            else
                            {
                                effectiveOnPitRoad = false;
                            }
                        }
                    }

                    tData.IsOnPitRoad = effectiveOnPitRoad;
                    tData.TrackSurface = nativeTrackSurface;
                    tData.NativePitStopCount = nativePitCount;
                    tData.NativeClassPosition = nativeClassPos;
                    tData.NativeLapDistPct = nativeLapDistPct;

                    // Calibrazione Geofence collegata a CarIdxOnPitRoad
                    if (!wasOnPitRoad && effectiveOnPitRoad)
                    {
                        double entrySample = nativeLapDistPct > 0 ? (double)nativeLapDistPct : currentPos;
                        radar.RecordPitEntrySample(entrySample, CalibrationConfidence.EstimatedOpponent, log);
                    }
                    else if (wasOnPitRoad && !effectiveOnPitRoad)
                    {
                        double exitSample = nativeLapDistPct > 0 ? (double)nativeLapDistPct : currentPos;
                        radar.RecordPitExitSample(exitSample, CalibrationConfidence.EstimatedOpponent, log);
                    }

                    // Sincronizzazione pit stop count ufficiale iRacing
                    if (nativePitCount > tData.PitCount)
                    {
                        tData.PitCount = nativePitCount;
                        tData.LastStopLap = rawCurrentLap;
                    }

                    // Controllo stato InPitStall nativo o fallback per fermata in pit lane
                    bool effectiveInPitStall = (nativeTrackSurface == IracingTrackSurface.InPitStall);

                    if (!effectiveInPitStall && (nativeOnPitRoad || nativeTrackSurface == IracingTrackSurface.AproachingPits))
                    {
                        // Fallback: se in pit lane e la vettura è ferma (< 0.5 km/h) con posizione invariata per almeno 1.0s, è in stallo
                        double currentSpeed = tData.LastValidSpeedKmh;
                        double currentPosPct = nativeLapDistPct > 0 ? (double)nativeLapDistPct : currentPos;

                        if (currentSpeed < 0.5)
                        {
                            if (tData.PitRoadStationaryStartSec == null)
                            {
                                tData.PitRoadStationaryStartSec = currentSessionClock;
                                tData.PitRoadStationaryPosPct = currentPosPct;
                            }
                            else
                            {
                                if (tData.PitRoadStationaryPosPct.HasValue && Math.Abs(currentPosPct - tData.PitRoadStationaryPosPct.Value) > 0.0005)
                                {
                                    // La vettura si è mossa: reset timer
                                    tData.PitRoadStationaryStartSec = currentSessionClock;
                                    tData.PitRoadStationaryPosPct = currentPosPct;
                                }
                                else if (Math.Abs(currentSessionClock - tData.PitRoadStationaryStartSec.Value) >= 1.0)
                                {
                                    effectiveInPitStall = true;
                                    tData.TrackSurface = IracingTrackSurface.InPitStall;
                                }
                            }
                        }
                        else
                        {
                            tData.PitRoadStationaryStartSec = null;
                            tData.PitRoadStationaryPosPct = null;
                        }
                    }
                    else if (!effectiveInPitStall)
                    {
                        tData.PitRoadStationaryStartSec = null;
                        tData.PitRoadStationaryPosPct = null;
                    }

                    // Cronometro InPitStall
                    if (effectiveInPitStall)
                    {
                        if (!tData.WasInPitStall)
                        {
                            tData.WasInPitStall = true;
                            tData.InPitStallStartTimeSec = tData.PitRoadStationaryStartSec ?? currentSessionClock;
                            tData.StopStartTimeSec = tData.InPitStallStartTimeSec;
                            log?.Log(LogModule.OPPONENTS, LogType.EVENT, "Opponent In Pit Stall Started", $"{tData.Name} | Clock: {currentSessionClock:F2}s");
                        }
                        tData.StationaryTimeSec = Math.Abs(currentSessionClock - tData.InPitStallStartTimeSec);
                    }
                    else if (tData.WasInPitStall)
                    {
                        tData.WasInPitStall = false;
                        tData.StationaryTimeSec = Math.Abs(currentSessionClock - tData.InPitStallStartTimeSec);
                        tData.LastPitStationaryTimeSec = tData.StationaryTimeSec;
                        tData.StopStartTimeSec = null;
                        tData.PitRoadStationaryStartSec = null;
                        tData.PitRoadStationaryPosPct = null;
                        log?.Log(LogModule.OPPONENTS, LogType.EVENT, "Opponent In Pit Stall Finished", $"{tData.Name} | StationaryTime: {tData.StationaryTimeSec:F2}s");
                    }
                }

                bool isSpatiallyInsideGeofence = false;

                if (pitEntryPct != -1.0 && pitExitPct != -1.0)
                {
                    if (pitEntryPct < pitExitPct)
                        isSpatiallyInsideGeofence = currentPos >= pitEntryPct && currentPos <= pitExitPct;
                    else
                        isSpatiallyInsideGeofence = currentPos >= pitEntryPct || currentPos <= pitExitPct;
                }

                // Spatial Entry Trigger and Strict Pit lane stopwatch initialization
                bool isRaceNotStartedYet = state.IsRaceSession && !state.RaceStarted;
                if (state.IsSessionActive && isSpatiallyInsideGeofence && !isRaceNotStartedYet && tData.HasExitedPitZoneAtLeastOnce)
                {
                    if (!tData.IsSpatiallyInsideStrict)
                    {
                        double entryDistFromGate = Math.Abs(currentPos - pitEntryPct);
                        if (entryDistFromGate > 0.5) entryDistFromGate = 1.0 - entryDistFromGate;
                        bool isValidEntryGate = entryDistFromGate <= 0.04;

                        if (isValidEntryGate)
                        {
                            tData.IsSpatiallyInsideStrict = true;
                            tData.SpatialStrictEntryTimeSec = currentSessionClock;
                            tData.SpatialStrictEntryLap = rawCurrentLap;
                            tData.EntryTimeSec = currentSessionClock; // Start strict stopwatch immediately
                            tData.StrictPitValidInTransit = false;    // Reset validation flag
                            tData.MaxSpeedInPitThisTransit = 0.0;     // Reset speed limit tracker
                            tData.StationaryTimeSec = 0;
                            tData.StopStartTimeSec = null;
                            tData.HasCountedPitThisTransit = false;
                            log.Log(LogModule.OPPONENTS, LogType.FLOW, "Opponent Spatial Strict Entry", $"{tData.Name} | Pos: {currentPos:F4} | EntryTime: {currentSessionClock:F2}s");
                        }
                    }
                }

                bool isInsideGeofence = false;
                string triggerReason = "";

                if (isNativeAvailable && tData.IsOnPitRoad)
                {
                    isInsideGeofence = true;
                    triggerReason = "iRacing Native (CarIdxOnPitRoad)";
                }
                else if (isNativeAvailable && !tData.IsOnPitRoad)
                {
                    // Telemetria nativa iRacing dichiara esplicitamente che l'auto non è in pit road
                    // (sia essa OnTrack, NotInWorld culled, o OffTrack): il fallback spaziale non deve scavalcare il dato certo!
                    isInsideGeofence = false;
                }
                else if (state.IsSessionActive && isSpatiallyInsideGeofence && !isRaceNotStartedYet && tData.HasExitedPitZoneAtLeastOnce)
                {
                    if (tData.IsInsideGeofence)
                    {
                        isInsideGeofence = true;
                        triggerReason = "Latched (Spatially Inside)";
                    }
                    else if (opp.IsCarInPit)
                    {
                        isInsideGeofence = true;
                        triggerReason = "Telemetry (IsCarInPit)";
                    }
                    else if (tData.LastValidSpeedKmh < 0.5 && tData.TrackSurface != IracingTrackSurface.NotInWorld)
                    {
                        isInsideGeofence = true;
                        triggerReason = "Stopped (Speed < 0.5 km/h)";
                    }
                    else
                    {
                        // Criterio B: persistenza sotto soglia. Y-9: la soglia non è più cablata
                        // a 80, ma derivata dal limite di pit lane appreso per questa classe.
                        // Finché non c'è nulla di imparato il fallback vale esattamente 80.
                        double pitSpeedThreshold = PitLaneDetector.SpeedThresholdFor(
                            radar.GetPitLaneSpeedLimit(tData.CarClass));

                        if (tData.LastValidSpeedKmh < pitSpeedThreshold)
                        {
                            tData.HighSpeedStartSec = null;
                            if (tData.LowSpeedStartSec == null)
                            {
                                tData.LowSpeedStartSec = currentSessionClock;
                            }

                            if (Math.Abs(currentSessionClock - tData.LowSpeedStartSec.Value) >= PitLaneDetector.LowSpeedPersistenceSec)
                            {
                                isInsideGeofence = true;
                                triggerReason = $"SpeedPersistence ({tData.LastValidSpeedKmh:F1} km/h < {pitSpeedThreshold:F0} for {PitLaneDetector.LowSpeedPersistenceSec:F0}s)";
                            }
                        }
                        else
                        {
                            // Debounce spike: se la velocità supera la soglia (jitter di pacchetto replay/rete),
                            // non resettare LowSpeedStartSec immediatamente ma attendi 0.4s di persistenza ad alta velocità
                            if (tData.LowSpeedStartSec != null)
                            {
                                if (tData.HighSpeedStartSec == null)
                                {
                                    tData.HighSpeedStartSec = currentSessionClock;
                                }

                                if (Math.Abs(currentSessionClock - tData.HighSpeedStartSec.Value) >= 0.4)
                                {
                                    tData.LowSpeedStartSec = null;
                                    tData.HighSpeedStartSec = null;
                                }
                            }
                            else
                            {
                                tData.HighSpeedStartSec = null;
                            }
                        }

                        // Criterio C: Paracadute di durata nel geofence.
                        // Non deve MAI scattare se l'auto viaggia a velocità di gara sul rettilineo (es. > 100 km/h o > pitSpeedThreshold + 15).
                        // La soglia di durata deve essere compatibile con un vero transito ai box (almeno 15.0s).
                        if (!isInsideGeofence && tData.LastValidSpeedKmh < (pitSpeedThreshold + 15.0) && tData.LastValidSpeedKmh < 100.0)
                        {
                            double currentPitTime = 0.0;
                            if (tData.PitZone.IsInside)
                            {
                                currentPitTime = tData.PitZone.GetCurrentTime(currentSessionClock);
                            }
                            else if (tData.IsSpatiallyInsideStrict)
                            {
                                currentPitTime = Math.Abs(currentSessionClock - tData.SpatialStrictEntryTimeSec);
                            }

                            double durationThreshold = Math.Max(15.0, (ClassBestPitZoneRacingTime > 0.0 ? ClassBestPitZoneRacingTime * 1.8 : 15.0));

                            if (currentPitTime > durationThreshold)
                            {
                                isInsideGeofence = true;
                                triggerReason = $"Duration ({currentPitTime:F1}s > {durationThreshold:F1}s)";
                            }
                        }
                    }
                }
                else
                {
                    tData.LowSpeedStartSec = null;
                    tData.HighSpeedStartSec = null;
                }

                if (isInsideGeofence && !tData.IsInsideGeofence)
                {
                    tData.IsInsideGeofence = true;
                    tData.StrictPitValidInTransit = true;
                    tData.HasCountedPitThisTransit = false;
                    tData.StationaryTimeSec = 0;
                    tData.StopStartTimeSec = null;
                    log.Log(LogModule.OPPONENTS, LogType.EVENT, "Opponent Geofence Enter (Validated)", $"{tData.Name} | Trigger: {triggerReason} | EntryTimeSec: {tData.EntryTimeSec:F2} | Pos: {currentPos:F4} | Speed: {tData.LastValidSpeedKmh:F1} km/h");

                    if (tData.CarClass == state.CarClassId)
                    {
                        double beforeFuel = tData.EstimatedFuel;
                        tData.EstimatedFuel = Math.Max(0.0, tData.EstimatedFuel - inlapFuelDeduction);
                        tData.EstimatedFuelTank = tData.EstimatedFuel;
                        log.Log(LogModule.SYSTEM, LogType.EVENT, "Opponent Inlap Fuel Deduction",
                            $"{tData.Name} | FuelBefore: {beforeFuel:F2}L | Deduction: {inlapFuelDeduction:F2}L | FuelAfter: {tData.EstimatedFuel:F2}L");

                        // Calcolo FuelToAdd intelligente per arrivare a fine gara (User Step 4376)
                        int currentOppLap = tData.HighestLapSeen > 0 ? tData.HighestLapSeen : (opp.CurrentLap ?? 0);
                        double lapsRemaining = Math.Max(0.0, (raceLapsRemaining > 0 ? raceLapsRemaining : (state.TotalLaps - currentOppLap)));
                        double fuelResiduo = Math.Max(0.0, tData.EstimatedFuel);
                        double safetyLitres = classFuelBurn > 0 ? classFuelBurn * 0.5 : 2.0;
                        double fuelNeeded = (lapsRemaining * classFuelBurn) + safetyLitres;
                        double smartFuelToAdd = Math.Min(classMaxTank - fuelResiduo, Math.Max(0.0, fuelNeeded - fuelResiduo));
                        tData.LastPitFuelAdded = smartFuelToAdd;
                        tData.LastRefuelLap = currentOppLap;
                        tData.FuelAfterLastPit = Math.Min(classMaxTank, fuelResiduo + smartFuelToAdd);
                        tData.EstimatedFuel = tData.FuelAfterLastPit;
                        tData.EstimatedFuelTank = tData.FuelAfterLastPit;
                        if (tData.EstimatedFuel >= lapsRemaining * classFuelBurn)
                        {
                            tData.NeedsPitStop = false;
                        }
                        log.Log(LogModule.OPPONENTS, LogType.EVENT, "Opponent Smart Refuel Projection",
                            $"{tData.Name} | LapsRem: {lapsRemaining:F1} | Burn: {classFuelBurn:F2} | Residuo: {fuelResiduo:F1}L | Needed: {fuelNeeded:F1}L | FuelToAdd: {smartFuelToAdd:F1}L | PostPit: {tData.FuelAfterLastPit:F1}L | NeedsPit: {tData.NeedsPitStop}");

                        if (state.TrackWetnessLevel >= 1)
                        {
                            OpponentPittedInWet = true;
                            LastOpponentPittedInWetName = tData.Name;
                        }
                    }
                }
                else if (isInsideGeofence && tData.IsInsideGeofence)
                {
                    bool isCulledNotInWorld = (tData.TrackSurface == IracingTrackSurface.NotInWorld);
                    if (tData.LastValidSpeedKmh < 0.5 && !isCulledNotInWorld)
                    {
                        if (!tData.HasCountedPitThisTransit)
                        {
                            tData.PitCount++;
                            tData.HasCountedPitThisTransit = true;
                            tData.LastStopLap = rawCurrentLap;
                            log.Log(LogModule.OPPONENTS, LogType.EVENT, "Opponent Stopped", $"{tData.Name} (Pit #{tData.PitCount}) | Lap: {tData.LastStopLap}");
                        }

                        if (tData.StopStartTimeSec == null) tData.StopStartTimeSec = currentSessionClock;
                    }
                    else if (!isCulledNotInWorld)
                    {
                        if (tData.StopStartTimeSec != null)
                        {
                            tData.StationaryTimeSec += Math.Abs(currentSessionClock - tData.StopStartTimeSec.Value);
                            tData.StopStartTimeSec = null;
                            log.Log(LogModule.OPPONENTS, LogType.EVENT, "Opponent Moving", $"{tData.Name} | StatTime: {tData.StationaryTimeSec:F1}s");
                        }
                    }
                }
                else if (!isInsideGeofence && tData.IsInsideGeofence)
                {
                    tData.IsInsideGeofence = false;

                    if (tData.StopStartTimeSec != null)
                    {
                        tData.StationaryTimeSec += Math.Abs(currentSessionClock - tData.StopStartTimeSec.Value);
                        tData.StopStartTimeSec = null;
                    }

                    double totalTransitTime = Math.Abs(currentSessionClock - tData.EntryTimeSec);
                    double adaptiveThreshold = ClassBestPitZoneRacingTime > 0.0
                        ? (ClassBestPitZoneRacingTime + 4.0)
                        : (ClassBestExtendedPitZoneTime > 0.0 ? (ClassBestExtendedPitZoneTime + 5.0) : 15.0);

                    if (tData.StrictPitValidInTransit || totalTransitTime > adaptiveThreshold)
                    {
                        tData.StrictPitValidInTransit = true;
                        tData.ExitTimeSec = currentSessionClock;

                        if (tData.NotInWorldStartSec != null)
                        {
                            tData.NotInWorldDurationSec += Math.Abs(currentSessionClock - tData.NotInWorldStartSec.Value);
                            tData.NotInWorldStartSec = null;
                        }

                        // Reverse-Engineering Stationary Time se l'avversario è stato in NotInWorld
                        if (tData.NotInWorldDurationSec > 0.0)
                        {
                            double refTransit = radar.PitTransitTime > 0.0
                                ? radar.PitTransitTime
                                : radar.GetTheoreticalTransitTimeSec(state.TrackLengthMeters);

                            if (refTransit > 0.0)
                            {
                                double deducedStationary = Math.Max(0.0, tData.NotInWorldDurationSec - refTransit);
                                if (deducedStationary > 0.5)
                                {
                                    tData.StationaryTimeSec = deducedStationary;
                                    tData.LastPitStationaryTimeSec = deducedStationary;
                                    log?.Log(LogModule.OPPONENTS, LogType.EVENT, "Opponent Stationary Time Reverse-Engineered",
                                        $"{tData.Name} | NotInWorld: {tData.NotInWorldDurationSec:F2}s | RefTransit: {refTransit:F2}s | DeducedStationary: {deducedStationary:F2}s");
                                }
                            }
                        }

                        if (!tData.HasCountedPitThisTransit)
                        {
                            tData.PitCount++;
                            tData.HasCountedPitThisTransit = true;
                            tData.LastStopLap = rawCurrentLap;
                            log.Log(LogModule.OPPONENTS, LogType.EVENT, "Opponent Pit Validated", $"{tData.Name} (Pit #{tData.PitCount}) | Lap: {tData.LastStopLap}");
                        }

                        double tTyres = radar.DbTireChangeTime; // default 26.0
                        double fillRate = radar.MeasuredFuelFillRate; // default 2.7
                        if (fillRate <= 0.1) fillRate = 2.7;

                        double targetFuel = classMaxTank;
                        if (state.IsRaceSession && raceLapsRemaining > 0)
                        {
                            targetFuel = Math.Min(classMaxTank, (raceLapsRemaining + 0.3) * effectiveClassFuelBurn);
                        }
                        double predictedFuelToAdd = tData.LastPitFuelAdded > 0.0
                            ? tData.LastPitFuelAdded
                            : Math.Max(0.0, targetFuel - tData.EstimatedFuel);
                        double tFuel = predictedFuelToAdd / fillRate;
                        double tStationary = tData.StationaryTimeSec;
                        const double RefuelOverhead = 2.0;
                        double activeRefuelTime = Math.Max(0.0, tStationary - RefuelOverhead);
                        double MaxReasonableRefuelTime = tFuel + 1.0;
                        double Min2TiresTime = (0.5 * tTyres) - 1.0;
                        double DeltaRemaining = activeRefuelTime - MaxReasonableRefuelTime;

                        bool opponentTiresChanged = false;
                        tData.FuelToAddTime = tFuel;
                        bool isSequential = radar.IsPitLayoutSequential;
                        double maxRegularTime = tFuel + tTyres + 6.0;

                        double refuelTime = activeRefuelTime;

                        if (tStationary > maxRegularTime)
                        {
                            opponentTiresChanged = true;
                            if (isSequential)
                            {
                                refuelTime = Math.Max(0.0, activeRefuelTime - tTyres);
                            }
                            log.Log(LogModule.SYSTEM, LogType.EVENT, "Opponent Pit Stop (Damage Repair)", 
                                $"{tData.Name} | StationaryTime={tStationary:F1}s > MaxRegular={maxRegularTime:F1}s. Assuming tires changed.");
                        }
                        else
                        {
                            radar.TriggerDynamicLayoutDetection(tStationary, tFuel, tTyres, log, tData.Name);
                            isSequential = radar.IsPitLayoutSequential;

                            if (isSequential)
                            {
                                if (DeltaRemaining < Min2TiresTime)
                                {
                                    opponentTiresChanged = false;
                                    refuelTime = Math.Min(activeRefuelTime, MaxReasonableRefuelTime);

                                    log.Log(LogModule.SYSTEM, LogType.EVENT, "Opponent Pit Sequential Squeeze (Capped)",
                                        $"{tData.Name} | StationaryTime={tStationary:F1}s | ActiveRefuel={activeRefuelTime:F1}s | MaxReasonableRefuel={MaxReasonableRefuelTime:F1}s | DeltaRemaining={DeltaRemaining:F1}s < Min2Tires={Min2TiresTime:F1}s | TiresChanged=False");
                                }
                                else
                                {
                                    opponentTiresChanged = true;
                                    double actualTireTime = tTyres;
                                    if (DeltaRemaining < tTyres - 1.0)
                                    {
                                        actualTireTime = 0.5 * tTyres;
                                    }
                                    refuelTime = Math.Max(0.0, activeRefuelTime - actualTireTime);

                                    log.Log(LogModule.SYSTEM, LogType.EVENT, "Opponent Pit Sequential Squeeze (Tires Changed)",
                                        $"{tData.Name} | StationaryTime={tStationary:F1}s | ActiveRefuel={activeRefuelTime:F1}s | TiresTime={actualTireTime:F1}s | RefuelTime={refuelTime:F1}s | TiresChanged=True");
                                }
                            }
                            else
                            {
                                // Simultaneous: rifornimento e gomme avvengono in parallelo.
                                // Il tempo totale alla piazzola è dettato dal maggiore tra il tempo di rifornimento e il cambio gomme.
                                double expectedTotalRefuelTime = tFuel + RefuelOverhead;
                                double minFullTiresTime = Math.Min(tTyres - 3.0, 18.0); // Almeno 18s per cambio 4 gomme in GT3

                                if (tStationary <= expectedTotalRefuelTime + 2.5 && tStationary < minFullTiresTime)
                                {
                                    // La sosta è spiegata interamente dal carburante e la durata è inferiore al tempo necessario per 4 gomme: Fuel Only
                                    opponentTiresChanged = false;
                                    refuelTime = activeRefuelTime;

                                    log.Log(LogModule.SYSTEM, LogType.EVENT, "Opponent Pit Simultaneous (No Tires)",
                                        $"{tData.Name} | StationaryTime={tStationary:F1}s | ExpectedRefuel={expectedTotalRefuelTime:F1}s | TiresChanged=False");
                                }
                                else if (tStationary >= minFullTiresTime && (tStationary >= expectedTotalRefuelTime + 3.0 || tStationary >= tTyres - 2.0))
                                {
                                    // Tempo sufficiente per completare il cambio gomme e superiore al tempo di rifornimento
                                    opponentTiresChanged = true;
                                    refuelTime = Math.Min(activeRefuelTime, expectedTotalRefuelTime);

                                    log.Log(LogModule.SYSTEM, LogType.EVENT, "Opponent Pit Simultaneous (Tires Changed)",
                                        $"{tData.Name} | StationaryTime={tStationary:F1}s | ExpectedRefuel={expectedTotalRefuelTime:F1}s | TiresChanged=True");
                                }
                                else if (tStationary >= minFullTiresTime)
                                {
                                    // Sosta lunga a sufficienza per gomme (>= 18s)
                                    opponentTiresChanged = true;
                                    refuelTime = Math.Min(activeRefuelTime, expectedTotalRefuelTime);

                                    log.Log(LogModule.SYSTEM, LogType.EVENT, "Opponent Pit Simultaneous (Tires Changed)",
                                        $"{tData.Name} | StationaryTime={tStationary:F1}s | ExpectedRefuel={expectedTotalRefuelTime:F1}s | TiresChanged=True");
                                }
                                else
                                {
                                    // Sosta breve (< 18s) e non chiaramente gomme: Fuel Only
                                    opponentTiresChanged = false;
                                    refuelTime = activeRefuelTime;

                                    log.Log(LogModule.SYSTEM, LogType.EVENT, "Opponent Pit Simultaneous (No Tires)",
                                        $"{tData.Name} | StationaryTime={tStationary:F1}s | ExpectedRefuel={expectedTotalRefuelTime:F1}s | TiresChanged=False");
                                }
                            }
                        }
                        double estimatedFuelAdded = Math.Max(0.0, Math.Min(classMaxTank, refuelTime * fillRate));

                        // Aggiorniamo il carburante a bordo e la baseline dopo la sosta se c'è stato rifornimento
                        if (tData.LastPitFuelAdded > 0.0)
                        {
                            // Smart Refueling ha già aggiornato EstimatedFuel e FuelAfterLastPit all'ingresso box
                            if (estimatedFuelAdded > 0.0)
                            {
                                tData.LastPitFuelAdded = estimatedFuelAdded;
                                tData.FuelAfterLastPit = Math.Min(classMaxTank, tData.EstimatedFuel);
                            }
                        }
                        else if (estimatedFuelAdded > 0.0)
                        {
                            tData.LastPitFuelAdded = estimatedFuelAdded;
                            tData.LastRefuelLap = tData.LastStopLap > 0 ? tData.LastStopLap : rawCurrentLap;
                            tData.YellowLapsInStint = 0;
                            tData.FuelAfterLastPit = Math.Min(classMaxTank, tData.EstimatedFuel + estimatedFuelAdded);
                            tData.EstimatedFuel = tData.FuelAfterLastPit;
                            tData.EstimatedFuelTank = tData.FuelAfterLastPit;
                        }

                        int exitOppLap = tData.HighestLapSeen > 0 ? tData.HighestLapSeen : (opp.CurrentLap ?? 0);
                        double exitLapsRem = Math.Max(0.0, (raceLapsRemaining > 0 ? raceLapsRemaining : (state.TotalLaps - exitOppLap)));
                        if (classFuelBurn > 0.0 && state.IsRaceSession)
                        {
                            tData.NeedsPitStop = tData.EstimatedFuel < ((exitLapsRem + 0.3) * classFuelBurn);
                        }

                        tData.LastPitTiresChanged = opponentTiresChanged;
                        tData.LastPitTransitTimeSec = totalTransitTime - tData.StationaryTimeSec;
                        tData.LastPitStationaryTimeSec = tData.StationaryTimeSec;
                        tData.LastOpponentStrictPitLaneTime = totalTransitTime;

                        log.Log(LogModule.SYSTEM, LogType.EVENT, "Opponent Pit Stop Deduction",
                            $"Name: {tData.Name} | TotalTime: {totalTransitTime:F1}s | StatTime: {tData.StationaryTimeSec:F1}s | " +
                            $"EstFuelAdded: {estimatedFuelAdded:F1}L | tFuel: {tFuel:F1}s | tTyres: {tTyres:F1}s | " +
                            $"Layout: {(isSequential ? "Sequential" : "Simultaneous")} | TiresChanged: {opponentTiresChanged}");

                        // Scorporo Pit Loss (User Step 4376: RawExtendedPitZoneTime = TempoMisurato - TempoStationary)
                        double observedExtendedTransit = tData.ExtendedPitZone.LastTransitTime > 0.0
                            ? tData.ExtendedPitZone.LastTransitTime
                            : totalTransitTime;
                        double statDuration = tData.StationaryTimeSec > 0.0 ? tData.StationaryTimeSec : tData.LastPitStationaryTimeSec;
                        double rawExtendedTime = Math.Max(0.0, observedExtendedTransit - statDuration);
                        double refuelDuration = tData.LastPitFuelAdded / fillRate;
                        double empiricalDeadTime = Math.Max(0.0, statDuration - refuelDuration);

                        log.Log(LogModule.OPPONENTS, LogType.EVENT, "Pit Loss Dissection",
                            $"{tData.Name} | ObservedTransit: {observedExtendedTransit:F2}s | Stationary: {statDuration:F2}s | RawExtended: {rawExtendedTime:F2}s | RefuelEst: {refuelDuration:F2}s | JackingDeadTime: {empiricalDeadTime:F2}s");

                        tData.NotInWorldDurationSec = 0.0;
                        tData.NotInWorldStartSec = null;

                        // Apprendimento limite di velocità della pitlane: solo se non è già noto da YAML o Player
                        double maxSpeed = tData.MaxSpeedInPitThisTransit;
                        if (maxSpeed > 30.0 && maxSpeed < 120.0 && radar.GetPitLaneSpeedLimit(tData.CarClass) <= 0.0)
                        {
                            radar.UpdatePitLaneSpeedLimit(maxSpeed, tData.CarClass);
                            log.Log(LogModule.OPPONENTS, LogType.EVENT, "Pit Speed Limit Learned",
                                $"{tData.Name} | Class: {tData.CarClass} | MaxSpeed: {maxSpeed:F1} km/h");
                        }

                        if (opponentTiresChanged)
                        {
                            int lapsOnTyresBeforePit = Math.Max(0, rawCurrentLap - tData.LastPitLap);
                            double distanceOnTyresBeforePit = lapsOnTyresBeforePit * state.TrackLengthMeters;

                            if (lapsOnTyresBeforePit >= 1)
                            {
                                tData.LastStintLaps = lapsOnTyresBeforePit;
                            }

                            if (lapsOnTyresBeforePit >= 1 && distanceOnTyresBeforePit < 40000.0)
                            {
                                // Sosta precoce: le gomme erano gia' fresche, confermiamo subito
                                tData.IsTiresChangedProvisional = false;
                                tData.LastPitLap = tData.LastStopLap > 0 ? tData.LastStopLap : rawCurrentLap;
                                tData.NormalizedTimes.LapBaseline = 0.0;
                                tData.NormalizedTimes.SectorBaseline = 0.0;
                                tData.RawTimes.LapBaseline = 0.0;
                                tData.RawTimes.SectorBaseline = 0.0;
                                tData.NormalizedRaceStartPace = 0.0;
                                tData.ExtendedSectorRacingZone.BestFreshNormalTime = 0.0;
                                tData.ExtendedSectorRacingZone.BestRawTime = 0.0;

                                tData.PrePitNormalizedAverage = tData.RecentPrePitSectors.Count > 0 ? tData.RecentPrePitSectors.Average() : tData.NormalizedTimes.SectorBaseline;
                                tData.PostPitTransitCount = 0;
                                Array.Clear(tData.PostPitNormalizedTimes, 0, 3);
                                for (int i = 0; i < 3; i++) tData.PostPitNormalizedDeltas[i] = 99.0;
                                for (int i = 0; i < 3; i++) tData.PostPitWarmupPenalties[i] = 0.0;

                                log.Log(LogModule.SYSTEM, LogType.EVENT, "Opponent Tires Changed - Baselines Reset (Immediate - Pre-Pit Tires Fresh)",
                                    $"{tData.Name} | Lap: {tData.LastPitLap} | PrePitAvg: {tData.PrePitNormalizedAverage:F3} | PrePitDist: {distanceOnTyresBeforePit:F0}m");
                            }
                            else
                            {
                                // Sosta standard: avviamo la Prova del Nove (Verifica Provvisoria)
                                tData.IsTiresChangedProvisional = true;

                                // 1. Eseguiamo il Backup per eventuale Rollback
                                tData.BackupLastPitLap = tData.LastPitLap;
                                tData.BackupNormalizedRaceStartPace = tData.NormalizedRaceStartPace;
                                tData.BackupLapBaseline = tData.NormalizedTimes.LapBaseline;
                                tData.BackupSectorBaseline = tData.NormalizedTimes.SectorBaseline;
                                tData.BackupRawLapBaseline = tData.RawTimes.LapBaseline;
                                tData.BackupRawSectorBaseline = tData.RawTimes.SectorBaseline;
                                tData.BackupSectorPaceDropDueToTyres = tData.SectorPaceDropDueToTyres;
                                tData.BackupSectorPaceDropDueToTyresRaw = tData.SectorPaceDropDueToTyresRaw;
                                tData.BackupPaceDropDueToTyres = tData.PaceDropDueToTyres;
                                tData.BackupBestFreshNormalTime = tData.ExtendedSectorRacingZone.BestFreshNormalTime;
                                tData.BackupBestRawTime = tData.ExtendedSectorRacingZone.BestRawTime;
                                tData.BackupBestRawTimeLapCount = tData.ExtendedSectorRacingZone.BestRawTimeLapCount;
                                tData.BackupLapPaceDrop = tData.NormalizedTimes.LapPaceDrop;
                                tData.BackupSectorPaceDrop = tData.NormalizedTimes.SectorPaceDrop;
                                tData.BackupRawLapPaceDrop = tData.RawTimes.LapPaceDrop;
                                tData.BackupRawSectorPaceDrop = tData.RawTimes.SectorPaceDrop;

                                // 2. Eseguiamo il Reset Provvisorio delle baseline
                                tData.LastPitLap = tData.LastStopLap > 0 ? tData.LastStopLap : rawCurrentLap;
                                tData.NormalizedTimes.LapBaseline = 0.0;
                                tData.NormalizedTimes.SectorBaseline = 0.0;
                                tData.RawTimes.LapBaseline = 0.0;
                                tData.RawTimes.SectorBaseline = 0.0;
                                tData.NormalizedRaceStartPace = 0.0;
                                tData.ExtendedSectorRacingZone.BestFreshNormalTime = 0.0;
                                tData.ExtendedSectorRacingZone.BestRawTime = 0.0;

                                tData.PrePitNormalizedAverage = tData.RecentPrePitSectors.Count > 0 ? tData.RecentPrePitSectors.Average() : tData.NormalizedTimes.SectorBaseline;
                                tData.PostPitTransitCount = 0;
                                Array.Clear(tData.PostPitNormalizedTimes, 0, 3);
                                for (int i = 0; i < 3; i++) tData.PostPitNormalizedDeltas[i] = 99.0;
                                for (int i = 0; i < 3; i++) tData.PostPitWarmupPenalties[i] = 0.0;

                                log.Log(LogModule.SYSTEM, LogType.EVENT, "Opponent Tires Changed - Baselines Reset (Provisional Started)",
                                    $"{tData.Name} | Lap: {tData.LastPitLap} | PrePitAvg: {tData.PrePitNormalizedAverage:F3} | PrePitDist: {distanceOnTyresBeforePit:F0}m");
                            }
                        }
                        else
                        {
                            log.Log(LogModule.SYSTEM, LogType.EVENT, "Opponent Pit Stop (No Tyre Change) - Baselines Kept", tData.Name);
                        }
                    }
                    else
                    {
                        tData.NotInWorldDurationSec = 0.0;
                        tData.NotInWorldStartSec = null;
                        log.Log(LogModule.SYSTEM, LogType.EVENT, "Opponent Geofence Exit (Discarded)", $"{tData.Name} | Transit not validated.");
                    }
                }

                // Spatial Exit Trigger and Cleanup
                if (!isSpatiallyInsideGeofence)
                {
                    tData.HasExitedPitZoneAtLeastOnce = true;
                    if (tData.IsSpatiallyInsideStrict)
                    {
                        tData.IsSpatiallyInsideStrict = false;
                        if (!tData.StrictPitValidInTransit)
                        {
                            bool isNativeConfirmedOnTrack = isNativeAvailable && !tData.IsOnPitRoad && tData.TrackSurface == IracingTrackSurface.OnTrack;
                            bool isHighSpeedTransit = tData.MaxSpeedInPitThisTransit > 120.0 || (tData.LastValidSpeedKmh > 120.0 && tData.TrackSurface == IracingTrackSurface.OnTrack);

                            if (isNativeConfirmedOnTrack || isHighSpeedTransit)
                            {
                                log.Log(LogModule.OPPONENTS, LogType.FLOW, "Opponent Spatial Transit Discarded (On Track)",
                                    $"{tData.Name} | Confirmed on track (Speed: {tData.MaxSpeedInPitThisTransit:F1} km/h, Surface: {tData.TrackSurface}, NativePitRoad: {tData.IsOnPitRoad}).");
                            }
                            else
                            {
                                double totalSpatialTransitTime = Math.Abs(currentSessionClock - tData.SpatialStrictEntryTimeSec);
                                double minPhysicalPitTime = radar.PitDriveThroughTime > 0.0 ? (radar.PitDriveThroughTime * 0.75) : 18.0;
                                double spatialAdaptiveThreshold = ClassBestPitZoneRacingTime > 0.0
                                    ? Math.Max(minPhysicalPitTime, ClassBestPitZoneRacingTime + 4.0)
                                    : (ClassBestExtendedPitZoneTime > 0.0 ? Math.Max(minPhysicalPitTime, ClassBestExtendedPitZoneTime + 5.0) : minPhysicalPitTime);

                                bool lapDiffValid = (rawCurrentLap >= tData.SpatialStrictEntryLap) && (rawCurrentLap <= tData.SpatialStrictEntryLap + 1);

                                if (totalSpatialTransitTime > spatialAdaptiveThreshold && lapDiffValid)
                                {
                                    log.Log(LogModule.OPPONENTS, LogType.EVENT, "Opponent Spatial Transit Retroactively Validated",
                                        $"{tData.Name} | TotalTransitTime: {totalSpatialTransitTime:F1}s > Threshold: {spatialAdaptiveThreshold:F1}s");

                                    if (!tData.HasCountedPitThisTransit)
                                    {
                                        tData.PitCount++;
                                        tData.HasCountedPitThisTransit = true;
                                        tData.LastStopLap = rawCurrentLap;
                                        log.Log(LogModule.OPPONENTS, LogType.EVENT, "Opponent Stopped", $"{tData.Name} (Pit #{tData.PitCount}) | Lap: {tData.LastStopLap}");
                                    }

                                    tData.LastPitLap = rawCurrentLap;

                                    if (tData.CarClass == state.CarClassId)
                                    {
                                        double beforeFuel = tData.EstimatedFuel;
                                        tData.EstimatedFuel = Math.Max(0.0, tData.EstimatedFuel - inlapFuelDeduction);
                                        int currentOppLap = tData.HighestLapSeen > 0 ? tData.HighestLapSeen : (opp.CurrentLap ?? 0);
                                        double lapsRemaining = Math.Max(0.0, (raceLapsRemaining > 0 ? raceLapsRemaining : (state.TotalLaps - currentOppLap)));
                                        double fuelResiduo = Math.Max(0.0, tData.EstimatedFuel);
                                        double safetyLitres = classFuelBurn > 0 ? classFuelBurn * 0.5 : 2.0;
                                        double fuelNeeded = (lapsRemaining * classFuelBurn) + safetyLitres;
                                        double smartFuelToAdd = Math.Min(classMaxTank - fuelResiduo, Math.Max(0.0, fuelNeeded - fuelResiduo));
                                        tData.LastPitFuelAdded = smartFuelToAdd;
                                        tData.LastRefuelLap = currentOppLap;
                                        tData.FuelAfterLastPit = Math.Min(classMaxTank, fuelResiduo + smartFuelToAdd);
                                        tData.EstimatedFuel = tData.FuelAfterLastPit;
                                        tData.EstimatedFuelTank = tData.FuelAfterLastPit;
                                        if (tData.EstimatedFuel >= lapsRemaining * classFuelBurn)
                                        {
                                            tData.NeedsPitStop = false;
                                        }
                                        log.Log(LogModule.OPPONENTS, LogType.EVENT, "Opponent Smart Refuel Projection (Retroactive)",
                                            $"{tData.Name} | LapsRem: {lapsRemaining:F1} | Burn: {classFuelBurn:F2} | Residuo: {fuelResiduo:F1}L | Needed: {fuelNeeded:F1}L | FuelToAdd: {smartFuelToAdd:F1}L | PostPit: {tData.FuelAfterLastPit:F1}L | NeedsPit: {tData.NeedsPitStop}");
                                    }
                                }
                                else
                                {
                                    log.Log(LogModule.OPPONENTS, LogType.FLOW, "Opponent Spatial Transit Discarded", $"{tData.Name} | Main straight passage ({totalSpatialTransitTime:F1}s <= {spatialAdaptiveThreshold:F1}s).");
                                }
                            }
                        }
                        tData.SpatialStrictEntryTimeSec = 0.0;
                    }
                }

                if (rawCurrentLap != tData.LastLap)
                {
                    // Il primo cambio di giro dopo la comparsa non e' un giro: l'ancoraggio da cui
                    // si misura cadeva a meta' tracciato, nell'istante in cui abbiamo visto la
                    // vettura per la prima volta. Vedi HasWitnessedLapStart.
                    if (CanMeasureLap(tData.LastLap, tData.LastLapStartTimeSec, tData.HasWitnessedLapStart))
                    {
                        // Y-32: il tempo sul giro si **legge dal gioco**, come si e' sempre fatto per
                        // il Player. Il cronometraggio interno resta solo come ripiego.
                        double selfTimedLap = Math.Abs(currentSessionClock - tData.LastLapStartTimeSec);
                        double gameLapTime = opp.LastLapTime.TotalSeconds;
                        double rawLapTime = ResolveOpponentLapTime(gameLapTime, state.TrackLengthMeters);

                        if (rawLapTime > 0.0)
                        {
                            int calculatedLapsSincePit = 0;
                            int calculatedLapsSinceRefuel = 0;

                            if (state.SessionStateStatus >= 4)
                            {
                                calculatedLapsSincePit = (rawCurrentLap - 1) - tData.LastPitLap;
                                if (calculatedLapsSincePit < 0) calculatedLapsSincePit = 0;

                                calculatedLapsSinceRefuel = (rawCurrentLap - 1) - tData.LastRefuelLap;
                                if (calculatedLapsSinceRefuel < 0) calculatedLapsSinceRefuel = 0;
                            }

                            double currentStartingFuel = tData.FuelAfterLastPit > 0 ? tData.FuelAfterLastPit : (opponentStartingFuel > 0 ? opponentStartingFuel : classMaxTank);
                            
                            double greenLaps = calculatedLapsSinceRefuel - tData.YellowLapsInStint;
                            if (greenLaps < 0.0) greenLaps = 0.0;

                            double fuelBurned = (greenLaps * classFuelBurn) + (tData.YellowLapsInStint * yellowFuelBurn);
                            double estimatedFuel = currentStartingFuel - fuelBurned;
                            // inlapFuelDeduction va applicata SOLO nel primo stint (prima della sosta),
                            // MAI dopo che l'auto ha già rifornito e opera su FuelAfterLastPit!
                            if (tData.IsInsideGeofence && tData.FuelAfterLastPit <= 0.0)
                            {
                                estimatedFuel = Math.Max(0.0, estimatedFuel - inlapFuelDeduction);
                            }

                            if (estimatedFuel < 0) estimatedFuel = 0.0;

                            log.Log(LogModule.OPPONENTS, LogType.EVENT, "Opponent Lap Fuel Sync", $"{tData.Name} | Lap: {rawCurrentLap - 1} | StintLaps: {calculatedLapsSinceRefuel} | YellowLaps: {tData.YellowLapsInStint:F2} | GreenLaps: {greenLaps:F2} | exactFuelBurned: {fuelBurned:F2}L | SyncFuel: {estimatedFuel:F2}L");

                            tData.EstimatedFuel = estimatedFuel;
                            tData.EstimatedFuelTank = estimatedFuel;

                            if (tData.CarClass == state.CarClassId)
                            {
                                double lapCurrentBurn = classFuelBurn;
                                if (state.Flag_Yellow == 1)
                                {
                                    lapCurrentBurn = yellowFuelBurn;
                                }
                                tData.EstimatedPitWindow = lapCurrentBurn > 0.0 ? (tData.EstimatedFuelTank / lapCurrentBurn) : 99.0;
                                tData.EstimatedPitWindowTargetLap = tData.LapCount + tData.EstimatedPitWindow;
                            }

                             double fuelPenalty = RaceTimeProjection.FuelWeightPenaltySec(estimatedFuel, fuelWeightCoef);

                             double tempPenalty = state.GlobalBaselineTemp > 0 ? ((state.TrackTemperature - state.GlobalBaselineTemp) * tempCoef) : 0.0;

                             double normalizedOppLap = rawLapTime - fuelPenalty - tempPenalty;



                             tData.RawTimes.LastLapTime = rawLapTime;

                             tData.NormalizedTimes.LastLapTime = normalizedOppLap;


                              bool isValidOppLap = true;

                              if (calculatedLapsSincePit <= 1) isValidOppLap = false;

                              if (tData.IsInsideGeofence) isValidOppLap = false;



                              if (tData.NormalizedRaceStartPace > 0 && isValidOppLap)

                              {

                                  double deviation = normalizedOppLap - tData.NormalizedRaceStartPace;

                                  // Y-49: solo il lato **lento** viene giudicato dall'ancora. Un giro
                                  // piu' veloce non puo' essere giudicato dal numero che deve
                                  // correggere: se lo fosse, un'ancora sbagliata renderebbe invalidi
                                  // proprio i dati che la aggiusterebbero. Il lato veloce lo decide
                                  // UpdatePaceAnchor, che chiede una conferma ai miglioramenti grandi.
                                  if (deviation > (tData.NormalizedRaceStartPace * 0.035))

                                  {

                                      isValidOppLap = false;

                                  }

                              }



                              if (isValidOppLap)

                              {

                                   double distanceOnTyres = calculatedLapsSincePit * state.TrackLengthMeters;

                                   if (distanceOnTyres <= 40000.0)

                                   {

                                       // Y-49: l'ancora del passo. Il lato veloce si decide qui,
                                       // non nella finestra di validita': un giro piu' veloce non
                                       // puo' essere giudicato dal numero che deve correggere.
                                       double anchorValue = tData.NormalizedTimes.LapBaseline;
                                       double anchorPending = tData.PendingPaceAnchor;
                                       double anchorBefore = anchorValue;
                                       bool anchorWasEmpty = anchorValue <= 0.0;

                                       bool anchorAccepts = UpdatePaceAnchor(normalizedOppLap,
                                                                             ref anchorValue,
                                                                             ref anchorPending);

                                       tData.NormalizedTimes.LapBaseline = anchorValue;
                                       tData.PendingPaceAnchor = anchorPending;

                                       if (!anchorAccepts)
                                       {
                                           // Miglioramento grande non ancora confermato: il giro
                                           // resta fuori dalla storia, altrimenti un errore di
                                           // misura inquinerebbe la media senza diventare ancora.
                                           isValidOppLap = false;
                                           log.Log(LogModule.OPPONENTS, LogType.EVENT, "Baseline Improvement Pending",
                                                   $"{tData.Name}: {normalizedOppLap:F3} contro ancora {anchorBefore:F3} | serve un secondo giro che confermi");
                                       }
                                       else if (anchorWasEmpty)
                                       {
                                           // Y-39: da dove arriva il numero. Una baseline sbagliata
                                           // e' diagnosticabile solo sapendo se il tempo l'ha dato
                                           // il gioco o se abbiamo ripiegato sul cronometro interno:
                                           // sono due difetti diversi con due rimedi diversi, e
                                           // dal solo valore finale non si distinguono.
                                           log.Log(LogModule.OPPONENTS, LogType.EVENT, "Baseline Established",
                                                   $"{tData.Name}: {anchorValue:F3} | game={gameLapTime:F3} | self={selfTimedLap:F3} | lap={rawCurrentLap}");
                                       }
                                       else if (anchorValue < anchorBefore)
                                       {
                                           if (anchorBefore - anchorValue > anchorBefore * OrdinaryImprovementFrac)
                                           {
                                               // Salto grande e confermato: i giri raccolti sotto
                                               // l'ancora vecchia erano giudicati da un numero
                                               // sbagliato e non valgono piu'.
                                               tData.NormalizedTimes.LapHistory.Clear();
                                               tData.RawTimes.LapHistory.Clear();
                                               log.Log(LogModule.OPPONENTS, LogType.EVENT, "Baseline Reset (Improvement)",
                                                       $"{tData.Name}: {anchorBefore:F3} -> {anchorValue:F3} | storia azzerata");
                                           }
                                           else
                                           {
                                               log.Log(LogModule.OPPONENTS, LogType.EVENT, "Baseline Updated (Better)",
                                                       $"{tData.Name}: {anchorValue:F3}");
                                           }
                                       }

                                       if (tData.RawTimes.LapBaseline == 0.0)

                                       {

                                           tData.RawTimes.LapBaseline = rawLapTime;

                                       }

                                       else if (rawLapTime < tData.RawTimes.LapBaseline)

                                       {

                                           if (tData.RawTimes.LapBaseline - rawLapTime > 1.5)

                                           {

                                               tData.RawTimes.LapBaseline = rawLapTime;

                                           }

                                           else

                                           {

                                               tData.RawTimes.LapBaseline = rawLapTime;

                                           }

                                       }

                                   }



                                   tData.NormalizedTimes.LapHistory.Add(normalizedOppLap);

                                   tData.RawTimes.LapHistory.Add(rawLapTime);



                                   if (tData.RawTimes.BestLapTime == 0.0 || rawLapTime < tData.RawTimes.BestLapTime)

                                   {

                                       tData.RawTimes.BestLapTime = rawLapTime;

                                       tData.RawTimes.BestLapLapCount = rawCurrentLap;

                                   }

                                   if (tData.NormalizedTimes.BestLapTime == 0.0 || normalizedOppLap < tData.NormalizedTimes.BestLapTime)

                                   {

                                       tData.NormalizedTimes.BestLapTime = normalizedOppLap;

                                       tData.NormalizedTimes.BestLapLapCount = rawCurrentLap;

                                   }



                                   if (tData.NormalizedTimes.LapHistory.Count > 0)

                                   {

                                       tData.NormalizedTimes.LapMovingAverage = tData.NormalizedTimes.LapHistory.Skip(Math.Max(0, tData.NormalizedTimes.LapHistory.Count - 5)).Average();

                                   }

                                   if (tData.RawTimes.LapHistory.Count > 0)

                                   {

                                       tData.RawTimes.LapMovingAverage = tData.RawTimes.LapHistory.Skip(Math.Max(0, tData.RawTimes.LapHistory.Count - 5)).Average();

                                   }



                                   if (tData.NormalizedTimes.LapBaseline > 0.0)

                                   {

                                       tData.NormalizedTimes.LapPaceDrop = Math.Max(0.0, tData.NormalizedTimes.LapMovingAverage - tData.NormalizedTimes.LapBaseline);

                                   }

                                   if (tData.RawTimes.LapBaseline > 0.0)

                                   {

                                       tData.RawTimes.LapPaceDrop = Math.Max(0.0, tData.RawTimes.LapMovingAverage - tData.RawTimes.LapBaseline);

                                       // Fase 0b: quanto il normalizzato corre piu' del misurato, per
                                       // vettura. Le proiezioni usano oggi il normalizzato; questo
                                       // dice di quanto sbagliano prima di cambiare qualcosa.
                                       log.Log(LogModule.OPPONENTS, LogType.FLOW, "Opponent Pace Sources",
                                               $"{tData.Name} | classe={tData.CarClass} | mediaGrezza={tData.RawTimes.LapMovingAverage:F3} | mediaNorm={tData.NormalizedTimes.LapMovingAverage:F3} | delta={tData.RawTimes.LapMovingAverage - tData.NormalizedTimes.LapMovingAverage:F3} | baseGrezza={tData.RawTimes.LapBaseline:F3} | baseNorm={tData.NormalizedTimes.LapBaseline:F3} | ultimoGiro={rawLapTime:F3}");

                                   }



                                   tData.NormalizedRaceStartPace = tData.NormalizedTimes.LapBaseline;

                                   tData.PaceDropDueToTyres = tData.NormalizedTimes.LapPaceDrop;

                                   // Run Phase 2 Microsector processing and Diagnosis
                                   ProcessLapEndMicrosectors(tData, rawLapTime, normalizedOppLap, rawCurrentLap - 1, tData.EstimatedFuel, tData.LastPitTiresChanged, log, state.TrackWetnessLevel, settings);
                                   GenerateGeneralDiagnosis(tData, tData.PaceDropDueToTyres, tData.IsInsideGeofence, state.TrackWetnessLevel, settings);
                                   LogMicrosectorLapEnd(tData, rawLapTime, normalizedOppLap, rawCurrentLap - 1, tData.EstimatedFuel, state.TrackWetnessLevel, log);
                              }

                        }

                    }

                    tData.LastLapStartTimeSec = currentSessionClock;

                    tData.HasWitnessedLapStart = AnchorIsGenuine(tData.LastLap);

                    tData.LastLap = rawCurrentLap;

                }



                if (radar.HasValidCleanSectorBounds() && state.IsSessionActive)
                {
                    double startingFuel = raceStartingFuel > 0 ? raceStartingFuel : classMaxTank;
                    double estimatedFuel = tData.EstimatedFuel > 0 ? tData.EstimatedFuel : startingFuel;
                    int oppLapsOnTyres = Math.Max(0, rawCurrentLap - tData.LastPitLap);

                    if (tData.ExtendedSectorRacingZone.Update(
                        currentPos,
                        currentSessionClock,
                        opp.IsCarInPit,
                        radar.IsInExtendedSectorRacingZone(currentPos),
                        radar.GetExtendedSectorRacingZoneWeight(),
                        estimatedFuel,
                        state.TrackTemperature,
                        state.GlobalBaselineTemp,
                        oppLapsOnTyres,
                        trackLen,
                        tData.LastPitTiresChanged,
                        log,
                        fuelWeightCoef,
                        tempCoef
                    ))
                    {
                        tData.RawTimes.LastSectorTime = tData.ExtendedSectorRacingZone.LastTransitTime;
                        tData.RawTimes.SectorHistory.Add(tData.ExtendedSectorRacingZone.LastTransitTime);
                        tData.NormalizedTimes.LastSectorTime = tData.ExtendedSectorRacingZone.LastNormalizedTime;
                        tData.NormalizedTimes.SectorHistory.Add(tData.ExtendedSectorRacingZone.LastNormalizedTime);

                        tData.RawTimes.BestSectorTime = tData.ExtendedSectorRacingZone.BestRawTime;
                        tData.RawTimes.BestSectorLapCount = tData.ExtendedSectorRacingZone.BestRawTimeLapCount;
                        tData.NormalizedTimes.BestSectorTime = tData.ExtendedSectorRacingZone.BestFreshNormalTime;

                        tData.NormalizedTimes.SectorBaseline = tData.ExtendedSectorRacingZone.BestFreshNormalTime;
                        tData.RawTimes.SectorBaseline = tData.ExtendedSectorRacingZone.BestRawTime;

                        double lastSectorNormalized = tData.ExtendedSectorRacingZone.LastNormalizedTime;
                        if (tData.PostPitTransitCount >= 0 && tData.PostPitTransitCount < 3)
                        {
                            tData.PostPitNormalizedTimes[tData.PostPitTransitCount] = lastSectorNormalized;
                            tData.PostPitNormalizedDeltas[tData.PostPitTransitCount] = lastSectorNormalized - tData.PrePitNormalizedAverage;
                            
                            log.Log(LogModule.SYSTEM, LogType.EVENT, "Opponent Post-Pit Sector Transit",
                                $"{tData.Name} | Index: {tData.PostPitTransitCount} | Time: {lastSectorNormalized:F3}s | PrePitAvg: {tData.PrePitNormalizedAverage:F3}s | Delta: {tData.PostPitNormalizedDeltas[tData.PostPitTransitCount]:F3}s");

                            tData.PostPitTransitCount++;
                        }

                        if (tData.IsTiresChangedProvisional)
                        {
                            bool isCleanSector = (tData.ExtendedSectorRacingZone.CurrentState == "NORMAL") &&
                                                 (!tData.IsProvisionalSectorDirty) &&
                                                 (lastSectorNormalized <= tData.PrePitNormalizedAverage * 1.04);

                            if (isCleanSector)
                            {
                                double delta = tData.PrePitNormalizedAverage - lastSectorNormalized;
                                double paceGainThreshold = tData.PrePitNormalizedAverage * 0.008;

                                log.Log(LogModule.SYSTEM, LogType.EVENT, "Opponent Prova Del Nove Check",
                                    $"{tData.Name} | Clean Transit | Time: {lastSectorNormalized:F3}s | PrePitAvg: {tData.PrePitNormalizedAverage:F3}s | Delta: {delta:F3}s | TargetGain: {paceGainThreshold:F3}s");

                                if (delta >= paceGainThreshold)
                                {
                                    // 1. CONFERMA: Gomme cambiate!
                                    tData.IsTiresChangedProvisional = false;
                                    tData.LastPitTiresChanged = true;
                                    log.Log(LogModule.SYSTEM, LogType.EVENT, "Opponent Tires Confirmed",
                                        $"{tData.Name} | Gain: {delta:F3}s >= Target: {paceGainThreshold:F3}s | Baselines Reset Confirmed");
                                }
                            }
                            else
                            {
                                log.Log(LogModule.SYSTEM, LogType.EVENT, "Opponent Prova Del Nove Ignored (Dirty)",
                                    $"{tData.Name} | State: {tData.ExtendedSectorRacingZone.CurrentState} | ProvisDirty: {tData.IsProvisionalSectorDirty} | Slow: {lastSectorNormalized > tData.PrePitNormalizedAverage * 1.04}");
                            }

                            // Verifica Timeout/Rollback (40 km = 40000m)
                            double distanceSincePit = (rawCurrentLap - tData.LastPitLap) * state.TrackLengthMeters;
                            if (distanceSincePit > 40000.0)
                            {
                                // 2. CONFUTAZIONE: Gomme NON cambiate! Eseguiamo il Rollback
                                tData.IsTiresChangedProvisional = false;
                                tData.LastPitTiresChanged = false;

                                // Ripristiniamo l'eta' delle gomme al valore precedente
                                tData.LastPitLap = tData.BackupLastPitLap;

                                // Ripristiniamo le baseline e le metriche di calo prestazionale dal backup
                                tData.NormalizedRaceStartPace = tData.BackupNormalizedRaceStartPace;
                                tData.NormalizedTimes.LapBaseline = tData.BackupLapBaseline;
                                tData.NormalizedTimes.SectorBaseline = tData.BackupSectorBaseline;
                                tData.RawTimes.LapBaseline = tData.BackupRawLapBaseline;
                                tData.RawTimes.SectorBaseline = tData.BackupRawSectorBaseline;
                                tData.SectorPaceDropDueToTyres = tData.BackupSectorPaceDropDueToTyres;
                                tData.SectorPaceDropDueToTyresRaw = tData.BackupSectorPaceDropDueToTyresRaw;
                                tData.PaceDropDueToTyres = tData.BackupPaceDropDueToTyres;
                                tData.ExtendedSectorRacingZone.BestFreshNormalTime = tData.BackupBestFreshNormalTime;
                                tData.ExtendedSectorRacingZone.BestRawTime = tData.BackupBestRawTime;
                                tData.ExtendedSectorRacingZone.BestRawTimeLapCount = tData.BackupBestRawTimeLapCount;
                                tData.NormalizedTimes.LapPaceDrop = tData.BackupLapPaceDrop;
                                tData.NormalizedTimes.SectorPaceDrop = tData.BackupSectorPaceDrop;
                                tData.RawTimes.LapPaceDrop = tData.BackupRawLapPaceDrop;
                                tData.RawTimes.SectorPaceDrop = tData.BackupRawSectorPaceDrop;

                                log.Log(LogModule.SYSTEM, LogType.EVENT, "Opponent Tires Refuted (Rollback)",
                                    $"{tData.Name} | No pace gain detected within 40km | Baselines Restored to Lap: {tData.LastPitLap}");
                            }
                        }

                        bool isAbnormal = tData.NormalizedTimes.SectorBaseline > 0.0 && 
                                          lastSectorNormalized > tData.NormalizedTimes.SectorBaseline * 1.04;

                        if (!isAbnormal)
                        {
                            tData.RecentPrePitSectors.Add(lastSectorNormalized);
                            if (tData.RecentPrePitSectors.Count > 3)
                            {
                                tData.RecentPrePitSectors.RemoveAt(0);
                            }
                        }
                        else
                        {
                            log.Log(LogModule.SYSTEM, LogType.EVENT, "Opponent Pre-Pit Sector Excluded (Abnormal)",
                                $"{tData.Name} | Time: {lastSectorNormalized:F3}s | Baseline: {tData.NormalizedTimes.SectorBaseline:F3}s (slower than +4%)");
                        }

                        if (tData.ExtendedSectorRacingZone.BestFreshNormalTime > 0.0)
                        {
                            int activeTransits = Math.Max(0, Math.Min(3, tData.PostPitTransitCount >= 0 ? tData.PostPitTransitCount : 0));
                            for (int i = 0; i < activeTransits; i++)
                            {
                                double oldPenalty = tData.PostPitWarmupPenalties[i];
                                tData.PostPitWarmupPenalties[i] = Math.Max(0.0, tData.PostPitNormalizedTimes[i] - tData.ExtendedSectorRacingZone.BestFreshNormalTime);
                                if (Math.Abs(tData.PostPitWarmupPenalties[i] - oldPenalty) > 0.001)
                                {
                                    log.Log(LogModule.SYSTEM, LogType.EVENT, "Opponent Post-Pit Warmup Penalty Update",
                                        $"{tData.Name} | Index: {i} | Time: {tData.PostPitNormalizedTimes[i]:F3}s | BestFresh: {tData.ExtendedSectorRacingZone.BestFreshNormalTime:F3}s | Penalty: {tData.PostPitWarmupPenalties[i]:F3}s");
                                }
                            }
                        }

                        if (tData.NormalizedTimes.SectorHistory.Count > 0)
                        {
                            tData.NormalizedTimes.SectorMovingAverage = tData.NormalizedTimes.SectorHistory.Skip(Math.Max(0, tData.NormalizedTimes.SectorHistory.Count - 5)).Average();
                        }
                        if (tData.RawTimes.SectorHistory.Count > 0)
                        {
                            tData.RawTimes.SectorMovingAverage = tData.RawTimes.SectorHistory.Skip(Math.Max(0, tData.RawTimes.SectorHistory.Count - 5)).Average();
                        }

                        if (tData.NormalizedTimes.SectorBaseline > 0.0)
                        {
                            tData.NormalizedTimes.SectorPaceDrop = Math.Max(0.0, tData.NormalizedTimes.SectorMovingAverage - tData.NormalizedTimes.SectorBaseline);
                        }
                        if (tData.RawTimes.SectorBaseline > 0.0)
                        {
                            tData.RawTimes.SectorPaceDrop = Math.Max(0.0, tData.RawTimes.SectorMovingAverage - tData.RawTimes.SectorBaseline);
                        }

                        tData.SectorPaceDropDueToTyres = tData.NormalizedTimes.SectorPaceDrop;
                        tData.SectorPaceDropDueToTyresRaw = tData.RawTimes.SectorPaceDrop;
                    }

                    tData.PitZone.Update(
                        currentPos,
                        currentSessionClock,
                        false, // isCarInPit - purely raw stopwatch
                        radar.IsInPitLaneZone(currentPos),
                        radar.GetPitZoneWeight(),
                        0.0, // fuel
                        0.0, // trackTemp
                        0.0, // baselineTemp
                        rawCurrentLap,
                        trackLen,
                        false, // tiresChanged
                        log
                    );

                    bool wasInsideExtendedPit = tData.ExtendedPitZone.IsInside;

                    tData.ExtendedPitZone.Update(
                        currentPos,
                        currentSessionClock,
                        false, // isCarInPit
                        radar.IsInExtendedPitLaneZone(currentPos),
                        1.0 - radar.GetExtendedSectorRacingZoneWeight(),
                        0.0, // fuel
                        0.0, // trackTemp
                        0.0, // baselineTemp
                        rawCurrentLap,
                        trackLen,
                        false, // tiresChanged
                        log
                    );

                    if (wasInsideExtendedPit && !tData.ExtendedPitZone.IsInside)
                    {
                        if (tData.LastOpponentStrictPitLaneTime > 0.0)
                        {
                            double accDecTime = tData.ExtendedPitZone.LastTransitTime - tData.LastOpponentStrictPitLaneTime;
                            
                            // Registriamo i dettagli del pit stop per l'avversario/target nel logger per debug e verifica
                            log.Log(LogModule.OPPONENTS, LogType.EVENT, "Opponent Pit AccDec Details", 
                                $"{tData.Name} | ExtendedPitZoneTime={tData.ExtendedPitZone.LastTransitTime:F2}s, StrictPitLaneTime={tData.LastOpponentStrictPitLaneTime:F2}s, InOutPitAccDecTime={accDecTime:F2}s, StationaryTime={tData.LastPitStationaryTimeSec:F2}s, GlobalDbAccDec={radar.PitInOutAccDecTime:F2}s");

                            if (accDecTime > 0.0)
                            {
                                tData.LastPitInOutAccDecTimeSec = accDecTime;
                                if (radar.PitInOutAccDecTime == 0.0)
                                {
                                    radar.UpdatePitInOutAccDecTime(accDecTime);
                                    log.Log(LogModule.OPPONENTS, LogType.EVENT, "AccDec Time Calibrated from Opponent", $"{tData.Name}: {accDecTime:F2}s");
                                }
                            }
                            tData.LastOpponentStrictPitLaneTime = 0.0;
                        }
                    }
                }
                else
                {
                    tData.ExtendedSectorRacingZone.Reset();
                    tData.ExtendedPitZone.Reset();
                }

                if (tData.NormalizedTimes.LapHistory.Count > 0 && tData.NormalizedRaceStartPace > 0)
                {
                    validClassPaceDrops.Add(tData.PaceDropDueToTyres);
                }

                if (tData.NormalizedTimes.SectorHistory.Count > 0 && tData.NormalizedTimes.SectorBaseline > 0.0)
                {
                    validClassSectorPaceDrops.Add(tData.SectorPaceDropDueToTyres);
                }

                if (tData.RawTimes.SectorHistory.Count > 0 && tData.RawTimes.SectorBaseline > 0.0)
                {
                    validClassSectorPaceDropsRaw.Add(tData.SectorPaceDropDueToTyresRaw);
                }
            }

            if (validClassPaceDrops.Count > 0)
            {
                validClassPaceDrops.Sort();
                int mid = validClassPaceDrops.Count / 2;
                if (validClassPaceDrops.Count % 2 == 0)
                    ClassAveragePaceDrop = (validClassPaceDrops[mid - 1] + validClassPaceDrops[mid]) / 2.0;
                else
                    ClassAveragePaceDrop = validClassPaceDrops[mid];
            }
            else
            {
                ClassAveragePaceDrop = 0.0;
            }

            if (validClassSectorPaceDrops.Count > 0)
            {
                validClassSectorPaceDrops.Sort();
                int mid = validClassSectorPaceDrops.Count / 2;
                if (validClassSectorPaceDrops.Count % 2 == 0)
                    ClassAverageSectorPaceDrop = (validClassSectorPaceDrops[mid - 1] + validClassSectorPaceDrops[mid]) / 2.0;
                else
                    ClassAverageSectorPaceDrop = validClassSectorPaceDrops[mid];
            }
            else
            {
                ClassAverageSectorPaceDrop = 0.0;
            }

            if (validClassSectorPaceDropsRaw.Count > 0)
            {
                validClassSectorPaceDropsRaw.Sort();
                int mid = validClassSectorPaceDropsRaw.Count / 2;
                if (validClassSectorPaceDropsRaw.Count % 2 == 0)
                    ClassAverageSectorPaceDropRaw = (validClassSectorPaceDropsRaw[mid - 1] + validClassSectorPaceDropsRaw[mid]) / 2.0;
                else
                    ClassAverageSectorPaceDropRaw = validClassSectorPaceDropsRaw[mid];
            }
            else
            {
                ClassAverageSectorPaceDropRaw = 0.0;
            }

            // Crossover Point slick/wet (Fase 3.3)
            double classBestDryTime = 9999.0;
            double classBestWetTime = 9999.0;
            string myClass = PlayerData.CarClass;

            foreach (var opp in _telemetry.Values)
            {
                if (opp.CarClass == myClass && opp.Name != PlayerData.Name)
                {
                    if (opp.BestNormalizedLapTime > 0.0 && opp.BestNormalizedLapTime < classBestDryTime)
                    {
                        classBestDryTime = opp.BestNormalizedLapTime;
                    }
                    if (opp.BestNormalizedLapTimeWet > 0.0 && opp.BestNormalizedLapTimeWet < classBestWetTime)
                    {
                        classBestWetTime = opp.BestNormalizedLapTimeWet;
                    }
                }
            }

            double playerPace = PlayerData.NormalizedTimes.LastLapTime > 0.0 ? PlayerData.NormalizedTimes.LastLapTime : PlayerData.BestNormalizedLapTime;
            
            CrossoverAlertState = "NONE";
            CrossoverDeltaSeconds = 0.0;

            if (playerPace > 0.0)
            {
                if (state.IsPlayerOnSlick)
                {
                    // Player is on Slicks, track is wet/damp
                    if (state.TrackWetnessLevel >= 1 && classBestWetTime < 9000.0 && playerPace > classBestWetTime + 2.5)
                    {
                        CrossoverAlertState = "BOX_WETS";
                        CrossoverDeltaSeconds = playerPace - classBestWetTime;
                    }
                }
                else
                {
                    // Player is on Wets, track is dry/damp
                    if (state.TrackWetnessLevel <= 1 && classBestDryTime < 9000.0 && playerPace > classBestDryTime + 2.5)
                    {
                        CrossoverAlertState = "BOX_SLICKS";
                        CrossoverDeltaSeconds = playerPace - classBestDryTime;
                    }
                }
            }
        }

        public void ResetSession()

        {

            _telemetry.Clear();

            ClassRaceStartingFuel = 0.0;

            ClassTopSpeed = 0.0;

            ClassAveragePaceDrop = 0.0;
            ClassAverageSectorPaceDrop = 0.0;
            ClassAverageSectorPaceDropRaw = 0.0;

            Array.Clear(PlayerMicrosectorTimestamps, 0, PlayerMicrosectorTimestamps.Length);
            _playerLastMicrosector = -1;
            _playerLastTimestampBucket = -1;
            _playerLastCompletedLaps = -1;

            PlayerData.BestRawLapTime = 0.0;
            PlayerData.BestRawLapTimeWet = 0.0;
            PlayerData.BestNormalizedLapTime = 0.0;
            PlayerData.BestNormalizedLapTimeWet = 0.0;
            PlayerData.WasInsideGeofenceThisLap = false;
            PlayerData.FuelAtBestLap = 0.0;
            PlayerData.FuelAtBestLapWet = 0.0;
            PlayerData.LapStartFuel = 0.0;
            PlayerData.BestMicrosectorSpeedLapCount = 0;
            PlayerData.BestMicrosectorSpeedLapCountWet = 0;
            PlayerData.BestLapAvgSpeedLow = 0.0;
            PlayerData.BestLapAvgSpeedLowWet = 0.0;
            PlayerData.BestLapAvgSpeedMid = 0.0;
            PlayerData.BestLapAvgSpeedMidWet = 0.0;
            PlayerData.BestLapAvgSpeedHigh = 0.0;
            PlayerData.BestLapAvgSpeedHighWet = 0.0;
            PlayerData.Diagnosis = "ANALYZING";
            PlayerData.ZoneDropLow = 0.0;
            PlayerData.ZoneDropMid = 0.0;
            PlayerData.ZoneDropHigh = 0.0;
            Array.Clear(PlayerData.CurrentLapMicrosectorSpeeds, 0, 100);
            Array.Clear(PlayerData.BestLapMicrosectorSpeeds, 0, 100);
            Array.Clear(PlayerData.BestLapMicrosectorSpeedsWet, 0, 100);

            _loggedOpponentNames.Clear();
            _loggedOpponentFuelDetails.Clear();
            _liveFuelAvgLogged = false;
            OpponentPittedInWet = false;
            LastOpponentPittedInWetName = "";
        }

        private void CalculateCategoryAverages(double[] speeds, out double avgLow, out double avgMid, out double avgHigh)
        {
            double sumLow = 0.0, sumMid = 0.0, sumHigh = 0.0;
            int countLow = 0, countMid = 0, countHigh = 0;

            for (int s = 0; s < 100; s++)
            {
                double speed = speeds[s];
                if (speed <= 0.0) continue;

                if (speed < 100.0)
                {
                    sumLow += speed;
                    countLow++;
                }
                else if (speed <= 200.0)
                {
                    sumMid += speed;
                    countMid++;
                }
                else
                {
                    sumHigh += speed;
                    countHigh++;
                }
            }

            avgLow = countLow > 0 ? sumLow / countLow : 0.0;
            avgMid = countMid > 0 ? sumMid / countMid : 0.0;
            avgHigh = countHigh > 0 ? sumHigh / countHigh : 0.0;
        }

        private void ProcessLapEndMicrosectors(OpponentTelemetryData tData, double rawLapTime, double normalizedLapTime, int lapNumber, double currentFuel, bool tyreChanged, LogManager log, int trackWetnessLevel, DataPluginDemoSettings settings)
        {
            if (rawLapTime > 20.0 && rawLapTime < 600.0)
            {
                tData.RawTimes.LastLapTime = rawLapTime;
                tData.NormalizedTimes.LastLapTime = normalizedLapTime;
                tData.LapCount = lapNumber;

                // Determine stint laps (laps completed on current tyre set)
                int stintLaps = Math.Max(0, lapNumber - tData.LastPitLap);

                // 1. Inlap / Outlap / Pit lane suppression
                if (tData.WasInsideGeofenceThisLap || stintLaps == 1)
                {
                    // Only treat as Inlap if it is not the Outlap
                    if (stintLaps != 1 && tData.WasInsideGeofenceThisLap)
                    {
                        tData.LastPitLap = lapNumber;
                        tData.LastPitTiresChanged = tyreChanged;
                    }

                    tData.WasInsideGeofenceThisLap = false;

                    // Suppress speed drops and Best Lap update
                    tData.ZoneDropLow = 0.0;
                    tData.ZoneDropMid = 0.0;
                    tData.ZoneDropHigh = 0.0;

                    // Update LapStartFuel for the next lap
                    tData.LapStartFuel = currentFuel;
                    return;
                }

                // 2. Check for new Best Lap (Normalized-based)
                bool isWetBaselineActive = (trackWetnessLevel >= 2);
                if (isWetBaselineActive)
                {
                    if (tData.BestNormalizedLapTimeWet <= 0.0 || normalizedLapTime < tData.BestNormalizedLapTimeWet)
                    {
                        tData.BestNormalizedLapTimeWet = normalizedLapTime;
                        tData.BestRawLapTimeWet = rawLapTime;
                        tData.BestMicrosectorSpeedLapCountWet = lapNumber;
                        Array.Copy(tData.CurrentLapMicrosectorSpeeds, tData.BestLapMicrosectorSpeedsWet, 100);
                        tData.FuelAtBestLapWet = tData.LapStartFuel;

                        CalculateCategoryAverages(tData.BestLapMicrosectorSpeedsWet, out double avgLW, out double avgMW, out double avgHW);
                        tData.BestLapAvgSpeedLowWet = avgLW;
                        tData.BestLapAvgSpeedMidWet = avgMW;
                        tData.BestLapAvgSpeedHighWet = avgHW;
                    }
                }
                else
                {
                    if (tData.BestNormalizedLapTime <= 0.0 || normalizedLapTime < tData.BestNormalizedLapTime)
                    {
                        tData.BestNormalizedLapTime = normalizedLapTime;
                        tData.BestRawLapTime = rawLapTime;
                        tData.NormalizedTimes.BestLapTime = normalizedLapTime;
                        tData.BestMicrosectorSpeedLapCount = lapNumber;
                        Array.Copy(tData.CurrentLapMicrosectorSpeeds, tData.BestLapMicrosectorSpeeds, 100);
                        tData.FuelAtBestLap = tData.LapStartFuel;

                        CalculateCategoryAverages(tData.BestLapMicrosectorSpeeds, out double avgL, out double avgM, out double avgH);
                        tData.BestLapAvgSpeedLow = avgL;
                        tData.BestLapAvgSpeedMid = avgM;
                        tData.BestLapAvgSpeedHigh = avgH;
                    }
                }

                // 3. Compute current lap category averages
                CalculateCategoryAverages(tData.CurrentLapMicrosectorSpeeds, out double curL, out double curM, out double curH);

                // 4. Fuel weight compensation & 5. Calculate speed drops
                if (isWetBaselineActive)
                {
                    if (tData.FuelAtBestLapWet <= 0.0)
                    {
                        tData.FuelAtBestLapWet = tData.LapStartFuel;
                    }
                    double deltaFuel = tData.FuelAtBestLapWet - currentFuel;
                    double expectedL = tData.BestLapAvgSpeedLowWet + (deltaFuel * settings.FuelSpeedCoef);
                    double expectedM = tData.BestLapAvgSpeedMidWet + (deltaFuel * settings.FuelSpeedCoef);
                    double expectedH = tData.BestLapAvgSpeedHighWet + (deltaFuel * settings.FuelSpeedCoef);

                    tData.ZoneDropLow = tData.BestNormalizedLapTimeWet > 0.0 ? Math.Max(0.0, expectedL - curL) : 0.0;
                    tData.ZoneDropMid = tData.BestNormalizedLapTimeWet > 0.0 ? Math.Max(0.0, expectedM - curM) : 0.0;
                    tData.ZoneDropHigh = tData.BestNormalizedLapTimeWet > 0.0 ? Math.Max(0.0, expectedH - curH) : 0.0;
                }
                else
                {
                    if (tData.FuelAtBestLap <= 0.0)
                    {
                        tData.FuelAtBestLap = tData.LapStartFuel;
                    }
                    double deltaFuel = tData.FuelAtBestLap - currentFuel;
                    double expectedL = tData.BestLapAvgSpeedLow + (deltaFuel * settings.FuelSpeedCoef);
                    double expectedM = tData.BestLapAvgSpeedMid + (deltaFuel * settings.FuelSpeedCoef);
                    double expectedH = tData.BestLapAvgSpeedHigh + (deltaFuel * settings.FuelSpeedCoef);

                    tData.ZoneDropLow = tData.BestNormalizedLapTime > 0.0 ? Math.Max(0.0, expectedL - curL) : 0.0;
                    tData.ZoneDropMid = tData.BestNormalizedLapTime > 0.0 ? Math.Max(0.0, expectedM - curM) : 0.0;
                    tData.ZoneDropHigh = tData.BestNormalizedLapTime > 0.0 ? Math.Max(0.0, expectedH - curH) : 0.0;
                }

                // 6. Update LapStartFuel for the next lap
                tData.LapStartFuel = currentFuel;
            }
        }

        private void GenerateGeneralDiagnosis(OpponentTelemetryData tData, double paceDropSeconds, bool isCurrentlyInPit, int trackWetnessLevel, DataPluginDemoSettings settings)
        {
            bool isWetBaselineActive = (trackWetnessLevel >= 2);
            double refBestLap = isWetBaselineActive ? tData.BestNormalizedLapTimeWet : tData.BestNormalizedLapTime;

            if (refBestLap <= 0.0 || tData.NormalizedTimes.LastLapTime <= 0.0)
            {
                tData.Diagnosis = "ANALYZING";
                return;
            }

            if (isCurrentlyInPit)
            {
                tData.Diagnosis = "TARGET PIT";
                return;
            }

            double timeDelta = tData.NormalizedTimes.LastLapTime - refBestLap;
            
            // Damp Pace offset:
            double effectiveTimeDelta = timeDelta;
            if (trackWetnessLevel == 1)
            {
                effectiveTimeDelta = timeDelta - settings.DampPaceOffsetSeconds;
            }

            // Damp speed drop offset:
            double effectiveThreshold = settings.SpeedDropThresholdKmh;
            if (trackWetnessLevel == 1)
            {
                effectiveThreshold += settings.DampSpeedDropOffsetKmh;
            }

            int stintLaps = Math.Max(0, tData.LapCount - tData.LastPitLap);

            // Outlap
            if (stintLaps == 1)
            {
                tData.Diagnosis = "TARGET OUTLAP";
                return;
            }

            // Tyre warmup phase (first 3 laps post-pit, so stintLaps == 2 or 3)
            if (stintLaps > 1 && stintLaps <= 3 && tData.LastPitTiresChanged)
            {
                if (effectiveTimeDelta <= 0.25 && tData.ZoneDropHigh < effectiveThreshold)
                {
                    tData.Diagnosis = isWetBaselineActive ? "TARGET WET PUSHING" : "TARGET PUSHING";
                }
                else
                {
                    tData.Diagnosis = "TARGET WARMUP";
                }
                return;
            }

            if (effectiveTimeDelta <= 0.25 && tData.ZoneDropHigh < effectiveThreshold)
            {
                tData.Diagnosis = isWetBaselineActive ? "TARGET WET PUSHING" : "TARGET PUSHING";
            }
            else if (effectiveTimeDelta > 0.4)
            {
                if (tData.ZoneDropHigh >= effectiveThreshold && tData.ZoneDropLow < effectiveThreshold)
                {
                    tData.Diagnosis = "TARGET FUEL SAVING";
                }
                else if ((tData.ZoneDropLow >= effectiveThreshold || tData.ZoneDropMid >= effectiveThreshold) && paceDropSeconds >= 0.3)
                {
                    tData.Diagnosis = isWetBaselineActive ? "TARGET WET STEADY" : "TARGET DEG HIGH";
                }
                else
                {
                    tData.Diagnosis = "TARGET CONSERVING";
                }
            }
            else
            {
                tData.Diagnosis = isWetBaselineActive ? "TARGET WET STEADY" : "TARGET STEADY";
            }
        }

        private void LogMicrosectorLapEnd(OpponentTelemetryData tData, double rawLapTime, double normalizedLapTime, int lapNumber, double currentFuel, int trackWetnessLevel, LogManager log)
        {
            if (log == null) return;
            int stintLaps = Math.Max(0, lapNumber - tData.LastPitLap);
            bool isWetBaselineActive = (trackWetnessLevel >= 2);
            double refBest = isWetBaselineActive ? tData.BestNormalizedLapTimeWet : tData.BestNormalizedLapTime;

            if (tData.Diagnosis == "TARGET PIT" || tData.Diagnosis == "TARGET INLAP" || tData.Diagnosis == "TARGET OUTLAP")
            {
                log.Log(LogModule.MICROSECTOR, LogType.EVENT, "Microsector Suppressed Lap",
                    $"{tData.Name} | Lap: {lapNumber} | StintLaps: {stintLaps} | LastTime: {rawLapTime:F3}s | LastTimeNorm: {normalizedLapTime:F3}s | " +
                    $"Diagnosis: {tData.Diagnosis} | Fuel: {currentFuel:F1}L | TyreChanged: {tData.LastPitTiresChanged} | WetnessLevel: {trackWetnessLevel}");
            }
            else
            {
                double bL = isWetBaselineActive ? tData.BestLapAvgSpeedLowWet : tData.BestLapAvgSpeedLow;
                double bM = isWetBaselineActive ? tData.BestLapAvgSpeedMidWet : tData.BestLapAvgSpeedMid;
                double bH = isWetBaselineActive ? tData.BestLapAvgSpeedHighWet : tData.BestLapAvgSpeedHigh;
                double fuelAtB = isWetBaselineActive ? tData.FuelAtBestLapWet : tData.FuelAtBestLap;

                log.Log(LogModule.MICROSECTOR, LogType.EVENT, "Microsector Lap End Analysis",
                    $"{tData.Name} | Lap: {lapNumber} | StintLaps: {stintLaps} | BestLapCount: {(isWetBaselineActive ? tData.BestMicrosectorSpeedLapCountWet : tData.BestMicrosectorSpeedLapCount)} | " +
                    $"LastTime: {rawLapTime:F3}s | LastTimeNorm: {normalizedLapTime:F3}s | BestTimeNorm: {refBest:F3}s | " +
                    $"DropLow: {tData.ZoneDropLow:F1} | DropMid: {tData.ZoneDropMid:F1} | DropHigh: {tData.ZoneDropHigh:F1} | " +
                    $"BestLow: {bL:F1} | BestMid: {bM:F1} | BestHigh: {bH:F1} | " +
                    $"FuelAtBest: {fuelAtB:F1}L | CurrentFuel: {currentFuel:F1}L | Diagnosis: {tData.Diagnosis} | WetnessLevel: {trackWetnessLevel}");
            }
        }

        private static string CleanString(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var chars = new List<char>();
            foreach (char c in s)
            {
                if (char.IsLetterOrDigit(c))
                {
                    chars.Add(char.ToLowerInvariant(c));
                }
            }
            return new string(chars.ToArray());
        }

    }
}