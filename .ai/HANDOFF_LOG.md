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

## [2026-08-24 21:10] antigravity -> chiunque prosegua il lavoro

**Task:** Esecuzione revisione indipendente (2026-08-24-cross-agent-review-brief.md)
**Piano:** `.ai/plans/2026-08-24-cross-agent-review-brief.md`
**Commit:** *nessun codice toccato, nessun lock richiesto*

### Fatto
Ho completato i tre livelli di revisione indicati nel brief:
1. **Verifica test:** Ispezionato i commit Y-25, Y-24, Y-23, Y-17 e Y-20/Y-21. I test associati usano le regole pure estratte dal codice di produzione, confermando che invocano la logica reale.
2. **Consistenza ADR-005:** Esaminati `PitRadar.cs` e `OpponentTracker.cs` per identificare campi non protetti. Ho registrato **Y-26** (per `PitTransitTime`, `PitDriveThroughTime` e `PitInOutAccDecTime`) e **Y-27** (per `BaseCapacity`) in `PROJECT_STATE.md`.
3. **Effetti collaterali non testati:**
  - **Y-16**: La rimozione del clamp al leader è sicura anche se i P1 passano a monoclasse. Il calcolo usa il countdown della sessione, senza rischi di stime assurde.
  - **Y-24/Y-25**: Se il leader esce dalla gara, e non vi è nessuno in testa (o la telemetria va a vuoto) il conteggio dei giri si ferma all'ultimo valido finché non torna, che è un comportamento accettabile e corretto per una vettura ritirata.
  - **Y-23**: 0.01 di giro (~250 m per una pista da 25 km come Nordschleife). Non dovrebbe scartare transiti veri, ma si deve verificare con log reali.

### Come verificare
Non c'è un comando di build necessario, i punti aperti sono in `PROJECT_STATE.md`.

---

## [2026-08-24 21:00] claude → chiunque riveda il lavoro (allineamento documentazione)

**Task:** allineare la documentazione al codice. Nessun file di codice toccato.
**Commit:** `eca0268` (lock) · questo

Emerso da una domanda dell'utente — *"se facessi rivedere il lavoro agli altri agenti, avrebbero gli
strumenti?"*. Verificato invece di assunto, e la risposta era **no**. Tre buchi, tutti miei:

1. **`ARCHITECTURE.md` fermo al commit di setup** (`f526cb3`, 18 agosto), 61 commit indietro. Nessuno
   dei **sette** moduli nuovi era nella mappa — non solo quelli recenti, anche
   `StrategyGateHysteresis`, `PitLaneDetector`, `TrackPositionValidator`, `GeofenceCalibrationGate`
   delle sessioni precedenti.
2. **Tre turni senza voce di consegna** (Y-23, Y-24, Y-25 e le verifiche sul campo): lock rilasciato
   senza scrivere l'handoff, contro quanto prescrive `CLAUDE.md`.
3. **`Y-22` duplicato** in `PROJECT_STATE.md`: la riga vecchia "aperto" era rimasta sotto quella
   nuova "risolto", in contraddizione.

### Fatto

- `ARCHITECTURE.md` — sezione **"Moduli di decisione, senza dipendenze SimHub"** con i sette file,
  cosa fanno e quale punto Y li ha generati; più l'elenco dei predicati statici estratti dentro i
  file grandi.
- `ARCHITECTURE.md` — **ADR-004** (un fix non è chiuso finché il suo test non fallisce senza di lui)
  e **ADR-005** (un campione singolo non è una misura). Formalizzano a posteriori i due criteri che
  hanno guidato tutte le correzioni: senza, la tabella dei punti Y si legge come un elenco di
  difetti scollegati invece che come un unico schema ricorrente.
- `PROJECT_STATE.md` — rimosso il duplicato, tabella riordinata per numero (era in ordine di
  inserimento, cioè al contrario), e aggiunta in testa la sezione **"Da dove partire"** con
  l'ordine di lettura e i comandi di verifica.

### Un dettaglio che vale la pena non perdere

