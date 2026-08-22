// -------------------------------------------------------------------------
// FILE: CalibrationConfidenceUnitTests.cs
// Livelli di confidenza dei dati calibrati. La regola che conta e' una sola:
// una stima non deve MAI poter cancellare una misura fatta dal pilota.
// -------------------------------------------------------------------------

using System;
using SimRIG;

namespace User.PluginSdkDemo.Tests
{
    public class CalibrationConfidenceUnitTests
    {
        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception("[CalibrationConfidence] " + message);
        }

        private static void Pass(string name)
        {
            Console.WriteLine("  [PASS] " + name);
        }

        public static void RunAllTests()
        {
            Console.WriteLine("[TEST] Running Calibration Confidence Tests...");

            Test_LevelsAreOrderedByStrength();
            Test_Regression_EstimateCannotOverwriteConfirmed();
            Test_StrongerAlwaysWins();
            Test_SameLevelIsAnUpdate();
            Test_UnknownAcceptsAnything();
            Test_NewRecordsStartUnknown();
            Test_Regression_ExistingDatabaseIsNotDowngraded();
            Test_MigrationLeavesEmptyRecordsAlone();
            Test_MigrationIsIdempotent();
            Test_Regression_StatusAccountsForGeofences();
            Test_StatusDistinguishesEstimatedFromMeasured();
            Test_StatusListsWhatIsMissing();

            Console.WriteLine("[TEST SUCCESS] All Calibration Confidence Tests Passed!");
        }

        /// <summary>
        /// L'ordinamento non e' cosmetico: la regola di scrittura e' un confronto >=, quindi
        /// invertire due valori nell'enum ribalterebbe silenziosamente le precedenze.
        /// </summary>
        private static void Test_LevelsAreOrderedByStrength()
        {
            Assert(CalibrationConfidence.Unknown < CalibrationConfidence.EstimatedOpponent,
                   "Unknown deve essere il piu' debole");
            Assert(CalibrationConfidence.EstimatedOpponent < CalibrationConfidence.EstimatedPlayer,
                   "una stima dagli avversari vale meno di una misura sul Player");
            Assert(CalibrationConfidence.EstimatedPlayer < CalibrationConfidence.Confirmed,
                   "una misura in gara vale meno di una calibrazione guidata");

            Pass("Test_LevelsAreOrderedByStrength");
        }

        /// <summary>
        /// Il requisito esplicito dell'utente: i dati stimati dagli opponent non devono poter
        /// sovrascrivere quelli appresi dal Player.
        /// </summary>
        private static void Test_Regression_EstimateCannotOverwriteConfirmed()
        {
            Assert(!PitRadar.CanOverwrite(CalibrationConfidence.Confirmed,
                                          CalibrationConfidence.EstimatedOpponent),
                   "REGRESSIONE: una stima dagli avversari non deve mai sovrascrivere una calibrazione guidata");

            Assert(!PitRadar.CanOverwrite(CalibrationConfidence.Confirmed,
                                          CalibrationConfidence.EstimatedPlayer),
                   "REGRESSIONE: nemmeno una misura di gara deve sovrascrivere una calibrazione guidata");

            Assert(!PitRadar.CanOverwrite(CalibrationConfidence.EstimatedPlayer,
                                          CalibrationConfidence.EstimatedOpponent),
                   "REGRESSIONE: una stima dagli avversari non deve sovrascrivere una misura sul Player");

            Pass("Test_Regression_EstimateCannotOverwriteConfirmed");
        }

        private static void Test_StrongerAlwaysWins()
        {
            // Il caso "pilota salta la practice, va in gara, poi fa un pit stop vero":
            // il dato buono deve poter sostituire la stima.
            Assert(PitRadar.CanOverwrite(CalibrationConfidence.EstimatedOpponent,
                                         CalibrationConfidence.EstimatedPlayer),
                   "la misura sul Player deve sostituire la stima dagli avversari");

            Assert(PitRadar.CanOverwrite(CalibrationConfidence.EstimatedOpponent,
                                         CalibrationConfidence.Confirmed),
                   "la calibrazione guidata deve sostituire qualsiasi stima");

            Assert(PitRadar.CanOverwrite(CalibrationConfidence.EstimatedPlayer,
                                         CalibrationConfidence.Confirmed),
                   "la calibrazione guidata vince sempre");

            Pass("Test_StrongerAlwaysWins");
        }

