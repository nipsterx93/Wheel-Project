# Review — Replay Daytona del 13/09: leader, MergeGap, perdita ai box

- **Autore:** claude — sessione di sola analisi: nessun file di codice toccato, nessun lock preso.
- **Data:** 2026-09-13.
- **Codice analizzato:** `863c65c` (HEAD, working tree pulito durante l'analisi).
- **Build/test:** non eseguiti in questo turno (la build installa la DLL in SimHub). Tutti i numeri
  qui sotto vengono dai log e dalla lettura del codice.
- **Punti registrati:** Y-58, Y-59, Y-60, Y-61 in `.ai/PROJECT_STATE.md`. (Y-57 compare solo come
  ipotesi nel piano `2026-09-06-riconciliazione-roadmap.md`, mai registrato: non l'ho riusato per non
  creare ambiguità.)
- **Richiesta di Andreas:** analizzare il run `20260913_163743`, fatto dopo aver impostato
  `PitInOutAccDecTime` = 11.6 in `E:\SimHub\SimRIG_Data.json`, dire cosa è cambiato, e lasciare un
  report leggibile anche da Gemini/Antigravity.

---

## Contesto e come leggere i numeri

Replay Daytona Road Course (`DAYTONA 2011 ROAD_IMSA23`), IMSA multiclasse, gara a tempo da 45 min.
Player: Porsche 911 GT3 R (992). Target: Daniel Wieland2 (Lamborghini Huracán GT3 EVO) per tutta la
gara. Leader assoluto: un GTP (Daan Scholing per la maggior parte della gara).

I log sono in `Logs/Daytona/` (gitignored: esistono solo sulla macchina di Andreas).

| Run | Velocità replay | `PitInOutAccDecTime` nel DB | Uso in questa review |
|---|---|---|---|
| `20260912_142223` | 1x | 17.90 | prima di `863c65c`: solo confronto sul leader |
| `20260913_140133` | 1x | 17.90 | dopo `863c65c` |
| `20260913_163743` | ~1.97x | **11.6** (modificato a mano da Andreas) | dopo `863c65c` |

È **lo stesso replay** in tutti e tre i run (stessi eventi agli stessi TL, es. `Total Laps
Transition` a TL 2536.2 e 375.9): la verità di terreno è comune e i confronti sono diretti. A 2x i log
periodici hanno circa metà righe (una riga diagnostica ogni ~2 s di sessione invece di 1 s), ma
percentuali e misure coincidono: i risultati sono riproducibili.

Convenzioni: TL = `SessionTimeLeft` (s); gap negativo = Player davanti. `DebugLog`, `MergeGapLog`,
`Snapshot` = `SimRIG_DebugLog_<run>.csv`, `SimRIG_MergeGapLog_<run>.txt`,
`SimRIG_StrategySnapshot_<run>.csv`; `DebugLog:1234` = riga 1234 di quel file.

---

## 0. Sintesi

| Area | Esito | Punto |
|---|---|---|
| `PitInOutAccDecTime` = 11.6 | ✅ corretto: Player 11.63 s misurati, avversari mediana 11.39 s su 32 soste | — |
| Cronometri della sosta Player a replay 2x | ✅ identici a 1x (`Pit Complete` 46.4 / 14.5 s): usano il session time | — |
| Bandiera e totale leader dal punto 4 | ✅ errore ≤ 0.30 giri da −20 min allo scadere | — |
| Valore di `Leader.RaceLapsCompleted` quando arriva | ✅ corretto (27 allo scadere); sparito l'offset di −5 giri pre-`863c65c` | — |
| Latch MergeGap durante la sosta del Target | ✅ −0.92 s (errore +1.8 s) | — |
| `Leader.RaceLapsCompleted` a 0 | ❌ nel 24.4% delle righe dopo il primo valore valido (regressione di Y-25) | Y-58 |
| `Leader.TrackPct` | ⚠️ 0.0 per ~530 s dal via, poi congelato (19–22 buchi, max ~152 s); il dead reckoning non scatta mai | Y-58 |
| Latch MergeGap durante la sosta del Player | ❌ −9.8 s contro −2.7 s reali (gap calcolato su Target `NotInWorld`) | Y-59 |
| Stazionario avversari dedotto | ⚠️ sottostimato dei secondi in cui l'auto è visibile in corsia (fino a 5.9 s) | Y-60 |
| MergeGap prima delle soste | ⚠️ +3.1 s pessimista, **invariato** con AccDec 11.6 | Y-61 |
| Riferimento `ExtZone` nella perdita ai box | ⚠️ 21.93 s (minimo di classe) contro ~23.8 s tipici: perdita +1.9 s per tutti | Y-61 |

---

## 1. Cosa è cambiato con `PitInOutAccDecTime` = 11.6

- **Perdite previste prima delle soste** (giro 15): Player 41.95 → 35.63 s, Target 40.73 → 34.40 s,
  entrambe −6.3 s.

  ```text
  SimRIG_MergeGapLog_20260913_140133.txt:2053: Player (+41.95s): Staz: 14.01s (incl. 2s) | Transit: 31.95s | AccDec: 17.90s | ExtZone: 21.92s
  SimRIG_MergeGapLog_20260913_140133.txt:2054: Target (+40.71s) : Staz: 12.77s | Transit: 31.95s | AccDec: 17.90s | ExtZone: 21.92s
  SimRIG_MergeGapLog_20260913_163743.txt:1078: Player (+35.63s): Staz: 14.01s (incl. 2s) | Transit: 31.95s | AccDec: 11.60s | ExtZone: 21.93s
  SimRIG_MergeGapLog_20260913_163743.txt:1079: Target (+34.40s) : Staz: 12.79s | Transit: 31.95s | AccDec: 11.60s | ExtZone: 21.93s
  ```

- **MergeGap prima delle soste: invariato**, +0.43 s in entrambi i run (`140133` MergeGapLog:2056,
  `163743` MergeGapLog:1081). L'errore sull'AccDec era di **modo comune**: finché entrambe le vetture
  devono ancora fermarsi si annulla in `LiveSignedGap + PerditaPlayer − PerditaTarget`.
- **Latch durante la sosta del Target: invariato**, −0.93 s (`140133` :2082) contro −0.92 s
  (`163743` :1094).
- **Cambia dopo la sosta del Target**, quando deve fermarsi solo il Player. Primo blocco dopo il
  rientro del Target: +5.63 s (`140133` MergeGapLog:2134, errore +8.3 s) → −1.45 s (`163743`
  MergeGapLog:1120, errore +1.25 s).
- **Perdita prevista del Player**: 35.63–35.73 s contro 34.7 s reali (dal gap) → errore +0.9 s (prima
  +7.2 s).
- **Non cambiano** (non dipendono dall'AccDec): il latch durante la sosta del Player (−9.80 / −9.77 s,
  vedi Y-59), gli stazionari dedotti degli avversari (Y-60), tutto il leader (Y-58).
- Durante il run l'AccDec in uso sale da 11.60 a 11.70 dopo l'osservazione della sosta del Target
  (`163743` DebugLog:6719, MergeGapLog:1117). A fine run il DB contiene ancora 11.6.

---

## 2. Cosa funziona (con le prove)

- **AccDec**: Player `InOutPitAccDecTime=11.63s` (`163743` DebugLog:7279) e 11.60 s (`140133`
  DebugLog:8942); avversari: mediana 11.39 s su 32 soste. Il vecchio 17.90 era sbagliato.
- **Cronometri robusti alla velocità del replay**: `Pit Complete` con `TotalTime: 46.4s | StatTime:
  14.5s | FuelAdded: 29.8L` identico a 1x (`140133` DebugLog:8894) e a 2x (`163743` DebugLog:7239);
  fill rate 2.49–2.50 L/s `Confirmed`; geofence ingresso/uscita 0.959 / 0.102 `Confirmed 9/9`
  (`163743` DebugLog:7017, 7237).
- **Leader, valore dei giri**: allo scadere `giriCompletati=27 | posAssoluta=27.742 |
  giriCheCompletera=28` (`163743` DebugLog:10927). Coerente col tempo: i cambi di giro coincidono con il
  passaggio della posizione da 0.99 a 0.00, e i giri misurati del leader valgono 94.1–98.0 s (mediana
  96.2 s).
- **Punto 4**: `Projection Validation` con errori +0.21 / +0.20 / +0.00 / −0.30 / −0.04 giri a
  −20 / −15 / −10 / −5 / −2 min (`163743` DebugLog:10928). Totale leader finale 28: giusto.
- **Latch durante la sosta del Target**: tiene −0.92 s per tutta la sosta e si rilascia al rientro.
- **Gap reale dopo le due soste**: −2.7 s, identico nei due run; da lì in poi MergeGap = gap live.

---

## 3. Cosa non funziona

### Y-58 — Leader: buchi di dati, regressione di Y-25, dead reckoning mai attivato

**Dati.** Il replay non manda i dati delle vetture lontane dal Player (lo stesso `NotInWorld` che si
vede sul Target): posizione nativa ≤ 0, `TrackPositionPercent` a 0 e `CurrentLap` ≤ 1 da SimHub.
Correlazione misurata su `140133`: distanza in pista leader–Player mediana 0.15 giri nelle righe con
dati, 0.43 giri ai bordi dei buchi.

| Misura | `142223` (pre-fix) | `140133` | `163743` |
|---|---|---|---|
| righe con `Leader PosPct=0.0000` | 1064/2673 (39.8%), 29 sequenze | 524/2675 (19.6%), 1 sola dal via (DebugLog:200–2725, TL 2691→2163) | 307/1384 (22.2%), 1 sola dal via (DebugLog:198–2279) |
| posizione congelata (valore identico su righe consecutive) | 0 | 502 righe, 22 sequenze, max 151.6 s (DebugLog:11538–12218) | 239 righe, 19 sequenze, max 151.8 s (DebugLog:9375–9909) |
| `LapsComp=0` dopo il primo valore valido | nessun lampeggio, ma offset −5 giri | 520/2132 righe (24.4%) | 260/1067 righe (24.4%) |
| giri completati allo scadere | 22 ❌ (DebugLog:13408) | 27 ✅ (DebugLog:13492) | 27 ✅ (DebugLog:10927) |

**Meccanismo** (`User.PluginSdkDemoEdit/RaceAnalyzer.cs`):

1. `:599-621` — quando la posizione non arriva si usa un valore di ripiego senza dire che è stantio:
   `OpponentTracker.cs:614-617` (`LastPosPct` dell'auto, fermo da quando è sparita) e poi
   `_lastGoodLeaderTrackPct`, che **non è legato al pilota**. Al cambio di leader la posizione mostrata
   non è quella del nuovo leader e, quando arriva il dato fresco, salta indietro (`140133`
   DebugLog:7652 → :7913, 0.6774 → 0.4482).
2. `:655` — giri = `CurrentLap − 1`: con il record vuoto vale 0.
3. `:669-670` — `HoldLeaderLapsCompleted` riceve la posizione **già tenuta**. Tiene i giri solo con
   posizione 0 (`IsLeaderSampleUsable`, `:1592-1599`), cosa che dopo il primo campione buono non
   succede più: lo 0 arriva in dashboard. Prima di `863c65c`, negli stessi buchi, i giri restavano
   tenuti (518 righe con posizione 0 e giri > 0 nel run `142223`).
4. `:848-859` — `ResolveLeaderAbsolutePos` fa dead reckoning solo con posizione esattamente 0.0
   (`IsLeaderPositionMissing`, `:1622-1625`): non succede più, quindi usa `0 + posizione tenuta` e
   riancora `_lastGoodLeaderAbsolutePos` su quel valore. Il ripiego "vecchioP1" crolla:
   `vecchioP1=3.536 (err -24.206)` a −5 min (`140133` DebugLog:13493, `163743` DebugLog:10928).
5. `:1652-1671` — `DeadReckonLeaderPos` è vincolato a `[giri noti, giri noti + 1)`: pensato per Y-35
   (Road Atlanta), dove i giri continuavano ad arrivare. Qui mancano insieme alla posizione, e il
   vincolo schiaccerebbe tutto in `[0, 1)`.
6. `OpponentTracker.cs:601` accetta `nativeDist <= 1.0f`: un 1.0000 esatto con giro già incrementato
   sposta la posizione assoluta di +1 giro per un tick (`140133` DebugLog:9358).
7. I test restano verdi: `User.PluginSdkDemo.Tests/UnitTests/LeaderSampleUnitTests.cs:106` prova
   `HoldLeaderLapsCompleted` isolata, non il chiamante — la lezione di Y-31 già scritta a
   `RaceAnalyzer.cs:1677-1679`.

Le proiezioni **pubblicate** sono protette dal punto 4 (`RaceAnalyzer.cs:1083-1090`, `:1105`,
`:1134`): il danno è su `SimRIG.Leader.RaceLapsCompleted`, `SimRIG.Leader.TrackPct` e sul ripiego
usato quando il punto 4 non ha risultato.

**Backtest del dead reckoning** (errore nel momento in cui il dato ritorna, buchi ≥ 3 s con lo stesso
leader ai due bordi):

| | `140133` (14 buchi) | `163743` (16 buchi) |
|---|---|---|
| posizione congelata (comportamento attuale): mediana / max | 0.119 / 1.721 giri | 0.105 / 1.759 giri |
| DR con passo 90.195 s: mediana | 0.037 giri (3.6 s) | 0.023 giri |
| buco più lungo: congelata → DR | 153.7 s: −1.721 → −0.017 giri | 157.8 s: −1.759 → −0.010 giri |

Gli unici errori DR sopra 0.12 giri: buchi con una sosta del leader dentro (+0.39 / +0.42 giri, cioè la
perdita ai box) e buchi il cui bordo cade su un contatore giri in ritardo di un tick (errore del dato,
non del DR). Buco iniziale di 554 s: DR col passo seed 90.195 s → +0.42 giri; col passo reale 96.2 s →
+0.05.

**Fix proposto** (da implementare con lock):

1. Separare la **freschezza** dal valore (`rawPos > 0.0` in questo tick): `HoldLeaderLapsCompleted` e
   `ResolveLeaderAbsolutePos` devono ricevere il dato grezzo, non quello tenuto.
2. Ancora del DR = ultima posizione assoluta fresca (giri + frazione), **per pilota**; reset al cambio
   di leader.
3. Con giri mancanti, avanzare l'ancora di `elapsed / passo` senza il vincolo sui giri correnti
   (limite sul tempo trascorso invece); giri mostrati = parte intera della posizione stimata, mai 0.
4. `OpponentTracker.cs:601`: trattare 1.0 come 0.0.
5. **Decisione di prodotto (Andreas):** `SimRIG.Leader.TrackPct` mostra la posizione stimata o
   l'ultima reale con un flag "stimata"?
6. **Da verificare prima, costo basso:** `CarIdxLapCompleted` nativo esiste già
   (`IracingTelemetryBridge.cs:30,44`). Se resta valido mentre il leader è `NotInWorld`, risolve i giri
   senza stime: basta loggarlo per il leader durante un buco.

**Test (ADR-004), con i numeri veri:** buco `140133` DebugLog:11532 → 12223. Ultimo campione fresco
23.391 a TL 410.4, passo 90.195: a TL 256.7 la posizione deve valere ≈ 25.095 (reale 25.112) e i giri
mostrati 23 → 24 → 25, mai 0. Il test deve fallire togliendo il fix.

### Y-59 — MergeGap: il latch della sosta Player congela un gap calcolato su Target `NotInWorld`

**Numeri.** Gap reale dopo entrambe le soste −2.7 s, stabile fino a TL ~842 (`163743`
Snapshot:2072–2174, `140133` Snapshot:3980–4173). Valore congelato durante la sosta del Player:
−9.77 s (`163743` MergeGapLog:1159, 1172) e −9.80 s (`140133` MergeGapLog:2212–2264). Errore −7.1 s,
**ottimista**: il cruscotto mostrava 9.8 s di margine contro 2.7 reali.

**Meccanismo.** Nei ~20 s prima dell'ingresso del Player il Target è `NotInWorld` con posizione ferma
(`163743` DebugLog:6869 a 37.53%, :6965 a 45.20%). Il gap per microsettori
(`TargetStrategyManager.cs:713-731`, ramo "Player davanti") usa quella posizione: il tempo di
passaggio del Player resta fisso e il gap cresce di ~1 s ogni secondo. La tolleranza a `:726`
(0.6 giri) non se ne accorge, perché anche `expectedGap` cresce. `_lastOnTrackProjectedMergeGap` si
aggiorna comunque (`:242-250`: basta che nessuno sia ai box), e `isTargetInPit` (`:1339`) non
considera `NotInWorld`.

Firma nello Snapshot (`163743`): gap −38.049 a TL 1003.5 (:1992) → −46.731 a TL 993.8 (:2003), poi
−37.701 a TL 992.8 (:2004) — l'unico tick con Target visibile (DebugLog:6962, `44.92% OnTrack`) — e la
rampa riparte fino all'ingresso del Player (:2013 −45.417, :2014 `PlayerPit=True`). Identica nel
`140133` (Snapshot:3816 → 3839, :3840 −37.496, ingresso a :3859). Con blocchi da 20 s di sessione, il
MergeGapLog `163743` mostra anche un blocco non congelato a −11.27 s (:1146, Target `NotInWorld` a
:1141).

**Fix proposto.** Con Target `NotInWorld` (o posizione invariata per più tick): non aggiornare
`SignedGapSeconds` (`:744`) — tenere l'ultimo gap fresco, eventualmente avanzato col delta di passo —
e non aggiornare `_lastOnTrackProjectedMergeGap`, così il latch parte dall'ultimo valore fresco.

**Test.** Gap fresco −37.50 (`140133` Snapshot:3840), gap stantio −45.23 (:3858), perdita Player
35.71: il valore congelato deve essere −1.79, non −9.52. Il test deve fallire togliendo il fix.

### Y-60 — Stazionario degli avversari: il transito si sottrae alla finestra `NotInWorld`, non al tempo in corsia

**Vincolo (confermato da Andreas).** Lo stazionario degli avversari non si può osservare: iRacing
mette le vetture `NotInWorld` appena entrano in corsia box. L'unica via è stimarlo dal nostro
`PitTransitTime` (31.95 s). Questo punto **non** mette in discussione il vincolo: riguarda a cosa si
sottrae il transito.

**Oggi** (`OpponentTracker.cs:1714-1728`): `DeducedStationary = NotInWorldDuration − PitTransitTime`.
Ma la finestra `NotInWorld` non copre sempre tutta la corsia: la vettura torna visibile prima della
linea di uscita (o sparisce dopo l'ingresso), e quei secondi visibili sono transito, sottratti due
volte. Con `vis = TotalTime − NotInWorld` (`TotalTime` = tempo in corsia misurato dai flag nativi
`CarIdxOnPitRoad`, `:1697`):

```text
DeducedStationary = Stazionario_reale + (Transito_reale − 31.95) − vis
```

**Proposta:** `Stazionario = TotalTime − PitTransitTime`, tenendo `NotInWorld > 0` solo come
condizione di attivazione. Per costruzione `stazionario stimato + transito = tempo in corsia misurato`:
la perdita ricostruita per la sosta successiva torna esatta anche se il transito dell'avversario
differisce dal nostro. Resta solo l'errore sul transito (δ); sparisce `vis`.

**Numeri** (`163743`; identici in `140133`, stesse soste):

- 33 soste avversarie, 27 con stazionario dedotto da `NotInWorld`. `vis` mediana 0.43 s, ma 5.93 s
  (Grigory Ivanov3), 5.23 s (Dániel Oláh), 3.78 s (Mark Calver2), 3.63 s (Daniel Wieland2), 2.23 s,
  1.97 s.
- 12 soste senza gomme con `NotInWorld`, confronto con `ExpectedRefuel` del modello carburante
  avversari: mediana (atteso − dedotto oggi) **+2.40 s**; mediana (atteso − proposta) **−0.25 s**.
- Target Daniel Wieland2: dedotto oggi 12.52 s (DebugLog:6677); proposta 48.13 − 31.95 = 16.18 s
  (DebugLog:6719); `ExpectedRefuel` 16.9 s (DebugLog:6678, da 37.2 L proiettati a :6437). Terza stima,
  dal gap reale: perdita misurata ~36.5 s = Staz + 31.95 + 11.70 − 23.63 (suo transito di corsa,
  DebugLog:6680) → Staz ≈ 16.5 s.

**Limite da conoscere.** Se il transito dell'avversario è più lungo del nostro (posizione del box,
traffico in corsia), la proposta sovrastima lo stazionario di quella differenza. Su questo replay non
è misurabile: le 6 soste GT3 visibili (TL 946–951) hanno 4 `In Pit Stall Started` allo stesso istante
(`163743` DebugLog:6979, 6982, 6991, 6993, TL 988.17–988.22, cioè quando il Player entrava in corsia),
quindi l'inizio sosta è probabilmente tagliato; prese alla lettera darebbero transiti di 32.4–35.3 s.

**Dove pesa lo stazionario dedotto:** classificazione gomme sì/no (`OpponentTracker.cs:1754-1850`),
carburante imbarcato stimato (`(stazionario − 2) × fill rate`), perdita ai box del leader
(`RaceAnalyzer.cs:1000`). La classificazione è già fragile: la stessa sosta di Ethan Carlton Wong
(TL ~1000) risulta `No Tires` a 1x (`140133` DebugLog:8482) e `Tires Changed` a 2x (`163743`
DebugLog:6892), con stazionario identico (18.00 contro 18.02 s).

**Collaterale (solo log).** `Pit Loss Dissection` (`OpponentTracker.cs:1892-1901`) legge
`ExtendedPitZone.LastTransitTime` all'uscita dalla corsia, quando il transito della sosta non è ancora
chiuso: legge l'ultimo transito **di corsa** (mediana 23.47 s su 32 soste) invece di quello della sosta
(mediana 60.59 s, scritto poi in `Opponent Pit AccDec Details`). `RawExtended` e `JackingDeadTime` di
quella riga sono privi di senso (es. `163743` DebugLog:6680 `ObservedTransit: 23.63s | Stationary:
12.52s | RawExtended: 11.12s`). Nessun consumatore oltre il log.

### Y-61 — Modello della perdita ai box: riferimento `ExtZone` "best", stazionario del Target col consumo del Player

Il MergeGap prima delle soste resta **+3.1 s pessimista** anche con AccDec 11.6: previsto +0.43 s
(`163743` MergeGapLog:1081) contro −2.7 s reali. Scomposizione per componente sul run `163743`:

| Termine | Previsto | Misurato / stimato | Effetto sul MergeGap |
|---|---|---|---|
| Stazionario Player (incl. 2 s) | 14.01 | 14.47 (DebugLog:7279) | −0.46 |
| Transito Player | 31.95 | 32.48 (46.95 − 14.47) | −0.53 |
| AccDec Player | 11.60 | 11.63 | −0.03 |
| `ExtZone` Player (sottratto) | 21.93 | 23.8 (vedi punto 1) | +1.9 |
| **Stazionario Target** | **12.79** | **16.2–16.5** (vedi Y-60) | **+3.4…+3.7** |
| AccDec Target | 11.60 | 11.70 (DebugLog:6719) | +0.10 |
| `ExtZone` Target (sottratto) | 21.93 | 23.63 (suo transito di corsa, DebugLog:6680) | −1.70 |
| **Totale** | | | **+2.7…+3.0** (osservato +3.1) |

Verifica di coerenza: con i componenti misurati la perdita del Player vale 14.47 + 32.48 + 11.63 −
23.8 = **34.8 s**, quella ricavata dal gap reale **34.7 s** (da −37.4 s prima a −2.7 s dopo la sua
sosta).

1. **`ExtZone` = minimo di classe, non tempo tipico.** `ClassBestExtendedPitZoneTime` è il **minimo**
   dei `BestRawTime` della classe del Player, Player compreso (`OpponentTracker.cs:890-907`), e
   `CalculateExtendedRacingTime` lo usa così com'è (`CarPitData.cs:165-170`, chiamato a
   `TargetStrategyManager.cs:932-934`). Transito di corsa misurato del Player nella zona estesa
   (0.9086 → 0.1516): mediana 23.76 s (`140133`) / 23.84 s (`163743`) sui giri senza box; esclusi il
   giro 1 (partenza) e il giro 21 (fuori pista), l'intervallo è 23.53–24.30 s. Le 14 soste senza gomme
   vicine a quelle di Target e Player danno transiti di corsa di 23.20–23.93 s (`ObservedTransit` della
   Dissection). Con 21.93 s la perdita è sovrastimata di ~1.9 s per tutte le vetture: il minimo pesca
   un singolo passaggio eccezionale (lo schema di ADR-005). Si annulla nel MergeGap prima delle soste
   (modo comune), ma pesa dopo la prima sosta e in ogni confronto assoluto (undercut/overcut,
   `TargetPitLoss` contro gap).
2. **Stazionario del Target calcolato col consumo del Player.** `TargetStrategyManager.cs:952` usa
   `fuel.AverageFuelPerLap` del Player (~3.0 L/giro), `:975` i giri rimanenti del Player e
   l'`EstimatedFuel` prima della deduzione dell'in-lap, `:987-992` il fill rate del Player → 12.79 s.
   Il modello avversari ha già il consumo del Target (`163743` DebugLog:6437: `Burn: 3.57 | Residuo:
   3.9L | Needed: 41.1L | FuelToAdd: 37.2L`) → 37.2 / 2.5 + 2 = 16.9 s, coerente con le altre due stime
   (16.2–16.5 s). È l'errore dominante che resta nel MergeGap prima delle soste.
3. **AccDec per avversario senza banda di plausibilità.** `OpponentTracker.cs:2571-2579` salva
   qualunque `ExtendedPitZoneTime − StrictPitLaneTime` in `LastPitInOutAccDecTimeSec`, usato per quel
   Target se > 0 (`TargetStrategyManager.cs:892`). Sulle 32 soste: mediana 11.39 s, ma 4.57–5.45 s
   (soste lunghe o riparazioni; Matt Loveridge 4.93, Jaydon Wilcox 5.23), 17.90 s (Jonathan
   Skadhauge) e −454.13 s (Vladislav Zhukov). Se una di quelle vetture diventa Target, la sua perdita
   ha fino a ~6.5 s di errore.
4. **Giro 1.** Stazionario del Player a 0.00 s (`FuelToAdd` congelato a 0 nel giro 1, affinamento di
   Y-52) → MergeGap −11.11 s per tutto il giro 1 (`163743` MergeGapLog:12, 15) contro +4.44 s dal giro
   2 (:132).

**Proposta.** (1) per `ExtZone`, un tempo tipico — mediana robusta dei transiti di corsa della classe
(vedi `robust-statistics.md` nella skill `motorsport-telemetry-engineering`) — non il minimo;
(2) per lo stazionario del Target, consumo, residuo e `FuelToAdd` del modello avversari, già
calcolati; (3) banda di plausibilità sull'AccDec per avversario (es. ±3 s dal valore consolidato di
pista), altrimenti usare quello di pista; (4) nel giro 1 escludere il MergeGap o usare lo stazionario
seed.

