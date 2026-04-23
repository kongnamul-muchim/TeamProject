using UnityEngine;
using HideAndInk.Core.Enemy.Interfaces;

namespace HideAndInk.Core.Enemy.Boss.Gimmicks
{
    /// <summary>
    /// Ch.1 가자미 매복 기믹 (ScriptableObject)
    /// Player 근처에 매복 → 거리 기반 2단계 의심도 상승 → Chase 시 1회 돌진 → 추적
    /// </summary>
    [CreateAssetMenu(menuName = "Enemy Gimmicks/Ambush Gimmick", fileName = "AmbushGimmick")]
    public sealed class AmbushGimmick : ScriptableObject, IEnemyGimmick
    {
        public GimmickType Type => GimmickType.Ambush;

        [Header("매복 설정")]
        [Tooltip("매복 중 Player 추적 속도 (patrolSpeed와 별도)")]
        [SerializeField] private float patrolTrackSpeed = 2f;
        [Tooltip("순간이동 시 Player 뒤쪽 거리 (m)")]
        [SerializeField] private float teleportBehindDistance = 8f;
        [Tooltip("순간이동 시 Player 시야각 밖 체크 (true = Player가 보지 않는 방향에서만 순간이동)")]
        [SerializeField] private bool checkPlayerVisionForTeleport = true;
        [Tooltip("매복 위치 재설정 시 Z축 고정 여부 (true = X축만 접근)")]
        [SerializeField] private bool lockZAxis = true;
        [Tooltip("매복 위치에서 대기 시간 (초)")]
        [SerializeField] private float ambushDuration = 3f;

        [Header("매복 스프라이트")]
        [Tooltip("매복 상태일 때 표시할 스프라이트 (땅에 숨은 모습)")]
        [SerializeField] private Sprite ambushSprite;
        [Tooltip("매복 상태일 때 적용할 SpriteRenderer (비워두면 자동 탐색)")]
        [SerializeField] private SpriteRenderer targetSpriteRenderer;

        [Header("의심도 설정")]
        [Tooltip("의심도 감지 범위 (X, Z). 타원형 영역으로 계산")]
        [SerializeField] private Vector2 suspicionRadius = new Vector2(10f, 10f);
        [Tooltip("의심도 상승률 (초당, 중심 기준 최대값)")]
        [SerializeField] private float suspicionRate = 15f;
        [Tooltip("의심도 커브 지수. 높을수록 중심에 가까울수록 급격히 상승 (2=2차곡선, 3=3차곡선)")]
        [SerializeField, Range(1f, 5f)] private float suspicionCurveExponent = 2f;
        [Tooltip("추적 취소 의심도 기준. 이 값 이하로 떨어지면 매복 복귀")]
        [SerializeField] private float suspicionDropThreshold = 20f;

        [Header("돌진 설정")]
        [Tooltip("기습 돌진 속도")]
        [SerializeField] private float dashSpeed = 8f;
        [Tooltip("돌진 지속 시간 (초). 1회만 돌진 후 일반 추적으로 전환")]
        [SerializeField] private float dashDuration = 1.5f;
        [Tooltip("Chase 진입 후 돌진 전 대기 시간 (초). 이동 정지 상태")]
        [SerializeField] private float dashPreDelay = 0.3f;

        [Header("돌진 쿨타임")]
        [Tooltip("돌진 후 다음 돌진까지 대기 시간 (초)")]
        [SerializeField] private float dashCooldown = 2f;

        // 상태
        private Transform _bossTransform;
        private Transform _playerTransform;
        private HideAndInk.Player.PlayerMovementAdapter _playerMovementAdapter; // Player 방향 (Sprite 기준)
        private bool _isAmbushing;
        private bool _hasDashed; // 돌진 1회 체크
        private bool _isDashing; // 돌진 중
        private bool _isDashPreDelay; // 돌진 전 대기 중 (이동 정지)
        private bool _isDashCooldown; // 돌진 후 쿨타임 중
        private float _dashTimer;
        private float _preDelayTimer;
        private float _cooldownTimer; // 쿨타임 타이머
        private Vector3 _dashTarget;
        private SpriteRenderer _spriteRenderer;
        private Sprite _originalSprite;
        private GroundBounds _groundBounds; // Ground Bounds 캐싱
        private bool _hasGroundBounds; // GroundBounds 설정 여부 (struct이므로 null 체크 불가)

        // 매복 위치 이동 상태
        private Vector3 _ambushPoint;
        private bool _isAmbushPointSet;
        private bool _isMovingToAmbush;
        private bool _isStoppedAtAmbush;
        private float _ambushTimer;

