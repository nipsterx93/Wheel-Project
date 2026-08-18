# PROJECT STATE — The Wheel Project / Antigravity 2.0

> Fonte di verità sullo stato corrente e sul **turno di scrittura**.
> Ogni agente legge questo file **prima** di toccare il codice e lo aggiorna **prima** di iniziare a scrivere.

---

## 🔒 LOCK

```yaml
owner:      NONE          # NONE | antigravity | claude | codex | human
since:      2026-08-18T17:15:00Z
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
3. **File `*_LEGACY.cs` orfani.** `DataPluginDemo_LEGACY.cs`, `FuelCalculator_LEGACY.cs`,
   `PitStrategyManager_LEGACY.cs` (~180 KB totali) sono sul disco ma **non** nel `<Compile>`
   del csproj: non vengono compilati. Rischio concreto che un agente li legga o li modifichi
   credendoli attivi.
4. **File sorgente molto grandi.** `DataPluginDemo.cs` (~155 KB), `OpponentTracker.cs` (~105 KB),
   `SettingsControlDemo.xaml` (~99 KB). Vanno letti a fette, non in blocco.

---

## 👥 Ruoli

| Agente | Punto di forza | Responsabilità primaria |
|--------|----------------|--------------------------|
| **Antigravity** (Google/Gemini) | Contesto globale, ambiente Windows/VS/MSBuild | Architettura di sistema, planning artifacts, integrazione toolchain |
| **Claude Code** (Anthropic) | Modifiche chirurgiche, CLI, esecuzione | Implementazione, refactoring, script, build e test |
| **Codex/ChatGPT** (OpenAI) | Analisi algoritmica | Code review, ottimizzazione, validazione logico-matematica, second opinion |
