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
/// 사용법:
/// 1. 이 스크립트를 게임 씬의 아무 GameObject에 부착
/// 2. (선택) zoneContainer에 Zone 부모 Transform 연결
/// 3. (선택) playerTransform / squidTransform 할당 (미할당 시 태그/이름으로 자동 탐색)
/// </summary>
public class ContinueZoneHandler : MonoBehaviour
{
    [Header("Zone 컨테이너")]
    [Tooltip("모든 Zone 오브젝트를 담는 부모 Transform (미할당 시 씬 전체 탐색)")]
    [SerializeField] private Transform zoneContainer;

    [Header("위치 참조")]
    [Tooltip("Player Transform (미할당 시 태그 'Player'로 자동 탐색)")]
    [SerializeField] private Transform playerTransform;

    [Tooltip("치치 Transform (미할당 시 이름 'Squid' 또는 '치치'로 탐색)")]
    [SerializeField] private Transform squidTransform;

    private void Awake()
    {
        // Player 자동 탐색
        if (playerTransform == null)
        {
            var playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo != null)
                playerTransform = playerGo.transform;
        }

        // 치치 자동 탐색
        if (squidTransform == null)
        {
            var squidGo = GameObject.Find("Squid") ?? GameObject.Find("치치");
            if (squidGo != null)
                squidTransform = squidGo.transform;
        }
    }

    private void Start()
    {
        // 모든 Zone 오브젝트 찾기
        List<GameObject> allZones = FindAllZoneObjects();

        if (SaveManager.IsContinueMode)
        {
            HandleContinue(allZones);
        }
        else
        {
            HandleNewGame(allZones);
        }

        Destroy(this);
    }

    /// <summary>
    /// 새 게임: Zone_1 활성화, 나머지 비활성화.
    /// </summary>
    private void HandleNewGame(List<GameObject> allZones)
    {
        int targetZone = 1;
        Debug.Log($"[ContinueZoneHandler] 새 게임: Zone_{targetZone} 활성화");

        ActivateZoneOnly(allZones, targetZone);
        TeleportPlayerToZone(targetZone);
    }

    /// <summary>
    /// 이어하기: 저장된 Zone 활성화 + Player/치치 위치 복원.
    /// </summary>
    private void HandleContinue(List<GameObject> allZones)
    {
        int targetZone = SaveManager.PendingZoneIndex;
        Vector3 playerPos = SaveManager.PendingPlayerPosition;
        Vector3 squidPos = SaveManager.PendingSquidPosition;
        SaveManager.ClearContinueZone();

        Debug.Log($"[ContinueZoneHandler] 이어하기 모드: Zone_{targetZone} 활성화");

        ActivateZoneOnly(allZones, targetZone);

        // Player 위치 복원
        if (playerTransform != null && playerPos != Vector3.zero)
        {
            playerTransform.position = playerPos;
            Debug.Log($"[ContinueZoneHandler] Player 위치 복원: {playerPos}");
        }

        // 치치 위치 복원
        if (squidTransform != null && squidPos != Vector3.zero)
        {
            squidTransform.position = squidPos;
            Debug.Log($"[ContinueZoneHandler] 치치 위치 복원: {squidPos}");
        }
    }

    /// <summary>
    /// 모든 Zone 중 targetZone 번호와 일치하는 것만 활성화합니다.
    /// </summary>
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

    /// <summary>
    /// 씬에서 "Zone_{number}_*" 패턴의 모든 오브젝트를 찾습니다.
    /// </summary>
    private List<GameObject> FindAllZoneObjects()
    {
        List<GameObject> results = new List<GameObject>();

        if (zoneContainer != null)
        {
            SearchInChildren(zoneContainer, results);
        }
        else
        {
            // 씬 전체 탐색
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go == null || go.hideFlags != HideFlags.None) continue;
                if (!go.scene.IsValid() || !go.scene.isLoaded) continue;
                if (go.name.Trim().StartsWith("Zone_") && go.transform.parent != null)
                    results.Add(go);
            }
        }

        // 중복 제거
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

    /// <summary>
    /// "Zone_{number}_..." 형식의 이름에서 number를 추출합니다.
    /// </summary>
    private int ExtractZoneNumber(string zoneName)
    {
        string[] parts = zoneName.Trim().Split('_');
        if (parts.Length >= 2 && int.TryParse(parts[1], out int number))
            return number;
        return -1;
    }

    /// <summary>
    /// 플레이어를 해당 Zone의 ZoneChanger 위치로 이동시킵니다.
    /// (새 게임 시작 시 사용)
    /// </summary>
    private void TeleportPlayerToZone(int zoneIndex)
    {
        if (playerTransform == null) return;

        foreach (var zc in FindObjectsByType<ZoneChanger>(FindObjectsSortMode.None))
        {
            if (zc.toZoneNumber == zoneIndex)
            {
                playerTransform.position = zc.transform.position;
                Debug.Log($"[ContinueZoneHandler] 플레이어를 Zone_{zoneIndex} 시작점으로 이동: {zc.transform.position}");
                return;
            }
        }

        Debug.LogWarning($"[ContinueZoneHandler] Zone_{zoneIndex} 의 ZoneChanger를 찾을 수 없습니다.");
    }
}