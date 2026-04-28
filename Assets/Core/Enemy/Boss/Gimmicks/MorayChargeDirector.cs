using System;
using System.Collections.Generic;
using UnityEngine;
using HideAndInk.Core.Enemy.Interfaces;

namespace HideAndInk.Core.Enemy.Boss.Gimmicks
{
    /// <summary>
    /// 곰치 돌진 전담 디렉터 (MonoBehaviour)
    /// - 네모 생성/소멸
    /// - 돌진 경로 계산 (Player 이동 예측 포함)
    /// - 곰치 위치 이동 명령 (이벤트 발행)
    /// 
    /// RelentlessChaseGimmick에서 호출되어 실제 돌진 로직 수행
    /// </summary>
    public class MorayChargeDirector : MonoBehaviour
    {
        [Header("네모 설정")]
        [SerializeField] private MorayChargeIndicator indicator;
        [SerializeField] private float indicatorWidth = 2.5f;
        [SerializeField] private Color activeColor = new Color(1f, 0.2f, 0.2f, 0.6f);
        [SerializeField] private Color imminentColor = new Color(1f, 0f, 0f, 0.9f);

        [Header("돌진 설정")]
        [SerializeField] private float chargeSpeed = 18f;
        [SerializeField] private float minSquareInterval = 0.3f;
        [SerializeField] private float chargeDelay = 0.2f; // 돌진 사이 텀
        [SerializeField] private float playerPredictionTime = 0.4f; // Player 예측 시간

        [Header("화면 여유")]
        [SerializeField] private float screenEdgeOffset = 2f;

        // ──────────────────────────────────────────────
        // 이벤트 (BossEnemyController가 구독)
        // ──────────────────────────────────────────────

        /// <summary>돌진 실행: start 위치로 순간이동 → end 위치로 돌진</summary>
        public event Action<Vector3, Vector3> OnChargeExecute;
        /// <summary>모든 돌진 완료</summary>
        public event Action OnChargesComplete;
        /// <summary>돌진 속도 설정</summary>
        public event Action<float> OnSpeedOverride;
        /// <summary>이동 정지</summary>
        public event Action OnMovementStop;
        /// <summary>Prepare 시작 시 화면 밴 진입점으로 순간이동</summary>
        public event Action<Vector3> OnPrepareTeleport;

        // ──────────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────────

        private Transform _bossTransform;
        private Transform _playerTransform;
        private GroundBounds _groundBounds;
        private bool _hasGroundBounds;
        private Vector3 _playerVelocity;
        private HideAndInk.Player.PlayerMovementAdapter _cachedMovement;

        private int _currentChargeIndex;
        private int _totalCharges;
        private float _stateTimer;
        private bool _isPreparing;
        private bool _isCharging;
        private bool _isTransitioning;

        // 선계산된 돌진 경로
        private Vector3[] _chargeStarts;
        private Vector3[] _chargeEnds;
        private int _lastDirection; // 0=좌→우, 1=우→좌

        // Charge별 충돌 플래그 (각 charge에서 한 번만 hit)
        private bool[] _chargeHitFlags;

        // ──────────────────────────────────────────────
        // MonoBehaviour
        // ──────────────────────────────────────────────

        private void Awake()
        {
            // Player 찾기 + MovementAdapter 캐싱
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                _playerTransform = playerObj.transform;
                _cachedMovement = playerObj.GetComponent<HideAndInk.Player.PlayerMovementAdapter>();
            }
        }

        private void Update()
        {
            // Player 속도 캐싱 (매 프레임 갱신, 캐시된 참조 사용)
            if (_cachedMovement != null)
            {
                Vector2 vel2D = _cachedMovement.CurrentVelocity;
                _playerVelocity = new Vector3(vel2D.x, 0f, vel2D.y);
            }

            // 상태 업데이트
            if (_isPreparing)
                UpdatePreparing();
            else if (_isCharging)
                UpdateCharging();
            else if (_isTransitioning)
                UpdateTransition();
        }

        // ──────────────────────────────────────────────
        // Public API (RelentlessChaseGimmick에서 호출)
        // ──────────────────────────────────────────────

        /// <summary>
        /// 초기화 (Boss 시작 시)
        /// </summary>
        public void Initialize(Transform bossTransform, GroundBounds bounds)
        {
            _bossTransform = bossTransform;
            _groundBounds = bounds;
            _hasGroundBounds = true;
            _currentChargeIndex = 0;
            _totalCharges = 0;
            _isPreparing = false;
            _isCharging = false;
            _isTransitioning = false;
            _lastDirection = -1;

            if (indicator != null)
                indicator.ClearAll();
        }

