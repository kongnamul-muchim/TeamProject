using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 구역 전환 트리거: 플레이어가 닿으면 현재 구역을 끄고 다음 구역을 켭니다.
/// 
/// 사용 방법:
/// 1. Inspector에서 직접 할당:
///    deactivateZones: 끌 구역 오브젝트들 (Zone_1_Images, Zone_1_Object 등)
///    activateZones: 켤 구역 오브젝트들 (Zone_2_Images, Zone_2_Object 등)
/// 
/// 2. 자동 탐색 (Inspector 할당 없으면 번호로 자동 찾음):
///    fromZoneNumber = 1 → "Zone_1_*" 패턴의 오브젝트들을 자동 비활성화
///    toZoneNumber = 2   → "Zone_2_*" 패턴의 오브젝트들을 자동 활성화
/// </summary>
public class ZoneChanger : MonoBehaviour
{
    [Header("전환할 구역 (직접 할당)")]
    [Tooltip("비활성화할 구역 오브젝트들")]
    public GameObject[] deactivateZones;

    [Tooltip("활성화할 구역 오브젝트들")]
    public GameObject[] activateZones;

    [Header("자동 탐색 (Inspector 할당 없을 시 사용)")]
    [Tooltip("비활성화할 구역 번호 (예: 1 = Zone_1_*)")]
    public int fromZoneNumber;

    [Tooltip("활성화할 구역 번호 (예: 2 = Zone_2_*)")]
    public int toZoneNumber;

    private bool _alreadyTriggered = false;

    private void Start()
    {
        // 자동 탐색: Inspector 할당이 없으면 이름으로 찾기
        if ((deactivateZones == null || deactivateZones.Length == 0) && fromZoneNumber > 0)
        {
            deactivateZones = FindZoneObjects(fromZoneNumber);
        }

        if ((activateZones == null || activateZones.Length == 0) && toZoneNumber > 0)
        {
            activateZones = FindZoneObjects(toZoneNumber);
        }

        Debug.Log($"[ZoneChanger] '{name}' 초기화: " +
            $"비활성화={fromZoneNumber}({(deactivateZones != null ? deactivateZones.Length : 0)}개), " +
            $"활성화={toZoneNumber}({(activateZones != null ? activateZones.Length : 0)}개), " +
            $"위치={transform.position}");
    }

    // 3D 콜라이더용 트리거
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[ZoneChanger] '{name}' OnTriggerEnter: {other.gameObject.name} (tag={other.gameObject.tag})");
        if (other.CompareTag("Player") && !_alreadyTriggered)
        {
            ChangeZone();
        }
    }

    // 2D 콜라이더용 트리거
    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log($"[ZoneChanger] '{name}' OnTriggerEnter2D: {other.gameObject.name} (tag={other.gameObject.tag})");
        if (other.CompareTag("Player") && !_alreadyTriggered)
        {
            ChangeZone();
        }
    }

    void ChangeZone()
    {
        _alreadyTriggered = true;
        bool changed = false;

        if (deactivateZones != null)
        {
            foreach (var zone in deactivateZones)
            {
                if (zone != null)
                {
                    Debug.Log($"[ZoneChanger] 비활성화: {zone.name}");
                    zone.SetActive(false);
                }
            }
            changed = true;
        }

        if (activateZones != null)
        {
            foreach (var zone in activateZones)
            {
                if (zone != null)
                {
                    Debug.Log($"[ZoneChanger] 활성화: {zone.name}");
                    zone.SetActive(true);
                }
            }
            changed = true;
        }

        if (changed)
        {
            string fromName = fromZoneNumber > 0 ? $"Zone_{fromZoneNumber}" : "?";
            string toName = toZoneNumber > 0 ? $"Zone_{toZoneNumber}" : "?";
            Debug.Log($"[ZoneChanger] 구역 전환 완료: {fromName} → {toName}");
        }
        else
        {
            Debug.LogWarning($"[ZoneChanger] '{name}': 전환할 구역이 설정되지 않았습니다!");
        }
    }

    /// <summary>
    /// "Zone_{number}_*" 패턴의 오브젝트를 씬에서 찾습니다.
    /// Resources.FindObjectsOfTypeAll + 씬 루트 재귀 탐색 모두 사용하여
    /// 비활성 오브젝트도 확실히 찾습니다.
    /// </summary>
    static GameObject[] FindZoneObjects(int zoneNumber)
    {
        string prefix = $"Zone_{zoneNumber}_";
        List<GameObject> found = new List<GameObject>();
        HashSet<int> added = new HashSet<int>();

        // 방법 1: Resources.FindObjectsOfTypeAll (비활성 포함)
        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go == null) continue;
            // 에셋이나 프리팹은 제외 (씬에 있는 것만)
            if (go.hideFlags != HideFlags.None) continue;
            if (!go.scene.IsValid() || !go.scene.isLoaded) continue;
            if (added.Contains(go.GetInstanceID())) continue;

            if (go.name.Trim().StartsWith(prefix))
            {
                found.Add(go);
                added.Add(go.GetInstanceID());
            }
        }

        // 방법 2: 씬 루트에서 재귀 탐색 (비활성 자식도 찾음)
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;

            foreach (var root in scene.GetRootGameObjects())
            {
                SearchInChildren(root.transform, prefix, found, added);
            }
        }

        if (found.Count == 0)
        {
            Debug.LogWarning($"[ZoneChanger] Zone_{zoneNumber}_* 오브젝트를 찾을 수 없습니다!");
        }
        else
        {
            string names = string.Join(", ", found.ConvertAll(g => g.name.Trim()));
            Debug.Log($"[ZoneChanger] Zone_{zoneNumber}_* 발견: {found.Count}개 ({names})");
        }

        return found.ToArray();
    }

    /// <summary>
    /// 트랜스폼 트리를 재귀적으로 탐색하여 이름이 접두사와 일치하는 오브젝트를 찾습니다.
    /// 비활성 자식도 포함합니다.
    /// </summary>
    static void SearchInChildren(Transform parent, string prefix, List<GameObject> found, HashSet<int> added)
    {
        if (parent.name.Trim().StartsWith(prefix))
        {
            if (!added.Contains(parent.gameObject.GetInstanceID()))
            {
                found.Add(parent.gameObject);
                added.Add(parent.gameObject.GetInstanceID());
            }
        }

        foreach (Transform child in parent)
        {
            SearchInChildren(child, prefix, found, added);
        }
    }
}