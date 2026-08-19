# PROJECT STATE — The Wheel Project / Antigravity 2.0

> Fonte di verità sullo stato corrente e sul **turno di scrittura**.
> Ogni agente legge questo file **prima** di toccare il codice e lo aggiorna **prima** di iniziare a scrivere.

---

## 🔒 LOCK

```yaml
owner:      NONE          # NONE | antigravity | claude | codex | human
since:      2026-08-18T23:50:00Z
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
| Y-1 | `CanFinishWithoutPitting` | Va mantenuta? Se sì va approvata come estensione, e le serve un `RejectReason` dedicato: oggi si maschera da `RaceTooLate`. Rischio noto: silenzia un overcut vincente quando il Player finisce col carburante ma il Target deve fermarsi. |
| Y-2 | `OvercutTrafficOK` cablato a `true` | Implementare il check o rimuovere il gate? Oggi è inerte e la dashboard espone un `true` privo di significato. |
| Y-11 | Modello warmup | La media mobile a 4 giri di `PaceDropDueToTyres` include i giri post-pit su gomma fredda, che vengono poi ri-sommati come warmup esplicito. Filtrare quei giri dalla media, o accettare il double counting? |
| Y-3 | `LapsSinceLastPit` | Renderlo continuo come da spec §26 richiede una misura frazionaria di giro sul target: esiste già in `OpponentTelemetryData`? |
| Y-8 | Deadband HUD 0.05 | La spec §30 lo richiede ma non definisce il formato: cosa mostrare per `|RelativePace| < 0.05` — `0.0s/lap`, `~0`, stringa vuota? |
| Y-12 | Isteresi dei gate strategici | 450 `STRATEGY_CHANGED` in 11 minuti nel replay `230037`. Cause misurate: `Position` 115, `Margin` 107, `Traffic` solo 3. Sono soglie attraversate di continuo (`SignedGap >= -0.5` e `CaptureMargin > 0`) combinate con il gap **istantaneo**. Serve un'isteresi: banda morta sulle soglie, un minimo di permanenza prima di cambiare stato, o un filtro sul gap. Con quali valori? |
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