        private static void Test_SameLevelIsAnUpdate()
        {
            // Una calibrazione rifatta, o una stima raffinata, sono aggiornamenti legittimi.
            Assert(PitRadar.CanOverwrite(CalibrationConfidence.Confirmed,
                                         CalibrationConfidence.Confirmed),
                   "rifare la calibrazione guidata deve essere possibile");
            Assert(PitRadar.CanOverwrite(CalibrationConfidence.EstimatedOpponent,
                                         CalibrationConfidence.EstimatedOpponent),
                   "una stima piu' recente puo' raffinare la precedente");

            Pass("Test_SameLevelIsAnUpdate");
        }

        private static void Test_UnknownAcceptsAnything()
        {
            Assert(PitRadar.CanOverwrite(CalibrationConfidence.Unknown,
                                         CalibrationConfidence.EstimatedOpponent),
                   "su un dato mai osservato anche la stima piu' debole e' un miglioramento");
            Assert(PitRadar.CanOverwrite(CalibrationConfidence.Unknown,
                                         CalibrationConfidence.Confirmed),
                   "e a maggior ragione una calibrazione guidata");

            Pass("Test_UnknownAcceptsAnything");
        }

        private static void Test_NewRecordsStartUnknown()
        {
            var track = new TrackRecord();
            Assert(track.GeofenceConfidence == CalibrationConfidence.Unknown,
                   "un circuito mai visto parte senza confidenza");
            Assert(track.PitEntryPct == -1.0 && track.PitExitPct == -1.0,
                   "e senza geofence");

            var cls = new ClassRecord();
            Assert(cls.FuelFillRateConfidence == CalibrationConfidence.Unknown
                   && cls.TyreChangeTimeConfidence == CalibrationConfidence.Unknown,
                   "una classe mai vista parte senza confidenza");

            Pass("Test_NewRecordsStartUnknown");
        }

        /// <summary>
        /// Il vincolo di compatibilita' del piano. Newtonsoft applica il default della proprieta'
        /// ai campi assenti dal JSON, quindi senza migrazione un database salvato prima che
        /// esistessero i livelli nascerebbe Unknown — e una stima dagli avversari potrebbe
        /// cancellare una calibrazione fatta a mano dal pilota.
        ///
        /// I valori sono quelli reali del database attuale (Misano GP / GT3 2025).
        /// </summary>
        private static void Test_Regression_ExistingDatabaseIsNotDowngraded()
        {
            var legacy = new SimRigDatabase();
            legacy.Tracks.Add(new TrackRecord
            {
                TrackClassID = "MISANO GP_GT3 2025",
                TrackID = "MISANO GP",
                CarClass = "GT3 2025",
                PitEntryPct = 0.9498,
                PitExitPct = 0.1088,
                PitLaneSpeedLimit = 60.0
                // GeofenceConfidence assente: nel JSON reale il campo non esiste
            });
            legacy.Classes.Add(new ClassRecord
            {
                CarClass = "GT3 2025",
                FuelFillRate = 2.62,
                TyreChangeTime = 27.0
            });

            PitRadar.MigrateLegacyConfidence(legacy);

            Assert(legacy.Tracks[0].GeofenceConfidence == CalibrationConfidence.Confirmed,
                   "REGRESSIONE: una geofence gia' presente deve valere come Confirmed, non Unknown");
            Assert(legacy.Classes[0].FuelFillRateConfidence == CalibrationConfidence.Confirmed,
                   "REGRESSIONE: un fuel fill rate gia' presente deve valere come Confirmed");
            Assert(legacy.Classes[0].TyreChangeTimeConfidence == CalibrationConfidence.Confirmed,
                   "REGRESSIONE: un tempo gomme gia' presente deve valere come Confirmed");

            // Conseguenza pratica: una stima dagli avversari non puo' toccarli.
            Assert(!PitRadar.CanOverwrite(legacy.Tracks[0].GeofenceConfidence,
                                          CalibrationConfidence.EstimatedOpponent),
                   "REGRESSIONE: dopo la migrazione il dato storico deve essere protetto dalle stime");

            // E i valori non devono essere stati toccati.
            Assert(legacy.Tracks[0].PitEntryPct == 0.9498 && legacy.Tracks[0].PitExitPct == 0.1088,
                   "la migrazione non deve alterare i valori, solo dichiararne la confidenza");

            Pass("Test_Regression_ExistingDatabaseIsNotDowngraded");
        }

        private static void Test_MigrationLeavesEmptyRecordsAlone()
        {
            var db = new SimRigDatabase();
            db.Tracks.Add(new TrackRecord { TrackClassID = "DAYTONA_GTP" }); // pista mai vista
            db.Classes.Add(new ClassRecord { CarClass = "GTP" });

            PitRadar.MigrateLegacyConfidence(db);

            Assert(db.Tracks[0].GeofenceConfidence == CalibrationConfidence.Unknown,
                   "un circuito senza geofence non deve essere promosso a Confirmed dal nulla");
            Assert(db.Classes[0].FuelFillRateConfidence == CalibrationConfidence.Unknown,
                   "una classe senza dati non deve essere promossa");

            Pass("Test_MigrationLeavesEmptyRecordsAlone");
        }

