# PROJECT STATE — The Wheel Project / Antigravity 2.0

> Fonte di verità sullo stato corrente e sul **turno di scrittura**.
> Ogni agente legge questo file **prima** di toccare il codice e lo aggiorna **prima** di iniziare a scrivere.

---

## 🔒 LOCK

```yaml
owner:      NONE
since:      —
task:       —
scope:      —
expires:    —
```

**Regole del lock**

1. `owner: NONE` → chiunque può prendere il turno: aggiorna questo blocco, committa, poi lavora.
2. `owner: <altro>` → **non modificare nessun file di codice**. Puoi leggere, analizzare, proporre piani in `.ai/plans/`, commentare. Nient'altro.
3. Il lock si prende e si rilascia con un commit dedicato, così il passaggio è tracciato nella history:
   - prendere: `[<agente>] chore: acquire lock — <task>`
   - rilasciare: `[<agente>] chore: release lock`
4. `scope` è vincolante: se durante il lavoro serve toccare file fuori scope, si allarga il lock esplicitamente (nuovo commit) invece di sconfinare in silenzio.
5. Lock stantio: se `expires` è passata, un altro agente può forzare il rilascio annotandolo in `HANDOFF_LOG.md`.
6. Il lock è **disciplina, non tecnologia**. La vera rete di sicurezza è Git: commit piccoli e frequenti, così ogni sovrascrittura accidentale è diffabile e reversibile.

---

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
> | il ragionamento completo di un punto **chiuso**, i numeri misurati, il commit | `.ai/archive/CLOSED_POINTS.md` (40 punti) |
> | un handoff più vecchio dei 10 tenuti | `.ai/archive/HANDOFF_LOG_archive.md` (12 voci, 24/08 → 01/09) |
> | qualsiasi altra cosa | `git log` — nulla è stato perso |
>
> L'indice dei punti chiusi resta più in basso in questo file: serve a sapere **che** un punto esiste
> ed è chiuso, senza caricarne il testo. Quando ti serve il *perché*, apri l'archivio a quell'ID.

