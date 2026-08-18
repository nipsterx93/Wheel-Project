# ARCHITECTURE — The Wheel Project / Antigravity 2.0

Decisioni architetturali (ADR), convenzioni e mappa dei moduli.

---

## Mappa dei moduli

Progetto principale: `User.PluginSdkDemoEdit/` → `User.PluginSdkDemo.dll` (.NET Framework 4.8, WPF)

| File | Tipo principale | Ruolo |
|------|-----------------|-------|
| `DataPluginDemo.cs` | `DataPluginDemo : IPlugin, IDataPlugin, IWPFSettingsV2` | **Entry point del plugin.** Ciclo `DataUpdate`, registrazione proprietà/azioni SimHub, orchestrazione dei manager. ~155 KB — leggere a fette. |
| `DataPluginDemoSettings.cs` | `DataPluginDemoSettings` | Modello di persistenza delle impostazioni |
| `SettingsControlDemo.xaml(.cs)` | `UserControl` | UI di configurazione dentro SimHub (~99 KB XAML + ~74 KB code-behind) |
| `SessionState.cs` | `SessionState` | Stato della sessione di gara corrente |
| `TelemetryReader.cs` | `TelemetryReader` | Lettura/normalizzazione telemetria dal game |
| `TargetStrategyManager.cs` | `TargetStrategyManager`, `TargetState` | Strategia di gara: target pace, finestre pit |
| `FuelManager.cs` | `FuelManager`, `FuelCalculations` | Consumo carburante, stint, calcoli di rifornimento |
| `TyreManager.cs` | `TyreManager` | Gestione gomme, usura, scelta mescola |
| `OpponentTracker.cs` | `OpponentTracker`, `OpponentTelemetryData` | Tracking avversari, gap, undercut/overcut (~105 KB) |
| `RaceAnalyzer.cs` | `RaceAnalyzer`, `RaceAnalysisResult` | Analisi post/in-gara |
| `SectorTracker.cs` / `LapSectorTimeContainer.cs` | `SectorTracker` | Tempi per settore |
| `PitRadar.cs` | `ClassRecord`, `SimRigDatabase` | Dati pit / database record per classe e tracciato |
| `CarPitData.cs` | `CarPitData` | Parametri pit per vettura |
| `PiperEngine.cs` | `PiperEngine`, `PiperVoice` | Sintesi vocale (Piper TTS) per gli annunci |
| `PitWallLanguage.cs` | `PitWallLanguage` (static) | Testi/localizzazione degli annunci "muretto box" |
| `MacroManager.cs` | `MacroManager` (static) | Macro / invio input |
| `SimRigHardwareManager.cs` | `SimRigHardwareManager` | Input hardware del rig (SharpDX.DirectInput) |
| `ProfileManager.cs` / `SimRigProfile.cs` | `ProfileManager` (static) | Profili di configurazione |
| `LogManager.cs` | `LogManager` | Logging applicativo |

**File `*_LEGACY.cs`** (`DataPluginDemo_LEGACY.cs`, `FuelCalculator_LEGACY.cs`, `PitStrategyManager_LEGACY.cs`):
presenti sul disco ma **non inclusi** nel `<Compile>` del `.csproj` → **non vengono compilati**.
Vanno trattati come archivio di sola lettura. Non modificarli aspettandosi un effetto sul plugin.

Progetto di test: `User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/` → eseguibile console con
runner custom (`TestRunner.cs`), **non** un framework tipo NUnit/xUnit. Vedi ADR-002.

---

## ADR — Architecture Decision Records

Ogni decisione architetturale rilevante è un ADR numerato progressivo.
Una volta `Accettato`, un ADR **non si modifica**: si scrive un ADR successivo che lo supera
(`Superato da ADR-00X`). Serve a poter ricostruire *perché* le cose sono come sono.

### Formato

```markdown
### ADR-00N — <Titolo>
- **Data:** YYYY-MM-DD
- **Stato:** Proposto | Accettato | Superato da ADR-00X
- **Deciso da:** <agente/umano>

**Contesto** — qual è il problema, quali vincoli esistono.
**Decisione** — cosa si è deciso, in una frase chiara.
**Conseguenze** — cosa diventa più facile, cosa più difficile, cosa va rivisto.
**Alternative scartate** — e perché.
```

---

