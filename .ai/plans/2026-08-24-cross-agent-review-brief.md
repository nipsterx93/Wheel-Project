# Prompt di revisione incrociata — Y-16…Y-25, sistema di calibrazione

> Testo pronto da incollare in una nuova chat (Codex/ChatGPT, Gemini/Antigravity, o un'altra
> istanza Claude) con accesso a questo repository. Scritto in seconda persona rivolta all'agente
> che lo riceve.

---

Sei un agente AI con accesso al repository `The Wheel Project / Antigravity 2.0` (plugin SimHub,
C# / .NET Framework 4.8). Un'altra AI (Claude) ha lavorato su questo progetto fra il 18 e il 24
agosto 2026, in una serie di sessioni concatenate, producendo 61 commit. Il tuo compito è una
**revisione indipendente** di quel lavoro — non continuarlo, non correggerlo di nascosto: verificarlo.

## Prima di tutto

Leggi in quest'ordine, senza saltare nulla:

1. `CLAUDE.md` — in particolare la sezione **"Sessioni di revisione"**: descrive esattamente come
   comportarti in questo turno, incluso cosa fare se trovi un problema.
2. `.ai/PROJECT_STATE.md` — la sezione **"Da dove partire"** in cima, poi la tabella dei punti `Y-1`
   … `Y-25`. Ogni riga ha il commit, i numeri misurati e il *perché* della decisione.
3. `.ai/ARCHITECTURE.md` — la mappa dei moduli e, soprattutto, **ADR-004** e **ADR-005**. Sono il
   criterio con cui ogni fix in questa serie è stato verificato: leggili per intero prima di
   giudicare qualunque commit, altrimenti rischi di segnalare come mancante qualcosa che è già
   coperto da quel criterio (o viceversa, di accettare qualcosa che non lo rispetta davvero).
4. `git log --oneline f526cb3..HEAD` seguito da `git log -p <hash>` sui commit che ti interessano
   di più — i messaggi sono scritti per contenere il ragionamento e i numeri, non solo il diff.

## Limite che devi conoscere prima di iniziare

`Logs/` è in `.gitignore`. I replay del simulatore su cui si basano molte delle conclusioni
registrate (numeri come "56.409 → 90.682", "0.65 m di dispersione", "231 tick su 534") **non sono
nel repository** — esistono solo sulla macchina dell'utente. Se non hai accesso a quel filesystem:
dillo esplicitamente nelle tue conclusioni invece di dare per buono un numero che non puoi
verificare tu stesso, e segnala all'utente quali file di log ti servirebbero per confermare un
claim specifico. Non è un motivo per saltare la review — la correttezza *logica* del codice e dei
test è verificabile comunque, ed è la parte più importante.

## Cosa voglio che tu verifichi

Non un'esecuzione meccanica dei test (fallo comunque, vedi sotto, ma non è il punto). Voglio un
giudizio tecnico su tre livelli:

### 1. I test dimostrano quello che dicono di dimostrare?

Il criterio dichiarato (ADR-004) è: ogni fix ha un test che, neutralizzando il fix, diventa rosso.
Scegli almeno 4-5 commit di correzione (cerca `[claude] fix:` nel log) e per ciascuno:
- individua il test di regressione associato;
- verifica **tu stesso** che chiami davvero il codice di produzione e non ne riproduca la logica
  internamente (è successo una volta, in Y-25 — vedi il commit `cd97b4e` per la descrizione
  dell'errore e come è stato corretto: è un buon esempio di cosa cercare);
- se hai modo di eseguire la build, prova a commentare/invertire tu stesso la condizione del guard
  e conferma che il test fallisce. Se il repository richiede `$SIMHUB_INSTALL_PATH` che non hai,
  documenta che non hai potuto eseguire la build e valuta il test staticamente.

### 2. Il pattern di ADR-005 è applicato in modo consistente, e ci sono altri punti dove manca?

ADR-005 descrive un problema ricorrente: dati calibrati scritti da un campione singolo non
verificato, cristallizzati per sempre (Y-20, Y-21, Y-23). `CalibrationConsensus.cs` e i guard di
plausibilità (`RaceTimeProjection`, `PitRadar.MinimumCredibleTransitSec`) sono il rimedio adottato.

Cerca nel codice **altri campi persistenti** con lo stesso rischio non ancora coperti — pattern da
cercare: `== 0.0` come condizione di scrittura, `> 0.5` o soglie temporali brevi come unico filtro,
assegnamenti diretti senza passare da un consenso o una confidenza. `PitRadar.cs` e
`OpponentTracker.cs` sono i file più densi di questo genere di logica. Non serve che li corregga tu
in questo turno — serve che li **trovi e li registri**, se ci sono.

### 3. Le correzioni hanno effetti collaterali non testati?

In particolare:
- **Y-16** ha tolto il tetto `Math.Min(projectedPlayerTotal, _latchedLeaderTotalLaps)` in
  multiclasse. È corretto per i casi osservati (Misano monoclasse, Daytona multiclasse, sempre
  26 giri). Esiste uno scenario — doppiaggio, leader che si ritira, sessione che passa da
  multiclasse a monoclasse a metà gara — dove la sua assenza produce un numero assurdo invece di
  uno leggermente impreciso?
- **Y-24/Y-25** tengono l'ultimo valore credibile del leader quando il campione è vuoto. Cosa
  succede se il leader **esce di sessione per davvero** (si ritira, disconnette) invece di avere
  solo un tick vuoto? Il valore tenuto resterebbe congelato per sempre — è un comportamento voluto
  o un nuovo debito da registrare?
- **Y-23** scarta l'intera visita in corsia box se lo spostamento è sotto `0.01` di giro. Esiste un
  circuito reale con una corsia box più corta di quella soglia (~42-57 m nei casi osservati) dove
  questo scarterebbe un transito vero?

Non devi risolvere questi dubbi da solo se richiedono dati che non hai — formulali come domande
verificabili nel tuo output, con l'esperimento che le chiuderebbe.

## Cosa NON fare

- Non correggere silenziosamente nulla che trovi. Se è abbastanza chiaro e circoscritto da
  sistemare subito, segui il protocollo normale del lock in `CLAUDE.md` — prendilo esplicitamente,
  un commit, rilascialo. Se preferisci solo segnalarlo, registralo e basta.
- Non fidarti di un numero citato in un messaggio di commit senza controllare almeno un campione a
  mano, se hai i mezzi per farlo (codice, o log se disponibili).
- Non riscrivere `PROJECT_STATE.md`/`ARCHITECTURE.md` da zero: sono vivi, li leggeranno altre
  sessioni dopo la tua. Aggiungi, non sostituire.

## Come consegnare

Una voce in `.ai/HANDOFF_LOG.md`, in cima, col tuo prefisso agente, che copra:
- cosa hai verificato e con quale metodo (letto codice / eseguito build / eseguito test / ispezionato log — specifica quali di questi non hai potuto fare);
- l'esito dei tre punti sopra, anche se è "nessun problema trovato" — è un esito legittimo e va scritto lo stesso;
- ogni nuovo punto aperto, con lo stesso stile delle voci `Y-NN` già presenti (dato misurato, non impressione).

Se hai corretto qualcosa: build 0 errori, test suite verde (152 PASS all'inizio di questo turno —
se il numero è diverso quando parti, qualcuno ha lavorato nel frattempo, aggiornati da
`PROJECT_STATE.md`), regressione verificata neutralizzando il fix come da ADR-004.