ADR-004 nasce da un errore vero commesso su **Y-25**: il test scritto per quel fix riproduceva la
regola al proprio interno invece di chiamarla, e restava **verde anche neutralizzando il guard nel
codice di produzione**. Se ne è accorto solo il passo di neutralizzazione. È la ragione per cui quel
passo non va saltato: senza, avremmo chiuso un punto con un test che sembrava coprirlo e non copriva
nulla.

### Come verificare

```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
```
```bash
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```
Atteso: exit code `0`, **152 `[PASS]`**. Invariato — questo turno non tocca codice.

### Stato
- ✅ Solo documentazione · ✅ 152 test passano (invariati)

### Per chi entra

**Se devi riprendere il lavoro:** i punti aperti (Y-13, Y-14, Y-15, Fase 6) aspettano **dati**, non
sviluppo. Servono replay con caratteristiche precise, indicate in ciascuna voce di
`PROJECT_STATE.md`. I più utili: uno senza soste anomale a fine gara (Y-15), uno con una sosta
*pulita* — solo gomme o solo benzina (Y-14).

**Se devi rivedere il lavoro:** parti dalla sezione "Da dove partire" in cima a `PROJECT_STATE.md`.

**Attenzione a una cosa che non è nei file:** `Logs/` è in `.gitignore`, quindi i log dei replay su
cui poggiano quasi tutte le conclusioni **non sono nel repository**. Esistono solo sul disco
dell'utente (`Logs/3 Run Test/`, `Logs/Daytona Run/`). Chi rivede da un clone pulito non può
riprodurre le analisi: deve chiedere i file.

---

## [2026-08-24 00:10] claude → prossimo replay Daytona (passi 1-3 chiusi)

**Task:** Y-17b (causa vera del passo del leader), più esiti dei passi 2 e 3 del percorso
**Commit:** `0541bf1` (lock) · `e907b67` (Y-17b) · più i turni precedenti

### Il percorso: dove siamo

| # | passo | stato |
|---|---|---|
| 1 | Consenso sui dati calibrati (Y-20 + Y-21) | ✅ `e9caad6` + `1792bac` |
| 2 | Misano ×3 diagnostico | ✅ geofence ripetibile **sotto il metro**, 26 giri 3/3 |
| 3 | Daytona multiclasse | ✅ 26 stabile dal giro 5 alla bandiera |
| 4 | Y-19 — tetto leader di classe | ⏹️ **chiuso senza intervento**, i dati dicono che non serve |
| 5 | Y-12 — sweep isteresi | ⏸️ **sospeso**: il target latchato era diverso fra i replay, i numeri non sono confrontabili |
| 6 | Y-15 — confronto automatico `FuelToAdd` | ⬜ |
| 7 | Y-14 — tempo gomme per sottrazione | ⬜ serve una sosta pulita |
| 8 | Fase 6 — geofence stimate dagli avversari | ⬜ |

### Cosa hanno dimostrato i replay

**Y-16 validato nel modo più forte possibile.** A Daytona il passo del leader era *ancora* rotto
(56.410 s congelato per 9 giri) **e la proiezione era comunque giusta**: 26 dal giro 5 alla
bandiera, contro il 20 → 39 → 28 → 34.5 di prima del fix. È esattamente l'obiettivo
dell'ancoraggio al countdown: l'errore sul leader non scala più con la durata della gara.

**Y-18 confermato con un A/B sullo stesso replay.** Le due soste finali: `StatTime` da `0.0s` a
`41.7s` e `68.5s`, `FuelAdded` fantasma da `15.8L`/`12.7L` a `0.0L`, modalità da `DriveThrough` a
`StopAndGo`. I due sistemi ora concordano (41.7 contro 41.67 dello spaziale).

**Il consenso ha evitato una corruzione grave, su dati veri.** Al giro 16 un `IsInPitLane` che
sfarfalla (true→false→true in 0.2 s sul rettilineo) ha prodotto `sample=0.963` per `PitExitPct`.
La mediana ha tenuto `0.099`. Con il vecchio "l'ultimo che scrive vince" la zona box sarebbe
passata da ~800 m a **23 m**, azzerando il rilevamento pit.

