using UnityEngine;
using HideAndInk.Core.Interfaces;

namespace HideAndInk.Core.Perception
{
    /// <summary>
    /// 의태 시스템 → 의심도 연동 관리자
    /// ICamouflageStateProvider의 상태를 감시하여 SuspicionMeter에 연동
    /// </summary>
    [RequireComponent(typeof(ICamouflageStateProvider))]
    public sealed class CamouflageToSuspicionLink : MonoBehaviour
    {
        [Header("연동할 의심도 계량기")]
        [SerializeField] private SuspicionMeter suspicionMeter;

        private ICamouflageStateProvider _camouflageProvider;
        private bool _wasCamouflaging;
        private bool _wasPerfect;

        private void Awake()
        {
            _camouflageProvider = GetComponent<ICamouflageStateProvider>();
        }

        private void Update()
        {
            if (suspicionMeter == null || _camouflageProvider == null) return;

            bool isCamouflaging = _camouflageProvider.IsCamouflaging;
            bool isPerfect = _camouflageProvider.IsPerfect;

            // 의태 상태 또는 Perfect 상태가 변경되면 SuspicionMeter에 알림
            if (isCamouflaging != _wasCamouflaging || isPerfect != _wasPerfect)
            {
                _wasCamouflaging = isCamouflaging;
                _wasPerfect = isPerfect;
                suspicionMeter.SetCamouflageState(isCamouflaging, isPerfect);
            }
        }

        /// <summary>
        /// 의심도 계량기 설정 (외부에서 호출)
        /// </summary>
        public void SetSuspicionMeter(SuspicionMeter meter)
        {
            suspicionMeter = meter;
        }
    }
}
