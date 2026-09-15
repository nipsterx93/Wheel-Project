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
| 7 | 2026-09-15 | Simulatori: **solo iRacing per ora**; in futuro almeno Assetto Corsa. |
| 8 | 2026-09-15 | Struttura: **nucleo comune (`Core`) + un adattatore con le regole proprie per ogni simulatore (`Sims/IRacing`) + guscio SimHub (`Plugin`)**, non un gruppo di moduli per simulatore. |
| 9 | 2026-09-15 | Contratti dei moduli (sezione 2) approvati. Vetture riconosciute per **`CarIdx`**, non per nome (gare con cambio pilota). Orologio: il **tempo di sessione crescente di iRacing**, non il conto alla rovescia. |
| 10 | 2026-09-15 | Ordine di costruzione e validazione (sezione 3) approvati, con il **tetto di tre cicli di validazione per logica** e lo stop per decidere insieme al terzo ciclo fallito. |

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

- **Congelato:** niente più correzioni, salvo guasti bloccanti e l'eccezione della sezione 4 per spegnere undercut e
  overcut. Il piano `2026-09-13-daytona-piano-correzioni.md` (passi 3–5) e il fix di Y-62 sono sospesi: i loro numeri
  servono come riferimento per le logiche nuove.
- La diagnostica di `32a8682` resta: serve al confronto.
- La dash legge le proprietà come `DataPluginDemo.SimRIG.*` (255 riferimenti nel solo `Test.djson`): SimHub usa come
  prefisso il nome della classe del plugin, non `[PluginName("SimRIG")]`.

## Sezioni del design

| # | Sezione | Stato |
|---|---|---|
| 1 | Architettura: principi, livelli e moduli, anelli da spezzare, struttura per simulatore | ✅ approvata da Andreas il 2026-09-15 |
| 2 | Contratti: com'è fatto un modulo (ingressi, uscite, qualità dei valori, storico e statistiche), come si testa | ✅ approvata da Andreas il 2026-09-15, con le note su orologio e `CarIdx` |
| 3 | Ordine di costruzione e validazione: passi di ogni logica, ordine, verità di confronto, soglie, tetto per logica, cosa si porta dal vecchio | ✅ approvata da Andreas il 2026-09-15 |
| 4 | Convivenza e passaggio: due plugin in SimHub, plugin vecchio durante la convivenza, log di confronto, prova di fattibilità, passaggio della dash, qualità in dash | 🟡 proposta, in discussione (sotto) |
| 5 | Progetto e processo: struttura della cartella, nomi, soluzione e test, lock e hook estesi alla cartella nuova, ADR-007, documenti da aggiornare | da presentare |

Dopo l'approvazione delle cinque sezioni: spec scritto, revisione di Andreas, poi il piano di implementazione.

## Sezione 1 — Architettura (approvata il 2026-09-15)

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
5. **Solo l'adattatore del simulatore tocca SimHub e iRacing; il guscio non calcola.** Tutto il resto è codice puro,
   testabile coi numeri dei log (ADR-004).
6. **Moduli piccoli.** Una responsabilità per modulo; un file che supera qualche centinaio di righe probabilmente fa
   due cose.

### Livelli e moduli

| Livello | Modulo | Possiede | Legge da |
|---|---|---|---|
| 0 | `Input` (adattatore del simulatore, oggi `Sims/IRacing`) | istantanea neutra per tick: sessione e, per ogni vettura, posizione, giro, dove si trova, corsia box, velocità, tempi sul giro; per il Player carburante e input | SimHub, iRacing |
| 1 | `Track` | geofence della corsia e della zona estesa, lunghezza, dati calibrati per circuito e classe | database |
| 1 | `Cars` | per ogni vettura: identità e classe, posizione valida o ferma, giro, in pista / corsia / piazzola | `Input` |
| 2 | `Events` | ingresso e uscita da corsia e zona estesa, fermo in piazzola, passaggio sul traguardo, drive-through, ricomparsa dopo un buco di dati | `Cars`, `Track` |
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
   di corsia box vengono dal flag nativo del simulatore; `Calibration` impara le geofence da quegli eventi e le scrive
   nel database; `Track` le rilegge dalla sessione successiva o dopo il consenso, mai nello stesso tick.

