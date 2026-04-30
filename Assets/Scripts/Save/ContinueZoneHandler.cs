using System.Collections.Generic;
using HideAndInk.Scripts.Save;
using UnityEngine;

/// <summary>
/// 게임 씬(Test_Jieun)에 배치하여 아래 두 가지를 처리합니다.
///
/// [새 게임]
/// - 모든 Zone 비활성화 → Zone_1 활성화
/// - ZoneChanger 중 toZoneNumber=1 인 위치로 플레이어 이동
///
/// [이어하기]
/// - 저장된 Zone 활성화, 나머지 비활성화
/// - 저장된 Player/치치 위치로 복원
///
/// Player/치치 Transform 참조 우선순위:
///   1. Inspector에서 playerTransform/squidTransform 직접 할당
///   2. CharacterRegistry.Player / CharacterRegistry.Squid
///   3. 없으면 로그 출력 후 스킵
///
/// 사용법:
/// 1. 이 스크립트를 게임 씬의 아무 GameObject에 부착
/// 2. (선택) zoneContainer에 Zone 부모 Transform 연결
/// 3. (선택) playerTransform / squidTransform 할당 (미할당 시 Registry 자동 사용)
/// </summary>
public class ContinueZoneHandler : MonoBehaviour
{
    [Header("Zone 컨테이너")]
    [Tooltip("모든 Zone 오브젝트를 담는 부모 Transform (미할당 시 씬 전체 탐색)")]
    [SerializeField] private Transform zoneContainer;

    [Header("위치 참조 (선택사항 - 미할당 시 Registry 자동 사용)")]
    [Tooltip("Player Transform (미할당 시 CharacterRegistry.Player 사용)")]
    [SerializeField] private Transform playerTransform;

    [Tooltip("치치 Transform (미할당 시 CharacterRegistry.Squid 사용)")]
    [SerializeField] private Transform squidTransform;

    private void Start()
    {
        // ResolveTransforms는 이미 Registry에 등록되어 있거나 Inspector에 할당되어 있음
        // (PlayerMovementAdapter.Awake()에서 CharacterRegistry에 자동 등록)
        DebugLogRegistryStatus();

        List<GameObject> allZones = FindAllZoneObjects();

        if (SaveManager.IsContinueMode)
            HandleContinue(allZones);
        else
            HandleNewGame(allZones);

        Destroy(this);
    }

    /// <summary>
    /// 현재 참조 가능한 Transform 상태를 로그로 출력합니다.
    /// </summary>
    private void DebugLogRegistryStatus()
    {
        Transform player = playerTransform != null ? playerTransform : CharacterRegistry.Player;
        Transform squid = squidTransform != null ? squidTransform : CharacterRegistry.Squid;

        Debug.Log($"[ContinueZoneHandler] Player 참조: {(player != null ? $"{player.name} at {player.position}" : "없음")}");
        Debug.Log($"[ContinueZoneHandler] 치치 참조: {(squid != null ? $"{squid.name} at {squid.position}" : "없음")}");
    }

    /// <summary>
    /// 현재 사용 가능한 Player Transform 반환.
    /// </summary>
    private Transform GetPlayer() => playerTransform != null ? playerTransform : CharacterRegistry.Player;

    /// <summary>
    /// 현재 사용 가능한 치치 Transform 반환.
    /// </summary>
    private Transform GetSquid() => squidTransform != null ? squidTransform : CharacterRegistry.Squid;

    // =====================================================
    // 새 게임
    // =====================================================

    private void HandleNewGame(List<GameObject> allZones)
    {
        int targetZone = 1;
        Debug.Log($"[ContinueZoneHandler] 새 게임: Zone_{targetZone} 활성화");

        ActivateZoneOnly(allZones, targetZone);
        TeleportPlayerToZone(targetZone);
    }

    // =====================================================
    // 이어하기
    // =====================================================

