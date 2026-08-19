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

## [2026-08-19 23:20] claude → tutti (solo verifica, nessun codice toccato)

**Task:** leggere il replay `20260819_230109` per verificare `GapJump`
**Commit:** documentazione soltanto — nessun lock preso, nessun file di codice modificato

### Y-12: stabile su una terza esecuzione ✅

| | `205004` | `221922` | `230109` |
|---|---|---|---|
| `STRATEGY_CHANGED` | 375 | 23 | **25** |
| violazioni del dwell | — | 0 | **0** |
| durata mediana di uno stato | 0.6 s | 44.6 s | 37.1 s |

23 e 25 su due esecuzioni della stessa gara: riproducibile, e ben dentro la finestra sana.

### Sintomi spariti, ma la verifica di `GapJump` è **inconcludente**

- `wasClamped=True`: **0** (erano 2)
- righe con `RelativePace` a ±10: **0** (erano 6)
- `RELATIVE_PACE_INVALIDATION reason=GapJump`: **0**

Il gate **non ha mai scattato**, quindi il merito dei sintomi spariti non è suo: il wrap
semplicemente non si è verificato. Nello stesso punto in cui colpiva (giro 24 sec=19 → giro 25
sec=0) il gap passa ora da −88.673 a −88.780, `gapDelta` 0.032 contro i 93.033 di prima. Il massimo
`gapDelta` dell'intera sessione è 1.045 s, contro una soglia di 46.5.

**Il difetto è intermittente**: è una race fra il campione macrosettoriale e il rollover del
contatore, con una finestra di pochi millisecondi. Lo conferma anche il numero di righe di
snapshot, che varia fra esecuzioni della stessa gara (1958 / 1473 / 1440): il campionamento non è
riproducibile.

Quindi: il gate resta giustificato dal difetto osservato e **è coperto dai test unitari con i
valori reali del replay**, ma la sua efficacia in gara non è ancora dimostrata. Non archiviare
Y-13 sulla base di questo replay.

### Altri riscontri

- `DeltaTimeTooLarge` ha scattato di nuovo una volta: quel gate lavora a ogni sessione.
- I due `instantRate` a due cifre residui sono **legittimi** (`gapDelta` 1.045 su `dt` 5.467, e
  0.339 su 3.133): variazioni di ritmo reali, nessun clamp, nessuna saturazione.
- Header `1.3.0` con `MaxGapDeltaFraction=0.5`, snapshot a 67 colonne.

### Per chi entra

**Prossimo passo:** il replay su un circuito diverso, che serve a Y-12 (sweep da rifare) e che
darà un'altra occasione a `GapJump` di scattare. Cercare
`RELATIVE_PACE_INVALIDATION reason=GapJump` nel log: se compare, il gate ha fatto il suo lavoro
su un caso reale e Y-13 si può valutare per la chiusura.

---

## [2026-08-19 23:00] claude → replay di verifica

**Task:** verifica in gara di Y-12, poi `GapJump` — gate sull'ampiezza del `DeltaGap`
**Commit:** `6f62dd4` (lock) · `ff480bd` (fix) · rilascio in commit dedicato

### 1. Y-12 verificato in gara ✅

Replay `20260819_221922`, **stessa gara** del precedente (26 giri, 39.9 min), quindi confronto diretto:

| | prima | ora |
|---|---|---|
| `STRATEGY_CHANGED` | 375 | **23** |
| al minuto | 9.39 | **0.58** |
| durata mediana di uno stato | 0.6 s | **44.6 s** |
| stato più breve | 0 s | **5.00 s** |
| violazioni del dwell | — | **0** |

**−94%**, e il numero atterra sopra il fondo scala di ~10 cambi realmente persistenti: l'isteresi
sta togliendo rumore, non segnale. Il minimo esatto di 5.00 s conferma che il dwell è vincolante.

`DeltaTimeTooLarge` ha scattato una volta su `deltaTime=20.267`, esattamente il caso patologico
del replay precedente. Tre sequenze di sosta su tre perfette.

