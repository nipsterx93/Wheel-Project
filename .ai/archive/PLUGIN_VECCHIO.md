# Plugin vecchio — stato fino al 2026-09-14

> Materiale del plugin vecchio (`User.PluginSdkDemoEdit/`) spostato qui **parola per parola** il 2026-09-15, nella
> riorganizzazione dei file di progetto decisa con lo spec del plugin nuovo (`.ai/plans/2026-09-15-remastered-spec.md`,
> §8.5). Il plugin vecchio è congelato: questi punti non si correggono più lì, ma sono materiale per il brainstorming dei
> moduli del plugin nuovo (tabella in `.ai/PROJECT_STATE.md`, sezione "Plugin vecchio").
>
> I percorsi e i numeri di riga citati qui sotto sono quelli della data di ciascuna voce: la roadmap, per esempio, oggi è
> in `.ai/archive/2026-08-24-roadmap.md`. Nei rimandi interni, "questo file", "qui sotto" o "la tabella" indicano il
> `PROJECT_STATE.md` di allora.

## Contenuto

1. Da `.ai/PROJECT_STATE.md`: da dove partire, punti aperti ("Congelati in attesa di decisione"), indice dei punti
   chiusi, stato corrente, debiti noti, ruoli.
2. Da `.ai/NEW_SESSION_PROMPT.md`: il blocco per il lavoro sulle proiezioni di fine gara (aggiornato il 2026-08-31).

---

# 1. Da `.ai/PROJECT_STATE.md`

## 📋 Da dove partire, per chi arriva adesso

Se devi **rivedere il lavoro** invece di continuarlo, questo è il percorso più corto.

**Cosa è cambiato dal setup (2026-08-18) a oggi:** una quarantina di punti chiusi e una decina
aperti — l'elenco esatto è la tabella qui sotto, e **non** è ricopiato in questa frase di proposito:
una cifra scritta a mano qui è rimasta sbagliata per due settimane ("30 di cui 26", e "186 test
PASS" quando erano 295). Per il conto esatto:
`grep -c '^| Y-' .ai/PROJECT_STATE.md` (aperti) e `grep -c '^| ~~' .ai/archive/CLOSED_POINTS.md`
(chiusi). Il filo
conduttore è uno solo, ed è più interessante dei singoli difetti: quasi tutti erano **un campione
singolo non verificato che si cristallizza in un dato persistente**. Cambia solo quale campione
vince — il primo (`PitExitPct` congelato a 0.1088 per settimane), l'ultimo (`PitLaneSpeedLimit`
sovrascritto da un outlier), o il più basso (una baseline da giro parziale che nessun giro vero
poteva più correggere). Il rimedio comune è in ADR-005.

**In che ordine leggere:**

1. `.ai/ARCHITECTURE.md` — mappa dei moduli, e soprattutto **ADR-004** (come si verifica un fix qui)
   e **ADR-005** (perché un campione singolo non basta). Sono il contesto che rende leggibile il resto.
2. La tabella dei **punti aperti** qui sotto — è quella su cui si lavora.
3. `git log --oneline f526cb3..HEAD` — i messaggi di commit sono deliberatamente estesi: contengono
   il ragionamento e i numeri, non solo il cosa.
4. `.ai/HANDOFF_LOG.md` dall'alto — solo se serve la cronologia dei turni.

> **Dove è finita la storia (potatura del 2026-09-05).** Fino a oggi questi file pesavano **226 KB**
> letti a ogni ingresso di sessione, prima ancora di aprire una riga di codice. Il contenuto non è
> stato riassunto né cancellato: è stato **spostato verbatim**, e si carica solo quando serve.
>
> | Serve… | Sta in |
> |---|---|
> | il ragionamento completo di un punto **chiuso**, i numeri misurati, il commit | `.ai/archive/CLOSED_POINTS.md` |
> | un handoff più vecchio dei 10 tenuti | `.ai/archive/HANDOFF_LOG_archive.md` (12 voci, 24/08 → 01/09) |
> | qualsiasi altra cosa | `git log` — nulla è stato perso |
>
> L'indice dei punti chiusi resta più in basso in questo file: serve a sapere **che** un punto esiste
> ed è chiuso, senza caricarne il testo. Quando ti serve il *perché*, apri l'archivio a quell'ID.

**Come è stato verificato tutto:** build 0 errori, **376 test PASS al 2026-09-14**, da rileggere nell'output del runner e non da qui (erano 111 al setup, 186 al
24 agosto, 295 dopo Y-52 passo 1, 311 dopo Y-52 passo 2, 314 dopo sblocco dump `SessionDataReader`,
321 dopo allineamento CarClassID/suffissi, 322 dopo sincronizzazione start line crossing latch,
324 dopo stima consumo robusta con mediana mobile e validazione su Road Atlanta e Misano, 332 dopo cronometro reale PitZone SectorTracker, 340 dopo fix pit detection replay e class position ranking, 342 dopo esposizione proprietà Player/Target TrackSurface, 343 dopo esposizione Player/Target TrackPositionPercent, 345 dopo fallback InPitStall per avversario fermo su pit road, 347 dopo priorità nativa CarIdxLapDistPct per posizione avversari e salvaguardia latch bersaglio su drop replay, 363 al 2026-09-13 prima del piano correzioni Daytona, 367 dopo il suo passo 1, 371 dopo il passo 2, 376 dopo la diagnostica di Y-62; ⚠️ vedi Y-54: il backtest sul replay
reale si salta in silenzio se il file non c'è, quindi il numero da solo non dice quanta copertura
sia davvero girata), e per ogni
correzione la **regressione neutralizzata** — si disattiva il fix e si controlla che il test diventi
rosso. I casi di regressione usano i numeri veri presi dai log dei replay, non valori inventati.

```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
```
```bash
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```

**Dove guardare per contestare una conclusione:** i log dei replay sono in `Logs/` (gitignored, ma
presenti in locale). `Logs/3 Run Test/` contiene le tre riproduzioni di Misano che hanno stabilito la
ripetibilità della geofence; `Logs/Daytona Run/` le tre di Daytona che hanno chiuso Y-17b, Y-23, Y-24
e Y-25. Gli snapshot del database di calibrazione sono in `.ai/db-snapshots/`.

**Alcuni punti aperti aspettano dati, non tempo** (Y-14, Y-15, e la Fase 6 del piano calibrazioni):
servono replay con caratteristiche precise, indicate in ciascuna voce.

---

## 🚧 Congelati in attesa di decisione

