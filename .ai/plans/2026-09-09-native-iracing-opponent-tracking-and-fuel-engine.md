# Motore Carburante Opponents, Telemetria Nativa iRacing, Scorporo Pit Loss e Stabilizzazione Gap

Sostituzione delle euristiche spaziali/velocità con i vettori di telemetria nativi di iRacing (`CarIdxOnPitRoad`, `CarIdxTrackSurface`, `CarIdxPitStopCount`, `CarIdxClassPosition`, `CarIdxLapDistPct`), applicazione delle 4 regole sul carburante opponents, scorporo del tempo pit zone e stabilizzazione del gap/MergeGap.

## User Review Required

> [!IMPORTANT]
> **Vincoli Chiave Convalidati con l'Utente:**
> 1. **Consumo Carburante:** Il calcolo proporzionato sulla mediana del Player viene attivato **soltanto dal giro 3 completato** (`CompletedLaps >= 3`, ovvero `CurrentLap >= 4`), garantendo almeno 3 valori validi.
> 2. **Cronometro Box (`TempoStationary`):** Misura direttamente l'intervallo in cui `CarIdxTrackSurface == InPitStall` (valore 1). Il rilascio dallo stallo chiude il cronometro.
> 3. **Scorporo Pit Loss:** $\text{RawExtendedPitZoneTime} = \text{TempoMisurato} - \text{TempoStationary}$. Il tempo di sollevamento/dead time empirico è isolabile confrontando $\text{TempoStationary} - (\text{FuelToAdd} / \text{RefuelRate})$.
> 4. **Smart Refueling:** All'ingresso box non si assume il 100% di pieno, ma il carburante necessario per finire la gara ($\text{FuelToAdd} = \min(\text{MaxTank} - \text{FuelResiduo}, \text{FuelNeeded} - \text{FuelResiduo})$). Se il carburante post-sosta copre la gara, $\text{TargetNeedsPit} = \text{false}$.
> 5. **Calibrazione Geofence:** Le transizioni di `CarIdxOnPitRoad` (`false -> true` e `true -> false`) registrano e raffinano `PitEntryPct` e `PitExitPct` in `PitRadar`.
> 6. **Gap e Posizione di Classe:** Eliminazione del wrapping $\pm 0.5$ in `WrapLapDifference` per bersagli distanti (che causava l'inversione di segno avanti/dietro) e utilizzo diretto di `CarIdxClassPosition`.

## Proposed Changes

### Telemetria Nativa e Mappatura

#### [NEW] [IracingTelemetryBridge.cs](file:///c:/Users/Andreas/Desktop/The%20Wheel%20Project/Antigravity2.0/User.PluginSdkDemoEdit/IracingTelemetryBridge.cs)
- Ponte ad alte prestazioni (zero allocazioni nel tick 60Hz) che estrae da `GameData.NewData.GetRawDataObject()` i riferimenti agli array:
  - `bool[] CarIdxOnPitRoad`
  - `iRacingSDK.TrackLocation[] CarIdxTrackSurface` (esposto anche come enum interno `IracingTrackSurface`)
  - `int[] CarIdxPitStopCount`
  - `int[] CarIdxClassPosition`
  - `float[] CarIdxLapDistPct`
- Espone metodi helper `TryGetCarTelemetry(int carIdx, out ...)` e stato `IsAvailable`.

#### [MODIFY] [SessionDataReader.cs](file:///c:/Users/Andreas/Desktop/The%20Wheel%20Project/Antigravity2.0/User.PluginSdkDemoEdit/SessionDataReader.cs)
- Durante la scansione di `DriverInfo.Drivers`, popola la mappa bidirezionale `CarIdxByUserName` e `UserNameByCarIdx` in `SessionMetadata`, consentendo la risoluzione immediata di `CarIdx` per ogni pilota.

---

### Motore Carburante e Tracciamento Opponents

#### [MODIFY] [OpponentTracker.cs](file:///c:/Users/Andreas/Desktop/The%20Wheel%20Project/Antigravity2.0/User.PluginSdkDemoEdit/OpponentTracker.cs)
- **Aggiornamento Dati Nativo:** A ogni tick, risolve `carIdx` dell'avversario tramite `opp.Name`:
  - Se `IracingTelemetryBridge.IsAvailable`:
    - `tData.IsInsideGeofence = onPitRoad[carIdx]`
    - `tData.PitCount = pitStopCount[carIdx]`
    - Monitoraggio `trackSurface[carIdx]`: avvio cronometro stationary al passaggio a `InPitStall`, arresto e salvataggio di `StationaryTimeSec` quando esce da `InPitStall`.
  - Se non disponibile (fallback per altri simulatori o mock): mantiene le euristiche esistenti.
- **Calibrazione Geofence:**
  - Su transizione `false -> true` di `CarIdxOnPitRoad`: se `PitRadar.PitEntryPct` non è calibrata (o confidenza bassa), invia il campione `CarIdxLapDistPct[carIdx]`.
  - Su transizione `true -> false` di `CarIdxOnPitRoad`: se `PitRadar.PitExitPct` non è calibrata, invia il campione `CarIdxLapDistPct[carIdx]`.
- **Regola 2 (Consumo Proporzionato al Player):**
  - Condizione di blocco: `if (playerCompletedLaps >= 3 && playerAvgFuelPerLap > 0.0)`.
  - Solo dal giro 4 corrente in poi la mediana del player viene applicata per scalare i consumi avversari in base ai rispettivi serbatoi BoP.
- **Regola 3 (Calcoli Continui):**
  - Esecuzione continua di `FuelLapsRemaining` e `EstimatedPitWindowLap`.
  - Mantenimento continuo dei valori anche dopo la sosta (azzeramento risolto grazie allo stato nativo).
- **Regola 4 (Smart Refueling all'ingresso box):**
  - All'attivazione di `IsInPit` / `CarIdxOnPitRoad`:
    - Calcolo `LapsRemaining = TotalRaceLaps - CurrentLap`
    - `FuelNeeded = (LapsRemaining * EffectiveBurn) + SafetyBuffer`
    - `FuelToAdd = Math.Min(MaxTank - FuelResiduo, Math.Max(0, FuelNeeded - FuelResiduo))`
    - Se `FuelResiduo + FuelToAdd >= LapsRemaining * EffectiveBurn`, imposta `TargetNeedsPit = false`.
- **Regola 4b (Scorporo Pit Loss):**
  - `RawExtendedPitZoneTime = Math.Max(0.0, ObservedExtendedPitTime - StationaryTimeSec)`
  - Calcolo `ObservedJackingBuffer = StationaryTimeSec - (FuelToAdd / FuelFillRate)`
  - Proiezione Pit Loss per le altre auto: `TotalPitLoss = RawExtendedPitZoneTime + (OpponentFuelToAdd / FuelFillRate + JackingBuffer)`

---

### Strategia Target, Gap e Posizione di Classe

#### [MODIFY] [TargetStrategyManager.cs](file:///c:/Users/Andreas/Desktop/The%20Wheel%20Project/Antigravity2.0/User.PluginSdkDemoEdit/TargetStrategyManager.cs)
- **Posizione di Classe:**
  - Assegna `CurrentTarget.ClassPosition = carIdxClassPosition[targetCarIdx]` (o fallback su ranking di classe se non iRacing).
- **Correzione Inversione di Segno Gap:**
  - Modifica `WrapLapDifference(posDiffLaps)`: limitare il wrapping esclusivamente al rollover dei contatori di giro (es. discontinuità $\pm 1.0$ al passaggio sul traguardo), senza ripiegare distacchi $\in [0.5, 1.0]$ in valori negativi, che invertivano la logica "davanti/dietro".
- **Stabilizzazione Gap durante la Sosta:**
  - Quando il target si trova in pit (`CarIdxOnPitRoad == true`), congela o proietta fluidamente il gap evitando la ricerca nei microsettori di pista che sfarfallava ad ogni sosta.

---

### Progetto e Riferimenti

#### [MODIFY] [User.PluginSdkDemo.csproj](file:///c:/Users/Andreas/Desktop/The%20Wheel%20Project/Antigravity2.0/User.PluginSdkDemoEdit/User.PluginSdkDemo.csproj)
- Aggiunta di `IracingTelemetryBridge.cs` all'ItemGroup `<Compile>`.
- Aggiunta reference `<Reference Include="iRacingSDK"> <HintPath>$(SIMHUB_INSTALL_PATH)iRacingSDK.dll</HintPath> <Private>False</Private> </Reference>` (garantita presente in `$(SIMHUB_INSTALL_PATH)`).

---

## Verification Plan

### Automated Tests
- Compilazione solution tramite MSBuild:
  ```powershell
  "C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "User.PluginSdkDemoEdit/User.PluginSdkDemo.sln" -p:Configuration=Debug -v:minimal -nologo
  ```
- Esecuzione del test runner console:
  ```powershell
  "User.PluginSdkDemoEdit/User.PluginSdkDemo.Tests/bin/Debug/User.PluginSdkDemo.Tests.exe"
  ```
- Aggiunta di nuovi unit test dedicati in `User.PluginSdkDemo.Tests`:
  - `OpponentFuelEngineUnitTests.cs`:
    - Verifica attivazione consumo proporzionato solo a `CompletedLaps >= 3`.
    - Verifica formula `FuelToAdd` per fine gara (non 100% pieno) e flag `TargetNeedsPit = false`.
    - Verifica scorporo $\text{RawExtendedPitZoneTime} = \text{ObservedTime} - \text{StationaryTime}$.
    - Verifica `WrapLapDifference` senza inversione di segno a 0.6 giri.

### Replay Backtest (Road Atlanta)
- Esecuzione del replay `20260909_081925`:
  - Verifica che per Aake Korte `EstimatedFuel` ed `EstimatedFuelTank` restino validi e coerenti dopo la sosta.
  - Verifica che `CarIdxPitStopCount` rifletta esattamente la sosta completata.
  - Verifica che `MergeGap` e `CurrentGap` rimangano stabili e privi di oscillazioni anomale durante la sosta di Aake Korte.
  - Verifica che `SimRIG.Target.Position` mostri la posizione di classe e non quella assoluta.
