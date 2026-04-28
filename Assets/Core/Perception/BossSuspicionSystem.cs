using UnityEngine;
using System;
using HideAndInk.Core.Interfaces;
using HideAndInk.Core.Enemy.Boss.Gimmicks;

namespace HideAndInk.Core.Perception
{
    /// <summary>
    /// 의심도 범위 바닥 가시성 모드
    /// </summary>
    public enum SuspicionFloorVisibilityMode
    {
        AlwaysOn,   // 항상 표시 (일반 보스용)
        ChaseOnly,  // Chase일 때만 표시
        Hidden      // 아예 표시 안 함 (Ambush 매복용)
    }

    /// <summary>
    /// 보스 전용 독립 의심도 시스템
    /// - 시야 기반 의심도 + 근접 기반 의심도 듀얼 채널
    /// - 의태 상태 연동 (감속/정지)
    /// - 보스 프리팹에 붙여 사용 (인스펙터 설정 가능)
    /// </summary>
    public class BossSuspicionSystem : MonoBehaviour
    {
        [Header("의심도 임계값")]
        [Tooltip("주의 상태 임계값")]
        [SerializeField] private float cautionThreshold = 30f;
        [Tooltip("위험 상태 임계값")]
        [SerializeField] private float dangerThreshold = 60f;
        [Tooltip("발각 상태 임계값")]
        [SerializeField] private float detectedThreshold = 100f;

        [Header("상승 속도 (초당)")]
        [Tooltip("시야 기반 의심도 상승 속도")]
        [SerializeField] private float visionIncreaseSpeed = 10f;

        [Header("하락 속도 (초당)")]
        [Tooltip("일반 의심도 하락 속도")]
        [SerializeField] private float normalDecreaseSpeed = 5f;
        [Tooltip("의태 중 의심도 하락 속도")]
        [SerializeField] private float camouflageDecreaseSpeed = 15f;

        [Header("의심 범위 바닥 시각화")]
        [Tooltip("의심 범위 바닥 표시 활성화")]
        [SerializeField] private bool showSuspicionRadiusInGame = true;
        [Tooltip("의심 범위 바닥 표시 색상")]
        [SerializeField] private Color suspicionFloorColor = new Color(1f, 0f, 0f, 0.5f);
        [Tooltip("바닥 메쉬 세그먼트 수 (높을수록 부드러움)")]
        [SerializeField, Range(16, 64)] private int floorSegmentCount = 32;
        [Tooltip("바닥과의 Z-fighting 방지 오프셋")]
        [SerializeField] private float floorYOffset = 0.05f;
        [Tooltip("바닥으로 인식할 레이어")]
        [SerializeField] private LayerMask groundLayer = -1;

#if UNITY_EDITOR
        [Header("에디터 Gizmos (AmbushGimmick 연동)")]
        [SerializeField] public AmbushGimmick linkedGimmick;
#endif

        // 상태
        private float _currentValue;
        private SuspicionLevel _currentLevel;
        private bool _isCamouflaging;
        private bool _isPerfectCamouflage;
        private bool _wasDetected;
        private float _lastDetectedTime;
        private float _suspicionDecayMultiplier = 1f; // 의심도 하락 배율 (RelentlessChase용)
        private bool _isIncreaseBlocked = false; // 의심도 상승 차단 플래그 (Ambush Chase용)

        // Gizmos 표시용 반경 (AmbushGimmick에서 설정)
        private Vector2 _suspicionRadius = new Vector2(10f, 10f);

        // 인게임 바닥 렌더링
        private GameObject _floorRenderObject;
        private MeshFilter _floorMeshFilter;
        private MeshRenderer _floorMeshRenderer;
        private Mesh _floorMesh;
        private Material _floorMaterial;
        private bool _isFloorInitialized = false;
        private bool _needsMeshRebuild = true; // 메쉬 재생성 플래그
        [SerializeField] private SuspicionFloorVisibilityMode floorVisibilityMode = SuspicionFloorVisibilityMode.ChaseOnly;
        private bool _isFloorVisible = true;

