using UnityEngine;
using HideAndInk.Core.Interfaces;
using HideAndInk.Core.Perception;
using HideAndInk.Core.Managers;

namespace HideAndInk.Player
{
    /// <summary>
    /// 의태 시스템 Unity 어댑터 (코디네이터)
    /// 토글 방식: C 누르고 있으면 진행, 떼면 Perfect 아니면 취소
    /// 실제 작업은 전용 컴포넌트에 위임
    /// </summary>
    [RequireComponent(typeof(PlayerMovementAdapter))]
    public sealed class CamouflageAdapter : MonoBehaviour
    {
        [Header("의태 탐지 설정")]
        [SerializeField] private float detectionRadius = 1.0f;
        [SerializeField] private LayerMask camouflageLayer = -1;

        [Header("의태 시간 설정")]
        [SerializeField] private float attachDelay = 0.3f;
        [SerializeField] private float lockTime = 0.4f;
        [SerializeField] private float blendTime = 1.0f;
        [SerializeField] private float perfectTime = 2.0f;

        [Header("의태 키 설정")]
        [SerializeField] private KeyCode camouflageKey = KeyCode.C;

        [Header("VFX 설정 (Lead Artist 전용)")]
        [Tooltip("의태 시작 시 발생할 이펙트")]
        [SerializeField] private GameObject startVfxPrefab;
        [SerializeField] private bool followPlayerOnStart = true;
        
        [Tooltip("의태 해제 시 발생할 애니메이션 이펙트 (단일)")]
        [SerializeField] private GameObject endAnimVfxPrefab;
        [SerializeField] private bool followPlayerOnEndAnim = true;

        [Tooltip("의태 해제 시 발생할 이펙트 (배열로 넣으면 랜덤 재생, 주로 보존되는 흔적용)")]
        [SerializeField] private GameObject[] endVfxPrefabs;
        [SerializeField] private bool followPlayerOnEnd = false;

        [Header("의존성")]
        [SerializeField] private SpriteDirector spriteDirector;

        private ICamouflageDetector _detector;
        private ICamouflageStateMachine _stateMachine;
        private IMaterialCloner _materialCloner;
        private PlayerMovementAdapter _playerMovement;
        private Renderer _playerRenderer;

        private Vector3 _originalPosition;
        private bool _justTransitionedFromPerfect;
        private float _transitionTimer;

        private bool _isRestoringRate;
        private float _rateRestoreProgress;
        private const float RATE_RESTORE_DURATION = 1.4f;

        private const float IGNORE_MOVEMENT_AFTER_ATTACH = 0.35f;
        private float _ignoreMovementTimer;

        private void Awake()
        {
            _playerMovement = GetComponent<PlayerMovementAdapter>();

            Transform visual = transform.Find("Visual");
            if (visual != null)
            {
                _playerRenderer = visual.GetComponentInChildren<Renderer>();
            }
            else
            {
                _playerRenderer = GetComponentInChildren<Renderer>();
            }

            if (GameManager.Container != null && GameManager.Container.IsRegistered<ICamouflageDetector>())
            {
                _detector = GameManager.Container.Resolve<ICamouflageDetector>();
            }
            else
            {
                _detector = new CamouflageDetector(detectionRadius);
                _detector.SetLayerMask(camouflageLayer);
            }

            if (GameManager.Container != null && GameManager.Container.IsRegistered<ICamouflageStateMachine>())
            {
                _stateMachine = GameManager.Container.Resolve<ICamouflageStateMachine>();
            }
            else
            {
                _stateMachine = new CamouflageStateMachine(attachDelay, lockTime, blendTime, perfectTime);
            }

            if (_playerRenderer != null)
            {
                _materialCloner = new MaterialCloner(_playerRenderer);
            }

            if (spriteDirector != null)
            {
                SpriteRenderer sr = _playerRenderer as SpriteRenderer;
                if (sr == null && visual != null)
                {
                    sr = visual.GetComponentInChildren<SpriteRenderer>();
                }
                spriteDirector.SetSpriteRenderer(sr);
            }
        }

