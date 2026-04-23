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
        [Tooltip("매복 대기 시간 (초). 이 시간 동안 매복 유지 후 Player 방향으로 이동")]
        [SerializeField] private float ambushDuration = 3f;
        [Tooltip("매복 중 Player 방향 접근 최소 거리 (m)")]
        [SerializeField] private float ambushMoveRadiusMin = 2f;
        [Tooltip("매복 중 Player 방향 접근 최대 거리 (m)")]
        [SerializeField] private float ambushMoveRadiusMax = 5f;
        [Tooltip("매복 위치 재설정 시 Z축 고정 여부 (true = X축만 접근)")]
        [SerializeField] private bool lockZAxis = true;

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

        // 상태
        private Transform _bossTransform;
        private Transform _playerTransform;
        private Vector3 _ambushPoint;
        private bool _isAmbushPointSet; // _ambushPoint 유효성 플래그 (Vector3.zero 비교 대체)
        private bool _isAmbushing;
        private float _ambushTimer;
        private bool _hasDashed; // 돌진 1회 체크
        private bool _isDashing;
        private float _dashTimer;
        private Vector3 _dashTarget;
        private SpriteRenderer _spriteRenderer;
        private Sprite _originalSprite;
        private GroundBounds _groundBounds; // Ground Bounds 캐싱
        private bool _hasGroundBounds; // GroundBounds 설정 여부 (struct이므로 null 체크 불가)

        // PatrolUpdate 상태 플래그 (매 프레임 콜백 최적화)
        private bool _isMovingToAmbush;
        private bool _isStoppedAtAmbush;

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

        public void OnActivate(Transform bossTransform)
        {
            _bossTransform = bossTransform;
            _isAmbushing = false;
            _hasDashed = false;
            _isDashing = false;
            _isAmbushPointSet = false;
            _ambushTimer = ambushDuration;
            _isMovingToAmbush = false;
            _isStoppedAtAmbush = false;

            // Player Transform 캐싱
            CachePlayerTransform();

            // SpriteRenderer 캐싱
            CacheSpriteRenderer();
        }

        public void OnDeactivate()
        {
            _isAmbushing = false;
            _isDashing = false;
            _isAmbushPointSet = false;
            _isMovingToAmbush = false;
            _isStoppedAtAmbush = false;

            OnMovementResume?.Invoke();
            OnVisibilityToggle?.Invoke(false); // 일반 시야 모드 복귀
            OnPatrolBehaviorOverride?.Invoke(false);

            // 이벤트 콜백 구독 해제 (메모리 누수 방지)
            ClearCallbacks();
        }

        #region Patrol

        public void OnPatrolEnter()
        {
            _isAmbushing = true;
            _hasDashed = false;
            _isDashing = false;
            _isAmbushPointSet = false;
            _ambushTimer = ambushDuration;
            _isMovingToAmbush = false;
            _isStoppedAtAmbush = false;

            // PatrolBehavior 이동 제어권 넘김
            OnPatrolBehaviorOverride?.Invoke(true);

            // 매복 스프라이트로 변경
            ApplyAmbushSprite(true);

            // Player 근처 랜덤 위치로 이동
            RequestRelocateAmbush();
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

            // 매복 위치가 설정되지 않았으면 대기
            if (!_isAmbushPointSet) return;

            // 매복 위치로 이동 중인지 체크 (도달 전까지 이동 계속)
            float distanceToAmbush = Vector3.Distance(_bossTransform.position, _ambushPoint);

            if (distanceToAmbush > 1f)
            {
                // 아직 매복 위치로 이동 중 (상태 변경 시에만 플래그 업데이트)
                // 이동은 PatrolBehavior.GetPatrolTarget()에서 처리됨
                if (!_isMovingToAmbush)
                {
                    _isMovingToAmbush = true;
                    _isStoppedAtAmbush = false;
                }
                return;
            }

            // 매복 위치 도착 → 상태 변경 시에만 콜백 호출
            if (!_isStoppedAtAmbush)
            {
                _isMovingToAmbush = false;
                _isStoppedAtAmbush = true;
                OnMovementStop?.Invoke();
                OnVisibilityToggle?.Invoke(true); // 거리 전용 모드 ON
            }

            // 매복 대기 타이머
            _ambushTimer -= deltaTime;

            // Player가 있으면 의심도 체크 (의심도 계산은 BossSuspicionSystem/AmbushSuspicionModule에서 전담)
            // UpdateSuspicion(deltaTime); // 중복 호출 방지: 모듈에서 통합 처리

            // 대기시간 끝나면 새 위치로 재매복
            if (_ambushTimer <= 0f)
            {
                RequestRelocateAmbush();
                _ambushTimer = ambushDuration;
                _isStoppedAtAmbush = false; // 새 위치로 이동하므로 상태 리셋
            }
        }

        public void OnPatrolExit()
        {
            _isAmbushing = false;
            _isMovingToAmbush = false;
            _isStoppedAtAmbush = false;
            _ambushTimer = ambushDuration; // 타이머 초기화 (복귀 시 정상 동작 보장)

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

            // 돌진 1회 체크
            if (!_hasDashed)
            {
                StartDash();
            }
            else
            {
                // 이미 돌진했으면 일반 추격 (속도 오버라이드 해제)
                OnMovementResume?.Invoke();
            }
        }

        public void OnChaseUpdate(float deltaTime)
        {
            if (!_isDashing) return;

            _dashTimer -= deltaTime;
            if (_dashTimer <= 0f)
            {
                EndDash();
            }
        }

        public void OnChaseExit()
        {
            _isDashing = false;
            _isAmbushing = false;
            _isMovingToAmbush = false;
            _isStoppedAtAmbush = false;
            OnMovementResume?.Invoke();
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
        /// 매복 위치 재설정 요청 (Player 방향으로 천천히 접근)
        /// </summary>
        private void RequestRelocateAmbush()
        {
            if (_playerTransform == null) return;
            if (_bossTransform == null) return;

            Vector3 playerPos = _playerTransform.position;
            Vector3 bossPos = _bossTransform.position;

            // Player 방향 벡터 계산
            Vector3 toPlayer = (playerPos - bossPos).normalized;

            // 접근 거리 (ambushMoveRadiusMin ~ Max)
            float approachDistance = Random.Range(ambushMoveRadiusMin, ambushMoveRadiusMax);

            Vector3 newAmbushPoint;
            if (lockZAxis)
            {
                // X축만 접근 (Z축 고정)
                float xDir = Mathf.Sign(toPlayer.x); // Player 방향 X 부호
                newAmbushPoint = new Vector3(
                    bossPos.x + xDir * approachDistance,
                    bossPos.y,
                    bossPos.z
                );
            }
            else
            {
                // X-Z 평면으로 Player 방향 접근
                newAmbushPoint = new Vector3(
                    bossPos.x + toPlayer.x * approachDistance,
                    bossPos.y,
                    bossPos.z + toPlayer.z * approachDistance
                );
            }

            // Ground Bounds 내에서 위치 보정
            if (_hasGroundBounds && (_groundBounds.MinX != _groundBounds.MaxX || _groundBounds.MinZ != _groundBounds.MaxZ))
            {
                newAmbushPoint = _groundBounds.ClampXZ(newAmbushPoint);
            }

            OnRelocateAmbush?.Invoke(newAmbushPoint);
            _ambushPoint = newAmbushPoint;
            _isAmbushPointSet = true;
        }

        /// <summary>
        /// 기습 돌진 시작 (1회)
        /// </summary>
        private void StartDash()
        {
            _hasDashed = true;
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

            OnDashModeToggle?.Invoke(true);
            OnSpeedOverride?.Invoke(dashSpeed);
            OnVisibilityToggle?.Invoke(false); // 일반 시야 모드 복귀

#if UNITY_EDITOR
            Debug.Log("[AmbushGimmick] 기습 돌진 시작!");
#endif
        }

        /// <summary>
        /// 돌진 종료 → 일반 추격으로 전환
        /// </summary>
        private void EndDash()
        {
            _isDashing = false;
            OnDashModeToggle?.Invoke(false);
            OnDashCompleted?.Invoke(_dashTarget);

#if UNITY_EDITOR
            Debug.Log("[AmbushGimmick] 기습 돌진 종료 → 일반 추격 전환");
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
        }

        #endregion

        #region Public Getters

        public bool IsDashing => _isDashing;
        public bool IsAmbushing => _isAmbushing;
        public bool HasDashed => _hasDashed;
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
        /// Patrol 상태 이동 목표: 매복 위치 (_ambushPoint) 반환
        /// Z축은 lockZAxis 설정에 따라 고정 또는 미세 이동
        /// </summary>
        public Vector3? GetPatrolTarget(Vector3 currentPos, GroundBounds bounds)
        {
            // 매복 위치가 설정되어 있으면 그곳으로 이동
            if (_isAmbushPointSet)
            {
                Vector3 target = _ambushPoint;

                // Ground 범위 내로 제한
                if (_hasGroundBounds && (bounds.MinX != bounds.MaxX || bounds.MinZ != bounds.MaxZ))
                {
                    target = bounds.ClampXZ(target);
                }

                // Z축 고정 (lockZAxis = true면 현재 Z 유지)
                if (lockZAxis)
                {
                    target.z = currentPos.z;
                }

                return target;
            }

            // 매복 위치 없으면 현재 위치 유지
            return currentPos;
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
