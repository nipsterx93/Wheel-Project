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

## [2026-09-10 12:10] antigravity → chiunque entri dopo

**Task:** Formattazione diagnostica e log completi di superficie e pit per Player e Target
**Piano:** —
**Commit:** `f432865`

### Fatto
- `User.PluginSdkDemoEdit/TargetStrategyManager.cs`:
  - Introdotti campi di tracking stato: `_lastLoggedPlayerSurface`, `_lastLoggedPlayerPitRoad`, `_lastLoggedTargetSurface`, `_lastLoggedTargetPitRoad` con reset in `ResetSession()`.
  - Aggiunto log di evento immediato (`LogModule.STRATEGY`, `LogType.EVENT`) ad ogni transizione di `TrackSurface` o `IsOnPitRoad` per Player e Target nel formato esatto:
    `Player: P1 | PosPct: 45.21% | Surface: OnTrack | InPitRoad: False | InPitStall : True | SurfaceCode : 3`
    `Target: P2 | PosPct: 43.80% | Surface: InPitStall | InPitRoad: True | InPitStall : True | SurfaceCode : 1`
  - Aggiornata la sezione `DRIVERS` del monitor periodico `[MERGE_GAP_MONITOR]` (file `..._MergeGap.log`) integrando la stessa riga completa per entrambi i piloti.
  - Aggiornato il log di transizione microsettori (`sectorChanged`) con la stringa di stato unificata.

### Come verificare
```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```
Atteso: 0 errori di compilazione, DLL copiata in SimHub, **343 PASS (100%)**.

### Stato
- ✅ Compila (0 errori, 1 warning CS0219 noto)
- ✅ 343 PASS (100%)

### Per chi entra
**Prossimo passo:** Collegamento delle nuove proprietà e telemetrie native (`TrackPositionPercent`, `IsInPitStall`, `TrackSurface`, `IsOnPitRoad`) alle logiche strategiche (Merge Gap fine-grained, stationary time, geofencing pit entry/exit).
**NON toccare:** Le formule di stationary time e pit loss senza test dedicati.
**Attenzione a:** Build con SimHub aperto fallisce con MSB3073 (DLL lockata da SimHub).

---

## [2026-09-10 11:35] antigravity → chiunque entri dopo

**Task:** Esposizione proprietà SimHub TrackPositionPercent per Player e Target
**Piano:** —
**Commit:** `7bccd62`

### Fatto
- `User.PluginSdkDemoEdit/OpponentTracker.cs:601-604`:
  - Aggiunto aggiornamento di `PlayerData.NativeLapDistPct` (tramite `IracingBridge.GetLapDistPct(state.PlayerCarIdx)`) e `PlayerData.LastPosPct` con fallback trasparente su `state.TrackPositionPercent` se nativo assente o non valido.
- `User.PluginSdkDemoEdit/TargetStrategyManager.cs:82, 497, 743, 1739`:
  - Aggiunta proprietà `TrackPositionPercent` a `TargetState` (default `0.0`).
  - Sincronizzata in `TargetStrategyManager.SelectTarget` e nel loop periodico di `Update`: legge prioritariamente `oppData.NativeLapDistPct` a 60Hz se `> 0.0f`, ricadendo su `targetOpp.TrackPositionPercent ?? oppData.LastPosPct`.
  - Resettata a `0.0` in `SetNoTarget()`.
- `User.PluginSdkDemoEdit/DataPluginDemo.cs:448, 593, 1814, 2032`:
  - Registrate e pubblicate le proprietà SimHub:
    - `SimRIG.Target.TrackPositionPercent` (double, arrotondato a 4 decimali)
    - `SimRIG.Player.TrackPositionPercent` (double, arrotondato a 4 decimali)
- `User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/UnitTests/NativeIracingOpponentTrackingUnitTests.cs:368, 382-425`:
  - Esteso `Test_TargetState_TrackSurface_Properties` per verificare il reset e l'assegnazione di `TrackPositionPercent`.
  - Aggiunto nuovo unit test `Test_PlayerAndTarget_TrackPositionPercent_Properties`: valida che `PlayerData.LastPosPct` utilizzi `NativeLapDistPct` quando disponibile o ripieghi su `state.TrackPositionPercent`, e che `TargetState.TrackPositionPercent` sincronizzi fedelmente la posizione.
  - Suite test: passata da 342 a **343 test PASS** (100% verdi, 0 falliti).

### Come verificare
```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/User.PluginSdkDemo.Tests.csproj" -p:Configuration=Debug -p:PostBuildEvent="" -v:minimal -nologo
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```
Atteso: build 0 errori, 343 PASS, exit code 0.

### Stato
- ✅ Compila senza errori
- ✅ 343 test passano (100%)

