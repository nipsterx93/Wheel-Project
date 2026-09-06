# Fisica del carburante

## Massa, non volume

Il motore fisico dei simulatori (iRacing, ACC) conserva l'energia a livello di **massa**, non di
volume. Il carburante va monitorato in **kg**, non in litri, ogni volta che entra in un calcolo di
prestazione — è esattamente la conversione mancante che ha causato Y-43 in questo progetto (la
penalità di peso applicata per litro invece che per kg, sovrastimata del 33%).

La densità varia con la temperatura:

```
ρ(T) = ρ15 · [1 - αT · (T - 15)]
```

Per benzine da competizione: `ρ15 ∈ [0.735, 0.760] kg/L`, `αT ≈ 0.001 K⁻¹`. Un delta di 20°C
(15→35°C) sposta la densità del 2%: su un serbatoio di 100 L sono ~1.5 kg, meno di mezzo giro di
autonomia per una GT3 — rilevante solo se il consumo è già calibrato molto fine, altrimenti
rumore rispetto ad altri errori. `RaceTimeProjection.cs` usa `FuelDensityKgPerLitre = 0.75`, un
valore fisso: coerente con l'intervallo, la temperatura non è tracciata.

## Fuel effect: quanto rallenta il carburante a bordo

```
c_fuel = ∂t_lap / ∂m_fuel     [s / (kg·giro)]
```

Ordini di grandezza per categoria (fuel effect a serbatoio pieno):

| Categoria | Massa min. | Serbatoio | c_fuel per 10 kg | Effetto a pieno |
|---|---|---|---|---|
| GT3 | 1250-1330 kg | 100-120 L | 0.025-0.040 s/giro | +0.25/+0.35 s |
| LMDh/GTP | 1030 kg | 60-70 kg | 0.035-0.055 s/giro | +0.25/+0.40 s |
| LMP2 | 930-950 kg | 75 L | 0.040-0.065 s/giro | +0.28/+0.45 s |

Il coefficiente si esprime in **secondi per kg**, non per litro — stessa nota di Y-43. Il valore
usato in `RaceTimeProjection.cs` (`FuelWeightPenaltySec`, ~0.03 s/kg) cade dentro il range GT3,
coerente con la classe tipicamente girata in questo progetto.

## Normalizzare il tempo sul giro

Per isolare il degrado gomma dal semplice alleggerimento per consumo:

```
t_norm(k) = t_meas(k) - c_fuel · [m_fuel(k) - m_ref]
```

`m_ref` è tipicamente 0 kg o il peso di qualifica. Serve a capire se il pilota sta gestendo le
mescole o se l'usura procede più veloce dello svuotamento del serbatoio.

## Stima del consumo nei primi giri

Il progetto oggi valida il consumo per giro con **IQR + tolleranza 15% sulla media**
(`FuelManager.cs:105`, `ValidateFuelConsumptionIQR`, portato da irdashies). Alternative dalla
letteratura, utili se l'IQR si rivela insufficiente su una casistica nuova:

- **Integrazione della portata istantanea**, se il simulatore la espone ad alta frequenza, invece
  di leggere solo il livello statico a fine giro (più robusto ai transitori di partenza/beccheggio).
- **Aggiornamento bayesiano ricorsivo**: parti da un prior dalle prove libere `C_prior ~
  N(μ_prior, σ_prior²)`, aggiorna con le osservazioni di gara pesando per la loro varianza:

  ```
  μ_post = (1 - K_n)·μ_prior + K_n·ȳ_n,   K_n = n·σ_prior² / (n·σ_prior² + σ_meas²)
  ```

  Con `n` piccolo il peso resta sul prior da setup; dopo ~3 giri a regime si sposta sui dati
  reali. Utile se in futuro si vuole ridurre ulteriormente il "buco nero" dei primi giri — oggi
  già attenuato per il **passo** da Y-52 passo 2, non ancora per il carburante.

**Non è una richiesta di implementazione**: sono alternative da valutare solo se l'IQR attuale
mostra un limite misurato, non un aggiornamento da fare a prescindere.
