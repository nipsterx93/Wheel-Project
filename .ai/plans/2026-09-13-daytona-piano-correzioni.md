# Piano correzioni — leader, MergeGap e perdita ai box (replay Daytona del 13/09)

- **Data:** 2026-09-13
- **Autore:** claude
- **Esecutore:** claude (deciso da Andreas il 2026-09-13), un passo per turno col lock
- **Stato:** ✅ Approvato da Andreas il 2026-09-13 — ordine 1 → 5 (vedi "Decisioni prese" in fondo).
  Passo 1 fatto il 2026-09-13 (`05f0002`), corretto (`c18a1b0`) e verificato sul replay `082515` il
  2026-09-14. Passo 2 fatto e verificato (`3e9d4ae`, replay `094551`); prossimo Y-62 (fuori piano, vedi
  "Decisioni prese"), poi il passo 3.
- **Basato su:**
  - review `.ai/reviews/2026-09-13-daytona-leader-mergegap-pitloss.md`, incluse le correzioni del §9;
  - confronto con Andreas del 13/09 sera: regola BoP del consumo, loop chiuso della pit road con
    `NotInWorld`, transito del Player;
  - verifiche indipendenti di Gemini sugli stessi log (dai suoi output delle 19:23–19:25). Le sue
    conclusioni scritte non sono nel repository: se su qualcosa diverge, va aggiunto qui.
- **Sostituisce** i "Fix proposto" di Y-60 e Y-61 nella review.

## Obiettivo

Alla fine, rigirando il replay Daytona (`20260913_163743`) con la DLL nuova, devono essere vere
queste cose:

1. MergeGap prima delle soste entro ±1 s dal reale (oggi +0.43 s contro −2.7 s).
2. Valore congelato durante la sosta del Player entro ±1.5 s dal reale (oggi −9.77 s contro −2.7 s).
3. `SimRIG.Leader.RaceLapsCompleted` mai a 0 dopo il primo valore valido (oggi nel 24.4% delle
   righe); `Leader.TrackPct` mai ferma durante i buchi di dati.
4. Stazionario e AccDec degli avversari salvati come misurati solo se l'uscita dalla corsia è stata
   osservata.
5. Nessuna regressione: test verdi (Gemini li ha contati alle 19:24: 363 PASS, exit 0) e replay
   Road Atlanta `20260911_231106`, quello usato per validare il latch `722a9d6`.

## Vincoli

- Un passo = un turno col lock, commit piccolo, test ADR-004 che fallisce togliendo il fix, con i
  numeri di questi log (non inventati).
- La build installa la DLL in SimHub: chiudere SimHub prima.
- Ogni passo va validato anche su Daytona (vetture lontane `NotInWorld`), non solo su Road Atlanta.
- Non toccare: `Hardware/`; `PitInOutAccDecTime` = 11.6 nel DB (verificato); le soglie gomme sì/no,
  salvo il ricontrollo previsto al passo 5.
- I criteri numerici sono scritti per l'ordine proposto: i passi 1 e 2 cambiano i valori attesi dei
  passi 3 e 5.

---

## Passo 1 — Consumo BoP del Target nel calcolo MergeGap/undercut (Y-61)

> ✅ **Fatto il 2026-09-13** (`05f0002`, claude): 367 PASS, replay Daytona da verificare. Allargato da
> Andreas a `SimRIG.Target.TankLapsRemaining` e al tetto del rifornimento (spazio libero nel serbatoio del
> Target invece della capienza del Player). Salvato solo il consumo verde: il giallo non ha consumatori.
> Dettagli e scostamenti: voce del 2026-09-13 23:12 in `.ai/HANDOFF_LOG.md`.
>
> ⚠️ **Corretto il 2026-09-14** (`c18a1b0`) dopo il replay `070557`. Nei giri 11-15 il passo funziona (errore
> medio del MergeGap prima delle soste da +3.74 a +0.37 s), ma il tetto sullo spazio libero attuale abbassava
> la sosta prevista del Target a inizio stint (2.7 s al giro 1 invece di ~17.4) e l'errore medio nei giri 2-7
> saliva da +5.95 a +9.50 s. Ora il tetto è la capienza del Target, come nel testo del passo.
>
> ✅ **Verificato il 2026-09-14** sul replay `082515`: errore medio del MergeGap prima delle soste +0.33 s nei
> giri 2-15, tutti i blocchi fra −0.43 e +0.87 s (prima del passo 1: +4.84). Il giro 1 resta a −14.8 s per lo
> stazionario del Player a 0 (fuori piano). Con l'undercut ora viable si vede Y-62 (traffico a metà giro).

