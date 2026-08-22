# Piano — Apprendimento autonomo delle calibrazioni

> Deciso con l'utente il 2026-08-19/20. Obiettivo dichiarato: **meno dati inseriti a mano,
> più il plugin impara da solo**, senza mai lasciare che un dato debole sovrascriva uno forte.

---

## Il problema che chiudiamo

Oggi la calibrazione ha tre difetti, tutti verificati nel codice:

1. **Scrive senza sapere da dove vieni.** `PitEntryPct` si scrive alla prima transizione di
   `IsInPitLane` con il campo a `-1.0` (`PitRadar.cs:706`), senza nessun filtro di sessione —
   zero occorrenze di `IsRaceSession`/`IsQualySession` in tutto il file. Partendo dai box,
   `PitEntryPct` finisce per contenere la posizione del **pit box**, non dell'ingresso corsia.
2. **Il primo valore vince per sempre.** `HasValidCleanSectorBounds()` verifica solo che i campi
   non siano `-1.0`. Un dato scritto male resta valido, e un dato migliore non può sostituirlo.
3. **Il carburante si impara solo da una procedura di test.** `FuelFillRate` si scrive solo dentro
   `CalibrationMode.SplashAndDash` (`PitRadar.cs:782`), che scatta solo con una richiesta di
   **esattamente 20 litri** (`:712`). Una sosta reale pulita non insegna nulla.

---

## A. Guard anti-teletrasporto

**Perché non una soglia di percentuale.** Valutate e scartate due alternative:
un guard **temporale** (aggirabile per costruzione: basta aspettare oltre la soglia, e su una
pista mai vista non abbiamo tempi di riferimento da cui derivarla) e una **soglia di percorso**
tipo `TrackPct >= 0.8` (assume che l'ingresso box sia tardi nel giro — vero a Misano con
`0.9498`, ma è una proprietà del tracciato; e non protegge da un ESC premuto *dopo* l'80%).

**Criterio adottato: plausibilità del movimento.** Un teletrasporto è per definizione una
discontinuità di `TrackPositionPercent`; guidare, per quanto veloce, ha un avanzamento per tick
limitato dalla velocità massima e dalla lunghezza del circuito.

È lo stesso principio già usato due volte in questo repo — `MaxSectorFraction` in
`RelativePaceTracker` e `GapJump` (Y-13): *un salto che il tempo trascorso non giustifica è un
artefatto, non un evento di gara*. Qui è ancora più diretto, perché a saltare è la posizione
stessa e non un valore derivato.

Usa **solo** `TrackPositionPercent` e la lunghezza pista: niente di appreso in anticipo, nessuna
assunzione su dove cada l'ingresso box.

```
delta = wrap(pos - posPrecedente)          // 0.99 -> 0.01 vale +0.02, non -0.98
maxDelta = (MaxPlausibleSpeedMs * dt) / trackLengthMeters
continuo  se  |delta| <= maxDelta
```

Casi limite da gestire:
- `dt` troppo grande (pausa, replay saltato) → **non valutabile**, non "continuo": si resetta lo
  stato invece di validare a vuoto;
- `pos == 0.0` → già oggi convenzione di "stato azzerato" (`SessionState.cs:134`,
  `PitRadar.cs:855`), si aggancia a quella invece di introdurne una seconda;
- lunghezza pista non nota → guard disattivato (come il fallback di `MaxSectorFraction`).

## B. Gate di autorizzazione — una condizione, non una sequenza

La sequenza rigida `True→False→True` non parte mai in gara: partendo dalla griglia manca il
`True` iniziale.

Requisito reale, più debole e sufficiente: **prima di fidarsi di una transizione `False→True`,
deve esserci stato almeno un campione genuino — in pista, posizione valida, movimento continuo —
in questa sessione.**

Una sola condizione copre entrambi i casi:

| partenza | `IsInPitLane` iniziale | quando autorizza |
|---|---|---|
| dai box (practice) | `True` | solo dopo l'uscita vera `True→False` — comportamento originale |
| dalla griglia (gara) | `False` | **subito**: si è già in pista, il primo pit stop è genuino |

Nota dall'utente, importante: `TrackPositionPercent` è la **stessa coordinata** in corsia box e in
pista — è `IsInPitLane` a distinguere dove ti trovi, non la posizione.

## C. Livelli di confidenza

Oggi non esiste il concetto: `-1.0` = mancante, qualunque altro valore = definitivo.

**Geofence (entry+exit), un solo flag per la coppia** — nascono sempre insieme dalla stessa
osservazione, sia nel flusso Player sia in quello opponent:

| livello | fonte | può sovrascrivere |
|---|---|---|
| `Unknown` | — | — |
| `Estimated` | reverse-engineering dagli opponent | solo `Unknown` e altri `Estimated` |
| `Confirmed` | Player, dopo autorizzazione | tutto |

**Carburante e gomme, quattro livelli.** Asimmetria voluta rispetto alle geofence: una posizione
non diventa ambigua per il fatto di essere misurata in gara, un tempo di erogazione **sì**, perché
in una sosta mista il tempo fermo non si separa fra benzina e gomme senza conoscere già una delle
due (è il modello Sequential/Simultaneous già coperto dai test esistenti).

| livello | fonte | condizione |
|---|---|---|
| `Unknown` | — | — |
| `EstimatedOpponent` | media di classe dagli avversari | usa `opponentTiresChanged`, già esistente |
| `EstimatedPlayer` | sosta di gara del Player | **solo se pulita**: benzina sola o gomme sole |
| `Confirmed` | test guidato in practice | isolato per costruzione, vince sempre |

Regola unica: **si scrive solo se la nuova confidenza è >= a quella registrata.** Un dato
stimato non può mai scavalcare un `Confirmed`.

**Fuori scope per ora:** derivare il valore da una sosta **mista** usando il layout
Sequential/Simultaneous e l'altra variabile già nota. È fattibile ma è logica in più che merita
test propri: meglio un passo successivo che costruirla alla cieca insieme al resto.

## D. Calibrazione in gara

Non c'è nessun gate di sessione da togliere — non esiste. Serve una **seconda via di scrittura**,
meno restrittiva del test guidato, che riconosca una sosta naturale pulita:

```
fuelFillRate = litriAggiunti / tempoErogazione
```

Verificato sull'esempio dell'utente: 16 L / 6.1 s = 2.62 L/s, **lo stesso valore** che il sistema
ha già nel database per la GT3. La formula generale riproduce il numero che la procedura guidata
considera buono.

I dati grezzi servono già a ogni sosta (`_lastFuelIncreaseTime`, `_fuelStartTime`,
`_fuelLevelAtStopStart`): non serve nuova strumentazione, solo leggerli fuori dal ramo guidato.

Per il Player la purezza della sosta è **certa** (`fuelToAdd` e lo scope gomme sono noti); per gli
opponent è **stimata** — da cui la differenza di livello.

## E. Stima delle geofence dagli avversari

Fallback per il pilota che salta la practice. Gli avversari non hanno `IsInPitLane` da telemetria,
quindi si ricava dalla velocità:

Al momento in cui scatta il trigger di persistenza (già esistente: 3 s sotto soglia adattiva),
la vettura viaggia **stabile** al limite — la fase di decelerazione è finita prima, quindi
l'integrazione a ritroso è a velocità costante per costruzione:

```
metriPercorsi = velocità * LowSpeedPersistenceSec
pctPercorsa   = metriPercorsi / trackLengthMeters
PitEntryPct   ≈ posAlTrigger - pctPercorsa
```

Speculare all'uscita, quando la persistenza cessa. Resta una stima (la velocità non è mai
perfettamente costante) → livello `Estimated`, sovrascrivibile dal Player.

