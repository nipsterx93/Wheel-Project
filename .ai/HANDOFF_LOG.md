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

## [2026-09-11 13:35] antigravity → chiunque entri dopo

**Task:** Risoluzione falsi stop su auto culled in NotInWorld e correzione classificazione gomme in soste simultanee
**Piano:** —
**Commit:** `6c0c1a4` (codice e test), questo (handoff e rilascio lock)

### Fatto
- `User.PluginSdkDemoEdit/OpponentTracker.cs`:
  - **Bypass universale telemetria nativa !IsOnPitRoad** (r. 1499-1504): se `isNativeAvailable` e `!tData.IsOnPitRoad`, `isInsideGeofence` viene forzato a `false` a prescindere da `TrackSurface` (`OnTrack`, `NotInWorld`, `OffTrack`). Elimina tutti i falsi trigger su auto lontane dal Player culled da iRacing a `NotInWorld (-1)`.
  - **Protezione Speed < 0.5 km/h su NotInWorld** (r. 1517): anche nel fallback spaziale puro, `Speed < 0.5` non scatta se l'auto è `NotInWorld`, poiché le coordinate culled non si aggiornano e simulano artificiosamente velocità zero.
  - **PredictedFuelToAdd da LastPitFuelAdded** (r. 1729-1731): all'uscita box, `predictedFuelToAdd` legge prioritariamente `tData.LastPitFuelAdded` precalcolato da Smart Refuel all'ingresso box (es. 36.3L) anziché `targetFuel - EstimatedFuel` (che era già stato ricaricato a 37.3L, stimando erroneamente solo 1.5L di carburante).
  - **Classificazione sosta simultanea basata su durata carburante** (r. 1782-1825): in pitstop simultanei (GT3), se il tempo stazionario ($16.2\text{s}$) è coperto dalla durata necessaria al rifornimento ($T_{\text{refuel}} = 15.4\text{s}$) e inferiore al tempo minimo per 4 gomme ($< 18.0\text{s}$), la sosta viene classificata correttamente come "Fuel Only" (`TiresChanged = false`), prevenendo il reset errato delle baseline e l'attivazione della modalità provvisoria.
  - **Protezione EstimatedFuel post-sosta** (r. 1806-1820): sincronizzazione coerente di `EstimatedFuel` senza doppio incremento né saturazione anticipata.
- `User.PluginSdkDemoEdit/TargetStrategyManager.cs:1375-1379`:
  - In `MergeGapLog`, `targetPitCount` legge `CurrentTarget.PitCount` o `logOppData.PitCount`, riportando correttamente `Pits: 1` anziché `0` al target monitor.
- `User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/UnitTests/NativeIracingOpponentTrackingUnitTests.cs`:
  - Aggiunto unit test `Test_SimultaneousPitStop_IdentifiesFuelOnlyWhenTimeExplainedByFuel` (r. 1110-1175) registrato in `RunAllTests()`.
  - Aggiornato `Test_SpatialGeofence_DoesNotTriggerPitStopAtRacingSpeedOnStraight` a verifica della protezione di auto culled in `NotInWorld`.
  - Suite test: **354 PASS (100%)**.

### Come verificare
```bash
& "C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
& "User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```
Atteso: build pulita (0 errori) e test runner console a **354 PASS (100%)**.

### Stato
- ✅ Compila (0 errori, 1 warning CS0219 noto)
- ✅ 354 PASS (100%)

### Per chi entra
**Prossimo passo:** Test replay Road Atlanta per osservare che né auto lontane (Connor Spree) né vicine (Bruno Carneiro) subiscano falsi trigger, e che la sosta di Carneiro a Lap 20 riporti `Fuel Only` con gap e baselines intatti.
**NON toccare:** `Hardware/` rimane territorio di Andreas.
**Attenzione a:** Il conteggio test corrente del plugin C# è **354 PASS**.

---

## [2026-09-11 12:35] antigravity → chiunque entri dopo

