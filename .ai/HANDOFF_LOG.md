# HANDOFF LOG

> Diario dei passaggi di consegne. **Append in cima** (il più recente per primo).
> Si tengono solo gli **ultimi 10** handoff: gli altri si tagliano, la storia completa resta in `git log`.
>
> Recuperare lo storico completo:
> ```bash
> git log --oneline --all
> ```

---

## Template (copiare e compilare)

```markdown
## [YYYY-MM-DD HH:MM] <agente-uscente> → <agente-entrante>

**Task:** <una riga: cosa doveva essere fatto>
**Piano:** `.ai/plans/<file>.md` (oppure "—" se il task era semplice)
**Commit:** `<sha breve>`

### Fatto
- `percorso/file.cs:123` — cosa è cambiato e perché
- `percorso/altro.cs` — ...

### Come verificare
```bash
<comando esatto di build>
<comando esatto di test>
```
Atteso: <cosa deve succedere se è andato tutto bene>

### Stato
- ✅ Compila / ❌ Non compila / ⚠️ Compila con warning
- ✅ Test passano / ❌ Test falliscono / ⏭️ Non eseguiti (motivo)

### Per chi entra
**Prossimo passo:** <azione concreta>
**NON toccare:** <file/aree fuori scope>
**Attenzione a:** <insidie, assunzioni, cose lasciate a metà>
```

---

## [2026-08-18 22:15] claude → tutti

**Task:** fix del logging strategico emerso dal replay `20260818_213214`
**Piano:** —
**Commit:** `37bd42a` (lock) · `<fix>` · `<release>`

### Causa reale, più profonda dell'ipotesi iniziale

L'ipotesi era "un errore su un file precedente impedisce la scrittura degli header strategy".
Il replay dice di più: **`SimRIG_DebugLog_*.csv` non esiste affatto** e il MergeGap non ha il banner.
Quindi non è fallito "un file precedente": è fallita la **primissima** `File.WriteAllText` dentro
l'unico `try` globale, e le tre scritture successive non sono mai state eseguite. Quale eccezione
fosse, non lo sapremo mai: il `catch { }` l'ha ingoiata. È il difetto principale, più della causa.

### Fatto

**A. Payload dell'evento** — `LogManager.cs:346` accodava solo `message`, scartando `data`.
Ora usa `FormatStrategyEventLine(...)`, con timestamp, session time e lap.
Il ramo `STRATEGY_SNAPSHOT` **resta volutamente `message`-only**: lì il messaggio è già la riga CSV
completa, e concatenare `data` sfonderebbe le 62 colonne. Aggiunta una guardia che segnala il caso
come errore del chiamante invece di corrompere la riga in silenzio.

**B. Header** — `WriteHeaders()` isola ogni file nel proprio `try`. `TryWriteHeader()` è idempotente
per costruzione: scrive **solo se il file è assente o vuoto**, quindi non può né duplicare un header
né troncare dati già accodati. `RetryStrategyHeaders()` ritenta a ogni ciclo del writer task e una
volta in `Shutdown()`: un fallimento transitorio all'avvio non condanna più l'intera sessione.

**Diagnostica** — zero `catch { }` residui in `LogManager.cs`. `ReportLogFailure()` scrive su
`SimHub.Logging.Current.Error` (prima occorrenza, poi una ogni 100, per non inondare il log) e
non solleva mai. `LastLogFailure` espone l'ultimo errore ai test.

**Falso positivo trovato dai test** — la cancellazione del writer task in `Shutdown()` veniva
riportata come errore a ogni chiusura normale. Corretto in `LogManager.cs:340` e `:406`.
Coperto da `Test_CleanLifecycle_ReportsNoFailure`.

**Robustezza CSV** — `TargetStrategyManager.Csv()`: un pilota con la virgola nel nome
("Rossi, Mario") avrebbe sfondato le 62 colonne. Il replay ha 23 righe con "José Barahona",
salvo per fortuna.

### Come verificare

```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
```
```bash
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```

Atteso: exit code `0`, **26 `[PASS]`** totali, di cui 9 nel blocco `Strategy Logging Tests`.
Nessuna riga `log4net:ERROR` nell'output: se ricompare, una diagnostica sta scattando a torto.

### Stato
- ✅ Compila — 0 errori (resta il warning preesistente `CS0219` in `ReplayBacktestIntegrationTest.cs:19`)
- ✅ 26 test passano, exit code `0`
- ✅ Formato dei file verificato su output reale, non solo sui test

### Per chi entra

