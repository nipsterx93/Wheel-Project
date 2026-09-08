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

## [2026-09-08 12:45] antigravity → chiunque entri dopo (Claude in particolare)

**Task:** Validazione sul campo stima consumo con mediana e latch traguardo via su Road Atlanta e Misano
**Piano:** —
**Commit:** questo

### Fatto
- **Road Atlanta (Replay `20260908_112922`)**:
  - Gara da 35 giri totali stabilizzati.
  - Latch iniziale a verde sul traguardo: `48.19 L`.
  - Giro 12 segna `2.14 L` di consumo anomalo per scia / lift.
  - Al Giro 13, con la mediana a 5 campioni (`[2.14, 2.22, 2.26, 2.27, 2.28]`), la stima mobile ha selezionato `2.26 L` (contro `2.236 L` della vecchia media aritmetica), assorbendo l'outlier al 100%.
  - Durante tutto l'In-Lap (Giro 14), `FuelToAdd` è rimasto stabilmente ancorato a **`31.00 L`** (in precedenza raccomandava solo `30.00 L`).
  - Nel replay sono stati riforniti realmente 31 L: tagliato il traguardo finale con **`0.20 L`** di riserva residua. Con 30 L la vettura sarebbe rimasta a secco prima dell'ultima curva.
- **Misano (Replay `20260908_120759`)**:
  - Gara da 26 giri totali.
  - Latch sul traguardo di verde con `50.15 L`.
  - Consumo medio su Stint 1 stabile tra `2.50 L` e `2.54 L/giro`.
  - Durante l'In-Lap (Giro 19), per tutti i 33 secondi di avvicinamento ai box `FuelToAdd` ha segnato costantemente **`16.00 L`** (33 campioni su 33).
  - Riforniti realmente 16.0 L nel replay: tagliato il traguardo al Giro 26 con **`0.19 L`** residui.
  - Zero errori o warning di sistema nei log.

### Come verificare
```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/User.PluginSdkDemo.Tests.csproj" -p:Configuration=Debug -v:minimal -nologo
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```
Criterio di successo: **324 PASS (100%)**.

### Stato
- ✅ Compila (0 errori, 1 warning CS0219 noto)
- ✅ 324 PASS

### Per chi entra
**Prossimo passo:** Analisi delle proiezioni del tempo alla bandiera (`ComputeFlagMoment` / Y-38 / Y-40) e impatto del ritardo della sosta (`PitLoss`) sul conteggio giri prima che il leader si fermi (come osservato a Misano con il passaggio da 27 a 26 giri dopo la sosta del leader).
**NON toccare:** `FuelManager.cs` nelle sezioni di detection del pit, stima con mediana e latch traguardo via.
**Attenzione a:** In `ComputeFlagMoment`, le soste future degli avversari non vengono sottratte (per evitare di stimare soste errate a 40 vetture); di conseguenza, gare a tempo con sosta obbligatoria tendono a sovrastimare di 1 giro il totale finché il leader non sconta la perdita fisica ai box.

---

## [2026-09-08 11:35] antigravity → chiunque entri dopo (Claude in particolare)

**Task:** Stima del consumo robusta con mediana su finestra a 5 giri (anti-draft / anti-lift)
**Piano:** —
**Commit:** `ad961ac` (codice e test), questo (handoff e rilascio lock)

### Fatto
- `User.PluginSdkDemoEdit/FuelManager.cs:144-160, 378, 391`:
  - Implementato `FuelManager.ComputeMedian(IReadOnlyList<double> samples)`: calcola la mediana statistica su collezioni arbitrarie di campioni ordinando l'array temporaneo (gestione campioni singoli, pari con media dei due centrali, o dispari col valore centrale).
  - Sostituito in `FuelManager.cs:378` il calcolo di `Calculations.AverageFuelPerLap` dalla media aritmetica semplice (`windowForAverage.Average()`) alla mediana mobile (`ComputeMedian(windowForAverage)`).
  - Motivazione fisica/ingegneristica: un singolo giro percorso in scia stretta o con lift-and-coast (es. Giro 12 a Road Atlanta con 2.14 L vs ritmo gara medio di 2.26 L) con la media aritmetica pesava per il 20% su una finestra a 5 giri (2.260 L -> 2.236 L). Su 21 giri residui di stint, questa flessione rimuoveva mezzo litro di carburante stimato (30.40 L -> 29.898 L), causando con `Math.Ceiling` in modalità `AGGR` l'imbarco di soli 30 L invece di 31 L (margine al traguardo di appena +0.10 L contro il reale +1.04 L necessario). Con la mediana, 1 o 2 giri isolati anomali (in difetto o in eccesso) non spostano la stima nominale; occorrono almeno 3 giri su 5 (breakdown point >= 50%) per consolidare un nuovo passo carburante.
  - Aggiornato anche il log diagnostico alla riga 391 per indicare la stima mediana corrente.