**Task:** Fix deduzione StationaryTime avversari in NotInWorld e protezione da falsi trigger box sul rettilineo
**Piano:** —
**Commit:** `0d9ec82` (codice e test), questo (handoff e rilascio lock)

### Fatto
- `User.PluginSdkDemoEdit/OpponentTracker.cs`:
  - **Bypass telemetria nativa OnTrack** (r. 1499-1504): se la telemetria nativa iRacing dichiara esplicitamente `!tData.IsOnPitRoad && tData.TrackSurface == IracingTrackSurface.OnTrack`, il fallback spaziale non scavalca il dato certo e `isInsideGeofence` viene forzato a `false`.
  - **Protezione Criterio C a velocità di gara** (r. 1567-1589): Criterio C (paracadute di durata) vietato se l'auto viaggia a velocità di gara (`tData.LastValidSpeedKmh < (pitSpeedThreshold + 15.0) && tData.LastValidSpeedKmh < 100.0`). Inoltre, introdotto pavimento minimo di 15.0s (`Math.Max(15.0, ClassBestPitZoneRacingTime * 1.8)`), impedendo a transiti di 9.4s sul rettilineo (es. a 196.8 km/h a Road Atlanta su zona pit da 598.9m) di far scattare false soste.
  - **Congelamento accumulo stazionario fittizio in NotInWorld** (r. 1640-1660): quando l'avversario viene culled in `NotInWorld (-1)`, iRacing congela le coordinate all'ultimo punto noto (`deltaPos = 0` => velocità calcolata 0.0 km/h). Escluso `NotInWorld` dall'accumulare artificialmente tempo stazionario in `StopStartTimeSec`.
  - **Deduzione inversa StationaryTime garantita all'uscita** (r. 1690-1718): rimossa la condizione bloccante `StationaryTimeSec <= 0.5`. Se `NotInWorldDurationSec > 0.0`, all'uscita box viene sempre applicata la deduzione inversa $T_{\text{stationary}} = \max(0.0, T_{\text{NotInWorld}} - T_{\text{refTransit}})$ (per Carneiro $42.56 - 26.37 = 16.19\text{s}$ anziché $42.6\text{s}$), classificando la sosta correttamente come "Fuel Only" invece di "Danni/Riparazioni". Garantito l'incremento di `PitCount` anche se l'avversario è stato in NotInWorld durante tutta la sosta.
- `User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/UnitTests/NativeIracingOpponentTrackingUnitTests.cs`:
  - Aggiunto unit test `Test_SpatialGeofence_DoesNotTriggerPitStopAtRacingSpeedOnStraight` (r. 1008-1077) registrato in `RunAllTests()`.
  - Allineato test `Test_Opponent_NotInWorld_LatchesPitRoadAndDeducesStationaryTime`.
  - Suite test: **353 PASS (100%)**.

### Come verificare
```bash
& "C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
& "User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```
Atteso: build pulita (0 errori) e test runner console a **353 PASS (100%)**.

### Stato
- ✅ Compila (0 errori, 1 warning CS0219 noto)
- ✅ 353 PASS (100%)

### Per chi entra
**Prossimo passo:** Test su replay reale in SimHub per osservare il comportamento su pista; chiarito con l'utente il motivo del FuelToAdd 33L vs 31L (proiezione giri 36 vs 35 latched al lap 1 per passo lento iniziale, nessuna formula modificata).
**NON toccare:** `Hardware/` rimane territorio di Andreas.
**Attenzione a:** Il conteggio test corrente del plugin C# è **353 PASS**.

---

## [2026-09-11 11:00] antigravity → chiunque entri dopo

**Task:** Calibrazione pit stop, persistenza PitTransitTime Player e deduzione StationaryTime avversari via NotInWorld
**Piano:** —
**Commit:** `e66a591` (codice e test), questo (handoff e rilascio lock)

