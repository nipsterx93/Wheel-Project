# Analisi del plugin DahlDesign — proiezioni di fine gara

> Fonte: `Solo per analisi logiche/DahlDesignProperties-master`, open source, di Andreas Dahl.
> File chiave: `iRacing/iRacing.cs` (6577 righe). Il blocco delle proiezioni sta alle righe
> **4139-4331**, il calcolo del passo alle **3480-3570**.
>
> Scopo: capire se le nostre formule sono sbagliate o se lo sono i loro ingressi.

---

## Risultato in una riga

**La formula per il momento della bandiera è algebricamente identica alla nostra.** Ciò che cambia,
e cambia tutto, è **come si stima il passo** — e lì il loro impianto è più semplice del nostro e
soprattutto **si auto-ripara** dal difetto che ci ha bloccati per due giorni (Y-32 / Y-39).

---

## 1. La formula della bandiera: identica

Dahl (`iRacing.cs:4253-4256`):

```csharp
double? leaderTimeOut = (timeLeftSeconds / leaderExpectedLapTime) + leaderTrackPosition;
leaderDecimal = leaderTimeOut - ((int)(leaderTimeOut * 100)) / 100;
double? timeUntillLeaderCheckered = leaderExpectedLapTime * (leaderTimeOut + (1 - leaderDecimal) - leaderTrackPosition);
timeLapsRemaining = (timeUntillLeaderCheckered / myExpectedLapTime) + trackPosition;
```

Sviluppando, con `T` = tempo residuo, `Lp` = passo leader, `p` = posizione leader:

```
leaderTimeOut            = T/Lp + p
leaderDecimal            = frac(T/Lp + p)                       <- P1LapBalance
timeUntillLeaderCheckered = Lp * (T/Lp + 1 - frac(T/Lp + p))
                          = T + Lp * (1 - frac(...))
                          = T + Lp * (ceil(leaderTimeOut) - leaderTimeOut)
```

**È esattamente la nostra `RaceTimeProjection.TimeUntilLeaderCheckered`**: countdown più la frazione
di giro che manca al leader, moltiplicata per il suo passo.

Due dettagli tecnici da non fraintendere leggendo il codice:

- `((int)(x * 100)) / 100` **non** tiene due decimali: entrambi gli operandi sono `int`, quindi è una
  divisione intera e il risultato è `floor(x)`. Serve a estrarre la parte intera, non ad arrotondare.
- Dahl non somma i giri già completati del leader; noi sì. **Irrilevante**: `ceil(N+x) − (N+x)` con
  `N` intero dà lo stesso risultato di `ceil(x) − x`. Le due scritture coincidono.

**Conclusione: la nostra matematica non era il problema.** Confermato dall'evidenza di ieri, dove
`L_PosAtFlag` partiva a `38.831` contro i `38.8` del software di riferimento e poi derivava — una
formula sbagliata non parte giusta.

---

## 2. Il passo del leader: tre righe, nessuno stato

`iRacing.cs:4170-4188`:

```csharp
double leaderExpectedLapTime = (leaderLastLap.TotalSeconds * 2 + leaderBestLap.TotalSeconds) / 3;
if (leaderLastLap.TotalSeconds == 0) leaderExpectedLapTime = leaderBestLap.TotalSeconds * 1.01;
if (leaderBestLap.TotalSeconds == 0) leaderExpectedLapTime = leaderLastLap.TotalSeconds;
```

Media pesata **2:1 fra ultimo giro e giro migliore**, entrambi grezzi e letti dal simulatore.

Cosa **non** c'è, ed è il punto:

- nessuna normalizzazione carburante o temperatura;
- nessuna media mobile con finestra di validità;
- nessuna baseline persistita che possa avvelenarsi;
- **nessuno stato fra un tick e l'altro**: due letture dal gioco e una divisione.

