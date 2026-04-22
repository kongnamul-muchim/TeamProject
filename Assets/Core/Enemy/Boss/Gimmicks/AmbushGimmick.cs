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
        [Tooltip("매복 대기 시간 (초). 이 시간 동안 매복 유지 후 새 위치로 이동")]
        [SerializeField] private float ambushDuration = 3f;
        [Tooltip("매복 중 Player 기준 최소 이동 거리 (m)")]
        [SerializeField] private float ambushMoveRadiusMin = 5f;
        [Tooltip("매복 중 Player 기준 최대 이동 거리 (m)")]
        [SerializeField] private float ambushMoveRadiusMax = 12f;
        [Tooltip("매복 위치 재설정 시 Z축 고정 여부 (true = X축만 이동)")]
        [SerializeField] private bool lockZAxis = true;

        [Header("매복 스프라이트")]
        [Tooltip("매복 상태일 때 표시할 스프라이트 (땅에 숨은 모습)")]
        [SerializeField] private Sprite ambushSprite;
        [Tooltip("매복 상태일 때 적용할 SpriteRenderer (비워두면 자동 탐색)")]
        [SerializeField] private SpriteRenderer targetSpriteRenderer;

        [Header("의심도 설정")]
        [Tooltip("원거리 의심도 범위 (X, Z). 이 거리 내에서 서서히 의심도 상승")]
        [SerializeField] private Vector2 farSuspicionRadius = new Vector2(10f, 10f);
        [Tooltip("근접 의심도 범위 (X, Z). 이 거리 내에서 급격히 의심도 상승")]
        [SerializeField] private Vector2 nearSuspicionRadius = new Vector2(3f, 3f);
        [Tooltip("원거리 의심도 상승률 (초당)")]
        [SerializeField] private float farSuspicionRate = 5f;
        [Tooltip("근접 의심도 상승률 (초당)")]
        [SerializeField] private float nearSuspicionRate = 15f;
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
        private bool _isAmbushing;
        private float _ambushTimer;
        private bool _hasDashed; // 돌진 1회 체크
        private bool _isDashing;
        private float _dashTimer;
        private Vector3 _dashTarget;
        private SpriteRenderer _spriteRenderer;
        private Sprite _originalSprite;

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
            _ambushTimer = ambushDuration;

            // Player Transform 캐싱
            CachePlayerTransform();

            // SpriteRenderer 캐싱
            CacheSpriteRenderer();
        }

        public void OnDeactivate()
        {
            _isAmbushing = false;
            _isDashing = false;
            OnMovementResume?.Invoke();
            OnVisibilityToggle?.Invoke(false); // 일반 시야 모드 복귀
            OnPatrolBehaviorOverride?.Invoke(false);
        }

        #region Patrol

        public void OnPatrolEnter()
        {
            _isAmbushing = true;
            _hasDashed = false;
            _isDashing = false;
            _ambushTimer = ambushDuration;

            // PatrolBehavior 이동 제어권 넘김
            OnPatrolBehaviorOverride?.Invoke(true);

            // 매복 스프라이트로 변경
            ApplyAmbushSprite(true);

            // Player 근처 랜덤 위치로 이동
            RequestRelocateAmbush();
        }

        public void OnPatrolUpdate(float deltaTime)
        {
            if (!_isAmbushing) return;

            // Player 캐싱 재시도
            if (_playerTransform == null) CachePlayerTransform();

            // 매복 위치로 이동 중인지 체크 (도달 전까지 이동 계속)
            float distanceToAmbush = Vector3.Distance(_bossTransform.position, _ambushPoint);
            if (distanceToAmbush > 1f)
            {
                // 아직 매복 위치로 이동 중
                OnRelocateAmbush?.Invoke(_ambushPoint);
                return;
            }

            // 매복 위치 도착 → 이동 멈춤 + 거리 전용 모드
            OnMovementStop?.Invoke();
            OnVisibilityToggle?.Invoke(true); // 거리 전용 모드 ON

            // 매복 대기 타이머
            _ambushTimer -= deltaTime;

            // Player가 있으면 의심도 체크
            if (_playerTransform != null)
            {
                UpdateSuspicion(deltaTime);
            }

            // 대기시간 끝나면 새 위치로 재매복
            if (_ambushTimer <= 0f)
            {
                RequestRelocateAmbush();
                _ambushTimer = ambushDuration;
            }
        }

        public void OnPatrolExit()
        {
            _isAmbushing = false;
            OnMovementResume?.Invoke();
            // OnVisibilityToggle은 ChaseEnter에서 false로 설정하므로 여기선 제거
            OnPatrolBehaviorOverride?.Invoke(false); // PatrolBehavior 제어권 반환
        }

        #endregion

        #region Chase

        public void OnChaseEnter()
        {
            _isAmbushing = false;
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
                // 이미 돌진했으면 일반 추격
                OnMovementResume?.Invoke();
                OnSpeedOverride?.Invoke(0f); // 속도 리셋 (ChaseBehavior가 제어)
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
            OnMovementResume?.Invoke();
        }

        #endregion

        #region Search

        public void OnSearchEnter()
        {
            _isAmbushing = false;
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
        /// 의심도 업데이트 (직사각형 거리 기반 가중치 적용)
        /// 중심에 가까울수록 의심도 상승률 증가
        /// </summary>
        private void UpdateSuspicion(float deltaTime)
        {
            if (_playerTransform == null) return;

            Vector3 delta = _playerTransform.position - _bossTransform.position;
            float absX = Mathf.Abs(delta.x);
            float absZ = Mathf.Abs(delta.z);

            // 근접 범위 체크 (직사각형)
            if (absX <= nearSuspicionRadius.x && absZ <= nearSuspicionRadius.y)
            {
                // 거리 가중치: 중심 1.0 → 가장자리 0.3
                float xFactor = 1f - (absX / nearSuspicionRadius.x);
                float zFactor = 1f - (absZ / nearSuspicionRadius.y);
                float distanceFactor = Mathf.Min(xFactor, zFactor);
                float weightedRate = nearSuspicionRate * Mathf.Lerp(0.3f, 1f, distanceFactor);
                OnSuspicionIncrease?.Invoke(weightedRate, deltaTime);
            }
            // 원거리 범위 체크 (직사각형)
            else if (absX <= farSuspicionRadius.x && absZ <= farSuspicionRadius.y)
            {
                // 거리 가중치: 중심 1.0 → 가장자리 0.2
                float xFactor = 1f - (absX / farSuspicionRadius.x);
                float zFactor = 1f - (absZ / farSuspicionRadius.y);
                float distanceFactor = Mathf.Min(xFactor, zFactor);
                float weightedRate = farSuspicionRate * Mathf.Lerp(0.2f, 1f, distanceFactor);
                OnSuspicionIncrease?.Invoke(weightedRate, deltaTime);
            }
            // 범위 밖이면 의심도 상승 없음 (자연 하락에 맡김)
        }

        /// <summary>
        /// 매복 위치 재설정 요청 (Player 근처 랜덤 위치)
        /// </summary>
        private void RequestRelocateAmbush()
        {
            if (_playerTransform == null) return;

            Vector3 playerPos = _playerTransform.position;

            // Player 기준 랜덤 방향 + 랜덤 거리
            float angle = Random.Range(0f, 360f);
            float distance = Random.Range(ambushMoveRadiusMin, ambushMoveRadiusMax);

            Vector3 newAmbushPoint;
            if (lockZAxis)
            {
                // X축만 이동 (Z축 고정)
                float xDir = Mathf.Cos(angle * Mathf.Deg2Rad);
                newAmbushPoint = new Vector3(
                    playerPos.x + xDir * distance,
                    _bossTransform.position.y,
                    _bossTransform.position.z
                );
            }
            else
            {
                // X-Z 평면 이동
                float xDir = Mathf.Cos(angle * Mathf.Deg2Rad);
                float zDir = Mathf.Sin(angle * Mathf.Deg2Rad);
                newAmbushPoint = new Vector3(
                    playerPos.x + xDir * distance,
                    _bossTransform.position.y,
                    playerPos.z + zDir * distance
                );
            }

            OnRelocateAmbush?.Invoke(newAmbushPoint);
            _ambushPoint = newAmbushPoint;
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

        #endregion

        #region Public Getters

        public bool IsDashing => _isDashing;
        public bool IsAmbushing => _isAmbushing;
        public bool HasDashed => _hasDashed;
        public Vector3 AmbushPoint => _ambushPoint;
        public float SuspicionDropThreshold => suspicionDropThreshold;
        public Vector2 FarSuspicionRadius => farSuspicionRadius;
        public Vector2 NearSuspicionRadius => nearSuspicionRadius;

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
            if (_ambushPoint != Vector3.zero)
            {
                Vector3 target = _ambushPoint;

                // Ground 범위 내로 제한
                if (bounds.MinX != bounds.MaxX || bounds.MinZ != bounds.MaxZ)
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
            if (bounds.MinX != bounds.MaxX || bounds.MinZ != bounds.MaxZ)
            {
                target = bounds.ClampXZ(target);
            }

            return target;
        }

        #endregion
    }
}