### Per chi entra
**Prossimo passo:** Test su cruscotto/dashboard con replay aperto per visualizzare `SimRIG.Player.TrackPositionPercent` e `SimRIG.Target.TrackPositionPercent` affiancate alle proprietà `TrackSurface`. Successivamente procedere con il collegamento delle proprietà alle logiche strategiche (Merge Gap, posizione rispetto a PitEntryPct/PitExitPct).
**NON toccare:** `Hardware/` (territorio di Andreas).
**Attenzione a:** Se SimHub è aperto in background, compilare con `-p:PostBuildEvent=""` per evitare errori di condivisione file (`Sharing violation` su `User.PluginSdkDemo.dll` lockata da SimHub).

---

## [2026-09-09 23:35] antigravity → chiunque entri dopo

**Task:** Esposizione proprietà SimHub TrackSurface / IsInPitStall / IsOnPitRoad per Player e Target e fallback per replay array
**Piano:** `implementation_plan.md`
**Commit:** `56321b5`

### Fatto
- `User.PluginSdkDemoEdit/IracingTelemetryBridge.cs:22, 60-150, 316-328`:
  - Aggiunto fallback da `PluginManager` in `IracingTelemetryBridge.Update(object rawObject, SimHub.Plugins.PluginManager pm = null)`. Quando si riproducono replay SimHub (`.telemetry.json`), i campi array di `GameRawData.Telemetry` (`CarIdxTrackSurface`, `CarIdxOnPitRoad`) possono essere restituiti come array .NET boxed (`System.Array`, `int[]`, `bool[]`, `TrackLocation[]`). Il bridge ora effettua l'unboxing dinamico per tutti i 64 indici auto `CarIdx`.
  - Aggiunto helper pubblico `GetTrackSurfaceString(IracingTrackSurface surface)` per mappare l'enum in stringhe leggibili (`"OnTrack"`, `"InPitStall"`, `"ApproachingPits"`, `"OffTrack"`, `"NotInWorld"`).
- `User.PluginSdkDemoEdit/SessionState.cs:137-140, 185-188`:
  - Aggiunte proprietà `PlayerTrackSurface` (`IracingTrackSurface`) e `PlayerIsOnPitRoad` (`bool`) con reset in `SessionState.Reset()`.
- `User.PluginSdkDemoEdit/OpponentTracker.cs:142, 658-662`:
  - Passato `PluginManager` a `IracingBridge.Update(rawObject, _pluginManager)`.
  - Popolati `PlayerData.TrackSurface`, `PlayerData.IsOnPitRoad`, `state.PlayerTrackSurface` e `state.PlayerIsOnPitRoad` per la vettura del giocatore (`PlayerCarIdx`).
- `User.PluginSdkDemoEdit/TargetStrategyManager.cs:122-126, 172-176, 1495-1510`:
  - Esteso `TargetState` con `TrackSurface` (string), `TrackSurfaceCode` (int), `TrackSurfaceString` (string), `IsInPitStall` (bool), `IsOnPitRoad` (bool).
  - A ogni tick di `Update`, i dati dello stato pista del target selezionato (`oppData.TrackSurface` e `oppData.IsOnPitRoad`) vengono mappati e sincronizzati in `CurrentTarget`.
- `User.PluginSdkDemoEdit/DataPluginDemo.cs:450-460, 595-605, 1815-1830, 2030-2045`:
  - Registrate e pubblicate a ogni tick le 8 nuove proprietà SimHub:
    - `SimRIG.Player.TrackSurface` (string, es. "OnTrack", "InPitStall")
    - `SimRIG.Player.TrackSurfaceCode` (int, 0=NotInWorld, 1=OffTrack, 2=InPitStall, 3=ApproachingPits, 4=OnTrack)
    - `SimRIG.Player.IsInPitStall` (bool, true se TrackSurface == InPitStall)
    - `SimRIG.Player.IsOnPitRoad` (bool, true se sulla pit road)
    - `SimRIG.Target.TrackSurface` (string)
    - `SimRIG.Target.TrackSurfaceCode` (int)
    - `SimRIG.Target.IsInPitStall` (bool)
    - `SimRIG.Target.IsOnPitRoad` (bool)
- `User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/UnitTests/NativeIracingOpponentTrackingUnitTests.cs:330-410`:
  - Aggiunti 2 unit test: `Test_IracingTelemetryBridge_PluginManagerReplayFallback` e `Test_TargetState_TrackSurface_Properties`.
  - Suite test: passata da 340 a **342 test PASS** (100% verdi, 0 falliti).

### Come verificare
```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```
Atteso: build 0 errori, 342 PASS, exit code 0.

### Stato
- ✅ Compila senza errori
- ✅ 342 test passano (100%)

