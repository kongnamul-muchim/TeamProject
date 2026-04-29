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
        [Tooltip("곰치 돌진 인디케이터")]
        [SerializeField] private MorayChargeIndicator indicator;
        [Tooltip("인디케이터 너비")]
        [SerializeField] private float indicatorWidth = 2.5f;
        [Tooltip("활성화 색상")]
        [SerializeField] private Color activeColor = new Color(1f, 0.2f, 0.2f, 0.6f);
        [Tooltip("임박 색상 (곧 돌진)")]
        [SerializeField] private Color imminentColor = new Color(1f, 0f, 0f, 0.9f);

        [Header("돌진 설정")]
        [Tooltip("돌진 속도")]
        [SerializeField] private float chargeSpeed = 18f;
        [Tooltip("최소 사각형 간격")]
        [SerializeField] private float minSquareInterval = 0.3f;
        [Tooltip("돌진 지연 시간")]
        [SerializeField] private float chargeDelay = 0.2f; // 돌진 사이 텀
        [Tooltip("플레이어 예측 시간")]
        [SerializeField] private float playerPredictionTime = 0.4f; // Player 예측 시간

        [Header("화면 여유")]
        [Tooltip("화면 가장자리 오프셋")]
        [SerializeField] private float screenEdgeOffset = 2f;
        [SerializeField, Range(0f, 0.4f), Tooltip("Viewport 좌/우에서 안쪽으로 margin (frustum culling 방지). 0.1 = 10%")]
        private float viewportEdgeMargin = 0.1f;

        [Header("바닥 높이")]
        [SerializeField, Tooltip("Ground 레이어 (Raycast로 바닥 높이 탐색)")]
        private LayerMask groundLayer;
        [SerializeField, Tooltip("인디케이터/돌진 Y 위치. Raycast 실패 시 fallback")]
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
        private Vector3 _playerVelocity;
        private HideAndInk.Player.PlayerMovementAdapter _cachedMovement;

        // ★ GroundBounds 캐싱 (CalculateChargePath에서 클램핑용)
        private GroundBounds? _groundBounds;

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

        private Camera _mainCamera;

        private void Awake()
        {
            // Camera 캐싱 (Camera.main이 null일 경우 Find fallback)
            _mainCamera = Camera.main ?? FindObjectOfType<Camera>();

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
            _groundBounds = bounds; // ★ GroundBounds 캐싱
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

            // Prepare 시작 → 화면 밖 진입점으로 순간이동 (Ground 높이 사용, boss Y 무시)
            if (_chargeStarts.Length > 0)
            {
                Vector3 preparePos = _chargeStarts[0];
                // _chargeStarts의 Y는 이미 CalculateChargePath에서 Ground 높이로 설정됨
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

        /// <summary>GroundBounds 재설정 (더 이상 사용하지 않음)</summary>
        public void SetGroundBounds(GroundBounds bounds)
        {
            // Player/Camera 중심 동적 범위로 대체
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
            // ★ Ground 높이로 통일 (boss Y 무시, Raycast로 찾은 groundY 사용)
            float groundY = GetGroundY(start, start.y);
            start.y = groundY;
            end.y = groundY;

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

        /// <summary>
        /// 지정 위치의 Ground 높이를 Raycast로 탐색 (실패 시 fallback 반환)
        /// <br/>개선: position.y 기준 양방향 Raycast + GroundBounds 클램핑
        /// </summary>
        private float GetGroundY(Vector3 position, float fallback)
        {
            if (groundLayer.value == 0) return fallback;

            // 1순위: 위에서 아래로 Raycast (position.y 기준 +10m 위에서 시작)
            float checkHeight = position.y + 10f;
            if (Physics.Raycast(new Vector3(position.x, checkHeight, position.z), Vector3.down,
                out RaycastHit hit, 20f, groundLayer))
            {
                return hit.point.y + 0.05f;
            }

            // 2순위: 아래에서 위로 Raycast (position.y가 Ground보다 낮은 경우)
            float checkLow = position.y - 0.1f;
            if (checkLow > -100f && Physics.Raycast(new Vector3(position.x, checkLow, position.z), Vector3.up,
                out hit, 20f, groundLayer))
            {
                return hit.point.y + 0.05f;
            }

            // 3순위: GroundBounds 내에서 최근접 Ground Y 탐색
            if (_groundBounds.HasValue && _bossTransform != null)
            {
                float scanX = _groundBounds.Value.ClampX(position.x);
                float scanZ = _groundBounds.Value.ClampZ(position.z);
                float scanY = Mathf.Max(position.y, fallback) + 10f;
                if (Physics.Raycast(new Vector3(scanX, scanY, scanZ), Vector3.down,
                    out hit, 20f, groundLayer))
                {
                    return hit.point.y + 0.05f;
                }
            }

            return fallback;
        }

        private void CalculateChargePath(int index)
        {
            float chargeZ = PredictChargeZ(index);

            // Camera Viewport 기준 X 범위 (해당 chargeZ depth에서 계산)
            float left, right;
            if (!TryGetViewBoundsAtZ(chargeZ, out left, out right))
            {
                // Fallback: Player 기준 고정 범위
                if (_playerTransform != null)
                {
                    left = _playerTransform.position.x - 15f - screenEdgeOffset;
                    right = _playerTransform.position.x + 15f + screenEdgeOffset;
                }
                else
                {
                    left = -20f; right = 20f;
                }
            }

            // ★ 1단계: Camera Viewport X → GroundBounds 클램핑 (Ground 밖 돌진 방지)
            if (_groundBounds.HasValue)
            {
                left = _groundBounds.Value.ClampX(left);
                right = _groundBounds.Value.ClampX(right);
                // 최소 간격 보장 (뷰포트가 Ground 가장자리에 걸친 경우)
                if (right - left < 5f)
                {
                    float cx = (left + right) * 0.5f;
                    left = cx - 5f;
                    right = cx + 5f;
                }
            }

            // 방향 교차
            int startDir = (_lastDirection == 0) ? 1 : 0;
            int endDir = (_lastDirection == 0) ? 0 : 1;
            _lastDirection = startDir;

            float startX = startDir == 0 ? left : right;
            float endX = endDir == 0 ? left : right;

            // ★ 개선: boss 실제 Y 기준으로 GetGroundY 호출 (fallback도 boss Y 사용)
            float bossY = _bossTransform != null ? _bossTransform.position.y : indicatorFloorY;
            float startY = GetGroundY(new Vector3(startX, bossY, chargeZ), bossY);
            float endY = GetGroundY(new Vector3(endX, bossY, chargeZ), bossY);
            float groundY = Mathf.Min(startY, endY); // 둘 중 낮은 쪽으로 통일

            _chargeStarts[index] = new Vector3(startX, groundY, chargeZ);
            _chargeEnds[index] = new Vector3(endX, groundY, chargeZ);
        }

        /// <summary>
        /// 돌진 Z 위치 — Ground Z 중앙 기준 (Player Z 무시)
        /// </summary>
        private float PredictChargeZ(int index)
        {
            // Player Z 위치 기준으로 돌진 Z 결정
            float centerZ = _playerTransform != null ? _playerTransform.position.z : 0f;
            if (_totalCharges <= 1) return centerZ;

            // 여러 돌진: Player Z 기준 ±5m 범위로 퍼뜨림
            float halfRange = 5f;
            float t = (float)index / (_totalCharges - 1);
            return centerZ + (t - 0.5f) * 2f * halfRange;
        }

        // ──────────────────────────────────────────────
        // Camera → Viewport-aligned bounds
        // ──────────────────────────────────────────────

        /// <summary>
        /// 특정 World Z 위치에서의 Camera Viewport 좌/우 X 범위 반환
        /// Orthographic / Perspective 모두 대응
        /// 
        /// 원리: chargeZ 위치의 한 점을 Viewport depth로 변환 → 같은 depth에서
        /// Viewport 좌/우 edge의 World X 좌표 계산
        /// → charge indicator가 항상 화면 안에 표시됨
        /// </summary>
        private bool TryGetViewBoundsAtZ(float worldZ, out float left, out float right)
        {
            left = 0; right = 0;
            if (_mainCamera == null) return false;

            float groundY = indicatorFloorY;

            // 1) chargeZ 위치의 depth (camera forward축 거리) 계산
            // ★ refPoint X = camera X (고정 0이면 멀어질수록 viewport 밖)
            Vector3 refPoint = new Vector3(_mainCamera.transform.position.x, groundY, worldZ);
            Vector3 viewportPos = _mainCamera.WorldToViewportPoint(refPoint);

            // behind camera → fallback
            if (viewportPos.z < 0f) return false;

            float depth = viewportPos.z;
            // viewport Y는 refPoint의 실제 Y를 사용 (ground level)
            float viewY = Mathf.Clamp(viewportPos.y, 0.05f, 0.95f);

            // 2) 같은 depth에서 Viewport 좌/우 (margin 적용 → frustum culling 방지)
            float vxLeft = viewportEdgeMargin;
            float vxRight = 1f - viewportEdgeMargin;
            Vector3 leftWorld = _mainCamera.ViewportToWorldPoint(new Vector3(vxLeft, viewY, depth));
            Vector3 rightWorld = _mainCamera.ViewportToWorldPoint(new Vector3(vxRight, viewY, depth));

            left = Mathf.Min(leftWorld.x, rightWorld.x) - screenEdgeOffset;
            right = Mathf.Max(leftWorld.x, rightWorld.x) + screenEdgeOffset;

            // 최소 폭 보장 (화면이 너무 좁을 경우)
            if (right - left < 5f)
            {
                float cx = (left + right) * 0.5f;
                left = cx - 5f;
                right = cx + 5f;
            }

            return true;
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
