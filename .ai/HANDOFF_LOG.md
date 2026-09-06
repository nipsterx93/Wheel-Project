# HANDOFF LOG

> Diario dei passaggi di consegne. **Append in cima** (il più recente per primo).
> Si tengono solo gli **ultimi 10** handoff.
>
> **Quando aggiungi una voce, togli l'undicesima** e spostala in `.ai/archive/HANDOFF_LOG_archive.md`
> (in cima, così l'archivio resta in ordine cronologico inverso come questo file). La regola era
> scritta ma non applicata: al 2026-09-05 il file conteneva **22 voci per 112 KB**, che ogni agente
> rileggeva a ogni ingresso di sessione.
>
> Storico più vecchio: `.ai/archive/HANDOFF_LOG_archive.md`, oppure
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

## [2026-09-06 09:10] claude → chiunque entri dopo (Andreas, Antigravity, Codex)

**Task:** Setup coworking Claude/Antigravity — hook di lock-enforcement, permessi progetto, tabella Ruoli senza divisione per compiti, protocollo di brainstorming in AGENTS.md. Nessun punto Y toccato: turno di infrastruttura, non di correzione.
**Piano:** discussione diretta con Andreas in chat (confronto con una proposta parallela di Antigravity, scartata sul punto della scrittura concorrente — vedi sotto).
**Commit:** `cbe66ef`, `e205d8b`, `87b9e46`

### Fatto
- `.ai/PROJECT_STATE.md`:
  - Tabella "Ruoli" riscritta: niente più compiti esclusivi per agente (era Antigravity=architettura, Claude=implementazione, Codex=review). Ogni agente fa tutto; principio guida "uno corregge l'altro" — decisione di Andreas, confermata da Antigravity.
- `.claude/hooks/check-lock.js` + `.claude/settings.json`:
  - Hook `PreToolUse` su `Edit|Write|MultiEdit` che legge il blocco `LOCK` in `PROJECT_STATE.md` e nega la scrittura in `User.PluginSdkDemoEdit/` se l'owner non è `NONE` né `claude`. Nega **sempre** scritture in `Hardware/` (Y-53), indipendentemente dal lock. Non tocca nient'altro (`.ai/`, root docs restano scrivibili senza lock, come già previsto da AGENTS.md per piani/handoff/review).
  - Verificato con pipe-test sintetico (4 casi: codice+lock libero→allow, Hardware→deny sempre, doc .ai/→allow, codice+lock altrui→deny) e poi con un trigger reale (sentinella temporanea rimossa a verifica avvenuta) per confermare che l'hook è effettivamente collegato, non solo scritto.
  - `permissions.allow`: `Bash(awk *)` e l'eseguibile esatto dei test, via skill `fewer-permission-prompts`. **Scartato deliberatamente** MSBuild dall'allowlist: builda e installa il plugin nel SimHub reale (side effect già documentato in AGENTS.md), non è "read-only" nel senso della skill.
- `AGENTS.md`: nuova sezione "Protocollo di brainstorming e coworking fra agenti" — niente ruoli esclusivi, lock seriale anche durante il brainstorming (una proposta di Antigravity per uno stato `owner: ALL` con scrittura concorrente su `.ai/plans/` è stata discussa e **scartata**: due processi che scrivono lo stesso file senza un commit in mezzo si sovrascrivono a livello di filesystem, prima che Git possa aiutare — la sicurezza del lock viene proprio dal seriale), esito del brainstorming sempre scritto in `.ai/plans/<data>-<argomento>.md`, niente inondazione di subagenti.

### Come verificare
```bash
node .claude/hooks/check-lock.js <<< '{"tool_name":"Edit","tool_input":{"file_path":"User.PluginSdkDemoEdit/PitRadar.cs"}}'
```
Atteso: con lock `owner: NONE`, `{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"allow"}}`.

### Stato
- ✅ Compila (nessun file di codice C# toccato in questo turno)
- ⏭️ Test non eseguiti (nessuna modifica alla logica del plugin)

### Per chi entra
**Prossimo passo (proposto, non deciso):** comandi custom `/new-session` e `/handoff` per automatizzare il bootstrap di una sessione nuova (ridurre la dipendenza da Andreas come "portavoce" fra chat), poi valutare l'installazione della skill Superpowers (obra/Jesse Vincent, `/plugin install superpowers@claude-plugins-official`) per il brainstorming strutturato — verificare dove scrive di default e se va redirezionato verso `.ai/plans/`. In coda, una skill di dominio motorsport (formule fuel/pit/proiezione) da costruire con l'esito di una deep search già preparata per Andreas.
**NON toccare:** nessuna area di codice interessata da questo turno.
**Attenzione a:** l'hook copre solo `User.PluginSdkDemoEdit/` e `Hardware/` — non impedisce scritture scorrette altrove; resta comunque disciplina per tutto il resto, come prima. Se Antigravity introduce un meccanismo equivalente per sé, va documentato in AGENTS.md invece di duplicare la logica qui.

---

## [2026-09-05 13:15] antigravity → chiunque entri dopo

**Task:** Y-52 Passo 2 di 4 — Seeding `DriverCarEstLapTime` e `CarClassEstLapTime` nei ripieghi di passo e introduzione flag `IsLapsPredictionValid`
**Piano:** —
**Commit:** questo

### Fatto
- `User.PluginSdkDemoEdit/RaceAnalyzer.cs`:
  - Aggiunto `public bool IsLapsPredictionValid { get; set; } = false;` in `RaceAnalysisResult`.
  - Introdotti helper puri `ResolvePlayerPace`, `ResolveLeaderPace`, `IsLapsPredictionValid`.
  - Sostituito il vecchio ripiego cablato `120.0s` con la cascata gerarchica: baseline normalizzata > best lap registrato in sessione > `DriverCarEstLapTime` (prior pilota) > `CarClassEstLapTime` (prior classe) > fisica del tracciato (`trackLength / 50.0`) > `120.0s`.
  - In `ComputeFlagMoment`, seminato il passo degli avversari non ancora cronometrati da `Metadata.EstimatedPaceFor`, garantendo fin dal via l'identificazione corretta della vettura al comando.
  - Connesso `Results.IsLapsPredictionValid` allo stato di gara e al ciclo di vita della sessione.
- `User.PluginSdkDemoEdit/TargetStrategyManager.cs`:
  - `refLapTime` ripiega su `state.Metadata.PlayerEstimatedPaceSec` e `state.Metadata.EstimatedPaceFor` prima di `trackLength / 50.0`.
- `User.PluginSdkDemoEdit/DataPluginDemo.cs`:
  - Registrata e pubblicata la proprietà SimHub `SimRIG.Session.IsLapsPredictionValid`.
  - Leaderboard laterale (`lapPaceSec`) ripiega sui metadati stimati prima di `trackLen / 45.0`.
- `User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/UnitTests/PredictedPaceUnitTests.cs`:
  - 15 unit test che verificano gerarchia fonti (ADR-005), risoluzione leader, transizioni flag di validità e regressione Road Atlanta GT3 (36 giri proiettati al semaforo verde, risolvendo il buco nero dei 23 giri causato dal fallback a 120s).

### Come verificare
```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```
Atteso: exit code `0`, tutti i 311 test passano (100%).

### Stato
- ✅ Compila — 0 errori
- ✅ Test passano (100% PASS, 311/311 unit test)

### Per chi entra
**Prossimo passo:** Y-52 Passo 3 di 4 — radar piazzola box metrica (`DriverPitTrkPct` per indicare la distanza in metri allo stallo assegnato).
**NON toccare:** `Hardware/` (territorio di Andreas).
**Attenzione a:** la semina è valida al semaforo verde (`SessionTimeLeft > 0`). In griglia con tempo `-1` `TimeUntilLeaderCheckered` restituisce 0 come da design.

---

## [2026-09-05 15:05] claude → Andreas, in particolare

**Task:** allineare i documenti al fatto che sul progetto lavorano **due persone**
**Piano:** —
**Commit:** questo (+ i 4 precedenti di oggi, ora pushati)

### Contesto per chi legge da Windows
Michael è entrato sul progetto oggi, da **macOS**. Tre turni consecutivi hanno riorganizzato `.ai/`
(potatura del contesto, `AGENTS.md` neutro): **nessun file di codice è stato toccato**, il plugin è
esattamente com'era a `bd00979`.

### Fatto
- `PROJECT_STATE.md` — **Y-53 riclassificato**: `PaddleClutch.h` non è un difetto da correggere, lo
  versiona Andreas. Resta scritto solo perché una review a freddo lo rialza come grave (è successo
  stamattina): se lo ritrovi, la risposta è "atteso".
- `ARCHITECTURE.md` — **`Hardware/` entra nella mappa dei moduli** (prima si fermava al plugin C#):
  cosa contengono i due sketch, e che è **fuori scope per gli agenti**. Annotato che la mappatura
  pulsante → azione non è documentata da nessuna parte, e che Y-14 ci dipende
  (`TyreManager.CurrentScope` è pilotato solo dai tasti volante).
- `PROJECT_STATE.md` — **Y-56 aperto, non corretto di proposito**: un lock non pushato non
  serializza nulla, e `human` non distingue quale dei due umani. Lo decidete voi due.

### Come verificare
```bash
git log --oneline eab83d8..HEAD     # i turni di oggi, tutti [claude] e tutti su .ai/ + *.md
git diff --stat eab83d8..HEAD -- User.PluginSdkDemoEdit/   # atteso: nessun output
```

### Stato
- ⏭️ Build e test **non eseguiti**: macchina macOS. La seconda riga qui sopra è la prova che non
  servivano — il codice non è stato toccato in nessuno dei tre turni.

### Per chi entra
**Prossimo passo:** Y-52 passo 2 di 4, invariato dal 12:30.
**NON toccare:** `Hardware/` (Andreas), e il protocollo del lock finché Y-56 non è deciso.
**Attenzione a:** le regole ora stanno in **`AGENTS.md`** alla radice. `CLAUDE.md` e `GEMINI.md`
sono puntatori: se ti trovi a modificarli, stai creando un duplicato (ADR-006).

---

## [2026-09-05 14:35] claude → chiunque entri dopo (Gemini/Antigravity compreso)

**Task:** regole di progetto in un file neutro, leggibile da qualunque agente
**Piano:** — (deciso con l'utente)
**Commit:** `8362a95` (lock) + questo

### Perché
Le regole vivevano in `CLAUDE.md`: Claude Code lo carica da solo, Gemini e Codex no. Il protocollo
arrivava automaticamente a **un agente su tre**, e si vede nei numeri — dei 22 handoff archiviati,
**21 firmati `claude`, uno `antigravity`**.

### Fatto
- `AGENTS.md` (nuovo, radice) — **la fonte unica delle regole.** Contenuto di `CLAUDE.md` reso
  neutro (`[<agente>]` invece di `[claude]`, "non è il tuo" invece di "non sono io"), più tre
  aggiunte: `CustomDialog.xaml.cs` fra le trappole (Y-55), l'avvertenza sul conteggio PASS da
  leggere dall'output e non ricopiare (Y-54), e una sezione **"Tenere leggeri i file di stato"**
  che rende esplicita la manutenzione dell'archivio.
- `CLAUDE.md` — ridotto a **puntatore**: rimando ad `AGENTS.md` + le due sole regole il cui costo,
  se ignorate, è una sovrascrittura silenziosa (leggi il lock; handoff e rilascio a fine turno).
- `GEMINI.md` (nuovo) — stesso puntatore, con prefisso commit `[antigravity]`.
- `.ai/ARCHITECTURE.md` — **ADR-006** che formalizza la decisione e scarta la duplicazione.
- `.ai/NEW_SESSION_PROMPT.md`, `.ai/STRATEGY_ENGINE_GUIDE.md` — l'ordine di lettura punta ora ad
  `AGENTS.md`, e il prompt spiega che i punti chiusi sono un indice + archivio.

I riferimenti a `CLAUDE.md` dentro documenti **datati** (review, handoff vecchi, piani) sono stati
lasciati intatti di proposito: dicevano il vero quando sono stati scritti.

### Come verificare
```bash
wc -l AGENTS.md CLAUDE.md GEMINI.md        # atteso: ~190 / ~20 / ~20
grep -rn "CLAUDE.md" .ai --include "*.md" | grep -v archive/ | grep -v reviews/ | grep -v plans/
```
Atteso: la seconda riga non trova più riferimenti "vivi" a `CLAUDE.md` come fonte delle regole.

### Stato
- ⏭️ Build e test **non eseguiti**: sessione macOS, e **nessun file di codice toccato**.
  Il codice resta esattamente a `bd00979`.

### Per chi entra
**Prossimo passo:** invariato — Y-52 passo 2 di 4.
**NON toccare:** nulla lasciato a metà.
**Attenzione a:** se cambi una regola, cambiala in **`AGENTS.md`**. `CLAUDE.md` e `GEMINI.md` non
contengono regole: se ti trovi a modificarle, quasi certamente stai creando il duplicato che
ADR-006 esiste per evitare.

---

## [2026-09-05 14:10] claude → chiunque entri dopo

**Task:** potatura del contesto — separare la storia chiusa dallo stato attivo
**Piano:** — (deciso con l'utente dopo la review a freddo di stamattina)
**Commit:** `7204135` (lock) + questo

### Perché
`.ai/` pesava **226 KB** letti a ogni ingresso di sessione, prima di aprire una riga di codice —
con `DataPluginDemo.cs` a 155 KB, un agente consumava buona parte della finestra in sola lettura.
Due cause misurate: `HANDOFF_LOG.md` dichiarava 10 voci e ne conteneva **22** (112 KB), e il **66%**
di `PROJECT_STATE.md` (47,6 KB su 72) era storia di punti già chiusi.

### Fatto
- `.ai/archive/HANDOFF_LOG_archive.md` (nuovo) — le 12 voci oltre le 10 tenute (24/08 → 01/09).
- `.ai/archive/CLOSED_POINTS.md` (nuovo) — i 40 punti chiusi col testo **integrale**.
- `.ai/PROJECT_STATE.md` — al posto delle voci chiuse un **indice** di 40 righe (ID · titolo ·
  esito · commit) e un riquadro che dice dove trovare il resto. **72 KB → 25 KB.**
- `.ai/HANDOFF_LOG.md` — testata che spiega *come* si pota (togli l'undicesima, spostala in
  archivio), perché la regola c'era ma nessuno la applicava. **112 KB → 48 KB.**
- `CLAUDE.md` — `.ai/archive/` nei riferimenti + sezione nuova **"Cosa si può concludere da quale
  macchina"**: il progetto è Windows-only, ma si lavora anche da macOS e la differenza va
  dichiarata invece che lasciata intendere.

**Niente è stato riassunto o cancellato: solo spostato.** Verificato con `diff` che il testo
spostato sia identico byte a byte (`Y-32`: 4066 byte, e la prima voce archiviata dell'handoff).

### Come verificare
```bash
wc -c CLAUDE.md .ai/PROJECT_STATE.md .ai/HANDOFF_LOG.md .ai/ARCHITECTURE.md
grep -c '^## \[20' .ai/HANDOFF_LOG.md          # atteso: 10
grep -c '^| ~~' .ai/archive/CLOSED_POINTS.md   # atteso: 40
git show 7204135:.ai/PROJECT_STATE.md | grep '^| ~~Y-32~~' | diff - <(grep '^| ~~Y-32~~' .ai/archive/CLOSED_POINTS.md)
```
Atteso: da **226 KB a ~95 KB** letti per ingresso, ultima riga senza output (identici).

### Stato
- ⏭️ Build e test **non eseguiti**: sessione macOS, e comunque **nessun file di codice toccato**
  (scope del lock: `.ai/**`). Il codice è esattamente com'era a `bd00979`.

### Per chi entra
**Prossimo passo:** invariato — Y-52 passo 2 di 4 (vedi handoff del 12:30). La potatura non tocca
il lavoro in corso.
**NON toccare:** nulla lasciato a metà.
**Attenzione a:** ora la regola dei 10 va **applicata**, non solo dichiarata: quando aggiungi la
tua voce, sposta l'undicesima in `.ai/archive/HANDOFF_LOG_archive.md`. Se torna a 22 voci fra un
mese, questo turno è stato inutile. Stessa cosa per i punti: quando ne chiudi uno, il testo lungo
va in `CLOSED_POINTS.md` e in `PROJECT_STATE.md` resta la riga d'indice.

---

## [2026-09-05 13:20] claude (review a freddo, senza lock) → chiunque entri dopo

**Task:** ingresso a freddo su richiesta dell'utente — analizzare progetto, codice e documenti,
aggiornare la documentazione sullo stato attuale. **Nessun file di codice toccato.**
**Piano:** — (review, non implementazione)
**Commit:** — (non committato al momento della scrittura)

### Fatto
- `.ai/reviews/2026-09-05-inventario-stato-progetto.md` (nuovo) — inventario misurato del repo e
  cinque discrepanze (R-1…R-5), con il metodo di misura accanto a ogni numero.
- `.ai/PROJECT_STATE.md` — registrati **Y-53** (`PaddleClutch.h` mancante), **Y-54** (backtest che
  si auto-salta + path assoluto), **Y-55** (`CustomDialog.xaml.cs` non compilato né documentato).
  Risincronizzati due numeri fermi al 24 agosto nel cappello "Da dove partire": `30/26` → `51/39`,
  `186 test PASS` → `295`.
- `.ai/ARCHITECTURE.md` — nota di aggiornamento su ADR-003: la conseguenza "il progetto di test non
  è nella solution" non è più vera. ADR non modificato, solo annotato.

### Come verificare
```bash
find Hardware -type f                      # Y-53: manca PaddleClutch.h
grep -n 'replayPath' User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/IntegrationTests/MisanoHuracanGT3ReplayTest.cs   # Y-54
grep -c CustomDialog User.PluginSdkDemoEdit/User.PluginSdkDemo.csproj   # Y-55: attesa 0
```

### Stato
- ⏭️ Build **non eseguita** e ⏭️ test **non eseguiti**: sessione su **macOS**, niente MSBuild.
  Nessuna conclusione di questo turno dipende da una build.
- ⏭️ `Logs/` non accessibile: **nessun numero dei replay è stato verificato né contestato.**

### Per chi entra
**Prossimo passo:** Y-53 è il più urgente ma è bloccato su una domanda all'utente (il file esiste in
locale? è suo o di terzi?). Y-54 e Y-55 sono correggibili subito, prendendo il lock.
**NON toccare:** niente è stato lasciato a metà. Y-52 resta al passo 1 di 4, come l'ha lasciato
l'handoff precedente.
**Attenzione a:** Y-54 significa che "295 PASS" su una macchina senza i replay non è lo stesso
"295 PASS" della macchina dell'utente. Finché non è corretto, il conteggio va letto sapendolo.

---

## [2026-09-05 12:30] claude → chiunque entri dopo

**Task:** Y-52 passo 1 di 4 — `SessionMetadata`, `SessionYamlParser`, cache di sessione, dump
**Piano:** — (quattro passi concordati con l'utente dopo l'analisi di irdashies)
**Commit:** `bd00979`

### Fatto
- `SessionMetadata.cs` (nuovo) — contenitore **agnostico**, campi nullable. È l'interfaccia che
  i consumatori leggeranno; il parser YAML è solo uno dei fornitori possibili.
- `SessionYamlParser.cs` (nuovo) — fornitore iRacing, **funzione pura**: stringa in, contenitore
  fuori. Nessun I/O, nessuno stato.
- `SessionMetadataDump.cs` (nuovo) — `SimRigMetadata.json` + YAML grezzo, cartella configurabile.
- `TelemetryReader.cs` — `RefreshSessionMetadata`: interpreta **solo quando lo YAML cambia**.
- `OpponentTracker.cs` — il BoP arriva dal contenitore; `ParseOpponentMaxFuelPct` **eliminato**
  (61 righe) e con lui la riscansione a 60 Hz.
- `DataPluginDemoSettings.cs` — `MetadataDumpFolder`, vuota = accanto alla DLL.
- Tre file nuovi nel `.csproj` principale, uno in quello dei test + `TestRunner`.

### Come verificare
```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/User.PluginSdkDemo.Tests.csproj" -p:Configuration=Debug -v:minimal -nologo
```
```bash
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```
Atteso: exit `0`, **295 PASS** (erano 289).

**Regressione neutralizzata (ADR-004):** togliendo il troncamento dell'unità di misura in
`SessionYamlParser.Number`, il test sul quirk del `'%'` di iRacing diventa rosso.

### Criterio osservabile sul replay — serve all'utente
Il passo 1 **non cambia comportamento**, quindi il replay non deve mostrare numeri diversi.
Deve mostrare due cose:

1. Una riga `Session Metadata Dumped` nel log **una volta sola per sessione**. Se comparisse a
   ripetizione, la cache non starebbe funzionando — ed è metà del motivo di questo passo.
2. Il file `SimRigMetadata.json` con dentro passo stimato, limite box, piazzola, incidenti; e
   accanto un `SimRigSessionYaml_*.txt` con lo YAML grezzo.
3. Il BoP deve continuare a comportarsi **identico**: nei log devono restare righe tipo
   `BoP Pct: 0.500`. Se sparissero o cambiassero valore, il refactor avrebbe rotto qualcosa.

### Attenzione per chi entra
- ⚠️ **I test usano uno YAML scritto a mano.** Verificano la struttura (annidamento, unità,
  quirk del `'%'`, risoluzione del Player per `CarIdx`) e la **preservazione del BoP**. Non
  verificano che i nomi dei campi siano quelli che iRacing manda davvero: per quello serve il
  dump che questo stesso passo produce. È il primo lavoro del prossimo turno, appena l'utente
  gira un replay.
- ⚠️ **La chiave di classe è incerta.** Lo YAML ha `CarClassShortName`, SimHub espone
  `data.NewData.CarClass`: potrebbero non combaciare. Per questo il passo è indicizzato **anche
  per nome pilota**, che è la chiave certa, e `EstimatedPaceFor` prova prima il pilota. Da
  confermare sul dump reale prima di appoggiarci il passo 2.
- ⚠️ **Il file di dump non va mai riletto a runtime.** È scritto nel commento della classe e
  vale la pena ripeterlo: rileggerlo farebbe vincere una copia vecchia su una realtà fresca, che
  è il difetto di Y-20, Y-21, Y-22 e Y-50.
- La cache è verificata **per costruzione e per lettura, non da un test**: sta dentro
  `TelemetryReader`, che richiede un `PluginManager` di SimHub. Il criterio osservabile è il
  punto 1 qui sopra.

### Prossimo passo
Passo 2: `CarClassEstLapTime` come seme al posto di `100.0` e `trackLength/50`
(`TargetStrategyManager.cs:535-539`), **con `IsLapsPredictionValid` propagato** — perché su
questo percorso lo zero non è silenzio, è "gara finita adesso" (`RaceAnalyzer.cs:1064`).
Rinominare anche `IsPredictionValid` in `IsFuelPredictionValid`, come concordato.
Nella dash il `N/A` vive nelle proprietà `*Str` (ce ne sono già 77), non nelle numeriche: il
flag governa l'**uso interno**, la stringa governa l'occhio.

---

## [2026-09-05 09:40] claude → chiunque entri dopo

**Task:** Y-34 — `FuelToAdd` all'intero, sempre per eccesso, in tutte le modalità
**Piano:** —
**Commit:** `0355676`

### Fatto
- `FuelManager.cs` — estratte due funzioni pure: `MarginForMode(mode, consumption)` e
  `RoundFuelToAdd(rawLitres, maxFuelCapacity)`.
- `NormalMarginLitres = 0.6` sostituisce `consumption * 0.3`.
- L'arrotondamento avviene **in fondo a tutti i percorsi**, Sync/anti-zavorra e Manual comprese.
- `FuelRoundingUnitTests.cs` — file nuovo, registrato **sia** nel `.csproj` **sia** in
  `TestRunner.cs`.

### Come verificare
```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/User.PluginSdkDemo.Tests.csproj" -p:Configuration=Debug -v:minimal -nologo
```
```bash
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```
Atteso: exit `0`, **289 PASS** (erano 283).

**Regressione neutralizzata (ADR-004):** rimettendo `Math.Round(..., 1)` in `RoundFuelToAdd` il
test del caso reale fallisce con `da' 30,5` — il numero che il plugin mandava davvero.

### Lo schema, e perché è cambiato
⚠️ **Diverso da quello approvato il 2026-08-29.** Non più AGGR per difetto / NORM al più
vicino / SAFE per eccesso, ma **per eccesso ovunque**, per decisione dell'utente.

| modalità | margine | note |
|---|---|---|
| AGGR | nessuno | il cuscinetto glielo dà il Ceiling, fra 0 e 1 L |
| NORM | `0.6 L` fissi | prima `consumo * 0.3`, cioè proporzionale |
| SAFE | un giro intero | invariato, era già `= consumption` |

Caso reale che lo chiude (`Correzioni Post Test Pirpi/Test.txt`, Road Atlanta): il software di
riferimento chiedeva **31 L**, noi ne mandavamo **30.5**. Col Ceiling coincidono.

### Attenzione per chi entra
- ⚠️ **NORM cambia comportamento in due versi opposti.** Da proporzionale a fisso: su una
  vettura da 4 L/giro il margine scende da 1.2 a 0.6 L, su una da 1.5 L/giro sale da 0.45 a 0.6.
  A Road Atlanta (2.25 L/giro misurati) i due quasi coincidono, quindi un replay lì **non**
  mostrerebbe la differenza. Serve una vettura con consumo molto diverso per vederla.
- ⚠️ **`FuelStep` parte da `0.1` L** e `UserFuelOffset` entra in *tutte* le modalità, non solo
  Manual. Con l'intero, un colpo di encoder da 0.1 spesso non muove più il risultato: il default
  andrebbe forse portato a `1.0`. È una **decisione di prodotto** e non è stata presa qui.
- ⚠️ **La rete di sicurezza su `MaxAchievableFuelSaving` non serve più** per l'arrotondamento.
  `ComputeFuelSaving` resta per `IsFuelSavingAchievable`, e il suo `0.15` resta un valore **scritto
  nel codice, non misurato su un replay**.
- La macro **non** è stata toccata: `string.Format("{0}", val)` su un double intero rende `31`,
  non `31.0`. Se un domani si passasse a un formato esplicito tipo `F1`, il fix di Y-34 si
  perderebbe silenziosamente.
- Il tetto del serbatoio si applica **dopo** l'arrotondamento e sul suo **intero inferiore**:
  `MaxFuel` è frazionario e limitare a 63.7 dopo aver arrotondato a 64 rimetterebbe un decimale.

### Stato dei lavori nati dall'analisi irdashies
Fatti: Y-50 (valvola di sfogo dell'IQR), Y-13 (piega del gap), Y-51 (timestamp a 400), Y-34.
Rimasti sul tavolo, mai iniziati: distanza metrica dalla piazzola box, e punto cieco sui LED della
corona (il canale d'uscita `_serialLeds` esiste già, manca solo leggere `CarLeftRight`).
**Escluso per decisione dell'utente:** il broadcast nativo iRacing — si resta sulle macro.

---

## [2026-09-04 10:30] claude → chiunque entri dopo

**Task:** Y-13 (il gap saltava di un giro al rollover) + Y-51 (risoluzione dei timestamp 100 → 400)
**Piano:** — (decisi dopo un confronto **misurato** con irdashies, non per impressione)
**Commit:** `96915ef`

### Fatto
- `TargetStrategyManager.cs:306` — aggiunta `WrapLapDifference`, che **riusa**
  `PhysicalGapSeconds` con periodo un giro. La primitiva esisteva già (aggiunta per Y-2):
  mancava solo di applicarla al gap.
- `TargetStrategyManager.cs:494` e `:1087` — `posDiff` ripiegato entro mezzo giro.
- **Non toccati di proposito:** `:798` (passa già da `PhysicalGapSeconds`) e `:1296`
  (**scelta del bersaglio**: minimizza `|posDiff|` in spazio assoluto, ripiegare lì farebbe
  scambiare un doppiato a un giro esatto per la vettura più vicina).
- `OpponentTracker.cs` — `TimestampBucketCount = 400` e `TimestampBucketOf` con clamp.
  Il tracciamento dei timestamp è stato **separato** da quello dei microsettori di velocità.
- `GapWrapAndResolutionUnitTests.cs` — file nuovo, registrato **sia** nel `.csproj` **sia** in
  `TestRunner.cs` (la trappola di CLAUDE.md).

### Come verificare
```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/User.PluginSdkDemo.Tests.csproj" -p:Configuration=Debug -v:minimal -nologo
```
```bash
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```
Atteso: exit `0`, **283 PASS** (erano 277).

**Regressioni neutralizzate (ADR-004), una per fix:**
- `WrapLapDifference` resa un passante (`return posDiffLaps;`) → il test Y-13 fallisce con
  `vale 93,033 s`, cioè **il numero reale del replay `20260819_221922`**, non uno inventato.
- `TimestampBucketCount` riportato a `100` → il test sulla risoluzione fallisce.

### Numeri misurati (profilo di velocità con 12 curve, Misano, giro 91.3 s)
Errore massimo nel ricostruire il tempo-alla-posizione:

| schema | max | rms |
|---|---|---|
| 100 campioni, lineare (prima) | 140.8 ms | 33.5 ms |
| 100 campioni, Hermite | 76.4 ms | 12.5 ms |
| **400 campioni, lineare (ora)** | **8.9 ms** | 1.9 ms |
| 423 campioni, Hermite (irdashies) | 0.9 ms | 0.1 ms |

Il vantaggio di irdashies **non era la spline**: campionano ogni 10 m, noi ogni 42. A parità di
risoluzione la lineare recupera il 94% del divario. Le bande di isteresi (Y-12) valgono
150-250 ms: si passava da *sul bordo* a **17 volte dentro**.

Il loro modello **non** è stato adottato: converte una distanza in tempo col passo di un giro
*veloce*, quindi sottostima il gap e l'errore **cresce col gap** — al +3.3% misurato nei nostri
log sono 96 ms su 3 s e 319 ms su 10 s. Il nostro misura il tempo vero.

### Attenzione per chi entra
- ⚠️ **Y-13 era intermittente** (race fra il campione macrosettoriale e il rollover, finestra di
  pochi millisecondi). L'assenza di `GapJump` in un singolo replay **non** è la prova che sia
  chiuso: serve confrontare `20260819_221922` (dove si vedeva) con `20260819_230109` (dove no).
- ⚠️ `ZoneDrop` **non è stato toccato**: i microsettori di velocità restano a 100 perché l'indice
  era condiviso e cambiarlo avrebbe spostato la classificazione Low/Mid/High del degrado gomme.
  Se qualcuno in futuro alza *quella* risoluzione, va rivalidato `ZoneDrop`, non solo il gap.
- ⚠️ La guardia `|t2 - t1| < 10.0` sui timestamp è rimasta a 10 s. Con 400 bucket il delta normale
  fra bucket adiacenti è ~0.23 s invece di ~0.91 s, quindi la guardia è ora più permissiva in
  proporzione. Continua a fare il suo mestiere (isolare il salto da un giro intero), ma se si
  volesse stringerla serve un numero **misurato**, non scelto a occhio.

### Prossimo passo concordato con l'utente
`FuelToAdd` arrotondato all'intero (Y-34), **senza** broadcast nativo — si resta sulle macro.
Schema deciso: **AGGR** zero margine + `Ceiling`; **NORM** 0.6 L fissi (oggi è `consumption * 0.3`,
cioè proporzionale: su una vettura da 4 L/giro il fisso dà **meno** margine, su una parca **più**);
**SAFE** invariato, già `= consumption`, cioè un giro esatto. Con `Ceiling` ovunque la rete di
sicurezza su `MaxAchievableFuelSaving` prevista dallo schema Y-34 originale diventa superflua:
non si può più arrotondare in difetto.

---

## [2026-09-03 21:15] claude → chiunque entri dopo

**Task:** Y-50 — il filtro IQR del carburante si chiudeva sui rifiuti e non si riapriva più
**Piano:** — (difetto trovato in review di `c0be69d`, non un lavoro pianificato)
**Commit:** `3ad938f`

### Fatto
- `FuelManager.cs:182-215` — `_fuelHistory` (soli accettati, mai accorciata sui rifiuti) sostituita
  da `_recentLaps`, **finestra cronologica** di 10 campioni `{FuelUsed, Accepted}`. È l'accumularsi
  dei rifiuti che espelle i vecchi accettati e fa scendere i validi sotto tre, riaprendo il filtro.
  `FuelHistory` resta esposta come i **soli accettati**: i test preesistenti non cambiano semantica.
- `FuelManager.cs:100` — `ValidateFuelConsumptionIQR` **non toccata**. Riceveva già la lista
  filtrata: il difetto era nel **chiamante**. Un test sulla sola aritmetica restava verde, come in
  Y-31 e Y-48.
- `FuelManager.cs:~218` — `LastLapFuelUsed` è una **misura**, non una statistica: torna ad
  aggiornarsi sui giri con gialla e sugli out-lap, resta esclusa sul solo **in-lap** (rifornimento
  parziale → il delta di serbatoio sottostima il consumo). Alimenta `FUEL_TARGET_ALERT`
  (`DataPluginDemo.cs:1152`), che altrimenti giudicava il pilota su un giro vecchio di due.
- `FuelManager.cs:1` — ripristinato il BOM UTF-8 rimosso da `c0be69d`. Il file ha commenti accentati.
- `FuelOutlierFilterUnitTests.cs` — due test nuovi (il file era già nel `<Compile>`, nessuna
  modifica al `.csproj`).

### Come verificare
```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/User.PluginSdkDemo.Tests.csproj" -p:Configuration=Debug -v:minimal -nologo
```
```bash
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```
Atteso: exit `0`, **277 PASS** (erano 275). Fra questi:
`[PASS] Y-50: un cambio reale di consumo oltre il 15% riapre il filtro invece di bloccarlo`

**Regressione neutralizzata (ADR-004):** aggiungendo `if (!accepted) return;` in cima a
`FuelManager.RecordLap` si riproduce il comportamento pre-fix; il test Y-50 diventa rosso con
`media 2,509` invece di `3,10`. Verificato, non dedotto.

### Numeri misurati
Storico di 10 giri a ~2.51 L, poi 15 giri al nuovo consumo reale, prima del fix:

| consumo reale | accettati (noi) | media finale | accettati (irdashies) |
|---|---|---|---|
| 2.10 L | 0/15 | 2.51 | 7/15 → 2.10 |
| 3.10 L | 0/15 | 2.51 | 7/15 → 3.10 |
| 3.50 L | 0/15 | 2.51 | 7/15 → 3.50 |

La riga pericolosa è l'ultima: `FuelToAdd` su 2.51 mentre si consuma 3.50, fino alla bandiera.

### Attenzione per chi entra
- ⚠️ **Latenza di rientro: 8 giri** (10 slot, soglia a 3 validi). È il comportamento di
  irdashies, riprodotto di proposito. Stringerlo richiede un numero **misurato su un replay**,
  non scelto a occhio (ADR-005). Da valutare un'**asimmetria**: rifiutare un consumo in *salita*
  è pericoloso, in *discesa* no.
- ⚠️ `Flag_Yellow` ora scarta l'**intero giro** se compare in un solo tick. La semantica è
  coerente con l'uso esistente (`OpponentTracker.cs:954` lo tratta come stato di gara globale),
  ma la conseguenza è nuova e più severa. In una gara molto neutralizzata lo storico pulito può
  restare affamato. **Da osservare sul primo replay con safety car**, non c'è ancora una misura.
- Il conteggio "186 test" fermo in `PROJECT_STATE.md` è **stantio**: il valore reale delle righe
  `[PASS]` era 275 prima di questo turno, 277 dopo.

### Contesto
Il turno nasce da un'analisi comparativa con `irdashies` (progetto MIT, iRacing-only, Electron/TS,
in `Solo per analisi logiche/`). Il filtro IQR è stato portato da `antigravity` in `c0be69d`; questa
è la correzione del pezzo che era rimasto fuori dal port. Restano da valutare, dalla stessa analisi:
il ripiegamento di `posDiff` entro ±0.5 giro per Y-13, il broadcast nativo iRacing per Y-34,
e la distanza metrica dalla piazzola box.

---

## Handoff più vecchi

Tutte le voci precedenti a quelle qui sopra sono in `.ai/archive/HANDOFF_LOG_archive.md`,
in ordine cronologico inverso come questo file. La prima potatura è del 2026-09-05: il file
dichiarava di tenere gli ultimi 10 e ne conteneva 22, per 112 KB letti a ogni ingresso.

*(Niente conteggi scritti qui: `grep -c '^## \[20' .ai/archive/HANDOFF_LOG_archive.md` dà il
numero esatto senza che nessuno debba ricordarsi di aggiornarlo.)*
