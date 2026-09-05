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

### Moduli di decisione, senza dipendenze SimHub

Nati tutti dallo stesso vincolo: le classi principali ricevono `SessionState`, `PitRadar`,
`OpponentTracker` e `LogManager`, quindi la loro logica **non è raggiungibile dai test**. Ogni volta
che una decisione andava verificata, è stata estratta qui. Sono file piccoli, puri, e sono il posto
naturale dove aggiungere nuove regole invece di annidarle nei file grandi.

| File | Ruolo | Nato da |
|------|-------|---------|
| `StrategyGateHysteresis.cs` | Bande di isteresi e permanenza minima sulle decisioni strategiche, per fermare lo sfarfallio undercut/overcut | Y-12 |
| `PitLaneDetector.cs` | Cascata unica di rilevamento pit (telemetria → geofence → fermo → velocità persistente), condivisa fra Player e avversari | Y-9 |
| `TrackPositionValidator.cs` | Distingue un avanzamento guidato da un teletrasporto, per plausibilità del movimento. `WrappedDelta` è il calcolo di delta con wrap del traguardo usato anche altrove | Fase 1 calibrazioni |
| `GeofenceCalibrationGate.cs` | Autorizza la calibrazione delle geofence solo dopo un tragitto genuino in pista | Fase 3 calibrazioni |
| `RaceTimeProjection.cs` | Tempo alla bandiera ancorato al countdown di sessione, e giri proiettati. Il leader pesa solo sulla frazione di giro che gli manca | Y-16 |
| `LeaderPaceFilter.cs` | Filtra il passo del leader: scarta i giri fisicamente impossibili e i campioni raccolti mentre l'identità del P1 sfarfalla | Y-17 |
| `CalibrationConsensus.cs` | Mediana su finestra scorrevole per i dati calibrati, al posto di "il primo/l'ultimo che scrive vince" | Y-20, Y-21 |
| `PlayerPitSpeedObserver.cs` | Legge il limite della corsia box dal limitatore del Player invece di dedurlo dagli avversari | Y-28 |
| `CalibrationCascade.cs` | Decide **quale** calibrazione manca e in che ordine chiederla | Y-28 |
| `CalibrationCascadeRunner.cs` | Decide **quando** l'ingegnere parla: insistenza legata al progresso, non al tempo | Y-28 |

Ci sono anche predicati statici puri estratti **dentro** i file grandi, per lo stesso motivo:
`OpponentTracker.CanMeasureLap` / `AnchorIsGenuine` (Y-17b), `PitRadar.HasTraversedPitLane` /
`IsPlayerStationaryInPit` (Y-18, Y-23), `RaceAnalyzer.IsLeaderSampleUsable` /
`HoldLeaderLapsCompleted` (Y-24, Y-25). Vedi ADR-004 sul perché.

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
- ~~Il progetto di test **non è nella solution**: va buildato esplicitamente (vedi `CLAUDE.md`).~~

> **Nota di aggiornamento (2026-09-05, claude).** Un ADR accettato non si modifica, ma questa
> conseguenza ha smesso di essere vera e seguirla porterebbe fuori strada: `User.PluginSdkDemo.sln`
> contiene **entrambi** i progetti, quindi il progetto di test viene compilato dalla build principale.
> `CLAUDE.md` è già allineato. Verificato leggendo le voci `Project(...)` della solution.

---

### ADR-004 — Un fix non è chiuso finché il suo test non fallisce senza di lui

- **Data:** 2026-08-24
- **Stato:** Accettato
- **Deciso da:** Claude + umano (emerso dalla pratica, formalizzato a posteriori)

**Contesto**
I difetti trovati fra il 19 e il 24 agosto avevano quasi tutti la stessa forma: un dato singolo non
verificato che si cristallizza (il primo valore che vince per sempre, l'ultimo che sovrascrive, il
più basso che non viene mai sostituito). Correggerli richiede toccare file grandi le cui classi non
sono istanziabili nei test, perché dipendono da tipi SimHub.

