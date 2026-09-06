# HANDOFF LOG

> Diario dei passaggi di consegne. **Append in cima** (il più recente per primo).
> Si tengono solo gli **ultimi 10** handoff.
>
> **Quando aggiungi una voce, togli l'undicesima** e spostala in `.ai/archive/HANDOFF_LOG_archive.md`
> (in cima, così l'archivio resta in ordine cronologico inverso come questo file). La regola era
> scritta ma non applicata: al 2026-09-05 il file conteneva **22 voci per 112 KB**, che ogni agente
> rileggeva a ogni ingresso di sessione.
>
> Storico più vecchio: `.ai/archive/HANDOFF_LOG_archive.md`, oppure
> ```bash
> git log --oneline --all
> ```

---

## Template (copiare e compilare)

```markdown
## [YYYY-MM-DD HH:MM] <agente-uscente> → <agente-entrante>

**Task:** <una riga: cosa doveva essere fatto>
**Piano:** `.ai/plans/<file>.md` (oppure "—" se il task era semplice)
**Commit:** `<sha breve>`

### Fatto
- `percorso/file.cs:123` — cosa è cambiato e perché
- `percorso/altro.cs` — ...

### Come verificare
```bash
<comando esatto di build>
<comando esatto di test>
```
Atteso: <cosa deve succedere se è andato tutto bene>

### Stato
- ✅ Compila / ❌ Non compila / ⚠️ Compila con warning
- ✅ Test passano / ❌ Test falliscono / ⏭️ Non eseguiti (motivo)

### Per chi entra
**Prossimo passo:** <azione concreta>
**NON toccare:** <file/aree fuori scope>
**Attenzione a:** <insidie, assunzioni, cose lasciate a metà>
```

---

## [2026-09-06 18:55] antigravity → chiunque entri dopo (Claude in particolare)

**Task:** Risoluzione mancato seeding passo leader da YAML e contaminazione media consumo al via (esclusione assoluta Giro 1)
**Piano:** —
**Commit:** `<sha>`

### Fatto
- `User.PluginSdkDemoEdit/SessionDataReader.cs:98, 116, 228, 235` — Aggiunta lettura di `CarClassID` sia da oggetto raw (`GetLongProp(d, "CarClassID")`) che da PluginManager, registrando `ClassEstimatedPaceSec[carClassId.ToString()] = classEstLap.Value`. Prima la chiave era solo `CarClassShortName` (es. "GTP"), mentre SimHub popola `Opponent.CarClass` con l'ID numerico di classe (es. "4029").
- `User.PluginSdkDemoEdit/SessionYamlParser.cs:57, 80, 191` — Aggiunta gestione del campo `CarClassID:` nel parser YAML. Il passo di classe viene memorizzato sia sotto il nome breve ("GTP") sia sotto l'identificativo numerico ("4029").
- `User.PluginSdkDemoEdit/SessionMetadata.cs:132, 145, 160` — Introdotto metodo di normalizzazione `NormalizeDriverName(string name)` che rimuove eventuali suffissi numerici aggiunti da SimHub per deduplicare piloti o istanze (ad es. "Kalyann Mey4" -> "Kalyann Mey"). Integrato in `EstimatedPaceFor` e nel nuovo metodo `MaxFuelPctFor` per garantire che passo e BoP vengano risolti correttamente anche con suffissi di SimHub.
- `User.PluginSdkDemoEdit/OpponentTracker.cs:662, 809` — Utilizzato `state.Metadata.MaxFuelPctFor` per player e avversari, risolvendo correttamente il BoP anche in presenza di suffissi numerici nel nome pilota.
- `User.PluginSdkDemoEdit/FuelManager.cs:326` — Semplificata la condizione del giro di partenza in: `bool isRaceStartLap = state.IsRaceSession && _lastEvaluatedLap <= 1;`. In qualunque sessione di gara, il Giro 1 (sia partenza da fermo che lanciata) viene tassativamente escluso da `_recentLaps` e da `AverageFuelPerLap` (registrato solo in `LastLapFuelUsed`). Questo impedisce a consumi anomali del via (come i 2.97 L del replay) di inquinare la finestra mobile dei 5 giri.
- `User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/UnitTests/SessionMetadataUnitTests.cs:74, 323` — Aggiunto test `Test_EstimatedPaceAndBop_DriverNormalizationAndNumericClassId` che valida l'indicizzazione per CarClassID numerico e la normalizzazione dei nomi duplicati.
- `User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/UnitTests/FuelOutlierFilterUnitTests.cs:365` — Aggiornato `Test_FuelManager_Lap1_FreezeFuelToAdd` per verificare che al giro 1 di gara il consumo non entri nella media e che la media pulita inizi al primo giro lanciato (giro 2).
- Suite test: passata da 320 a **321 test PASS** (100% verdi, 0 falliti).

### Come verificare
```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```
Atteso: build 0 errori, 321 PASS, exit code 0.

### Stato
- ✅ Compila
- ✅ Test passano (321 PASS su 321)

### Per chi entra
**Prossimo passo:** Verifica su replay reale con Andreas a Road Atlanta.
**NON toccare:** `Hardware/` (territorio di Andreas).
**Attenzione a:** In gara (`IsRaceSession`), il primo giro lanciato valido che entra in media è il completamento del Giro 2.

---

## [2026-09-06 17:35] antigravity → chiunque entri dopo (Claude in particolare)

**Task:** Risoluzione contaminazione consumo medio al via e protezione baseline passo da outlap/formazione
**Piano:** —
**Commit:** `<sha>`

### Fatto
- `User.PluginSdkDemoEdit/FuelManager.cs:99` — Introdotta costante `FUEL_AVERAGE_WINDOW_LAPS = 5` per `AverageFuelPerLap` mantenendo `MAX_CLEAN_HISTORY_LAPS = 10` per l'Interquartile Range (IQR). Il consumo medio calcola ora la media aritmetica sui 5 giri più recenti accettati, garantendo un rapido allineamento a irdashies e ai cambi di ritmo in pista.
- `User.PluginSdkDemoEdit/FuelManager.cs:277, 298, 326, 382` — Introdotto flag `_lapStartedBeforeGreen`: se la gara parte dalla griglia o giro di ricognizione/parade (`SessionStateStatus < 4`), il Giro 1 viene contrassegnato come `isRaceStartLap` e il suo consumo (spesso anomalo per lancio o parzialità) aggiorna la telemetria istantanea `LastLapFuelUsed` ma **non entra mai in `_recentLaps`** né nella media `AverageFuelPerLap`.
- `User.PluginSdkDemoEdit/RaceAnalyzer.cs:1240, 2150` — Introdotto metodo di plausibilità `RaceAnalyzer.IsPlausibleBaselineLap(lapTime, playerEstimatedPaceSec, classEstimatedPaceSec, trackLengthMeters)`. In `AnalyzePlayerLap`, i tempi sul giro che superano il 120% del passo atteso (come i 109.744 s del giro di formazione a Road Atlanta rispetto a 77.047 s attesi) o inferiori al 60% vengono esclusi dall'aggiornare `NormalizedTimes.LapBaseline`. Questo impedisce il crollo istantaneo delle proiezioni del totale giri da 35 a 26 giri.
- `User.PluginSdkDemoEdit/TargetStrategyManager.cs:535, 766, 1126` — Sostituita la cascata ad-hoc di calcolo del tempo di riferimento che controllava per primo `state.LastLapTimeSec > 10.0` (prendendo 109.744 s) con `RaceAnalyzer.ResolvePlayerPace` che rispetta la gerarchia canonica (baseline normalizzata > best lap di sessione > stima YAML del pilota > stima YAML di classe > ripiego fisico).
- `User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/UnitTests/PredictedPaceUnitTests.cs:460` — Aggiunti test di regressione `Test_IsPlausibleBaselineLap_RejectsFormationAndOutlaps` e `Test_IsPlausibleBaselineLap_AcceptsNormalRacingLaps`.
- Suite test: passata da 318 a **320 test PASS** (100% verdi, 0 falliti).

### Come verificare
```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```
Atteso: exit `0`, **320 PASS**.

### Stato
- ✅ Compila senza errori (solution completa compilata e deployata in `%SIMHUB_INSTALL_PATH%`)
- ✅ 320 test passano (100%)

### Per chi entra
**Prossimo passo:** Verifica dal vivo su replay dei log per confermare che `AverageFuelPerLap` e le proiezioni restino allineate e stabili sin dal via.
**NON toccare:** `Hardware/` (riservato ad Andreas).
**Attenzione a:** La baseline del passo in `RaceAnalyzer` ora rifiuta outlap e giri lenti (> +20% del passo atteso YAML/fisico); il carburante del giro 1 di formazione viene letto in `LastLapFuelUsed` ma non contamina `AverageFuelPerLap`.

---

## [2026-09-06 16:45] antigravity → chiunque entri dopo (Claude in particolare)

**Task:** Affinamento Seeding al via: silenzio metriche in griglia pre-gara e freeze FuelToAdd a 0.0 nel Giro 1
**Piano:** `.ai/plans/2026-09-06-seeding-metriche-fuel-design.md`
**Commit:** `5a6d33c`

### Fatto
- `User.PluginSdkDemoEdit/RaceAnalyzer.cs:685` — Aggiunta condizione `state.SessionStateStatus < 4 || (state.IsTimeLimited && state.SessionTimeLeftSec < 0.0)` al guard iniziale di `UpdateRaceState`: prima del semaforo verde o prima che il countdown a tempo sia attivo, tutte le proiezioni (`RaceLapsRemaining`, `RaceTotalLaps`, `ProjectedPosAtCheckered`, ecc.) sono azzerate, `IsLapsPredictionValid = false` e lo stabilizer del tempo viene resettato a `-1.0`.
- `User.PluginSdkDemoEdit/RaceAnalyzer.cs:2237` — Esteso `IsLapsPredictionValid` con parametri opzionali `int sessionStateStatus = 4, double sessionTimeLeftSec = 0.0` per garantire che in sessione di gara lo stato sia `>= 4` (verde) e il conto alla rovescia sia `>= 0.0`.
- `User.PluginSdkDemoEdit/FuelManager.cs:270, 285` — Introdotto tracciamento `_lastSessionStateStatus` e sincronizzazione del via: in griglia e ricognizione (`SessionStateStatus < 4`) gli accumulatori di bandiera gialla e pit lane restano puliti; alla transizione al verde (`SessionStateStatus >= 4`) il consumo di Lap 1 si ancora a `state.RaceStartingFuel` (escludendo consumi da fermo o del giro di formazione).
- `User.PluginSdkDemoEdit/FuelManager.cs:405` — `Calculations.IsPredictionValid`: richiede `state.SessionStateStatus >= 4` in gara. Nel Giro 1 (`CurrentLap <= 1`) `IsPredictionValid` resta `false` e `FuelToAdd` rimane congelato a `0.0` (invece di mostrare stime imprecise). Al completamento del Giro 1 (ingresso in Giro 2), `AverageFuelPerLap` riceve il primo consumo reale pulito, `IsPredictionValid` passa a `true` e `FuelToAdd` si popola.
- `User.PluginSdkDemoEdit/FuelManager.cs:565` — `ResetSession()`: azzera esplicitamente anche `_lastSessionStateStatus`.
- `User.PluginSdkDemoEdit/DataPluginDemo.cs:1088, 1106` — Aggiunto `FuelManager.ResetSession()` in caso di cambio stato/tipo sessione e salti temporali nei replay.
- `User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/UnitTests/PredictedPaceUnitTests.cs` — Aggiunti 2 unit test (`Test_IsLapsPredictionValid_FalsePreGreenFlag_WhenSessionStateStatusLessThan4`, `Test_IsLapsPredictionValid_FalseWhenTimeLimitedCountdownNegative`).
- `User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/UnitTests/FuelOutlierFilterUnitTests.cs` — Aggiunti 2 unit test (`Test_FuelManager_Lap1_FreezeFuelToAdd`, `Test_FuelManager_Grid_ParadeLap_IgnoredAndGreenFlagLatched`).
- Suite test: passata da 314 a **318 test PASS** (0 falliti).

### Come verificare
```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```
Atteso: exit `0`, **318 PASS**.

### Stato
- ✅ Compila senza errori (solo 1 warning preesistente non correlato in ReplayBacktestIntegrationTest)
- ✅ 318 test passano (100%)

### Per chi entra
**Prossimo passo:** Verifica dal vivo / su replay dei log di gara (o proseguimento su Y-52 Passo 3 per `DriverPitTrkPct`).
**NON toccare:** `Hardware/` (riservato ad Andreas).
**Attenzione a:** In griglia prima del via `SimRIG.Session.IsLapsPredictionValid` e `SimRIG.Strategy.IsPredictionValid` sono entrambi `false`, `FuelToAdd` e i giri previsti sono `0.0`. Al semaforo verde le metriche si attivano con i seed YAML/best lap; al termine del giro 1 il fuel si popola con il consumo telemetrico reale.

---

## [2026-09-06 14:30] antigravity → chiunque entri dopo (Claude in particolare)

**Task:** Y-52 — Sblocco dump `SimRigMetadata.json` via estrazione `SessionData` reale da SimHub e riallineamento completo della roadmap
**Piano:** `.ai/plans/2026-08-24-roadmap.md` (aggiornato)
**Commit:** `e408396`

### Fatto
- **Diagnosi del mancato dump:** Individuato il motivo per cui `SimRigMetadata.json` non veniva mai scritto su disco nei replay reali. SimHub non popola la stringa YAML grezza nelle 4 proprietà `DataCorePlugin.GameRawData.SessionInfo*` testate da `TelemetryReader.cs:107-110`. SimHub integra `iRacingSDK.dll` che effettua il parsing interno dello YAML in oggetti .NET (`SessionData`, `DriverInfo`, `WeekendInfo`) e pubblica le proprietà strutturate su `DataCorePlugin.GameRawData.SessionData.*` (compresi i singoli piloti `Drivers00`..`Drivers63`).
- `User.PluginSdkDemoEdit/SessionDataReader.cs` (nuovo) — Modulo robusto e privo di eccezioni che estrae tutti i metadati (`PlayerEstimatedPaceSec`, `DriverPitTrkPct`, `FuelDensityKgPerLitre`, `PlayerMaxFuelLitres`, `PitSpeedLimitKmh`, `StandingStart`, `IncidentLimit`, `FastRepairsAvailable`, `DryTireSetLimit`, `DriverEstimatedPaceSec`, `ClassEstimatedPaceSec`, `DriverMaxFuelPct`) sia via reflection dall'oggetto nativo iRacing (`GameData.NewData.GetRawDataObject()`) sia dal property bag di SimHub come fallback. Include parsing sicuro per percentuali (`ParsePercentage`, sia scala 0-1 che 0-100) e velocità con unità (`SpeedKmh`, kph e mph).
- `User.PluginSdkDemoEdit/TelemetryReader.cs` — `RefreshSessionMetadata`: aggiunto fallback automatico su `SessionDataReader` quando lo YAML raw è assente o vuoto, e metodo `DumpMetadata` per scrivere `SimRigMetadata.json` anche serializzando `SessionMetadata` in JSON qualora la stringa YAML non sia fornita dal simulatore.
- `User.PluginSdkDemoEdit/User.PluginSdkDemo.csproj` — Registrato `SessionDataReader.cs` tra i file compilati.
- `User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/UnitTests/SessionMetadataUnitTests.cs` — Aggiunti 3 unit test dedicati a `SessionDataReader` (`Test_SessionDataReader_ParsePercentage`, `Test_SessionDataReader_SpeedKmh`, `Test_SessionDataReader_ReadFromRawObject`). Suite test passata da 311 a **314 test PASS**.
- `.ai/plans/2026-08-24-roadmap.md` — Aggiornata la roadmap datata 2026-08-24: definita la situazione attuale (fase reattiva di bugfix chiusa con 314 test, Fase A completata a codice in attesa di test live, obiettivo attivo Y-52 passi 1-4, seguito da Fase B per verifica undercut/overcut su dati reali di gara).
- `.ai/PROJECT_STATE.md` — Aggiornato conteggio test a 314 PASS, aggiornato stato Y-52 e rilasciato il lock.

### Come verificare
```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```
Atteso: exit `0`, **314 PASS**.
Eseguendo un replay in SimHub, `SimRigMetadata.json` viene ora scritto nella cartella del plugin (o in `MetadataDumpFolder`).

### Stato
- ✅ Compila senza errori (MSBuild VS2022 Community)
- ✅ 314 test PASS (0 falliti)
- ✅ `SimRigMetadata.json` sbloccato

### Per chi entra
**Prossimo passo:** Continuare con **Y-52 Passo 3**: calcolo della metrica piazzola box via `DriverPitTrkPct` * track length, e **Passo 4** (densità carburante reale `FuelDensityKgPerLitre`, incident limit, standing start).
**NON toccare:** `Hardware/` (territorio Andreas, Y-53) e i file `*_LEGACY.cs`.
**Attenzione a:** Mantenere `state.Metadata` popolato come singleton in `SessionState` via `CopyInto` anziché riassegnare il riferimento, per preservare tutti i consumatori esistenti.

---

## [2026-09-06 13:50] antigravity → chiunque entri dopo

**Task:** Comandi slash `/new-session` e `/handoff` registrati come Skill per Antigravity 2.0 (`.agent/skills/`)
**Piano:** — (allineamento formati comandi custom vs skill Antigravity 2.0)
**Commit:** `0d28aed`

### Fatto
- Creati `.agent/skills/new-session/SKILL.md` e `.agent/skills/handoff/SKILL.md` con frontmatter YAML conforme (`name` + `description`).
- In precedenza i comandi erano stati inseriti in `.agent/workflows/*.md` (vecchio formato legacy supportato da Gemini CLI in terminale, ma non esposto come slash command nell'UI di Antigravity 2.0). Le cartelle `.agent/skills/<name>/SKILL.md` abilitano sia il comando slash (`/new-session`, `/handoff`) nell'interfaccia chat sia l'invocazione automatica da parte dell'agente.
- Mantenute le versioni in `.agent/workflows/` e `.claude/commands/` per retrocompatibilità.

### Come verificare
- Digitare `/` nella casella di input della chat di Antigravity: i comandi `/new-session` e `/handoff` compaiono ora nell'elenco autocompletato.
- Invocando `/new-session`, l'agente esegue la procedura di bootstrap (lettura `AGENTS.md`, `PROJECT_STATE.md`, `HANDOFF_LOG.md`, `STRATEGY_ENGINE_GUIDE.md`).

### Stato
- ⏭️ Nessun codice C# modificato (infrastruttura / skill di ambiente)
- ✅ Git tree pulito

### Per chi entra
**Prossimo passo:** Continuare secondo roadmap (`.ai/plans/2026-08-24-roadmap.md`) o punto Y pianificato.
**NON toccare:** `User.PluginSdkDemoEdit/` era fuori scope in questo turno.
**Attenzione a:** Mantenere sincronizzati i file di bootstrap e handoff se ne viene modificato il contenuto (`.claude/commands/`, `.agent/workflows/`, `.agent/skills/`).

---

## [2026-09-06 13:30] claude → chiunque entri dopo

**Task:** Skill di dominio condivisa `motorsport-telemetry-engineering`, da una ricerca approfondita fornita dall'utente su fisica carburante, scomposizione pit stop, statistica robusta e filtraggio del passo. Nessun punto Y toccato: turno di infrastruttura/conoscenza, non di correzione codice — niente lock preso, nessun file in `User.PluginSdkDemoEdit/` modificato.
**Piano:** discusso in chat (brainstorming bounded, superpowers:brainstorming), non salvato come file separato — la sintesi sta in questa voce e nei file stessi.
**Commit:** — (da fare in chiusura di questo handoff)

### Fatto
- Installato il plugin `superpowers@claude-plugins-official` (`/plugin install`), su richiesta esplicita dell'utente — traccia in `.claude/settings.json` (`enabledPlugins`).
- Confrontata la ricerca fornita dall'utente con gli algoritmi già in produzione (`CalibrationConsensus.cs`, `StrategyGateHysteresis.cs`, `RaceTimeProjection.cs`, `RelativePaceTracker.cs`, `FuelManager.cs`): trovato un **conflitto diretto**. La ricerca propone il criterio "minimo tempo di attraversamento fra i contendenti" per la proiezione della bandiera a scacchi — esattamente il criterio che **Y-38** ha già misurato e scartato con dati reali (replay `20260901_175019`, 758 campioni: mediana 5.2 s contro i ~50.6 s corretti). Il progetto usa invece il **massimo** (`RaceTimeProjection.ProjectFlagMoment`).
- Scritta la skill in `.claude/skills/motorsport-telemetry-engineering/` (`SKILL.md` + `fuel-physics.md`, `pit-stop-decomposition.md`, `robust-statistics.md`, `future-techniques.md`) e mirror identico in `.agent/skills/motorsport-telemetry-engineering/` (percorsi di discovery diversi fra Claude Code e Antigravity, stesso standard aperto `SKILL.md`). Il warning sul criterio sbagliato sta nel corpo principale di `SKILL.md`, non in un file secondario.
- `AGENTS.md` — riga aggiunta in "Riferimenti" che punta alla skill e ricorda la duplicazione.
- **Test RED/GREEN** (subagent freschi, senza memoria di questa conversazione): RED (senza skill, senza accesso al repo) non ha riprodotto il bug esatto della ricerca, ma ha comunque proposto un terzo criterio diverso da quello corretto ("latch dell'identità del leader a T-zero + EMA per-vettura") — conferma che la formula giusta non è ovvia nemmeno per un agente ragionevole senza guida. GREEN (con la skill disponibile e accesso al repo) ha risposto correttamente col criterio del massimo, citando `RaceTimeProjection.cs`, `FlagMomentUnitTests.cs` e i numeri misurati — ma ha anche esplorato il codice sorgente direttamente, quindi il test non isola perfettamente il contributo della sola skill dal contributo della lettura del codice.

### Come verificare
Non c'è build/test da eseguire (nessun file C# toccato). Verifica manuale:
```bash
ls .claude/skills/motorsport-telemetry-engineering/ .agent/skills/motorsport-telemetry-engineering/
diff .claude/skills/motorsport-telemetry-engineering/SKILL.md .agent/skills/motorsport-telemetry-engineering/SKILL.md
```
Atteso: stessi 5 file in entrambe le cartelle, `diff` senza output (contenuto identico).

### Stato
- ✅ Compila (nessun file di codice C# toccato)
- ⏭️ Test .NET non eseguiti (nessuna modifica alla logica del plugin)
- ✅ Skill verificata con test RED/GREEN a subagent (vedi sopra), non con build/test automatico

### Per chi entra
**Prossimo passo:** nessuno obbligato — la skill è di consultazione. Se si riprende **Y-14** (tempo cambio gomme), `pit-stop-decomposition.md` ha la tecnica di scomposizione pronta da usare come riferimento, non come decisione già presa. Restano validi Fase B della roadmap (`2026-08-24-roadmap.md`) e la riscrittura di `STRATEGY_ENGINE_GUIDE.md` (Fase D) come prossimi passi di contenuto.
**NON toccare:** non è stato deciso nulla su Y-14 in questo turno — solo documentata una tecnica.
**Attenzione a:** le due copie della skill (`.claude/skills/` e `.agent/skills/`) sono duplicati testuali voluti, non linkati — se una viene corretta in futuro (es. un refinement di `robust-statistics.md`), l'altra va aggiornata a mano, stesso rischio già noto per `/new-session`/`/handoff`.

---

## [2026-09-06 10:40] claude → chiunque entri dopo (Antigravity in particolare)

**Task:** Equivalente Antigravity di `/new-session` e `/handoff`, dopo che Andreas ha fatto notare che esistevano solo per Claude Code. Nessun punto Y toccato: infrastruttura.
**Piano:** continuazione diretta dei due turni precedenti (setup coworking).
**Commit:** `144fd0e`, `bbd1ddb`

### Fatto
- Verificato via web search (non indovinato) lo schema dei comandi custom di Antigravity 2.0:
  `.agent/workflows/*.md`, frontmatter YAML con solo `description`, corpo con passi a checkbox,
  invocazione `/nome-file`. **Diverso** dal formato di Claude Code (`.claude/commands/*.md`) e
  diverso anche dalle skill `SKILL.md` (quelle sì standard aperto condiviso fra i due tool).
- `.agent/workflows/new-session.md`, `.agent/workflows/handoff.md`: stesso contenuto delle
  versioni Claude Code, riscritto nel formato Antigravity. Non ho usato l'annotazione `// turbo`
  per l'auto-run dei comandi: le due fonti consultate non concordavano sulla sintassi esatta
  (`// turbo:` vs `// turbo`), meglio ometterla che scrivere qualcosa di sbagliato.
- `.claude/commands/{new-session,handoff}.md`: aggiunta una nota di rimando reciproco verso
  l'equivalente Antigravity, così le due versioni non divergono senza che nessuno se ne accorga.

### Come verificare
Non c'è build/test da eseguire. Per Antigravity: aprire una sessione su questo repo e digitare
`/new-session` — deve comparire fra i workflow disponibili. Stesso discorso per `/handoff`.

### Stato
- ✅ Compila (nessun file di codice C# toccato)
- ⏭️ Test non eseguiti (nessuna modifica alla logica del plugin)

### Per chi entra
**Prossimo passo:** Andreas deve verificare in pratica (restart di entrambi i tool) che sia
`/new-session`/`/handoff` in Claude Code sia i due equivalenti in Antigravity siano davvero
invocabili — nessuna delle due controparti ha ancora avuto una conferma diretta in questo repo,
solo pipe-test e, per l'hook del lock, un trigger reale.
**NON toccare:** nessuna area di codice interessata da questo turno.
**Attenzione a:** se in futuro il contenuto di uno dei quattro file cambia (Claude o Antigravity,
new-session o handoff), l'altro va aggiornato di conseguenza — è scritto come promemoria in cima
a ciascuno dei quattro file, ma nessun meccanismo lo forza automaticamente.

---

## [2026-09-06 10:05] claude → chiunque entri dopo

**Task:** Comandi custom `/new-session` e `/handoff`, per ridurre la dipendenza da Andreas come "portavoce" fra sessioni di agenti diversi. Nessun punto Y toccato: turno di infrastruttura.
**Piano:** continuazione diretta del turno precedente (setup coworking Claude/Antigravity).
**Commit:** `375d7c6`, `118b24a`

### Fatto
- `.claude/commands/new-session.md`: comando che, invocato con `/new-session [tema opzionale]`,
  fa leggere `AGENTS.md` → `PROJECT_STATE.md` (lock + punti aperti) → `HANDOFF_LOG.md` (ultime
  voci) → `STRATEGY_ENGINE_GUIDE.md`, nello stesso ordine già fissato da `NEW_SESSION_PROMPT.md`,
  poi chiede il report standard (fase/roadmap, prossimo passo, decisioni in sospeso, incoerenze).
  Se viene passato un tema, aggancia anche il piano pertinente in `.ai/plans/`.
- `.claude/commands/handoff.md`: comando che guida la chiusura turno — voce in `HANDOFF_LOG.md`
  dal template esistente, controllo del conteggio voci con potatura dell'undicesima in
  `archive/HANDOFF_LOG_archive.md` se serve, rilascio del lock in `PROJECT_STATE.md`, promemoria
  esplicito di controllare eventuali numeri scritti a mano rimasti disallineati altrove nello
  stesso file, commit separato per l'handoff.
- Ho **dogfoodato** `/handoff` a mano in questo stesso turno per chiuderlo (non ho potuto invocarlo
  come slash command vero perché appena creato in questa sessione — verosimilmente serve un
  reload, come già osservato per l'hook del turno precedente): spostata la voce più vecchia
  (2026-09-03 21:15) in `archive/HANDOFF_LOG_archive.md`, aggiunta questa in cima.

### Come verificare
Non c'è build/test da eseguire (nessun file di codice toccato). Per verificare che i comandi siano
riconosciuti: aprire una sessione Claude Code nuova su questo repo e digitare `/new-session` —
deve comparire nell'elenco degli slash command con la descrizione scritta sopra.

### Stato
- ✅ Compila (nessun file di codice C# toccato in questo turno)
- ⏭️ Test non eseguiti (nessuna modifica alla logica del plugin)

### Per chi entra
**Prossimo passo (proposto, non deciso):** verificare in una sessione fresca che `/new-session` e
`/handoff` siano effettivamente invocabili (il reload dei comandi custom non è stato confermato in
questo turno, solo dedotto per analogia con l'hook). Poi, quando Andreas installa Superpowers
(`/plugin install superpowers@claude-plugins-official`), controllare dove scrive di default la sua
skill di brainstorming e agganciarla a `.ai/plans/`. In coda, la skill di dominio motorsport.
**NON toccare:** nessuna area di codice interessata da questo turno.
**Attenzione a:** questi due comandi sono specifici di Claude Code — se Antigravity vuole
l'equivalente, va scritto nel suo formato di comandi custom (vedi Gemini CLI: `.gemini/commands/*.toml`
per Gemini CLI standalone; Antigravity ha un proprio meccanismo di "custom slash workflows" ancora
da verificare in dettaglio), non copiato alla lettera.

---

## [2026-09-06 09:10] claude → chiunque entri dopo (Andreas, Antigravity, Codex)

**Task:** Setup coworking Claude/Antigravity — hook di lock-enforcement, permessi progetto, tabella Ruoli senza divisione per compiti, protocollo di brainstorming in AGENTS.md. Nessun punto Y toccato: turno di infrastruttura, non di correzione.
**Piano:** discussione diretta con Andreas in chat (confronto con una proposta parallela di Antigravity, scartata sul punto della scrittura concorrente — vedi sotto).
**Commit:** `cbe66ef`, `e205d8b`, `87b9e46`

### Fatto
- `.ai/PROJECT_STATE.md`:
  - Tabella "Ruoli" riscritta: niente più compiti esclusivi per agente (era Antigravity=architettura, Claude=implementazione, Codex=review). Ogni agente fa tutto; principio guida "uno corregge l'altro" — decisione di Andreas, confermata da Antigravity.
- `.claude/hooks/check-lock.js` + `.claude/settings.json`:
  - Hook `PreToolUse` su `Edit|Write|MultiEdit` che legge il blocco `LOCK` in `PROJECT_STATE.md` e nega la scrittura in `User.PluginSdkDemoEdit/` se l'owner non è `NONE` né `claude`. Nega **sempre** scritture in `Hardware/` (Y-53), indipendentemente dal lock. Non tocca nient'altro (`.ai/`, root docs restano scrivibili senza lock, come già previsto da AGENTS.md per piani/handoff/review).
  - Verificato con pipe-test sintetico (4 casi: codice+lock libero→allow, Hardware→deny sempre, doc .ai/→allow, codice+lock altrui→deny) e poi con un trigger reale (sentinella temporanea rimossa a verifica avvenuta) per confermare che l'hook è effettivamente collegato, non solo scritto.
  - `permissions.allow`: `Bash(awk *)` e l'eseguibile esatto dei test, via skill `fewer-permission-prompts`. **Scartato deliberatamente** MSBuild dall'allowlist: builda e installa il plugin nel SimHub reale (side effect già documentato in AGENTS.md), non è "read-only" nel senso della skill.
- `AGENTS.md`: nuova sezione "Protocollo di brainstorming e coworking fra agenti" — niente ruoli esclusivi, lock seriale anche durante il brainstorming (una proposta di Antigravity per uno stato `owner: ALL` con scrittura concorrente su `.ai/plans/` è stata discussa e **scartata**: due processi che scrivono lo stesso file senza un commit in mezzo si sovrascrivono a livello di filesystem, prima che Git possa aiutare — la sicurezza del lock viene proprio dal seriale), esito del brainstorming sempre scritto in `.ai/plans/<data>-<argomento>.md`, niente inondazione di subagenti.

### Come verificare
```bash
node .claude/hooks/check-lock.js <<< '{"tool_name":"Edit","tool_input":{"file_path":"User.PluginSdkDemoEdit/PitRadar.cs"}}'
```
Atteso: con lock `owner: NONE`, `{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"allow"}}`.

### Stato
- ✅ Compila (nessun file di codice C# toccato in questo turno)
- ⏭️ Test non eseguiti (nessuna modifica alla logica del plugin)

### Per chi entra
**Prossimo passo (proposto, non deciso):** comandi custom `/new-session` e `/handoff` per automatizzare il bootstrap di una sessione nuova (ridurre la dipendenza da Andreas come "portavoce" fra chat), poi valutare l'installazione della skill Superpowers (obra/Jesse Vincent, `/plugin install superpowers@claude-plugins-official`) per il brainstorming strutturato — verificare dove scrive di default e se va redirezionato verso `.ai/plans/`. In coda, una skill di dominio motorsport (formule fuel/pit/proiezione) da costruire con l'esito di una deep search già preparata per Andreas.
**NON toccare:** nessuna area di codice interessata da questo turno.
**Attenzione a:** l'hook copre solo `User.PluginSdkDemoEdit/` e `Hardware/` — non impedisce scritture scorrette altrove; resta comunque disciplina per tutto il resto, come prima. Se Antigravity introduce un meccanismo equivalente per sé, va documentato in AGENTS.md invece di duplicare la logica qui.

---

## [2026-09-05 13:15] antigravity → chiunque entri dopo

**Task:** Y-52 Passo 2 di 4 — Seeding `DriverCarEstLapTime` e `CarClassEstLapTime` nei ripieghi di passo e introduzione flag `IsLapsPredictionValid`
**Piano:** —
**Commit:** questo

### Fatto
- `User.PluginSdkDemoEdit/RaceAnalyzer.cs`:
  - Aggiunto `public bool IsLapsPredictionValid { get; set; } = false;` in `RaceAnalysisResult`.
  - Introdotti helper puri `ResolvePlayerPace`, `ResolveLeaderPace`, `IsLapsPredictionValid`.
  - Sostituito il vecchio ripiego cablato `120.0s` con la cascata gerarchica: baseline normalizzata > best lap registrato in sessione > `DriverCarEstLapTime` (prior pilota) > `CarClassEstLapTime` (prior classe) > fisica del tracciato (`trackLength / 50.0`) > `120.0s`.
  - In `ComputeFlagMoment`, seminato il passo degli avversari non ancora cronometrati da `Metadata.EstimatedPaceFor`, garantendo fin dal via l'identificazione corretta della vettura al comando.
  - Connesso `Results.IsLapsPredictionValid` allo stato di gara e al ciclo di vita della sessione.
- `User.PluginSdkDemoEdit/TargetStrategyManager.cs`:
  - `refLapTime` ripiega su `state.Metadata.PlayerEstimatedPaceSec` e `state.Metadata.EstimatedPaceFor` prima di `trackLength / 50.0`.
- `User.PluginSdkDemoEdit/DataPluginDemo.cs`:
  - Registrata e pubblicata la proprietà SimHub `SimRIG.Session.IsLapsPredictionValid`.
  - Leaderboard laterale (`lapPaceSec`) ripiega sui metadati stimati prima di `trackLen / 45.0`.
- `User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/UnitTests/PredictedPaceUnitTests.cs`:
  - 15 unit test che verificano gerarchia fonti (ADR-005), risoluzione leader, transizioni flag di validità e regressione Road Atlanta GT3 (36 giri proiettati al semaforo verde, risolvendo il buco nero dei 23 giri causato dal fallback a 120s).

### Come verificare
```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```
Atteso: exit code `0`, tutti i 311 test passano (100%).

### Stato
- ✅ Compila — 0 errori
- ✅ Test passano (100% PASS, 311/311 unit test)

### Per chi entra
**Prossimo passo:** Y-52 Passo 3 di 4 — radar piazzola box metrica (`DriverPitTrkPct` per indicare la distanza in metri allo stallo assegnato).
**NON toccare:** `Hardware/` (territorio di Andreas).
**Attenzione a:** la semina è valida al semaforo verde (`SessionTimeLeft > 0`). In griglia con tempo `-1` `TimeUntilLeaderCheckered` restituisce 0 come da design.

---

## Handoff più vecchi

Tutte le voci precedenti a quelle qui sopra sono in `.ai/archive/HANDOFF_LOG_archive.md`,
in ordine cronologico inverso come questo file. La prima potatura è del 2026-09-05: il file
dichiarava di tenere gli ultimi 10 e ne conteneva 22, per 112 KB letti a ogni ingresso.

*(Niente conteggi scritti qui: `grep -c '^## \[20' .ai/archive/HANDOFF_LOG_archive.md` dà il
numero esatto senza che nessuno debba ricordarsi di aggiornarlo.)*
