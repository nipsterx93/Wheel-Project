# PROJECT STATE — The Wheel Project / Antigravity 2.0

> Fonte di verità sullo stato corrente e sul **turno di scrittura**.
> Ogni agente legge questo file **prima** di toccare il codice e lo aggiorna **prima** di iniziare a scrivere.

---

## 🔒 LOCK

```yaml
owner:      NONE          # NONE | antigravity | claude | codex | human
since:      2026-08-29T11:00:00Z
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

**Cosa è cambiato dal setup (2026-08-18) a oggi:** 30 punti aperti, di cui 26 chiusi. Il filo
conduttore è uno solo, ed è più interessante dei singoli difetti: quasi tutti erano **un campione
singolo non verificato che si cristallizza in un dato persistente**. Cambia solo quale campione
vince — il primo (`PitExitPct` congelato a 0.1088 per settimane), l'ultimo (`PitLaneSpeedLimit`
sovrascritto da un outlier), o il più basso (una baseline da giro parziale che nessun giro vero
poteva più correggere). Il rimedio comune è in ADR-005.

**In che ordine leggere:**

1. `.ai/ARCHITECTURE.md` — mappa dei moduli, e soprattutto **ADR-004** (come si verifica un fix qui)
   e **ADR-005** (perché un campione singolo non basta). Sono il contesto che rende leggibile il resto.
2. La tabella qui sotto — ogni punto ha il commit, i numeri misurati e il **perché** della decisione,
   incluse le cose chiuse *senza* scrivere codice (Y-3, Y-8, Y-19).
3. `git log --oneline f526cb3..HEAD` — i messaggi di commit sono deliberatamente estesi: contengono
   il ragionamento e i numeri, non solo il cosa.
4. `.ai/HANDOFF_LOG.md` dall'alto — solo se serve la cronologia dei turni.

**Come è stato verificato tutto:** build 0 errori, **186 test PASS** (erano 111 al setup), e per ogni
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
| ~~Y-1~~ | ~~`CanFinishWithoutPitting`~~ | ✅ **Opzione 1 implementata** (`bc20a67`). Non più silenziamento indiscriminato: `canFinishWithoutPitting` sopprime l'undercut (con `RejectReason` dedicato `NoPitNeeded`) ma **non** l'overcut, che in quel caso è già vinto. Aggiunto `FuelSaveTarget` con filtro di fattibilità. |
| ~~Y-2~~ | ~~`OvercutTrafficOK` cablato a `true`~~ | ✅ **Implementato** (`f08bf43`) come "chi ho davanti adesso", distinto dall'undercut che è proiettato all'uscita box. Finestra 2.0 s, **da calibrare** sul primo replay con overcut e traffico veri. |
| ~~Y-3~~ | ~~`LapsSinceLastPit`~~ | ⏹️ **Chiuso senza intervento.** L'imprecisione (fino a un giro) è trascurabile rispetto alla scala delle decisioni che governa. Eventualmente in futuro. |
| ~~Y-8~~ | ~~Deadband HUD 0.05~~ | ⏹️ **Chiuso senza intervento.** Il dato resta com'è: mostrare un valore per un altro non serve a nulla. |
| ~~Y-9~~ | ~~Euristica pit del Player~~ | ✅ **Uniformato** (`81a5f12`). Il Player usa la stessa cascata degli avversari via `PitLaneDetector`. Soglie di velocità ora derivate dal `PitLaneSpeedLimit` appreso per traccia+classe invece che cablate. Corretto anche un bug multiclasse nell'apprendimento. |
| ~~Y-11~~ | ~~Modello warmup~~ | ✅ **Corretto** (`8b42efd`). Gli outlap non entrano più nella media del degrado. Soglia `0.10` ora condivisa invece che ricopiata in tre punti. |
| ~~Y-12~~ | ~~Isteresi dei gate strategici~~ | ✅ **Deciso e implementato** (commit `1fa7b15`), approvato da Antigravity e Codex. Banda `±0.25` sulla posizione, `±0.15` sui margini, dwell `5 s`. Valori da sweep sul replay `20260819_205004` con simulatore validato 1958/1958 campioni. Il filtro EMA sul gap è stato **valutato e scartato**. Da confermare su un secondo replay e su un circuito diverso. |
| Y-13 | Gap che salta di un giro al rollover | `GapJump` (commit `ff480bd`) scarta il campione nel **ritmo relativo**, ma la causa è a monte: `posDiffLaps * refLapTime` produce un gap sbagliato di esattamente un giro per un tick, quando i due contatori sono disallineati. Lo stesso gap alimenta **anche i gate strategici**, dove non c'è filtro. Oggi l'isteresi assorbe il colpo (banda 0.25 s, dwell 5 s), quindi non è urgente. Si corregge il calcolo a monte, o si accetta il sintomo curato in un posto solo? **Il difetto è intermittente**: è una race fra il campione macrosettoriale e il rollover, e la finestra dura pochi millisecondi. Comparso nel replay `221922` allo stesso punto in cui il `230109` non mostra nulla (`gapDelta` 93.033 contro 0.032). Non contare quindi sull'assenza di `GapJump` in un singolo replay come prova che sia risolto. |
| Y-14 | Derivare `TyreChangeTime` senza `TyreSelectionScope` | `TyreManager.CurrentScope` è pilotato solo dai tasti volante (`TyreManager.cs:89`), nessun percorso lo deriva dalla telemetria — in un replay non presidiato resta a `None`. **Corretto**: un replay *presidiato*, dove qualcuno guarda la sosta e riproduce lo scope a mano in tempo reale, funziona — per il plugin è indistinguibile da un pilota live. Resta comunque `EstimatedPlayer`, mai `Confirmed`: il sistema non sa che è una ricostruzione deliberata. Percorso più solido, proposto dall'utente: derivare il tempo gomme **senza mai leggere lo scope**, per sottrazione da dati già interamente grondati in telemetria — `StationaryTime` e `RefuelingTime` (quest'ultimo già isolato con precisione dai timestamp reali di `CurrentFuelLevel`, non dal solo scope). Ma la formula si biforca per layout: **Sequential** → `TempoGomme = StationaryTime − RefuelingTime` (esatta); **Simultaneous** → la sottrazione sottostima, la relazione corretta è `TempoGomme = StationaryTime` quando `RefuelingTime < StationaryTime`, altrimenti solo un limite superiore. `IsPitLayoutSequential` (`PitRadar.cs:335`) di default presume **Sequential per ogni gioco tranne iRacing**, e **Simultaneous per iRacing** finché non rilevato dinamicamente — se Daytona/IMSA è iRacing, il default è già il caso in cui la sottrazione sbaglierebbe. `TriggerDynamicLayoutDetection` (che confermerebbe quale dei due) richiede di conoscere **già** sia `tFuel` sia `tTyres`: stesso uovo-e-gallina per una classe mai vista. Il tetto anti-riparazione-danni (`OpponentTracker.cs:1007`, `tFuel+tTyres+6.0`) ha lo stesso limite, e ricade su un default generico di 26 s (`PitRadar.cs:329`) non calibrato sulla vettura reale. |
| Y-15 | Validazione `FuelToAdd` contro il reale | Oggi non c'è modo di sapere se la raccomandazione `SimRIG.Fuel.FuelToAdd` (calcolata prima della sosta) corrisponde a quanto viene poi realmente versato. Diverso dalla Fase 4: quella impara `FuelFillRate` dai litri reali, non giudica la qualità del consiglio. Il dato grezzo esiste già (`Pit Complete` logga `FuelAdded` dalla variazione di `CurrentFuelLevel`), manca solo l'abbinamento col valore predetto, congelato al momento di `PitLaneEntered` (il gate della Fase 3 lo espone già). Diagnostico puro, non tocca il sistema di confidenza. Stesso pattern già usato a mano in `ReplayBacktestIntegrationTest` (`Ground Truth: 16.0L` scritto nel test) — qui diventerebbe automatico, letto dalla telemetria invece che digitato. |
| ~~Y-16~~ | ~~`RaceLifeTimeLeftSec` ricostruito dal leader~~ | ✅ **Corretto** (`cc35f97`). Era `leaderLapsRem * leaderPace + leaderRemainingPitTime`, cioè un conteggio giri **latchato** rimoltiplicato per un passo che nel frattempo poteva essere cambiato — in multiclasse succede a ogni cambio di identità del P1 assoluto. Daytona giri 12-15: 2368 s stimati contro ~1400 s reali, giri del Player quasi raddoppiati, e da lì dritto in `FuelToAdd` (`#fuel 50l` dove ne servivano ~31.5; versati realmente 31.6). Ora ancorato al countdown di sessione (`RaceTimeProjection.TimeUntilLeaderCheckered`): il leader pesa **solo** sulla frazione di giro che gli manca per tagliare, quindi l'errore è limitato a un giro invece di scalare con la durata della gara. |
| ~~Y-17~~ | ~~Passo del leader contaminato dallo sfarfallio~~ | ✅ **Corretto** (`cc35f97`). `LeaderPaceFilter` scarta i giri fisicamente impossibili (>110 m/s di media su giro intero) e i campioni raccolti mentre l'identità del leader non si è ancora stabilizzata (dwell 2 s — le raffiche di `TargetChanged` nel log reale sono a decine di millisecondi). Media mobile invariata (α 0.10). **È un secondo strato, non il primo**: la protezione vera è Y-16. Nota onesta: il limite fisico a Daytona cade a 52.1 s e il valore patologico osservato era 56.391 s — il floor da solo **non** l'avrebbe preso, è il dwell a farlo. |
| ~~Y-17b~~ | ~~**Causa vera** del passo del leader sbagliato~~ | ✅ **Trovata e corretta** (`e907b67`). Non era lo sfarfallio: in `OpponentTracker` l'ancoraggio da cui si misura il giro (`LastLapStartTimeSec`) viene piazzato **alla prima comparsa** della vettura, che cade a metà tracciato — quindi il primo "giro" misurato è una frazione di giro. E siccome la baseline si sostituisce **solo al ribasso**, i giri veri (più lenti) non la correggono mai. Correlazione netta sul replay Daytona: LMP2 e GT3 presenti dal via → baseline 97-107 s corrette; GTP entrate al giro 5-7 → 56.4 / 62.3 / 64.4 / 67.0 s contro ~100 s reali. `HasWitnessedLapStart` distingue un ancoraggio genuino da uno di comparsa; si misura dal secondo cambio di giro. Copre anche il sintomo opposto (primo giro con partenza da fermo: 176-339 s nel log). **✅ Verificato sul campo** (replay `20260823_232612`): Sam Kuitert 56.409→90.682, David J Adam2 62.330→97.586, Fornaciari 64.586→99.496, Telkkälä 176.617→98.752. `L_Pace` da 56.410 congelato per 9 giri a **92-93 s stabile**. |
| ~~Y-18~~ | ~~Due definizioni divergenti di "fermo ai box"~~ | ✅ **Corretto** (`cedc2aa`). `PitRadar` usava `IsInPitBox` nel percorso di calibrazione e `SpeedKmh < 0.5 \|\| IsInPitBox` in quello spaziale. `IsInPitBox` è il flag grezzo del gioco: si alza solo nello stallo assegnato col servizio in corso, quindi una sosta per cedere una posizione non lo fa mai scattare. Stesso log, stesse soste: 41.73 s / 68.40 s dal percorso spaziale contro `StatTime: 0.0s` da quello di calibrazione. Il danno vero era `_fuelLevelAtStopStart` mai reinizializzato → `FuelAdded` fantasma (15.8 L e 12.7 L con zero secondi di sosta). Ora unificato via `PitRadar.IsPlayerStationaryInPit`. |
| ~~Y-19~~ | ~~Tetto sui giri totali del Player in multiclasse~~ | ⚠️ **Nota aggiunta il 2026-08-29 (Y-31):** l'argomento "le proiezioni danno il totale corretto senza alcun tetto — 26 stabile dal giro 5 alla bandiera" va riletto. Quella stabilità **non era la prova che la proiezione fosse giusta**: era il dente d'arresto di Y-31, che sale di un giro e non torna più indietro. Ricontrollati i log: Daytona `24→25→26`, Misano `22→25→26`, **sempre e solo in salita**. Lì il numero era giusto, a Road Atlanta era giusto per un giro di troppo. La conclusione può reggere lo stesso, ma va **riverificata** sui log dopo `dbcb168`. Testo originale: ⏹️ **Chiuso senza intervento.** Il clamp `Math.Min(projectedPlayerTotal, _latchedLeaderTotalLaps)` era già stato disattivato in multiclasse (`cc35f97`): una GT3 e una GTP non fanno lo stesso numero di giri, quindi il confronto col leader **assoluto** non dice nulla. Restava da decidere se rimpiazzarlo con un tetto sul leader **di classe**. **I dati dicono di no**: le proiezioni danno il totale corretto senza alcun tetto, sia a Misano (monoclasse, 26 giri, 3 run su 3) sia a Daytona (multiclasse, 26 stabile dal giro 5 alla bandiera). L'ancoraggio al countdown (Y-16) basta da solo, e cablare un clamp richiederebbe la posizione assoluta degli avversari con convenzioni di conteggio giri non verificate (`PositionInClass` non è usato da nessuna parte nel repo): un off-by-one si propagherebbe silenziosamente in `FuelToAdd`. Se un replay futuro mostrasse una proiezione sopra il possibile, si riapre. |
| ~~Y-20~~ | ~~`PitLaneSpeedLimit` "l'ultimo che scrive vince"~~ | ✅ **Corretto** (`e9caad6`). Era un assegnamento nudo (`target.PitLaneSpeedLimit = speedLimit`), senza confidenza né consenso. Misano `20260823_133904`: 11 osservazioni a 60 km/h e una a 80 (vettura ancora in decelerazione) — l'80 ha sovrascritto il 60, salvato solo perché altri hanno scritto dopo. Ora consenso per classe, scrive la **mediana**. |
| ~~Y-21~~ | ~~Geofence riscritte da un campione singolo~~ | ✅ **Corretto** (`e9caad6`). `CanOverwrite(Confirmed, Confirmed)` è vera, quindi ogni sosta riscriveva `PitEntryPct`/`PitExitPct`. Su Misano una sola sosta ha spostato l'uscita da 0.1088 a 0.0737 (~148 m) — e quella geofence alimenta `IsInExtendedPitLaneZone`, quindi la cascata Y-9 del Player **e** il rilevamento avversari. Ora: una sosta = `EstimatedPlayer` (usabile su pista nuova, non scalza un dato consolidato), tre soste concordi = `Confirmed`. |
| ~~Y-22~~ | ~~Quale sia il valore vero di `PitExitPct` a Misano~~ | ✅ **Risolto dai dati** (tre replay in `Logs/3 Run Test`, 2026-08-23). La misura è ripetibile **sotto il metro**: `PitExitPct` 0.0738605 / 0.0738605 / 0.0737054 (dispersione 0.65 m), `PitEntryPct` 0.9495097 / 0.9495097 / 0.9495739 (0.27 m). I run 1 e 2 sono bit-identici. I 148 m che separavano il vecchio `0.1088` dal nuovo `0.0737` sono duecento volte il jitter osservato: **non era dispersione di misura**, era un valore di altra provenienza congelato dal guard `== -1.0`. Il valore corretto è `0.0737`. Nota: i replay giravano a 5x, dove il campionamento è più grossolano rispetto alla distanza percorsa — il jitter a 1x sarà uguale o minore. |
| ~~Y-23~~ | ~~Visite fantasma in corsia box da sfarfallio di `IsInPitLane`~~ | ✅ **Corretto** (`0b52641`). A Daytona il flag della telemetria ha fatto true→false→true in 0.2 s mentre il Player era sul rettilineo, producendo un "Pit Complete" da 0.7 s che ha scritto `PitDriveThroughTime = 0.666`. Da lì spegneva **per sempre** la richiesta vocale di calibrazione (`IsDriveThroughTimeMissing` verifica `== 0.0`, e 0.666 risulta "presente"). Ora si scarta l'intera visita se non ha percorso almeno 0.01 di giro (~42 m a Misano): il criterio è la **strada percorsa**, non il tempo. Aggiunto anche un pavimento di plausibilità sui transiti, derivato da `PitDistanceMeters`/`PitLaneSpeedLimit` invece che da una costante. Il `0.666` è stato ripulito dal database (backup in `.ai/db-snapshots/SimRIG_Data_pre-dt-cleanup.json`). **✅ Verificato sul campo**: `Pit Visit Discarded (No Traversal); traversed=0.0039 | minimo=0.0100 | durata=0.67s`. Effetto collaterale positivo: i consensi di uscita sono passati da `3/4 · 4/5 · 5/6` a **`4/4 · 5/5 · 6/6`** — il campione spurio non entra più, quindi la mediana non deve più isolarlo. |
| ~~Y-24~~ | ~~`LeaderRaceLapsRemaining` bloccato sul totale latchato~~ | ✅ **Corretto** (`2c13874`). Ai giri 12-15 il diagnostico riportava `Leader: PosPct=0.0000, LapsComp=0, LapsRem=30.00` mentre il leader era al giro ~12: il record dell'avversario in P1 va momentaneamente **vuoto** e `RaceAnalyzer` lo prendeva per buono, azzerando `leaderAbsolutePos` — da 18.65 a 30.00 in un tick. La convenzione "posizione zero = stato azzerato" esisteva già (`TrackPositionValidator`, `PitRadar`) e `OpponentTracker` scartava quei tick da sempre; mancava solo qui. `IsLeaderSampleUsable` distingue il record vuoto dal leader davvero sul traguardo. Dopo Y-16 l'impatto era confinato alle proprietà da dashboard, ma restava un numero sbagliato mostrato al pilota. **✅ Verificato sul campo** (`20260824_193916`): `L_Rem` ora decresce in modo monotono (26 → 21.92 → 20.84 → ... → 2.90 → 0.00), nessun blocco su valori interi. Al giro 14, con il record grezzo ancora vuoto, `LapsRem` vale 14.69 invece di 30.00. |
| ~~Y-25~~ | ~~`LeaderRaceLapsCompleted` lampeggia a zero~~ | ✅ **Corretto** (`cd97b4e`). Coda di Y-24: quel fix proteggeva il calcolo derivato ma non il dato alla sorgente. Nel replay `20260824_193916` **231 tick su 534 (43%)** avevano ancora `LapsComp=0`, e il valore finisce direttamente sulla dashboard (`SimRIG.Session.LeaderRaceLapsCompleted`). Nessun impatto strategico, ma un numero visibile e falso. La logica di tenuta è in `HoldLeaderLapsCompleted`, statica: la **prima** versione del test la riproduceva invece di chiamarla e restava verde anche neutralizzando il guard — la stessa trappola evitata in Y-17b. |
| Y-26 | Dati pit lane cristallizzati senza consenso | ⚠️ **Chiuso in parte** (`c646d7b`). Trovato da Antigravity in revisione. **Chiuso**: `PitInOutAccDecTime` (osservazione incidentale da ogni sosta avversaria, molti campioni) ora passa da `CalibrationConsensus` con tolleranza 2 s; `PitTransitTime` nelle due procedure **guidate** (SplashAndDash, TyreChange) ha perso il lock `== 0.0`, che non proteggeva da nulla e impediva solo di rifare una calibrazione riuscita male. **Resta aperto**: `PitDriveThroughTime` e il ramo `StopAndGo` mantengono `== 0.0`, perché `DriveThrough` è la modalità di ripiego in cui si finisce anche in gara — senza lock, un transito qualunque riscriverebbe la calibrazione, e nel ramo StopAndGo dirotterebbe le soste dall'apprendimento naturale della Fase 4. Chiuderlo richiede **distinguere una calibrazione guidata da un transito incidentale**, che il codice oggi non sa fare: è la stessa decisione di prodotto del punto Y-28. |
| Y-29 | `FuelFillRate`/`TyreChangeTime` senza consenso nel percorso **non guidato** | Trovato preparando il piano d'implementazione di Y-28 (`.ai/plans/2026-08-25-calibration-cascade-implementation.md`). Il ramo "else" di `PitRadar.Update` (Fase 4 originale: apprendimento da una sosta di gara qualunque, non da una calibrazione deliberata) scrive `EstimatedPlayer` senza consenso fra osservazioni multiple — `CanOverwrite(EstimatedPlayer, EstimatedPlayer)` è vero, quindi l'ultima sosta naturale osservata sovrascrive la precedente. Stesso schema di Y-26/Y-27, su un percorso diverso: la cascata guidata (Y-28) non lo attraversa mai, quindi non blocca quel lavoro. Non ancora corretto. |
| ~~Y-30~~ | ~~Voce dell ingegnere: tono e ripetizioni~~ | ✅ **Riscritte** (`9c79ece`). Le frasi parlavano dal punto di vista del software ("ci servono dati per i calcoli") e usavano gergo da menu (ALL4, FuelToAdd). Ora tono pit wall, gesto prima del motivo, verbi variati, e frasi **neutre** perché la cascata salta i passi noti e qualunque passo può essere il primo — il contesto lo dà una sola apertura (`CALIB_INTRO`). Solleciti progressivamente più asciutti invece della stessa frase ripetuta. `CALIB_SG_REQ` rimosso. **Trovato e corretto un difetto del turno precedente**: il progetto ha **7 lingue**, non 3, e le chiavi della cascata erano finite solo in EN/IT/DE — gli altri utenti avrebbero sentito silenzio, senza errori visibili. |
| ~~Y-28~~ | ~~Calibrazione guidata in Practice~~ | ✅ **Implementato** (`490902a`, `449cf88`, `dd16814`, `d0a692f`). Disegno in `.ai/plans/2026-08-25-calibration-cascade-design.md`, piano in `...-implementation.md`. L'ingegnere guida passo passo: giro genuino → drive-through → sosta solo benzina → gomme All4/2/1, saltando ciò che è già noto (Track+Class e Class verificati separatamente). Riconosce ciò che il pilota fa davvero invece di rifiutare; ripete solo se un giro passa senza avanzamento, massimo tre volte. **Da verificare sul campo**: nessuna sessione di Practice reale è ancora stata girata. |
| ~~Y-27~~ | ~~`BaseCapacity` sovrascritta senza consenso~~ | ✅ **Corretto** (`c646d7b`). Trovato da Antigravity. `OpponentTracker.cs:402` riscriveva a **ogni tick** con qualunque campione fuori da 0.5 L — che non è un filtro ma una soglia di attivazione. Il valore è un rapporto fra due letture di telemetria (`MaxFuelCapacity / BoP`), quindi una lettura transitoria entrava dritta nel database. Ora consenso con mediana, tolleranza 1 L. |
| ~~Y-31~~ | ~~Totale giri del Player: dente d'arresto a +1~~ | ✅ **Corretto** (`dbcb168`). `UpdateLatchedLaps` ha una banda asimmetrica (sale a +0.05, scende a −1.05) corretta per un ingresso **continuo**; il chiamante però arrotondava la posizione all'intero prima di passarla, e un calo di un giro vale esattamente 1.00 — sotto soglia, sempre. Road Atlanta `20260828_205434`: 35 alle 21:06:16.912, **36 un tick dopo**, e 36 fino alla bandiera in una gara da 35 giri; +2.26 L in `FuelToAdd`, esattamente il consumo di un giro misurato. Lo stesso filtro sul leader funziona (48→47→46→45→36→37→39→38) perché lì la posizione arriva continua: la differenza era **una istruzione**. Esposto anche `ProjectedPosAtCheckered` (dove sarà il Player alla bandiera, col decimale — es. `34.80`), che prima veniva calcolato e scartato subito dopo. **Residuo noto:** togliere l'arrotondamento sblocca la discesa ma non stabilizza la stima grezza, che nello stesso log ha oscillato fra 34 e 36. Quanto il filtro debba lavorare si misura solo osservando la proprietà nuova su un replay. |
| Y-32 | Passo del leader sbagliato: **riapertura della famiglia Y-17b** | Road Atlanta `20260828_205434`. `Federico Sartor`, leader dei primi 14 giri, ha passo **vero** 69-70 s (misurato da passaggi consecutivi allo stesso punto: 21:05:26, 21:06:35, 21:07:44, 21:08:55, 21:10:05) e baseline registrata **55.929 s**. Compare al giro 3 (BoP caricata 21:05:24), a gara in corso: l'ancoraggio finisce a metà del suo giro e mezza tornata viene archiviata come giro intero. Non è isolato — stessa sessione: `51.192`, `53.389`, `55.492`, e una a `162.849`. `L_Pace` durante la gara: `120.0` (default) → `77.6` → **`55.9` per dieci giri** → `80.9` → `68.4` → `82.3`, mai vicino al vero. **Perché conta due volte:** alimenta la stima dei giri del leader (`LatchedTotal` da 36 a 48 nella stessa gara) *e*, tramite `TimeUntilLeaderCheckered`, il momento in cui esce la bandiera — quindi entra nel nostro `ProjectedPosAtCheckered`. Y-17b era stato chiuso e verificato su Daytona: o la protezione `HasWitnessedLapStart` non copre questo percorso, o non copre il caso multiclasse con ingressi a gara in corso. **Da non chiudere senza capire quale dei due.** L'utente ha chiesto la proprietà con i giri del leader: va esposta **dopo** questo punto. |
| Y-33 | `Player Spatial Pit Entry` scatta a ogni giro in pista | Road Atlanta `20260828_205434`. In gara il Player si è fermato **una volta sola** (giro 21), eppure l'evento di ingresso corsia box è stato emesso **a ogni giro dal 22 al 34 — tredici volte** — mentre era in pista, sempre a `Pos: 0.9576`. Il rilevamento scatta sulla sola posizione sul tracciato, senza distinguere chi entra davvero da chi passa di lì. Dopo la sosta vera, il primo "ingresso" registrato è a `Pos: 0.0884`, cioè il punto di **uscita** archiviato come ingresso. Spiega in un colpo solo le tre cose osservate nella practice del 28/08: i drive-through mai confermati (nessun transito si chiude, ne viene aperto uno nuovo ogni giro), la geofence corrotta (`Pit Entry Pct Calibrated 0.070`, consenso 1/2 poi 2/3, contro lo `0.957` corretto imparato in gara — l'utente ha **visto** `PitEntryPct` e `PitExitPct` quasi identici nel database), e il `PitTransitTime` di ~4 s trovato nel JSON. Correlati: `InOutPitAccDecTime=-47.65s` (negativo), e valori appresi che non convergeranno mai (9.55 / 6.87 / 12.35 / 12.13 / 10.48 s con tolleranza di consenso 2 s). L'utente chiede una revisione **alla radice**, non un ritocco, più un log dedicato alle calibrazioni che viva in Practice/Test e taccia in gara (attenzione: `LogManager` oggi fa l'opposto, scarta le righe fuori dalla gara). |
| Y-34 | Arrotondamento di `FuelToAdd` all'intero | **Deciso con l'utente il 2026-08-29, non ancora implementato.** iRacing non accetta decimali nel rifornimento; oggi il plugin manda `#fuel 30.5l` col decimale e **arrotonda il gioco**, con regola ignota. Schema approvato: **AGGR** per difetto, **NORM** al più vicino, **SAFE** per eccesso — con rete di sicurezza su AGGR: se il risparmio richiesto non è realizzabile si arrotonda per eccesso, e in ogni caso l'ingegnere annuncia la percentuale. Il calcolo esiste già (`ComputeFuelSaving`, soglia `MaxAchievableFuelSaving = 0.15` più il vincolo di una sola sosta) ma **quel 15% non viene da un replay misurato**, sta scritto nel codice stesso. Numeri veri Road Atlanta (2.25 L/giro, 1.63 L/s): con 14 giri residui un ammanco di 0.50 L chiede l'1.6% di risparmio, con 3 giri il 6.7%, con 2 giri l'8.9% — la stessa frazione di litro costa quasi sei volte tanto a fine gara, ed è lì che la rete di sicurezza deve intervenire. |

