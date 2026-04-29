using System;

namespace HideAndInk.Core.Perception
{
    /// <summary>
    /// 의태 상태 변경 감지 및 SuspicionMeter 동기화 전담 순수 C# 서비스
    /// MonoBehaviour 의존성 없음 — 테스트 가능
    /// 
    /// 사용법:
    ///   var service = new CamouflageStateSyncService(
    ///       (isCamouflaging, isPerfect) => meter.SetCamouflageState(isCamouflaging, isPerfect)
    ///   );
    ///   service.CheckAndSync(provider.IsCamouflaging, provider.IsPerfect);
    /// </summary>
    public sealed class CamouflageStateSyncService
    {
        private readonly Action<bool, bool> _onStateChanged;
        private bool _wasCamouflaging;
        private bool _wasPerfect;

        /// <summary>
        /// 이전에 동기화된 의태 상태
        /// </summary>
        public bool WasCamouflaging => _wasCamouflaging;
        public bool WasPerfect => _wasPerfect;

        /// <param name="onStateChanged">
        /// 의태 상태가 변경되었을 때 호출될 콜백
        /// (isCamouflaging, isPerfect)를 인자로 받음
        /// </param>
        public CamouflageStateSyncService(Action<bool, bool> onStateChanged)
        {
            _onStateChanged = onStateChanged ?? throw new ArgumentNullException(nameof(onStateChanged));
        }

        /// <summary>
        /// 현재 의태 상태를 확인하고 변경이 있으면 콜백 호출
        /// </summary>
        /// <param name="isCamouflaging">현재 의태 중인지 여부</param>
        /// <param name="isPerfect">현재 완벽 의태인지 여부</param>
        /// <returns>상태가 변경되었으면 true</returns>
        public bool CheckAndSync(bool isCamouflaging, bool isPerfect)
        {
            if (isCamouflaging == _wasCamouflaging && isPerfect == _wasPerfect)
                return false;

            _wasCamouflaging = isCamouflaging;
            _wasPerfect = isPerfect;
            _onStateChanged.Invoke(isCamouflaging, isPerfect);
            return true;
        }

        /// <summary>
        /// 추적 상태 초기화 (컴포넌트 비활성화 등)
        /// </summary>
        public void Reset()
        {
            _wasCamouflaging = false;
            _wasPerfect = false;
        }
    }
}