### Un simulatore oggi, altri domani (approvata il 2026-09-15)

Un gruppo iRacing con dentro tutti i moduli, all'arrivo di Assetto Corsa, costringerebbe a copiare `Timings`, `Fuel`,
`PitLoss` e gli altri nel gruppo nuovo: ogni logica esisterebbe due volte. Fra un simulatore e l'altro cambiano **i
dati** e **alcune regole**, non la matematica.

```
User.PluginSdkDemoRemastered/
  Core/          logiche comuni: Track, Cars, Events, Timings, Calibration, Fuel, Pace, Gaps, PitLoss, Race, Target, MergeGap
  Sims/
    IRacing/     adattatore (SimHub + SDK iRacing → istantanea neutra) e regole proprie di iRacing
    (AssettoCorsa/ in futuro)
  Plugin/        guscio SimHub: ciclo, proprietà, log, voce, volante, impostazioni
```

- **`Core`** non conosce nessun simulatore: lavora su un'istantanea neutra, in cui ogni dato che un simulatore può
  non fornire è opzionale e ha la sua qualità.
- **`Sims/IRacing`** contiene due cose sole: l'adattatore che riempie l'istantanea, e le regole di iRacing che il
  nucleo riceve come parametri. Esempi già nel codice vecchio: il consumo BoP da `CarClassMaxFuelPct` nei dati di
  sessione (`SessionYamlParser.cs`, `SessionDataReader.cs`), i servizi ai box in simultanea, le vetture lontane
  `NotInWorld` nei replay.
- **Regola:** nessun tipo o concetto di iRacing fuori da `Sims/IRacing/`. Per esempio `IracingTrackSurface` diventa
  una posizione neutra: in pista, fuori pista, corsia, piazzola, non visibile.
- **Costo oggi:** quasi nullo; per Assetto Corsa non si scrive nulla adesso.
- **Limite:** un'istantanea pensata su un solo simulatore andrà ritoccata quando arriva il secondo, ma in un posto solo.
- **Il plugin vecchio fa il contrario:** `PitRadar.cs:463-475` sceglie il layout dei box in base al nome del gioco,
  dentro un modulo di calcolo.

### Domande aperte della sezione 1

- La regola "una vettura è una vettura" regge anche dove oggi il Player ha logiche proprie (calibrazioni guidate,
  rilevamento delle soste)?
- Il flag nativo di corsia box è abbastanza affidabile da fare da fonte degli eventi? Y-23 e Y-33 riguardavano il
  flag di SimHub e la geofence, non quello nativo.
- Assetto Corsa o Assetto Corsa Competizione? Non serve saperlo adesso.

## Sezione 2 — Contratti: com'è fatto un modulo (approvata il 2026-09-15)

### Anatomia di un modulo

Ogni modulo del `Core` ha le stesse cinque parti:

1. **Ingressi:** solo i risultati dei moduli dei livelli sotto, ricevuti nel costruttore come viste in sola lettura.
   Le dipendenze si leggono nella firma del costruttore; nessun modulo va a cercarsi dati altrove.
2. **Aggiornamento:** un solo `Update` per tick, chiamato dal guscio in ordine di livello.
3. **Uscite:** un risultato in sola lettura, per vettura dove serve. Nessuno scrive nel risultato di un altro.
4. **Stato interno:** privato, per esempio l'istante di partenza di un cronometro.
5. **Reset:** a cambio di sessione e a salto del replay, segnalati dall'ingresso.

Un modulo non scrive file e non conosce SimHub: la diagnostica la manda a un logger ricevuto nel costruttore, e il
guscio la scrive.

### Ogni valore porta la sua qualità

Le grandezze escono come valore + qualità + istante a cui si riferiscono:

| Qualità | Significa | Esempio |
|---|---|---|
| `Measured` | misurato adesso o in questo evento | giro appena chiuso da una vettura visibile |
| `Held` | ultimo valore misurato, tenuto; porta la sua età | posizione di una vettura sparita dai dati |
| `Estimated` | calcolato da un modello, non misurato | carburante a bordo di un avversario |
| `Unavailable` | non disponibile | tempo di sosta di una vettura mai vista fermarsi |