        private void Update()
        {
            // 키 입력 처리
            HandleKeyInput();

            // 의태 상태 (Attached 이후)에서는 이동 잠금 + 벽 충돌 무시
            if (_stateMachine.CurrentState != CamouflageState.None)
            {
                _playerMovement?.SetMovementLocked(true);
                _playerMovement?.SetIgnoreWallCollision(true);
            }
            else
            {
                _playerMovement?.SetMovementLocked(false);
                _playerMovement?.SetIgnoreWallCollision(false);

                UpdateSpriteDirection();
            }

            // 의태 시작 후 잠시 이동 무시 ( Attached → Locked 전환 시간 )
            if (_ignoreMovementTimer > 0f)
            {
                _ignoreMovementTimer -= Time.deltaTime;
            }

            // 이동 상태 확인 (의태 시작 후 잠시 무시)
            bool isMoving = _ignoreMovementTimer <= 0f && _playerMovement != null && _playerMovement.IsMoving;

            // Perfect에서 전환直後에는 잠시 이동 무시
            // (Partial/Perfect 도달하거나 0.5초 경과하면 해제)
            if (_justTransitionedFromPerfect)
            {
                isMoving = false;
                _transitionTimer += Time.deltaTime;
                if (_transitionTimer >= 0.5f)
                {
                    Debug.Log("[CamouflageAdapter] Transition timeout, allowing movement cancel");
                    _justTransitionedFromPerfect = false;
                    _transitionTimer = 0f;
                }
            }

            var prevState = _stateMachine.CurrentState;
            bool wasNotNone = prevState != CamouflageState.None;

            // 상태 시스템 업데이트
            _stateMachine.Update(Time.deltaTime, isMoving);

            // [Artist Fallback] 머티리얼 누락 시 경고 로그 (의태 시도 중일 때만)
            if (_stateMachine.CurrentState != CamouflageState.None && _materialCloner != null && !_materialCloner.IsUsingOctopusMaterial)
            {
                // Octopus 머티리얼이 로드되지 않았을 때만 1회성 경고 (또는 지속 로그)
                if (Time.frameCount % 120 == 0) // 매 2초마다 출력
                {
                    Debug.LogWarning("[CamouflageAdapter] 아티스트 알림: Resources/Materials/Octopus 머티리얼이 없습니다! 비주얼 효과가 제한됩니다.");
                }
            }

            // 상태가 None으로 변화 → 취소됨 → OriginalRate 복원 시작 및 해제 이펙트 발생
            if (wasNotNone && _stateMachine.CurrentState == CamouflageState.None)
            {
                Debug.Log($"[CamouflageAdapter] State returned to None. wasNotNone={wasNotNone}, _isRestoringRate will be set to true");
                _isRestoringRate = true;
                _rateRestoreProgress = 0f;
                // Note: SpriteRenderer.color은 변경하지 않음 - OriginalRate만으로 색상 조절
                
                // 해제 이펙트 생성 (먹물 흔적 등)
                PlayEndVfx();
            }

            // 상태 변화 로그
            if (prevState != _stateMachine.CurrentState)
            {
                Debug.Log($"[CamouflageAdapter] State changed: {prevState} -> {_stateMachine.CurrentState}");

                // None → 의태 상태로 전환 시 Octopus Material 적용 및 시작 이펙트 발생
                if (prevState == CamouflageState.None && _stateMachine.CurrentState != CamouflageState.None)
                {
                    Debug.Log($"[CamouflageAdapter] State changed from None! Current state: {_stateMachine.CurrentState}. Applying Octopus Material...");
                    _materialCloner?.ApplyOctopusMaterial();
                    
                    // 시작 이펙트 생성
                    PlayStartVfx();
                }

                // Partial 또는 Perfect에 도달하면 플래그 해제
                if (_stateMachine.CurrentState == CamouflageState.Partial || 
                    _stateMachine.CurrentState == CamouflageState.Perfect)
                {
                    _justTransitionedFromPerfect = false;
                }
            }

            // Attached 완료 전에는 추가 처리 안 함
            if (_stateMachine.CurrentState == CamouflageState.Attached && !_stateMachine.IsAttachedComplete)
            {
                return;
            }

            // 색상 보간 업데이트
            UpdateBlend();

            // 의태 상태에 따른 위치 조정
            UpdatePosition();
        }

        /// <summary>
        /// 키 입력 처리 (C 키를 누르고 있는 동안 계속 진행)
        /// </summary>
        private void HandleKeyInput()
        {
            // C Down 감지
            if (Input.GetKeyDown(camouflageKey))
            {
                Debug.Log("[CamouflageAdapter] C keyDown detected");
                TryHandleKeyDown();
            }
            
            // C Up 감지
            if (Input.GetKeyUp(camouflageKey))
            {
                OnKeyReleased();
            }
        }

        /// <summary>
        /// C Down 처리
        /// </summary>
        private void TryHandleKeyDown()
        {
            Debug.Log($"[CamouflageAdapter] TryHandleKeyDown. Current state: {_stateMachine.CurrentState}");

            // Perfect 상태에서 C Down → 즉시 취소 (의태 해제)
            // 색상은 Update에서 천천히 복원됨
            if (_stateMachine.CurrentState == CamouflageState.Perfect)
            {
                Debug.Log("[CamouflageAdapter] Perfect state, C Down → cancelling camouflage");
                _stateMachine.CancelCamouflage(true);
                _justTransitionedFromPerfect = false;
                _transitionTimer = 0f;
                return;
            }

            // None 상태에서만 Attached 시작
            if (_stateMachine.CurrentState != CamouflageState.None)
            {
                Debug.Log("[CamouflageAdapter] State is not None, skipping attach");
                return;
            }

            TryStartAttach();
        }