**Nota utile per interpretare i log:** nello snapshot il candidato pre-dwell cambia 19 volte e la
decisione 23 — la decisione sembra cambiare *più* del candidato. È un artefatto del campionamento
(1 riga ogni 25 tick): il dwell sfasa i cambi nel tempo. Il numero autorevole è l'event log.
Ne segue però una cosa vera: **il grosso del lavoro l'hanno fatto le bande morte, non il dwell** —
se il dwell fosse il vincolo dominante la mediana sarebbe 5 s, non 44.6 s.

### 2. `GapJump`, difetto nuovo emerso dallo stesso replay

Restavano 2 `wasClamped=True`, ma di famiglia diversa. Al rollover del contatore giri
(lap 24 sec=19 → lap 25 sec=0) il gap salta di **un giro intero** e torna subito:

```
gapDelta= 93.033 | deltaTime=4.000 -> instantRate= 2160.4
gapDelta=-92.976 | deltaTime=3.800 -> instantRate=-2279.8
```

`deltaTime` è del tutto plausibile, quindi i due gate temporali non potevano vederlo: qui il
difetto è l'**ampiezza**. Il clamp conteneva il danno (±10 invece di 2160) ma lasciava il
`RelativePace` falso e saturo per 6 righe, ~9 s di gara.

Nello snapshot il gap risulta stabile a −88.7: il transitorio dura meno del campionamento a 0.9 s.
**Si vede solo nell'event log** — utile ricordarlo per i difetti futuri.

**Soglia scelta sui dati, non a occhio.** Distribuzione di `abs(gapDelta)` sul replay:

| p50 | p90 | p95 | p99 | poi | max |
|---|---|---|---|---|---|
| 0.073 | 0.313 | 0.373 | **0.540** | *(vuoto)* | **93.033** |

Due ordini di grandezza vuoti fra segnale e artefatto, quindi la soglia esatta conta poco.
`MaxGapDeltaFraction = 0.5` (mezzo giro) perché è il punto di mezzo naturale — oltre metà giro un
salto è più plausibilmente un wrap che un episodio di gara — ed è **già la convenzione usata per il
wrap del MergeGap** in `TargetStrategyManager.cs:701`. A Misano vale ~47 s: 87 volte il p99 del
segnale legittimo, metà dell'artefatto.

### Fatto

- `RelativePaceTracker.cs` — terzo gate, dopo il calcolo del `DeltaGap` e prima dell'`InstantRate`.
  Nuovo `MaxGapDelta` nel sample per il log.
- Nuovo reason `GapJump`.
- `RELATIVE_PACE_INVALIDATION` porta ora `gapDelta` e `maxGapDelta` nel payload, per **tutte** le
  cause: senza, un `GapJump` nel log non sarebbe diagnosticabile.
- Versione motore → `1.3.0`. **Snapshot invariato a 67 colonne.**
- `Test_EmaAndClamp` aggiornato: saturava il clamp con un `deltaGap` di un giro intero, che ora è
  respinto a monte e non ci arriverebbe mai. Usa ±2.0 s su `dt=10 s` — grande ma plausibile.

### Come verificare

```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
```
```bash
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```

Atteso: exit code `0`, **51 `[PASS]`**, di cui 16 nel blocco `Strategy Hysteresis Tests`.

### Stato
- ✅ Compila — 0 errori (resta il `CS0219` preesistente)
- ✅ 51 test passano
- ✅ **Regressione verificata**: con `MaxGapDeltaFraction = 0.0` il test fallisce con exit code `1`

### Per chi entra

**Prossimo passo:** replay di verifica. Attesi **zero `wasClamped=True`** e nessuna riga con
`RelativePace` a ±10 fuori da una situazione reale. A ogni cambio giro dovrebbe comparire un
`RELATIVE_PACE_INVALIDATION reason=GapJump` con `gapDelta` prossimo al tempo di giro — se **non**
compare mai, il gate non sta scattando e va capito perché.

**NON toccare:** i sei punti congelati in `PROJECT_STATE.md` (Y-1, Y-2, Y-3, Y-8, Y-9, Y-11).

**Attenzione a:** `GapJump` cura il **sintomo**, non la causa. Il gap che salta di un giro esatto
al rollover è un difetto del calcolo a monte (`posDiffLaps * refLapTime` con i due contatori
disallineati per un tick). Scartare il campione è corretto e sufficiente per il ritmo relativo, ma
lo stesso gap sbagliato viene usato **anche dai gate strategici**, dove non c'è nessun filtro. Con
l'isteresi attuale un singolo campione anomalo non riesce a ribaltare la decisione — la banda è
0.25 s e il dwell 5 s — ma la causa resta lì. Vale la pena aprirci un punto separato.

