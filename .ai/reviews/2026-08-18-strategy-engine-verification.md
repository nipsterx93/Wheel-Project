# Strategy Engine — Verifica formula-by-formula vs Technical Verification Specification

- **Data:** 2026-08-18
- **Reviewer:** claude
- **Baseline:** *Technical Verification Specification — SimRIG Strategy Engine — Definitive Review Baseline*
- **Codice analizzato:** `TargetStrategyManager.cs` (1325 righe), `LogManager.cs` (295), `DataPluginDemo.cs` (estratti), `RaceAnalyzer.cs` (estratti), `FuelManager.cs` (estratti)
- **Metodo:** lettura diretta del sorgente. Nessun report precedente è stato usato come fonte.

## VERDICT: 🔴 **RED**

Un errore concreto di logica di stato nella gestione della pit lane, che ricade **esattamente** nella
condizione che la spec §38 designa come *priorità assoluta*: il primo `DeltaGap` post-pit usa come
reference un macrosettore campionato **dentro** il pit.

Tutta la matematica pura di Undercut e Overcut (§13–§26) è invece corretta e supera i test numerici
1–5 per ispezione. L'anello debole è la macchina di stato del `RelativePace`, non le formule.

---

# 🔴 RED-1 — Reference pit-contaminata nel primo delta post-pit

**Spec:** §9.1, §9.2, §37.9, §37.10, Test 8, Test 9
**Codice:** `TargetStrategyManager.cs:412-428` (branch di invalidazione), `:429-454` (branch valido)
**Esito:** `MISMATCH`
**Impatto:** **strategico / critico**

### Comportamento richiesto

> §9.1 — «**nessun campione osservato durante il pit deve diventare una reference valida per il primo
> rate post-pit**. Questo ultimo requisito è fondamentale perché il `TrackPercentage` continua a
> progredire nella pit lane.»
>
> §9.2 — «Il primo campione pulito dopo la fine del pit deve essere trattato **sempre come nuovo seed**,
> indipendentemente dal fatto che la transizione numerica dei macrosettori sembri valida.»

### Comportamento trovato

Il branch di invalidazione ri-semina **incondizionatamente** con il campione corrente, anche quando
quel campione è stato osservato in pit lane:

```csharp
// TargetStrategyManager.cs:412
if (playerInPit || targetInPit || !isValidSequence || dt < 1.0)
{
    // ... log RELATIVE_PACE_INVALIDATION ...

    // Invalidate and re-seed
    _lastValidMacroSector  = macroSector;        // :424  ← settore osservato IN PIT
    _lastMacroSectorTime   = currentSessionClock; // :425  ← timestamp IN PIT
    _lastMacroSectorFluidGap = currentSignedGap;  // :426  ← gap IN PIT
    _emaInitialized = false;
}
```

Non esiste alcun flag che ricordi «l'ultimo seed è contaminato». Appena il gate pit si libera, la
transizione successiva viene giudicata valida sulla sola progressione numerica:

```csharp
// :406
bool isValidSequence = (_lastValidMacroSector != -1) &&
    ((macroSector == _lastValidMacroSector + 1) || (macroSector == 0 && _lastValidMacroSector == 19));
```

### Traccia di Test 8 (spec §35, test obbligatorio)

| Transizione | `playerInPit` | Gate | Effetto reale | Effetto richiesto |
|---|---|---|---|---|
| → settore 9 | `true` | invalidazione | seed = **gap@9 (in pit)** | congela, **nessun** seed dal pit |
| → settore 10 (uscita) | `false` | `isValidSequence = (10 == 9+1)` → **true**, `dt ≥ 1.0` → **rate calcolato** | `deltaGap = gap@10 − gap@9` **attraversa il pit** | settore 10 = nuovo seed, nessun rate |
| → settore 11 | `false` | rate | secondo rate | **primo** rate pulito |

### Perché è grave, non cosmetico

`_emaInitialized == false` al momento del calcolo contaminato, quindi si entra nel ramo di seed
dell'EMA (`:435-440`) e il valore sporco **inizializza direttamente** la media:

```csharp
CurrentTarget.RelativePace = Math.Max(-10.0, Math.Min(10.0, instantPaceRate)); // :437
```

