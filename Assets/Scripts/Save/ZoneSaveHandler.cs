using HideAndInk.Scripts.Save;
using UnityEngine;

/// <summary>
/// ZoneChanger의 구역 전환 이벤트를 받아 자동 저장합니다.
/// ZoneChanger와 같은 GameObject에 부착하여 사용합니다.
///
/// Player/치치 Transform 참조 우선순위:
///   1. Inspector에서 playerTransform 직접 할당
///   2. CharacterRegistry.Player (PlayerMovementAdapter가 자동 등록)
///   3. 없으면 Vector3.zero (ZoneChanger 위치로 fallback)
///
/// 저장 데이터:
/// - Zone 번호
/// - Player 위치
/// - 치치 위치
/// </summary>
[RequireComponent(typeof(ZoneChanger))]
public class ZoneSaveHandler : MonoBehaviour
{
    [Header("위치 참조 (선택사항 - 미할당 시 Registry 자동 사용)")]
    [Tooltip("Player Transform (미할당 시 CharacterRegistry.Player 사용)")]
    [SerializeField] private Transform playerTransform;

    [Tooltip("치치 Transform (미할당 시 CharacterRegistry.Squid 사용)")]
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
    /// 현재 시점의 Player/치치 Transform을 반환합니다.
    /// 우선순위: Inspector 할당 > CharacterRegistry 등록 > null
    /// </summary>
    private Transform ResolvePlayer() => playerTransform != null ? playerTransform : CharacterRegistry.Player;
    private Transform ResolveSquid() => squidTransform != null ? squidTransform : CharacterRegistry.Squid;

    /// <summary>
    /// ZoneChanger.onZoneChanged 콜백.
    /// 구역이 변경될 때마다 Player/치치 위치를 포함하여 저장합니다.
    /// </summary>
    private void OnZoneChanged(int toZoneNumber)
    {
        Transform player = ResolvePlayer();
        Transform squid = ResolveSquid();

        Vector3 playerPos = player != null ? player.position : Vector3.zero;
        Vector3 squidPos = squid != null ? squid.position : Vector3.zero;

        // 위치가 (0,0,0)이면 ZoneChanger 위치로 대체 (안전장치)
        if (playerPos == Vector3.zero && _zoneChanger != null)
        {
            playerPos = _zoneChanger.transform.position;
            Debug.LogWarning($"[ZoneSaveHandler] Player 위치를 ZoneChanger 위치로 대체: {playerPos}");
        }
        if (squidPos == Vector3.zero && _zoneChanger != null)
        {
            squidPos = _zoneChanger.transform.position;
            Debug.LogWarning($"[ZoneSaveHandler] 치치 위치를 ZoneChanger 위치로 대체: {squidPos}");
        }

        string playerSrc = player != null ? player.name : "(Registry null)";
        string squidSrc = squid != null ? squid.name : "(Registry null)";

        Debug.Log($"[ZoneSaveHandler] Zone_{toZoneNumber} 저장 | " +
                  $"Player: {playerPos} (src: {playerSrc}), " +
                  $"치치: {squidPos} (src: {squidSrc})");

        var data = new SaveData(toZoneNumber, playerPos, squidPos)
        {
            playerSourceName = playerSrc,
            squidSourceName = squidSrc
        };
        SaveManager.Save(data);
    }
}