**`FuelToAdd`:** da `#fuel 50l` (errore 18.4 L) a `#fuel 30l` contro 32 L realmente imbarcati.
Il residuo di ~2 L è il **carburante del giro d'ingresso**: la raccomandazione è congelata quando
parte la macro, ma il consumo continua fino alla sosta. Materiale per Y-15.

### Come verificare

```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
```
```bash
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```
Atteso: exit code `0`, **142 `[PASS]`**.

### Stato
- ✅ Compila — 0 errori · ✅ 142 test passano
- ✅ **Regressione verificata** neutralizzando il guard: il test accetta `56.4090000000001`, cioè
  esattamente la baseline che Sam Kuitert aveva preso nel log reale

### Per chi entra

**Y-17b va verificato sul campo.** Il fix è testato in isolamento ma non ancora su un replay: al
prossimo Daytona controllare che le GTP (classe 4029, entrano al giro 5-7) prendano baseline
intorno ai **100 s** invece che 56-89. Comando:
```bash
grep "Baseline Established" <DebugLog> | sed -E 's/.*;([0-9]+);.*Established;([^:]+): ([0-9.]+).*/lap \1 | \2 | \3/'
```
Atteso anche: `L_Pace` senza il crollo a 56, e `L_Rem` senza l'oscillazione 21.24 → 6.09.

**Attenzione a un debito emerso e non chiuso:** lo stesso sfarfallio di `IsInPitLane` del giro 16 ha
scritto `"PitDriveThroughTime": 0.666` nel database — un drive-through da 0.67 s non esiste. Non è
stato tracciato dove quel campo venga consumato.

**NON toccare:** Y-13 resta congelato (confermato race condition: `GapJump` 2/0/0 su tre
riproduzioni della stessa telemetria).

---

## [2026-08-23 15:10] claude → Misano ×2-3 diagnostico (passo 2 del percorso)

**Task:** Y-20/Y-21 — consenso sui dati calibrati. È il **passo 1** del percorso concordato.
**Commit:** `5505015` (lock) · `e9caad6`

### Perché questo passo per primo

Non per urgenza astratta: finché la scrittura è "l'ultimo che scrive vince", **ogni replay che
giriamo è anche un'occasione di corruzione del database**, e il dato corrotto inquina le verifiche
successive. Chiudere la falla prima di continuare a raccogliere.

### Il percorso concordato con l'utente

| # | passo | stato |
|---|---|---|
| 1 | Consenso sui dati calibrati (Y-20 + Y-21) | ✅ questo turno |
| 2 | **Misano ×2-3 di fila, diagnostico** | ⬅️ **prossimo** |
| 3 | Daytona con tutto in piedi | ⬜ |
| 4 | Y-19 — tetto sul leader di classe (*decidere se serve*) | ⬜ dipende da 3 |
| 5 | Y-12 — sweep isteresi su Daytona | ⬜ dipende da 3 |
| 6 | Y-15 — confronto automatico `FuelToAdd` | ⬜ |
| 7 | Y-14 — tempo gomme per sottrazione | ⬜ serve una sosta pulita |
| 8 | Fase 6 — geofence stimate dagli avversari | ⬜ |

### Come verificare

```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
```
```bash
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```
Atteso: exit code `0`, **133 `[PASS]`** (erano 125).

### Stato
- ✅ Compila — 0 errori · ✅ 133 test passano
- ✅ **Regressione verificata** neutralizzando la mediana a "l'ultimo che scrive": il test riproduce
  esattamente il fallimento reale (atteso 60, ottenuto 80)

### Per chi entra — il passo 2 in concreto

**Prima di ogni replay: copiare `E:\SimHub\SimRIG_Data.json` in `.ai/db-snapshots/`.** È l'unico
stato persistente che i replay modificano; senza copia gli esperimenti non sono ripetibili.
Lo snapshot post-Misano del 2026-08-23 è già lì.

