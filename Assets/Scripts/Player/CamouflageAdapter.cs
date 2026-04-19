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
        private float _originalZ;  // 원래 Z값 저장
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

            // [Artist Fallback] 머티리얼 누락 시 경고 로그 (의태 시도 중일 때만)
            if (_stateMachine.CurrentState != CamouflageState.None && _materialCloner != null && !_materialCloner.IsUsingOctopusMaterial)
            {
                if (Time.frameCount % 120 == 0) // 매 2초마다 출력
                {
                    Debug.LogWarning("[CamouflageAdapter] 아티스트 알림: Resources/Materials/Octopus 머티리얼이 없습니다! 비주얼 효과가 제한됩니다.");
                }
            }

            // 상태가 None으로 변화 → 취소됨 → OriginalRate 복원 시작 + Outline 복원 + Z값 복원
            if (wasNotNone && _stateMachine.CurrentState == CamouflageState.None)
            {
                Debug.Log($"[CamouflageAdapter] State returned to None. wasNotNone={wasNotNone}, _isRestoringRate will be set to true");
                
                // [이벤트] 의태 해제
                CamouflageEvents.InvokeCamouflageEnd(_stateMachine.TargetObject);
                
                _isRestoringRate = true;
                _rateRestoreProgress = 0f;
                StartRestoreOutline();

                // [2D OutlineHidden] Player Z값 원래대로 복원
                Vector3 restorePos = transform.position;
                restorePos.z = _originalZ;
                transform.position = restorePos;

                // [2D OutlineHidden] 타겟 Material 원래대로 복원
                if (_stateMachine.TargetObject != null)
                {
                    _materialCloner?.RestoreTargetMaterial(_stateMachine.TargetObject);
                }
                
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
                    
                    // [이벤트] 의태 상태 변경 + 의태 시작
                    CamouflageEvents.InvokeStateChanged(_stateMachine.CurrentState);
                    CamouflageEvents.InvokeCamouflageStart(_stateMachine.TargetObject);

                    // 시작 이펙트 생성
                    PlayStartVfx();
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
                    CamouflageEvents.InvokeCamouflageComplete(_stateMachine.TargetObject);
                }
            }
            
            // 상태 변경 이벤트 (모든 상태 변화에서 발생)
            if (prevState != _stateMachine.CurrentState && _stateMachine.CurrentState != CamouflageState.None)
            {
                if (!(prevState == CamouflageState.None && _stateMachine.CurrentState != CamouflageState.None))
                {
                    CamouflageEvents.InvokeStateChanged(_stateMachine.CurrentState);
                }
            }

            // 의태 상태에 따른 위치 조정
            UpdatePosition();

            // Outline 색상 업데이트
            UpdateOutlineColor();

            // Attached 완료 전에는 색상 보간 처리 안 함
            if (_stateMachine.CurrentState == CamouflageState.Attached && !_stateMachine.IsAttachedComplete)
            {
                return;
            }

            // 색상 보간 업데이트
            UpdateBlend();
        }

        private void HandleKeyInput()
        {
            if (Input.GetKeyDown(camouflageKey))
            {
                TryHandleKeyDown();
            }

            if (Input.GetKeyUp(camouflageKey))
            {
                OnKeyReleased();
            }
        }

        private void TryHandleKeyDown()
        {
            if (_stateMachine.CurrentState == CamouflageState.Perfect)
            {
                _stateMachine.CancelCamouflage(true);
                StartRestoreOutline();
                _justTransitionedFromPerfect = false;
                _transitionTimer = 0f;
                return;
            }

            if (_stateMachine.CurrentState != CamouflageState.None) return;

            TryStartAttach();
        }

        private void TryStartAttach()
        {
            if (_isRestoringRate)
            {
                _isRestoringRate = false;
                _rateRestoreProgress = 0f;
            }

            GameObject nearest = _detector.FindNearestCandidate(transform.position);

            if (nearest != null)
            {
                _originalPosition = transform.position;
                _originalZ = transform.position.z;
                _stateMachine.StartAttach(nearest);

                SetupOutlineForTarget(nearest);
                _ignoreMovementTimer = IGNORE_MOVEMENT_AFTER_ATTACH;
                _isRestoringOutline = false;

                _materialCloner?.ApplyOctopusMaterial();
                _materialCloner?.BlendToTarget(nearest, 1f);
                _materialCloner?.SetOriginalRate(1f);

                spriteDirector?.ChangeToDefaultSprite();
                spriteDirector?.UpdateColorPart(_playerMovement.Direction);
            }
        }

        private void OnKeyReleased()
        {
            if (_stateMachine.CurrentState == CamouflageState.None) return;

            if (!_stateMachine.IsPerfectReached)
            {
                _stateMachine.CancelCamouflage(true);
                StartRestoreOutline();
            }
        }

        private void UpdateBlend()
        {
            if (_isRestoringRate)
            {
                _rateRestoreProgress += Time.deltaTime;
                float progress = Mathf.Clamp01(_rateRestoreProgress / RATE_RESTORE_DURATION);
                float rate = Mathf.Lerp(0f, 1f, progress);
                _materialCloner?.SetOriginalRate(rate);

                if (progress >= 1f)
                {
                    _isRestoringRate = false;
                    _rateRestoreProgress = 0f;
                }
                return;
            }

            if (_stateMachine.TargetObject == null) return;

            switch (_stateMachine.CurrentState)
            {
                case CamouflageState.Attached:
                    _materialCloner?.BlendToTarget(_stateMachine.TargetObject, 1f);
                    _materialCloner?.SetOriginalRate(1f);
                    break;
                case CamouflageState.Locked:
                    _materialCloner?.SetOriginalRate(1f);
                    break;
                case CamouflageState.Partial:
                    if (_stateMachine is CamouflageStateMachine stateMachineImpl)
                    {
                        float rate = Mathf.Lerp(1f, 0f, stateMachineImpl.BlendProgress);
                        _materialCloner?.SetOriginalRate(rate);
                    }
                    break;
                case CamouflageState.Perfect:
                    _materialCloner?.SetOriginalRate(0f);
                    break;
            }
        }

        private void UpdateSpriteDirection()
        {
            if (_playerMovement == null || spriteDirector == null) return;

            if (_stateMachine.CurrentState == CamouflageState.None)
                spriteDirector.UpdateDirection(_playerMovement.Direction);
            else
                spriteDirector.UpdateColorPart(_playerMovement.Direction);
        }

        private void UpdatePosition()
        {
            if (_stateMachine.TargetObject == null) return;

            Vector3 targetPos = CalculateTargetPosition();

            switch (_stateMachine.CurrentState)
            {
                case CamouflageState.Attached:
                    _positionLerpProgress += Time.deltaTime / attachMoveSpeed;
                    _positionLerpProgress = Mathf.Clamp01(_positionLerpProgress);

                    float startZ = _originalPosition.z;
                    float targetZ = targetPos.z;
                    float newZ = startZ + ((targetZ - startZ) * _positionLerpProgress);
                    transform.position = new Vector3(transform.position.x, transform.position.y, newZ);
                    break;
                default:
                    // Attached 이후 혹은 이동 중일 때 Z값 스냅
                    if (_stateMachine.CurrentState != CamouflageState.None)
                        transform.position = new Vector3(transform.position.x, transform.position.y, targetPos.z);
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
            // 아티스트 요구사항: 프리팹 회전 보존 + 가림 방지를 위해 Z축 상으로 카메라 방향(-0.5f) 오프셋 추가
            Vector3 spawnPos = transform.position + new Vector3(0, 0, -0.5f);
            GameObject vfx = Instantiate(startVfxPrefab, spawnPos, startVfxPrefab.transform.rotation, spawnParent);
            
            Debug.Log($"<color=cyan>[의태 시작]</color> 시각 효과 생성됨: {vfx.name} (Z-Offset 적용 완료)");
        }

        /// <summary>
        /// 의태 해제 이펙트 생성 (랜덤 기능 및 애니메이션 기능 포함)
        /// </summary>
        private void PlayEndVfx()
        {
            // 1. 단일 해제 애니메이션 생성
            if (endAnimVfxPrefab != null)
            {
                Transform animParent = followPlayerOnEndAnim ? transform : null;
                // 가림 방지를 위해 Z축 상으로 카메라 방향(-0.5f) 오프셋 추가
                Vector3 animPos = transform.position + new Vector3(0, 0, -0.5f);
                GameObject animVfx = Instantiate(endAnimVfxPrefab, animPos, endAnimVfxPrefab.transform.rotation, animParent);
                Debug.Log($"<color=yellow>[의태 해제]</color> 애니메이션 효과 재생: {animVfx.name}");
            }

            // 2. 랜덤 바닥 흔적 생성
            if (endVfxPrefabs == null || endVfxPrefabs.Length == 0)
            {
                Debug.LogWarning("[CamouflageAdapter] 아티스트 알림: End VFX Prefabs 배열(흔적용)이 비어있습니다.");
                return;
            }

            int randomIndex = Random.Range(0, endVfxPrefabs.Length);
            GameObject selectedPrefab = endVfxPrefabs[randomIndex];

            if (selectedPrefab == null) return;

            Transform spawnParent = followPlayerOnEnd ? transform : null;
            // 아티스트 요구사항: 프리팹 회전 보존 + 가림 방지를 위해 Z축 상으로 카메라 방향(-0.5f) 오프셋 추가
            Vector3 tracePos = transform.position + new Vector3(0, 0, -0.5f);
            GameObject vfx = Instantiate(selectedPrefab, tracePos, selectedPrefab.transform.rotation, spawnParent);
            Debug.Log($"<color=white>[의태 흔적]</color> 랜덤 흔적 생성: {vfx.name}");
        }

        private Vector3 CalculateTargetPosition()
        {
            if (_stateMachine.TargetObject == null) return transform.position;

            Vector3 targetPos = _stateMachine.TargetObject.transform.position;
            Vector3 offset = _isAttachingFromBehind
                ? -_stateMachine.TargetObject.transform.forward * BACK_OFFSET
                : _stateMachine.TargetObject.transform.forward * BACK_OFFSET;

            return targetPos + offset;
        }

        private void SetupOutlineForTarget(GameObject target)
        {
            Transform visual = transform.Find("Visual");
            _targetOutline = visual != null ? visual.GetComponent<Outline>() : GetComponent<Outline>();

            _outlineChangeProgress = 0f;
            _positionLerpProgress = 0f;

            if (_targetOutline == null) return;

            Vector3 dirToPlayer = (transform.position - target.transform.position).normalized;
            Vector3 targetForward = target.transform.forward;
            _isAttachingFromBehind = Vector3.Dot(dirToPlayer, targetForward) < 0f;

            _originalOutlineColor = _targetOutline.OutlineColor;

            Renderer targetRenderer = target.GetComponent<Renderer>();
            if (targetRenderer != null)
            {
                Color targetColor = targetRenderer.sharedMaterial?.color ?? Color.white;
                _darkOutlineColor = new Color(targetColor.r * 0.55f, targetColor.g * 0.55f, targetColor.b * 0.55f);
            }
            else
            {
                _darkOutlineColor = Color.white;
            }
        }

        private void UpdateOutlineColor()
        {
            if (_targetOutline == null) return;

            if (_isRestoringOutline)
            {
                _outlineChangeProgress += Time.deltaTime / RATE_RESTORE_DURATION;
                _outlineChangeProgress = Mathf.Clamp01(_outlineChangeProgress);
                _targetOutline.OutlineColor = Color.Lerp(_darkOutlineColor, Color.white, _outlineChangeProgress);

                if (_outlineChangeProgress >= 1f)
                {
                    _isRestoringOutline = false;
                    _targetOutline = null;
                }
                return;
            }

            if (_stateMachine.CurrentState == CamouflageState.None || _stateMachine.TargetObject == null) return;

            if (_stateMachine.CurrentState == CamouflageState.Attached && !_stateMachine.IsAttachedComplete) return;

            _outlineChangeProgress += Time.deltaTime / blendTime;
            _outlineChangeProgress = Mathf.Clamp01(_outlineChangeProgress);
            _targetOutline.OutlineColor = Color.Lerp(_originalOutlineColor, _darkOutlineColor, _outlineChangeProgress);
        }

        private void StartRestoreOutline()
        {
            if (_targetOutline == null) return;
            _isRestoringOutline = true;
            _outlineChangeProgress = 0f;
        }

        public CamouflageState CurrentState => _stateMachine.CurrentState;
    }
}