Prendere il lock **non** autorizza a toccare questi punti: servono decisioni di prodotto, non di
implementazione. Chi decide, aggiorni questa tabella prima di far partire il lavoro.

| ID | Punto | Decisione richiesta |
|----|-------|---------------------|
| Y-14 | Derivare `TyreChangeTime` senza `TyreSelectionScope` | `TyreManager.CurrentScope` è pilotato solo dai tasti volante (`TyreManager.cs:89`), nessun percorso lo deriva dalla telemetria — in un replay non presidiato resta a `None`. **Corretto**: un replay *presidiato*, dove qualcuno guarda la sosta e riproduce lo scope a mano in tempo reale, funziona — per il plugin è indistinguibile da un pilota live. Resta comunque `EstimatedPlayer`, mai `Confirmed`: il sistema non sa che è una ricostruzione deliberata. Percorso più solido, proposto dall'utente: derivare il tempo gomme **senza mai leggere lo scope**, per sottrazione da dati già interamente grondati in telemetria — `StationaryTime` e `RefuelingTime` (quest'ultimo già isolato con precisione dai timestamp reali di `CurrentFuelLevel`, non dal solo scope). Ma la formula si biforca per layout: **Sequential** → `TempoGomme = StationaryTime − RefuelingTime` (esatta); **Simultaneous** → la sottrazione sottostima, la relazione corretta è `TempoGomme = StationaryTime` quando `RefuelingTime < StationaryTime`, altrimenti solo un limite superiore. `IsPitLayoutSequential` (`PitRadar.cs:335`) di default presume **Sequential per ogni gioco tranne iRacing**, e **Simultaneous per iRacing** finché non rilevato dinamicamente — se Daytona/IMSA è iRacing, il default è già il caso in cui la sottrazione sbaglierebbe. `TriggerDynamicLayoutDetection` (che confermerebbe quale dei due) richiede di conoscere **già** sia `tFuel` sia `tTyres`: stesso uovo-e-gallina per una classe mai vista. Il tetto anti-riparazione-danni (`OpponentTracker.cs:1007`, `tFuel+tTyres+6.0`) ha lo stesso limite, e ricade su un default generico di 26 s (`PitRadar.cs:329`) non calibrato sulla vettura reale. |
| Y-15 | Validazione `FuelToAdd` contro il reale | Oggi non c'è modo di sapere se la raccomandazione `SimRIG.Fuel.FuelToAdd` (calcolata prima della sosta) corrisponde a quanto viene poi realmente versato. Diverso dalla Fase 4: quella impara `FuelFillRate` dai litri reali, non giudica la qualità del consiglio. Il dato grezzo esiste già (`Pit Complete` logga `FuelAdded` dalla variazione di `CurrentFuelLevel`), manca solo l'abbinamento col valore predetto, congelato al momento di `PitLaneEntered` (il gate della Fase 3 lo espone già). Diagnostico puro, non tocca il sistema di confidenza. Stesso pattern già usato a mano in `ReplayBacktestIntegrationTest` (`Ground Truth: 16.0L` scritto nel test) — qui diventerebbe automatico, letto dalla telemetria invece che digitato. |
| Y-26 | Dati pit lane cristallizzati senza consenso | ⚠️ **Chiuso in parte** (`c646d7b`). Trovato da Antigravity in revisione. **Chiuso**: `PitInOutAccDecTime` (osservazione incidentale da ogni sosta avversaria, molti campioni) ora passa da `CalibrationConsensus` con tolleranza 2 s; `PitTransitTime` nelle due procedure **guidate** (SplashAndDash, TyreChange) ha perso il lock `== 0.0`, che non proteggeva da nulla e impediva solo di rifare una calibrazione riuscita male. **Resta aperto**: `PitDriveThroughTime` e il ramo `StopAndGo` mantengono `== 0.0`, perché `DriveThrough` è la modalità di ripiego in cui si finisce anche in gara — senza lock, un transito qualunque riscriverebbe la calibrazione, e nel ramo StopAndGo dirotterebbe le soste dall'apprendimento naturale della Fase 4. Chiuderlo richiede **distinguere una calibrazione guidata da un transito incidentale**, che il codice oggi non sa fare: è la stessa decisione di prodotto del punto Y-28. |
| Y-29 | `FuelFillRate`/`TyreChangeTime` senza consenso nel percorso **non guidato** | Trovato preparando il piano d'implementazione di Y-28 (`.ai/plans/2026-08-25-calibration-cascade-implementation.md`). Il ramo "else" di `PitRadar.Update` (Fase 4 originale: apprendimento da una sosta di gara qualunque, non da una calibrazione deliberata) scrive `EstimatedPlayer` senza consenso fra osservazioni multiple — `CanOverwrite(EstimatedPlayer, EstimatedPlayer)` è vero, quindi l'ultima sosta naturale osservata sovrascrive la precedente. Stesso schema di Y-26/Y-27, su un percorso diverso: la cascata guidata (Y-28) non lo attraversa mai, quindi non blocca quel lavoro. Non ancora corretto. |
| Y-33 | `Player Spatial Pit Entry` scatta a ogni giro in pista | Road Atlanta `20260828_205434`. In gara il Player si è fermato **una volta sola** (giro 21), eppure l'evento di ingresso corsia box è stato emesso **a ogni giro dal 22 al 34 — tredici volte** — mentre era in pista, sempre a `Pos: 0.9576`. Il rilevamento scatta sulla sola posizione sul tracciato, senza distinguere chi entra davvero da chi passa di lì. Dopo la sosta vera, il primo "ingresso" registrato è a `Pos: 0.0884`, cioè il punto di **uscita** archiviato come ingresso. Spiega in un colpo solo le tre cose osservate nella practice del 28/08: i drive-through mai confermati (nessun transito si chiude, ne viene aperto uno nuovo ogni giro), la geofence corrotta (`Pit Entry Pct Calibrated 0.070`, consenso 1/2 poi 2/3, contro lo `0.957` corretto imparato in gara — l'utente ha **visto** `PitEntryPct` e `PitExitPct` quasi identici nel database), e il `PitTransitTime` di ~4 s trovato nel JSON. Correlati: `InOutPitAccDecTime=-47.65s` (negativo), e valori appresi che non convergeranno mai (9.55 / 6.87 / 12.35 / 12.13 / 10.48 s con tolleranza di consenso 2 s). L'utente chiede una revisione **alla radice**, non un ritocco, più un log dedicato alle calibrazioni che viva in Practice/Test e taccia in gara (attenzione: `LogManager` oggi fa l'opposto, scarta le righe fuori dalla gara). |
| Y-36 | Salto del totale giri all'uscita dai box | Road Atlanta `20260830_113151`: `Pit Complete` alle 11:44:03.455 e **1.3 s dopo** il totale del Player sale da 35 a 36 (`PosAtFlag` grezzo `35.151`, appena oltre la soglia di salita `35.05`), per tornare a 35 due giri dopo. Nel run precedente (`102220`, stessa gara, stessa sosta) **non succedeva**: non e' una regressione ma un transitorio che prima era mascherato: con il passo del leader corretto (69 s invece di 61) il supplemento di fine gara e' piu' lungo, la proiezione siede piu' in alto e lo stesso transitorio ora supera la soglia. Sospetto: il ramo di correzione per la sosta (`RaceAnalyzer.cs:874-891`) si spegne all'uscita dai box quando `FuelToAdd` crolla e il serbatoio torna pieno, e `playerL_left` salta al valore non corretto. **Da verificare con `PosAtFlag` tick per tick attorno all'uscita**, non a intuito. |
| Y-52 | Metadati di sessione dallo YAML (4 passi) | 🟡 **Passi 1 e 2 di 4 fatti + Sblocco dump SessionDataReader + Affinamento Seeding/Fuel** (Passo 1: `bd00979`; Passo 2: 2026-09-05; Sblocco Dump: 2026-09-06; Seeding al via: 2026-09-06). Nasce dall'analisi di irdashies: iRacing pubblica nel SessionInfo YAML dati che oggi rimpiazziamo con ripieghi cablati. **La premessa era gia' verificata sul campo**: `TelemetryReader.cs:90-94` legge lo YAML con quattro fallback e i log di gara lo provano — `BoP Pct: 0.500` non può venire da altro che dal parsing di `CarClassMaxFuelPct`. **Passo 1 (fatto):** `SessionMetadata` (contenitore agnostico, campi nullable) + `SessionYamlParser` (fornitore iRacing, funzione pura) + cache per sessione in `TelemetryReader` + dump `SimRigMetadata.json` su cartella configurabile. **Passo 2 (fatto):** seeding `DriverCarEstLapTime` e `CarClassEstLapTime` nei ripieghi di passo di `RaceAnalyzer.cs` (player, leader e avversari al via) e `TargetStrategyManager.cs`. Introdotto `IsLapsPredictionValid` su `RaceAnalysisResult` e pubblicato su SimHub come `SimRIG.Session.IsLapsPredictionValid` (true su gare a giri o gare a tempo con passo misurato/YAML; false fuori gara o su mero ripiego forfettario). Risolto il "buco nero dei primi 3 giri" (a Road Atlanta GT3 dava 23 giri con ripiego a 120s; ora 36 giri al semaforo verde). **Sblocco Dump SimHub (fatto 2026-09-06):** Risolto il problema del dump `SimRigMetadata.json` mai generato su disco. SimHub non popola la stringa YAML grezza ma espone l'oggetto deserializzato da `iRacingSDK` (`SessionData`/`DataSample` via `GetRawDataObject()`) e proprietà `DataCorePlugin.GameRawData.SessionData.*`. Creato `SessionDataReader.cs` con reflection e fallback, integrato in `TelemetryReader.cs`. 3 unit test aggiunti (314 PASS). **Affinamento Seeding al via & Fuel Giro 1 (fatto 2026-09-06):** Silenziate le proiezioni in griglia (`SessionStateStatus < 4` o countdown negativo); congelato `FuelToAdd` a 0.0 nel Lap 1; sincronizzato latch carburante e flag puliti al verde; sbloccato `FuelToAdd` su consumo reale live al termine del Lap 1. 4 unit test aggiunti (318 PASS). **Passi 3-4 (da fare):** (3) `DriverPitTrkPct` → distanza metrica alla piazzola box; (4) densità carburante, incidenti, gomme, partenza. |
| Y-40 | Il passo del leader **deriva** durante la gara, e con lui i giri totali | Road Atlanta `20260830_121813`, con `L_PosAtFlag` finalmente visibile (Y-37). La proiezione del leader **parte corretta**: `38.814` al giro 2, contro i `38.8` del software di riferimento. Poi deriva: `39.564`, `39.040`, `39.504`, `39.284`, fino a `40.925` nella finestra del passo sballato, e **rientra solo a fine gara** (`38.371`, `38.245`, `38.146`, `38.095`). In parallelo `L_Pace` scende da `69.540` a `67.484-68.132` mentre i giri reali del leader misurati dai passaggi valgono `69.4-71.4`. **Due domande aperte, da chiudere con un breakdown del flusso dati e non a intuito:** (1) perche' la media mobile del passo deriva verso il basso invece di stabilizzarsi sui giri veri; (2) quanto pesa il fatto che la proiezione usi il tempo **normalizzato** (a serbatoio scarico) invece del tempo che la vettura fara' davvero — per il leader la penalita' carburante vale `2.40 s` su `71.9` (**3.3%**), contro `1.21 s` su `77.5` (**1.6%**) del Player, e il 3.3% su 38.8 giri fa circa **+1.3 giri**. **Perche' si aggiusta solo alla fine:** la parte *proiettata* (che usa il passo) si riduce a zero col passare del tempo, mentre la parte *misurata* (i giri gia' completati) cresce — quindi l'errore si annulla da solo alla bandiera, indipendentemente dalla causa. Nota sulla domanda "mischiamo le classi?": la baseline e' **per vettura** (`tData`), quindi no; ma la *normalizzazione* dipende da `estimatedFuel`, che deriva da parametri **di classe** (`classMaxTank`, `classFuelBurn`) — un errore li' biasa in blocco tutte le vetture di quella classe. E' l'unico punto dove una grandezza di classe entra in un passo per vettura. |
| Y-38 | Identita' del leader assoluto che sfarfalla | ⚠️ **Meccanismo dimostrato il 2026-09-01, il punto resta aperto.** Il salto di `RaceTotalLaps` a 37 nel replay `20260831_195300` e' suo, e la catena e' chiusa aritmeticamente: alle 20:04:08.520 il P1 assoluto passa da `Sven Neiss` (`L_Pace` 68.443) ad `Alessandro Barbagallo`, che porta con se' un passo registrato di **278.563 s**; `L_PosAtFlag` crolla nello stesso tick da `38.185` a `27.985`. Il supplemento di `TimeUntilLeaderCheckered` vale al massimo **un giro del leader**, quindi passa da ~68 a fino a 278 s: sul Player sono `278.563 / 76.524 =` **3.64 giri in un fotogramma**. Serviva superare 36.05 per arrivare a 37 e con quel passo si arriva a 37.8. ⚠️ **Correzione a una diagnosi precedente:** i "cinque tick sopra 35.05" registrati nell'handoff del 31/08 **non sono la causa** — quattro dei cinque (20:04:23-26) cadono *dopo* che il totale era gia' 37, e il piu' alto vale 35.435, insufficiente. Il fotogramma colpevole non e' a log: la diagnostica scrive **1 riga/s** mentre il calcolo gira almeno a 12 Hz (misurato: intervallo massimo 1.085 s con strozzatura a 1.0 s). Da qui la riga `Total Laps Transition`, che scatta al **cambio** del totale con dentro l'ingresso che l'ha causato. Diagnosi originale: | Road Atlanta `20260830_113151`: nella finestra dei giri 19-24 il P1 assoluto cambia fra `Sven Neiss` (96 tick), `Alessandro Barbagallo` (29) e `Kalyann Mey4` (28). Ogni cambio porta con se' il passo di quella vettura. `LeaderPaceFilter` ha un dwell di 2 s sull'identita': evidentemente non basta. **Il punto 4 (bandiera = minimo del tempo di attraversamento su tutte le vetture) lo dissolve** e gira gia' in modalita' ombra da questo turno: sugli stessi numeri la vettura col passo a 278 s attraversa a 1114.3 s contro i 934.9 s del leader vero, quindi **perde il minimo da sola** e la proiezione del Player resta 34.29 invece di saltare a 37. |
| Y-54 | Il backtest sul replay reale si auto-salta in silenzio | Trovato il 2026-09-05 (stessa review, R-2). `User.PluginSdkDemo.Tests/IntegrationTests/MisanoHuracanGT3ReplayTest.cs:20` cabla `@"E:\SimHub\Replays\IRacing\20260303_151035.telemetry.json"` — **unico path assoluto rimasto** nel codice compilato, contro la convenzione esplicita "mai nuovi path assoluti hardcoded". Se il file manca (`:26-30`) il test stampa `Skipping full replay backtest` e **ritorna senza fallire**: il runner chiude comunque exit `0`. Il conteggio "295 PASS" non distingue quindi la copertura girata da quella saltata — è il pattern che ADR-004 combatte, spostato dal contenuto del test alla sua assenza. Rimedio proposto: path derivato da `SIMHUB_INSTALL_PATH` e conteggio `SKIPPED` separato dai `PASS`. |
| Y-56 | Un lock non pushato non serializza niente, e `human` non dice **quale** umano | Trovato il 2026-09-05. **Da decidere fra Andreas e Michael, non da un agente** — per questo è registrato e non corretto. Due difetti nello stesso punto. **(a)** Il lock vive in `PROJECT_STATE.md`, cioè in Git: ma finché il commit resta locale, il lock è **privato**. Il 2026-09-05 il lock è stato preso e rilasciato due volte su una macchina, con 5 commit mai pushati: per l'altra parte quelle acquisizioni non sono mai esistite. Se entrambi l'avessero preso in contemporanea, entrambi avrebbero creduto in buona fede di averlo — ADR-001 dice che la rete di sicurezza è Git, ma Git serializza solo ciò che è stato pushato. Rimedio proposto: `AGENTS.md` impone **`git push` subito dopo il commit che prende il lock** (e prima di scrivere codice), così l'acquisizione è visibile a chiunque. **(b)** Il blocco `LOCK` ammette `human` come owner unico, ma gli umani sono **due** con macchine e capacità diverse (Andreas: Windows, SimHub, `Logs/`, può verificare; Michael: macOS, non può). Rimedio proposto: `human:andreas` / `human:michael`. ⚠️ Conseguenza già in atto: tutta `.ai/` dice **"l'utente"** al singolare — in ogni frase tipo "i replay esistono solo sulla macchina dell'utente" oggi non è più chiaro chi sia. |
| Y-55 | `CustomDialog.xaml.cs` non compilato e non documentato | Trovato il 2026-09-05 (stessa review, R-3). Il confronto disco↔`<Compile>` del `.csproj` principale dà **quattro** `.cs` esclusi: i tre `_LEGACY` (documentati in `CLAUDE.md`) più `CustomDialog.xaml.cs`, che non compare nel `.csproj` né come `Compile` né come `Page`. Stessa trappola dei `_LEGACY` — modificarlo non ha alcun effetto sul plugin — ma senza il cartello che avverte e senza il suffisso che la renderebbe evidente dal nome. Da decidere: eliminarlo, o documentarlo fra le trappole. |
| Y-58 | Leader: buchi di dati, `LeaderRaceLapsCompleted` di nuovo a 0, dead reckoning mai attivato | Trovato il 2026-09-13 (claude, replay Daytona `20260913_140133` e `_163743`). Regressione di Y-25 introdotta da `863c65c`: il hold della posizione (`RaceAnalyzer.cs:599-621`) disinnesca `HoldLeaderLapsCompleted` (`:669-670`) e il dead reckoning (`:848-859`, `:1622-1625`). Giri a 0 nel 24.4% delle righe, posizione congelata fino a ~152 s, 0.0 per ~530 s dal via (il replay manda `NotInWorld` le vetture lontane dal Player). Backtest: posizione congelata errore mediano 0.11-0.12 giri (max 1.76), dead reckoning 0.02-0.04 (buco più lungo −0.01). **Deciso da Andreas (2026-09-13):** `SimRIG.Leader.TrackPct` mostra la posizione stimata. Piano: passo 4 di `.ai/plans/2026-09-13-daytona-piano-correzioni.md`. Dettagli, fix e test: `.ai/reviews/2026-09-13-daytona-leader-mergegap-pitloss.md` §3. |
| Y-59 | MergeGap: il latch della sosta Player congela un gap calcolato su Target `NotInWorld` | Trovato il 2026-09-13 (claude), stesso replay. Congelato −9.8 s contro −2.7 s reali (errore −7.1 s, ottimista). Nei 20 s prima dell'ingresso il Target è `NotInWorld` con posizione ferma: il gap per microsettori (`TargetStrategyManager.cs:713-731`) cresce ~1 s/s e `_lastOnTrackProjectedMergeGap` (`:242-250`) lo registra. Con l'ultimo gap fresco il latch avrebbe tenuto −1.79 s. Il latch di `722a9d6` era validato solo su Road Atlanta. Dettagli e test: review §3. |
| Y-60 | Stazionario avversari dedotto dalla finestra `NotInWorld` invece che dal tempo in corsia | Trovato il 2026-09-13 (claude). Vincolo confermato da Andreas: lo stazionario avversario si può solo stimare dal nostro `PitTransitTime`. `OpponentTracker.cs:1722` però sottrae il transito a `NotInWorldDuration`, che esclude i secondi in cui la vettura è visibile in corsia (fino a 5.9 s): sottostima. Proposta iniziale `TotalTime − PitTransitTime` (su 12 soste senza gomme lo scarto dal carburante atteso passa da +2.40 s a −0.25 s), **rivista dopo il confronto con Andreas**: con l'auto `NotInWorld` la pit road la forziamo noi (`OpponentTracker.cs:1349-1360`), quindi il tempo in corsia è affidabile solo se l'uscita è osservata; AccDec avversario ~5 s (6 soste su 32) = uscita non osservata. Piano: passo 5 di `.ai/plans/2026-09-13-daytona-piano-correzioni.md`. Collaterale: `Pit Loss Dissection` legge il transito di corsa invece di quello della sosta (solo log). Dettagli: review §3. |
| Y-61 | Perdita ai box: `ExtZone` "best" e stazionario Target col consumo del Player | Trovato il 2026-09-13 (claude). Il MergeGap pre-sosta resta +3.1 s pessimista anche con `PitInOutAccDecTime` 11.6 (l'AccDec era un errore di modo comune). Cause (**precisate dopo il confronto con Andreas**): il calcolo MergeGap/undercut (`TargetStrategyManager.cs:952-977`) non usa il consumo proporzionato al BoP che `OpponentTracker.cs:1093-1110` calcola già (Target 3.60 L/giro contro i 3.0 del Player: stazionario 12.79 s contro ~16 s, autonomia +20%); `ExtZone` = minimo di classe (`OpponentTracker.cs:890-907`: 21.93 s) invece della mediana dei transiti del Player, già salvati in `SectorTracker.RawNormalHistory` (23.8 s: perdita +1.9 s per tutti); AccDec per avversario senza banda di plausibilità (4.6-17.9 s, un caso a −454 s). Scomposizione: review §3 e §9. Piano: passi 1, 2 e 5 di `.ai/plans/2026-09-13-daytona-piano-correzioni.md`. **Passo 1 fatto il 2026-09-13** (`05f0002`): la previsione della sosta del Target usa il suo consumo BoP e il suo serbatoio, anche per `SimRIG.Target.TankLapsRemaining`; replay da verificare. Restano i passi 2 (`ExtZone`) e 5 (AccDec avversari). **2026-09-14:** replay `070557` — giri 11-15 come previsto (errore medio del MergeGap prima delle soste da +3.74 a +0.37 s), ma regressione nei giri 1-10 per il tetto sullo spazio libero attuale (errore fino a +15.5 s); corretta in `c18a1b0` (tetto = capienza del Target); **verificata sul replay `082515`**: errore medio +0.33 s nei giri 2-15, tutti i blocchi fra −0.43 e +0.87 s. **Passo 2 fatto il 2026-09-14** (`3e9d4ae`): la perdita ai box (MergeGap/undercut e `SimRIG.Pit.TotalPitLoss`) sottrae la mediana degli ultimi 7 transiti del Player nella zona estesa invece del minimo di classe; **verificato sul replay `094551`**: `ExtZone` 23.73 s dal giro 7 (era 22.18), perdita del Player 33.84 s (reale 34.7), MergeGap subito dopo la sosta del Target −2.93 s (era −1.30, reale −2.7). Resta il passo 5 (AccDec avversari). `RaceAnalyzer.cs:1186` (proiezione del totale giri) usa ancora il minimo di classe. |
| Y-62 | Undercut che si spegne per "traffico" una volta al giro, sempre a metà giro | Trovato il 2026-09-14 (claude, replay Daytona `082515`; stessi TL in `070557`, e a TL 2395.1 già in `163743`). Col passo 1 l'undercut sul Target è viable quasi tutta la gara. Ogni giro, quando il Player è fra i macrosettori 8 e 10 (di 20), scatta `UNDERCUT_NONVIABLE reason=Traffic` per ~5 s e poi torna viable: 11 dei 32 `STRATEGY_CHANGED` del run, a intervalli di ~106 s, cioè un giro del Player. Causa nel codice (`TargetStrategyManager.cs:1248-1268`, righe dopo `32a8682`): il conflitto richiede che la vettura nella bolla di ±3 s attorno al rientro proiettato sia **adesso** entro ±0.05 giri dall'uscita box estesa. Una vettura della classe ~35 s dietro (la perdita ai box del Player) è vicina all'uscita box solo mentre il Player è a metà giro: il filtro segue la posizione del Player, non il traffico al rientro. Quale vettura sia non è verificato: il DebugLog non registra il traffico. Rimedio da valutare: controllo spaziale sulla posizione dell'avversario al momento del rientro, oppure la sola bolla temporale. Fuori dal piano Daytona. **Deciso da Andreas (2026-09-14):** si corregge dopo il passo 2 e prima del passo 3 del piano. **Analisi del 2026-09-14 (claude):** i log di `094551` non identificano la vettura. Nei giri 15–19 ci sono vetture di classe davvero nella bolla (distacchi veri al passaggio da 0.959), che il controllo vede solo col Player a metà giro; nei giri 2–14 nessuna vettura è osservata fra 7 e 53 s dietro al Player, proprio la fascia della bolla. Due ipotesi: H1 vettura vera al bordo della bolla, portata dentro dalla stima posizione × passo; H2 vettura `NotInWorld` con la posizione ferma all'ultimo valore memorizzato (`OpponentTracker.cs:728-732`). **Deciso da Andreas (2026-09-14):** prima la diagnostica, poi il fix. Diagnostica in `32a8682` (DebugLog: `Pit Exit Traffic Candidate` e `Pit Exit Traffic Conflict`; la decisione non cambia), in attesa del replay. Cosa cercare: voce del 2026-09-14 12:33 in `.ai/HANDOFF_LOG.md`. **Replay `124637` (claude, voce del 2026-09-14 13:19):** nei giri 2–15 la causa è H2 in 13 finestre su 16 (soprattutto Leon van Elewout, Dallara P217, `NotInWorld` con la posizione ferma a 0.1883 dentro la finestra dell'uscita box), H1 in 1 (Hilden, distacco vero +4.6 s), traffico reale in 2 (giro 14). Secondo difetto nello stesso controllo: la minaccia delle altre classi usa `SectorBaseline × 3.0` (`TargetStrategyManager.cs:1240`), ma quella baseline è la zona di corsa fuori dai box, ~75% del giro (`OpponentTracker.cs:2501`): anche le classi più veloci risultano minaccia. Fix proposto nella voce di handoff, da confermare con Andreas. |

### Punti già chiusi — indice

Il testo completo di questi punti (ragionamento, numeri misurati, regressione
neutralizzata) è in **`.ai/archive/CLOSED_POINTS.md`**. Qui resta solo l'indice: serve a
sapere che un punto esiste ed è chiuso, senza caricarne 47 KB a ogni sessione.

| ID | Punto | Esito |
|----|-------|-------|
| ~~Y-1~~ | `CanFinishWithoutPitting` | ✅ `bc20a67` |
| ~~Y-2~~ | `OvercutTrafficOK` cablato a `true` | ✅ `f08bf43` |
| ~~Y-3~~ | `LapsSinceLastPit` | ⏹️ — |
| ~~Y-8~~ | Deadband HUD 0.05 | ⏹️ — |
| ~~Y-9~~ | Euristica pit del Player | ✅ `81a5f12` |
| ~~Y-11~~ | Modello warmup | ✅ `8b42efd` |
| ~~Y-12~~ | Isteresi dei gate strategici | ✅ `1fa7b15` |
| ~~Y-13~~ | Gap che salta di un giro al rollover | ⚠️ `96915ef` |
| ~~Y-51~~ | Timestamp dei microsettori troppo radi per la precisione richiesta dai gate | ✅ `96915ef` |
| ~~Y-16~~ | `RaceLifeTimeLeftSec` ricostruito dal leader | ✅ `cc35f97` |
| ~~Y-17~~ | Passo del leader contaminato dallo sfarfallio | ✅ `cc35f97` |
| ~~Y-17b~~ | **Causa vera** del passo del leader sbagliato | ✅ `e907b67` |
| ~~Y-18~~ | Due definizioni divergenti di "fermo ai box" | ✅ `cedc2aa` |
| ~~Y-19~~ | Tetto sui giri totali del Player in multiclasse | ⏹️ `dbcb168` |
| ~~Y-20~~ | `PitLaneSpeedLimit` "l'ultimo che scrive vince" | ✅ `e9caad6` |
| ~~Y-21~~ | Geofence riscritte da un campione singolo | ✅ `e9caad6` |
| ~~Y-22~~ | Quale sia il valore vero di `PitExitPct` a Misano | ✅ — |
| ~~Y-23~~ | Visite fantasma in corsia box da sfarfallio di `IsInPitLane` | ✅ `0b52641` |
| ~~Y-24~~ | `LeaderRaceLapsRemaining` bloccato sul totale latchato | ✅ `2c13874` |
| ~~Y-25~~ | `LeaderRaceLapsCompleted` lampeggia a zero | ✅ `cd97b4e` |
| ~~Y-30~~ | Voce dell ingegnere: tono e ripetizioni | ✅ `9c79ece` |
| ~~Y-28~~ | Calibrazione guidata in Practice | ✅ `490902a` |
| ~~Y-27~~ | `BaseCapacity` sovrascritta senza consenso | ✅ `c646d7b` |
| ~~Y-31~~ | Totale giri del Player: dente d'arresto a +1 | ✅ `dbcb168` |
| ~~Y-32~~ | Passo del leader sbagliato | ✅ `9cbc01c` |
| ~~Y-35~~ | Posizione del leader assente, letta come "leader sul traguardo" | ✅ `66678e0` |
| ~~Y-37~~ | Proiezione del leader senza decimali | ✅ `9d16172` |
| ~~Y-39~~ | Baseline anomale residue dopo Y-32 | ✅ `579b77c` |
| ~~Y-41~~ | `MaxTank` per classe invece che per vettura | ⚠️ `0cb0e93` |
| ~~Y-43~~ | Penalita' carburante applicata per litro invece che per kg | ✅ `7fe6d58` |
| ~~Y-42~~ | La correzione per la sosta sottrae piu' del dovuto | ✅ `7fe6d58` |
| ~~Y-44~~ | Il **valore** del tempo di sosta e' sovrastimato | ⚠️ — |
| ~~Y-45~~ | Il ritardo di 30 s sulla discesa si riarma da solo e blocca il totale | ✅ — |
| ~~Y-46~~ | Il tetto leader→Player si riapplica senza condizione in multiclasse | ⚠️ — |
| ~~Y-47~~ | Verita' di terreno sulla posizione del leader allo scadere | ⚠️ — |
| ~~Y-48~~ | Il totale del leader seguiva ancora il P1 istantaneo, col punto 4 gia' acceso | ✅ — |
| ~~Y-49~~ | L'ancora del passo sa migliorare, ma non ci arriva mai | ⚠️ — |
| ~~Y-50~~ | Il filtro IQR sul carburante si chiude sui rifiuti e non si riapre | ⚠️ `3ad938f` |
| ~~Y-34~~ | Arrotondamento di `FuelToAdd` all'intero | ⚠️ `0355676` |
| ~~Punto 4~~ | Bandiera dalla vettura al comando | ✅ vedi archivio |
| ~~Y-53~~ | `PaddleClutch.h` manca dal repository | ✅ versionato |



---

## 📍 Stato corrente

**Dal 2026-09-15: riscrittura del plugin** (deciso da Andreas). Il plugin nuovo nasce in `User.PluginSdkDemoRemastered/`, una logica alla volta; lo spec è in `.ai/plans/2026-09-15-remastered-spec.md` (design approvato da Andreas, spec in revisione). Il plugin vecchio (`User.PluginSdkDemoEdit/`) è **congelato**: niente correzioni salvo guasti bloccanti; il piano correzioni Daytona e il fix di Y-62 sono sospesi. Le righe qui sotto descrivono lo stato fino al 2026-09-14.

**Fase attiva della Roadmap:** **piano correzioni Daytona** (Y-58…Y-61, `.ai/plans/2026-09-13-daytona-piano-correzioni.md`), prima di riprendere Y-52 (Metadati di Sessione da iRacing / irdashies), in pausa — vedi [roadmap.md](.ai/plans/2026-08-24-roadmap.md). Le due righe qui sotto sono lo stato di Y-52.
- **Passi 1 e 2:** Completati e testati (contenitore agnostico `SessionMetadata`, seeding passo stimato e validità `IsLapsPredictionValid`).
- **Stabilizzazione Fuel al via e Start Line Crossing Latch (2026-09-07):** Sincronizzato con precisione il latch di `RaceStartingFuel` e l'avanzamento dei giri al primo attraversamento effettivo della linea del traguardo sotto bandiera verde (`RaceStartLineCrossed`). Lo sprint pre-traguardo della rolling start viene escluso dal calcolo dei litri; al completamento del primo giro di gara (Giro 1 -> 2 di gara) entra il consumo pulito e reale (2.21 L a Road Atlanta) nella finestra a 5 giri di `AverageFuelPerLap`, garantendo allineamento immediato con irdashies e con la realtà (322 test PASS).
- **Prossimo lavoro tecnico (deciso da Andreas il 2026-09-13):** piano `.ai/plans/2026-09-13-daytona-piano-correzioni.md`, passi 1 → 5 in ordine, eseguiti da claude, un passo per turno col lock. Y-52 Passo 3 (`DriverPitTrkPct` → coordinata metrica piazzola box) e Passo 4 (densità carburante reale, opzioni gara) riprendono dopo. **Passo 1 fatto il 2026-09-13** (`05f0002`) e **corretto il 2026-09-14** dopo il replay `070557`: il tetto sul carburante previsto del Target è la sua capienza, non lo spazio libero (regressione nei giri 1-10). **Verificato sul replay `082515`**: errore medio del MergeGap prima delle soste +0.33 s nei giri 2-15 (era +4.84). **Passo 2 fatto e verificato il 2026-09-14** (`3e9d4ae`, replay `094551`); in corso Y-62 (deciso da Andreas il 2026-09-14): diagnostica aggiunta (`32a8682`), il fix dopo il replay; poi i passi 3-5.
- **Prossima fase strategica:** Fase B (Verifica consigli undercut/overcut contro esito reale di gara su replay idoneo fornito dall'utente).

### Contesto del progetto

Plugin SimHub in C# / .NET Framework 4.8 (`User.PluginSdkDemoEdit/`), assemblato come libreria
`User.PluginSdkDemo.dll` e copiato in `%SIMHUB_INSTALL_PATH%` da un post-build event.
Il cuore funzionale è la strategia di gara: gestione pit, carburante, gomme, tracking avversari,
annunci vocali (Piper TTS) e telemetria.

---

## ⚠️ Debiti noti (da affrontare, non ancora pianificati)

Rilevati durante il setup e la verifica:

1. ~~**Il progetto di test non è nella solution.**~~ ✅ *Risolto:* `User.PluginSdkDemo.Tests.csproj` aggiunto a `User.PluginSdkDemo.sln`.
2. ~~**Path assoluti hardcoded nel `.csproj`.**~~ ✅ *Risolto:* reference a `Newtonsoft.Json` e `SharpDX` convertite in `$(SIMHUB_INSTALL_PATH)`.
3. ~~**RED-1 — reference pit-contaminata nel RelativePace.**~~ ✅ *Risolto* (commit `1e296cf`):
   `RelativePaceTracker.cs` con flag di contaminazione, test obbligatori 8 e 9 a copertura.
4. **File `*_LEGACY.cs` orfani.** `DataPluginDemo_LEGACY.cs`, `FuelCalculator_LEGACY.cs`,
   `PitStrategyManager_LEGACY.cs` (~180 KB totali) sono sul disco ma **non** nel `<Compile>`
   del csproj: non vengono compilati. Rischio concreto che un agente li legga o li modifichi
   credendoli attivi.
5. **File sorgente molto grandi.** `DataPluginDemo.cs` (~155 KB), `OpponentTracker.cs` (~105 KB),
   `SettingsControlDemo.xaml` (~99 KB). Vanno letti a fette, non in blocco.

---

## 👥 Ruoli

> **Aggiornato 2026-09-06** (decisione Andreas, discussa con Claude e Antigravity): **niente più
> compiti esclusivi per agente.** La versione precedente di questa tabella assegnava
> un'area a testa (Antigravity=architettura, Claude=implementazione, Codex=review). Si è deciso di
> abbandonarla: ogni agente può fare brainstorming, scrivere codice, revisionare — **il controllo
> di qualità viene dal fatto che un agente diverso da chi ha scritto rilegge**, non dalla divisione
> del lavoro per competenza dichiarata. Vale ancora il protocollo del lock (un solo scrittore alla
> volta, vedi sopra): cambia solo *chi* può prendere in mano un task, non *come* si passa il turno.

| Agente | Cosa può fare | Nota |
|--------|----------------|------|
| **Antigravity** (Google/Gemini) | Tutto: brainstorming, planning, codice, review | Ambiente Windows/VS/MSBuild nativo |
| **Claude Code** (Anthropic) | Tutto: brainstorming, planning, codice, review | CLI, esecuzione build/test |
| **Codex/ChatGPT** (OpenAI) | Tutto: brainstorming, planning, codice, review | Nessun accesso diretto al filesystem del progetto in questo setup — tipicamente usato per second opinion su un lavoro già fatto |

**Principio guida:** *uno corregge l'altro*. Chi finisce un turno non è l'ultima parola — la sessione
successiva (qualunque agente sia) rilegge con occhio critico invece di dare per buono il lavoro
precedente, come già previsto dalla sezione "Sessioni di revisione" in `AGENTS.md`.

---

# 2. Da `.ai/NEW_SESSION_PROMPT.md`

## Blocco per il lavoro in corso — proiezioni di fine gara (aggiornato 2026-08-31)

> Da incollare **dopo** il blocco generico, quando si riprende il filone su cui si sta lavorando
> adesso. Il blocco sopra ricostruisce il contesto del progetto; questo dice a che punto siamo su
> questo lavoro specifico e cosa fare.

```
LAVORO IN CORSO: la proiezione dei giri di fine gara (quanti giri completeremo io e il leader
assoluto quando esce la bandiera). È il numero da cui dipende quanto carburante imbarcare:
sbagliarlo di un giro significa sbagliare il rifornimento di ~2.3 litri.

LEGGI QUESTI, IN QUEST'ORDINE, PRIMA DI PROPORRE QUALSIASI COSA:

1. .ai/HANDOFF_LOG.md — la prima voce (2026-08-31). Contiene i numeri misurati, le trappole in cui
   sono già cascato, e l'ordine di lavoro concordato con me.

2. .ai/plans/2026-08-30-formule-corrette-fine-gara.md — LE FORMULE CORRETTE, da una revisione
   esterna. ATTENZIONE: nel PDF originale (DeepSearch/) le equazioni sono IMMAGINI, non testo: se
   cerchi nel testo del PDF non le trovi. Lì sono trascritte.

3. .ai/plans/2026-08-30-analisi-dahldesign.md — analisi di un plugin open source (Andreas Dahl,
   in "Solo per analisi logiche/") che risolve gli stessi problemi. Utile perché conferma lo
   scheletro e mostra soluzioni più semplici delle nostre su alcune parti.

4. .ai/PROJECT_STATE.md — i punti Y-31 … Y-44. Quelli aperti sono Y-13, Y-14, Y-15, Y-26, Y-29,
   Y-33, Y-34, Y-36, Y-38, Y-40, Y-44.

DOVE SIAMO, IN BREVE

Sette correzioni fatte (Y-31, Y-32, Y-35, Y-39, Y-41, Y-42, Y-43). Le proiezioni ora sono vicine al
software di riferimento che uso in pista, in alcuni tick identiche al secondo decimale:

  LeaderProjectedPosAtCheckered   38.0-39.0 tutta la gara    (riferimento: 38.8)
  LeaderRaceTotalLaps             39 per quasi tutta la gara  (riferimento: 39)
  ProjectedPosAtCheckered         34.83-34.91 nell'ultimo terzo (valore reale: 34.83)
  RaceTotalLaps finale            35                          (giri realmente completati: 35)

Resta un difetto visibile: a metà gara RaceTotalLaps sale a 37 per tre giri, poi torna a 35.
Causa già isolata: CINQUE tick su 795 in cui il passo stimato del leader schizza a 278 secondi
invece di ~69, e l'isteresi asimmetrica poi tiene il picco.

COSA RESTA DA FARE, IN ORDINE (concordato)

  3. Δt_cur dal timestamp dell'ultimo passaggio, non dalla posizione campionata
  4. Bandiera = minimo su tutta la classe veloce, in modalità ombra (solo a log, non usata)
  5. Filtro di Hampel + MAD + change-point al posto della finestra percentuale
  6. Filtro Alpha-Beta al posto dell'isteresi asimmetrica
  7. Base Pace + reiniezione della massa carburante giro per giro
  Y-44. Il valore del tempo di sosta è sovrastimato del 49% (0.79 giri contro 0.53 reali)

ORDINE CONSIGLIATO: fare 4 e 6 per primi, gli altri dopo.

Il 6 (Alpha-Beta) perché l'outlier del passo leader continuerà a esistere finché non si affronta
Y-38, ma un filtro che sa scendere lo ASSORBE invece di amplificarlo per tre giri. Non dipende da
nessun'altra correzione.

Il 4 perché risolve la causa invece del sintomo, e va capito bene prima di implementarlo:

- OGGI proiettiamo UNA SOLA vettura: quella che in questo istante è P1 assoluto. Non calcoliamo la
  proiezione per tutte le vetture di nessuna classe. Il punto 4 è ancora tutto da fare.
- Il criterio corretto è il MINIMO DEL TEMPO DI ATTRAVERSAMENTO, cioè chi taglierà per primo dopo
  lo scadere del cronometro — non "la posizione proiettata più alta". Quasi coincidono, ma due
  vetture con passo diverso possono invertirsi.
- Il nostro RaceTotalLaps NON dipende dalle altre classi: dipende solo da quando esce la bandiera
  (che dipende solo dal leader assoluto) e dal nostro passo. Un cambio di posizioni fra GT3 non
  tocca il nostro numero.
- Il caso che ci fa male è il cambio di leader assoluto per una SOSTA: oggi cambiamo di colpo
  vettura di riferimento e la proiezione salta. Col minimo, se il leader attuale ha ancora una
  sosta da fare il suo tempo di sosta sposta avanti il suo attraversamento e la vettura dietro che
  ha già finito le soste diventa il minimo IN MODO CONTINUO, prima del sorpasso fisico.
- RIFINITURA rispetto al report: estendere il minimo a TUTTE le vetture, non solo alla classe più
  veloce. Costa quasi nulla ed è più robusto quando la classe veloce non è davvero davanti (inizio
  gara, o finestra in cui tutti i prototipi sono ai box insieme). Chi taglia per primo dopo lo
  scadere È il leader per definizione, qualunque classe abbia.

COSA NON ABBIAMO, E NON SERVE PER IL CARBURANTE: nessun leader DI CLASSE, da nessuna parte. È la
stessa lacuna che aveva bloccato Y-19 (PositionInClass non è usato in nessun punto del progetto, e
le convenzioni di conteggio giri degli avversari non sono verificate). Servirà solo se un giorno
vorremo mostrare la posizione finale di classe.

TRE TRAPPOLE, TUTTE GIÀ COSTATE TEMPO

- Nei log, dal commit 9d16172 ogni riga RaceProjectionsDiagnostics contiene DUE campi PosAtFlag
  (Player e leader). Un grep ingenuo li somma e produce statistiche inventate. Usare:
  sed -E 's/.*Player:[^|]*PosAtFlag=([0-9.]+).*/\1/'

- Il totale converge SEMPRE al valore giusto a fine gara, perché la parte proiettata si riduce a
  zero. "Il numero finale è corretto" non è mai una prova che il calcolo sia giusto.

- Leggere valori da un log senza controllare a quale SESSIONE appartengono (pre-gara, qualifica,
  gara) porta a conclusioni sbagliate di un ordine di grandezza. È già successo due volte.

NON TOCCARE

- La formula TimeUntilLeaderCheckered: è identica a quella della revisione esterna E a quella di
  DahlDesign. Tre fonti indipendenti concordi. Non è lì il problema.
- Il coefficiente FuelWeightCoef nelle impostazioni: resta 0.03 e ora è in secondi per CHILOGRAMMO.
  La conversione litri→kg è nel codice, esplicita.

COME VERIFICARE CHE PARTI DA UNA BASE SANA

  "C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
  "User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"

Atteso: exit code 0, 219 test PASS.

COSA POSSO DARTI IO

Replay di Road Atlanta girati a 3x, sempre la stessa gara, così i confronti fra un run e l'altro
sono puliti. I log finiscono in Logs/Road Atlanta/. L'ultimo buono è SimRIG_DebugLog_20260831_195300.
Dimmi cosa vuoi misurare e te lo procuro.
```
