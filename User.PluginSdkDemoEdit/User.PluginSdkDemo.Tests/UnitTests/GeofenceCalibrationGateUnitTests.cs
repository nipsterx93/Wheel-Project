// -------------------------------------------------------------------------
// FILE: GeofenceCalibrationGateUnitTests.cs
// Autorizzazione alla scrittura di PitEntryPct / PitExitPct.
// I due casi che contano: partenza dai box (practice) e partenza in griglia
// (gara). Una condizione sola deve coprirli entrambi.
// -------------------------------------------------------------------------

using System;
using SimRIG;

namespace User.PluginSdkDemo.Tests
{
    public class GeofenceCalibrationGateUnitTests
    {
        private const double MisanoMeters = 4200.0;

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception("[GeofenceCalibrationGate] " + message);
        }

        private static void Pass(string name)
        {
            Console.WriteLine("  [PASS] " + name);
        }

        /// <summary>
        /// Simula un tratto guidato in pista: campioni a 0.5 s con avanzamento plausibile
        /// (1% di giro per campione, dentro il margine di continuità).
        ///
        /// Le posizioni contano: alla corsia box ci si arriva **guidando**, quindi un test che
        /// salta da metà giro all'ingresso starebbe simulando un teletrasporto — che il gate
        /// deve giustamente rifiutare. Da qui la firma con posizione finale esplicita.
        /// </summary>
        private static double DriveOnTrack(GeofenceCalibrationGate gate, double startPos,
                                           double startClock, int samples)
        {
            double pos = startPos;
            double clock = startClock;
            for (int i = 0; i < samples; i++)
            {
                gate.Update(false, pos, clock, MisanoMeters);
                pos += 0.010;
                if (pos >= 1.0) pos -= 1.0;
                clock -= 0.5;
            }
            return clock;
        }

        /// <summary>Guida fino a ridosso dell'ingresso box (~0.93), da cui si può entrare.</summary>
        private static double DriveUpToPitEntry(GeofenceCalibrationGate gate, double startClock)
        {
            return DriveOnTrack(gate, 0.750, startClock, 19); // ultimo campione a 0.930
        }

        public static void RunAllTests()
        {
            Console.WriteLine("[TEST] Running Geofence Calibration Gate Tests...");

            Test_Regression_BoxStartDoesNotCalibrate();
            Test_GridStartCalibratesOnFirstPitStop();
            Test_PracticeAuthorisesAfterRealOutLap();
            Test_Regression_TeleportDoesNotAuthorise();
            Test_ExitRequiresAuthorisedEntry();
            Test_ResetClearsAuthorisation();

            Console.WriteLine("[TEST SUCCESS] All Geofence Calibration Gate Tests Passed!");
        }

        /// <summary>
        /// Il difetto originale: partendo dai box, la prima transizione utile registrava la
        /// posizione del PIT BOX come ingresso corsia, e non era piu' correggibile.
        /// </summary>
        private static void Test_Regression_BoxStartDoesNotCalibrate()
        {
            var gate = new GeofenceCalibrationGate();

            // Sessione che inizia in corsia box, vettura ferma.
            double clock = 1000.0;
            for (int i = 0; i < 10; i++)
            {
                gate.Update(true, 0.947, clock, MisanoMeters);
                Assert(!gate.CanCalibrateEntry,
                       "REGRESSIONE: partendo dai box non si deve mai calibrare l'ingresso");
                clock -= 0.5;
            }

            Assert(!gate.HasGenuineTrackSample,
                   "senza essere mai stati in pista non ci puo' essere un campione genuino");

            Pass("Test_Regression_BoxStartDoesNotCalibrate");
        }

        /// <summary>
        /// Il caso che la sequenza rigida True->False->True avrebbe rotto: partendo dalla griglia
        /// manca il True iniziale, e in gara non si calibrerebbe piu' nulla.
        /// </summary>
        private static void Test_GridStartCalibratesOnFirstPitStop()
        {
            var gate = new GeofenceCalibrationGate();

            // Partenza in griglia: gia' in pista dal primo campione, si guida fino all'ingresso.
            double clock = DriveUpToPitEntry(gate, 1000.0);
            Assert(gate.HasGenuineTrackSample,
                   "guidando in pista si deve accumulare un campione genuino");

            // Primo pit stop della gara.
            gate.Update(true, 0.940, clock, MisanoMeters);
            Assert(gate.PitLaneEntered, "l'ingresso in corsia deve essere rilevato");
            Assert(gate.CanCalibrateEntry,
                   "partendo dalla griglia il primo pit stop e' genuino e deve autorizzare");

            Pass("Test_GridStartCalibratesOnFirstPitStop");
        }