**Propagazione:** un risultato vale quanto il suo ingresso più debole. Un distacco calcolato da una posizione `Held`
non è `Measured`; un MergeGap con lo stazionario del Target stimato è `Estimated`. Chi usa un valore non misurato
decide in modo esplicito cosa farne, invece di scoprirlo da un difetto (Y-58…Y-62).

### Identità della vettura

Le vetture si riconoscono dallo slot del simulatore (in iRacing `CarIdx`), non dal nome del pilota: nelle gare a
squadre il pilota cambia sulla stessa vettura. Il nome è un attributo. Il plugin vecchio indicizza gli avversari per
nome (`OpponentTracker.cs:1101-1103`). **Confermato da Andreas.**

### Un solo orologio

Tutti i moduli usano il **tempo di sessione crescente di iRacing**. I salti del replay (riavvolto o spostato) li rileva
solo l'ingresso e li comunica come reset.

- **Nota di Andreas:** nei log la colonna `SessionTime` è un conto alla rovescia. **Verificato:** è vero per i log del
  plugin vecchio, perché quella colonna contiene il tempo rimanente pur chiamandosi `SessionTime` (`LogManager.cs:48`,
  `:280`) e i cronometri usano il conto alla rovescia (`DataPluginDemo.cs:1273` passa `SessionTimeLeft`, compensato
  con `Math.Abs` nei distacchi).
- Il tempo crescente il plugin vecchio **lo legge già**
  (`DataCorePlugin.GameRawData.CurrentSessionInfo._SessionTime`, `TelemetryReader.cs:70-73`), ma lo usa solo per lo
  storico delle pressioni (`TelemetryReader.cs:368-372`). Il plugin nuovo usa quello, invece di ribaltare il conto
  alla rovescia: il ribaltamento non regge a fine gara a tempo, quando il timer arriva a zero mentre si corre ancora
  l'ultimo giro (il plugin vecchio ha dovuto gestire valori a zero e negativi), né nelle sessioni a giri.
- Durante la convivenza i log del plugin nuovo portano **entrambe le colonne**, tempo crescente e tempo rimanente, per
  confrontarli coi log vecchi.

### Storico e statistiche

- Il **produttore possiede i campioni** della sua grandezza e le **statistiche standard**: ultimo, mediana degli
  ultimi N validi, migliore valido. Se due moduli possono volere la stessa statistica, la calcola il produttore, una
  volta sola.
- Il **produttore etichetta** ogni campione: giro con sosta, giro di uscita, fuori pista, bandiera gialla, non
  osservato. Il **consumatore sceglie** per etichetta e non ri-etichetta mai: `TyreDeg` chiede a `Timings` i giri
  senza sosta e senza fuori pista.
- I campioni fisicamente impossibili si scartano alla fonte (ADR-005), con l'etichetta del motivo.
- Le statistiche proprie di una logica, come la pendenza del degrado, restano nel modulo che le usa.

### Esempio completo: `Timings`

- **Ingressi:** eventi del tick da `Events`, validità della vettura da `Cars`.
- **Uscite per vettura:** tempi sul giro e tempi di passaggio; cronometri completati di corsia, zona estesa,
  transito, stazionario, drive-through, AccDec; storico etichettato e statistiche standard.
- **Stato interno:** i cronometri in corso.
- **Non fa:** decidere quando una vettura entra in corsia (`Events`), normalizzare i giri col carburante (`Pace`),
  calcolare la perdita ai box (`PitLoss`).
- **Casi di test dai log:**
  - sosta del Player, Daytona `094551` (`Player Pit AccDec Details`): zona estesa 58.62 s, corsia 46.98 s, AccDec
    11.63 s, stazionario 14.47 s;
  - avversario che ricompare dopo `NotInWorld` già oltre l'uscita (Matt Loveridge, `163743`, AccDec 4.93 s): corsia e
    AccDec "non osservati", non salvati come misura.

### Test

- I moduli del `Core` sono puri: nei test si costruiscono con ingressi finti, senza tipi SimHub.
- I casi usano numeri veri dei log, col riferimento all'evento (ADR-004); prima del commit si neutralizza la logica e
  si controlla che il test diventi rosso.
- Runner console come oggi (ADR-003), con due correzioni: raccoglie tutti i fallimenti invece di fermarsi al primo, e
  conta a parte i test saltati (lezione di Y-54).
- Un file di test per modulo.

### Dimensioni e nomi

