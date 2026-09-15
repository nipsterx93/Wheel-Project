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

## [2026-09-15 14:54] claude → chiunque entri dopo

**Task:** Chiudere con Andreas il brainstorming del plugin nuovo e scrivere lo spec. Nessun file di codice toccato, lock non preso.
**Piano:** `.ai/plans/2026-09-15-remastered-spec.md`
**Commit:** `f4fa901`, `deac428`, `5274424`, `4e95b76`, `8efd8df` (sezioni del brainstorming), questo (spec)

### Fatto
- Sezioni 1–5 del design approvate da Andreas una alla volta (decisioni 7–14 nello spec): nucleo comune con un adattatore per simulatore; contratti dei moduli (qualità dei valori, `CarIdx`, tempo di sessione crescente); ordine di costruzione con tetto di tre cicli per modulo; convivenza dei due plugin con interruttore alla fonte per spegnere undercut e overcut nel vecchio; progetti separati in formato SDK e nomi `SimRIG.Remastered.*`.
- Decisioni di Andreas di oggi: tutti fanno tutto, senza ruoli fissi; ogni modulo parte da un brainstorming con lui (cosa fa, cosa legge, cosa calcola, cosa mette a disposizione) prima dell'implementazione.
- Il brainstorming è diventato lo spec: `2026-09-15-remastered-brainstorming.md` rimosso, `2026-09-15-remastered-spec.md` al suo posto; rimandi aggiornati in `PROJECT_STATE.md`, roadmap e piano Daytona.
- Proposta sui file di progetto nel §8.5 dello spec: stessi file riorganizzati attorno al plugin nuovo, materiale del plugin vecchio in archivio; da confermare con la revisione.
- Voce del 2026-09-13 21:21 spostata in `.ai/archive/HANDOFF_LOG_archive.md`.

### Come verificare
Nessuna build: turno di sola documentazione.
```bash
grep -n "^## " .ai/plans/2026-09-15-remastered-spec.md
```
Atteso: i paragrafi da 1 a 10 dello spec.

### Stato
- ⏭️ Build e test non eseguiti (nessun file di codice modificato)
- ✅ Codice invariato rispetto a `32a8682`

### Per chi entra
**Prossimo passo:** Andreas rivede lo spec e conferma il §8.5; poi riorganizzazione dei file di progetto, ADR-007 e hook esteso; poi l'interruttore nel plugin vecchio e il passo 0.
**NON toccare:** `User.PluginSdkDemoEdit/` (congelato); `User.PluginSdkDemoRemastered/` prima della revisione dello spec; `Hardware/`.
**Attenzione a:** la voce del 2026-09-15 11:38 rimanda ancora a `2026-09-15-remastered-brainstorming.md`, oggi sostituito dallo spec.

---

## [2026-09-15 11:38] claude → chiunque entri dopo

**Task:** Decidere con Andreas il futuro del progetto dopo l'analisi del 2026-09-14 (stesse grandezze calcolate in più moduli in modi diversi). Nessun file di codice toccato, lock non preso.
**Piano:** `.ai/plans/2026-09-15-remastered-brainstorming.md`
**Commit:** questo (decisioni e stato del brainstorming)

