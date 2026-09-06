// -------------------------------------------------------------------------
// FILE: PredictedPaceUnitTests.cs
// Y-52 passo 2. Seeding di DriverCarEstLapTime e CarClassEstLapTime
// nei ripieghi di passo e flag IsLapsPredictionValid.
// -------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using SimRIG;

namespace User.PluginSdkDemo.Tests
{
    public class PredictedPaceUnitTests
    {
        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception("[PredictedPace] " + message);
        }

        private static void Pass(string name)
        {
            Console.WriteLine("  [PASS] " + name);
        }

        public static void RunAllTests()
        {
            Console.WriteLine("[TEST] Running Predicted Pace Seeding Tests (Y-52 Passo 2)...");

            Test_ResolvePlayerPace_NormalizedBaselineTakesPrecedence();
            Test_ResolvePlayerPace_BestLapTakesPrecedenceOverMetadata();
            Test_ResolvePlayerPace_SeedsFromPlayerMetadata();
            Test_ResolvePlayerPace_SeedsFromClassMetadataWhenPlayerNull();
            Test_ResolvePlayerPace_FallsBackToPhysicalSpeed();
            Test_ResolvePlayerPace_FallsBackTo120WhenNoTrackLength();

            Test_ResolveLeaderPace_PlayerIsLeader();
            Test_ResolveLeaderPace_UsesOpponentMovingAverage();
            Test_ResolveLeaderPace_SeedsFromMetadataPrior();
            Test_ResolveLeaderPace_FallbackWhenNoMetadata();

            Test_IsLapsPredictionValid_LapLimitedSession();
            Test_IsLapsPredictionValid_TrueWithMetadataPrior();
            Test_IsLapsPredictionValid_TrueWithClassMetadataPrior();
            Test_IsLapsPredictionValid_FalseWhenOnlyBlindFallback();
            Test_IsLapsPredictionValid_FalseOutsideRaceSession();
            Test_IsLapsPredictionValid_FalsePreGreenFlag_WhenSessionStateStatusLessThan4();
            Test_IsLapsPredictionValid_FalseWhenTimeLimitedCountdownNegative();

            Test_RoadAtlanta_GT3_LapProjection_Solves3LapBlackHole();

            Console.WriteLine("[TEST SUCCESS] All Predicted Pace Seeding Tests Passed!");
        }

        /// <summary>
        /// ADR-005: Un dato misurato vince sempre su un prior di configurazione.
        /// Appena esiste una baseline normalizzata in pista, quella vince sui metadati YAML.
        /// </summary>
        private static void Test_ResolvePlayerPace_NormalizedBaselineTakesPrecedence()
        {
            double pace = RaceAnalyzer.ResolvePlayerPace(
                normalizedRaceStartPace: 77.850,
                bestLapTimeSec: 77.100,
                playerEstimatedPaceSec: 76.524,
                classEstimatedPaceSec: 76.524,
                trackLengthMeters: 4088.0);

            Assert(Math.Abs(pace - 77.850) < 0.001,
                $"Atteso 77.850 dalla baseline misurata, ottenuto {pace}");
            Pass("La baseline normalizzata misurata in pista vince sempre sul prior YAML (ADR-005)");
        }

        /// <summary>
        /// Se non c'e' ancora una baseline normalizzata ma c'e' un miglior giro registrato dal gioco,
        /// il miglior giro vince sul prior YAML.
        /// </summary>
        private static void Test_ResolvePlayerPace_BestLapTakesPrecedenceOverMetadata()
        {
            double pace = RaceAnalyzer.ResolvePlayerPace(
                normalizedRaceStartPace: 0.0,
                bestLapTimeSec: 76.900,
                playerEstimatedPaceSec: 76.524,
                classEstimatedPaceSec: 76.524,
                trackLengthMeters: 4088.0);

            Assert(Math.Abs(pace - 76.900) < 0.001,
                $"Atteso 76.900 dal miglior giro, ottenuto {pace}");
            Pass("Il tempo sul giro misurato in sessione vince sul prior YAML");
        }