- `User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/FuelOutlierFilterUnitTests.cs:552-640`:
  - Aggiunto `Test_ComputeMedian_RobustStatistics()`: verifica il calcolo della mediana statistica su array di 0, 1, 2, 3 e 5 elementi, validando l'immunità a 1 o 2 outlier isolati (sia verso il basso che verso l'alto) e la transizione solo a fronte di persistenza (>= 3 campioni).
  - Aggiunto `Test_RoadAtlanta_MedianFuelEstimation_AntiDraft()`: replica lo scenario reale di Road Atlanta al Giro 13 pre-pit stop con la serie [2.27, 2.27, 2.23, 2.14, 2.26]. Verifica che la mediana dia esattamente 2.26 L (contro il 2.236 L della media aritmetica) e che i litri calcolati per il pit stop risultino:
    - AGGR: 31 L (invece dei rischiosi 30 L)
    - NORM: 32 L (invece di 31 L)
  - Registrati entrambi i test in `FuelOutlierFilterUnitTests.RunAllTests()`.

### Come verificare
```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/User.PluginSdkDemo.Tests.csproj" -p:Configuration=Debug -v:minimal -nologo
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```
Criterio di successo: output console termina con `ALL UNIT TESTS PASSED SUCCESSFULLY! (100%)` e il conteggio dei `[PASS]` sale a **324** (era 322).

### Stato
- ✅ Compila (0 errori, 1 warning noto CS0219 non correlato)
- ✅ Test passano: 324 PASS (conteggiati da output console)

### Per chi entra
**Prossimo passo:** Continuare secondo roadmap `.ai/plans/2026-08-24-roadmap.md` o analizzare eventuali feedback di telemetria da nuove gare/replay.
**NON toccare:** `FuelManager.cs` nelle sezioni di detection del pit/sessione già stabilizzate.
**Attenzione a:** La finestra di campionamento per il consumo medio resta a 5 giri (`Calculations.FuelAveragesWindowSize = 5`). Se in futuro si vorrà aumentare o diminuire tale finestra, la mediana manterrà sempre un breakdown point del 50% (ossia immunità a un numero di outlier pari a `(N - 1) / 2`).

---

## [2026-09-07 00:35] antigravity → chiunque entri dopo (Claude in particolare)

**Task:** Sincronizzazione precisa del via di gara sul primo taglio del traguardo (latch carburante e conteggio giri a verde)
**Piano:** —
**Commit:** questo

### Fatto
- `User.PluginSdkDemoEdit/SessionState.cs:133-134, 154-155`:
  - Introdotti campi `public bool RaceStartLineCrossed { get; set; } = false;` e `public int RaceStartLap { get; set; } = 0;`, entrambi opportunamente resettati in `Reset()`.
  - Riscritto il latch del carburante iniziale (`ManageStartingFuelLatch`). Che la partenza sia lanciata (rolling start con giro di ricognizione/formazione) o da fermo (standing start), allo sventolare della bandiera verde (`SessionStateStatus >= 4`) `RaceStartingFuel` **non viene più agganciato anticipatamente**: si attende che la vettura tagli per la prima volta il traguardo sotto bandiera verde (rilevamento transizione linea / incremento giro). Solo in quell'istante esatto viene registrato `state.RaceStartingFuel = state.CurrentFuelLevel`, `RaceStartingFuelLatched = true`, `RaceStartLineCrossed = true` e `RaceStartLap = state.CurrentLap`.
