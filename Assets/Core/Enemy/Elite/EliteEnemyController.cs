using UnityEngine;
using HideAndInk.Core.Enemy.Interfaces;
using HideAndInk.Core.Enemy.Movement;
using HideAndInk.Core.Enemy.Elite.Behaviors;

namespace HideAndInk.Core.Enemy.Elite
{
    /// <summary>
    /// 정예 몬스터 컨트롤러
    /// 센서 없이 행동 패턴(IEliteBehavior) 기반 기믹 발동
    /// X-Z 평면 이동
    /// </summary>
    public class EliteEnemyController : EnemyAIController
    {
        public override EnemyType Type => EnemyType.Elite;

        [Header("순찰 패턴 설정 (정예 전용)")]
        [Tooltip("이동 명령 주기 (초)")]
        [SerializeField] private float moveInterval = 3f;
        [Tooltip("한 번 이동 시 최대 거리")]
        [SerializeField] private float moveDistance = 3f;
        [Tooltip("이동 후 대기 시간 (초)")]
        [SerializeField] private float idleTime = 2f;

        [Header("행동 패턴 설정")]
        [Tooltip("정예 몬스터 행동 패턴 (ScriptableObject 또는 MonoBehaviour)")]
        [SerializeField] private MonoBehaviour eliteBehavior;
        
        [Header("감지 범위 설정")]
        [Tooltip("Player 감지 거리 (X축 기준, m)")]
        [SerializeField] private float detectionRadius = 8f;
        [Tooltip("Player 감지 너비 (Z축 기준, m). 0이면 무한")]
        [SerializeField] private float detectionWidth = 6f;
        [Tooltip("감지 범위 시각화 (Scene에서 직사각형 표시)")]
        [SerializeField] private bool showDetectionRangeInScene = true;
        [Tooltip("게임 중 감지 범위 시각화 (Ground 위에 직사각형 표시)")]
        [SerializeField] private bool showDetectionRangeInGame = true;
        [Tooltip("시각화 색상")]
        [SerializeField] private Color detectionRangeColor = new Color(1f, 0f, 0f, 0.5f);
        [Tooltip("Ground 위 시각화 높이 (m)")]
        [SerializeField] private float detectionRangeHeight = 0.05f;

        [Header("스프라이트 방향")]
        [Tooltip("기본 에셋이 왼쪽을 보고 있는지 여부 (true: 왼쪽 기본, false: 오른쪽 기본)")]
        [SerializeField] private bool isDefaultFacingLeft = true;
        [SerializeField] private SpriteRenderer eliteSpriteRenderer;

        // 상태
        private float _stateTimer;
        private Vector3 _targetPosition;
        private bool _isMoving;
        private IEliteBehavior _behavior;
        
        // 감지 범위 시각화
        private GameObject _visualizerObj;
        private MeshRenderer _detectionRangeMeshRenderer;
        private MeshFilter _detectionRangeMeshFilter;
        private Mesh _detectionRangeMesh;
        private bool _isMeshInitialized;

        protected override void InitializeMovement()
        {
            _movement = new EnemyMovement(
                enemy: this,
                speed: moveSpeed,
                acceleration: 6f,
                friction: 0.85f,
                maxSpeed: moveSpeed,
                groundLayer: groundLayer,
                groundCheckDistance: groundCheckDistance,
                groundCheckRadius: groundCheckRadius);
        }

