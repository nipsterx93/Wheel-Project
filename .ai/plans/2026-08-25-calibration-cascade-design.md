# Disegno del flusso — Y-28, cascata di calibrazione guidata

> Documento di **disegno**, non di implementazione. Nessun codice scritto. Scopo: mettere per
> iscritto quanto deciso in chat il 24-25 agosto, perché l'utente lo riveda a mente fredda prima
> che si trasformi in lavoro.

---

## Principio guida

L'ingegnere vocale **guida** l'utente passo-passo, **osserva** quello che succede davvero, e **si
adatta**. Non chiede conferme rigide, non si aspetta che l'utente esegua un copione — riconosce
l'esito reale e avanza di conseguenza. Se l'utente sgarra, l'ingegnere si adatta a *lui*, mai il
contrario.

---

## 1. Quando la cascata è attiva

Due condizioni, entrambe necessarie:

- **Sessione**: né gara né qualifica — `!IsRaceSession && !IsQualySession`. Copre Practice e Test
  allo stesso modo, senza leggere `SessionTypeName`. In gara e qualifica l'ingegnere non parla mai
  di calibrazioni, punto.
- **Dati mancanti**: la combinazione Track+Class corrente non è `READY` secondo
  `BuildCalibrationStatus` (già esistente, non va reinventato — restituisce già l'elenco di cosa
  manca).

Se una delle due è falsa, l'ingegnere tace del tutto.

## 2. Prerequisito: un giro genuino prima di qualunque calibrazione

Non si accetta **nessun** dato di calibrazione — nemmeno il primo drive-through — prima che
`GeofenceCalibrationGate` segnali un tragitto genuino in pista. È lo stesso gate già usato per
autorizzare la scrittura delle geofence in gara (Fase 3): un ingresso ai box dalla griglia di
partenza o dalla piazzola non è un dato, è la posizione di partenza.

**Comportamento all'ingresso in sessione**: se il gate non è ancora autorizzato, l'unico messaggio
possibile è *"esci in pista e completa un giro, poi calibriamo"*. Nessuna istruzione sui passi
successivi finché questo non è vero.

## 3. La cascata

Tre famiglie di dati, verificate **indipendentemente** — non è un copione lineare rigido, è una
lista di priorità che salta quello che è già noto:

| ordine | dato | livello | azione richiesta all'utente |
|---|---|---|---|
| 1 | Geofence, `PitDriveThroughTime` | Track+Class | Un giro, poi un ingresso/uscita **senza fermarsi** (drive-through) |
| 2 | `PitTransitTime`, `PitInOutAccDecTime`, `FuelFillRate` | Track+Class + Class | Una sosta con **solo carburante**, qualunque quantità sopra ~10 L, gomme su `NONE` |
| 3 | `TyreChangeTime` + moltiplicatori 2/1 gomme | Class | Una sosta solo gomme con `All4`, poi una con 2 gomme, poi una con 1 |

**Perché quest'ordine e non un altro**: il passo 1 è quello che tutti gli altri passi producono
comunque come sottoprodotto — ogni ingresso/uscita in corsia box, qualunque sia lo scopo, è
un'osservazione di geofence. Metterlo per primo significa che a fine cascata la geofence avrà
ricevuto *almeno* tre osservazioni (una per passo), raggiungendo da sola il consenso a tre — è il
punto 9 della proposta originale, e resta il pezzo più elegante di questo disegno.

**Perché Track+Class e Class sono verificati separatamente**: `FuelFillRate` e `TyreChangeTime`
sono per **classe**, non per circuito. Se la classe è già calibrata da un altro tracciato, la
cascata su un circuito nuovo chiede **solo** il passo 1 (e la parte di transito del passo 2, non la
parte carburante). Corrisponde esattamente alla struttura del database già esistente
(`Tracks[]` per la coppia, `Classes[]` per la classe sola) — nessuna modifica allo schema per
questa parte.

## 4. Come si riconosce che un passo è riuscito — riusando quello che già esiste

Nessun passo si dichiara "riuscito" perché l'utente ha impostato un numero. Si dichiara riuscito
quando il **risultato osservato** supera le soglie già presenti nel codice:

