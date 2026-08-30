# Piano — proiezioni sui tempi misurati, non sui normalizzati

> Deciso con l'utente il 2026-08-30, dopo l'analisi dei replay Road Atlanta
> `20260830_102220`, `113151`, `121813`.
>
> **Principio guida, parole dell'utente:** *"Il codice si deve basare su ciò che è certo, misurato al
> momento. Non sulle stime/normalizzazioni. Le normalizzazioni servono per stimare il consumo gomme,
> non devono infierire sulle previsioni di passo e di fine gara."*

---

## Il problema, in una pagina

Il plugin **calcola già** il passo realistico di ogni vettura e poi **proietta con quello
irrealistico**.

| dove | cosa usa oggi | equivalente misurato, già popolato |
|---|---|---|
| proiezione Player | `Results.NormalizedRaceStartPace` | `RaceAnalyzer.RawTimes.LapBaseline` / `LapMovingAverage` |
| proiezione leader | `leaderData.NormalizedTimes.LapMovingAverage` | `tData.RawTimes.LapMovingAverage` |

Il tempo normalizzato è il tempo **a serbatoio scarico e temperatura di riferimento**: si ottiene
sottraendo `estimatedFuel * 0.03` e la penalità termica. È più veloce del tempo che la vettura farà
davvero, e la differenza non è simmetrica fra le vetture:

| | penalità carburante | su un giro di | bias |
|---|---|---|---|
| leader (Cadillac GTP, 89 L) | 2.40 s | 71.94 s | **3.34%** |
| Player (BMW M4 GT3) | 1.21 s | 77.52 s | **1.56%** |

Il 3.34% su 38.8 giri fa **+1.30 giri**: è il "+1" osservato sul totale del leader per tutta la gara.

**Perché si aggiusta solo alla fine.** Il totale è `giri già completati` (misurati) + `giri proiettati`
(stimati col passo). La parte proiettata si riduce a zero col passare del tempo, quindi l'errore si
annulla da solo alla bandiera — indipendentemente dalla causa. Non è il sistema che impara: è che
smette di dover indovinare.

**La prova che l'intenzione era già questa.** In `RaceAnalyzer.cs:1256` esiste:

```csharp
Results.EstimatedCurrentPace = NormalizedRaceStartPace + PaceDropDueToTyres + fuelPenalty + tempPenalty;
```

cioè si ri-aggiunge tutto quello che la normalizzazione aveva tolto, per ottenere "quanto farà
davvero adesso". Quel numero **esiste e viene usato solo per la dashboard**. Nelle proiezioni non
entra mai.

---

## Decisione: `RawTimes`, non `EstimatedCurrentPace`

Scelta dell'utente, condivisa. Le ragioni, in ordine:

1. **`RawTimes.LapMovingAverage` è una misura. `EstimatedCurrentPace` è una ricostruzione.** La
   seconda è la somma di quattro termini, tre dei quali sono modelli con coefficienti propri
   (`0.03 s/L`, `tempCoef`, il degrado gomme). Ognuno può essere sbagliato, e gli errori si sommano.
   La media dei giri grezzi non ha coefficienti.
2. **La domanda che poniamo è "quanti giri farà nel tempo che resta".** Il miglior predittore dei
   prossimi giri di una vettura sono i suoi ultimi giri veri — con il traffico che troverà davvero e
   la benzina che porterà davvero.
3. **La media mobile che l'utente proponeva esiste già**: `RawTimes.LapMovingAverage` è la media
   degli ultimi 5 giri grezzi (`OpponentTracker.cs:1593`). Non c'è meccanica nuova da costruire.

**Limite accettato:** la media mobile ha un ritardo. Dopo una sosta i 5 giri in memoria sono ancora
del vecchio stint. È un ritardo di pochi giri e **limitato**, contro un bias sistematico del 3.3%
che non si riassorbe mai.

---

## Cosa NON è un problema (verificato, non assunto)

Avevo sollevato il rischio che passando ai grezzi entrassero nella media giri con traffico,
safety car, out-lap. **È sbagliato e l'ho ritirato**: i filtri esistono già e sono tre
(`OpponentTracker.cs`, blocco `isValidOppLap`):

```csharp
if (calculatedLapsSincePit <= 1) isValidOppLap = false;   // out-lap e giro successivo
if (tData.IsInsideGeofence)      isValidOppLap = false;   // in-lap / vettura in corsia box
if (deviation > baseline*0.035 || deviation < -baseline*0.020) isValidOppLap = false;  // outlier
```

---

## Il punto sul filtro che invece resta, ed è quantificato

Domanda dell'utente: *"perché il filtro si comporterebbe diversamente sui grezzi? Sempre quel range
va a coprire."*

La percentuale è la stessa; a cambiare è **la dispersione del dato che deve coprire**.

Il tempo normalizzato è per costruzione **piatto lungo lo stint**: l'effetto del carburante è tolto.
Il tempo grezzo no — cala man mano che la benzina si consuma:

| | escursione carburante su uno stint | banda inferiore del filtro (−2%) |
|---|---|---|
| GTP, 89 L | `89 × 0.03` = **2.67 s** | `−2%` di 70 s = **−1.40 s** |
| GT3, 100 L | `100 × 0.03` = **3.00 s** | `−2%` di 77.5 s = **−1.55 s** |