### Per chi entra
**Prossimo passo:** Test su replay con SimHub aperto per visualizzare le nuove proprietà `SimRIG.Player.*` e `SimRIG.Target.*` nella lista proprietà e su dashboard/overlay.
**NON toccare:** Non agganciare `StationaryTime` a `InPitStall` (richiesta esplicita utente: mantenere separato per ora).
**Attenzione a:** Se si modificano o leggono altre proprietà array da `GameRawData.Telemetry`, utilizzare sempre l'estrazione unboxing tramite `IracingTelemetryBridge` con fallback su `PluginManager`.

---

## [2026-09-09 15:15] antigravity → chiunque entri dopo

**Task:** Fix replay pit detection fallback, Opponents fuel drop to 0L, gap flicker and multiclass target class position
**Piano:** —
**Commit:** `questo`

### Fatto
- `User.PluginSdkDemoEdit/OpponentTracker.cs:185, 595-625, 1345, 1775-1825`:
  - **Replay Pit Detection Fallback:** Risolto bug critico in cui la presenza di `sample.Telemetry` (oggetti bridge non nulli) impostava `isNativeAvailable = true` ma `CarIdxOnPitRoad` conteneva tutti `false` (nei replay SimHub/iRacing non trasmette telemetria nativa avversari). Cambiata condizione a `isNativeAvailable && tData.IsOnPitRoad`: se nativo è assente o false, l'avversario ricade correttamente su geofence spaziale, persistenza di velocità e durata.
  - **Fix Fuel Drop to 0.00L & Pit Count Mancante:** In `Opponent Spatial Transit Retroactively Validated`, aggiunta l'esecuzione completa di pit stop (`PitCount++`, `LastStopLap`, `LastPitLap`, e `SmartRefuelProjection`) protetta da verifica `rawCurrentLap <= SpatialStrictEntryLap + 1` contro salti di replay. `LastRefuelLap` viene aggiornato e `EstimatedFuel` rifornito al fabbisogno per arrivare a fine gara (`NeedsPitStop = false`).
  - **Dynamic Class Position Ranking:** Aggiunto calcolo periodico del ranking di classe raggruppando `state.Opponents` per `CarClass` e ordinando per progresso di gara continuo. Popola `OpponentTelemetryData.ClassPosition` e `state.PositionInClass` sia in sessione live che replay.
- `User.PluginSdkDemoEdit/TargetStrategyManager.cs:484, 507, 560-610, 1220-1245, 1415-1465, 1755`:
  - **Class Position Targeting (P1..P24):** Modalità "P1".."P24" ora seleziona prioritariamente per posizione di classe nella classe del Player (`CarClass == state.CarClassId`), selezionando il leader o contendente GT3 invece di un prototipo GTP assoluto.
  - **Class Position Fallback:** Risolto bug per cui `CurrentTarget.ClassPosition` leggeva solo `NativeClassPosition` (0 nei replay) ricadendo sulla posizione assoluta. Ora prioritizza `trk.ClassPosition > 0`.
  - **Stabilizzazione Microsettori e Gap Flicker:** Quando il target è in pit (`oppData.IsOnPitRoad || oppData.IsInsideGeofence || targetOpp.IsCarInPit`) o quando i timestamp dei microsettori divergono di oltre il 60% da `posDiff * refLapTime` (microsettori vecchi di 2-3 giri), il calcolo ripiega istantaneamente sulla progressione continua `Math.Abs(posDiff * refLapTime)`, eliminando i salti a +116s/+194s/+272s durante le soste di Aake Korte.
  - **ResetSession:** Aggiunto reset di `_prevPosDiff = double.NaN` e `_lastTargetForPosDiff = ""`.
- `User.PluginSdkDemoEdit/DataPluginDemo.cs:443, 584, 1799, 2012`:
  - Registrate e pubblicate le proprietà `SimRIG.Target.ClassPosition` e `SimRIG.Player.ClassPosition`.
- `User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/UnitTests/NativeIracingOpponentTrackingUnitTests.cs`:
  - Aggiunti 2 test unitari: `Test_SelectTarget_P1_P2_InMulticlass` e `Test_ReplayFallback_RetroactiveTransitValidatesStop`.
  - Conteggio test suite: **340 test PASS** (100% verdi, 0 falliti).

### Come verificare
```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```
Atteso: build 0 errori, 340 PASS, exit code 0.

### Stato
- ✅ Compila senza errori
- ✅ 340 test passano (100%)

### Per chi entra
**Prossimo passo:** Test su replay Road Atlanta `20260909_134044` per verificare in log/HUD che Aake Korte mantenga il conteggio pit, il carburante stimato aggiornato post-sosta, e che i gap verso i target rimangano stabili senza inversioni o sfarfallii.
**NON toccare:** `Hardware/` (territorio di Andreas).
**Attenzione a:** Nei replay di iRacing riprodotti su SimHub, la telemetria nativa di `sample.Telemetry` per gli avversari (`CarIdxOnPitRoad`, `CarIdxTrackSurface`) non è popolata: la cascata geofence + euristica spaziale è il canale primario per i replay.