Rigirare Misano 2-3 volte **con i log accesi** e guardare i nuovi eventi RADAR:

| evento | cosa dice |
|---|---|
| `Pit Entry/Exit Pct Calibrated` | ora riporta `median`, `sample`, `agreeing=N/M` e la confidenza |
| `Pit Entry/Exit Pct Held` | il consenso ha **rifiutato** la scrittura: il payload dice perché |

Domanda a cui il passo 2 risponde: **`PitExitPct` è ripetibile fra playback dello stesso replay?**
Se esce 0.074 tre volte su tre la misura è stabile e il sospettato diventa il vecchio 0.1088; se
balla, il problema è nella misura stessa. Non risponde invece a *quale sia il valore fisicamente
vero* — per quello serve una sosta diversa (Y-22).

**Attenzione:** il database ha `GeofenceConfidence: 3` (Confirmed) per Misano e Daytona. Con il
consenso nuovo, una sosta sola vale `EstimatedPlayer` e **non** può sovrascriverlo: nei prossimi
replay a sosta singola ci si aspetta `Pit Exit Pct Held`, non `Calibrated`. È il comportamento
voluto, non un difetto.

---

## [2026-08-23 12:40] claude → verifica sui replay (Misano prima, poi Daytona)

**Task:** Y-16/Y-17/Y-18 — proiezioni gara ancorate al countdown reale, "fermo ai box" unificato
**Commit:** `cb14f72` (lock) · `cc35f97` (Y-16+Y-17) · `cedc2aa` (Y-18)

### Da dove è venuta la diagnosi

Il primo replay Daytona (`20260823_095158`) aveva il `DebugLog` **vuoto**: i toggle di
`LogManager` sono `false` di default (`LogManager.cs:112-120`) e le checkbox della tab Debug
erano spente. Con i log accesi, il secondo replay (`20260823_104939`, gara completa fino alla
bandiera) ha reso visibile `Results.LeaderEstimatedPace` — ed era impazzito: `120 → 62.4 → 56.391`
congelato identico per 9 giri.

**Attenzione per chi legge i log**: ogni riavvio del plugin crea un **file nuovo** col proprio
timestamp; quello vecchio resta congelato. Non è più un file per modulo, è uno solo con la colonna
`Module`.

### Cosa è stato corretto

Dettaglio completo in `PROJECT_STATE.md`, Y-16/Y-17/Y-18. In breve: `RaceLifeTimeLeftSec` non si
ricostruisce più dai giri del leader ma si legge dal countdown di sessione, con il leader che pesa
solo sulla frazione di giro che gli manca per tagliare. L'errore sul passo del leader passa da
"proporzionale alla durata della gara" a "limitato a un giro".

### Come verificare

```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
```
```bash
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```
Atteso: exit code `0`, **125 `[PASS]`** (erano 111).

