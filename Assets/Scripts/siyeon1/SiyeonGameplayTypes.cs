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
        ApproachCharge,
        Charging,
        ChargeInterrupted,
        InspectIgnored
    }
}