---

## [2026-09-09 13:30] antigravity → chiunque entri dopo

**Task:** Firmware INPUT V2.8.2 — aggiunta stato TEST su Rotary POS 7
**Piano:** —
**Commit:** `questo`

### Fatto
- `Hardware/Firmware INPUT/V2_8_2/V2_8_2.ino:408`:
  - Aggiunto stato `TEST` su posizione rotary 7 in `sendNormalModeUpdate(int pos)` (`else if (pos == 7) sendSimHubMsg(SH_MODE_PREFIX, F("TEST"));`), posizionato subito dopo `MAP` (pos 6). Il firmware invia ora `WMODE:TEST` verso SimHub quando il Rotary 1 viene ruotato in posizione 7. Nessun'altra logica modificata.
- Compilazione firmware verificata con `arduino-cli` con 0 errori (24.758 byte programma, 1.364 byte variabili globali).

### Come verificare
```bash
& "C:\Users\Andreas\AppData\Local\Programs\Arduino IDE\resources\app\lib\backend\resources\arduino-cli.exe" compile --fqbn arduino:avr:leonardo "Hardware/Firmware INPUT/V2_8_2"
```
Atteso: compilazione completata con 0 errori.

### Stato
- ✅ Firmware compila pulito con `arduino-cli` (0 errori)
- ✅ 338 test PASS C# (invariati)

### Per chi entra
**Prossimo passo:** Test su volante fisico ruotando il selettore Rotary 1 su posizione 7 e verifica ricezione proprietà `SimRIG.Mode` = `"TEST"` in SimHub.
**NON toccare:** `Hardware/` rimane territorio di Andreas.
**Attenzione a:** Il conteggio test corrente del plugin C# è 338 PASS.

---

## [2026-09-09 12:15] antigravity → chiunque entri dopo

**Task:** Telemetria nativa iRacing per Opponents: latch Fuel a giro 4, scomposizione soste box e stabilizzazione gap
**Piano:** `.ai/plans/2026-09-09-native-iracing-opponent-tracking-and-fuel-engine.md`
**Commit:** `questo`

### Fatto
- `User.PluginSdkDemoEdit/IracingTelemetryBridge.cs` (nuovo) — Modulo ponte nativo per telemetria iRacing a 60Hz da `DataSample.Telemetry`:
  - `CarIdxOnPitRoad` (bool[]): rilevamento istantaneo ingresso/uscita pit lane avversari.
  - `CarIdxTrackSurface` (TrackLocation[]): `InPitStall` (1) per cronometro tempo di sosta stazionario, `AproachingPits` (2), `OnTrack` (3).
  - `CarIdxPitStopCount` (int[]), `CarIdxClassPosition` (int[]), `CarIdxLapDistPct` (float[]).
- `User.PluginSdkDemoEdit/SessionMetadata.cs` e `SessionDataReader.cs` — Mappatura bidirezionale `CarIdxByUserName` / `UserNameByCarIdx` per risoluzione O(1) tra nome avversario e carIdx nativo.
- `User.PluginSdkDemoEdit/PitRadar.cs` — Metodi `RecordPitEntrySample(pct)` e `RecordPitExitSample(pct)` integrati col consenso per apprendere automaticamente le soglie geometriche pit lane da `CarIdxOnPitRoad`.
- `User.PluginSdkDemoEdit/OpponentTracker.cs`:
  - **Fuel Latch a Giro 4:** La mediana del Player ora viene agganciata solo dopo il completamento di almeno 3 giri (`CompletedLaps >= 3`, giro corrente >= 4), evitando che il giro 1 (2.12L anomalo rispetto alla mediana successiva di 2.26L) avveleni i consumi degli avversari.
  - **EstimatedFuelTank non azzerato:** Sostituito l'artificioso `EstimatedFuelTank = classMaxTank` con `Math.Max(0.0, EstimatedFuel)`.
  - **Smart Refueling all'ingresso box:** Quando l'avversario entra nei box (`tData.IsOnPitRoad == true`), il carburante aggiunto viene calcolato esattamente come `Math.Min(classMaxTank - residuo, fuelNeeded - residuo)` per finire la gara con margine; se il carburante copre i giri rimanenti, `NeedsPitStop = false`, evitando il doppio conteggio della sosta in `ProjectedMergeGap`.
  - **Cronometro Stazionario Diretto:** Misurato con precisione quando `TrackSurface == InPitStall`.
  - **Scomposizione Pit Loss all'uscita box:** Calcolato `RawExtendedPitZoneTime = ObservedTransit - StationaryTimeSec` e isolato l'overhead empirico dei martinetti (`EmpiricalDeadTime = StationaryTime - (FuelToAdd / FillRate)`).