        /// <summary>
        /// Partenza dai box: si autorizza solo dopo l'uscita vera e un tratto guidato,
        /// cioe' il comportamento che l'utente aveva descritto come True->False->True.
        /// </summary>
        private static void Test_PracticeAuthorisesAfterRealOutLap()
        {
            var gate = new GeofenceCalibrationGate();

            // 1. Fermi in corsia box.
            double clock = 1000.0;
            for (int i = 0; i < 5; i++)
            {
                gate.Update(true, 0.947, clock, MisanoMeters);
                clock -= 0.5;
            }
            Assert(!gate.CanCalibrateEntry, "precondizione: ancora nessuna autorizzazione");

            // 2. Uscita dai box.
            gate.Update(false, 0.100, clock, MisanoMeters);
            clock -= 0.5;
            Assert(gate.PitLaneExited, "l'uscita deve essere rilevata");
            Assert(!gate.CanCalibrateExit,
                   "un'uscita senza ingresso autorizzato non deve registrare nulla");

            // 3. Giro di uscita vero, fino a ridosso dell'ingresso box.
            clock = DriveUpToPitEntry(gate, clock);
            Assert(gate.HasGenuineTrackSample, "il giro di uscita deve produrre campioni genuini");

            // 4. Rientro ai box: ora e' credibile.
            gate.Update(true, 0.940, clock, MisanoMeters);
            Assert(gate.CanCalibrateEntry, "dopo un giro vero il rientro deve autorizzare");

            // 5. Uscita successiva: chiude la coppia entry+exit.
            clock -= 0.5;
            gate.Update(false, 0.945, clock, MisanoMeters);
            Assert(gate.CanCalibrateExit, "l'uscita dopo un ingresso autorizzato chiude la coppia");

            Pass("Test_PracticeAuthorisesAfterRealOutLap");
        }

        /// <summary>
        /// Il caso ESC sollevato dall'utente: il pilota esce dai box, guida un pezzo, preme ESC
        /// e viene riportato ai box. La transizione di IsInPitLane e' identica a un rientro vero.
        /// </summary>
        private static void Test_Regression_TeleportDoesNotAuthorise()
        {
            var gate = new GeofenceCalibrationGate();

            // Partenza dai box, uscita, e un tratto guidato VERO a meta' giro: a questo punto
            // il tragitto accumulato e' pienamente credibile.
            gate.Update(true, 0.947, 1000.0, MisanoMeters);
            double clock = DriveOnTrack(gate, 0.100, 999.5, 20);   // arriva a ~0.29
            Assert(gate.HasGenuineTrackSample, "precondizione: il tratto guidato e' genuino");

            // ESC a meta' giro: salto istantaneo all'ingresso box, senza percorrere il tragitto.
            gate.Update(true, 0.940, clock, MisanoMeters);
            Assert(gate.PitLaneEntered, "la transizione avviene comunque");
            Assert(!gate.CanCalibrateEntry,
                   "REGRESSIONE: un teletrasporto non deve autorizzare la calibrazione, " +
                   "nemmeno dopo un tratto guidato valido");
            Assert(!gate.HasGenuineTrackSample,
                   "una discontinuita' deve invalidare il tragitto accumulato, non lasciarlo valido");

            // Per tornare autorizzati serve guidare di nuovo davvero.
            clock -= 0.5;
            gate.Update(false, 0.945, clock, MisanoMeters);
            clock = DriveUpToPitEntry(gate, clock - 0.5);
            gate.Update(true, 0.940, clock, MisanoMeters);
            Assert(gate.CanCalibrateEntry,
                   "dopo un nuovo tragitto genuino l'autorizzazione deve tornare");

            Pass("Test_Regression_TeleportDoesNotAuthorise");
        }

        private static void Test_ExitRequiresAuthorisedEntry()
        {
            var gate = new GeofenceCalibrationGate();

            // Partenza dai box, uscita subito: l'ingresso iniziale non era autorizzato,
            // quindi nemmeno l'uscita deve registrare.
            gate.Update(true, 0.947, 1000.0, MisanoMeters);
            gate.Update(false, 0.100, 999.5, MisanoMeters);
            Assert(!gate.CanCalibrateExit,
                   "entry ed exit devono venire dallo stesso transito credibile");

            Pass("Test_ExitRequiresAuthorisedEntry");
        }

        private static void Test_ResetClearsAuthorisation()
        {
            var gate = new GeofenceCalibrationGate();
            DriveOnTrack(gate, 0.100, 1000.0, 20);
            Assert(gate.HasGenuineTrackSample, "precondizione: campione genuino accumulato");

            gate.Reset();
            Assert(!gate.HasGenuineTrackSample,
                   "quello che si e' visto in practice non vale per la sessione successiva");

            // Dopo il reset, un ingresso immediato non deve autorizzare.
            gate.Update(true, 0.950, 900.0, MisanoMeters);
            Assert(!gate.CanCalibrateEntry, "dopo il reset serve un nuovo tragitto genuino");

            Pass("Test_ResetClearsAuthorisation");
        }
    }
}