Il rischio pratico osservato: si scrive un test che *riproduce* la regola invece di chiamarla, e
resta verde qualunque cosa faccia il codice di produzione. È successo davvero su Y-25 — il test
passava anche con il guard disattivato, perché ne aveva una copia al proprio interno.

**Decisione**
Ogni correzione segue tre passi, nell'ordine:
1. la decisione viene **estratta** in una funzione pura (file nuovo senza dipendenze SimHub, oppure
   metodo statico dentro il file grande);
2. il test **chiama quella funzione**, non ne riproduce la logica;
3. prima di committare si **neutralizza il fix** e si verifica che il test diventi rosso.

Il passo 3 non è una formalità: è l'unico che dimostra che il test copre davvero il difetto.

**Conseguenze**
- I casi di regressione usano i **numeri reali** presi dai log, non valori inventati: `56.409` per
  la baseline di Y-17b, `0.963` per la geofence di Y-23, `0.004` di giro per lo sfarfallio.
  Chi rilegge il test ritrova l'evento nel log.
- I file grandi si arricchiscono di metodi statici pubblici che esistono per essere testati.
  È un costo accettato consapevolmente.
- Il conteggio dei test è un indicatore di copertura reale, non decorativo (111 → 152 in questa
  serie di interventi).

**Alternative scartate**
- *Test di integrazione sul replay completo*: già presenti, ma troppo lenti e grossolani per isolare
  una singola regola, e non falliscono in modo diagnostico.
- *Mock dei tipi SimHub*: sposta il costo sulla costruzione dei mock, che è dove le AI sbagliano di più.

---

### ADR-005 — Un campione singolo non è una misura

- **Data:** 2026-08-24
- **Stato:** Accettato
- **Deciso da:** Claude + umano

**Contesto**
La telemetria dei simulatori produce artefatti transitori: flag che sfarfallano fra due tick,
record avversario momentaneamente vuoti, giri parziali per vetture che compaiono a gara in corso.
Ogni volta che uno di questi campioni è finito dritto in un dato persistente, ci è rimasto: il
database non ha memoria di *quanto* fosse solido il valore che contiene.

**Decisione**
Un dato calibrato si scrive solo con un criterio di solidità esplicito, scelto in base a quanti
campioni sono disponibili:
- **molti campioni** (limite pit lane, dagli avversari) → mediana su finestra scorrevole;
- **pochi campioni** (geofence, una per sosta) → consenso persistito fra sessioni, con il livello di
  `CalibrationConfidence` che sale al crescere delle osservazioni concordi;
- **campione palesemente non fisico** → si scarta a monte, senza nemmeno entrare nel consenso.

Il criterio di scarto guarda sempre a una **grandezza fisica**, non a una soglia temporale: la
strada percorsa, non la durata; il movimento plausibile, non il tempo trascorso. Una soglia
temporale è aggirabile e non si trasferisce fra circuiti.

**Conseguenze**
- Un valore consolidato resiste a un campione anomalo, ma non diventa inamovibile: abbastanza
  osservazioni concordi nuove lo sostituiscono comunque.
- Serve un campo in più a persistere il consenso (`GeofenceSampleCount`), perché la memoria di
  sessione non basta su circuiti dove si fa una sosta a gara.
- I dati già presenti prima di questa regola vengono trattati come consolidati dalla migrazione:
  degradarli li esporrebbe a essere sovrascritti da un campione singolo, cioè l'opposto dello scopo.

**Alternative scartate**
- *Media invece di mediana*: un solo campione fuori scala la trascina, ed è il caso da cui ci si difende.
- *Prendere sempre l'ultimo valore*: è il difetto che ha lasciato passare gli 80 km/h di Misano.
- *Prendere sempre il primo*: è il difetto opposto, che ha congelato `PitExitPct` a 0.1088 per settimane.

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
