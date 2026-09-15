---
description: Bootstrap di una sessione nuova su The Wheel Project / Antigravity 2.0 — legge i file giusti nell'ordine giusto e riporta lo stato, senza che Andreas debba incollare a mano il blocco di NEW_SESSION_PROMPT.md
---

# Bootstrap sessione

Equivalente Antigravity di `/new-session` in Claude Code (`.claude/commands/new-session.md`); la
stessa procedura è registrata anche come skill in `.agent/skills/new-session/SKILL.md`. Stesso
contenuto, formato diverso: se una delle tre versioni cambia, aggiorna anche le altre o segnalalo
in `AGENTS.md` — non devono andare fuori sincrono.

Sei un agente AI su The Wheel Project / Antigravity 2.0 (plugin SimHub, C#/.NET 4.8). Questa
potrebbe essere una sessione nuova senza memoria delle precedenti: il contesto è nei file, non
nella chat. Se hai già letto questi file in questa stessa sessione, non rileggerli — vai diretto
al report finale.

Nel repository convivono il plugin nuovo (`User.PluginSdkDemoRemastered/`, SimRIG Remastered, il
progetto attivo) e il plugin vecchio (`User.PluginSdkDemoEdit/`, congelato).

1. [ ] Leggi `AGENTS.md` per intero — regole operative, protocollo del lock, ciclo di un modulo del
       plugin nuovo, trappole del repo. Se il compito di questa sessione è **rivedere** un lavoro
       invece di continuarlo, presta particolare attenzione alla sezione "Sessioni di revisione".
       Leggi anche "Protocollo di brainstorming e coworking fra agenti" se la sessione prevede più
       di un semplice fix.
2. [ ] Leggi `.ai/PROJECT_STATE.md`, a partire dal blocco `LOCK` in cima: se `owner` non è `NONE`
       e non è `antigravity`, non scrivere codice in nessuno dei due plugin (Claude Code ha un hook
       che lo impedisce tecnicamente dal suo lato — da questo lato resta disciplina, quindi vale
       ancora di più rispettarla). Poi leggi la sezione "Da dove partire" e la tabella
       **Avanzamento** del plugin nuovo.
3. [ ] Leggi `.ai/plans/2026-09-15-remastered-spec.md` — il design approvato del plugin nuovo:
       decisioni, contratti dei moduli, ordine dei passi, soglie di validazione.
4. [ ] Leggi le prime 3-4 voci dall'alto di `.ai/HANDOFF_LOG.md` — cronologia dei turni recenti,
       con "per chi entra" alla fine di ognuna: lì sta scritto cosa NON toccare e a cosa fare
       attenzione.
5. [ ] Se Andreas ha indicato un tema di lavoro specifico nel messaggio con cui ti ha invocato,
       cerca il file più recente in `.ai/plans/` che lo riguarda (per un modulo del plugin nuovo:
       `<data>-remastered-<modulo>.md`) e leggilo prima di procedere. `.ai/STRATEGY_ENGINE_GUIDE.md`
       descrive il plugin vecchio: leggilo solo se il lavoro riguarda quello.
6. [ ] Riporta ad Andreas, prima di fare qualsiasi altra cosa:
       - a che passo siamo (tabella Avanzamento in `PROJECT_STATE.md`, ordine nello spec §6.1) e
         qual è il prossimo passo concreto;
       - se c'è una decisione che aspetta lui invece che lavoro da fare (sezione "Decisioni in
         sospeso" di `PROJECT_STATE.md` e spec §9);
       - se qualcosa nei file ti sembra incoerente, obsoleto, o un numero scritto a mano che
         potrebbe essere andato fuori sincrono (è già successo più volte in questo repo — vedi
         AGENTS.md, sezione "Tenere leggeri i file di stato").

Non toccare file di codice finché non hai finito questo report e Andreas non ha confermato la
direzione.