### Fatto
- Decisioni di Andreas, con motivazioni nel piano: riscrittura del nucleo come **plugin nuovo** in `User.PluginSdkDemoRemastered/`, accanto al vecchio; una logica alla volta (analisi del vecchio codice → definizione → test coi numeri dei log → implementazione → validazione su Daytona e Road Atlanta); tetto per logica; undercut e overcut spenti durante i lavori; se la strada non funziona, versione ridotta (A).
- Plugin vecchio **congelato**: sospesi il piano correzioni Daytona (passi 3-5) e il fix di Y-62. Annotati `PROJECT_STATE.md` (stato corrente), roadmap e piano Daytona.
- Misure a supporto: 299 proprietà `SimRIG.*` con nome fisso, 116 usate dalle 8 dash attive (`E:\SimHub\DashTemplates\Test\`); la dash legge `DataPluginDemo.SimRIG.*` (255 riferimenti in `Test.djson`).
- Sezione 1 del design (architettura) proposta nel piano, in discussione con Andreas.
- Voce del 2026-09-13 17:45 spostata in `.ai/archive/HANDOFF_LOG_archive.md`.

### Come verificare
Nessuna build: turno di sola documentazione.
```bash
grep -n "Decisioni prese\|Sezione 1" .ai/plans/2026-09-15-remastered-brainstorming.md
grep -n "riscrittura del plugin" .ai/PROJECT_STATE.md .ai/plans/2026-08-24-roadmap.md
```
Atteso: il piano con la tabella delle decisioni e la sezione 1; stato e roadmap che rimandano al piano.

### Stato
- ⏭️ Build e test non eseguiti (nessun file di codice modificato)
- ✅ Codice invariato rispetto a `32a8682`

### Per chi entra
**Prossimo passo:** continuare il brainstorming con Andreas dalla sezione 1 del piano, poi le sezioni 2–5; niente codice nella cartella nuova finché lo spec non è approvato.
**NON toccare:** `User.PluginSdkDemoEdit/` (congelato, salvo guasti bloccanti); `Hardware/`; `User.PluginSdkDemoRemastered/` prima dell'approvazione dello spec.
**Attenzione a:** `AGENTS.md` descrive lock e hook di Claude Code solo per `User.PluginSdkDemoEdit/`: vanno estesi alla cartella nuova prima di scriverci codice (sezione 5 del design).

---

## [2026-09-14 13:19] claude → chiunque entri dopo

**Task:** Analisi del replay Daytona `20260914_124637`, rigirato da Andreas con la diagnostica di Y-62 (`32a8682`). Nessun file di codice toccato, lock non preso.
**Piano:** — (Y-62, fuori dal piano Daytona)
**Commit:** questo (analisi)

### Fatto
- DLL nuova attiva: 62 righe `Pit Exit Traffic Conflict` (31 finestre) e 6241 `Pit Exit Traffic Candidate`. Decisione invariata rispetto a `094551`: 29 finestre `UndercutTrafOK=False` contro 27, stessi TL a pochi decimi (una finestra di un tick in più nel giro 14, una del giro 17 spezzata in due); 12 `reason=Traffic` in entrambi.
- Chi fa scattare il conflitto nei giri 2–15, dove l'undercut è in gioco: 16 finestre, **13 H2**, 1 H1, 2 traffico reale.
  - **H2, posizione ferma:** `src=Memory surf=NotInWorld`, posizione immobile nella finestra dell'uscita box, `gapVero` che cresce di ~1 s/s mentre `mergeStimato` attraversa la bolla. Leon van Elewout (Dallara P217) fermo a **0.1883** nei giri 2–9, 12 e 13 (e 19–21); Sheppard (giro 11, 0.1721), Daughtrey (13, 0.1266), Arteaga (15, 0.1810). Giro 6: TL 2081.9→2077.2, PlayerPos 0.4728→0.5415, perdita 33.89, `mergeStimato` −4.15→+3.03, `gapVero` 36.02→41.72, `mergeVero` +2.13→+7.83.
  - **H1, stima posizione × passo:** Jan Hilden (giro 10), in pista con posizione nativa: `mergeStimato` −2.98→+0.69, `mergeVero` +4.70→+4.48. Stesso caso per Abdu Yilmaz nel giro 16 (`mergeVero` +7.7…+8.7).
  - **traffico reale:** Skadhauge e Martens (IMSA23, giro 14, `mergeVero` +1.7…+2.5).
  - Dopo la sosta del Player (perdita ~20 s): traffico reale (Loveridge, Kichiku, Wei-Ting Lo) e ancora posizioni ferme (van Elewout, Boillot fermo a 0.1384 nei giri 22–26).
- **Secondo difetto nello stesso controllo:** la minaccia delle altre classi confronta `oData.NormalizedTimes.SectorBaseline * 3.0` col passo del Player + 1.5 (`TargetStrategyManager.cs:1240`), ma `SectorBaseline` è il tempo nella zona di corsa fuori dalla zona box estesa (`OpponentTracker.cs:2501`), circa il 75% del giro: Michal Zajac2 76.29 s su un giro di 98.19, Wei-Ting Lo 66.13 s. Per 3 fanno 229 e 198 s: ogni vettura di un'altra classe con una baseline risulta minaccia, anche le più veloci. Righe `Candidate`: Dallara P217 minaccia in 2112 su 2352, GTP in 656 su 1799.
- **Controllo corretto simulato** sulle righe `Candidate` (solo posizione nativa, |`mergeVero`| ≤ 3, nessun controllo spaziale, solo la classe del Player): nessun conflitto nei giri 2–13; nel giro 14 un conflitto reale di ~20 s (Skadhauge, Martens); dal giro 16 i gruppi attorno alle soste, quando l'undercut non è più in gioco. Col filtro minaccia di oggi si aggiungerebbero i Dallara P217: Zajac2 giro 10 (19 s), Hilden giro 11 (18 s), Kharitonov e Sheppard giro 12 (9 s), Daughtrey e Peralta giro 14 (~20 s).
- Voce del 2026-09-12 22:45 spostata in `.ai/archive/HANDOFF_LOG_archive.md`.

### Come verificare
Nessuna build: turno di sola analisi.
```bash
grep -n "Pit Exit Traffic Conflict" "Logs/Daytona/SimRIG_DebugLog_20260914_124637.csv"
grep -n "Leon van Elewout | classe" "Logs/Daytona/SimRIG_DebugLog_20260914_124637.csv"
grep -n "Baseline: 76.286s\|Baseline: 66.130s" "Logs/Daytona/SimRIG_DebugLog_20260914_124637.csv"
```
Atteso: 62 righe `Conflict`; le righe di van Elewout con `pos=0.1883 src=Memory surf=NotInWorld`; le baseline di settore di Zajac2 (76.286 s) e Wei-Ting Lo (66.130 s).

### Stato
- ⏭️ Build e test non eseguiti (nessun file di codice modificato)
- ✅ Codice invariato rispetto a `32a8682`

### Per chi entra
**Prossimo passo:** fix di Y-62 col lock, dopo la conferma di Andreas sul perimetro: (1) scartare le vetture con posizione non viva (`PositionSource.Memory` o `NotInWorld`); (2) bolla sul distacco vero (`TimestampGapBehindSeconds`) invece che su posizione × passo; (3) togliere la condizione "vicina all'uscita adesso"; (4) minaccia delle altre classi sul passo sul giro invece che su `SectorBaseline × 3`. Test ADR-004 coi casi di `124637`: van Elewout (H2, giro 6), Hilden (H1, giro 10), Skadhauge (reale, giro 14, col Player lontano dall'uscita), Zajac2 (baseline di settore 76.29 s, giro 98.19).
**NON toccare:** `Hardware/`; `PitInOutAccDecTime` = 11.6 nel DB; `RaceAnalyzer.cs:1186` senza discuterne; la diagnostica di `32a8682` finché il fix non è verificato sul replay (serve al confronto).
**Attenzione a:** scartando le posizioni `NotInWorld` il controllo, nei replay, non vede le vetture lontane: è corretto, ma va detto. Il distacco vero usa i timestamp del Player: nel giro dopo la sua sosta, nella zona della corsia box, vale il transito lento. Col Player fermo ai box (giro 17, PlayerPos 0.032) la simulazione trova conflitti spuri: valutare se sospendere il controllo in corsia box. Restano rimandati il margine undercut ≈ 105 s nel giro 3 e i disallineamenti di `PROJECT_STATE.md`.

---

## [2026-09-14 12:33] claude → chiunque entri dopo

**Task:** Y-62 (traffico a metà giro): capire quale vettura fa scattare il controllo traffico dell'undercut. I log di `094551` non bastano, quindi (deciso con Andreas) prima una diagnostica, solo log e decisione invariata; il fix dopo il replay.
**Piano:** — (fuori dal piano Daytona: decisione 5 di `.ai/plans/2026-09-13-daytona-piano-correzioni.md`)
**Commit:** `d7d8f54` (lock), `32a8682` (diagnostica e test), questo (handoff, stato, rilascio lock)

### Fatto
- Analisi dei log di `094551`, senza codice (script nella cartella temporanea della sessione, non nel repository):
  - snapshot: `UndercutTrafOK=False` in 27 finestre. Giri 2–15: una per giro (15, due nel giro 13), 3.6–5.8 s, Player fra i macrosettori 8 e 11, perdita 33.6–35.4 s. Dopo la sosta del Player (perdita ~19.9 s): macrosettori 5–8, per lo più 8–19 s. I `reason=Traffic` sono 12 perché nei giri 4, 9 e 15 l'undercut era già non viable. Gli eventi `UNDERCUT_*` si scrivono sul gate grezzo: le durate sono reali, non il dwell di 5 s.
  - distacchi veri al passaggio da 0.959 (`Opponent Spatial Strict Entry` per gli avversari, `RaceProjectionsDiagnostics` per il Player): nei giri 15–19 ci sono vetture di classe davvero nella bolla (giro 16: Lukaszewski −0.6 s, Proteau −1.6, Calver2 +1.9), che il controllo di oggi vede solo col Player a metà giro. Nei giri 2–14 nessuna vettura entro ±15 s, ma **non è conclusivo**: nessuna vettura osservata fra 7 e 53 s dietro al Player (28–35 visibili su 48), proprio la fascia della bolla.
  - restano due ipotesi: **H1** vettura vera al bordo della bolla, portata dentro a metà giro dalla stima posizione × passo (`TargetStrategyManager.cs:1211-1212`); **H2** vettura `NotInWorld` con la posizione ferma all'ultimo valore memorizzato (`OpponentTracker.cs:728-732`, riscritto a ogni tick da `:1421`), il cui distacco scorre attraverso la bolla una volta per giro.
- `User.PluginSdkDemoEdit/OpponentTracker.cs:682-694` — enum `PositionSource` (Native, SimHub, Memory) e overload di `GetOpponentTrackPosition` con la sorgente; quello a due argomenti (`:676`) delega, comportamento invariato.
- `User.PluginSdkDemoEdit/TargetStrategyManager.cs:529` — `TimestampGapBehindSeconds`: da quanti secondi il Player è passato dalla posizione attuale dell'avversario, dai `PlayerMicrosectorTimestamps` (stessa interpolazione e stesse soglie del gap del Target). `:553` `ShouldLogTrafficCandidate`, `:581` `LogTrafficCandidate`.
- `TargetStrategyManager.cs:1188-1289` (ciclo traffico) — riga FLOW `Pit Exit Traffic Candidate` per ogni vettura vicina alla bolla stimata o a quella vera (quando entra, quando esce, al cambio del suo conflitto, al massimo 1/s mentre è dentro) con `pos`, `src`, `surf`, `mergeStimato`, `dUscita`, `conflitto`, `gapVero`, `mergeVero`, posizione del Player, perdita e passo; evento `Pit Exit Traffic Conflict` al cambio del conflitto complessivo, con le vetture che lo causano. Minaccia e distanza dall'uscita (controllo spaziale `:1248-1253`) si calcolano ora per ogni vettura, con le stesse formule: la decisione non cambia. Reset in `ResetSession` (`:2151`).
- `User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/UnitTests/PitExitTrafficDiagnosticsUnitTests.cs` (nuovo, registrato in `TestRunner.cs` e nel `.csproj`) — 5 test coi numeri del giro 2 di `094551`: distacco vero 35.0 s, e 40.0 s con la posizione ferma 5 s dopo; NaN senza timestamp e oltre 1.5 giri; sorgente nativa, SimHub o memoria; quando scrivere la riga.
- Voce del 2026-09-12 14:00 spostata in `.ai/archive/HANDOFF_LOG_archive.md`.

### Come verificare
```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```
Atteso: build 0 errori (1 warning CS già presente), exit 0, 376 righe `[PASS]`, 5 delle quali sotto `[TEST] Running Pit Exit Traffic Diagnostics Tests (Y-62)...`. Con gli stub (distacco 0, sorgente `None`, nessuna riga) fallivano tutti e 5: exit 1, 370 righe `[PASS]` perché il runner si ferma a quella suite. È la neutralizzazione ADR-004 di questo turno.

Sul replay Daytona di `094551` rigirato con la DLL nuova (la build l'ha già installata):
```bash
grep -n "Pit Exit Traffic Conflict" "Logs/Daytona/SimRIG_DebugLog_<run>.csv"
grep -n "Pit Exit Traffic Candidate" "Logs/Daytona/SimRIG_DebugLog_<run>.csv"
grep -n "reason=Traffic" "Logs/Daytona/SimRIG_StrategyEvent_<run>.txt"
```
Atteso:
- decisione invariata: 12 `reason=Traffic` ai TL di `094551` (2501.9, 2396.3, 2187.1, 2081.7, 1976.0, 1869.7, 1659.7, 1554.5, 1447.9, 1348.7, 1342.7, 1238.8), a meno di pochi decimi;
- una riga `conflitto=True` all'inizio di ogni finestra, con le vetture in `vetture=`;
- nelle righe `Candidate` di quelle vetture: H2 se `src=Memory` (o `surf=NotInWorld`), `pos` ferma e `gapVero` che cresce di ~1 s al secondo; H1 se `src=Native` con `|mergeStimato| ≤ 3` e `|mergeVero| > 3`.

### Stato
- ✅ Compila (0 errori, 1 warning CS già presente)
- ✅ Test passano: 376 PASS, exit 0. ⚠️ Il backtest sul replay Misano si salta ancora (Y-54)
- ⏭️ Replay Daytona con la diagnostica non ancora rigirato

### Per chi entra
**Prossimo passo:** Andreas rigira il replay Daytona (lo stesso di `094551`) e indica il nome del log; poi il fix di Y-62 coi numeri di quel log (test ADR-004 sul caso reale), poi il passo 3 del piano.
**NON toccare:** `Hardware/`; `PitInOutAccDecTime` = 11.6 nel DB; la decisione del controllo traffico prima di aver letto la diagnostica; `RaceAnalyzer.cs:1186` senza discuterne.
**Attenzione a:** la diagnostica vede solo le vetture che il ciclo non scarta (posizione > 0, fuori dalla geofence box): una vettura che entra ai box mentre è nella bolla riceve la riga `esce` solo quando torna valutata. `gapVero` ha senso solo con la posizione viva: con `src=Memory` cresce di un secondo al secondo, ed è il segnale da cercare. Rimandati da Andreas il 2026-09-14, da riprendere: i disallineamenti del report d'ingresso in `PROJECT_STATE.md` (archivio "12 voci", `Logs/Daytona Run/`, tabella "Congelati" coi punti in lavorazione) e il margine undercut ≈ 105 s nel giro 3 di tutti i run Daytona dal 12/09, non ancora registrato come punto.

---

## [2026-09-14 10:14] claude → chiunque entri dopo

**Task:** Verifica, sul replay Daytona `20260914_094551` rigirato da Andreas con `3e9d4ae`, del passo 2 (tempo di corsa nella zona estesa dalla mediana dei transiti del Player). Nessun file di codice toccato, lock non preso.
**Piano:** `.ai/plans/2026-09-13-daytona-piano-correzioni.md` (passo 2)
**Commit:** questo (verifica)

### Fatto
- Stesso replay dei run precedenti: `Opponent Smart Refuel Projection` del Target a TL 1089.2, `Pit Complete` del Player a TL 938.6, `Projection Validation` con vero=27.742.
- Confronto con `082515` (passo 1 già corretto, passo 2 non ancora), dal MergeGapLog:

| Misura | `082515` | `094551` | Atteso / reale |
|---|---|---|---|
| `ExtZone` dal giro 7 | 22.18 s (21.90–22.27) | **23.73 s** (23.67–23.77) | ≈ 23.8 |
| Perdita prevista del Player, giri 11-15 | 35.41 s | **33.84 s** | reale 34.7 |
| MergeGap nei primi blocchi dopo la sosta del Target | −1.30 / −1.72 | **−2.93 / −3.18** | reale −2.7 |
| Errore medio del MergeGap prima delle soste, giri 2-15 | +0.33 | +0.37 | invariato: vale per entrambe |

- `ExtZone` resta sul minimo di classe fino al giro 3 (25.40 / 23.47 / 23.47 s); dal giro 4 usa la mediana del Player.
- L'errore sulla perdita del Player passa da +0.7 a −0.9 s: è il residuo di transito e stazionario del Player che il piano dà già fuori piano.
- Congelato durante la sosta del Player −11.29 s (era −9.60): è il gap stantio di Y-59 con una perdita più bassa, lo corregge il passo 3. Congelato durante la sosta del Target −3.62 s (passo 3, punto 4). Gap dopo le soste −2.57 s; `TargetNeedsPit` mai vero dopo la sosta del Target.
- Y-62, riferimenti nuovi: 12 `UNDERCUT_NONVIABLE reason=Traffic`, spostati di ~1–1.5 s come previsto, a TL 2501.9, 2396.3, 2187.1, 2081.7, 1976.0, 1869.7, 1659.7, 1554.5, 1447.9, 1348.7, 1342.7 e 1238.8; `STRATEGY_CHANGED` 34 (32 in `082515`).
- Voce del 2026-09-12 12:45 spostata in `.ai/archive/HANDOFF_LOG_archive.md`.

### Come verificare
Nessuna build: turno di sola analisi.
```bash
grep -n "ExtZone" "Logs/Daytona/SimRIG_MergeGapLog_20260914_094551.txt"
grep -n "reason=Traffic" "Logs/Daytona/SimRIG_StrategyEvent_20260914_094551.txt"
```

### Stato
- ⏭️ Build e test non eseguiti (nessun file di codice modificato)
- ✅ Passo 2 verificato sul replay; codice invariato rispetto a `3e9d4ae`

### Per chi entra
**Prossimo passo:** Y-62 (traffico a metà giro) coi riferimenti di `094551` qui sopra; poi il passo 3.
**NON toccare:** `Hardware/`; `PitInOutAccDecTime` = 11.6 nel DB; `RaceAnalyzer.cs:1186` senza discuterne.
**Attenzione a:** il piano chiede ancora di ricontrollare le raccomandazioni undercut/overcut sul replay Road Atlanta `20260911_231106`, ora che le perdite ai box sono più basse di ~1.6 s.

---

## [2026-09-14 09:34] claude → chiunque entri dopo

**Task:** Passo 2 del piano correzioni Daytona (Y-61): il tempo di corsa nella zona estesa sottratto alla perdita ai box è la mediana dei transiti recenti del Player, non il minimo di classe. Scope deciso con Andreas: calcolo MergeGap/undercut e `SimRIG.Pit.TotalPitLoss`.
**Piano:** `.ai/plans/2026-09-13-daytona-piano-correzioni.md` (passo 2)
**Commit:** `d4d0f96` (lock), `3e9d4ae` (codice e test), questo (handoff, stato, piano, rilascio lock)

### Fatto
- `User.PluginSdkDemoEdit/SectorTracker.cs` — `RecentRawTimeMedian(window = 7, minSamples = 3)`: mediana degli ultimi 7 transiti validi di `RawNormalHistory`, 0 se sono meno di 3. Nello storico entrano solo i transiti di corsa: il giro della sosta e i passaggi oltre il 115% del migliore restano fuori (lo verifica il test che guida il vero `Update`).
- `User.PluginSdkDemoEdit/OpponentTracker.cs` — nuovo parametro `playerTypicalExtendedPitZoneTime` di `Update` e nuova proprietà `ExtendedRacingReferenceTime` = `ResolveExtendedRacingReference(mediana, minimo di classe, pavimento fisico)`: la mediana se è > 0 e sopra il pavimento, altrimenti il minimo di classe. `ClassBestExtendedPitZoneTime` è invariato: lo usano ancora le soglie di rilevamento soste (`ClassBest + 5`), il ripiego della perdita del leader (`ClassBest + 25`) e la proprietà di dashboard omonima.
- `User.PluginSdkDemoEdit/TargetStrategyManager.cs` — `extendedRacingTime` del MergeGap/undercut dal nuovo riferimento.
- `User.PluginSdkDemoEdit/DataPluginDemo.cs` — passa `PlayerExtendedPitZone.RecentRawTimeMedian()` a `OpponentTracker.Update`; `SimRIG.Pit.TotalPitLoss` usa il nuovo riferimento.
- `User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/UnitTests/ExtendedRacingReferenceUnitTests.cs` (nuovo, registrato in `TestRunner.cs` e nel `.csproj`) — 4 test: mediana dei 14 transiti del Player di `140133` (23.69 s, stabile col giro fuori pista da 28.12 s); stesso risultato guidando il vero `SectorTracker.Update`; scelta del riferimento con ricaduta sul blocco `163743`:1117-1120 (perdita del Player 35.73 → 33.97 s, MergeGap dopo la sosta del Target −1.45 → −3.21 s, reale −2.7); ripiego sotto i 3 transiti.
- **Fuori scope, annotato:** `RaceAnalyzer.cs:1186` (perdita del Player nella proiezione del totale giri) usa ancora il minimo di classe. Toccarlo sposterebbe la proiezione di ~0.02 giri, in un'area sensibile (Y-36, Y-45).
- Voce del 2026-09-11 22:50 spostata in `.ai/archive/HANDOFF_LOG_archive.md`.

### Come verificare
```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```
Atteso: build 0 errori (1 warning CS già presente), exit 0, 371 righe `[PASS]`, 4 delle quali sotto `[TEST] Running Extended Racing Reference Tests (Y-61, passo 2)...`. Con gli stub (nessuna mediana, minimo di classe) gli stessi 4 test fallivano: exit 1, 366 PASS. È la neutralizzazione ADR-004 di questo passo.

Sul replay Daytona `20260913_163743` rigirato con la DLL nuova (la build l'ha già installata):
```bash
grep -n "ExtZone" "Logs/Daytona/SimRIG_MergeGapLog_<run>.txt"
```
Atteso: `ExtZone` 21.93 s nei primi giri (meno di 3 transiti), poi ~23.7–23.8 s; perdita prevista del Player ~33.8–34.2 s (reale 34.7); MergeGap nel primo blocco dopo la sosta del Target ~−3.1…−3.3 s (oggi −1.30…−1.45, reale −2.7); MergeGap prima delle soste invariato (errore medio +0.33 s nei giri 2-15), perché lì l'effetto vale per entrambe le vetture.

### Stato
- ✅ Compila (0 errori, 1 warning CS già presente)
- ✅ Test passano: 371 PASS, exit 0. ⚠️ Il backtest sul replay Misano si salta ancora (Y-54)
- ⏭️ Replay Daytona con il passo 2 non ancora rigirato

### Per chi entra
**Prossimo passo:** Andreas rigira il replay Daytona per verificare il passo 2; poi Y-62 (traffico a metà giro, prima del passo 3), coi riferimenti presi da quel replay.
**NON toccare:** `Hardware/`; `PitInOutAccDecTime` = 11.6 nel DB; `ClassBestExtendedPitZoneTime` e le soglie di rilevamento soste che lo usano; `RaceAnalyzer.cs:1186` senza discuterne (proiezione del totale giri).
**Attenzione a:** il riferimento cambia quando il Player ha 3 transiti validi (dal giro ~4): da lì tutte le perdite ai box scendono di ~1.8 s. Prima delle soste l'effetto vale per entrambe le vetture, dopo la prima sosta no. Si sposta di ~1.8 s anche la bolla del traffico di Y-62 (`timeGap − playerTotalPitLoss`), quindi gli istanti degli spegnimenti possono cambiare. Il piano chiede di ricontrollare le raccomandazioni undercut/overcut anche sul replay Road Atlanta `20260911_231106`.

---

## [2026-09-14 08:54] claude → chiunque entri dopo

**Task:** Verifica, sul replay Daytona `20260914_082515` rigirato da Andreas con `c18a1b0`, della correzione del passo 1. Nessun file di codice toccato, lock non preso.
**Piano:** `.ai/plans/2026-09-13-daytona-piano-correzioni.md` (passo 1)
**Commit:** questo (verifica, registrato Y-62)

### Fatto
- Stesso replay di `163743` e `070557`: sosta del Target a TL 1089.2 con la stessa `Opponent Smart Refuel Projection` (37.3 L), `Pit Complete` del Player a TL 938.5, `Projection Validation` con vero=27.742.
- Errore del MergeGap prima delle soste rispetto ai −2.7 s reali, sui blocchi del MergeGapLog in cui entrambe le vetture devono ancora fermarsi:

| Giri | `163743` (prima del passo 1) | `070557` (tetto sullo spazio libero) | `082515` (tetto sulla capienza) |
|---|---|---|---|
| 1 | −8.31 | −0.14 | −14.81 |
| 2–7 | +5.95 | +9.50 | +0.37 |
| 8–10 | +4.45 | +2.07 | +0.15 |
| 11–15 | +3.74 | +0.37 | +0.41 |
| 2–15 | +4.84 | +4.56 | **+0.33** (tutti i blocchi fra −0.43 e +0.87) |

- Stazionario previsto del Target: 17.7 / 18.9 / 18.1 / 16.2 s nei giri 1-4 (in `070557` 2.4–6.9 s), 15.35–15.53 s nei giri 13-15; il reale è ~16.2–16.5 s. A inizio gara il consumo BoP del Target preso dal database vale ora 3.60 L/giro (3.57 in `070557`): il database del plugin si è aggiornato fra i due run.
- Invariati, come previsto: giro 1 a −14.8 s (stazionario del Player a 0, fuori piano); congelato durante la sosta del Target −3.62 s (passo 3, punto 4); congelato durante la sosta del Player −9.60 s (Y-59); gap dopo le soste fra −2.64 e −2.76 s; `TargetNeedsPit` mai vero dopo la sosta del Target (0 blocchi su 53).
- **Registrato Y-62** in `.ai/PROJECT_STATE.md`. Con l'undercut viable quasi tutta la gara i `STRATEGY_CHANGED` passano a 32 (22 in `070557`, 6 in `163743`): 11 sono spegnimenti per "traffico", uno per giro, sempre col Player fra i macrosettori 8 e 10, per ~5 s. Causa: il controllo spaziale di `TargetStrategyManager.cs:1129-1164` chiede che la vettura nella bolla di rientro sia *adesso* vicina all'uscita box.
- Voce del 2026-09-11 15:35 spostata in `.ai/archive/HANDOFF_LOG_archive.md`.

### Come verificare
Nessuna build: turno di sola analisi.
```bash
grep -n "FROZEN IN PIT" "Logs/Daytona/SimRIG_MergeGapLog_20260914_082515.txt"
grep -n "reason=Traffic" "Logs/Daytona/SimRIG_StrategyEvent_20260914_082515.txt"
```
Atteso: congelati −3.62 e −9.60; 12 righe `reason=Traffic` (11 con cambio di strategia), a TL 2501.9, 2394.9, 2185.9, 2080.6, 1974.8, 1868.5, 1659.2, 1553.3, 1446.8, 1347.2, 1341.5 e 1237.3.

### Stato
- ⏭️ Build e test non eseguiti (nessun file di codice modificato)
- ✅ Codice invariato rispetto a `c18a1b0`; passo 1 verificato sul replay

### Per chi entra
**Prossimo passo:** passo 2 del piano (`ExtZone` dalla mediana dei transiti del Player); poi Y-62, prima del passo 3 (deciso da Andreas il 2026-09-14).
**NON toccare:** `Hardware/`; `PitInOutAccDecTime` = 11.6 nel DB; il giro 1 del MergeGap (stazionario del Player a 0, fuori piano) senza discuterne; le soglie gomme sì/no senza il ricontrollo del passo 5.
**Attenzione a:** il passo 2 abbassa di ~1.9 s la perdita ai box di tutte le vetture. Prima delle soste è un errore di modo comune e il MergeGap non cambia; cambia dopo la prima sosta. Sposta anche la bolla del traffico di Y-62 (`timeGap − playerTotalPitLoss`), quindi i TL degli spegnimenti possono cambiare: confrontare con i numeri qui sopra.

---

## [2026-09-14 08:18] claude → chiunque entri dopo

**Task:** Verifica del passo 1 del piano correzioni Daytona sul replay `20260914_070557` (rigirato da Andreas con `05f0002`) e correzione della regressione trovata: il tetto sul carburante previsto del Target torna la capienza del serbatoio, non lo spazio libero attuale.
**Piano:** `.ai/plans/2026-09-13-daytona-piano-correzioni.md` (stato del passo 1; nuovo punto 4 nel passo 3)
**Commit:** `56e65a1` (lock), `c18a1b0` (correzione e test), questo (handoff, piano, stato, rilascio lock)

### Fatto
- Confronto fra `070557` (dopo il passo 1) e `163743` (prima): stesso replay, stessi eventi agli stessi TL (sosta del Target a TL 1089.2, `Projection Validation` identica). Errore medio del MergeGap prima delle soste rispetto ai −2.7 s reali, sui blocchi del MergeGapLog in cui entrambe devono ancora fermarsi:

| Giri | Prima (`163743`) | Passo 1 (`070557`) | Tetto = capienza (ricalcolato) |
|---|---|---|---|
| 1 | −8.31 | −0.14 | −14.50 |
| 2–7 | +5.95 | +9.50 (max +15.51) | +0.75 |
| 8–10 | +4.45 | +2.07 | +0.34 |
| 11–15 | +3.74 | +0.37 | +0.48 |

- Giri 11–15 come da piano: stazionario del Target 12.8 → 15.5 s, perdita 34.4 → 37.2 s, `FuelLaps` 2.3 → 1.9; ultimo blocco prima della sosta −2.78 s (prima +0.43). Congelato durante la sosta del Target −3.62 s (prima −0.92). Invariati: congelato durante la sosta del Player −9.70 s (Y-59), gap dopo le due soste, `TargetNeedsPit` mai vero dopo la sosta del Target. Il −0.14 del giro 1 è una compensazione di due errori: lo stazionario del Player vale 0 al giro 1 (annotato fuori piano).
- **Regressione corretta** in `User.PluginSdkDemoEdit/TargetStrategyManager.cs` (`ForecastTargetPit`): il tetto sullo spazio libero attuale, variante mia rispetto al piano, vale per il rifornimento all'ingresso box, non per una sosta prevista giri prima. Al giro 1 (MergeGapLog `070557`, TL 2652.5) il Target ha 58.19 L a bordo: 1.81 L e 2.67 s invece di 38.56 L e 17.42 s. Ora il tetto è la capienza del Target, con ripiego su quella del Player se non è nota.
- `User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/UnitTests/TargetBopFuelForecastUnitTests.cs` — il test del tetto (ora `Test_Regression_FuelToAddCappedByTargetTankCapacity`) usa i numeri del giro 1 di `070557`, più un secondo caso (4 L a bordo, 20 giri alla fine: 60 L, non i 50 del Player).
- Strategia: i cambi passano da 6 a 22. I 6 dei giri 1–4 sono identici nei due run; i 16 nuovi sono nei giri 10–15, dove l'undercut sul Target diventa viable (margine +0.15…+1.08 s) e oscilla sul filtro traffico (6 volte `UNDERCUT_NONVIABLE reason=Traffic`, di nuovo viable 3–6 s dopo). Se il consiglio sia giusto lo dirà la Fase B.
- **Nuovo punto 4 nel passo 3 del piano** (deciso con Andreas): congelare il MergeGap già quando il Target passa ad `ApproachingPits`. Da TL 1092.7 il Target frena e il gap live scende da −0.81 a circa −2.1 prima del flag di corsia box (TL 1089.2): la frenata entra due volte nel conto. Congelato −3.62 s contro −2.31 ad `ApproachingPits` (reale −2.7); stessa deriva in `163743`. `ApproachingPits` compare solo alla sosta vera (3 eventi per run).
- Voce del 2026-09-11 14:25 spostata in `.ai/archive/HANDOFF_LOG_archive.md`.

### Come verificare
```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```
Atteso: build 0 errori (1 warning CS già presente), exit 0, 367 righe `[PASS]`. Prima della correzione lo stesso test falliva con `got 1,81` (exit 1, 365 PASS): è la neutralizzazione ADR-004 di questa correzione.

Sul replay Daytona `20260913_163743` rigirato con la DLL nuova (la build l'ha già installata):
```bash
grep -n "Target (+" "Logs/Daytona/SimRIG_MergeGapLog_<run>.txt"
```
Atteso: `Staz` del Target fra ~15.4 e ~17.9 s già dal giro 2 (in `070557` 3.7 s al giro 2 e 15.9 al giro 10); errore medio del MergeGap prima delle soste entro ~1 s dal giro 2 al 15; giri 11–15 come in `070557`.

### Stato
- ✅ Compila (0 errori, 1 warning CS già presente)
- ✅ Test passano: 367 PASS, exit 0. ⚠️ Il backtest sul replay Misano si salta ancora (Y-54)
- ⏭️ Replay Daytona con la correzione non ancora rigirato

### Per chi entra
**Prossimo passo:** Andreas rigira il replay Daytona con `c18a1b0` per confermare i giri 1–10; poi il passo 2 del piano.
**NON toccare:** `Hardware/`; `PitInOutAccDecTime` = 11.6 nel DB; il giro 1 del MergeGap (stazionario del Player a 0, fuori piano) senza discuterne; le soglie gomme sì/no senza il ricontrollo del passo 5.
**Attenzione a:** la colonna "tetto = capienza" della tabella è ricalcolata dai valori loggati (consumo costante 3.57 L/giro, `FuelLaps` arrotondato a 0.1), non misurata: la conferma è il replay. L'oscillazione del filtro traffico (Y-2) ora si vede nei giri 10–15. Il punto 4 del passo 3 va verificato anche sulla sosta del Player.

---

## [2026-09-13 23:12] claude → chiunque entri dopo

**Task:** Passo 1 del piano correzioni Daytona (Y-61): la previsione della sosta del Target nel calcolo MergeGap/undercut usa il consumo proporzionato al BoP e il serbatoio del Target, non quelli del Player. Scope allargato da Andreas a `SimRIG.Target.TankLapsRemaining` e al tetto del rifornimento.
**Piano:** `.ai/plans/2026-09-13-daytona-piano-correzioni.md` (passo 1)
**Commit:** `76a1d6e` (lock), `05f0002` (codice e test), questo (handoff, file di stato, rilascio lock)

### Fatto
- `User.PluginSdkDemoEdit/OpponentTracker.cs` — consumo verde/giallo degli avversari estratto in `ResolveOpponentFuelBurn`, comportamento e costanti invariati. Sull'avversario restano ora `BopFuelPerLap` (solo il consumo che viene dalla regola BoP, dal consumo del Player o dal database; 0 dove si usa una costante: 2.5 L/giro senza dati, 3.0 per le altre classi) e `FuelTankCapacity` (`classMaxTank`, la capienza che il modello avversari già usa). Assegnati a ogni tick dopo il `continue` per posizione ≤ 0: con l'auto `NotInWorld` resta l'ultimo valore visto.
- `User.PluginSdkDemoEdit/TargetStrategyManager.cs` — `ForecastTargetPit` (+ `TargetPitForecast`): una sola funzione per il calcolo e per il blocco del MergeGapLog, che ne avevano due copie. Consumo BoP del Target con ripiego sul Player (e su 3.0 L/giro, come prima); tetto = spazio libero nel serbatoio del Target (prima la capienza del Player); `CurrentTarget.TankLapsRemaining` (ex riga 887, pubblicato come `SimRIG.Target.TankLapsRemaining`) col consumo del Target, 99 se nessun consumo è noto.
- `User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/UnitTests/TargetBopFuelForecastUnitTests.cs` (nuovo, registrato in `TestRunner.cs` e nel `.csproj`) — 4 test coi numeri del giro 15 di `163743`. La suite raccoglie i fallimenti invece di fermarsi al primo: una neutralizzazione mostra tutti i test rossi in un giro solo.
- **Scostamenti dal piano:** (1) salvato solo il consumo verde, il giallo non ha consumatori nella strategia; (2) tetto sullo spazio libero (capienza − carburante a bordo), come già fa `smartFuelToAdd` nel modello avversari, invece della capienza intera; (3) il blocco del MergeGapLog usa ora lo stesso fill rate del calcolo (`radar.MeasuredFuelFillRate`, ripiego sul profilo di classe del Target) invece di `refuelRate` (ripiego sul profilo del Player): cambia qualcosa solo senza fill rate misurato.
- File di stato, incoerenze del report d'ingresso e della review §8: `PROJECT_STATE.md` — 347 → 367 test misurati; tolti i conteggi a mano dei punti chiusi (dicevano 39 e 40, sono 41: il `grep` per contarli è già nel file); Y-13 tolto dalla frase sui punti che aspettano dati; "Fase attiva" allineata alla roadmap. `AGENTS.md` — tolto il "40". `STRATEGY_ENGINE_GUIDE.md` — i file di log sono quattro, aggiunto `SimRIG_DebugLog_*.csv` (nomi da `LogManager.cs:140-143`).
- Voce del 2026-09-11 13:35 spostata in `.ai/archive/HANDOFF_LOG_archive.md`, di cui ho corretto l'intestazione (dichiarava solo 2026-08-24 → 2026-09-01).
- Pushati su `origin` anche gli 8 commit che erano solo locali, su richiesta di Andreas.

### Come verificare
```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```
Atteso: build 0 errori (1 warning CS già presente), exit 0, 367 righe `[PASS]`, 4 delle quali sotto `[TEST] Running Target BoP Fuel Forecast Tests (Y-61)...`. Neutralizzazione ADR-004 fatta prima del commit (consumo del Player, tetto del Player, nessun consumo esposto): exit 1 con tre fallimenti (`got 3,00`, `got 50,00`, `got 0,00`), test del ripiego verde.

Sul replay Daytona `20260913_163743`, rigirato con la DLL nuova (la build l'ha già installata):
```bash
grep -n "Target (+\|ProjectedMergeGap" "Logs/Daytona/SimRIG_MergeGapLog_<run>.txt"
```
Atteso al giro 15: `Target (+≈37.3s) : Staz: ≈15.7s`, `ProjectedMergeGap` ≈ −2.5 s e `FuelLaps` del Target ≈ 1.9 (oggi +34.40 s, 12.79 s, +0.43 s, 2.3).

### Stato
- ✅ Compila (0 errori, 1 warning CS già presente)
- ✅ Test passano: 367 PASS, exit 0 (363 prima). ⚠️ Il backtest sul replay Misano si salta anche su questa macchina (`No frames loaded from replay file`, Y-54)
- ⏭️ Replay Daytona con la DLL nuova non ancora rigirato: passo chiuso come "test verdi, replay da verificare"

### Per chi entra
**Prossimo passo:** passo 2 del piano (`ExtZone` dalla mediana dei transiti del Player invece del minimo di classe), dopo il replay del passo 1 se Andreas lo rigira prima. Lock con scope sui file indicati nel passo 2 (`SectorTracker.cs`, `DataPluginDemo.cs:1273`, `OpponentTracker.cs`) e sui test.
**NON toccare:** `Hardware/`; `PitInOutAccDecTime` = 11.6 nel DB; le soglie gomme sì/no senza il ricontrollo del passo 5; il consumo fisso delle altre classi (fuori piano).
**Attenzione a:** i test chiamano le funzioni pure, non i due `Update` (servirebbe il `GameData` di SimHub): l'assegnazione di `BopFuelPerLap`/`FuelTankCapacity` in `OpponentTracker.Update` e le due chiamate a `ForecastTargetPit` le verifica solo il replay. Nei primi 3 giri, senza consumo nel database, il Target usa ancora il consumo del Player (la costante 2.5 non viene esposta). I criteri numerici dei passi 3 e 5 presuppongono questo passo.

---

## [2026-09-13 21:50] claude → chiunque entri dopo

**Task:** Registrate le decisioni di Andreas sul piano correzioni Daytona (ordine 1 → 5, `SimRIG.Leader.TrackPct` = posizione stimata, esegue claude) e preparata la ripartenza in una nuova sessione. Nessun file di codice toccato, lock non preso.
**Piano:** `.ai/plans/2026-09-13-daytona-piano-correzioni.md`
**Commit:** `2f69778` (decisioni in piano, stato e roadmap), questo (handoff)

### Fatto
- `.ai/plans/2026-09-13-daytona-piano-correzioni.md` — stato "Approvato", esecutore claude, nuove sezioni "Decisioni prese" e "Come si riparte in una nuova sessione".
- `.ai/PROJECT_STATE.md` — Y-58: decisione presa (posizione stimata); "Stato corrente": prossimo lavoro tecnico = piano correzioni Daytona, poi Y-52 Passo 3.
- `.ai/plans/2026-08-24-roadmap.md` — lavoro attivo = piano correzioni Daytona; Y-52 in pausa, non chiusa.

### Come verificare
Nessuna build: turno di sola documentazione.
```bash
grep -n "Approvato\|Decisioni prese" .ai/plans/2026-09-13-daytona-piano-correzioni.md
grep -n "correzioni Daytona" .ai/plans/2026-08-24-roadmap.md .ai/PROJECT_STATE.md
```
Atteso: il piano risulta approvato con le decisioni; roadmap e "Stato corrente" rimandano al piano come lavoro attivo.

### Stato
- ⏭️ Build e test non eseguiti (nessun file di codice modificato)
- ✅ Codice invariato rispetto a `863c65c`

### Per chi entra
**Prossimo passo:** passo 1 del piano (consumo BoP del Target nel calcolo MergeGap/undercut), eseguito da claude. Lock con scope `User.PluginSdkDemoEdit/OpponentTracker.cs`, `User.PluginSdkDemoEdit/TargetStrategyManager.cs`, il nuovo file di test e `User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/User.PluginSdkDemo.Tests.csproj`. Test prima del fix coi numeri del piano: consumo Target 3.60 L/giro, carburante da imbarcare ≈ 34.1 L, stazionario ≈ 15.7 s (oggi 12.79).
**NON toccare:** `Hardware/`; `PitInOutAccDecTime` = 11.6 nel DB; i passi 2–5 nello stesso turno (un passo per turno).
**Attenzione a:** chiudere SimHub prima della build (la build installa la DLL). Il criterio "sul replay" richiede che Andreas rigiri il replay Daytona con la DLL nuova e indichi il nome del log. Gli script di analisi della review erano nella cartella temporanea della sessione del 13/09: i numeri di riferimento sono nel piano e nella review.

---


---

---


---

---

---


---


---

---

---


---

---


---

---

---


---

---

---


---

---

---

