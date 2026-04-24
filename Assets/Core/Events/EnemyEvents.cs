using System;
using UnityEngine;

namespace HideAndInk.Core.Events
{
    /// <summary>
    /// 적(Enemy) 관련 이벤트
    /// 정적 클래스로 의존성 분리
    /// </summary>
    public static class EnemyEvents
    {
        /// <summary>
        /// Player가 성게에 접촉하여 둔부 상태가 되었을 때 발생
        /// </summary>
        /// <param name="urchinPosition">성게 위치</param>
        /// <param name="slowPercent">이동 속도 감소 비율 (0.5 = 50% 감소)</param>
        /// <param name="duration">지속 시간 (초)</param>
        public static event Action<Vector3, float, float> OnPlayerSlowed;

        public static void InvokePlayerSlowed(Vector3 urchinPosition, float slowPercent, float duration)
        {
            OnPlayerSlowed?.Invoke(urchinPosition, slowPercent, duration);
        }
    }
}