        /// <summary>
        /// Attached 상태로 전환 시도 (C Down)
        /// </summary>
        private void TryStartAttach()
        {
            Debug.Log("[CamouflageAdapter] TryStartAttach called");

            // OriginalRate 복원 중이면 취소 (새로운 의태 시작)
            if (_isRestoringRate)
            {
                Debug.Log("[CamouflageAdapter] Cancelling rate restore for new camouflage");
                _isRestoringRate = false;
                _rateRestoreProgress = 0f;
            }

            // 반경 내 가장 가까운 오브젝트 탐지
            GameObject nearest = _detector.FindNearestCandidate(transform.position);

            if (nearest != null)
            {
                Debug.Log($"[CamouflageAdapter] Found target: {nearest.name}");
                _originalPosition = transform.position;
                _stateMachine.StartAttach(nearest);
                
                // 의태 시작 시 이동 무시 타이머 설정
                _ignoreMovementTimer = IGNORE_MOVEMENT_AFTER_ATTACH;
                
                // 의태 시작 시 Octopus Material 적용
                Debug.Log("[CamouflageAdapter] Applying Octopus Material on attach start");
                _materialCloner?.ApplyOctopusMaterial();
                
                // 의태 시작 시 SpriteRenderer.color를 타겟 색으로 즉시 변경
                _materialCloner?.BlendToTarget(nearest, 1f);
                
                // 의태 시작 시 OriginalRate를 1로 설정
                _materialCloner?.SetOriginalRate(1f);
                
                spriteDirector?.ChangeToDefaultSprite();
            }
            else
            {
                Debug.LogWarning("[CamouflageAdapter] No target found nearby!");
            }
        }

        /// <summary>
        /// 키가 떼어졌을 때 처리 (C Up)
        /// </summary>
        private void OnKeyReleased()
        {
            Debug.Log($"[CamouflageAdapter] C key released. State: {_stateMachine.CurrentState}");

            if (_stateMachine.CurrentState == CamouflageState.None)
            {
                return;
            }

            // Perfect 도달 전이면 취소
            // 색상은 Update에서 천천히 복원됨
            if (!_stateMachine.IsPerfectReached)
            {
                Debug.Log("[CamouflageAdapter] Not perfect yet, cancelling...");
                _stateMachine.CancelCamouflage(true);
            }
            else
            {
                // Perfect 도달했으면 유지 (아무것도 안 함)
                Debug.Log("[CamouflageAdapter] Perfect reached, maintaining camouflage...");
            }
        }

        /// <summary>
        /// 색상 및 OriginalRate 업데이트
        /// </summary>
        private void UpdateBlend()
        {
            // OriginalRate 복원 중이면 천천히 복원
            if (_isRestoringRate)
            {
                _rateRestoreProgress += Time.deltaTime;
                float progress = Mathf.Clamp01(_rateRestoreProgress / RATE_RESTORE_DURATION);
                
                // 0 → 1로 복원 (같은 속도로)
                float rate = Mathf.Lerp(0f, 1f, progress);
                _materialCloner?.SetOriginalRate(rate);
                
                // 복원 완료
                if (progress >= 1f)
                {
                    _isRestoringRate = false;
                    _rateRestoreProgress = 0f;
                    Debug.Log("[CamouflageAdapter] OriginalRate restore complete");
                }
                return;
            }

            if (_stateMachine.TargetObject == null) return;

            switch (_stateMachine.CurrentState)
            {
                case CamouflageState.Attached:
                    // SpriteRenderer.color를 타겟 색으로 즉시 변경, OriginalRate = 1
                    _materialCloner?.BlendToTarget(_stateMachine.TargetObject, 1f);
                    _materialCloner?.SetOriginalRate(1f);
                    break;

                case CamouflageState.Locked:
                    // SpriteRenderer.color는 유지, OriginalRate = 1
                    _materialCloner?.SetOriginalRate(1f);
                    break;

                case CamouflageState.Partial:
                    // SpriteRenderer.color는 유지 (이미 타겟 색)
                    // OriginalRate: 1 → 0 감소 (blendProgress에 비례)
                    if (_stateMachine is CamouflageStateMachine stateMachineImpl)
                    {
                        float rate = Mathf.Lerp(1f, 0f, stateMachineImpl.BlendProgress);
                        _materialCloner?.SetOriginalRate(rate);
                    }
                    break;

                case CamouflageState.Perfect:
                    // SpriteRenderer.color는 유지, OriginalRate = 0
                    _materialCloner?.SetOriginalRate(0f);
                    break;

                case CamouflageState.None:
                    // 해제 시 SpriteRenderer.color는 변경하지 않음
                    // OriginalRate는 _isRestoringRate에서 처리
                    break;
            }
        }