**Ancora aperto:** i valori di Y-12 sono calibrati su **due replay, entrambi a Misano**.
`PositionHysteresis` a 0.25 sta a un decimo dal cliff misurato a 0.35. Rifare lo sweep sul primo
replay su un circuito diverso.

---

## [2026-08-19 22:05] claude → replay di verifica

**Task:** Y-12 (isteresi dei gate strategici) + finestra di plausibilità del `deltaTime`
**Commit:** `caf64be` (lock) · `1fa7b15` (fix) · rilascio in commit dedicato

### Come sono stati trovati i valori

Non a occhio. Simulatore costruito sullo snapshot del replay `20260819_205004` e **validato
1958/1958 campioni** contro `UndercutViable` e `StrategyDecision` loggati: se il modello non
riproduce il baseline, lo sweep non vale nulla.

Tre livelli di dati, perché lo snapshot da solo inganna:
- **snapshot** (1 campione ogni 25 tick, ~0.9 s) → effetto delle bande morte
- **traccia degli `STRATEGY_CHANGED`** (piena risoluzione, ~27 Hz) → effetto del dwell
- **campi `failed=` degli `UNDERCUT_NONVIABLE`** → cause esatte, non campionate

Le cause vere sono `Position` 113 (60.1%), `Margin` 74 (39.4%), `Traffic` 1 (0.5%). Lo snapshot
suggeriva 42%/46%: **sotto-campionava il colpevole principale.**

### Fatto

- `StrategyGateHysteresis.cs` (**nuovo**, 208 righe, nessuna dipendenza SimHub come
  `RelativePaceTracker`) — `HysteresisLatch` (banda morta latching) + `DwellFilter` (permanenza
  minima) + il contenitore con le tre costanti.
- `TargetStrategyManager.cs:762` e `:818` — i due gate passano per il latch.
  `:840` — la decisione passa per il dwell; il candidato pre-dwell resta in `candidateDecision`.
  Reset dell'isteresi aggiunto a **tutti e quattro** i punti che già resettavano `_relativePace`.
- `RelativePaceTracker.cs` — finestra `[0.5, 2.0] × macrosettore nominale` (`refLapTime / 20`),
  quindi adattiva al circuito. Nuovi `MinDeltaTime`/`MaxDeltaTime` nel sample, per il log.
- Nuovo reason `DeltaTimeTooLarge`.
- `LogManager.cs` — snapshot **63 → 67 colonne** (in coda), header parametri esteso, versione `1.2.0`.

### I valori, con lo sweep che li giustifica

| parametro | valore | evidenza |
|---|---|---|
| `PositionHysteresis` | ±0.25 s | 0.20 → 4.09% disaccordo · **0.25 → 2.38%** · 0.30 → 2.59% · 0.35 → 11.69% e lag p90 da 5 s a **199 s** (cliff) |
| `MarginHysteresis` | ±0.15 s | 0.10 → 3.40% · **0.15 → 2.55%** · 0.20 → 2.72% · 0.25 → 5.30% |
| `MinimumStateDwell` | 5 s | piena risoluzione: 1 s −64% · 2 s −72% · **5 s −82%** · 10 s −87% |

"Disaccordo" = frazione del tempo di gara in cui la variante diverge da una verità ripulita
offline. Le config scelte scendono **sotto il baseline** (5.02%): tolgono rumore, non segnale.
Robustezza verificata ridefinendo la verità a 5/10/20 s — la classifica non cambia.

**Scartato:** filtro EMA sul gap a monte. A `alpha=0.2` il disaccordo sale al 16-17% e il lag p90
a 199 s. Era l'alternativa che avevo lasciato aperta nel turno precedente: i dati l'hanno chiusa.

### Come verificare

```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
```
```bash
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```

Atteso: exit code `0`, **48 `[PASS]`**, di cui 13 nel blocco `Strategy Hysteresis Tests`.

