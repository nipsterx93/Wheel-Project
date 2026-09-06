---
description: Bootstrap di una sessione nuova su The Wheel Project / Antigravity 2.0 — legge i file giusti nell'ordine giusto e riporta lo stato, senza che Andreas debba incollare a mano il blocco di NEW_SESSION_PROMPT.md
---

# Bootstrap sessione

Equivalente Antigravity di `/new-session` in Claude Code (`.claude/commands/new-session.md`).
Stesso contenuto, formato diverso: se una delle due versioni cambia, aggiorna anche l'altra o
segnalalo in `AGENTS.md` — non devono andare fuori sincrono.

Sei un agente AI su The Wheel Project / Antigravity 2.0 (plugin SimHub, C#/.NET 4.8). Questa
potrebbe essere una sessione nuova senza memoria delle precedenti: il contesto è nei file, non
nella chat. Se hai già letto questi file in questa stessa sessione, non rileggerli — vai diretto
al report finale.

1. [ ] Leggi `AGENTS.md` per intero — regole operative, protocollo del lock, trappole del repo
       (file `*_LEGACY.cs` non compilati, `.csproj` senza glob, file enormi da leggere a fette).
       Se il compito di questa sessione è **rivedere** un lavoro invece di continuarlo, presta
       particolare attenzione alla sezione "Sessioni di revisione". Leggi anche "Protocollo di
       brainstorming e coworking fra agenti" se la sessione prevede più di un semplice fix.
2. [ ] Leggi `.ai/PROJECT_STATE.md`, a partire dal blocco `LOCK` in cima: se `owner` non è `NONE`
       e non è `antigravity`, non scrivere codice in `User.PluginSdkDemoEdit/` (Claude Code ha un
       hook che lo impedisce tecnicamente dal suo lato — da questo lato resta disciplina, quindi
       vale ancora di più rispettarla). Poi leggi la sezione "Da dove partire", poi la tabella dei
       punti **aperti**. I punti già chiusi sono un indice di una riga: il testo integrale sta in
       `.ai/archive/CLOSED_POINTS.md`, apri quel file solo se ti serve il ragionamento di un punto
       preciso.
3. [ ] Leggi le prime 3-4 voci dall'alto di `.ai/HANDOFF_LOG.md` — cronologia dei turni recenti,
       con "per chi entra" alla fine di ognuna: lì sta scritto cosa NON toccare e a cosa fare
       attenzione.
4. [ ] Leggi `.ai/STRATEGY_ENGINE_GUIDE.md` — il quadro d'insieme di cosa fa il motore strategico,
       in parole povere. Utile soprattutto se il lavoro tocca undercut/overcut, carburante o
       proiezioni di fine gara.
5. [ ] Se Andreas ha indicato un tema di lavoro specifico nel messaggio con cui ti ha invocato,
       cerca il piano più recente in `.ai/plans/` che lo riguarda (i nomi file sono
       `<data>-<argomento>.md`) e leggilo prima di procedere.
6. [ ] Riporta ad Andreas, prima di fare qualsiasi altra cosa:
       - in che fase siamo secondo la roadmap (`.ai/plans/2026-08-24-roadmap.md` per il dettaglio),
         e qual è il prossimo passo concreto;
       - se c'è una decisione che aspetta lui invece che lavoro da fare (guarda anche la tabella
         "Congelati in attesa di decisione" in `PROJECT_STATE.md`);
       - se qualcosa nei file ti sembra incoerente, obsoleto, o un numero scritto a mano che
         potrebbe essere andato fuori sincrono (è già successo più volte in questo repo — vedi
         AGENTS.md, sezione "Tenere leggeri i file di stato").

Non toccare file di codice finché non hai finito questo report e Andreas non ha confermato la
direzione.