        /// <summary>
        /// Al giro 1 con zero giri cronometrati completati (il buco nero dei primi 3 giri),
        /// si usa DriverCarEstLapTime dallo YAML invece di 120.0s.
        /// </summary>
        private static void Test_ResolvePlayerPace_SeedsFromPlayerMetadata()
        {
            double pace = RaceAnalyzer.ResolvePlayerPace(
                normalizedRaceStartPace: 0.0,
                bestLapTimeSec: 0.0,
                playerEstimatedPaceSec: 76.524,
                classEstimatedPaceSec: 77.000,
                trackLengthMeters: 4088.0);

            Assert(Math.Abs(pace - 76.524) < 0.001,
                $"Atteso 76.524 dal prior del pilota, ottenuto {pace}");
            Pass("Con zero giri completati il passo viene seminato da DriverCarEstLapTime (non 120.0s)");
        }

        /// <summary>
        /// Se il prior specifico del pilota e' nullo, si usa il prior di classe dallo YAML.
        /// </summary>
        private static void Test_ResolvePlayerPace_SeedsFromClassMetadataWhenPlayerNull()
        {
            double pace = RaceAnalyzer.ResolvePlayerPace(
                normalizedRaceStartPace: 0.0,
                bestLapTimeSec: 0.0,
                playerEstimatedPaceSec: null,
                classEstimatedPaceSec: 76.524,
                trackLengthMeters: 4088.0);

            Assert(Math.Abs(pace - 76.524) < 0.001,
                $"Atteso 76.524 dal prior di classe, ottenuto {pace}");
            Pass("Se il prior pilota e' nullo si usa il prior della classe");
        }

        /// <summary>
        /// Senza metadati e senza giri cronometrati, si usa la velocita' fisica di ripiego
        /// (trackLength / 50.0 m/s).
        /// </summary>
        private static void Test_ResolvePlayerPace_FallsBackToPhysicalSpeed()
        {
            double trackLen = 4088.0;
            double pace = RaceAnalyzer.ResolvePlayerPace(
                normalizedRaceStartPace: 0.0,
                bestLapTimeSec: 0.0,
                playerEstimatedPaceSec: null,
                classEstimatedPaceSec: null,
                trackLengthMeters: trackLen);

            double expected = trackLen / 50.0;
            Assert(Math.Abs(pace - expected) < 0.001,
                $"Atteso {expected} dalla velocita' fisica, ottenuto {pace}");
            Pass("Senza metadati ne' misure si usa trackLength / 50.0 come ripiego fisico");
        }

        /// <summary>
        /// Senza metadati e con lunghezza tracciato non valida, si ripiega su 120.0s.
        /// </summary>
        private static void Test_ResolvePlayerPace_FallsBackTo120WhenNoTrackLength()
        {
            double pace = RaceAnalyzer.ResolvePlayerPace(
                normalizedRaceStartPace: 0.0,
                bestLapTimeSec: 0.0,
                playerEstimatedPaceSec: null,
                classEstimatedPaceSec: null,
                trackLengthMeters: 0.0);

            Assert(Math.Abs(pace - 120.0) < 0.001,
                $"Atteso 120.0 come ultimo ripiego forfettario, ottenuto {pace}");
            Pass("Senza alcuna informazione si usa 120.0s come ultimo ripiego forfettario");
        }

        /// <summary>
        /// Se il Player e' P1, il passo del leader e' il passo risolto del Player.
        /// </summary>
        private static void Test_ResolveLeaderPace_PlayerIsLeader()
        {
            var result = RaceAnalyzer.ResolveLeaderPace(
                playerPosition: 1,
                playerResolvedPace: 76.524,
                opponents: null,
                trackedOpponents: null,
                metadata: null,
                bestLapTimeSec: 0.0,
                trackLengthMeters: 4088.0);

            Assert(Math.Abs(result.Pace - 76.524) < 0.001, "Il passo del leader deve essere quello del Player");
            Assert(result.LeaderName == "PLAYER", "Il nome del leader deve essere PLAYER");
            Pass("Da P1 il passo del leader coincide con il passo risolto del Player");
        }