- **Drive-through**: la visita ha superato il guard sulla traversata (Y-23, `HasTraversedPitLane`)
  e il guard temporale nuovo (punto 6) — vedi sotto. Nessun'altra condizione: non c'è nulla da
  "inviare" per un drive-through.
- **Carburante**: `ObserveNaturalPitStop` restituisce `FuelRateUsable = true`. Questo flag già
  incorpora — a codice invariato — che siano stati aggiunti almeno `MinLitresForFuelRate` litri in
  almeno `MinFuelingSeconds` secondi, misurati dalla variazione **reale** del serbatoio, mai dal
  valore impostato. Se torna `false`, l'ingegnere non dichiara successo: dice che la sosta non è
  bastata e resta sul passo 2.
- **Gomme**: `TyreTimeUsable = true`, stesso principio.

**Perché non serve sapere se il comando è stato inviato dal volante**: l'obiezione sollevata
dall'utente (impostare 20 L senza mai inviarli, e ottenere un rate assurdo) è già impossibile per
costruzione — verificato nel codice (`PitRadar.cs:994-1020`): il numeratore del calcolo è
`litresAdded`, la variazione reale del serbatoio, non il valore impostato. Il rischio reale non è
un dato sporco nel database (già protetto), è che l'ingegnere dichiari un successo che non c'è
stato. Si chiude guardando lo stesso `FuelRateUsable`/`TyreTimeUsable` che il codice già calcola,
non inventando un modo di tracciare l'origine del comando — cosa che, per come è fatto
`MacroManager` oggi, non è nemmeno tecnicamente possibile (nessun tag di provenienza su
`SendChatCommand`).

## 5. `TyreScope == None` resta una condizione hard per il passo 2

Confermato: il passo carburante richiede `TyreManager.CurrentScope == None` **al momento
dell'ingresso** in corsia box — esattamente la condizione già usata oggi per riconoscere
`CalibrationMode.SplashAndDash`. Non è una novità da costruire, è il comportamento esistente con la
sola soglia dei 20 litri esatti rilassata a "una quantità ragionevole".

## 6. Il guard sullo sfarfallio di `IsInPitLane` — due controlli complementari

Confermato nella discussione precedente, entrambi necessari perché coprono casi diversi:

- **Guard sulla distanza** (Y-23, già implementato): la visita deve aver percorso almeno una
  frazione minima di giro.
- **Guard temporale** (nuovo): la transizione deve durare almeno una soglia minima. All'inizio,
  prima che la geofence sia nota, una soglia fissa bassa (1-2 secondi) basta come rete di
  sicurezza — un transito vero dura 30+ secondi, quindi non c'è rischio di scartare un dato buono.
  Una volta che la geofence è consolidata, si può passare a `MinimumCredibleTransitSec` (già
  esistente, derivato da distanza e limite di velocità), molto più selettivo.

I due guard coprono casi che l'altro da solo lascerebbe passare: un guizzo abbastanza lungo da
superare la soglia di distanza su un circuito corto (es. Misano) viene comunque preso dal guard
temporale, e viceversa un guizzo abbastanza breve da superare quello temporale ma che copre
comunque una distanza minima resta preso dall'altro. Verificato con i numeri reali dello sfarfallio
di Daytona (0.2 s, 0.004 di giro) contro un transito vero (30+ s): il margine è ampio in entrambe
le direzioni.

## 7. Il limite di pit lane non è un passo della cascata

Confermato: si legge da `PitLimiterOn` (già disponibile in SimHub) mentre il limitatore è inserito,
non si deduce da un drive-through. È un'osservazione **ambientale**, continua, indipendente dai
passi della cascata e dalla sessione — funziona anche fuori dalla Practice. Non richiede nessuna
istruzione vocale dedicata.

## 8. Cosa fa l'ingegnere se l'utente devia

Non rifiuta, riconosce. Se l'ingegnere si aspetta un drive-through e l'utente invece fa il pieno,
il plugin osserva comunque un `Pit Complete` con le sue caratteristiche reali (modalità, litri,
tempo) e lo classifica per quello che è — esattamente come fa oggi con le soste in gara. La cascata
si limita ad aggiornare quale casella risulta ancora mancante, e a chiedere quella.

## 9. Insistenza e silenzio

- L'ingegnere ripete l'istruzione del passo corrente solo se **un giro è passato senza
  avanzamento** — non a tempo fisso. Se l'utente sta eseguendo, non lo interrompe; se si è
  distratto, lo richiama.
