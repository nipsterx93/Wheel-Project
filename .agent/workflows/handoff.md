---
description: Chiude il turno su The Wheel Project / Antigravity 2.0 — voce in HANDOFF_LOG.md dal template, potatura se serve, rilascio del lock, commit
---

# Chiusura turno

Equivalente Antigravity di `/handoff` in Claude Code (`.claude/commands/handoff.md`). Stesso
contenuto, formato diverso: se una delle due versioni cambia, aggiorna anche l'altra o segnalalo
in `AGENTS.md` — non devono andare fuori sincrono.

1. [ ] Esegui `git status --short` prima di scrivere qualunque cosa. Se ci sono modifiche non
       committate che fanno parte di questo turno, valuta se committarle prima dell'handoff (di
       norma sì: un commit per il lavoro e uno separato per l'handoff/rilascio lock, come da
       convenzione già in uso in questo repo — vedi `HANDOFF_LOG.md` per esempi reali).
2. [ ] Leggi il blocco `LOCK` in `.ai/PROJECT_STATE.md`.
       - Se `owner` è `antigravity`: hai lavorato sotto lock, quindi questo turno deve finire con
         il lock rilasciato (`owner: NONE`, `since/task/scope/expires: null`).
       - Se `owner` è già `NONE`: non avevi preso il lock (probabile sessione di sola
         analisi/review). Scrivi comunque la voce di handoff — `AGENTS.md` lo richiede
         esplicitamente anche per un turno che non ha toccato codice.
3. [ ] Conta le voci esistenti in `.ai/HANDOFF_LOG.md` (righe che iniziano con `## [20`). Se
       aggiungendo la tua il conto supererebbe 10, sposta l'undicesima voce (la più in fondo,
       prima della sezione "Handoff più vecchi") in cima a `.ai/archive/HANDOFF_LOG_archive.md`,
       **verbatim**, non riassunta. Poi rimuovila da `HANDOFF_LOG.md`.
4. [ ] Scrivi la voce in cima a `.ai/HANDOFF_LOG.md`, usando il template già presente nel file
       (sezione "Template (copiare e compilare)"). Compilalo con informazioni vere e verificabili
       — `file:riga`, non descrizioni vaghe — guardando il diff reale di questo turno (`git diff`,
       `git log`), non solo quello che ricordi della conversazione.
5. [ ] Se avevi il lock, rilascialo nel blocco `LOCK` di `.ai/PROJECT_STATE.md`:
       ```yaml
       owner:      NONE
       since:      null
       task:       null
       scope:      null
       expires:    null
       ```
6. [ ] Se durante il turno hai cambiato un conteggio (test, punti aperti/chiusi), controlla che
       non resti scritto a mano altrove nello stesso file (es. il cappello di `PROJECT_STATE.md`
       cita spesso il conteggio test in prosa) — è il tipo di disallineamento che questo repo ha
       già pagato più volte.
7. [ ] Committa: un commit per il lavoro (se non già fatto) con prefisso `[antigravity]` e
       messaggio nello stile già usato nella history (`git log` per lo stile esatto), poi un
       commit separato per l'handoff/rilascio lock: `[antigravity] chore: release lock — <riassunto
       brevissimo>`. Non fare `git push` a meno che Andreas non lo chieda esplicitamente.
8. [ ] Riporta ad Andreas in due o tre righe: cosa è stato fatto, se main compila/i test passano
       (o perché no), qual è il prossimo passo secondo il campo "Per chi entra" appena scritto.