        // 외부 연동 콜백
        public System.Action<float> OnSpeedOverride;
        public System.Action OnMovementStop;
        public System.Action OnMovementResume;
        public System.Action<bool> OnVisibilityToggle;
        public System.Action<Vector3> OnDashCompleted;
        public System.Action<float, float> OnSuspicionIncrease; // (rate, deltaTime)
        public System.Action<Vector3> OnRelocateAmbush; // 새 매복 위치 요청
        public System.Action<bool> OnDashModeToggle; // 돌진 모드 ON/OFF
        public System.Action<bool> OnPatrolBehaviorOverride; // PatrolBehavior 이동 제어권 토글
        public System.Action<Vector3> OnDashMoveTo; // 돌진 이동 요청 (목표 위치)
        public System.Action<float> OnDashAnimationTrigger; // 돌진 애니메이션 재생 요청 (지속시간 전달)
        public System.Action OnDashAnimationEnd; // 돌진 애니메이션 종료 요청

        public void OnActivate(Transform bossTransform)
        {
            _bossTransform = bossTransform;
            _isAmbushing = false;
            _hasDashed = false;
            _isDashing = false;

            // Player Transform 캐싱
            CachePlayerTransform();

            // Player Movement Adapter 캐싱 (Sprite 방향용)
            CachePlayerMovementAdapter();

            // SpriteRenderer 캐싱
            CacheSpriteRenderer();
        }

        public void OnDeactivate()
        {
            _isAmbushing = false;
            _isDashing = false;
            _isDashPreDelay = false;
            _isDashCooldown = false;

            OnMovementResume?.Invoke();
            OnVisibilityToggle?.Invoke(false); // 일반 시야 모드 복귀
            OnPatrolBehaviorOverride?.Invoke(false);
            OnDashModeToggle?.Invoke(false);

            // 이벤트 콜백 구독 해제 (메모리 누수 방지)
            ClearCallbacks();
        }

        #region Patrol

        public void OnPatrolEnter()
        {
            _isAmbushing = true;
            _hasDashed = false;
            _isDashing = false;
            _isDashPreDelay = false;
            _isDashCooldown = false;
            _isAmbushPointSet = false;
            _isMovingToAmbush = false;
            _isStoppedAtAmbush = false;

            // PatrolBehavior 이동 제어권 넘김
            OnPatrolBehaviorOverride?.Invoke(true);

            // 매복 스프라이트로 변경
            ApplyAmbushSprite(true);

            // 추적 속도 적용
            OnSpeedOverride?.Invoke(patrolTrackSpeed);
        }

        public void OnPatrolUpdate(float deltaTime)
        {
            if (!_isAmbushing)
            {
#if UNITY_EDITOR
                Debug.LogWarning("[AmbushGimmick] OnPatrolUpdate: _isAmbushing is FALSE");
#endif
                return;
            }

            // Boss Transform null 체크
            if (_bossTransform == null) return;

            // Player 캐싱 재시도
            if (_playerTransform == null) CachePlayerTransform();
            if (_playerTransform == null) return;

            // PlayerMovementAdapter 캐싱 재시도
            if (_playerMovementAdapter == null) CachePlayerMovementAdapter();

            // 화면 밖 체크 및 순간이동
            CheckAndTeleportIfOffScreen();
        }

        public void OnPatrolExit()
        {
            _isAmbushing = false;
            _isMovingToAmbush = false;
            _isStoppedAtAmbush = false;

            OnMovementResume?.Invoke();
            OnPatrolBehaviorOverride?.Invoke(false); // PatrolBehavior 제어권 반환
        }

        #endregion

        #region Chase

        public void OnChaseEnter()
        {
            _isAmbushing = false;
            _isMovingToAmbush = false;
            _isStoppedAtAmbush = false;
            OnVisibilityToggle?.Invoke(false); // 일반 시야 모드 복귀

            // 원래 스프라이트로 복원
            ApplyAmbushSprite(false);

            // ChaseBehavior 이동 제어 중단 (돌진만 이동)
            OnDashModeToggle?.Invoke(true);

            // 돌진 시작
            StartDashPreDelay();
        }

