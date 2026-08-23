# PROJECT STATE — The Wheel Project / Antigravity 2.0

> Fonte di verità sullo stato corrente e sul **turno di scrittura**.
> Ogni agente legge questo file **prima** di toccare il codice e lo aggiorna **prima** di iniziare a scrivere.

---

## 🔒 LOCK

```yaml
owner:      claude        # NONE | antigravity | claude | codex | human
since:      2026-08-23T10:30:00Z
task:       Y-16/Y-17/Y-18 — proiezioni gara ancorate al countdown reale, guard sul passo
            del leader, condizione "fermo ai box" unificata
scope:      RaceAnalyzer.cs, PitRadar.cs, RaceTimeProjection.cs (nuovo),
            LeaderPaceFilter.cs (nuovo), User.PluginSdkDemo.Tests/**
expires:    2026-08-23T18:00:00Z
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
