using UnityEngine;
using System;

namespace HideAndInk.Core.Interfaces
{
    /// <summary>
    /// 의심도 레벨
    /// </summary>
    public enum SuspicionLevel
    {
        /// <summary>
        /// 안전 (0-30%)
        /// </summary>
        Safe = 0,

        /// <summary>
        /// 주의 (31-60%)
        /// </summary>
        Caution = 1,

        /// <summary>
        /// 위험 (61-80%)
        /// </summary>
        Danger = 2,

        /// <summary>
        /// 심각 (81-99%)
        /// </summary>
        Critical = 3,

        /// <summary>
        /// 발견됨 (100%)
        /// </summary>
        Detected = 4
    }

    /// <summary>
    /// 의심도 시스템 인터페이스
    /// </summary>
    public interface ISuspicionMeter
    {
        /// <summary>
        /// 현재 의심도 값 (0~100)
        /// </summary>
        float CurrentValue { get; }

        /// <summary>
        /// 현재 의심도 레벨
        /// </summary>
        SuspicionLevel CurrentLevel { get; }

        /// <summary>
        /// 의심도 상승
        /// </summary>
        /// <param name="amount">상승량</param>
        void AddSuspicion(float amount);

        /// <summary>
        /// 의심도 하락
        /// </summary>
        /// <param name="amount">하락량</param>
        void ReduceSuspicion(float amount);

        /// <summary>
        /// 의심도 리셋 (0으로)
        /// </summary>
        void Reset();

        /// <summary>
        /// 의심도 설정 (직접 값 설정)
        /// </summary>
        /// <param name="value">설정할 값</param>
        void SetSuspicion(float value);

        /// <summary>
        /// 상승 속도 설정
        /// </summary>
        /// <param name="speed">상승 속도</param>
        void SetIncreaseSpeed(float speed);

        /// <summary>
        /// 하락 속도 설정
        /// </summary>
        /// <param name="speed">하락 속도</param>
        void SetDecreaseSpeed(float speed);

        /// <summary>
        /// 의심도 레벨 변경 이벤트
        /// </summary>
        event Action<SuspicionLevel> OnLevelChanged;

        /// <summary>
        /// 100% 도달 (발각) 이벤트
        /// </summary>
        event Action OnDetected;

        /// <summary>
        /// 의심도가 0으로 복귀 이벤트
        /// </summary>
        event Action OnClear;
    }
}
