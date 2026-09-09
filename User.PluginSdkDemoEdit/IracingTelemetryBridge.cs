// -------------------------------------------------------------------------
// FILE: IracingTelemetryBridge.cs
// Estrae i vettori di telemetria nativi di iRacing a 60Hz da DataSample.Telemetry
// con accesso O(1), zero allocazioni e supporto a mock per i test.
// -------------------------------------------------------------------------

using System;

namespace SimRIG
{
    public enum IracingTrackSurface
    {
        NotInWorld = -1,
        OffTrack = 0,
        InPitStall = 1,
        AproachingPits = 2,
        OnTrack = 3
    }

    public class IracingTelemetryBridge
    {
        public bool IsAvailable { get; private set; }

        public bool[] CarIdxOnPitRoad { get; private set; }
        public iRacingSDK.TrackLocation[] CarIdxTrackSurface { get; private set; }
        public int[] CarIdxPitStopCount { get; private set; }
        public int[] CarIdxClassPosition { get; private set; }
        public float[] CarIdxLapDistPct { get; private set; }
        public int[] CarIdxLap { get; private set; }
        public int[] CarIdxLapCompleted { get; private set; }

        public void Update(object rawObject)
        {
            if (rawObject is iRacingSDK.DataSample sample && sample.Telemetry != null)
            {
                var tel = sample.Telemetry;
                CarIdxOnPitRoad = tel.CarIdxOnPitRoad;
                CarIdxTrackSurface = tel.CarIdxTrackSurface;
                CarIdxPitStopCount = tel.CarIdxPitStopCount;
                CarIdxClassPosition = tel.CarIdxClassPosition;
                CarIdxLapDistPct = tel.CarIdxLapDistPct;
                CarIdxLap = tel.CarIdxLap;
                CarIdxLapCompleted = tel.CarIdxLapCompleted;
                IsAvailable = CarIdxOnPitRoad != null;
            }
            else
            {
                // Non azzeriamo IsAvailable se stiamo usando mock data manuale nei test
                if (rawObject == null && CarIdxOnPitRoad == null)
                {
                    IsAvailable = false;
                }
            }
        }

        public IracingTrackSurface GetTrackSurface(int carIdx)
        {
            if (CarIdxTrackSurface != null && carIdx >= 0 && carIdx < CarIdxTrackSurface.Length)
            {
                return (IracingTrackSurface)(int)CarIdxTrackSurface[carIdx];
            }
            return IracingTrackSurface.NotInWorld;
        }

        public bool IsInPitStall(int carIdx) => GetTrackSurface(carIdx) == IracingTrackSurface.InPitStall;

        public bool IsOnPitRoad(int carIdx)
        {
            if (CarIdxOnPitRoad != null && carIdx >= 0 && carIdx < CarIdxOnPitRoad.Length)
                return CarIdxOnPitRoad[carIdx];
            return false;
        }

        public int GetPitStopCount(int carIdx)
        {
            if (CarIdxPitStopCount != null && carIdx >= 0 && carIdx < CarIdxPitStopCount.Length)
                return CarIdxPitStopCount[carIdx];
            return 0;
        }

        public int GetClassPosition(int carIdx)
        {
            if (CarIdxClassPosition != null && carIdx >= 0 && carIdx < CarIdxClassPosition.Length)
                return CarIdxClassPosition[carIdx];
            return 0;
        }

        public float GetLapDistPct(int carIdx)
        {
            if (CarIdxLapDistPct != null && carIdx >= 0 && carIdx < CarIdxLapDistPct.Length)
                return CarIdxLapDistPct[carIdx];
            return 0f;
        }

        public void SetMockData(bool[] onPitRoad, IracingTrackSurface[] trackSurface, int[] pitStopCount, int[] classPosition, float[] lapDistPct)
        {
            CarIdxOnPitRoad = onPitRoad;
            if (trackSurface != null)
            {
                CarIdxTrackSurface = new iRacingSDK.TrackLocation[trackSurface.Length];
                for (int i = 0; i < trackSurface.Length; i++)
                {
                    CarIdxTrackSurface[i] = (iRacingSDK.TrackLocation)(int)trackSurface[i];
                }
            }
            else
            {
                CarIdxTrackSurface = null;
            }
            CarIdxPitStopCount = pitStopCount;
            CarIdxClassPosition = classPosition;
            CarIdxLapDistPct = lapDistPct;
            IsAvailable = true;
        }
    }
}