Il peso 2:1 sull'ultimo giro dà reattività; il giro migliore fa da ancora contro un ultimo giro
sporco. Se l'ultimo giro è un in-lap da 140 s, il risultato è `(280 + 70)/3 = 117` — sbagliato per
un tick, ma **si auto-corregge al giro successivo** perché non c'è memoria.

Il nostro impianto, al confronto, ha baseline persistita, media mobile su 5 giri, finestra di
validità ancorata alla baseline, e normalizzazione. Quattro pezzi di stato, ognuno un punto in cui
un valore sbagliato può insediarsi e restare.

---

## 3. Il passo proprio: dove sta la lezione vera

`iRacing.cs:3483-3550`. Il pilota ha lo storico dei propri giri, quindi la stima è più ricca.

```csharp
double thresholdLap = fastLap * 1.015;   // 1.5% sopra il GIRO PIU' VELOCE
double runOffLap    = fastLap * 1.05;

// esclusioni
if ((lapStatusList[i] < 3 && lapStatusList[i] != 0)                       // no in/out/joker lap
    && !(lapListSeconds[i] > (fastLap + 8) && lapListSeconds[i] > runOffLap))  // no incidenti
{
    if (lapListSeconds[i] < thresholdLap) fastList.Add(...); else slowList.Add(...);
}

pace = fastList.Count > 0 ? fastList.Average() : 0.0;
```

### 3a. L'ancoraggio: il giro **più veloce**, non il primo

È la differenza che conta. Il nostro criterio di validità è ancorato alla **baseline del primo giro
valido**; il suo al **giro più veloce visto finora**.

Conseguenza: una baseline può solo **migliorare**. Un primo giro lento non la avvelena, perché verrà
sostituito dal primo giro più veloce.

### 3b. E se il giro più veloce è anomalo? Il sistema **fallisce in sicurezza**

È esattamente il nostro caso Y-32: `Sven Neiss` con una prima misura a `60.950` contro un passo vero
di `69-71`.

Con l'impianto di Dahl: `fastLap = 60.95` → `thresholdLap = 61.86` → tutti i giri veri (69-71) finiscono
in `slowList` → **`fastList` resta vuota** → `pace = 0`.

E `pace = 0` non è un numero sbagliato: fa scattare la cascata di ripieghi
(`iRacing.cs:4139-4151`):

```csharp
myExpectedLapTime = pace;
if (== 0) myExpectedLapTime = predictedLapTime.TotalSeconds;
if (== 0) myExpectedLapTime = lapRecord.TotalSeconds * 1.03;
if (== 0) myExpectedLapTime = trackLength / 40;          // 40 m/s = 144 km/h di media
```

**Da noi lo stesso campione produceva una finestra `[59.34, 62.67]` che rifiutava la realtà e teneva
un passo falso per tutta la gara. Da lui produce un valore nullo che degrada su un ripiego onesto.**
La differenza fra fallire rumorosamente e fallire in silenzio.

### 3c. Due scorciatoie che risolvono il ritardo della media mobile

`iRacing.cs:3537-3549`:

```csharp
// il passo sta calando: due giri lenti consecutivi e validi
if (lap[0] > threshold && lap[1] > threshold && status[0] < 3 && status[1] < 3 && slowList.Count > 1)
    pace = (slowList[0] + slowList[1]) / 2;

// il passo sta salendo: due giri veloci consecutivi
if (lap[0] < fastLap * 1.005 && lap[1] < fastLap * 1.005 && status[0] == 1 && status[1] == 1)
    pace = (fastList[0] + fastList[1]) / 2;
```

Due giri consecutivi nella stessa direzione **scavalcano la media**. Risolve il compromesso
"robusta ma lenta": la media protegge dal rumore, le due scorciatoie danno la reattività.

E nota: nel caso Y-32, la prima scorciatoia avrebbe messo `pace` alla media dei due ultimi giri
lenti — cioè **~70 s, il valore corretto**.

### 3d. Confidenza esplicita

```
fastList.Count > 3 -> calculationAccuracy = 3
fastList.Count > 1 -> 2
altrimenti         -> 1
pace irrealistico rispetto al record di pista -> 1
```

