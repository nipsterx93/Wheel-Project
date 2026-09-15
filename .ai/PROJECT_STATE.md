# PROJECT STATE — The Wheel Project / Antigravity 2.0

> Fonte di verità sullo stato corrente e sul **turno di scrittura**.
> Ogni agente legge questo file **prima** di toccare il codice e lo aggiorna **prima** di iniziare a scrivere.

---

## 🔒 LOCK

```yaml
owner:      claude
since:      2026-09-15 16:36
task:       Riorganizzazione dei file di progetto (spec §8.5), ADR-007, hook esteso alla cartella nuova
scope:      AGENTS.md; .ai/ (PROJECT_STATE, ARCHITECTURE, HANDOFF_LOG, archive, plans, STRATEGY_ENGINE_GUIDE, NEW_SESSION_PROMPT); .claude/hooks/check-lock.js; .claude/commands/new-session.md; .agent/workflows/new-session.md; .agent/skills/new-session/SKILL.md
expires:    2026-09-15 19:36
```

**Regole del lock**

1. `owner: NONE` → chiunque può prendere il turno: aggiorna questo blocco, committa, poi lavora.
2. `owner: <altro>` → **non modificare nessun file di codice**. Puoi leggere, analizzare, proporre piani in `.ai/plans/`, commentare. Nient'altro.
3. Il lock si prende e si rilascia con un commit dedicato, così il passaggio è tracciato nella history:
   - prendere: `[<agente>] chore: acquire lock — <task>`
   - rilasciare: `[<agente>] chore: release lock`
4. `scope` è vincolante: se durante il lavoro serve toccare file fuori scope, si allarga il lock esplicitamente (nuovo commit) invece di sconfinare in silenzio.
5. Lock stantio: se `expires` è passata, un altro agente può forzare il rilascio annotandolo in `HANDOFF_LOG.md`.
6. Il lock è **disciplina, non tecnologia**. La vera rete di sicurezza è Git: commit piccoli e frequenti, così ogni sovrascrittura accidentale è diffabile e reversibile.

---

## 📋 Da dove partire, per chi arriva adesso

**Dal 2026-09-15 si costruisce un plugin nuovo**, SimRIG Remastered, in `User.PluginSdkDemoRemastered/`, un modulo
alla volta (ADR-007). Il plugin vecchio, `User.PluginSdkDemoEdit/`, è **congelato**: serve la dash finché il nuovo non
lo sostituisce e fa da riferimento per l'analisi dei moduli.

**In che ordine leggere:**

1. `AGENTS.md` — regole, compreso il ciclo di un modulo.
2. `.ai/plans/2026-09-15-remastered-spec.md` — il design approvato: decisioni, contratti, ordine dei passi, soglie.
3. `.ai/ARCHITECTURE.md` — mappa dei moduli del plugin nuovo e ADR, in particolare ADR-004, ADR-005 e ADR-007.
4. La tabella **Avanzamento** qui sotto e le prime voci di `.ai/HANDOFF_LOG.md`.

**Dove sta la storia.** Niente è stato riassunto né cancellato: è stato spostato, e si apre solo quando serve.

| Serve… | Sta in |
|---|---|
| punti aperti, indice dei chiusi, stato e debiti del plugin vecchio fino al 2026-09-14 | `.ai/archive/PLUGIN_VECCHIO.md` |
| il ragionamento completo di un punto chiuso, i numeri misurati, il commit | `.ai/archive/CLOSED_POINTS.md` |
| un handoff più vecchio dei 10 tenuti | `.ai/archive/HANDOFF_LOG_archive.md` |
| la roadmap del 2026-08-24 | `.ai/archive/2026-08-24-roadmap.md` |
| i log dei replay e gli snapshot del database di calibrazione | `Logs/` (gitignored, solo sulla macchina di Andreas) e `.ai/db-snapshots/` |

---

## 📍 Avanzamento del plugin nuovo

Ordine e contenuto dei passi: spec §6.1. Una riga cambia stato solo con un handoff che lo dimostra.

| Passo | Lavoro | Stato | Commit |
|---|---|---|---|
| — | riorganizzazione dei file di progetto, ADR-007, hook esteso alla cartella nuova | ✅ fatto il 2026-09-15 | handoff del 2026-09-15 17:01 |
| — | interruttore che spegne undercut e overcut nel plugin vecchio (spec §7.2) | ⏳ prossimo | |
| 0 | prova di fattibilità (spec §7.4) | da fare | |
| 1 | `Input` + `Cars` | da fare | |
| 2 | `Track` + `Events` | da fare | |
| 3 | `Timings` | da fare | |
| 4 | `Gaps` + `Target` | da fare | |
| 5 | `Fuel` | da fare | |
| 6 | `Pace` / `TyreDeg` | da fare | |
| 7 | `Calibration` + `PitLoss` | da fare | |
| 8 | `Race` | da fare | |
| 9 | `MergeGap` | da fare | |
| 10 | passaggio della dash (spec §7.5) | da fare | |

---

## 🐞 Punti aperti del plugin nuovo

Un difetto trovato nel plugin nuovo si registra qui, con la numerazione `Y-NN` che continua da Y-63 (vedi `AGENTS.md`,
"Sessioni di revisione"). Al 2026-09-15 non ce ne sono.

| ID | Modulo | Punto | Decisione o stato |
|----|--------|-------|-------------------|

---

## 🧊 Plugin vecchio (congelato)

Accetta solo guasti bloccanti e l'interruttore che spegne undercut e overcut (spec §7.2); build e test in `AGENTS.md`. I
suoi punti aperti non si correggono più lì: ognuno è materiale per il brainstorming del modulo nuovo che lo riguarda.

| Punto aperto del plugin vecchio | Serve al brainstorming di |
|---|---|
| Y-52 metadati di sessione dallo YAML | `Input` |
| Y-58 buchi di dati del leader | `Cars`, `Race` |
| Y-33 ingresso box segnalato a ogni giro in pista | `Events` |
| Y-60 stazionario degli avversari dedotto dalla finestra `NotInWorld` | `Timings` |
| Y-62 traffico a metà giro | `Gaps` |
| Y-61 perdita ai box: zona estesa, consumo del Target, AccDec degli avversari | `Fuel`, `PitLoss` |
| Y-14 tempo gomme senza scope; Y-26 e Y-29 calibrazioni senza consenso | `Calibration`, `PitLoss` |
| Y-40 deriva del passo del leader | `Pace`, `Race` |
| Y-15 carburante consigliato contro versato; Y-36 e Y-38 totale giri e identità del leader | `Race` |
| Y-59 latch del MergeGap | `MergeGap` |
| Y-54 test saltati in silenzio | runner dei test (spec §5.7) |

Y-55 (`CustomDialog.xaml.cs`) riguarda solo il plugin vecchio; Y-56 è fra le decisioni in sospeso. Il testo completo di
ogni punto è in `.ai/archive/PLUGIN_VECCHIO.md`.

---

## 🚧 Decisioni in sospeso

| Punto | Chi decide |
|---|---|
| Y-56: un lock non pushato non serializza niente, e `human` non dice quale umano | Andreas e Michael |
| Domande aperte del plugin nuovo (spec §9) | nel brainstorming dei moduli e nella prova di fattibilità |