**Problema.** La regola esiste già: `OpponentTracker.cs:1093-1110` calcola
`opponentMaxTank × consumo Player / playerMaxTankBoP`, per il Target 60 × 3.0 / 50 = **3.60 L/giro**
(`GreenBurn: 3.60L/lap`). Però `classFuelBurn` è una variabile locale, non salvata sull'avversario, e
`TargetStrategyManager.cs:952` usa il consumo del Player così com'è (3.0) per l'autonomia
(`targetFuelLaps`, `:953`), il deficit e quindi `targetNeedsPit` (`:954`) e il carburante da imbarcare
(`:975-977`). Il blocco del log MergeGap (`:1428-1449`) ripete lo stesso calcolo.

**Modifica.**
1. Salvare su `OpponentTelemetryData` il consumo BoP verde e giallo, dove oggi si calcola
   `classFuelBurn` (`OpponentTracker.cs:1083-1123`).
2. In `TargetStrategyManager.cs:952-977` usarlo (ripiego sul consumo del Player solo se vale 0).
3. Una sola funzione per carburante e stazionario del Target, usata sia dal calcolo sia dal log
   (oggi sono due copie).

**Test** (dati `163743`, giro 15). Player 3.0 L/giro con serbatoio BoP 50 L; Target 60 L,
`EstimatedFuel` 6.9 L, giri rimanenti 11.1, fill rate 2.50 L/s. Atteso: consumo Target **3.60**,
carburante da imbarcare ≈ 34.1 L, stazionario ≈ 15.7 s (oggi 12.79), autonomia 1.9 giri (oggi 2.3).

**Criterio sul replay.** MergeGapLog al giro 15: `Target (+≈37.3s) : Staz: ≈15.7s`, MergeGap
≈ −2.5 s (oggi +0.43).

**Fuori da questo passo.** Per le vetture di altre classi il consumo è fisso a 3.0 L/giro
(`OpponentTracker.cs:1119-1123`; il leader GTP ha `GreenBurn: 3.00L/lap`).

## Passo 2 — Tempo di corsa nella zona estesa: mediana del Player, non minimo di classe (Y-61)

> ✅ **Fatto il 2026-09-14** (`3e9d4ae`, claude): 371 PASS. **Verificato sul replay `094551`**: `ExtZone` 23.73 s,
> perdita del Player 33.84 s (reale 34.7), MergeGap subito dopo la sosta del Target −2.93 s contro −2.7 reali. Mediana degli ultimi 7
> transiti validi del Player (`SectorTracker.RecentRawTimeMedian`), con ripiego sul minimo di classe sotto i 3
> transiti o sotto il pavimento fisico (`OpponentTracker.ExtendedRacingReferenceTime`). La usano il calcolo
> MergeGap/undercut e, deciso con Andreas, `SimRIG.Pit.TotalPitLoss`. Restano sul minimo di classe le soglie di
> rilevamento soste, il ripiego del leader e la proiezione del totale giri (`RaceAnalyzer.cs:1186`).

**Problema.** `ExtendedRacingTime` (`TargetStrategyManager.cs:932-934` → `CarPitData.cs:165-170`) usa
`ClassBestExtendedPitZoneTime`: il **minimo** fra `PlayerExtendedPitZone.BestRawTime` (passato a
`DataPluginDemo.cs:1273`) e i best degli avversari di classe (`OpponentTracker.cs:890-907`) → 21.93 s.
Il transito di corsa tipico del Player vale 23.8 s (giri 13–19: 23.5–23.9 s).

**Modifica.**
1. In `SectorTracker` esporre la mediana degli ultimi 5–7 transiti validi. Lo storico c'è già
   (`RawNormalHistory`, `SectorTracker.cs:23, 173`), ma cresce senza limite.
