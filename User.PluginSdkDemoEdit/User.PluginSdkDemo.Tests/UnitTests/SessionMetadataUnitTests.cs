// -------------------------------------------------------------------------
// FILE: SessionMetadataUnitTests.cs
// Y-52 passo 1. Contenitore agnostico, fornitore YAML, dump diagnostico.
//
// ATTENZIONE SULLA PORTATA DI QUESTI TEST:
// lo YAML usato qui e' scritto a mano, quindi verifica la STRUTTURA (annidamento,
// unita' di misura, quirk del '%', risoluzione del Player per indice) e la
// PRESERVAZIONE del comportamento del vecchio parser del BoP. NON verifica che i
// nomi dei campi siano quelli che iRacing manda davvero: quello si potra' fare
// solo con un dump reale, che il dump diagnostico di questo stesso passo produce.
// -------------------------------------------------------------------------

using System;
using SimRIG;

namespace User.PluginSdkDemo.Tests
{
    public class SessionMetadataUnitTests
    {
        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception("[SessionMetadata] " + message);
        }

        private static void Pass(string name)
        {
            Console.WriteLine("  [PASS] " + name);
        }

        /// <summary>Uno YAML nella forma di iRacing, ridotto all'osso.</summary>
        private const string Yaml =
            "WeekendInfo:\n" +
            " TrackDisplayName: Road Atlanta\n" +
            " TrackPitSpeedLimit: 60.00 kph\n" +
            " WeekendOptions:\n" +
            "  StandingStart: 1\n" +
            "  IncidentLimit: 17\n" +
            "  FastRepairsLimit: 1\n" +
            "DriverInfo:\n" +
            " DriverCarIdx: 7\n" +
            " DriverCarEstLapTime: 76.5240\n" +
            " DriverPitTrkPct: 0.029294\n" +
            " DriverCarFuelKgPerLtr: 0.780000\n" +
            " DriverCarFuelMaxLtr: 62.000000\n" +
            " Drivers:\n" +
            " - CarIdx: 3\n" +
            "   UserName: Sven Neiss\n" +
            "   CarClassShortName: GTP\n" +
            "   CarClassEstLapTime: 68.4430\n" +
            "   CarClassMaxFuelPct: 1.000 %\n" +
            "   CarClassDryTireSetLimit: 3 %\n" +
            " - CarIdx: 7\n" +
            "   UserName: Sara Tolotti\n" +
            "   CarClassShortName: GT3\n" +
            "   CarClassEstLapTime: 76.5240\n" +
            "   CarClassMaxFuelPct: 0.500 %\n" +
            "SessionInfo:\n" +
            " Sessions:\n" +
            "  - SessionNum: 0\n";

        public static void RunAllTests()
        {
            Console.WriteLine("[TEST] Running Session Metadata & YAML Parser Tests...");

            Test_EmptyInputIsNotPopulated();
            Test_ParsesWeekendAndDriverFields();
            Test_BopBehaviourIsPreserved();
            Test_SpeedUnitsAreConverted();
            Test_EstimatedPaceFallsBackFromDriverToClass();
            Test_DumpKeyAndDedup();
            Test_SessionDataReader_ParsePercentage();
            Test_SessionDataReader_SpeedKmh();
            Test_SessionDataReader_ReadFromRawObject();

            Console.WriteLine("[TEST SUCCESS] All Session Metadata Tests Passed!");
        }

        /// <summary>
        /// Su un gioco che non pubblica nulla il contenitore resta vuoto, e vuoto deve voler
        /// dire "non lo so" — non zero. E' il presupposto del cross-game.
        /// </summary>
        private static void Test_EmptyInputIsNotPopulated()
        {
            foreach (string input in new[] { null, "", "   " })
            {
                SessionMetadata m = SessionYamlParser.Parse(input);
                Assert(!m.IsPopulated, "un ingresso vuoto non deve risultare popolato");
                Assert(!m.PlayerEstimatedPaceSec.HasValue, "il passo deve restare assente, non zero");
                Assert(!m.PitSpeedLimitKmh.HasValue, "il limite box deve restare assente");
                Assert(m.EstimatedPaceFor("chiunque", "GT3") == null, "nessun passo per nessuno");
            }
            Pass("senza fornitore il contenitore resta assente, non azzerato");
        }