        protected override void Start()
        {
            base.Start();
            
            // 스프라이트 렌더러 자동 할당 (없을 경우)
            if (eliteSpriteRenderer == null)
            {
                eliteSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
            
#if UNITY_EDITOR
            Debug.Log($"[EliteEnemyController] Start: showDetectionRangeInGame={showDetectionRangeInScene}");
#endif
            
            // 감지 범위 시각화 초기화
            InitializeDetectionRangeVisualizer();
            
#if UNITY_EDITOR
            Debug.Log($"[EliteEnemyController] Start: isMeshInitialized={_isMeshInitialized}, visualizerObj={(_visualizerObj != null ? "OK" : "NULL")}");
            if (_detectionRangeMeshRenderer != null)
            {
                Debug.Log($"[EliteEnemyController] Start: MeshRenderer.enabled={_detectionRangeMeshRenderer.enabled}, material={(_detectionRangeMeshRenderer.material != null ? "OK" : "NULL")}");
            }
#endif
            
            InitializeBehavior();
        }

        /// <summary>
        /// 행동 패턴 초기화
        /// </summary>
        private void InitializeBehavior()
        {
            if (eliteBehavior != null)
            {
                _behavior = eliteBehavior as IEliteBehavior;
                if (_behavior == null)
                {
                    Debug.LogError($"[EliteEnemyController] eliteBehavior가 IEliteBehavior를 구현하지 않았습니다: {eliteBehavior.GetType().Name}");
                }
                else
                {
                    _behavior.OnActivate();
#if UNITY_EDITOR
                    Debug.Log($"[EliteEnemyController] Elite behavior loaded: {_behavior.BehaviorName}");
#endif
                }
            }
            else
            {
                Debug.LogWarning("[EliteEnemyController] No elite behavior assigned. Set eliteBehavior in Inspector.");
            }
        }

        protected override void UpdateAI(float deltaTime)
        {
            // Player 감지 체크
            CheckPlayerDetection();

            // 행동 패턴 업데이트
            if (_behavior != null)
            {
                _behavior.OnUpdate(deltaTime);
            }

            // 이동 상태 머신 (행동 패턴이 제어하지 않을 때만)
            if (_behavior == null || !IsBehaviorControllingMovement())
            {
                UpdatePatrolMovement(deltaTime);
            }

            // 스프라이트 방향 업데이트
            UpdateSpriteDirection();
            
            // 감지 범위 시각화 업데이트
            UpdateDetectionRangeVisualizer();
        }

        /// <summary>
        /// Player 감지 체크 (직사각형 영역 기반)
        /// 현재 스프라이트가 바라보는 방향으로만 감지
        /// </summary>
        private void CheckPlayerDetection()
        {
            if (_playerTransform == null) return;

            // 현재 바라보는 방향 계산
            bool isFacingLeft = IsCurrentlyFacingLeft();
            
            Vector3 toPlayer = _playerTransform.position - transform.position;
            
            // X축 방향 체크
            bool playerIsOnLeft = toPlayer.x < 0;
            bool playerIsOnRight = toPlayer.x > 0;
            
            // 바라보는 방향으로만 감지
            bool isInDirection = isFacingLeft ? playerIsOnLeft : playerIsOnRight;
            
            // X축 거리 체크
            float distanceX = Mathf.Abs(toPlayer.x);
            bool isInXRange = distanceX <= detectionRadius;
            
            // Z축 너비 체크 (0이면 무한)
            bool isInZRange = true;
            if (detectionWidth > 0f)
            {
                float distanceZ = Mathf.Abs(toPlayer.z);
                isInZRange = distanceZ <= detectionWidth * 0.5f;
            }
            
            if (isInDirection && isInXRange && isInZRange)
            {
                _behavior?.OnPlayerApproached(distanceX, _playerTransform.position);
            }
        }

        /// <summary>
        /// 현재 스프라이트가 왼쪽을 보고 있는지 확인
        /// </summary>
        private bool IsCurrentlyFacingLeft()
        {
            return CalculateFacingLeft();
        }

        /// <summary>
        /// 행동 패턴이 이동 제어권 가졌는지 확인
        /// </summary>
        private bool IsBehaviorControllingMovement()
        {
            // SwordfishBehavior가 Idle이 아니면 (돌진 지연/돌진/쿨타임 중) 이동 제어권 넘김
            if (_behavior is SwordfishBehavior swordfish)
            {
                return swordfish.IsControllingMovement;
            }
            return false;
        }

        /// <summary>
        /// 순찰 이동 업데이트 (끊김 없이 지속 이동)
        /// </summary>
        private void UpdatePatrolMovement(float deltaTime)
        {
            // 목표 도달 감지 (EnemyMovement 내부 distance < 0.5f 기준)
            bool hasReachedTarget = _movement != null && 
                                    !_movement.IsMoving && 
                                    _movement.Velocity.sqrMagnitude < 0.01f;

            if (hasReachedTarget)
            {
                // 즉시 새 목표 설정 (대기 시간 없이)
                PickNewTarget();
                _movement.MoveTo(_targetPosition);
            }
            else if (!_movement.IsMoving)
            {
                // 초기 시작 시 목표 설정
                PickNewTarget();
                _movement.MoveTo(_targetPosition);
            }
        }

        /// <summary>
        /// 새로운 이동 목표 지점 선택 (X-Z 평면)
        /// </summary>
        private void PickNewTarget()
        {
            Vector2 randomDirection = Random.insideUnitCircle.normalized;
            Vector3 currentPos = transform.position;
            Vector3 target = new Vector3(
                currentPos.x + randomDirection.x * moveSpeed,
                currentPos.y,
                currentPos.z + randomDirection.y * moveDistance);

            _targetPosition = ClampToGroundBounds(target);
        }

        #region Behavior 연동 메서드

        /// <summary>
        /// 이동 방향에 따라 스프라이트 좌우 반전
        /// 부모 클래스의 UpdateViewDirection()을 오버라이드하여 transform.rotation 변경을 막고 flipX만 사용
        /// </summary>
        protected override void UpdateViewDirection()
        {
            // 부모의 transform.rotation 변경 로직을 완전히 무시하고 flipX만 사용
            UpdateSpriteDirection();
        }

        /// <summary>
        /// 이동 방향에 따라 스프라이트 좌우 반전 (내부 로직)
        /// </summary>
        private void UpdateSpriteDirection()
        {
            if (eliteSpriteRenderer == null) return;

            // 이동 중일 때만 방향 전환 (정지 시 현재 방향 유지)
            // 돌진 중에는 _movement.Velocity가 0일 수 있으므로 현재 상태 확인
            bool isCharging = _behavior is SwordfishBehavior sf && sf.IsCharging;
            
            if (_movement != null && (_movement.Velocity.sqrMagnitude > 0.01f || isCharging))
            {
                // X축 이동 방향 확인
                bool movingRight = _movement.Velocity.x > 0;
                bool movingLeft = _movement.Velocity.x < 0;
                
                // 돌진 중이고 Velocity가 0이면 이전 방향 유지 (또는 돌진 방향 사용)
                if (isCharging && _movement.Velocity.sqrMagnitude < 0.01f)
                {
                    // 돌진 방향은 SwordfishBehavior에서 관리하므로 여기서는 생략
                    // 필요시 _behavior에서 방향 정보 받아올 수 있음
                    return;
                }

                // 기본이 왼쪽 Facing일 때:
                // - 왼쪽 이동: flipX = false (원래대로)
                // - 오른쪽 이동: flipX = true (반전)
                // 기본이 오른쪽 Facing일 때:
                // - 왼쪽 이동: flipX = true (반전)
                // - 오른쪽 이동: flipX = false (원래대로)
                
                if (isDefaultFacingLeft)
                {
                    eliteSpriteRenderer.flipX = movingRight;
                }
                else
                {
                    eliteSpriteRenderer.flipX = movingLeft;
                }
            }
        }

        /// <summary>
        /// 이동 속도 설정 (행동 패턴에서 호출)
        /// </summary>
        public void SetSpeed(float speed)
        {
            if (_movement != null)
            {
                _movement.Speed = speed;
                _movement.SetMaxSpeed(speed);
            }
        }

        /// <summary>
        /// 기본 이동 속도 반환
        /// </summary>
        public float GetDefaultSpeed()
        {
            return moveSpeed;
        }

        /// <summary>
        /// 이동 중지 (행동 패턴에서 호출)
        /// </summary>
        public void Stop()
        {
            _movement?.Stop();
        }

        /// <summary>
        /// 목표 위치로 이동 (행동 패턴에서 호출)
        /// </summary>
        public void MoveTo(Vector3 target)
        {
            _movement?.MoveTo(target);
        }

        /// <summary>
        /// 감지 반경 반환
        /// </summary>
        public float GetDetectionRadius()
        {
            return detectionRadius;
        }

        /// <summary>
        /// 현재 이동 중인지 여부 (행동 패턴에서 확인용)
        /// </summary>
        public bool IsMoving => _movement?.IsMoving ?? false;

        #endregion

        #region 감지 범위 시각화

        /// <summary>
        /// 감지 범위 시각화 초기화
        /// </summary>
        private void InitializeDetectionRangeVisualizer()
        {
            if (!showDetectionRangeInGame)
            {
#if UNITY_EDITOR
                Debug.Log("[EliteEnemyController] 시각화 비활성화됨 (showDetectionRangeInGame = false)");
#endif
                return;
            }

#if UNITY_EDITOR
            Debug.Log("[EliteEnemyController] 시각화 초기화 시작...");
#endif

            // 시각화용 자식 오브젝트 생성
            _visualizerObj = new GameObject("DetectionRangeVisualizer");
            _visualizerObj.transform.SetParent(transform);
            _visualizerObj.transform.localPosition = Vector3.zero;
            _visualizerObj.transform.localRotation = Quaternion.identity;
            _visualizerObj.transform.localScale = Vector3.one;

            // MeshFilter, MeshRenderer 추가
            _detectionRangeMeshFilter = _visualizerObj.AddComponent<MeshFilter>();
            _detectionRangeMeshRenderer = _visualizerObj.AddComponent<MeshRenderer>();

            // Mesh 생성 (1x1 크기 기준, XZ 평면)
            // 정점 순서를 반시계 방향으로 변경 (법선이 위쪽을 향하도록)
            _detectionRangeMesh = new Mesh();
            _detectionRangeMesh.name = "DetectionRangeMesh";
            
            _detectionRangeMesh.vertices = new Vector3[4]
            {
                new Vector3(-0.5f, 0, 0.5f),   // 좌상
                new Vector3(0.5f, 0, 0.5f),    // 우상
                new Vector3(0.5f, 0, -0.5f),   // 우하
                new Vector3(-0.5f, 0, -0.5f)   // 좌하
            };
            _detectionRangeMesh.triangles = new int[6] { 0, 1, 2, 0, 2, 3 };
            _detectionRangeMesh.uv = new Vector2[4] { new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), new Vector2(0, 0) };
            _detectionRangeMesh.RecalculateNormals();
            
            _detectionRangeMeshFilter.mesh = _detectionRangeMesh;

#if UNITY_EDITOR
            Debug.Log($"[EliteEnemyController] Mesh 생성 완료: 정점={_detectionRangeMesh.vertexCount}, 삼각형={_detectionRangeMesh.triangles.Length}");
#endif

            // 머티리얼 설정 (URP 호환 Shader 우선 사용)
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            
#if UNITY_EDITOR
            Debug.Log($"[EliteEnemyController] 사용된 Shader: {(shader != null ? shader.name : "NULL")}");
#endif
            
            if (shader != null)
            {
                Material mat = new Material(shader);
                mat.color = detectionRangeColor;
                
                // URP Unlit Shader는 투명도 처리를 위해 Rendering Mode 설정 필요
                if (shader.name.Contains("Universal Render Pipeline"))
                {
                    mat.SetFloat("_Surface", 1); // Transparent
                    mat.SetFloat("_Blend", 0); // Alpha
                    mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetFloat("_ZWrite", 0);
                    mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                }
                
                _detectionRangeMeshRenderer.material = mat;
                _detectionRangeMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _detectionRangeMeshRenderer.receiveShadows = false;
                
#if UNITY_EDITOR
                Debug.Log($"[EliteEnemyController] Material 생성 완료: Color={mat.color}, RenderQueue={mat.renderQueue}");
#endif
            }
            else
            {
                Debug.LogWarning("[EliteEnemyController] No suitable shader found for detection range visualization.");
            }
            
            _isMeshInitialized = true;
            
#if UNITY_EDITOR
            Debug.Log($"[EliteEnemyController] 시각화 초기화 완료: isMeshInitialized={_isMeshInitialized}");
#endif
        }

