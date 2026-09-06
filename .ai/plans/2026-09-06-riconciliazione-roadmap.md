# Riconciliare roadmap.md, PROJECT_STATE.md e Y-52 — un solo ordine di lavoro

- **Data:** 2026-09-06
- **Autore:** claude
- **Esecutore:** antigravity (su approvazione utente)
- **Stato:** ✅ Eseguito (2026-09-06)

## Obiettivo

Oggi esistono **due tracciati indipendenti** che rispondono entrambi alla domanda "cosa facciamo
adesso", e non si parlano:

1. `.ai/plans/2026-08-24-roadmap.md` — fasi di prodotto (A→D), ferma al 24/08. Dice che la
   prossima è la **Fase B** (verificare gli undercut/overcut contro un esito di gara reale), mai
   iniziata.
2. **Y-52** (metadati di sessione dallo YAML) — deciso il 05/09 in chat dopo un'analisi di un
   progetto esterno (irdashies), **mai inserito nella roadmap**. È il lavoro effettivamente in
   corso da due turni (passi 1-2 di 4 fatti).

Risultato: chi legge solo `roadmap.md` non sa che Y-52 esiste; chi legge solo `PROJECT_STATE.md`
non sa se Y-52 ha priorità sulla Fase B o la sospende. È la causa diretta della confusione
riportata dall'utente ("saltiamo da un Y all'altro senza un senso logico").

**Alla fine di questo piano deve essere vero:** un solo documento dice, senza ambiguità, qual è il
lavoro attivo adesso e cosa viene dopo — e una regola scritta impedisce che la prossima iniziativa
nata "in chat" (come Y-52) sparisca di nuovo dalla roadmap.

## Vincoli

- **Non cambia nessun comportamento del plugin.** Solo file `.md` in `.ai/`, zero `.cs` toccati.
- **Non decide da solo le domande di prodotto** (vedi "Domande aperte" sotto) — le propone,
  l'utente sceglie.
