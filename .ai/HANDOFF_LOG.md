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
**Prossimo passo:** passo 2 del piano (`ExtZone` dalla mediana dei transiti del Player). Y-62 è fuori piano: quando affrontarlo lo decide Andreas.
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

## [2026-09-13 21:21] claude → chiunque entri dopo

**Task:** Piano delle correzioni per Y-58…Y-61 dopo il confronto con Andreas (regola BoP del consumo avversari, loop chiuso della pit road con `NotInWorld`, transito di corsa del Player) e dopo la revisione di Gemini. Nessun file di codice toccato, lock non preso.
**Piano:** `.ai/plans/2026-09-13-daytona-piano-correzioni.md`
**Commit:** `c64bab4` (piano + correzioni a review e stato), questo (handoff)

### Fatto
- `.ai/plans/2026-09-13-daytona-piano-correzioni.md` — 5 passi in ordine proposto (consumo BoP del Target nel calcolo MergeGap, `ExtZone` dalla mediana del Player, latch MergeGap senza campioni `NotInWorld`, leader nei buchi di dati, stazionario avversari senza loop chiuso), ognuno con `file:riga`, modifica, test coi numeri dei log e criterio sul replay.
- `.ai/reviews/2026-09-13-daytona-leader-mergegap-pitloss.md` — avviso in testa e nuovo §9: corretti Y-60 (il tempo in corsia è nostro quando l'auto ricompare fuori dalla corsia; AccDec avversario ~5 s = uscita non osservata) e Y-61 (la regola BoP esiste già in `OpponentTracker.cs:1093-1110`, è il calcolo MergeGap a non usarla; lo storico dei transiti del Player è già in `SectorTracker.RawNormalHistory`).
- `.ai/PROJECT_STATE.md` — righe Y-60 e Y-61 aggiornate con le correzioni e il rimando al piano.
- Misurato: parti d'ingresso e d'uscita dell'AccDec del Player sul giro della sosta, 5.1 s (0.9086→0.9586) e 6.5 s (0.1016→0.1516), su entrambi i run Daytona.
- Letti gli output di verifica di Gemini (19:23–19:25): coerenti con la review; test 363 PASS, exit 0. Le sue conclusioni scritte non sono nel repository.

### Come verificare
Nessuna build: turno di sola documentazione.
```bash
grep -n "^## Passo" .ai/plans/2026-09-13-daytona-piano-correzioni.md
grep -n "^## 9\." .ai/reviews/2026-09-13-daytona-leader-mergegap-pitloss.md
```
Atteso: 5 righe `## Passo 1…5` nel piano; una riga `## 9. Correzioni dopo il confronto con Andreas` nella review.

### Stato
- ⏭️ Build e test non eseguiti da claude (nessun file di codice modificato); Gemini li ha eseguiti alle 19:24: 363 PASS, exit 0
- ✅ Codice invariato rispetto a `863c65c`

### Per chi entra
**Prossimo passo:** Andreas risponde alle tre domande in fondo al piano (ordine, `Leader.TrackPct` stimata o reale con flag, esecutori); poi passo 1 col lock.
**NON toccare:** `Hardware/`; `PitInOutAccDecTime` = 11.6 nel DB; le soglie gomme sì/no senza il ricontrollo previsto al passo 5.
**Attenzione a:** i criteri numerici del piano valgono per l'ordine proposto (i passi 1 e 2 cambiano i valori attesi dei passi 3 e 5). Se Gemini ha conclusioni diverse da quanto scritto nel piano, vanno aggiunte al piano prima di iniziare.

---

## [2026-09-13 17:45] claude → chiunque entri dopo

**Task:** Review dei replay Daytona `20260913_140133` e `20260913_163743` (il secondo dopo `PitInOutAccDecTime` 17.90 → 11.6 nel DB, modificato a mano da Andreas): leader, MergeGap, stazionario avversari. Nessun file di codice toccato, lock non preso.
**Piano:** — (review: `.ai/reviews/2026-09-13-daytona-leader-mergegap-pitloss.md`)
**Commit:** `0e256bf` (review + Y-58…Y-61), questo (handoff)

### Fatto
- `.ai/reviews/2026-09-13-daytona-leader-mergegap-pitloss.md` — review completa: cosa funziona e cosa no, numeri misurati su tre run dello stesso replay, righe di log, `file:riga`, fix e test proposti con i dati veri.
- `.ai/PROJECT_STATE.md:115-118` — registrati Y-58 (leader: regressione Y-25 da `863c65c` + dead reckoning mai attivato), Y-59 (latch MergeGap su Target `NotInWorld`), Y-60 (stazionario avversari dalla finestra `NotInWorld`), Y-61 (perdita ai box: `ExtZone` "best" + stazionario Target col consumo del Player).
- Verificato che `PitInOutAccDecTime` = 11.6 è corretto: Player 11.63 s (`SimRIG_DebugLog_20260913_163743.csv:7279`), mediana avversari 11.39 s su 32 soste. Effetto: sparito l'errore di +8.3 s del MergeGap nel blocco subito dopo la sosta del Target (ora +1.25 s). Il MergeGap pre-sosta non cambia (+0.43 s in entrambi i run): l'AccDec era un errore di modo comune.

### Come verificare
Nessuna build: turno di sola analisi. Numeri chiave:
```bash
grep -n "FROZEN IN PIT" "Logs/Daytona/SimRIG_MergeGapLog_20260913_163743.txt"
grep -n "Leader Position At Expiry\|Projection Validation" "Logs/Daytona/SimRIG_DebugLog_20260913_163743.csv"
grep -n "Reverse-Engineered\|Opponent Pit Stop Deduction" "Logs/Daytona/SimRIG_DebugLog_20260913_163743.csv"
```
Atteso: congelati −0.92 (righe 1094, 1107) e −9.77 (righe 1159, 1172); `giriCompletati=27` e `vecchioP1=3.536 (err -24.207)`; per Daniel Wieland2 `DeducedStationary: 12.52s` contro `TotalTime: 48.1s`.

### Stato
- ⏭️ Build e test non eseguiti (nessun file di codice modificato; la build installerebbe la DLL in SimHub)
- ✅ Codice invariato rispetto a `863c65c`

### Per chi entra
**Prossimo passo:** Andreas decide ordine e assegnazione di Y-58…Y-61 rispetto a Y-52 Passo 3. Più visibili in dashboard: Y-59 e Y-58. Più piccolo: Y-60 (una riga a `OpponentTracker.cs:1722` + test, ricontrollando le soglie gomme sì/no). Per Y-58, prima di scrivere il fix, loggare `CarIdxLapCompleted` del leader durante un buco.
**NON toccare:** `Hardware/`; il valore 11.6 di `PitInOutAccDecTime` nel DB (verificato); le soglie di classificazione gomme (`OpponentTracker.cs:1790-1850`) senza ricontrollarle coi nuovi stazionari, se si fa Y-60.
**Attenzione a:** i replay Daytona mandano `NotInWorld` le vetture lontane dal Player: ogni fix su leader, gap e soste va validato anche lì, non solo su Road Atlanta. Non "tenere" valori al posto di stimarli: una posizione tenuta è una posizione ferma (Y-35). A replay 2x i log periodici hanno metà righe ma gli stessi valori.

---

## [2026-09-12 22:45] antigravity → chiunque entri dopo

**Task:** Fix Leader.TrackPct (telemetria nativa e hold), Leader.RaceLapsCompleted (sync CurrentLap senza offset _leaderRaceStartLap), e rimozione freeze proiezioni all'ultimo giro
**Piano:** —
**Commit:** questo (fix RaceAnalyzer, test e rilascio lock)

### Fatto
- `User.PluginSdkDemoEdit/RaceAnalyzer.cs`:
  - **Punto 1 (`Leader.TrackPct` non va più a zero)**:
    * `r. 588-630`: Per `state.Position != 1`, `leaderTrackPosPct` ora interroga in via prioritaria `tracker.GetOpponentTrackPosition(overallLeader, state)` (telemetria nativa 60 Hz `CarIdxLapDistPct` via IracingBridge, poi SimHub `TrackPositionPercent`, poi memoria del tracker).
    * Aggiunto campo `_lastGoodLeaderTrackPct` che mantiene l'ultimo valore valido in caso di drop temporaneo di pacchetti SimHub, evitando che la proprietà `SimRIG.Leader.TrackPct` lampeggi a `0.0`.
    * In `r. 834` (`ResolveLeaderAbsolutePos`) e `r. 1325` (diagnostica), unificato l'uso di `leaderTrackPosPct` invece di rileggere il dato grezzo non filtrato.
  - **Punto 2 (`Leader.RaceLapsCompleted` sincronizzato)**:
    * `r. 630-650`: Rimosso l'erroneo latch `_leaderRaceStartLap` (che nel replay Daytona aveva agganciato 6 congelando la differenza a `10 - 6 = 4` al giro 9).
    * I giri completati del leader per gli avversari sono ora semplicemente `Math.Max(0, leaderCurrentLap - 1)` (in iRacing `CurrentLap` è 1-indicizzato), perfettamente allineato a `FlagMoment` (linea 1836).
    * Rimosso il campo `_leaderRaceStartLap` e il suo azzeramento da `ResetSession()`.
  - **Punto 5 (Nessun freeze proiezioni all'ultimo giro)**:
    * `r. 774`: La guardia di uscita anticipata `state.IsTimeLimited && state.SessionTimeLeftSec < 0.0` azzerava immediatamente tutte le proiezioni appena il timer di sessione raggiungeva 0:00, mentre il Player stava ancora correndo l'in-lap finale. Ora la condizione richiede `!_hasSeenPositiveCountdown` (evita l'uscita a gara avviata).
    * `r. 896-908`: Quando `_leaderHasFinished == true`, se il Player non ha ancora tagliato il traguardo (`!_isRaceFinished`), il sistema aggiorna attivamente `RaceLifeTimeLeftSec = remainingLapFraction * activePlayerPace`, mantiene `RaceTotalLaps = _latchedPlayerTotalReality`, e `RaceLapsRemaining` scala dolcemente la frazione residua (`1.0 - effTrackPos`) fino alla linea del traguardo.
    * `r. 1268-1286`: `IsLapsPredictionValid` rimane `true` durante l'in-lap finale.
    * `r. 776-785`: Quando la gara è formalmente conclusa (`_isRaceFinished == true`), `RaceTotalLaps` preserva il totale latchato invece di azzerarsi a 0.
- `User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/UnitTests/LeaderSampleUnitTests.cs`:
  - Aggiunti test mirati:
    * `Test_OpponentLeaderLapsCompleted_MatchesCurrentLapMinusOne`
    * `Test_LeaderTrackPct_HoldsLastGoodWhenTelemetryDrops`
    * `Test_LastLapInLapProjections_ActiveAfterTimeExpiry`
- Build e Test:
  - MSBuild VS2022: 0 errori, installazione DLL in SimHub completata.
  - Test runner: **363 PASS (100% success)**, exit code 0.

### Come verificare
```bash
& "C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
& "User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```
Atteso: build 0 errori, 363 test PASS (100%), exit code 0.

### Stato
- ✅ Compila senza errori
- ✅ 363 PASS (100%)

### Per chi entra
**Prossimo passo:** Continuare l'analisi con Andreas sulle metriche di gara e backtest Daytona.
**NON toccare:** `Hardware/` (territorio di Andreas).
**Attenzione a:** `PitDistanceMeters: 813.35m` a Daytona è strettamente la distanza fisica tra `PitEntryPct` e `PitExitPct` (`PitRadar.cs:1740-1745`). La formula di pit loss è `TotalPitLoss = Stationary + (PitTransitTime + InOutAccDecTime - ExtendedPitZoneRacingTime)`.

---

## [2026-09-12 14:00] antigravity → chiunque entri dopo

**Task:** Esposizione proprietà SimRIG.Hardware (WheelMode, WheelMessage, LiveBitePoint), retrocompatibilità e migrazione dash Test.djson
**Piano:** —
**Commit:** `afbda6d` (hardware properties & UI), questo (handoff e rilascio lock)

### Fatto
- `User.PluginSdkDemoEdit/DataPluginDemo.cs:98, 309-312, 719-733, 1675-1680`:
  - Registrate come proprietà ufficiali SimHub:
    * `SimRIG.Hardware.WheelMode` (string, default "NORMAL"): modalità attiva ricevuta dal volante via seriale/USB.
    * `SimRIG.Hardware.WheelMessage` (string, default "READY"): messaggi e notifiche a display.
    * `SimRIG.Hardware.LiveBitePoint` (double, default 50.0): percentuale live punto di stacco frizione.
  - Aggiornate in tempo reale all'evento seriale `HardwareManager_OnHardwareInputReceived()` (`MODE`, `MSG`, `VAL`) e nel ciclo `UpdateSimHubProperties()`.
  - Mantenute le delegazioni `PersoSteeringWheelMode`, `PersoSteeringWheelMessage`, `PersoSteeringWheelLiveBitePoint` e la proprietà `SimRIG.Mode` per retrocompatibilità trasparente al 100%.
  - Aggiunta proprietà pubblica C# `LiveBitePoint => _liveBitePoint` mantenendo `PersoSteeringWheelLiveBitePoint` come getter alias.
- `User.PluginSdkDemoEdit/SettingsControlDemo.xaml.cs:1797`:
  - Aggiornato il binding UI da `Plugin.PersoSteeringWheelLiveBitePoint` a `Plugin.LiveBitePoint`.
- `E:/SimHub/DashTemplates/Test/Test.djson`:
  - Creato backup di sicurezza in `Test.djson.bak`.
  - Aggiornate tutte le formule dei 14 Item/gruppi di cambio schermata (RACE, PIT, PIT2, STRAT, FORECAST, MAP, TESTS, TEST, PRECISE/GROSS BITE, CLUTCH CAL) mappando `DataPluginDemo.PersoSteeringWheelMode` su `DataPluginDemo.SimRIG.Hardware.WheelMode`.
  - Aggiornate le notifiche popup mappando `PersoSteeringWheelMessage` su `SimRIG.Hardware.WheelMessage`.
  - Aggiornati i campi bite point su `SimRIG.Hardware.LiveBitePoint`.
  - Allineati i campi diagnostici nelle pagine `STRAT`, `TEST` e `TESTS` alle proprietà unificate (`CurrentTank`, `TankLapsRemaining`, `LastPitFuelAdded`, `LastPitStationaryTime`, `EstimatedStationaryTime`, `Leader.RaceTotalLaps`, `Leader.ProjectedPosAtCheckered`).
- Build e test:
  - MSBuild VS2022: 0 errori, plugin installato in `%SIMHUB_INSTALL_PATH%`.
  - Test runner: **360 PASS (100% success)**.

### Come verificare
```bash
& "C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
& "User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```
Atteso: build 0 errori, 360 test PASS (100%), exit code 0.

### Stato
- ✅ Compila senza errori
- ✅ Test passano (360 PASS, 100%)
- ✅ `Test.djson` migrato e validato JSON con 0 errori (backup in `Test.djson.bak`)

### Per chi entra
**Prossimo passo:** Procedere con la roadmap delle feature successive concordate con Andreas.
**NON toccare:** `Hardware/` (territorio di Andreas).
**Attenzione a:** Le proprietà hardware sono ora ufficialmente esposte come `SimRIG.Hardware.WheelMode`, `SimRIG.Hardware.WheelMessage`, `SimRIG.Hardware.LiveBitePoint` sia in SimHub che nella dashboard del volante.

---

## [2026-09-12 12:45] antigravity → chiunque entri dopo

**Task:** Pulizia e rimozione proprietà SimHub obsolete/morte, deduplica e consolidamento namespace Leader
**Piano:** —
**Commit:** `dc1a53d` (refactor proprietà), questo (handoff e rilascio lock)

### Fatto
- `User.PluginSdkDemoEdit/DataPluginDemo.cs:214-223`:
  - Rimosse le registrazioni delegate ridondanti `Enc_TopLeft_Label`, `Enc_TopRight_Label`, `Enc_BotLeft_Label`, `Enc_BotRight_Label` (già coperte da `SimRIG.Input.Enc_*_Label`) e `LeftWidgetPage`, `BottomLeftWidgetPage`, `BottomRightWidgetPage` (già coperte da `SimRIG.*WidgetPage`).
  - Mantenute intatte le proprietà delegate richieste dal volante fisico: `PersoSteeringWheelMode`, `PersoSteeringWheelMessage`, `PersoSteeringWheelLiveBitePoint`.
- `User.PluginSdkDemoEdit/DataPluginDemo.cs:655-665`:
  - Rimosse 35 proprietà morte non aggiornate create nel loop `for (int i = 1; i <= 7; i++)`: `SimRIG.Relative.R{i}_Pos`, `_Name`, `_Gap`, `_LastLap`, `_Class` (residui storici mai usati dopo lo split nei widget Left/Right).
- `User.PluginSdkDemoEdit/DataPluginDemo.cs:319, 424, 495, 500, 507, 509, 512, 1690, 1702, 1850-1865`:
  - Eliminate duplicazioni matematiche al 100%:
    * `SimRIG.Target.ProjectedStationaryTime` (duplicato esatto di `SimRIG.Target.EstimatedStationaryTime`)
    * `SimRIG.Target.EstimatedFuelTank` (duplicato esatto di `SimRIG.Target.CurrentTank`)
    * `SimRIG.Fuel.EstimatedPitWindow` (duplicato esatto di `SimRIG.Fuel.TankLapsRemaining`)
    * `SimRIG.Target.EstimatedPitWindow` (duplicato esatto di `SimRIG.Target.TankLapsRemaining`)
    * `SimRIG.Pit.SelectedTireTime` (duplicato esatto di `SimRIG.Tyres.SelectedTireTime`)
    * `SimRIG.Session.PitLayoutMode` (duplicato esatto di `SimRIG.Pit.PitLayoutMode`)
    * `SimRIG.Target.TargetMode` (duplicato esatto di `SimRIG.Target.Mode`)
- `User.PluginSdkDemoEdit/DataPluginDemo.cs:502, 505, 381, 388, 1711, 1855, 1860`:
  - Risolta la sovrapposizione concettuale e ambiguità nei nomi:
    * `SimRIG.Target.CalculatedStationaryTime` -> `SimRIG.Target.LastPitStationaryTime` (durata stop passata misurata vs stime future)
    * `SimRIG.Target.EstimatedFuelAdded` -> `SimRIG.Target.LastPitFuelAdded` (carburante imbarcato nello stop appena concluso vs stime future)
    * `SimRIG.Strategy.RemainingPitsPlayer` -> `SimRIG.Strategy.PlayerPitsRemaining` (uniformità con `Leader.PitsRemaining` e `Target.PitsRemaining`)
    * `SimRIG.Strategy.IsPredictionValid` -> `SimRIG.Fuel.IsPredictionValid` (spostata sotto Fuel per distinguere la validità stime carburante da `SimRIG.Session.IsLapsPredictionValid`)
- `User.PluginSdkDemoEdit/DataPluginDemo.cs:338-355, 375-387, 1715-1735`:
  - Consolidamento namespace Leader: accorpate tutte le metriche del Leader di gara sotto `SimRIG.Leader.*`:
    * `Pace`, `PaceStr`, `AveragePace` (da `SimRIG.Strategy.Leader*`)
    * `StintLaps`, `PitsRemaining`, `PitLossTime`, `DataSource` (da `SimRIG.Strategy.Leader*`)
    * `RaceTotalLaps`, `RaceLapsCompleted`, `RaceLapsRemaining`, `ProjectedPosAtCheckered`, `TrackPct` (da `SimRIG.Session.Leader*`)
- Build e test:
  - Soluzione compilata con successo (0 errori, MSBuild VS2022).
  - Test runner: **360 PASS (100% success)**.

### Come verificare
```bash
& "C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
& "User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```
Atteso: build 0 errori, 360 test PASS (100%), exit code 0.

### Stato
- ✅ Compila senza errori
- ✅ Test passano (360 PASS, 100%)

### Per chi entra
**Prossimo passo:** Procedere con la roadmap delle feature successive concordate con Andreas.
**NON toccare:** `Hardware/` (territorio di Andreas).
**Attenzione a:** Se si configurano nuove dashboard SimHub, fare riferimento alle proprietà unificate sotto `SimRIG.Leader.*` e ai nuovi nomi non ambigui (`LastPitStationaryTime`, `LastPitFuelAdded`, `PlayerPitsRemaining`, `SimRIG.Fuel.IsPredictionValid`).

---

---

## [2026-09-11 22:50] antigravity → chiunque entri dopo

**Task:** Congelamento (latch) del ProjectedMergeGap durante la fase attiva di pit stop di Target e Player
**Piano:** —
**Commit:** `722a9d6` (latch e test), questo (handoff e rilascio lock)

### Fatto
- `User.PluginSdkDemoEdit/TargetStrategyManager.cs:115, 165, 875-925, 1835-1845, 1895-1910`:
  - **Latch ProjectedMergeGap durante i box:** implementato `UpdateProjectedMergeGap(double rawMergeGap, bool isTargetInPit, bool isPlayerInPit, string targetName)` e introdotte proprietà `IsMergeGapLatched` in `TargetState` e `TargetStrategyManager`.
  - Quando il Target o il Player è in corsia box (`isTargetInPit`: `IsOnPitRoad || IsInPitStall || IsInsideGeofence || TrackSurface == InPitStall`; `isPlayerInPit`: `PlayerData.IsOnPitRoad || TrackSurface == InPitStall || state.IsInPitLane`), il valore di `CurrentTarget.ProjectedMergeGap` viene congelato all'ultima stima on-track stabile prima dell'ingresso (`_lastOnTrackProjectedMergeGap`).
  - Questo impedisce a `ProjectedMergeGap` di collassare temporaneamente sul `LiveSignedGap` mentre l'auto è ferma in piazzola o percorre la pitlane a limitatore, mantenendo sulla dashboard/HUD il gap di ricongiungimento previsto.
  - Al rientro effettivo di entrambe le vetture in pista a regime di gara (`!isTargetInPit && !isPlayerInPit`), il latch si rilascia istantaneamente e il valore converge sul gap on-track reale.
  - Aggiunti guard di sicurezza: timeout di 120s (in caso di ritiro/tow) e reset immediato in caso di cambio target o `ResetSession()` / `SetNoTarget()`.
  - Monitor log `SimRIG_MergeGapLog` aggiornato con tag `[FROZEN IN PIT]` durante la fase di blocco.
- `User.PluginSdkDemoEdit/DataPluginDemo.cs:468, 612, 1840, 2055`:
  - Registrata ed esposta la nuova proprietà SimHub: `SimRIG.Target.IsMergeGapLatched` (bool).
- `User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/UnitTests/NativeIracingOpponentTrackingUnitTests.cs:59, 1368-1428`:
  - Aggiunto unit test esaustivo: `Test_ProjectedMergeGap_LatchesDuringPitStopAndUnlatchesOnTrackExit` che copre:
    1. Fase pre-pit in pista (unlatched, -1.90s).
    2. Ingresso pit road Target (latched, resta -1.90s anche se il raw gap crolla a +20s).
    3. Fermata in piazzola e sfilamento Player (latched, resta -1.90s con raw gap a +5.0s e -3.5s).
    4. Rientro Target in pista (unlatch immediato, gap reale -5.25s).
    5. Test simmetrico per Player in pit lane (latch a +12.40s, unlatch all'uscita).
    6. Safety guard per cambio target durante il pit.
  - Suite di test: **360 PASS (100% success)**.

### Come verificare
```bash
& "C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
& "User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```
Atteso: build 0 errori, 360 test PASS (100%), exit code 0.

### Stato
- ✅ Compila senza errori
- ✅ Test passano (360 PASS, 100%)
- ✅ Verificato su replay reale Road Atlanta (`Logs/Road Atlanta/*20260911_231106*`):
  - **Sosta Player (giro 15, TLeft 1561.8s)**: Player in box (`InPitStall`, `InPitRoad: True`), `LiveSignedGap` schizza a +17.05s, `ProjectedMergeGap` congelato a `-0.35s [FROZEN IN PIT]`. Rientro in pista: unlatch pulito, gap reale +33.77s / stima +0.94s.
  - **Sosta Target (giri 20-21, TLeft 1111.4s - 1081.3s)**: Bruno Carneiro entra ai box (`InPitRoad: True`), `LiveSignedGap` passa da +27.94s a -3.77s quando il Player lo supera sfilandogli a fianco sul rettilineo mentre è fermo in piazzola: `ProjectedMergeGap` rimane perfettamente congelato a `-1.38s [FROZEN IN PIT]` senza alcuna fluttuazione!
  - **Rientro Target in pista (giro 21, TLeft 1051.2s)**: unlatch immediato, `ProjectedMergeGap: -5.25s` identico a `LiveSignedGap: -5.25s` on-track.
  - Sosta Bruno: 16.30s stazionari reali contro 16.50s previsti (delta di appena 0.20s!), classificata correttamente `Simultaneous (No Tires)`.
  - Transiti sul rettilineo dei box a 250–264 km/h scartati al 100% come `Confirmed on track` per tutti i 20 giri precedenti.

### Per chi entra
**Prossimo passo:** Motore di tracciamento soste e proiezione ricongiungimento (`ProjectedMergeGap` + Latching) empiricamente validati e stabilizzati al 100% su Road Atlanta sia per sosta Player che Target. Procedere con le feature successive della roadmap.
**NON toccare:** `Hardware/`, file `*_LEGACY.cs`.
**Attenzione a:** Se si gestiscono multiclassi con sorpassi in pit lane da vetture di classi diverse, il latch isola specificamente il delta fra Player e Target corrente.

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

