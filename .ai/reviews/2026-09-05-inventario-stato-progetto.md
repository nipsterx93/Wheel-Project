# Inventario e stato del progetto — sessione di ingresso a freddo

- **Data:** 2026-09-05
- **Reviewer:** claude (sessione nuova, nessuna memoria delle precedenti)
- **Tipo:** review di sola lettura — **nessun file di codice toccato**, lock non preso (regola 2 di
  "Sessioni di revisione" in `CLAUDE.md`)
- **Metodo:** lettura diretta di `.ai/` nell'ordine prescritto, poi misure sul filesystem
  (conteggi righe, confronto disco↔`<Compile>`, grep). Ogni numero qui sotto è **misurato oggi**,
  non ripreso da un documento.

## ⚠️ Limite dichiarato di questa sessione

Gira su **macOS**, non su Windows. Conseguenze da tenere presenti leggendo tutto il resto:

- **Non ho compilato** (serve MSBuild/VS2022 + `SIMHUB_INSTALL_PATH`) e **non ho eseguito i test**.
  Nessuna affermazione qui dipende da una build.
- **Non ho accesso a `Logs/`** (gitignored, solo sulla macchina dell'utente). Tutti i numeri dei
  replay citati in `PROJECT_STATE.md` — `56.409`, `0.0737`, `93.033` — sono **non verificati in
  questa sessione**. Non li contesto: dichiaro solo che non li ho potuti controllare.
- Quello che ho potuto verificare è **ciò che è nel repository**: struttura, inclusioni nei progetti,
  coerenza interna dei documenti.

---

## 1. Cos'è il progetto, in due mondi

Il nome "Wheel Project" copre due cose che condividono il rig ma non il codice:

**A. Il plugin SimHub** (`User.PluginSdkDemoEdit/`, C# .NET Framework 4.8 + WPF) — è il 95% del
lavoro. Fa da **ingegnere di pista virtuale**: guarda un avversario (il *Target*) e risponde a
"mi conviene fermarmi prima di lui, dopo di lui, o non cambia nulla?" (`STRATEGY_ENGINE_GUIDE.md`).
Attorno a quella domanda: carburante, gomme, tracking avversari, proiezione dei giri a fine gara,
calibrazione automatica dei dati di pista, annunci vocali via Piper TTS.

**B. Il firmware del volante** (`Hardware/`, Arduino) — due sketch indipendenti:
- `Firmware INPUT/V2_8_2/V2_8_2.ino` (465 righe) — Arduino Leonardo + MCP23017, 80 pulsanti HID,
  encoder, rotary a 11 posizioni, paddle frizione con deadzone e bite point, calibrazione in EEPROM.
- `Firmware LED/V1_5_7_StartupLeds/V1_5_7_StartupLeds.ino` (521 righe).

Il ponte fra i due mondi è `SimRigHardwareManager.cs` (714 righe, SharpDX.DirectInput): il plugin
legge i pulsanti del volante come periferica DirectInput.

## 2. Stato misurato oggi

| Misura | Valore | Come l'ho ottenuto |
|---|---|---|
| Righe C# compilate (progetto principale) | ~21 100 | `wc -l` meno i tre `_LEGACY` |
| File `.cs` più grandi | `DataPluginDemo.cs` 2661, `OpponentTracker.cs` 2485, `RaceAnalyzer.cs` 2219, `SettingsControlDemo.xaml.cs` 2061, `PitRadar.cs` 1836 | `wc -l` |
| File di test | 40 su disco, **40 elencati** nel `<Compile>` — nessun orfano | `comm` disco↔csproj |
| Classi di test invocate da `TestRunner` | 36 `RunAllTests()`, **tutte le classi coperte** | grep incrociato per nome classe |
| Metodi di test | ~294 | grep `static void Test*` |
| Punti `Y-NN` chiusi | **39** | `grep -c '^| ~~Y-'` |
| Punti `Y-NN` aperti | **9** — Y-14, Y-15, Y-26 (parziale), Y-29, Y-33, Y-36, Y-38, Y-40, Y-52 (1/4) | `grep '^| Y-'` |
| `TODO`/`FIXME`/`HACK` nel codice compilato | **0** | grep |

**Impressione onesta, dichiarata come tale:** la disciplina di questo repo è molto sopra la media.
ADR-004 (un fix non è chiuso finché il test non fallisce disattivandolo) e ADR-005 (un campione
singolo non è una misura) non sono buoni propositi: si vedono applicati nelle voci `Y-NN`, con il
numero reale del log dentro il caso di regressione. Le cinque osservazioni sotto sono dettagli su
un impianto che regge, non crepe strutturali.

---

## 3. Cinque discrepanze trovate

Registrate qui secondo il punto 3 del protocollo di review: **nessuna corretta in questo turno**.
Le prime due sono difetti veri, le altre tre sono documentazione che ha smesso di dire il vero.

### R-1 — `PaddleClutch.h` manca dal repository: il firmware INPUT non compila da clone pulito

`Hardware/Firmware INPUT/V2_8_2/V2_8_2.ino:11` fa `#include "PaddleClutch.h"`, ma la cartella
contiene **solo il `.ino`**:

```
Hardware/Firmware INPUT/compile_and_upload.py
Hardware/Firmware INPUT/V2_8_2/V2_8_2.ino
Hardware/Firmware LED/V1_5_7_StartupLeds/V1_5_7_StartupLeds.ino
```

`PaddleClutch.h` non è in `.gitignore` (che ignora solo `Logs/` e gli artefatti .NET): non è
un'esclusione deliberata, è un file mai committato. `PaddleClutchManager` governa deadzone, bite
point e modalità della frizione — non è un accessorio.

**Impatto:** chi clona non può ricompilare il firmware del volante. Il codice sorgente di quella
logica esiste solo sulla macchina dell'utente, senza rete di sicurezza Git — cioè esattamente la
condizione che ADR-001 identifica come rischio principale, applicata però all'hardware invece che
al plugin.

**Da verificare con l'utente:** il file esiste in locale? Se sì va committato; se è una libreria
di terzi, va documentata la provenienza.

### R-2 — Il backtest sul replay reale si auto-salta in silenzio, con path assoluto hardcoded

`User.PluginSdkDemo.Tests/IntegrationTests/MisanoHuracanGT3ReplayTest.cs:20`:

```csharp
string replayPath = @"E:\SimHub\Replays\IRacing\20260303_151035.telemetry.json";
```

e `:26-30`, se il file non c'è:

```csharp
if (frames.Count == 0)
{
    Console.WriteLine("[!] No frames loaded from replay file. Skipping full replay backtest.");
    return;
}
```

Due problemi distinti:

1. **Viola una convenzione esplicita del progetto** — `CLAUDE.md` e ADR "Convenzioni": *"Reference
   esterne via `$(SIMHUB_INSTALL_PATH)`. Mai nuovi path assoluti hardcoded."* È l'unico path
   assoluto rimasto nel codice compilato (gli altri due hit di grep sono un commento e una stringa
   di test deliberata in `SessionMetadataUnitTests.cs:216`).
2. **Salta senza fallire.** Su qualunque macchina che non sia quella dell'utente, il runner stampa
   verde e chiude con exit `0` — ma la validazione end-to-end sul replay **non è girata**. Il
   conteggio "295 PASS" non distingue i due casi. È il pattern che ADR-004 combatte (un test che
   resta verde qualunque cosa faccia il codice), spostato dal contenuto del test alla sua assenza.

**Rimedio minimo, non applicato:** far derivare il path da `SIMHUB_INSTALL_PATH` e stampare uno
`SKIPPED` contato separatamente dai `PASS`, così il numero finale dice quanta copertura è davvero
girata.

### R-3 — `CustomDialog.xaml.cs` è su disco ma non compilato, e non è documentato

Confronto disco↔`<Compile>` del `.csproj` principale — quattro `.cs` presenti ma esclusi:

```
CustomDialog.xaml.cs        ← non documentato
DataPluginDemo_LEGACY.cs    ← documentato
FuelCalculator_LEGACY.cs    ← documentato
PitStrategyManager_LEGACY.cs ← documentato
```

`CLAUDE.md` ("Trappole di questo repo") elenca **solo i tre `_LEGACY`**. `CustomDialog` non compare
da nessuna parte nel `.csproj` — né come `Compile` né come `Page` per lo XAML. È quindi la stessa
trappola descritta per i `_LEGACY` (modificarlo non ha alcun effetto) ma senza il cartello che
avverte, e senza il suffisso `_LEGACY` che la renderebbe evidente dal nome.

### R-4 — ADR-003 dice che il progetto di test non è nella solution: non è più vero

`ARCHITECTURE.md:160`, fra le conseguenze di ADR-003: *"Il progetto di test **non è nella
solution**: va buildato esplicitamente"*. Ma `User.PluginSdkDemo.sln` contiene entrambi i progetti:

```
"User.PluginSdkDemo", "User.PluginSdkDemo.csproj"
"User.PluginSdkDemo.Tests", "User.PluginSdkDemo.Tests\User.PluginSdkDemo.Tests.csproj"
```

`CLAUDE.md` è aggiornato e dice il contrario (*"è incluso nella solution ... viene compilato
automaticamente"*). Un ADR accettato non si modifica (regola del formato in `ARCHITECTURE.md`), ma
una **nota di aggiornamento** sulla conseguenza superata evita che il prossimo agente segua
l'istruzione sbagliata.

### R-5 — I numeri riassuntivi in testa a `PROJECT_STATE.md` sono fermi al 24 agosto

Due frasi nella sezione "Da dove partire" — che è **la prima cosa che legge chi arriva**:

| Riga | Dice | Misurato oggi |
|---|---|---|
| `PROJECT_STATE.md:35` | "30 punti aperti, di cui 26 chiusi" | 48 voci, **39 chiuse**, 9 aperte (ID fino a Y-52) |
| `PROJECT_STATE.md:52` | "**186 test PASS** (erano 111 al setup)" | l'handoff del 2026-09-05 dichiara **295 PASS** |

La tabella sotto è aggiornatissima; è solo il cappello introduttivo a non essere stato risincronizzato.
Effetto pratico: chi entra si fa un'idea sbagliata della scala del lavoro fatto, e potrebbe usare
"186" come baseline per capire se una build ha rotto qualcosa.

---

## 4. Domande aperte per l'utente

1. **`PaddleClutch.h` (R-1)** — esiste in locale? È tuo o di terzi?
2. **Il firmware è in scope per gli agenti AI?** `.ai/` e `CLAUDE.md` parlano **solo** del plugin C#.
   Il protocollo del lock si applica anche a `Hardware/`, o quella cartella è territorio tuo?
3. **Chi tiene il conteggio dei test?** Se il numero atteso resta scritto solo negli handoff, va
   fuori sincrono a ogni turno (è già successo, R-5). Vale la pena farlo stampare al runner come
   riga finale confrontabile?
4. **I due sketch Arduino condividono il rig ma nessun documento.** Serve una mappa dell'hardware
   (pin, indirizzo I2C, mappatura pulsanti→azioni SimHub) al livello di `ARCHITECTURE.md`, o la
   tieni a mente?

## 5. Cosa NON ho fatto, deliberatamente

- Non ho preso il lock (`owner: NONE` al momento della lettura, lasciato tale).
- Non ho toccato un solo file di codice, né firmware.
- Non ho corretto R-1…R-5: registrate qui e in `PROJECT_STATE.md`, per essere implementate in un
  turno con lock, secondo il punto 3 del protocollo di review.
- Non ho contestato né confermato alcun numero dei replay: non ho i log (vedi limite dichiarato).