2. Passare a `OpponentTracker.Update` la mediana del Player invece di `BestRawTime`
   (`DataPluginDemo.cs:1273`) e usarla come `ExtendedRacingTime`.
3. Ripiego sul valore di oggi finché il Player ha meno di 3 transiti validi; tenere il pavimento fisico
   (`OpponentTracker.cs:879-888`).

**Test.** Transiti del Player dal log `140133`: 23.76, 23.70, 23.73, 23.66, 23.92, 24.24, 23.97,
23.74, 23.69, 23.69, 23.76, 23.57, 23.59, 23.53 → mediana ≈ 23.7 s; aggiungendo 28.12 (giro con fuori
pista) la mediana non si sposta; con 2 soli transiti → ripiego.

**Criterio sul replay.** MergeGapLog `ExtZone` ≈ 23.8 s; perdita prevista del Player ≈ 33.8 s (reale
34.7); MergeGap subito dopo la sosta del Target ≈ −3.3 s (oggi −1.45; reale −2.7). Il residuo
(~−0.9 s) viene da transito e stazionario del Player: fuori da questo piano.

**Rischio.** Tutte le perdite ai box scendono di ~1.9 s: alcune raccomandazioni undercut/overcut
possono cambiare. Ricontrollarle sul replay Road Atlanta.

## Passo 3 — MergeGap: niente valori da Target `NotInWorld` nel latch (Y-59)

**Problema.** Con Target `NotInWorld` il gap per microsettori (`TargetStrategyManager.cs:713-731`) usa
una posizione ferma e cresce di ~1 s al secondo; `_lastOnTrackProjectedMergeGap` (`:242-250`) lo
registra e il latch alla sosta del Player (`:273-280`) congela −9.77 s.

**Modifica.**
1. Campione del Target "stantio" se `TrackSurface == NotInWorld`, o se la posizione non avanza da più
   di ~1 s di sessione.
2. Con campione stantio: non aggiornare `SignedGapSeconds` (`:744`) — tenere l'ultimo gap fresco — e
   non aggiornare `_lastOnTrackProjectedMergeGap`.
3. Se il campione resta stantio a lungo: far invecchiare il gap tenuto col delta di passo, oppure
   dichiararlo non valido dopo un timeout.
4. **Congelare già quando il Target passa ad `ApproachingPits`**, non solo al flag di corsia box (aggiunto il
   2026-09-14 dopo il replay `070557`, deciso con Andreas). Da `ApproachingPits` (TL 1092.7, PosPct 92.98%)
   il Target frena: il gap live scende da −0.81 a circa −2.1 prima del flag di corsia box (TL 1089.2),
   mentre il MergeGap continua a sottrarre tutto l'AccDec, quindi la frenata entra nel conto due volte.
   Valore congelato alla sosta del Target: −3.62 s, contro −2.31 s ad `ApproachingPits` (reale −2.7:
   errore −0.9 invece di +0.4). Stessa deriva nel run `163743` (−0.92 contro +0.43). `ApproachingPits`
   compare solo alla sosta vera: 3 eventi per run, a TL 1092.7, 1089.2 e 1044.6. Da verificare anche
   per la sosta del Player. **Test:** gap −0.808 al primo tick dopo `ApproachingPits` (Snapshot `070557`:1749,
   TL 1092.617), perdite Player 35.713 e Target 37.212 → congelato −2.31, non −3.62.

**Test.** Gap fresco −37.50 (`140133` Snapshot:3840), gap stantio −45.23 (:3858), perdita Player
35.71: valore congelato −1.79, non −9.52. Il test deve fallire togliendo il fix.

**Criterio sul replay** (`163743`). Nessun blocco del MergeGapLog sotto −3 s tra TL 1003 e 985 (oggi
−11.27 a TL 993.5); `[FROZEN IN PIT]` durante la sosta Player ≈ −3.6 s coi passi 1–2 applicati
(≈ −1.8 s senza), contro −2.7 reali.

## Passo 4 — Leader nei buchi di dati (Y-58)