        public void OnChaseUpdate(float deltaTime)
        {
            // 돌진 전 대기 중
            if (_isDashPreDelay)
            {
                _preDelayTimer -= deltaTime;
                if (_preDelayTimer <= 0f)
                {
                    // 대기 종료 → 돌진 시작
                    StartDash();
                }
                return;
            }

            // 돌진 중
            if (_isDashing)
            {
                _dashTimer -= deltaTime;
                if (_dashTimer <= 0f)
                {
                    EndDash();
                }
                return;
            }

            // 쿨타임 중 (매복 애니메이션 대기)
            if (_isDashCooldown)
            {
                _cooldownTimer -= deltaTime;
                if (_cooldownTimer <= 0f)
                {
                    // 쿨타임 종료 → 다시 돌진 시작
                    _isDashCooldown = false;
                    StartDashPreDelay();
                }
                return;
            }
        }

        public void OnChaseExit()
        {
            _isDashing = false;
            _isDashPreDelay = false;
            _isDashCooldown = false;
            _isAmbushing = false;
            _isMovingToAmbush = false;
            _isStoppedAtAmbush = false;
            OnMovementResume?.Invoke();
            OnDashModeToggle?.Invoke(false); // ChaseBehavior 이동 제어권 반환
        }

        #endregion

        #region Search

        public void OnSearchEnter()
        {
            _isAmbushing = false;
            _isMovingToAmbush = false;
            _isStoppedAtAmbush = false;
            _ambushTimer = ambushDuration; // 타이머 초기화 (Patrol 복귀 시 정상 동작 보장)
            OnMovementResume?.Invoke();
        }

        public void OnSearchUpdate(float deltaTime)
        {
            // Search 상태에서는 의심도 자연 하락만 허용 (BossEnemyController가 시야 발견 시 상승 처리)
        }

        public void OnSearchExit()
        {
            _isAmbushing = false;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Player Transform 캐싱
        /// </summary>
        private void CachePlayerTransform()
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                _playerTransform = playerObj.transform;
            }
        }

        /// <summary>
        /// PlayerMovementAdapter 캐싱 (Sprite 방향 확인용)
        /// </summary>
        private void CachePlayerMovementAdapter()
        {
            if (_playerTransform != null)
            {
                _playerMovementAdapter = _playerTransform.GetComponent<HideAndInk.Player.PlayerMovementAdapter>();
            }

            if (_playerMovementAdapter == null)
            {
                _playerMovementAdapter = FindObjectOfType<HideAndInk.Player.PlayerMovementAdapter>();
            }
        }

        /// <summary>
        /// MoveDirection을 Vector3로 변환 (Sprite가 바라보는 방향 - 좌우만)
        /// </summary>
        private Vector3 GetPlayerViewDirection()
        {
            if (_playerMovementAdapter == null)
            {
                // fallback: Player Transform forward 사용
                return _playerTransform != null ? _playerTransform.forward : Vector3.forward;
            }

            switch (_playerMovementAdapter.Direction)
            {
                case HideAndInk.Core.Interfaces.MoveDirection.Right:
                    return Vector3.right;
                case HideAndInk.Core.Interfaces.MoveDirection.Left:
                    return Vector3.left;
                default:
                    // Up/Down은 마지막 방향 유지 (변경 없음)
                    return Vector3.right; // 기본값: 오른쪽
            }
        }

        /// <summary>
        /// SpriteRenderer 캐싱
        /// </summary>
        private void CacheSpriteRenderer()
        {
            if (targetSpriteRenderer != null)
            {
                _spriteRenderer = targetSpriteRenderer;
            }
            else if (_bossTransform != null)
            {
                _spriteRenderer = _bossTransform.GetComponentInChildren<SpriteRenderer>();
            }

            if (_spriteRenderer != null)
            {
                _originalSprite = _spriteRenderer.sprite;
            }
        }

        /// <summary>
        /// 매복 스프라이트 적용/해제
        /// </summary>
        private void ApplyAmbushSprite(bool isAmbushing)
        {
            if (_spriteRenderer == null) return;

            if (isAmbushing && ambushSprite != null)
            {
                _spriteRenderer.sprite = ambushSprite;
            }
            else if (_originalSprite != null)
            {
                _spriteRenderer.sprite = _originalSprite;
            }
        }