**Come è stato verificato tutto:** build 0 errori, **314 test PASS** (erano 111 al setup, 186 al
24 agosto, 295 dopo Y-52 passo 1, 311 dopo Y-52 passo 2, 314 dopo sblocco dump `SessionDataReader`
il 2026-09-06; ⚠️ vedi Y-54: il backtest sul replay
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

**I punti ancora aperti** (Y-13, Y-14, Y-15, e la Fase 6 del piano calibrazioni) sono aperti per
mancanza di **dati**, non di tempo: servono replay con caratteristiche precise, indicate in ciascuna
voce.

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
| Y-52 | Metadati di sessione dallo YAML (4 passi) | 🟡 **Passi 1 e 2 di 4 fatti + Sblocco dump SessionDataReader** (Passo 1: `bd00979`; Passo 2: 2026-09-05; Sblocco Dump: 2026-09-06). Nasce dall'analisi di irdashies: iRacing pubblica nel SessionInfo YAML dati che oggi rimpiazziamo con ripieghi cablati. **La premessa era gia' verificata sul campo**: `TelemetryReader.cs:90-94` legge lo YAML con quattro fallback e i log di gara lo provano — `BoP Pct: 0.500` non può venire da altro che dal parsing di `CarClassMaxFuelPct`. **Passo 1 (fatto):** `SessionMetadata` (contenitore agnostico, campi nullable) + `SessionYamlParser` (fornitore iRacing, funzione pura) + cache per sessione in `TelemetryReader` + dump `SimRigMetadata.json` su cartella configurabile. **Passo 2 (fatto):** seeding `DriverCarEstLapTime` e `CarClassEstLapTime` nei ripieghi di passo di `RaceAnalyzer.cs` (player, leader e avversari al via) e `TargetStrategyManager.cs`. Introdotto `IsLapsPredictionValid` su `RaceAnalysisResult` e pubblicato su SimHub come `SimRIG.Session.IsLapsPredictionValid` (true su gare a giri o gare a tempo con passo misurato/YAML; false fuori gara o su mero ripiego forfettario). Risolto il "buco nero dei primi 3 giri" (a Road Atlanta GT3 dava 23 giri con ripiego a 120s; ora 36 giri al semaforo verde). **Sblocco Dump SimHub (fatto 2026-09-06):** Risolto il problema del dump `SimRigMetadata.json` mai generato su disco. SimHub non popola la stringa YAML grezza ma espone l'oggetto deserializzato da `iRacingSDK` (`SessionData`/`DataSample` via `GetRawDataObject()`) e proprietà `DataCorePlugin.GameRawData.SessionData.*`. Creato `SessionDataReader.cs` con reflection e fallback, integrato in `TelemetryReader.cs`. 3 unit test aggiunti (314 PASS). **Passi 3-4 (da fare):** (3) `DriverPitTrkPct` → distanza metrica alla piazzola box; (4) densità carburante, incidenti, gomme, partenza. |
| Y-40 | Il passo del leader **deriva** durante la gara, e con lui i giri totali | Road Atlanta `20260830_121813`, con `L_PosAtFlag` finalmente visibile (Y-37). La proiezione del leader **parte corretta**: `38.814` al giro 2, contro i `38.8` del software di riferimento. Poi deriva: `39.564`, `39.040`, `39.504`, `39.284`, fino a `40.925` nella finestra del passo sballato, e **rientra solo a fine gara** (`38.371`, `38.245`, `38.146`, `38.095`). In parallelo `L_Pace` scende da `69.540` a `67.484-68.132` mentre i giri reali del leader misurati dai passaggi valgono `69.4-71.4`. **Due domande aperte, da chiudere con un breakdown del flusso dati e non a intuito:** (1) perche' la media mobile del passo deriva verso il basso invece di stabilizzarsi sui giri veri; (2) quanto pesa il fatto che la proiezione usi il tempo **normalizzato** (a serbatoio scarico) invece del tempo che la vettura fara' davvero — per il leader la penalita' carburante vale `2.40 s` su `71.9` (**3.3%**), contro `1.21 s` su `77.5` (**1.6%**) del Player, e il 3.3% su 38.8 giri fa circa **+1.3 giri**. **Perche' si aggiusta solo alla fine:** la parte *proiettata* (che usa il passo) si riduce a zero col passare del tempo, mentre la parte *misurata* (i giri gia' completati) cresce — quindi l'errore si annulla da solo alla bandiera, indipendentemente dalla causa. Nota sulla domanda "mischiamo le classi?": la baseline e' **per vettura** (`tData`), quindi no; ma la *normalizzazione* dipende da `estimatedFuel`, che deriva da parametri **di classe** (`classMaxTank`, `classFuelBurn`) — un errore li' biasa in blocco tutte le vetture di quella classe. E' l'unico punto dove una grandezza di classe entra in un passo per vettura. |
| Y-38 | Identita' del leader assoluto che sfarfalla | ⚠️ **Meccanismo dimostrato il 2026-09-01, il punto resta aperto.** Il salto di `RaceTotalLaps` a 37 nel replay `20260831_195300` e' suo, e la catena e' chiusa aritmeticamente: alle 20:04:08.520 il P1 assoluto passa da `Sven Neiss` (`L_Pace` 68.443) ad `Alessandro Barbagallo`, che porta con se' un passo registrato di **278.563 s**; `L_PosAtFlag` crolla nello stesso tick da `38.185` a `27.985`. Il supplemento di `TimeUntilLeaderCheckered` vale al massimo **un giro del leader**, quindi passa da ~68 a fino a 278 s: sul Player sono `278.563 / 76.524 =` **3.64 giri in un fotogramma**. Serviva superare 36.05 per arrivare a 37 e con quel passo si arriva a 37.8. ⚠️ **Correzione a una diagnosi precedente:** i "cinque tick sopra 35.05" registrati nell'handoff del 31/08 **non sono la causa** — quattro dei cinque (20:04:23-26) cadono *dopo* che il totale era gia' 37, e il piu' alto vale 35.435, insufficiente. Il fotogramma colpevole non e' a log: la diagnostica scrive **1 riga/s** mentre il calcolo gira almeno a 12 Hz (misurato: intervallo massimo 1.085 s con strozzatura a 1.0 s). Da qui la riga `Total Laps Transition`, che scatta al **cambio** del totale con dentro l'ingresso che l'ha causato. Diagnosi originale: | Road Atlanta `20260830_113151`: nella finestra dei giri 19-24 il P1 assoluto cambia fra `Sven Neiss` (96 tick), `Alessandro Barbagallo` (29) e `Kalyann Mey4` (28). Ogni cambio porta con se' il passo di quella vettura. `LeaderPaceFilter` ha un dwell di 2 s sull'identita': evidentemente non basta. **Il punto 4 (bandiera = minimo del tempo di attraversamento su tutte le vetture) lo dissolve** e gira gia' in modalita' ombra da questo turno: sugli stessi numeri la vettura col passo a 278 s attraversa a 1114.3 s contro i 934.9 s del leader vero, quindi **perde il minimo da sola** e la proiezione del Player resta 34.29 invece di saltare a 37. |
| Y-53 | `PaddleClutch.h` manca dal repository | Trovato il 2026-09-05 da una review a freddo (`.ai/reviews/2026-09-05-inventario-stato-progetto.md`, R-1). `Hardware/Firmware INPUT/V2_8_2/V2_8_2.ino:11` fa `#include "PaddleClutch.h"`, ma la cartella contiene **solo il `.ino`** — verificato con `find Hardware -type f`. Il file **non è in `.gitignore`** (che ignora solo `Logs/` e gli artefatti .NET): non è un'esclusione deliberata, è un file mai committato. `PaddleClutchManager` governa deadzone, bite point e modalità della frizione, quindi da un clone pulito il firmware del volante **non compila**. ⏸️ **Risposta avuta il 2026-09-05: non è un difetto da correggere, e nessun agente deve toccarlo.** Il file esiste sulla macchina di **Andreas**, che lo versionerà più avanti. La segnalazione resta scritta qui solo perché una review a freddo lo rialza naturalmente come difetto grave — se lo ritrovi, la risposta è "atteso, arriva da Andreas". Vale in generale per `Hardware/`: è territorio suo, fuori dallo scope del lock (vedi `ARCHITECTURE.md`, mappa dei moduli). Finché il file non c'è, resta vero che da un clone pulito il firmware non compila: è una conseguenza nota e accettata, non una cosa da sistemare. |
| Y-54 | Il backtest sul replay reale si auto-salta in silenzio | Trovato il 2026-09-05 (stessa review, R-2). `User.PluginSdkDemo.Tests/IntegrationTests/MisanoHuracanGT3ReplayTest.cs:20` cabla `@"E:\SimHub\Replays\IRacing\20260303_151035.telemetry.json"` — **unico path assoluto rimasto** nel codice compilato, contro la convenzione esplicita "mai nuovi path assoluti hardcoded". Se il file manca (`:26-30`) il test stampa `Skipping full replay backtest` e **ritorna senza fallire**: il runner chiude comunque exit `0`. Il conteggio "295 PASS" non distingue quindi la copertura girata da quella saltata — è il pattern che ADR-004 combatte, spostato dal contenuto del test alla sua assenza. Rimedio proposto: path derivato da `SIMHUB_INSTALL_PATH` e conteggio `SKIPPED` separato dai `PASS`. |
| Y-56 | Un lock non pushato non serializza niente, e `human` non dice **quale** umano | Trovato il 2026-09-05. **Da decidere fra Andreas e Michael, non da un agente** — per questo è registrato e non corretto. Due difetti nello stesso punto. **(a)** Il lock vive in `PROJECT_STATE.md`, cioè in Git: ma finché il commit resta locale, il lock è **privato**. Il 2026-09-05 il lock è stato preso e rilasciato due volte su una macchina, con 5 commit mai pushati: per l'altra parte quelle acquisizioni non sono mai esistite. Se entrambi l'avessero preso in contemporanea, entrambi avrebbero creduto in buona fede di averlo — ADR-001 dice che la rete di sicurezza è Git, ma Git serializza solo ciò che è stato pushato. Rimedio proposto: `AGENTS.md` impone **`git push` subito dopo il commit che prende il lock** (e prima di scrivere codice), così l'acquisizione è visibile a chiunque. **(b)** Il blocco `LOCK` ammette `human` come owner unico, ma gli umani sono **due** con macchine e capacità diverse (Andreas: Windows, SimHub, `Logs/`, può verificare; Michael: macOS, non può). Rimedio proposto: `human:andreas` / `human:michael`. ⚠️ Conseguenza già in atto: tutta `.ai/` dice **"l'utente"** al singolare — in ogni frase tipo "i replay esistono solo sulla macchina dell'utente" oggi non è più chiaro chi sia. |
| Y-55 | `CustomDialog.xaml.cs` non compilato e non documentato | Trovato il 2026-09-05 (stessa review, R-3). Il confronto disco↔`<Compile>` del `.csproj` principale dà **quattro** `.cs` esclusi: i tre `_LEGACY` (documentati in `CLAUDE.md`) più `CustomDialog.xaml.cs`, che non compare nel `.csproj` né come `Compile` né come `Page`. Stessa trappola dei `_LEGACY` — modificarlo non ha alcun effetto sul plugin — ma senza il cartello che avverte e senza il suffisso che la renderebbe evidente dal nome. Da decidere: eliminarlo, o documentarlo fra le trappole. |

### Punti già chiusi — indice

Il testo completo di questi 39 punti (ragionamento, numeri misurati, regressione
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


---

## 📍 Stato corrente

**Fase attiva della Roadmap:** **Fase Y-52 (Metadati di Sessione da iRacing / irdashies)** — vedi [roadmap.md](.ai/plans/2026-08-24-roadmap.md).
- **Passi 1 e 2:** Completati e testati (contenitore agnostico `SessionMetadata`, seeding passo stimato e validità `IsLapsPredictionValid`).
- **Sblocco Dump SimHub (2026-09-06):** Risolto il mancato dump nei replay reali tramite `SessionDataReader.cs` che estrae i metadati dall'oggetto nativo `SessionData` / `DataSample` e dal property bag SimHub. Dump su disco sbloccato (314 test PASS).
- **Prossimo lavoro tecnico:** Y-52 Passo 3 (`DriverPitTrkPct` → coordinata metrica piazzola box) e Passo 4 (densità carburante reale, opzioni gara).
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
