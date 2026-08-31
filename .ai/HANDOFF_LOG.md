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