---

## 📍 Stato corrente

**Fase:** setup del protocollo di collaborazione multi-AI.
**Milestone attiva:** M0 — Infrastruttura & Debiti di configurazione.

### Contesto del progetto

Plugin SimHub in C# / .NET Framework 4.8 (`User.PluginSdkDemoEdit/`), assemblato come libreria
`User.PluginSdkDemo.dll` e copiato in `%SIMHUB_INSTALL_PATH%` da un post-build event.
Il cuore funzionale è la strategia di gara: gestione pit, carburante, gomme, tracking avversari,
annunci vocali (Piper TTS) e telemetria.

### Milestone

| ID | Milestone | Stato |
|----|-----------|-------|
| M0 | Infrastruttura collaborazione multi-AI (git, `.ai/`, `CLAUDE.md`, config fix) | ✅ fatto |
| M1 | — da definire | ⬜ |

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

| Agente | Punto di forza | Responsabilità primaria |
|--------|----------------|--------------------------|
| **Antigravity** (Google/Gemini) | Contesto globale, ambiente Windows/VS/MSBuild | Architettura di sistema, planning artifacts, integrazione toolchain |
| **Claude Code** (Anthropic) | Modifiche chirurgiche, CLI, esecuzione | Implementazione, refactoring, script, build e test |
| **Codex/ChatGPT** (OpenAI) | Analisi algoritmica | Code review, ottimizzazione, validazione logico-matematica, second opinion |
