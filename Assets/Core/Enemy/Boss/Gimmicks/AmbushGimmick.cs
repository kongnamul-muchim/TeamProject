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

        [Header("의심도 설정")]
        [Tooltip("원거리 의심도 범위 (m). 이 거리 내에서 서서히 의심도 상승")]
        [SerializeField] private float farSuspicionRadius = 10f;
        [Tooltip("근접 의심도 범위 (m). 이 거리 내에서 급격히 의심도 상승")]
        [SerializeField] private float nearSuspicionRadius = 3f;
        [Tooltip("원거리 의심도 상승률 (초당)")]
        [SerializeField] private float farSuspicionRate = 10f;
        [Tooltip("근접 의심도 상승률 (초당)")]
        [SerializeField] private float nearSuspicionRate = 40f;
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

        // 외부 연동 콜백
        public System.Action<float> OnSpeedOverride;
        public System.Action OnMovementStop;
        public System.Action OnMovementResume;
        public System.Action<bool> OnVisibilityToggle;
        public System.Action<Vector3> OnDashCompleted;
        public System.Action<float, float> OnSuspicionIncrease; // (rate, deltaTime)
        public System.Action<Vector3> OnRelocateAmbush; // 새 매복 위치 요청
        public System.Action<bool> OnDashModeToggle; // 돌진 모드 ON/OFF

        public void OnActivate(Transform bossTransform)
        {
            _bossTransform = bossTransform;
            _isAmbushing = false;
            _hasDashed = false;
            _isDashing = false;
            _ambushTimer = ambushDuration;

            // Player Transform 캐싱
            CachePlayerTransform();
        }

        public void OnDeactivate()
        {
            _isAmbushing = false;
            _isDashing = false;
            OnMovementResume?.Invoke();
            OnVisibilityToggle?.Invoke(true);
        }

        #region Patrol

        public void OnPatrolEnter()
        {
            _isAmbushing = true;
            _hasDashed = false;
            _isDashing = false;
            _ambushTimer = ambushDuration;

            // Player 근처 랜덤 위치로 이동
            RequestRelocateAmbush();
        }

        public void OnPatrolUpdate(float deltaTime)
        {
            if (!_isAmbushing) return;

            // Player 캐싱 재시도
            if (_playerTransform == null) CachePlayerTransform();

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
            OnVisibilityToggle?.Invoke(true);
        }

        #endregion

        #region Chase

        public void OnChaseEnter()
        {
            _isAmbushing = false;
            OnVisibilityToggle?.Invoke(true);

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
            // Search 중에도 Player가 있으면 의심도 체크
            if (_playerTransform != null)
            {
                UpdateSuspicion(deltaTime);
            }
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
        /// 의심도 업데이트 (2단계: 원거리/근접)
        /// </summary>
        private void UpdateSuspicion(float deltaTime)
        {
            if (_playerTransform == null) return;

            float distanceToPlayer = Vector3.Distance(_bossTransform.position, _playerTransform.position);

            if (distanceToPlayer <= nearSuspicionRadius)
            {
                // 근접: 급격한 의심도 상승
                OnSuspicionIncrease?.Invoke(nearSuspicionRate, deltaTime);
            }
            else if (distanceToPlayer <= farSuspicionRadius)
            {
                // 원거리: 서서한 의심도 상승
                OnSuspicionIncrease?.Invoke(farSuspicionRate, deltaTime);
            }
            // farSuspicionRadius 밖이면 의심도 상승 없음 (자연 하락에 맡김)
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

            // 매복 시작: 이동 멈춤 + 시야 숨김
            OnMovementStop?.Invoke();
            OnVisibilityToggle?.Invoke(false);
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
            OnVisibilityToggle?.Invoke(true);

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
        public float FarSuspicionRadius => farSuspicionRadius;

        #endregion
    }
}