        // 의심도 모듈 (기믹별 계산 로직)
        private ISuspicionModule _suspicionModule;

        // Player Transform 캐싱 (매 프레임 FindWithTag 방지)
        private Transform _playerTransform;
        private float _playerCacheTimer;
        private const float PLAYER_CACHE_INTERVAL = 1f; // 1초마다 갱신

        /// <summary>
        /// 의심도 범위 설정 (AmbushGimmick에서 호출)
        /// </summary>
        public void SetSuspicionRadius(Vector2 radius)
        {
            _suspicionRadius = radius;
            _needsMeshRebuild = true; // 반경 변경 시 메쉬 재생성
        }

        /// <summary>
        /// 의심 범위 바닥 가시성 모드 설정
        /// </summary>
        public void SetFloorVisibilityMode(SuspicionFloorVisibilityMode mode)
        {
            floorVisibilityMode = mode;
            UpdateFloorVisibility();
        }

        /// <summary>
        /// 의심 범위 바닥 가시성 설정 (레거시 호환용)
        /// </summary>
        public void SetFloorVisibility(bool visible)
        {
            floorVisibilityMode = visible ? SuspicionFloorVisibilityMode.AlwaysOn : SuspicionFloorVisibilityMode.Hidden;
            UpdateFloorVisibility();
        }

        /// <summary>
        /// 현재 가시성 모드에 따라 바닥 표시/숨김 처리
        /// </summary>
        private void UpdateFloorVisibility()
        {
            bool shouldBeVisible = floorVisibilityMode != SuspicionFloorVisibilityMode.Hidden;
            if (_isFloorVisible != shouldBeVisible)
            {
                _isFloorVisible = shouldBeVisible;
                if (_floorRenderObject != null)
                {
                    _floorRenderObject.SetActive(shouldBeVisible);
                }
            }
        }

        /// <summary>
        /// 의심도 계산 모듈 설정 (기믹별 로직 주입)
        /// </summary>
        public void SetSuspicionModule(ISuspicionModule module)
        {
            // 기존 모듈 해제
            if (_suspicionModule != null)
            {
                _suspicionModule.OnSuspicionIncrease -= OnModuleSuspicionIncrease;
                _suspicionModule.OnDeactivate();
            }

            _suspicionModule = module;

            if (_suspicionModule != null)
            {
                _suspicionModule.OnSuspicionIncrease += OnModuleSuspicionIncrease;
                _suspicionModule.OnActivate();
            }
        }

        /// <summary>
        /// 모듈에서 발생한 의심도 상승 처리
        /// </summary>
        private void OnModuleSuspicionIncrease(float rate, float deltaTime)
        {
            if (_isIncreaseBlocked) return; // 차단 중이면 상승 무시
            if (_isCamouflaging) return; // 의태 중이면 거리 감지 무시 (시야각 밖 안전)
            AddSuspicion(rate, deltaTime);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (linkedGimmick != null)
            {
                _suspicionRadius = linkedGimmick.SuspicionRadius;
            }
        }
#endif

        // 이벤트
        public event Action<SuspicionLevel> OnLevelChanged;
        public event Action OnDetected;
        public event Action<float> OnValueChanged;

        // 프로퍼티
        public float CurrentValue => Mathf.Clamp(_currentValue, 0f, 100f);
        public SuspicionLevel CurrentLevel => _currentLevel;
        public bool IsCamouflaging => _isCamouflaging;

        private void Awake()
        {
            SuspicionUIManager.Instance?.Register(this);
        }

        private void Update()
        {
            // 의심도 모듈 업데이트 (기믹별 계산)
            if (_suspicionModule != null)
            {
                Vector3? playerPos = FindPlayerPosition();
                _suspicionModule.Update(Time.deltaTime, transform.position, playerPos);
            }

            // 발각 상태 체크
            if (_currentValue >= detectedThreshold && !_wasDetected)
            {
                _wasDetected = true;
                _lastDetectedTime = Time.time;
                OnDetected?.Invoke();
            }

            // 의심도 하락 처리
            if (_currentValue > 0f)
            {
                // 의태 중이면 빠른 하락
                float decreaseSpeed = _isCamouflaging ? camouflageDecreaseSpeed : normalDecreaseSpeed;
                // RelentlessChase 배율 적용
                decreaseSpeed *= _suspicionDecayMultiplier;
                _currentValue -= decreaseSpeed * Time.deltaTime;
                _currentValue = Mathf.Max(_currentValue, 0f);
            }

            // 레벨 체크
            CheckLevelChange();

            // 인게임 의심 범위 바닥 업데이트
            if (showSuspicionRadiusInGame)
                UpdateSuspicionFloorVisual();

            // 이벤트 발생
            OnValueChanged?.Invoke(CurrentValue);
        }