- A cascata completata: un messaggio sulla dash e un annuncio vocale di conferma, una volta sola.
- In una sessione successiva sulla stessa combinazione Track+Class, se tutto è già `READY`
  l'ingegnere non dice nulla fin dall'ingresso in pista.

---

## 10. Gomme a 2 e 1 — deciso il 25/08

Si misurano per differenza, non per assunzione. Sequenza obbligata: prima `All4` (è il denominatore,
senza non si può calcolare nulla), poi 2 gomme, poi 1 gomma. Ogni moltiplicatore si calcola dal
tempo misurato diviso il tempo `All4` già in database:

```
tempo_All4 = 27s        (misurato, passo 3a)
tempo_2gomme = 13s      (misurato, passo 3b)  →  moltiplicatore = 13/27 = 0.481
tempo_1gomma = 7s       (misurato, passo 3c)  →  moltiplicatore = 7/27  = 0.259
```

**Trattamento come le altre procedure guidate** (punto 4): si scrive `Confirmed` al primo tentativo
riuscito, stessa logica di `FuelFillRate`/`TyreChangeTime` — una procedura guidata è isolata per
costruzione, non serve consenso a tre passaggi.

**Una semplificazione che vale la pena rendere esplicita**: oggi il codice raggruppa già le
selezioni parziali in due sole categorie (`Fronts`/`Rears`/`Left`/`Right` → tutte allo stesso
moltiplicatore 0.5; le quattro singole ruote → tutte a 0.25). Il disegno misura **un
rappresentante per categoria**, non le quattro combinazioni separatamente — coerente con come il
codice le tratta oggi, non un cambio di granularità.

**Sul database**: servono due campi nuovi per classe (oggi c'è un solo `TyreChangeTime`, i
moltiplicatori sono costanti cablate). Devono nascere a **zero/non-calibrato**, così
`GetTireMultiplier` continua a usare 0.5/0.25 finché non arriva una misura vera — stesso principio
già applicato ovunque: il fallback resta, si sovrascrive solo con un dato migliore.

## 11. `PitDriveThroughTime`/`StopAndGo` — chiarimento (non era chiaro, colpa della spiegazione)

In parole più semplici di ieri: **questo punto non blocca nulla della cascata così com'è
disegnata.** La cascata chiede solo i dati che *mancano* — non tenta mai di riscrivere un dato che
esiste già, quindi il fatto che quei due campi accettino la scrittura una volta sola non la
riguarda.

Diventa rilevante solo in uno scenario diverso, che oggi **non fa parte** del disegno: se un giorno
volessi un modo per dire esplicitamente "quel drive-through l'ho fatto male, rifallo", oggi non
esiste — l'unico modo per correggere quei due campi è modificare il JSON a mano, come abbiamo fatto
noi durante i test. È un limite noto, non un problema per questo disegno; lo lascio scritto solo
perché non si perda per strada.

## 12. Cascata interrotta a metà — in realtà non c'è rischio da accettare

Ci ho pensato meglio dopo la tua risposta, e la conclusione è più netta di un compromesso: **con
questo disegno non esiste uno scenario in cui riprendere la cascata sovrascrive un dato buono con
uno peggiore.** Non serve accettarlo come rischio, perché non si verifica:

- Un passo scrive solo quando è **riuscito** (punto 4: `FuelRateUsable`/`TyreTimeUsable` veri, o
  traversata + guard temporale superati per il drive-through). Un tentativo abbandonato a metà non
  scrive nulla — non c'è un dato parziale da sovrascrivere.
- Geofence, tempo acc/dec e limite pit lane passano dal consenso (Y-20/Y-21): un singolo campione,
  anche se la sessione successiva lo aggiunge in un contesto diverso, non può scalzare un valore
  già consolidato — è esattamente lo scopo per cui il consenso esiste.
- I dati a scrittura singola guidata (`FuelFillRate`, `TyreChangeTime`, e i due moltiplicatori
  nuovi) sono affidabili al primo colpo **perché** derivano da una sosta già verificata pulita, non
  nonostante l'interruzione.

Quindi: riprendere da dove mancava, come proponevi, è corretto — e in più non richiede di accettare
alcun compromesso sulla qualità del dato.
