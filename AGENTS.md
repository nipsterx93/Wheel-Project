# AGENTS.md — The Wheel Project / Antigravity 2.0

Plugin SimHub in C# / .NET Framework 4.8. Nel repository convivono due plugin:

- **`User.PluginSdkDemoRemastered/` — plugin nuovo (SimRIG Remastered), in costruzione. È il progetto attivo.**
  Design approvato: `.ai/plans/2026-09-15-remastered-spec.md`. Mappa dei moduli e decisione (ADR-007):
  `.ai/ARCHITECTURE.md`. Avanzamento: `.ai/PROJECT_STATE.md`.
- **`User.PluginSdkDemoEdit/` — plugin vecchio, congelato dal 2026-09-15.** Serve la dash finché il nuovo non lo
  sostituisce e fa da riferimento per l'analisi dei moduli. Accetta solo guasti bloccanti e l'interruttore che spegne
  undercut e overcut (spec §7.2).

> **Questo è il file delle regole, per qualunque agente.** Claude Code, Gemini/Antigravity, Codex,
> o una chat nuova senza memoria: valgono le stesse regole, e stanno solo qui.
> `CLAUDE.md` e `GEMINI.md` alla radice sono **puntatori a questo file**, non copie — esistono
> perché ciascuno strumento carica il proprio, ma non contengono regole proprie.
> **Se una regola cambia, si cambia in questo file e basta.**

---

## ⚠️ Prima di toccare qualsiasi file di codice

Vale per il codice di entrambi i plugin: `User.PluginSdkDemoRemastered/` e `User.PluginSdkDemoEdit/`.

1. Leggi **`.ai/PROJECT_STATE.md`** e controlla il blocco `LOCK`.
2. Se `owner` non è `NONE` e non è il tuo → **non scrivere codice.** Puoi leggere, analizzare,
   proporre un piano in `.ai/plans/`. Nient'altro.
3. Se `owner: NONE` → prendi il turno: aggiorna il blocco (owner, since, task, scope, expires),
   committa `[<agente>] chore: acquire lock — <task>`, poi lavora.
4. A fine turno: aggiorna `.ai/HANDOFF_LOG.md` (in cima), rilascia il lock, committa.

Col lock si fanno anche le **riorganizzazioni** di `AGENTS.md` e dei file condivisi di `.ai/`. Aggiornare handoff,
stato o un piano durante un turno di analisi non lo richiede.

Il lock è disciplina, non tecnologia. La rete di sicurezza vera è Git: **commit piccoli e frequenti**.

**`<agente>` è il tuo nome, sempre lo stesso**: `claude`, `antigravity`, `codex`, `human`. Serve a
distinguere chi ha fatto cosa quando le sessioni si alternano senza canale diretto tra loro — è
l'unico modo che ha il prossimo per sapere chi ha verificato un numero.

---

## Come si lavora sul plugin nuovo

### Il ciclo di un modulo

1. **Scelta del modulo**, con Andreas, nell'ordine dello spec §6.1.
2. **Brainstorming del modulo con Andreas.** Si decide cosa deve fare, cosa legge, cosa calcola, cosa mette a
   disposizione degli altri moduli, e la soglia con cui si valida (spec §6.2), fissata prima di scrivere codice.
   L'agente porta l'analisi: cosa fa il plugin vecchio, quali punti aperti e chiusi riguardano il modulo (tabella in
   `.ai/PROJECT_STATE.md`), quali casi limite sono già noti coi numeri. L'esito è la **scheda del modulo** approvata da
   Andreas.
3. **Implementazione:** test coi numeri dei log che partono rossi, codice fino ai test verdi, neutralizzazione come da
   ADR-004.
4. **Validazione** su Daytona e Road Atlanta, coi replay rigirati da Andreas e lo script di validazione del modulo.
5. **Chiusura:** handoff, riga aggiornata nella tabella Avanzamento; il modulo è disponibile ai successivi.