    private void HandleContinue(List<GameObject> allZones)
    {
        int targetZone = SaveManager.PendingZoneIndex;
        Vector3 playerPos = SaveManager.PendingPlayerPosition;
        Vector3 squidPos = SaveManager.PendingSquidPosition;
        SaveManager.ClearContinueZone();

        Debug.Log($"[ContinueZoneHandler] 이어하기 모드: Zone_{targetZone} 활성화");
        Debug.Log($"[ContinueZoneHandler] 로드된 위치 - Player: {playerPos}, 치치: {squidPos}");

        ActivateZoneOnly(allZones, targetZone);

        Transform player = GetPlayer();
        Transform squid = GetSquid();

        // ---- Player 위치 복원 ----
        if (player != null)
        {
            if (playerPos != Vector3.zero)
            {
                player.position = playerPos;
                Debug.Log($"[ContinueZoneHandler] Player 위치 복원: {playerPos}");
            }
            else
            {
                Debug.Log($"[ContinueZoneHandler] 저장된 Player 위치 없음 → ZoneChanger 위치로 fallback");
                TeleportPlayerToZone(targetZone);
            }
        }

        // ---- 치치 위치 복원 ----
        if (squid != null)
        {
            if (squidPos != Vector3.zero)
            {
                squid.position = squidPos;
                Debug.Log($"[ContinueZoneHandler] 치치 위치 복원: {squidPos}");
            }
            else
            {
                TeleportSquidToZone(targetZone);
            }
        }
    }

    // =====================================================
    // Zone 활성화
    // =====================================================

    private void ActivateZoneOnly(List<GameObject> allZones, int targetZone)
    {
        foreach (var zone in allZones)
        {
            int zoneNum = ExtractZoneNumber(zone.name);
            bool isTarget = zoneNum == targetZone;
            zone.SetActive(isTarget);
            Debug.Log($"[ContinueZoneHandler] Zone_{zoneNum} {(isTarget ? "활성화" : "비활성화")}");
        }
    }

    // =====================================================
    // Zone 오브젝트 탐색
    // =====================================================

    private List<GameObject> FindAllZoneObjects()
    {
        List<GameObject> results = new List<GameObject>();

        if (zoneContainer != null)
        {
            SearchInChildren(zoneContainer, results);
        }
        else
        {
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go == null || go.hideFlags != HideFlags.None) continue;
                if (!go.scene.IsValid() || !go.scene.isLoaded) continue;
                if (go.name.Trim().StartsWith("Zone_") && go.transform.parent != null)
                    results.Add(go);
            }
        }

        HashSet<int> seen = new HashSet<int>();
        results.RemoveAll(go => !seen.Add(go.GetInstanceID()));

        return results;
    }

    private void SearchInChildren(Transform parent, List<GameObject> results)
    {
        if (parent.name.Trim().StartsWith("Zone_"))
            results.Add(parent.gameObject);

        foreach (Transform child in parent)
            SearchInChildren(child, results);
    }

    private int ExtractZoneNumber(string zoneName)
    {
        string[] parts = zoneName.Trim().Split('_');
        if (parts.Length >= 2 && int.TryParse(parts[1], out int number))
            return number;
        return -1;
    }

    // =====================================================
    // 텔레포트
    // =====================================================

    private void TeleportPlayerToZone(int zoneIndex)
    {
        Transform player = GetPlayer();
        if (player == null) return;

        foreach (var zc in FindObjectsByType<ZoneChanger>(FindObjectsSortMode.None))
        {
            if (zc.toZoneNumber == zoneIndex)
            {
                player.position = zc.transform.position;
                Debug.Log($"[ContinueZoneHandler] 플레이어를 Zone_{zoneIndex} 시작점으로 이동: {zc.transform.position}");
                return;
            }
        }

        Debug.LogWarning($"[ContinueZoneHandler] Zone_{zoneIndex} 의 ZoneChanger를 찾을 수 없습니다.");
    }

    private void TeleportSquidToZone(int zoneIndex)
    {
        Transform squid = GetSquid();
        if (squid == null) return;

        foreach (var zc in FindObjectsByType<ZoneChanger>(FindObjectsSortMode.None))
        {
            if (zc.toZoneNumber == zoneIndex)
            {
                squid.position = zc.transform.position;
                Debug.Log($"[ContinueZoneHandler] 치치를 Zone_{zoneIndex} 시작점으로 이동: {zc.transform.position}");
                return;
            }
        }

        Debug.Log($"[ContinueZoneHandler] Zone_{zoneIndex} 의 ZoneChanger 없음 → 치치 위치 유지");
    }
}