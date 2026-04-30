using System;

namespace HideAndInk.Siyeon1
{
    public enum DuduState
    {
        Normal,
        AutoCamouflaging,
        PerfectCamouflage,
        Fleeing,
        Dead
    }

    public enum ChichiState
    {
        Idle,
        Walk,
        Guide,
        ApproachCharge,
        Charging,
        ChargeInterrupted,
        InspectIgnored
    }

    public enum SuspicionLevel
    {
        Safe,
        Caution,
        Danger,
        Critical,
        Dead
    }

    public interface ISuspicionSource
    {
        float DangerPercent { get; }
        float ObservationTimer { get; }
        bool IsTargetVisible { get; }
    }
}
