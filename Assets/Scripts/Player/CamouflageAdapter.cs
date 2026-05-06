using UnityEngine;
using System.Collections;
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
        [Tooltip("의태 가능 오브젝트 탐지 반경")]
        [SerializeField] private float detectionRadius = 1.0f;
        [Tooltip("의태 감지 레이어 마스크")]
        [SerializeField] private LayerMask camouflageLayer = -1;

        [Header("의태 시간 설정")]
        [Tooltip("의태 부착 지연 시간")]
        [SerializeField] private float attachDelay = 0.3f;
        [Tooltip("의태 잠금 시간")]
        [SerializeField] private float lockTime = 0.4f;
        [Tooltip("의태 혼합(블렌드) 시간")]
        [SerializeField] private float blendTime = 1.0f;
        [Tooltip("완벽 의태 도달 시간")]
        [SerializeField] private float perfectTime = 2.0f;

        [Header("의태 키 설정")]
        [Tooltip("의태 키")]
        [SerializeField] private KeyCode camouflageKey = KeyCode.C;

        [Header("의존성")]
        [Tooltip("스프라이트 디렉터 참조")]
        [SerializeField] private SpriteDirector spriteDirector;

        private ICamouflageDetector _detector;
        private ICamouflageStateMachine _stateMachine;
        private IMaterialCloner _materialCloner;
        private PlayerMovementAdapter _playerMovement;
        private Renderer _playerRenderer;
        private IEventBus _eventBus;                      // EventBus 참조

        // ── 아키텍처: OnStateChanged 이벤트 구독 + 코루틴 기반 애니메이션 ──

        private Vector3 _originalPosition;
        private float _originalZ;

        // 코루틴 관리
        private Coroutine _restoreCoroutine;
        private Coroutine _enterTimerCoroutine;
        private Coroutine _perfectCooldownCoroutine;

        // 이동 블록 카운터 (여러 소스가 동시에 블록 가능)
        private int _movementBlockCount;
        private bool IsMovementBlocked => _movementBlockCount > 0;

        // End 이벤트 중복 방지 (외부 호출자가 미리 발행했는지)
        private bool _didFireEndEvent;

        private const float RATE_RESTORE_DURATION = 1.4f;
        private const float PERFECT_COOLDOWN_DURATION = 0.5f;
        private const float ENTER_DELAY_DURATION = 0.35f;
        private const float BACK_OFFSET = 0.1f;

        // C 키 쿨타임: 연타 방지
        private float _camouflageCooldown;
        [Tooltip("의태 쿨타임 (연타 방지)")]
        [SerializeField] private float camouflageCooldownTime = 0.5f;

        [Header("의태 이동 설정")]
        [Tooltip("의태 부착 중 이동 속도")]
        [SerializeField] private float attachMoveSpeed = 5f;
        private float _positionLerpProgress;

        // Outline 관련
        private Outline _targetOutline;
        private Color _originalOutlineColor;
        private Color _darkOutlineColor;
        private float _outlineChangeProgress;
        private bool _isAttachingFromBehind;

        // 블렌드 상태 기억 (복원 시 시작값으로 사용)
        private float _currentBlendRate = 1f;

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

            // Detector: Config 직접 생성 후 주입 (DI 우회 — InitOrder 안전성)
            var detectorConfig = new CamouflageDetectorConfig(detectionRadius, camouflageLayer);
            _detector = new CamouflageDetector(detectorConfig);

            // StateMachine: Config 직접 생성 후 주입 (DI 우회 — InitOrder 안전성)
            var stateConfig = new CamouflageStateMachineConfig(attachDelay, lockTime, blendTime, perfectTime);
            _stateMachine = new CamouflageStateMachine(stateConfig);

            // EventBus 해결
            if (GameManager.Container != null && GameManager.Container.IsRegistered<IEventBus>())
            {
                _eventBus = GameManager.Container.Resolve<IEventBus>();
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

            // DI: ICamouflageStateProvider self-register (EnemyAIController 등에서 resolve)
            if (GameManager.Container != null)
            {
                GameManager.Container.RegisterInstance<ICamouflageStateProvider>(this);
            }

            // StateMachine.OnStateChanged 구독 → polling 제거
            if (_stateMachine != null)
            {
                _stateMachine.OnStateChanged += HandleStateChanged;
            }
        }

        private void OnEnable()
        {
            // 조류 밀림 이벤트 구독 (EventBus 통해)
            _eventBus?.Subscribe<PlayerPushedByTideEvent>(OnPlayerPushedByTideEvent);
        }

        private void OnDisable()
        {
            // 조류 밀림 이벤트 해제
            _eventBus?.Unsubscribe<PlayerPushedByTideEvent>(OnPlayerPushedByTideEvent);
        }

        private void OnDestroy()
        {
            if (_stateMachine != null)
            {
                _stateMachine.OnStateChanged -= HandleStateChanged;
            }
            StopAllCoroutines();
        }

        /// <summary>
        /// 조류에 밀렸을 때 처리
        /// 의태 중이고 타겟과의 거리가 detectionRadius를 벗어나면 의태 해제
        /// </summary>
        private void OnPlayerPushedByTideEvent(PlayerPushedByTideEvent e)
        {
            if (_stateMachine.CurrentState == CamouflageState.None) return;
            if (_stateMachine.TargetObject == null) return;

            float distanceToTarget = Vector3.Distance(transform.position, _stateMachine.TargetObject.transform.position);

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
            _didFireEndEvent = true;
            _eventBus?.Publish(new CamouflageEndEvent(_stateMachine.TargetObject));
            _stateMachine.CancelCamouflage(); // → OnStateChanged(None) → OnExitCamouflage가 복원 처리
        }

        private void Update()
        {
            HandleKeyInput();

            // 이동 잠금 / 벽 충돌 제어
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

            // 이동 상태 계산 (코루틴 기반 블록)
            bool isMoving = !IsMovementBlocked && _playerMovement != null && _playerMovement.IsMoving;

            // 상태 머신 업데이트 → 내부에서 OnStateChanged 자동 발행
            _stateMachine.Update(Time.deltaTime, isMoving);

            // 활성 의태 중: 매 프레임 애니메이션
            if (_stateMachine.CurrentState != CamouflageState.None)
            {
                UpdatePosition();
                UpdateOutlineColor();

                if (_stateMachine.CurrentState != CamouflageState.Attached || _stateMachine.IsAttachedComplete)
                {
                    UpdateBlend();
                }
            }
        }

        // ====================================================================
        // OnStateChanged 이벤트 핸들러 (polling 제거 → 단일 진리)
        // ====================================================================
        private void HandleStateChanged(CamouflageState prev, CamouflageState cur)
        {
            // ── ENTER: None → 의태 활성 ──
            if (prev == CamouflageState.None && cur != CamouflageState.None)
            {
                OnEnterCamouflage(cur);
                return;
            }

            // ── EXIT: 의태 → None ──
            if (prev != CamouflageState.None && cur == CamouflageState.None)
            {
                OnExitCamouflage(prev);
                return;
            }

            // ── 활성 의태 중 상태 전환 ──
            if (cur == CamouflageState.Perfect && prev != CamouflageState.Perfect)
            {
                _camouflageCooldown = 0f;
                _eventBus?.Publish(new CamouflageStateChangedEvent(cur));
                _eventBus?.Publish(new CamouflageCompleteEvent(_stateMachine.TargetObject));
            }
            else if (cur != CamouflageState.None)
            {
                _eventBus?.Publish(new CamouflageStateChangedEvent(cur));
            }
        }

        private void OnEnterCamouflage(CamouflageState cur)
        {
            _materialCloner?.ApplyOctopusMaterial();
            _eventBus?.Publish(new CamouflageStateChangedEvent(cur));

            // 이동 블록 타이머 시작
            StartEnterDelay();
        }

        private void OnExitCamouflage(CamouflageState prev)
        {
            // End 이벤트 (외부에서 이미 발행했으면 스킵)
            if (!_didFireEndEvent)
            {
                _eventBus?.Publish(new CamouflageEndEvent(_stateMachine.TargetObject));
            }
            _didFireEndEvent = false;

            // 복원 코루틴 시작
            StartRestoreSequence();

            // Z값 복원
            Vector3 restorePos = transform.position;
            restorePos.z = _originalZ;
            transform.position = restorePos;

            // 타겟 Material 복원
            if (_stateMachine.TargetObject != null)
            {
                _materialCloner?.RestoreTargetMaterial(_stateMachine.TargetObject);
            }

            // Perfect → None 시 잠시 이동 블록
            if (prev == CamouflageState.Perfect)
            {
                StartPerfectCooldown();
            }
        }

        // ====================================================================
        // 코루틴: 8개 플래그 → 3개 코루틴
        // ====================================================================

        /// <summary>색상 + Outline 복원 (RATE_RESTORE_DURATION 초)</summary>
        private void StartRestoreSequence()
        {
            if (_restoreCoroutine != null) StopCoroutine(_restoreCoroutine);
            _restoreCoroutine = StartCoroutine(RestoreCoroutine());
        }

        private IEnumerator RestoreCoroutine()
        {
            float timer = 0f;
            float startRate = _currentBlendRate;

            while (timer < RATE_RESTORE_DURATION)
            {
                timer += Time.deltaTime;
                float t = Mathf.Clamp01(timer / RATE_RESTORE_DURATION);

                float rate = Mathf.Lerp(startRate, 1f, t);
                _materialCloner?.SetOriginalRate(rate);

                if (_targetOutline != null)
                {
                    _targetOutline.OutlineColor = Color.Lerp(_darkOutlineColor, Color.white, t);
                }

                yield return null;
            }

            _materialCloner?.SetOriginalRate(1f);
            _currentBlendRate = 1f;
            _targetOutline = null;
        }

        /// <summary>의태 시작 직후 이동 블록 (ENTER_DELAY_DURATION 초)</summary>
        private void StartEnterDelay()
        {
            if (_enterTimerCoroutine != null) StopCoroutine(_enterTimerCoroutine);
            _enterTimerCoroutine = StartCoroutine(EnterDelayCoroutine());
        }

        private IEnumerator EnterDelayCoroutine()
        {
            _movementBlockCount++;
            yield return new WaitForSeconds(ENTER_DELAY_DURATION);
            _movementBlockCount--;
        }

        /// <summary>Perfect 직후 이동 블록 (PERFECT_COOLDOWN_DURATION 초)</summary>
        private void StartPerfectCooldown()
        {
            if (_perfectCooldownCoroutine != null) StopCoroutine(_perfectCooldownCoroutine);
            _perfectCooldownCoroutine = StartCoroutine(PerfectCooldownCoroutine());
        }

        private IEnumerator PerfectCooldownCoroutine()
        {
            _movementBlockCount++;
            yield return new WaitForSeconds(PERFECT_COOLDOWN_DURATION);
            _movementBlockCount--;
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
                _didFireEndEvent = true;
                _eventBus?.Publish(new CamouflageEndEvent(_stateMachine.TargetObject));
                _stateMachine.CancelCamouflage(); // → OnExitCamouflage가 복원 + PerfectCooldown 처리
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
            // 진행 중인 복원 코루틴 중단
            if (_restoreCoroutine != null)
            {
                StopCoroutine(_restoreCoroutine);
                _restoreCoroutine = null;
            }

            // End 이벤트 플래그 리셋 (이전 사이클 잔여 방지)
            _didFireEndEvent = false;

            // 반경 내 가장 가까운 오브젝트 탐지
            GameObject nearest = _detector.FindNearestCandidate(transform.position);

            if (nearest != null)
            {
                _originalPosition = transform.position;
                _originalZ = transform.position.z;
                _positionLerpProgress = 0f;

                // StartAttach → OnStateChanged(None→Attached) → OnEnterCamouflage 자동 호출
                _stateMachine.StartAttach(nearest);

                // 쿨타임 설정
                _camouflageCooldown = camouflageCooldownTime;

                // 이벤트 (Start는 StateChanged보다 먼저)
                _eventBus?.Publish(new CamouflageStartEvent(nearest));

                // Outline 초기화
                SetupOutlineForTarget(nearest);

                // 타겟 색상 블렌드
                _materialCloner?.BlendToTarget(nearest, 1f);
                _materialCloner?.SetOriginalRate(1f);
                _currentBlendRate = 1f;

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
            if (!_stateMachine.IsPerfectReached)
            {
                _didFireEndEvent = true;
                _eventBus?.Publish(new CamouflageEndEvent(_stateMachine.TargetObject));
                _stateMachine.CancelCamouflage(); // → OnExitCamouflage가 복원 처리
            }
            // else: Perfect 도달했으면 유지 (아무 동작 안 함)
        }

        /// <summary>
        /// 활성 의태 중 색상 블렌드만 처리 (복원은 RestoreCoroutine이 담당)
        /// </summary>
        private void UpdateBlend()
        {
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
                        _currentBlendRate = rate;
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
        /// 활성 의태 중 Outline 색상만 처리 (복원은 RestoreCoroutine이 담당)
        /// </summary>
        private void UpdateOutlineColor()
        {
            if (_targetOutline == null) return;
            if (_stateMachine.CurrentState == CamouflageState.None) return;
            if (_stateMachine.TargetObject == null) return;

            // Attached 완료 전에는 보간 안 함
            if (_stateMachine.CurrentState == CamouflageState.Attached && !_stateMachine.IsAttachedComplete)
            {
                return;
            }

            // Attached 완료 후 → 타겟 색으로 보간
            _outlineChangeProgress += Time.deltaTime / blendTime;
            _outlineChangeProgress = Mathf.Clamp01(_outlineChangeProgress);

            Color newOutlineColor = Color.Lerp(_originalOutlineColor, _darkOutlineColor, _outlineChangeProgress);
            _targetOutline.OutlineColor = newOutlineColor;
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

            _didFireEndEvent = true;
            _eventBus?.Publish(new CamouflageEndEvent(_stateMachine.TargetObject));
            _stateMachine.CancelCamouflage(); // → OnExitCamouflage가 복원 처리
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