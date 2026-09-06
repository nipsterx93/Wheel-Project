# Statistica robusta su campioni radi

## Il problema che risolve

Calibrare un dato dalle soste (tempo gomme, portata carburante, limite corsia box) con
osservazioni che arrivano una ogni 45-65 minuti di gara. Con `N < 10` la media e la deviazione
standard sono fragili: un solo intoppo meccanico le sposta.

## Cosa fa già questo progetto

`CalibrationConsensus.cs` usa già la **mediana** su una finestra scorrevole (9 campioni,
consenso minimo 3) con una **tolleranza di accordo fissa** passata dal chiamante — non calcolata
dai dati. È deliberatamente più semplice della tecnica sotto, ed è già verificata in gara (dodici
osservazioni del limite di pit lane a Misano, un outlier a 80 km/h contro undici a 60: la mediana
tiene, la media darebbe 61.7).

## L'affinamento possibile: soglia dinamica invece che fissa

La MAD (Median Absolute Deviation) stima la dispersione in modo robusto quanto la mediana stima
il centro (breakdown point 50% per entrambe):

```
mediana = mediana(campioni)
MAD = mediana(|campione_i - mediana|)
σ_MAD = 1.4826 · c_N · MAD
```

`c_N` è la correzione per campioni piccoli (Rousseeuw & Croux 1993) — senza, la MAD è distorta
per bassa cardinalità:

| N | 2 | 3 | 4 | 5 | 6 | 7 | 8 |
|---|---|---|---|---|---|---|---|
| c_N | 1.196 | 1.495 | 1.363 | 1.206 | 1.200 | 1.140 | 1.129 |

Test di ammissibilità per un nuovo campione (Z-score modificato, Iglewicz & Hoaglin 1993), con
un pavimento minimo `σ_floor` per evitare instabilità quando i campioni sono quasi identici:

```
M = |nuovo_campione - mediana| / max(σ_MAD, σ_floor)
M > τ  →  rigetta il campione (τ ≈ 2.5 per N tra 3 e 6)
```

**Differenza pratica rispetto a `CalibrationConsensus.cs` di oggi:** la tolleranza di accordo
diventerebbe funzione della dispersione osservata invece di un numero fisso per grandezza. Più
adattivo, ma anche più difficile da prevedere/debuggare — e la versione attuale non ha mai
mostrato un limite misurato in gara. **Non sostituire `CalibrationConsensus.cs` con questo senza
un caso concreto in cui la tolleranza fissa si è dimostrata insufficiente.**

## Il limite che si applica a entrambe le versioni

Un cambiamento meccanico permanente durante la gara (es. un danno che altera stabilmente il
flusso di rifornimento) viene inizialmente rigettato come outlier: servono 2-3 campioni del nuovo
regime prima che mediana/consenso si spostino. Per non restare bloccati, un contatore di rigetti
consecutivi coerenti (stesso verso di deviazione) può rilassare la soglia — tecnica non
implementata in questo progetto, utile solo se un caso reale la richiede.