### Stato
- ✅ Compila — 0 errori (resta il `CS0219` preesistente in `ReplayBacktestIntegrationTest.cs:19`)
- ✅ 48 test passano
- ✅ **Regressioni verificate** neutralizzando i tre fix uno alla volta:
  `PositionHysteresis → 0.0`, `MaxSectorFraction → 1000.0`, `MinSectorFraction → 0.0`.
  Ognuna fa fallire il test corrispondente con exit code `1`.

### Per chi entra

**Prossimo passo:** replay di verifica. Attesi **~25 `STRATEGY_CHANGED`** per sessione invece di 375
(da ~34/min a ~2/min). Il fondo scala sono i 27 cambi realmente persistenti (≥10 s) misurati nella
traccia: se scendessero **sotto** ~10, l'isteresi starebbe mangiando segnale e i valori vanno
allentati. Confrontare `CandidateDecision` con `StrategyDecision` nello snapshot per misurare
quanto filtra il dwell.

Verificare anche che **non compaia più nessun `instantRate` a due cifre**, e che `RelativePace` non
resti incollato a ±10 a fine gara.

**NON toccare:** i sei punti congelati rimasti in `PROJECT_STATE.md` (Y-1, Y-2, Y-3, Y-8, Y-9, Y-11).

**Attenzione a due cose.**

1. **Lo snapshot è passato da 63 a 67 colonne.** Le nuove sono tutte **in coda**, quindi un parser
   che legge per indice le prime 63 continua a funzionare — ma il conteggio cambia, e i log vecchi
   non sono confrontabili colonna per colonna con i nuovi.

2. **I valori sono calibrati su una sola sessione, un solo circuito, e un target che domina
   la statistica** (371 flip su 375 sono `Egor Ogorodnikov3`). Sono solidi *su quei dati*. Un
   replay su un tracciato diverso può spostarli — in particolare `PositionHysteresis`, che a 0.25
   sta a un solo decimo dal cliff misurato a 0.35.

**Trappola incontrata, per chi ripete la verifica di regressione:** ripristinare un file da backup
con `Copy-Item` ne preserva il timestamp, quindi MSBuild salta la ricompilazione e i test girano
sulla DLL vecchia. Serve un `-t:Rebuild` o toccare il file.

---

## [2026-08-19 13:35] claude → replay di verifica

**Task:** log strategici scritti solo in gara + guida di lettura del motore
**Commit:** `d4a51ab` (lock) · `<fix>`

### Fatto

- `LogManager.cs:102` — `IsRaceRunning`: `STRATEGY_SNAPSHOT` e `STRATEGY_EVENT` vengono scartati
  quando `SessionStateStatus != 4`. Indicazione dell'utente, confermata dai dati: nel replay
  `20260819_125249` erano **71 righe di rumore su 1021** (59 in griglia con `SessionTime=-1`,
  12 post-bandiera con `SessionTime=0`), più una coda di campioni identici a sessione ferma.
  Con `_sessionState` nullo (test) non si filtra: senza telemetria non c'è stato da valutare.
- `LogManager.StrategyLinesSkippedOutsideRace` — contatore diagnostico delle righe scartate.
- `.ai/STRATEGY_ENGINE_GUIDE.md` (**nuovo**) — spiegazione in parole povere del motore, delle due
  misure di ritmo e di come leggere una sequenza di sosta. Referenziato da `CLAUDE.md`.

### Effetto collaterale atteso

Il gate chiude anche il terzo bug rilevato ieri: il delta di **−28.8 s su 146 s** nasceva nella
finestra post-bandiera del giro 26, dove il clock di sessione andava a zero. Fuori dalla gara quei
campioni non vengono più nemmeno prodotti. **Va confermato sul prossimo replay**: se ricompare un
`instantRate` a due cifre, serve comunque un tetto su `deltaTime`.

### Come verificare

```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
```
```bash
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```

Atteso: exit code `0`, **35 `[PASS]`**.

### Stato
- ✅ Compila — 0 errori
- ✅ 35 test passano

### Per chi entra

**Prossimo passo:** replay di verifica. Nel nuovo snapshot **nessuna riga** deve avere
`SessionTime <= 0`, e non deve esserci la coda di righe identiche a fine sessione.

**NON toccare:** i sette punti congelati in `PROJECT_STATE.md`.

**Attenzione a:** questo turno ha ristretto *cosa* viene loggato, non la logica strategica.
Y-12 (isteresi dei gate) resta il problema aperto con più impatto pratico.

