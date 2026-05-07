using HideAndInk.Core.Audio;
using HideAndInk.Core.Interfaces;
using HideAndInk.Core.Managers;
using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;
using HideAndInk.Core.Transition;
using HideAndInk.CameraSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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

    [Header("UI - Canvas_Ingame")]
    [Tooltip("Zone 1~6에서 활성화할 Canvas_Ingame 오브젝트. 미할당 시 자동 탐색")]
    public GameObject canvasIngame;

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

    [Header("왼쪽 경계 벽 (Zone 시작점 추락 방지)")]
    [Tooltip("새 Zone으로 전환 후, 시작점 왼쪽 경계에 투명 벽을 자동 생성할지 여부")]
    public bool createLeftBoundaryWall = true;

    [Tooltip("왼쪽 경계 벽의 크기 (X=너비, Y=높이). 0이면 기본값(2, 20) 사용")]
    public Vector2 leftBoundaryWallSize = new Vector2(2f, 20f);

    [Tooltip("시작점(왼쪽 끝)으로부터 벽을 배치할 오프셋. 음수면 왼쪽, 양수면 오른쪽")]
    public float leftBoundaryOffset = -5f;

    [Tooltip("수동으로 만든 왼쪽 경계 벽 오브젝트들 (자동 생성과 함께 사용 가능)")]
    public GameObject[] leftBoundaryWalls;

    [Header("Zone별 벽 관리 (폴드 방식)")]
    [Tooltip("Wall_01 ~ Wall_05 등이 들어있는 부모 컨테이너 (빈 오브젝트)")]
    public GameObject wallContainer;

    [Tooltip("이 Zone이 활성화될 때 켤 벽 이름 (예: Wall_01). 비워두면 사용 안 함")]
    public string activeWallName = "";

    [Header("Events")]
    [Tooltip("구역 전환 완료 시 호출 (매개변수: toZoneNumber)")]
    public UnityEvent<int> onZoneChanged;

    [Header("DI - 칼라이동 추적 (미할당 시 자동 탐색)")]
    [Tooltip("CameraFollow 컴포넌트 (미할당 시 씬에서 자동 탐색)")]
    [SerializeField] private CameraFollow cameraFollow;

        private ISfxService _sfxService;
        private IBgmService _bgmService;
        private bool _alreadyTriggered = false;
    private GameObject _autoCreatedWall;
    private GameObject _autoCreatedLeftBoundaryWall;

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

            // SFX 서비스 해결
            if (GameManager.Container != null && GameManager.Container.IsRegistered<ISfxService>())
                _sfxService = GameManager.Container.Resolve<ISfxService>();

            // BGM 서비스 해결
            if (GameManager.Container != null && GameManager.Container.IsRegistered<IBgmService>())
                _bgmService = GameManager.Container.Resolve<IBgmService>();

        // 게임 시작 시 현재 활성 Zone의 Ground 동기화
        // (deactivateZones[0]이 활성화되어 있으면 fromZoneNumber가 현재 활성 Zone)
        if (fromZoneNumber >= 0 && deactivateZones != null && deactivateZones.Length > 0)
        {
            bool isCurrentZoneActive = false;
            foreach (var zone in deactivateZones)
            {
                if (zone != null && zone.activeSelf)
                {
                    isCurrentZoneActive = true;
                    break;
                }
            }

            if (isCurrentZoneActive)
            {
                SyncGroundObjects(fromZoneNumber);
                Debug.Log($"[ZoneChanger] '{name}' 초기 Ground 동기화: Zone_{fromZoneNumber} (Ground_{fromZoneNumber:D2} 활성화)");
            }

            // 게임 시작 시 Fog Zone(1,2,3,6)이면 Underwater Effects 활성화
            if (isCurrentZoneActive && IsFogZone(fromZoneNumber))
            {
                bool isDark = IsDarkFogZone(fromZoneNumber);
                SetUnderwaterEffect(true, isDark);
                Debug.Log($"[ZoneChanger] 게임 시작 - Zone {fromZoneNumber}, Underwater Effects {(isDark ? "어둡게" : "밝게")} 활성화");
            }
            else if (isCurrentZoneActive && !IsFogZone(fromZoneNumber))
            {
                SetUnderwaterEffect(false);
                Debug.Log($"[ZoneChanger] 게임 시작 - Zone {fromZoneNumber}, Underwater Effects 비활성화");
            }
        }

        // Canvas_Ingame 자동 탐색 및 초기 상태 설정
        if (canvasIngame == null)
        {
            canvasIngame = GameObject.Find("Canvas_Ingame");
        }
        int currentZone = fromZoneNumber >= 0 ? fromZoneNumber : toZoneNumber;
        UpdateCanvasIngame(currentZone);

        Debug.Log($"[ZoneChanger] '{name}' 초기화: " +
            $"비활성화={fromZoneNumber}({(deactivateZones != null ? deactivateZones.Length : 0)}개), " +
            $"활성화={toZoneNumber}({(activateZones != null ? activateZones.Length : 0)}개), " +
            $"칼라이동={enableCameraMove}, " +
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

        if (useTransition && FadeInTransitionController.Instance != null)
        {
            // 칼라이동 추적 일시정지
            if (enableCameraMove && cameraFollow != null)
            {
                cameraFollow.Pause();
            }

            // 트랜지션 인 → 투명 벽 생성 → 구역 전환 + 칼라이동 → 트랜지션 아웃
            // 주의: 투명 벽을 ChangeZone()보다 먼저 생성해야
            //       Zone 비활성화 시 벽이 같이 사라지는 문제를 방지할 수 있음
            FadeInTransitionController.Instance.PlayIn(() =>
            {
                ActivateInvisibleWalls();
                ChangeZone();
                MoveCamera();

                // 칼라이동 완료 후 추적 재개
                if (enableCameraMove && cameraFollow != null)
                {
                    cameraFollow.Resume(snapToTargetAfterMove);
                }

                FadeInTransitionController.Instance.PlayOut();
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

        // 플레이어 위치를 기준으로 콜라이더 중심을 플레이어 반대편으로 offset
        // (플레이어가 벽 생성 시점에 벽 영역 안에 있으면 갇히는 문제 방지)
        Vector3 colliderCenterOffset = Vector3.zero;
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            Vector3 toPlayer = playerObj.transform.position - wallPosition;
            colliderCenterOffset = new Vector3(
                -Mathf.Sign(toPlayer.x) * finalSize.x * 0.5f,
                -Mathf.Sign(toPlayer.y) * finalSize.y * 0.5f,
                -Mathf.Sign(toPlayer.z) * finalSize.z * 0.5f
            );
        }

        // 3D BoxCollider - 부모에 직접 추가
        var collider3D = _autoCreatedWall.AddComponent<BoxCollider>();
        collider3D.size = finalSize;
        collider3D.center = colliderCenterOffset;
        collider3D.isTrigger = false;

        // 2D BoxCollider - 자식 GameObject에 추가 (Unity에서 같은 GameObject에 추가 불가)
        var child2D = new GameObject("Collider2D");
        child2D.transform.SetParent(_autoCreatedWall.transform);
        child2D.transform.localPosition = Vector3.zero;
        child2D.transform.localRotation = Quaternion.identity;
        child2D.transform.localScale = Vector3.one;
        var collider2D = child2D.AddComponent<BoxCollider2D>();
        collider2D.size = new Vector2(finalSize.x, finalSize.y);
        collider2D.offset = new Vector2(colliderCenterOffset.x, colliderCenterOffset.y);
        collider2D.usedByComposite = false;
        collider2D.isTrigger = false;

        Debug.Log($"[ZoneChanger] 투명 벽 자동 생성: {_autoCreatedWall.name} " +
            $"위치={wallPosition}, 크기={finalSize}, centerOffset={colliderCenterOffset} " +
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

                    // 자식 중 BoundaryWall 태그를 가진 오브젝트 자동 활성화
                    ActivateBoundaryWallsInZone(zone.transform);

                    // 자식 중 보스 트리거 자동 활성화
                    ActivateBossTriggersInZone(zone.transform);
                }
            }
            changed = true;
        }

        // ── 안개 효과 전환 (URP FullScreenPassRendererFeature) ──
        if (toZoneNumber >= 0)
        {
            if (IsFogZone(toZoneNumber))
            {
                bool isDark = IsDarkFogZone(toZoneNumber);
                SetUnderwaterEffect(true, isDark);
                Debug.Log($"[ZoneChanger] Underwater Effects {(isDark ? "어둡게" : "밝게")} 활성화 (Zone {toZoneNumber} 진입)");
            }
            else
            {
                SetUnderwaterEffect(false);
                Debug.Log($"[ZoneChanger] Underwater Effects 비활성화 (Zone {toZoneNumber} 진입)");
            }
        }

        // ── Ground 동기화: 현재 Zone에 해당하는 Ground만 활성화, 나머지는 비활성화 ──
        if (toZoneNumber >= 0)
        {
            SyncGroundObjects(toZoneNumber);
        }

        if (changed)
        {
            string fromName = fromZoneNumber >= 0 ? $"Zone_{fromZoneNumber}" : "?";
            string toName = toZoneNumber >= 0 ? $"Zone_{toZoneNumber}" : "?";
            Debug.Log($"[ZoneChanger] 구역 전환 완료: {fromName} → {toName}");

            // 구역 전환 이벤트 발생 (Save 등 외부에서 구독)
            if (toZoneNumber >= 0)
                onZoneChanged?.Invoke(toZoneNumber);

            // 구역 전환 효과음 재생
            _sfxService?.Play(SfxId.StageClear);

            // BGM 전환 (Zone 1~5 → BgmId)
            if (_bgmService != null && toZoneNumber >= 1 && toZoneNumber <= 5)
                _bgmService.Play((BgmId)toZoneNumber);
        }
        else
        {
            Debug.LogWarning($"[ZoneChanger] '{name}': 전환할 구역이 설정되지 않았습니다!");
        }

        // ── 왼쪽 경계 벽 생성: 새 Zone의 시작점 왼쪽에 투명 벽 생성 (추락 방지) ──
        if (createLeftBoundaryWall)
        {
            CreateLeftBoundaryWall();
        }

        // 수동으로 연결된 왼쪽 경계 벽도 함께 활성화
        if (leftBoundaryWalls != null)
        {
            foreach (var wall in leftBoundaryWalls)
            {
                if (wall != null)
                {
                    wall.SetActive(true);
                    Debug.Log($"[ZoneChanger] 왼쪽 경계 벽 활성화 (수동): {wall.name}");
                }
            }
        }

        // ── Zone별 벽 관리: 컨테이너에서 activeWallName만 활성화, 나머지는 비활성화 ──
        if (wallContainer != null && !string.IsNullOrEmpty(activeWallName))
        {
            ActivateSingleWallInContainer();
        }

        // ── Canvas_Ingame: Zone 1~6 활성화 ──
        UpdateCanvasIngame(toZoneNumber);
    }

    /// <summary>
    /// Canvas_Ingame의 활성화 상태를 Zone 번호에 따라 설정합니다.
    /// Zone 1~6에서는 활성화합니다.
    /// </summary>
    private void UpdateCanvasIngame(int zoneNumber)
    {
        if (canvasIngame == null) return;

        bool shouldBeActive = (zoneNumber >= 1 && zoneNumber <= 6);
        if (canvasIngame.activeSelf != shouldBeActive)
        {
            canvasIngame.SetActive(shouldBeActive);
            Debug.Log($"[ZoneChanger] Canvas_Ingame = {shouldBeActive} (Zone {zoneNumber})");
        }
    }

    /// <summary>
    /// 씬 내 모든 Ground 오브젝트를 찾아, 현재 활성 Zone 번호에 해당하는 것만 활성화하고
    /// 나머지는 비활성화합니다.
    /// </summary>
    private static void SyncGroundObjects(int activeZoneNumber)
    {
        GameObject[] allGrounds = FindAllGroundObjects();
        foreach (var ground in allGrounds)
        {
            int groundNumber = ExtractGroundNumber(ground.name);
            if (groundNumber < 0) continue;

            bool shouldBeActive = (groundNumber == activeZoneNumber);
            if (ground.activeSelf != shouldBeActive)
            {
                ground.SetActive(shouldBeActive);
                Debug.Log($"[ZoneChanger] Ground 동기화: {ground.name} = {shouldBeActive}");
            }
        }
    }

    /// <summary>
    /// Ground_01 ~ Ground_06 형식의 이름에서 번호를 추출합니다.
    /// </summary>
    private static int ExtractGroundNumber(string name)
    {
        const string prefix = "Ground_";
        if (!name.Trim().StartsWith(prefix)) return -1;

        string numberStr = name.Trim().Substring(prefix.Length);
        if (int.TryParse(numberStr, out int number))
        {
            return number;
        }
        return -1;
    }

    /// <summary>
    /// 씬 내 모든 Ground 오브젝트(Ground_*)를 찾습니다. 비활성 오브젝트도 포함합니다.
    /// </summary>
    static GameObject[] FindAllGroundObjects()
    {
        List<GameObject> grounds = new List<GameObject>();
        HashSet<int> added = new HashSet<int>();

        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go == null) continue;
            if (go.hideFlags != HideFlags.None) continue;
            if (!go.scene.IsValid() || !go.scene.isLoaded) continue;
            if (added.Contains(go.GetInstanceID())) continue;

            if (go.name.Trim().StartsWith("Ground_"))
            {
                grounds.Add(go);
                added.Add(go.GetInstanceID());
            }
        }

        return grounds.ToArray();
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

    /// <summary>
    /// 새 Zone의 왼쪽 경계(시작점)에 투명 벽을 생성합니다.
    /// 플레이어가 Zone 경계를 넘어 그라운드 밑으로 추락하는 것을 방지합니다.
    /// 벽은 씬 루트에 생성되어 Zone 비활성화와 무관하게 유지됩니다.
    /// </summary>
    private void CreateLeftBoundaryWall()
    {
        // 이미 생성된 벽이 있으면 중복 생성 방지
        if (_autoCreatedLeftBoundaryWall != null)
        {
            _autoCreatedLeftBoundaryWall.SetActive(true);
            Debug.Log($"[ZoneChanger] 기존 왼쪽 경계 벽 재활성화: {_autoCreatedLeftBoundaryWall.name}");
            return;
        }

        // 새 Zone의 시작점(왼쪽 끝) 위치 계산
        Vector3 boundaryPosition = Vector3.zero;
        bool foundPosition = false;

        // activateZones의 첫 번째 활성화된 오브젝트를 기준으로 왼쪽 경계 찾기
        if (activateZones != null && activateZones.Length > 0)
        {
            GameObject firstZone = activateZones[0];
            if (firstZone != null)
            {
                // Zone 오브젝트의 왼쪽 경계 (Renderer/SpriteRenderer 기준)
                SpriteRenderer sr = firstZone.GetComponentInChildren<SpriteRenderer>();
                if (sr != null)
                {
                    float leftEdge = sr.bounds.min.x;
                    boundaryPosition = new Vector3(leftEdge + leftBoundaryOffset, firstZone.transform.position.y, 0f);
                    foundPosition = true;
                }
                else
                {
                    // Renderer가 없으면 오브젝트 위치 기준
                    boundaryPosition = firstZone.transform.position + new Vector3(leftBoundaryOffset, 0f, 0f);
                    foundPosition = true;
                }
            }
        }

        // activateZones가 없으면 현재 칼라이동 위치나 트리거 위치 기준
        if (!foundPosition)
        {
            if (enableCameraMove)
            {
                boundaryPosition = moveCameraTo + new Vector3(leftBoundaryOffset, 0f, 0f);
            }
            else
            {
                boundaryPosition = transform.position + new Vector3(leftBoundaryOffset, 0f, 0f);
            }
        }

        // 투명 벽 생성 (씬 루트에)
        _autoCreatedLeftBoundaryWall = new GameObject($"LeftBoundaryWall_{name}");
        _autoCreatedLeftBoundaryWall.transform.position = boundaryPosition;

        // 벽 크기 설정
        Vector2 finalSize = leftBoundaryWallSize;
        if (finalSize.x <= 0) finalSize.x = 2f;
        if (finalSize.y <= 0) finalSize.y = 20f;

        // 3D BoxCollider
        var collider3D = _autoCreatedLeftBoundaryWall.AddComponent<BoxCollider>();
        collider3D.size = new Vector3(finalSize.x, finalSize.y, 2f);
        collider3D.isTrigger = false;

        // 2D BoxCollider
        var child2D = new GameObject("Collider2D");
        child2D.transform.SetParent(_autoCreatedLeftBoundaryWall.transform);
        child2D.transform.localPosition = Vector3.zero;
        child2D.transform.localRotation = Quaternion.identity;
        child2D.transform.localScale = Vector3.one;
        var collider2D = child2D.AddComponent<BoxCollider2D>();
        collider2D.size = finalSize;
        collider2D.isTrigger = false;

        Debug.Log($"[ZoneChanger] 왼쪽 경계 벽 자동 생성: {_autoCreatedLeftBoundaryWall.name} " +
            $"위치={boundaryPosition}, 크기={finalSize} " +
            $"(Zone 시작점 추락 방지)");
    }

    /// <summary>
    /// Zone 오브젝트의 자식들 중 이름에 "Wall" 또는 "Boundary"가 포함된 오브젝트를 자동으로 활성화합니다.
    /// 수동으로 배치한 벽을 Inspector에 연결하지 않아도 자동으로 인식됩니다.
    /// (태그를 정의할 필요 없이 이름만 맞추면 됨)
    /// </summary>
    private void ActivateBoundaryWallsInZone(Transform zoneTransform)
    {
        if (zoneTransform == null) return;

        // 직계 자식만 검색 (너무 깊게 들어가면 다른 Collider도 걸릴 수 있음)
        foreach (Transform child in zoneTransform)
        {
            string childNameLower = child.name.ToLower();
            if (childNameLower.Contains("wall") || childNameLower.Contains("boundary"))
            {
                child.gameObject.SetActive(true);
                Debug.Log($"[ZoneChanger] 경계 벽 자동 활성화 (이름): {child.name}");
            }
        }
    }

    /// <summary>
    /// Zone의 자식 중 보스 트리거(Trigger_Boss_*)를 찾아 활성화합니다.
    /// </summary>
    private void ActivateBossTriggersInZone(Transform zoneTransform)
    {
        if (zoneTransform == null) return;

        foreach (Transform child in zoneTransform)
        {
            if (child.name.StartsWith("Trigger_Boss_"))
            {
                child.gameObject.SetActive(true);
                Debug.Log($"[ZoneChanger] 보스 트리거 자동 활성화: {child.name}");
            }
        }
    }

    /// <summary>
    /// wallContainer의 자식들 중 activeWallName과 이름이 일치하는 벽만 활성화하고,
    /// 나머지 벽들은 모두 비활성화합니다.
    /// </summary>
    private void ActivateSingleWallInContainer()
    {
        if (wallContainer == null) return;

        bool foundActiveWall = false;

        foreach (Transform child in wallContainer.transform)
        {
            if (child.name == activeWallName)
            {
                child.gameObject.SetActive(true);
                foundActiveWall = true;
                Debug.Log($"[ZoneChanger] Zone 벽 활성화: {child.name}");
            }
            else
            {
                child.gameObject.SetActive(false);
            }
        }

        if (!foundActiveWall)
        {
            Debug.LogWarning($"[ZoneChanger] wallContainer에서 '{activeWallName}'을 찾을 수 없습니다.");
        }
    }

    /// <summary>
    /// 해당 Zone 번호가 안개 효과가 적용되는 Zone인지 확인합니다.
    /// (Zone 1, 2, 3, 4, 5, 6)
    /// </summary>
    private static bool IsFogZone(int zoneNumber)
    {
        return zoneNumber == 1 || zoneNumber == 2 || zoneNumber == 3 || zoneNumber == 4 || zoneNumber == 5 || zoneNumber == 6;
    }

    /// <summary>
    /// 해당 Zone 번호가 어두운 안개 효과를 사용하는 Zone인지 확인합니다.
    /// (Zone 4, 5)
    /// </summary>
    private static bool IsDarkFogZone(int zoneNumber)
    {
        return zoneNumber == 4 || zoneNumber == 5;
    }

    /// <summary>
    /// URP Renderer의 FullScreenPassRendererFeature를 활성화/비활성화하고,
    /// Zone에 따라 머티리얼 설정을 조정합니다.
    /// </summary>
    private static void SetUnderwaterEffect(bool active, bool isDark = false)
    {
        if (GraphicsSettings.currentRenderPipeline is not UniversalRenderPipelineAsset pipelineAsset)
        {
            Debug.LogWarning("[ZoneChanger] UniversalRenderPipelineAsset을 찾을 수 없습니다.");
            return;
        }

        var rendererDataListField = pipelineAsset.GetType()
            .GetField("m_RendererDataList", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (rendererDataListField == null)
        {
            Debug.LogWarning("[ZoneChanger] m_RendererDataList 필드를 찾을 수 없습니다.");
            return;
        }

        var rendererDataList = rendererDataListField.GetValue(pipelineAsset) as ScriptableRendererData[];
        if (rendererDataList == null || rendererDataList.Length == 0)
        {
            Debug.LogWarning("[ZoneChanger] RendererDataList가 비어있습니다.");
            return;
        }

        foreach (var rendererData in rendererDataList)
        {
            if (rendererData == null) continue;

            foreach (var feature in rendererData.rendererFeatures)
            {
                if (feature is FullScreenPassRendererFeature fsFeature && feature.name == "Underwater Effects")
                {
                    fsFeature.SetActive(active);
                    if (active)
                    {
                        ApplyFogMaterialSettings(fsFeature, isDark);
                    }
                    Debug.Log($"[ZoneChanger] Underwater Effects {(active ? (isDark ? "어둡게 활성화" : "활성화") : "비활성화")}");
                    return;
                }
            }
        }

        Debug.LogWarning("[ZoneChanger] 'Underwater Effects' RendererFeature를 찾을 수 없습니다.");
    }

    /// <summary>
    /// UnderwaterFog 머티리얼의 속성을 Zone에 맞게 조정합니다.
    /// isDark=true면 Zone 4/5용 어두운 설정, false면 일반 설정을 적용합니다.
    /// </summary>
    private static void ApplyFogMaterialSettings(FullScreenPassRendererFeature feature, bool isDark)
    {
        var materialField = feature.GetType().GetField("passMaterial",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (materialField == null) return;

        var material = materialField.GetValue(feature) as Material;
        if (material == null) return;

        if (isDark)
        {
            // Zone 4/5: 어두운 설정
            material.SetFloat("_alpha", 0.4f);
            material.SetFloat("_RayIntensity", 0.3f);
            material.SetColor("_color", new Color(0.34f, 0.37f, 0.34f, 1f));
            material.SetColor("_RayColor", new Color(0.4f, 0.39f, 0.31f, 0.4f));
            material.SetColor("Color_551f3de45b3f45d188af0756b6c21b12", new Color(0.2f, 0.31f, 0.36f, 1f));
        }
        else
        {
            // Zone 1/2/3/6: 원래 설정 복원
            material.SetFloat("_alpha", 0.15f);
            material.SetFloat("_RayIntensity", 1.01f);
            material.SetColor("_color", new Color(0.847f, 0.929f, 0.855f, 1f));
            material.SetColor("_RayColor", new Color(1f, 0.976f, 0.769f, 0.659f));
            material.SetColor("Color_551f3de45b3f45d188af0756b6c21b12", new Color(0.494f, 0.784f, 0.890f, 1f));
        }
    }
}