        /// <summary>
        /// 이동 방향에 따른 스프라이트 업데이트 (SpriteDirector에 위임)
        /// </summary>
        private void UpdateSpriteDirection()
        {
            if (_playerMovement == null || spriteDirector == null) return;
            spriteDirector.UpdateDirection(_playerMovement.Direction);
        }

        /// <summary>
        /// 위치 스냅 업데이트
        /// </summary>
        private void UpdatePosition()
        {
            if (_stateMachine.TargetObject == null) return;

            Vector3 targetPos = _stateMachine.TargetObject.transform.position;
            
            // 끼임 방지를 위해 Z 위치를 살짝 앞으로 (카메라 방향)
            // 오브젝트와 같은 Z에 있으면 충돌해서 끼이므로 0.1f 앞에 배치
            const float FRONT_OFFSET = 0.1f;

            switch (_stateMachine.CurrentState)
            {
                case CamouflageState.Attached:
                    // Attached 상태: X, Y는 유지, Z만 타겟보다 살짝 앞으로 보정
                    transform.position = new Vector3(transform.position.x, transform.position.y, targetPos.z - FRONT_OFFSET);
                    break;

                case CamouflageState.Locked:
                case CamouflageState.Approaching:
                case CamouflageState.Partial:
                case CamouflageState.Perfect:
                    // Attached 완료 후: X, Y는 유지, Z만 타겟보다 살짝 앞으로 보정
                    transform.position = new Vector3(transform.position.x, transform.position.y, targetPos.z - FRONT_OFFSET);
                    break;

                case CamouflageState.None:
                    // None으로 돌아왔을 때 원래 위치 복원 (선택적)
                    // 지금은 복원 안 함
                    break;
            }
        }

        /// <summary>
        /// 의태 시작 이펙트 생성
        /// </summary>
        private void PlayStartVfx()
        {
            if (startVfxPrefab == null)
            {
                Debug.LogWarning("[CamouflageAdapter] 아티스트 알림: Start VFX Prefab이 할당되지 않았습니다.");
                return;
            }

            Transform spawnParent = followPlayerOnStart ? transform : null;
            GameObject vfx = Instantiate(startVfxPrefab, transform.position, Quaternion.identity, spawnParent);
            Debug.Log($"[CamouflageAdapter] Played Start VFX: {vfx.name} (Follow: {followPlayerOnStart})");
        }

        /// <summary>
        /// 의태 해제 이펙트 생성 (랜덤 형태 기능 및 단일 애니메이션 기능 포함)
        /// </summary>
        private void PlayEndVfx()
        {
            // 1. 단일 해제 애니메이션 생성
            if (endAnimVfxPrefab != null)
            {
                Transform animParent = followPlayerOnEndAnim ? transform : null;
                GameObject animVfx = Instantiate(endAnimVfxPrefab, transform.position, Quaternion.identity, animParent);
                Debug.Log($"[CamouflageAdapter] Played End Animation VFX: {animVfx.name} (Follow: {followPlayerOnEndAnim})");
            }

            // 2. 랜덤 바닥 흔적 생성
            if (endVfxPrefabs == null || endVfxPrefabs.Length == 0)
            {
                Debug.LogWarning("[CamouflageAdapter] 아티스트 알림: End VFX Prefabs 배열(흔적용)이 비어있습니다.");
                return;
            }

            // 랜덤 선택
            int randomIndex = Random.Range(0, endVfxPrefabs.Length);
            GameObject selectedPrefab = endVfxPrefabs[randomIndex];

            if (selectedPrefab == null) return;

            Transform spawnParent = followPlayerOnEnd ? transform : null;
            // 아티스트 요구사항: 정면 고정을 위해 Quaternion.identity 사용
            GameObject vfx = Instantiate(selectedPrefab, transform.position, Quaternion.identity, spawnParent);
            Debug.Log($"[CamouflageAdapter] Played End VFX (Random {randomIndex}): {vfx.name} (Follow: {followPlayerOnEnd})");
        }

        /// <summary>
        /// 현재 의태 상태 확인 (외부 참조용)
        /// </summary>
        public CamouflageState CurrentState => _stateMachine.CurrentState;

        /// <summary>
        /// 의태 가능한 오브젝트 탐지 (디버그/UI용)
        /// </summary>
        public GameObject FindNearestCamouflageable()
        {
            return _detector.FindNearestCandidate(transform.position);
        }
    }
}