- Identificatori in inglese. Un modulo è una cartella con pochi file, per esempio `Core/Timings/`: il modulo, il
  cronometro, il risultato.
- Un file oltre qualche centinaio di righe segnala che il modulo fa due cose.

## Sezione 3 — Ordine di costruzione e validazione (approvata il 2026-09-15)

### I passi di ogni logica

1. **Analisi:** cosa fa il plugin vecchio, quali punti chiusi la riguardano, quali casi limite sono già noti coi numeri.
2. **Contratto:** definizione, ingressi, uscite, qualità (sezione 2), e **soglia di validazione fissata adesso**, sui
   numeri di entrambi i circuiti, prima di scrivere codice. Mai dopo aver visto il risultato.
3. **Test rossi:** casi coi numeri dei log.
4. **Implementazione** fino ai test verdi, con la neutralizzazione di ADR-004.
5. **Validazione:** il plugin nuovo gira su Daytona e Road Atlanta; i suoi log si confrontano con la verità e la soglia.
6. **Chiusura:** handoff; da qui la logica è disponibile alle successive.

Una logica non è chiusa senza il passo 5 su entrambi i circuiti.

### L'ordine

| Passo | Logica | Cosa si ottiene |
|---|---|---|
| 0 | prova di fattibilità (sezione 4) | scheletro del plugin caricato in SimHub accanto al vecchio; l'adattatore legge iRacing e scrive un log |
| 1 | `Input` + `Cars` | identità per `CarIdx`, posizione con la sua qualità, dove si trova ogni vettura |
| 2 | `Track` + `Events` | geofence dal database; eventi di corsia, zona estesa, traguardo, piazzola, ricomparsa |
| 3 | `Timings` | giri, tempi di passaggio, cronometri dei box |
| 4 | `Gaps` + `Target` (scelta e distacco) | primo numero confrontabile col plugin vecchio in dash |
| 5 | `Fuel` | consumo del Player, stima BoP degli avversari |
| 6 | `Pace` / `TyreDeg` | passo normalizzato e degrado |
| 7 | `Calibration` + `PitLoss` | calibrazioni col consenso, perdita ai box di qualunque vettura |
| 8 | `Race` | leader, bandiera, giri totali, carburante da imbarcare |
| 9 | `MergeGap` | dove si rientra rispetto al Target |
| 10 | passaggio della dash (sezione 4) | la dash legge il plugin nuovo |

Dopo, fuori da questo piano: undercut e overcut (Fase B), meteo, gomme e pressioni, voce, volante, profili.

**Perché questo ordine.** Al passo 4 l'architettura è provata da cima a fondo, dall'ingresso a un numero in dash. E
le funzioni della strada A (carburante, giri totali, distacco) sono tutte pronte al passo 8, **prima** del MergeGap:
se il MergeGap si blocca, il plugin nuovo è già la versione ridotta, senza lavoro in più.

### Verità di confronto e soglie

| Logica | Verità di confronto | Soglia proposta |
|---|---|---|
| `Cars` | superficie nativa e posizione nei log | nessuna posizione ferma segnata `Measured`; a Daytona `124637` van Elewout fermo a 0.1883 risulta `Held` |
| `Events` | flag nativo di corsia e soste note | ogni sosta del Player rilevata una volta, nessun ingresso fantasma (Y-33) |
| `Timings` | tempo del giro del gioco; sosta del Player misurata dal plugin vecchio (`094551`: 58.62 / 46.98 / 11.63 / 14.47 s) | giro entro ±0.05 s; componenti della sosta entro ±0.2 s |
| `Gaps` | differenza dei passaggi sul traguardo, esatta sulla linea | entro ±0.1 s sulla linea; nessun distacco `Measured` da posizione ferma |
| `Fuel` | livello carburante del Player | consumo per giro entro ±0.05 L; per gli avversari soglia da fissare nell'analisi, confrontando col rifornimento osservato |
| `Pace` / `TyreDeg` | giri puliti misurati | passo entro ±0.3 s dalla mediana dei giri puliti dello stint; per il degrado oggi non c'è una verità misurata, da definire nell'analisi |
| `PitLoss` | perdita reale della sosta osservata (Player a Daytona: 34.7 s) | previsione prima della sosta entro ±1 s |
| `Race` | giri reali a fine gara (Daytona: `Projection Validation`, vero = 27.742) | da fissare nell'analisi, sui numeri dei due circuiti |
| `MergeGap` | gap reale dopo le soste (Daytona: −2.7 s) | ±1 s prima delle soste (il plugin vecchio arriva a +0.33 s), ±1.5 s durante, ±1 s dopo |

