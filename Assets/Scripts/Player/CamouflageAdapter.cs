using UnityEngine;
using HideAndInk.Core.Interfaces;
using HideAndInk.Core.Perception;
using HideAndInk.Core.Managers;
using HideAndInk.Core.Events;

namespace HideAndInk.Player
{
    /// <summary>
    /// 의태 시스템 Unity 어댑터 (코디네이터)
    /// 토글 방식: C 누르고 있으면 진행, 떼면 Perfect 아니면 취소
    /// 실제 작업은 전용 컴포넌트에 위임
    /// </summary>
    [RequireComponent(typeof(PlayerMovementAdapter))]
    public sealed class CamouflageAdapter : MonoBehaviour, ICamouflageStateProvider
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
        private float _originalZ;  // 원래 Z값 저장
        private bool _justTransitionedFromPerfect;
        private float _transitionTimer;

        private bool _isRestoringRate;
        private float _rateRestoreProgress;
        private const float RATE_RESTORE_DURATION = 1.4f;

        // C 키 쿨타임: 연타 방지
        private float _camouflageCooldown;
        [SerializeField] private float camouflageCooldownTime = 0.5f;

        // 의태 취소 시 InvokeCamouflageEnd 중복 호출 방지
        private bool _hasInvokedEndEvent;

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
        private Color _darkOutlineColor;  // 타겟 어두운 색상 저장
        private float _outlineChangeProgress;
        private bool _isAttachingFromBehind;
        private bool _isRestoringOutline;

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

            // Detector: Config 생성 + DI 등록 후 Resolve
            var detectorConfig = new CamouflageDetectorConfig(detectionRadius, camouflageLayer);
            if (GameManager.Container != null && GameManager.Container.IsRegistered<ICamouflageDetector>())
            {
                GameManager.Container.RegisterInstance<ICamouflageDetectorConfig>(detectorConfig);
                _detector = GameManager.Container.Resolve<ICamouflageDetector>();
            }
            else
            {
                _detector = new CamouflageDetector(detectorConfig);
            }