---

## 4. Altre osservazioni (non registrate come punti)

- **Totale leader nei primi giri** 30–31 contro 28 finali: il passo seed resta 90.195 s fino al giro 7
  (`Race Projections Update`, `163743` DebugLog:732 → 2807) contro 96.2 s reali. Il passo misurato
  arriva tardi perché i dati del leader mancano (Y-58). Si allinea a 28 da TL 1248.
- **Calo del totale leader 28 → 25 a TL 375.9** (`163743` DebugLog:9475, rientro a :9517): per un
  tick il punto 4 dà `comanda=PLAYER … inLotta=1` (`140133` DebugLog:11670) e
  `LeaderRaceLapsRemaining` resta a ~1.5 per ~15 s. Identico nel run pre-fix (`142223` DebugLog:11606):
  non è una regressione.
- **`PlayerPit=True` per ~1 s durante un fuori pista** del Player a TL ~400 (`163743` Snapshot:2681;
  `140133` Snapshot:5206–5209, fuori pista a `140133` DebugLog:11559–11613). Innocuo qui, perché
  entrambe le soste erano già fatte.

---

## 5. Stringhe dal log (run `163743`)

Colonne dello Snapshot riportate: `SessionTime,Lap,MacroSector,Target,SignedGap,PlayerPit,TargetPit,RaceLapsRem`.

