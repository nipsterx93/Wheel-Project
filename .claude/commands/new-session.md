---
description: Bootstrap di una sessione nuova su questo repository — legge i file giusti nell'ordine giusto e riporta lo stato, senza che l'utente debba incollare a mano il blocco di NEW_SESSION_PROMPT.md
argument-hint: [tema di lavoro in corso, opzionale — es. "proiezioni fine gara"]
---

Sei un agente AI su The Wheel Project / Antigravity 2.0 (plugin SimHub, C#/.NET 4.8). Questa
potrebbe essere una sessione nuova senza memoria delle precedenti: il contesto è nei file, non
nella chat. Se hai già letto questi file in questa stessa sessione, non rileggerli — usa quanto
già sai e vai diretto al report finale.

Leggi in quest'ordine, senza saltare nulla:

1. **`AGENTS.md`** — regole operative, protocollo del lock, trappole del repo (file `*_LEGACY.cs`
   non compilati, `.csproj` senza glob, file enormi da leggere a fette). Se il tuo compito in
   questa sessione è **rivedere** un lavoro invece di continuarlo, presta particolare attenzione
   alla sezione "Sessioni di revisione". Leggi anche la sezione "Protocollo di brainstorming e
   coworking fra agenti" se la sessione prevede più di un semplice fix.

2. **`.ai/PROJECT_STATE.md`** — parti dal blocco `LOCK` in cima: se `owner` non è `NONE` e non è
   il tuo, non puoi scrivere codice in `User.PluginSdkDemoEdit/` (c'è anche un hook che lo
   impedisce tecnicamente, ma va comunque rispettato nello spirito). Poi la sezione "Da dove
   partire", poi la tabella dei punti **aperti** — quella su cui eventualmente si lavora. I punti
   già chiusi sono un indice di una riga: il testo integrale sta in
   `.ai/archive/CLOSED_POINTS.md` e si apre solo se serve quel punto preciso.

3. **`.ai/HANDOFF_LOG.md`** — le prime 3-4 voci dall'alto. Cronologia dei turni recenti, con "per
   chi entra" alla fine di ognuna: è lì che sta scritto cosa NON toccare e a cosa fare attenzione.

4. **`.ai/STRATEGY_ENGINE_GUIDE.md`** — il quadro d'insieme di cosa fa il motore strategico, in
   parole povere. Utile soprattutto se il lavoro tocca undercut/overcut, carburante o proiezioni
   di fine gara.

$ARGUMENTS

Se è stato passato un tema sopra, cerca anche il piano più recente in `.ai/plans/` che lo riguarda
(i nomi file sono `<data>-<argomento>.md`) e leggilo prima di procedere.

Poi dimmi, prima di fare qualsiasi altra cosa:
- in che fase siamo secondo la roadmap (`.ai/plans/2026-08-24-roadmap.md` se serve il dettaglio), e
  qual è il prossimo passo concreto;
- se c'è una decisione che aspetta l'utente invece che lavoro da fare (guarda anche la tabella
  "Congelati in attesa di decisione" in `PROJECT_STATE.md`);
- se qualcosa nei file ti sembra incoerente, obsoleto, o un numero scritto a mano che potrebbe
  essere andato fuori sincrono (è già successo più volte in questo repo — vedi AGENTS.md, sezione
  "Tenere leggeri i file di stato").

Non toccare file di codice finché non hai finito questo report e l'utente non ha confermato la
direzione.