- `User.PluginSdkDemoEdit/TargetStrategyManager.cs`:
  - **Class Position Nativo:** Assegnato `CurrentTarget.ClassPosition` tramite `targetTrackData.NativeClassPosition > 0 ? targetTrackData.NativeClassPosition : targetOpp.PositionInClass`, risolvendo il ranking mostrato sempre in assoluto.
  - **Stabilizzazione Gap e MergeGap:** Introdotto `NormalizeLapDifference` che neutralizza lo spike spurio di $\pm 1.0$ giro al traguardo senza ripiegare a modulo 0.5 giri. I gap $> 0.5$ giri (come Aake Korte) conservano il segno corretto e continuo (+ per Player dietro, - per Player davanti).
  - **Soppressione Microsettori in Pit Lane:** Quando l'avversario è in corsia box (`IsOnPitRoad == true`), la telemetria a microsettori ad alta velocità viene soppressa a favore della progressione continua `Math.Abs(posDiff * refLapTime)`, eliminando i violenti sfarfallamenti tra 90s e 15s.
- `User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/UnitTests/NativeIracingOpponentTrackingUnitTests.cs` (nuovo) — 6 unit test a copertura di latch carburante, smart refueling, scomposizione soste, stabilizzazione gap e ponte telemetrico.
- Suite test: passata da 332 a **338 test PASS** (0 falliti).

### Come verificare
```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```
Atteso: exit `0`, **338 PASS**.

### Stato
- ✅ Compila senza errori (solo 1 warning preesistente in ReplayBacktestIntegrationTest)
- ✅ 338 test passano (100%)

### Per chi entra
**Prossimo passo:** Test su replay reale in SimHub (es. Road Atlanta) per verificare visivamente i dati di telemetria avversari, tempi sosta e stabilità gap su HUD.
**NON toccare:** `Hardware/` (riservato ad Andreas).
**Attenzione a:** `iRacingSDK.dll` viene copiato in `bin/Debug` tramite PostBuildEvent del `.csproj`. Nei test mock o simulatori non-iRacing, `IracingTelemetryBridge` degrada dolcemente alla telemetria euristiche standard.

---

## [2026-09-09 08:55] antigravity → chiunque entri dopo

**Task:** Firmware INPUT V2.8.2 — aggiunta stato MAP su Rotary POS 6, versionamento PaddleClutch.h e chiusura Y-53
**Piano:** —
**Commit:** `questo`

### Fatto
- `Hardware/Firmware INPUT/V2_8_2/V2_8_2.ino:407`:
  - Aggiunto stato `MAP` su posizione rotary 6 in `sendNormalModeUpdate(int pos)` (`else if (pos == 6) sendSimHubMsg(SH_MODE_PREFIX, F("MAP"));`), posizionato subito dopo `FORECAST` (pos 5). Il firmware invia ora `WMODE:MAP` verso SimHub quando il Rotary 1 viene ruotato in posizione 6. Nessun'altra logica modificata come richiesto.
- `Hardware/Firmware INPUT/V2_8_2/PaddleClutch.h`:
  - Copiato `PaddleClutch.h` da `Hardware/Firmware INPUT/libraries/PaddleClutch-main/` direttamente nella cartella dello sketch `Hardware/Firmware INPUT/V2_8_2/`. Versionato nel repository.
