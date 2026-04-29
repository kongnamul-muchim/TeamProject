using UnityEngine;
using HideAndInk.Core.Interfaces;
using HideAndInk.Core.Managers;

namespace HideAndInk.Core.Perception
{
    /// <summary>
    /// 의태 시스템 → 의심도 연동 관리자 (MonoBehaviour Shell)
    /// - 의태 상태 감지: CamouflageStateSyncService에 위임
    /// - ISuspicionMeter 연결: DI 우선, fallback으로 SuspicionManager.Instance
    /// </summary>
    [RequireComponent(typeof(ICamouflageStateProvider))]
    public sealed class CamouflageToSuspicionLink : MonoBehaviour
    {
        private ICamouflageStateProvider _camouflageProvider;
        private CamouflageStateSyncService _syncService;

        private void Awake()
        {
            _camouflageProvider = GetComponent<ICamouflageStateProvider>();
            _syncService = CreateSyncService();
        }

        private CamouflageStateSyncService CreateSyncService()
        {
            // 1순위: DI 컨테이너의 ISuspicionMeter
            if (GameManager.Container != null && GameManager.Container.IsRegistered<ISuspicionMeter>())
            {
                var meter = GameManager.Container.Resolve<ISuspicionMeter>();
                return new CamouflageStateSyncService((c, p) => meter.SetCamouflageState(c, p));
            }

            // 2순위: SuspicionManager.Instance (legacy fallback)
            if (SuspicionManager.Instance != null)
            {
                var mgr = SuspicionManager.Instance;
                return new CamouflageStateSyncService((c, p) => mgr.SetCamouflageState(c, p));
            }

#if UNITY_EDITOR
            Debug.LogWarning("[CamouflageToSuspicionLink] No ISuspicionMeter available. Sync disabled.");
#endif
            return null;
        }

        private void Update()
        {
            if (_syncService == null || _camouflageProvider == null) return;

            _syncService.CheckAndSync(
                _camouflageProvider.IsCamouflaging,
                _camouflageProvider.IsPerfect
            );
        }

        private void OnDisable()
        {
            // 비활성화 시 추적 상태 리셋 (재활성화 때 처음부터 다시 추적)
            _syncService?.Reset();
        }
    }
}