        private static void Test_ParsesWeekendAndDriverFields()
        {
            SessionMetadata m = SessionYamlParser.Parse(Yaml);

            Assert(m.IsPopulated, "con uno YAML valido il contenitore deve risultare popolato");
            Assert(m.TrackName == "Road Atlanta", "traccia letta: " + m.TrackName);
            Assert(m.IncidentLimit == 17, "tetto incidenti");
            Assert(m.FastRepairsAvailable == 1, "riparazioni rapide");
            Assert(m.IsStandingStart == true, "partenza da fermo");
            Assert(m.DryTireSetLimit == 3, "treni gomme, col quirk del '%' di iRacing");

            Assert(m.PlayerPitStallPct.HasValue && Math.Abs(m.PlayerPitStallPct.Value - 0.029294) < 1e-9,
                   "posizione della piazzola");
            Assert(m.FuelDensityKgPerLitre.HasValue && Math.Abs(m.FuelDensityKgPerLitre.Value - 0.78) < 1e-9,
                   "densita' carburante: e' il valore che sostituisce lo 0.75 cablato");
            Assert(m.PlayerMaxFuelLitres.HasValue && Math.Abs(m.PlayerMaxFuelLitres.Value - 62.0) < 1e-9,
                   "capienza serbatoio");

            // Il passo per pilota e' la chiave certa: gli avversari li indicizziamo per nome.
            Assert(m.DriverEstimatedPaceSec.Count == 2, "due piloti letti");
            Assert(Math.Abs(m.DriverEstimatedPaceSec["Sven Neiss"] - 68.443) < 1e-9, "passo GTP per pilota");
            Assert(Math.Abs(m.DriverEstimatedPaceSec["Sara Tolotti"] - 76.524) < 1e-9, "passo GT3 per pilota");

            Assert(m.ClassEstimatedPaceSec.Count == 2, "due classi lette");
            Assert(Math.Abs(m.ClassEstimatedPaceSec["GTP"] - 68.443) < 1e-9, "passo per classe GTP");

            Assert(m.PlayerEstimatedPaceSec.HasValue &&
                   Math.Abs(m.PlayerEstimatedPaceSec.Value - 76.524) < 1e-9,
                   "il Player si risolve per indice (CarIdx 7), non per nome");

            Pass("i campi di WeekendInfo e DriverInfo vengono letti con l'annidamento giusto");
        }

        /// <summary>
        /// Il vecchio <c>ParseOpponentMaxFuelPct</c> restituiva il numero **com'era scritto**:
        /// <c>0.500 %</c> diventava <c>0.5</c>, non <c>50</c>. I log di gara lo confermano
        /// (<c>BoP Pct: 0.500</c>). Questo e' un refactor, non un cambio: se il numero cambiasse,
        /// cambierebbe la capienza dedotta di ogni avversario.
        /// </summary>
        private static void Test_BopBehaviourIsPreserved()
        {
            SessionMetadata m = SessionYamlParser.Parse(Yaml);

            Assert(m.DriverMaxFuelPct.Count == 2, "due valori di BoP letti");
            Assert(Math.Abs(m.DriverMaxFuelPct["Sara Tolotti"] - 0.5) < 1e-9,
                   "0.500 % deve restare 0.5, come faceva il vecchio parser");
            Assert(Math.Abs(m.DriverMaxFuelPct["Sven Neiss"] - 1.0) < 1e-9,
                   "1.000 % deve restare 1.0");

            Pass("il BoP conserva esattamente la semantica del vecchio parser");
        }

        /// <summary>
        /// Il limite box arriva come stringa con unita'. Su un tracciato americano puo' essere in
        /// miglia: senza conversione il limite risulterebbe sbagliato del 61%.
        /// </summary>
        private static void Test_SpeedUnitsAreConverted()
        {
            SessionMetadata kph = SessionYamlParser.Parse(Yaml);
            Assert(kph.PitSpeedLimitKmh.HasValue && Math.Abs(kph.PitSpeedLimitKmh.Value - 60.0) < 1e-9,
                   "60.00 kph resta 60");

            string mph = Yaml.Replace("TrackPitSpeedLimit: 60.00 kph",
                                      "TrackPitSpeedLimit: 37.28 mph");
            SessionMetadata m = SessionYamlParser.Parse(mph);
            Assert(m.PitSpeedLimitKmh.HasValue && Math.Abs(m.PitSpeedLimitKmh.Value - 60.0) < 0.05,
                   "37.28 mph devono diventare ~60 km/h, valgono "
                   + (m.PitSpeedLimitKmh ?? 0).ToString("F2"));

            Pass("le velocita' con unita' vengono convertite in km/h");
        }