        /// <summary>
        /// Chase 준비 단계 시작 (네모 생성)
        /// </summary>
        public void BeginPrepare(int chargeCount)
        {
            _totalCharges = chargeCount;
            _currentChargeIndex = 0;
            _isPreparing = true;
            _isCharging = false;
            _isTransitioning = false;
            _stateTimer = 0f;

            // 이전 인디케이터 정리
            if (indicator != null)
                indicator.ClearAll();

            // 경로 선계산
            _chargeStarts = new Vector3[chargeCount];
            _chargeEnds = new Vector3[chargeCount];
            _chargeHitFlags = new bool[chargeCount];

            for (int i = 0; i < chargeCount; i++)
            {
                CalculateChargePath(i);
            }

            // Prepare 시작 → 화면 밖 진입점으로 순간이동
            if (_chargeStarts.Length > 0)
            {
                Vector3 preparePos = _chargeStarts[0];
                if (_bossTransform != null)
                    preparePos.y = _bossTransform.position.y;
                OnPrepareTeleport?.Invoke(preparePos);
            }

            // Indicator에 설정값 적용 (중복 방지)
            ApplyIndicatorSettings();
        }

        private void ApplyIndicatorSettings()
        {
            if (indicator == null) return;
            indicator.Width = indicatorWidth;
            indicator.ActiveColor = activeColor;
            indicator.ImminentColor = imminentColor;
        }

        /// <summary>리셋 (Chase 종료 시)</summary>
        public void ResetCharges()
        {
            _isPreparing = false;
            _isCharging = false;
            _isTransitioning = false;
            if (indicator != null)
                indicator.ClearAll();
        }

        /// <summary>
        /// 강제 중단 (Player 이탈/사망 등 예외 상황)
        /// 상태 리셋 + 이벤트 발행 → Patrol 복귀 트리거
        /// </summary>
        public void ForceInterrupt()
        {
            _isPreparing = false;
            _isCharging = false;
            _isTransitioning = false;
            _stateTimer = 0f;

            if (indicator != null)
                indicator.ClearAll();

            OnMovementStop?.Invoke();
            OnChargesComplete?.Invoke();
        }

        /// <summary>GroundBounds 재설정</summary>
        public void SetGroundBounds(GroundBounds bounds)
        {
            _groundBounds = bounds;
            _hasGroundBounds = true;
        }

        // ──────────────────────────────────────────────
        // 내부 상태 업데이트
        // ──────────────────────────────────────────────

        private void UpdatePreparing()
        {
            _stateTimer += Time.deltaTime;
            float interval = minSquareInterval;

            // 순차적으로 네모 생성
            int nextIdx = _currentChargeIndex;
            while (nextIdx < _totalCharges && _stateTimer >= nextIdx * interval)
            {
                if (indicator != null)
                    indicator.SpawnIndicator(_chargeStarts[nextIdx], _chargeEnds[nextIdx]);
                nextIdx++;
            }
            _currentChargeIndex = nextIdx - 1;

            // 모든 네모 생성 완료 → 첫 돌진 실행
            if (_currentChargeIndex >= _totalCharges - 1)
            {
                _currentChargeIndex = 0;
                ExecuteCharge(0);
            }
        }

        private void UpdateCharging()
        {
            _stateTimer -= Time.deltaTime;

            // 충돌 체크 (Player가 돌진 경로 내에 있는지)
            CheckChargeHit();

            // 돌진 종료 체크
            bool reachedTarget = false;
            if (_bossTransform != null && _currentChargeIndex < _totalCharges)
            {
                float dist = Vector3.Distance(
                    new Vector3(_bossTransform.position.x, 0f, _bossTransform.position.z),
                    new Vector3(_chargeEnds[_currentChargeIndex].x, 0f, _chargeEnds[_currentChargeIndex].z));
                reachedTarget = dist < 1f;
            }

            if (_stateTimer <= 0f || reachedTarget)
            {
                // 현재 돌진 완료
                if (indicator != null)
                    indicator.DespawnIndicator(_currentChargeIndex);

                int nextIdx = _currentChargeIndex + 1;
                if (nextIdx >= _totalCharges)
                {
                    // 모든 돌진 완료
                    _isCharging = false;
                    OnMovementStop?.Invoke();
                    OnChargesComplete?.Invoke();
                }
                else
                {
                    // 다음 돌진 준비
                    _isCharging = false;
                    _isTransitioning = true;
                    _stateTimer = chargeDelay;
                }
            }
        }

        private void UpdateTransition()
        {
            _stateTimer -= Time.deltaTime;
            if (_stateTimer <= 0f)
            {
                _isTransitioning = false;
                ExecuteCharge(_currentChargeIndex + 1);
            }
        }