**4a — solo log, prima di tutto.** Durante un buco del leader, loggare `CarIdxLapCompleted`,
`CarIdxLap` e `CarIdxLapDistPct` nativi del suo `carIdx` (il bridge li legge già:
`IracingTelemetryBridge.cs:28-30, 42-44`). Serve a sapere se i giri nativi restano validi con l'auto
`NotInWorld`: se sì, 4b li usa per i giri; se no, resta solo la stima.

**4b — fix.**
1. Separare la freschezza dal valore: `rawPos > 0` in questo tick (`RaceAnalyzer.cs:599-621`).
2. `HoldLeaderLapsCompleted` (`:669-670`) e `ResolveLeaderAbsolutePos` (`:848-859`) ricevono il dato
   grezzo, non quello tenuto.
3. Ancora del dead reckoning = ultima posizione assoluta fresca **per pilota**, reset al cambio di
   leader (oggi `_lastGoodLeaderTrackPct` non è legato al pilota).
4. Con giri mancanti: posizione = ancora + tempo trascorso / passo, senza il vincolo
   `[giri noti, giri noti + 1)` (`:1652-1671`) e con un limite sul tempo; giri mostrati = parte intera.
5. `OpponentTracker.cs:601`: trattare 1.0 come 0.0.

**Test.** Buco `140133` DebugLog:11532→12223: ultimo campione fresco 23.391 a TL 410.4, passo 90.195
→ a TL 256.7 posizione ≈ 25.095 (reale 25.112), giri mostrati 23 → 24 → 25, mai 0. Test sul
**chiamante**: quello sulla sola funzione (`LeaderSampleUnitTests.cs:106`) è già verde col difetto.

**Criterio sul replay.** `LapsComp=0` solo prima del primo valore valido; `Projection Validation` a
−5 min con `vecchioP1` entro 0.5 giri dal vero (oggi `3.536`, errore −24 giri).

**Deciso da Andreas (2026-09-13):** `SimRIG.Leader.TrackPct` mostra la posizione **stimata** (quella del
punto 4 di 4b), non l'ultima reale.

## Passo 5 — Stazionario e AccDec degli avversari senza loop chiuso (Y-60, rivisto)

**Perché la proposta della review non basta.** Con l'auto `NotInWorld`, `OpponentTracker.cs:1349-1360`
forza la pit road a true e il cronometro della corsia (`:1515-1519`, `:1687-1705`) si ferma quando
l'auto ricompare: se ricompare già fuori dalla corsia, il tempo in corsia l'abbiamo deciso noi. Si
riconosce dall'AccDec. Sul giro della sosta il Player impiega 5.1 s da 0.9086 a 0.9586 e 6.5 s da
0.1016 a 0.1516 (misurato su entrambi i run). 24 soste avversarie hanno AccDec 11.0–12.3 s (uscita
vista in tempo); 6 hanno 4.6–5.5 s, cioè la sola parte d'ingresso: sono ricomparse oltre la zona
estesa, con tempi gonfiati di almeno 6.5 s.

**5a — solo log.** Alla prima ricomparsa dopo `NotInWorld`: TL, posizione, `CarIdxOnPitRoad` nativo,
superficie. Un replay dice quanto è frequente ciascun caso.

