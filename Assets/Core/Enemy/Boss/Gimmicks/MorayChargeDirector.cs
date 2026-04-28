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

        [Header("바닥 높이")]
        [SerializeField, Tooltip("인디케이터/돌진 Y 위치. Ground 표면 Y값을 직접 입력 (기본 0)")]
        private float indicatorFloorY = 0f;

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

        // 선계산된 돌진 경로
        private Vector3[] _chargeStarts;
        private Vector3[] _chargeEnds;
        private int _lastDirection; // 0=좌→우, 1=우→좌

        // Charge별 충돌 플래그 (각 charge에서 한 번만 hit)
        private bool[] _chargeHitFlags;

        // 시퀀스 코루틴 (상태머신 대체)
        private Coroutine _sequenceCoroutine;

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
            // ★ 상태 업데이트 제거: Prepare/Charge/Transition은 코루틴이 직접 관리
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
            _lastDirection = -1;

            if (_sequenceCoroutine != null)
            {
                StopCoroutine(_sequenceCoroutine);
                _sequenceCoroutine = null;
            }

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

            // 이전 시퀀스 정리
            if (_sequenceCoroutine != null)
            {
                StopCoroutine(_sequenceCoroutine);
                _sequenceCoroutine = null;
            }

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

            // Prepare 시작 → 화면 밖 진입점으로 순간이동 (boss Y 유지)
            if (_chargeStarts.Length > 0)
            {
                Vector3 preparePos = _chargeStarts[0];
                if (_bossTransform != null)
                    preparePos.y = _bossTransform.position.y;
                OnPrepareTeleport?.Invoke(preparePos);
            }

            // Indicator에 설정값 적용 (중복 방지)
            ApplyIndicatorSettings();

            // 시퀀스 시작 (코루틴)
            _sequenceCoroutine = StartCoroutine(ChargeSequenceCoroutine());
        }

        private void ApplyIndicatorSettings()
        {
            if (indicator == null) return;
            indicator.Width = indicatorWidth;
            indicator.ActiveColor = activeColor;
            indicator.ImminentColor = imminentColor;
        }

        /// <summary>Prepare 단계의 첫 Charge 방향 (시야각 동기화용)</summary>
        public Vector3 GetPrepareDirection()
        {
            if (_chargeStarts == null || _chargeStarts.Length == 0 || _chargeEnds == null || _chargeEnds.Length == 0)
                return Vector3.right;
            Vector3 dir = _chargeEnds[0] - _chargeStarts[0];
            dir.y = 0f;
            return dir.normalized;
        }

        /// <summary>리셋 (Chase 종료 시)</summary>
        public void ResetCharges()
        {
            if (_sequenceCoroutine != null)
            {
                StopCoroutine(_sequenceCoroutine);
                _sequenceCoroutine = null;
            }
            if (indicator != null)
                indicator.ClearAll();
        }

        /// <summary>
        /// 강제 중단 (Player 이탈/사망 등 예외 상황)
        /// 코루틴 정지 + 이벤트 발행 → Patrol 복귀 트리거
        /// </summary>
        public void ForceInterrupt()
        {
            if (_sequenceCoroutine != null)
            {
                StopCoroutine(_sequenceCoroutine);
                _sequenceCoroutine = null;
            }

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
        // 시퀀스 코루틴 (Prepare → Charge[N] → Complete)
        // ──────────────────────────────────────────────

        private System.Collections.IEnumerator ChargeSequenceCoroutine()
        {
            // === Prepare Phase: 네모 순차 생성 ===
            for (int i = 0; i < _totalCharges; i++)
            {
                if (indicator != null)
                    indicator.SpawnIndicator(_chargeStarts[i], _chargeEnds[i]);
                yield return new WaitForSeconds(minSquareInterval);
            }

            // === Charge Phase: N회 돌진 ===
            for (int i = 0; i < _totalCharges; i++)
            {
                yield return StartCoroutine(ExecuteSingleChargeCoroutine(i));

                // 돌진 사이 텀 (마지막이 아니면)
                if (i < _totalCharges - 1)
                    yield return new WaitForSeconds(chargeDelay);
            }

            // === Complete ===
            if (indicator != null)
                indicator.ClearAll();
            OnMovementStop?.Invoke();
            OnChargesComplete?.Invoke();

            _sequenceCoroutine = null;
        }

        private System.Collections.IEnumerator ExecuteSingleChargeCoroutine(int index)
        {
            _currentChargeIndex = index;

            // 임박 색상
            if (indicator != null)
                indicator.SetImminent(index);

            // 이벤트 발행: 곰치 이동 명령
            Vector3 start = _chargeStarts[index];
            Vector3 end = _chargeEnds[index];
            // ★ boss는 자기 Y 유지, 네모만 indicatorFloorY 사용
            start.y = _bossTransform != null ? _bossTransform.position.y : start.y;
            end.y = start.y;

            OnChargeExecute?.Invoke(start, end);
            OnSpeedOverride?.Invoke(chargeSpeed);

            // 돌진 지속 시간 계산
            float distance = Vector3.Distance(start, end);
            float duration = Mathf.Max(distance / chargeSpeed + 0.5f, 1.5f);
            float elapsed = 0f;

#if UNITY_EDITOR
            Debug.Log($"[MorayChargeDirector] Charge #{index}: Start=({start.x:F1},{start.z:F1}) End=({end.x:F1},{end.z:F1}) Dist={distance:F1} Duration={duration:F1}s");
#endif

            // 매 프레임 충돌 체크 + 종료 체크
            while (elapsed < duration)
            {
                CheckChargeHit();

                // 목표 도달 확인
                if (_bossTransform != null)
                {
                    float dist = Vector3.Distance(
                        new Vector3(_bossTransform.position.x, 0f, _bossTransform.position.z),
                        new Vector3(end.x, 0f, end.z));
                    if (dist < 1f)
                        break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            // 돌진 완료 처리
            if (indicator != null)
                indicator.DespawnIndicator(index);
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

            float groundY = indicatorFloorY; // Boss 현재 Y가 아닌 바닥 Y 사용 (네모 부양 방지)

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
        /// 돌진 Z 위치 — 항상 Ground Z 중앙 (Player Z 무시)
        /// Player가 Ground 가장자리로 가도 네모가 화면 밖으로 나가지 않음
        /// </summary>
        private float PredictChargeZ(int index)
        {
            if (!_hasGroundBounds) return 0f;
            float minZ = _groundBounds.MinZ;
            float maxZ = _groundBounds.MaxZ;
            float centerZ = (minZ + maxZ) * 0.5f;

            if (_totalCharges <= 1) return centerZ;

            // 여러 돌진: Z축으로 퍼뜨리되 중심 범위 내에서만
            float halfRange = (maxZ - minZ) * 0.3f; // 전체 범위의 30%
            float t = (float)index / (_totalCharges - 1); // 0~1
            return centerZ + (t - 0.5f) * 2f * halfRange; // centerZ ± halfRange
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
