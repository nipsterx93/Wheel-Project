# Design & Decisioni: Seeding Metriche Gara e Strategia Carburante

- **Data:** 2026-09-06
- **Autori / Partecipanti:** Andreas & antigravity
- **Riferimento:** Roadmap Y-52 (Passi 2, 3 e 4) / Transizione YAML -> Live / Fuel Logic

---

## 1. Decisioni Concordate con l'Utente

### Decisione 1: Griglia di Partenza (Pre-Gara)
- **Scelta:** **Nessun calcolo anticipato in griglia.**
- **Motivazione:** Prima del semaforo verde (`SessionStateStatus < 4` o `SessionTimeLeft = -1`), i timer di iRacing non sono stabili (possono indicare countdown di griglia o `-1`).
- **Comportamento:** Il motore di calcolo delle metriche di gara (`RemainingLaps`, `RaceTotalLaps`, `TimeUntilLeaderCheckered`) si attiva in modo pulito e affidabile **dal semaforo verde** in poi, senza logiche acrobatiche o ripieghi forzati prima del via.

---

### Decisione 2: Transizione YAML -> Live e Giri Iniziali Più Lenti
- **Domanda dell'utente:** Il codice accetta tempi sul giro più alti rispetto a `PlayerEstimatedPaceSec` (es. 78s contro 77.05s stimati da YAML) dovuti a bagarre, traffico e partenza?
- **Comportamento verificato a codice:**
  1. **Esclusione giro di lancio:** In `RaceAnalyzer.cs:1265` la baseline esclude esplicitamente il primo giro (`if (raceLapsCompleted < 2) isValidForBaseline = false;`) perché il giro di partenza comprende lo spunto da fermo/rolling restart e pneumatici freddi.
  2. **Normalizzazione del carburante:** Quando al giro 2 viene stabilita la baseline, il tempo viene normalizzato sottraendo la penalità per il pieno di carburante e la temperatura tracciato:
     $$\text{normalizedLap} = \text{lapTime} - \text{fuelPenalty} - \text{tempPenalty}$$
     Un tempo grezzo di 78.2s a pieno carico corrisponde a circa 77.1s a serbatoio scarico.
  3. **Aggiornamento a scendere:** Se nei giri successivi il pilota abbassa i tempi (es. pista libera), `LapBaseline` si aggiorna immediatamente al miglioramento (`else if (normalizedLap < LapBaseline)`).
  4. **Gerarchia delle fonti (ADR-005):**
     $$\text{Baseline Normalizzata Live} \succ \text{Best Lap Live} \succ \text{YAML Estimated Pace} \succ \text{Ripiego Fisico}$$

---

### Decisione 3: Opponent Tracker
- Utilizzo di `DriverEstimatedPaceSec` (e `ClassEstimatedPaceSec`) estratti da `SessionDataReader` per inizializzare il passo atteso degli avversari fin dal primo giro.
- Il BoP serbatoio (`CarClassMaxFuelPct`, es. GTP 65%, LMP2 80%, GT3 50%) è già operativo per impostare la capienza massima reale di ogni rivale.

---

### Decisione 4: Carburante (`FuelFillRate` e `FuelToAdd`)
- **`FuelFillRate`:** Letto direttamente da `SimRIG_Data.json` (`ClassRecord.FuelFillRate`). Se assente, fallback a 2.7 L/s fino alla prima sosta calibrata.
- **`AverageFuelPerLap`:** Lo YAML **non contiene** il consumo al giro.
- **`FuelToAdd`:** **Freeze nel Giro 1.** Come richiesto esplicitamente da Andreas: *"Preferisco nessun dato piuttosto che dati sostitutivi e non precisi"*.
  - Nel Giro 1: `Calculations.IsPredictionValid = false`, `FuelToAdd = 0.0`.
  - Al completamento del Giro 1: viene registrato il primo consumo reale in telemetria, `AverageFuelPerLap` si popola, `IsPredictionValid` diventa `true` e `FuelToAdd` calcola il fabbisogno esatto sui `RemainingLaps`.
