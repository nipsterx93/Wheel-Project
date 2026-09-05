# Motore strategico — guida in parole povere

> A cosa serve questo file: rispondere a "cosa stiamo combinando con tutte queste formule".
> Nessuna algebra. Per la verifica formale c'è `.ai/reviews/2026-08-18-strategy-engine-verification.md`.

---

## Cosa fa il plugin, in una frase

Guarda un avversario (il **Target**) e risponde a una domanda: **mi conviene fermarmi ai box prima
di lui, dopo di lui, o non cambia niente?**

---

## I quattro numeri che contano

| Numero | Cosa dice | Unità |
|--------|-----------|-------|
| **Gap** | Quanti secondi ci separano. Positivo = sono dietro, negativo = sono davanti | s |
| **PaceDeficit** | Chi ha il passo migliore, io o lui, considerando l'usura gomme | s/giro |
| **UndercutMargin** | Se mi fermo **prima** di lui, di quanto lo scavalco (o non lo scavalco) | s |
| **OvercutMargin** | Se mi fermo **dopo** di lui, stessa cosa | s |

Se un margine è **positivo**, quella strategia mi fa guadagnare la posizione. Il motore sceglie il
margine più alto fra i due, purché tutti i controlli di sicurezza siano soddisfatti (ho abbastanza
benzina? la gara è abbastanza lunga? all'uscita dai box trovo traffico?).

---

## Le due misure di ritmo, che sono diverse

Qui nasce metà della confusione. Ce ne sono **due**, con unità diverse:

| | `RelativePace` | `RelativeGapDelta` |
|---|---|---|
| Cosa misura | Ritmo relativo mediato | Variazione grezza del gap |
| Unità | **secondi al giro** | **secondi per macrosettore** |
| Filtrato? | Sì, media mobile + tetto a ±10 | No, valore puro |
| Stato | Legacy, tenuto per non rompere la dash | Nuovo, da usare |

Un macrosettore è **1/20 di giro** (~4.7 s a Misano). Quindi `-0.20s/sector` è molto più aggressivo
di `-0.20s/lap`: sono ordini di grandezza diversi. **Non vanno mai mostrati con la stessa etichetta.**

`RelativeGapDelta` va letto **solo** quando `RelativeGapDeltaValid` è vero. Quando è falso il numero
è l'ultima misura buona, ormai vecchia.

Nota importante: **nessuna delle due entra nelle decisioni di undercut/overcut.** Servono solo a te
per capire cosa sta succedendo. Le decisioni usano il passo (`NormalizedRaceStartPace` + usura) e il gap.

---

## Perché tanto lavoro sul RelativePace

Il calcolo è semplice: *guardo di quanto è cambiato il gap fra due macrosettori e lo proietto sul giro*.

Il problema è **quando quel confronto non ha senso**. Se fra le due misure c'è di mezzo una sosta ai
box, il gap cambia di 20 secondi per motivi che non c'entrano nulla col ritmo. Il numero che ne esce
è spazzatura.

L'insidia che ci ha impegnati: la posizione in pista **continua ad avanzare anche in corsia box**.
Quindi il codice "vedeva" una sequenza regolare di macrosettori e calcolava il ritmo come se nulla
fosse. Risultato: dopo ogni sosta il ritmo relativo restava incollato al valore massimo (±10 s/giro)
per mezzo giro — proprio nel momento in cui serve.

**Come è stato risolto:** appena qualcuno entra ai box si alza una bandierina. Alla riuscita si
scartano **3 macrosettori** (~14 s) prima di ricominciare a misurare, perché anche subito dopo
l'uscita la vettura sta ancora rientrando e accelerando.

---

## Cosa scrivono i log

Tre file in `E:\SimHub\Logs\SimRig Logs\`, uno per sessione:

| File | Cosa contiene |
|------|---------------|
| `SimRIG_StrategySnapshot_*.csv` | Fotografia completa ogni ~0.5 s. 63 colonne. Serve per i grafici |
| `SimRIG_StrategyEvent_*.txt` | Solo i momenti in cui **cambia qualcosa**. Serve per capire il perché |
| `SimRIG_MergeGapLog_*.txt` | Riepilogo leggibile ogni 10 s |

Entrambi i file strategy iniziano con i **parametri del modello** (`# RelativePaceAlpha=0.3`, ecc.),
così un log vecchio resta interpretabile anche se le costanti cambiano.

Formato di una riga evento:

```
sessionTimeLeft | lap | orologio | NOME_EVENTO | dettagli
```

Il **session time guida**, non l'orologio: è l'unica base temporale valida quando un replay viene
riprodotto accelerato, ed è la stessa chiave della prima colonna del CSV.

**I log strategici si scrivono solo a gara in corso** (`SessionState == 4`). Griglia, formazione,
pausa e post-bandiera producono dati che non descrivono nulla di strategico.

---

## Come leggere una sequenza di sosta

Questa è la prova che il fix funziona. Cercala dopo ogni pit:

```
RELATIVE_PACE_INVALIDATION  | reason=PlayerInPit          <- qualcuno è ai box
RELATIVE_PACE_POST_PIT_SEED | sectorsRemaining=2          <- assestamento
RELATIVE_PACE_POST_PIT_SEED | sectorsRemaining=1
RELATIVE_PACE_POST_PIT_SEED | sectorsRemaining=0
RELATIVE_PACE_SEED          | instantRate=1.895 | clamped=False   <- prima misura buona
```

**Campanello d'allarme:** se fra la prima e l'ultima riga compare un `RELATIVE_PACE_UPDATE`, oppure
se il primo `SEED` ha `clamped=True`, qualcosa è regredito.

---

## Cosa è stato fatto finora

| # | Problema | Stato |
|---|----------|-------|
| 1 | Il ritmo relativo veniva calcolato attraverso le soste | ✅ risolto, verificato in gara |
| 2 | Gli eventi salvavano solo il nome, senza i dati | ✅ risolto |
| 3 | I file di log non avevano intestazione | ✅ risolto (regge anche se svuoti la cartella) |
| 4 | Il ritmo saturava per mezzo giro dopo ogni sosta | ✅ risolto |
| 5 | Log scritti anche fuori dalla gara | ✅ risolto |
| 6 | Undercut/overcut oscillano decine di volte al minuto | ✅ risolto (Y-12), **verificato in gara: 375 → 23** |
| 7 | Campioni degeneri saturavano il ritmo a fine gara | ✅ risolto, verificato in gara |
| 8 | Il ritmo impazziva per un istante a ogni cambio giro | ✅ risolto, da confermare in gara |

---

## I tre controlli sul ritmo relativo

Il ritmo si calcola confrontando il gap fra due macrosettori consecutivi. Tre cose possono rendere
quel confronto una sciocchezza, e ognuna ha il suo controllo:

| Controllo | Scarta il campione se… | Perché |
|---|---|---|
| **Sosta** | c'è di mezzo un pit | Il gap cambia di 20 s per motivi che non sono il ritmo |
| **Tempo** | è passato meno di mezzo macrosettore o più del doppio | Un frammento di settore, o quattro settori persi contati come uno |
| **Ampiezza** | il gap è saltato di oltre mezzo giro | Al cambio giro il conto salta di un giro esatto e torna: non è ritmo, è aritmetica |

L'ultimo è il più insidioso perché **il tempo trascorso sembra normale**: 4 secondi, un macrosettore
perfetto. È il salto del gap a essere impossibile — 93 secondi, cioè esattamente un giro di Misano.
Senza quel controllo il ritmo schizzava a 2160 s/giro e restava al massimo per una decina di secondi
a ogni cambio giro.

---

## Perché la strategia non balla più

Il motore diceva "undercut / neutral / undercut" **375 volte in 40 minuti**, quasi sempre sullo
stesso avversario. Non era indecisione: erano due semafori piazzati esattamente dove il segnale
traballa di più.

Il gap fra due vetture non è un numero fermo. Fra una misura e l'altra si muove di circa 0.2 s solo
per come vengono campionati i dati. Se il semaforo scatta a −0.5 s e il gap balla di ±0.2 attorno
a −0.5, quel semaforo lampeggia in continuazione — pur non essendo successo niente in pista.

Le tre contromisure, tutte misurate sui dati veri e non scelte a occhio:

| Rimedio | Valore | A cosa serve |
|---|---|---|
| **Banda morta sulla posizione** | ±0.25 s | Il semaforo cambia solo se il gap si muove *davvero*, non per il tremolio |
| **Banda morta sui margini** | ±0.15 s | Stessa cosa per il "mi conviene o no" |
| **Permanenza minima** | 5 s | Una raccomandazione dura almeno 5 secondi, altrimenti non fai in tempo a leggerla |

Le prime due tolgono le oscillazioni **piccole**, la terza quelle **veloci**: metà duravano meno di
0.6 secondi. Sono complementari, non doppioni.

Attesa: da ~34 cambi al minuto a ~2. Se nel prossimo replay ne vedi ancora decine, qualcosa non ha
funzionato. La colonna `CandidateDecision` nello snapshot mostra cosa avrebbe detto il motore
*senza* la permanenza minima: confrontarla con `StrategyDecision` dice quanto sta filtrando.

**Una cosa non è cambiata:** le soglie sono le stesse di prima. Non abbiamo reso il motore più
prudente né più aggressivo — gli abbiamo solo tolto il tremolio. Un undercut che valeva la pena
prima, vale la pena anche adesso.

I punti in attesa di decisione sono elencati in `.ai/PROJECT_STATE.md`, ognuno con la domanda
precisa a cui rispondere. Nessun agente deve toccarli prima.

---

## Se apri una chat nuova

Una sessione nuova **non ricorda niente** delle precedenti. Quello che sopravvive è solo il
repository. Per ripartire, chiedi all'agente di leggere in quest'ordine:

1. `AGENTS.md` — regole operative, comandi di build e test (vale per ogni agente;
   `CLAUDE.md` e `GEMINI.md` sono puntatori a quello)
2. `.ai/PROJECT_STATE.md` — lock, debiti noti, punti congelati
3. `.ai/HANDOFF_LOG.md` — cosa è successo negli ultimi turni
4. **questo file** — il quadro d'insieme
5. `.ai/reviews/2026-08-18-strategy-engine-verification.md` — solo se serve il dettaglio formale
