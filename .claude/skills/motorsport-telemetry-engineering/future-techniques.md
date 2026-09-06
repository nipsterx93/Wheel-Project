# Tecniche non ancora adottate

Queste due tecniche non hanno un equivalente in questo progetto oggi, e **nessun punto Y aperto
le richiede**. Sono qui come riferimento per quando (se) servirà davvero un caso concreto — non
come lavoro da pianificare. Implementarle senza un problema misurato che le giustifichi
sarebbe esattamente l'anti-pattern YAGNI che il progetto evita altrove.

## Filtro Alpha-Beta — tracciare un trend, non solo un valore

`RelativePaceTracker.cs` oggi ha un **EMA a singolo stato** (`Alpha = 0.30`): smussa un valore,
non ne stima esplicitamente la tendenza. Un filtro Alpha-Beta tiene invece due stati — valore e
derivata — utile se in futuro servisse **isolare esplicitamente un tasso di degrado** (es. gomme)
invece di limitarsi a smussare il segnale:

```
p̂(k|k-1) = p̂(k-1|k-1) + T · v̂(k-1|k-1)      (predizione posizione)
v̂(k|k-1) = v̂(k-1|k-1)                        (predizione trend, costante)
ỹ = z(k) - p̂(k|k-1)                          (innovazione)
p̂(k|k) = p̂(k|k-1) + α·ỹ
v̂(k|k) = v̂(k-1|k-1) + (β/T)·ỹ
```

Vincolo di stabilità: `0 < α < 2`, `0 < β < 4 - 2α`. Per evitare oscillazioni:
`β = α² / (2 - α)` (Benedict-Bordner). Fragile a un singolo outlier non filtrato a monte — la
sua innovazione distorce sia il valore sia il trend per molti giri successivi: va sempre fatto
seguire a un filtro di outlier (es. Hampel/MAD, vedi `robust-statistics.md`), mai usato da solo
su un segnale grezzo.

## CUSUM — rilevare un cambio di regime, non una tendenza

Utile per intercettare un salto di livello netto (foratura lenta, danno aerodinamico, cambio
meteo) che un filtro di smoothing assorbirebbe gradualmente invece di segnalarlo. Due statistiche
cumulative, una per direzione:

```
S⁺(k) = max(0, S⁺(k-1) + (x_k - μ0 - k_tol))       (crollo di passo)
S⁻(k) = max(0, S⁻(k-1) + (μ0 - k_tol - x_k))       (miglioramento di passo)

k_tol = δ·σ / 2          (tolleranza di deriva)
H = h·σ,  h ∈ [4.0, 5.0] (soglia di allarme, serie corte)
```

Quando `S⁺` o `S⁻` supera `H`: evento di cambio regime, si azzera e si riallinea `μ0` sul nuovo
livello. Limite: non distingue un gradino da un drift continuo marcato (un degrado gomma forte
accumula massa statistica come un falso allarme) — richiede che il trend lineare sia sottratto a
monte se il segnale ha già una deriva nota.

## Quando riconsiderarle

Se in futuro un replay mostra un caso che i meccanismi attuali (EMA singolo, IQR, hysteresis)
non gestiscono — es. una foratura lenta che il filtro attuale scambia per rumore, o un degrado
gomme che serve isolare esplicitamente dal resto del segnale — registralo come nuovo punto in
`.ai/PROJECT_STATE.md` con il replay che lo dimostra, poi si valuta se una di queste due tecniche
è la risposta. Non prima.
