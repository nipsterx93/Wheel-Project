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

        public void Update(object rawObject, SimHub.Plugins.PluginManager pm = null)
        {
            bool populatedFromSample = false;
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
                populatedFromSample = (CarIdxOnPitRoad != null || CarIdxTrackSurface != null);
            }

            // Fallback per Replay SimHub (.telemetry.json) tramite PluginManager
            if (!populatedFromSample && pm != null)
            {
                var rawSurfaces = pm.GetPropertyValue("DataCorePlugin.GameRawData.Telemetry.CarIdxTrackSurface");
                if (rawSurfaces is iRacingSDK.TrackLocation[] locations)
                {
                    CarIdxTrackSurface = locations;
                }
                else if (rawSurfaces is Array arrSurf)
                {
                    if (CarIdxTrackSurface == null || CarIdxTrackSurface.Length != arrSurf.Length)
                        CarIdxTrackSurface = new iRacingSDK.TrackLocation[arrSurf.Length];
                    for (int i = 0; i < arrSurf.Length; i++)
                    {
                        CarIdxTrackSurface[i] = (iRacingSDK.TrackLocation)Convert.ToInt32(arrSurf.GetValue(i));
                    }
                }

                var rawPitRoad = pm.GetPropertyValue("DataCorePlugin.GameRawData.Telemetry.CarIdxOnPitRoad");
                if (rawPitRoad is bool[] bools)
                {
                    CarIdxOnPitRoad = bools;
                }
                else if (rawPitRoad is Array arrPit)
                {
                    if (CarIdxOnPitRoad == null || CarIdxOnPitRoad.Length != arrPit.Length)
                        CarIdxOnPitRoad = new bool[arrPit.Length];
                    for (int i = 0; i < arrPit.Length; i++)
                    {
                        CarIdxOnPitRoad[i] = Convert.ToBoolean(arrPit.GetValue(i));
                    }
                }

                var rawPitCount = pm.GetPropertyValue("DataCorePlugin.GameRawData.Telemetry.CarIdxPitStopCount");
                if (rawPitCount is int[] pitCounts)
                {
                    CarIdxPitStopCount = pitCounts;
                }
                else if (rawPitCount is Array arrCount)
                {
                    if (CarIdxPitStopCount == null || CarIdxPitStopCount.Length != arrCount.Length)
                        CarIdxPitStopCount = new int[arrCount.Length];
                    for (int i = 0; i < arrCount.Length; i++)
                    {
                        CarIdxPitStopCount[i] = Convert.ToInt32(arrCount.GetValue(i));
                    }
                }

                var rawClassPos = pm.GetPropertyValue("DataCorePlugin.GameRawData.Telemetry.CarIdxClassPosition");
                if (rawClassPos is int[] classPositions)
                {
                    CarIdxClassPosition = classPositions;
                }
                else if (rawClassPos is Array arrPos)
                {
                    if (CarIdxClassPosition == null || CarIdxClassPosition.Length != arrPos.Length)
                        CarIdxClassPosition = new int[arrPos.Length];
                    for (int i = 0; i < arrPos.Length; i++)
                    {
                        CarIdxClassPosition[i] = Convert.ToInt32(arrPos.GetValue(i));
                    }
                }

                var rawLapDist = pm.GetPropertyValue("DataCorePlugin.GameRawData.Telemetry.CarIdxLapDistPct");
                if (rawLapDist is float[] lapDists)
                {
                    CarIdxLapDistPct = lapDists;
                }
                else if (rawLapDist is Array arrDist)
                {
                    if (CarIdxLapDistPct == null || CarIdxLapDistPct.Length != arrDist.Length)
                        CarIdxLapDistPct = new float[arrDist.Length];
                    for (int i = 0; i < arrDist.Length; i++)
                    {
                        CarIdxLapDistPct[i] = Convert.ToSingle(arrDist.GetValue(i));
                    }
                }
            }

            if (CarIdxOnPitRoad != null || CarIdxTrackSurface != null)
            {
                IsAvailable = true;
            }
            else if (rawObject == null && pm == null)
            {
                IsAvailable = false;
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

        public static string GetTrackSurfaceString(IracingTrackSurface surface)
        {
            switch (surface)
            {
                case IracingTrackSurface.OnTrack: return "OnTrack";
                case IracingTrackSurface.InPitStall: return "InPitStall";
                case IracingTrackSurface.AproachingPits: return "ApproachingPits";
                case IracingTrackSurface.OffTrack: return "OffTrack";
                default: return "NotInWorld";
            }
        }
    }
}
