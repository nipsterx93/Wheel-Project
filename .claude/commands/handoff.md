---
description: Chiude il turno — scrive la voce in HANDOFF_LOG.md dal template, pota l'undicesima voce in archivio se serve, rilascia il lock in PROJECT_STATE.md, e propone i commit
argument-hint: [riassunto in una riga di cosa è stato fatto, opzionale]
---

Equivalente Claude Code di `/handoff` in Antigravity (`.agent/workflows/handoff.md`). Stesso
contenuto, formato diverso: se una delle due versioni cambia, aggiorna anche l'altra o segnalalo
in `AGENTS.md` — non devono andare fuori sincrono.

Stai chiudendo il turno su The Wheel Project / Antigravity 2.0. Segui questi passi nell'ordine,
senza saltarne nessuno.

## 1. Controlla lo stato prima di scrivere qualunque cosa

```bash
git status --short
```

Se ci sono modifiche non committate che fanno parte di questo turno, valuta se vanno committate
prima dell'handoff (di norma sì, un commit per il lavoro e uno per l'handoff/rilascio lock, come
da convenzione di questo repo — vedi `HANDOFF_LOG.md` per esempi reali di come si è fatto finora).

## 2. Controlla il lock

Leggi il blocco `LOCK` in `.ai/PROJECT_STATE.md`.

- Se `owner` è il tuo agente: hai lavorato sotto lock, quindi questo turno **deve** finire con il
  lock rilasciato (`owner: NONE`, `since/task/scope/expires: null`).
- Se `owner` è già `NONE`: non avevi preso il lock (probabilmente una sessione di sola
  analisi/review). Scrivi comunque la voce di handoff — AGENTS.md lo richiede esplicitamente anche
  per un turno che non ha toccato codice.

## 3. Scrivi la voce in cima a `.ai/HANDOFF_LOG.md`

Usa il template già presente nel file (sezione "Template (copiare e compilare)"). Compilalo con
informazioni vere, verificabili — `file:riga`, non descrizioni vaghe — guardando il diff reale di
questo turno (`git diff`, `git log`), non solo quello che ricordi di aver detto in chat.

$ARGUMENTS

Se sopra è stato passato un riassunto, usalo come punto di partenza per il campo "Task", ma
verifica comunque i dettagli guardando i file toccati — un riassunto dato a mano può essere
impreciso o incompleto quanto un numero scritto a mano altrove in questo repo.

Prima di scrivere, conta le voci esistenti:

```bash
grep -c '^## \[20' .ai/HANDOFF_LOG.md
```

Se il conto (contando anche quella che stai per aggiungere) supererebbe 10, **sposta l'undicesima
voce** (quella più in fondo, prima della sezione "Handoff più vecchi") in cima a
`.ai/archive/HANDOFF_LOG_archive.md`, verbatim — non riassumerla. Poi rimuovila da `HANDOFF_LOG.md`.

## 4. Rilascia il lock, se lo tenevi tu

Nel blocco `LOCK` di `.ai/PROJECT_STATE.md`, riporta:

```yaml
owner:      NONE
since:      null
task:       null
scope:      null
expires:    null
```

## 5. Un'occhiata finale a numeri scritti a mano

Se durante il turno hai cambiato il conteggio dei test o dei punti aperti/chiusi, controlla che
non resti scritto a mano altrove nello stesso file (es. il cappello di `PROJECT_STATE.md` cita
spesso il conteggio test in prosa) — è il tipo di disallineamento che questo repo ha già pagato
più volte.

## 6. Committa

Un commit per il lavoro (se non già fatto) con prefisso `[claude]` e messaggio nello stile già
usato nella history (`git log` per lo stile esatto), poi un commit separato per l'handoff/rilascio
lock: `[claude] chore: release lock — <riassunto brevissimo>`. Non usare `--amend`, non fare
`git push` a meno che l'utente non lo chieda esplicitamente.

## 7. Riporta all'utente

Due o tre righe: cosa è stato fatto, se main compila/i test passano (o perché no), qual è il
prossimo passo secondo il campo "Per chi entra" appena scritto.
