# Le formule corrette per la fine gara — sintesi del report esterno

> Fonte: `DeepSearch/Strategia Gara Endurance Motorsport.pdf` (17 pagine, Gemini Deep Research).
>
> ⚠️ **Nota tecnica sull'estrazione:** il PDF contiene **130 immagini** e le equazioni sono figure,
> non testo. L'estrazione testuale restituisce solo la prosa; le formule qui sotto sono state lette
> una per una dalle immagini. Chi rilegge non le trovi cercando nel testo del PDF.

---

## Le formule, trascritte

### Momento in cui esce la bandiera

**1. Tempo per chiudere il giro in corso** — ancorato al **timestamp** dell'ultimo passaggio, non
alla frazione di posizione:

```
Δt_cur,i = max(0, τ_i − (t_now − t_last_cross,i))
```

**2. Tempo per completare k giri futuri** (incluso quello in corso):

```
E_i(k) = Δt_cur,i + (k−1)·τ_i + Σ t_loss,i        (somma sulle soste ancora da fare)
```

**3. Istante del k-esimo taglio del traguardo:**

```
T_cross,i(k) = t_now + E_i(k)
```

**4. Si cerca il minimo intero k*_i tale che:**

```
T_cross,i(k*) ≥ T_zero      ⟺      E_i(k*) ≥ T_rem
```

**5. L'istante della bandiera è il MINIMO su tutte le vetture della classe più veloce:**

```
T_flag = min          T_cross,i(k*_i)
       i ∈ C_hyper
```

**6. Decimale del leader allo scadere del cronometro** (interpolazione fra il penultimo taglio e
`T_zero`):

```
Giri_dec,L*(T_zero) = Giri_tot,L* − 1 + (T_zero − T_cross,L*(k*−1)) / τ_L*
```

### Vettura target

**7. Tempo netto disponibile per giri interi futuri:**

```
T_rem,M^net = ΔT_M − Δt_cur,M − (N_pits,M · t_loss,M)          con  ΔT_M = T_flag − t_now
```

**8-9. Giri interi e frazione:**

```
N_full,M = ⌊ T_rem,M^net / τ_M ⌋
p_flag,M = ( T_rem,M^net − N_full,M · τ_M ) / τ_M
```

**10. Posizione decimale alla bandiera:**

```
Giri_dec,M(T_flag) = L_M + 1 + N_full,M + p_flag,M
```

**11. Giri per cui imbarcare carburante — arrotondamento per ECCESSO:**

```
Giri_completati,M = ⌈ Giri_dec,M(T_flag) ⌉ = ⌈34.83⌉ = 35
```

---

## Confronto con quello che abbiamo

### ✅ Lo scheletro è identico

La nostra `TimeUntilLeaderCheckered` e la coppia (4)+(5) ristretta al leader corrente sono la stessa
cosa. E la decomposizione (7)-(10) è algebricamente equivalente alla nostra
`playerAbsPos + playerL_left`, **a patto che `Δt_cur` sia coerente con la frazione di posizione**:

```
L + 1 + (ΔT − (1−pos)·τ − pits)/τ  =  L + pos + (ΔT − pits)/τ
```

Confermato anche da DahlDesign, che usa la stessa struttura. **Tre fonti indipendenti concordano
sullo scheletro.** Non era mai lì il problema.

### ❌ Tre differenze concrete, tutte nostre

**A) Il giro in corso si misura dal timestamp, non dalla posizione.**
Noi ricaviamo il tempo che manca a chiudere il giro dalla frazione di posizione campionata. Con
campionamento a 3 s la frazione soffre di aliasing. Il report è esplicito: ancorarsi al **timestamp
dell'ultimo passaggio** — `max(0, τ − (t_now − t_last_cross))` — è "matematicamente più solido".
Il `max(0, ...)` protegge dal caso in cui la vettura stia impiegando più del previsto.

**B) La bandiera è un minimo su tutta la classe veloce, non il leader corrente.**
La formula (5) **dissolve** il nostro Y-38 (sfarfallio dell'identità del leader): non si insegue più
chi è primo adesso, si calcola per ogni vettura della classe regina quando taglierà dopo lo scadere
e si prende il primo. Se il leader corrente deve ancora fermarsi, il suo `t_loss` lo sposta avanti e
un altro diventa il minimo **in modo continuo**, prima ancora del sorpasso fisico. Nessun dwell,
nessuna isteresi sull'identità.

**C) La sosta si sottrae come tempo, punto.**
Formula (7): `− (N_pits · t_loss)`. Con una sosta da 41 s su un giro da 77.5 s fa **0.53 giri**.
La nostra formula analitica (con il termine `J = pitLoss/stintLaps` che cambia anche il
denominatore) ne toglie **1.25**. **È Y-42, e adesso sappiamo qual è la forma corretta.**

