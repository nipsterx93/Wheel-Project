# HANDOFF LOG — archivio

> Handoff dal **2026-08-24 → 2026-09-01** (12 voci), tolti da `.ai/HANDOFF_LOG.md`
> il 2026-09-05 per riportarlo agli ultimi 10 che dichiara di tenere.
>
> **Niente è andato perso:** le voci sono qui verbatim, e la storia completa
> resta comunque in `git log`. Si legge questo file solo quando serve
> ricostruire un turno vecchio, non a ogni ingresso di sessione.

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

## [2026-09-03 22:45] antigravity -> claude / utente

**Task:** Passo 1: Filtro IQR Carburante, latch giro verde/pit lane e buffer a 10 giri (allineamento irdashies).
**Piano:** `implementation_plan.md` e discussione in chat.
**Commit:** `c0be69d`

### Fatto
- `User.PluginSdkDemoEdit/FuelManager.cs:90-130`:
  - Aggiunta costante `MAX_CLEAN_HISTORY_LAPS = 10` (estensione da 4 a 10 giri puliti).
  - Implementazione del metodo puro `ValidateFuelConsumptionIQR(fuelUsed, cleanHistory)` con indicizzazione esatta di irdashies `(int)Math.Floor(count * 0.25)` e `(int)Math.Floor(count * 0.75)`, fattore motorsport `factor = 2.0` e tolleranza `mean * 0.15`.
- `User.PluginSdkDemoEdit/FuelManager.cs:180-275`:
  - Aggiunti accumulatori di stato durante il giro: `_wasInPitLaneDuringLap`, `_wasPreviousLapPit`, `_isLapFullyGreen`.
  - Aggiornamento in `Update()`: esclusione da `_fuelHistory` di giri out-lap, in-lap e con bandiera gialla durante qualsiasi tick del giro.
  - Reset esplicito e pulito di `_wasInPitLaneDuringLap = false;` e `_isLapFullyGreen = true;` al cambio giro per evitare race condition di timing al traguardo (fix proposto da Claude su bug tipo Y-18/Y-23).
  - Integrazione filtro IQR al passaggio sul traguardo prima dell'inserimento in `_fuelHistory`.
  - `ResetSession()` aggiornato con reset di tutti gli accumulatori.
- `User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/UnitTests/FuelOutlierFilterUnitTests.cs`:
  - Nuova suite di 7 test unitari dedicati che copre: baseline <3 campioni, reiezione outlier IQR (caution a 1.4L e picchi a 3.5L), tolleranza 15% su pilota costante, indicizzazione esatta Math.Floor, latch di stato (in-lap, out-lap, gialla), capacità massima 10 giri, e reset sessione.
- `User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/TestRunner.cs` e `.csproj`:
  - Registrazione della nuova suite. Tutti i 193 test (186 preesistenti + 7 nuovi) passano con successo (100%).

### Come verificare
```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/User.PluginSdkDemo.Tests.csproj" -p:Configuration=Debug -v:minimal -nologo
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```
Atteso: 0 errori di build, `[TEST SUCCESS] All Fuel Outlier Filter Tests Passed!`, `ALL UNIT TESTS PASSED SUCCESSFULLY! (100%)`.

### Stato
- ✅ Compila senza errori
- ✅ 193 test passano (100%)

### Per chi entra
**Prossimo passo:** Passo 2: Invio carburante nativo iRacing via broadcast Win32 (`irsdk_BroadcastPitCommand`) per risolvere definitivamente Y-34.
**NON toccare:** `FuelManager.Calculations.AverageFuelPerLap` resta l'API consumata all'esterno (nessuna breaking change).
**Attenzione a:** I flag di latch in `FuelManager.cs` mantengono il reset separato dopo la valutazione del giro per evitare corruzione al traguardo.

---

## [2026-09-03 18:00] claude -> utente

**Task:** inventario di passo e perdita ai box (nessun codice), poi **Y-49** — l'ancora del passo che
non riusciva mai a correggersi.
**Commit:** `f033a78` (inventario) · `bc03c45` (lock) · questo
**Documento:** `.ai/plans/2026-09-03-inventario-passo-e-sosta.md`

### Perche' un inventario

Lo stesso schema — *un calcolo corretto che esiste gia', collegato al posto sbagliato* — si era
ripetuto cinque volte in una settimana. L'utente ha chiesto una mappa prima di continuare a
correggere. Ne sono usciti **sette scollegamenti** non ancora noti, oltre ai quattro gia' chiusi.

I due che contano di piu':

- **`EstimatedCurrentPace`** (baseline + degrado + peso carburante + temperatura) e' calcolato,
  esposto come proprieta' SimHub e loggato a ogni giro. La proiezione usa la baseline nuda: **77.24
  contro 76.524**, cioe' **0.94%**, cioe' **0.33 giri su 35**. *(L'utente ha poi chiarito che quel
  valore e' teorico e non lo vuole usare: la decisione resta sua, il dato resta misurato.)*
- **`IsSequential`** e' letto da due formule su tre. La proiezione del carburante usa sempre
  `max(benzina, gomme)`: su una Porsche Cup sono **18 s di errore per sosta**.

### La correzione dell'utente, e il difetto vero

Nell'inventario avevo scritto che la baseline del passo e' "il primo giro valido", e avevo proposto
come lavoro nuovo di ancorarla al giro piu' veloce. **L'utente ha corretto: l'ancora sul giro
migliore esiste gia'** (`OpponentTracker.cs:1506-1520`, la abbassa a ogni giro piu' veloce). Aveva
ragione, e il documento e' stato corretto.

Ma allora il **278.563** di Barbagallo non doveva sopravvivere. Non sopravviveva per caso: e'
l'**ordine dei controlli**. La finestra di validita' gira *prima* dell'aggiornamento e scarta i giri
che deviano oltre il **-2.0%** dalla baseline — quindi un giro molto piu' veloce viene giudicato dal
numero che dovrebbe correggere.

```
Baseline Established           48 volte
Baseline Updated (Better)      55 volte
Baseline Reset (Improvement)    0 volte     <- non e' sfortuna

Alessandro Barbagallo   ancora 278.563 s al giro 7, mai piu' aggiornata
finestra che ne deriva  [273.0 , 288.3]
i suoi giri veri        ~69 s    ->  204 s sotto il limite, tutti rifiutati
```

**E c'era un secondo blocco, aritmetico.** Il ramo `Reset (Improvement)` pretendeva un miglioramento
superiore a **1.5 s**, ma la finestra ne concedeva al massimo il **2%** della baseline. Sotto i 75 s
di giro le due condizioni sono incompatibili: su una GTP a 69 s la finestra concede 1.38 s e il ramo
ne chiede 1.5. **Non poteva scattare mai.** Gli zero casi contati lo confermano.

### Fatto

- `OpponentTracker.UpdatePaceAnchor` (nuova, funzione pura) — il lato veloce dell'ancora. Un
  miglioramento ordinario si prende subito; uno **grande** resta in attesa finche' un secondo giro
  non lo conferma, e finche' e' in attesa **il giro non entra nella storia**.
- La finestra di validita' giudica ora **solo il lato lento** (+3.5%).
- `OpponentTelemetryData.PendingPaceAnchor` — il valore in attesa.
- Riga di log `Baseline Improvement Pending`, per vedere le attese invece di dedurle.

**Perche' la conferma e non l'accettazione immediata:** col criterio del massimo (punto 4) un passo
falsamente **veloce** e' la direzione pericolosa, perche' porta quella vettura al comando e anticipa
la bandiera per tutti. Due giri consecutivi d'accordo non sono un errore di misura.

### Come verificare

```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
```
```bash
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```
Atteso: exit code `0`, **268 `[PASS]`** (erano 260).

### Stato

- Compila - 0 errori. 268 test passano.
- Regressione ADR-004, **due neutralizzazioni**, entrambe rosse:

| cosa ho neutralizzato | test rosso |
|---|---|
| il miglioramento grande non viene mai confermato | `il secondo giro conferma e va accettato` |
| un miglioramento grande viene preso subito, senza conferma | `il primo giro vero va tenuto in attesa` |

Un test non esercita codice di produzione ed e' deliberato: fissa per iscritto che il vecchio ramo
era **aritmeticamente irraggiungibile** sotto i 75 s di giro. Senza, la prossima sessione potrebbe
rimettere una soglia fissa e rifare l'errore.

### Per chi entra

**Prossimo passo: un replay.** Le due cose da guardare:

1. **Barbagallo si corregge?** Nelle righe `Baseline Established` / `Baseline Updated (Better)` /
   `Baseline Improvement Pending`, la sua ancora deve scendere dai ~278 s ai ~69 in due giri. Se
   resta a 278, il rimedio non basta.
2. **Il totale del Player e' rimasto buono?** Migliore mai misurato **1.0%**; sopra il 13% si torna
   indietro.

**Poi, dall'inventario, in ordine di valore:**
- collegare `IsSequential` al ramo carburante (banale, 18 s per sosta su alcune classi);
- la cascata della perdita ai box: misurato per la classe del Player, geometrico per le altre,
  **eliminando** il livello che usa la velocita' di punta;
- decidere se il passo dei candidati del punto 4 debba essere filtrato (scelta di prodotto).

**Decisioni dell'utente da rispettare:** `EstimatedCurrentPace` e' considerato **teorico** e non va
usato nella proiezione. Il passo resta il normalizzato con media mobile. Resta aperta la sua
proposta di usare i tempi **grezzi** per tutti — va misurata prima, non adottata: il bias della
normalizzazione era GT3 0.7%, GTP 2.2%, LMP2 2.9% prima di Y-43.

**Attenzione a:** questo turno ha prodotto un inventario che ha commesso l'errore che denuncia — ho
dato per assente un calcolo che c'era. Un inventario va **verificato leggendo il codice**, non
ricordando.

---

## [2026-09-02 16:00] claude -> utente (serve un replay di verifica)

**Task:** Y-44 — il costo della sosta contava l'intera traversata della corsia box come tempo perso.
**Commit:** `dcc04d8` (lock) - questo
**Log analizzato:** `Logs/Road Atlanta/SimRIG_DebugLog_20260901_211532.csv` e `Logs/Misano/SimRIG_DebugLog_20260901_220037.csv`

### Cosa ha aperto questo turno: Misano

L'utente ha segnalato che a fine gara la proiezione del leader risultava **sotto** quella del Player,
che gli sta dietro. Vero: 160 tick su 802, scarto massimo **0.098 giri**. Ma non e' un errore di
calcolo — **e' un errore di nomenclatura mio**: le due proprieta' misurano istanti diversi.

| | posizione **quando** |
|---|---|
| `ProjectedPosAtCheckered` (Player) | esce la bandiera |
| `LeaderProjectedPosAtCheckered` | scade il cronometro |

A Misano fra i due istanti passano **40.2 s** (il supplemento). Su un giro da 92.08 s il Player
percorre **0.44 giri**, e il leader lo precede di **0.34**. Differenza **+0.10**; scarto massimo
misurato **0.098**. La coincidenza chiude la questione. **Da rinominare.**

### E ha smentito una risposta che avevo dato io

Due turni fa avevo risposto che **non serviva** conoscere il tempo di sosta degli avversari. La
misura dice il contrario. La proiezione del leader col criterio nuovo sta **+0.96 giri** sopra quella
del vecchio (mediana su 495 campioni), perche' il vecchio **sottrae la sosta del leader** e il nuovo
no — come avevo scritto io, deliberatamente. Contro la verita' di terreno (25.545):

```
criterio vecchio (sottrae la sosta)   25.03    errore  -0.51
criterio nuovo   (non sottrae nulla)  25.98    errore  +0.44
```

Il vero sta in mezzo. Ignorare una sosta non puo' che sovrastimare: l'errore **ha** un segno, e io
avevo detto di no. **Ed e' la causa del totale del leader a 27** (53.6% dei tick a Misano, contro un
vero 26): la proiezione a 25.98 sta tre centesimi sopra la soglia sotto cui il filtro puo' scendere
da 27. Non e' instabilita': e' un numero alto di mezzo giro.

**Scoperta collaterale:** la formula che sottrae la sosta sul ramo del leader (`RaceAnalyzer.cs:992`)
e' **la stessa che Y-42 aveva dichiarato sbagliata** — quella col termine `J` che altera il
denominatore. Y-42 fu corretto **solo sul ramo del Player**. Sul leader e' ancora li'.

### Y-44: la misura, e la prova della causa

La perdita non e' stata stimata ma **misurata dai tempi sul giro**, che sono la definizione stessa
della grandezza:

```
giri 14+15+16 (in-lap, out-lap, primo giro pieno)   268.20 s
tre giri normali (77.45 s l'uno)                    232.35 s
--------------------------------------------------------------
perdita reale                                        35.85 s
  di cui da fermo                                    14.75 s
  quindi perdita non-ferma                           21.05 s

il plugin ne contava                                 36.10 s   (26.38 + 9.72)
eccesso                                              15.05 s
```

**La prova della causa e' una coincidenza numerica**: 15.05 s a 77.45 s/giro fanno **0.194 di giro**,
ed e' esattamente quanto misura la zona box — 0.131 di corsia stretta (ingresso 0.957, uscita 0.088)
piu' due volte il margine di esclusione. Attraversando la corsia si copre comunque tracciato, e quel
tempo non e' perso.

**Il secondo sospetto di Y-44 e' escluso**, non rimandato: `PitTransitTime` non contiene il tempo da
fermo, `PitRadar.cs:1454` lo sottrae gia' (`num2 = num - _pitBoxTimeCache`).

### Fatto

- `RaceTimeProjection.PitLossSec` (nuova) — il costo e' il tempo nella zona **meno** l'equivalente
  in pista, piu' il tempo da fermo.
- `RaceTimeProjection.PitZoneLapFraction` (nuova) — gestisce lo **scavalco del traguardo**: a Road
  Atlanta la corsia va da 0.957 a 0.088 e la sottrazione diretta darebbe un negativo, che farebbe
  sparire la correzione **in silenzio**.
- `RaceAnalyzer.cs:~1094` — collegata al posto della somma vecchia.

### Come verificare

```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
```
```bash
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```
Atteso: exit code `0`, **260 `[PASS]`** (erano 253).

### Stato

- Compila - 0 errori. 260 test passano.
- Regressione ADR-004, **tre neutralizzazioni**, tutte rosse:

| cosa ho neutralizzato | valore ottenuto |
|---|---|
| la sottrazione dell'equivalente in pista | `ottenuto 50,85` (il valore vecchio) |
| la gestione dello scavalco del traguardo | `ottenuto 50,85` (la correzione sparisce) |
| il guard contro l'azzeramento del costo | `ottenuto -87,75` — una sosta che regala giri |

⚠️ **Errore mio da non ripetere:** ho scritto i test nuovi in `PitLossUnitTests.cs`, che
**esisteva gia'** con tre test su gomme e sosta, sovrascrivendolo. Accorto perche' il conteggio dei
PASS e' salito di 4 invece che di 7. Ripristinato da git e spostati in
`PitLossOnTrackEquivalentUnitTests.cs`. **Controllare che un file non esista prima di scriverlo**, e
diffidare di un conteggio test che non torna.

### Per chi entra

**Prossimo passo: un replay di verifica, e c'e' una previsione falsificabile.** Sul log
`20260901_211532`, all'ingresso ai box la proiezione del Player valeva **34.617** contro un vero
**34.83** — bassa di 0.213. Questa correzione restituisce `15.05 / 76.5 =` **+0.197 giri**.
**Attesa: la proiezione prima della sosta deve salire a ~34.81.** Se non succede, la diagnosi e'
incompleta.

Metro di sicurezza: tick con `RaceTotalLaps != 35` in gara. Migliore mai misurato **1.0%**; se sale
sopra il 13% (la baseline) si torna indietro.

**Poi, come concordato con l'utente, i punti 1 e 2 insieme:**
1. Correggere Y-42 anche sul **ramo del leader** (`RaceAnalyzer.cs:992`): la funzione giusta esiste
   gia' ed e' testata (`ProjectLapsLeftWithStops`), va solo collegata. **Quarta volta** che il
   problema e' un collegamento mancante (Y-31, Y-46, Y-48, questo).
2. Rimettere la sosta nella proiezione del punto 4, che avevo tolto sulla base di un ragionamento
   che la misura ha smentito. **Solo dopo Y-44**, altrimenti si sottrae un tempo sovrastimato.

Piu' la **rinomina** di `LeaderProjectedPosAtCheckered`, che dice "alla bandiera" e misura "allo
scadere".

**NON toccare:** `TimeUntilLeaderCheckered` (tre fonti concordi), `FuelWeightCoef` (`0.03`, s/kg), il
limite di plausibilita' sul passo (portante col criterio del massimo).

**Attenzione a:** il metodo che ha funzionato ogni volta in questo filone e' trovare una grandezza
**misurabile senza il codice** e chiedersi se il numero e' possibile. Qui erano i tempi sul giro:
la perdita di una sosta **e'** la differenza fra i giri con la sosta e i giri senza, e non serve
sapere nulla del plugin per calcolarla.

---

## [2026-09-02 10:00] claude -> utente (serve un replay di verifica)

**Task:** Y-48 — collegare al punto 4 anche il **totale** e la **proiezione** del leader, che
seguivano ancora il P1 istantaneo. Piu' la correzione della validazione a orizzonti fissi.
**Commit:** `954ec57` (lock) - questo
**Log analizzato:** `Logs/Road Atlanta/SimRIG_DebugLog_20260901_202537.csv`

### La verita' di terreno e' arrivata, ed e' il risultato del progetto

Primo dato **misurato** invece che riferito. Leader `Kalyann Mey4`, nell'istante in cui il cronometro
e' andato a zero: **38 giri completati + 0.0615 di giro = 38.061**, quindi 39 giri.

Verificato contro i tick adiacenti: l'ultimo col countdown positivo dava 38.055, il primo a zero
38.073. La fotografia e' caduta esattamente in mezzo.

**Validazione a orizzonti fissi, contro il vero 38.061:**

| a | punto 4 (chi comanda) | criterio vecchio (P1) |
|---|---|---|
| -20 min | 38.845 (**+0.784**) | 38.705 (+0.644) |
| -15 min | 38.148 (**+0.087**) | 29.028 (**-9.033**) |
| -10 min | 38.074 (**+0.013**) | 38.074 (+0.013) |
| -5 min | 38.027 (**-0.034**) | 37.918 (-0.143) |
| -2 min | 38.031 (**-0.030**) | 38.029 (-0.032) |

Il punto 4 cala in modo **monotono** — il criterio che il report esterno chiedeva e che non avevamo
mai potuto applicare. Il vecchio no: a 15 minuti crolla a 29.028.

**E non e' convergenza di fine gara.** La proiezione inchioda 38.06-38.10 **da 12 minuti dalla fine**,
cioe' dieci giri del leader prima, e non se ne muove:

```
tl=717  (12.0 min)  proiettata=38.10   errore +0.039
tl=535  ( 8.9 min)  proiettata=38.07   errore +0.009
tl=444  ( 7.4 min)  proiettata=38.07   errore +0.009
```

⚠️ **Il 38.8 del software di riferimento non e' la verita' finale.** Il vero e' 38.061; il
riferimento mostra 38.8-38.99 a meta' gara e il nostro punto 4 mostrava 38.845 a 20 minuti. Sono
entrambi alti di ~0.78 e **concordano fra loro**. Il numero che trattavamo come metro esatto era una
proiezione di meta' gara con il nostro stesso scostamento. Ipotesi sull'origine (non verificata): le
soste che ai leader mancavano ancora.

### Il difetto che l'utente ha visto, e perche' c'era

Il totale del leader crollava ancora a 28-30 per **29 tick**. Nello stesso identico tick:

```
t=20:36:33  VECCHIO: P1=Alessandro Barbagallo  proiez=27.886  -> totale 30
            PUNTO 4: comanda=Aleix Nogue  passo=68.18  proiez=38.27
```

**La risposta giusta era calcolata e non collegata.** Nel turno di accensione avevo cablato al punto 4
solo il *tempo alla bandiera*, lasciando di proposito il totale del leader sul criterio vecchio per non
spostare il numero che l'utente usava per i confronti. Svista di scoping, non un limite del criterio.

### Fatto

- `RaceAnalyzer.cs:ResolveLeaderPosAtZero` (nuovo, funzione pura) — la scelta fra la proiezione di
  chi comanda e quella del P1 istantaneo. Estratta perche' **questo difetto era esattamente il
  chiamante**: e' la lezione di Y-31 per la terza volta in questo repository.
- Il calcolo del punto 4 e' stato **spostato piu' in alto**, prima del totale del leader: e' da li'
  che discendono sia il totale sia il tempo alla bandiera.
- `LeaderProjectedPosAtCheckered`, `LeaderRaceTotalLaps` e i giri rimanenti del leader vengono ora
  tutti dalla vettura al comando. I giri rimanenti si contano sulla **sua** posizione: mescolare il
  totale di una vettura con la posizione di un'altra darebbe un conteggio che non descrive nessuna
  vettura reale.
- La riga `Flag Moment` espone `totLeader=` e la proiezione del vecchio criterio, per il confronto.
- **Corretta la strumentazione di ieri**: la riga di verita' di terreno confrontava la proiezione
  **allo scadere**, quando il countdown vale zero e la proiezione coincide per costruzione con la
  posizione misurata — i tre numeri leggevano tutti 38.061 e il confronto non dimostrava niente. Ora
  `RecordValidationHorizons` fotografa la proiezione a 20/15/10/5/2 minuti e la riga
  `Projection Validation` le stampa a fine gara con gli errori gia' calcolati.

### Come verificare

```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
```
```bash
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```
Atteso: exit code `0`, **253 `[PASS]`** (erano 248).

### Stato

- Compila - 0 errori. 253 test passano.
- Regressione ADR-004, **due neutralizzazioni**, entrambe rosse:

| cosa ho neutralizzato | test rosso | valore ottenuto |
|---|---|---|
| il totale del leader torna a seguire il P1 istantaneo | deve vincere la proiezione di chi comanda | `ottenuto 27,886` |
| via il ripiego quando non c'e' vettura al comando | e' dichiarato assente | `ottenuto 500` |

### Per chi entra

**Prossimo passo: un replay di verifica.** Road Atlanta, la solita gara a 3x. Tre cose:

1. **Il crollo a 28 e' sparito?** Nelle righe `RaceProjectionsDiagnostics`, `LatchedTotal` del leader
   non deve piu' scendere sotto 38. Era 29 tick su 882.
2. **Il totale del Player e' rimasto buono?** E' il numero da cui esce il carburante e l'unico motivo
   per tornare indietro. Il migliore mai misurato e' **1.2%** di tick sbagliati (`20260901_202537`);
   la baseline era 13.3%. **Cautela:** i due run con codice attivo identico prima di questo hanno
   dato 3.6% e 12.9%, quindi la variabilita' e' alta e un solo replay non basta a concludere.
3. **La riga `Projection Validation`** a fine gara: gli errori devono calare in modo monotono. Ora e'
   automatica, non serve piu' ricostruirla a mano dal log.

**Poi:** **Y-44** (il tempo di sosta sovrastimato del 49%), che e' il difetto residuo sul numero del
Player. Restano anche 3, 5, 7.

**NON toccare:** `TimeUntilLeaderCheckered` (tre fonti concordi), `FuelWeightCoef` (`0.03`, s/kg), e
il limite di plausibilita' sul passo, che col criterio del massimo e' **portante**.

**Attenzione a:** in questo filone lo stesso schema si e' ripetuto tre volte — una formula corretta
collegata al posto sbagliato (Y-31, Y-46, Y-48). Quando un valore sembra sbagliato, prima di
sospettare la formula conviene controllare **da dove arriva**.

---

## [2026-09-02 01:00] claude -> utente (serve un replay di verifica)

