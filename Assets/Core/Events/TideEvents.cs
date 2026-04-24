using System;
using UnityEngine;
using HideAndInk.Core.Environment;

namespace HideAndInk.Core.Events
{
    /// <summary>
    /// 조류 관련 이벤트
    /// 정적 클래스로 의존성 분리
    /// </summary>
    public static class TideEvents
    {
        /// <summary>
        /// 조류 발동 시 발생 (성게 풀 소환용)
        /// </summary>
        public static event Action<TideDirection, float> OnTideStarted;

        /// <summary>
        /// Player가 조류에 밀렸을 때 발생 (의태 해제 체크용)
        /// </summary>
        public static event Action<Vector3, float> OnPlayerPushed;

        /// <summary>
        /// 조류 종료 시 발생
        /// </summary>
        public static event Action OnTideEnded;

        public static void InvokeTideStarted(TideDirection direction, float force)
        {
            OnTideStarted?.Invoke(direction, force);
        }

        public static void InvokePlayerPushed(Vector3 forceDirection, float force)
        {
            OnPlayerPushed?.Invoke(forceDirection, force);
        }

        public static void InvokeTideEnded()
        {
            OnTideEnded?.Invoke();
        }
    }
}