```text
# Prima delle soste
SimRIG_MergeGapLog_20260913_163743.txt:1076: Target: P13 | PosPct: 91.75% | Surface: OnTrack | InPitRoad: False | InPitStall : False | SurfaceCode : 3 | Pits: 0 | FuelLaps: 2.3 | RemLaps: 11.1 | TargetNeedsPit: True
SimRIG_MergeGapLog_20260913_163743.txt:1081: LiveSignedGap: -0.80s -> ProjectedMergeGap: 0.43s (-0.80 + 35.63 - 34.40)
SimRIG_StrategySnapshot_20260913_163743.csv:1889: 1093.800,15,18,Daniel Wieland2,-0.810,False,False,11.1
SimRIG_StrategySnapshot_20260913_163743.csv:1894: 1089.500,15,19,Daniel Wieland2,-1.974,False,False,11.0

# Sosta Target
SimRIG_DebugLog_20260913_163743.csv:6435: 16:53:55.070;1089.2;15;EVENT;OPPONENTS;Opponent Geofence Enter (Validated);Daniel Wieland2 | Trigger: iRacing Native (CarIdxOnPitRoad) | EntryTimeSec: 1089.20 | Pos: 0.9586 | Speed: 73.6 km/h
SimRIG_DebugLog_20260913_163743.csv:6437: 16:53:55.070;1089.2;15;EVENT;OPPONENTS;Opponent Smart Refuel Projection;Daniel Wieland2 | LapsRem: 11.0 | Burn: 3.57 | Residuo: 3.9L | Needed: 41.1L | FuelToAdd: 37.2L | PostPit: 41.1L | NeedsPit: False
SimRIG_MergeGapLog_20260913_163743.txt:1089: Target: P9 | PosPct: 95.90% | Surface: NotInWorld | InPitRoad: True | InPitStall : False | SurfaceCode : -1 | Pits: 0 | FuelLaps: 13.8 | RemLaps: 10.9 | TargetNeedsPit: False
SimRIG_MergeGapLog_20260913_163743.txt:1094: LiveSignedGap: -18.10s -> ProjectedMergeGap: -0.92s [FROZEN IN PIT] (-18.10 + 35.63 - 0.00)
SimRIG_DebugLog_20260913_163743.csv:6677: 16:54:19.134;1041.1;16;EVENT;OPPONENTS;Opponent Stationary Time Reverse-Engineered;Daniel Wieland2 | NotInWorld: 44.47s | RefTransit: 31.95s | DeducedStationary: 12.52s
SimRIG_DebugLog_20260913_163743.csv:6678: 16:54:19.134;1041.1;16;EVENT;SYSTEM;Opponent Pit Simultaneous (No Tires);Daniel Wieland2 | StationaryTime=12.5s | ExpectedRefuel=16.9s | TiresChanged=False
SimRIG_DebugLog_20260913_163743.csv:6679: 16:54:19.134;1041.1;16;EVENT;SYSTEM;Opponent Pit Stop Deduction;Name: Daniel Wieland2 | TotalTime: 48.1s | StatTime: 12.5s | EstFuelAdded: 26.3L | tFuel: 14.9s | tTyres: 26.0s | Layout: Simultaneous | TiresChanged: False
SimRIG_DebugLog_20260913_163743.csv:6680: 16:54:19.134;1041.1;16;EVENT;OPPONENTS;Pit Loss Dissection;Daniel Wieland2 | ObservedTransit: 23.63s | Stationary: 12.52s | RawExtended: 11.12s | RefuelEst: 10.52s | JackingDeadTime: 2.00s
SimRIG_DebugLog_20260913_163743.csv:6719: 16:54:22.436;1034.5;16;EVENT;OPPONENTS;Opponent Pit AccDec Details;Daniel Wieland2 | ExtendedPitZoneTime=59.83s, StrictPitLaneTime=48.13s, InOutPitAccDecTime=11.70s, StationaryTime=12.52s, GlobalDbAccDec=11.60s

# Fra le due soste
SimRIG_MergeGapLog_20260913_163743.txt:1117: Player (+35.73s): Staz: 14.01s (incl. 2s) | Transit: 31.95s | AccDec: 11.70s | ExtZone: 21.93s
SimRIG_MergeGapLog_20260913_163743.txt:1120: LiveSignedGap: -37.18s -> ProjectedMergeGap: -1.45s (-37.18 + 35.73 - 0.00)
SimRIG_StrategySnapshot_20260913_163743.csv:1991: 1004.333,16,14,Daniel Wieland2,-37.431,False,False,10.3

# Target NotInWorld: gap calcolato su posizione congelata
SimRIG_DebugLog_20260913_163743.csv:6869: 16:54:38.170;1003.0;16;EVENT;STRATEGY;Target Track State Changed;Target: P9 | PosPct: 37.53% | Surface: NotInWorld | InPitRoad: False | InPitStall : False | SurfaceCode : -1
SimRIG_StrategySnapshot_20260913_163743.csv:1992: 1003.467,16,14,Daniel Wieland2,-38.049,False,False,10.3
SimRIG_StrategySnapshot_20260913_163743.csv:2003: 993.800,16,17,Daniel Wieland2,-46.731,False,False,10.1
SimRIG_DebugLog_20260913_163743.csv:6962: 16:54:43.034;993.3;16;EVENT;STRATEGY;Target Track State Changed;Target: P9 | PosPct: 44.92% | Surface: OnTrack | InPitRoad: False | InPitStall : False | SurfaceCode : 3
SimRIG_StrategySnapshot_20260913_163743.csv:2004: 992.783,16,17,Daniel Wieland2,-37.701,False,False,10.1
SimRIG_DebugLog_20260913_163743.csv:6965: 16:54:43.150;993.0;16;EVENT;STRATEGY;Target Track State Changed;Target: P9 | PosPct: 45.20% | Surface: NotInWorld | InPitRoad: False | InPitStall : False | SurfaceCode : -1
SimRIG_MergeGapLog_20260913_163743.txt:1141: Target: P9 | PosPct: 37.53% | Surface: NotInWorld | InPitRoad: False | InPitStall : False | SurfaceCode : -1 | Pits: 1 | FuelLaps: 13.5 | RemLaps: 10.1 | TargetNeedsPit: False
SimRIG_MergeGapLog_20260913_163743.txt:1146: LiveSignedGap: -47.00s -> ProjectedMergeGap: -11.27s (-47.00 + 35.73 - 0.00)
SimRIG_StrategySnapshot_20260913_163743.csv:2013: 985.067,16,19,Daniel Wieland2,-45.417,False,False,10.0

# Sosta Player
SimRIG_StrategySnapshot_20260913_163743.csv:2014: 984.233,16,19,Daniel Wieland2,-46.251,True,False,10.0
SimRIG_MergeGapLog_20260913_163743.txt:1154: Target: P9 | PosPct: 45.20% | Surface: NotInWorld | InPitRoad: False | InPitStall : False | SurfaceCode : -1 | Pits: 1 | FuelLaps: 13.5 | RemLaps: 10.0 | TargetNeedsPit: False
SimRIG_MergeGapLog_20260913_163743.txt:1159: LiveSignedGap: -56.97s -> ProjectedMergeGap: -9.77s [FROZEN IN PIT] (-56.97 + 35.73 - 0.00)
SimRIG_MergeGapLog_20260913_163743.txt:1172: LiveSignedGap: -37.64s -> ProjectedMergeGap: -9.77s [FROZEN IN PIT] (-37.64 + 0.00 - 0.00)

# Gap reale dopo le due soste
SimRIG_StrategySnapshot_20260913_163743.csv:2072: 932.583,17,2,Daniel Wieland2,-2.842,False,False,9.9
SimRIG_MergeGapLog_20260913_163743.txt:1198: LiveSignedGap: -2.61s -> ProjectedMergeGap: -2.61s (-2.61 + 0.00 - 0.00)
SimRIG_MergeGapLog_20260913_163743.txt:1224: LiveSignedGap: -2.70s -> ProjectedMergeGap: -2.70s (-2.70 + 0.00 - 0.00)
SimRIG_StrategySnapshot_20260913_163743.csv:2174: 841.950,18,0,Daniel Wieland2,-2.768,False,False,9.0
```