        /// <summary>
        /// La migrazione gira a ogni caricamento del database: non deve promuovere dati che nel
        /// frattempo sono stati scritti come stime.
        /// </summary>
        private static void Test_MigrationIsIdempotent()
        {
            var db = new SimRigDatabase();
            db.Tracks.Add(new TrackRecord
            {
                PitEntryPct = 0.5,
                PitExitPct = 0.6,
                GeofenceConfidence = CalibrationConfidence.EstimatedOpponent
            });

            PitRadar.MigrateLegacyConfidence(db);
            PitRadar.MigrateLegacyConfidence(db);

            Assert(db.Tracks[0].GeofenceConfidence == CalibrationConfidence.EstimatedOpponent,
                   "una stima dichiarata non deve essere promossa a Confirmed dalla migrazione");

            Pass("Test_MigrationIsIdempotent");
        }

        /// <summary>
        /// Il difetto dello stato precedente: guardava solo PitTransitTime e FuelFillRate, quindi
        /// un circuito con le **geofence** non calibrate risultava comunque READY — proprio il caso
        /// in cui il rilevamento pit del Player perde colpi.
        /// </summary>
        private static void Test_Regression_StatusAccountsForGeofences()
        {
            string missing;
            string status = PitRadar.BuildCalibrationStatus(
                geofenceCalibrated: false,
                pitTransitTime: 36.0,
                fuelFillRate: 2.62,
                tyreChangeTime: 27.0,
                geofenceConfidence: CalibrationConfidence.Unknown,
                fuelConfidence: CalibrationConfidence.Confirmed,
                tyreConfidence: CalibrationConfidence.Confirmed,
                missing: out missing);

            Assert(status != "READY",
                   "REGRESSIONE: senza geofence calibrate lo stato non puo' essere READY");
            Assert(missing.Contains("PIT ZONES"),
                   $"le zone mancanti devono essere elencate, ottenuto '{missing}'");

            Pass("Test_Regression_StatusAccountsForGeofences");
        }

        private static void Test_StatusDistinguishesEstimatedFromMeasured()
        {
            string missing;

            // Tutto presente e tutto misurato.
            string measured = PitRadar.BuildCalibrationStatus(true, 36.0, 2.62, 27.0,
                CalibrationConfidence.Confirmed, CalibrationConfidence.Confirmed,
                CalibrationConfidence.Confirmed, out missing);
            Assert(measured == "READY", $"tutto misurato deve dare READY, ottenuto '{measured}'");
            Assert(missing == "", "non deve mancare nulla");

            // Tutto presente ma il fill rate viene da una sosta di gara.
            string estimated = PitRadar.BuildCalibrationStatus(true, 36.0, 2.62, 27.0,
                CalibrationConfidence.Confirmed, CalibrationConfidence.EstimatedPlayer,
                CalibrationConfidence.Confirmed, out missing);
            Assert(estimated == "READY (ESTIMATED)",
                   $"un dato stimato deve essere dichiarato, ottenuto '{estimated}'");
            Assert(missing == "", "ma non manca nulla: funziona, e' solo meno preciso");

            Pass("Test_StatusDistinguishesEstimatedFromMeasured");
        }

        private static void Test_StatusListsWhatIsMissing()
        {
            string missing;

            // Circuito e classe mai visti: tutto da fare.
            string fresh = PitRadar.BuildCalibrationStatus(false, 0.0, 0.0, 0.0,
                CalibrationConfidence.Unknown, CalibrationConfidence.Unknown,
                CalibrationConfidence.Unknown, out missing);
            Assert(fresh == "NEEDS FULL CALIBRATION",
                   $"con quattro dati mancanti serve la calibrazione completa, ottenuto '{fresh}'");

            // Manca solo il tempo gomme.
            string partial = PitRadar.BuildCalibrationStatus(true, 36.0, 2.62, 0.0,
                CalibrationConfidence.Confirmed, CalibrationConfidence.Confirmed,
                CalibrationConfidence.Unknown, out missing);
            Assert(partial.Contains("TYRE TIME"),
                   $"lo stato deve dire cosa manca, ottenuto '{partial}'");
            Assert(!partial.Contains("FUEL RATE"),
                   "e non deve elencare cio' che c'e' gia'");

            Pass("Test_StatusListsWhatIsMissing");
        }
    }
}