- **Non serve il lock**: nessun file di codice coinvolto (`AGENTS.md`, sezione "Sessioni di
  revisione" e il precedente della potatura del 2026-09-05, entrambi fatti senza lock).
- Non tocca il testo dei punti Y già chiusi in `archive/CLOSED_POINTS.md`.

## File coinvolti

- `.ai/plans/2026-08-24-roadmap.md` — inserire Y-52 come voce esplicita, aggiornare "Dove siamo,
  in una pagina" e la tabella delle fasi.
- `.ai/PROJECT_STATE.md` — riscrivere la sezione "📍 Stato corrente": oggi nomina solo
  "Milestone M0 — infrastruttura collaborazione multi-AI", che è vera ma parziale (non dice nulla
  del lavoro tecnico in corso). Deve rimandare a `roadmap.md` invece di mantenere una sua propria
  nozione di "fase" scollegata.
- `.ai/PROJECT_STATE.md`, tabella punti aperti — **eventuale** nuovo `Y-57` per il difetto del
  dump YAML trovato in questa sessione (`_metadataDumped = true` settato a prescindere dall'esito
  di `Write()`, vedi `TelemetryReader.cs:585`) — **solo dopo** che il riavvio pulito di SimHub in
  corso dà un esito (dump riuscito, o `Session Metadata Dump Failed` con l'eccezione reale). Non
  lo registro adesso perché non so ancora come si chiude.
- `AGENTS.md` — una riga nuova nella sezione "Come voglio le consegne" o nel protocollo di
  brainstorming: quando una decisione in chat cambia l'ordine di lavoro, lo stesso turno aggiorna
  `roadmap.md`, non solo `HANDOFF_LOG.md`.

## Passi

1. **Aspettare l'esito del riavvio pulito di SimHub** (in corso mentre scrivo questo piano). Serve
   per sapere se il punto sul dump YAML va registrato come difetto attivo o si chiude da solo.
2. **Decidere le due domande aperte sotto**, con l'utente.
3. **Riscrivere `roadmap.md`**:
   - Nella tabella "Cosa non sappiamo ancora" / sezione fasi, inserire una voce esplicita per
     Y-52 con il suo stato reale (passi 1-2 fatti, 3-4 da fare) e la sua posizione decisa al
     punto 2.
   - Aggiungere una riga in cima al file ("Ultimo aggiornamento: 2026-09-06") così la prossima
     volta che sparisce per due settimane si vede a colpo d'occhio.
4. **Riscrivere la sezione "📍 Stato corrente" di `PROJECT_STATE.md`**: sostituire la tabella
   "Milestone" (M0/M1, che non riflette il lavoro reale da settimane) con un rimando diretto a
   "fase attuale della roadmap: vedi `roadmap.md`" + una riga sola su cosa si sta facendo *ora*
   nel dettaglio tecnico (oggi: Y-52 passo 3/4). Non cancellare la storia di M0: sposta la riga
   "M0 fatto" nell'indice, come già si fa per i punti Y chiusi.
5. **Se il riavvio SimHub conferma il difetto**, aggiungere `Y-57` alla tabella dei punti aperti
   con il numero di riga e la spiegazione già scritta in questa sessione (il messaggio precedente
   all'utente ha già il testo pronto, basta trasferirlo).
6. **Aggiungere la regola di procedura** in `AGENTS.md` (vedi Domande aperte, punto 2) se
   l'utente la conferma.
7. **Un solo commit** `[claude] chore: riconciliazione roadmap/stato corrente + eventuale Y-57`
   (o due, se il punto 5 arriva in un momento diverso dal resto).
8. **Voce in `HANDOFF_LOG.md`** che riassume la riconciliazione e link a questo piano.

## Verifica

Nessun build/test: solo documentazione. Il criterio è leggibile a occhio:

```bash
grep -n "Y-52" ".ai/plans/2026-08-24-roadmap.md"
```
Atteso: almeno una riga trovata (oggi: zero).

```bash
grep -n "roadmap.md" ".ai/PROJECT_STATE.md"
```
Atteso: la sezione "Stato corrente" rimanda esplicitamente al file, invece di ripetere una fase
propria.

## Rischi

- **Duplicare invece di unificare**: se `roadmap.md` e "Stato corrente" continuano a dire cose
  leggermente diverse sulla fase attuale, il problema si ripresenta identico fra due settimane.
  Per questo il passo 4 non copia il testo della fase, **rimanda** al file che la contiene — una
  fonte sola, come già deciso per le regole in `AGENTS.md` (ADR-006).
- **Il punto 2 (Y-52 relazione con Fase B) è una decisione di prodotto**, non tecnica: se la
  eseguo senza risposta dell'utente, rifaccio lo stesso errore che sto correggendo (un agente che
  decide l'ordine di lavoro senza che sia scritto che l'ha deciso l'utente).

## Domande aperte

1. **Y-52 sospende la Fase B, o corrono in parallelo?** La Fase B (verificare undercut/overcut
   contro un esito di gara reale) serve un replay dove *tu* ricordi cosa è successo in pista — è
   un collo di bottiglia sul procurare quel replay, non sul tempo di sviluppo. Y-52 passo 3/4 è
   lavoro di codice puro, nessun replay speciale richiesto. **Non sono necessariamente in
   conflitto** — potrebbero avanzare insieme. Propongo: Y-52 resta il lavoro tecnico attivo finché
   non arriva il replay giusto per la Fase B; quando arriva, la Fase B ha la precedenza (è "il vero
   cuore del progetto", per usare le parole della roadmap stessa). Confermi, o preferisci un altro
   ordine?
2. **Regola procedurale**: ogni volta che in chat si decide un lavoro che cambia l'ordine (come
   Y-52 il 05/09), lo stesso turno tocca anche `roadmap.md`, non solo `HANDOFF_LOG.md`. Va bene
   come regola da scrivere in `AGENTS.md`, o preferisci un meccanismo diverso (es. un'unica
   sezione "cosa sto facendo ora" in cima a `PROJECT_STATE.md`, aggiornata a ogni turno, e
   `roadmap.md` resta un documento a bassa frequenza)?
3. **Milestone M0/M1 in `PROJECT_STATE.md`**: la ritiriamo del tutto (sposto "M0 fatto" nell'indice
   dei chiusi) in favore del solo rimando a `roadmap.md`, o la teniamo come traccia separata per
   l'asse "infrastruttura di collaborazione fra agenti" (che è concettualmente diverso dalle fasi
   A-D, le quali parlano solo del comportamento del plugin)?
