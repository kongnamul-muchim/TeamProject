using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using HideAndInk.Core.Transition;
using HideAndInk.CameraSystem;

/// <summary>
/// 구역 전환 트리거: 플레이어가 닿으면 현재 구역을 끄고 다음 구역을 켭니다.
/// 패턴 트랜지션 효과와 함께 화면 전환이 진행됩니다.
/// 
/// 사용 방법:
/// 1. Inspector에서 직접 할당:
///    deactivateZones: 끌 구역 오브젝트들 (Zone_1_Images, Zone_1_Object 등)
///    activateZones: 켤 구역 오브젝트들 (Zone_2_Images, Zone_2_Object 등)
/// 
/// 2. 자동 탐색 (Inspector 할당 없으면 번호로 자동 찾음):
///    fromZoneNumber = 1 → "Zone_1_*" 패턴의 오브젝트들을 자동 비활성화
///    toZoneNumber = 2   → "Zone_2_*" 패턴의 오브젝트들을 자동 활성화
/// 
/// 3. 트랜지션 효과:
///    useTransition = true → 패턴 트랜지션 효과와 함께 구역 전환
///    useTransition = false → 즉시 구역 전환 (트랜지션 없음)
/// 
/// 4. 카메라 이동:
///    moveCameraTo를 설정하면 트랜지션 중 화면이 덮인 사이에 카메라를 이동합니다.
///    CameraFollow가 있으면 추적을 잠시 멈추고 이동 후 재개합니다.
/// 
/// 5. 투명 벽 (돌아갈 수 없게 차단):
///    createInvisibleWall = true → 트리거 위치에 자동으로 투명 벽 생성
///    invisibleWalls → 수동으로 만든 벽 오브젝트를 연결 (자동 생성 대신/추가로 사용)
/// </summary>
public class ZoneChanger : MonoBehaviour
{
    [Header("전환할 구역 (직접 할당)")]
    [Tooltip("비활성화할 구역 오브젝트들")]
    public GameObject[] deactivateZones;

    [Tooltip("활성화할 구역 오브젝트들")]
    public GameObject[] activateZones;

    [Header("자동 탐색 (Inspector 할당 없을 시 사용)")]
    [Tooltip("비활성화할 구역 번호 (예: 0 = Zone_0_*, 1 = Zone_1_*). -1이면 자동 탐색 안 함")]
    public int fromZoneNumber = -1;

    [Tooltip("활성화할 구역 번호 (예: 1 = Zone_1_*, 2 = Zone_2_*). -1이면 자동 탐색 안 함")]
    public int toZoneNumber = -1;

    [Header("트랜지션 설정")]
    [Tooltip("패턴 트랜지션 효과 사용 여부")]
    public bool useTransition = true;

    [Header("카메라 이동")]
    [Tooltip("트랜지션 중 카메라를 이동할 목적지 좌표 (체크하면 이동함)")]
    public bool enableCameraMove = false;

    [Tooltip("카메라가 이동할 목적지 좌표")]
    public Vector3 moveCameraTo = Vector3.zero;

    [Tooltip("카메라 이동 완료 후 CameraFollow의 타겟 위치로 스냅할지 여부")]
    public bool snapToTargetAfterMove = true;

    [Header("투명 벽 (돌아갈 수 없게 차단)")]
    [Tooltip("트리거 발동 시 투명 벽을 자동 생성할지 여부")]
    public bool createInvisibleWall = true;

    [Tooltip("자동 생성할 투명 벽의 크기 (X=너비, Y=높이, Z=깊이). 0이면 트리거 콜라이더 크기 자동 사용")]
    public Vector3 wallSize = new Vector3(0f, 0f, 0f);

    [Tooltip("트리거 위치로부터 벽의 오프셋 (벽을 트리거 뒤에 배치하려면 음수 X 사용)")]
    public Vector3 wallOffset = Vector3.zero;

    [Tooltip("수동으로 만든 투명 벽 오브젝트들 (자동 생성과 함께 사용 가능)")]
    public GameObject[] invisibleWalls;

    [Header("DI - 카메라 추적 (미할당 시 자동 탐색)")]
    [Tooltip("CameraFollow 컴포넌트 (미할당 시 씬에서 자동 탐색)")]
    [SerializeField] private CameraFollow cameraFollow;

    private bool _alreadyTriggered = false;
    private GameObject _autoCreatedWall;

    private void Start()
    {
        // 자동 탐색: Inspector 할당이 없으면 이름으로 찾기
        if ((deactivateZones == null || deactivateZones.Length == 0) && fromZoneNumber >= 0)
        {
            deactivateZones = FindZoneObjects(fromZoneNumber);
        }

        if ((activateZones == null || activateZones.Length == 0) && toZoneNumber >= 0)
        {
            activateZones = FindZoneObjects(toZoneNumber);
        }

        // CameraFollow 자동 탐색
        if (cameraFollow == null)
        {
            cameraFollow = FindObjectOfType<CameraFollow>();
        }

        Debug.Log($"[ZoneChanger] '{name}' 초기화: " +
            $"비활성화={fromZoneNumber}({(deactivateZones != null ? deactivateZones.Length : 0)}개), " +
            $"활성화={toZoneNumber}({(activateZones != null ? activateZones.Length : 0)}개), " +
            $"카메라이동={enableCameraMove}, " +
            $"투명벽={createInvisibleWall}, " +
            $"위치={transform.position}");
    }