---

## 6. Come ricontrollare

```bash
grep -n "FROZEN IN PIT" "Logs/Daytona/SimRIG_MergeGapLog_20260913_163743.txt"
grep -n "Target Track State Changed" "Logs/Daytona/SimRIG_DebugLog_20260913_163743.csv"
grep -n "Leader Position At Expiry\|Projection Validation" "Logs/Daytona/SimRIG_DebugLog_20260913_163743.csv"
grep -n "Reverse-Engineered\|Opponent Pit Stop Deduction\|Opponent Pit AccDec Details" "Logs/Daytona/SimRIG_DebugLog_20260913_163743.csv"
grep -n "RaceProjectionsDiagnostics" "Logs/Daytona/SimRIG_DebugLog_20260913_163743.csv"
```

---

## 7. Attenzione per chi implementa

- **Validare anche su Daytona.** Il latch del MergeGap (`722a9d6`) e il fix leader (`863c65c`) erano
  stati validati solo su Road Atlanta, dove il culling `NotInWorld` non si vedeva. Su Daytona si
  rompono.
- **Non "tenere" un valore al posto di stimarlo:** una posizione tenuta è una posizione ferma (Y-35).
  Servono una stima e un flag di freschezza.
- **Test sul chiamante con i numeri di questi log** (ADR-004): il test sulla sola funzione è già verde
  con il difetto presente.