Un modulo non è chiuso senza il passo 4 su entrambi i circuiti. Tetto: tre cicli di validazione, poi ci si ferma e si
decide con Andreas (spec §6.3). Scheda e passi di implementazione stanno in un solo file per modulo:
`.ai/plans/<data>-remastered-<modulo>.md`. Il lock di un turno su un modulo ha per `scope` la cartella del modulo, i suoi
test e il suo script di validazione; un modulo può richiedere più turni, un commit per turno.

### Regole di semplicità

- Un modulo fa **solo** quello che dice la sua scheda approvata. Ogni aggiunta passa da Andreas.
- Niente logica "per il futuro": se oggi nessun modulo la usa, non si scrive. Vale anche per Assetto Corsa.
- Se un modulo non si spiega in poche righe, fa troppo: si torna al brainstorming.

### Chi fa cosa

Tutti fanno tutto: Andreas, claude e Antigravity possono proporre, implementare e rivedere. Il controllo di qualità
viene dal fatto che il lavoro di un agente è verificabile dall'altro, non da una divisione dei compiti. La scrittura
resta seriale col lock.

---

## Build e test — plugin nuovo

Ambiente: `SIMHUB_INSTALL_PATH` = `E:\SimHub\` — le reference alle DLL di SimHub si risolvono da lì.
MSBuild: Visual Studio 2022 Community.

```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoRemastered/SimRIG.Remastered.sln" -restore -p:Configuration=Debug -v:minimal -nologo
```

- La soluzione nasce al passo 0 (prova di fattibilità): fino ad allora non c'è niente da compilare.
- `-restore` serve ai progetti in formato SDK. Il percorso dell'eseguibile dei test si scrive qui al passo 0.
- Il runner dei test raccoglie tutti i fallimenti invece di fermarsi al primo, e conta a parte i test saltati.
- Come per il plugin vecchio, la build installa il plugin in SimHub: SimHub va chiuso prima.

## Build e test — plugin vecchio (congelato)

```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
```

**Attenzione:** il `.csproj` ha un post-build event che fa `XCOPY` della DLL in `%SIMHUB_INSTALL_PATH%`.
Ogni build **installa** il plugin nel SimHub reale. Conseguenze:
- Se SimHub è in esecuzione, la DLL è lockata e la build fallisce con errore di copia → chiudere SimHub.
- Non buildare "tanto per provare" senza sapere che si sta sovrascrivendo il plugin in uso.

Runner console custom (`TestRunner.cs`), **non** NUnit/xUnit. Il progetto di test è incluso nella solution `User.PluginSdkDemo.sln` (quindi viene compilato automaticamente con la build principale), oppure può essere compilato singolarmente:

```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/User.PluginSdkDemo.Tests.csproj" -p:Configuration=Debug -v:minimal -nologo
```

```bash
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```

Exit code `0` = tutto verde, `1` = fallito. Il runner **si ferma alla prima eccezione**: dopo un fix,
rieseguire per intero — un test che passa non garantisce che i successivi girino.

Aggiungere un test = metodo statico `RunAllTests()` chiamato da `TestRunner.Main` **+** il file
aggiunto al `<Compile>` di `User.PluginSdkDemo.Tests.csproj` (nessun glob: se non lo elenchi, non compila).

⚠️ **Il conteggio dei PASS va letto dall'output, non ricopiato da un documento.** Le cifre scritte a
mano vanno fuori sincrono (è successo: "186 test PASS" è rimasto in `PROJECT_STATE.md` per due
settimane mentre erano già 295). E attenzione a Y-54: il backtest sul replay reale **si salta in
silenzio** se il file non c'è, quindi lo stesso numero verde può nascondere una copertura diversa
su macchine diverse.

---

## Trappole di questo repo

### Plugin nuovo

- **Due plugin in SimHub insieme.** Il nuovo ha DLL, classe principale (quindi prefisso delle proprietà), impostazioni,
  database delle calibrazioni e log propri: tabella nello spec §7.1. Il file `SimRIG_Data.json` del plugin vecchio non
  si scrive mai.
- **`Core` non riferisce SimHub né iRacing.** Se a un modulo serve un tipo di SimHub o di iRacing, quel codice sta nel
  posto sbagliato: va in `Sims/IRacing` o in `Plugin`.
- **Progetti in formato SDK:** i file `.cs` si includono da soli. La trappola dei `.csproj` senza glob riguarda solo il
  plugin vecchio.
- **Orologio:** si usa il tempo di sessione crescente di iRacing. Nei log del plugin vecchio la colonna `SessionTime`
  contiene il tempo rimanente (spec §5.4).

### Plugin vecchio (congelato)

- **`*_LEGACY.cs` non sono compilati.** `DataPluginDemo_LEGACY.cs`, `FuelCalculator_LEGACY.cs`,
  `PitStrategyManager_LEGACY.cs` esistono su disco ma non sono nel `<Compile>` del csproj.
  Modificarli non ha alcun effetto sul plugin. Trattarli come archivio in sola lettura.
- **`CustomDialog.xaml.cs` idem**, ma senza il suffisso che avverte: è su disco e **non** nel
  `.csproj`, né come `Compile` né come `Page` (vedi Y-55).
- **`User.PluginSdkDemoBackup/` è una copia manuale obsoleta**, esclusa da Git. Non è il progetto
  attivo. Se una ricerca ci finisce dentro, è un falso positivo.
- **Nessun glob nei `.csproj`.** Ogni nuovo `.cs` va aggiunto a mano al `<Compile>`, altrimenti
  la build passa e il codice semplicemente non esiste.
- **File enormi:** `DataPluginDemo.cs` (~155 KB), `OpponentTracker.cs` (~105 KB),
  `SettingsControlDemo.xaml` (~99 KB). Leggerli a fette, mai in blocco.

---

## Convenzioni di codice

- Identificatori sempre in inglese. Commenti in italiano o inglese, coerenti col file.
- `PascalCase` tipi/metodi pubblici, `camelCase` locali/parametri, `_camelCase` campi privati.
- Seguire lo stile del file circostante quando diverge da quanto sopra.
- Reference esterne via `$(SIMHUB_INSTALL_PATH)`. **Mai** nuovi path assoluti hardcoded.
- Niente nuovi file `*_LEGACY.cs`: per archiviare, basta la history di Git.
- Nel plugin nuovo: un modulo per cartella, con contratti e qualità dei valori come nello spec §5.

---

## Git

- Tutti su `main`, serializzati dal lock (vedi `.ai/ARCHITECTURE.md` ADR-002). Niente branch per agente.
- Un commit per turno, prefisso agente: `[claude] fix: ...`, `[antigravity] arch: ...`, `[codex] review: ...`
- `main` deve restare compilabile: non rilasciare il lock lasciando il codice rotto.
  Se resta rotto, dichiararlo esplicitamente in `HANDOFF_LOG.md`.

---

## Come voglio le consegne

Nell'handoff, perché io (l'utente) sia efficace:
- **Percorsi file espliciti**, con riga dove serve (`OpponentTracker.cs:1420`), non "il tracker avversari".
- **Comandi esatti** di build e test da eseguire, non "testalo".
- **Scope esplicito**: cosa NON toccare in questo turno.
- **Criterio di successo osservabile**: cosa deve stampare/succedere se è andata bene.
- Link al piano in `.ai/plans/` se il task è complesso, invece di ridescriverlo nel log.

---

## Sessioni di revisione (una review invece di un'implementazione)

Chi apre questo progetto per **rivedere** un lavoro invece di continuarlo segue lo stesso protocollo
di chi implementa, non uno più leggero — cambia solo cosa succede quando trovi qualcosa.

1. **Ordine di lettura obbligatorio**: `PROJECT_STATE.md` (lock + sezione "Da dove partire"),
   poi `ARCHITECTURE.md` (mappa moduli + ADR — in particolare ADR-004 e ADR-005, che spiegano *come*
   si verifica un fix in questo repo e *perché* i dati calibrati hanno la forma che hanno), poi le
   ultime voci di `HANDOFF_LOG.md`.
2. **Una review che non tocca codice non ha bisogno del lock.** Ma non è mai silenziosa: quando finisci,
   scrivi comunque una voce in `HANDOFF_LOG.md` — anche solo "rivisto X, nessun problema trovato" è
   informazione utile per chi entra dopo. Un'analisi senza traccia scritta è tempo perso per tutti
   gli agenti successivi.
3. **Se trovi un difetto**: non correggerlo di nascosto durante la review. Registralo — un nuovo `Y-NN`
   in `PROJECT_STATE.md` se è un punto nuovo, un'annotazione su un ID esistente se ne mette in dubbio
   la chiusura — poi o lo implementi tu prendendo il lock come da protocollo normale, o lo lasci
   descritto per chi entra dopo. Le due cose non si accavallano mai nello stesso turno senza lock.
4. **Ogni claim verificabile, non impressionistico.** "Sembra corretto" non è una conclusione
   accettabile in questo repo — nemmeno da chi implementa (vedi ADR-004: un fix qui non è chiuso
   finché il suo test non fallisce senza di lui). Una review vale quanto i riferimenti che porta:
   comando esatto eseguito, `file:riga`, numero misurato invece di stimato.
5. **`Logs/` è in `.gitignore`.** I replay su cui si basano molte delle conclusioni registrate in
   `PROJECT_STATE.md` non sono nel repository — esistono solo sulla macchina dell'utente. Se la tua
   sessione non ha accesso a quel filesystem, dillo esplicitamente invece di dare per buono un numero
   che non puoi verificare, e chiedi all'utente i file se ti servono per confermare un claim specifico.
6. **Firma i tuoi ritrovamenti**: prefisso agente nel commit (`[codex]`, `[antigravity]`) e nella voce
   di `HANDOFF_LOG.md`, come già previsto per chi implementa.

---

## Protocollo di brainstorming e coworking fra agenti

Aggiunto il 2026-09-06, dopo un confronto diretto fra Claude e Antigravity su come lavorare insieme
su sessioni lunghe (design di formule, revisione di un piano, discussione di un difetto complesso)
senza che Andreas debba fare il portavoce copiando risposte da una chat all'altra.

1. **Niente ruoli esclusivi.** Vedi "Chi fa cosa" in questo file: ogni agente fa brainstorming, scrive
   codice, revisiona. Il controllo di qualità viene dalla revisione incrociata a turni alterni ("uno
   corregge l'altro"), non dalla specializzazione dichiarata.
2. **Il lock resta seriale, anche durante il brainstorming.** Non esiste uno stato "tutti scrivono
   insieme": due processi che editano lo stesso file senza un commit in mezzo si sovrascrivono a
   livello di filesystem, prima ancora che Git possa aiutare. Se serve iterare velocemente, si fanno
   turni brevi (prendi lock → scrivi 5-10 righe di ragionamento nel piano → commit → rilascia),
   non un turno unico più permissivo.
3. **L'esito di una sessione di brainstorming va scritto**, non solo discusso in chat: un file
   `.ai/plans/<data>-<argomento>.md`. È quello che permette alla sessione successiva (qualunque
   agente sia, aperta da Andreas senza che lui debba riassumere nulla) di ripartire dal punto esatto
   in cui si era arrivati — vedi anche `.ai/NEW_SESSION_PROMPT.md`.
4. **Niente inondazione di subagenti.** Un agente che delega a sotto-agenti per ogni piccolo passo
   crea più confusione di quanta ne risolva in un repository già coordinato da file. Sottoagenti solo
   dove il task lo richiede davvero (es. una ricerca ampia e isolata), mai come default.
5. **Il lock sul codice è ora tecnico, non solo scritto.** Un hook (`.claude/hooks/check-lock.js`,
   configurato in `.claude/settings.json`) blocca a livello di tool le scritture in
   `User.PluginSdkDemoEdit/` e in `User.PluginSdkDemoRemastered/` quando il lock in `PROJECT_STATE.md`
   appartiene a un altro agente, e blocca sempre le scritture in `Hardware/` (territorio di Andreas,
   vedi Y-53). Vale solo per Claude Code in questo momento — se Antigravity introduce un meccanismo
   equivalente, va documentato qui, non duplicato con logica diversa.

## Cosa si può concludere da quale macchina

Il progetto è **Windows-only**: build (MSBuild/VS2022 + `SIMHUB_INSTALL_PATH`), test e replay
girano solo lì. Ma si lavora anche da **macOS**, e la differenza va dichiarata invece che lasciata
intendere — una sessione su Mac che scrive "compila" sta inventando.

| Da Windows (con SimHub) | Da macOS / senza SimHub |
|---|---|
| build, test, conteggio PASS reale | lettura, analisi, review del codice |
| replay e verifica dei numeri in `Logs/` | documentazione, piani, ADR |
| chiudere un punto secondo ADR-004 | **proporre** un fix, non dichiararlo chiuso |

`Logs/` è gitignored: esiste solo sulla macchina dell'utente. Chi non ce l'ha **lo dice** invece di
dare per buono un numero che non può verificare, e chiede i file se gli servono.

---

## Tenere leggeri i file di stato

`.ai/` viene letto a ogni ingresso di sessione, da ogni agente. Il 2026-09-05 pesava **226 KB** —
oltre 50k token prima di aprire una riga di codice — perché due regole erano scritte ma non
applicate. Ora sono ~95 KB. **Mantenerli così fa parte del lavoro:**

- **`HANDOFF_LOG.md` tiene 10 voci.** Quando aggiungi la tua, sposta l'undicesima in
  `.ai/archive/HANDOFF_LOG_archive.md` (in cima, stesso ordine inverso).
- **Quando chiudi un punto `Y-NN`**, il testo lungo va in `.ai/archive/CLOSED_POINTS.md` e in
  `PROJECT_STATE.md` resta la riga d'indice (ID · titolo · esito · commit).
- **Non riassumere mai per accorciare: sposta.** Il ragionamento e i numeri misurati sono il valore
  di questo archivio; è il *caricarli sempre* che era sbagliato, non il conservarli.

---

## Riferimenti

- `.ai/NEW_SESSION_PROMPT.md` — **da incollare all'apertura di una chat nuova.** Ricostruisce il
  contesto senza dipendere dalla memoria della sessione precedente.
- `.ai/plans/2026-09-15-remastered-spec.md` — **il design approvato del plugin nuovo**: decisioni,
  contratti dei moduli, ordine dei passi, soglie. Ha preso il posto della roadmap, che ora è in
  `.ai/archive/2026-08-24-roadmap.md`.
- `.ai/PROJECT_STATE.md` — lock, avanzamento del plugin nuovo, legame fra i punti aperti del plugin
  vecchio e i moduli nuovi
- `.ai/HANDOFF_LOG.md` — passaggi di consegne (ultimi 10)
- `.ai/ARCHITECTURE.md` — ADR, mappa del plugin nuovo e di quello vecchio
- `.ai/plans/` — spec e un file per modulo del plugin nuovo; piani del plugin vecchio
- `.ai/reviews/` — revisioni con verdetto e riferimenti
- `.ai/STRATEGY_ENGINE_GUIDE.md` — come funziona il motore strategico **del plugin vecchio**, in
  parole povere
- `.ai/archive/` — **storia consultabile a richiesta, non da caricare a ogni sessione.**
  `CLOSED_POINTS.md` (il ragionamento completo dei punti chiusi, con numeri e commit),
  `HANDOFF_LOG_archive.md` (gli handoff oltre i 10 tenuti), `PLUGIN_VECCHIO.md` (punti aperti,
  indice dei chiusi, stato e debiti del plugin vecchio fino al 2026-09-14) e la roadmap del
  2026-08-24. Ci si va quando serve contestare una conclusione o ricostruire un turno vecchio — non
  all'apertura.
- **Skill di dominio `motorsport-telemetry-engineering`** — formule di fisica carburante,
  scomposizione pit stop, statistica robusta su campioni radi, e un avviso esplicito su un
  criterio di proiezione fine gara già misurato come sbagliato in questo progetto (Y-38). Copie
  gemelle in `.claude/skills/motorsport-telemetry-engineering/` (Claude Code) e
  `.agent/skills/motorsport-telemetry-engineering/` (Antigravity) — stesso contenuto, percorsi
  diversi perché i due tool cercano le skill di progetto in cartelle diverse. Se una copia cambia,
  l'altra va aggiornata di conseguenza.
