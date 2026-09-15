# SimRIG Remastered — spec del plugin nuovo

- **Data:** 2026-09-15
- **Autori:** Andreas e claude, dal brainstorming del 2026-09-14 e del 2026-09-15
- **Stato:** ✅ approvato da Andreas il 2026-09-15: il design una sezione alla volta, poi lo spec con la conferma del §8.5
- **Cartella del plugin nuovo:** `User.PluginSdkDemoRemastered/`
- **Sostituisce** come lavoro attivo il piano `2026-09-13-daytona-piano-correzioni.md`, sospeso. La discussione che ha
  portato qui è nella storia Git di `.ai/plans/2026-09-15-remastered-brainstorming.md` (commit da `179199e` a `8efd8df`).
- **Parti spostate il 2026-09-15** con la riorganizzazione del §8.5, perché ogni informazione stia in un posto solo: il
  modo di lavorare (§3) e build, lock e hook (§8.3, §8.4) in `AGENTS.md`; livelli e moduli (§4.2), struttura e nomi
  (§8.1, §8.2) in `.ai/ARCHITECTURE.md`. I titoli restano qui, per i rimandi.

## 1. Perché

Ogni replay analizzato portava difetti nuovi. L'analisi del 2026-09-14 ha trovato la causa strutturale: le stesse
grandezze erano calcolate in più moduli con approssimazioni diverse, e ogni replay metteva alla prova un calcolo diverso
della stessa cosa. Esempi misurati nel plugin vecchio:

- perdita ai box del Player in tre posti: `TargetStrategyManager.cs:1106` e `DataPluginDemo.cs:2063` con la mediana
  dei transiti, `RaceAnalyzer.cs:1186-1195` col minimo di classe (~1.5–1.8 s di differenza nello stesso istante);
- perdita ai box del leader con 25 s fissi (`RaceAnalyzer.cs:954-1003`);
- distacco fra due vetture in tre modi: tempi di passaggio (gap del Target), posizione × passo (traffico, fino a
  ~8–10 s di errore a Daytona), distanza / velocità (`OpponentTracker.cs:1315`);
- vettura `NotInWorld` gestita in quattro modi diversi (Y-58, Y-59, Y-60, Y-62);
- passo degli avversari come `SectorBaseline × 3.0` (`TargetStrategyManager.cs:1240`), unità sbagliata.

