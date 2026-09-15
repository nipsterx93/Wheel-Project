# Remastered — brainstorming del plugin nuovo

- **Data:** 2026-09-15 (decisioni del 2026-09-14 e del 2026-09-15)
- **Autore:** claude, in brainstorming con Andreas
- **Stato:** 🟡 in discussione. Il design si approva una sezione alla volta; niente codice finché lo spec non è approvato.
- **Cartella del plugin nuovo:** `User.PluginSdkDemoRemastered/` (creata da Andreas il 2026-09-15, vuota, non esclusa da Git)

## Perché

Ogni replay analizzato portava difetti nuovi. L'analisi del 2026-09-14 ha trovato la causa strutturale: le stesse
grandezze sono calcolate in più moduli con approssimazioni diverse, quindi ogni replay mette alla prova un calcolo
diverso della stessa cosa. Esempi misurati:

- perdita ai box del Player in tre posti: `TargetStrategyManager.cs:1106` e `DataPluginDemo.cs:2063` con la mediana
  dei transiti, `RaceAnalyzer.cs:1186-1195` col minimo di classe (~1.5–1.8 s di differenza nello stesso istante);
- perdita ai box del leader con 25 s fissi (`RaceAnalyzer.cs:954-1003`);
- distacco fra due vetture in tre modi: tempi di passaggio (gap del Target), posizione × passo (traffico, fino a
  ~8–10 s di errore a Daytona), distanza / velocità (`OpponentTracker.cs:1315`);
- vettura `NotInWorld` gestita in quattro modi diversi (Y-58, Y-59, Y-60, Y-62);
- passo degli avversari come `SectorBaseline × 3.0` (`TargetStrategyManager.cs:1240`), unità sbagliata.

