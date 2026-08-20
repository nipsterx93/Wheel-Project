# PROJECT STATE — The Wheel Project / Antigravity 2.0

> Fonte di verità sullo stato corrente e sul **turno di scrittura**.
> Ogni agente legge questo file **prima** di toccare il codice e lo aggiorna **prima** di iniziare a scrivere.

---

## 🔒 LOCK

```yaml
owner:      claude        # NONE | antigravity | claude | codex | human
since:      2026-08-19T23:40:00Z
task:       Y-11, Y-2, Y-9, Y-1 — decisioni prese con l'utente, sblocco dei punti congelati
scope:      Y-11  RaceAnalyzer.cs — escludere gli outlap dalla media di PaceDropDueToTyres
            Y-2   TargetStrategyManager.cs — implementare OvercutTrafficOK (traffico davanti ora)
            Y-9   TargetStrategyManager.cs + OpponentTracker.cs — rilevamento pit del Player
                  uniformato alla cascata degli avversari; soglie adattive da PitLaneSpeedLimit
                  appreso per classe; PitRadar.cs + PitLaneSpeedLimit per classe nel JSON
            Y-1   FuelManager.cs + TargetStrategyManager.cs — FuelSaveTarget e selezione
                  degli avvisi di fine gara (Opzione 1)
            + test e .csproj se servono nuovi file
expires:    2026-08-20T06:00:00Z
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
| Y-1 | `CanFinishWithoutPitting` | Va mantenuta? Se sì va approvata come estensione, e le serve un `RejectReason` dedicato: oggi si maschera da `RaceTooLate`. Rischio noto: silenzia un overcut vincente quando il Player finisce col carburante ma il Target deve fermarsi. |
| Y-2 | `OvercutTrafficOK` cablato a `true` | Implementare il check o rimuovere il gate? Oggi è inerte e la dashboard espone un `true` privo di significato. |
| Y-11 | Modello warmup | La media mobile a 4 giri di `PaceDropDueToTyres` include i giri post-pit su gomma fredda, che vengono poi ri-sommati come warmup esplicito. Filtrare quei giri dalla media, o accettare il double counting? |
| Y-3 | `LapsSinceLastPit` | Renderlo continuo come da spec §26 richiede una misura frazionaria di giro sul target: esiste già in `OpponentTelemetryData`? |
| Y-8 | Deadband HUD 0.05 | La spec §30 lo richiede ma non definisce il formato: cosa mostrare per `|RelativePace| < 0.05` — `0.0s/lap`, `~0`, stringa vuota? |
| ~~Y-12~~ | ~~Isteresi dei gate strategici~~ | ✅ **Deciso e implementato** (commit `1fa7b15`), approvato da Antigravity e Codex. Banda `±0.25` sulla posizione, `±0.15` sui margini, dwell `5 s`. Valori da sweep sul replay `20260819_205004` con simulatore validato 1958/1958 campioni. Il filtro EMA sul gap è stato **valutato e scartato**. Da confermare su un secondo replay e su un circuito diverso. |
| Y-13 | Gap che salta di un giro al rollover | `GapJump` (commit `ff480bd`) scarta il campione nel **ritmo relativo**, ma la causa è a monte: `posDiffLaps * refLapTime` produce un gap sbagliato di esattamente un giro per un tick, quando i due contatori sono disallineati. Lo stesso gap alimenta **anche i gate strategici**, dove non c'è filtro. Oggi l'isteresi assorbe il colpo (banda 0.25 s, dwell 5 s), quindi non è urgente. Si corregge il calcolo a monte, o si accetta il sintomo curato in un posto solo? **Il difetto è intermittente**: è una race fra il campione macrosettoriale e il rollover, e la finestra dura pochi millisecondi. Comparso nel replay `221922` allo stesso punto in cui il `230109` non mostra nulla (`gapDelta` 93.033 contro 0.032). Non contare quindi sull'assenza di `GapJump` in un singolo replay come prova che sia risolto. |
| Y-9 | Euristica pit del Player | `TrackPositionPercent > 0.85 && 10 < SpeedKmh < 100` classifica come "in pit" anche un tornante lento di fine giro. Sostituire con il solo `IsInPitLane`, o con la geofence già usata per gli avversari? |

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
