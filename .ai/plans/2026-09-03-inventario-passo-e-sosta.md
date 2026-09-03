# Inventario: il passo e la perdita ai box — chi calcola cosa, e dove finisce

> Scritto il 2026-09-03 su richiesta dell'utente, dopo che lo stesso schema — *un calcolo corretto
> che esiste già, collegato al posto sbagliato* — si era ripetuto cinque volte in una settimana
> (Y-31, Y-46, Y-48, il ramo leader di Y-42, la perdita ai box).
>
> **Scopo:** prima di scrivere una funzione nuova in questo repository, sapere se esiste già.
>
> **Copertura:** solo le due aree in gioco adesso — il **passo** e la **perdita ai box**. Non è un
> inventario dell'intero plugin. Le geofence (Y-33), il carburante e la strategia undercut/overcut
> restano da mappare.

---

## Parte 1 — Il passo

### 1.1 Il Player: calcoliamo due passi, la proiezione usa quello sbagliato

Esistono **due** grandezze, entrambe calcolate a ogni giro:

| grandezza | com'è fatta | valore a Road Atlanta |
|---|---|---|
| `NormalizedRaceStartPace` | il **primo giro valido**, normalizzato | **76.524 s** |
| `EstimatedCurrentPace` | baseline + degrado gomme + peso carburante + temperatura | **77.24 s** |

`EstimatedCurrentPace` (`RaceAnalyzer.cs:1468`) è il passo *vero di adesso*: parte dalla baseline e ci
aggiunge tutto quello che nel frattempo è cambiato. È esposto come proprietà SimHub
(`SimRIG.Driver.EstimatedCurrentPace`) e finisce a log a ogni giro.

**La proiezione usa l'altro** (`RaceAnalyzer.cs:755`):

```
activePlayerPace = NormalizedRaceStartPace     // 76.524
```

**Scarto misurato: 0.72 s al giro, cioè 0.94%.** Su 35 giri fa **0.33 giri**; sui 21 giri che
restavano alla sosta del replay `20260901_211532` fa 15.1 s, cioè **0.20 giri**.

Direzione dell'errore: usiamo un passo più veloce del reale, quindi proiettiamo **più** giri e
imbarchiamo **più** carburante. È la direzione sicura, ma è un errore dello stesso ordine di
grandezza di quelli che stiamo inseguendo da giorni.

> **Nota:** buona parte del punto 7 della roadmap ("Base Pace + reiniezione della massa carburante")
> **esiste già** ed è `EstimatedCurrentPace`. Manca il collegamento, non il calcolo.

### 1.2 Gli avversari: la catena, in ordine

Per ogni vettura, a ogni giro completato:

1. **Il tempo arriva dal gioco** (`Opponent.LastLapTime`). Il cronometraggio interno come ripiego è
   stato **rimosso** (Y-39): un dato mancante resta mancante.
2. **Credibilità assoluta** (`IsCredibleOpponentLap`): fra 20 e 600 s, e sopra il minimo fisico
   dato dalla lunghezza della pista.
3. **Validità relativa** (`isValidOppLap`), tre condizioni:
   - scartato se è un out-lap (`lapsSincePit <= 1`);
   - scartato se la vettura è dentro la geofence dei box;
   - scartato se devia dalla **baseline** più di **+3.5%** o meno di **−2.0%**.
4. **Se valido e con gomme sotto i 40 km**: entra nella storia dei giri. Se la baseline non esiste
   ancora, **questo giro diventa la baseline**.
5. **`LapMovingAverage`** = media degli **ultimi 5** giri della storia.

Ogni tempo esiste in **due contenitori paralleli**: `RawTimes` (grezzo) e `NormalizedTimes`
(scontato di peso carburante e temperatura). La proiezione usa il **normalizzato**.

### 1.3 Il difetto strutturale della catena, e come il 278 è entrato

Il punto 4 dice: *se la baseline non esiste ancora, questo giro diventa la baseline*. Ma il controllo
di deviazione al punto 3 si applica **solo se la baseline esiste già**:

```
if (NormalizedRaceStartPace > 0 && isValidOppLap) { ...controllo -2% / +3.5%... }
```

