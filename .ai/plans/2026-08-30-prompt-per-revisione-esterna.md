# Prompt per revisione esterna — proiezione dei giri di fine gara

> Da incollare a un modello esterno (Gemini Deep Research o equivalente) **senza** dargli accesso al
> repository. È volutamente autoconsistente: descrive il problema fisico e i vincoli, non il nostro
> codice. Serve a farci dire quali sono le formule corrette, non a farci validare le nostre.
>
> Contesto per chi legge qui dentro: dopo cinque giorni di correzioni successive
> (Y-31, Y-32, Y-35, Y-39, Y-41) la proiezione dei giri del Player è accurata, quella del leader
> assoluto no, e ogni correzione ne ha scoperta un'altra sotto. L'utente ha giustamente chiesto di
> fermarsi e far rivedere l'impianto da fuori invece di continuare a inseguire i sintomi.

---

## Il prompt

```
Devo calcolare, durante una gara automobilistica a tempo, due previsioni, e voglio che tu mi dia le
formule corrette e le insidie da evitare. Non ho bisogno che tu guardi del codice: descrivimi la
matematica giusta e i casi limite.

CONTESTO DELLA GARA

- Gara a tempo, non a giri: c'è un countdown di sessione. Quando il countdown arriva a zero la
  bandiera a scacchi NON esce subito: esce quando il leader assoluto taglia il traguardo la volta
  successiva. Da quel momento ogni altra vettura prende la bandiera al proprio successivo passaggio
  sul traguardo, e la sua gara finisce lì.
- Gara multiclasse: classi con prestazioni molto diverse (per esempio prototipi con giri da 69 s e
  vetture GT con giri da 77 s sullo stesso tracciato). Il leader assoluto appartiene quasi sempre a
  una classe diversa dalla mia.
- Le vetture fanno soste ai box: perdono tempo (transito in corsia box a velocità limitata più
  tempo da ferme) e ripartono con il serbatoio più pieno.
- La durata tipica è 45 minuti, circa 35 giri per la mia vettura e 39 per il leader.

I DUE NUMERI CHE MI SERVONO

(A) Per la MIA vettura: quanti giri avrò completato quando prendo la bandiera. Mi serve anche il
    valore con i decimali, cioè dove mi troverò nell'istante in cui la bandiera esce (esempio:
    34.83 significa che sono all'83% del mio 35° giro quando il leader taglia, quindi completerò 35
    giri). Questo numero determina quanto carburante devo imbarcare: sbagliarlo di un giro significa
    sbagliare il rifornimento di circa 2.3 litri, che può voler dire restare a secco o portare peso
    inutile.

(B) Per il LEADER ASSOLUTO: quanti giri completerà lui, con i decimali. Serve sia da mostrare al
    pilota sia perché determina QUANDO esce la bandiera, e quindi entra nel calcolo (A).

DATI CHE HO DISPONIBILI, A OGNI ISTANTE

- Countdown di sessione, in secondi.
- Per ogni vettura in pista: giri completati (intero), posizione sul giro come frazione fra 0 e 1,
  tempo dell'ultimo giro completato fornito dal simulatore, classe, se è in corsia box.
- Storico dei tempi sul giro di ogni vettura, con la possibilità di calcolarne medie mobili.
- Stima del carburante a bordo di ogni vettura e capienza del serbatoio.
- Tempo tipico perso in una sosta.

Nota importante: campiono questi dati a intervalli discreti (nel caso peggiore ogni 3 secondi di
tempo di gara), quindi so quando una vettura ha tagliato il traguardo solo con quella precisione.

LE DOMANDE

1. Qual è la formula corretta per (B), i giri totali del leader? Il mio primo istinto era
   "tempo totale di gara diviso tempo sul giro", ma sospetto sia troppo ingenua. Come si tratta
   correttamente il fatto che il leader debba completare il giro in corso quando scade il
   countdown? E come si incorpora il tempo che perderà nelle soste che gli restano da fare?

2. Qual è la formula corretta per (A)? In particolare: come si passa da "quando esce la bandiera"
   a "dove sarò io in quel momento", tenendo conto che io e il leader abbiamo passi diversi e che
   io potrei dovermi ancora fermare ai box mentre lui no (o viceversa)?

3. QUALE TEMPO SUL GIRO va usato in queste proiezioni? Ho a disposizione:
   - il tempo grezzo dell'ultimo giro;
   - una media mobile degli ultimi N giri grezzi;
   - un tempo "normalizzato", cioè il giro grezzo a cui ho sottratto una penalità proporzionale al
     carburante a bordo (circa 0.03 s per litro) e una penalità per la temperatura pista, per
     ottenere il tempo che quella vettura farebbe a serbatoio scarico e in condizioni di
     riferimento.
   Il tempo normalizzato è più veloce del reale del 2-3% sulle vetture con serbatoi grandi.
   Intuitivamente proiettare il futuro con un tempo "a serbatoio scarico" mi sembra sbagliato,
   perché nel tempo che resta la vettura la benzina la porterà davvero — ma vorrei una risposta
   ragionata, non la mia intuizione. Esiste un approccio migliore di entrambi, per esempio
   modellare esplicitamente l'alleggerimento progressivo lungo lo stint?

4. Come si costruisce una stima del passo che sia robusta ma non lenta a reagire? Devo scartare
   i giri non rappresentativi (giro d'uscita dai box, giro d'ingresso, giri sotto bandiera gialla,
   giri con traffico) e allo stesso tempo accorgermi in fretta se una vettura ha davvero cambiato
   passo (gomme nuove, pioggia, danni). Quali sono i criteri statistici corretti? Quanti giri di
   finestra? Mediana o media? E soprattutto: come si evita la trappola in cui una prima stima
   sbagliata definisce il criterio di validità che poi rifiuta tutti i dati corretti successivi?

5. La mia stima dei giri totali oscilla e devo stabilizzarla per non mostrare un numero che salta.
   Che tipo di filtro è corretto qui? Ho usato un'isteresi asimmetrica (sale facilmente, scende
   solo se il valore crolla di più di un giro) e mi si è bloccata su un valore sbagliato per
   un'intera gara. Qual è l'approccio giusto per stabilizzare una stima che deve poter correggersi?

6. Il leader assoluto cambia identità durante la gara (sorpassi, soste, ritiri). Ogni cambio
   porta con sé un passo diverso. Come si gestisce questa discontinuità senza che la proiezione
   salti a ogni cambio, e senza introdurre un ritardo che la renda inutile?

7. Quali errori sistematici sono tipici in questo tipo di calcolo e quali sono i controlli di
   sanità che dovrei avere? Mi interessa soprattutto il genere di errore che produce un numero
   plausibile e stabile ma sbagliato, perché è quello che non si nota guardando la dashboard.

8. Come si struttura la validazione di un calcolo del genere? Ho registrazioni complete di gare
   con l'esito reale noto: che metriche dovrei estrarre per dire "questa proiezione è corretta"
   invece di limitarmi a controllare il valore finale, che per costruzione converge sempre a
   quello giusto quando il tempo residuo va a zero?

Rispondi con le formule, le condizioni al contorno e i casi limite. Se ci sono approcci standard
noti nel motorsport o nella letteratura di race strategy, citali.
```

---

## Cosa NON chiedere a Gemini

Da tenere presente quando arriva la risposta: alcune cose le sappiamo già misurate e non vanno
rimesse in discussione sulla base di un'opinione generica.

- Il tempo sul giro degli avversari **si legge dal simulatore**, non si cronometra a campionamento.
  Deciso e misurato (Y-32, Y-39).
- La capienza del serbatoio è **della vettura**, non della classe (Y-41).
- Il totale del leader **converge sempre al valore giusto a fine gara**, qualunque sia l'errore:
  la parte proiettata si riduce a zero. Quindi "il numero finale è corretto" non è mai una prova
  che il calcolo sia giusto — è la domanda 8 del prompt.
