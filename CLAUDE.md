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

## Riferimenti

- `.ai/STRATEGY_ENGINE_GUIDE.md` — **come funziona il motore strategico, in parole povere.**
  Da leggere per primo se non si ha il contesto delle sessioni precedenti.
- `.ai/PROJECT_STATE.md` — stato, lock, milestone, debiti noti
- `.ai/HANDOFF_LOG.md` — passaggi di consegne (ultimi 10)
- `.ai/ARCHITECTURE.md` — ADR, mappa dei moduli, convenzioni
- `.ai/plans/` — piani di implementazione