Il `deltaGap` attraverso una sosta è dell'ordine di **+20 s** su un `dt` di pochi secondi: `instantPaceRate`
satura, e `RelativePace` finisce **incollato alla sbarra del clamp (±10 s/giro)**. Da lì l'EMA a
α=0.30 impiega ~8–10 macrosettori puliti per rientrare in un intervallo credibile — cioè mezzo giro
di dato strategico inservibile proprio nel momento in cui serve, subito dopo una sosta.

Vale identicamente per **Test 9** (target in pit, player in pista).

### Direzione di fix (non applicata — review-only)

Un flag di contaminazione, alzato ogni volta che il gate pit scatta e abbassato solo dopo aver
consumato un seed pulito:

```csharp
if (playerInPit || targetInPit) _pitContaminatedSeed = true;
// ...
// nel branch valido, prima di calcolare il rate:
if (_pitContaminatedSeed)
{
    _pitContaminatedSeed = false;
    // re-seed forzato + evento RELATIVE_PACE_POST_PIT_SEED, nessun rate in questo giro
    _lastValidMacroSector = macroSector;
    _lastMacroSectorTime = currentSessionClock;
    _lastMacroSectorFluidGap = currentSignedGap;
    return; // o equivalente: salta il calcolo
}
```

Questo chiude anche 🟡 Y-7 (evento post-pit seed osservabile).

---

# 🟡 YELLOW

## Y-1 — `CanFinishWithoutPitting`: estensione non approvata

**Spec:** §27 — `ADDITIONAL IMPLEMENTATION RULE — NOT PART OF APPROVED CORE`
**Codice:** `TargetStrategyManager.cs:686` (definizione), `:719` (undercut), `:772` (overcut)
**Impatto:** **strategico**

Come richiesto dalla spec, i 8 punti:

1. **Formula:** `fuel.IsPredictionValid && (fuel.TankLapsRemaining > raceResult.RaceLapsRemaining)`
2. **Input:** `FuelManager.cs:169` → `TankLapsRemaining = CurrentFuelLevel / consumption` (default `99.0`);
   `FuelManager.cs:175` → `IsPredictionValid = (state.CurrentLap > 1) && (consumption > 0)`
3. **Unità:** giri vs giri — dimensionalmente coerente
4. **Gate:** fuso in **OR** dentro il gate `RaceTooLate` di **entrambi** i motori:
   `if (!undercutRaceLapsOK || canFinishWithoutPitting)` (`:719`), idem `:772`
5. **Effetto su Undercut:** forza `UndercutViable = false`
6. **Effetto su Overcut:** forza `OvercutViable = false`
7. **Rischio di escludere una strategia valida:** **sì, concreto.**
   - Se il Player arriva in fondo col carburante ma il **Target deve fermarsi**, il Player è già in
     posizione di overcut naturalmente vincente — e il motore risponde `OvercutViable = false`,
     `Decision = Neutral`. La strategia migliore disponibile viene silenziata.
   - Ignora le soste motivate dalle **gomme**, non dal carburante: un pit per degrado resta legittimo
     anche con il serbatoio sufficiente.
   - `TankLapsRemaining` vale `99.0` per default quando `consumption <= 0`, ma è protetto da
     `IsPredictionValid` — quindi niente falsi positivi prima del giro 2. Questa parte è solida.
8. **Perché è stata introdotta:** non documentata nel codice. Nessun commento, nessun ADR. Presumibile
   soppressione di alert pit a fine gara, ma è una ricostruzione, non un fatto verificabile.

**Difetto diagnostico collaterale:** il rifiuto viene etichettato `StrategyRejectReason.RaceTooLate` e
la stringa `failed=` riporta `RaceLaps` — sia nel log evento sia nella colonna `UndercutRejectReason`
dello snapshot. Chi legge il log non può distinguere «gara troppo corta» da «non serve fermarsi».
Servirebbe un valore d'enum dedicato.

## Y-2 — `OvercutTrafficOK` cablato a `true`

**Spec:** §26 · **Codice:** `:757` — `CurrentTarget.OvercutTrafficOK = true; // In future: check player sector traffic`
**Impatto:** strategico (minore). Il gate esiste ma è inerte: un overcut non può mai essere rifiutato
per traffico. Direzione dell'errore permissiva, non restrittiva. La proprietà SimHub esiste ed è
esposta, quindi la dashboard mostra un `true` privo di significato.

## Y-3 — `LapsSinceLastPit` è discreto, la spec lo vuole continuo

