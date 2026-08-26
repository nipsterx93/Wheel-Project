# Piano di implementazione — Y-28, cascata di calibrazione guidata

> Traduce in lavoro concreto `.ai/plans/2026-08-25-calibration-cascade-design.md` (disegno,
> approvato) e `.ai/plans/2026-08-24-cross-agent-review-brief.md` (metodo di verifica).
> Ogni fase è un commit separato e reversibile da solo, come da convenzione del repo.
> Nessun codice scritto in questo documento: è la sequenza, non l'implementazione.

---

## Prima di cominciare

Un dettaglio trovato preparando questo piano, che cambia l'ordine di una fase: `PitLimiterOn` oggi
si legge **solo al volo** in `DataPluginDemo.cs:1246` (per il colore del LED), non è mai stato
copiato in `SessionState`. Serve esporlo prima di poterlo riusare per imparare il limite di pit lane
dal Player — Fase 0c qui sotto.

---

## Fase 0 — Fondamenta indipendenti dalla cascata

Queste tre cose hanno valore **anche da sole**, prima ancora che esista una cascata che le usi.
Vanno prima perché la cascata si appoggia su di esse.

### 0a — Riconoscere una sosta di solo carburante senza pretendere 20 L esatti

Oggi `CalibrationMode.SplashAndDash` scatta solo con `fuelToAdd == 20.0`. Si cambia in una soglia
minima (si riusa `MinLitresForFuelRate`, già esistente — nessun nuovo numero magico).

**File:** `PitRadar.cs`, il punto dove si determina `_activeMode` all'ingresso in corsia box.
**Test:** dato un `fuelToAdd` e uno scope gomme, quale `CalibrationMode` risulta — casi: 20 L esatti
(deve continuare a funzionare, non si rompe nulla di esistente), 12 L, 4 L (sotto soglia, non deve
attivare SplashAndDash), 12 L con gomme diverse da `None` (non deve attivare SplashAndDash).

### 0b — Guard temporale sullo sfarfallio di `IsInPitLane`

Estende Y-23. Oggi si scarta una visita solo per distanza percorsa insufficiente; si aggiunge una
soglia minima di **durata** della permanenza, complementare (vedi punto 6 del disegno per il perché
servono entrambe). Soglia fissa bassa (1-2 s) quando la geofence non è ancora nota, poi
`MinimumCredibleTransitSec()` una volta disponibile.

**File:** `PitRadar.cs`, stesso punto del guard sulla traversata (Y-23).
**Test:** riproduce lo sfarfallio reale di Daytona (0.2 s) → scartato; un transito vero (30+ s) →
accettato; caso di confine sulla soglia.

### 0c — Il Player impara il limite di pit lane dal proprio limitatore

Nuova fonte, indipendente dagli avversari. Si legge `PitLimiterOn`, si tiene la velocità massima
tenuta col limitatore inserito (con una persistenza minima, per non prendere un singolo tick
anomalo), e si alimenta lo **stesso** consenso già esistente (`UpdatePitLaneSpeedLimit`, Y-20) con
la classe del Player. Nessun nuovo meccanismo di scrittura: si riusa quello che già protegge il dato
degli avversari.

**File:** `SessionState.cs`/`TelemetryReader.cs` (esporre `PitLimiterOn`), `PitRadar.cs` o un nuovo
modulo puro tipo `PlayerPitSpeedObserver.cs` per la logica di persistenza.
**Test:** una sequenza di velocità con limitatore inserito che supera la persistenza minima scrive;
una che non la raggiunge no; un singolo tick anomalo in mezzo a una serie buona non altera l'esito
(il consenso già lo garantisce, va solo verificato che il nuovo percorso lo attraversi davvero).

**Nota per il futuro, dal punto 2 della revisione con l'utente**: non si esclude che in futuro serva
un modo per **forzare** una ricalibrazione (oggi impossibile per `PitDriveThroughTime` e il ramo
`StopAndGo` di `PitTransitTime`, che restano a scrittura singola). Non si implementa qui — si tiene
solo a mente di non scrivere la Fase 2 in un modo che renda difficile aggiungerlo dopo.

---

## Fase 1 — Riconoscere una sessione di calibrazione

Un predicato puro, minuscolo ma va isolato e testato: `!IsRaceSession && !IsQualySession`. Usato
**solo** per decidere se la cascata vocale può parlare — l'apprendimento passivo (Fase 4 esistente)
resta universale, non lo si tocca.

**File:** nuovo metodo statico, o dentro il modulo della Fase 2.
**Test:** race → falso, qualifica → falso, né l'uno né l'altro → vero.

---

## Fase 2 — La macchina a stati della cascata

Il pezzo centrale. Nuovo modulo **puro**, senza dipendenze SimHub (come tutti gli altri nati da
Y-9 in poi — vedi `ARCHITECTURE.md`, sezione "Moduli di decisione").

**Input:** cosa manca oggi per la combinazione Track+Class corrente (riusa `BuildCalibrationStatus`,
non lo duplica) e se il prerequisito del giro genuino è soddisfatto.

**Output:** quale sia il prossimo passo da annunciare, in quest'ordine:

```
0. (prerequisito) giro genuino completato?  → no: "esci in pista, fai un giro"
1. geofence / PitDriveThroughTime           → "fai un drive-through"
2. PitTransitTime / AccDec / FuelFillRate   → "sosta solo benzina, gomme su NONE"
3a. TyreChangeTime (All4)                   → "sosta solo gomme, tutte e 4"
3b. moltiplicatore 2 gomme                  → "sosta solo gomme, 2 gomme"
3c. moltiplicatore 1 gomma                  → "sosta solo gomme, 1 gomma"
done                                        → nessun messaggio
```