- `User.PluginSdkDemoEdit/FuelManager.cs:258-295, 335`:
  - Rimosso l'hack grezzo precedente `isRaceStartLap = state.IsRaceSession && _lastEvaluatedLap <= 1;`.
  - Finché `state.IsRaceSession && !state.RaceStartLineCrossed`, `_lastEvaluatedLap` viene mantenuto allineato a `state.CurrentLap` e nessun consumo viene contabilizzato né in telemetria né nelle medie.
  - Al frame esatto in cui `RaceStartLineCrossed` diventa `true` (primo taglio linea under green), `_fuelAtLapStart` viene agganciato a `RaceStartingFuel` (es. 46.07 L nel replay di Road Atlanta, invece dei 49.04 L alla bandiera verde): lo sprint prima della linea viene scartato a monte.
  - Al secondo taglio del traguardo (fine del primo giro effettivo di gara, es. da Lap 2 a Lap 3 in iRacing), il consumo calcolato (46.07 - 43.86 = 2.21 L) entra direttamente come **primo campione pulito** nella finestra dei 5 giri di `AverageFuelPerLap`, garantendo un allineamento istantaneo con irdashies e con la realtà.
  - Nei log di consumo viene riportato sia il giro di gara relativo (`RaceLap`) sia il giro raw di telemetria (`Lap`).
- `User.PluginSdkDemoEdit/RaceAnalyzer.cs:640-660, 750-770, 780-800, 910-1010, 1120-1155, 1280-1295, 1715-1825`:
  - `Results.RaceLapsCompleted`: calcolato come `state.RaceStartLineCrossed ? Math.Max(0, state.CurrentLap - state.RaceStartLap) : 0` (e 0 se fuori gara o prima del verde). Sia in formazione (`SessionStateStatus == 3`) che nello sprint pre-via prima della linea (`SessionStateStatus == 4`), i giri completati di gara rimangono a 0.
  - `Results.LeaderRaceLapsCompleted`: sincronizzato analogamente col giro iniziale del leader.
  - `playerAbsolutePos` e `leaderAbsolutePos`: tenuti a 0.0 finché non si taglia la linea del via under green.
  - Reso completamente null-safe `RaceAnalyzer.Update` (null-conditional su `log?.Log(...)`, guardie contro `tracker == null`, `radar == null`, `state.Opponents == null`, `fuel == null`).
- `User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/UnitTests/FuelOutlierFilterUnitTests.cs:365, 480, 500`:
  - Aggiornati i test esistenti per riflettere il latch al passaggio sulla linea.
  - Aggiunto test completo end-to-end `Test_RaceStartLineCrossed_FuelAndLapSync` che valida l'intero ciclo: formazione -> sprint pre-via -> attraversamento linea del via (giri 0, fuel latched) -> primo giro reale completato (giri completati 1, consumo 2.21 L).
- Suite test: passata da 321 a **322 test PASS** (100% verdi, 0 falliti).

### Come verificare
```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```
Atteso: build 0 errori, 322 PASS, exit code 0.

### Stato
- ✅ Compila
- ✅ Test passano (322 PASS su 322)

### Per chi entra
**Prossimo passo:** Verifica live con replay a Road Atlanta con Andreas.
**NON toccare:** `Hardware/` (territorio di Andreas).
**Attenzione a:** In iRacing durante il formation lap il giro raw può essere 1 o 2 a seconda del tracciato. Il conteggio giri di gara `RaceLapsCompleted` parte tassativamente da 0 sul primo attraversamento linea under green e scala a 1 al completamento del primo giro di gara.

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

## Handoff più vecchi

Tutte le voci precedenti a quelle qui sopra sono in `.ai/archive/HANDOFF_LOG_archive.md`,
in ordine cronologico inverso come questo file. La prima potatura è del 2026-09-05: il file
dichiarava di tenere gli ultimi 10 e ne conteneva 22, per 112 KB letti a ogni ingresso.

*(Niente conteggi scritti qui: `grep -c '^## \[20' .ai/archive/HANDOFF_LOG_archive.md` dà il
numero esatto senza che nessuno debba ricordarsi di aggiornarlo.)*
