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

	public void UpdatePitInOutAccDecTime(double val)
	{
		if (_currentTrack != null && val > 0.0)
		{
			_currentTrack.PitInOutAccDecTime = val;
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
			_currentTrack = _database.Tracks.FirstOrDefault((TrackRecord t) => t.TrackClassID == lookupKey);
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
		// Gate di autorizzazione: valutato a ogni tick, prima di qualunque scrittura di geofence.
		_geofenceGate.Update(state.IsInPitLane, state.TrackPositionPercent, sessionClock, state.TrackLengthMeters);

		if (state.IsInPitLane)
		{
			if (!_pitEntryTime.HasValue)
			{
				_pitEntryTime = sessionClock;
				_pitBoxTimeCache = 0.0;
				_dtInvalidated = false;
				_isFueling = false;
				_lastFuelIncreaseTime = 0.0;
				_fuelStartTime = 0.0;

				// Si registra solo arrivando da un tragitto genuino, e solo se il dato che
				// andremmo a sostituire non è più forte di questo (Confirmed dal Player).
				if (_geofenceGate.CanCalibrateEntry
					&& CanOverwrite(_currentTrack.GeofenceConfidence, CalibrationConfidence.Confirmed))
				{
					_currentTrack.PitEntryPct = state.TrackPositionPercent;
					SaveDatabase();
					log.Log(LogModule.RADAR, LogType.EVENT, "Pit Entry Pct Calibrated",
						$"{state.TrackPositionPercent:F3} | confidence=Confirmed");
				}
				else if (_geofenceGate.PitLaneEntered && !_geofenceGate.CanCalibrateEntry)
				{
					// Ingresso non credibile: partenza dai box, teletrasporto, o sessione appena
					// iniziata. Va detto, altrimenti una calibrazione mancante sembra un bug.
					log.Log(LogModule.RADAR, LogType.EVENT, "Pit Entry Pct Calibration Skipped",
						$"pos={state.TrackPositionPercent:F3} | genuineTrackSample={_geofenceGate.HasGenuineTrackSample} | continuity={_geofenceGate.LastContinuity}");
				}
				if (fuelToAdd == 20.0 && selectedTyres == TyreSelectionScope.None)
				{
					_activeMode = CalibrationMode.SplashAndDash;
				}
				else if (fuelToAdd == 0.0 && selectedTyres == TyreSelectionScope.All4)
				{
					_activeMode = CalibrationMode.TyreChange;
				}
				else
				{
					_activeMode = CalibrationMode.DriveThrough;
				}
				log.Log(LogModule.RADAR, LogType.EVENT, "Player Pit Entry", $"Mode: {_activeMode}");
			}
			if (state.IsInPitBox)
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
		else if (_pitEntryTime.HasValue)
		{
			// L'uscita si registra solo se il relativo ingresso era autorizzato: entry ed exit
			// devono venire dallo stesso transito, altrimenti la zona sarebbe composta da due
			// osservazioni scollegate. Qui la coppia si chiude, quindi si dichiara la confidenza.
			if (_geofenceGate.CanCalibrateExit
				&& CanOverwrite(_currentTrack.GeofenceConfidence, CalibrationConfidence.Confirmed))
			{
				_currentTrack.PitExitPct = state.TrackPositionPercent;
				_currentTrack.GeofenceConfidence = CalibrationConfidence.Confirmed;
				SaveDatabase();
				log.Log(LogModule.RADAR, LogType.EVENT, "Pit Exit Pct Calibrated",
					$"{state.TrackPositionPercent:F3} | confidence=Confirmed | entry={_currentTrack.PitEntryPct:F3}");
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
				if (_currentTrack.PitTransitTime == 0.0 && num2 > 0.5)
				{
					_currentTrack.PitTransitTime = num2;
					flag = true;
				}
			}
			else if (_activeMode == CalibrationMode.TyreChange)
			{
				if (_pitBoxTimeCache > 0.5
					&& CanOverwrite(_currentClass.TyreChangeTimeConfidence, CalibrationConfidence.Confirmed))
				{
					_currentClass.TyreChangeTime = _pitBoxTimeCache;
					_currentClass.TyreChangeTimeConfidence = CalibrationConfidence.Confirmed;
					flag = true;
					log.Log(LogModule.RADAR, LogType.EVENT, "Tyre Change Time Calibrated", $"{_pitBoxTimeCache:F1}s | confidence=Confirmed");
				}
				if (_currentTrack.PitTransitTime == 0.0 && num2 > 0.5)
				{
					_currentTrack.PitTransitTime = num2;
					flag = true;
				}
			}
			else if (_activeMode == CalibrationMode.DriveThrough && !_dtInvalidated)
			{
				if (_currentTrack.PitDriveThroughTime == 0.0 && num > 0.5)
				{
					_currentTrack.PitDriveThroughTime = num;
					flag = true;
					log.Log(LogModule.RADAR, LogType.EVENT, "Drive-Through Time Calibrated", num.ToString("F2"));
				}
			}
			else if (_activeMode == CalibrationMode.StopAndGo && _currentTrack.PitTransitTime == 0.0 && num2 > 0.5)
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
			target.PitLaneSpeedLimit = speedLimit;
			SaveDatabase();
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