        // ──────────────────────────────────────────────
        // 돌진 실행
        // ──────────────────────────────────────────────

        private void ExecuteCharge(int index)
        {
            _currentChargeIndex = index;
            _isCharging = true;

            // 임박 색상
            if (indicator != null)
                indicator.SetImminent(index);

            // 이벤트 발행: 곰치 이동 명령
            Vector3 start = _chargeStarts[index];
            Vector3 end = _chargeEnds[index];
            start.y = _bossTransform != null ? _bossTransform.position.y : start.y;
            end.y = start.y;

            OnChargeExecute?.Invoke(start, end);
            OnSpeedOverride?.Invoke(chargeSpeed);

            // 돌진 지속 시간 동적 계산 (거리 기반)
            float distance = Vector3.Distance(start, end);
            _stateTimer = Mathf.Max(distance / chargeSpeed + 0.5f, 1.5f);

#if UNITY_EDITOR
            Debug.Log($"[MorayChargeDirector] Charge #{index}: Start=({start.x:F1},{start.z:F1}) End=({end.x:F1},{end.z:F1}) Dist={distance:F1} Timer={_stateTimer:F1}s");
#endif
        }

        // ──────────────────────────────────────────────
        // 경로 계산 (Player 예측 포함)
        // ──────────────────────────────────────────────

        private void CalculateChargePath(int index)
        {
            if (!_hasGroundBounds)
            {
                _chargeStarts[index] = new Vector3(-20f, 0f, 0f);
                _chargeEnds[index] = new Vector3(20f, 0f, 0f);
                return;
            }

            float groundY = _bossTransform != null ? _bossTransform.position.y : 0f;

            // 방향 교차
            int startDir = (_lastDirection == 0) ? 1 : 0;
            int endDir = (_lastDirection == 0) ? 0 : 1;
            _lastDirection = startDir;

            float left = _groundBounds.MinX - screenEdgeOffset;
            float right = _groundBounds.MaxX + screenEdgeOffset;

            // Player 예측 위치 기반 Z 결정
            float chargeZ = PredictChargeZ(index);

            _chargeStarts[index] = new Vector3(startDir == 0 ? left : right, groundY, chargeZ);
            _chargeEnds[index] = new Vector3(endDir == 0 ? left : right, groundY, chargeZ);
        }

        /// <summary>
        /// Player 이동 예측 기반 Z 위치 계산
        /// </summary>
        private float PredictChargeZ(int index)
        {
            if (!_hasGroundBounds) return 0f;

            float minZ = _groundBounds.MinZ;
            float maxZ = _groundBounds.MaxZ;
            float range = maxZ - minZ;

            if (range < 0.5f) return (minZ + maxZ) * 0.5f;

            // Player 현재 위치 + 예측
            Vector3 predictedPos = _playerTransform != null
                ? _playerTransform.position + _playerVelocity * playerPredictionTime
                : Vector3.zero;

            float predictedZ = predictedPos.z;

            // 여러 돌진일 경우 Z 분산
            if (_totalCharges > 1)
            {
                float t = (float)index / (_totalCharges - 1);
                float spreadZ = minZ + range * t;
                // 예측 위치와 분산 위치 혼합 (50:50)
                predictedZ = Mathf.Lerp(predictedZ, spreadZ, 0.5f);
            }

            // GroundBounds 내로 클램프
            return Mathf.Clamp(predictedZ, minZ, maxZ);
        }

        // ──────────────────────────────────────────────
        // 충돌 체크
        // ──────────────────────────────────────────────

        private void CheckChargeHit()
        {
            if (_bossTransform == null || _playerTransform == null) return;
            if (_currentChargeIndex >= _totalCharges) return;
            if (_chargeHitFlags == null || _currentChargeIndex >= _chargeHitFlags.Length) return;
            if (_chargeHitFlags[_currentChargeIndex]) return;

            Vector3 start = _chargeStarts[_currentChargeIndex];
            Vector3 end = _chargeEnds[_currentChargeIndex];

            Vector3 dir = (end - start).normalized;
            float distance = Vector3.Distance(start, end);
            Vector3 center = (start + end) * 0.5f;
            Vector3 halfExtents = new Vector3(indicatorWidth * 0.5f, 2f, distance * 0.5f);

            int playerLayer = 1 << LayerMask.NameToLayer("Player");
            if (playerLayer == 0) return;

            Collider[] hits = Physics.OverlapBox(center, halfExtents, Quaternion.LookRotation(dir), playerLayer);
            if (hits.Length > 0)
            {
                _chargeHitFlags[_currentChargeIndex] = true;
                OnPlayerHit?.Invoke();
            }
        }

        public event Action OnPlayerHit;
    }
}
