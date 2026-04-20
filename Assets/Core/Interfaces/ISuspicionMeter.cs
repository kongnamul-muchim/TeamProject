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

    /// <summary>
    /// 적 AI의 경계 상태
    /// </summary>
    public enum EnemyAlertState
    {
        Idle = 0,       // 평소 (의심도 0)
        Suspicious = 1, // 의심 중 (의심도 있음, 추적 안 함)
        Tracking = 2,   // 추적 중 (마지막 위치 향해 이동)
        Alert = 3       // 발각 (플레이어를 발견!)
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

        /// <summary>
        /// 발각 후 쿨다운 중인지 여부 (의심도 강제 유지 기간)
        /// </summary>
        bool IsInDetectedCooldown { get; }
    }
}