- **Y-60 cambia l'input della classificazione gomme sì/no** (`OpponentTracker.cs:1754-1850`): per le
  soste senza gomme lo stazionario stimato sale da 10.5–17.1 s a 15.7–17.7 s. Ricontrollare le soglie
  (`expectedTotalRefuelTime + 3.0`, `tTyres − 2.0`) prima di dichiarare chiuso.
- **`PitInOutAccDecTime` = 11.6 nel DB è verificato:** non riportarlo a 17.90.

---

## 8. Incoerenze nei file di stato (segnalate, non corrette in questo turno)

- `PROJECT_STATE.md:69` dice "347 test PASS"; l'handoff del 12/09 dichiara 363 (da ricontare col
  runner).
- Punti chiusi: "39" (`PROJECT_STATE.md:122`) e "40" (`PROJECT_STATE.md:62`, `AGENTS.md:220`);
  `grep -c '^| ~~' .ai/archive/CLOSED_POINTS.md` ne conta 41.
- `PROJECT_STATE.md:90` elenca Y-13 fra gli aperti, ma è nell'indice dei chiusi (`:135`).
- La tabella "Congelati in attesa di decisione" è di fatto la tabella dei punti aperti: contiene Y-52
  (fase attiva) e ora Y-58–Y-61, che non aspettano decisioni di prodotto (tranne la domanda su
  `Leader.TrackPct` in Y-58).
- `STRATEGY_ENGINE_GUIDE.md:73` parla di "tre file" di log: sono quattro (manca
  `SimRIG_DebugLog_*.csv`).
- La roadmap (aggiornata il 06/09) indica come lavoro attivo Y-52 Passo 3; i turni dell'11–12/09
  (tracking nativo, pit loss, MergeGap, leader) non vi compaiono.