Esposta come proprietà. Noi non abbiamo niente del genere: un passo stimato su un campione e uno su
venti sono indistinguibili a valle.

### 3e. Criterio doppio per gli incidenti

`!(lap > fastLap + 8 && lap > fastLap * 1.05)` — **entrambe** le condizioni. Otto secondi persi
contano solo se sono almeno il 5% del giro. Si trasferisce fra circuiti corti e lunghi senza ritarare.

---

## 4. La sosta **non** entra nei giri rimanenti

`truncRemainingLaps` arriva al calcolo carburante (`iRacing.cs:5076`) **senza** correzione per il
tempo perso ai box. Le soste sono trattate separatamente (`FuelPitStops`, `FuelPitWindowFirst`).

Noi invece la sottraiamo dentro `playerL_left`, ed è l'origine di Y-42: la correzione toglie
**1.25 giri** dove la sosta reale ne è costata **0.53**.

La differenza di rischio è asimmetrica e vale la pena dirla chiara:

| | errore | effetto sul carburante |
|---|---|---|
| Dahl (non sottrae) | sovrastima i giri di ~0.5 per sosta | imbarca un po' **in più** — conservativo |
| noi (sottraiamo troppo) | sottostima di ~1 giro | imbarca **in meno** — si resta a piedi |

**Sbagliamo nella direzione pericolosa.**

---

## 5. `LapsRemaining` + `LapBalance`: la stessa idea che abbiamo appena implementato

```csharp
int truncRemainingLaps = ((int)(remainingLaps * 100)) / 100;   // parte intera
double? lapBalance = remainingLaps - truncRemainingLaps;       // parte decimale
```

Espone intero e decimale separati. È esattamente ciò che abbiamo aggiunto con
`ProjectedPosAtCheckered` (Y-37) e `LeaderProjectedPosAtCheckered`. Conferma indipendente che la
scomposizione era la mossa giusta.

`P1LapBalance` è il decimale del **leader**: il `.8` del `38.8` mostrato dal software di riferimento
dell'utente.

---

## 6. Cosa prendere, in ordine di valore

| # | cosa | perché |
|---|---|---|
| 1 | **Ancorare il criterio di validità al giro più veloce**, non alla prima baseline | Elimina la trappola che si auto-difende (Y-32/Y-39). Una baseline può solo migliorare |
| 2 | **`pace = 0` invece di un valore inventato**, con cascata di ripieghi | Fallire rumorosamente invece che in silenzio |
| 3 | **Le due scorciatoie a due giri** | Reattività senza rinunciare alla robustezza |
| 4 | **Passo del leader = `(2×ultimo + migliore)/3`** | Tre righe senza stato al posto di baseline + media mobile + finestra + normalizzazione |
| 5 | **`calculationAccuracy` esposto** | A valle si può decidere quanto fidarsi |
| 6 | **Criterio doppio (assoluto E relativo) per gli scarti** | Si trasferisce fra circuiti |
| 7 | **Togliere la sosta dai giri rimanenti** (Y-42) | Oggi sbagliamo nella direzione pericolosa |

**Non** serve prendere il motore intero: la formula centrale ce l'abbiamo già identica. Serve
sostituire il modo in cui stimiamo il passo — che è, guarda caso, esattamente la Fase 2 del piano,
ma con un disegno migliore di quello che avevo abbozzato.

---

## 7. Cosa questo NON risolve

Onestà: l'impianto di Dahl è pensato per iRacing e per un solo pilota alla volta.

- Il **passo degli avversari** ha meno protezioni delle nostre (solo ultimo e migliore giro): non
  gestisce out-lap o giri sotto giallo di un avversario, perché non ne tiene lo storico.
- Non risolve lo **sfarfallio dell'identità del leader** (nostro Y-38): usa il leader corrente e basta.
- Non affronta la **corsia box e le geofence** (nostro Y-33), che resta il nostro lavoro più grosso.