### Fatto
- `User.PluginSdkDemoEdit/PitRadar.cs`:
  - Aggiunto metodo `GetTheoreticalTransitTimeSec(double trackLengthMeters)` (r. 290-305): calcola il tempo di transito teorico al limite YAML della pitlane $(d / v)$ con fallback a velocità media.
  - In `SetCurrentTrackForTesting` (r. 525-533): registra e sincronizza il record di test in `_database.Tracks`, garantendo l'isolamento dei test dai file di stato su disco.
  - In `Update` (r. 1320-1327): comparazione case-insensitive (`StringComparison.OrdinalIgnoreCase`) per `TrackClassID` e `lookupKey`.
  - In `Update` (r. 1569, 1617, 1646): salvataggio `_currentTrack.PlayerRecordSet = true` ogni volta che `PitTransitTime` viene calibrato da procedure guidate.
  - In `Update` (r. 1682-1703): introdotto apprendimento automatico di `PitTransitTime` e `PlayerRecordSet = true` per soste naturali del Player in cui c'è arresto in piazzola (`_pitBoxTimeCache > 0.5s`), distinguendolo dal Drive-Through naturale (`PitDriveThroughTime`).
  - Salvaguardato `log?.Log(...)` in tutti i rami di `PitRadar` contro `NullReferenceException` con logger nullo.
- `User.PluginSdkDemoEdit/OpponentTracker.cs`:
  - Gestione `NotInWorld` (r. 1333-1375): latching di `effectiveOnPitRoad = true` se un avversario era già in pit road ed entra in `NotInWorld` (culling di rete iRacing). Accumulo preciso del tempo trascorso in `NotInWorldDurationSec`.
  - Protezione geofence: impedita la registrazione di false uscite su culling (`RecordPitExitSample` a 0.958 ignorato durante `NotInWorld`).
  - Reverse-engineering StationaryTime (r. 1689-1706): all'uscita dalla pit lane, deduzione inversa $T_{\text{stationary}} = \max(0.0, T_{\text{NotInWorld}} - T_{\text{refTransit}})$, con $T_{\text{refTransit}}$ ricavato da `radar.PitTransitTime` (Player) o dal transito teorico da YAML.
  - Rimozione arrotondamenti artificiali sul pit speed limit appreso (r. 1838-1845): salvata la velocità reale esatta senza forzare multipli di 10.
- `User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/UnitTests/NativeIracingOpponentTrackingUnitTests.cs`:
  - Aggiunti 5 nuovi unit test registrati in `RunAllTests()`:
    1. `Test_RecordPitExitSample_RejectsSampleTooCloseToEntry`: valida il rifiuto di uscite fittizie a 0.9582.
    2. `Test_TheoreticalTransitTime_CalculatesFromYamlSpeedAndTrackLength`: verifica il calcolo teorico su distanza e limite YAML (Road Atlanta 26.40s).
    3. `Test_Player_NaturalPitStop_SavesPitTransitTime`: verifica la sosta naturale di Sara Tolotti con rifornimento (41.12s totali, 13.83s fermo -> 27.29s transito persistito e `PlayerRecordSet = true`).
    4. `Test_Player_NaturalDriveThrough_SavesPitDriveThroughTime`: verifica che un passaggio senza fermarsi aggiorni `PitDriveThroughTime` senza toccare `PitTransitTime`.
    5. `Test_Opponent_NotInWorld_LatchesPitRoadAndDeducesStationaryTime`: simula la sosta di Bruno Carneiro con culling `NotInWorld` (42.56s) deducendo 15.27s di sosta e classificando correttamente "Fuel Only" (nessun cambio gomme).
  - Suite eseguita con successo: **352 PASS (100%)**.

### Come verificare
```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```
Atteso: build pulita (0 errori) e test runner console a **352 PASS (100%)**.

### Stato
- ✅ Compila (0 errori, 1 warning CS0219 noto)
- ✅ 352 PASS (100%)