- Chiusura punto **Y-53**:
  - Rimosso Y-53 dalla tabella "Congelati in attesa di decisione" di `.ai/PROJECT_STATE.md`.
  - Archiviato il punto con motivazione tecnica e dettagli in `.ai/archive/CLOSED_POINTS.md`.
  - Aggiunta riga di riferimento nell'indice dei punti chiusi di `.ai/PROJECT_STATE.md`.
  - Aggiornato conteggio test PASS a 332 in `.ai/PROJECT_STATE.md` (allineato all'ultimo handoff verificato).

### Come verificare
```bash
& "C:\Users\Andreas\AppData\Local\Programs\Arduino IDE\resources\app\lib\backend\resources\arduino-cli.exe" compile --fqbn arduino:avr:leonardo "Hardware/Firmware INPUT/V2_8_2"
```
Atteso: compilazione completata con 0 errori (24.744 byte programma, 1.364 byte variabili globali).

### Stato
- ✅ Firmware compila pulito con `arduino-cli` (0 errori)
- ✅ `PaddleClutch.h` presente nello sketch folder
- ✅ 332 test PASS C# (invariati, nessun codice .NET toccato)

### Per chi entra
**Prossimo passo:** Test su volante fisico ruotando il selettore Rotary 1 su posizione 6 e verifica ricezione proprietà `SimRIG.Mode` = `"MAP"` in SimHub.
**NON toccare:** `Hardware/` rimane territorio di Andreas.
**Attenzione a:** Il conteggio test corrente del plugin C# è 332 PASS.

---

## [2026-09-08 22:15] antigravity → chiunque entri dopo (Claude in particolare)

**Task:** Cronometro reale Pit Zone (SectorTracker) per Player e Opponent, rimozione formula geometrica, fix cascata pit detection e inizializzazione fuel Lap 1
**Piano:** —
**Commit:** `42564a2` (codice e test), questo (handoff e rilascio lock)

### Fatto
- `User.PluginSdkDemoEdit/PitRadar.cs:275-288, 946-956`:
  - Aggiunto `TrackRecord.GetPitZoneWeight()` e `PitRadar.GetPitZoneWeight()` che calcolano la frazione esatta del tracciato coperta dalla zona pit standard `[PitEntryPct, PitExitPct]` (clamp `[0.01, 0.50]`).
- `User.PluginSdkDemoEdit/RaceAnalyzer.cs:298, 455-470, 511, 2414`:
  - Aggiunto cronometro puro `PlayerPitZone` (`SectorTracker { Name = "PlayerPitZone" }`) cablato in `RaceAnalyzer.Update` con `radar.IsInPitLaneZone` e `radar.GetPitZoneWeight()`.
  - Aggiunto reset in entrambi i blocchi di pulizia sessione.
- `User.PluginSdkDemoEdit/SectorTracker.cs:112, 163`:
  - Abbassata la soglia di transito minimo da 5.0s a 3.0s per supportare zone pit corte su rettilinei veloci.
  - Abilitato l'aggiornamento di `BestRawTime` a partire dal giro 1 (`lapsOnTyres >= 1`) anche oltre i 40 km di vita gomma se `sectorTime < BestRawTime`.
- `User.PluginSdkDemoEdit/OpponentTracker.cs:177, 192, 543, 565-566, 768-782, 885-910, 1263-1310, 1391-1398, 1619-1635, 2095-2115`:
  - Aggiunto `PitZone` (`SectorTracker`) in `OpponentTelemetryData` e aggiornato frame-by-frame nel settore pit standard.
  - Aggiunta proprietà pubblica `ClassBestPitZoneRacingTime` che traccia il miglior tempo di transito a velocità di gara attraverso la pit zone per la classe, sia dal Player che dagli avversari.
  - Inizializzazione carburante Lap 1: assegnati `EstimatedFuel = opponentStartingFuel` ed `EstimatedFuelTank = opponentStartingFuel` sia alla creazione dell'istanza sia quando `raceStartingFuel` diventa disponibile.
  - Criterio B (Speed Persistence): aggiunto debounce di 0.4s (`HighSpeedStartSec`) per tollerare spike e jitter dei pacchetti di telemetria o replay senza azzerare prematuramente la persistenza di velocità.
  - Criterio C (Duration Parachute): sganciato dal controllo restrittivo di velocità (`LastValidSpeedKmh < pitSpeedThreshold + 5.0`). Ora scatta direttamente per superamento del tempo di gara di classe (`> ClassBestPitZoneRacingTime + 4.0s` o fallback 15s), garantendo il rilevamento della sosta anche in caso di salti temporali/lag.
  - Validazione uscita box: attivato l'uso effettivo di `adaptiveThreshold` (`if (tData.StrictPitValidInTransit || totalTransitTime > adaptiveThreshold)`).
  - Livello 4 (Safety Net uscita spaziale): logging dettagliato e verifica adattiva su `SpatialStrictEntryTimeSec` all'uscita spaziale da `PitExitPct`.
- `User.PluginSdkDemoEdit/TargetStrategyManager.cs:707-711`:
  - `PitLaneZoneRacingTime` ora assegna prioritariamente la misura reale `tracker.ClassBestPitZoneRacingTime > 0.0`, conservando la formula geometrica `pitDistance / racingSpeedMs` esclusivamente come ripiego iniziale a freddo.
- `User.PluginSdkDemoEdit/DataPluginDemo.cs:373, 1262, 1754`:
  - Registrata ed esposta la proprietà SimHub `SimRIG.Session.ClassBestPitZoneRacingTime`.
  - Passati sia `RaceAnalyzer.PlayerPitZone.BestRawTime` sia `RaceAnalyzer.PlayerExtendedPitZone.BestRawTime` a `OpponentTracker.Update`.
- `User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/UnitTests/PitZoneStopwatchAndOpponentCascadeUnitTests.cs`:
  - Nuova suite di test con 4 unit test dedicati che validano:
    - Rilevamento e registrazione di `BestRawTime` su transito veloce di gara.
    - Immunità di `BestRawTime` da corruzione durante una sosta lenta ai box.
    - Esclusione dei transiti di outlap a Lap 0.
    - Calcolo geometrico del peso pit zone.

### Come verificare
```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```
Atteso: build pulita (0 errori) e test runner console a **332 PASS (100%)**.

### Stato
- ✅ Compila (0 errori, 1 warning CS0219 noto)
- ✅ 332 PASS (100%)

### Per chi entra
**Prossimo passo:** Esecuzione del replay Road Atlanta (`20260908_144534` o nuova corsa) per verificare che Aake Korte venga correttamente riconosciuto ai box (PitCount = 1, calcolo fuel tank accurato, nessun transito scartato) e che `SimRIG.Session.ClassBestPitZoneRacingTime` e `Target.PitLaneZoneRacingTime` mostrino il tempo cronometrato reale anziché la stima geometrica.
**NON toccare:** La struttura del `SectorTracker` per la gestione outlap/inlap/normal.
**Attenzione a:** Il conteggio test corrente è **332 PASS**. Se si aggiornano altri file di documentazione, mantenere allineato il numero reale.

---

## [2026-09-08 14:35] antigravity → chiunque entri dopo (Claude in particolare)

**Task:** Merge Gap simmetrico a 4 stati, rimozione hardcoded target Egor dal monitor, e azioni SimHub per selezione e lock target da tastiera/replay
**Piano:** —
**Commit:** `45cfb4c` (codice e test), questo (handoff e rilascio lock)

### Fatto
- `User.PluginSdkDemoEdit/TargetStrategyManager.cs:211-224, 1095-1105, 1180-1205`:
  - Implementato metodo centralizzato `CalculateProjectedMergeGap(double signedGap, bool playerNeedsPit, double playerPitLoss, bool targetNeedsPit, double targetPitLoss)` che applica la formula simmetrica: `signedGap + (playerNeedsPit ? playerPitLoss : 0.0) - (targetNeedsPit ? targetPitLoss : 0.0)`.
  - Risolta l'anomalia dello stato post-sosta: quando entrambe le vetture hanno completato la sosta o possono finire senza fermarsi (`!playerNeedsPit && !targetNeedsPit`), il `ProjectedMergeGap` coincide esattamente con `SignedGapSeconds` senza aggiungere il falso ritardo fantasma (+25~32s).
  - Rimossa la stringa hardcoded `"Egor"` / `"Ogorodnicov"` in `MERGE_GAP_MONITOR`: ora il monitor prioritizza `LatchedTargetName` se presente (lock attivo), altrimenti traccia dinamicamente `targetOpp` (il bersaglio attivo).
  - Nel log del monitor aggiornati i campi `PIT LOSS TIMINGS` e `RESULT` per mostrare `+0.00s` e `PlayerNeedsPit: False` quando non c'è sosta residua.
- `User.PluginSdkDemoEdit/DataPluginDemo.cs:260-295`:
  - Aggiunta azione SimHub `Target_ToggleLock`: permette di agganciare/sganciare il lock sul target corrente (`LatchedTargetName`) via tastiera o pulsante SimHub durante i replay, senza richiedere il volante fisico.
  - Aggiunte azioni SimHub `Target_NextTarget` e `Target_PrevTarget` per scorrere i bersagli anche da tastiera o interfaccia SimHub.
- `User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/UnitTests/MergeGapUnitTests.cs`:
  - Aggiornati i test per coprire tutti e 4 gli stati della sosta con `TargetStrategyManager.CalculateProjectedMergeGap`:
    - Stato 1: Player deve pittare, Target no (+11.48s).
    - Stato 2: Entrambi hanno pittato (nessuna sosta fantasma, SignedGap = MergeGap = +11.70s).
    - Stato 3: Entrambi devono pittare (differenziale perdite = -9.32s).
    - Stato 4: Target deve pittare, Player no (-25.00s).
- **Analisi Replay Road Atlanta (`Logs/Road Atlanta/SimRIG_StrategySnapshot_20260908_112922.csv`)**:
  - Sara Tolotti (Player, BMW M4 GT3 EVO) ha concluso la gara in **P19** direttamente dietro a **Aake Korte** (Ferrari 296 GT3, **P18**) con un distacco finale di circa 3.2s.
  - Aake Korte è stato l'avversario diretto di riferimento sia nel primo stint (Giri 13-14, gap ~3.6s) sia per tutto il secondo stint (Giri 22-35, gap ~3.5s).
  - Per il test sul replay di Road Atlanta, agganciare il lock su **Aake Korte** per monitorare sia la fase pre-pit che la stabilità del Merge Gap nel secondo stint (Giri 22-35) che ora rimarrà fedele a ~3.2s invece del balzo anomalo a +29s.

### Come verificare
```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```
Criterio di successo: **328 PASS (100%)**.

### Stato
- ✅ Compila (0 errori, 1 warning CS0219 noto)
- ✅ 328 PASS (100%)

### Per chi entra
**Prossimo passo:** Esecuzione del replay di Road Atlanta per confermare la stabilità di `ProjectedMergeGap` con lock su Aake Korte (Giri 22-35: deve rimanere ancorato a ~3.2s senza salti a +29s), poi avanzamento nella roadmap (`.ai/plans/2026-08-24-roadmap.md`).
**NON toccare:** `CarPitData.cs` e la formula unificata di `CalculateProjectedMergeGap`.
**Attenzione a:** L'azione SimHub `Target_ToggleLock` opera su `TargetStrategyManager.LatchedTargetName`. Per usarla in SimHub, mappare un tasto o pulsante sull'azione `User.PluginSdkDemo.Target_ToggleLock`.

---

## [2026-09-08 13:20] antigravity → chiunque entri dopo (Claude in particolare)

**Task:** Unificazione formule di Pit Loss e tempo da fermo (CarPitData) tra RaceAnalyzer, TargetStrategyManager e DataPluginDemo
**Piano:** `.ai/plans/2026-09-03-inventario-passo-e-sosta.md`
**Commit:** `14e9a08` (codice e test), questo (handoff e rilascio lock)

### Fatto
- `User.PluginSdkDemoEdit/CarPitData.cs:127-197`:
  - Implementato `CalculateStationaryTime`: calcola il tempo da fermo distinguendo tra sosta simultanea (GT3/LMP2/GTP: `Max(fuel, tyres)`) e sequenziale (PCUP/OpenWheel: `fuel + tyres`) con buffer martinetti (+2.0s se sosta > 0).
  - Implementato `CalculateExtendedRacingTime` (con overload geometrico e per velocità): prioritizza la misura cronometrata reale della classe da `OpponentTracker.ClassBestExtendedPitZoneTime`, con fallback sulla frazione di tracciato box per passo sul giro.
  - Implementato `CalculateTotalPitLoss`: unifica il calcolo della perdita netta ai box con guard di sicurezza `extended >= timeInZone` per evitare azzeramenti anomali o sottrazioni errate con telemetria incompleta.
- `User.PluginSdkDemoEdit/RaceAnalyzer.cs:1125-1150`:
  - Sostituita la formula manuale semplificata che ignorava `IsSequential` e il buffer jack con `CarPitData.CalculateStationaryTime`, `CarPitData.CalculateExtendedRacingTime` e `CarPitData.CalculateTotalPitLoss`.
- `User.PluginSdkDemoEdit/TargetStrategyManager.cs:695-715, 735-745, 1160-1170`:
  - Sostituiti i blocchi di calcolo inline duplicati per `playerTotalPitLoss`, `targetTotalPitLoss` e nel monitor merge gap con le chiamate centralizzate a `CarPitData`.
- `User.PluginSdkDemoEdit/DataPluginDemo.cs:1990-2005`:
  - Sostituito il calcolo inline di `totalStationaryTimeVal` e `totalPitLossVal` con le chiamate unificate a `CarPitData`.
- `User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/UnitTests/PitLossUnitTests.cs`:
  - Aggiunti 3 unit test dedicati: `Test_CalculateStationaryTime_Centralized`, `Test_CalculateExtendedRacingTime_Centralized`, `Test_CalculateTotalPitLoss_Centralized`.

### Come verificare
```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```
Criterio di successo: **327 PASS (100%)**.

### Stato
- ✅ Compila (0 errori, 1 warning CS0219 noto)
- ✅ 327 PASS (100%)

### Per chi entra
**Prossimo passo:** Verifica del merge gap post-sosta (`ProjectedMergeGap` / `TargetStrategyManager.cs`) su replay reali e continuazione della Fase B della roadmap (`2026-08-24-roadmap.md`).
**NON toccare:** `CarPitData.cs` e la logica di calcolo centrale senza test di non-regressione.
**Attenzione a:** L'unificazione di PitLoss non altera le proiezioni di fine gara né il consumo carburante su Road Atlanta (35 giri / 31 L) e Misano (26 giri / 16 L), poiché la variazione su `playerL_left` è inferiore a 0.05 giri (sotto la soglia di sensibilità del round up).

---

## Handoff più vecchi

Tutte le voci precedenti a quelle qui sopra sono in `.ai/archive/HANDOFF_LOG_archive.md`,
in ordine cronologico inverso come questo file. La prima potatura è del 2026-09-05: il file
dichiarava di tenere gli ultimi 10 e ne conteneva 22, per 112 KB letti a ogni ingresso.

*(Niente conteggi scritti qui: `grep -c '^## \[20' .ai/archive/HANDOFF_LOG_archive.md` dà il
numero esatto senza che nessuno debba ricordarsi di aggiornarlo.)*