### Stato
- ✅ Compila — 0 errori (resta il `CS0219` preesistente in `ReplayBacktestIntegrationTest.cs:19`)
- ✅ 125 test passano
- ✅ **Regressioni verificate** neutralizzando un meccanismo alla volta (supplemento giro extra,
  dwell sull'identità, limite fisico): ognuno rende rosso un test diverso e specifico

### Per chi entra

**Prossimo passo: riprodurre prima Misano, non Daytona.** Il valore vero è noto (26 giri) ed è
monoclasse, quindi isola il fix dal multiclasse. Solo se Misano concorda si passa a Daytona.

Cosa guardare nel `DebugLog`, riga `Race Projections Update`:

| campo | atteso dopo il fix |
|---|---|
| `L_Pace` | stabile e plausibile per il circuito, niente salti a 56 s |
| `P_Rem` | decrescente e monotono, senza risalite a metà gara |
| `L_Rem + Lap` | vicino al totale reale già da metà gara, non solo nel finale |

**Y-19 lasciato aperto di proposito** (tetto sul leader di classe): serve la posizione assoluta
degli avversari e le convenzioni di conteggio giri di `GameReaderCommon.Opponent` non sono
verificate. Va tarato sui log Daytona già in `Logs/`, dove il totale vero è noto — cablarlo alla
cieca rischia un off-by-one che si propagherebbe silenziosamente in `FuelToAdd`.

**Attenzione a:** `PitRadar.cs` usa **TAB**. Un Edit con spazi fallisce il match — è costato un
tentativo anche in questo turno.

---

## [2026-08-20 03:15] claude → prossima chat (correzione + insight sulla sottrazione)

**Task:** nessuno — solo analisi su due osservazioni dell'utente, nessun codice toccato.

Due correzioni al turno precedente, entrambe confluite nell'aggiornamento di Y-14.

**Correzione su "non impareremo mai TyreChangeTime da un replay, punto".** Era falso in
generale: vero solo per un'analisi automatica non presidiata. Un replay *presidiato* — qualcuno
guarda la sosta e riproduce lo scope gomme a mano in tempo reale sui tasti volante — funziona,
perché il plugin non distingue quei tasti premuti mentre si guarda una riproduzione da quelli
premuti in gara. Resta comunque `EstimatedPlayer`, mai `Confirmed`.

**Insight dell'utente, verificato e più solido della mia proposta di ieri**: si può derivare
`TyreChangeTime` per sottrazione — `StationaryTime − RefuelingTime` — usando solo dati già
grondati in telemetria, senza mai leggere lo scope. Verificato che è **esatto solo per layout
Sequential**; per Simultaneous la sottrazione sottostima, serve un'uguaglianza diversa. Trovato un
fatto concreto e tempestivo: `IsPitLayoutSequential` (`PitRadar.cs:335`) presume Sequential per
ogni gioco **tranne iRacing**, e Simultaneous per iRacing finché non rilevato — se Daytona/IMSA è
iRacing, il default è già il caso sfavorevole alla sottrazione naive. Il meccanismo che
confermerebbe il layout richiede di conoscere già sia il tempo benzina sia quello gomme: stesso
uovo-e-gallina per una classe mai vista. Dettaglio completo in Y-14, `PROJECT_STATE.md`.

Non implementato: solo analisi, su richiesta esplicita dell'utente.

---

## [2026-08-20 03:00] claude → prossima chat (context window in esaurimento)

**Task:** nessuno — solo analisi, nessun codice toccato, nessun lock preso.

Due limiti sollevati dall'utente prima del replay Daytona, verificati nel codice e aggiunti come
Y-14 e Y-15 in `PROJECT_STATE.md`. Non sono difetti in quanto costruito stanotte — dicono **cosa si
può concludere da un replay** riprodotto senza un pilota dal vivo:

- **Y-14**: `TyreManager.CurrentScope` è pilotato solo dai tasti volante, mai dalla telemetria.
  In un replay resta a `None`. Conseguenza diretta sulla Fase 4 appena costruita: il ramo "solo
  gomme" di `ObserveNaturalPitStop` non scatterà mai durante l'analisi di stanotte, anche davanti
  a una sosta con cambio gomme vero. **Il lato benzina non è affetto** — `litresAdded` viene dalla
  variazione reale di `CurrentFuelLevel`, non dallo scope. Quindi dal replay Daytona: valutabile il
  lato benzina (se càpita una sosta pulita), non valutabile il lato gomme — non riportarlo come
  "non funziona", semplicemente non è testabile così.
- **Y-15**: manca un confronto automatico fra `SimRIG.Fuel.FuelToAdd` (la raccomandazione) e quanto
  viene realmente versato. Diverso dalla Fase 4 (quella impara la velocità di erogazione dai litri
  reali, non giudica il consiglio). Il dato grezzo c'è già nel log `Pit Complete`; manca solo
  l'abbinamento col valore predetto, da congelare al momento di `PitLaneEntered`.

Nessuno dei due blocca il replay di stanotte. Sono debiti registrati per non perderli nel passaggio
alla chat successiva.

### Per chi entra (nuova chat)

**Stato del lock:** libero (`NONE`), nessun turno in corso.

**Lavoro pendente più fresco:** Fasi 1-5 dell'apprendimento calibrazioni chiuse (turno precedente,
commit `9c8d638`…`9c3aa9b`), Fase 6 deliberatamente rimandata al replay Daytona. Quel replay non è
ancora stato analizzato in questa sessione — è il prossimo passo naturale.