**Quindi il primo giro valido di ogni vettura entra senza nessun controllo di plausibilità** oltre
ai 20-600 s. E da lì in poi la finestra di validità è centrata **su quel giro**.

È così che `Alessandro Barbagallo` è arrivato a un passo di **278.563 s**: il suo primo giro
accettato era un giro sporco, è diventato la baseline, e la finestra centrata su 278 ha poi
rifiutato tutti i suoi giri veri. Il difetto si auto-difende — una baseline sbagliata rende
invalidi proprio i dati che la correggerebbero.

**È esattamente ciò che il filtro ancorato al giro più veloce risolve**: un'àncora che può solo
migliorare non può insediarsi.

### 1.4 Il passo del leader: due strade, e il filtro non è più su quella principale

Esiste `LeaderPaceFilter`, una media esponenziale con `alpha = 0.10`, scritta apposta per
stabilizzare il passo del leader. Il risultato è `_smoothedLeaderPace`.

Da quando il punto 4 è attivo, le due strade si sono separate:

| dove | passo usato |
|---|---|
| candidati del punto 4 (**decide la bandiera**) | `NormalizedTimes.LapMovingAverage` **grezzo**, non filtrato |
| stima della posizione quando manca (Y-35) | `_smoothedLeaderPace` (filtrato) |
| ripiego sul P1 istantaneo | `_smoothedLeaderPace` (filtrato) |

**Il filtro costruito per stabilizzare il passo del leader non è più sul percorso che decide il
momento della bandiera.** Non è necessariamente sbagliato — il punto 4 sceglie la vettura in modo
diverso — ma è una scelta che nessuno ha preso: è successa.

---

## Parte 2 — La perdita ai box

### 2.1 Tre formule diverse per la stessa grandezza

| # | dove | usata da | forma |
|---|---|---|---|
| 1 | `RaceTimeProjection.PitLossSec` | **la proiezione del carburante** | zona − equivalente **geometrico** + fermo |
| 2 | `TargetStrategyManager.cs:691` | strategia undercut/overcut | zona − equivalente **misurato** + fermo |
| 3 | `DataPluginDemo.cs:1996` | proprietà `SimRIG.Pit.TotalPitLoss` | come la 2 |

La #1 l'ho scritta io il 2026-09-02 per Y-44, **senza sapere che esistevano la #2 e la #3**. È la
più debole delle tre: deduce l'equivalente in pista dalla geometria della zona invece di leggerlo
misurato, e **non applica `IsSequential`** né i 2 s di tempo morto dei martinetti.

### 2.2 Gli ingredienti, e quanto valgono

**Il lato "in box"** — quanto tempo si passa nella zona, misurato:

```
ExtendedPitZoneTime  =  StrictPitLaneTime  +  InOutPitAccDecTime
      50.85          =       41.13         +        9.72            (Road Atlanta, replay 211532)
PitTransitTime       =  StrictPitLaneTime  −  tempo da fermo  =  26.38
```

`PitTransitTime` **non contiene** il tempo da fermo: `PitRadar.cs:1454` lo sottrae in calibrazione.

**Il lato "in pista"** — quanto ci avresti messo a percorrere lo stesso tratto in gara. Cascata a
tre livelli, di qualità decrescente:

| livello | grandezza | com'è fatta | zona |
|---|---|---|---|
| 1 | `ClassBestExtendedPitZoneTime` | **misurata**: cronometro sulla zona estesa, miglior tempo fra le vetture | estesa ✅ |
| 2 | `PitLaneZoneRacingTime` | `pitDistance / ClassTopSpeed` | **corta** ❌ |
| 3 | `PitRadar.PitTransitTime` | tempo di transito **a velocità box** | **corta, e in box** ❌❌ |

⚠️ **Il livello 1 è coerente, i livelli 2 e 3 no.** Il lato "in box" della sottrazione è sempre la
zona **estesa**; i ripieghi 2 e 3 forniscono la zona **corta**. Sottrarre una zona corta da una
estesa lascia dentro il tratto di raccordo e **sovrastima la perdita**.