        /// <summary>
        /// Player 위치 탐색 (캐싱 기반, 주기적 갱신)
        /// </summary>
        private Vector3? FindPlayerPosition()
        {
            _playerCacheTimer -= Time.deltaTime;

            if (_playerCacheTimer <= 0f || _playerTransform == null)
            {
                _playerCacheTimer = PLAYER_CACHE_INTERVAL;
                GameObject playerObj = GameObject.FindWithTag("Player");
                if (playerObj != null)
                {
                    _playerTransform = playerObj.transform;
                }
                else
                {
                    _playerTransform = null;
                }
            }

            if (_playerTransform != null)
            {
                return _playerTransform.position;
            }
            return null;
        }

        /// <summary>
        /// 시야 기반 의심도 보고 (Player가 시야각 내에 있을 때)
        /// 의태 중이면 상승 안 함
        /// 상승 차단 중(Ambush Chase)이면 무시
        /// </summary>
        public void ReportVisionDetection(float intensity = 1f)
        {
            if (_isCamouflaging) return; // 의태 중이면 시야 기반 상승 무시
            if (_isIncreaseBlocked) return; // 상승 차단 중이면 무시

            _currentValue += intensity * visionIncreaseSpeed * Time.deltaTime;
            _currentValue = Mathf.Clamp(_currentValue, 0f, 100f);
            CheckLevelChange();
        }

        /// <summary>
        /// 의심도 직접 추가 (기믹에서 호출)
        /// </summary>
        public void AddSuspicion(float rate, float deltaTime)
        {
            float previousValue = _currentValue;
            _currentValue += rate * deltaTime;
            _currentValue = Mathf.Clamp(_currentValue, 0f, 100f);

#if UNITY_EDITOR
            Debug.Log($"[BossSuspicionSystem] AddSuspicion: {previousValue:F2} → {_currentValue:F2} (Rate={rate:F2}, DeltaTime={deltaTime:F3})");
#endif

            CheckLevelChange();
        }

        /// <summary>
        /// 의태 상태 설정
        /// </summary>
        public void SetCamouflageState(bool isCamouflaging, bool isPerfect = false)
        {
            _isCamouflaging = isCamouflaging;
            _isPerfectCamouflage = isPerfect;
        }

        /// <summary>
        /// 의심도 리셋 (챕터 전환 등)
        /// </summary>
        public void ResetSuspicion()
        {
            float previousValue = _currentValue;
            _currentValue = 0f;
            _currentLevel = SuspicionLevel.Safe;
            _wasDetected = false;
            _lastDetectedTime = 0f;

            if (previousValue > 0f)
            {
                OnValueChanged?.Invoke(0f);
            }
        }

        /// <summary>
        /// 의심도 강제 설정 (내부 전용)
        /// 외부에서는 AddSuspicion(rate, deltaTime)만 사용
        /// </summary>
        private void SetSuspicion(float value)
        {
            float prev = _currentValue;
            _currentValue = Mathf.Clamp(value, 0f, 100f);
            CheckLevelChange();

#if UNITY_EDITOR
            if (Mathf.Abs(_currentValue - prev) > 1f)
                Debug.LogWarning($"[BossSuspicionSystem] SetSuspicion: {prev:F1} → {_currentValue:F1} (점프 발생)");
#endif
        }

        /// <summary>
        /// 시야 기반 의심도 상승 속도 설정 (상태 전환 시 사용)
        /// </summary>
        public void SetVisionIncreaseSpeed(float speed)
        {
            visionIncreaseSpeed = Mathf.Max(0f, speed);
        }

