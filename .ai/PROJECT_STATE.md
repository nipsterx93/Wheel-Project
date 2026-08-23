# PROJECT STATE — The Wheel Project / Antigravity 2.0

> Fonte di verità sullo stato corrente e sul **turno di scrittura**.
> Ogni agente legge questo file **prima** di toccare il codice e lo aggiorna **prima** di iniziare a scrivere.

---

## 🔒 LOCK

```yaml
owner:      NONE          # NONE | antigravity | claude | codex | human
since:      2026-08-23T17:00:00Z
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

## 🚧 Congelati in attesa di decisione

Prendere il lock **non** autorizza a toccare questi punti: servono decisioni di prodotto, non di
implementazione. Chi decide, aggiorni questa tabella prima di far partire il lavoro.

| ID | Punto | Decisione richiesta |
|----|-------|---------------------|
| ~~Y-1~~ | ~~`CanFinishWithoutPitting`~~ | ✅ **Opzione 1 implementata** (`bc20a67`). Non più silenziamento indiscriminato: `canFinishWithoutPitting` sopprime l'undercut (con `RejectReason` dedicato `NoPitNeeded`) ma **non** l'overcut, che in quel caso è già vinto. Aggiunto `FuelSaveTarget` con filtro di fattibilità. |
| ~~Y-2~~ | ~~`OvercutTrafficOK` cablato a `true`~~ | ✅ **Implementato** (`f08bf43`) come "chi ho davanti adesso", distinto dall'undercut che è proiettato all'uscita box. Finestra 2.0 s, **da calibrare** sul primo replay con overcut e traffico veri. |
| ~~Y-11~~ | ~~Modello warmup~~ | ✅ **Corretto** (`8b42efd`). Gli outlap non entrano più nella media del degrado. Soglia `0.10` ora condivisa invece che ricopiata in tre punti. |
| ~~Y-3~~ | ~~`LapsSinceLastPit`~~ | ⏹️ **Chiuso senza intervento.** L'imprecisione (fino a un giro) è trascurabile rispetto alla scala delle decisioni che governa. Eventualmente in futuro. |
| ~~Y-8~~ | ~~Deadband HUD 0.05~~ | ⏹️ **Chiuso senza intervento.** Il dato resta com'è: mostrare un valore per un altro non serve a nulla. |
| ~~Y-12~~ | ~~Isteresi dei gate strategici~~ | ✅ **Deciso e implementato** (commit `1fa7b15`), approvato da Antigravity e Codex. Banda `±0.25` sulla posizione, `±0.15` sui margini, dwell `5 s`. Valori da sweep sul replay `20260819_205004` con simulatore validato 1958/1958 campioni. Il filtro EMA sul gap è stato **valutato e scartato**. Da confermare su un secondo replay e su un circuito diverso. |
| Y-13 | Gap che salta di un giro al rollover | `GapJump` (commit `ff480bd`) scarta il campione nel **ritmo relativo**, ma la causa è a monte: `posDiffLaps * refLapTime` produce un gap sbagliato di esattamente un giro per un tick, quando i due contatori sono disallineati. Lo stesso gap alimenta **anche i gate strategici**, dove non c'è filtro. Oggi l'isteresi assorbe il colpo (banda 0.25 s, dwell 5 s), quindi non è urgente. Si corregge il calcolo a monte, o si accetta il sintomo curato in un posto solo? **Il difetto è intermittente**: è una race fra il campione macrosettoriale e il rollover, e la finestra dura pochi millisecondi. Comparso nel replay `221922` allo stesso punto in cui il `230109` non mostra nulla (`gapDelta` 93.033 contro 0.032). Non contare quindi sull'assenza di `GapJump` in un singolo replay come prova che sia risolto. |
| ~~Y-9~~ | ~~Euristica pit del Player~~ | ✅ **Uniformato** (`81a5f12`). Il Player usa la stessa cascata degli avversari via `PitLaneDetector`. Soglie di velocità ora derivate dal `PitLaneSpeedLimit` appreso per traccia+classe invece che cablate. Corretto anche un bug multiclasse nell'apprendimento. |
| Y-14 | Derivare `TyreChangeTime` senza `TyreSelectionScope` | `TyreManager.CurrentScope` è pilotato solo dai tasti volante (`TyreManager.cs:89`), nessun percorso lo deriva dalla telemetria — in un replay non presidiato resta a `None`. **Corretto**: un replay *presidiato*, dove qualcuno guarda la sosta e riproduce lo scope a mano in tempo reale, funziona — per il plugin è indistinguibile da un pilota live. Resta comunque `EstimatedPlayer`, mai `Confirmed`: il sistema non sa che è una ricostruzione deliberata. Percorso più solido, proposto dall'utente: derivare il tempo gomme **senza mai leggere lo scope**, per sottrazione da dati già interamente grondati in telemetria — `StationaryTime` e `RefuelingTime` (quest'ultimo già isolato con precisione dai timestamp reali di `CurrentFuelLevel`, non dal solo scope). Ma la formula si biforca per layout: **Sequential** → `TempoGomme = StationaryTime − RefuelingTime` (esatta); **Simultaneous** → la sottrazione sottostima, la relazione corretta è `TempoGomme = StationaryTime` quando `RefuelingTime < StationaryTime`, altrimenti solo un limite superiore. `IsPitLayoutSequential` (`PitRadar.cs:335`) di default presume **Sequential per ogni gioco tranne iRacing**, e **Simultaneous per iRacing** finché non rilevato dinamicamente — se Daytona/IMSA è iRacing, il default è già il caso in cui la sottrazione sbaglierebbe. `TriggerDynamicLayoutDetection` (che confermerebbe quale dei due) richiede di conoscere **già** sia `tFuel` sia `tTyres`: stesso uovo-e-gallina per una classe mai vista. Il tetto anti-riparazione-danni (`OpponentTracker.cs:1007`, `tFuel+tTyres+6.0`) ha lo stesso limite, e ricade su un default generico di 26 s (`PitRadar.cs:329`) non calibrato sulla vettura reale. |
| ~~Y-16~~ | ~~`RaceLifeTimeLeftSec` ricostruito dal leader~~ | ✅ **Corretto** (`cc35f97`). Era `leaderLapsRem * leaderPace + leaderRemainingPitTime`, cioè un conteggio giri **latchato** rimoltiplicato per un passo che nel frattempo poteva essere cambiato — in multiclasse succede a ogni cambio di identità del P1 assoluto. Daytona giri 12-15: 2368 s stimati contro ~1400 s reali, giri del Player quasi raddoppiati, e da lì dritto in `FuelToAdd` (`#fuel 50l` dove ne servivano ~31.5; versati realmente 31.6). Ora ancorato al countdown di sessione (`RaceTimeProjection.TimeUntilLeaderCheckered`): il leader pesa **solo** sulla frazione di giro che gli manca per tagliare, quindi l'errore è limitato a un giro invece di scalare con la durata della gara. |
| ~~Y-17~~ | ~~Passo del leader contaminato dallo sfarfallio~~ | ✅ **Corretto** (`cc35f97`). `LeaderPaceFilter` scarta i giri fisicamente impossibili (>110 m/s di media su giro intero) e i campioni raccolti mentre l'identità del leader non si è ancora stabilizzata (dwell 2 s — le raffiche di `TargetChanged` nel log reale sono a decine di millisecondi). Media mobile invariata (α 0.10). **È un secondo strato, non il primo**: la protezione vera è Y-16. Nota onesta: il limite fisico a Daytona cade a 52.1 s e il valore patologico osservato era 56.391 s — il floor da solo **non** l'avrebbe preso, è il dwell a farlo. |
| ~~Y-18~~ | ~~Due definizioni divergenti di "fermo ai box"~~ | ✅ **Corretto** (`cedc2aa`). `PitRadar` usava `IsInPitBox` nel percorso di calibrazione e `SpeedKmh < 0.5 \|\| IsInPitBox` in quello spaziale. `IsInPitBox` è il flag grezzo del gioco: si alza solo nello stallo assegnato col servizio in corso, quindi una sosta per cedere una posizione non lo fa mai scattare. Stesso log, stesse soste: 41.73 s / 68.40 s dal percorso spaziale contro `StatTime: 0.0s` da quello di calibrazione. Il danno vero era `_fuelLevelAtStopStart` mai reinizializzato → `FuelAdded` fantasma (15.8 L e 12.7 L con zero secondi di sosta). Ora unificato via `PitRadar.IsPlayerStationaryInPit`. |
| ~~Y-20~~ | ~~`PitLaneSpeedLimit` "l'ultimo che scrive vince"~~ | ✅ **Corretto** (`e9caad6`). Era un assegnamento nudo (`target.PitLaneSpeedLimit = speedLimit`), senza confidenza né consenso. Misano `20260823_133904`: 11 osservazioni a 60 km/h e una a 80 (vettura ancora in decelerazione) — l'80 ha sovrascritto il 60, salvato solo perché altri hanno scritto dopo. Ora consenso per classe, scrive la **mediana**. |
| ~~Y-21~~ | ~~Geofence riscritte da un campione singolo~~ | ✅ **Corretto** (`e9caad6`). `CanOverwrite(Confirmed, Confirmed)` è vera, quindi ogni sosta riscriveva `PitEntryPct`/`PitExitPct`. Su Misano una sola sosta ha spostato l'uscita da 0.1088 a 0.0737 (~148 m) — e quella geofence alimenta `IsInExtendedPitLaneZone`, quindi la cascata Y-9 del Player **e** il rilevamento avversari. Ora: una sosta = `EstimatedPlayer` (usabile su pista nuova, non scalza un dato consolidato), tre soste concordi = `Confirmed`. |
| ~~Y-22~~ | ~~Quale sia il valore vero di `PitExitPct` a Misano~~ | ✅ **Risolto dai dati** (tre replay in `Logs/3 Run Test`, 2026-08-23). La misura è ripetibile **sotto il metro**: `PitExitPct` 0.0738605 / 0.0738605 / 0.0737054 (dispersione 0.65 m), `PitEntryPct` 0.9495097 / 0.9495097 / 0.9495739 (0.27 m). I run 1 e 2 sono bit-identici. I 148 m che separavano il vecchio `0.1088` dal nuovo `0.0737` sono duecento volte il jitter osservato: **non era dispersione di misura**, era un valore di altra provenienza congelato dal guard `== -1.0`. Il valore corretto è `0.0737`. Nota: i replay giravano a 5x, dove il campionamento è più grossolano rispetto alla distanza percorsa — il jitter a 1x sarà uguale o minore. |
| Y-22 | Quale sia il valore vero di `PitExitPct` a Misano | Aperto e **non deciso**: `0.1088` (vecchio, congelato dal guard `== -1.0` di prima delle fasi 1-5) contro `0.0737` (misurato il 2026-08-23). Distano 0.035 di giro, ~148 m. Il tempo di transito misurato (36.0 s in movimento, `PitInOutAccDecTime` 12.2 s) è compatibile con entrambi entro il margine di accelerazione/decelerazione: **non è dirimente**. Rigirare lo *stesso* replay non risolve — è la stessa misura ripetuta, non una seconda osservazione; dice solo se la misura è sensibile al timing di riproduzione (utile: sappiamo che due playback dello stesso replay non sono identici, `STRATEGY_CHANGED` 25 contro 12 a Daytona). Serve una **sosta diversa**: altra sessione a Misano, o un replay con due soste. |
| Y-19 | Tetto sui giri totali del Player in multiclasse | Il clamp `Math.Min(projectedPlayerTotal, _latchedLeaderTotalLaps)` è stato **disattivato in multiclasse** (`cc35f97`): una GT3 e una GTP non fanno lo stesso numero di giri, quindi il confronto col leader **assoluto** non dice nulla — e se il totale del leader è sovrastimato il tetto non protegge comunque. Il tetto corretto è il leader **di classe**, che gira nella stessa vettura del Player. Non cablato perché richiede la posizione assoluta degli avversari (giri completati + frazione di giro) e le convenzioni di conteggio giri di `GameReaderCommon.Opponent` non sono verificate: `PositionInClass` non è usato da nessuna parte nel repo, e un off-by-one qui si propagherebbe silenziosamente in `FuelToAdd`. **Da tarare sui log Daytona già in `Logs/`**, dove il valore vero (26 giri) è noto. |
| Y-15 | Validazione `FuelToAdd` contro il reale | Oggi non c'è modo di sapere se la raccomandazione `SimRIG.Fuel.FuelToAdd` (calcolata prima della sosta) corrisponde a quanto viene poi realmente versato. Diverso dalla Fase 4: quella impara `FuelFillRate` dai litri reali, non giudica la qualità del consiglio. Il dato grezzo esiste già (`Pit Complete` logga `FuelAdded` dalla variazione di `CurrentFuelLevel`), manca solo l'abbinamento col valore predetto, congelato al momento di `PitLaneEntered` (il gate della Fase 3 lo espone già). Diagnostico puro, non tocca il sistema di confidenza. Stesso pattern già usato a mano in `ReplayBacktestIntegrationTest` (`Ground Truth: 16.0L` scritto nel test) — qui diventerebbe automatico, letto dalla telemetria invece che digitato. |

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