**Spec:** §26 «`LapsSinceLastPit` è continuo» · **Codice:** `:754`

```csharp
double lapsSinceLastPit = oppData.LastPitLap > 0 ? (double)(oppData.HighestLapSeen - oppData.LastPitLap) : 999.0;
```

Differenza di **interi** promossa a `double`. A 2.9 giri dalla sosta il codice calcola `2` → `TargetPittedRecently = true`;
la spec continua darebbe `false`. Gate più permissivo di ~1 giro nel caso peggiore.
Impatto: **numerico**. Il sentinel `999.0` replica correttamente la semantica `HasValue`.

## Y-4 — Snapshot: `DeltaGap`, `DeltaTime`, `InstantPace` sempre vuote

**Spec:** §32 (ricostruibilità di `Input → Intermediate → …`) · **Codice:** `LogManager.cs:108` (header), `TargetStrategyManager.cs:824` (formato)

L'header dichiara 50 colonne e le **posizioni combaciano** con la riga dati (verificato campo per campo).
Ma il formato emette tre campi vuoti proprio dove servono gli intermedi del `RelativePace`:

```csharp
"{0:F3},{1},{2},{3},{4:F3},{5},{6},{7:F1},,,,{8:F3}," +
//                                        ↑↑↑ DeltaGap, DeltaTime, InstantPace: mai popolate
```

Conseguenza: **lo snapshot non permette di ricostruire la matematica del RelativePace**, che è proprio
la parte con il difetto RED-1. Quei valori esistono solo nell'event log, non correlabili riga-per-riga.
Le sezioni Undercut e Overcut sono invece complete e ricostruibili.

