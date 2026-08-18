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