I riferimenti di Road Atlanta (`20260911_231106`) si estraggono al passo 1 di ogni logica: oggi il piano ha i numeri di
Daytona. Le soglie si fissano in modo definitivo al passo 2 di ogni logica.

### Tetto per logica

- **Tre cicli di validazione per logica.** Un ciclo è: implementazione o correzione → replay sui due circuiti →
  confronto con la soglia.
- Se al terzo ciclo la soglia non è raggiunta, **ci si ferma e si decide con Andreas**: definizione sbagliata, dato
  non disponibile, soglia irrealistica. Mai una quarta correzione in automatico.
- Il passaggio alla strada A non è automatico: è una decisione, e grazie all'ordine le sue funzioni sono già pronte.

### Cosa si porta dal vecchio (candidati, da confermare nell'analisi di ciascuna logica)

| Logica | Candidati dal plugin vecchio | Probabile |
|---|---|---|
| `Cars` | `TrackPositionValidator` (teletrasporti) | portato |
| `Events` | `PitLaneDetector` (cascata di rilevamento) | da rivedere sul flag nativo |
| `Timings` | tempi di passaggio a 400 punti (`OpponentTracker`), `SectorTracker` (mediana dei transiti) | riscritti in un modulo unico per tutte le vetture |
| `Calibration` | `CalibrationConsensus`, `GeofenceCalibrationGate` | portati |
| `Fuel` | consumo robusto di `FuelManager`, regola BoP di `OpponentTracker` | riscritti in un modulo unico |
| `Race` | `RaceTimeProjection.ProjectFlagMoment` (criterio del massimo, Y-38), `LeaderPaceFilter` | portati |
| `PitLoss` | formule di `CarPitData` (`CalculateTotalPitLoss`), `PlayerPitSpeedObserver` | da verificare, poi portati |
| `MergeGap` | previsione della sosta del Target (`ForecastTargetPit`, passo 1 del piano Daytona) | riscritta; i casi del passo 3 (latch, `ApproachingPits`) diventano test |

## Sezione 4 — Convivenza e passaggio (proposta, in discussione)

### Due plugin in SimHub

| | Plugin vecchio | Plugin nuovo |
|---|---|---|
| DLL | `User.PluginSdkDemo.dll` | nome proprio, altrimenti la copia dopo la build sovrascrive la vecchia |
| Classe principale (dà il prefisso alle proprietà) | `DataPluginDemo` | nome proprio durante la convivenza |
| Nome in SimHub | `SimRIG` | nome proprio, riconoscibile nell'elenco dei plugin |
| Impostazioni | `GeneralSettings` (`DataPluginDemo.cs:185`), nome generico | nome proprio: non legge e non sovrascrive quelle del vecchio |
| Database delle calibrazioni | `SimRIG_Data.json` nella cartella di SimHub, letto e riscritto (`PitRadar.cs:297`, `:1100`, `:1286`) | file proprio, creato una volta copiando quello vecchio; il plugin nuovo non scrive mai il file vecchio |
| Log | `Logs\SimRig Logs\SimRIG_*_{data}` (`LogManager.cs:132-143`) | cartella propria, stesso formato di data per accoppiare i file |
| Dash | serve la dash di oggi | proprietà col suo prefisso, visibili in una pagina di prova |
| Voce, volante, pagina impostazioni | restano al vecchio | niente fino al passaggio: nessun annuncio doppio |

I nomi concreti si decidono nella sezione 5.

### Il plugin vecchio durante la convivenza

Congelato, con un'eccezione già decisa: spegnere undercut e overcut (decisione 3). Oggi non c'è un'impostazione
dedicata: `EnableVoiceEngineer` zittirebbe tutto l'ingegnere. Gli annunci legati alla strategia sono tre, e partono
tutti da `UndercutViable` o `OvercutViable`:

- `REPORT_UNDERCUT` (`DataPluginDemo.cs:1098-1100`);
- `AUTO_UNDERCUT_ALERT` (`DataPluginDemo.cs:1599-1601`);
- `TARGET_ENTERING_PITS_OVERCUT` (`DataPluginDemo.cs:1650-1652`).

Due modi:

- **(a) Interruttore alla fonte:** una modifica piccola che tiene a falso `UndercutViable`, `OvercutViable` e
  `TrafficAlert`. Si spengono insieme le spie in dash e i tre annunci, senza toccare altra logica. Serve il lock e un
  test, come per ogni modifica.
- **(b) Nessuna modifica al codice:** Andreas toglie le tre spie dalla dash; gli annunci vocali restano.

### Log di confronto

Risponde alla domanda aperta della sezione 2.

- Un file CSV per modulo e per sessione, nella cartella di log del plugin nuovo.
- Colonne comuni: tempo crescente, tempo rimanente (per accoppiare coi log vecchi), giro, `CarIdx`, grandezza, valore,
  qualità, età (per `Held`), etichette.
- Righe a ogni evento e a campionamento regolare (per esempio una al secondo per vettura), regolabile per modulo.
- **Script di validazione nel repository:** per ogni logica uno script che legge i log dei due circuiti e stampa il
  confronto con la soglia, coi numeri. Oggi gli script di analisi restano nella cartella temporanea della sessione e si
  perdono (è già successo: handoff del 2026-09-13 21:50). Con un formato comune diventano riusabili da qualunque agente.

### Prova di fattibilità (passo 0)

Su un pezzo di replay Daytona deve dimostrare:

1. i due plugin caricati e attivi insieme, senza errori;
2. il plugin nuovo riceve i dati a ogni tick, col tempo di calcolo per tick scritto nel log;
3. l'adattatore legge tempo crescente e rimanente; per ogni `CarIdx` posizione, superficie e corsia box; tempi sul
   giro; carburante del Player; dati di sessione (classi, BoP, nome per `CarIdx`);
4. nel log si vedono le vetture `NotInWorld` e i salti del replay;
5. la colonna del tempo rimanente coincide con quella dei log vecchi.

**Se i due plugin non convivono** (per esempio per l'accesso ai dati di iRacing): il nucleo resta un progetto separato,
ma lo carica il plugin vecchio al posto di un secondo plugin. Il confine resta, perché la libreria nuova non vede il
codice vecchio.

### Passaggio della dash (passo 10)

- **Quando:** logiche validate fino al passo 9, oppure fino all'8 se si decide la strada A.
- **Prima:** una pagina di prova con i numeri chiave affiancati, vecchio e nuovo (carburante da imbarcare, giri
  totali, distacco, MergeGap), guardata da Andreas in una sessione vera.
- **Come:** il plugin nuovo pubblica gli stessi nomi delle 116 proprietà usate dalla dash, con una tabella nel guscio
  "proprietà → modulo che la produce". Al passaggio la sua classe principale prende il nome `DataPluginDemo` e la DLL
  vecchia si toglie: la dash non si tocca. Le proprietà che il nuovo non produce (undercut e overcut fino alla Fase B)
  restano vuote o si tolgono dalla dash.
- **Ritorno indietro:** la DLL vecchia resta disponibile, in Git e in una copia, per rimetterla se serve.

### Valori tenuti o stimati in dash

Risponde all'altra domanda aperta della sezione 2.

- Per i numeri chiave, non per tutte le 116 proprietà, il guscio pubblica anche la qualità: per esempio accanto a
  `SimRIG.Target.GapSeconds` una proprietà con misurato / tenuto / stimato / non disponibile. La dash decide come
  mostrarla (grigio, trattino).
- Precedente nel plugin vecchio: `SimRIG.Session.IsLapsPredictionValid` (Y-52).

### Domande aperte della sezione 4

- Come si spengono undercut e overcut nel plugin vecchio: (a) interruttore alla fonte o (b) solo dash?
- Quali numeri chiave pubblicano anche la qualità.

## Come si riprende in una nuova sessione

```
/new-session riprendi il brainstorming di .ai/plans/2026-09-15-remastered-brainstorming.md
```

Si riparte dalla sezione segnata "in discussione", oppure dalla prima "da presentare". Le decisioni della tabella sono
prese: non si rimettono in discussione senza Andreas.