---

## Le tre cose nuove che non avevamo considerato

**1. Il passo: né grezzo né normalizzato — Base Pace + reiniezione del peso.**
Il report conferma che il normalizzato è sbagliato per proiettare (è un limite asintotico a
serbatoio vuoto), **ma dice anche che la media grezza è sbagliata**: campionata a serbatoio pieno
riflette un passo lento e ignora l'alleggerimento futuro, producendo una sottostima cronica dei giri
e mancanza di carburante a fine stint. La procedura corretta:

1. filtrare i tempi **normalizzati** per estrarre il *Base Pace* (stato reale di gomme e grip);
2. nell'integrazione in avanti, ricalcolare `τ` **per ogni singolo giro futuro**, ri-aggiungendo la
   massa attesa a bordo in quel giro.

**Questo contraddice la scelta che avevo raccomandato io** (`RawTimes.LapMovingAverage`) ed è più
corretto di entrambe le opzioni che avevamo sul tavolo.

**2. Filtro di Hampel (MAD) al posto della finestra percentuale.**
Finestra mobile di ~7 giri, mediana (punto di rottura 50%: regge anche con 3 giri su 7 rovinati),
deviazione assoluta dalla mediana, scarto se `|x − mediana| > k · 1.4826 · MAD` con `k` = 3 o 4.
**Più il change-point reset:** se 3 giri consecutivi risultano anomali **tutti nella stessa
direzione**, non è rumore — è un cambio vero di ritmo, e si svuota la finestra ripopolandola coi
tempi nuovi. È il rimedio esatto alla trappola auto-bloccante di Y-32/Y-39.

**3. Filtro Alpha-Beta al posto dell'isteresi asimmetrica.**
Il report descrive **testualmente il nostro difetto Y-31**: l'isteresi asimmetrica crea "stati
assorbenti", il valore si blocca in alto e le condizioni per scendere non si verificano più,
lasciando il sistema "bloccato su un valore sbagliato per un'intera gara". La soluzione è un filtro
predittivo-correttivo (Alpha-Beta, o Kalman 1D): assorbe una frazione `α` dell'innovazione a ogni
tick, non ha gradini su cui incastrarsi, e con **guadagno dinamico** (se l'innovazione supera una
soglia, `α` sale temporaneamente) aggancia subito i cambi veri.

---

## Due cose da tenere presenti, che il report NON risolve

**Ghost Lap.** Se l'istante di taglio previsto cade a meno del rumore di campionamento dal
`T_zero`, non possiamo sapere se il leader farà o no un giro in più. Il report dice: **non
restituire un numero puntuale** — esporre lo scenario binario con la probabilità, e **dimensionare
il carburante sul limite superiore**. Oggi noi restituiamo sempre un punto.

**Il minimo sulla classe (formula 5) dipende dalle soste previste degli avversari**, che noi stimiamo
male. La formula è giusta, ma la qualità dell'ingresso resta un nostro problema aperto.

---

## Valutazione critica del documento

Da leggere prima di trattarlo come oro colato.

- **Le tecniche citate sono reali e standard**: filtro di Hampel su MAD, Alpha-Beta, Brier score
  sono strumenti consolidati in elaborazione del segnale e valutazione probabilistica.
- **Le fonti su HH Timing sono pertinenti** (software di cronometraggio professionale reale) e
  toccano proprio "Race finish calculation", "Sanitized average lap time", "Pit stop length".
- ⚠️ **La bibliografia è però gonfiata.** Le voci 24-31 comprendono un articolo sulla domotica Honda,
  uno sulla manutenzione di pannelli solari, uno di classificazione EMG, due indici di volume MDPI
  (elenchi di articoli, non articoli) e uno di navigazione inerziale. **Non sostengono le
  affermazioni specifiche a cui sono agganciate.** Il contenuto tecnico regge lo stesso, ma
  l'apparato di citazioni è decorativo: non va usato come garanzia di autorevolezza.
- ⚠️ **Discrepanza di unità sul coefficiente carburante.** Il report parla di `0.03 s/kg`. Il nostro
  codice usa `0.03 s/litro`, e la benzina pesa ~0.75 kg/l. Se il coefficiente giusto è per kg,
  **stiamo applicando la penalità carburante ~33% in eccesso**. Da verificare, non da assumere.

---

## Il quadro finale, in una riga

Lo scheletro è giusto (tre fonti indipendenti concordano). Sbagliamo **tre ingressi** — il giro in
corso misurato dalla posizione invece che dal timestamp, la sosta sottratta con la formula sbagliata,
il passo preso da un contenitore inadatto — e **due filtri** (validità dei giri, stabilizzazione del
totale) che sono concettualmente del tipo sbagliato e vanno sostituiti, non ritarati.
