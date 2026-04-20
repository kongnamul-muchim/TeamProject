using System;

namespace HideAndInk.Core.Events
{
    /// <summary>
    /// 게임 전반 전역 이벤트
    /// 다른 시스템은 이 이벤트를 구독하여 게임 상태 변화에 반응
    /// </summary>
    public static class GameEvents
    {
        /// <summary> 플레이어 발각 시 발생 </summary>
        public static event Action OnPlayerDetected;
        
        /// <summary> 플레이어 사망 시 발생 </summary>
        public static event Action OnPlayerDeath;
        
        /// <summary> 스테이지 클리어 시 발생 </summary>
        public static event Action OnStageClear;
        
        // 내부 호출 메서드 (GameStateMachine 등에서 호출)
        internal static void InvokePlayerDetected() => OnPlayerDetected?.Invoke();
        internal static void InvokePlayerDeath() => OnPlayerDeath?.Invoke();
        internal static void InvokeStageClear() => OnStageClear?.Invoke();
    }
}