        /// <summary>
        /// 감지 범위 시각화 업데이트
        /// </summary>
        private void UpdateDetectionRangeVisualizer()
        {
            if (!showDetectionRangeInGame || !_isMeshInitialized || _visualizerObj == null)
            {
#if UNITY_EDITOR
                if (Time.frameCount % 60 == 0) // 1초에 한 번만 로그
                {
                    Debug.Log($"[EliteEnemyController] 시각화 업데이트 스: showDetectionRangeInGame={showDetectionRangeInGame}, isMeshInitialized={_isMeshInitialized}, visualizerObj={(_visualizerObj != null ? "OK" : "NULL")}");
                }
#endif
                return;
            }

            // 현재 바라보는 방향 결정
            bool isFacingLeft = IsCurrentlyFacingLeft();
            float direction = isFacingLeft ? -1f : 1f;
            
            // 직사각형 크기 계산
            float width = detectionRadius;
            float height = detectionWidth > 0f ? detectionWidth : 20f;
            
            // 부모 스케일 보정 (lossyScale로 나누어 월드 기준 크기 유지)
            Vector3 lossyScale = transform.lossyScale;
            Vector3 targetLocalScale = new Vector3(
                width / lossyScale.x,
                1f / lossyScale.y,
                height / lossyScale.z
            );
            _visualizerObj.transform.localScale = targetLocalScale;
            
            // 위치 보정 (부모 스케일 영향 제거)
            Vector3 offset = new Vector3(
                (direction * width * 0.5f) / lossyScale.x,
                detectionRangeHeight / lossyScale.y,
                0f
            );
            _visualizerObj.transform.localPosition = offset;
            
#if UNITY_EDITOR
            if (Time.frameCount % 60 == 0) // 1초에 한 번만 로그
            {
                Debug.Log($"[EliteEnemyController] 시각화 업데이트: facingLeft={isFacingLeft}, width={width}, height={height}, lossyScale={lossyScale}, localScale={_visualizerObj.transform.localScale}, localPos={_visualizerObj.transform.localPosition}");
            }
#endif
        }

