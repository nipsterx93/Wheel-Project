# GEMINI.md — The Wheel Project / Antigravity 2.0

**Le regole di questo progetto sono in [`AGENTS.md`](AGENTS.md). Leggilo adesso, per intero,
prima di qualsiasi altra cosa.**

Questo file non ne contiene di proprie ed è deliberatamente corto: le regole valgono uguali per
ogni agente (Claude Code, Gemini/Antigravity, Codex), quindi stanno in un solo posto e si
aggiornano in un solo posto. Un duplicato andrebbe fuori sincrono — in questo repo è già successo
con un conteggio di test ricopiato a mano.

In due righe, quello che non puoi sbagliare:

1. **Prima di scrivere codice**, leggi il blocco `LOCK` in `.ai/PROJECT_STATE.md`. Se `owner` non è
   `NONE` e non è il tuo, **non scrivi codice**: puoi leggere, analizzare, proporre un piano.
2. **A fine turno**: voce in `.ai/HANDOFF_LOG.md`, lock rilasciato, commit con prefisso
   `[antigravity]` (il nome storico di questo agente nel repo — usa sempre lo stesso, così si
   distingue chi ha verificato cosa).

Tutto il resto — build, test, trappole del repo, convenzioni, protocollo di review — è in
[`AGENTS.md`](AGENTS.md).