        /// <summary>
        /// Quando l'avversario al comando ha una media mobile, si usa la sua media mobile.
        /// </summary>
        private static void Test_ResolveLeaderPace_UsesOpponentMovingAverage()
        {
            var opponents = new List<GameReaderCommon.Opponent>
            {
                new GameReaderCommon.Opponent { Name = "Sven Neiss", Position = 1, CarClass = "GTP" }
            };

            var tracked = new Dictionary<string, OpponentTelemetryData>();
            var data = new OpponentTelemetryData();
            data.NormalizedTimes.LapMovingAverage = 68.443;
            tracked["Sven Neiss"] = data;

            var result = RaceAnalyzer.ResolveLeaderPace(
                playerPosition: 2,
                playerResolvedPace: 76.524,
                opponents: opponents,
                trackedOpponents: tracked,
                metadata: null,
                bestLapTimeSec: 0.0,
                trackLengthMeters: 4088.0);

            Assert(Math.Abs(result.Pace - 68.443) < 0.001, "Deve usare la LapMovingAverage dell'avversario");
            Assert(result.LeaderName == "Sven Neiss", "Il nome del leader deve essere Sven Neiss");
            Pass("L'avversario al comando con giri registrati usa la sua media mobile");
        }

        /// <summary>
        /// Al giro 1 con avversari senza giri cronometrati, il leader risolve il passo dal SessionMetadata.
        /// </summary>
        private static void Test_ResolveLeaderPace_SeedsFromMetadataPrior()
        {
            var opponents = new List<GameReaderCommon.Opponent>
            {
                new GameReaderCommon.Opponent { Name = "Sven Neiss", Position = 1, CarClass = "GTP" }
            };

            var tracked = new Dictionary<string, OpponentTelemetryData>();
            tracked["Sven Neiss"] = new OpponentTelemetryData(); // 0 giri registrati

            var metadata = new SessionMetadata();
            metadata.ClassEstimatedPaceSec["GTP"] = 68.443;

            var result = RaceAnalyzer.ResolveLeaderPace(
                playerPosition: 2,
                playerResolvedPace: 76.524,
                opponents: opponents,
                trackedOpponents: tracked,
                metadata: metadata,
                bestLapTimeSec: 0.0,
                trackLengthMeters: 4088.0);

            Assert(Math.Abs(result.Pace - 68.443) < 0.001, "Deve usare il prior GTP dai metadati");
            Assert(result.LeaderName == "Sven Neiss", "Il nome del leader deve essere Sven Neiss");
            Pass("Al giro 1 l'avversario leader usa il prior di classe da SessionMetadata");
        }

        /// <summary>
        /// Senza metadati ne' giri registrati, il leader ripiega sulla fisica del tracciato.
        /// </summary>
        private static void Test_ResolveLeaderPace_FallbackWhenNoMetadata()
        {
            var opponents = new List<GameReaderCommon.Opponent>
            {
                new GameReaderCommon.Opponent { Name = "Leader Anon", Position = 1, CarClass = "UNKNOWN" }
            };

            var tracked = new Dictionary<string, OpponentTelemetryData>();
            tracked["Leader Anon"] = new OpponentTelemetryData();

            var result = RaceAnalyzer.ResolveLeaderPace(
                playerPosition: 2,
                playerResolvedPace: 81.76,
                opponents: opponents,
                trackedOpponents: tracked,
                metadata: null,
                bestLapTimeSec: 0.0,
                trackLengthMeters: 4088.0);

            double expected = 4088.0 / 50.0;
            Assert(Math.Abs(result.Pace - expected) < 0.001, "Deve ripiegare sulla velocita' fisica");
            Pass("Senza metadati il leader avversario usa trackLength / 50.0 come ripiego");
        }

        /// <summary>
        /// Gara a giri fissi: il totale giri e' noto per definizione, la stima e' sempre valida.
        /// </summary>
        private static void Test_IsLapsPredictionValid_LapLimitedSession()
        {
            bool valid = RaceAnalyzer.IsLapsPredictionValid(
                isRaceSession: true,
                isRaceFinished: false,
                isLapLimited: true,
                isTimeLimited: false,
                totalLaps: 25,
                normalizedRaceStartPace: 0.0,
                bestLapTimeSec: 0.0,
                playerEstimatedPaceSec: null,
                classEstimatedPaceSec: null);

            Assert(valid, "Gara a giri: deve essere sempre valida");
            Pass("Gara a giri: IsLapsPredictionValid e' true fin dal via");
        }