### Per chi entra
**Prossimo passo:** Test sui replay completi (es. Road Atlanta) per validare la visualizzazione live delle soste degli avversari con tempi in piazzola dedotti e persistenza su `SimRIG.Pit.TransitTime`.
**NON toccare:** `Hardware/` rimane territorio di Andreas.
**Attenzione a:** Il conteggio test corrente del plugin C# è **352 PASS**. Se si aggiornano altri file di documentazione, mantenere allineato il numero reale.

---

## [2026-09-10 16:05] antigravity → chiunque entri dopo

**Task:** Prioritizzazione telemetria nativa CarIdxLapDistPct su SimHub opponent position e salvaguardia target lock su replay jump
**Piano:** —
**Commit:** `[antigravity] feat: prioritize native CarIdxLapDistPct over SimHub opponent position and safeguard target latch`

### Fatto
- `User.PluginSdkDemoEdit/OpponentTracker.cs`:
  - Implementato `GetOpponentTrackPosition(opp, state)` (r. 574-618): priorità tassativa al canale nativo a 60 Hz `CarIdxLapDistPct[carIdx]` via `IracingBridge`. Solo se non disponibile (<= 0), fallback subordinato su `opp.TrackPositionPercent`, e infine continuità su `tData.LastPosPct`.
  - In `activeOpponents` (r. 921): ammessi anche gli avversari con `GetOpponentTrackPosition(o, state) > 0.0` anche se SimHub ha `TrackPositionPercent` nullo o asincrono.
  - In `sortedOpponents` (r. 965): ordinamento basato su `GetOpponentTrackPosition`.
  - In r. 1037: `currentPos = GetOpponentTrackPosition(opp, state)` calcolato prima dell'inizializzazione di `_telemetry`, eliminando il bug per cui un valore SimHub nullo o a 0 saltava l'avversario prima ancora di poter leggere la telemetria nativa.
  - In r. 1243: aggiornato calcolo distacco vettura davanti (`gapToFront`) con la posizione nativa di `ahead`.
  - In r. 715: aggiornato ordinamento di classe per considerare la posizione nativa degli avversari e del player.
- `User.PluginSdkDemoEdit/TargetStrategyManager.cs`:
  - In `Update` (r. 429, 530, 781): aggiornati `myPos`, `oppPos` e `CurrentTarget.TrackPositionPercent` per utilizzare la posizione nativa.
  - In r. 446-480: salvaguardato `LatchedTargetName` contro micro-drop di frame o salti nel replay. Se l'avversario manca temporaneamente in `state.Opponents`, viene sintetizzato da `TrackedOpponents` o dai metadati della sessione, preservando il lock impostato dall'utente senza azzerarlo.
  - In r. 960-975 e 1024: ricalcolati i gap fisici e proiettati di overcut/undercut (`oppPosVal`) con la posizione nativa prioritaria.
  - In `SelectTarget` (r. 1474-1590): tutte le modalità (`LEADER_CLASS`, `P1..Pn`, `AHEAD`, `BEHIND`) usano `tracker.GetOpponentTrackPosition(opp, state)`.
  - In `ResetSession(bool preserveLatchedTarget = false)` (r. 1856): aggiunto parametro per preservare `LatchedTargetName` durante i salti nel replay.
- `User.PluginSdkDemoEdit/DataPluginDemo.cs:1140, 1159`:
  - Aggiunta sincronizzazione `TargetStrategyManager.ResetSession(preserveLatchedTarget: true)` su rilevamento di salto temporale nel replay (`Replay Time Jump Detected`).
  - Aggiunto `TargetStrategyManager.ResetSession()` su transizione reale di sessione.
- `User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/UnitTests/NativeIracingOpponentTrackingUnitTests.cs`:
  - Aggiunti 2 unit test: `Test_OpponentPosition_NativeLapDistPct_TakesPriorityOverSimHubTrackPositionPercent` e `Test_LatchedTarget_PreservedOnTemporaryDropOrReplayJump`.
  - Suite eseguita con successo: **347 PASS (100%)**.

