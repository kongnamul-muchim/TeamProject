using HideAndInk.Scripts.Save;
using UnityEngine;

/// <summary>
/// ZoneChanger의 구역 전환 이벤트를 받아 자동 저장합니다.
/// ZoneChanger와 같은 GameObject에 부착하거나, 
/// ZoneChanger를 참조하도록 Inspector에서 연결하세요.
/// 
/// ZoneChanger의 onZoneChanged 이벤트에 
/// ZoneSaveHandler.OnZoneChanged 를 연결하여 사용합니다.
/// 
/// 저장 데이터:
/// - Zone 번호
/// - Player 위치 (Inspector에서 playerTransform 할당, 미할당 시 "Player" 태그로 탐색)
/// - 치치 위치 (Inspector에서 squidTransform 할당, 미할당 시 저장 안 함)
/// </summary>
[RequireComponent(typeof(ZoneChanger))]
public class ZoneSaveHandler : MonoBehaviour
{
    [Header("위치 참조 (Inspector 할당 권장)")]
    [Tooltip("Player Transform (미할당 시 태그 'Player'로 자동 탐색)")]
    [SerializeField] private Transform playerTransform;

    [Tooltip("치치 Transform (미할당 시 저장 안 함)")]
    [SerializeField] private Transform squidTransform;

    private ZoneChanger _zoneChanger;

    private void Awake()
    {
        _zoneChanger = GetComponent<ZoneChanger>();

        // Player 자동 탐색
        if (playerTransform == null)
        {
            var playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo != null)
                playerTransform = playerGo.transform;
        }
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
    /// </summary>
    private void OnZoneChanged(int toZoneNumber)
    {
        Vector3 playerPos = playerTransform != null ? playerTransform.position : Vector3.zero;
        Vector3 squidPos = squidTransform != null ? squidTransform.position : playerPos;

        Debug.Log($"[ZoneSaveHandler] Zone_{toZoneNumber} 도착 → 자동 저장 " +
            $"(Player: {playerPos}, 치치: {squidPos})");

        var data = new SaveData(toZoneNumber, playerPos, squidPos);
        SaveManager.Save(data);
    }
}
