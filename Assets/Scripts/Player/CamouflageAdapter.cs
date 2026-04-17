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

        [Header("의태 이동 설정")]
        [SerializeField] private float attachMoveSpeed = 5f;
        private float _positionLerpProgress;

        // 뒷면 접근 시 이동 오프셋
        private const float BACK_OFFSET = 0.1f;

        // Outline 관련
        private Outline _targetOutline;
        private Color _originalOutlineColor;
        private float _outlineChangeProgress;
        private bool _isAttachingFromBehind;

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
                SpriteRenderer sr = visual != null
                    ? visual.GetComponentInChildren<SpriteRenderer>()
                    : GetComponentInChildren<SpriteRenderer>();
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

            // 상태가 None으로 변화 → 취소됨 → OriginalRate 복원 시작 + Outline 복원
            if (wasNotNone && _stateMachine.CurrentState == CamouflageState.None)
            {
                Debug.Log($"[CamouflageAdapter] State returned to None. wasNotNone={wasNotNone}, _isRestoringRate will be set to true");
                _isRestoringRate = true;
                _rateRestoreProgress = 0f;
                RestoreOutline();  // Outline도 함께 복원
                Debug.Log($"[CamouflageAdapter] After setting: _isRestoringRate={_isRestoringRate}, _rateRestoreProgress={_rateRestoreProgress}");
                // Note: SpriteRenderer.color은 변경하지 않음 - OriginalRate만으로 색상 조절
            }

            // 상태 변화 로그
            if (prevState != _stateMachine.CurrentState)
            {
                Debug.Log($"[CamouflageAdapter] State changed: {prevState} -> {_stateMachine.CurrentState}");

                // None → 의태 상태로 전환 시 Octopus Material 적용
                if (prevState == CamouflageState.None && _stateMachine.CurrentState != CamouflageState.None)
                {
                    Debug.Log($"[CamouflageAdapter] State changed from None! Current state: {_stateMachine.CurrentState}. Applying Octopus Material...");
                    Debug.Log($"[CamouflageAdapter] _materialCloner is null: {_materialCloner == null}");
                    _materialCloner?.ApplyOctopusMaterial();
                }

                // Partial 또는 Perfect에 도달하면 플래그 해제
                if (_stateMachine.CurrentState == CamouflageState.Partial ||
                    _stateMachine.CurrentState == CamouflageState.Perfect)
                {
                    _justTransitionedFromPerfect = false;
                }
            }

            // 의태 상태에 따른 위치 조정 (Attached 상태에서도 실행되어야 함)
            UpdatePosition();

            // Outline 색상 업데이트 - 상태 전환 시점에 맞춰 실행
            UpdateOutlineColor();

            // Attached 완료 전에는 색상 보간 처리 안 함
            if (_stateMachine.CurrentState == CamouflageState.Attached && !_stateMachine.IsAttachedComplete)
            {
                return;
            }

            // 색상 보간 업데이트
            UpdateBlend();
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
                RestoreOutline();
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

                // Outline 설정 (앞면/뒷면 감지)
                SetupOutlineForTarget(nearest);

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
                spriteDirector?.UpdateColorPart(_playerMovement.Direction);
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
                RestoreOutline();
            }
            else
            {
                // Perfect 도달했으면 유지하되, Outline은 복원 (의태 효과는 유지)
                Debug.Log("[CamouflageAdapter] Perfect reached, maintaining camouflage but restoring outline...");
                RestoreOutline();
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

            // 의태 중이 아니면 스프라이트도 함께 업데이트
            if (_stateMachine.CurrentState == CamouflageState.None)
            {
                spriteDirector.UpdateDirection(_playerMovement.Direction);
            }
            else
            {
                // 의태 중: 스프라이트는 그대로, ColorPart만 업데이트
                spriteDirector.UpdateColorPart(_playerMovement.Direction);
            }
        }

        /// <summary>
        /// 위치 스냅 업데이트 (Z값만 부드럽게 보간)
        /// </summary>
        private void UpdatePosition()
        {
            if (_stateMachine.TargetObject == null) return;

            Vector3 targetPos = CalculateTargetPosition();

            switch (_stateMachine.CurrentState)
            {
                case CamouflageState.Attached:
                    // Attached 상태: Z값만 천천히 오브젝트 위치로 보간
                    // attachMoveSpeed = 도달까지 걸리는 시간(초)
                    _positionLerpProgress += Time.deltaTime / attachMoveSpeed;
                    _positionLerpProgress = Mathf.Clamp01(_positionLerpProgress);

                    float startZ = _originalPosition.z;
                    float targetZ = targetPos.z;
                    float newZ = startZ + ((targetZ - startZ) * _positionLerpProgress);
                    transform.position = new Vector3(transform.position.x, transform.position.y, newZ);
                    break;

                case CamouflageState.Locked:
                case CamouflageState.Approaching:
                case CamouflageState.Partial:
                case CamouflageState.Perfect:
                    // Attached 완료 후: 현재 X,Y 유지, Z만 타겟으로
                    transform.position = new Vector3(transform.position.x, transform.position.y, targetPos.z);
                    break;

                case CamouflageState.None:
                    // None으로 돌아왔을 때 원래 위치 복원 (선택적)
                    // 지금은 복원 안 함
                    break;
            }
        }

        /// <summary>
        /// 목표 위치 계산 (앞면/뒷면 따라 다름)
        /// </summary>
        private Vector3 CalculateTargetPosition()
        {
            if (_stateMachine.TargetObject == null) return transform.position;

            Vector3 targetPos = _stateMachine.TargetObject.transform.position;

            // 뒷면에서 접근: 오브젝트 뒤로 이동
            // 앞면에서 접근: 오브젝트 앞으로 이동
            Vector3 offset = _isAttachingFromBehind
                ? -_stateMachine.TargetObject.transform.forward * BACK_OFFSET
                : _stateMachine.TargetObject.transform.forward * BACK_OFFSET;

            return targetPos + offset;
        }

        /// <summary>
        /// 타겟 오브젝트의 Outline 설정 (앞면/뒷면 감지)
        /// </summary>
        private void SetupOutlineForTarget(GameObject target)
        {
            // Player Visual에서 Outline 찾기
            Transform visual = transform.Find("Visual");
            if (visual != null)
            {
                _targetOutline = visual.GetComponent<Outline>();
            }
            else
            {
                _targetOutline = GetComponent<Outline>();
            }

            _outlineChangeProgress = 0f;
            _positionLerpProgress = 0f;

            if (_targetOutline == null)
            {
                _isAttachingFromBehind = false;
                return;
            }

            // 플레이어에서 타겟으로의 방향
            Vector3 dirToPlayer = (transform.position - target.transform.position).normalized;

            // 타겟의 forward 벡터 (앞면)
            Vector3 targetForward = target.transform.forward;

            // 내적으로 앞면/뒷면 판정
            // 음수 = 뒷면 (플레이어가 오브젝트 뒤에 있음)
            // 양수 = 앞면 (플레이어가 오브젝트 앞에 있음)
            float dot = Vector3.Dot(dirToPlayer, targetForward);
            _isAttachingFromBehind = dot < 0f;

            if (_isAttachingFromBehind)
            {
                // 뒷면에서 접근: Outline 저장 후 타겟 색으로 변경 시작
                _originalOutlineColor = _targetOutline.OutlineColor;
                _outlineChangeProgress = 0f;
            }
            else
            {
                // 앞면에서 접근: Outline 변경 없음
                _targetOutline = null;
            }
        }

        /// <summary>
        /// Outline 색상 업데이트 (상태 전환과同步)
        /// </summary>
        private void UpdateOutlineColor()
        {
            if (_targetOutline == null) return;
            if (_stateMachine.TargetObject == null) return;

            // 뒷면에서만 Outline 변경
            if (_isAttachingFromBehind)
            {
                // Attached 상태에서는 Outline 변경 안 함 (상태 전환 후 변경)
                if (_stateMachine.CurrentState == CamouflageState.Attached)
                {
                    return;
                }

                // Locked/Approaching/Partial 상태에서만 Outline 변경
                _outlineChangeProgress += Time.deltaTime / lockTime;
                _outlineChangeProgress = Mathf.Clamp01(_outlineChangeProgress);

                // 타겟 오브젝트의 메인 색상 가져오기 (어두운 계열로)
                Renderer targetRenderer = _stateMachine.TargetObject.GetComponent<Renderer>();
                if (targetRenderer != null)
                {
                    Color targetColor = targetRenderer.sharedMaterial?.color ?? Color.white;
                    // 오브젝트 색상의 55% 어두운 계열로 변경
                    Color darkOutlineColor = new Color(
                        targetColor.r * 0.55f,
                        targetColor.g * 0.55f,
                        targetColor.b * 0.55f
                    );
                    Color newOutlineColor = Color.Lerp(_originalOutlineColor, darkOutlineColor, _outlineChangeProgress);
                    _targetOutline.OutlineColor = newOutlineColor;
                }
            }
            else
            {
                // 앞면: Outline 원래 색상으로 복원
                if (_outlineChangeProgress > 0f)
                {
                    _outlineChangeProgress -= Time.deltaTime / lockTime;
                    _outlineChangeProgress = Mathf.Clamp01(_outlineChangeProgress);
                    _targetOutline.OutlineColor = Color.Lerp(_originalOutlineColor, Color.white, _outlineChangeProgress);
                }
            }
        }

        /// <summary>
        /// Outline 색상 즉시 복원 (의태 해제 시 호출)
        /// </summary>
        private void RestoreOutline()
        {
            if (_targetOutline == null) return;
            _targetOutline.OutlineColor = Color.white;
            _targetOutline = null;
            _outlineChangeProgress = 0f;
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