        /// <summary>
        /// 화면 밖 체크 및 순간이동
        /// Player가 Ambush를 보지 않으면 Player 카메라 뒤쪽으로 순간이동
        /// </summary>
        private void CheckAndTeleportIfOffScreen()
        {
            if (_playerTransform == null) return;
            if (_bossTransform == null) return;

            // Player 카메라 가져오기
            Camera playerCamera = Camera.main;
            if (playerCamera == null) return;

            // Boss가 Player 카메라 시야 내에 있는지 체크
            Vector3 screenPos = playerCamera.WorldToViewportPoint(_bossTransform.position);
            bool isInScreen = screenPos.z > 0 && screenPos.x >= 0 && screenPos.x <= 1 && screenPos.y >= 0 && screenPos.y <= 1;

            // Player가 보고 있지 않으면 순간이동
            if (!isInScreen)
            {
                // Player 시야각 체크 (선택적) - Sprite 좌우 방향 기준
                if (checkPlayerVisionForTeleport)
                {
                    Vector3 playerViewDir = GetPlayerViewDirection();
                    Vector3 toBoss = (_bossTransform.position - _playerTransform.position).normalized;
                    float dot = Vector3.Dot(playerViewDir, toBoss);
                    // Player가 보고 있는 방향(좌우)이면 순간이동 안 함
                    if (dot > 0.5f) return; // 약 60도 이내
                }

                // Player 카메라 뒤쪽으로 순간이동 위치 계산
                Vector3 teleportPos = CalculateTeleportPosition(playerCamera);
                if (teleportPos != Vector3.zero)
                {
                    _bossTransform.position = teleportPos;
#if UNITY_EDITOR
                    Debug.Log($"[AmbushGimmick] 순간이동: Player 시야 밖 → {teleportPos}");
#endif
                }
            }
        }

        /// <summary>
        /// 순간이동 위치 계산 (Player Sprite가 바라보는 방향 기준 X축 앞쪽)
        /// </summary>
        private Vector3 CalculateTeleportPosition(Camera playerCamera)
        {
            // Player Sprite가 바라보는 방향 (좌우만)
            Vector3 playerViewDir = GetPlayerViewDirection();

            // Player 위치에서 Sprite가 바라보는 방향 앞쪽으로 순간이동 (X축만)
            float teleportX = _playerTransform.position.x + playerViewDir.x * teleportBehindDistance;

            // X-Z 평면으로 보정 (Y축은 현재 높이 유지, Z축은 Player 위치 유지)
            Vector3 teleportPos = new Vector3(
                teleportX,
                _bossTransform.position.y,
                _playerTransform.position.z
            );

            // Ground Bounds 내에서 위치 보정
            if (_hasGroundBounds && (_groundBounds.MinX != _groundBounds.MaxX || _groundBounds.MinZ != _groundBounds.MaxZ))
            {
                teleportPos = _groundBounds.ClampXZ(teleportPos);
            }

            return teleportPos;
        }

        /// <summary>
        /// 돌진 전 대기 시작 (ChaseBehavior 일시정지 + 이동 정지)
        /// </summary>
        private void StartDashPreDelay()
        {
            _hasDashed = true;
            _isDashPreDelay = true;
            _isDashing = false;
            _preDelayTimer = dashPreDelay;

            // ChaseBehavior 일시정지 (이동 제어 중단)
            OnDashModeToggle?.Invoke(true);

            // 이동 정지
            OnMovementStop?.Invoke();

#if UNITY_EDITOR
            Debug.Log($"[AmbushGimmick] 돌진 전 대기 시작 ({dashPreDelay:F1}초)");
#endif
        }

        /// <summary>
        /// 기습 돌진 시작 (1회)
        /// </summary>
        private void StartDash()
        {
            _isDashPreDelay = false;
            _isDashing = true;
            _dashTimer = dashDuration;

            // 돌진 방향: 매복 위치 → Player
            if (_playerTransform != null)
            {
                _dashTarget = _playerTransform.position;
            }
            else if (_bossTransform != null)
            {
                // Player 없으면 현재 위치를 목표로 (fallback)
                _dashTarget = _bossTransform.position;
#if UNITY_EDITOR
                Debug.LogWarning("[AmbushGimmick] StartDash: Player Transform is NULL, using current position as fallback.");
#endif
            }

            // 돌진 이동 요청 (ChaseBehavior 이동 중단 후 직접 제어)
            OnDashMoveTo?.Invoke(_dashTarget);
            OnSpeedOverride?.Invoke(dashSpeed);
            OnVisibilityToggle?.Invoke(false); // 일반 시야 모드 복귀

            // 애니메이션 트리거 (지속시간 전달)
            OnDashAnimationTrigger?.Invoke(dashDuration);

#if UNITY_EDITOR
            Debug.Log($"[AmbushGimmick] 기습 돌진 시작! 목표: {_dashTarget}");
#endif
        }

        /// <summary>
        /// 돌진 종료 → 정지 → 매복 대기 → 쿨타임 후 재돌진
        /// </summary>
        private void EndDash()
        {
            _isDashing = false;
            OnMovementStop?.Invoke(); // 정지
            ApplyAmbushSprite(true); // 매복 스프라이트로 변경

            // 쿨타임 시작
            _isDashCooldown = true;
            _cooldownTimer = dashCooldown;

            // 애니메이션 종료 알림
            OnDashAnimationEnd?.Invoke();

#if UNITY_EDITOR
            Debug.Log($"[AmbushGimmick] 돌진 종료 → 매복 대기 → 쿨타임 ({dashCooldown:F1}초)");
#endif
        }