**5b — fix.**
1. Guardia: AccDec avversario < 8 s, o negativo (c'è un −454 s) → uscita non osservata → non salvare
   `LastPitInOutAccDecTimeSec` (`OpponentTracker.cs:2571-2579`) né lo stazionario come misurati.
2. Uscita dalla corsia:
   - ricomparsa con flag nativo ancora true → uscita osservata davvero (caso del Target, DebugLog
     `163743`:6656 → :6682);
   - ricomparsa tra 0.1016 e 0.1516 → uscita = passaggio a 0.1516 − 6.5 s;
   - ricomparsa oltre 0.1516 → passaggio a 0.1516 ricostruito all'indietro coi microsettori della
     vettura, poi − 6.5 s.
3. Stazionario = (uscita − entrata) − `PitTransitTime` (oggi `NotInWorldDuration − PitTransitTime`,
   `:1722`).
4. Plausibilità: una sosta senza gomme non può durare meno del rifornimento stimato col consumo BoP
   del passo 1, meno una tolleranza; altrimenti confidenza bassa e valore non propagato (ADR-005).
5. Ricontrollare le soglie gomme sì/no (`:1754-1850`): la classificazione è già instabile (la sosta di
   Ethan Carlton Wong è `No Tires` a 1x e `Tires Changed` a 2x, con lo stesso stazionario).
6. `Pit Loss Dissection` (`:1892-1901`): leggere il transito della sosta a zona chiusa, non all'uscita
   dalla corsia (solo log).

**Test.** Target (DebugLog `163743`:6435, 6677–6719): entrata 1089.20, uscita osservata 1041.07 →
stazionario 16.18 s (oggi 12.52). Matt Loveridge (AccDec 4.93) → uscita non osservata → AccDec non
salvato.

**Dipende da:** passo 1 (controllo di plausibilità col carburante).

---

## Annotati, fuori da questo piano

- Totale leader 30–31 invece di 28 nei primi 7 giri: passo seed 90.195 s contro 96.2 reali.
- Calo del totale leader 28→25 per un tick del punto 4 (`comanda=PLAYER … inLotta=1`), presente anche
  prima di `863c65c`.
- Stazionario del Player a 0 nel giro 1 → MergeGap −11 s per un giro.
- Residuo nella perdita ai box del Player: transito 31.95 s nel DB contro 32.48 misurati, stazionario
  14.01 contro 14.47.
- `PlayerPit=True` per ~1 s durante un fuori pista.
- Consumo fisso 3.0 L/giro per le vetture di altre classi.

## Verifica (a ogni passo)

```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
```
```bash
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```
Atteso: exit 0; numero di PASS letto dall'output (363 oggi) più i test nuovi del passo. Poi rigirare il
replay Daytona e controllare i criteri del passo:
```bash
grep -n "FROZEN IN PIT\|LiveSignedGap" "Logs/Daytona/SimRIG_MergeGapLog_<run>.txt"
grep -n "Projection Validation\|Reverse-Engineered\|Opponent Pit AccDec Details" "Logs/Daytona/SimRIG_DebugLog_<run>.csv"
```

## Decisioni prese (Andreas, 2026-09-13)

1. **Ordine:** passi 1 → 2 → 3 → 4 → 5, adesso, prima di Y-52 Passo 3 (roadmap aggiornata).
2. **Y-58:** `SimRIG.Leader.TrackPct` mostra la posizione **stimata**.
3. **Esecutore:** claude, un passo per turno col lock.
4. **Passo 3, punto 4** (2026-09-14): congelare il MergeGap già su `ApproachingPits` del Target, oltre che su
   `NotInWorld` (numeri nel passo 3).
5. **Y-62** (2026-09-14, undercut che si spegne per "traffico" a metà giro, trovato sul replay `082515`):
   si corregge dopo il passo 2 e prima del passo 3, fuori da questo piano. Il passo 2 sposta la bolla del
   traffico (`timeGap − playerTotalPitLoss`), quindi i riferimenti di Y-62 vanno ripresi dal replay del passo 2.
   **Aggiornato il 2026-09-14:** i log di `094551` non dicono quale vettura faccia scattare il traffico. Deciso con
   Andreas: prima una diagnostica (`32a8682`, solo log), poi il fix sui numeri del replay successivo.

## Come si riparte in una nuova sessione

1. `/new-session esegui il passo N di .ai/plans/2026-09-13-daytona-piano-correzioni.md`, con N = primo
   passo non ancora chiuso (lo dicono le voci in cima a `.ai/HANDOFF_LOG.md`).
2. Prendere il lock con scope limitato ai file del passo (`[claude] chore: acquire lock — …`).
3. Test ADR-004 prima del fix (deve fallire), poi fix, build + test, commit, handoff, rilascio lock.
4. Il criterio "sul replay" richiede che Andreas rigiri il replay Daytona in SimHub con la DLL nuova e
   indichi il nome del log. Senza, il passo si chiude come "test verdi, replay da verificare".
5. Gli script di analisi usati per la review erano nella cartella temporanea della sessione del 13/09 e
   non sono nel repository: i numeri di riferimento sono tutti in questo piano e nella review.
