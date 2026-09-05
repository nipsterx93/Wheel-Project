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

1. AGENTS.md
   Regole operative, protocollo del lock, trappole del repo (file LEGACY non compilati, .csproj
   senza glob, file enormi da leggere a fette). Include la sezione "Sessioni di revisione" se il
   tuo compito è rivedere invece di implementare.
   Valgono per ogni agente: CLAUDE.md e GEMINI.md sono solo puntatori a questo file.

2. .ai/PROJECT_STATE.md
   Parti dal blocco LOCK (se owner non è NONE e non sei tu, NON tocchi codice), poi la sezione
   "Da dove partire", poi la tabella dei punti APERTI, con commit e numeri misurati.
   I punti già chiusi sono un indice di una riga ciascuno: il testo integrale sta in
   .ai/archive/CLOSED_POINTS.md e si apre solo quando serve quel punto preciso.

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

## Blocco per il lavoro in corso — proiezioni di fine gara (aggiornato 2026-08-31)

> Da incollare **dopo** il blocco generico, quando si riprende il filone su cui si sta lavorando
> adesso. Il blocco sopra ricostruisce il contesto del progetto; questo dice a che punto siamo su
> questo lavoro specifico e cosa fare.

```
LAVORO IN CORSO: la proiezione dei giri di fine gara (quanti giri completeremo io e il leader
assoluto quando esce la bandiera). È il numero da cui dipende quanto carburante imbarcare:
sbagliarlo di un giro significa sbagliare il rifornimento di ~2.3 litri.

LEGGI QUESTI, IN QUEST'ORDINE, PRIMA DI PROPORRE QUALSIASI COSA:

1. .ai/HANDOFF_LOG.md — la prima voce (2026-08-31). Contiene i numeri misurati, le trappole in cui
   sono già cascato, e l'ordine di lavoro concordato con me.

2. .ai/plans/2026-08-30-formule-corrette-fine-gara.md — LE FORMULE CORRETTE, da una revisione
   esterna. ATTENZIONE: nel PDF originale (DeepSearch/) le equazioni sono IMMAGINI, non testo: se
   cerchi nel testo del PDF non le trovi. Lì sono trascritte.

3. .ai/plans/2026-08-30-analisi-dahldesign.md — analisi di un plugin open source (Andreas Dahl,
   in "Solo per analisi logiche/") che risolve gli stessi problemi. Utile perché conferma lo
   scheletro e mostra soluzioni più semplici delle nostre su alcune parti.

4. .ai/PROJECT_STATE.md — i punti Y-31 … Y-44. Quelli aperti sono Y-13, Y-14, Y-15, Y-26, Y-29,
   Y-33, Y-34, Y-36, Y-38, Y-40, Y-44.

DOVE SIAMO, IN BREVE

Sette correzioni fatte (Y-31, Y-32, Y-35, Y-39, Y-41, Y-42, Y-43). Le proiezioni ora sono vicine al
software di riferimento che uso in pista, in alcuni tick identiche al secondo decimale:

  LeaderProjectedPosAtCheckered   38.0-39.0 tutta la gara    (riferimento: 38.8)
  LeaderRaceTotalLaps             39 per quasi tutta la gara  (riferimento: 39)
  ProjectedPosAtCheckered         34.83-34.91 nell'ultimo terzo (valore reale: 34.83)
  RaceTotalLaps finale            35                          (giri realmente completati: 35)

Resta un difetto visibile: a metà gara RaceTotalLaps sale a 37 per tre giri, poi torna a 35.
Causa già isolata: CINQUE tick su 795 in cui il passo stimato del leader schizza a 278 secondi
invece di ~69, e l'isteresi asimmetrica poi tiene il picco.

COSA RESTA DA FARE, IN ORDINE (concordato)

  3. Δt_cur dal timestamp dell'ultimo passaggio, non dalla posizione campionata
  4. Bandiera = minimo su tutta la classe veloce, in modalità ombra (solo a log, non usata)
  5. Filtro di Hampel + MAD + change-point al posto della finestra percentuale
  6. Filtro Alpha-Beta al posto dell'isteresi asimmetrica
  7. Base Pace + reiniezione della massa carburante giro per giro
  Y-44. Il valore del tempo di sosta è sovrastimato del 49% (0.79 giri contro 0.53 reali)

ORDINE CONSIGLIATO: fare 4 e 6 per primi, gli altri dopo.

Il 6 (Alpha-Beta) perché l'outlier del passo leader continuerà a esistere finché non si affronta
Y-38, ma un filtro che sa scendere lo ASSORBE invece di amplificarlo per tre giri. Non dipende da
nessun'altra correzione.

Il 4 perché risolve la causa invece del sintomo, e va capito bene prima di implementarlo:

- OGGI proiettiamo UNA SOLA vettura: quella che in questo istante è P1 assoluto. Non calcoliamo la
  proiezione per tutte le vetture di nessuna classe. Il punto 4 è ancora tutto da fare.
- Il criterio corretto è il MINIMO DEL TEMPO DI ATTRAVERSAMENTO, cioè chi taglierà per primo dopo
  lo scadere del cronometro — non "la posizione proiettata più alta". Quasi coincidono, ma due
  vetture con passo diverso possono invertirsi.
- Il nostro RaceTotalLaps NON dipende dalle altre classi: dipende solo da quando esce la bandiera
  (che dipende solo dal leader assoluto) e dal nostro passo. Un cambio di posizioni fra GT3 non
  tocca il nostro numero.
- Il caso che ci fa male è il cambio di leader assoluto per una SOSTA: oggi cambiamo di colpo
  vettura di riferimento e la proiezione salta. Col minimo, se il leader attuale ha ancora una
  sosta da fare il suo tempo di sosta sposta avanti il suo attraversamento e la vettura dietro che
  ha già finito le soste diventa il minimo IN MODO CONTINUO, prima del sorpasso fisico.
- RIFINITURA rispetto al report: estendere il minimo a TUTTE le vetture, non solo alla classe più
  veloce. Costa quasi nulla ed è più robusto quando la classe veloce non è davvero davanti (inizio
  gara, o finestra in cui tutti i prototipi sono ai box insieme). Chi taglia per primo dopo lo
  scadere È il leader per definizione, qualunque classe abbia.

COSA NON ABBIAMO, E NON SERVE PER IL CARBURANTE: nessun leader DI CLASSE, da nessuna parte. È la
stessa lacuna che aveva bloccato Y-19 (PositionInClass non è usato in nessun punto del progetto, e
le convenzioni di conteggio giri degli avversari non sono verificate). Servirà solo se un giorno
vorremo mostrare la posizione finale di classe.

TRE TRAPPOLE, TUTTE GIÀ COSTATE TEMPO

- Nei log, dal commit 9d16172 ogni riga RaceProjectionsDiagnostics contiene DUE campi PosAtFlag
  (Player e leader). Un grep ingenuo li somma e produce statistiche inventate. Usare:
  sed -E 's/.*Player:[^|]*PosAtFlag=([0-9.]+).*/\1/'

- Il totale converge SEMPRE al valore giusto a fine gara, perché la parte proiettata si riduce a
  zero. "Il numero finale è corretto" non è mai una prova che il calcolo sia giusto.

- Leggere valori da un log senza controllare a quale SESSIONE appartengono (pre-gara, qualifica,
  gara) porta a conclusioni sbagliate di un ordine di grandezza. È già successo due volte.

NON TOCCARE

- La formula TimeUntilLeaderCheckered: è identica a quella della revisione esterna E a quella di
  DahlDesign. Tre fonti indipendenti concordi. Non è lì il problema.
- Il coefficiente FuelWeightCoef nelle impostazioni: resta 0.03 e ora è in secondi per CHILOGRAMMO.
  La conversione litri→kg è nel codice, esplicita.

COME VERIFICARE CHE PARTI DA UNA BASE SANA

  "C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
  "User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"

Atteso: exit code 0, 219 test PASS.

COSA POSSO DARTI IO

Replay di Road Atlanta girati a 3x, sempre la stessa gara, così i confronti fra un run e l'altro
sono puliti. I log finiscono in Logs/Road Atlanta/. L'ultimo buono è SimRIG_DebugLog_20260831_195300.
Dimmi cosa vuoi misurare e te lo procuro.
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
