// -------------------------------------------------------------------------
// FILE: PitRadar.cs
// VERSION: Fix errori 43 (Restored and Extended)
// -------------------------------------------------------------------------
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace SimRIG
{
	public enum CalibrationMode
	{
		None,
		SplashAndDash,
		TyreChange,
		DriveThrough,
		StopAndGo
	}

	/// <summary>
	/// Quanto è affidabile un dato calibrato. I valori sono **ordinati per forza crescente**:
	/// la regola di scrittura è sempre "si sovrascrive solo con confidenza maggiore o uguale",
	/// quindi una stima non può mai cancellare una misura.
	/// </summary>
	public enum CalibrationConfidence
	{
		/// <summary>Mai osservato.</summary>
		Unknown = 0,

		/// <summary>Dedotto dagli avversari. Il più debole: nessuna telemetria diretta, solo stime.</summary>
		EstimatedOpponent = 1,

		/// <summary>
		/// Misurato sul Player durante una sosta di gara, ma solo se **pulita**: benzina sola o
		/// gomme sole. In una sosta mista il tempo fermo non si separa fra le due cause.
		/// </summary>
		EstimatedPlayer = 2,

		/// <summary>
		/// Procedura di calibrazione guidata. Isolata per costruzione, quindi è la verità di
		/// riferimento e non viene mai sovrascritta automaticamente.
		/// </summary>
		Confirmed = 3
	}

	public class ClassRecord
	{
		public string CarClass { get; set; }

		public double FuelFillRate { get; set; }

		public double TyreChangeTime { get; set; }

		public CalibrationConfidence FuelFillRateConfidence { get; set; } = CalibrationConfidence.Unknown;

		public CalibrationConfidence TyreChangeTimeConfidence { get; set; } = CalibrationConfidence.Unknown;

		/// <summary>
		/// Quanto dura un cambio di **due** gomme rispetto a quello di quattro, misurato.
		/// Zero = mai calibrato, e allora vale il valore cablato in <c>GetTireMultiplier</c> (0.5).
		///
		/// Il fallback resta apposta: si sovrascrive solo con una misura vera, mai il contrario.
		/// Il valore assunto oggi (esattamente meta') e' ottimistico — il tempo dei martinetti e'
		/// fisso, quindi dimezzare le gomme non dimezza la sosta.
		/// </summary>
		public double TyreChangeMultiplierHalf { get; set; } = 0.0;

		/// <summary>
		/// Come <see cref="TyreChangeMultiplierHalf"/>, per il cambio di **una** gomma sola.
		/// Zero = mai calibrato, vale il cablato (0.25).
		/// </summary>
		public double TyreChangeMultiplierSingle { get; set; } = 0.0;
	}

	public class SimRigDatabase
	{
		public List<TrackRecord> Tracks { get; set; } = new List<TrackRecord>();

		public List<ClassRecord> Classes { get; set; } = new List<ClassRecord>();

		public List<CarRecord> Cars { get; set; } = new List<CarRecord>();
	}

	public class CarRecord
	{
		public string CarModel { get; set; }
		public double BaseCapacity { get; set; }
	}

	public class TrackRecord
	{
		public string TrackClassID { get; set; }

		public string TrackID { get; set; }

		public string CarClass { get; set; }

		public double PitTransitTime { get; set; }

		public double PitDriveThroughTime { get; set; }

		public double PitDistanceMeters { get; set; }

		public double PitInOutAccDecTime { get; set; }

		public bool PlayerRecordSet { get; set; }

		public double PitEntryPct { get; set; } = -1.0;

		public double PitExitPct { get; set; } = -1.0;

		/// <summary>
		/// Confidenza della **coppia** entry+exit. Un solo flag e non due: i due valori nascono
		/// sempre insieme dalla stessa osservazione, sia nel flusso Player (stesso ciclo box) sia
		/// in quello opponent (stesso attraversamento), quindi separarli aggiungerebbe stati
		/// senza un caso d'uso reale.
		/// </summary>
		public CalibrationConfidence GeofenceConfidence { get; set; } = CalibrationConfidence.Unknown;

		/// <summary>
		/// Quante osservazioni concordi stanno dietro alla coppia entry+exit memorizzata.
		///
		/// Persiste il consenso fra una sessione e l'altra. Senza, un circuito dove si fa **una
		/// sosta a gara** non arriverebbe mai a tre osservazioni: il consenso vive in memoria e si
		/// azzera alla chiusura del gioco, quindi ogni sessione ripartirebbe da un campione solo —
		/// cioe' di fatto "l'ultimo che scrive vince". Verificato sui tre replay Misano del
		/// 2026-08-23: la confidenza restava EstimatedPlayer in tutti e tre.
		/// </summary>
		public int GeofenceSampleCount { get; set; } = 0;

		public double ExclusionMargin { get; set; } = 0.05;

		// Nuove proprietà aggiunte per il calcolo dei giri netti rimanenti
		public double AverageStintLaps { get; set; } = 0.0;

		public double AverageLapPace { get; set; } = 0.0;

		public double FuelPerLap { get; set; } = 0.0;

		public double MaxTank { get; set; } = 0.0;

		public double PitLaneSpeedLimit { get; set; } = 0.0;

		public double ExtendedPitEntryPct
		{
			get
			{
				if (!HasValidCleanSectorBounds())
				{
					return -1.0;
				}
				double num = PitEntryPct - ExclusionMargin;
				if (num < 0.0)
				{
					num += 1.0;
				}
				return num;
			}
		}

		public double ExtendedPitExitPct
		{
			get
			{
				if (!HasValidCleanSectorBounds())
				{
					return -1.0;
				}
				double num = PitExitPct + ExclusionMargin;
				if (num >= 1.0)
				{
					num -= 1.0;
				}
				return num;
			}
		}

		public bool HasValidCleanSectorBounds()
		{
			if (PitEntryPct != -1.0)
			{
				return PitExitPct != -1.0;
			}
			return false;
		}

		public bool IsInPitLaneZone(double pos)
		{
			if (!HasValidCleanSectorBounds())
			{
				return false;
			}
			if (PitEntryPct < PitExitPct)
			{
				if (pos >= PitEntryPct)
				{
					return pos <= PitExitPct;
				}
				return false;
			}
			if (!(pos >= PitEntryPct))
			{
				return pos <= PitExitPct;
			}
			return true;
		}

		public bool IsInExtendedPitLaneZone(double pos)
		{
			if (!HasValidCleanSectorBounds())
			{
				return false;
			}
			double num = PitEntryPct - ExclusionMargin;
			if (num < 0.0)
			{
				num += 1.0;
			}
			double num2 = PitExitPct + ExclusionMargin;
			if (num2 >= 1.0)
			{
				num2 -= 1.0;
			}
			if (num < num2)
			{
				if (pos >= num)
				{
					return pos <= num2;
				}
				return false;
			}
			if (!(pos >= num))
			{
				return pos <= num2;
			}
			return true;
		}

		public bool IsInExtendedSectorRacingZone(double pos)
		{
			if (!HasValidCleanSectorBounds())
			{
				return false;
			}
			return !IsInExtendedPitLaneZone(pos);
		}

		public double GetExtendedSectorRacingZoneWeight()
		{
			if (!HasValidCleanSectorBounds())
			{
				return 1.0;
			}
			double num = PitExitPct + ExclusionMargin;
			if (num >= 1.0)
			{
				num -= 1.0;
			}
			double num2 = PitEntryPct - ExclusionMargin;
			if (num2 < 0.0)
			{
				num2 += 1.0;
			}
			double num3 = num2 - num;
			if (num3 <= 0.0)
			{
				num3 += 1.0;
			}
			if (num3 < 0.1)
			{
				num3 = 0.1;
			}
			return num3;
		}
	}

	public class PitRadar
	{

	private SimRigDatabase _database = new SimRigDatabase();

	private string _dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SimRIG_Data.json");

	private TrackRecord _currentTrack;

	private ClassRecord _currentClass;

	public SimRigDatabase Database => _database;

	public TrackRecord CurrentTrack => _currentTrack;

	public ClassRecord CurrentClass => _currentClass;


	private double _fuelLevelAtStopStart;

	private bool? _isSequentialLayoutDetected;

	/// <summary>
	/// Autorizza la scrittura delle geofence (vedi GeofenceCalibrationGate). Prima le due
	/// percentuali si registravano alla prima transizione utile di IsInPitLane, senza sapere
	/// se ci si arrivasse guidando: partendo dai box finiva registrata la posizione del box.
	/// </summary>
	private readonly GeofenceCalibrationGate _geofenceGate = new GeofenceCalibrationGate();

	/// <summary>
	/// Scarto entro cui due osservazioni del limite di pit lane parlano dello stesso limite.
	/// I valori accettati sono 50/60/80/90, cioe' distanti almeno 10: 5 km/h separa senza ambiguita'.
	/// </summary>
	public const double SpeedLimitAgreementKmh = 5.0;

	/// <summary>
	/// Scarto entro cui due osservazioni di una geofence parlano dello stesso punto, in frazione
	/// di giro. 0.01 sono ~42 m a Misano: abbastanza largo da assorbire il jitter fra un tick e
	/// l'altro, abbastanza stretto da non far passare per concordi i due valori storici di
	/// PitExitPct (0.1088 e 0.0737, distanti 0.035 cioe' ~148 m).
	/// </summary>
	public const double GeofenceAgreementPct = 0.01;

	/// <summary>
	/// Percorso minimo, in frazione di giro, perché una permanenza in corsia box sia una
	/// **traversata** e non un artefatto della telemetria.
	///
	/// Lo sfarfallio osservato a Daytona il 2026-08-23 entrava a 0.959 e usciva a 0.963: 0.004 di
	/// giro, una ventina di metri, in 0.7 secondi. 0.01 di giro sono ~42 m a Misano e ~57 m a
	/// Daytona — nessuna corsia box reale è così corta, quindi la soglia non può scartare un
	/// transito vero, e prende comodamente lo 0.004 osservato.
	/// </summary>
	public const double MinimumPitTraversalPct = 0.01;

	/// <summary>Posizione in pista all'ingresso in corsia box, per misurare quanto si è percorso.</summary>
	private double _pitEntryPosition = -1.0;

	// Consenso per sessione. Le geofence si osservano una volta per sosta, il limite di pit lane
	// una volta per sosta di ogni avversario — da cui la separazione per classe.
	/// <summary>
	/// Scarto entro cui due misure di tempo in corsia box parlano della stessa corsia, in secondi.
	/// Misurato: il transito di Misano su tre riproduzioni ha dato 36.017 / 36.017 / 35.933, cioe'
	/// 0.084 s di dispersione. Due secondi sono venti volte quel margine.
	/// </summary>
	public const double PitTimingAgreementSec = 2.0;

	private readonly PlayerPitSpeedObserver _playerPitSpeed = new PlayerPitSpeedObserver();
	private readonly CalibrationConsensus _accDecConsensus = new CalibrationConsensus(PitTimingAgreementSec);
	private readonly CalibrationConsensus _entryConsensus = new CalibrationConsensus(GeofenceAgreementPct);
	private readonly CalibrationConsensus _exitConsensus = new CalibrationConsensus(GeofenceAgreementPct);
	private readonly Dictionary<string, CalibrationConsensus> _speedLimitConsensus =
		new Dictionary<string, CalibrationConsensus>();

	public GeofenceCalibrationGate GeofenceGate => _geofenceGate;

	private bool _playerIsInsideStrictGeofence;

	private double? _playerStrictEntryTime;

	private double _playerStrictBoxTimeCache;

	private double? _playerStrictStopStartTime;

	private bool _playerStrictPitValidInTransit;

	private double? _pitEntryTime;

	private double? _stopStartTime;

	private bool _isFueling;

	private double _fuelStartTime;

	private double _lastFuelLevel;

	private double _lastFuelIncreaseTime;

	private CalibrationMode _activeMode;

	private bool _dtInvalidated;

	private double _pitBoxTimeCache;

	public double MeasuredFuelFillRate
	{
		get
		{
			if (_currentClass == null || !(_currentClass.FuelFillRate > 0.0))
			{
				return 2.7;
			}
			return _currentClass.FuelFillRate;
		}
	}

	public double DbTireChangeTime
	{
		get
		{
			if (_currentClass == null || !(_currentClass.TyreChangeTime > 0.0))
			{
				return 26.0;
			}
			return _currentClass.TyreChangeTime;
		}
	}

	/// <summary>Moltiplicatore misurato per due gomme, 0 se mai calibrato. Vedi Y-28.</summary>
	public double CalibratedTyreMultiplierHalf
	{
		get { return _currentClass != null ? _currentClass.TyreChangeMultiplierHalf : 0.0; }
	}

	/// <summary>Moltiplicatore misurato per una gomma, 0 se mai calibrato.</summary>
	public double CalibratedTyreMultiplierSingle
	{
		get { return _currentClass != null ? _currentClass.TyreChangeMultiplierSingle : 0.0; }
	}

	public bool IsPitLayoutSequential
	{
		get
		{
			if (_isSequentialLayoutDetected.HasValue)
			{
				return _isSequentialLayoutDetected.Value;
			}
			if (string.IsNullOrEmpty(CurrentGameName))
			{
				return true;
			}
			return CurrentGameName.IndexOf("iRacing", StringComparison.OrdinalIgnoreCase) < 0;
		}
	}

	public string PitLayoutMode
	{
		get
		{
			if (!_isSequentialLayoutDetected.HasValue)
			{
				if (string.IsNullOrEmpty(CurrentGameName) || CurrentGameName.IndexOf("iRacing", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					return "CALIBRATING";
				}
				return "SEQUENTIAL";
			}
			if (!_isSequentialLayoutDetected.Value)
			{
				return "SIMULTANEOUS";
			}
			return "SEQUENTIAL";
		}
	}

	public string CurrentGameName { get; private set; }

	public double PitTransitTime
	{
		get
		{
			if (_currentTrack == null)
			{
				return 0.0;
			}
			return _currentTrack.PitTransitTime;
		}
	}

	public double PitDriveThroughTime
	{
		get
		{
			if (_currentTrack == null)
			{
				return 0.0;
			}
			return _currentTrack.PitDriveThroughTime;
		}
	}

	public double PitDistanceMeters
	{
		get
		{
			if (_currentTrack == null)
			{
				return 0.0;
			}
			return _currentTrack.PitDistanceMeters;
		}
	}

	public double PitInOutAccDecTime
	{
		get
		{
			if (_currentTrack == null)
			{
				return 0.0;
			}
			return _currentTrack.PitInOutAccDecTime;
		}
	}

	public bool IsFuelFillRateMissing
	{
		get
		{
			if (_currentClass != null)
			{
				return _currentClass.FuelFillRate == 0.0;
			}
			return false;
		}
	}

	public bool IsTyreChangeTimeMissing
	{
		get
		{
			if (_currentClass != null)
			{
				return _currentClass.TyreChangeTime == 0.0;
			}
			return false;
		}
	}

	public bool IsDriveThroughTimeMissing
	{
		get
		{
			if (_currentTrack != null)
			{
				return _currentTrack.PitDriveThroughTime == 0.0;
			}
			return false;
		}
	}

	public bool IsPitTransitTimeMissing
	{
		get
		{
			if (_currentTrack != null)
			{
				return _currentTrack.PitTransitTime == 0.0;
			}
			return false;
		}
	}

	public string CalibrationStatus { get; private set; } = "ANALYZING";

	/// <summary>Geofence della corsia box calibrate per il circuito corrente.</summary>
	public bool IsGeofenceCalibrated
	{
		get { return _currentTrack != null && _currentTrack.HasValidCleanSectorBounds(); }
	}

	/// <summary>Confidenza delle geofence correnti. Unknown se il circuito non è ancora noto.</summary>
	public CalibrationConfidence GeofenceConfidence
	{
		get { return _currentTrack != null ? _currentTrack.GeofenceConfidence : CalibrationConfidence.Unknown; }
	}

	public CalibrationConfidence FuelFillRateConfidence
	{
		get { return _currentClass != null ? _currentClass.FuelFillRateConfidence : CalibrationConfidence.Unknown; }
	}

	public CalibrationConfidence TyreChangeTimeConfidence
	{
		get { return _currentClass != null ? _currentClass.TyreChangeTimeConfidence : CalibrationConfidence.Unknown; }
	}

	/// <summary>
	/// Elenco leggibile di cosa manca ancora. Vuoto quando tutto è calibrato.
	/// Serve alla dash e all'ingegnere vocale: "READY" da solo non dice cosa fare.
	/// </summary>
	public string CalibrationMissing { get; private set; } = "";

	/// <summary>
	/// Ricalcola lo stato di calibrazione.
	///
	/// La versione precedente guardava solo PitTransitTime e FuelFillRate: un circuito con le
	/// **geofence** non calibrate risultava comunque READY, che è proprio il caso in cui il
	/// rilevamento pit del Player perde colpi. Ora le zone contano, e lo stato distingue un dato
	/// stimato da uno misurato invece di dire solo presente/assente.
	/// </summary>
	private void RefreshCalibrationStatus()
	{
		if (_currentTrack == null || _currentClass == null)
		{
			CalibrationStatus = "ANALYZING";
			CalibrationMissing = "";
			return;
		}

		string missing;
		CalibrationStatus = BuildCalibrationStatus(
			IsGeofenceCalibrated,
			_currentTrack.PitTransitTime,
			_currentClass.FuelFillRate,
			_currentClass.TyreChangeTime,
			_currentTrack.GeofenceConfidence,
			_currentClass.FuelFillRateConfidence,
			_currentClass.TyreChangeTimeConfidence,
			out missing);
		CalibrationMissing = missing;
	}

	/// <summary>
	/// Costruisce stato ed elenco dei mancanti. Statico e senza stato interno, così la logica
	/// che la dash e l'ingegnere vocale leggono è verificabile senza toccare il disco.
	/// </summary>
	public static string BuildCalibrationStatus(bool geofenceCalibrated, double pitTransitTime,
												double fuelFillRate, double tyreChangeTime,
												CalibrationConfidence geofenceConfidence,
												CalibrationConfidence fuelConfidence,
												CalibrationConfidence tyreConfidence,
												out string missing)
	{
		var pending = new List<string>();
		if (!geofenceCalibrated) pending.Add("PIT ZONES");
		if (pitTransitTime == 0.0) pending.Add("PIT TRANSIT");
		if (fuelFillRate == 0.0) pending.Add("FUEL RATE");
		if (tyreChangeTime == 0.0) pending.Add("TYRE TIME");

		missing = string.Join(", ", pending);

		if (pending.Count == 0)
		{
			// Tutto presente: resta da dire se è *misurato* o solo dedotto. Un dato stimato
			// funziona, ma il pilota deve sapere che una calibrazione vera lo migliorerebbe.
			bool anyEstimated =
				geofenceConfidence < CalibrationConfidence.Confirmed
				|| fuelConfidence < CalibrationConfidence.Confirmed
				|| tyreConfidence < CalibrationConfidence.Confirmed;

			return anyEstimated ? "READY (ESTIMATED)" : "READY";
		}

		if (pending.Count >= 3) return "NEEDS FULL CALIBRATION";

		return "NEEDS " + missing;
	}


	public double LastStationaryTime { get; private set; }

	public double LastPlayerStrictPitLaneTime { get; private set; }

	public double LastPlayerStationaryTime { get; private set; }

	public void TriggerDynamicLayoutDetection(double tStationary, double tFuel, double tTyres, LogManager log, string sourceName)
	{
		if (!_isSequentialLayoutDetected.HasValue && !(Math.Min(tFuel, tTyres) < 5.0))
		{
			double num = tFuel + tTyres;
			double num2 = Math.Max(tFuel, tTyres);
			double num3 = Math.Abs(tStationary - num);
			double num4 = Math.Abs(tStationary - num2);
			if (num3 < 4.0 && num3 < num4)
			{
				_isSequentialLayoutDetected = true;
				log.Log(LogModule.SYSTEM, LogType.EVENT, "Pit Layout Detected (Sequential)", $"Source: {sourceName} | Stationary={tStationary:F1}s, tFuel={tFuel:F1}s, tTyres={tTyres:F1}s | ExpectedSeq={num:F1}s, ExpectedSim={num2:F1}s");
			}
			else if (num4 < 4.0 && num4 < num3)
			{
				_isSequentialLayoutDetected = false;
				log.Log(LogModule.SYSTEM, LogType.EVENT, "Pit Layout Detected (Simultaneous)", $"Source: {sourceName} | Stationary={tStationary:F1}s, tFuel={tFuel:F1}s, tTyres={tTyres:F1}s | ExpectedSeq={num:F1}s, ExpectedSim={num2:F1}s");
			}
		}
	}

	/// <summary>
	/// Registra il tempo di accelerazione/decelerazione in ingresso e uscita dai box, osservato
	/// da un avversario.
	///
	/// Osservazione **incidentale**, non procedura guidata: arriva a ogni sosta di ogni avversario,
	/// quindi i campioni sono molti e passa dal consenso. Prima si scriveva il primo valore e
	/// bastava (`if (radar.PitInOutAccDecTime == 0.0)` in `OpponentTracker`), quindi un singolo
	/// transito anomalo restava per sempre — Y-26, trovato da Antigravity in revisione.
	/// </summary>
	public void UpdatePitInOutAccDecTime(double val)
	{
		if (_currentTrack == null || val <= 0.0) return;

		_accDecConsensus.Add(val);

		// Si scrive con il consenso, oppure quando non c'e' ancora nessun valore: su una pista mai
		// vista un dato debole vale piu' di nessun dato. Stesso criterio di PitLaneSpeedLimit.
		bool haveNothingYet = _currentTrack.PitInOutAccDecTime <= 0.0;
		if (!_accDecConsensus.HasConsensus && !haveNothingYet) return;

		double consolidated = _accDecConsensus.Value;
		if (_currentTrack.PitInOutAccDecTime != consolidated)
		{
			_currentTrack.PitInOutAccDecTime = consolidated;
			SaveDatabase();
		}
	}

	public void ResetLastPlayerStrictPitLaneTime()
	{
		LastPlayerStrictPitLaneTime = 0.0;
	}

	public bool HasValidCleanSectorBounds()
	{
		if (_currentTrack != null)
		{
			return _currentTrack.HasValidCleanSectorBounds();
		}
		return false;
	}

	public bool IsInPitLaneZone(double pos)
	{
		if (_currentTrack != null)
		{
			return _currentTrack.IsInPitLaneZone(pos);
		}
		return false;
	}

	/// <summary>
	/// Sotto questa velocita' la vettura e' ferma. Stessa soglia gia' usata dal percorso spaziale
	/// piu' sotto: e' un "fermo o quasi", non una misura di velocita'.
	/// </summary>
	public const double StationarySpeedKmh = 0.5;

	/// <summary>
	/// Che tipo di sosta sta iniziando, in base a cosa e' stato richiesto.
	///
	/// **Perche' non piu' i 20 litri esatti.** La soglia storica era `fuelToAdd == 20.0`, un numero
	/// preso dalla procedura guidata originale. Ma la velocita' di erogazione e' litri diviso
	/// secondi: funziona con 15, con 22, con qualunque quantita' significativa. Pretendere il valore
	/// esatto costringeva l'utente a indovinare un numero che non serve a niente, e faceva fallire
	/// silenziosamente la calibrazione se ne impostava 18.
	///
	/// Cio' che conta davvero e' che la sosta sia **inequivocabile**: solo benzina, o solo gomme.
	/// Su quello non si transige, perche' in una sosta mista il tempo fermo non si separa fra le due
	/// senza conoscerne gia' una (vedi <see cref="ObserveNaturalPitStop"/>).
	///
	/// La soglia minima e' la stessa gia' usata per l'apprendimento passivo
	/// (<see cref="MinLitresForFuelRate"/>): un solo numero condiviso invece di due che potrebbero
	/// divergere.
	/// </summary>
	/// <summary>
	/// Durata minima di una permanenza in corsia box, quando non si sa ancora nulla del circuito.
	///
	/// Rete di sicurezza per il primo transito in assoluto, prima che geofence e limite di velocita'
	/// esistano: li' <see cref="MinimumCredibleTransitSec"/> non puo' calcolare nulla e vale zero.
	/// Un transito vero dura decine di secondi (30.9 s a Daytona, 36.0 s a Misano, misurati),
	/// quindi due secondi non rischiano di scartare un dato buono.
	/// </summary>
	public const double MinimumPitVisitSeconds = 2.0;

	/// <summary>
	/// La permanenza in corsia box e' plausibile, o e' uno sfarfallio della telemetria?
	///
	/// **Due criteri complementari, non ridondanti.** Il flag `IsInPitLane` puo' oscillare
	/// true->false->true; a Daytona, il 2026-08-23, l'ha fatto in 0.2 secondi sul rettilineo
	/// principale, scrivendo un `PitDriveThroughTime` da 0.666 s nel database.
	///
	/// - **Strada percorsa** (Y-23): un guizzo brevissimo non copre distanza.
	/// - **Durata**: un guizzo piu' lungo, ad alta velocita', puo' invece coprirne. A 200 km/h un
	///   secondo vale 55 m, cioe' 0.013 di giro a Misano — sopra la soglia di distanza, che da sola
	///   lo lascerebbe passare. Il criterio temporale lo prende comunque.
	///
	/// Ognuno dei due copre il caso che l'altro si farebbe sfuggire, da cui la scelta di tenerli
	/// entrambi invece di sostituire il primo con il secondo.
	///
	/// <paramref name="derivedFloorSec"/> e' il pavimento calcolato dal circuito quando disponibile
	/// (molto piu' selettivo: ~16 s a Daytona); quando vale zero si ricade su
	/// <see cref="MinimumPitVisitSeconds"/>.
	/// </summary>
	public static bool IsPitVisitPlausible(double entryPosition, double exitPosition,
										   double durationSec, double derivedFloorSec)
	{
		if (!HasTraversedPitLane(entryPosition, exitPosition)) return false;

		double floor = derivedFloorSec > 0.0 ? derivedFloorSec : MinimumPitVisitSeconds;
		return durationSec >= floor;
	}

	/// <summary>
	/// Lo scope riguarda **due** gomme (un asse o un lato)? Le quattro combinazioni sono trattate
	/// come una categoria sola, com'e' gia' oggi in <c>GetTireMultiplier</c>: si misura un
	/// rappresentante, non tutte e quattro separatamente.
	/// </summary>
	public static bool IsHalfSetScope(TyreSelectionScope scope)
	{
		return scope == TyreSelectionScope.Fronts || scope == TyreSelectionScope.Rears
			|| scope == TyreSelectionScope.Left || scope == TyreSelectionScope.Right;
	}

	/// <summary>
	/// Moltiplicatore misurato: quanto dura un cambio parziale rispetto a quello completo.
	///
	/// Esempio dell'utente, che e' anche il test di regressione: 27 s per quattro gomme, 13 s per
	/// due, quindi 13/27 = 0.481 — invece dello 0.5 assunto oggi. La differenza non e' cosmetica:
	/// il tempo dei martinetti e' fisso, quindi dimezzare le gomme non dimezza la sosta, e quel
	/// numero entra dritto nel calcolo del pit loss e quindi nei consigli di undercut.
	///
	/// Restituisce 0 se la misura non e' utilizzabile: senza un riferimento All4 non c'e' rapporto
	/// da calcolare, e un parziale piu' lento dell'intero e' un dato sporco, non una misura.
	/// </summary>
	public static double TyreMultiplierFromMeasurement(double partialSeconds, double all4Seconds)
	{
		if (all4Seconds <= 0.0 || partialSeconds <= 0.0) return 0.0;
		if (partialSeconds >= all4Seconds) return 0.0;

		return partialSeconds / all4Seconds;
	}

	public static CalibrationMode ClassifyCalibrationMode(double fuelToAdd, TyreSelectionScope tyres)
	{
		bool tyresRequested = tyres != TyreSelectionScope.None;

		if (!tyresRequested && fuelToAdd >= MinLitresForFuelRate)
		{
			return CalibrationMode.SplashAndDash;
		}

		// Solo gomme: nessun carburante richiesto. Qualunque scope, non solo All4 — i moltiplicatori
		// per 2 e 1 gomma si misurano con lo stesso meccanismo (vedi Y-28, fase 5).
		if (tyresRequested && fuelToAdd <= 0.0)
		{
			return CalibrationMode.TyreChange;
		}

		return CalibrationMode.DriveThrough;
	}

	/// <summary>
	/// La permanenza in corsia box ha davvero percorso la corsia, o e' un artefatto?
	///
	/// Il criterio e' la **strada percorsa**, non il tempo: un tempo breve puo' essere un
	/// drive-through veloce, uno spostamento quasi nullo no. Usa il delta con il wrap del traguardo
	/// gia' presente in <see cref="TrackPositionValidator.WrappedDelta"/>, cosi' un ingresso a 0.99
	/// e un'uscita a 0.05 contano come 0.06 di giro e non come -0.94.
	///
	/// Posizione d'ingresso non nota (negativa) = nessun giudizio possibile: si accetta, invece di
	/// scartare una sosta vera per mancanza di dati. Stessa scelta del fallback di MaxSectorFraction.
	/// </summary>
	public static bool HasTraversedPitLane(double entryPosition, double exitPosition)
	{
		if (entryPosition < 0.0) return true;

		double traversed = Math.Abs(TrackPositionValidator.WrappedDelta(exitPosition, entryPosition));
		return traversed >= MinimumPitTraversalPct;
	}

	/// <summary>
	/// Frazione del tempo teorico di percorrenza sotto la quale una misura di transito non e'
	/// credibile. Meta': la geofence e' piu' larga della corsia vera e la vettura non e' al limite
	/// per tutto il tratto, quindi il tempo reale puo' scostarsi parecchio da quello teorico —
	/// ma non della meta'.
	/// </summary>
	public const double CredibleTransitFraction = 0.5;

	/// <summary>
	/// Tempo minimo perche' una misura di transito in corsia box sia credibile, in secondi.
	///
	/// Derivato dai dati del circuito invece che da una costante: quanto ci vorrebbe a percorrere
	/// <c>PitDistanceMeters</c> al <c>PitLaneSpeedLimit</c> appreso, meno un margine generoso.
	/// A Daytona (795 m, 90 km/h) sono ~16 s contro un transito reale di 30.9 s; i 0.666 s dello
	/// sfarfallio restano fuori di un fattore 24.
	///
	/// Se distanza o limite non sono ancora noti si restituisce 0, cioe' nessun filtro: su una
	/// pista mai vista un dato debole vale piu' di nessun dato, ed e' comunque protetto dal guard
	/// sulla traversata. Stessa scelta del fallback di MaxSectorFraction.
	/// </summary>
	private double MinimumCredibleTransitSec()
	{
		if (_currentTrack == null) return 0.0;

		double meters = _currentTrack.PitDistanceMeters;
		double limitKmh = _currentTrack.PitLaneSpeedLimit;
		if (meters <= 0.0 || limitKmh <= 0.0) return 0.0;

		double theoretical = meters / (limitKmh / 3.6);
		return theoretical * CredibleTransitFraction;
	}

	/// <summary>
	/// Il Player e' fermo in corsia box?
	///
	/// <c>IsInPitBox</c> da solo non basta: e' il flag grezzo del gioco, che si alza soltanto nello
	/// stallo assegnato mentre il servizio e' in corso. Una sosta in corsia che non sia il proprio
	/// stallo — cedere una posizione a un compagno, scontare una penalita' — non lo fa mai scattare,
	/// e prima veniva contata come tempo fermo pari a zero.
	/// </summary>
	public static bool IsPlayerStationaryInPit(SessionState state)
	{
		if (state == null) return false;
		return state.IsInPitBox || state.SpeedKmh < StationarySpeedKmh;
	}

	public bool IsInExtendedPitLaneZone(double pos)
	{
		if (_currentTrack != null)
		{
			return _currentTrack.IsInExtendedPitLaneZone(pos);
		}
		return false;
	}

	public bool IsInExtendedSectorRacingZone(double pos)
	{
		if (_currentTrack != null)
		{
			return _currentTrack.IsInExtendedSectorRacingZone(pos);
		}
		return false;
	}

	public double GetExtendedSectorRacingZoneWeight()
	{
		if (_currentTrack == null)
		{
			return 1.0;
		}
		return _currentTrack.GetExtendedSectorRacingZoneWeight();
	}

	public double GetPitEntryPctValue()
	{
		if (_currentTrack == null)
		{
			return -1.0;
		}
		return _currentTrack.PitEntryPct;
	}

	public double GetPitExitPctValue()
	{
		if (_currentTrack == null)
		{
			return -1.0;
		}
		return _currentTrack.PitExitPct;
	}

	public double GetExtendedPitEntryPct()
	{
		if (_currentTrack == null)
		{
			return -1.0;
		}
		return _currentTrack.ExtendedPitEntryPct;
	}

	public double GetExtendedPitExitPct()
	{
		if (_currentTrack == null)
		{
			return -1.0;
		}
		return _currentTrack.ExtendedPitExitPct;
	}

	public PitRadar()
	{
		LoadDatabase();
	}

	private void LoadDatabase()
	{
		try
		{
			if (File.Exists(_dbPath))
			{
				string value = File.ReadAllText(_dbPath);
				_database = JsonConvert.DeserializeObject<SimRigDatabase>(value) ?? new SimRigDatabase();
			}
		}
		catch
		{
		}

		if (_database == null)
		{
			_database = new SimRigDatabase();
		}

		MigrateLegacyConfidence();

		if (_database.Cars == null)
		{
			_database.Cars = new List<CarRecord>();
		}

		if (_database.Cars.Count == 0)
		{
			// Pre-populate with iRacing GT3, GTP, and LMP2 default capacities
			_database.Cars.Add(new CarRecord { CarModel = "porsche992gt3r", BaseCapacity = 99.94 });
			_database.Cars.Add(new CarRecord { CarModel = "astonmartinvantagegt3evo", BaseCapacity = 106.0 });
			_database.Cars.Add(new CarRecord { CarModel = "ferrari296gt3", BaseCapacity = 104.0 });
			_database.Cars.Add(new CarRecord { CarModel = "bmwm4gt3", BaseCapacity = 100.0 });
			_database.Cars.Add(new CarRecord { CarModel = "audir8evogt3", BaseCapacity = 121.1 });
			_database.Cars.Add(new CarRecord { CarModel = "audir8lms", BaseCapacity = 120.0 });
			_database.Cars.Add(new CarRecord { CarModel = "lamborghinihuracangt3", BaseCapacity = 120.0 });
			_database.Cars.Add(new CarRecord { CarModel = "mclaren720sgt3", BaseCapacity = 110.0 });
			_database.Cars.Add(new CarRecord { CarModel = "mercedesamggt3", BaseCapacity = 106.0 });
			_database.Cars.Add(new CarRecord { CarModel = "fordmustanggt3", BaseCapacity = 110.0 });
			_database.Cars.Add(new CarRecord { CarModel = "corvettez06gt3", BaseCapacity = 104.0 });

			// GTP / LMDh
			_database.Cars.Add(new CarRecord { CarModel = "bmwmhybridv8", BaseCapacity = 89.0 });
			_database.Cars.Add(new CarRecord { CarModel = "porsche963", BaseCapacity = 89.0 });
			_database.Cars.Add(new CarRecord { CarModel = "cadillacvseriesr", BaseCapacity = 89.0 });
			_database.Cars.Add(new CarRecord { CarModel = "acuraarx06", BaseCapacity = 89.0 });

			// LMP2 / Formula / Indy / Late Model
			_database.Cars.Add(new CarRecord { CarModel = "dallarap217", BaseCapacity = 96.90 });
			_database.Cars.Add(new CarRecord { CarModel = "dallarair18", BaseCapacity = 70.0 });
			_database.Cars.Add(new CarRecord { CarModel = "fiaf4", BaseCapacity = 40.0 });
			_database.Cars.Add(new CarRecord { CarModel = "superlatemodel", BaseCapacity = 83.28 });

			SaveDatabase();
		}
	}

	/// <summary>
	/// Promuove a Confirmed i dati salvati **prima** che esistessero i livelli di confidenza.
	///
	/// Necessaria perché Newtonsoft applica il default della proprietà ai campi assenti dal JSON:
	/// senza questa migrazione un database esistente nascerebbe Unknown, e una stima dedotta dagli
	/// avversari potrebbe sovrascrivere una calibrazione fatta a mano dal pilota. Un dato già
	/// presente è quasi certamente stato misurato dal Player, quindi va trattato come tale.
	///
	/// Idempotente: agisce solo dove il valore c'è ma la confidenza è ancora Unknown.
	/// </summary>
	private void MigrateLegacyConfidence()
	{
		MigrateLegacyConfidence(_database);
	}

	/// <summary>Overload statico, per poter verificare la migrazione senza toccare il disco.</summary>
	public static void MigrateLegacyConfidence(SimRigDatabase database)
	{
		if (database == null) return;

		if (database.Tracks != null)
		{
			foreach (TrackRecord track in database.Tracks)
			{
				if (track.GeofenceConfidence == CalibrationConfidence.Unknown
					&& track.PitEntryPct != -1.0 && track.PitExitPct != -1.0)
				{
					track.GeofenceConfidence = CalibrationConfidence.Confirmed;
				}

				// Un record gia' Confirmed ma senza contatore viene da prima che il consenso
				// esistesse: il valore c'e' ed e' stato misurato dal Player, quindi si considera
				// consolidato invece di degradarlo. Degradarlo lo esporrebbe a essere sovrascritto
				// da un campione singolo — l'opposto di quello che il consenso serve a impedire.
				if (track.GeofenceSampleCount <= 0
					&& track.GeofenceConfidence == CalibrationConfidence.Confirmed
					&& track.PitEntryPct != -1.0 && track.PitExitPct != -1.0)
				{
					track.GeofenceSampleCount = CalibrationConsensus.MinimumForConsensus;
				}
			}
		}

		if (database.Classes != null)
		{
			foreach (ClassRecord cls in database.Classes)
			{
				if (cls.FuelFillRateConfidence == CalibrationConfidence.Unknown && cls.FuelFillRate > 0.0)
				{
					cls.FuelFillRateConfidence = CalibrationConfidence.Confirmed;
				}
				if (cls.TyreChangeTimeConfidence == CalibrationConfidence.Unknown && cls.TyreChangeTime > 0.0)
				{
					cls.TyreChangeTimeConfidence = CalibrationConfidence.Confirmed;
				}
			}
		}
	}

	/// <summary>
	/// La regola unica di scrittura: un dato entra solo se **almeno forte quanto** quello che
	/// sostituisce. L'uguaglianza è ammessa perché una nuova osservazione dello stesso livello
	/// è un aggiornamento legittimo (una calibrazione rifatta, una stima raffinata).
	/// </summary>
	public static bool CanOverwrite(CalibrationConfidence existing, CalibrationConfidence incoming)
	{
		return incoming >= existing;
	}

	/// <summary>Litri minimi perché il rapporto litri/tempo sia significativo.</summary>
	public const double MinLitresForFuelRate = 5.0;

	/// <summary>Durata minima dell'erogazione, per non dividere per un tempo quasi nullo.</summary>
	public const double MinFuelingSeconds = 1.0;

	/// <summary>Sosta minima perché un tempo fermo valga come cambio gomme.</summary>
	public const double MinStationaryForTyres = 5.0;

	/// <summary>Cosa si può imparare da una sosta non guidata.</summary>
	public struct NaturalPitObservation
	{
		public bool FuelRateUsable;
		public double FuelFillRate;

		public bool TyreTimeUsable;
		public double TyreChangeTime;
	}

	/// <summary>
	/// Estrae ciò che una sosta **naturale** può insegnare, senza la procedura guidata.
	///
	/// Serve al pilota che salta la practice e va diretto in gara: la sua prima sosta vera è il
	/// dato migliore disponibile, e prima veniva buttata via perché la scrittura di FuelFillRate
	/// avveniva solo dentro CalibrationMode.SplashAndDash, che scatta solo con una richiesta di
	/// esattamente 20 litri.
	///
	/// Si accettano **solo soste inequivocabili**: benzina sola, o gomme sole. In una sosta mista
	/// il tempo fermo non si separa fra le due cause senza conoscerne già una — è il modello
	/// Sequential/Simultaneous, che merita test propri e resta fuori scope per ora.
	/// </summary>
	public static NaturalPitObservation ObserveNaturalPitStop(double fuelRequested,
															  TyreSelectionScope tyres,
															  double litresAdded,
															  double fuelingSeconds,
															  double stationarySeconds)
	{
		var observation = new NaturalPitObservation();

		bool tyresTouched = tyres != TyreSelectionScope.None;

		// Solo benzina: nessuna gomma toccata, quindi tutto il tempo di erogazione è carburante.
		if (!tyresTouched && litresAdded >= MinLitresForFuelRate && fuelingSeconds >= MinFuelingSeconds)
		{
			observation.FuelRateUsable = true;
			observation.FuelFillRate = litresAdded / fuelingSeconds;
		}

		// Solo gomme: nessun carburante richiesto né erogato, quindi il tempo fermo è tutto gomme.
		if (tyresTouched && fuelRequested <= 0.0 && litresAdded < MinLitresForFuelRate
			&& stationarySeconds >= MinStationaryForTyres)
		{
			observation.TyreTimeUsable = true;
			observation.TyreChangeTime = stationarySeconds;
		}

		return observation;
	}

	public void SaveDatabase()
	{
		try
		{
			string contents = JsonConvert.SerializeObject(_database, Formatting.Indented);
			File.WriteAllText(_dbPath, contents);
		}
		catch
		{
		}
	}

	public double GetPitEntryPct()
	{
		if (_currentTrack != null && _currentTrack.PitEntryPct != -1.0)
		{
			return _currentTrack.PitEntryPct;
		}
		return 0.95;
	}

	public double GetPitExitPct()
	{
		if (_currentTrack != null && _currentTrack.PitExitPct != -1.0)
		{
			return _currentTrack.PitExitPct;
		}
		return 0.05;
	}

	public void Update(SessionState state, double sessionClock, TyreSelectionScope selectedTyres, double fuelToAdd, LogManager log)
	{
		if (string.IsNullOrEmpty(state.CarClassId) || string.IsNullOrEmpty(state.TrackId))
		{
			return;
		}
		CurrentGameName = state.GameName;
		bool flag = false;
		string lookupKey = (state.TrackId + "_" + state.CarClassId).ToUpper();
		if (_currentTrack == null || _currentTrack.TrackClassID != lookupKey)
		{
			// Cambio pista o classe: le osservazioni accumulate parlano di un'altra geofence.
			// Il consenso invece sopravvive al cambio di *sessione* (vedi ResetSession).
			_entryConsensus.Reset();
			_exitConsensus.Reset();

			_currentTrack = _database.Tracks.FirstOrDefault((TrackRecord t) => t.TrackClassID == lookupKey);

			// Si riparte dal consenso gia' raggiunto in passato, invece che da zero: altrimenti un
			// circuito con una sosta a gara non consoliderebbe mai il dato.
			if (_currentTrack != null && _currentTrack.HasValidCleanSectorBounds())
			{
				_entryConsensus.Seed(_currentTrack.PitEntryPct, _currentTrack.GeofenceSampleCount);
				_exitConsensus.Seed(_currentTrack.PitExitPct, _currentTrack.GeofenceSampleCount);
			}
			if (_currentTrack == null)
			{
				_currentTrack = new TrackRecord
				{
					TrackClassID = lookupKey,
					TrackID = state.TrackId,
					CarClass = state.CarClassId
				};
				_database.Tracks.Add(_currentTrack);
				flag = true;
				log.Log(LogModule.RADAR, LogType.EVENT, "New Track-Class Compound Record Created", lookupKey);
			}
		}
		if (_currentClass == null || _currentClass.CarClass != state.CarClassId)
		{
			_currentClass = _database.Classes.FirstOrDefault((ClassRecord c) => c.CarClass == state.CarClassId);
			if (_currentClass == null)
			{
				_currentClass = new ClassRecord
				{
					CarClass = state.CarClassId
				};
				_database.Classes.Add(_currentClass);
				flag = true;
				log.Log(LogModule.RADAR, LogType.EVENT, "New Class DB Created", state.CarClassId);
			}
		}
		if (flag)
		{
			SaveDatabase();
		}
		RefreshCalibrationStatus();

		// Il limite di corsia box si legge dal limitatore del Player, non si deduce. Vale in
		// qualunque sessione e non fa parte della cascata di calibrazione: e' un'osservazione
		// ambientale continua. Vedi PlayerPitSpeedObserver e Y-28.
		if (_playerPitSpeed.Update(state.IsPitLimiterOn, state.SpeedKmh, sessionClock))
		{
			// Arrotondato a 5 km/h: i limiti reali sono numeri tondi, e la lettura oscilla di
			// qualche decimo. Nessuna lista di valori ammessi come nel percorso avversari — qui il
			// dato e' letto, non dedotto, quindi un circuito con un limite inusuale non va scartato.
			double rounded = Math.Round(_playerPitSpeed.ObservedLimitKmh / 5.0) * 5.0;
			UpdatePitLaneSpeedLimit(rounded, state.CarClassId);
			log.Log(LogModule.RADAR, LogType.EVENT, "Pit Speed Limit Observed (Player Limiter)",
				$"{rounded:F0} km/h | misurato={_playerPitSpeed.ObservedLimitKmh:F1} | class={state.CarClassId}");
		}

		// Gate di autorizzazione: valutato a ogni tick, prima di qualunque scrittura di geofence.
		_geofenceGate.Update(state.IsInPitLane, state.TrackPositionPercent, sessionClock, state.TrackLengthMeters);

		if (state.IsInPitLane)
		{
			if (!_pitEntryTime.HasValue)
			{
				_pitEntryTime = sessionClock;
				_pitEntryPosition = state.TrackPositionPercent;
				_pitBoxTimeCache = 0.0;
				_dtInvalidated = false;
				_isFueling = false;
				_lastFuelIncreaseTime = 0.0;
				_fuelStartTime = 0.0;

				// Si registra solo arrivando da un tragitto genuino. Il campione non finisce
				// dritto nel database: entra in un consenso, e a scrivere e' la mediana. Una
				// sosta sola vale EstimatedPlayer — utilizzabile subito su una pista mai vista,
				// ma non abbastanza per scalzare un dato gia' consolidato; da tre soste concordi
				// in poi diventa Confirmed e puo' sostituirlo.
				if (_geofenceGate.CanCalibrateEntry)
				{
					_entryConsensus.Add(state.TrackPositionPercent);

					CalibrationConfidence entryLevel = _entryConsensus.HasConsensus
						? CalibrationConfidence.Confirmed
						: CalibrationConfidence.EstimatedPlayer;

					if (CanOverwrite(_currentTrack.GeofenceConfidence, entryLevel))
					{
						_currentTrack.PitEntryPct = _entryConsensus.Value;
						_currentTrack.GeofenceSampleCount = _entryConsensus.AgreeingCount;
						SaveDatabase();
						log.Log(LogModule.RADAR, LogType.EVENT, "Pit Entry Pct Calibrated",
							$"{_entryConsensus.Value:F3} | confidence={entryLevel} | sample={state.TrackPositionPercent:F3} | agreeing={_entryConsensus.AgreeingCount}/{_entryConsensus.SampleCount}");
					}
					else
					{
						log.Log(LogModule.RADAR, LogType.FLOW, "Pit Entry Pct Held",
							$"sample={state.TrackPositionPercent:F3} | median={_entryConsensus.Value:F3} | agreeing={_entryConsensus.AgreeingCount}/{_entryConsensus.SampleCount} | stored={_currentTrack.PitEntryPct:F3} ({_currentTrack.GeofenceConfidence})");
					}
				}
				else if (_geofenceGate.PitLaneEntered && !_geofenceGate.CanCalibrateEntry)
				{
					// Ingresso non credibile: partenza dai box, teletrasporto, o sessione appena
					// iniziata. Va detto, altrimenti una calibrazione mancante sembra un bug.
					log.Log(LogModule.RADAR, LogType.EVENT, "Pit Entry Pct Calibration Skipped",
						$"pos={state.TrackPositionPercent:F3} | genuineTrackSample={_geofenceGate.HasGenuineTrackSample} | continuity={_geofenceGate.LastContinuity}");
				}
				_activeMode = ClassifyCalibrationMode(fuelToAdd, selectedTyres);
				log.Log(LogModule.RADAR, LogType.EVENT, "Player Pit Entry", $"Mode: {_activeMode}");
			}
			// "Fermo ai box" e' la stessa cosa qui e nel percorso spaziale piu' sotto (riga ~1178),
			// che usa velocita' **oppure** IsInPitBox. Prima qui c'era il solo IsInPitBox, cioe' il
			// flag grezzo del gioco, che si alza solo nello stallo assegnato con il servizio in
			// corso: una sosta in corsia che non sia il proprio stallo — cedere una posizione,
			// scontare uno stop&go — non lo faceva mai scattare. Conseguenze misurate sul replay
			// Daytona del 2026-08-23: StatTime 0.0 s su soste durate 42 e 68 s reali, e soprattutto
			// _fuelLevelAtStopStart mai reinizializzato, da cui un FuelAdded fantasma (15.8 L e
			// 12.7 L "aggiunti" con zero secondi di sosta) letto dalla sosta precedente.
			if (IsPlayerStationaryInPit(state))
			{
				_dtInvalidated = true;
				if (_activeMode == CalibrationMode.DriveThrough)
				{
					_activeMode = CalibrationMode.StopAndGo;
				}
				if (!_stopStartTime.HasValue)
				{
					_stopStartTime = sessionClock;
					_fuelLevelAtStopStart = state.CurrentFuelLevel;
					log.Log(LogModule.RADAR, LogType.EVENT, "Player Stopped in Box", $"Mode: {_activeMode}");
				}
				if (_activeMode == CalibrationMode.SplashAndDash && state.CurrentFuelLevel > _lastFuelLevel + 0.05)
				{
					if (!_isFueling)
					{
						_isFueling = true;
						_fuelStartTime = sessionClock;
					}
					_lastFuelIncreaseTime = sessionClock;
				}
			}
			else if (_stopStartTime.HasValue)
			{
				_pitBoxTimeCache += Math.Abs(sessionClock - _stopStartTime.Value);
				_stopStartTime = null;
			}
			LastStationaryTime = _pitBoxTimeCache + (_stopStartTime.HasValue ? Math.Abs(sessionClock - _stopStartTime.Value) : 0.0);
		}
		else if (_pitEntryTime.HasValue
			&& !IsPitVisitPlausible(_pitEntryPosition, state.TrackPositionPercent,
									Math.Abs(sessionClock - _pitEntryTime.Value),
									MinimumCredibleTransitSec()))
		{
			// Una permanenza che non ha percorso strada non e' una sosta. Il flag IsInPitLane della
			// telemetria puo' sfarfallare (true->false->true in due decimi di secondo): a Daytona,
			// il 2026-08-23, ha prodotto un ingresso a 0.959 e un'uscita a 0.963 mentre il Player
			// era ancora sul rettilineo, e da li' un "Pit Complete" da 0.7 s che ha scritto
			// PitDriveThroughTime = 0.666 nel database.
			//
			// Si scarta l'intera visita invece del singolo campo: lo stesso artefatto alimentava
			// anche i consensi delle geofence e — su una pista vergine, dove PitTransitTime e'
			// ancora a zero — avrebbe scritto un transito da 0.7 s, che entra nella matematica
			// strategica. Vedi IsPitVisitPlausible per il perche' dei due criteri complementari
			// (strada percorsa **e** durata).
			double visitSeconds = Math.Abs(sessionClock - _pitEntryTime.Value);
			double visitFloor = MinimumCredibleTransitSec() > 0.0 ? MinimumCredibleTransitSec() : MinimumPitVisitSeconds;
			log.Log(LogModule.RADAR, LogType.FLOW, "Pit Visit Discarded (Implausible)",
				$"entry={_pitEntryPosition:F4} | exit={state.TrackPositionPercent:F4} | " +
				$"traversed={Math.Abs(TrackPositionValidator.WrappedDelta(state.TrackPositionPercent, _pitEntryPosition)):F4} | " +
				$"minTraversal={MinimumPitTraversalPct:F4} | durata={visitSeconds:F2}s | minDurata={visitFloor:F2}s");

			_pitEntryTime = null;
			_pitEntryPosition = -1.0;
			_stopStartTime = null;
			_pitBoxTimeCache = 0.0;
			_activeMode = CalibrationMode.None;
			_isFueling = false;
			_lastFuelIncreaseTime = 0.0;
			_fuelStartTime = 0.0;
			LastStationaryTime = 0.0;
		}
		else if (_pitEntryTime.HasValue)
		{
			// L'uscita si registra solo se il relativo ingresso era autorizzato: entry ed exit
			// devono venire dallo stesso transito, altrimenti la zona sarebbe composta da due
			// osservazioni scollegate. Qui la coppia si chiude, quindi si dichiara la confidenza.
			if (_geofenceGate.CanCalibrateExit)
			{
				_exitConsensus.Add(state.TrackPositionPercent);

				// Entry ed exit nascono dalla stessa osservazione, quindi la coppia si consolida
				// insieme: il livello e' quello del piu' debole dei due consensi.
				CalibrationConfidence pairLevel =
					(_exitConsensus.HasConsensus && _entryConsensus.HasConsensus)
						? CalibrationConfidence.Confirmed
						: CalibrationConfidence.EstimatedPlayer;

				if (CanOverwrite(_currentTrack.GeofenceConfidence, pairLevel))
				{
					_currentTrack.PitExitPct = _exitConsensus.Value;
					_currentTrack.GeofenceConfidence = pairLevel;

					// Il contatore descrive la **coppia**, come la confidenza: si tiene il piu'
					// debole dei due, altrimenti un lato consolidato coprirebbe l'altro.
					_currentTrack.GeofenceSampleCount =
						Math.Min(_entryConsensus.AgreeingCount, _exitConsensus.AgreeingCount);

					SaveDatabase();
					log.Log(LogModule.RADAR, LogType.EVENT, "Pit Exit Pct Calibrated",
						$"{_exitConsensus.Value:F3} | confidence={pairLevel} | sample={state.TrackPositionPercent:F3} | agreeing={_exitConsensus.AgreeingCount}/{_exitConsensus.SampleCount} | entry={_currentTrack.PitEntryPct:F3}");
				}
				else
				{
					log.Log(LogModule.RADAR, LogType.FLOW, "Pit Exit Pct Held",
						$"sample={state.TrackPositionPercent:F3} | median={_exitConsensus.Value:F3} | agreeing={_exitConsensus.AgreeingCount}/{_exitConsensus.SampleCount} | stored={_currentTrack.PitExitPct:F3} ({_currentTrack.GeofenceConfidence})");
				}
			}
			if (_stopStartTime.HasValue)
			{
				_pitBoxTimeCache += Math.Abs(sessionClock - _stopStartTime.Value);
				_stopStartTime = null;
			}
			double num = Math.Abs(sessionClock - _pitEntryTime.Value);
			double num2 = num - _pitBoxTimeCache;
			if (!HasValidCleanSectorBounds())
			{
				LastPlayerStrictPitLaneTime = num;
			}
			flag = false;
			if (_activeMode == CalibrationMode.SplashAndDash && _isFueling)
			{
				double num3 = Math.Abs(_lastFuelIncreaseTime - _fuelStartTime);
				if (num3 > 0.5)
				{
					double num4 = 20.0 / num3;
					// Procedura guidata: isolata per costruzione, quindi Confirmed.
					if (CanOverwrite(_currentClass.FuelFillRateConfidence, CalibrationConfidence.Confirmed))
					{
						_currentClass.FuelFillRate = num4;
						_currentClass.FuelFillRateConfidence = CalibrationConfidence.Confirmed;
						flag = true;
						log.Log(LogModule.RADAR, LogType.EVENT, "Fuel Fill Rate Calibrated", $"{num4:F2} L/s | confidence=Confirmed");
					}
				}
				// Procedura **guidata**: il pilota ha deliberatamente chiesto questa calibrazione,
				// quindi rifarla deve poter migliorare una misura precedente. Prima c'era
				// `PitTransitTime == 0.0`, che congelava per sempre anche una calibrazione riuscita
				// male (Y-26). Il pavimento di plausibilita' protegge comunque dai valori assurdi.
				if (num2 > MinimumCredibleTransitSec() && num2 > 0.5)
				{
					_currentTrack.PitTransitTime = num2;
					flag = true;
				}
			}
			else if (_activeMode == CalibrationMode.TyreChange)
			{
				// Il tempo di riferimento e' **sempre** quello delle quattro gomme: e' il
				// denominatore da cui si ricavano i moltiplicatori parziali. Una sosta a 2 o 1
				// gomma non lo puo' scrivere, altrimenti il riferimento diventerebbe piu' corto
				// del vero e tutti i moltiplicatori sarebbero sbagliati verso l'alto.
				if (selectedTyres == TyreSelectionScope.All4)
				{
					if (_pitBoxTimeCache > 0.5
						&& CanOverwrite(_currentClass.TyreChangeTimeConfidence, CalibrationConfidence.Confirmed))
					{
						_currentClass.TyreChangeTime = _pitBoxTimeCache;
						_currentClass.TyreChangeTimeConfidence = CalibrationConfidence.Confirmed;
						flag = true;
						log.Log(LogModule.RADAR, LogType.EVENT, "Tyre Change Time Calibrated", $"{_pitBoxTimeCache:F1}s | confidence=Confirmed");
					}
				}
				else if (_pitBoxTimeCache > 0.5 && _currentClass.TyreChangeTime > 0.0)
				{
					// Scope parziale: si misura il **rapporto** rispetto alle quattro gomme, non un
					// tempo assoluto. Serve che All4 sia gia' noto, da cui l'ordine obbligato della
					// cascata (prima All4, poi 2, poi 1).
					double measured = TyreMultiplierFromMeasurement(_pitBoxTimeCache, _currentClass.TyreChangeTime);
					if (measured > 0.0)
					{
						if (IsHalfSetScope(selectedTyres))
						{
							_currentClass.TyreChangeMultiplierHalf = measured;
							flag = true;
							log.Log(LogModule.RADAR, LogType.EVENT, "Tyre Multiplier Calibrated (2 tyres)",
								$"{measured:F3} | {_pitBoxTimeCache:F1}s / {_currentClass.TyreChangeTime:F1}s | scope={selectedTyres}");
						}
						else
						{
							_currentClass.TyreChangeMultiplierSingle = measured;
							flag = true;
							log.Log(LogModule.RADAR, LogType.EVENT, "Tyre Multiplier Calibrated (1 tyre)",
								$"{measured:F3} | {_pitBoxTimeCache:F1}s / {_currentClass.TyreChangeTime:F1}s | scope={selectedTyres}");
						}
					}
				}
				if (num2 > MinimumCredibleTransitSec() && num2 > 0.5)
				{
					_currentTrack.PitTransitTime = num2;
					flag = true;
				}
			}
			else if (_activeMode == CalibrationMode.DriveThrough && !_dtInvalidated)
			{
				// Un drive-through vero dura decine di secondi: si percorre l'intera corsia al
				// limite di velocita'. La soglia storica di 0.5 s non escludeva niente di utile —
				// e' quella che ha lasciato passare i 0.666 s dello sfarfallio di Daytona.
				// Il tempo minimo credibile si ricava dai dati del circuito invece che da una
				// costante: quanto ci vorrebbe a percorrere la corsia al limite, con meta' del
				// valore come margine per l'imprecisione della geofence.
				if (_currentTrack.PitDriveThroughTime == 0.0 && num > MinimumCredibleTransitSec() && num > 0.5)
				{
					// Il test `== 0.0` resta, ma per un motivo diverso dalle due procedure guidate
					// qui sopra: DriveThrough e' la modalita' di **ripiego** — ci si finisce ogni
					// volta che si entra ai box senza la firma di fuel/gomme, quindi anche in gara.
					// Senza il lock, un transito qualunque riscriverebbe il dato di calibrazione.
					// Y-26 resta quindi aperto per questo campo e per il ramo StopAndGo: chiuderlo
					// richiede distinguere una calibrazione guidata da un transito incidentale, che
					// oggi il codice non sa fare — e' una decisione di prodotto, non un fix.
					_currentTrack.PitDriveThroughTime = num;
					flag = true;
					log.Log(LogModule.RADAR, LogType.EVENT, "Drive-Through Time Calibrated", num.ToString("F2"));
				}
			}
			else if (_activeMode == CalibrationMode.StopAndGo && _currentTrack.PitTransitTime == 0.0 && num2 > MinimumCredibleTransitSec() && num2 > 0.5)
			{
				_currentTrack.PitTransitTime = num2;
				flag = true;
				log.Log(LogModule.RADAR, LogType.EVENT, "Transit Time Calibrated", num2.ToString("F2"));
			}
			else
			{
				// Nessuna procedura guidata in corso: e' una sosta normale. Se e' pulita,
				// e' comunque il dato migliore che abbiamo. Prima veniva buttata via, perche'
				// FuelFillRate si scriveva solo dentro SplashAndDash, cioe' a 20 litri esatti.
				double litresAdded = Math.Max(0.0, state.CurrentFuelLevel - _fuelLevelAtStopStart);
				double fuelingSeconds = Math.Abs(_lastFuelIncreaseTime - _fuelStartTime);

				NaturalPitObservation observed = ObserveNaturalPitStop(fuelToAdd, selectedTyres,
									litresAdded, fuelingSeconds, _pitBoxTimeCache);

				if (observed.FuelRateUsable
					&& CanOverwrite(_currentClass.FuelFillRateConfidence, CalibrationConfidence.EstimatedPlayer))
				{
					_currentClass.FuelFillRate = observed.FuelFillRate;
					_currentClass.FuelFillRateConfidence = CalibrationConfidence.EstimatedPlayer;
					flag = true;
					log.Log(LogModule.RADAR, LogType.EVENT, "Fuel Fill Rate Learned (Natural Stop)",
						$"{observed.FuelFillRate:F2} L/s | litres={litresAdded:F1} | seconds={fuelingSeconds:F1} | confidence=EstimatedPlayer");
				}

				if (observed.TyreTimeUsable
					&& CanOverwrite(_currentClass.TyreChangeTimeConfidence, CalibrationConfidence.EstimatedPlayer))
				{
					_currentClass.TyreChangeTime = observed.TyreChangeTime;
					_currentClass.TyreChangeTimeConfidence = CalibrationConfidence.EstimatedPlayer;
					flag = true;
					log.Log(LogModule.RADAR, LogType.EVENT, "Tyre Change Time Learned (Natural Stop)",
						$"{observed.TyreChangeTime:F1}s | confidence=EstimatedPlayer");
				}
			}
			if (flag)
			{
				SaveDatabase();
			}
			double num5 = Math.Max(0.0, state.CurrentFuelLevel - _fuelLevelAtStopStart);
			double num6 = num5 / MeasuredFuelFillRate;
			double pitBoxTimeCache = _pitBoxTimeCache;
			double dbTireChangeTime = DbTireChangeTime;
			double num7 = num6 + dbTireChangeTime + 6.0;
			if (selectedTyres != 0 && pitBoxTimeCache <= num7)
			{
				TriggerDynamicLayoutDetection(pitBoxTimeCache, num6, dbTireChangeTime, log, "Player");
			}
			log.Log(LogModule.RADAR, LogType.EVENT, "Pit Complete", $"TotalTime: {num:F1}s | StatTime: {_pitBoxTimeCache:F1}s | Mode: {_activeMode} | FuelAdded: {num5:F1}L | tFuel: {num6:F1}s");
			if (!HasValidCleanSectorBounds())
			{
				LastPlayerStrictPitLaneTime = num;
				LastPlayerStationaryTime = _pitBoxTimeCache;
			}
			_pitEntryTime = null;
			LastStationaryTime = 0.0;
		}
		_lastFuelLevel = state.CurrentFuelLevel;
		if (_currentTrack != null && _currentTrack.PitEntryPct != -1.0 && _currentTrack.PitExitPct != -1.0)
		{
			double num8 = 0.0;
			num8 = ((!(_currentTrack.PitEntryPct > _currentTrack.PitExitPct)) ? ((_currentTrack.PitExitPct - _currentTrack.PitEntryPct) * state.TrackLengthMeters) : ((1.0 - _currentTrack.PitEntryPct + _currentTrack.PitExitPct) * state.TrackLengthMeters));
			_currentTrack.PitDistanceMeters = num8;
		}
		if (HasValidCleanSectorBounds())
		{
			if (state.TrackPositionPercent == 0.0)
			{
				_playerIsInsideStrictGeofence = false;
				_playerStrictEntryTime = null;
				_playerStrictStopStartTime = null;
				_playerStrictPitValidInTransit = false;
			}
			else if (IsInPitLaneZone(state.TrackPositionPercent))
			{
				if (!_playerIsInsideStrictGeofence)
				{
					_playerIsInsideStrictGeofence = true;
					_playerStrictEntryTime = sessionClock;
					_playerStrictBoxTimeCache = 0.0;
					_playerStrictStopStartTime = null;
					_playerStrictPitValidInTransit = false;
					log.Log(LogModule.RADAR, LogType.EVENT, "Player Spatial Pit Entry", $"Pos: {state.TrackPositionPercent:F4}");
				}
				if (state.IsInPitLane || state.IsInPitBox)
				{
					_playerStrictPitValidInTransit = true;
				}
				if (state.SpeedKmh < 0.5 || state.IsInPitBox)
				{
					if (!_playerStrictStopStartTime.HasValue)
					{
						_playerStrictStopStartTime = sessionClock;
						log.Log(LogModule.RADAR, LogType.EVENT, "Player Spatial Stopped in Box");
					}
				}
				else if (_playerStrictStopStartTime.HasValue)
				{
					_playerStrictBoxTimeCache += Math.Abs(sessionClock - _playerStrictStopStartTime.Value);
					_playerStrictStopStartTime = null;
				}
			}
			else if (_playerIsInsideStrictGeofence)
			{
				_playerIsInsideStrictGeofence = false;
				if (_playerStrictStopStartTime.HasValue)
				{
					_playerStrictBoxTimeCache += Math.Abs(sessionClock - _playerStrictStopStartTime.Value);
					_playerStrictStopStartTime = null;
				}
				if (_playerStrictPitValidInTransit)
				{
					double num10 = (LastPlayerStrictPitLaneTime = Math.Abs(sessionClock - _playerStrictEntryTime.Value));
					LastPlayerStationaryTime = _playerStrictBoxTimeCache;
					log.Log(LogModule.RADAR, LogType.EVENT, "Player Spatial Pit Complete", $"TotalTime: {num10:F2}s | StatTime: {_playerStrictBoxTimeCache:F2}s");
				}
				else
				{
					log.Log(LogModule.RADAR, LogType.FLOW, "Player Spatial Transit Discarded", "Car crossed pit zone on track main straight.");
				}
			}
		}
		else
		{
			_playerIsInsideStrictGeofence = false;
			_playerStrictEntryTime = null;
			_playerStrictStopStartTime = null;
			_playerStrictPitValidInTransit = false;
		}
	}

	
	public void UpdatePlayerTrackRecord(double stintLaps, double lapPace, double fuelPerLap, double maxTank)
	{
		if (_currentTrack != null)
		{
			_currentTrack.AverageStintLaps = stintLaps;
			_currentTrack.AverageLapPace = lapPace;
			_currentTrack.FuelPerLap = fuelPerLap;
			_currentTrack.MaxTank = maxTank;
			_currentTrack.PlayerRecordSet = true;
			SaveDatabase();
		}
	}

	/// <summary>
	/// Registra il limite di pit lane appreso osservando una vettura.
	///
	/// <paramref name="carClass"/> è la classe della vettura **osservata**, non quella del
	/// Player: i record sono indicizzati per traccia+classe, e scrivere il limite di una GT3
	/// nel record delle LMP contaminerebbe entrambi in una gara multiclasse. Passando null si
	/// scrive nel record corrente, comportamento storico.
	/// </summary>
	public void UpdatePitLaneSpeedLimit(double speedLimit, string carClass = null)
	{
		TrackRecord target = _currentTrack;

		if (!string.IsNullOrEmpty(carClass) && _currentTrack != null
			&& !string.Equals(carClass, _currentTrack.CarClass, StringComparison.OrdinalIgnoreCase))
		{
			string lookupKey = (_currentTrack.TrackID + "_" + carClass).ToUpper();
			target = _database.Tracks.FirstOrDefault((TrackRecord t) => t.TrackClassID == lookupKey);
			if (target == null)
			{
				target = new TrackRecord
				{
					TrackClassID = lookupKey,
					TrackID = _currentTrack.TrackID,
					CarClass = carClass
				};
				_database.Tracks.Add(target);
			}
		}

		if (target != null)
		{
			// Il campione non vince da solo: entra in un consenso per classe e a scrivere e' la
			// mediana. Sul replay Misano del 2026-08-23 undici osservazioni a 60 km/h e una a 80
			// (vettura ancora in decelerazione): con "l'ultimo che scrive vince" l'80 sovrascriveva
			// il 60, e il dato tornava corretto solo perche' altri scrivevano dopo.
			string consensusKey = target.TrackClassID ?? "";
			CalibrationConsensus consensus;
			if (!_speedLimitConsensus.TryGetValue(consensusKey, out consensus))
			{
				consensus = new CalibrationConsensus(SpeedLimitAgreementKmh);
				_speedLimitConsensus[consensusKey] = consensus;
			}

			consensus.Add(speedLimit);

			// Si scrive quando il consenso c'e', **oppure** quando non c'e' ancora nessun valore:
			// su una pista mai vista un dato debole vale piu' di nessun dato, e la soglia di
			// rilevamento pit ha comunque un fallback sensato (PitLaneDetector.SpeedThresholdFor).
			// Senza questa distinzione un primo campione anomalo verrebbe scritto e resterebbe in
			// vigore finche' non arrivano gli altri: si auto-corregge, ma la finestra sbagliata
			// esiste.
			bool haveNothingYet = target.PitLaneSpeedLimit <= 0.0;
			if (!consensus.HasConsensus && !haveNothingYet) return;

			double consolidated = consensus.Value;
			if (target.PitLaneSpeedLimit != consolidated)
			{
				target.PitLaneSpeedLimit = consolidated;
				SaveDatabase();
			}
		}
	}

	/// <summary>
	/// Limite di pit lane appreso, in km/h. 0.0 se non ancora imparato.
	///
	/// Con <paramref name="carClass"/> nullo restituisce quello della classe corrente. Se la
	/// classe richiesta non ha ancora un valore, ricade su qualunque classe abbia imparato un
	/// limite sulla **stessa pista**: il limite è quasi sempre unico per circuito, quindi un
	/// dato imparato da un'altra classe vale più di nessun dato.
	/// </summary>
	/// <summary>
	/// Se il circuito corrente ha una zona box calibrata utilizzabile come filtro spaziale.
	/// Serve a chi vuole usare la geofence come criterio: senza bordi validi il filtro va
	/// disattivato, non applicato a vuoto.
	/// </summary>
	public bool HasCalibratedPitZone()
	{
		return _currentTrack != null && _currentTrack.HasValidCleanSectorBounds();
	}

	public double GetPitLaneSpeedLimit(string carClass = null)
	{
		if (_currentTrack == null) return 0.0;

		if (string.IsNullOrEmpty(carClass)
			|| string.Equals(carClass, _currentTrack.CarClass, StringComparison.OrdinalIgnoreCase))
		{
			if (_currentTrack.PitLaneSpeedLimit > 0.0) return _currentTrack.PitLaneSpeedLimit;
		}
		else
		{
			string lookupKey = (_currentTrack.TrackID + "_" + carClass).ToUpper();
			var record = _database.Tracks.FirstOrDefault((TrackRecord t) => t.TrackClassID == lookupKey);
			if (record != null && record.PitLaneSpeedLimit > 0.0) return record.PitLaneSpeedLimit;
		}

		var sameTrack = _database.Tracks.FirstOrDefault(
			(TrackRecord t) => t.TrackID == _currentTrack.TrackID && t.PitLaneSpeedLimit > 0.0);
		return sameTrack != null ? sameTrack.PitLaneSpeedLimit : 0.0;
	}

	public void ResetSession()
	{
		// L'autorizzazione a calibrare non sopravvive al cambio di sessione: quello che si è
		// osservato in practice non dice nulla su come inizierà la gara.
		_geofenceGate.Reset();

		// Il **consenso** invece sì, di proposito: l'ingresso della corsia box è una proprietà
		// fisica del circuito, non cambia fra practice e gara. Le geofence si osservano una volta
		// per sosta, e ci sono gare con una sosta sola: azzerare a ogni cambio di sessione
		// significherebbe non raggiungere mai le tre osservazioni concordi. Si azzera invece al
		// cambio di pista o classe (vedi Update).
		_pitEntryTime = null;
		_stopStartTime = null;
		_isFueling = false;
		_activeMode = CalibrationMode.None;
		_dtInvalidated = false;
		_pitBoxTimeCache = 0.0;
		_lastFuelIncreaseTime = 0.0;
		_fuelStartTime = 0.0;
		LastStationaryTime = 0.0;
		LastPlayerStrictPitLaneTime = 0.0;
		LastPlayerStationaryTime = 0.0;
		_fuelLevelAtStopStart = 0.0;
		_isSequentialLayoutDetected = null;
		CurrentGameName = null;
		_playerIsInsideStrictGeofence = false;
		_playerStrictEntryTime = null;
		_playerStrictBoxTimeCache = 0.0;
		_playerStrictStopStartTime = null;
		_playerStrictPitValidInTransit = false;
	}

	}
}