        /// <summary>
        /// 의심도 하락 배율 설정 (RelentlessChaseGimmick에서 호출)
        /// 1보다 작으면 느리게 하락, 1이면 기본값
        /// </summary>
        public void SetSuspicionDecayMultiplier(float multiplier)
        {
            _suspicionDecayMultiplier = Mathf.Max(0.1f, multiplier);
        }

        /// <summary>
        /// 의심도 상승 차단 (Ambush Chase 중 호출)
        /// Chase 중에는 의심도 하락만 허용
        /// </summary>
        public void BlockSuspicionIncrease()
        {
            _isIncreaseBlocked = true;
        }

        /// <summary>
        /// 의심도 상승 허용 (Patrol/Search 상태로 복귀 시 호출)
        /// </summary>
        public void AllowSuspicionIncrease()
        {
            _isIncreaseBlocked = false;
        }

        private void CheckLevelChange()
        {
            SuspicionLevel newLevel = CalculateLevel(_currentValue);
            if (newLevel != _currentLevel)
            {
                _currentLevel = newLevel;
                OnLevelChanged?.Invoke(_currentLevel);
            }
        }

        private SuspicionLevel CalculateLevel(float value)
        {
            if (value >= detectedThreshold) return SuspicionLevel.Detected;
            if (value >= dangerThreshold) return SuspicionLevel.Danger;
            if (value >= cautionThreshold) return SuspicionLevel.Caution;
            return SuspicionLevel.Safe;
        }

        /// <summary>
        /// 인게임 의심 범위 바닥 시각화 업데이트
        /// 의심도 레벨에 따라 색상 변경 (Danger=빨강, Caution=주황)
        /// </summary>
        private void UpdateSuspicionFloorVisual()
        {
            // 가시성 모드 체크 (Hidden이면 렌더링 안 함)
            if (floorVisibilityMode == SuspicionFloorVisibilityMode.Hidden)
            {
                if (_isFloorInitialized && _floorRenderObject != null && _floorRenderObject.activeSelf)
                {
                    _floorRenderObject.SetActive(false);
                }
                return;
            }

            // 초기화
            if (!_isFloorInitialized)
            {
                InitializeFloorRenderer();
                if (!_isFloorInitialized) return;
            }

            // 바닥 높이 찾기 (Raycast)
            if (!Physics.Raycast(transform.position, Vector3.down, out RaycastHit floorHit, 20f, groundLayer)) return;
            float floorY = floorHit.point.y + floorYOffset;

            // 자식 객체 위치 업데이트
            _floorRenderObject.transform.position = new Vector3(transform.position.x, floorY, transform.position.z);

            // 정점 색상을 직접 업데이트 (Vertex Color 셰이더가 재질 색상 무시하므로)
            if (_floorMesh != null)
            {
                Color[] colors = _floorMesh.colors;
                if (colors != null && colors.Length > 0)
                {
                    // 중심점: 진하게
                    colors[0] = new Color(suspicionFloorColor.r, suspicionFloorColor.g, suspicionFloorColor.b, 0.8f);
                    // 가장자리: 연하게
                    for (int i = 1; i < colors.Length; i++)
                    {
                        colors[i] = new Color(suspicionFloorColor.r, suspicionFloorColor.g, suspicionFloorColor.b, 0.4f);
                    }
                    _floorMesh.colors = colors;
                }
            }

            // 메쉬 재생성 (반경 변경 시 또는 초기화 후 첫 프레임)
            if (_needsMeshRebuild && _floorMesh != null)
            {
                BuildFloorMesh();
                _needsMeshRebuild = false;
            }
        }

