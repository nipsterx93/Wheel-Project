---
description: Bootstrap di una sessione nuova su questo repository — legge i file giusti nell'ordine giusto e riporta lo stato, senza che l'utente debba incollare a mano il blocco di NEW_SESSION_PROMPT.md
argument-hint: [tema di lavoro in corso, opzionale — es. "brainstorming del modulo Timings"]
---

Equivalente Claude Code di `/new-session` in Antigravity (`.agent/workflows/new-session.md` e
`.agent/skills/new-session/SKILL.md`). Stesso contenuto, formato diverso: se una delle tre versioni
cambia, aggiorna anche le altre o segnalalo in `AGENTS.md` — non devono andare fuori sincrono.

Sei un agente AI su The Wheel Project / Antigravity 2.0 (plugin SimHub, C#/.NET 4.8). Questa
potrebbe essere una sessione nuova senza memoria delle precedenti: il contesto è nei file, non
nella chat. Se hai già letto questi file in questa stessa sessione, non rileggerli — usa quanto
già sai e vai diretto al report finale.

Nel repository convivono il plugin nuovo (`User.PluginSdkDemoRemastered/`, SimRIG Remastered, il
progetto attivo) e il plugin vecchio (`User.PluginSdkDemoEdit/`, congelato).

Leggi in quest'ordine, senza saltare nulla:

1. **`AGENTS.md`** — regole operative, protocollo del lock, ciclo di un modulo del plugin nuovo,
   trappole del repo. Se il tuo compito in questa sessione è **rivedere** un lavoro invece di
   continuarlo, presta particolare attenzione alla sezione "Sessioni di revisione". Leggi anche la
   sezione "Protocollo di brainstorming e coworking fra agenti" se la sessione prevede più di un
   semplice fix.

2. **`.ai/PROJECT_STATE.md`** — parti dal blocco `LOCK` in cima: se `owner` non è `NONE` e non è
   il tuo, non puoi scrivere codice in nessuno dei due plugin (c'è anche un hook che lo impedisce
   tecnicamente, ma va comunque rispettato nello spirito). Poi la sezione "Da dove partire" e la
   tabella **Avanzamento** del plugin nuovo.

3. **`.ai/plans/2026-09-15-remastered-spec.md`** — il design approvato del plugin nuovo: decisioni,
   contratti dei moduli, ordine dei passi, soglie di validazione.

4. **`.ai/HANDOFF_LOG.md`** — le prime 3-4 voci dall'alto. Cronologia dei turni recenti, con "per
   chi entra" alla fine di ognuna: è lì che sta scritto cosa NON toccare e a cosa fare attenzione.

`.ai/STRATEGY_ENGINE_GUIDE.md` descrive il plugin vecchio: leggilo solo se il lavoro riguarda quello.

$ARGUMENTS

Se è stato passato un tema sopra, cerca anche il file più recente in `.ai/plans/` che lo riguarda
(per un modulo del plugin nuovo: `<data>-remastered-<modulo>.md`) e leggilo prima di procedere.

Poi dimmi, prima di fare qualsiasi altra cosa:
- a che passo siamo (tabella Avanzamento in `PROJECT_STATE.md`, ordine nello spec §6.1) e qual è il
  prossimo passo concreto;
- se c'è una decisione che aspetta l'utente invece che lavoro da fare (sezione "Decisioni in
  sospeso" di `PROJECT_STATE.md` e spec §9);
- se qualcosa nei file ti sembra incoerente, obsoleto, o un numero scritto a mano che potrebbe
  essere andato fuori sincrono (è già successo più volte in questo repo — vedi AGENTS.md, sezione
  "Tenere leggeri i file di stato").

Non toccare file di codice finché non hai finito questo report e l'utente non ha confermato la
direzione.