        /// <summary>
        /// L'ordine di ripiego conta: il nome del pilota e' una chiave certa, il nome della
        /// classe potrebbe non combaciare con quello che SimHub espone. Prima il pilota.
        /// </summary>
        private static void Test_EstimatedPaceFallsBackFromDriverToClass()
        {
            SessionMetadata m = SessionYamlParser.Parse(Yaml);

            double? perPilota = m.EstimatedPaceFor("Sven Neiss", "CLASSE_CHE_NON_ESISTE");
            Assert(perPilota.HasValue && Math.Abs(perPilota.Value - 68.443) < 1e-9,
                   "il nome del pilota deve vincere sulla classe");

            double? perClasse = m.EstimatedPaceFor("Pilota Mai Visto", "GT3");
            Assert(perClasse.HasValue && Math.Abs(perClasse.Value - 76.524) < 1e-9,
                   "senza il pilota si ripiega sulla sua classe");

            Assert(m.EstimatedPaceFor("Pilota Mai Visto", "Classe Mai Vista") == null,
                   "se non si sa nulla si deve restituire null, non un numero inventato");

            Pass("il passo stimato ripiega dal pilota alla classe, e poi tace");
        }

        /// <summary>
        /// Rigiocare lo stesso replay venti volte deve produrre una voce sola. Ma se il contenuto
        /// cambia davvero (altra vettura, altro BoP) la voce va aggiornata.
        /// </summary>
        private static void Test_DumpKeyAndDedup()
        {
            Assert(SessionMetadataDump.EntryKey("Road Atlanta", "GT3") ==
                   SessionMetadataDump.EntryKey(" Road Atlanta ", " GT3 "),
                   "la chiave non deve dipendere dagli spazi");
            Assert(SessionMetadataDump.EntryKey("Road Atlanta", "GT3") !=
                   SessionMetadataDump.EntryKey("Road Atlanta", "GTP"),
                   "traccia uguale ma classe diversa e' un'altra voce: il passo e' per classe");

            SessionMetadata m = SessionYamlParser.Parse(Yaml);
            m.PlayerCarClass = "GT3";

            var a = SessionMetadataDump.ToRecord(m, "2026-09-05T10:00:00Z");
            var b = SessionMetadataDump.ToRecord(m, "2026-09-05T18:30:00Z");

            Assert(SessionMetadataDump.NeedsWrite(null, a), "una voce assente va sempre scritta");
            Assert(!SessionMetadataDump.NeedsWrite(a, b),
                   "stesso contenuto a ore diverse non deve riscrivere: il confronto ignora l'orario");

            m.IncidentLimit = 25;
            var c = SessionMetadataDump.ToRecord(m, "2026-09-05T19:00:00Z");
            Assert(SessionMetadataDump.NeedsWrite(a, c),
                   "un contenuto davvero diverso deve aggiornare la voce");

            Assert(SessionMetadataDump.ResolveFolder("   ").Length > 0,
                   "una cartella vuota deve ricadere accanto alla DLL, non restare vuota");
            Assert(SessionMetadataDump.ResolveFolder(@"C:\Qualcosa") == @"C:\Qualcosa",
                   "una cartella configurata va rispettata");
            Assert(SessionMetadataDump.SafeFileName("Road Atlanta | GT3").IndexOf('|') < 0,
                   "il nome del file non deve contenere caratteri illegali");

            Pass("il dump si deduplica per traccia+classe e ignora l'orario");
        }

        private static void Test_SessionDataReader_ParsePercentage()
        {
            Assert(SessionDataReader.ParsePercentage(null) == null, "null da' null");
            Assert(SessionDataReader.ParsePercentage("") == null, "stringa vuota da' null");
            Assert(Math.Abs(SessionDataReader.ParsePercentage(0.5).Value - 0.5) < 1e-9, "double 0.5");
            Assert(Math.Abs(SessionDataReader.ParsePercentage(100.0).Value - 1.0) < 1e-9, "double 100.0 diventa 1.0");
            Assert(Math.Abs(SessionDataReader.ParsePercentage("0.500 %").Value - 0.5) < 1e-9, "0.500 % diventa 0.5");
            Assert(Math.Abs(SessionDataReader.ParsePercentage("1.000 %").Value - 1.0) < 1e-9, "1.000 % diventa 1.0");
            Assert(Math.Abs(SessionDataReader.ParsePercentage("50 %").Value - 0.5) < 1e-9, "50 % diventa 0.5");
            Assert(Math.Abs(SessionDataReader.ParsePercentage("100 %").Value - 1.0) < 1e-9, "100 % diventa 1.0");
            Pass("SessionDataReader.ParsePercentage gestisce sia scala 0-1 che 0-100");
        }

