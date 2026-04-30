using System.Collections.Generic;
using HideAndInk.Scripts.Save;
using UnityEngine;

/// <summary>
/// 게임 씬(Test_Jieun)에 배치하여 이어하기(Continue) 시 
/// 저장된 Zone을 활성화합니다.
/// 
/// 사용법:
/// 1. 이 스크립트를 게임 씬의 아무 GameObject에 부착
/// 2. 씬이 로드될 때 자동으로 저장된 Zone을 찾아 활성화
/// </summary>
public class ContinueZoneHandler : MonoBehaviour
{
    [Header("Auto Setup")]
    [Tooltip("모든 Zone 오브젝트를 담는 부모 Transform (미할당 시 자동 탐색)")]
    [SerializeField] private Transform zoneContainer;

    private void Start()
    {
        // 일반 모드(New Game)면 아무것도 안 함
        if (!SaveManager.IsContinueMode)
        {
            Destroy(this);
            return;
        }

        int targetZone = SaveManager.PendingZoneIndex;
        SaveManager.ClearContinueZone();

        Debug.Log($"[ContinueZoneHandler] 이어하기 모드: Zone_{targetZone} 활성화");

        // Zone 오브젝트 찾기
        List<GameObject> allZones = FindAllZoneObjects();

        // 모든 Zone 비활성화 → 타겟 Zone만 활성화
        foreach (var zone in allZones)
        {
            bool isTarget = ExtractZoneNumber(zone.name) == targetZone;
            zone.SetActive(isTarget);
        }

        // 플레이어를 Zone 위치로 이동
        TeleportPlayerToZone(targetZone);

        Destroy(this);
    }

    /// <summary>
    /// 씬에서 "Zone_{number}_*" 패턴의 모든 오브젝트를 찾습니다.
    /// </summary>
    private List<GameObject> FindAllZoneObjects()
    {
        List<GameObject> results = new List<GameObject>();

        // zoneContainer가 지정되어 있으면 그 안에서만 탐색
        Transform root = zoneContainer;
        if (root == null)
        {
            // 씬 전체 탐색
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go == null || go.hideFlags != HideFlags.None) continue;
                if (!go.scene.IsValid() || !go.scene.isLoaded) continue;
                if (go.name.StartsWith("Zone_") && go.transform.parent != null)
                    results.Add(go);
            }
        }
        else
        {
            SearchInChildren(root, results);
        }

        return results;
    }

    private void SearchInChildren(Transform parent, List<GameObject> results)
    {
        if (parent.name.StartsWith("Zone_"))
            results.Add(parent.gameObject);

        foreach (Transform child in parent)
            SearchInChildren(child, results);
    }

    /// <summary>
    /// "Zone_{number}_..." 형식의 이름에서 number를 추출합니다.
    /// </summary>
    private int ExtractZoneNumber(string zoneName)
    {
        // "Zone_0_Images" → 0
        string[] parts = zoneName.Split('_');
        if (parts.Length >= 2 && int.TryParse(parts[1], out int number))
            return number;
        return -1;
    }

    /// <summary>
    /// 플레이어를 해당 Zone의 위치로 이동시킵니다.
    /// ZoneChanger의 transform.position을 기준으로 이동합니다.
    /// </summary>
    private void TeleportPlayerToZone(int zoneIndex)
    {
        // ZoneChanger 중에서 해당 zone을 target으로 하는 것을 찾음
        foreach (var zc in FindObjectsByType<ZoneChanger>(FindObjectsSortMode.None))
        {
            if (zc.toZoneNumber == zoneIndex)
            {
                // ZoneChanger 위치로 플레이어 이동
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    player.transform.position = zc.transform.position;
                    Debug.Log($"[ContinueZoneHandler] 플레이어를 Zone_{zoneIndex} 시작점으로 이동: {zc.transform.position}");
                }
                return;
            }
        }

        Debug.LogWarning($"[ContinueZoneHandler] Zone_{zoneIndex} 의 ZoneChanger를 찾을 수 없습니다.");
    }
}