        /// <summary>
        /// 이벤트 콜백 구독 해제 (메모리 누수 방지)
        /// </summary>
        private void ClearCallbacks()
        {
            OnSpeedOverride = null;
            OnMovementStop = null;
            OnMovementResume = null;
            OnVisibilityToggle = null;
            OnDashCompleted = null;
            OnSuspicionIncrease = null;
            OnRelocateAmbush = null;
            OnDashModeToggle = null;
            OnPatrolBehaviorOverride = null;
            OnDashMoveTo = null;
        }

        #endregion

        #region Public Getters

        public bool IsDashing => _isDashing;
        public bool IsAmbushing => _isAmbushing;
        public bool HasDashed => _hasDashed;
        public bool IsDashCooldown => _isDashCooldown;
        public Vector3 AmbushPoint => _ambushPoint;
        public float SuspicionDropThreshold => suspicionDropThreshold;
        public Vector2 SuspicionRadius => suspicionRadius;
        public float SuspicionRate => suspicionRate;
        public float SuspicionCurveExponent => suspicionCurveExponent;
        public bool LockZAxis => lockZAxis;

        /// <summary>
        /// Ground Bounds 설정 (BossEnemyController에서 호출)
        /// </summary>
        public void SetGroundBounds(GroundBounds bounds)
        {
            _groundBounds = bounds;
            _hasGroundBounds = true;
        }

        #endregion

        #region Movement Override (IEnemyGimmick 확장)

        /// <summary>
        /// AmbushGimmick은 항상 이동 제어권을 가짐 (매복 위치 기반 이동)
        /// </summary>
        public bool HasMovementOverride => true;

        /// <summary>
        /// Patrol 상태 이동 목표: Player 위치 지속 추적
        /// Z축은 lockZAxis 설정에 따라 고정 또는 미세 이동
        /// </summary>
        public Vector3? GetPatrolTarget(Vector3 currentPos, GroundBounds bounds)
        {
            if (_playerTransform == null) return currentPos;

            Vector3 playerPos = _playerTransform.position;
            Vector3 target;

            if (lockZAxis)
            {
                // X축만 Player 방향으로 이동 (Z축 고정)
                target = new Vector3(playerPos.x, currentPos.y, currentPos.z);
            }
            else
            {
                // X-Z 평면으로 Player 방향 추적
                target = new Vector3(playerPos.x, currentPos.y, playerPos.z);
            }

            // Ground 범위 내로 제한
            if (_hasGroundBounds && (bounds.MinX != bounds.MaxX || bounds.MinZ != bounds.MaxZ))
            {
                target = bounds.ClampXZ(target);
            }

            return target;
        }

        /// <summary>
        /// Search 상태 이동 목표: 마지막 Player 위치 주변 수색
        /// Z축은 미세 이동만 허용 (±1m)
        /// </summary>
        public Vector3? GetSearchTarget(Vector3 currentPos, Vector3 lastKnownPos, GroundBounds bounds)
        {
            // 마지막 Player 위치 기준 랜덤 수색
            float searchRadius = 3f;
            float angle = Random.Range(0f, 360f);
            float distance = Random.Range(1f, searchRadius);

            Vector3 target;
            if (lockZAxis)
            {
                // Z축 고정, X축만 수색
                float xDir = Mathf.Cos(angle * Mathf.Deg2Rad);
                target = new Vector3(
                    lastKnownPos.x + xDir * distance,
                    currentPos.y,
                    currentPos.z
                );
            }
            else
            {
                // X-Z 평면 수색 (Z축 미세 이동 ±1m 제한)
                float xDir = Mathf.Cos(angle * Mathf.Deg2Rad);
                float zDir = Mathf.Sin(angle * Mathf.Deg2Rad);
                float zOffset = Mathf.Clamp(zDir * distance, -1f, 1f);
                target = new Vector3(
                    lastKnownPos.x + xDir * distance,
                    currentPos.y,
                    currentPos.z + zOffset
                );
            }

            // Ground 범위 내로 제한
            if (_hasGroundBounds && (bounds.MinX != bounds.MaxX || bounds.MinZ != bounds.MaxZ))
            {
                target = bounds.ClampXZ(target);
            }

            return target;
        }

        #endregion
    }
}