**L'escursione è quasi il doppio della banda.** Sui grezzi, i giri di fine stint (vettura leggera,
quindi veloce) rischiano di cadere sotto la soglia ed essere **scartati** — con l'effetto di
tenere la media artificialmente lenta.

Mitigazione già presente: la baseline grezza si sposta verso il basso (`OpponentTracker.cs:1519-1535`),
quindi insegue i giri leggeri. Se la rincorsa sia abbastanza rapida **è una cosa da misurare, non da
assumere** — ed è il motivo per cui la Fase 0 viene prima della Fase 2.

---

## Il piano

### Fase 0 — misurare prima di cambiare *(nessun cambio di comportamento)*

Tre domande aperte a cui un solo replay può rispondere, se strumentiamo.

**0a. Il Player è corretto per caso?** `PosAtFlag` del Player è ~34.85 contro un vero 34.83, quindi
sembra giusto. Ma il bias di normalizzazione dovrebbe gonfiarlo dell'1.56% (**+0.54 giri**). Il
sospetto è che la correzione per la sosta lo **sottragga** di altrettanto e i due errori si annullino.
Se è così, togliere la normalizzazione **peggiorerebbe il Player** di mezzo giro mentre migliora il
leader di uno. Da loggare: `activePlayerPace`, `playerL_left` **prima e dopo** la correzione per la
sosta, `RaceLifeTimeLeftSec`.

**0b. Quanto divergono grezzo e normalizzato, per vettura?** Loggare `RawTimes.LapMovingAverage`
accanto a `NormalizedTimes.LapMovingAverage` a ogni aggiornamento. Dà la dimensione vera del bias per
classe, invece della stima da due campioni.

**0c. Il tetto di classe morde davvero?** Loggare `classMaxTank` accanto a `opponentMaxTank`
(oggi il log scrive solo il secondo, mentre il codice usa il primo). Conferma se l'override della
Fase 1 sta già facendo danno in questa combo pista/classe o se è solo latente.

**Criterio di uscita:** un replay Road Atlanta con le tre righe sopra a log.

---

### Fase 1 — `MaxTank` per vettura *(indipendente, basso rischio)*

`OpponentTracker.cs:768-773`: il valore corretto per vettura viene calcolato e poi sovrascritto.

```csharp
double classMaxTank = opponentMaxTank;                    // ✅ per vettura, con BoP di sessione
var dbRecord = Database.Tracks.FirstOrDefault(t => t.TrackID == trackId && t.CarClass == tData.CarClass);
if (dbRecord != null && dbRecord.MaxTank > 0.0)
    classMaxTank = dbRecord.MaxTank;                      // ❌ appiattito sul valore di classe
```

**Intervento:** usare `opponentMaxTank` a valle. Il record di classe resta come ripiego **solo**
quando la vettura non è riconosciuta nel database (`carRec == null`), che è il caso per cui
presumibilmente era stato messo.

**Caso di regressione, numeri veri dal log** (tutte classe 4011 a Road Atlanta):
BMW M4 GT3 EVO `100.0 L` · Ferrari 296 GT3 `104.0 L` · Ford Mustang GT3 `110.0 L` ·
McLaren 720S GT3 `110.0 L`. Devono restare distinte. Errore massimo evitato: **10 L**.

---

### Fase 2 — proiezioni sui tempi misurati

Solo dopo la Fase 0, perché 0a può cambiarne la forma.

- **leader:** `rawLeaderPace` da `RawTimes.LapMovingAverage` invece di `NormalizedTimes.LapMovingAverage`
  (`RaceAnalyzer.cs:586`, `590`).
- **Player:** `activePlayerPace` dall'equivalente grezzo (`RaceAnalyzer.cs:615`).
- **Non si tocca** il calcolo del degrado gomme: lì il normalizzato resta, ed è il suo mestiere.

**Criterio di successo osservabile:** sul replay Road Atlanta,
`SimRIG.Session.LeaderProjectedPosAtCheckered` ≈ **38.8** stabile per tutta la gara (oggi parte da
`38.814`, deriva a `39.5`, tocca `40.9`), e `LeaderRaceTotalLaps` = **39** dall'inizio invece di
assestarsi solo alla bandiera. Il `PosAtFlag` del Player deve **restare** ~34.85.

---

### Fase 3 — ritarare il filtro di validità *(solo se la Fase 0/2 lo dimostra)*

Se la 0b/2 mostrano che sui grezzi i giri di fine stint vengono scartati, la banda va rivista —
probabilmente asimmetrica e ancorata a una statistica robusta invece che alla prima baseline. Non si
tocca prima di avere il dato: oggi non sappiamo se il problema si presenta davvero.

---

### Fase 4 — quello che resta aperto

- **Y-36** — salto del totale all'uscita dai box (transitorio, prima mascherato dal passo sbagliato).
- **Y-38** — sfarfallio dell'identità del leader assoluto (dwell 2 s insufficiente).
- **Y-33** — corsia box e calibrazione. Il più grosso, indipendente da tutto questo.

---

## Cosa serve dall'utente

- **Un replay Road Atlanta dopo la Fase 0** (3x va bene: la strumentazione non dipende dalla velocità).
- Nient'altro fino alla Fase 2.