**Leggere in ordine:** `CLAUDE.md` → `PROJECT_STATE.md` (lock, punti congelati incl. Y-13/Y-14/Y-15)
→ questo file dall'alto → `.ai/plans/2026-08-20-calibration-learning.md` se serve il dettaglio della
Fase 6.

---

## [2026-08-20 02:30] claude → replay Daytona (IMSA multiclasse)

**Task:** apprendimento autonomo delle calibrazioni, fasi 1-5 di 6
**Piano:** `.ai/plans/2026-08-20-calibration-learning.md`
**Commit:** `21a5167` (lock+piano) · `9c8d638` · `2c64469` · `467e8c7` · `b0dd7a7` · `9c3aa9b`

Un commit per fase, ognuno reversibile da solo.

### Il problema di partenza, verificato nel codice

1. **Si scriveva senza sapere da dove si arrivava.** `PitEntryPct` alla prima transizione utile di
   `IsInPitLane`, nessun filtro di sessione in tutto `PitRadar.cs`. Partendo dai box finiva
   registrata la posizione del **pit box**.
2. **Il primo valore vinceva per sempre.** `HasValidCleanSectorBounds()` verifica solo `!= -1.0`.
3. **Il carburante si imparava solo dal test guidato** a 20 litri esatti — una sosta reale non
   insegnava nulla.

### Fasi 1-3: quando è lecito calibrare