    // 3D 콜라이더용 트리거
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[ZoneChanger] '{name}' OnTriggerEnter: {other.gameObject.name} (tag={other.gameObject.tag})");
        if (other.CompareTag("Player") && !_alreadyTriggered)
        {
            TriggerZoneChange();
        }
    }

    // 2D 콜라이더용 트리거
    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log($"[ZoneChanger] '{name}' OnTriggerEnter2D: {other.gameObject.name} (tag={other.gameObject.tag})");
        if (other.CompareTag("Player") && !_alreadyTriggered)
        {
            TriggerZoneChange();
        }
    }

    /// <summary>
    /// 트랜지션 설정에 따라 구역 전환을 시작합니다.
    /// useTransition이 true면 패턴 트랜지션 효과와 함께 전환,
    /// false면 즉시 전환합니다.
    /// </summary>
    private void TriggerZoneChange()
    {
        _alreadyTriggered = true;

        if (useTransition && PatternTransitionController.Instance != null)
        {
            // 카메라 추적 일시정지
            if (enableCameraMove && cameraFollow != null)
            {
                cameraFollow.Pause();
            }

            // 트랜지션 인 → 투명 벽 생성 → 구역 전환 + 카메라 이동 → 트랜지션 아웃
            // 주의: 투명 벽을 ChangeZone()보다 먼저 생성해야
            //       Zone 비활성화 시 벽이 같이 사라지는 문제를 방지할 수 있음
            PatternTransitionController.Instance.PlayIn(() =>
            {
                ActivateInvisibleWalls();
                ChangeZone();
                MoveCamera();

                // 카메라 이동 완료 후 추적 재개
                if (enableCameraMove && cameraFollow != null)
                {
                    cameraFollow.Resume(snapToTargetAfterMove);
                }

                PatternTransitionController.Instance.PlayOut();
            });
        }
        else
        {
            // 트랜지션 없이 즉시 전환
            ActivateInvisibleWalls();
            ChangeZone();

            if (enableCameraMove)
            {
                MoveCameraInstant();
            }
        }
    }

    /// <summary>
    /// 투명 벽을 활성화합니다.
    /// createInvisibleWall이 true면 트리거 위치에 자동 생성하고,
    /// invisibleWalls에 수동 할당된 벽도 함께 활성화합니다.
    /// </summary>
    private void ActivateInvisibleWalls()
    {
        // 자동 생성 모드: 트리거 위치에 투명 벽 생성
        if (createInvisibleWall)
        {
            CreateInvisibleWall();
        }

        // 수동 할당된 투명 벽 활성화
        if (invisibleWalls != null)
        {
            foreach (var wall in invisibleWalls)
            {
                if (wall != null)
                {
                    wall.SetActive(true);
                    Debug.Log($"[ZoneChanger] 투명 벽 활성화 (수동): {wall.name}");
                }
            }
        }
    }

    /// <summary>
    /// 트리거 위치에 투명 벽 GameObject를 자동 생성합니다.
    /// 렌더러가 없는 콜라이더만 포함하여 시각적으로 보이지 않습니다.
    /// 2D와 3D 물리 모두 지원하기 위해 자식 GameObject에 각각 콜라이더를 추가합니다.
    /// (Unity에서 BoxCollider와 BoxCollider2D를 같은 GameObject에 추가할 수 없기 때문)
    /// 벽은 항상 씬 루트에 생성되어 Zone 비활성화에 영향받지 않습니다.
    /// </summary>
    private void CreateInvisibleWall()
    {
        // 이미 생성된 벽이 있으면 중복 생성 방지
        if (_autoCreatedWall != null)
        {
            _autoCreatedWall.SetActive(true);
            Debug.Log($"[ZoneChanger] 기존 투명 벽 재활성화: {_autoCreatedWall.name}");
            return;
        }

        // 투명 벽 부모 GameObject 생성 (항상 씬 루트에 → Zone 비활성화와 무관하게 유지)
        _autoCreatedWall = new GameObject($"InvisibleWall_{name}");

        // 트리거 위치 + 오프셋에 배치
        Vector3 wallPosition = transform.position + wallOffset;
        _autoCreatedWall.transform.position = wallPosition;

        // 벽 크기 결정: wallSize의 각 축이 0이면 트리거 콜라이더 크기로 대체
        // (이전 Vector2 직렬화 데이터에서 Z=0으로 로드되는 문제 방지)
        Vector3 finalSize = wallSize;
        Vector3 triggerSize = GetTriggerColliderSize();
        // 최소 크기 보장 (너무 작으면 통과 가능)
        triggerSize = Vector3.Max(triggerSize, new Vector3(2f, 10f, 2f));

        if (finalSize.x <= 0) finalSize.x = triggerSize.x;
        if (finalSize.y <= 0) finalSize.y = triggerSize.y;
        if (finalSize.z <= 0) finalSize.z = triggerSize.z;

        // 3D BoxCollider - 부모에 직접 추가
        var collider3D = _autoCreatedWall.AddComponent<BoxCollider>();
        collider3D.size = finalSize;
        collider3D.isTrigger = false;

        // 2D BoxCollider - 자식 GameObject에 추가 (Unity에서 같은 GameObject에 추가 불가)
        var child2D = new GameObject("Collider2D");
        child2D.transform.SetParent(_autoCreatedWall.transform);
        child2D.transform.localPosition = Vector3.zero;
        child2D.transform.localRotation = Quaternion.identity;
        child2D.transform.localScale = Vector3.one;
        var collider2D = child2D.AddComponent<BoxCollider2D>();
        collider2D.size = new Vector2(finalSize.x, finalSize.y);
        collider2D.usedByComposite = false;
        collider2D.isTrigger = false;

        Debug.Log($"[ZoneChanger] 투명 벽 자동 생성: {_autoCreatedWall.name} " +
            $"위치={wallPosition}, 크기={finalSize} " +
            $"(3D BoxCollider=부모, 2D BoxCollider2D=자식)");
    }

    /// <summary>
    /// 트리거에 부착된 콜라이더의 크기를 반환합니다.
    /// 3D BoxCollider, 2D BoxCollider2D 순서로 확인합니다.
    /// </summary>
    private Vector3 GetTriggerColliderSize()
    {
        // 3D BoxCollider 확인
        var box3D = GetComponent<BoxCollider>();
        if (box3D != null)
        {
            return Vector3.Scale(box3D.size, transform.lossyScale);
        }

        // 2D BoxCollider2D 확인
        var box2D = GetComponent<BoxCollider2D>();
        if (box2D != null)
        {
            Vector2 size2D = box2D.size;
            Vector3 scale = transform.lossyScale;
            return new Vector3(size2D.x * scale.x, size2D.y * scale.y, 2f);
        }

        // 기본값
        return new Vector3(2f, 10f, 2f);
    }

    /// <summary>
    /// 카메라를 moveCameraTo 좌표로 즉시 이동합니다.
    /// 트랜지션 없이 구역 전환 시 사용됩니다.
    /// </summary>
    private void MoveCameraInstant()
    {
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            Vector3 newPos = moveCameraTo;
            newPos.z = mainCam.transform.position.z;
            mainCam.transform.position = newPos;
            Debug.Log($"[ZoneChanger] 카메라 즉시 이동: {newPos}");
        }

        if (cameraFollow != null)
        {
            cameraFollow.Resume(snapToTargetAfterMove);
        }
    }

    /// <summary>
    /// 카메라를 moveCameraTo 좌표로 이동합니다.
    /// 트랜지션 중 화면이 덮인 상태에서 호출되므로 이동이 보이지 않습니다.
    /// </summary>
    private void MoveCamera()
    {
        if (!enableCameraMove) return;

        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            Vector3 newPos = moveCameraTo;
            newPos.z = mainCam.transform.position.z;
            mainCam.transform.position = newPos;
            Debug.Log($"[ZoneChanger] 카메라 이동: {newPos}");
        }
    }

    void ChangeZone()
    {
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
            string fromName = fromZoneNumber >= 0 ? $"Zone_{fromZoneNumber}" : "?";
            string toName = toZoneNumber >= 0 ? $"Zone_{toZoneNumber}" : "?";
            Debug.Log($"[ZoneChanger] 구역 전환 완료: {fromName} → {toName}");
        }
        else
        {
            Debug.LogWarning($"[ZoneChanger] '{name}': 전환할 구역이 설정되지 않았습니다!");
        }
    }

    /// <summary>
    /// "Zone_{number}_*" 패턴과 "Ground_{number:D2}" 오브젝트를 씬에서 찾습니다.
    /// Resources.FindObjectsOfTypeAll + 씬 루트 재귀 탐색 모두 사용하여
    /// 비활성 오브젝트도 확실히 찾습니다.
    /// </summary>
    static GameObject[] FindZoneObjects(int zoneNumber)
    {
        string prefix = $"Zone_{zoneNumber}_";
        string groundName = $"Ground_{zoneNumber:D2}";
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

        // 방법 3: Ground_{zoneNumber:D2} 탐색 (Zone 외 Ground 오브젝트 포함)
        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go == null) continue;
            if (go.hideFlags != HideFlags.None) continue;
            if (!go.scene.IsValid() || !go.scene.isLoaded) continue;
            if (added.Contains(go.GetInstanceID())) continue;

            if (go.name.Trim() == groundName)
            {
                found.Add(go);
                added.Add(go.GetInstanceID());
            }
        }

        if (found.Count == 0)
        {
            Debug.LogWarning($"[ZoneChanger] Zone_{zoneNumber}_* / {groundName} 오브젝트를 찾을 수 없습니다!");
        }
        else
        {
            string names = string.Join(", ", found.ConvertAll(g => g.name.Trim()));
            Debug.Log($"[ZoneChanger] Zone_{zoneNumber} (Zone+Ground) 발견: {found.Count}개 ({names})");
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