**Prossimo passo:** un replay reale. Poi confrontare i nuovi file con
`Logs/SimRIG_Strategy*_20260818_213214.*`, che restano come baseline "rotta".

**NON toccare:** i sei punti congelati in `PROJECT_STATE.md`.

**Attenzione a:** l'event log ora pesa di più (payload completo su ogni riga: ~2 KB/riga contro
~30 byte). Nel replay c'erano 1369 eventi → l'ordine di grandezza passa da ~27 KB a ~200 KB per
sessione. Accettabile, ma va tenuto d'occhio su gare lunghe.

---

## [2026-08-18 21:30] claude → codex (seconda review mirata)

**Task:** fix RED-1 + osservabilità + snapshot/header, dai punti 1-3 del piano concordato
**Piano:** review completa in `.ai/reviews/2026-08-18-strategy-engine-verification.md`
**Commit:** `0013c11` (lock) · `1e296cf` (fix)

### Fatto

**1. RED-1 — nessun DeltaGap attraversa più il pit**
- `User.PluginSdkDemoEdit/RelativePaceTracker.cs` (**nuovo**, 190 righe) — macchina di stato del
  RelativePace estratta da `TargetStrategyManager`, senza dipendenze SimHub. Il flag
  `_pitContaminatedSeed` viene alzato appena il gate pit scatta e **sopravvive all'uscita dai box**:
  il primo campione pulito post-pit diventa sempre un seed, mai un rate.
- `TargetStrategyManager.cs:393-432` — il blocco di calcolo ora delega al tracker e si limita a loggare.
- `TargetStrategyManager.cs:1173`, `:1253`, `:1307` — `SetPlayerAsTarget`, `SetNoTarget` e
  `ResetSession` ora resettano il tracker: l'asimmetria segnalata nella review è chiusa.

**2. Osservabilità**
- Nuovo evento `RELATIVE_PACE_POST_PIT_SEED`.
- `DuplicateSector`, `MissingSector`, `TargetChanged` ora vengono effettivamente assegnate
  (`RelativePaceTracker.ClassifyInvalidation`). `MissingSector` = salto in avanti ≤ 10 settori,
  `InvalidSequence` = salto all'indietro.
- `RELATIVE_PACE_INVALIDATION reason=TargetChanged` emesso anche su `STRATEGY_EVENT`
  (`TargetStrategyManager.cs:287`), prima era solo su `STRATEGY`.
- `RELATIVE_PACE_UPDATE` ora include anche `prevGap` e `wasClamped`.

**3. Snapshot e header**
- `LogManager.SnapshotHeader` è ora una costante pubblica, **62 colonne**. `DeltaGap`, `DeltaTime`,
  `InstantPace` non sono più vuote; aggiunte `PrevGap`, `SeqValid`, `InvalidReason`,
  `PitSeedPending`, `PostPitSeed`, `PositiveGap`, `WarmupW0-2`, `WarmupFallback`, `MaxStayLaps`,
  `PlayerTrackPace`.
- `string.Format` a 47 placeholder → array `snapFields` + `string.Join`, con **guardia runtime**
  (`TargetStrategyManager.cs:826`) che logga `SNAPSHOT_COLUMN_MISMATCH` se le larghezze divergono.
- Header parametri: aggiunti `Beta` e `MinimumDeltaTime`, e ora sono **letti dalle costanti reali**
  di `RelativePaceTracker` invece che ricopiati a mano. `RecentPitThreshold` e `MinimumRaceLaps`
  rinominati come da spec §34. Versione motore → `1.1.0`.

**Extra (deduplicazione, nessun cambio di comportamento)**
- `TargetStrategyManager.IsPlayerInPitLane()` — l'euristica era triplicata a `:398`, `:821`, `:1206`.
  Ora è una sola. **L'euristica in sé non è stata toccata**: resta il debito Y-9.

### Come verificare

```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
```
```bash
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```

Atteso: build 0 errori (resta un warning preesistente `CS0219` in
`ReplayBacktestIntegrationTest.cs:19`), exit code `0`, e nel blocco
`RelativePace State Machine Tests` **9 righe `[PASS]`**, fra cui
`Test8_PlayerPit_NoDeltaAcrossPit` e `Test9_TargetPit_NoDeltaAcrossPit`.

### Stato
- ✅ Compila — 0 errori, solution completa
- ✅ Test passano — 17 `[PASS]` totali, exit code `0`
- ✅ **Regressione verificata**: neutralizzando il branch del seed post-pit, il Test 8 fallisce con
  `[TEST FAILED] REGRESSIONE RED-1: nessun rate deve attraversare il pit` ed exit code `1`.
  Il test protegge davvero, non è tautologico.