**Task:** accendere il punto 4 v3, e aggiungere la verita' di terreno sulla posizione del leader
allo scadere del cronometro (Y-47, richiesta dell'utente).
**Commit:** `b9e090a` (lock) - questo
**Log che ha deciso l'accensione:** `Logs/Road Atlanta/SimRIG_DebugLog_20260901_184453.csv`

### Perche' si e' acceso

La prova era una sola: il **supplemento** di fine gara, cioe' di quanto la bandiera esce dopo lo
scadere del cronometro. Allo scadere il leader e' a meta' giro in media, quindi su un giro da 69 s
deve valere ~35 s e non puo' quasi mai essere zero. Su 758 campioni di gara:

```
criterio ATTUALE (P1 di adesso)     mediana 24.7 s
criterio v3 (chi COMANDA)           mediana 53.7 s   <- plausibile
criterio v2 (bocciato)              mediana  4.9 s   <- impossibile
```

v2 ha ridato 4.9 s dove il run precedente dava 5.2: il difetto si e' riprodotto identico.

**La finestra Barbagallo, che e' il caso per cui il punto 4 esiste.** Con lui P1 istantaneo e passo
registrato 278.60 s:

```
tl=876  USATO=1030.9 (P1=Barbagallo, suppl 154.7)  V3=927.3 (comanda Aleix Nogue, passo 68.22, suppl 51.1)
tl=864  USATO= 982.4 (P1=Barbagallo, suppl 118.4)  V3=917.7 (comanda Aleix Nogue, passo 68.22, suppl 53.6)
tl=851  USATO= 946.8 (P1=Barbagallo, suppl  94.9)  V3=903.9 (comanda Aleix Nogue, passo 68.22, suppl 52.0)
```

Un supplemento di 154.7 s e' piu' di due giri interi del leader: impossibile. v3 tiene ~50 s stabili
e al comando non mette **mai** Barbagallo. **Y-38 sciolto, osservato e non dedotto.**

Sul leader v3 da' mediana **38.840** contro il **38.8** del software di riferimento dell'utente; il
criterio vecchio dava 38.549.

### Costi, dichiarati prima del replay e non dopo

- identita' del comando: **67 cambi** contro 3;
- salti del valore oltre 10 s: **44** contro 16 — ma con massimo **68.6 s** (un giro del leader,
  cioe' il giro fantasma, che e' fisico) contro i **132.8 s** del criterio vecchio, che non
  corrisponde a niente;
- impatto stimato sul totale del Player: **80.2% identico, 16.9% un giro in piu', 2.9% un giro in
  meno**. La direzione prevalente e' quella sicura.

Quel 16.9% e' calcolato sulla proiezione **grezza**: il totale esposto passa prima dal filtro di
Y-45, che e' fatto apposta per assorbire picchi brevi, e i salti di v3 sono brevi. L'impatto vero
sara' minore, ma quanto minore non e' misurabile senza accendere.

### Y-47: smettere di trattare il 38.8 come verita' rivelata

Richiesta dell'utente, e ha ragione: il 38.8 e' il numero di **un altro software**, mai verificato,
che puo' avere una sua deviazione. Aggiunte, senza alcun cambio di comportamento:

- `LeaderPosAtExpiry` - dove si trova il leader nell'**istante esatto** in cui il cronometro va a
  zero: giri completati + frazione di giro. Fotografato una volta sola e mai piu' toccato.
  `Math.Ceiling` di quel numero **e'** quanti giri il leader completera' davvero.
- `LeaderNameAtExpiry`, `LeaderTrackPctAtExpiry` - chi era e a che punto del giro.
- `LeaderTrackPct` - posizione grezza del leader **adesso**, che cambia vettura col leader.
- `FlagLeaderName`, `FlagLeaderProjectedPos` - chi comanda secondo il punto 4 e dove sara'.
- riga di log `Leader Position At Expiry`: allo scadere mette in fila verita' di terreno, proiezione
  del punto 4, proiezione del criterio vecchio.

### Come verificare

```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
```
```bash
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```
Atteso: exit code `0`, **248 `[PASS]`** (erano 242).

### Stato

- Compila - 0 errori. 248 test passano.
- Regressione ADR-004, **cinque neutralizzazioni**, tutte verificate rosse.

| cosa ho neutralizzato | test rosso | valore ottenuto |
|---|---|---|
| il ripiego quando non c'e' vettura al comando | un risultato assente non si usa | `ottenuto 500` |
| ritorno al P1 di questo istante | deve vincere la vettura al comando | `ottenuto 1030,9` |
| il guard sul countdown positivo | in griglia non si fotografa | rosso |
| il guard "una volta sola" | ma non una seconda volta | rosso |
| il guard sul cronometro in corsa | a meta' gara no | rosso |

**Una delle cinque ha trovato un buco nei miei stessi test, ed e' il motivo per cui il passo
esiste.** La prima neutralizzazione non diventava rossa: il caso che avevo scritto per coprire il
ripiego passava **anche** grazie a un secondo guard sullo zero, quindi il primo non era verificato da
nulla. Aggiunto il caso con un valore non nullo, ora scatta.

### Per chi entra

**Prossimo passo: un replay di verifica.** Road Atlanta, la solita gara a 3x. Tre cose, in ordine:

1. **Il totale del Player e' peggiorato?** E' il numero da cui esce il carburante e l'unico motivo
   per tornare indietro. Metro: tick con `RaceTotalLaps != 35` in gara. Il migliore mai misurato e'
   **3.6%** (`20260901_175019`); la baseline era 13.3%. Se sale sopra il 13%, si reverte: e' un solo
   commit, e il punto di partenza e' il tag `baseline-proiezioni-2026-08-31`.
2. **La verita' di terreno cosa dice?** Riga `Leader Position At Expiry`. Confronta `posAssoluta`
   (misurata) con `noiAvevamoProiettato` e con `vecchioCriterio`. **E' la prima volta che possiamo
   dire chi ha ragione senza dipendere da un altro software.**
3. **Il supplemento resta plausibile?** Nelle righe `Flag Moment`, il campo `suppl=` di `USATO=` deve
   stare intorno a 35-55 s.

**Poi:** **Y-44** (il tempo di sosta sovrastimato del 49%), che e' il difetto visibile residuo sul
numero del Player. Restano anche 3, 5, 7.

**NON toccare:** `TimeUntilLeaderCheckered` (tre fonti concordi), `FuelWeightCoef` (`0.03`, s/kg), e
il limite di plausibilita' sul passo — che ora e' **portante**, perche' col criterio del massimo un
passo falsamente veloce va al comando.

**Attenzione a:** il punto 4 e' stato riscritto tre volte, e ogni bocciatura e' arrivata dalla stessa
mossa: trovare una grandezza **fisica**, controllabile senza sapere niente del codice, e chiedersi se
il numero e' possibile. Prima il minimo che collassava sul countdown, poi il supplemento da 5
secondi. Prima di accendere qualcosa, cercare quella grandezza.

---

## [2026-09-01 21:30] claude -> utente (serve un replay)

**Task:** punto 4, dal minimo del tempo di attraversamento al **massimo della posizione proiettata**.
Resta in ombra.
**Commit:** `5f715c7` (lock) - questo
**Log analizzato:** `Logs/Road Atlanta/SimRIG_DebugLog_20260901_175019.csv`

### Il replay ha dato tre risposte, e una ha cambiato il disegno

**1. Il totale del Player e' il migliore mai misurato.** Tick sbagliati (il vero e' 35), sui sette replay:

| replay | Player | leader |
|---|---|---|
| `20260830_102220` | 12.1% | 82.6% |
| `20260830_113151` | 23.3% | 59.3% |
| `20260830_121813` | 30.0% | 67.3% |
| `20260830_140721` | 36.5% | 49.6% |
| `20260831_195300` (baseline) | 13.3% | 13.8% |
| `20260831_222417` | 15.8% | 6.7% |
| **`20260901_175019`** | **3.6%** | 18.2% |

`ProjectedPosAtCheckered` ha mediana **34.603** contro il vero 34.83 — la piu' vicina mai misurata
(la baseline era 34.533) e la banda piu' stretta (0.914 contro 0.965).

**2. Sul leader l'utente ha ragione, ma non nel modo che sembra.** La differenza fra gli ultimi due
run **non e' attribuibile** alle modifiche: il diff mostra che ogni riga cambiata scrive sul *Player*
o sta nella funzione in ombra, e `_latchedLeaderTotalLaps` non e' mai toccato. La variabilita'
run-to-run su questa metrica e' enorme — i quattro replay vecchi, con codice **identico** fra loro,
danno 49.6 / 59.3 / 67.3 / 82.6%.

I 148 tick sbagliati di questo run si scompongono cosi':

| errore | tick | c'era nella baseline? |
|---|---|---|
| totale a **40** (`L_PosAtFlag=39.085`, appena sopra soglia) | 93 | **si', e di piu': 112** |
| crollo a **28-30** (Barbagallo, passo 278 s) | 29 | **no** |
| risalita 37->38 dopo il crollo | 26 | no |

Il crollo non c'era nella baseline perche' il ritardo di 30 s impediva al filtro di scendere. Non lo
*preveniva*: lo **nascondeva** — ed e' la stessa cecita' che li' teneva il totale del Player a 37 per
tre giri. La baseline non e' migliore sul leader: e' piu' cieca. **Ma Y-38 resta intatto.**

**3. Il punto 4 v2 e' impossibile, e lo dice una grandezza che non dipende dal codice.** Il
**supplemento** — di quanto la bandiera esce dopo lo scadere del cronometro. Allo scadere il leader
e' a meta' giro in media, quindi su un giro da 69 s deve valere ~35 s e non puo' quasi mai essere
zero. Su 758 campioni di gara:

```
criterio attuale (P1 di adesso)        mediana 50.6 s   (da 6.1 a 67.1)
criterio v2 (minimo contendenti)       mediana  5.2 s   (da 0.5 a 19.6)
```

Cinque secondi: la bandiera uscirebbe sullo scadere del cronometro, sempre.

**Causa.** "Chi taglia per primo dopo lo scadere" coincide col leader **solo fra vetture sullo stesso
giro**. Se una e' a inizio del giro 39 e un'altra a fine del 38, la seconda taglia prima ma sta
chiudendo il *suo* giro 38: non fa finire la gara. Con 5-8 contendenti dentro una finestra di un giro
ce ne sono sempre a cavallo del confine.

### Fatto

- `RaceTimeProjection.cs` - `EarliestCheckeredTime` -> **`ProjectFlagMoment`**, e la struct
  `EarliestCheckered` -> `FlagMoment`. Il nome vecchio era diventato una bugia: non si cerca piu' il
  primo ad attraversare. Il criterio e' il **massimo della posizione proiettata**, poi il tempo di
  attraversamento di quella vettura.
- Il vecchio minimo resta calcolato in `FlagMoment.EarliestCrossingSec` come **diagnostica di
  confronto sullo stesso tick**, cosi' il prossimo replay mette i due criteri fianco a fianco.
- `Contenders` degradato a diagnostico: dice quanto e' contesa la testa, non entra nel calcolo.
- `RaceAnalyzer.cs:LogShadowFlagTime` - la riga ora scrive `suppl=` per il criterio in uso e per il
  nuovo, piu' `vecchioMin=`. Il supplemento e' la grandezza che ha bocciato v2: va letta per prima.
- Test rinominati `EarliestCheckeredUnitTests.cs` -> `FlagMomentUnitTests.cs`, con la **storia delle
  tre versioni in testa al file** perche' nessuno ripercorra la stessa strada.

### La domanda dell'utente sulle soste degli avversari, e la risposta

*"Se non conosciamo i dati di transito dei box della classe leader, come possiamo prevederne la
perdita di tempo?"* Non serve, per tre motivi:

1. Del leader serve **un numero solo**, e vive dentro un limite stretto: il tempo alla bandiera e' il
   countdown (telemetria esatta) piu' un supplemento che non puo' mai superare **un giro del
   leader**. Tutti gli errori sul leader stanno dentro quei ~69 s.
2. Una sosta sposta la posizione proiettata di ~0.55 giri (40 s su 69). Col criterio del massimo
   conta solo se cambia **chi** comanda: se due vetture distano piu' di mezzo giro non le inverte, e
   se distano meno i loro tempi di attraversamento si somigliano comunque.
3. **Non abbiamo un numero da esportare.** Il tempo di sosta che conosciamo meglio e' il nostro, ed
   e' sovrastimato del 49% (Y-44). Applicarlo a 43 avversari sostituirebbe un'ignoranza onesta con un
   errore sistematico moltiplicato per 43. DahlDesign non lo modella affatto.

**Prezzo, detto chiaro:** e' esattamente la promessa del punto 4 che non si realizza — "il leader che
deve ancora fermarsi cede il posto *prima* del sorpasso fisico". Senza quel dato il passaggio avviene
al sorpasso. Resta l'immunita' al passo avvelenato, che e' il difetto che costa davvero.

### Come verificare

```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
```
```bash
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```
Atteso: exit code `0`, **242 `[PASS]`**.

### Stato

- Compila - 0 errori. 242 test passano.
- Regressione ADR-004, **tre neutralizzazioni**, tutte verificate rosse:

| cosa ho neutralizzato | test rosso | valore ottenuto |
|---|---|---|
| massimo -> minimo della posizione proiettata | deve comandare il leader vero | `comanda Alessandro Barbagallo` |
| ritorno al criterio v2 (minimo del tempo di attraversamento) | la bandiera cade sul suo attraversamento | `ottenuto 925,98` |
| limite di plausibilita' che non scarta piu' | col limite deve comandare il leader vero | `comanda baseline anomala (Y-39)` |

### Per chi entra

**Prossimo passo: serve un replay.** Road Atlanta, la solita gara a 3x. Due domande, in ordine:

