using System;
using UnityEngine;
using HideAndInk.Core.Interfaces;

namespace HideAndInk.Core.Events
{
    /// <summary>
    /// 의태 시스템 전역 이벤트
    /// 다른 시스템은 이 이벤트를 구독하여 상태 변화에 반응
    /// </summary>
    public static class CamouflageEvents
    {
        /// <summary> 의태 상태가 변경될 때 발생 </summary>
        public static event Action<CamouflageState> OnStateChanged;
        
        /// <summary> 의태 시작 시 발생 (None → Attached) </summary>
        public static event Action<GameObject> OnCamouflageStart;
        
        /// <summary> 완벽 의태 달성 시 발생 (Perfect 도달) </summary>
        public static event Action<GameObject> OnCamouflageComplete;
        
        /// <summary> 의태 해제 시 발생 (→ None) </summary>
        public static event Action<GameObject> OnCamouflageEnd;
        
        // 내부 호출 메서드 (CamouflageAdapter에서만 호출)
        internal static void InvokeStateChanged(CamouflageState state) 
            => OnStateChanged?.Invoke(state);
        
        internal static void InvokeCamouflageStart(GameObject target) 
            => OnCamouflageStart?.Invoke(target);
        
        internal static void InvokeCamouflageComplete(GameObject target) 
            => OnCamouflageComplete?.Invoke(target);
        
        internal static void InvokeCamouflageEnd(GameObject target) 
            => OnCamouflageEnd?.Invoke(target);
    }
}
