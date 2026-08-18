# Piani di implementazione

Un file per task complesso, nominato `YYYY-MM-DD-<slug>.md`.
I task semplici non hanno bisogno di un piano: si descrivono direttamente in `HANDOFF_LOG.md`.

Un piano serve quando il task tocca più moduli, cambia un contratto/interfaccia, o richiede
che un altro agente lo riveda **prima** che il codice venga scritto.

## Template

```markdown
# <Titolo>

- **Data:** YYYY-MM-DD
- **Autore:** <agente>
- **Esecutore previsto:** <agente>
- **Stato:** Bozza | In review | Approvato | In corso | Completato | Abbandonato

## Obiettivo
Cosa deve essere vero alla fine, in 2-3 righe.

## Vincoli
Cosa NON deve cambiare (API pubbliche, comportamento a runtime, formato dei settings salvati...).

## File coinvolti
- `percorso/file.cs` — natura della modifica

## Passi
1. ...
2. ...

## Verifica
Comandi esatti di build e test, e cosa ci si aspetta come output.

## Rischi
Cosa può rompersi, e come ce ne accorgiamo.

## Domande aperte
Decisioni che servono prima di partire.
```
