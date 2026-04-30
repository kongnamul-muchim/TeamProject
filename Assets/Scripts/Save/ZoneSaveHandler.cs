using HideAndInk.Scripts.Save;
using UnityEngine;

/// <summary>
/// ZoneChanger의 구역 전환 이벤트를 받아 자동 저장합니다.
/// ZoneChanger와 같은 GameObject에 부착하여 사용합니다.
///
/// 저장 데이터:
/// - Zone 번호
/// - Player 위치 (저장 시점에 태그 "Player"로 동적 탐색, Inspector 할당 우선)
/// - 치치 위치 (Inspector 할당 필요, 미할당 시 저장 안 함)
///
/// 개선안 1: 저장 시 매번 동적 탐색 (참조 캐싱 제거)
/// 개선안 2: 위치가 (0,0,0)이면 ZoneChanger 위치로 대체
/// 개선안 3: 저장 객체명을 SaveData에 기록
/// </summary>
[RequireComponent(typeof(ZoneChanger))]
public class ZoneSaveHandler : MonoBehaviour
{
    [Header("위치 참조 (선택사항 - 미할당 시 태그/이름으로 자동 탐색)")]
    [Tooltip("Player Transform (미할당 시 저장 시점에 태그 'Player'로 동적 탐색)")]
    [SerializeField] private Transform playerTransform;

    [Tooltip("치치 Transform (미할당 시 저장 안 함)")]
    [SerializeField] private Transform squidTransform;

    private ZoneChanger _zoneChanger;

    private void Awake()
    {
        _zoneChanger = GetComponent<ZoneChanger>();
    }

    private void OnEnable()
    {
        if (_zoneChanger != null)
            _zoneChanger.onZoneChanged.AddListener(OnZoneChanged);
    }

    private void OnDisable()
    {
        if (_zoneChanger != null)
            _zoneChanger.onZoneChanged.RemoveListener(OnZoneChanged);
    }

    /// <summary>
    /// ZoneChanger.onZoneChanged 콜백.
    /// 구역이 변경될 때마다 Player/치치 위치를 포함하여 저장합니다.
    ///
    /// [개선안 1] 매 저장 시점에 GameObject.Find 로 Player/치치를 다시 찾습니다.
    ///   → Awake()에서 한 번 찾고 평생 쓰면, 잘못 찾은 객체를 계속 참조하는 문제 발생
    ///   → 저장 직전에 항상 현재 씬에서 다시 검색하여 정확한 Transform.position 확보
    ///
    /// [개선안 2] 위치가 (0,0,0)이면 ZoneChanger.transform.position 으로 대체합니다.
    ///   → Transform 참조가 null이거나 위치를 못 읽어오는 경우를 대비한 안전장치
    ///
    /// [개선안 3] 어떤 객체의 위치를 저장했는지 sourceObjectName 에 기록합니다.
    ///   → save.json 열어보면 "어디서" 위치를 읽었는지 확인 가능
    ///   → 문제 발생 시 디버깅이 훨씬 쉬워짐
    /// </summary>
    private void OnZoneChanged(int toZoneNumber)
    {
        // ============================================================
        // [개선안 1] 저장 시점에 매번 Player/치치 Transform 동적 탐색
        // ============================================================
        Transform player = playerTransform; // Inspector 할당 우선
        if (player == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) player = go.transform;
        }

        Transform squid = squidTransform; // Inspector 할당 우선
        if (squid == null)
        {
            squid = GameObject.Find("Squid")?.transform
                  ?? GameObject.Find("치치")?.transform;
        }

        // 위치 캡처
        Vector3 playerPos = player != null ? player.position : Vector3.zero;
        Vector3 squidPos = squid != null ? squid.position : Vector3.zero;

        // ============================================================
        // [개선안 2] (0,0,0)이면 ZoneChanger 위치로 대체
        // ============================================================
        if (playerPos == Vector3.zero && _zoneChanger != null)
        {
            playerPos = _zoneChanger.transform.position;
            Debug.LogWarning($"[ZoneSaveHandler] Player 위치가 (0,0,0) → ZoneChanger 위치로 대체: {playerPos}");
        }
        if (squidPos == Vector3.zero && _zoneChanger != null)
        {
            squidPos = _zoneChanger.transform.position;
            Debug.LogWarning($"[ZoneSaveHandler] 치치 위치가 (0,0,0) → ZoneChanger 위치로 대체: {squidPos}");
        }

        // ============================================================
        // [개선안 3] 저장되는 객체명을 데이터에 기록
        // ============================================================
        string playerSrc = player != null ? player.name : "(null)";
        string squidSrc = squid != null ? squid.name : "(null)";

        Debug.Log($"[ZoneSaveHandler] Zone_{toZoneNumber} 도착 → 저장 중 | " +
                  $"Player: {playerPos} (출처: {playerSrc}), " +
                  $"치치: {squidPos} (출처: {squidSrc})");

        var data = new SaveData(toZoneNumber, playerPos, squidPos)
        {
            playerSourceName = playerSrc,
            squidSourceName = squidSrc
        };
        SaveManager.Save(data);
    }
}