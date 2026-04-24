using UnityEngine;
using System;

namespace HideAndInk.Core.Perception
{
    /// <summary>
    /// 보스 기믹별 의심도 계산 모듈 인터페이스
    /// 각 기믹(ScriptableObject)이 자신의 의심도 로직을 이 모듈로 제공
    /// </summary>
    public interface ISuspicionModule
    {
        /// <summary>
        /// 모듈 이름 (디버깅용)
        /// </summary>
        string ModuleName { get; }

        /// <summary>
        /// 의심도 업데이트 (매 프레임 호출)
        /// </summary>
        /// <param name="deltaTime">경과 시간</param>
        /// <param name="bossPos">보스 위치</param>
        /// <param name="playerPos">Player 위치 (null이면 Player 없음)</param>
        void Update(float deltaTime, Vector3 bossPos, Vector3? playerPos);

        /// <summary>
        /// 현재 의심도 상승률 가져오기 (초당)
        /// </summary>
        float GetCurrentRate();

        /// <summary>
        /// 의심도 상승 이벤트 (rate, deltaTime)
        /// </summary>
        event Action<float, float> OnSuspicionIncrease;

        /// <summary>
        /// 모듈 활성화 (기믹 적용 시 호출)
        /// </summary>
        void OnActivate();

        /// <summary>
        /// 모듈 비활성화 (기믹 해제 시 호출)
        /// </summary>
        void OnDeactivate();
    }
}