Mancano inoltre (§32): array warmup e flag di fallback, `maxOvercutStayLaps`, `playerRawPace`,
`positiveGapToTarget` (quest'ultimo derivabile da `SignedGap`).

## Y-5 — Header parametri: mancano `Beta` e `MinimumDeltaTime`

**Spec:** §34 · **Codice:** `LogManager.cs:96-106`

| Parametro richiesto | Presente | Nota |
|---|---|---|
| StrategyEngineVersion | ✅ | `1.0.0` |
| Alpha = 0.30 | ✅ | |
| **Beta = 0.70** | ❌ | **assente** |
| RelativePaceClamp = 10.0 | ✅ | |
| **MinimumDeltaTime = 1.0s** | ❌ | **assente** |
| PitDecisionBuffer = 0.8 | ✅ | |
| MaxUndercutReactionWindow = 1.0 | ✅ | |
| WarmupThreshold = 0.10 | ✅ | |
| FuelReserve = 0.4 | ✅ | |
| UndercutPositionThreshold = −0.5 | ✅ | |
| TargetPittedRecentlyThreshold = 2.0 | ⚠️ | nome `RecentPitThreshold` |
| MinimumOvercutStay = 0.5 | ✅ | |
| MinimumRaceLapsRemaining = 2.0 | ⚠️ | nome `MinimumRaceLaps` |

Impatto: documentale. Nota: sono **stringhe letterali** nell'header, disaccoppiate dalle costanti reali
nel codice — se qualcuno cambia `0.30` in `TargetStrategyManager.cs:444`, l'header continua a
dichiarare `0.30`. Debito latente.

## Y-6 — Tre valori d'enum di invalidazione mai assegnati

**Spec:** §11 · **Codice:** `:414-419`

Assegnati: `PlayerInPit`, `TargetInPit`, `NoPreviousSeed`, `InvalidSequence`, `DeltaTimeTooSmall`, `None`.
**Mai assegnati:** `DuplicateSector`, `MissingSector`, `TargetChanged`.

- `DuplicateSector` (`7 → 7`): non raggiunge mai il blocco, filtrato a monte da
  `sectorChanged = (macroSector != _myLastMacroSector && ...)` (`:240`). Comportamento corretto — non
  viene calcolato alcun rate — ma **non osservabile** nell'event log.
- `MissingSector` (`12 → 15`): confluisce in `InvalidSequence`. Il salto non è distinguibile da altre
  rotture di sequenza.
- `TargetChanged`: il reset è corretto (vedi GREEN), ma viene loggato come `"Target Changed"` sotto
  `LogModule.STRATEGY`, non come `RELATIVE_PACE_INVALIDATION` sotto `STRATEGY_EVENT`. Chi analizza il
  solo `SimRIG_StrategyEvent_*.txt` non vede la causa del reset.

## Y-7 — Nessun evento post-pit seed distinguibile

**Spec:** §11, §37 · Grep su tutto il progetto: nessuna occorrenza di `POST_PIT_SEED` o equivalente.
Conseguenza diretta di RED-1: non esistendo il concetto di seed post-pit, non c'è nulla da loggare.
Si chiude insieme a RED-1.

## Y-8 — Deadband HUD 0.05 non implementato

**Spec:** §30 · **Codice:** `DataPluginDemo.cs:1647`

```csharp
PluginManager.SetPropertyValue("SimRIG.Target.RelativePaceStr", t, $"{(tgt.RelativePace > 0 ? "+" : "")}{tgt.RelativePace:F2}s/lap");
```

Il requisito **primario** della spec è rispettato: il valore numerico non viene alterato dal
formatting (`:1646` fa `Math.Round(..., 3)` su una proprietà separata). Manca però il deadband:
per `|RelativePace| < 0.05` non c'è alcun trattamento speciale, e un valore di `-0.004` produce
la stringa `-0.00s/lap`. Impatto: **documentale/estetico**.
Stesso schema, coerente, su `PaceDeficitStr` (`:1654`) e sui due `CaptureMarginStr` (`:1671`, `:1676`).

## Y-9 — L'euristica di rilevamento pit del Player produce falsi positivi

**Codice:** `:398`, `:821`, `:1206` (tre copie della stessa espressione)

```csharp
bool playerInPit = state.IsInPitLane || (state.TrackPositionPercent > 0.85 && state.SpeedKmh < 100.0 && state.SpeedKmh > 10.0);
```

Qualunque vettura nell'ultimo 15% del tracciato tra 10 e 100 km/h viene classificata «in pit».
Un tornantino lento nel settore finale — Misano stesso, curva 14/15 — soddisfa la condizione a pieno
regime di gara. Effetto: gate pit spurio → `RelativePace` congelato e ri-seminato una volta per giro
senza motivo, con perdita di un campione utile per giro. Fuori spec (che non definisce il rilevamento
pit), ma **amplifica RED-1**: più spesso scatta il gate, più spesso si materializza il seed contaminato.

Nota di manutenzione: l'espressione è **triplicata** invece di essere un metodo condiviso.

## Y-10 — Cadenza snapshot tick-based, non time-based

**Spec:** §31 (500 ms / 25 tick a 50 Hz) · **Codice:** `:817-818`

```csharp
_snapshotTickCounter++;
if (_snapshotTickCounter >= 25)
```

`TargetStrategyManager.Update` è invocato una volta per tick di telemetria
(`DataPluginDemo.cs:1184`, dentro `DataUpdate`, senza throttling). I 500 ms valgono **solo se**
SimHub gira esattamente a 50 Hz; a 60 Hz la cadenza reale è ~417 ms. Conforme all'assunzione della
spec, fragile rispetto alla realtà. Impatto: **innocuo** (il timestamp è nella riga).

Nota: il contatore vive dentro `if (tracker.TrackedOpponents.ContainsKey(CurrentTarget.Name))`
(`:463`), quindi lo snapshot si ferma quando il target non è tracciato — corretto, ma crea buchi
non annotati nel CSV.

## Y-11 — Possibile double counting del warmup dentro `PaceDropDueToTyres`

**Spec:** §12 «Il warmup post-pit non deve essere già contenuto in questi termini» · **Codice:**
`RaceAnalyzer.cs:889`, `:1041`, `:1059`, `:1075`

```csharp
if (isInPit || lapTime <= 0 || blackFlag > 0) return;                                  // :889
NormalizedTimes.LapMovingAverage = ...Skip(Count - 4).Average();                       // :1041
NormalizedTimes.LapPaceDrop = Math.Max(0.0, LapMovingAverage - LapBaseline);           // :1059
Results.PaceDropDueToTyres = NormalizedTimes.LapPaceDrop;                              // :1075
```

I giri **in pit** sono esclusi (`:889`) — corretto. Ma i giri **successivi** all'out-lap, quelli su
gomma ancora fredda, entrano regolarmente in `LapHistory` e quindi nella media mobile a 4 giri.
Per i ~3 giri dopo una sosta, `PaceDropDueToTyres` contiene quindi una quota di warmup, che poi
viene **sommata di nuovo** esplicitamente come `playerWarmup` (`:700`) e `totalWarmupGain` (`:746`).

Struttura corretta (baseline e drop sono separati), filtro incompleto.
Impatto: **numerico**, limitato alla finestra post-pit, direzione conservativa per l'undercut.

---

# ✅ GREEN — conforme

## RelativePace

| # | Spec | Codice | Esito |
|---|---|---|---|
| §2.1 | `SignedGap > 0` ⇒ player dietro | `:387` `posDiff < 0 ? +gap : -gap`, con `posDiff = (myLap+myPos) − (targetLap+oppPos)` | `MATCH` |
| §2.1 | `PositiveGapToTarget = max(0, SignedGap)` | `:702` | `MATCH` |
| §3.1 | `(ΔGap / Δt) × RefLapTime` | `:432-433` | `MATCH` |
| §4 | EMA `0.30·instant + 0.70·old` | `:444` | `MATCH` |
| §4 | Clamp `[−10, +10]` | `:437`, `:445` | `MATCH` |
| §5 | Sequenza `cur == prev+1` | `:406-407` | `MATCH` |
| §5 | Wrap `19 → 0` valido | `:407` `(macroSector == 0 && _lastValidMacroSector == 19)` | `MATCH` |
| §5 | `18 → 0` invalido | non soddisfa nessuna delle due clausole | `MATCH` |
| §6 | Pit come gate **indipendente** dalla sequenza | `:412` — quattro condizioni in OR, il pit non passa da `isValidSequence` | `MATCH` |
| §6 | `Δt ≥ 1.0 s` | `:412` `dt < 1.0` | `MATCH` |
| §7 | Sequenza anomala → congela, re-seed, rate solo al campione successivo | `:424-427` | `MATCH` |
| §8 | `Δt < 1.0` → nessun rate, nuovo seed | `:412`, `:424-427` | `MATCH` |
| §9.3 | Primo rate post-invalidazione: `EMA = clamp(instantRate)` | `:435-440` | `MATCH` *(formula giusta, seed sbagliato — vedi RED-1)* |
| §10 | Cambio target → reset completo | `:283-307`: `RelativePace = 0.0`, `_myLastMacroSector = -1`, `_lastValidMacroSector = -1`, `_emaInitialized = false`, `_lastMacroSectorTime = 0.0`, `_lastMacroSectorFluidGap = 0.0`, `sectorChanged = false` | `MATCH` |
| §10 | Reset ≠ invalidazione temporanea | cambio target → `0.0`; invalidazione → valore preservato | `MATCH` |

**§10, nota:** il reset è più completo di quanto la spec richieda (azzera anche `sectorChanged`,
impedendo un campione spurio nello stesso tick). Corretto.

## PaceDeficit (§12)

| Spec | Codice | Esito |
|---|---|---|
| `PlayerPace = RaceStartPace + TyreDrop` | `:519` | `MATCH` |
| `TargetPace = RaceStartPace + TyreDrop` | `:521` | `MATCH` |
| `PaceDeficit = Player − Target` | `:529` | `MATCH` |
| Baseline puro / drop separato | `RaceAnalyzer.cs:1073-1075` | `MATCH WITH CAVEAT` → Y-11 |

## Undercut (§13–§18)

| Spec | Codice | Esito |
|---|---|---|
| `PitDecisionBuffer = 0.8` | `:692` | `MATCH` |
| `MaxUndercutReactionWindow = 1.0` | `:691` | `MATCH` |
| `TargetLapsUntilPit = max(0, TargetFuelLaps − 0.8)` | `:693` | `MATCH` |
| `ReactionDeltaLaps = min(TargetLapsUntilPit, 1.0)` | `:694` | `MATCH` |
| `TargetDegradedPace` **senza** warmup | `:696` = `targetRawPace` (`:521`) | `MATCH` |
| `PrePitPaceGain = (TargetDeg − PlayerFresh) × ReactionDeltaLaps` | `:699` | `MATCH` |
| `PlayerFreshPace` | `:697` `NormalizedRaceStartPace + FuelToAdd × fuelWeightCoef` | `MATCH WITH CAVEAT` — la spec non ne fissa la formula; il codice modella la penalità di peso carburante |
| `NetPitAdvantage = TargetPitLoss − PlayerPitLoss` | `:595` | `MATCH` |
| `UndercutAdvantage = PrePitGain + NetPitAdv − Warmup` | `:700` | `MATCH` |
| `UndercutCaptureMargin = Adv − PositiveGap` | `:703` | `MATCH` |
| `PositionOK: SignedGap ≥ −0.5` | `:705` | `MATCH` |
| `FuelOK: PlayerFuelLaps ≥ 1.0` | `:706` | `MATCH` |
| `TrafficOK = ¬TrafficAlert` | `:707` | `MATCH` |
| `CaptureMargin > 0` | `:708` | `MATCH` |
| `RaceLapsRemaining > 2` | `:709` | `MATCH` + estensione Y-1 |
| `TargetNeedsPit` | `:570` `targetFuelDeficit > 0.8` | `MATCH` (formula non vincolata dalla spec) |

## Overcut (§19–§26)

| Spec | Codice | Esito |
|---|---|---|
| `WarmupThreshold = 0.10` | `:735` | `MATCH` |
| Prefisso contiguo `W[k] ≥ 0.10` con `break` | `:733-739` | `MATCH` |
| Fallback `2.0` solo se `null`/vuoto | `:729-730` | `MATCH` |
| Array di soli zeri → `0`, **non** fallback | `:732` azzera prima del loop | `MATCH` |
| `FuelReserve = 0.4` | `:742` | `MATCH` |
| `MaxOvercutStayLaps = max(0, PlayerFuelLaps − 0.4)` | `:742` | `MATCH` |
| `N_eff = min(MaxStay, WarmupAvailable)` | `:743` | `MATCH` |
| Stessa `N_eff` per `StayOutsideGain` **e** `TotalWarmupGain` | `:746`, `:748` | `MATCH` |
| Interpolazione `N' = max(0, min(N, L))`, `I = ⌊N'⌋`, `F = N'−I` | `:204-206` | `MATCH` |
| `Σ W[0..I−1] + F·W[I] se I<L, else 0` | `:209-216` | `MATCH` |
| Nessun accesso a `W[L]` | `:213` guardia `if (I < L)` | `MATCH` |
| `TargetPacePreWarmup = RaceStartPace + TyreDrop` | `:747` | `MATCH` |
| `StayOutsideGain = (TargetPacePreWarmup − PlayerTrackPace) × N_eff` | `:748` | `MATCH` |
| `OvercutAdvantage = StayOut + WarmupGain + NetPitAdv` | `:750` | `MATCH` |
| `OvercutCaptureMargin = Adv − PositiveGap` | `:751` | `MATCH` |
| `TargetIsInPit ∨ TargetPittedRecently` | `:753`, `:759` | `MATCH` |
| `N_eff ≥ 0.5` | `:760` | `MATCH` |
| `OvercutFuelOK: PlayerFuelLaps ≥ N_eff + 0.4` | `:756` | `MATCH` |
| `CaptureMargin > 0` | `:761` | `MATCH` |
| `RaceLapsRemaining > 2` | `:762` | `MATCH` + estensione Y-1 |
| `TargetPittedRecently ≤ 2.0` | `:755` | `MATCH WITH CAVEAT` → Y-3 |

## Decision Engine (§28) — tutti e quattro i casi + parità

**Codice:** `:781-792`

| Caso | Atteso | Codice | Esito |
|---|---|---|---|
| Entrambe viable, `U > O` | Undercut | `:784-785` confronto stretto `>` | `MATCH` |
| Entrambe viable, `U == O` | **Overcut** (tie-break) | `:786-787` `else` | `MATCH` |
| Entrambe viable, `U < O` | Overcut | `:786-787` | `MATCH` |
| Solo Undercut | Undercut | `:789` | `MATCH` |
| Solo Overcut | Overcut | `:790` | `MATCH` |
| Nessuna | Neutral/None | `:781` inizializzato a `Neutral` | `MATCH` |

## Proprietà SimHub (§29) — 21/21 presenti

Registrate in `DataPluginDemo.cs:378-419`, aggiornate in `:1646-1687`. Nessun duplicato, tipi coerenti
(`double` / `bool` / `string`), naming esatto come da spec.

| # | Proprietà | Registrata | Aggiornata |
|---|---|---|---|
| 1 | `SimRIG.Target.RelativePace` | `:378` | `:1646` |
| 2 | `SimRIG.Target.RelativePaceStr` | `:379` | `:1647` |
| 3 | `SimRIG.Target.PaceDeficit` | `:385` | `:1653` |
| 4 | `SimRIG.Target.PaceDeficitStr` | `:386` | `:1654` |
| 5 | `SimRIG.Target.UndercutAdvantage` | `:401` | `:1669` |
| 6 | `SimRIG.Target.UndercutCaptureMargin` | `:402` | `:1670` |
| 7 | `SimRIG.Target.UndercutCaptureMarginStr` | `:403` | `:1671` |
| 8 | `SimRIG.Target.UndercutViable` | `:404` | `:1672` |
| 9 | `SimRIG.Target.OvercutAdvantage` | `:406` | `:1674` |
| 10 | `SimRIG.Target.OvercutCaptureMargin` | `:407` | `:1675` |
| 11 | `SimRIG.Target.OvercutCaptureMarginStr` | `:408` | `:1676` |
| 12 | `SimRIG.Target.OvercutViable` | `:409` | `:1677` |
| 13 | `SimRIG.Target.TargetLapsUntilPit` | `:411` | `:1679` |
| 14 | `SimRIG.Target.ReactionDeltaLaps` | `:412` | `:1680` |
| 15 | `SimRIG.Target.OvercutStayLaps` | `:413` | `:1681` |
| 16 | `SimRIG.Target.UndercutPositionOK` | `:414` | `:1682` |
| 17 | `SimRIG.Target.UndercutFuelOK` | `:415` | `:1683` |
| 18 | `SimRIG.Target.UndercutTrafficOK` | `:416` | `:1684` |
| 19 | `SimRIG.Target.OvercutFuelOK` | `:417` | `:1685` |
| 20 | `SimRIG.Target.OvercutTrafficOK` | `:418` | `:1686` |
| 21 | `SimRIG.Target.TargetPittedRecently` | `:419` | `:1687` |

## Logging (§31, §33)

| Spec | Codice | Esito |
|---|---|---|
| Due file distinti `StrategySnapshot_*.csv` / `StrategyEvent_*.txt` | `LogManager.cs:80-81` | `MATCH` |
| Code separate per snapshot ed eventi | `:45-46`, `:204-214` | `MATCH` |
| Eventi **non** throttled dagli snapshot | emissione immediata in coda; flush comune ogni 500 ms (`:190`) | `MATCH` |
| `RELATIVE_PACE_UPDATE` con `instantRate`, `emaBefore`, `emaAfter`, `clamped`, sector | `TargetStrategyManager.cs:447` — tutti presenti, più `deltaGap` e `deltaTime` | `MATCH` |
| `reason=Multiple` con elenco gate falliti | `:806`, `:813` — `reason={...} | failed={uFailedStr}` | `MATCH` |
| Eventi `UNDERCUT_VIABLE/NONVIABLE`, `OVERCUT_*`, `STRATEGY_CHANGED` | `:797-815`, solo sul cambio di stato | `MATCH` |
| Allineamento header ↔ dati snapshot (50 colonne) | `LogManager.cs:108-112` vs `TargetStrategyManager.cs:823-833` — verificato campo per campo | `MATCH` |

---

# Test numerici obbligatori (§35)

Verificati **per ispezione del codice**, non per esecuzione: la suite attuale
(`User.PluginSdkDemo.Tests`) non copre nessuno di questi casi.

| Test | Atteso | Esito |
|---|---|---|
| 1 — Undercut: `(91.5−90.0)×1.0 = 1.5`; `Adv = 1.5+0−0.5 = 1.0`; `Margin = 1.0−0.6 = 0.4` | ✅ | `:699`, `:700`, `:702-703` |
| 2 — `N_eff = min(1.5, 1.2) = 1.2`, stessa quantità per entrambi i gain | ✅ | `:743`, `:746`, `:748` |
| 3 — Prefix: `[1.0,0,0.5] → 1`; `[0,0,0] → 0`; `null/empty → 2.0` | ✅ | `:729-740` |
| 4 — Interpolazione: `W=[1.0,0.4,…]`, `N=1.4` → `1.0 + 0.4×0.4 = 1.16` | ✅ | `:204-217` |
| 5 — `NetPitAdv = 22−20 = 2`; `OvercutAdv = 1+1+2 = 4` | ✅ | `:595`, `:750` |
| 6 — Wrap: `18→19` ok, `19→0` ok, `18→0` invalido, `0` seed, `0→1` primo rate | ✅ | `:406-407`, `:424-427` |
| 7 — `8→10` invalido, `10` seed, rate solo su `10→11` | ✅ | `:406-407`, `:424-427`, `:435-437` |
| **8 — Pit con TrackPercentage continuo** | ❌ | **FALLISCE — RED-1.** Il codice produce un rate su `9→10` con reference presa in pit |
| **9 — Target in pit** | ❌ | **FALLISCE — RED-1**, stesso meccanismo |

---

# Riepilogo per priorità

| ID | Titolo | Impatto | File:riga |
|---|---|---|---|
| 🔴 **RED-1** | Reference pit-contaminata nel primo delta post-pit | **critico** | `TargetStrategyManager.cs:412-428` |
| 🟡 Y-1 | `CanFinishWithoutPitting` — estensione non approvata | strategico | `:686`, `:719`, `:772` |
| 🟡 Y-2 | `OvercutTrafficOK` cablato a `true` | strategico | `:757` |
| 🟡 Y-9 | Euristica pit con falsi positivi (amplifica RED-1) | strategico | `:398`, `:821`, `:1206` |
| 🟡 Y-3 | `LapsSinceLastPit` discreto anziché continuo | numerico | `:754` |
| 🟡 Y-11 | Warmup double counting in `PaceDropDueToTyres` | numerico | `RaceAnalyzer.cs:889`, `:1041` |
| 🟡 Y-4 | Snapshot: intermedi RelativePace sempre vuoti | osservabilità | `:824`, `LogManager.cs:108` |
| 🟡 Y-6 | `DuplicateSector`/`MissingSector`/`TargetChanged` mai assegnati | osservabilità | `:414-419` |
| 🟡 Y-7 | Nessun evento post-pit seed | osservabilità | — |
| 🟡 Y-5 | Header: mancano `Beta`, `MinimumDeltaTime` | documentale | `LogManager.cs:96-106` |
| 🟡 Y-8 | Deadband HUD 0.05 non implementato | documentale | `DataPluginDemo.cs:1647` |
| 🟡 Y-10 | Cadenza snapshot tick-based | innocuo | `:817-818` |

---

# Osservazioni fuori spec

1. **`TargetStrategyManager.cs:686`** — due istruzioni sulla stessa riga fisica:
   ```csharp
   CurrentTarget.TrafficAlert = pitExitTrafficConflict;                    bool canFinishWithoutPitting = ...
   ```
   `canFinishWithoutPitting`, cioè la regola non approvata, è nascosta in coda a una riga di
   assegnazione non correlata. Difficile da individuare rileggendo il file.

2. **`ComputeInterpolatedWarmup` con `laps = NaN`** (`:200-218`): `Math.Min(NaN, L)` → `NaN`,
   `(int)Math.Floor(NaN)` → `int.MinValue`, la guardia `I < L` passa, `penalties[int.MinValue]` →
   `IndexOutOfRangeException`. `nEffective` deriva da `fuel.TankLapsRemaining`, che è un quoziente:
   percorso improbabile ma non impossibile. Una guardia `double.IsNaN` costa una riga.

3. **`SetPlayerAsTarget` / `SetNoTarget`** (`:1188`, `:1268`) azzerano `_myLastMacroSector` ma **non**
   `_lastValidMacroSector`, `_emaInitialized`, `_lastMacroSectorFluidGap`. Non è un difetto attivo —
   il rientro su un target reale passa dal reset completo di `:283-307` — ma è un'asimmetria che
   diventerà un bug se qualcuno tocca quel percorso. `ResetSession()` (`:1316`) lo fa correttamente.

4. **Duplicazione del calcolo pit loss** (`:565-590` vs `:901-920`): il blocco di log MergeGap
   ricalcola da zero fuel deficit, fuel to add, stationary time e pit loss del target, con le stesse
   costanti (`0.8`, `0.3`, `+2.0`) ricopiate. Due implementazioni da tenere allineate a mano.

5. **`refLapTime` ricalcolato due volte** (`:372-376` e `:614-617`) con catene di fallback **diverse**:
   la seconda omette il ramo `TrackLengthMeters / 50.0`. Il `RelativePace` usa la prima, il traffic
   check la seconda.