Il livello 3 è peggio ancora: `PitTransitTime` è il tempo di percorrenza **a velocità limitata**.
Sottrarlo dalla zona estesa in box lascia praticamente solo l'accelerazione/decelerazione, che non
è la perdita.

⚠️ **`ClassBestExtendedPitZoneTime` copre solo la classe del Player**
(`OpponentTracker.cs:612`, `opp.CarClass == state.CarClassId`). Per le altre classi non esiste.

### 2.3 `IsSequential`: c'è, ed è letto da due formule su tre

`CarPitData` distingue le vetture in cui gomme e benzina si fanno **insieme** (GT3, LMP2, GTP) da
quelle in cui si fanno **in sequenza** (Porsche Cup, monoposto, NASCAR).

| formula | applica `IsSequential`? |
|---|---|
| strategia undercut/overcut | ✅ |
| proprietà `SimRIG.Pit.TotalPitLoss` | ✅ |
| **proiezione del carburante** | ❌ — sempre `max(benzina, gomme)` |

Per una GT3 non cambia niente. Per una Porsche Cup il tempo da fermo è la **somma**, non il massimo:
con 30 s di gomme e 18 s di benzina, 48 s contro 30. **Diciotto secondi di errore su una sosta.**

### 2.4 I tempi di sosta degli avversari: li abbiamo

`OpponentTelemetryData` traccia per ogni vettura `StationaryTimeSec`, `LastPitStationaryTimeSec`,
`LastOpponentStrictPitLaneTime`, e la zona estesa via `SectorTracker`. Il log emette
`Opponent Pit AccDec Details` con tutti e quattro i tempi.

⚠️ **Ma sono spazzatura nei primi giri**: `ExtendedPitZoneTime=0.00s`, `StrictPitLaneTime=2691.73s`,
`InOutPitAccDecTime=-2691.73s`. Il cronometro parte all'inizio della sessione e non viene azzerato
finché la vettura non compie un transito vero.

---

## Parte 3 — Il quadro: sette scollegamenti, non uno

Lo schema che ha motivato questo inventario, elencato:

| # | il calcolo giusto | dove finisce invece |
|---|---|---|
| 1 | `EstimatedCurrentPace` (77.24 s) | la proiezione usa la baseline (76.524) — **0.94%** |
| 2 | `IsSequential` | la proiezione usa sempre `max()` |
| 3 | perdita ai box con equivalente **misurato** | la proiezione usa quella geometrica (scritta ieri) |
| 4 | `ClassBestExtendedPitZoneTime` | solo la classe del Player |
| 5 | `LeaderPaceFilter` (alpha 0.10) | non più sul percorso che decide la bandiera |
| 6 | àncora sul giro **più veloce** | la finestra è centrata sul **primo** giro valido |
| 7 | `PitLaneZoneRacingTime` | zona corta sottratta da zona estesa |

Più i quattro già chiusi in questa settimana: Y-31 (posizione arrotondata prima del filtro), Y-46
(tetto riapplicato senza condizione), Y-48 (totale leader dal P1 istantaneo), Y-42 sul ramo leader
(la formula sbagliata è **ancora lì**, `RaceAnalyzer.cs:992`).

**Undici volte lo stesso schema.** Non è distrazione: è la conseguenza di un plugin cresciuto per
strati, dove ogni strato ha risolto il problema che aveva davanti scrivendosi il proprio calcolo.

### Cosa ne deriva, per l'ordine di lavoro

1. **L'àncora sul giro più veloce (#6)** resta la correzione con più valore: è la causa a monte del
   passo a 278 s, e nessuna delle altre la tocca.
2. **#1 e #2 sono collegamenti**, non algoritmi: poche righe, effetto misurabile, rischio basso.
3. **#3, #4 e #7 vanno fatti insieme**, perché sono la stessa cascata.
4. **#5 va deciso, non corretto**: serve stabilire se il passo dei candidati del punto 4 debba
   essere filtrato. È una scelta di prodotto.

### Regola da tenere

> Prima di scrivere una funzione nuova in questo repository, cercare se esiste già.
> Undici volte su undici esisteva.
