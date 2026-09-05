# CLAUDE.md — The Wheel Project / Antigravity 2.0

Plugin SimHub in C# / .NET Framework 4.8 (WPF). Progetto attivo: `User.PluginSdkDemoEdit/`.

---

## ⚠️ Prima di toccare qualsiasi file di codice

1. Leggi **`.ai/PROJECT_STATE.md`** e controlla il blocco `LOCK`.
2. Se `owner` non è `NONE` e non sono io → **non scrivo codice.** Posso leggere, analizzare,
   proporre un piano in `.ai/plans/`. Nient'altro.
3. Se `owner: NONE` → prendo il turno: aggiorno il blocco (owner, since, task, scope, expires),
   committo `[claude] chore: acquire lock — <task>`, poi lavoro.
4. A fine turno: aggiorno `.ai/HANDOFF_LOG.md` (in cima), rilascio il lock, committo.

Il lock è disciplina, non tecnologia. La rete di sicurezza vera è Git: **commit piccoli e frequenti**.

---

## Build

Ambiente: `SIMHUB_INSTALL_PATH` = `E:\SimHub\` — le reference alle DLL di SimHub si risolvono da lì.
MSBuild: Visual Studio 2022 Community.

```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
```

**Attenzione:** il `.csproj` ha un post-build event che fa `XCOPY` della DLL in `%SIMHUB_INSTALL_PATH%`.
Ogni build **installa** il plugin nel SimHub reale. Conseguenze:
- Se SimHub è in esecuzione, la DLL è lockata e la build fallisce con errore di copia → chiudere SimHub.
- Non buildare "tanto per provare" senza sapere che si sta sovrascrivendo il plugin in uso.

## Test

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

---

## Trappole di questo repo

- **`*_LEGACY.cs` non sono compilati.** `DataPluginDemo_LEGACY.cs`, `FuelCalculator_LEGACY.cs`,
  `PitStrategyManager_LEGACY.cs` esistono su disco ma non sono nel `<Compile>` del csproj.
  Modificarli non ha alcun effetto sul plugin. Trattarli come archivio in sola lettura.
- **`User.PluginSdkDemoBackup/` è una copia manuale obsoleta**, esclusa da Git. Non è il progetto
  attivo. Se una ricerca ci finisce dentro, è un falso positivo.
- **Nessun glob nei `.csproj`.** Ogni nuovo `.cs` va aggiunto a mano al `<Compile>`, altrimenti
  la build passa e il codice semplicemente non esiste.
- **File enormi:** `DataPluginDemo.cs` (~155 KB), `OpponentTracker.cs` (~105 KB),
  `SettingsControlDemo.xaml` (~99 KB). Leggerli a fette con `offset`/`limit`, mai in blocco.

---

## Convenzioni di codice

- Identificatori sempre in inglese. Commenti in italiano o inglese, coerenti col file.
- `PascalCase` tipi/metodi pubblici, `camelCase` locali/parametri, `_camelCase` campi privati.
- Seguire lo stile del file circostante quando diverge da quanto sopra.
- Reference esterne via `$(SIMHUB_INSTALL_PATH)`. **Mai** nuovi path assoluti hardcoded.
- Niente nuovi file `*_LEGACY.cs`: per archiviare, basta la history di Git.

---

## Git

- Tutti su `main`, serializzati dal lock (vedi `.ai/ARCHITECTURE.md` ADR-002). Niente branch per agente.
- Un commit per turno, prefisso agente: `[claude] fix: ...`, `[antigravity] arch: ...`, `[codex] review: ...`
- `main` deve restare compilabile: non rilasciare il lock lasciando il codice rotto.
  Se resta rotto, dichiararlo esplicitamente in `HANDOFF_LOG.md`.

---

## Come voglio le consegne

Nell'handoff, perché io sia efficace:
- **Percorsi file espliciti**, con riga dove serve (`OpponentTracker.cs:1420`), non "il tracker avversari".
- **Comandi esatti** di build e test da eseguire, non "testalo".
- **Scope esplicito**: cosa NON toccare in questo turno.
- **Criterio di successo osservabile**: cosa deve stampare/succedere se è andata bene.
- Link al piano in `.ai/plans/` se il task è complesso, invece di ridescriverlo nel log.

---

## Sessioni di revisione (Codex, Gemini, o una nuova chat senza memoria)

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
   di `HANDOFF_LOG.md`, come già previsto per chi implementa — serve a distinguere chi ha verificato
   cosa quando le sessioni si alternano senza canale diretto tra loro.

---

## Riferimenti

- `.ai/NEW_SESSION_PROMPT.md` — **da incollare all'apertura di una chat nuova.** Ricostruisce il
  contesto senza dipendere dalla memoria della sessione precedente.
- `.ai/plans/2026-08-24-roadmap.md` — **cosa fare adesso e in che ordine.** Il progetto è uscito
  dalla fase reattiva: non proporre lavoro fuori da qui senza discuterlo.
- `.ai/STRATEGY_ENGINE_GUIDE.md` — **come funziona il motore strategico, in parole povere.**
  Da leggere per primo se non si ha il contesto delle sessioni precedenti.
- `.ai/PROJECT_STATE.md` — stato, lock, milestone, debiti noti
- `.ai/HANDOFF_LOG.md` — passaggi di consegne (ultimi 10)
- `.ai/ARCHITECTURE.md` — ADR, mappa dei moduli, convenzioni
- `.ai/plans/` — piani di implementazione
- `.ai/archive/` — **storia consultabile a richiesta, non da caricare a ogni sessione.**
  `CLOSED_POINTS.md` (il ragionamento completo dei 40 punti chiusi, con i numeri e i commit) e
  `HANDOFF_LOG_archive.md` (gli handoff oltre i 10 tenuti). Ci si va quando serve contestare una
  conclusione o ricostruire un turno vecchio — non all'apertura.

---

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
dare per buono un numero che non può verificare, e chiede i file se gli servono (regola già in
"Sessioni di revisione", qui estesa a chi implementa).