### Come verificare
```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```
Atteso: 347 PASS (100%).

### Stato
- ✅ Compila (0 errori, 1 warning CS0219 noto)
- ✅ 347 PASS (100%)

### Per chi entra
**Prossimo passo:** Rivedere i log del replay su Road Atlanta per confermare la fluidità della posizione di Bruno Carneiro e la persistenza del target lock durante i salti nel replay. Procedere poi con i restanti punti dell'analisi (Punto 2 sbalzi CurrentTank, Punto 7 GapStr vs MergeGap, Punto 3 ExtendedZoneRacingTime, ecc.).
**NON toccare:** La priorità di `IracingBridge.GetLapDistPct` rispetto a `opp.TrackPositionPercent`.
**Attenzione a:** `ResetSession(preserveLatchedTarget: true)` su replay jump resetta solo i buffer temporali/delta di calcolo, conservando il target bloccato dall'utente.

---

## [2026-09-10 15:10] antigravity → chiunque entri dopo

**Task:** Fallback rilevamento InPitStall per avversario fermo su pit road (Punto 1 dell'analisi Road Atlanta)
**Piano:** —
**Commit:** `626158d`

### Fatto
- `User.PluginSdkDemoEdit/OpponentTracker.cs`:
  - Aggiunti campi `PitRoadStationaryStartSec` e `PitRoadStationaryPosPct` in `TrackedOpponent` (r. 196-198).
  - Aggiunto fallback `state.IsInPitBox` per `PlayerData.TrackSurface` (r. 603-606).
  - In `OpponentTracker.Update` (r. 1314-1355): quando `nativeTrackSurface != InPitStall` ma l'auto è su pit road (`IsOnPitRoad` o `nativeTrackSurface == AproachingPits`), se la vettura è ferma (`Speed < 0.5 km/h` e posizione stabile) per $\ge 1.0\text{s}$, promuove `effectiveInPitStall = true`, forza `TrackSurface = InPitStall`, e retrodata l'inizio sosta `InPitStallStartTimeSec` all'inizio dell'arresto.
  - Al movimento (`Speed >= 0.5 km/h`), decade a `AproachingPits` e `WasInPitStall` salva `LastPitStationaryTimeSec`.
  - In r. 1720, `statDuration` a fine sosta calcola `StationaryTimeSec > 0 ? StationaryTimeSec : LastPitStationaryTimeSec`.
- `User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/UnitTests/NativeIracingOpponentTrackingUnitTests.cs`:
  - Aggiunti test `Test_InPitStall_Fallback_WhenApproachingPitsAndStationary` e `Test_InPitStall_NativeTakesPriorityImmediately`.
- Build MSBuild e Test: **345 PASS** su 345 (100%).

### Come verificare
```bash
& "C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
& "User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```
Atteso: 345 PASS (100%).

### Stato
- ✅ Compila
- ✅ 345 PASS (100%)

### Per chi entra
**Prossimo passo:** Procedere con i punti successivi dell'analisi Road Atlanta (Punti 2, 4, 5, 7, 8, 3).
**NON toccare:** La priorità del segnale nativo iRacing (`CarIdxTrackSurface == InPitStall`).
**Attenzione a:** Il fallback si disattiva istantaneamente quando la vettura riparte (`Speed >= 0.5 km/h`), consentendo al normale ciclo di pit stop di registrare la ripartenza.

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

## Handoff più vecchi

Tutte le voci precedenti a quelle qui sopra sono in `.ai/archive/HANDOFF_LOG_archive.md`,
in ordine cronologico inverso come questo file. La prima potatura è del 2026-09-05: il file
dichiarava di tenere gli ultimi 10 e ne conteneva 22, per 112 KB letti a ogni ingresso.

*(Niente conteggi scritti qui: `grep -c '^## \[20' .ai/archive/HANDOFF_LOG_archive.md` dà il
numero esatto senza che nessuno debba ricordarsi di aggiornarlo.)*
