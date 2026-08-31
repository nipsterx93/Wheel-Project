# Prompt di apertura per una nuova sessione

> Quando la finestra di contesto si satura e serve aprire una chat nuova: **incolla il blocco qui
> sotto** come primo messaggio. Vale per Claude, Codex/ChatGPT e Antigravity/Gemini — tutti hanno
> accesso a questo repository.
>
> Non serve altro. Il blocco dice all'agente cosa leggere e in che ordine; il contesto lo ricostruisce
> da solo dai file, che sono la memoria vera del progetto.

---

```
Sei un agente AI sul progetto The Wheel Project / Antigravity 2.0 (plugin SimHub, C#/.NET 4.8).
Questa è una sessione nuova: non hai memoria delle precedenti, ma il progetto sì — è nei file.

LEGGI IN QUESTO ORDINE, SENZA SALTARE:

1. CLAUDE.md
   Regole operative, protocollo del lock, trappole del repo (file LEGACY non compilati, .csproj
   senza glob, file enormi da leggere a fette). Include la sezione "Sessioni di revisione" se il
   tuo compito è rivedere invece di implementare.

2. .ai/PROJECT_STATE.md
   Parti dal blocco LOCK (se owner non è NONE e non sei tu, NON tocchi codice), poi la sezione
   "Da dove partire", poi la tabella dei punti Y-1…Y-30: è lo stato di ogni questione aperta e
   chiusa, con commit e numeri misurati.

3. .ai/plans/2026-08-30-formule-corrette-fine-gara.md
   LE FORMULE CORRETTE per la proiezione di fine gara, trascritte da una revisione esterna.
   ATTENZIONE: nel PDF originale (DeepSearch/) le equazioni sono IMMAGINI, non testo — se cerchi
   nel testo del PDF non le trovi. Qui sono trascritte. Leggi anche
   .ai/plans/2026-08-30-analisi-dahldesign.md (analisi di un plugin open source che risolve gli
   stessi problemi) e .ai/plans/2026-08-24-roadmap.md per il quadro generale.

4. .ai/ARCHITECTURE.md
   Mappa dei moduli e ADR. Leggi almeno ADR-004 (come si verifica un fix qui) e ADR-005 (perché
   un campione singolo non è una misura): senza, i punti Y sembrano difetti scollegati invece che
   uno schema ricorrente.

5. .ai/HANDOFF_LOG.md
   Le ultime 3-4 voci dall'alto. Cronologia dei turni, con "per chi entra" alla fine di ognuna.

POI DIMMI, PRIMA DI FARE QUALSIASI COSA:
- in che fase siamo secondo la roadmap, e qual è il prossimo passo concreto;
- se c'è una decisione che aspetta me (utente) invece che lavoro da fare;
- se qualcosa nei file ti sembra incoerente o obsoleto.

COME LAVORARE QUI:
- Prendi il lock prima di toccare codice, rilascialo a fine turno, un commit per turno con prefisso
  agente ([claude], [codex], [antigravity]).
- Ogni fix segue ADR-004: la decisione estratta in una funzione pura, il test che la chiama davvero,
  e la verifica che il test diventi rosso neutralizzando il fix. Non saltare l'ultimo passo — ha già
  intercettato un test che non copriva nulla (vedi commit cd97b4e).
- I casi di regressione usano numeri veri presi dai log, non valori inventati.
- A fine turno aggiorna .ai/HANDOFF_LOG.md e .ai/PROJECT_STATE.md. Anche una sessione di sola
  analisi lascia una voce.

COME PARLARMI:
Non leggo codice e non sono in grado di validare la matematica. Spiegami le conclusioni in italiano
comune, ancorate a cosa vedo io: un numero sulla dashboard, un annuncio vocale, un consiglio di
rifornimento. Se una cosa non si può spiegare senza codice, dimmi almeno cosa cambia per me e cosa
ti serve da me. Quello che posso fare è procurare i replay, raccontarti cosa è successo in pista, e
decidere cosa il plugin deve fare quando ci sono più opzioni sensate.

NOTA: la cartella Logs/ è in .gitignore. I log dei replay su cui poggiano molte conclusioni sono sul
mio disco, non nel repository. Se ti servono per verificare qualcosa, chiedimeli.
```

---

## Quando aprire una chat nuova

Non aspettare che il contesto si esaurisca del tutto: quando la sessione comincia a rallentare o a
dimenticare dettagli di poco prima, conviene **chiudere il turno per bene** (lock rilasciato, voce
in `HANDOFF_LOG.md`, tutto committato) e riaprire. Un turno chiuso male costa alla sessione
successiva più di quanto si guadagni tirando avanti.

Prima di chiudere, controlla:

```bash
git status --short
```

Se non è vuoto, c'è lavoro non committato che la prossima sessione non troverà.
