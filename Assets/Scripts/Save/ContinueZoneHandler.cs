using System.Collections.Generic;
using HideAndInk.Core.Managers;
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
        // Transform 참조 해결 (Inspector > Registry > 씬 탐색 순)
        ResolveTransforms();

        List<GameObject> allZones = FindAllZoneObjects();

        if (SaveManager.IsContinueMode)
            HandleContinue(allZones);
        else
            HandleNewGame(allZones);

        Destroy(this);
    }

    /// <summary>
    /// Player/치치 Transform 참조를 다음 순서로 해결:
    /// 1. Inspector에 직접 할당된 값
    /// 2. CharacterRegistry에 등록된 값 (PlayerMovementAdapter가 자동 등록)
    /// 3. 씬에서 태그/이름으로 탐색 (최후의 fallback)
    /// </summary>
    private void ResolveTransforms()
    {
        // Player
        if (playerTransform == null)
            playerTransform = CharacterRegistry.Player;
        if (playerTransform == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null)
            {
                playerTransform = go.transform;
                Debug.Log($"[ContinueZoneHandler] Player 씬 탐색: {go.name} at {go.transform.position}");
            }
        }

        // 치치
        if (squidTransform == null)
            squidTransform = CharacterRegistry.Squid;
        if (squidTransform == null)
        {
            var go = GameObject.Find("Chichi_Robot") ?? GameObject.Find("치치");
            if (go != null)
            {
                squidTransform = go.transform;
                Debug.Log($"[ContinueZoneHandler] 치치 씬 탐색: {go.name} at {go.transform.position}");
            }
        }

        Debug.Log($"[ContinueZoneHandler] Player 참조: {(playerTransform != null ? $"{playerTransform.name} at {playerTransform.position}" : "없음")}");
        Debug.Log($"[ContinueZoneHandler] 치치 참조: {(squidTransform != null ? $"{squidTransform.name} at {squidTransform.position}" : "없음")}");
    }

    private Transform GetPlayer() => playerTransform;
    private Transform GetSquid() => squidTransform;

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

        // ── Ground 동기화, 안개 효과, Canvas_Ingame 처리 ──
        ZoneChanger.SyncGroundObjects(targetZone);
        if (ZoneChanger.IsFogZone(targetZone))
        {
            bool isDark = ZoneChanger.IsDarkFogZone(targetZone);
            ZoneChanger.SetUnderwaterEffect(true, isDark);
        }
        else
        {
            ZoneChanger.SetUnderwaterEffect(false);
        }
        UpdateCanvasIngame(targetZone);

        Transform player = GetPlayer();
        Transform squid = GetSquid();

        // ---- Player 위치 복원 ----
        if (player != null)
        {
            if (playerPos != Vector3.zero)
            {
                // Player는 Rigidbody가 있으므로 Transform 직접 설정 + Rigidbody 동기화
                Rigidbody rb = player.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.position = playerPos;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    Debug.Log($"[ContinueZoneHandler] Player 위치 복원 (Rigidbody): {playerPos}");
                }
                else
                {
                    player.position = playerPos;
                    Debug.Log($"[ContinueZoneHandler] Player 위치 복원 (Transform): {playerPos}");
                }
            }
            else
            {
                Debug.Log($"[ContinueZoneHandler] 저장된 Player 위치 없음 → ZoneChanger 위치로 fallback");
                TeleportPlayerToZone(targetZone);
                playerPos = player.position;
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

        // ---- 치메라를 Player 위치로 즉시 이동 ----
        GameManager.MoveCameraToPlayer(player?.gameObject, playerPos);
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

    private void UpdateCanvasIngame(int zoneNumber)
    {
        var canvasIngame = GameObject.Find("Canvas_Ingame");
        if (canvasIngame != null)
        {
            bool shouldBeActive = zoneNumber >= 1 && zoneNumber <= 6;
            canvasIngame.SetActive(shouldBeActive);
            Debug.Log($"[ContinueZoneHandler] Canvas_Ingame = {shouldBeActive} (Zone {zoneNumber})");
        }
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
                Vector3 targetPos = zc.transform.position;
                Rigidbody rb = player.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.position = targetPos;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                else
                {
                    player.position = targetPos;
                }
                Debug.Log($"[ContinueZoneHandler] 플레이어를 Zone_{zoneIndex} 시작점으로 이동: {targetPos}");
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