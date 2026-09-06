# Scomposizione del tempo di sosta

## Il problema

Un plugin telemetrico agnostico riceve solo canali scalari generali: tempo di sessione,
velocità, livello carburante. Da questi soli tre segnali si può scomporre una sosta osservata in
**quanto è costato il rifornimento** e **quanto il cambio gomme**, senza leggere lo stato
esplicito dei meccanici (che molti simulatori non espongono) e — punto rilevante per questo
progetto — **senza leggere `TyreManager.CurrentScope`**, che oggi è pilotato solo dai tasti
volante e non è derivabile dalla telemetria (vedi il punto congelato **Y-14** in
`.ai/PROJECT_STATE.md`).

## Le tre finestre di una sosta

| Fase | Condizione telemetrica | Timestamp |
|---|---|---|
| Arresto piazzola | `v(t) < 0.05 m/s` e in corsia box | `t_stop` |
| Inizio flusso carburante | derivata del livello carburante `≥ ε_flow` (~0.5 L/s) | `t_fuel_start` |
| Fine flusso carburante | derivata `< ε_flow` per almeno 0.5 s | `t_fuel_end` |
| Ripartenza | `v(t) ≥ 0.1 m/s` | `t_launch` |

```
T_stat = t_launch - t_stop                    (tempo totale fermo)
T_fuel = t_fuel_end - t_fuel_start            (finestra di erogazione)
Δt_post = t_launch - t_fuel_end               (residuo dopo il rifornimento)
```

## Derivare il tempo gomme da `Δt_post` — la formula già proposta per Y-14

Questa è **esattamente** la strada già annotata come "percorso più solido" nel punto congelato
Y-14, qui con la stima del confine del rifornimento più precisa (soglia di flow-rate invece del
solo scope):

- **Layout sequenziale** (i meccanici iniziano solo a lancia scollegata): la sottrazione è esatta.
  ```
  TempoGomme = StationaryTime - RefuelingTime
  ```
- **Layout simultaneo** (gomme e carburante in parallelo, es. iRacing IMSA GT3/GTP): la
  sottrazione **sottostima**. Vale solo un limite superiore, o — se si assume che il cambio gomme
  domini quando `RefuelingTime < StationaryTime` — `TempoGomme ≈ StationaryTime`.

`PitRadar.IsPitLayoutSequential` (`PitRadar.cs:335`) distingue già i due casi per default statico
per gioco (Sequential ovunque tranne iRacing, Simultaneous per iRacing finché non rilevato
dinamicamente) — la stessa distinzione che questa tecnica userebbe, non una nuova.

**Perché resta una decisione, non un'implementazione:** `TriggerDynamicLayoutDetection` richiede
di conoscere già sia `tFuel` sia `tTyres` per confermare quale dei due layout è in uso — stesso
problema uovo-e-gallina descritto in Y-14. Questa tecnica **non lo risolve**, migliora solo la
precisione del confine `RefuelingTime` una volta che il layout è noto o assunto.

```csharp
public struct PitStopBreakdown
{
    public double TotalStationaryTime, FuelTime, FuelMassAdded, FlowRate;
    public double TireTime, RepairTime;
    public bool IsSequentialMode;
}

// fEnd = ultimo campione con derivata del livello carburante sopra soglia.
// postFuelDuration = tLaunch - t[fEnd].Time.
// Se postFuelDuration supera (nominalTireDuration - margine), il cambio gomme è
// avvenuto a lancia staccata (sequenziale): TireTime = min(postFuelDuration - drop, nominalTireDuration).
// Altrimenti il cambio è stato assorbito dal rifornimento: TireTime = 0.
// L'eccesso oltre la durata nominale gomme, se presente, è tempo di riparazione danni.
```

## Riparazione danni

Se il residuo eccede la durata massima plausibile per un cambio gomme, l'eccesso è **tempo di
riparazione**, non gomme. Questo progetto ha già un tetto anti-riparazione-danni equivalente
(`OpponentTracker.cs:1007`, `tFuel + tTyres + 6.0`) — la stessa idea, soglia diversa.