        /// <summary>
        /// Gara a tempo con prior dal SessionMetadata: valida fin dalla griglia di partenza.
        /// </summary>
        private static void Test_IsLapsPredictionValid_TrueWithMetadataPrior()
        {
            bool valid = RaceAnalyzer.IsLapsPredictionValid(
                isRaceSession: true,
                isRaceFinished: false,
                isLapLimited: false,
                isTimeLimited: true,
                totalLaps: 0,
                normalizedRaceStartPace: 0.0,
                bestLapTimeSec: 0.0,
                playerEstimatedPaceSec: 76.524,
                classEstimatedPaceSec: 76.524);

            Assert(valid, "Gara a tempo con metadati YAML: deve essere valida al giro 1");
            Pass("Gara a tempo con prior YAML: IsLapsPredictionValid e' true al giro 1");
        }

        /// <summary>
        /// Gara a tempo con prior di sola classe dal SessionMetadata: valida al giro 1.
        /// </summary>
        private static void Test_IsLapsPredictionValid_TrueWithClassMetadataPrior()
        {
            bool valid = RaceAnalyzer.IsLapsPredictionValid(
                isRaceSession: true,
                isRaceFinished: false,
                isLapLimited: false,
                isTimeLimited: true,
                totalLaps: 0,
                normalizedRaceStartPace: 0.0,
                bestLapTimeSec: 0.0,
                playerEstimatedPaceSec: null,
                classEstimatedPaceSec: 76.524);

            Assert(valid, "Gara a tempo con prior di classe: deve essere valida");
            Pass("Gara a tempo con prior di sola classe: IsLapsPredictionValid e' true");
        }

        /// <summary>
        /// Gara a tempo senza alcun dato (ne' telemetrico ne' da YAML): falsa perche' si usano ripieghi ciechi.
        /// </summary>
        private static void Test_IsLapsPredictionValid_FalseWhenOnlyBlindFallback()
        {
            bool valid = RaceAnalyzer.IsLapsPredictionValid(
                isRaceSession: true,
                isRaceFinished: false,
                isLapLimited: false,
                isTimeLimited: true,
                totalLaps: 0,
                normalizedRaceStartPace: 0.0,
                bestLapTimeSec: 0.0,
                playerEstimatedPaceSec: null,
                classEstimatedPaceSec: null);

            Assert(!valid, "Gara a tempo su solo ripiego forfettario non deve essere considerata valida");
            Pass("Gara a tempo su solo ripiego forfettario: IsLapsPredictionValid e' false");
        }

        /// <summary>
        /// Fuori da una sessione di gara o a gara finita, la predizione non e' valida.
        /// </summary>
        private static void Test_IsLapsPredictionValid_FalseOutsideRaceSession()
        {
            bool valid = RaceAnalyzer.IsLapsPredictionValid(
                isRaceSession: false,
                isRaceFinished: false,
                isLapLimited: false,
                isTimeLimited: true,
                totalLaps: 0,
                normalizedRaceStartPace: 76.0,
                bestLapTimeSec: 76.0,
                playerEstimatedPaceSec: 76.0,
                classEstimatedPaceSec: 76.0);

            Assert(!valid, "Fuori gara IsLapsPredictionValid deve essere false");
            Pass("Fuori dalla sessione di gara IsLapsPredictionValid e' false");
        }

        /// <summary>
        /// In griglia o giro di ricognizione (SessionStateStatus < 4), IsLapsPredictionValid deve essere false.
        /// </summary>
        private static void Test_IsLapsPredictionValid_FalsePreGreenFlag_WhenSessionStateStatusLessThan4()
        {
            bool valid = RaceAnalyzer.IsLapsPredictionValid(
                isRaceSession: true,
                isRaceFinished: false,
                isLapLimited: false,
                isTimeLimited: true,
                totalLaps: 0,
                normalizedRaceStartPace: 0.0,
                bestLapTimeSec: 0.0,
                playerEstimatedPaceSec: 76.524,
                classEstimatedPaceSec: 76.524,
                sessionStateStatus: 3,
                sessionTimeLeftSec: 2700.0);

            Assert(!valid, "In griglia prima del via IsLapsPredictionValid deve essere false");
            Pass("Griglia pre-gara (SessionStateStatus < 4): IsLapsPredictionValid e' false");
        }