Ogni passo salta da solo se il dato corrispondente è già presente (Track+Class e Class verificati
indipendentemente, come da disegno) — compreso il caso Spa discusso: classe nota, circuito nuovo →
si parte direttamente dal passo 1.

**File:** nuovo `CalibrationCascade.cs`.
**Test:** matrice di combinazioni "cosa manca" → "passo atteso". Casi da coprire esplicitamente:
tutto mancante (parte dal prerequisito), solo Track+Class mancante con Class già nota (salta dritto
al passo 1, si ferma prima del 3), solo Class mancante con Track+Class nota (salta dritto al 3a),
tutto presente (nessun messaggio).

---

## Fase 3 — Collegare il completamento di un passo agli eventi reali

Qui la macchina a stati della Fase 2 incontra la telemetria vera. Servono due cose:

- **Il criterio di successo per ogni passo**, riusando esiti già calcolati e già verificati — non se
  ne inventano di nuovi (punto 4 del disegno): `FuelRateUsable`/`TyreTimeUsable` per benzina e
  gomme, il guard di traversata + il nuovo guard temporale (Fase 0b) per il drive-through.
- **Il contatore di permanenza** per la logica di insistenza del disegno (punto 9): un giro passato
  senza avanzamento → ripeti l'istruzione corrente; avanzamento avvenuto → passa oltre in silenzio.

Questa è la parte con più stato (a che punto siamo, da quanti giri) e quindi la più vicina
all'orchestratore esistente.

**File:** nuovo `CalibrationCascadeRunner` (o l'orchestrazione dentro `DataPluginDemo.cs`, da
valutare in base a quanto stato serve tenere).
**Test:** un passo che riceve un evento "usable" avanza; uno che riceve un evento non-usable resta
fermo e lo segnala; il contatore giri scatta solo in assenza di avanzamento.

---

## Fase 4 — Messaggistica: dash e ingegnere vocale

Le chiavi vocali per i passi nuovi (gomme a 2 e a 1, completamento cascata) — quelle per benzina,
gomme intere e drive-through esistono già (`CALIB_FUEL_REQ`, `CALIB_TYRE_REQ`, `CALIB_DT_REQ`,
`CALIB_SG_REQ`). Una proprietà dash che esponga il passo corrente e l'istruzione testuale.

**File:** `PitWallLanguage.cs` (nuove chiavi), `DataPluginDemo.cs` (nuove proprietà SimHub).
**Test:** non applicabile come test automatico puro — verifica manuale in gioco, come da nota su
"cosa non posso testare da solo" in `CLAUDE.md`.

---

## Fase 5 — Moltiplicatori gomme 2 e 1: schema dati

Due campi nuovi in `ClassRecord` (`TyreChangeMultiplier2`, `TyreChangeMultiplier1`), a zero di
default — `GetTireMultiplier` in `TargetStrategyManager.cs` usa il valore calibrato se presente,
altrimenti il fallback cablato attuale (0.5/0.25), esattamente come deciso per Y-26.

**File:** `PitRadar.cs` (schema `ClassRecord`), `TargetStrategyManager.cs` (`GetTireMultiplier`).
**Test:** con moltiplicatore non calibrato → fallback cablato; con moltiplicatore calibrato →
quello misurato; il calcolo `tempo_misurato / TyreChangeTime_All4` con i numeri dell'esempio
dell'utente (27s → 13s → 0.481).

---

## Cosa NON è in questo piano

- **Nessuna persistenza extra del progresso della cascata fra sessioni.** Analizzato nel disegno,
  punto 12: non serve, il database stesso è la fonte di verità di cosa manca.
- **Nessun comando di ricalibrazione forzata.** Tenuto a mente come estensione futura (Fase 0c),
  non implementato ora.
- **Nessuna modifica al fallback dei moltiplicatori gomme per le quattro ruote singole
  separatamente** — si misura un rappresentante per categoria (2 gomme, 1 gomma), non le quattro
  combinazioni, come da disegno.

---

## Un residuo trovato preparando questo piano, non in scope qui

Verificando dove si scrive `FuelFillRate`/`TyreChangeTime` per il percorso **non guidato** (una
sosta di gara qualunque, non una calibrazione deliberata — il ramo "else" di `PitRadar.Update`, Fase
4 del piano calibrazioni originale), ho trovato che quel percorso scrive a livello `EstimatedPlayer`
**senza consenso fra osservazioni multiple**: `CanOverwrite(EstimatedPlayer, EstimatedPlayer)` è
vero, quindi l'ultima sosta naturale osservata sovrascrive la precedente. È lo stesso schema di
Y-26/Y-27, su un percorso diverso. Non tocca la cascata (che passa sempre dal ramo guidato,
`Confirmed` al primo colpo) — lo registro come **Y-29** in `PROJECT_STATE.md` per non perderlo,
resta fuori da questo piano.

---

## Come verificare tutto il piano a fine lavoro

```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
```
```bash
"User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
```

Ogni fase, prima di passare alla successiva: build 0 errori, test verdi, regressione verificata
neutralizzando il fix di quella fase (ADR-004) — non si salta mai l'ultimo passo, ha già trovato un
test che non copriva nulla (Y-25).

**Criterio di successo osservabile per l'utente**, a tutte le fasi concluse: entrando in Practice o
Test su un circuito o una classe mai calibrati, l'ingegnere annuncia da solo cosa manca e guida
passo passo, senza che l'utente debba sapere quale sosta fare per quale dato.