        #endregion

        private void OnDestroy()
        {
            if (_behavior != null)
            {
                _behavior.OnDeactivate();
            }
            
            // 시각화 오브젝트 정리
            if (_visualizerObj != null)
            {
                Destroy(_visualizerObj);
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Scene에서 감지 범위 시각화 (항상 표시 - Play 중에도)
        /// </summary>
        private void OnDrawGizmos()
        {
            if (!showDetectionRangeInScene) return;

            DrawDetectionRange();
        }

        /// <summary>
        /// Scene에서 감지 범위 시각화 (선택 시 추가 정보 표시)
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (!showDetectionRangeInScene) return;

            DrawDetectionRange();
            
            // 선택 시 추가 정보 (중심점 강조)
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.3f);
        }

        /// <summary>
        /// 감지 범위 그리기 (공통 로직)
        /// </summary>
        private void DrawDetectionRange()
        {
            // 현재 바라보는 방향 결정 (에디터에서도 작동하도록 직접 계산)
            bool isFacingLeft = CalculateFacingLeft();
            float direction = isFacingLeft ? -1f : 1f;
            
            // 직사각형 영역 계산
            float xStart = transform.position.x;
            float xEnd = transform.position.x + (detectionRadius * direction);
            
            // 직사각형 그리기
            Gizmos.color = new Color(1f, 0f, 0f, 0.2f); // 반투명 빨강
            
            Vector3 center = new Vector3((xStart + xEnd) * 0.5f, transform.position.y, transform.position.z);
            Vector3 size = new Vector3(Mathf.Abs(xEnd - xStart), 0.1f, detectionWidth > 0f ? detectionWidth : 20f);
            
            Gizmos.DrawCube(center, size);
            
            // 테두리
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(center, size);
        }

        /// <summary>
        /// 현재 바라보는 방향 계산 (에디터/런타임 공용)
        /// </summary>
        private bool CalculateFacingLeft()
        {
            // 1순위: SpriteRenderer flipX 기반 (가장 정확)
            SpriteRenderer sr = eliteSpriteRenderer;
            if (sr == null)
            {
                sr = GetComponentInChildren<SpriteRenderer>();
                if (sr != null) eliteSpriteRenderer = sr; // 캐싱
            }
            
            if (sr != null)
            {
                if (isDefaultFacingLeft)
                {
                    return !sr.flipX;
                }
                else
                {
                    return sr.flipX;
                }
            }

            // 2순위: 이동 방향 기반 (SpriteRenderer가 없을 때 Fallback)
            if (_movement != null)
            {
                return _movement.Direction == HideAndInk.Core.Interfaces.MoveDirection.Left;
            }

            // 기본값: 왼쪽
            return true;
        }
#endif
    }
}