        private static void Test_SessionDataReader_SpeedKmh()
        {
            Assert(SessionDataReader.SpeedKmh(null) == null, "null da' null");
            Assert(SessionDataReader.SpeedKmh("") == null, "stringa vuota da' null");
            Assert(SessionDataReader.SpeedKmh("0 kmh") == null, "zero da' null");
            Assert(Math.Abs(SessionDataReader.SpeedKmh("60.00 kph").Value - 60.0) < 1e-9, "60.00 kph");
            Assert(Math.Abs(SessionDataReader.SpeedKmh("37.28 mph").Value - 60.0) < 0.05, "37.28 mph convertito in kmh");
            Pass("SessionDataReader.SpeedKmh converte correttamente velocita' con unita'");
        }

        private static void Test_SessionDataReader_ReadFromRawObject()
        {
            var mockRaw = new
            {
                SessionData = new
                {
                    WeekendInfo = new
                    {
                        TrackDisplayName = "Road Atlanta",
                        TrackPitSpeedLimit = "60.00 kph",
                        WeekendOptions = new
                        {
                            StandingStart = 1L,
                            IncidentLimit = 17L,
                            FastRepairsLimit = 1L
                        }
                    },
                    DriverInfo = new
                    {
                        DriverCarIdx = 7L,
                        DriverCarEstLapTime = 76.524,
                        DriverPitTrkPct = 0.029294,
                        DriverCarFuelKgPerLtr = 0.78,
                        DriverCarFuelMaxLtr = 62.0,
                        Drivers = new object[]
                        {
                            new
                            {
                                CarIdx = 3L,
                                UserName = "Sven Neiss",
                                CarClassShortName = "GTP",
                                CarClassEstLapTime = 68.443,
                                CarClassMaxFuelPct = "1.000 %",
                                CarClassDryTireSetLimit = 3L
                            },
                            new
                            {
                                CarIdx = 7L,
                                UserName = "Sara Tolotti",
                                CarClassShortName = "GT3",
                                CarClassEstLapTime = 76.524,
                                CarClassMaxFuelPct = "0.500 %"
                            }
                        }
                    }
                }
            };

            SessionMetadata m = SessionDataReader.ReadFromRawObject(mockRaw);
            Assert(m.IsPopulated, "oggetto raw interpretato con successo");
            Assert(m.SourceName == SessionDataReader.SourceLabelObject, "fonte impostata su oggetto nativo");
            Assert(m.TrackName == "Road Atlanta", "TrackName estratto");
            Assert(m.IncidentLimit == 17, "IncidentLimit estratto");
            Assert(m.FastRepairsAvailable == 1, "FastRepairsAvailable estratto");
            Assert(m.IsStandingStart == true, "IsStandingStart estratto");
            Assert(m.DryTireSetLimit == 3, "DryTireSetLimit estratto");
            Assert(m.PlayerPitStallPct.HasValue && Math.Abs(m.PlayerPitStallPct.Value - 0.029294) < 1e-9, "PlayerPitStallPct");
            Assert(m.FuelDensityKgPerLitre.HasValue && Math.Abs(m.FuelDensityKgPerLitre.Value - 0.78) < 1e-9, "FuelDensityKgPerLitre");
            Assert(m.PlayerMaxFuelLitres.HasValue && Math.Abs(m.PlayerMaxFuelLitres.Value - 62.0) < 1e-9, "PlayerMaxFuelLitres");
            Assert(m.PlayerEstimatedPaceSec.HasValue && Math.Abs(m.PlayerEstimatedPaceSec.Value - 76.524) < 1e-9, "PlayerEstimatedPaceSec");
            Assert(m.DriverEstimatedPaceSec.Count == 2, "2 piloti estratti");
            Assert(Math.Abs(m.DriverEstimatedPaceSec["Sven Neiss"] - 68.443) < 1e-9, "passo Sven Neiss");
            Assert(Math.Abs(m.DriverEstimatedPaceSec["Sara Tolotti"] - 76.524) < 1e-9, "passo Sara Tolotti");
            Assert(m.ClassEstimatedPaceSec.Count == 2, "2 classi estratte");
            Assert(Math.Abs(m.ClassEstimatedPaceSec["GTP"] - 68.443) < 1e-9, "passo classe GTP");
            Assert(Math.Abs(m.ClassEstimatedPaceSec["GT3"] - 76.524) < 1e-9, "passo classe GT3");
            Assert(Math.Abs(m.DriverMaxFuelPct["Sara Tolotti"] - 0.5) < 1e-9, "BoP Sara Tolotti");
            Assert(Math.Abs(m.DriverMaxFuelPct["Sven Neiss"] - 1.0) < 1e-9, "BoP Sven Neiss");

            Pass("SessionDataReader.ReadFromRawObject estrae tutti i campi dall'oggetto nativo iRacingSDK");
        }
    }
}