`TrackPositionValidator` (fase 1) distingue la guida dal teletrasporto per **plausibilità del
movimento**: un salto che il tempo trascorso non giustifica è un artefatto. Stesso principio di
`MaxSectorFraction` e `GapJump`. Scartate su indicazione dell'utente sia una soglia temporale
(aggirabile, e su pista nuova non c'è un tempo da cui derivarla) sia una soglia di percorso
(assume che l'ingresso box sia tardi nel giro — vero a Misano, non in generale).

`GeofenceCalibrationGate` (fase 3) sostituisce la sequenza `True→False→True` — che in gara non
parte mai, mancando il `True` iniziale — con **una condizione sola**: prima di fidarsi di un
ingresso, dev'esserci stato un campione genuino in pista in questa sessione. Copre entrambi i
casi: dai box si autorizza dopo l'uscita vera, dalla griglia si è già in pista e il primo pit stop
vale.

### Fase 2: gerarchia dei dati

`Unknown < EstimatedOpponent < EstimatedPlayer < Confirmed`, con regola unica `CanOverwrite`.
Una stima non può mai cancellare una misura. **Migrazione**: Newtonsoft applica i default ai campi
assenti, quindi senza intervento il database esistente nascerebbe `Unknown` e sarebbe sovrascrivibile
da una stima peggiore. `MigrateLegacyConfidence` promuove a `Confirmed` i dati già presenti; è
idempotente e non tocca le stime dichiarate.

### Fase 4: imparare da una sosta vera

`ObserveNaturalPitStop` accetta solo soste **inequivocabili** (benzina sola o gomme sole). In una
sosta mista il tempo fermo non si separa senza conoscerne già una parte — fuori scope per ora.
Verificato sull'esempio reale: 16 L in 6.1 s → **2.62 L/s**, esattamente il valore già nel database.

### Fase 5: dire cosa manca

`CalibrationStatus` copriva solo transito e carburante: un circuito con geofence non calibrate
risultava `READY`, proprio il caso in cui il rilevamento pit del Player perde colpi. Ora copre
quattro dati, elenca i mancanti e distingue `READY` da `READY (ESTIMATED)`.

### Come verificare

```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
```
```bash
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```

Atteso: exit code `0`, **111 `[PASS]`**.

### Stato
- ✅ Compila — 0 errori (resta il `CS0219` preesistente)
- ✅ 111 test passano
- ✅ **Regressioni verificate** su fasi 1, 2, 3 neutralizzando un fix alla volta

### Per chi entra

**Prossimo passo: il replay Daytona.** Il database è **vergine** per quel circuito, quindi si
vedrà la calibrazione avvenire da zero. Cosa guardare nel log RADAR:

| evento | significato |
|---|---|
| `Pit Entry Pct Calibrated` | il gate ha autorizzato — verificare che il valore sia plausibile |
| `Pit Entry Pct Calibration Skipped` | il gate ha bloccato — il payload dice perché |
| `Fuel Fill Rate Learned (Natural Stop)` | imparato da una sosta vera invece che dal test guidato |

Verificare anche `SimRIG_Data.json`: deve comparire un record per **ogni classe** IMSA incontrata,
con il proprio `PitLaneSpeedLimit`. È la prima sessione multiclasse, quindi la prima prova reale
del fix di ieri sull'apprendimento per classe.

**Fase 6 (stima geofence dagli avversari) rimandata di proposito.** È il fallback per chi salta la
practice, quindi la meno urgente; ma soprattutto la formula di reverse-engineering va tarata su
dati veri, e Daytona li fornirà. Costruirla prima significherebbe indovinare le soglie.

**NON toccare:** Y-13 resta congelato.

**Attenzione a:** `PitRadar.cs` usa **TAB**, non spazi. Un Edit con spazi fallisce il match
silenziosamente — è costato due tentativi in questo turno.

---

## [2026-08-20 00:30] claude → replay su circuito diverso

**Task:** sbloccare Y-11, Y-2, Y-9, Y-1 dopo le decisioni prese con l'utente
**Commit:** `74481ea` (lock) · `8b42efd` (Y-11) · `f08bf43` (Y-2) · `81a5f12` (Y-9) · `bc20a67` (Y-1)

Quattro punti congelati chiusi, due archiviati senza intervento. Un commit per punto, così
ognuno è reversibile da solo.

### Y-11 — gli outlap non entrano più nella media del degrado (`8b42efd`)

Il warmup era contato due volte: i giri su gomma fredda finivano in `LapHistory`
(`RaceAnalyzer.cs:1009`), alzavano `LapMovingAverage` e quindi `PaceDropDueToTyres`, e poi la
stessa lentezza veniva ri-sommata come warmup esplicito.

Il modello di warmup del Player misura `PlayerExtendedSectorRacingZone`, una zona attraversata
una volta per giro: `PostPitWarmupPenalties[0..2]` sono quindi **i primi tre giri post-pit**,
esattamente quelli che finivano anche nella media a 4.

`ActiveWarmupLaps()` conta il prefisso contiguo sopra soglia — lo stesso criterio del gate
overcut, così i due lati restano allineati per costruzione. La finestra si stringe da sola man
mano che le gomme entrano in temperatura. Rimosso il magic number `0.10`, che era ricopiato in
tre punti: ora è `RaceAnalyzer.WarmupThreshold`.

### Y-2 — `OvercutTrafficOK` implementato (`f08bf43`)

Le due strategie fanno domande diverse sul traffico, e il codice rispondeva solo a una:

| | domanda | misura |
|---|---|---|
| undercut | dove mi ritrovo all'**uscita** dai box | gap proiettato, meno il tempo perso ai box |
| overcut | chi ho davanti **adesso**, mentre spingo | gap istantaneo |

`PhysicalGapSeconds()` estratto e testato: il caso che lo rende necessario è il doppiato, che a
88 s di conteggio su un giro da 93 è fisicamente 5 s davanti — cioè il traffico più pericoloso
mentre spingi. Il Target non conta come traffico: è il bersaglio, non un ostacolo.

Snapshot **67 → 68 colonne**, nuova `OvercutTrafficGap` in coda.

### Y-9 — rilevamento pit del Player uniformato (`81a5f12`)

L'euristica era `IsInPitLane || (TrackPositionPercent > 0.85 && 10 < SpeedKmh < 100)`: nessuna
geofence, nessuna persistenza. `PitLaneDetector.cs` (nuovo, senza dipendenze SimHub) porta il
Player sulla stessa cascata degli avversari — telemetria, geofence, fermo, velocità persistente.
È il criterio geofence a chiudere il difetto: un tornante non è dentro la zona box.

**Soglie adattive:** la soglia non è più cablata a 80 km/h ma derivata dal `PitLaneSpeedLimit`
appreso, più 20 km/h di margine. Il limite più diffuso (60) produce esattamente 80, cioè il
valore storico: la formula generalizza il comportamento invece di cambiarlo.

**Bug corretto:** `UpdatePitLaneSpeedLimit` scriveva sempre nel record della classe del *Player*,
anche quando il limite veniva imparato osservando un avversario di classe diversa. In multiclasse
contaminava entrambi i record.

### Y-1 — avvisi selezionati invece che silenziati (`bc20a67`)

Il gate fondeva due condizioni sotto lo stesso `RejectReason`. Separate: `canFinishWithoutPitting`
sopprime l'undercut (con il suo `NoPitNeeded`) ma **non più l'overcut** — se io arrivo in fondo e
lui deve fermarsi, l'overcut non è rischioso, è già vinto. Era il caso che costava posizioni.

`FuelSaveTarget` con due filtri di fattibilità: taglio entro il 15% del consumo, e una sola sosta
residua. Senza il primo, il caso endurance segnalato dall'utente (100 giri, 25 litri) produrrebbe
un consiglio di 0.25 L/giro contro un consumo di 3.0 — un taglio del 92%, ineseguibile.
`PitRequiredNumber` **esisteva già** (`FuelManager.cs:191`) ed è il secondo filtro.

### Archiviati senza intervento

- **Y-3** (`LapsSinceLastPit` frazionario): imprecisione fino a un giro, trascurabile rispetto
  alla scala delle decisioni che governa.
- **Y-8** (deadband HUD): il dato resta com'è.

### Come verificare

```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
```
```bash
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```

Atteso: exit code `0`, **78 `[PASS]`**.

### Stato
- ✅ Compila — 0 errori (resta il `CS0219` preesistente)
- ✅ 78 test passano
- ✅ **Regressioni verificate** su tutti e quattro, neutralizzando un fix alla volta:
  esclusione warmup, filtro geofence, filtro di fattibilità del fuel saving. Ognuna fa fallire
  il test corrispondente con exit code `1`.

### Per chi entra

**Prossimo passo:** il replay su circuito diverso. Serve a **tre** cose ora:
1. rifare lo sweep di Y-12 (`PositionHysteresis` a 0.25 sta a un decimo dal cliff);
2. dare a `GapJump` un'altra occasione di scattare;
3. verificare che il rilevamento pit del Player non sia peggiorato — controllare che i pit del
   Player siano rilevati **tutti** e nei momenti giusti. Il rischio nuovo è il contrario di
   prima: se la geofence del circuito non è ancora calibrata il filtro spaziale si disattiva,
   ma se fosse calibrata *male* si perderebbero pit veri.

**NON toccare:** Y-13 resta congelato.

**Attenzione a — due valori stimati, non misurati.** A differenza delle bande di Y-12, questi
non vengono da uno sweep sui dati, perché nessun replay finora contiene il fenomeno:
- `OvercutTrafficWindowSeconds = 2.0` — serve un replay con overcut attivo e traffico vero;
- `MaxAchievableFuelSaving = 0.15` — serve un replay con una fase di fuel saving.
Sono documentati come tali nel codice. Non trattarli come calibrati.

**Copertura, detto onestamente:** i test coprono le funzioni di decisione estratte
(`ActiveWarmupLaps`/`IsLapExcludedFromDegradation`, `PhysicalGapSeconds`/`BlocksOvercut`,
`PitLaneDetector`, `ComputeFuelSaving`), non la loro integrazione nei metodi chiamanti, che
restano legati a stato SimHub non costruibile in test. Quel lato si verifica solo sul replay.

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