## F. `CalibrationStatus` dettagliato

Oggi guarda solo `PitTransitTime` e `FuelFillRate`: un circuito con geofence calibrate male
risulta comunque `READY`. Va esteso a coprire anche le zone **e** a esporre il livello di
confidenza, non solo presenza/assenza.

`SimRIG.Pit.CalibrationStatus` esiste già ed è esposta (`DataPluginDemo.cs:531`): si arricchisce
quella invece di aggiungerne una nuova, più proprietà granulari per la dash.

---

## Ordine di implementazione

Ogni fase è un commit separato e reversibile da solo.

| # | fase | file | rischio |
|---|---|---|---|
| 1 | `TrackPositionValidator` | nuovo, senza dipendenze SimHub | basso — codice nuovo, isolato |
| 2 | Livelli di confidenza | `PitRadar.cs` (`TrackRecord`, `ClassRecord`) | medio — tocca lo schema JSON |
| 3 | Gate di autorizzazione | `PitRadar.cs` | medio — cambia *quando* si scrive |
| 4 | Fuel/gomme da sosta pulita | `PitRadar.cs` | medio |
| 5 | `CalibrationStatus` esteso | `PitRadar.cs`, `DataPluginDemo.cs` | basso |
| 6 | Stima geofence da opponent | `OpponentTracker.cs` | alto — lasciare per ultimo |

**Compatibilità JSON:** i record esistenti non hanno i campi di confidenza. Un valore già presente
senza livello dichiarato va trattato come `Confirmed` — è stato quasi certamente misurato dal
Player, e degradarlo a `Estimated` lo esporrebbe a essere sovrascritto da una stima peggiore.
Il record Misano attuale (`entry=0.9498 exit=0.1088 pitLimit=60 transit=36.0`) deve continuare a
funzionare senza intervento manuale.

## Criteri di successo osservabili

- Nessun `PitEntryPct` scritto durante una partenza dai box.
- Un ESC a metà giro non produce mai una calibrazione.
- Su Daytona (database vergine) le geofence si scrivono e sono plausibili.
- Una sosta pulita in gara produce un `FuelFillRate` coerente con la formula litri/tempo.
- Il record Misano esistente resta valido e non viene degradato.