        /// <summary>
        /// Gara a tempo con countdown non ancora attivo (negativo), IsLapsPredictionValid deve essere false.
        /// </summary>
        private static void Test_IsLapsPredictionValid_FalseWhenTimeLimitedCountdownNegative()
        {
            bool valid = RaceAnalyzer.IsLapsPredictionValid(
                isRaceSession: true,
                isRaceFinished: false,
                isLapLimited: false,
                isTimeLimited: true,
                totalLaps: 0,
                normalizedRaceStartPace: 0.0,
                bestLapTimeSec: 0.0,
                playerEstimatedPaceSec: 76.524,
                classEstimatedPaceSec: 76.524,
                sessionStateStatus: 4,
                sessionTimeLeftSec: -1.0);

            Assert(!valid, "Gara a tempo con orologio negativo non deve essere considerata valida");
            Pass("Gara a tempo con orologio negativo: IsLapsPredictionValid e' false");
        }

        /// <summary>
        /// REGRESSIONE ROAD ATLANTA GT3: Il "buco nero dei primi 3 giri".
        ///
        /// Su una gara da 45 minuti (2700 s) a Road Atlanta (giro reale ~77.5 s, totale reale 35 giri,
        /// 1 sosta ai box con pit loss 41.1 s):
        /// - Col difetto vecchio (120.0s di fallback cablato): (2760 - 41.1) / 120.0 = 22.65 giri -> 23 giri totali.
        ///   Un deficit madornale di 12-13 giri che durava fino al primo giro cronometrato (giro 2 o 3).
        /// - Con la semina Y-52 da DriverCarEstLapTime (76.524s): (2754.86 - 41.1) / 76.524 = 35.46 giri -> 36 giri totali
        ///   (con Math.Ceiling della posizione continua). Un errore di 1 solo giro rispetto al reale 35,
        ///   invece del deficit di 12-13 giri che rendeva la strategia inservibile al via.
        ///
        /// Neutralizzazione (ADR-004): se si forza il passo di fallback a 120.0s, il calcolo
        /// produce 23 giri e il test fallisce.
        /// </summary>
        private static void Test_RoadAtlanta_GT3_LapProjection_Solves3LapBlackHole()
        {
            double raceTimeLeftSec = 2700.0; // 45 minuti
            double playerEstPace = 76.524;   // DriverCarEstLapTime Road Atlanta GT3 da YAML
            double pitLossSec = 41.10;       // Pit loss reale a Road Atlanta
            double stintLaps = 22.0;         // Autonomia serbatoio pieno

            double resolvedPace = RaceAnalyzer.ResolvePlayerPace(
                normalizedRaceStartPace: 0.0,
                bestLapTimeSec: 0.0,
                playerEstimatedPaceSec: playerEstPace,
                classEstimatedPaceSec: playerEstPace,
                trackLengthMeters: 4088.0);

            // Calcolo proiezione al tempo della bandiera a inizio gara (giro 1 con 0 completati)
            double flagTime = RaceTimeProjection.TimeUntilLeaderCheckered(raceTimeLeftSec, 0.0, resolvedPace);
            var pitPlan = RaceAnalyzer.ProjectLapsLeftWithStops(flagTime, resolvedPace, stintLaps, stintLaps, pitLossSec);
            double totalLaps = RaceAnalyzer.ProjectPlayerTotalLaps(pitPlan.LapsLeft, 0.0, true, 0.0, false);

            Assert(totalLaps == 36.0,
                $"Regressione Road Atlanta GT3: attesi 36 giri al semaforo verde, ottenuti {totalLaps:F0} (con 120s ne dava 23)");

            // Verifichiamo che col vecchio fallback (120.0s) darebbe davvero 23
            double oldFlagTime = RaceTimeProjection.TimeUntilLeaderCheckered(raceTimeLeftSec, 0.0, 120.0);
            var oldPitPlan = RaceAnalyzer.ProjectLapsLeftWithStops(oldFlagTime, 120.0, stintLaps, stintLaps, pitLossSec);
            double oldTotalLaps = RaceAnalyzer.ProjectPlayerTotalLaps(oldPitPlan.LapsLeft, 0.0, true, 0.0, false);
            Assert(oldTotalLaps == 23.0,
                $"Verifica difetto: col fallback a 120s deve dare 23 giri, ottenuto {oldTotalLaps:F0}");

            Pass("Regressione Road Atlanta GT3: al semaforo verde la proiezione e' 36 giri (risolto il buco nero dei 23)");
        }
    }
}
