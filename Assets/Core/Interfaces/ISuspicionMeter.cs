using System;

namespace HideAndInk.Core.Interfaces
{
    public enum SuspicionLevel
    {
        Safe = 0,
        Caution = 1,
        Danger = 2,
        Critical = 3,
        Detected = 4
    }

    public interface ISuspicionMeter
    {
        float CurrentValue { get; }
        SuspicionLevel CurrentLevel { get; }
        void AddSuspicion(float amount);
        void ReduceSuspicion(float amount);
        void Reset();
        void SetSuspicion(float value);
        void SetIncreaseSpeed(float speed);
        void SetDecreaseSpeed(float speed);
        void SetCamouflageState(bool isCamouflaging, bool isPerfect = false);
        void OnDetectedTarget(float detectionIntensity = 1f);
        event Action<SuspicionLevel> OnLevelChanged;
        event Action OnDetected;
        event Action OnClear;
    }
}