### ADR-001 — Protocollo di collaborazione multi-AI su cartella `.ai/`

- **Data:** 2026-08-18
- **Stato:** Accettato
- **Deciso da:** Antigravity + Claude + umano

**Contesto**
Tre agenti AI (Antigravity, Claude Code, Codex) lavorano sugli stessi file fisici, senza canale
di comunicazione diretto tra loro e senza garanzie di ordine temporale. Il rischio è sovrascrittura
silenziosa e disallineamento di contesto.

**Decisione**
Stato condiviso in `.ai/` (`PROJECT_STATE.md` con lock esplicito, `HANDOFF_LOG.md`,
`ARCHITECTURE.md`, `plans/`), **appoggiato su Git** come rete di sicurezza reale.
Il lock è procedurale; Git è ciò che rende ogni errore reversibile.

**Conseguenze**
- Ogni agente può ricostruire lo stato senza rileggere l'intera history.
- Serve disciplina: commit piccoli, un commit per turno, prefisso agente nel messaggio.
- `HANDOFF_LOG.md` va potato (ultimi 10) o diventa un peso in context.

**Alternative scartate**
- *Solo convenzione verbale, senza file di stato*: non sopravvive a un cambio di sessione.
- *Lock tecnico (file di lock + tooling)*: sovradimensionato per tre agenti che si alternano.

---

### ADR-002 — Trunk-based seriale invece di branch per agente

- **Data:** 2026-08-18
- **Stato:** Accettato
- **Deciso da:** Antigravity + Claude + umano

**Contesto**
Con tre AI che editano gli stessi file C# di grandi dimensioni (`DataPluginDemo.cs` ~155 KB),
i merge conflict sarebbero frequenti e la loro risoluzione automatica è la parte più fragile
del lavoro di un agente.

**Decisione**
Tutti lavorano su `main`, un agente alla volta secondo il lock in `PROJECT_STATE.md`.
La serializzazione avviene tramite il lock, non tramite branch.

**Conseguenze**
- Zero merge conflict tra agenti: chi non ha il lock non scrive.
- `main` deve restare sempre compilabile: chi rilascia il lock lascia il codice in stato buildabile.
- Niente parallelismo sulla scrittura. Analisi, review e planning restano invece parallelizzabili.

**Alternative scartate**
- *Branch per agente + PR*: più isolamento, ma sposta il costo sul merge, dove le AI sbagliano di più.

---

### ADR-003 — Test tramite runner console custom

- **Data:** 2026-08-18
- **Stato:** Accettato (constatazione dell'esistente)
- **Deciso da:** — (preesistente, formalizzato qui)

**Contesto**
`User.PluginSdkDemo.Tests` è un progetto `OutputType=Exe` con un `TestRunner.Main` che chiama in
sequenza `PitLossUnitTests`, `MergeGapUnitTests` e due test di integrazione su replay registrati.
Ritorna exit code `0` se tutto passa, `1` alla prima eccezione.

**Decisione**
Si mantiene il runner custom. I nuovi test si aggiungono come metodi statici `RunAllTests()`
richiamati da `TestRunner.Main`, e il file va aggiunto al `<Compile>` del `.csproj` di test.

**Conseguenze**
- Nessuna dipendenza esterna da NUnit/xUnit: la build resta semplice.
- L'exit code è verificabile da CLI → si integra bene con l'automazione.
- **Limite:** il runner si ferma al primo fallimento, quindi non dà il quadro completo in un colpo solo.
- Il progetto di test **non è nella solution**: va buildato esplicitamente (vedi `CLAUDE.md`).

---

## Convenzioni di progetto

- **Lingua:** commenti e messaggi di commit in italiano o inglese, purché coerenti nel file.
  Identificatori di codice sempre in inglese.
- **Stile C#:** convenzioni .NET standard — `PascalCase` per tipi e metodi pubblici,
  `camelCase` per locali e parametri, `_camelCase` per campi privati. Seguire lo stile del file
  circostante quando diverge.
- **Dipendenze:** le reference esterne si risolvono via `$(SIMHUB_INSTALL_PATH)`.
  Nessun nuovo path assoluto hardcoded (quelli esistenti a `E:\SimHub\` sono debito, vedi `PROJECT_STATE.md`).
- **Nessun nuovo file `*_LEGACY.cs`:** per archiviare codice, la history di Git basta.