1. **Il supplemento e' plausibile?** Nelle righe `Shadow Flag Time`, il campo `suppl=` del criterio
   `max=` deve stare intorno a 35-50 s e **non** avvicinarsi a zero. Se ci va vicino, anche v3 e'
   sbagliato e va abbandonato invece di ritarato.
2. **`comanda=` sfarfalla?** Deve essere una vettura di testa e non deve cambiare a ogni tick. Se e'
   stabile dove oggi il P1 sfarfalla, il punto 4 chiude Y-38 e si puo' accendere.

**Poi:** se v3 regge, accenderlo (turno separato). Altrimenti **Y-44**, che e' il difetto visibile
residuo sul numero del Player.

**NON toccare:** `TimeUntilLeaderCheckered` (tre fonti concordi), `FuelWeightCoef` (`0.03`, s/kg).
E **non aggiungere le soste degli avversari** finche' Y-44 e' aperto.

**Attenzione a:** questo filone ha ora bocciato **due** disegni del punto 4 su misura, non su
opinione. Il metodo che ha funzionato entrambe le volte e' lo stesso: trovare una grandezza fisica
che si possa controllare **senza sapere niente del codice** — qui il supplemento di fine gara — e
guardare se il numero e' possibile. Prima di accendere qualcosa, cercare quella grandezza.

---

## [2026-09-01 18:00] claude -> utente (serve un replay)

**Task:** Y-46 (il tetto leader->Player che si riapplicava senza condizione) e revisione del
punto 4, che resta in ombra.
**Commit:** `4f573b0` (lock) - questo
**Log analizzato:** `Logs/Road Atlanta/SimRIG_DebugLog_20260831_222417.csv` (primo replay dopo Y-45)

### Cosa ha detto il replay, in tre punti

**1. Il calo 35 -> 34 alla sosta e' Y-44, non un difetto nuovo.** L'utente l'aveva notato e
ipotizzato che fosse dovuto alla risposta piu' rapida del filtro. Meta' giusto: il filtro si comporta
bene, ma l'ingresso e' sbagliato.

| `ProjectedPosAtCheckered` | mediana | min | max |
|---|---|---|---|
| **prima** della sosta | 33.967 | 33.919 | 34.802 |
| **dopo** la sosta | 34.460 | 34.121 | 35.089 |

Il vero e' 34.83. Prima della sosta sottostimiamo di quasi un giro. **Il calo non e' al momento della
sosta:** inizia alle 22:30:14 (giro 11, cinque minuti di gara PRIMA) e finisce un secondo dopo il
`Pit Complete` delle 22:31:53. Non e' "la sosta sballa il conto", e' "finche' una sosta e' pendente,
il conto e' sballato". Il ritardo di 30 s lo stava nascondendo. Margine sottilissimo: la soglia di
discesa e' a 33.95, la mediana 33.967 — diciassette millesimi.

**2. Y-46, e l'ho scoperchiato io.** Alle 22:35:25 il totale del Player e' sceso a **28** per 103
secondi di gara (29 tick su 811), mentre la sua proiezione era ferma a 34.53:

```
t=22:35:24  posAtFlag=34.537  TOT=35   leaderTot=39
t=22:35:25  posAtFlag=34.530  TOT=30   leaderTot=30
t=22:35:29  posAtFlag=34.527  TOT=28   leaderTot=28
```

Il totale copiava quello del leader cifra per cifra: un `Math.Min(player, leader)` incondizionato,
tre righe sotto un commento che dice che in multiclasse quel tetto non va applicato. Difetto vecchio,
ma **prima di Y-45 non si vedeva**: verificato sul log `195300`, dove nella stessa finestra
`leaderTot` resta 39.00 perche' il ritardo di 30 s bloccava la discesa. Togliendo una protezione
rotta se n'e' trovata una seconda sotto.

**3. La modalita' ombra ha bocciato il punto 4 come era specificato.** Ed e' il risultato piu' utile
del turno, perche' e' costato zero.

- minimo **piu' basso** del valore in uso **867 volte su 910**, mediana **-28.9 s** (circa -0.4 giri),
  code fino a -169 s;
- vincitore che ruota fra ~40 vetture, **Player compreso** (80 volte);
- limite di plausibilita' sul passo: **0 scarti su 910**.

Causa, e non dipende dal circuito: con 43 vetture in pista, in ogni istante *qualcuna* e' a pochi
metri dalla linea. Il minimo su tutte collassa sul countdown e il giro finale del leader sparisce.
**Il discriminante non e' la classe, e' il conteggio giri: una vettura doppiata che taglia il
traguardo non fa uscire la bandiera.**

### Fatto

- `RaceAnalyzer.cs:ApplyLeaderTotalCap` (nuovo) - il tetto vale in monoclasse e quando il Player
  **e'** il leader; in multiclasse e non leader, no. Estratto in funzione pura perche' la lezione di
  Y-31 e' che il difetto sta nel chiamante. **Non e' ridondante col tetto a monte:** quello agisce
  sulla posizione continua prima della banda, questo sul totale gia' formato — se il totale
  memorizzato e' gia' sopra il tetto, la banda puo' tenercelo.
- `RaceTimeProjection.cs:EarliestCheckeredTime` - due passate: si calcola dove sara' ciascuno allo
  scadere, si prende il massimo, si tengono solo quelli entro `LeadLapMarginLaps` (= 1.0 giro), e
  **fra quelli** si prende il minimo. Aggiunti `Contenders` e `MaxProjectedPos` al risultato.
- `RaceAnalyzer.cs:LogShadowFlagTime` - la riga ora espone `inLotta=` e `maxProiettato=`.

Il margine di un giro non e' stretto di proposito: non deve discriminare fra contendenti, deve solo
escludere i doppiati, e una sosta ancora da fare costa ~0.5 giri che deve restare dentro.

### Come verificare

```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
```
```bash
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```
Atteso: exit code `0`, **243 `[PASS]`** (erano 235).

### Stato

- Compila - 0 errori. 243 test passano.
- Regressione ADR-004, **due neutralizzazioni**, entrambe verificate rosse:

| cosa ho neutralizzato | test rosso | valore ottenuto |
|---|---|---|
| la condizione in `ApplyLeaderTotalCap` (tetto di nuovo incondizionato) | il Player non segue il leader | `ottenuto 28` |
| `LeadLapMarginLaps` 1.0 -> 9999 (via la restrizione) | la bandiera la fa uscire il leader | `ha vinto doppiato vicino al traguardo` |

Nota di metodo: la seconda neutralizzazione all'inizio faceva fallire una *premessa* del test invece
dell'asserzione significativa, con un messaggio fuorviante. Le premesse sono state riscritte in modo
da dipendere solo dagli ingressi, cosi' il rosso cade dove serve.

### Per chi entra

**Prossimo passo: serve un replay dall'utente.** Road Atlanta, la solita gara a 3x. Tre domande:

1. **Y-46 e' chiuso?** Il totale del Player non deve piu' seguire quello del leader. Nelle righe
   `Total Laps Transition` non devono piu' comparire discese del Player con `grezzo`/`lisciato`
   fermi intorno a 34.5.
2. **Il punto 4 rivisto e' plausibile?** Dalle righe `Shadow Flag Time`: `inLotta` deve stare fra 2 e
   ~8 (le vetture in lotta per la vittoria assoluta). Se vale 1 per tutta la gara il minimo degenera
   nel leader corrente e il punto 4 non porta niente; se vale ~40 la restrizione non filtra. E
   `delta` deve tornare vicino a zero, non piu' -28.9 s di mediana.
3. **Quanto resta di Y-44?** Il calo prima della sosta e' ora il difetto visibile piu' grosso.

**Poi:** se il punto 4 regge, accenderlo (turno separato). Altrimenti Y-44 prima. Restano 3, 5, 7.

**NON toccare:** `TimeUntilLeaderCheckered` (tre fonti concordi), `FuelWeightCoef` (`0.03`, s/kg), e
il limite di plausibilita' sul passo, che ora sappiamo non scattare mai — va lasciato dov'e' come
rete, non tarato.

**Attenzione a:** questo turno ha mostrato due volte lo stesso schema — una protezione rotta che ne
nascondeva un'altra. Dopo il prossimo replay, prima di aggiungere, conviene chiedersi cosa **altro**
era mascherato.

---

## [2026-09-01 12:00] claude -> utente (serve un replay)

**Task:** punto 6 (stabilizzazione) **attivo**, punto 4 (bandiera = minimo) **in ombra**, piu' la
strumentazione che rende osservabile il cambio del totale.
**Commit:** `7bcd587` (lock) - questo
**Log analizzato:** `Logs/Road Atlanta/SimRIG_DebugLog_20260831_195300.csv`
**Punto di partenza taggato:** `baseline-proiezioni-2026-08-31` (= `7fe6d58`)

### La cosa piu' importante di questo turno: la diagnosi registrata era sbagliata

L'handoff del 31/08 attribuiva il `RaceTotalLaps` a 37 a "cinque tick sopra 35.05". **Non e' cosi',
e la verifica ha cambiato cosa andava scritto nel codice.**

- Quattro dei cinque tick (20:04:23-26) cadono **dopo** che il totale era gia' 37 dalle 20:04:09.525.
- Il piu' alto dei cinque vale `35.435`. Per far scattare il filtro da 35 a 37 serve un ingresso
  sopra **36.05**, che in tutto il log **non compare**.
- Motivo: `RaceProjectionsDiagnostics` e' strozzata a **una riga al secondo**, mentre il calcolo gira
  almeno a **12 Hz** (misurato: intervallo massimo fra due righe 1.085 s con strozzatura a 1.0 s).
  Vediamo al massimo un fotogramma su dodici.