Dimensioni: ~14.000 righe nei 9 file principali; 299 proprietà `SimRIG.*` con nome fisso, di cui 116 usate dalle 8
dash attive in `E:\SimHub\DashTemplates\Test\`.

## 2. Decisioni

| # | Data | Decisione |
|---|---|---|
| 1 | 2026-09-14 | Strada **B**: un solo produttore per ogni grandezza. Se non funziona, strada **A**: versione ridotta con carburante, giri totali e distacco dal Target. |
| 2 | 2026-09-14 | Uscita dalla B: **soglie misurate + tetto di lavoro**, con il tetto **per modulo** e non come scadenza globale. |
| 3 | 2026-09-14 | Durante i lavori **undercut e overcut spenti**: proprietà in dash, avviso di traffico, annunci vocali. Restano carburante, giri totali, distacco e MergeGap. |
| 4 | 2026-09-14 | Ogni modulo si verifica su **due circuiti**, Daytona e Road Atlanta, con replay rigirati da Andreas. |
| 5 | 2026-09-15 | **Riscrittura del nucleo come plugin nuovo**, progetto separato nello stesso repository, accanto al plugin vecchio. |
| 6 | 2026-09-15 | **Un modulo alla volta**; il successivo usa i risultati del precedente. Logiche semplici, testate e disponibili agli altri moduli. |
| 7 | 2026-09-15 | **Solo iRacing per ora**; in futuro almeno Assetto Corsa. |
| 8 | 2026-09-15 | **Nucleo comune (`Core`) + un adattatore con le regole proprie per ogni simulatore (`Sims/IRacing`) + guscio SimHub (`Plugin`)**. |
| 9 | 2026-09-15 | Contratti dei moduli approvati. Vetture riconosciute per **`CarIdx`** (gare con cambio pilota). Orologio: **tempo di sessione crescente di iRacing**. |
| 10 | 2026-09-15 | Ordine di costruzione approvato, con **tetto di tre cicli di validazione per modulo**. |
| 11 | 2026-09-15 | Convivenza dei due plugin approvata. Undercut e overcut si spengono nel plugin vecchio con un **interruttore alla fonte**. |
| 12 | 2026-09-15 | **Tutti fanno tutto:** nessuna divisione di ruoli fra gli agenti, così ogni passo può essere verificato da due agenti. |
| 13 | 2026-09-15 | **Ogni modulo parte da un brainstorming con Andreas**: cosa deve fare, cosa legge, cosa calcola, cosa mette a disposizione degli altri. Solo dopo si implementa. Andreas partecipa in prima persona per tenere il progetto semplice. |
| 14 | 2026-09-15 | Progetto e nomi del plugin nuovo approvati (§8). |

## 3. Come si lavora

Spostato il 2026-09-15 in `AGENTS.md`, sezione "Come si lavora sul plugin nuovo".

### 3.1 Il ciclo di un modulo

→ `AGENTS.md`, "Il ciclo di un modulo".

### 3.2 Regole di semplicità

→ `AGENTS.md`, "Regole di semplicità".

### 3.3 Chi fa cosa

→ `AGENTS.md`, "Chi fa cosa".

## 4. Architettura

### 4.1 Principi

1. **Un produttore per grandezza.** Chi ha bisogno di un valore lo chiede al modulo che lo possiede e non lo
   ricalcola. Esempio di Andreas: `Timings` avvia tutti i cronometri; `TyreDeg` prende i tempi sul giro da `Timings` e
   applica solo la sua normalizzazione.
2. **Una vettura è una vettura.** Stesso codice per Player e avversari: stessi eventi, stessi cronometri, stesso passo.
   Il Player ha solo più dati (carburante, input). Nel plugin vecchio Player e avversari avevano percorsi separati
   (`PitRadar` e `OpponentTracker`), fonte di doppioni come l'AccDec misurato in due modi (Y-60).
3. **Dipendenze a senso unico.** I moduli stanno su livelli; un modulo legge solo dai livelli sotto. Gli anelli si
   spezzano con una regola scritta (§4.3).
4. **Ogni valore dice quanto vale:** misurato, tenuto, stimato o non disponibile, e da quando (§5.2). Nessun valore
   fermo passa per vivo: è la radice comune di Y-58…Y-62.
5. **Solo l'adattatore del simulatore tocca SimHub e iRacing; il guscio non calcola.** Tutto il resto è codice puro,
   testabile coi numeri dei log.
6. **Moduli piccoli:** una responsabilità ciascuno. Un file oltre qualche centinaio di righe segnala che il modulo fa
   due cose.

### 4.2 Livelli e moduli

Spostato il 2026-09-15 in `.ai/ARCHITECTURE.md`, sezione "Plugin nuovo — SimRIG Remastered", tabella "Livelli e
moduli". Il brainstorming di ogni modulo la corregge lì.

### 4.3 Anelli da spezzare

1. **Carburante ↔ sosta ↔ giri.** Il carburante da imbarcare dipende dai giri rimanenti, i giri rimanenti dalla durata
   della sosta, la sosta dal carburante da imbarcare. Regola: `Race` usa la durata della sosta calcolata al tick
   precedente.
2. **Geofence ↔ eventi.** Gli eventi usano le geofence, e le geofence si imparano dagli eventi. Regola: gli eventi di
   corsia box vengono dal flag nativo del simulatore; `Calibration` impara le geofence da quegli eventi e le scrive nel
   database; `Track` le rilegge dalla sessione successiva o dopo il consenso, mai nello stesso tick.

### 4.4 Un nucleo comune, un adattatore per simulatore

Fra un simulatore e l'altro cambiano i dati e alcune regole, non la matematica. Un gruppo di moduli per simulatore
costringerebbe a copiare `Timings`, `Fuel`, `PitLoss` e gli altri all'arrivo del secondo gioco.

- **`Core`** non conosce nessun simulatore: lavora su un'istantanea neutra, in cui ogni dato che un simulatore può non
  fornire è opzionale e porta la sua qualità.
- **`Sims/IRacing`** contiene due cose sole: l'adattatore che riempie l'istantanea, e le regole di iRacing che il nucleo
  riceve come parametri. Esempi dal plugin vecchio: consumo BoP da `CarClassMaxFuelPct` nei dati di sessione
  (`SessionYamlParser.cs`, `SessionDataReader.cs`), servizi ai box in simultanea, vetture lontane `NotInWorld` nei
  replay.
- **Nessun tipo o concetto di iRacing fuori da `Sims/IRacing`.** Per esempio `IracingTrackSurface` diventa una posizione
  neutra: in pista, fuori pista, corsia, piazzola, non visibile.
- Il plugin vecchio faceva il contrario: `PitRadar.cs:463-475` sceglie il layout dei box in base al nome del gioco,
  dentro un modulo di calcolo.

## 5. Contratti dei moduli

### 5.1 Anatomia

Ogni modulo del `Core` ha le stesse cinque parti:

1. **Ingressi:** solo i risultati dei moduli dei livelli sotto, ricevuti nel costruttore come viste in sola lettura.
   Guardando il costruttore si vede da chi dipende.
2. **Aggiornamento:** un solo `Update` per tick, chiamato dal guscio in ordine di livello.
3. **Uscite:** un risultato in sola lettura, per vettura dove serve. Nessuno scrive nel risultato di un altro.
4. **Stato interno:** privato, per esempio l'istante di partenza di un cronometro.
5. **Reset:** a cambio di sessione e a salto del replay, segnalati dall'ingresso.

Un modulo non scrive file e non conosce SimHub: manda la diagnostica a un logger ricevuto nel costruttore, e il guscio
la scrive.

### 5.2 Qualità dei valori

| Qualità | Significa | Esempio |
|---|---|---|
| `Measured` | misurato adesso o in questo evento | giro appena chiuso da una vettura visibile |
| `Held` | ultimo valore misurato, tenuto; porta la sua età | posizione di una vettura sparita dai dati |
| `Estimated` | calcolato da un modello, non misurato | carburante a bordo di un avversario |
| `Unavailable` | non disponibile | tempo di sosta di una vettura mai vista fermarsi |

**Propagazione:** un risultato vale quanto il suo ingresso più debole. Un distacco calcolato da una posizione `Held` non
è `Measured`; un MergeGap con lo stazionario del Target stimato è `Estimated`. Chi usa un valore non misurato decide in
modo esplicito cosa farne.

### 5.3 Identità della vettura

Le vetture si riconoscono dallo slot del simulatore, in iRacing `CarIdx`, non dal nome del pilota: nelle gare a squadre
il pilota cambia sulla stessa vettura. Il nome è un attributo. Il plugin vecchio indicizzava gli avversari per nome
(`OpponentTracker.cs:1101-1103`).

### 5.4 Orologio

Tutti i moduli usano il **tempo di sessione crescente di iRacing**, che il plugin vecchio legge già
(`DataCorePlugin.GameRawData.CurrentSessionInfo._SessionTime`, `TelemetryReader.cs:70-73`) ma usa solo per lo storico
delle pressioni. I salti del replay li rileva solo l'ingresso e li comunica come reset.

- Nei log del plugin vecchio la colonna `SessionTime` contiene in realtà il tempo rimanente (`LogManager.cs:48`, `:280`),
  e i cronometri usano il conto alla rovescia (`DataPluginDemo.cs:1273`), compensato con `Math.Abs` nei distacchi.
- Ribaltare il conto alla rovescia non basta: a fine gara a tempo il timer arriva a zero mentre si corre ancora l'ultimo
  giro, e nelle gare a giri non misura la gara.
- Durante la convivenza i log del plugin nuovo portano entrambe le colonne, tempo crescente e tempo rimanente, per il
  confronto coi log vecchi.

### 5.5 Storico e statistiche

- Il **produttore possiede i campioni** della sua grandezza e le **statistiche standard**: ultimo, mediana degli ultimi
  N validi, migliore valido. Se due moduli possono volere la stessa statistica, la calcola il produttore, una volta sola.
- Il **produttore etichetta** ogni campione: giro con sosta, giro di uscita, fuori pista, bandiera gialla, non
  osservato. Il **consumatore sceglie** per etichetta e non ri-etichetta mai: `TyreDeg` chiede a `Timings` i giri senza
  sosta e senza fuori pista.
- I campioni fisicamente impossibili si scartano alla fonte (ADR-005), con l'etichetta del motivo.
- Le statistiche proprie di una logica, come la pendenza del degrado, restano nel modulo che le usa.

### 5.6 Esempio: `Timings`

- **Legge:** gli eventi del tick da `Events`, la validità delle vetture da `Cars`.
- **Produce per ogni vettura:** tempi sul giro e tempi di passaggio; cronometri completati di corsia, zona estesa,
  transito, stazionario, drive-through, AccDec; storico etichettato e statistiche standard.
- **Non fa:** non decide quando una vettura entra in corsia (`Events`), non normalizza i giri col carburante (`Pace`),
  non calcola la perdita ai box (`PitLoss`).
- **Casi di test dai log:** la sosta del Player a Daytona `094551` (zona estesa 58.62 s, corsia 46.98 s, AccDec 11.63 s,
  stazionario 14.47 s); Matt Loveridge che ricompare dopo `NotInWorld` già oltre l'uscita (`163743`, AccDec 4.93 s):
  corsia e AccDec risultano "non osservati", non salvati come misura.

### 5.7 Test

- I moduli del `Core` sono puri: nei test si costruiscono con ingressi finti, senza tipi SimHub.
- I casi usano numeri veri dei log, col riferimento all'evento; prima del commit si neutralizza la logica e si
  controlla che il test diventi rosso (ADR-004).
- Runner console come nel plugin vecchio (ADR-003), con due correzioni: raccoglie tutti i fallimenti invece di fermarsi
  al primo, e conta a parte i test saltati (lezione di Y-54).
- Un file di test per modulo; identificatori in inglese; un modulo per cartella con pochi file.

## 6. Ordine di costruzione e validazione

### 6.1 Ordine

| Passo | Lavoro | Cosa si ottiene |
|---|---|---|
| — | interruttore nel plugin vecchio (decisione 11) | undercut e overcut spenti in dash e in voce |
| 0 | prova di fattibilità (§7.4) | scheletro del plugin nuovo in SimHub accanto al vecchio; l'adattatore legge iRacing e scrive un log |
| 1 | `Input` + `Cars` | identità per `CarIdx`, posizione con la sua qualità, dove si trova ogni vettura |
| 2 | `Track` + `Events` | geofence dal database; eventi di corsia, zona estesa, traguardo, piazzola, ricomparsa |
| 3 | `Timings` | giri, tempi di passaggio, cronometri dei box |
| 4 | `Gaps` + `Target` (scelta e distacco) | primo numero confrontabile col plugin vecchio in dash |
| 5 | `Fuel` | consumo del Player, stima BoP degli avversari |
| 6 | `Pace` / `TyreDeg` | passo normalizzato e degrado |
| 7 | `Calibration` + `PitLoss` | calibrazioni col consenso, perdita ai box di qualunque vettura |
| 8 | `Race` | leader, bandiera, giri totali, carburante da imbarcare |
| 9 | `MergeGap` | dove si rientra rispetto al Target |
| 10 | passaggio della dash (§7.5) | la dash legge il plugin nuovo |

Dopo, fuori da questo spec: undercut e overcut (Fase B), meteo, gomme e pressioni, voce, volante, profili.

Al passo 4 l'architettura è provata da cima a fondo. Al passo 8 il plugin nuovo ha già carburante, giri totali e
distacco, cioè tutta la strada A, prima del MergeGap: se il MergeGap si blocca, la versione ridotta è già fatta.

### 6.2 Verità di confronto e soglie

| Modulo | Verità di confronto | Soglia di partenza |
|---|---|---|
| `Cars` | superficie nativa e posizione nei log | nessuna posizione ferma segnata `Measured`; a Daytona `124637` van Elewout fermo a 0.1883 risulta `Held` |
| `Events` | flag nativo di corsia e soste note | ogni sosta del Player rilevata una volta, nessun ingresso fantasma (Y-33) |
| `Timings` | tempo del giro del gioco; sosta del Player misurata dal plugin vecchio (`094551`: 58.62 / 46.98 / 11.63 / 14.47 s) | giro entro ±0.05 s; componenti della sosta entro ±0.2 s |
| `Gaps` | differenza dei passaggi sul traguardo, esatta sulla linea | entro ±0.1 s sulla linea; nessun distacco `Measured` da posizione ferma |
| `Fuel` | livello carburante del Player | consumo per giro entro ±0.05 L; avversari da fissare nel brainstorming, confrontando col rifornimento osservato |
| `Pace` / `TyreDeg` | giri puliti misurati | passo entro ±0.3 s dalla mediana dei giri puliti dello stint; degrado da definire nel brainstorming |
| `PitLoss` | perdita reale della sosta osservata (Player a Daytona: 34.7 s) | previsione prima della sosta entro ±1 s |
| `Race` | giri reali a fine gara (Daytona: `Projection Validation`, vero = 27.742) | da fissare nel brainstorming |
| `MergeGap` | gap reale dopo le soste (Daytona: −2.7 s) | ±1 s prima delle soste (il plugin vecchio arriva a +0.33 s), ±1.5 s durante, ±1 s dopo |

Le soglie definitive e i riferimenti di Road Atlanta (`20260911_231106`) si fissano nel brainstorming di ogni modulo,
prima del codice.

### 6.3 Tetto per modulo

- **Tre cicli di validazione.** Un ciclo è: implementazione o correzione → replay sui due circuiti → confronto con la
  soglia.
- Al terzo ciclo fallito **ci si ferma e si decide con Andreas**: definizione sbagliata, dato non disponibile o soglia
  irrealistica. Mai una quarta correzione in automatico.
- Il passaggio alla strada A non è automatico: è una decisione.

### 6.4 Cosa si porta dal plugin vecchio

Ogni pezzo passa da una di tre porte: **portato** (validato e pulito), **riscritto** (doppio o sbagliato),
**abbandonato** (non usato). Candidati, da confermare nel brainstorming di ciascun modulo:

| Modulo | Candidati dal plugin vecchio | Probabile |
|---|---|---|
| `Cars` | `TrackPositionValidator` (teletrasporti) | portato |
| `Events` | `PitLaneDetector` (cascata di rilevamento) | da rivedere sul flag nativo |
| `Timings` | tempi di passaggio a 400 punti (`OpponentTracker`), `SectorTracker` (mediana dei transiti) | riscritti in un modulo unico per tutte le vetture |
| `Calibration` | `CalibrationConsensus`, `GeofenceCalibrationGate` | portati |
| `Fuel` | consumo robusto di `FuelManager`, regola BoP di `OpponentTracker` | riscritti in un modulo unico |
| `Race` | `RaceTimeProjection.ProjectFlagMoment` (criterio del massimo, Y-38), `LeaderPaceFilter` | portati |
| `PitLoss` | formule di `CarPitData` (`CalculateTotalPitLoss`), `PlayerPitSpeedObserver` | da verificare, poi portati |
| `MergeGap` | previsione della sosta del Target (`ForecastTargetPit`) | riscritta; i casi del passo 3 del piano Daytona (latch, `ApproachingPits`) diventano test |

Altra eredità: la conoscenza in `.ai/archive/CLOSED_POINTS.md` (41 punti), ADR-004 e ADR-005, i casi di test coi numeri
veri, replay e log in `Logs/`, il database delle calibrazioni. Non si eredita: la struttura per zone, le logiche doppie,
le 183 proprietà che nessuna dash usa, undercut e overcut finché la Fase B non li valida.

## 7. Convivenza e passaggio

### 7.1 Due plugin in SimHub

| | Plugin vecchio | Plugin nuovo |
|---|---|---|
| DLL | `User.PluginSdkDemo.dll` | `SimRIG.Remastered.*` (§8.2) |
| Classe principale (prefisso delle proprietà) | `DataPluginDemo` | `SimRigRemastered` fino al passaggio |
| Nome in SimHub | `SimRIG` | `SimRIG Remastered` |
| Impostazioni | `GeneralSettings` (`DataPluginDemo.cs:185`) | `SimRigRemasteredSettings` |
| Database delle calibrazioni | `SimRIG_Data.json` nella cartella di SimHub, letto e riscritto (`PitRadar.cs:297`, `:1100`, `:1286`) | `SimRIG_Remastered_Data.json`, copiato una volta dal vecchio; il file vecchio non si scrive mai |
| Log | `Logs\SimRig Logs\SimRIG_*_{data}` (`LogManager.cs:132-143`) | `Logs\SimRig Remastered\SimRIGR_<Modulo>_<data>.csv` |
| Dash | serve la dash di oggi | proprietà col suo prefisso, visibili in una pagina di prova |
| Voce, volante, pagina impostazioni | restano al vecchio | niente fino al passaggio: nessun annuncio doppio |

SimHub usa come prefisso delle proprietà il nome della classe del plugin: la dash legge `DataPluginDemo.SimRIG.*`
(255 riferimenti nel solo `Test.djson`).

### 7.2 Il plugin vecchio durante i lavori

- **Congelato:** niente correzioni, salvo guasti bloccanti e l'interruttore della decisione 11. Il piano correzioni
  Daytona (passi 3–5) e il fix di Y-62 sono sospesi; la diagnostica di `32a8682` resta per il confronto.
- **Interruttore alla fonte:** tiene a falso `UndercutViable`, `OvercutViable` e `TrafficAlert`. Con essi si spengono le
  spie in dash e i tre annunci che ne dipendono: `REPORT_UNDERCUT` (`DataPluginDemo.cs:1098-1100`),
  `AUTO_UNDERCUT_ALERT` (`:1599-1601`), `TARGET_ENTERING_PITS_OVERCUT` (`:1650-1652`). Non esisteva un'impostazione
  dedicata: `EnableVoiceEngineer` avrebbe zittito tutto l'ingegnere. Si fa col lock e con un test.

### 7.3 Log di confronto e script di validazione

- Un file CSV per modulo e per sessione, con colonne comuni: tempo crescente, tempo rimanente, giro, `CarIdx`, grandezza,
  valore, qualità, età (per `Held`), etichette.
- Righe a ogni evento e a campionamento regolare (per esempio una al secondo per vettura), regolabile per modulo.
- **Uno script di validazione per modulo nel repository** (`User.PluginSdkDemoRemastered/Validation/`): legge i log dei
  due circuiti e stampa il confronto con la soglia, coi numeri. Gli script di analisi del plugin vecchio restavano nella
  cartella temporanea delle sessioni e si perdevano.

### 7.4 Prova di fattibilità (passo 0)

Su un pezzo di replay Daytona deve dimostrare:

1. i due plugin caricati e attivi insieme, senza errori;
2. i progetti in formato SDK per .NET Framework 4.8 compilano con MSBuild di VS2022 e si installano in SimHub;
3. il plugin nuovo riceve i dati a ogni tick, col tempo di calcolo per tick nel log;
4. l'adattatore legge tempo crescente e rimanente; per ogni `CarIdx` posizione, superficie e corsia box; tempi sul
   giro; carburante del Player; dati di sessione (classi, BoP, nome per `CarIdx`);
5. nel log si vedono le vetture `NotInWorld` e i salti del replay, e il tempo rimanente coincide con i log vecchi.

Se i due plugin non convivono, il nucleo resta un progetto separato ma lo carica il plugin vecchio: il confine resta,
perché la libreria nuova non vede il codice vecchio.

### 7.5 Passaggio della dash (passo 10)

- **Quando:** moduli validati fino al passo 9, oppure fino all'8 se si decide la strada A.
- **Prima:** una pagina di prova con i numeri chiave affiancati, vecchio e nuovo (carburante da imbarcare, giri totali,
  distacco, MergeGap), guardata da Andreas in una sessione vera.
- **Come:** il plugin nuovo pubblica gli stessi nomi delle 116 proprietà usate dalla dash, con una tabella nel guscio
  "proprietà → modulo che la produce". Al passaggio la classe principale prende il nome `DataPluginDemo` e la DLL
  vecchia si toglie: la dash non si tocca. Le proprietà che il nuovo non produce restano vuote o si tolgono dalla dash.
- **Ritorno indietro:** la DLL vecchia resta disponibile, in Git e in una copia.

### 7.6 Qualità in dash

Per i numeri chiave, non per tutte le 116 proprietà, il guscio pubblica anche la qualità (misurato, tenuto, stimato, non
disponibile); la dash decide come mostrarla. Precedente nel plugin vecchio: `SimRIG.Session.IsLapsPredictionValid`
(Y-52). Quali sono i numeri chiave si decide nel brainstorming dei moduli che li producono.

## 8. Progetto e processo

### 8.1 Struttura e progetti

Spostato il 2026-09-15 in `.ai/ARCHITECTURE.md`, sezione "Plugin nuovo — SimRIG Remastered", "Cartelle e progetti".

### 8.2 Nomi (approvati)

Spostato il 2026-09-15 in `.ai/ARCHITECTURE.md`, sezione "Plugin nuovo — SimRIG Remastered", "Nomi".

### 8.3 Build e test

Spostato il 2026-09-15 in `AGENTS.md`, sezione "Build e test — plugin nuovo".

### 8.4 Lock e hook

Spostato il 2026-09-15 in `AGENTS.md`: il lock copre le due cartelle di codice e le riorganizzazioni di `.ai/` ("Prima di
toccare qualsiasi file di codice"), lo scope del lock di un modulo sta nel ciclo di un modulo, l'hook nel protocollo di
coworking, punto 5. Y-56 è fra le decisioni in sospeso di `.ai/PROJECT_STATE.md`.

### 8.5 File di progetto (confermato da Andreas il 2026-09-15, eseguito)

**Un solo insieme di file per tutto il repository, riorganizzato attorno al plugin nuovo.** Né copie parallele per il
Remastered, né sovrascritture che cancellano: il materiale del plugin vecchio si sposta in `.ai/archive/` parola per
parola, come nella potatura del 2026-09-05.

| File | Cosa diventa |
|---|---|
| `AGENTS.md` | resta l'unico file delle regole (ADR-006). Regole generali invariate (lock, Git, handoff, revisioni); build, test e trappole divisi fra plugin nuovo e plugin vecchio congelato; si aggiungono il ciclo di un modulo (§3.1) e le regole di semplicità (§3.2) |
| `CLAUDE.md`, `GEMINI.md` | restano puntatori ad `AGENTS.md` |
| `.ai/PROJECT_STATE.md` | stesso file e stesso lock. "Da dove partire" e "Stato corrente" riscritti per il plugin nuovo; tabella corta di avanzamento (passo, modulo, stato, commit). La tabella dei punti aperti del plugin vecchio va in archivio con una riga di rimando: ogni punto diventa materiale per il brainstorming del modulo che riguarda (Y-58 → `Cars`/`Race`, Y-59 → `MergeGap`, Y-60 → `Timings`, Y-62 → `Gaps`) |
| `.ai/HANDOFF_LOG.md` | stesso file e stesso modello; la cronologia continua e le voci del plugin vecchio escono da sole con la regola delle 10 |
| `.ai/ARCHITECTURE.md` | ADR-007 e mappa dei moduli del plugin nuovo in testa; la mappa di oggi in fondo come "plugin vecchio, congelato"; ADR-001…006 restano validi |
| `.ai/plans/2026-08-24-roadmap.md` | in archivio: la sostituisce l'ordine del §6.1, e l'avanzamento sta solo in `PROJECT_STATE.md`. I due comandi `new-session` (`.claude/commands/`, `.agent/workflows/`) puntano a questo spec |
| `.ai/STRATEGY_ENGINE_GUIDE.md` | descrive il plugin vecchio: resta con un avviso in testa, e si riscrive al passaggio della dash |
| `.ai/plans/<data>-remastered-<modulo>.md` | un file per modulo con la scheda approvata e i passi di implementazione |

**Perché non file nuovi per il Remastered:** due `AGENTS.md` o due lock andrebbero fuori sincrono, e il repository ne ha
già la prova (ADR-006, i conteggi ricopiati a mano). Con due lock, due agenti potrebbero scrivere insieme gli stessi file
di `.ai/`. È la stessa regola che vale per il codice: un'informazione, un posto.

**Eseguito il 2026-09-15**, con queste differenze dalla tabella:

- in archivio c'è un file solo per il plugin vecchio, `.ai/archive/PLUGIN_VECCHIO.md`: da `PROJECT_STATE.md` tutte le
  sezioni sotto le regole del lock, compresi l'indice dei punti chiusi, i debiti e i ruoli; da `NEW_SESSION_PROMPT.md` il
  blocco del 2026-08-31 sulle proiezioni di fine gara;
- "Stato corrente" è diventato la tabella "Avanzamento". In `PROJECT_STATE.md` c'è anche una tabella, oggi vuota, per i
  punti aperti del plugin nuovo: le regole di revisione di `AGENTS.md` registrano lì i difetti nuovi;
- la sezione "Ruoli" è sostituita da "Chi fa cosa" in `AGENTS.md`;
- in `ARCHITECTURE.md` la mappa del plugin nuovo è in testa e ADR-007 segue ADR-006; `Hardware/` è una sezione a sé,
  perché non riguarda un plugin solo;
- i comandi `new-session` sono tre: c'è anche la skill di Antigravity, `.agent/skills/new-session/SKILL.md`.

## 9. Domande aperte

| Domanda | Dove si decide |
|---|---|
| "Una vettura è una vettura" regge per calibrazioni guidate e rilevamento delle soste? | brainstorming di `Events` e `Calibration` |
| Il flag nativo di corsia box è affidabile come fonte degli eventi? | prova di fattibilità (lo registra nel log) e brainstorming di `Events` |
| Quali numeri chiave pubblicano anche la qualità in dash | brainstorming dei moduli che li producono |
| Soglie definitive e riferimenti di Road Atlanta | brainstorming di ogni modulo |
| Assetto Corsa o Assetto Corsa Competizione | quando arriva il secondo simulatore |
| Y-56: lock non pushato e `human` generico | Andreas e Michael |

## 10. Prossimi passi

L'avanzamento sta solo nella tabella "Avanzamento" di `.ai/PROJECT_STATE.md` (§8.5). L'elenco scritto con lo spec,
aggiornato un'ultima volta il 2026-09-15:

1. ✅ Andreas rivede questo spec e conferma il §8.5 — fatto il 2026-09-15.
2. ✅ Riorganizzazione dei file di progetto (§8.5), ADR-007, hook esteso alla cartella nuova — fatto il 2026-09-15.
3. Interruttore nel plugin vecchio, poi il passo 0.
4. Brainstorming del primo modulo con Andreas: `Input` + `Cars`.

In una nuova sessione basta il comando di avvio, che legge la tabella e riporta il prossimo passo:

```
/new-session
```