        /// <summary>
        /// 바닥 렌더러 초기화
        /// </summary>
        private void InitializeFloorRenderer()
        {
            // 자식 GameObject 생성
            _floorRenderObject = new GameObject("SuspicionRadiusFloor");
            _floorRenderObject.transform.SetParent(transform);
            _floorRenderObject.transform.localPosition = Vector3.zero;
            _floorRenderObject.transform.localRotation = Quaternion.identity;

            _floorMeshFilter = _floorRenderObject.AddComponent<MeshFilter>();
            _floorMeshRenderer = _floorRenderObject.AddComponent<MeshRenderer>();

            if (_floorMeshFilter == null || _floorMeshRenderer == null) return;

            _floorMesh = new Mesh();
            _floorMesh.name = "SuspicionRadiusFloorMesh";
            _floorMeshFilter.sharedMesh = _floorMesh;

            // URP 호환 셰이더 사용 (VisionConeRenderer와 동일)
            Shader shader = Shader.Find("Custom/VertexColorUnlitTransparent");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            if (shader != null)
            {
                _floorMaterial = new Material(shader);
                _floorMaterial.color = suspicionFloorColor;
                _floorMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                _floorMeshRenderer.material = _floorMaterial;
                _floorMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _floorMeshRenderer.sortingLayerName = "Default";
                _floorMeshRenderer.sortingOrder = -10;
            }

            // 초기 메쉬 생성
            BuildFloorMesh();

            // 초기 가시성 적용
            if (floorVisibilityMode == SuspicionFloorVisibilityMode.Hidden)
            {
                _floorRenderObject.SetActive(false);
            }

            _isFloorInitialized = true;
        }

        /// <summary>
        /// 의심 범위 바닥 메쉬 생성 (타원형)
        /// </summary>
        private void BuildFloorMesh()
        {
            if (_floorMesh == null) return;

            // 반경이 0이면 기본값 사용 (초기화 순서 문제 방지)
            float rx = Mathf.Max(_suspicionRadius.x, 0.1f);
            float rz = Mathf.Max(_suspicionRadius.y, 0.1f);

            int segments = floorSegmentCount;
            Vector3[] vertices = new Vector3[segments + 1];
            Color[] colors = new Color[segments + 1];
            int[] triangles = new int[segments * 3];

            // 중심점
            vertices[0] = Vector3.zero;
            colors[0] = Color.white; // 재질 색상(dangerColor/cautionColor)이 그대로 적용됨

            // 타원형 가장자리 점 생성
            float angleStep = 360f / segments;

            for (int i = 0; i < segments; i++)
            {
                float angle = angleStep * i * Mathf.Deg2Rad;
                float x = Mathf.Cos(angle) * rx;
                float z = Mathf.Sin(angle) * rz;
                vertices[i + 1] = new Vector3(x, 0f, z);

                // 가장자리: 재질 색상 적용을 위해 흰색 + alpha만 설정
                float edgeAlpha = 0.4f;
                colors[i + 1] = new Color(1f, 1f, 1f, edgeAlpha);

                // 삼각형 인덱스
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = (i + 1) % segments + 1;
            }

            _floorMesh.Clear();
            _floorMesh.vertices = vertices;
            _floorMesh.colors = colors;
            _floorMesh.triangles = triangles;
            _floorMesh.RecalculateNormals();
        }

        /// <summary>
        /// 의심도 상승 범위 Gizmos 표시 (단일 타원형)
        /// - 타원형 영역: 주황색 와이어프레임 (SuspicionRadius 기준)
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            Vector2 radius = _suspicionRadius;

#if UNITY_EDITOR
            if (linkedGimmick != null)
            {
                radius = linkedGimmick.SuspicionRadius;
            }
#endif

            // 타원형 영역 - 주황색
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.6f);
            Gizmos.matrix = Matrix4x4.TRS(transform.position, Quaternion.identity, new Vector3(radius.x, 0.05f, radius.y));
            Gizmos.DrawWireSphere(Vector3.zero, 1f);

            // 중심점 표시
            Gizmos.color = Color.red;
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.DrawSphere(transform.position, 0.15f);
        }

        private void OnDestroy()
        {
            SuspicionUIManager.Instance?.Unregister(this);

            if (_floorMaterial != null) DestroyImmediate(_floorMaterial);
            if (_floorMesh != null) DestroyImmediate(_floorMesh);
            if (_floorRenderObject != null) DestroyImmediate(_floorRenderObject);
        }
    }
}