**La causa vera, chiusa aritmeticamente (e' Y-38).** Alle 20:04:08.520 il P1 assoluto passa da
`Sven Neiss` (passo 68.443) ad `Alessandro Barbagallo`, che porta un passo registrato di
**278.563 s**. Il supplemento di `TimeUntilLeaderCheckered` vale al massimo un giro del leader:
`278.563 / 76.524 =` **3.64 giri del Player in un fotogramma**.

**La permanenza, invece, e' un difetto nuovo: Y-45.** Non era la banda di `UpdateLatchedLaps` - che
e' geometricamente **simmetrica** (0.05 oltre il confine in entrambe le direzioni) e non ha mai
bloccato niente. Era il **ritardo di 30 s applicato alla sola discesa**, che ripartiva da zero a
ogni cambio del bersaglio:

```
20:04:37  raw=34.959 -> bersaglio 36   timer riarmato
20:04:39  raw=34.946 -> bersaglio 35   timer riarmato
20:04:40  raw=34.955 -> bersaglio 36   timer riarmato
20:04:47  raw=34.950 -> bersaglio 35   timer riarmato
20:04:50  raw=34.956 -> bersaglio 36   timer riarmato
```

**Nove millesimi di giro** - meno di un secondo di gara - a cavallo dello scalino di
`Ceiling(x + 0.05)`, che sta esattamente a 34.95. Risultato: 79 s di blocco invece di 30, cioe' 237 s
di gara, cioe' **tre giri**, con 4.2-4.4 L di rifornimento chiesti e non necessari.

### Fatto

- `ProjectionStabilizer.cs` (nuovo) - lisciamento della posizione proiettata con riconoscimento dei
  cambi persistenti. Il tempo si misura in **secondi di gara**, non di orologio: a 3x un tick vale
  3 s di gara, e un filtro tarato sull'orologio renderebbe due run non confrontabili.
- `RaceAnalyzer.cs:905` e `RaceAnalyzer.cs:985` - **rimossi entrambi i ritardi di 30 s** (leader e
  Player). La posizione entra nella banda gia' lisciata.
- `RaceAnalyzer.cs:74` - `SmoothedPosAtCheckered` esposta accanto a `ProjectedPosAtCheckered`.
  **La grezza resta grezza**: e' il numero che l'utente confronta col software di riferimento, e
  lisciarlo al suo posto ne avrebbe cambiato il significato.
- `RaceTimeProjection.cs:217` - `EarliestCheckeredTime`: il minimo del tempo di attraversamento su
  tutte le vetture, con limite di plausibilita' sul passo. **Non usata da nessun calcolo.**
- `RaceAnalyzer.cs:LogShadowFlagTime` - riga `Shadow Flag Time` (1/s) con il valore usato oggi, il
  minimo con limite fisico, il minimo con limite stretto, e chi vince.
- `RaceAnalyzer.cs:LogTotalLapsTransition` - riga `Total Laps Transition` che scatta **al cambio**
  del totale, con l'ingresso grezzo, quello lisciato e lo stato del filtro.

### Una scelta di progetto da non lasciare implicita

Il report esterno raccomanda un **Alpha-Beta**. Il termine di velocita' e' stato **omesso**, non
dimenticato: la posizione proiettata alla bandiera e' la previsione di un valore **finale e fisso**,
la deriva misurata sul replay vale 0.5 giri in 900 s, e il ritardo che ne consegue e'
`4 x 0.5/900 =` **0.002 giri** - quattro ordini di grandezza sotto la banda di 0.05 che dovrebbe
superare per contare. Aggiungerlo estrapolerebbe rumore senza correggere nulla di misurabile. Va
aggiunto **con una misura che lo giustifichi**, non per completezza formale.

### Una proprieta' controintuitiva del punto 4, fissata in un test

**Un passo piu' veloce non vince automaticamente il minimo.** Il tempo di attraversamento vale
`T + frazione x passo` con la frazione in [0,1): un passo piu' veloce abbassa il *tetto* del
supplemento, non il supplemento di adesso. Stessa vettura a 58.810 s: da posizione 24.0 attraversa a
940.96 s e **perde**, da 24.2 attraversa a 929.20 s e **vince**. Conseguenza per quando si accendera'
il punto 4: un passo falsamente veloce non falsa la bandiera in continuazione, la falsa **a
intermittenza** - piu' difficile da riconoscere a occhio, non meno grave.

### Come verificare

```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
```
```bash
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```
Atteso: exit code `0`, **235 `[PASS]`** (erano 219).

### Stato

- Compila - 0 errori. 235 test passano.
- Regressione ADR-004, **tre neutralizzazioni separate**, tutte verificate rosse:

| cosa ho neutralizzato | test diventato rosso | valore ottenuto |
|---|---|---|
| `SuspectJumpLaps` 1.5 -> 1000 (via il riconoscimento dell'artefatto) | il picco non deve muovere la stima | `ottenuto 35,779` - esattamente il 35.78 predetto nel commento del test |
| `SmoothingTauRaceSec` 4.0 -> 0.0001 (via il lisciamento) | il bersaglio non deve piu' cambiare | `cambi contati 6` |
| minimo -> massimo in `EarliestCheckeredTime` | deve vincere il leader vero | `ha vinto Alessandro Barbagallo` |

### Per chi entra

**Prossimo passo: serve un replay dall'utente.** Road Atlanta, la solita gara a 3x. Le due domande a
cui deve rispondere, in ordine:

1. **Il punto 6 funziona?** `RaceTotalLaps` deve restare 35 per tutta la seconda meta' e `FuelToAdd`
   non deve piu' avere il gradino a 4.3 L. Si legge dalle righe `Total Laps Transition`: se ce ne
   sono meno di prima, e quelle che restano hanno `grezzo` e `lisciato` vicini, il filtro lavora.
2. **Il punto 4 cosa darebbe?** Dalle righe `Shadow Flag Time`: quanto vale `delta` (minimo meno
   valore usato) e se `min` e `minStretto` divergono mai. Se non divergono, il limite fisico basta e
   il punto 4 puo' accendersi senza il limite stretto.

**Poi:** accendere il punto 4 (turno separato, un solo cambio di comportamento per volta - e' la
lezione di Y-42). Restano 3, 5, 7 e Y-44.

**NON toccare:** `TimeUntilLeaderCheckered` (tre fonti concordi), `FuelWeightCoef` (resta `0.03`,
s/kg). E **non aggiungere le soste al minimo** finche' Y-44 e' aperto: il tempo di sosta e'
sovrastimato del 49%, sommarlo adesso sostituirebbe un errore misurato con uno non misurato.

**Attenzione a:** il totale converge sempre al valore giusto a fine gara, perche' la parte proiettata
si riduce a zero. "Il numero finale e' corretto" non e' mai una prova. E la diagnostica a 1 Hz non
basta a chiudere un difetto: e' costata una diagnosi sbagliata a questo stesso filone.

---

## [2026-08-31 21:00] claude → nuova chat (contesto saturo)

**Task:** Y-42 (forma della sottrazione della sosta) e Y-43 (penalita' carburante in s/kg)
**Commit:** `be3352d` (lock) · `7fe6d58` · `50a308f` · questo
**Log di verifica:** `Logs/Road Atlanta/SimRIG_DebugLog_20260831_195300.csv`

### Dove siamo arrivati — leggere questo prima di tutto

Dopo sette correzioni (Y-31, Y-32, Y-35, Y-39, Y-41, Y-42, Y-43) le proiezioni sono **vicine al
software di riferimento dell'utente, in alcuni tick identiche al secondo decimale**. Parole sue.

| | nostro | riferimento |
|---|---|---|
| `LeaderProjectedPosAtCheckered` | **38.0 - 39.0** tutta la gara, centrato ~38.5-38.8 | 38.8 |
| `LeaderRaceTotalLaps` | **39** per quasi tutta la gara | 39 |
| `ProjectedPosAtCheckered` (ultimo terzo) | **34.83 - 34.91** | 34.83 (valore reale) |
| `RaceTotalLaps` finale | **35** | 35 (giri realmente completati) |

Dispersione del Player, misurata sui quattro replay consecutivi:

| run | p05 | mediana | p95 | fascia |
|---|---|---|---|---|
| `102220` | 33.597 | 34.386 | 34.855 | 1.258 |
| `113151` | 33.995 | 34.489 | 34.989 | 0.994 |
| `140721` | 33.814 | 34.651 | 34.934 | 1.120 |
| **`195300`** | 33.964 | **34.542** | 34.932 | **0.968** ← migliore |

⚠️ **Trappola di misura da non ripetere:** dal commit `9d16172` (Y-37) ogni riga
`RaceProjectionsDiagnostics` contiene **due** campi `PosAtFlag`, quello del Player e quello del
leader. Un `grep -oE "PosAtFlag=[0-9.]+"` li prende entrambi e produce statistiche senza senso (io
ci sono cascato: avevo misurato una "fascia" di 5.778 che non esisteva). Usare
`sed -E 's/.*Player:[^|]*PosAtFlag=([0-9.]+).*/\1/'`.

### Verifica delle due correzioni di oggi

**Y-43 confermato.** Il delta grezzo-normalizzato e' calato come previsto:

| classe | prima | dopo |
|---|---|---|
| Dallara P217 | 2.11 s (2.9%) | **1.42 s (1.9%)** |
| GTP | 1.61 s (2.2%) | **1.00 s (1.4%)** |
| IMSA23 (GT3) | 0.57 s (0.7%) | **0.43 s (0.6%)** |

**Y-42 confermato nella forma, ma resta il valore.** Vedi Y-44 qui sotto.

### L'unico difetto grosso rimasto, e ha un solo colpevole

L'utente ha visto `RaceTotalLaps` salire a **37** a meta' gara per poi tornare a 35. Causa isolata:

- `L_Pace` schizza a **278.563 s** al giro 23-24 (leader `Kalyann Mey4`), contro i ~69 s reali;
- il supplemento in `TimeUntilLeaderCheckered` puo' valere fino a un giro del leader, quindi passa
  da ~70 s a ~278 s: **+208 s = +2.7 giri** sulla proiezione del Player;
- **cinque tick su 795** superano 35.05 — uno al giro 19, quattro al giro 23. Sono quelli;
- l'isteresi asimmetrica poi **tiene il picco per tre giri**. E' esattamente lo "stato assorbente"
  descritto dal report esterno.

Quindi: **un solo campione sbagliato per gara**, amplificato da un filtro che non sa scendere.
Due difetti gia' registrati (Y-38/Y-40 per l'outlier, e l'isteresi per la tenuta), non uno nuovo.

### Come verificare

```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
```
```bash
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```
Atteso: exit code `0`, **219 `[PASS]`** (erano 212).

### Stato
- ✅ Compila — 0 errori · ✅ 219 test passano
- ✅ Regressione ADR-004, **due neutralizzazioni separate**: togliendo la densita' il test diventa
  rosso con `ottenuto 2,400`; rimettendo il termine `J` sul denominatore, rosso con `ne costa 0,829`

### Per chi entra — il percorso, in ordine

**Leggere prima:** `.ai/plans/2026-08-30-formule-corrette-fine-gara.md` (le formule corrette,
trascritte dalle figure del PDF: nel testo del PDF **non ci sono**, sono immagini) e
`.ai/plans/2026-08-30-analisi-dahldesign.md`.

Restano cinque voci della lista concordata con l'utente, gia' in ordine di rischio:

| # | cosa | perche' adesso |
|---|---|---|
| 3 | `Δt_cur` dal **timestamp** dell'ultimo passaggio, non dalla posizione | Toglie l'aliasing da campionamento a 3 s |
| 4 | Bandiera = **minimo su tutta la classe veloce**, in modalita' ombra (solo a log) | Dissolve Y-38. Ombra perche' dipende dalle soste avversarie, che stimiamo male |
| 5 | **Hampel + MAD + change-point** al posto della finestra percentuale | Rimedio alla trappola auto-bloccante di Y-32 |
| 6 | **Alpha-Beta** al posto dell'isteresi asimmetrica | E' cio' che ha tenuto il 37 per tre giri |
| 7 | **Base Pace + reiniezione della massa** giro per giro | Il piu' delicato. Da fare per ultimo |

Piu' **Y-44** (nuovo oggi): il *valore* del tempo di sosta e' sovrastimato del 49%.

**Il mio consiglio sull'ordine:** fare **6 prima di 4-5**. L'outlier del passo leader esiste e
continuera' a esistere finche' non si affronta Y-38, ma un filtro che sa scendere lo **assorbe** —
mentre oggi lo amplifica per tre giri. E' la correzione col rapporto valore/rischio migliore fra
quelle rimaste, e non dipende da nessuna delle altre.

**NON toccare:** il coefficiente `FuelWeightCoef` nelle impostazioni (resta `0.03`, ora in s/kg — la
conversione e' nel codice). E la formula di `TimeUntilLeaderCheckered`: e' identica a quella del
report esterno **e** a quella di DahlDesign, tre fonti indipendenti concordi.

**Attenzione a:** il totale converge sempre al valore giusto a fine gara, perche' la parte proiettata
si riduce a zero. "Il numero finale e' corretto" **non e' mai** una prova che il calcolo sia giusto.
Il report esterno propone come validazione l'RMSE misurato a orizzonti fissi (20/15/10/5/2 minuti
dalla bandiera) con **decrescita monotona** obbligatoria — se l'errore a 10 minuti e' peggiore che a
20, qualcosa e' rotto. Non l'abbiamo ancora implementato ed e' il modo giusto per chiudere la
partita.

---

## [2026-08-30 21:30] claude → revisione esterna (Gemini) prima di continuare

**Task:** Fasi 0 e 1 del piano `2026-08-30-proiezioni-su-tempi-misurati.md`, poi **stop deciso
dall'utente**: l'impianto va rivisto da fuori invece di continuare a correggere sintomi.
**Commit:** `50518a0` (lock) · `0cb0e93` · `bfc2aed` · questo
**Log analizzato:** `Logs/Road Atlanta/SimRIG_DebugLog_20260830_140721.csv`

### Perché ci si ferma qui

In cinque giorni: Y-31, Y-32, Y-35, Y-39, Y-41. Ogni correzione era giusta e verificata, e ognuna
ne ha scoperta un'altra sotto. La proiezione del Player e' accurata **dopo la sosta** (34.81-34.85
contro un vero 34.83); quella del leader parte corretta (`38.831` contro i `38.8` del software di
riferimento) e deriva a 39.5-40. L'utente ha chiesto — a ragione — di far rivedere l'impianto da un
altro modello invece di proseguire a tentoni.

**Il prompt per la revisione esterna e' in `.ai/plans/2026-08-30-prompt-per-revisione-esterna.md`.**
E' autoconsistente: descrive il problema fisico, non il nostro codice, e non richiede accesso al
repository.

### Fatto in questo turno

**Fase 1 — `MaxTank` per vettura** (`0cb0e93`). Corretto: il record traccia+classe non sovrascrive
piu' il valore per modello. **Ma vedi la correzione qui sotto sull'impatto reale.**

**Fase 0 — strumentazione** (stesso commit, nessun cambio di comportamento): `Opponent Pace Sources`
(media grezza e normalizzata affiancate per vettura), `P_Pace` / `P_LeftPre` / `P_LeftPost` /
`RaceLife` in `Race Projections Update`, e `usato=` / `recClasse=` / `inDb=` in `Opponent BoP Loaded`.

### Tre misure, e un mio errore da non ripetere

**1. Il bias della normalizzazione, misurato per classe** (era una stima su due campioni):

| classe | media grezza | media normalizzata | delta | bias |
|---|---|---|---|---|
| Dallara P217 (LMP2) | 73.65 s | 71.53 s | 2.11 s | **2.9%** |
| GTP | 74.60 s | 73.00 s | 1.61 s | **2.2%** |
| IMSA23 (GT3) | 77.98 s | 77.40 s | 0.57 s | **0.7%** |

Il 2.2% del leader su 38.8 giri fa **+0.85 giri**, coerente con la deriva osservata. La GT3 — la
classe del Player — ha il bias piu' basso di tutti: ecco perche' il Player sembrava a posto.

**2. La correzione per la sosta toglie 1.25 giri prima della sosta e 0 dopo.** La mia ipotesi (che
annullasse il bias di normalizzazione) era **sbagliata**: sono grandezze diverse e non si
compensano. Ma la misura ha mostrato altro: `PosAtFlag` del Player vale 33.75-34.37 **prima** della
sosta e 34.67-34.85 **dopo**, contro un vero 34.83. **Dopo e' accurato, prima sottostima di circa un
giro.** La sosta reale e' costata 41.1 s = 0.53 giri, la correzione ne toglie 1.25. Ipotesi non
verificata: `playerPitLoss` somma il transito *intero* in corsia box, mentre la perdita vera e' il
transito **meno** il tempo che ci avresti messo passando in pista. Registrato, non corretto.

**3. ⚠️ Ho sovrastimato l'impatto di Y-41 due volte, e la lezione conta piu' del punto.** Avevo detto
"fino a 10 L", poi "fino a 60 L", leggendo dal log capienze di 100/104/110 L contro un record di
classe da 50. **Non avevo controllato a quale sessione appartenessero quelle righe.** I valori a
`BoP 1.000` sono tutti fra le 14:07:39 e le 14:08:09 — la **sessione pre-gara**. Alle 14:09:26.727,
un istante prima del via, il BoP si risolve a `0.500` e le capienze diventano BMW `50.0`, Ferrari
`52.0`, Mustang `55.0`, McLaren `55.0`. **In gara l'errore evitato e' 2-5 L, non 60.** La correzione
resta giusta nel principio; l'annuncio era sbagliato di un ordine di grandezza.

### Come verificare

```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
```
```bash
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```
Atteso: exit code `0`, **212 `[PASS]`** (erano 208).

### Stato
- ✅ Compila — 0 errori · ✅ 212 test passano
- ✅ Regressione ADR-004 su Y-41: facendo vincere sempre il record di classe il test diventa rosso
  con `Ferrari deve restare a 104 L, ottenuto 100,0`

### Per chi entra

**Prossimo passo: aspettare la risposta della revisione esterna.** La Fase 2 del piano (proiezioni
sui tempi grezzi) e' **pronta ma sospesa**: la direzione e' probabilmente giusta, ma prima di
toccare ancora le proiezioni vale la pena sapere se l'impianto complessivo e' quello corretto.

**Se si riprende la Fase 2**, una cosa emersa oggi e da non perdere: **il numero del Player dipende
dal passo del leader** attraverso `RaceLifeTimeLeftSec` (= countdown + frazione di giro del leader ×
passo del leader). Correggere il passo del leader lo rende piu' lento, il supplemento si allunga e la
proiezione del Player **sale**; correggere il passo del Player la fa **scendere**. Le due modifiche
si oppongono: vanno fatte insieme e misurate insieme, altrimenti si conclude il falso su entrambe.

**NON toccare:** il filtro di validita' dei giri e l'isteresi del totale, finche' non arriva la
revisione. Ricevono ingressi sani da poco e non sappiamo ancora se il problema si ripresenta.

**Attenzione a:** il totale del leader **converge sempre al valore giusto a fine gara**, perche' la
parte proiettata si riduce a zero. "Il numero finale e' corretto" non e' mai una prova che il
calcolo sia giusto — e' l'errore in cui e' facile cadere guardando la dashboard alla bandiera.

Restano aperti: **Y-33** (corsia box e calibrazione, il piu' grosso, indipendente da tutto questo),
**Y-34** (arrotondamento carburante, schema gia' approvato), **Y-36**, **Y-38**, **Y-40**, e il
difetto nuovo sulla correzione per la sosta descritto sopra.

---

## [2026-08-30 13:00] claude → prossimo turno: rigirare il replay, meglio se a 1x

**Task:** Y-32 — passo del leader sbagliato
**Commit:** `2afb7cf` (lock) · `9cbc01c` · questo
**Log analizzato:** `Logs/Road Atlanta/SimRIG_DebugLog_20260830_102220.csv`

### La cosa che vale la pena non riscoprire

Y-32 **non era la famiglia Y-17b**, come avevo scritto ieri. La misura non era sporca all'origine:
era *presa male*, sempre, per tutti gli avversari.

Il tempo sul giro degli avversari veniva cronometrato per campionamento — `clock adesso − clock
all'ultimo cambio di giro`. Il traguardo si vede al primo tick **dopo** che e' avvenuto, quindi
l'errore vale fino a un tick per estremo. E un tick non e' un istante: e' quanto tempo di **gara**
scorre fra due letture. Misurato su 888 intervalli in questo replay: **3.0 secondi** (min 2.8, max
3.4), perche' girava a 3x.

L'incoerenza e' logica prima che numerica, ed e' la frase da ricordare:

> Fissiamo un riferimento con uno strumento **meno preciso** della tolleranza che poi pretendiamo
> con quel riferimento.

Un tick a 3x vale il 4.3% di un giro; la finestra di validita' dei giri e' −2%/+3.5%. A 1x un tick
vale l'1.4% e il sistema sarebbe coerente con se stesso.

Cascata completa sul leader `Kalyann Mey4`: giro reale 69.4 s → prima misura 62.95 (~2 tick corta)
→ baseline normalizzata 60.550 → finestra `[59.34, 62.67]` → giri veri (~66.6 normalizzati)
**tutti rifiutati per il resto della gara** → passo fermo a ~61 s → giri totali del leader 42-45
invece di 38.8. Il valore sbagliato non restava e basta: **definiva il criterio con cui si giudicava
se un dato fosse credibile**, e quel criterio escludeva la realta' che lo avrebbe corretto.

### Cosa e' cambiato

Si legge `Opponent.LastLapTime` dal gioco — la stessa fonte usata da sempre per il Player, e che il
plugin leggeva **gia'** per la classifica a schermo (`DataPluginDemo.cs:2617`). Solo il tracker se
lo cronometrava da solo. Le normalizzazioni (carburante, temperatura) sono invariate e si applicano
al nuovo numero grezzo.

**Il filtro di validita' non e' stato toccato**, su indicazione esplicita dell'utente: con la misura
giusta non ci si finisce dentro, e cambiarlo alla cieca rischiava di rompere altro. La trappola
resta latente — se un giorno una baseline dovesse comunque incastrarsi, il rimedio da valutare e'
quello ADR-005 (rifiuti tutti nella stessa direzione = e' il riferimento a essere sbagliato).

### Come verificare

```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
```
```bash
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```
Atteso: exit code `0`, **208 `[PASS]`** (erano 202).

### Stato
- ✅ Compila — 0 errori · ✅ 208 test passano
- ✅ Regressione ADR-004: invertendo la preferenza in `ResolveOpponentLapTime` il test diventa rosso
  con `ottenuto 62,95` — il valore che aveva avvelenato la baseline

### Per chi entra

**Prossimo passo, e' una misura.** Rigirare il replay Road Atlanta e controllare, in ordine:

| cosa | prima | atteso adesso |
|---|---|---|
| `L_Pace` (passo leader) | ~61 s | **~69.4 s** |
| `LatchedTotal` leader | 42-45 | **~38-39** |
| `PosAtFlag` (fascia p05-p95) | 1.26 giri | piu' stretta |

**Chiedere all'utente di girarlo a 1x se ha tempo.** Non e' pignoleria: a 3x un tick copre 3 s di
gara e falsa *tutte* le misure sugli avversari. Sapere quanto cambia fra 1x e 3x ci dice se in
passato abbiamo inseguito artefatti del metodo di test — sospetto di si', almeno in parte.

**NON toccare:** il filtro di validita' dei giri avversari (`isValidOppLap`, −2%/+3.5%) e il filtro
di stabilita' del totale giri. Entrambi ricevono adesso ingressi sani per la prima volta: vanno
osservati prima di essere ritoccati.

**Attenzione a:** il fallback al cronometro interno resta attivo quando il gioco non espone il
tempo. Se in qualche gioco/categoria `LastLapTime` non arrivasse, il difetto tornerebbe in silenzio
— vale la pena, prima o poi, loggare quale delle due fonti e' stata usata.

Restano aperti e non toccati: **Y-33** (corsia box, il piu' grosso — aspetta il JSON), **Y-34**
(arrotondamento carburante, schema gia' approvato), e le due incoerenze minori segnalate due turni
fa (`RaceAnalyzer.cs:935`, ramo `_leaderHasFinished`).

---

## [2026-08-29 17:00] claude → prossimo turno: rigirare il replay e misurare

**Task:** Y-35 (posizione del leader assente) e Y-32 (passo del leader)
**Commit:** `e05456d` (lock) · `66678e0` · questo
**Log analizzato:** `Logs/Road Atlanta/SimRIG_DebugLog_20260829_140004.csv` — primo replay girato con
la correzione Y-31 a bordo.

### Y-31 verificato sul campo, prima di tutto

Il replay serviva a misurare `ProjectedPosAtCheckered`, e la risposta è netta. **Il totale ora
torna indietro**: `36 → 35` al giro 17 e `37 → 35` al giro 25, cosa che prima era matematicamente
impossibile. Alla bandiera: proiezione **35**, giri realmente completati **35**, `PosAtFlag=34.828`.

Ma la misura ha mostrato che la stima grezza oscillava, e da lì è uscito Y-35.

### Y-35, corretto — il difetto più insidioso trovato finora

Su 797 campioni: p05 `33.797`, mediana `34.435`, p95 `34.833`. Una fascia di ~1.03 giri. Guardando
**dentro** un giro invece che al confine, non era rumore ma un **dente di sega**.

Il meccanismo, e vale la pena non riscoprirlo: la posizione del leader arriva a `0.0000` esatto per
giri interi (26% dei tick). Con la posizione ferma, dentro `TimeUntilLeaderCheckered` il countdown
**si semplifica algebricamente**:

```
tempo = timeLeft + (Ceiling(pos + timeLeft/pace) - pos - timeLeft/pace) * pace
      = pace * (Ceiling(posAtExpiry) - pos)        <- timeLeft sparisce
```

Il cronometro esce dalla formula e il tempo alla bandiera diventa una **scala a gradini di giri
interi del leader**. Nel giro 30 vale `429.352` per tre tick — 12.2 s di gara senza muoversi di un
millesimo — poi crolla a `368.016`. Congelato quello, si congela `playerL_left` mentre il Player
avanza: la proiezione sale 1:1 col pilota e cade al gradino.

**Tenere l'ultima posizione buona non sarebbe bastato** ed è il punto meno ovvio: una posizione
tenuta è comunque ferma e produce lo stesso congelamento. Va **fatta avanzare**.

### Y-32, riprodotto ma NON corretto — leggere prima di riprovarci

`Kalyann Mey4`: passo reale **68.5-70.5 s** (nove passaggi consecutivi), baseline **60.550**.
La logica di Y-17b letta in isolamento è corretta e non ho capito come venga aggirata. Dettaglio
completo e indizi in `PROJECT_STATE.md`, riga Y-32 — incluso il sospetto non verificato
(`Opponent BoP Loaded` che ricompare a gara in corso). **Non ho toccato niente**: senza meccanismo
dimostrato sarebbe stata una supposizione.

### Come verificare

```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
```
```bash
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```
Atteso: exit code `0`, **202 `[PASS]`** (erano 193).

### Stato
- ✅ Compila — 0 errori · ✅ 202 test passano
- ✅ Regressione ADR-004: azzerando il riconoscimento della posizione mancante dentro
  `ResolveLeaderAbsolutePos` il test diventa rosso con `ottenuto 32,0000` — il valore del log

### Per chi entra

**Prossimo passo, ed è una misura non un'implementazione:** rigirare il replay Road Atlanta con la
DLL nuova e confrontare la dispersione di `PosAtFlag` con quella di oggi (p05 `33.797`,
mediana `34.435`, p95 `34.833`, 10 tick su 797 sopra `35.05`).

```bash
grep "Race Projections Update" <DebugLog>.csv
```

Se la fascia si stringe molto, Y-35 era la causa dominante e Y-32 scende di priorità. Se resta
larga, il residuo è il passo del leader e Y-32 va affrontato **prima** di esporre qualunque
proprietà sui giri del leader.

**NON toccare:** la banda del filtro (`+0.05` / `-1.05`) e `IsLeaderSampleUsable`, che conserva
apposta la semantica di Y-24.

**Attenzione a:** il vincolo di `DeadReckonLeaderPos` al giro noto è ciò che tiene l'errore sotto il
giro **finché il passo del leader è sbagliato**. Quando Y-32 sarà chiuso quel vincolo resta utile ma
smette di essere l'unica rete. Restano aperte le due incoerenze già segnalate nel turno precedente
(`RaceAnalyzer.cs:935` clamp sul leader anche in multiclasse, e il ramo `_leaderHasFinished`).

---

## [2026-08-29 11:00] claude → prossimo turno: misurare la proiezione su un replay

**Task:** analisi della serata di test del 28/08 (Road Atlanta) e correzione Y-31
**Commit:** `1d75ad7` (lock) · `dbcb168` · questo
**Osservazioni dell'utente:** `Correzioni Post Test Pirpi/Test.txt` (fuori dal repo, non versionato)
**Log analizzati:** `Logs/Serata Test Pirpi/SimRIG_DebugLog_20260828_205434.csv` — contiene **due
sessioni**: la gara (20:59→21:47) e la practice di calibrazione (22:05→23:33, con cambio pista a
Hockenheim alle 23:17).

### Corretto in questo turno

**Y-31, il dente d'arresto sul totale giri.** Dettaglio completo in `PROJECT_STATE.md` e nel messaggio
di `dbcb168`. In sintesi: il filtro di stabilità è giusto, il chiamante gli passava un numero già
arrotondato e così la soglia di discesa (−1.05) diventava irraggiungibile per un calo di un giro
(che vale 1.00 esatto). Il totale saliva di uno e non tornava più indietro.

Tre cose che vale la pena non riscoprire da capo:

- **Il momento in cui il numero era giusto era il momento in cui due errori si annullavano.** Al
  giro 21 il totale mostrato è sceso a 35 (corretto). Ma il ramo di discesa restituisce
  `Math.Ceiling(raw + 0.05)`: per arrivare a 35 la stima grezza doveva essere a **34**. Cioè: un
  giro *in meno*, riportato sul valore giusto dal +1 del filtro.
- **La stima grezza balla di ±1 giro.** Ricostruita dalle transizioni del latch: 36 al giro 4,
  ≤34 al giro 21, ≥36 al giro 27. Togliere l'arrotondamento sblocca la discesa ma non toglie
  l'oscillazione. **Serve misurarla**, ed è il motivo per cui la proprietà nuova viene prima.
- **La stabilità osservata nei log vecchi era un artefatto.** Daytona `24→25→26`, Misano `22→25→26`,
  sempre e solo in salita. La nota su Y-19 è già aggiornata.

### Da dove ripartire — questo è il passo concreto

L'utente ha **un replay di Road Atlanta di una gara precedente** (non la stessa dei log analizzati).
Va rigirato con la DLL nuova per **misurare** `ProjectedPosAtCheckered` giro per giro. Cosa cercare
nei log risultanti:

```
grep "Race Projections Update" <DebugLog>.csv     # una riga per giro: P_PosAtFlag, P_Total
```

Domande a cui il replay deve rispondere, in ordine:
1. `P_PosAtFlag` converge verso un valore stabile o continua a oscillare di ±1 giro?
2. `P_Total` scende quando deve, adesso, o resta ancora appiccicato?
3. Nei giri centrali (passo stabile, niente soste) quanto vale lo scarto fra `P_PosAtFlag` e il
   totale reale della gara?

Se `P_PosAtFlag` oscilla ancora di un giro intero, il lavoro **non è finito**: significa che a
monte c'è un ingresso instabile, e il primo sospetto è Y-32 (il passo del leader entra nel calcolo
tramite `TimeUntilLeaderCheckered`, non è solo un numero da dashboard).

### Punti nuovi registrati, non affrontati

- **Y-32** — passo del leader sbagliato di 15 s, riapre la famiglia Y-17b. Bloccante per la
  proprietà "giri del leader" che l'utente ha chiesto, e ingresso del calcolo di Y-31.
- **Y-33** — `Player Spatial Pit Entry` scatta a ogni giro in pista (13 volte in gara con **una**
  sosta). Radice della geofence corrotta e dei drive-through mai confermati. L'utente vuole una
  revisione alla radice + un log dedicato alle calibrazioni.
- **Y-34** — arrotondamento di `FuelToAdd` all'intero. **Schema già approvato dall'utente**, pronto
  da implementare: AGGR per difetto, NORM al più vicino, SAFE per eccesso, con rete di sicurezza
  sul risparmio realizzabile.

### Come verificare

```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
```
```bash
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```
Atteso: exit code `0`, **193 `[PASS]`** (erano 186).

### Stato
- ✅ Compila — 0 errori · ✅ 193 test passano
- ✅ Regressione ADR-004 verificata: rimettendo `Math.Ceiling` dentro `ProjectPlayerTotalLaps` il
  test diventa rosso con `ottenuto 36` — il numero osservato in pista

### Per chi entra

**Prossimo passo:** far girare il replay Road Atlanta dell'utente e misurare `P_PosAtFlag`. Poi, in
base a cosa si vede, o Y-34 (indipendente, approvato, veloce) o Y-32 (se la stima balla ancora).

**NON toccare:** la banda del filtro (`+0.05` / `−1.05`) — è tarata e adesso riceve finalmente
l'ingresso giusto. Cambiarla prima di aver misurato significa inseguire un sintomo.

**Attenzione a:** `RaceAnalyzer.cs:935` applica ancora `Math.Min(playerTotal, leaderTotal)` **anche
in multiclasse**, in contraddizione con quanto documentato dieci righe sopra (Y-19). Non ha morso in
questo log (in multiclasse il leader di classe più veloce ha un totale più alto) e l'ho lasciato
com'era per non allargare la modifica, ma è un'incoerenza vera. Idem il ramo `_leaderHasFinished`
(`RaceAnalyzer.cs:657`), dove la posizione è integrale per costruzione e quindi il filtro resta
bloccato anche dopo questa correzione: a fine gara conta poco, ma il numero mostrato è ancora
appiccicato.

---

## [2026-08-28] claude → nuova chat sul portatile (nessun codice toccato)

**Task:** nessun lavoro sul codice in questo turno — diagnosi di un problema di replay corrotti, e
preparazione del trasferimento del progetto su un secondo PC ("portatile") per una sessione di test
in programma la sera del 27-28/08 con la persona che fornisce i replay.

**Nessun commit** (nessun file di codice toccato, quindi nessun lock preso).

### Cosa è successo in questo turno

**1. Tre replay Daytona nuovi sono risultati corrotti — diagnosticato, non risolto.** L'utente aveva
3 replay nuovi (`20260813_144908`, `20260813_205545`, `20260816_145833`, cartella
`Replay SimHub Pirpi\Non funziona\` sul suo PC, fuori dal repo) che bloccano il pulsante Replay di
SimHub. Analisi byte a byte (non il contenuto logico, i byte grezzi):

- Tutti e 3 hanno un buco di **~9.5-10 KB di zeri puri** vicino all'inizio del `.telemetry.json`
  (offset diversi per coincidenza di allineamento, ma stessa dimensione del buco).
- Il `.metadata` di tutti e 3 è illeggibile: invece del JSON atteso (`IsEmpty`, `StartDate`,
  `EndDate`, `CarModel`, `TrackName`, `Description`, `ScreenCaptureFrameCount`, `GameAndReader`,
  `Thumbnail` — schema confermato leggendo un file sano), contiene dati che sembrano un frammento
  base64 di un'immagine. Due dei tre file hanno **lo stesso identico contenuto sbagliato**, spostato
  di un byte — segno di un bug software (buffer riusato/non inizializzato), non di un guasto fisico
  casuale.
- **Confermato che la corruzione è già sulla chiavetta originale**, non introdotta dalle nostre
  copie: l'utente ha ricopiato un replay direttamente dalla chiavetta e ha riprodotto lo stesso
  blocco.
- **Tentativo di riparazione fallito**: copiare il `.metadata` di un file sano su uno rotto non ha
  sbloccato il Replay — atteso, perché il buco vero è nel `.telemetry.json`, non nel `.metadata`.
- **Ipotesi più probabile**: SimHub non ha finalizzato correttamente la registrazione (crash o
  chiusura brusca durante/dopo una sessione lunga), non un guasto della chiavetta. Cercato riscontro
  su GitHub/forum SimHub: nessuna issue chiusa descrive esattamente questo sintomo, ma
  [issue #883](https://github.com/SHWotever/SimHub/issues/883) conferma blocchi incostanti in
  caricamento di replay grandi, e un thread del forum del 2023 conferma che il formato `.telemetry.json`
  non è testo JSON e non è documentato nemmeno dalla community.
- **Non recuperabile con i mezzi disponibili.** L'utente ha chiesto altri replay alla persona che li
  ha registrati; test in programma stasera per procurarne di nuovi con attenzione a fermare la
  registrazione col tasto Stop dedicato (non chiudendo SimHub a forza).

**2. Chiarito lo stato dei punti aperti di Fase C — nessuno richiede multiclasse.** Y-14, Y-15 e la
Fase B (verifica undercut/overcut) non dipendono dal multiclasse: quello serviva solo ai punti già
chiusi (Y-16/17/19/24/25). Il replay Misano esistente (**uno solo**, catturato 3 volte per verifica
di ripetibilità: `SimRIG_DebugLog_20260823_095158/104939/133904`) copre già bene **Y-15**: sosta
unica al giro 19/20, `FuelAdded: 15.8L` (`Mode: StopAndGo`, nessun cambio gomme), finale con
~0.16 L in serbatoio (raccontato dall'utente a memoria, non ancora incrociato col log). **Y-14** è
coperto solo a metà (la sosta Misano è solo benzina, serve ancora una sosta con cambio gomme vero).
**Fase B** non ha ancora nessun replay utile: serve un vero undercut/overcut eseguito, con l'esito
reale raccontato dall'utente — atteso dai test di stasera.

**3. Preparato il trasferimento del progetto su un secondo PC.** Verificato nel codice: **nessun
percorso assoluto cablato** (cercato `C:\Users\...`, `Andreas`, `The Wheel Project` in tutto il
sorgente — compaiono solo in cache `.vs/` e in un attributo `[PluginAuthor]`, niente che dipenda dal
percorso). Il progetto può stare in qualunque cartella, con qualunque nome utente Windows. L'utente
sposterà la cartella su un portatile via chiavetta, in `Desktop\Antigravity2.0` (senza la sottocartella
`The Wheel Project`). Piano concordato per stasera:

- **Portatile** (dove gira Claude Code): SimHub (installazione locale, serve solo per le reference di
  build) + Visual Studio 2022 + variabile d'ambiente `SIMHUB_INSTALL_PATH` puntata lì + build della
  DLL (`User.PluginSdkDemoEdit\bin\Debug\User.PluginSdkDemo.dll` dopo il comando MSBuild in
  `CLAUDE.md`).
- **PC dell'amico**: quella DLL copiata a mano nella cartella principale di SimHub (dove sta
  `SimHubWPF.exe`, non in una sottocartella), replay `.json` per i test, e i Log generati durante la
  sessione — questi ultimi si creano **da soli** in `{SimHub}\Logs\SimRig Logs\`, nessuna cartella da
  preparare a mano.
- I Log generati stasera tornano sul portatile via chiavetta, dentro `Logs/` del progetto (cartella
  gitignored, va ricopiata a mano se il progetto viaggia via Git — qui viaggia via copia diretta,
  quindi non è un problema).

### Per chi entra (prossima chat, probabilmente sul portatile)

**Prossimo passo:** se stasera sono arrivati replay nuovi puliti, seguire l'ordine della roadmap
(prima Fase C — Y-14 sosta con cambio gomme, poi Fase B — undercut/overcut con esito raccontato
dall'utente). Se invece si riparte senza novità, il lavoro pronto e già autorizzato dall'utente è
**Y-15 sul replay Misano esistente**: confrontare `FuelToAdd` congelato all'ingresso corsia box coi
15.8 L reali versati e col residuo finale ~0.16 L, seguendo ADR-004 (funzione pura, test coi numeri
veri, verifica che il test fallisca senza il fix).

**NON toccare:** i 3 replay corrotti (`Non funziona\`, fuori dal repo) — sono considerati persi, non
c'è nulla da riparare lì dentro. Non serve nemmeno riprovare a copiarli.

**Attenzione a:** `HANDOFF_LOG.md` ha **23 voci**, il doppio del limite di 10 dichiarato in testa al
file — non tagliato in questo turno perché fuori scope, ma da fare in un turno dedicato prima che
diventi ingestibile.

---

## [2026-08-25 16:00] claude → nuova chat: analisi dei replay Daytona

**Task:** Y-30 — riscrittura delle frasi dell'ingegnere; poi cambio chat
**Commit:** `d409058` (lock) · `9c79ece` · `bb6a957` · `9b02f82`

### Fatto

Le frasi di calibrazione parlavano dal punto di vista del software ("ci servono dati per i calcoli",
tre volte su sette) e usavano gergo da menu (ALL4, FuelToAdd, DriveThrough). Riscritte in tono pit
wall: il gesto prima del motivo, e il motivo **operativo** ("cronometro i meccanici").

Tre scelte che vale la pena non rifare da capo:

- **Frasi neutre, apertura separata.** La cascata salta i passi già noti, quindi *qualunque* passo
  può essere il primo: un "ora" o un "ultima" suonerebbe come se ci fosse stato qualcosa prima.
  Le frasi sono scritte senza parole di continuità, e una sola apertura (`CALIB_INTRO`) le
  contestualizza al primo annuncio — una chiave in più invece di due versioni di ognuna.
- **Verbi variati** sul gesto che spetta all'utente (seleziona e conferma / chiedi / manda la
  richiesta / passa la richiesta): ripetere "imposta e invia" a ogni passo ricadeva nello stesso
  automatismo che si voleva evitare.
- **Solleciti progressivamente più asciutti** (`_R1`, `_R2`) invece della stessa frase ripetuta.
  Ripetizioni scese da 3 a 2, cioè tre annunci in tutto per passo.

`CALIB_SG_REQ` rimosso: orfano dalla vecchia catena di `if`, la cascata non chiede più stop and go.

### Il difetto che conta ricordare

**Il progetto ha SETTE lingue** (EN, IT, DE, ES, FR, NL, PT), non tre. Le chiavi introdotte con la
cascata il giorno prima erano finite solo in EN/IT/DE. `GetPhrase` restituisce `""` per una chiave
assente e `TriggerRadioVoice` esce subito: un utente spagnolo, francese, olandese o portoghese non
avrebbe sentito **nulla** su metà dei passi, senza nessun errore visibile.

C'è ora un test che verifica ogni chiave in ogni lingua e nomina quale manca dove. Ne ha trovato
subito un secondo mentre lo scrivevo: `CALIB_NEED_LAP` non seguiva la convenzione `_REQ` delle
altre, quindi `VoiceKeyFor` ne componeva una inesistente — anche quella silenziosa.

**Chi aggiunge una frase deve metterla in tutte e sette.** Non è ovvio guardando il file: i blocchi
lingua sono lunghi e distanti fra loro.

### Come verificare

```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
```
```bash
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```
Atteso: exit code `0`, **186 `[PASS]`**.

### Stato
- ✅ Compila — 0 errori · ✅ 186 test passano
- ✅ Regressione verificata rimuovendo una traduzione portoghese: il test nomina chiave e lingua

### Per chi entra — il lavoro proposto per la chat nuova

**Analizzare i replay Daytona senza soste anomale**, già copiati dall'utente nella cartella Replay
di SimHub. Servono a chiudere due punti fermi da giorni:

| punto | cosa serve dal replay |
|---|---|
| **Y-15** | Una gara **senza** le due soste finali anomale, per confrontare `FuelToAdd` consigliato e litri realmente imbarcati a fine gara |
| **Y-14** | Una sosta **pulita** — solo gomme, oppure solo benzina — per il tempo gomme |

Sul carburante sappiamo già cosa cercare: la raccomandazione è congelata quando parte la macro, ma
il consumo continua fino a che l'auto si ferma. Nel replay del 23/08 erano ~2 L su 32, ed è
esattamente il carburante del giro d'ingresso.

**La cascata di calibrazione (Y-28/Y-30) è completa ma mai girata in una Practice reale.** L'utente
conta di provarla nel weekend. Non serve toccarla nel frattempo.

---

## [2026-08-25 12:00] claude → prima sessione di Practice reale

**Task:** Y-28, cascata di calibrazione guidata — tutte le fasi del piano
**Piano:** `.ai/plans/2026-08-25-calibration-cascade-implementation.md`
**Commit:** `e4a4ae8` (lock) · `490902a` · `449cf88` · `dd16814` · `d0a692f`

Un commit per blocco di fasi, ognuno reversibile da solo.

### Cosa c'è adesso che prima non c'era

L'ingegnere **guida** invece di limitarsi a segnalare. Prima diceva "manca il dato X" una volta
sola, da fermo in piazzola, e taceva. Adesso: giro genuino → drive-through → sosta solo benzina →
gomme All4, poi 2, poi 1 — saltando quello che è già noto.

Due moduli puri separati apposta, perché sono due decisioni diverse:
`CalibrationCascade` decide **cosa** chiedere, `CalibrationCascadeRunner` decide **quando** dirlo.

### Tre cose che vale la pena non riscoprire da capo

**Il buco trovato ragionando sul flusso, non sul codice.** Il limite di velocità della corsia box
si imparava **solo** osservando gli avversari. In una Practice da soli in pista — cioè esattamente
la sessione in cui si calibra — restava a zero per sempre, e con lui il pavimento di plausibilità
sui transiti e la soglia adattiva di rilevamento pit. Ora si **legge** dal limitatore del Player
(`PitLimiterOn` era già letto per il colore del LED, ma mai esposto). Leggere batte dedurre: quando
il limitatore è inserito, la velocità *è* il limite.

**Il doppio guard sullo sfarfallio non è ridondante.** A 200 km/h un secondo vale 55 m, cioè 0.013
di giro a Misano: **sopra** la soglia di distanza di Y-23, che da sola lo lascerebbe passare. Il
criterio temporale lo prende. Il test di regressione verifica prima che il guard di Y-23 da solo
accetterebbe quel caso, poi che i due insieme lo rifiutano.

**L'ordine della cascata produce il consenso gratis.** Ogni passo attraversa comunque la corsia box,
quindi a cascata finita la geofence ha ricevuto almeno cinque osservazioni — ben più delle tre che
servono al consenso — senza chiedere al pilota un solo giro in più. Era il punto 9 della proposta
dell'utente, e ha risolto da solo la domanda "una calibrazione guidata vale subito o servono tre
passaggi?": non serve decidere, la cascata li produce.

### Come verificare

```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
```
```bash
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```
Atteso: exit code `0`, **182 `[PASS]`** (erano 154).

### Stato
- ✅ Compila — 0 errori · ✅ 182 test passano
- ✅ Regressioni verificate neutralizzando: la mediana del consenso, l'ordine dei passi gomme
- ⚠️ **Mai girato in una Practice reale.** Tutto verificato in isolamento.

### Per chi entra

**Il prossimo passo è una sessione di Practice vera**, su un circuito o una classe mai calibrati.
Cosa guardare nel log RADAR (serve `EnableLogRadar` acceso):

| evento | significato |
|---|---|
| `Calibration Step Announced` | quale passo, quale chiave vocale, a che giro |
| `Pit Speed Limit Observed (Player Limiter)` | il limite letto dal limitatore — **mai visto prima**, è nuovo |
| `Tyre Multiplier Calibrated (2 tyres)` / `(1 tyre)` | i moltiplicatori misurati invece che assunti |
| `Pit Visit Discarded (Implausible)` | il doppio guard ha scartato uno sfarfallio |

Sulla dash c'è una proprietà nuova, `SimRIG.Pit.CalibrationStep`, con l'istruzione corrente in
chiaro.

**Attenzione a una cosa non verificata**: `SessionStateStatus` per una sessione di Practice non è
noto — il codice sa che vale 4 in gara e 3 in qualifica, ma per la Practice nessuno l'ha mai
misurato. Se la cascata non parte, è il primo posto dove guardare.

**Resta aperto Y-29** (apprendimento passivo senza consenso), registrato ma non toccato: non
interseca la cascata, che passa sempre dal ramo guidato.

---

## [2026-08-24 22:30] claude → prossima sessione (Y-26/Y-27 + apertura Y-28)

**Task:** chiudere le segnalazioni di Antigravity, e rispondere alla domanda dell'utente sulla
calibrazione in Practice.
**Commit:** `73a0618` (lock) · `c646d7b`

### Fatto

Y-27 chiuso, Y-26 chiuso a metà — il dettaglio è nella tabella di `PROJECT_STATE.md`. La parte
che **non** si chiude con un fix è diventata **Y-28**, ed è una decisione di prodotto.

### La domanda dell'utente, e la risposta ragionata

> *"Il valore di geofence ottenuto nelle calibrazioni in Practice deve essere letto 3 volte come in
> pista oppure basta una sola volta?"*

**Risposta breve: una volta basta, ma solo se il plugin sa che è una calibrazione guidata — e oggi
non lo sa.** È esattamente il nodo di Y-28.

Il ragionamento, perché la decisione resti tracciabile:

Il consenso a tre campioni (Y-21) non è nato per **precisione**. La precisione della misura è già
ottima: tre riproduzioni dello stesso replay a Misano hanno dato `PitExitPct` entro **0.65 metri**.
Un campione solo è quindi già accurato.

Il consenso è nato per **rigettare gli intrusi**: il campione `0.963` prodotto dallo sfarfallio di
`IsInPitLane` a Daytona, e il valore storico `0.1088` distante 148 m. Cioè osservazioni che *non
erano* misure della corsia box.

Ma quella famiglia di intrusi è già bloccata a monte da Y-23 (il guard sulla traversata): una
"visita" che non percorre almeno 0.01 di giro non arriva nemmeno al consenso. Quindi, in una
calibrazione **deliberata** dove il pilota percorre la corsia apposta, il consenso a tre sta
proteggendo da un rischio già coperto — e in cambio costringe a rifare la procedura tre volte.

C'è anche un precedente coerente nel progetto: `FuelFillRate` e `TyreChangeTime` si scrivono
`Confirmed` da **una sola** osservazione guidata (`PitRadar.cs`, rami SplashAndDash e TyreChange).
Il principio già adottato è *una procedura guidata è isolata per costruzione, quindi vale come
misura*. La geofence dovrebbe seguire la stessa regola.

**La differenza che rende la cosa non banale**, e per cui non l'ho implementata di mia iniziativa:
le procedure guidate esistenti si **autoverificano** (chiedi esattamente 20 L e controlli di averne
ricevuti 20). Un passaggio in corsia box non ha un controllo interno equivalente. Quindi propongo:

- una calibrazione guidata scrive **subito** `Confirmed`, senza attendere tre passaggi;
- se un passaggio **successivo** dà un valore fuori tolleranza, il plugin non lo scarta in silenzio
  ma lo **segnala**: è il sintomo che qualcosa non torna, e l'utente deve saperlo.

Resta il prerequisito tecnico: **serve che il plugin sappia di essere in calibrazione.** Oggi
`CalibrationMode` viene *dedotto* dalla firma della richiesta (20 L esatti → SplashAndDash, 0 L +
4 gomme → TyreChange), non dichiarato. Finché è così, non c'è modo di distinguere un passaggio
deliberato da uno incidentale — ed è la stessa ragione per cui la parte residua di Y-26 non si
chiude. Una modalità di calibrazione **esplicita** risolve entrambi.

### Come verificare

```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
```
```bash
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```
Atteso: exit code `0`, **154 `[PASS]`**.

### Stato
- ✅ Compila — 0 errori · ✅ 154 test passano
- ✅ **Regressione verificata** neutralizzando la mediana

### Per chi entra

**Il progetto ha cambiato fase.** Fino a qui il lavoro è stato *reattivo*: trovare e chiudere difetti
emersi dai replay. Da adesso serve un piano, ed è in
**`.ai/plans/2026-08-24-roadmap.md`** — leggerlo prima di decidere cosa fare.

**Il prossimo passo non è codice**: è la decisione Y-28 sulla calibrazione guidata, che sblocca sia
la parte residua di Y-26 sia il flusso Practice che l'utente ha chiesto.

**Nota sull'utente, importante per come comunicare:** non legge codice e non ha modo di validare la
matematica. Ha detto esplicitamente di sentirsi disorientato. Le conclusioni vanno date in italiano
comune, ancorate a **cosa vede lui** (un numero sulla dash, un annuncio vocale, un consiglio di
rifornimento), non alla struttura interna. Il suo contributo reale — e va chiesto — è procurare i
replay giusti e dire quando qualcosa nel gioco non torna con quello che il plugin mostra.

---

---

> Le voci precedenti sono state tagliate come previsto dal protocollo (si tengono gli ultimi 10 handoff).
> La storia completa resta in `git log`:
>
> ```bash
> git log --oneline --all -- .ai/HANDOFF_LOG.md
> ```
