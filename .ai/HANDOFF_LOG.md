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

## [2026-09-06 13:50] antigravity → chiunque entri dopo

**Task:** Comandi slash `/new-session` e `/handoff` registrati come Skill per Antigravity 2.0 (`.agent/skills/`)
**Piano:** — (allineamento formati comandi custom vs skill Antigravity 2.0)
**Commit:** `0d28aed`

### Fatto
- Creati `.agent/skills/new-session/SKILL.md` e `.agent/skills/handoff/SKILL.md` con frontmatter YAML conforme (`name` + `description`).
- In precedenza i comandi erano stati inseriti in `.agent/workflows/*.md` (vecchio formato legacy supportato da Gemini CLI in terminale, ma non esposto come slash command nell'UI di Antigravity 2.0). Le cartelle `.agent/skills/<name>/SKILL.md` abilitano sia il comando slash (`/new-session`, `/handoff`) nell'interfaccia chat sia l'invocazione automatica da parte dell'agente.
- Mantenute le versioni in `.agent/workflows/` e `.claude/commands/` per retrocompatibilità.

### Come verificare
- Digitare `/` nella casella di input della chat di Antigravity: i comandi `/new-session` e `/handoff` compaiono ora nell'elenco autocompletato.
- Invocando `/new-session`, l'agente esegue la procedura di bootstrap (lettura `AGENTS.md`, `PROJECT_STATE.md`, `HANDOFF_LOG.md`, `STRATEGY_ENGINE_GUIDE.md`).

### Stato
- ⏭️ Nessun codice C# modificato (infrastruttura / skill di ambiente)
- ✅ Git tree pulito

### Per chi entra
**Prossimo passo:** Continuare secondo roadmap (`.ai/plans/2026-08-24-roadmap.md`) o punto Y pianificato.
**NON toccare:** `User.PluginSdkDemoEdit/` era fuori scope in questo turno.
**Attenzione a:** Mantenere sincronizzati i file di bootstrap e handoff se ne viene modificato il contenuto (`.claude/commands/`, `.agent/workflows/`, `.agent/skills/`).

---

## [2026-09-06 13:30] claude → chiunque entri dopo

**Task:** Skill di dominio condivisa `motorsport-telemetry-engineering`, da una ricerca approfondita fornita dall'utente su fisica carburante, scomposizione pit stop, statistica robusta e filtraggio del passo. Nessun punto Y toccato: turno di infrastruttura/conoscenza, non di correzione codice — niente lock preso, nessun file in `User.PluginSdkDemoEdit/` modificato.
**Piano:** discusso in chat (brainstorming bounded, superpowers:brainstorming), non salvato come file separato — la sintesi sta in questa voce e nei file stessi.
**Commit:** — (da fare in chiusura di questo handoff)

### Fatto
- Installato il plugin `superpowers@claude-plugins-official` (`/plugin install`), su richiesta esplicita dell'utente — traccia in `.claude/settings.json` (`enabledPlugins`).
- Confrontata la ricerca fornita dall'utente con gli algoritmi già in produzione (`CalibrationConsensus.cs`, `StrategyGateHysteresis.cs`, `RaceTimeProjection.cs`, `RelativePaceTracker.cs`, `FuelManager.cs`): trovato un **conflitto diretto**. La ricerca propone il criterio "minimo tempo di attraversamento fra i contendenti" per la proiezione della bandiera a scacchi — esattamente il criterio che **Y-38** ha già misurato e scartato con dati reali (replay `20260901_175019`, 758 campioni: mediana 5.2 s contro i ~50.6 s corretti). Il progetto usa invece il **massimo** (`RaceTimeProjection.ProjectFlagMoment`).
- Scritta la skill in `.claude/skills/motorsport-telemetry-engineering/` (`SKILL.md` + `fuel-physics.md`, `pit-stop-decomposition.md`, `robust-statistics.md`, `future-techniques.md`) e mirror identico in `.agent/skills/motorsport-telemetry-engineering/` (percorsi di discovery diversi fra Claude Code e Antigravity, stesso standard aperto `SKILL.md`). Il warning sul criterio sbagliato sta nel corpo principale di `SKILL.md`, non in un file secondario.
- `AGENTS.md` — riga aggiunta in "Riferimenti" che punta alla skill e ricorda la duplicazione.
- **Test RED/GREEN** (subagent freschi, senza memoria di questa conversazione): RED (senza skill, senza accesso al repo) non ha riprodotto il bug esatto della ricerca, ma ha comunque proposto un terzo criterio diverso da quello corretto ("latch dell'identità del leader a T-zero + EMA per-vettura") — conferma che la formula giusta non è ovvia nemmeno per un agente ragionevole senza guida. GREEN (con la skill disponibile e accesso al repo) ha risposto correttamente col criterio del massimo, citando `RaceTimeProjection.cs`, `FlagMomentUnitTests.cs` e i numeri misurati — ma ha anche esplorato il codice sorgente direttamente, quindi il test non isola perfettamente il contributo della sola skill dal contributo della lettura del codice.

### Come verificare
Non c'è build/test da eseguire (nessun file C# toccato). Verifica manuale:
```bash
ls .claude/skills/motorsport-telemetry-engineering/ .agent/skills/motorsport-telemetry-engineering/
diff .claude/skills/motorsport-telemetry-engineering/SKILL.md .agent/skills/motorsport-telemetry-engineering/SKILL.md
```
Atteso: stessi 5 file in entrambe le cartelle, `diff` senza output (contenuto identico).

### Stato
- ✅ Compila (nessun file di codice C# toccato)
- ⏭️ Test .NET non eseguiti (nessuna modifica alla logica del plugin)
- ✅ Skill verificata con test RED/GREEN a subagent (vedi sopra), non con build/test automatico

### Per chi entra
**Prossimo passo:** nessuno obbligato — la skill è di consultazione. Se si riprende **Y-14** (tempo cambio gomme), `pit-stop-decomposition.md` ha la tecnica di scomposizione pronta da usare come riferimento, non come decisione già presa. Restano validi Fase B della roadmap (`2026-08-24-roadmap.md`) e la riscrittura di `STRATEGY_ENGINE_GUIDE.md` (Fase D) come prossimi passi di contenuto.
**NON toccare:** non è stato deciso nulla su Y-14 in questo turno — solo documentata una tecnica.
**Attenzione a:** le due copie della skill (`.claude/skills/` e `.agent/skills/`) sono duplicati testuali voluti, non linkati — se una viene corretta in futuro (es. un refinement di `robust-statistics.md`), l'altra va aggiornata a mano, stesso rischio già noto per `/new-session`/`/handoff`.

---

## [2026-09-06 10:40] claude → chiunque entri dopo (Antigravity in particolare)

**Task:** Equivalente Antigravity di `/new-session` e `/handoff`, dopo che Andreas ha fatto notare che esistevano solo per Claude Code. Nessun punto Y toccato: infrastruttura.
**Piano:** continuazione diretta dei due turni precedenti (setup coworking).
**Commit:** `144fd0e`, `bbd1ddb`

### Fatto
- Verificato via web search (non indovinato) lo schema dei comandi custom di Antigravity 2.0:
  `.agent/workflows/*.md`, frontmatter YAML con solo `description`, corpo con passi a checkbox,
  invocazione `/nome-file`. **Diverso** dal formato di Claude Code (`.claude/commands/*.md`) e
  diverso anche dalle skill `SKILL.md` (quelle sì standard aperto condiviso fra i due tool).
- `.agent/workflows/new-session.md`, `.agent/workflows/handoff.md`: stesso contenuto delle
  versioni Claude Code, riscritto nel formato Antigravity. Non ho usato l'annotazione `// turbo`
  per l'auto-run dei comandi: le due fonti consultate non concordavano sulla sintassi esatta
  (`// turbo:` vs `// turbo`), meglio ometterla che scrivere qualcosa di sbagliato.
- `.claude/commands/{new-session,handoff}.md`: aggiunta una nota di rimando reciproco verso
  l'equivalente Antigravity, così le due versioni non divergono senza che nessuno se ne accorga.

### Come verificare
Non c'è build/test da eseguire. Per Antigravity: aprire una sessione su questo repo e digitare
`/new-session` — deve comparire fra i workflow disponibili. Stesso discorso per `/handoff`.

### Stato
- ✅ Compila (nessun file di codice C# toccato)
- ⏭️ Test non eseguiti (nessuna modifica alla logica del plugin)

### Per chi entra
**Prossimo passo:** Andreas deve verificare in pratica (restart di entrambi i tool) che sia
`/new-session`/`/handoff` in Claude Code sia i due equivalenti in Antigravity siano davvero
invocabili — nessuna delle due controparti ha ancora avuto una conferma diretta in questo repo,
solo pipe-test e, per l'hook del lock, un trigger reale.
**NON toccare:** nessuna area di codice interessata da questo turno.
**Attenzione a:** se in futuro il contenuto di uno dei quattro file cambia (Claude o Antigravity,
new-session o handoff), l'altro va aggiornato di conseguenza — è scritto come promemoria in cima
a ciascuno dei quattro file, ma nessun meccanismo lo forza automaticamente.

---

## [2026-09-06 10:05] claude → chiunque entri dopo

**Task:** Comandi custom `/new-session` e `/handoff`, per ridurre la dipendenza da Andreas come "portavoce" fra sessioni di agenti diversi. Nessun punto Y toccato: turno di infrastruttura.
**Piano:** continuazione diretta del turno precedente (setup coworking Claude/Antigravity).
**Commit:** `375d7c6`, `118b24a`

### Fatto
- `.claude/commands/new-session.md`: comando che, invocato con `/new-session [tema opzionale]`,
  fa leggere `AGENTS.md` → `PROJECT_STATE.md` (lock + punti aperti) → `HANDOFF_LOG.md` (ultime
  voci) → `STRATEGY_ENGINE_GUIDE.md`, nello stesso ordine già fissato da `NEW_SESSION_PROMPT.md`,
  poi chiede il report standard (fase/roadmap, prossimo passo, decisioni in sospeso, incoerenze).
  Se viene passato un tema, aggancia anche il piano pertinente in `.ai/plans/`.
- `.claude/commands/handoff.md`: comando che guida la chiusura turno — voce in `HANDOFF_LOG.md`
  dal template esistente, controllo del conteggio voci con potatura dell'undicesima in
  `archive/HANDOFF_LOG_archive.md` se serve, rilascio del lock in `PROJECT_STATE.md`, promemoria
  esplicito di controllare eventuali numeri scritti a mano rimasti disallineati altrove nello
  stesso file, commit separato per l'handoff.
- Ho **dogfoodato** `/handoff` a mano in questo stesso turno per chiuderlo (non ho potuto invocarlo
  come slash command vero perché appena creato in questa sessione — verosimilmente serve un
  reload, come già osservato per l'hook del turno precedente): spostata la voce più vecchia
  (2026-09-03 21:15) in `archive/HANDOFF_LOG_archive.md`, aggiunta questa in cima.

### Come verificare
Non c'è build/test da eseguire (nessun file di codice toccato). Per verificare che i comandi siano
riconosciuti: aprire una sessione Claude Code nuova su questo repo e digitare `/new-session` —
deve comparire nell'elenco degli slash command con la descrizione scritta sopra.

### Stato
- ✅ Compila (nessun file di codice C# toccato in questo turno)
- ⏭️ Test non eseguiti (nessuna modifica alla logica del plugin)

### Per chi entra
**Prossimo passo (proposto, non deciso):** verificare in una sessione fresca che `/new-session` e
`/handoff` siano effettivamente invocabili (il reload dei comandi custom non è stato confermato in
questo turno, solo dedotto per analogia con l'hook). Poi, quando Andreas installa Superpowers
(`/plugin install superpowers@claude-plugins-official`), controllare dove scrive di default la sua
skill di brainstorming e agganciarla a `.ai/plans/`. In coda, la skill di dominio motorsport.
**NON toccare:** nessuna area di codice interessata da questo turno.
**Attenzione a:** questi due comandi sono specifici di Claude Code — se Antigravity vuole
l'equivalente, va scritto nel suo formato di comandi custom (vedi Gemini CLI: `.gemini/commands/*.toml`
per Gemini CLI standalone; Antigravity ha un proprio meccanismo di "custom slash workflows" ancora
da verificare in dettaglio), non copiato alla lettera.

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

## Handoff più vecchi

Tutte le voci precedenti a quelle qui sopra sono in `.ai/archive/HANDOFF_LOG_archive.md`,
in ordine cronologico inverso come questo file. La prima potatura è del 2026-09-05: il file
dichiarava di tenere gli ultimi 10 e ne conteneva 22, per 112 KB letti a ogni ingresso.

*(Niente conteggi scritti qui: `grep -c '^## \[20' .ai/archive/HANDOFF_LOG_archive.md` dà il
numero esatto senza che nessuno debba ricordarsi di aggiornarlo.)*