---

## [2026-08-18 23:50] claude → replay di verifica

**Task:** header auto-riparanti + finestra di assestamento post-pit
**Piano:** —
**Commit:** `780df63` (lock) · `fe37bf6` (fix)

### 1. Header: la causa vera era un'altra

Il fix precedente non bastava. Dal replay `20260818_230037`: i file risultano creati alle **23:01:05**,
non alle 23:00:37 quando parte il plugin, e `SimRIG_DebugLog_*.csv` non esiste affatto.
Fra la costruzione del `LogManager` e la prima scrittura di dati passano ~28 secondi, e in quella
finestra **la cartella dei log era stata svuotata a mano** (pratica legittima dell'utente).

Il difetto era mio: `_snapshotHeaderOk` diventava `true` dopo la prima scrittura riuscita e non
ricontrollava più. I file ricreati da `AppendText` restavano senza header per tutta la sessione,
e il `DebugLog` non tornava affatto perché nessuno ci scriveva mai nulla.

Ora l'header è garantito **prima di ogni append** (`LogManager.cs:294,311,328,345`), non una volta
sola. `TryWriteHeader` era già idempotente — scrive solo se il file è assente o vuoto — quindi non
duplica nulla né tronca dati. Costo: un `File.Exists` + `FileInfo.Length` per ciclo da 500 ms, solo
a coda non vuota. Nessun impatto sul percorso a 60 Hz.

### 2. Assestamento post-pit

Nel replay il primo rate post-pit **saturava il clamp in tutte e tre le soste** (1 del Player,
2 del Target): il campione veniva preso mentre la vettura stava ancora rientrando, con balzi di
gap fino a +2.5 s in un macrosettore → `instantRate` 39-50 s/giro → clamp a ±10.

`_pitContaminatedSeed` (bool) diventa `_postPitSectorsToSkip` (int), reimpostato a
`RelativePaceTracker.PostPitSettlingSectors = 3` a **ogni** campione in pit. A ~4.7 s per
macrosettore sono ~14 s di assestamento. La costante è esposta nei model params del log.
`RELATIVE_PACE_POST_PIT_SEED` riporta ora `sectorsRemaining`.

### Come verificare

```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
```
```bash
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```

Atteso: exit code `0`, **34 `[PASS]`**.

### Stato
- ✅ Compila — 0 errori
- ✅ 34 test passano
- ✅ **Regressioni verificate**: rimettendo `PostPitSettlingSectors = 1`, il test fallisce con
  `REGRESSIONE: il primo rate post-pit satura ancora il clamp (instantRate=50,30)`. Ripristinato e riverificato.

### Per chi entra

**Prossimo passo:** replay di verifica. Nei nuovi log devono comparire header in testa a entrambi
i file strategy, e ogni sosta deve mostrare **3** `RELATIVE_PACE_POST_PIT_SEED` con
`sectorsRemaining=2,1,0` prima del primo rate.

**NON toccare:** i sei punti congelati in `PROJECT_STATE.md`.

**Attenzione a — nuovo debito da decidere (Y-12):** nel replay 450 `STRATEGY_CHANGED` in 11 minuti.
Le cause misurate sono `Position` (115) e `Margin` (107), non il traffico. Sono due soglie
attraversate di continuo: `SignedGapSeconds >= -0.5` e `CaptureMargin > 0`. Gli ingredienti di passo
sono lenti (`NormalizedRaceStartPace` + `PaceDropDueToTyres`, gli stessi di `PaceDeficit`), ma
vengono combinati con il **gap istantaneo** e poi confrontati con zero senza isteresi.
`RelativePace` non è coinvolto: non entra in nessun gate.

---

## [2026-08-18 22:40] claude → dashboard

**Task:** RelativeGapDelta (s/macrosettore) affiancato a RelativePace + SessionTime guida nell'event log
**Piano:** —
**Commit:** `981e4d2` (lock) · `6f0c0ec` (feat) · rilascio in commit dedicato

### 1. SessionTime come base temporale guida dell'event log

Il session time c'era già, ma in seconda posizione dietro l'orologio di sistema. Con i replay
riprodotti in accelerato il wall clock non è una base temporale utilizzabile, e soprattutto **non
correla** con lo snapshot CSV, che è indicizzato su `SessionTime` in prima colonna.

Nuovo formato (`LogManager.cs:154`): `sessionTimeLeft | lap | wallClock | EVENT | payload`
Il wall clock resta in terza posizione per incrociare col log di SimHub. L'indice del nome evento
resta il quarto campo, quindi i parser esistenti non cambiano.

### 2. RelativeGapDelta — transizione compatibile, non sostituzione

`SimRIG.Target.RelativePace` **resta invariato**: stessa unità (s/giro), stessa EMA, stesso clamp.
La dash attuale continua a funzionare senza modifiche. Il nuovo valore lo affianca:

| Proprietà | Tipo | Note |
|-----------|------|------|
| `SimRIG.Target.RelativeGapDelta` | double | `SignedGap_current − SignedGap_previous`, **s/macrosettore** |
| `SimRIG.Target.RelativeGapDeltaStr` | string | `-0.20s/sector` / `+0.30s/sector` / `--.--s/sector` |
| `SimRIG.Target.RelativeGapDeltaValid` | bool | **da controllare sempre prima di leggere il valore** |

Registrate in `DataPluginDemo.cs:379-383`, aggiornate in `:1650-1652`.

- Popolato da `paceSample.DeltaGap`, che il tracker già calcolava: nessuna nuova matematica.
- Nessuna EMA, nessun clamp, nessuna normalizzazione temporale. Delta grezzo.
- `Valid=false` durante pit, seed post-pit, sequenza invalida, `dt < 1s`, cambio target, assenza
  di seed. In quei casi il valore numerico **conserva l'ultima misura buona** invece di azzerarsi:
  uno zero verrebbe letto come "nessuna variazione", che è un'affermazione falsa, non un'assenza
  di dato. Per lo stesso motivo `...Str` mostra `--.--s/sector`, non `0.00s/sector`.
- Protezione RED-1 intatta: `Valid` deriva da `RateComputed`, quindi nessun delta post-pit può
  usare un riferimento raccolto in pit. Coperto da test dedicati oltre a quelli esistenti.

Formato stringa in `TargetStrategyManager.FormatGapDelta()` — solo ASCII, verificato da test:
la dash può usare font senza glifi estesi.

### 3. Snapshot ed event log

- Snapshot: **62 → 63 colonne**, nuova `GapDeltaValid` subito dopo `DeltaGap`.
  Allineamento header ↔ array verificato: 63 = 63.
- `RELATIVE_PACE_UPDATE` e `RELATIVE_PACE_SEED` rinominano `deltaGap` in `gapDelta` e il SEED ora
  porta anche `prevGap`, `gapDelta`, `deltaTime`.

### Come verificare

```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
```
```bash
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```

Atteso: exit code `0`, **32 `[PASS]`**, di cui 5 nel blocco `Relative Gap Delta Tests`.

### Stato
- ✅ Compila — 0 errori
- ✅ 32 test passano, exit code `0`
- ✅ Formato event log e formatter HUD verificati su output reale

### Per chi entra

**Prossimo passo — dashboard.** Il consumo corretto è:
`if (RelativeGapDeltaValid) mostra RelativeGapDeltaStr else mostra il segnaposto`.
Non leggere `RelativeGapDelta` ignorando `Valid`: fuori dai macrosettori puliti è stantio.
Quando la dash sarà migrata, `RelativePace` potrà essere deprecato — **non prima**.

**NON toccare:** i sei punti congelati in `PROJECT_STATE.md`.

**Attenzione a:** `RelativePace` e `RelativeGapDelta` hanno **unità diverse** (s/giro contro
s/macrosettore) e ordini di grandezza diversi. Mostrarli con la stessa etichetta sarebbe l'errore
peggiore possibile: un delta di `-0.20s/sector` su 20 macrosettori è un ritmo ben più aggressivo
di `-0.20s/lap`.

---

## [2026-08-18 22:15] claude → tutti

**Task:** fix del logging strategico emerso dal replay `20260818_213214`
**Piano:** —
**Commit:** `37bd42a` (lock) · `e711e37` (fix + rilascio lock)

> Nota di protocollo: il rilascio del lock è finito **dentro** il commit di fix invece che in un
> commit dedicato. Deviazione mia, non del protocollo — la prossima volta due commit separati.

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