Dimensioni: ~14.000 righe nei 9 file principali; 299 proprietà `SimRIG.*` con nome fisso, di cui 116 usate dalle 8
dash attive in `E:\SimHub\DashTemplates\Test\`.

## Decisioni prese

| # | Data | Decisione |
|---|---|---|
| 1 | 2026-09-14 | Strada **B**: un solo produttore per ogni grandezza. Se non funziona, strada **A**: versione ridotta con carburante, giri totali e distacco dal Target. |
| 2 | 2026-09-14 | Uscita dalla B: **soglie misurate + tetto di lavoro**. Con la riscrittura il tetto è **per logica**, non una scadenza globale che spinge verso la A. |
| 3 | 2026-09-14 | Durante i lavori **undercut e overcut spenti**: proprietà in dash, avviso di traffico, annunci vocali. Restano carburante, giri totali, distacco e MergeGap. |
| 4 | 2026-09-14 | Ogni logica si verifica su **due circuiti**, Daytona e Road Atlanta, con replay rigirati da Andreas. |
| 5 | 2026-09-15 | Approccio: **riscrittura del nucleo come plugin nuovo**, progetto separato nello stesso repository, accanto al plugin vecchio. |
| 6 | 2026-09-15 | Modo di lavorare: **una logica alla volta**. Si analizza nel vecchio codice, si definisce, si testa coi numeri dei log, si implementa, si valida; la successiva usa i risultati della precedente. Logiche semplici, testate e disponibili alle altre. |

Esempio di Andreas: un modulo **Timings** avvia tutti i cronometri (corsia box, zona estesa, transito, drive-through,
stazionario, tempo sul giro e altri); un modulo **TyreDeg** prende i tempi sul giro da Timings e applica la sua
normalizzazione per il degrado.

## Cosa si eredita dal plugin vecchio

Ogni pezzo passa da una di tre porte: **portato** (validato e pulito), **riscritto** (doppio o sbagliato),
**abbandonato** (non usato).

- **Conoscenza:** `.ai/archive/CLOSED_POINTS.md` (41 punti con numeri e ragionamento), ADR-004 e ADR-005, handoff.
- **Casi di test** coi numeri veri dei log (es. la baseline di Y-17b `56.409`, la geofence di Y-23 `0.963`).
- **Moduli già puri**, se l'analisi li conferma: `RaceTimeProjection`, `CalibrationConsensus`, `LeaderPaceFilter`,
  `PitLaneDetector`, `TrackPositionValidator`.
- **Dati:** replay e log in `Logs/` come verità di confronto; database delle calibrazioni `SimRIG_Data.json`.
- **Protocollo** `.ai/`: lock e handoff.

Non si eredita: la struttura per zone e i file da 2–3 mila righe, le logiche doppie, le 183 proprietà che nessuna dash
usa, undercut e overcut finché la Fase B non li valida.

## Il plugin vecchio durante i lavori

- **Congelato:** niente più correzioni, salvo guasti bloccanti. Il piano `2026-09-13-daytona-piano-correzioni.md`
  (passi 3–5) e il fix di Y-62 sono sospesi: i loro numeri servono come riferimento per le logiche nuove.
- La diagnostica di `32a8682` resta: serve al confronto.
- La dash legge le proprietà come `DataPluginDemo.SimRIG.*` (255 riferimenti nel solo `Test.djson`): SimHub usa come
  prefisso il nome della classe del plugin, non `[PluginName("SimRIG")]`. Al passaggio si sostituisce il prefisso
  negli 8 file dash, oppure la classe del plugin nuovo prende lo stesso nome quando il vecchio viene tolto.

## Sezioni del design

| # | Sezione | Stato |
|---|---|---|
| 1 | Architettura: principi, livelli e moduli, anelli da spezzare | 🟡 proposta, in discussione (sotto) |
| 2 | Contratti: com'è fatto un modulo (ingressi, uscite, validità, storico e statistiche), come si testa | da presentare |
| 3 | Ordine di costruzione e validazione: catena delle logiche, verità di confronto, soglie, tetto per logica, cosa si porta dal vecchio | da presentare |
| 4 | Convivenza e passaggio: due plugin in SimHub, log di confronto, prova di fattibilità iniziale, passaggio della dash | da presentare |
| 5 | Progetto e processo: struttura della cartella, soluzione e test, lock e hook estesi alla cartella nuova, ADR-007, documenti da aggiornare | da presentare |

Dopo l'approvazione delle cinque sezioni: spec scritto, revisione di Andreas, poi il piano di implementazione.

## Sezione 1 — Architettura (proposta, in discussione)

### Principi

1. **Un produttore per grandezza.** Chi ha bisogno di un valore lo chiede al modulo che lo possiede e non lo
   ricalcola: `Timings` possiede i cronometri, `TyreDeg` legge i tempi sul giro.
2. **Una vettura è una vettura.** Stesso codice per Player e avversari: stessi eventi, stessi cronometri, stesso
   passo. Il Player ha solo più dati (carburante, input). Nel plugin vecchio Player e avversari hanno percorsi separati
   (`PitRadar` e `OpponentTracker`), fonte di doppioni come l'AccDec misurato in due modi (Y-60).
3. **Dipendenze a senso unico.** I moduli stanno su livelli; un modulo legge solo dai livelli sotto. Gli anelli si
   spezzano con una regola scritta.
4. **Ogni valore dice quanto vale.** Misurato, stimato o non disponibile, e da quando. Nessun valore fermo passa per
   vivo: è la radice comune di Y-58…Y-62.
5. **Solo `Input` tocca SimHub e iRacing; il guscio non calcola.** Tutto il resto è codice puro, testabile coi numeri
   dei log (ADR-004).
6. **Moduli piccoli.** Una responsabilità per modulo; un file che supera qualche centinaio di righe probabilmente fa
   due cose.

### Livelli e moduli

| Livello | Modulo | Possiede | Legge da |
|---|---|---|---|
| 0 | `Input` | istantanea per tick: sessione e, per ogni vettura, posizione, giro, superficie, corsia box nativa, velocità, tempi sul giro del gioco; per il Player carburante e input | SimHub, iRacing |
| 1 | `Track` | geofence della corsia e della zona estesa, lunghezza, dati calibrati per circuito e classe | database |
| 1 | `Cars` | per ogni vettura: identità e classe, posizione valida o ferma, giro, in pista / corsia / piazzola | `Input` |
| 2 | `Events` | ingresso e uscita da corsia e zona estesa, fermo in piazzola, passaggio sul traguardo, drive-through, ricomparsa dopo `NotInWorld` | `Cars`, `Track` |
| 3 | `Timings` | cronometri per vettura: giro, tempi di passaggio (400 punti), corsia, zona estesa, transito, stazionario, drive-through, AccDec; ognuno completo o "non osservato" | `Events`, `Cars` |
| 3 | `Calibration` | impara geofence e tempi del Player dagli eventi e li scrive nel database col consenso (ADR-005) | `Events`, `Timings` |
| 4 | `Fuel` | consumo misurato del Player; stima BoP e carburante a bordo degli avversari | `Input`, `Timings` |
| 4 | `Pace` / `TyreDeg` | giri puliti, passo normalizzato, degrado gomme, per ogni vettura | `Timings`, `Fuel` |
| 4 | `Gaps` | distacco in secondi fra due vetture qualunque | `Timings`, `Cars` |
| 5 | `PitLoss` | perdita ai box di qualunque vettura: tempi misurati più stazionario previsto | `Timings`, `Fuel`, `Pace`, `Track` |
| 5 | `Race` | leader, bandiera, giri totali e rimanenti, carburante da imbarcare | `Cars`, `Pace`, `PitLoss`, `Fuel` |
| 5 | `Target` | quale vettura è il Target e i suoi dati | `Cars`, `Gaps`, `PitLoss`, `Fuel` |
| 6 | `MergeGap` | dove si rientra rispetto al Target | `Gaps`, `PitLoss`, `Pace`, `Race`, `Target` |
| — | guscio | ciclo SimHub, proprietà per la dash, log, voce, volante, impostazioni | tutti, in sola lettura |

Da collocare nelle sezioni successive: meteo, gomme e pressioni, voce e cascata di calibrazione guidata, volante,
profili.

### Anelli da spezzare con una regola scritta

1. **Carburante ↔ sosta ↔ giri.** Il carburante da imbarcare dipende dai giri rimanenti; i giri rimanenti dipendono
   da quanto dura la sosta; la sosta dipende dal carburante da imbarcare. Proposta: `Race` usa la durata della sosta
   calcolata al tick precedente.
2. **Geofence ↔ eventi.** Gli eventi usano le geofence, e le geofence si imparano dagli eventi. Proposta: gli eventi
   di corsia box vengono dal flag nativo di iRacing; `Calibration` impara le geofence da quegli eventi e le scrive nel
   database; `Track` le rilegge dalla sessione successiva o dopo il consenso, mai nello stesso tick.

### Domande aperte della sezione 1

- Il plugin nuovo è **solo per iRacing** o deve funzionare anche con altri simulatori? Cambia `Input` ed `Events`:
  il plugin vecchio ha ripieghi per altri giochi.
- La regola "una vettura è una vettura" regge anche dove oggi il Player ha logiche proprie (calibrazioni guidate,
  rilevamento delle soste)?
- Le statistiche (mediana degli ultimi transiti, migliore valido) stanno in `Timings` o nel modulo che le usa?
  (Sezione 2.)
- Il flag nativo di corsia box è abbastanza affidabile da fare da fonte degli eventi? Y-23 e Y-33 riguardavano il
  flag di SimHub e la geofence, non quello nativo.

## Come si riprende in una nuova sessione

```
/new-session riprendi il brainstorming di .ai/plans/2026-09-15-remastered-brainstorming.md
```

Si riparte dalla sezione segnata "in discussione", oppure dalla prima "da presentare". Le decisioni della tabella sono
prese: non si rimettono in discussione senza Andreas.
