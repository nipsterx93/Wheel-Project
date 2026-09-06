<!-- Copia gemella: .agent/skills/motorsport-telemetry-engineering/SKILL.md (stesso contenuto,
     percorso diverso perché Claude Code e Antigravity cercano le skill di progetto in cartelle
     diverse). Se modifichi questo file, aggiorna anche l'altro. -->
---
name: motorsport-telemetry-engineering
description: Use when implementing or reviewing fuel consumption, pit stop timing, pace/degradation filtering, or race-end checkered-flag projection logic in sim-racing telemetry or strategy plugins (iRacing, ACC, rFactor 2, SimHub).
---

# Motorsport Telemetry Engineering

## Overview

Formule e tecniche di riferimento per la telemetria strategica del sim-racing: fisica del
carburante, scomposizione dei tempi di sosta, statistica robusta su campioni radi, filtraggio di
passo/degrado. Fonte: ricerca approfondita generale sul motorsport engineering, **verificata
contro le misure reali già fatte in questo progetto** — dove le due cose divergono, vince la
misura di questo progetto, non la teoria generale.

## ⚠️ Prima di usare qualsiasi cosa qui dentro: un errore già misurato

Per la stima di **quando uscirà la bandiera a scacchi** in una gara a tempo con identità del
leader assoluto instabile (multiclasse, sorpassi, soste), la letteratura generale suggerisce il
**minimo tempo di attraversamento fra tutti i contendenti in lotta per il giro** ("chi taglia
per primo dopo lo scadere è il leader"). **Questo criterio è stato provato in questo progetto e
smentito da dati reali** (punto Y-38, replay `20260901_175019`, 758 campioni): dà una mediana di
**5.2 s** di supplemento sul cronometro, contro i **~50.6 s** corretti — sbagliato perché il
minimo pesca sistematicamente una vettura che sta chiudendo il giro *precedente*, non quella che
determina la fine gara.

Il criterio corretto, in uso in `RaceTimeProjection.cs` (`ProjectFlagMoment`): il **massimo**
della posizione proiettata allo scadere del cronometro fra tutte le vetture valutabili — cioè chi
sarà **al comando** quel momento, non chi arriva prima al traguardo in astratto. Vedi i commenti
in quel file per il ragionamento completo e il confronto numerico fra i due criteri.

Non riproporre il criterio del minimo. Se un'altra fonte lo consiglia, è una fonte che non ha
questo dato di replay.

## Quando si applica

- Si sta scrivendo o rivedendo codice di **proiezione fine gara** (giri rimanenti, tempo alla
  bandiera) in una gara a tempo.
- Si sta calcolando **quanto costa una sosta** o **scomponendo una sosta osservata** in
  rifornimento/gomme/riparazione dalla sola telemetria (velocità, livello carburante, tempo).
- Si sta stimando il **consumo di carburante** nei primi giri di uno stint, quando i dati sono
  pochi o rumorosi.
- Si sta filtrando una serie temporale di **tempi sul giro o passo relativo** contaminata da
  soste, bandiere gialle, o traffico.
- Si sta validando un **dato calibrato** (tempo gomme, limite corsia box, geofence pit) contro
  osservazioni ripetute scarse (3-9 campioni).

## Riferimento rapido

| Argomento | File | Stato in questo progetto |
|---|---|---|
| Fisica carburante (densità, fuel effect, normalizzazione, stima consumo) | `fuel-physics.md` | Coerente con `RaceTimeProjection.cs` (Y-43) |
| Scomposizione sosta (fuel/gomme/riparazione da flow-rate) | `pit-stop-decomposition.md` | Tecnica nuova, si aggancia al punto congelato Y-14 |
| Statistica robusta su campioni radi (Hampel/MAD, consenso) | `robust-statistics.md` | Il progetto usa già una mediana semplice (`CalibrationConsensus.cs`); qui il possibile affinamento |
| Proiezione fine gara / bandiera a scacchi | *(sopra, in questo file)* | ⚠️ Divergenza nota — vedi warning |
| Filtro Alpha-Beta, CUSUM (change-point) | `future-techniques.md` | Non implementate, nessun punto Y aperto le richiede oggi |

## Errori comuni

- **Applicare una formula di questa skill senza controllare se il progetto ha già una misura
  reale che la contraddice.** Il warning sopra è l'esempio noto; se ne trovi un altro, registralo
  come nuovo punto in `.ai/PROJECT_STATE.md`, non correggerlo silenziosamente.
- **Trattare `future-techniques.md` come lavoro da fare.** Sono tecniche plausibili, non un
  backlog: si implementano solo se un punto Y aperto lo richiede davvero (YAGNI).
- **Decidere Y-14 al posto dell'utente.** `pit-stop-decomposition.md` documenta la tecnica; la
  scelta fra i due rami (Sequential/Simultaneous) e come integrarla resta una decisione di
  prodotto congelata in `PROJECT_STATE.md`.
