using UnityEngine;

namespace HideAndInk
{
    /// <summary>
    /// 트리거에 닿으면 구역을 전환하는 스크립트.
    /// Zone_1을 끄고 Zone_2를 켜는 식으로 구역을 교체한다.
    /// 
    /// 사용법:
    /// 1. 빈 GameObject에 BoxCollider2D (Is Trigger = true)와 이 스크립트 부착
    /// 2. Inspector에서 zoneToDeactivate, zoneToActivate에 각각 할당
    /// 3. 여러 구역을 순환하려면 zones 배열 사용
    /// 
    /// SRP: 구역 전환만 담당, DI: [SerializeField]로 참조
    /// </summary>
    public sealed class ZoneSwitcher : MonoBehaviour
    {
        [Header("간단 전환 (둘 중 하나만 사용)")]
        [Tooltip("비활성화할 구역")]
        [SerializeField] private GameObject zoneToDeactivate;

        [Tooltip("활성화할 구역")]
        [SerializeField] private GameObject zoneToActivate;

        [Header("순환 전환 (여러 구역을 돌아가며 전환)")]
        [Tooltip("순환할 구역 목록 (순서대로 전환됨)")]
        [SerializeField] private GameObject[] zones;

        [Header("전환 설정")]
        [Tooltip("한 번만 전환할지 여부 (false면 재진입 시마다 전환)")]
        [SerializeField] private bool switchOnce = true;

        [Tooltip("트리거를 발동시킬 태그 (비워두면 모든 오브젝트)")]
        [SerializeField] private string triggerTag = "Player";

        // ── 내부 상태 ──

        private bool _hasSwitched;
        private int _currentZoneIndex;

        // ── Unity 라이프사이클 ──

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_hasSwitched && switchOnce) return;

            if (!string.IsNullOrEmpty(triggerTag) && !other.CompareTag(triggerTag)) return;

            SwitchZone();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_hasSwitched && switchOnce) return;

            if (!string.IsNullOrEmpty(triggerTag) && !other.CompareTag(triggerTag)) return;

            SwitchZone();
        }

        // ── 공개 메서드 ──

        /// <summary>구역을 수동으로 전환한다.</summary>
        public void SwitchZone()
        {
            // 순환 전환 모드
            if (zones != null && zones.Length > 0)
            {
                SwitchCycling();
                return;
            }

            // 간단 전환 모드
            SwitchSimple();
        }

        /// <summary>전환 상태를 초기화한다. (switchOnce인 경우 재사용 가능)</summary>
        public void ResetSwitch()
        {
            _hasSwitched = false;
        }

        // ── 내부 메서드 ──

        private void SwitchSimple()
        {
            if (zoneToDeactivate != null)
                zoneToDeactivate.SetActive(false);

            if (zoneToActivate != null)
                zoneToActivate.SetActive(true);

            _hasSwitched = true;
        }

        private void SwitchCycling()
        {
            // 현재 구역 끄기
            if (zones[_currentZoneIndex] != null)
                zones[_currentZoneIndex].SetActive(false);

            // 다음 구역으로 이동
            _currentZoneIndex = (_currentZoneIndex + 1) % zones.Length;

            // 다음 구역 켜기
            if (zones[_currentZoneIndex] != null)
                zones[_currentZoneIndex].SetActive(true);

            _hasSwitched = true;
        }
    }
}