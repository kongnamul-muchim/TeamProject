using UnityEngine;
using HideAndInk.Core.Interfaces;

namespace HideAndInk.Core.Perception
{
    /// <summary>
    /// 의태 시스템 → 의심도 연동 관리자
    /// CamouflageAdapter의 상태를 감시하여 SuspicionMeter에 연동
    /// </summary>
    [RequireComponent(typeof(CamouflageAdapter))]
    public sealed class CamouflageToSuspicionLink : MonoBehaviour
    {
        [Header("연동할 의심도 계량기")]
        [SerializeField] private ISuspicionMeter suspicionMeter;

        private CamouflageAdapter _camouflageAdapter;
        private bool _wasCamouflaging;

        private void Awake()
        {
            _camouflageAdapter = GetComponent<CamouflageAdapter>();
        }

        private void Update()
        {
            if (suspicionMeter == null) return;

            CamouflageState currentState = _camouflageAdapter.CurrentState;
            bool isCamouflaging = currentState != CamouflageState.None;
            bool isPerfect = currentState == CamouflageState.Perfect;

            // 의태 상태가 변경되면SuspicionMeter에 알림
            if (isCamouflaging != _wasCamouflaging)
            {
                _wasCamouflaging = isCamouflaging;
                suspicionMeter.SetCamouflageState(isCamouflaging, isPerfect);
                Debug.Log($"[CamouflageSuspicion] Camouflage state changed: isCamouflaging={isCamouflaging}, isPerfect={isPerfect}");
            }

            // Perfect 상태가 변경되면
            if (isCamouflaging && isPerfect)
            {
                // Perfect 도달 시 추가 처리 필요시
            }
        }

        /// <summary>
        /// 의심도 계량기 설정 (외부에서 호출)
        /// </summary>
        public void SetSuspicionMeter(ISuspicionMeter meter)
        {
            suspicionMeter = meter;
        }
    }
}