            // StateMachine: Config 생성 + DI 등록 후 Resolve
            var stateConfig = new CamouflageStateMachineConfig(attachDelay, lockTime, blendTime, perfectTime);
            if (GameManager.Container != null && GameManager.Container.IsRegistered<ICamouflageStateMachine>())
            {
                GameManager.Container.RegisterInstance<ICamouflageStateMachineConfig>(stateConfig);
                _stateMachine = GameManager.Container.Resolve<ICamouflageStateMachine>();
            }
            else
            {
                _stateMachine = new CamouflageStateMachine(stateConfig);
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

        private void OnEnable()
        {
            // 조류 밀림 이벤트 구독
            HideAndInk.Core.Events.TideEvents.OnPlayerPushed += OnPlayerPushedByTide;
        }

        private void OnDisable()
        {
            // 조류 밀림 이벤트 해제
            HideAndInk.Core.Events.TideEvents.OnPlayerPushed -= OnPlayerPushedByTide;
        }

        /// <summary>
        /// 조류에 밀렸을 때 처리
        /// 의태 중이고 타겟과의 거리가 detectionRadius를 벗어나면 의태 해제
        /// </summary>
        private void OnPlayerPushedByTide(Vector3 pushDirection, float force)
        {
            if (_stateMachine.CurrentState == CamouflageState.None) return;
            if (_stateMachine.TargetObject == null) return;

            // Player와 타겟 간 거리 체크
            float distanceToTarget = Vector3.Distance(transform.position, _stateMachine.TargetObject.transform.position);

            // 거리가 탐지 반경을 벗어나면 의태 해제
            if (distanceToTarget > detectionRadius)
            {
#if UNITY_EDITOR
                Debug.Log($"[CamouflageAdapter] Tide pushed player too far from target ({distanceToTarget:F2}m > {detectionRadius:F2}m). Camouflage cancelled.");
#endif
                CancelCamouflageDueToTide();
            }
        }

        /// <summary>
        /// 조류로 인한 의태 해제 처리
        /// </summary>
        private void CancelCamouflageDueToTide()
        {
            _hasInvokedEndEvent = true;
            CamouflageEvents.InvokeCamouflageEnd(_stateMachine.TargetObject);

            _stateMachine.CancelCamouflage(true);
            _isRestoringRate = true;
            _rateRestoreProgress = 0f;
            StartRestoreOutline();
            _justTransitionedFromPerfect = false;
            _transitionTimer = 0f;
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

            // Perfect에서 전환 직후에는 잠시 이동 무시
            // (Partial/Perfect 도달하거나 0.5초 경과하면 해제)
            if (_justTransitionedFromPerfect)
            {
                isMoving = false;
                _transitionTimer += Time.deltaTime;
                if (_transitionTimer >= 0.5f)
                {
                    _justTransitionedFromPerfect = false;
                    _transitionTimer = 0f;
                }
            }

            var prevState = _stateMachine.CurrentState;
            bool wasNotNone = prevState != CamouflageState.None;

            // 상태 시스템 업데이트
            _stateMachine.Update(Time.deltaTime, isMoving);

            // 상태가 None으로 변화 → 취소됨 → OriginalRate 복원 시작 + Outline 복원 + Z값 복원
            if (wasNotNone && _stateMachine.CurrentState == CamouflageState.None)
            {
                // OnKeyReleased에서 이미 호출했다면 중복 방지
                if (!_hasInvokedEndEvent)
                {
                    // [이벤트] 의태 해제
                    CamouflageEvents.InvokeCamouflageEnd(_stateMachine.TargetObject);
                }
                _hasInvokedEndEvent = false;
                
                _isRestoringRate = true;
                _rateRestoreProgress = 0f;
                StartRestoreOutline();  // Outline 보간 복원 시작

                // [2D OutlineHidden] Player Z값 원래대로 복원
                Vector3 restorePos = transform.position;
                restorePos.z = _originalZ;
                transform.position = restorePos;

                // [2D OutlineHidden] 타겟 Material 원래대로 복원
                if (_stateMachine.TargetObject != null)
                {
                    _materialCloner?.RestoreTargetMaterial(_stateMachine.TargetObject);
                }
                
                // Note: SpriteRenderer.color은 변경하지 않음 - OriginalRate만으로 색상 조절
            }

            // 상태 변화 처리
            if (prevState != _stateMachine.CurrentState)
            {
                // None → 의태 상태로 전환 시 Octopus Material 적용
                if (prevState == CamouflageState.None && _stateMachine.CurrentState != CamouflageState.None)
                {
                    _materialCloner?.ApplyOctopusMaterial();
                    
                    // [이벤트] 의태 상태 변경 (Start는 TryStartAttach에서 이미 호출)
                    CamouflageEvents.InvokeStateChanged(_stateMachine.CurrentState);
                }

                // Partial 또는 Perfect에 도달하면 플래그 해제
                if (_stateMachine.CurrentState == CamouflageState.Partial ||
                    _stateMachine.CurrentState == CamouflageState.Perfect)
                {
                    _justTransitionedFromPerfect = false;
                }
                
                // Perfect 도달 시 이벤트 발생
                if (_stateMachine.CurrentState == CamouflageState.Perfect && prevState != CamouflageState.Perfect)
                {
                    // Perfect 도달 시 쿨타임 초기화 (다시 C 입력 가능)
                    _camouflageCooldown = 0f;
                    CamouflageEvents.InvokeCamouflageComplete(_stateMachine.TargetObject);
                }
            }
            
            // 상태 변경 이벤트 (모든 상태 변화에서 발생)
            if (prevState != _stateMachine.CurrentState && _stateMachine.CurrentState != CamouflageState.None)
            {
                // None → Attached는 위에서 이미 처리했으므로 중복 방지
                if (!(prevState == CamouflageState.None && _stateMachine.CurrentState != CamouflageState.None))
                {
                    CamouflageEvents.InvokeStateChanged(_stateMachine.CurrentState);
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
            // 쿨타임 감소
            if (_camouflageCooldown > 0f)
            {
                _camouflageCooldown -= Time.deltaTime;
            }

            // C Down 감지
            if (Input.GetKeyDown(camouflageKey))
            {
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
            // Perfect 상태에서는 쿨타임 무시 (즉시 해제 가능)
            if (_stateMachine.CurrentState != CamouflageState.Perfect && _camouflageCooldown > 0f)
            {
                return;
            }

            // Perfect 상태에서 C Down → 즉시 취소 (의태 해제)
            if (_stateMachine.CurrentState == CamouflageState.Perfect)
            {
                
                // End VFX 생성 (Perfect 해제 시에도 End VFX 필요)
                _hasInvokedEndEvent = true;
                CamouflageEvents.InvokeCamouflageEnd(_stateMachine.TargetObject);
                
                _stateMachine.CancelCamouflage(true);
                _isRestoringRate = true;
                _rateRestoreProgress = 0f;
                StartRestoreOutline();
                _justTransitionedFromPerfect = false;
                _transitionTimer = 0f;
                return;
            }

            // None 상태에서만 Attached 시작
            if (_stateMachine.CurrentState != CamouflageState.None)
            {
                return;
            }

            TryStartAttach();
        }

        /// <summary>
        /// Attached 상태로 전환 시도 (C Down)
        /// </summary>
        private void TryStartAttach()
        {
            // OriginalRate 복원 중이면 취소 (새로운 의태 시작)
            if (_isRestoringRate)
            {
                _isRestoringRate = false;
                _rateRestoreProgress = 0f;
            }

            // End 이벤트 플래그 리셋 (이전 사이클 잔여 방지)
            _hasInvokedEndEvent = false;

            // 반경 내 가장 가까운 오브젝트 탐지
            GameObject nearest = _detector.FindNearestCandidate(transform.position);

            if (nearest != null)
            {
                _originalPosition = transform.position;
                _originalZ = transform.position.z;  // 원래 Z값 저장
                _stateMachine.StartAttach(nearest);

                // 쿨타임 설정 (연타 방지)
                _camouflageCooldown = camouflageCooldownTime;

                // [이벤트] 의태 시작
                CamouflageEvents.InvokeCamouflageStart(nearest);

                // Outline 설정 (앞면/뒷면 감지)
                SetupOutlineForTarget(nearest);

                // 의태 시작 시 이동 무시 타이머 설정
                _ignoreMovementTimer = IGNORE_MOVEMENT_AFTER_ATTACH;

                // Outline 복원 플래그 리셋
                _isRestoringOutline = false;

                // 의태 색상 적용
                _materialCloner?.ApplyOctopusMaterial();
                _materialCloner?.BlendToTarget(nearest, 1f);
                _materialCloner?.SetOriginalRate(1f);

                spriteDirector?.ChangeToDefaultSprite();
                spriteDirector?.UpdateColorPart(_playerMovement.Direction);
            }
            else
            {
                Debug.LogWarning("[CamouflageAdapter] No camouflageable target found nearby!");
            }
        }

        /// <summary>
        /// 키가 떼어졌을 때 처리 (C Up)
        /// </summary>
        private void OnKeyReleased()
        {
            if (_stateMachine.CurrentState == CamouflageState.None)
            {
                return;
            }

            // Perfect 도달 전이면 취소
            // 색상은 Update에서 천천히 복원됨
            if (!_stateMachine.IsPerfectReached)
            {
                
                // Start VFX 즉시 삭제 (Update에서 상태 변화 감지 전에 미리 삭제)
                _hasInvokedEndEvent = true;
                CamouflageEvents.InvokeCamouflageEnd(_stateMachine.TargetObject);
                
                _stateMachine.CancelCamouflage(true);
                
                // 색상 복원 시작 (Update에서 wasNotNone 체크가 실패하므로 여기서 직접 설정)
                _isRestoringRate = true;
                _rateRestoreProgress = 0f;
                
                StartRestoreOutline();
            }
            else
            {
                // Perfect 도달했으면 유지 (Outline도 유지, 복원 안 함)
            }
        }

        private float _currentBlendRate = 1f; // 현재 애니메이션 보간 기억용

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

                // 취소 시점의 색상(_currentBlendRate)에서 1(원본)로 부드럽게 복원
                float rate = Mathf.Lerp(_currentBlendRate, 1f, progress);
                _materialCloner?.SetOriginalRate(rate);

                // 복원 완료
                if (progress >= 1f)
                {
                    _isRestoringRate = false;
                    _rateRestoreProgress = 0f;
                    _currentBlendRate = 1f;
                }
                return;
            }

            if (_stateMachine.TargetObject == null) return;

            switch (_stateMachine.CurrentState)
            {
                case CamouflageState.Attached:
                    _materialCloner?.BlendToTarget(_stateMachine.TargetObject, 1f);
                    _materialCloner?.SetOriginalRate(1f);
                    _currentBlendRate = 1f;
                    break;

                case CamouflageState.Locked:
                    _materialCloner?.SetOriginalRate(1f);
                    _currentBlendRate = 1f;
                    break;

                case CamouflageState.Partial:
                    if (_stateMachine is CamouflageStateMachine stateMachineImpl)
                    {
                        float rate = Mathf.Lerp(1f, 0f, stateMachineImpl.BlendProgress);
                        _materialCloner?.SetOriginalRate(rate);
                        _currentBlendRate = rate; // 현재 진행률 저장
                    }
                    break;

                case CamouflageState.Perfect:
                    _materialCloner?.SetOriginalRate(0f);
                    _currentBlendRate = 0f;
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

            // 뒷면에서 접근: 오브젝트 앞으로 이동
            // 앞면에서 접근: 오브젝트 뒤로 이동
            Vector3 offset = _isAttachingFromBehind
                ? _stateMachine.TargetObject.transform.forward * BACK_OFFSET
                : -_stateMachine.TargetObject.transform.forward * BACK_OFFSET;

            return targetPos + offset;
        }

        /// <summary>
        /// 타겟 오브젝트의 Outline 설정
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

            if (_targetOutline == null) return;

            // 앞면/뒷면 감지 (위치 계산용)
            Vector3 dirToPlayer = (transform.position - target.transform.position).normalized;
            Vector3 targetForward = target.transform.forward;
            float dot = Vector3.Dot(dirToPlayer, targetForward);
            _isAttachingFromBehind = dot > 0f;

            // Outline 원래 색상 저장
            _originalOutlineColor = _targetOutline.OutlineColor;

            // 타겟 어두운 색상 미리 계산
            Renderer targetRenderer = target.GetComponent<Renderer>();
            if (targetRenderer != null)
            {
                Color targetColor = targetRenderer.sharedMaterial?.color ?? Color.white;
                _darkOutlineColor = new Color(
                    targetColor.r * 0.55f,
                    targetColor.g * 0.55f,
                    targetColor.b * 0.55f
                );
            }
            else
            {
                _darkOutlineColor = Color.white;
            }
        }

        /// <summary>
        /// Outline 색상 업데이트 (blendTime과 동기화)
        /// </summary>
        private void UpdateOutlineColor()
        {
            if (_targetOutline == null) return;

            float duration = blendTime;

            // 의태 해제 시 Outline 복원 (천천히 의태 색상 → 흰색)
            if (_isRestoringOutline)
            {
                _outlineChangeProgress += Time.deltaTime / RATE_RESTORE_DURATION;
                _outlineChangeProgress = Mathf.Clamp01(_outlineChangeProgress);
                Color lerpColor = Color.Lerp(_darkOutlineColor, Color.white, _outlineChangeProgress);
                _targetOutline.OutlineColor = lerpColor;

                // 복원 완료
                if (_outlineChangeProgress >= 1f)
                {
                    _isRestoringOutline = false;
                    _targetOutline = null;
                }
                return;
            }

            // 의태 중이 아니면 Outline 복원 시작
            if (_stateMachine.CurrentState == CamouflageState.None)
            {
                return;
            }

            // 의태 중: TargetObject 필요
            if (_stateMachine.TargetObject == null) return;

            // Attached 상태: 아직 색상 보간 안 함
            if (_stateMachine.CurrentState == CamouflageState.Attached && !_stateMachine.IsAttachedComplete)
            {
                return;
            }

            // Attached 완료 후 ~ Partial까지: Outline 색상 보간 시작
            _outlineChangeProgress += Time.deltaTime / duration;
            _outlineChangeProgress = Mathf.Clamp01(_outlineChangeProgress);

            Color newOutlineColor = Color.Lerp(_originalOutlineColor, _darkOutlineColor, _outlineChangeProgress);
            _targetOutline.OutlineColor = newOutlineColor;
        }

        /// <summary>
        /// Outline 복원 시작 (의태 해제 시 호출 - OriginalRate 복원 속도와 동일)
        /// </summary>
        private void StartRestoreOutline()
        {
            if (_targetOutline == null) return;
            _isRestoringOutline = true;
            _outlineChangeProgress = 0f; // 복원 시작 (0 → 1로 가야 함)
        }

        /// <summary>
        /// 현재 의태 상태 확인 (외부 참조용) - ICamouflageStateProvider 구현
        /// </summary>
        public CamouflageState CurrentState => _stateMachine.CurrentState;

        /// <summary>
        /// 의태 중인지 여부 - ICamouflageStateProvider 구현
        /// </summary>
        public bool IsCamouflaging => _stateMachine.CurrentState != CamouflageState.None;

        /// <summary>
        /// 완벽 의태 여부 - ICamouflageStateProvider 구현
        /// </summary>
        public bool IsPerfect => _stateMachine.CurrentState == CamouflageState.Perfect;

        /// <summary>
        /// 현재 의태 중인 타겟 오브젝트 (없으면 null)
        /// </summary>
        public GameObject CurrentTarget => _stateMachine?.TargetObject;

        /// <summary>
        /// 강제 의태 해제 (백상아리 오브젝트 파괴 등)
        /// 지정된 targetObject와 현재 의태 대상이 같을 때만 해제
        /// </summary>
        /// <returns>실제로 의태가 해제되었으면 true</returns>
        public bool ForceCancelCamouflage(GameObject targetObject)
        {
            if (_stateMachine == null) return false;
            if (_stateMachine.CurrentState == CamouflageState.None) return false;
            if (_stateMachine.TargetObject != targetObject) return false;

            _hasInvokedEndEvent = true;
            CamouflageEvents.InvokeCamouflageEnd(_stateMachine.TargetObject);

            _stateMachine.CancelCamouflage(true);
            _isRestoringRate = true;
            _rateRestoreProgress = 0f;
            StartRestoreOutline();
            _justTransitionedFromPerfect = false;
            _transitionTimer = 0f;
            return true;
        }

        /// <summary>
        /// 의태 가능한 오브젝트 탐지 (디버그/UI용)
        /// </summary>
        public GameObject FindNearestCamouflageable()
        {
            return _detector.FindNearestCandidate(transform.position);
        }
    }
}