### Per chi entra

**Prossimo passo:** seconda review mirata sul diff `1e296cf` e sui test. In particolare vale la pena
sfidare due scelte di progetto che ho fatto io e che non sono dettate dalla spec:
1. `MissingSector` vs `InvalidSequence` sono separate dalla soglia `forward <= 10`
   (`RelativePaceTracker.cs:180`). È una convenzione mia: la spec non dice come distinguerle.
2. Il seed post-pit **consuma un macrosettore**: dopo l'uscita dai box il primo rate arriva un
   settore più tardi rispetto a prima. È ciò che §9.2 richiede, ma va confermato che sia il
   comportamento voluto e non una perdita di reattività inaccettabile.

**NON toccare:** i sei punti congelati in `PROJECT_STATE.md` — `CanFinishWithoutPitting`,
`OvercutTrafficOK`, modello warmup, `LapsSinceLastPit` continuo, deadband HUD, euristica pit.
Richiedono una decisione esplicita prima di essere modificati.

**Attenzione a:** l'header dello snapshot è cambiato (50 → 62 colonne, con inserimenti **in mezzo**,
non solo in coda). Qualunque parser o dashboard che leggeva i CSV vecchi per indice di colonna va
aggiornato. I file di log precedenti non sono confrontabili con i nuovi.

---

## [2026-08-18 19:15] antigravity → tutti

**Task:** Risolvere debiti di configurazione progetto (.csproj reference e inclusione Tests in solution)
**Piano:** —
**Commit:** —

### Fatto
- `User.PluginSdkDemoEdit/User.PluginSdkDemo.csproj:72,77,80` — sostituito path hardcoded `E:\SimHub\` con `$(SIMHUB_INSTALL_PATH)` per `Newtonsoft.Json`, `SharpDX`, `SharpDX.DirectInput`.
- `User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/User.PluginSdkDemo.Tests.csproj:37` — sostituito path hardcoded `E:\SimHub\` con `$(SIMHUB_INSTALL_PATH)` per `Newtonsoft.Json`.
- `User.PluginSdkDemoEdit/User.PluginSdkDemo.sln` — aggiunto progetto `User.PluginSdkDemo.Tests` alla solution. Ora la build di `User.PluginSdkDemo.sln` compila sia il plugin che la test suite.
- `CLAUDE.md` — aggiornate note sui test (ora inclusi nella build della solution).

### Come verificare
```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```
Atteso: build completata con 0 errori, esecuzione test con 100% PASS.

### Stato
- ✅ Compila (0 errori, solution include entrambi i progetti)
- ✅ Test passano (100% PASS)

### Per chi entra
**Prossimo passo:** Definire M1 (feature / logica su cui concentrarsi)
**NON toccare:** `*_LEGACY.cs` (file orfani non compilati)
**Attenzione a:** Post-build event fa XCOPY in `%SIMHUB_INSTALL_PATH%` — assicurarsi che SimHub sia chiuso prima di compilare.

---

## [2026-08-18] setup → tutti

**Task:** inizializzare Git e il protocollo di collaborazione multi-AI
**Piano:** —
**Commit:** `f526cb3`

### Fatto
- `.gitignore` — regole per C#/VS/SimHub: esclusi `bin/`, `obj/`, `.vs/`, `.vscode/`, `Logs/`,
  `*.user`, archivi (`*.rar`/`*.zip`), `scratch/` e `User.PluginSdkDemoBackup/`
- `.ai/PROJECT_STATE.md` — stato, lock, milestone, debiti noti rilevati durante l'ispezione
- `.ai/HANDOFF_LOG.md` — questo file
- `.ai/ARCHITECTURE.md` — struttura ADR + mappa dei moduli
- `.ai/plans/` — cartella per i piani di implementazione
- `CLAUDE.md` — istruzioni operative, comandi di build/test, regola del lock

### Come verificare
```bash
git log --stat -1
```
Atteso: un solo commit di setup; nessun `bin/`, `obj/` o `.vs/` tra i file tracciati.

### Stato
- ⏭️ Build non eseguita (nessuna modifica al codice sorgente in questo turno)

### Per chi entra
**Prossimo passo:** definire la milestone M1 in `PROJECT_STATE.md` e prendere il lock.
**NON toccare:** nulla — il lock è libero, ma va preso prima di scrivere codice.
**Attenzione a:** i 4 debiti noti elencati in `PROJECT_STATE.md`, in particolare i file
`*_LEGACY.cs` che sono sul disco ma **non** vengono compilati.
