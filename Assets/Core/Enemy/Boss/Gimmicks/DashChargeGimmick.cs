using UnityEngine;
using System.Collections.Generic;
using HideAndInk.Core.Enemy.Interfaces;

namespace HideAndInk.Core.Enemy.Boss.Gimmicks
{
    /// <summary>
    /// 백상아리 (Great White Shark) 최종 보스 기믹 (ScriptableObject)
    /// 
    /// 설계 기준: Plans/GreatWhite_Design_v1.md
    /// 핵심: Patrol 의심도 단계별 가속 → 의태 시 오브젝트 타겟팅 돌진 → Chase 폭주 모드
    /// 기존 DashChargeGimmick을 전면 개편
    /// </summary>
    [CreateAssetMenu(menuName = "Enemy Gimmicks/Dash Charge Gimmick", fileName = "DashChargeGimmick")]
    public sealed class DashChargeGimmick : ScriptableObject, IEnemyGimmick,
        IGimmickPlayerAware, IGimmickViewDirection, IGimmickCombatCycle, IGimmickTransitionOverride
    {
        public GimmickType Type => GimmickType.DashCharge;

        // ──────────────────────────────────────────────
        //  Inspector Parameters
        // ──────────────────────────────────────────────

        [Header("의심도 단계별 상승률 (%/s)")]
        [SerializeField, Tooltip("의심도 0~30%일 때 초당 상승량")]
        private float suspicionRateStage1 = 9f;
        [SerializeField, Tooltip("의심도 30~60%일 때 초당 상승량")]
        private float suspicionRateStage2 = 20f;
        [SerializeField, Tooltip("의심도 60~90%일 때 초당 상승량")]
        private float suspicionRateStage3 = 35f;
        [SerializeField, Tooltip("의심도 90~100%일 때 초당 상승량")]
        private float suspicionRateStage4 = 50f;
        [SerializeField, Tooltip("의태 중 초당 의심도 하락량")]
        private float suspicionDecreaseRateCamouflage = 1f;
        [SerializeField, Tooltip("Chase 종료 후 설정될 의심도")]
        private float postChaseSuspicion = 0f;

        [Header("이동 속도")]
        [SerializeField, Tooltip("일반 순찰 속도")]
        private float patrolSpeed = 4.5f;
        [SerializeField, Tooltip("Patrol 중 의태 트리거 돌진 속도")]
        private float chargeSpeedPatrol = 24f;
        [SerializeField, Tooltip("Chase 폭주 모드 돌진 속도")]
        private float chargeSpeedChase = 38f;
        [SerializeField, Tooltip("돌진 판정 너비 (m)")]
        private float chargeWidth = 3.5f;
        [SerializeField, Tooltip("최대 돌진 거리 (m)")]
        private float maxDashDistance = 25f;
        [SerializeField, Tooltip("Chase 중 돌진 사이 쿨타임 (초)")]
        private float chargeCooldownChase = 0.6f;

        [Header("의태 타겟팅")]
        [SerializeField, Tooltip("의태 → 오브젝트 선정까지 딜레이 (초)")]
        private float camouflageLockDelay = 0.15f;
        [SerializeField, Tooltip("오브젝트 머리 위 공격 표식 지속 시간 (초)")]
        private float indicatorDuration = 0.8f;
        [SerializeField, Tooltip("Player 기준 오브젝트 탐색 반경 (m)")]
        private float targetSearchRadius = 14f;

        [Header("Patrol 순찰")]
        [SerializeField, Tooltip("Player와 최소 거리 (m)")]
        private float minPatrolRadius = 4f;
        [SerializeField, Tooltip("Player와 최대 거리 (m)")]
        private float maxPatrolRadius = 8f;

        // ──────────────────────────────────────────────
        //  Internal State
        // ──────────────────────────────────────────────

        // --- 의심도 ---
        private float _normalizedSuspicion;       // 0~1, IGimmickPlayerAware에서 수신
        private bool _isCamouflaging;             // Player 의태 여부
        private bool _isChaseMode;                // Chase 폭주 모드 플래그

        // --- Player ---
        private Transform _playerTransform;
        private bool _isPlayerVisible;

        // --- Patrol (각도 기반 링 순찰) ---
        private float _patrolAngle;
        private float _patrolRadius;
        private Vector3 _patrolCenter; // 순찰 중심 (Player 위치 스냅샷)
        private Vector3 _cachedPatrolTarget; // 캐싱된 순찰 목표
        private float _patrolRefreshTimer; // 목표 갱신 타이머

        // --- 의태 타겟팅 ---
        private enum GimmickPhase { Idle, Locking, Charging, Cooldown }
        private GimmickPhase _currentPhase = GimmickPhase.Idle;
        private Transform _targetObject;
        private float _phaseTimer;
        private float _camouflageLockTimer;

        // --- 돌진 ---
        private enum ChasePhase { Idle, Locking, Charging, Cooldown }
        private ChasePhase _chasePhase = ChasePhase.Idle;
        private Vector3 _chargeDirection;
        private Vector3 _lastChargeDirection;      // 마지막 돌진 방향 캐싱 (overlap fallback용)
        private Vector3 _chargeStartPosition;      // 돌진 시작 시 보스 위치 (실제 이동 거리 측정용)
        private float _traveledDistance;
        private float _chaseCooldownTimer;

        // --- 회피 ---
        private Transform _bossTransform;

        // --- Player 의태 타겟 (BossEnemyController가 설정) ---
        private GameObject _currentCamouflageTarget;

        // ──────────────────────────────────────────────
        //  Callbacks (Controller 연결용)
        // ──────────────────────────────────────────────

        /// <summary>속도 오버라이드 (돌진 속도 설정)</summary>
        public System.Action<float> OnSpeedOverride;
        /// <summary>돌진 방향 전달 (시선 고정용)</summary>
        public System.Action<Vector3> OnDashStarted;
        /// <summary>오브젝트 파괴 시 호출</summary>
        public System.Action<GameObject> OnObstacleHit;
        /// <summary>오브젝트 HP 2→1 (손상, 아직 살아있음)</summary>
        public System.Action<GameObject> OnObstacleDamaged;
        /// <summary>오브젝트 완전 파괴 (HP 1→0)</summary>
        public System.Action<GameObject> OnObstacleDestroyed;
        /// <summary>의태 타겟 Lock-On (인디케이터 표시)</summary>
        public System.Action<GameObject> OnLockOnTarget;
        /// <summary>목표 위치로 이동 지시</summary>
        public System.Action<Vector3> OnMoveTo;
        /// <summary>이동 정지</summary>
        public System.Action OnMovementStop;
        /// <summary>의심도 증감 (Controller가 BossSuspicionSystem에 반영)</summary>
        public System.Action<float, float> OnIncreaseSuspicion;
        /// <summary>Player 피격 시 호출</summary>
        public System.Action OnPlayerHit;

        // ──────────────────────────────────────────────
        //  IEnemyGimmick
        // ──────────────────────────────────────────────

        public void OnActivate(Transform bossTransform)
        {
            _bossTransform = bossTransform;
            _normalizedSuspicion = 0f;
            _isCamouflaging = false;
            _isChaseMode = false;
            _currentPhase = GimmickPhase.Idle;
            _chasePhase = ChasePhase.Idle;
        }

        public void OnDeactivate()
        {
            _isChaseMode = false;
            _currentPhase = GimmickPhase.Idle;
            _chasePhase = ChasePhase.Idle;
            OnMovementStop?.Invoke();
        }

        /// <summary>
        /// BossEnemyController가 매 프레임 Player 의태 타겟 정보를 전달
        /// </summary>
        public void SetCamouflageTarget(GameObject target)
        {
            _currentCamouflageTarget = target;
        }

        // ──────────────────────────────────────────────
        //  Patrol
        // ──────────────────────────────────────────────

        public void OnPatrolEnter()
        {
            _isChaseMode = false;
            _currentPhase = GimmickPhase.Idle;
            _chasePhase = ChasePhase.Idle;
            InitPatrolAngle();
            OnSpeedOverride?.Invoke(patrolSpeed);
        }

        public void OnPatrolUpdate(float deltaTime)
        {
            // 의심도 자동 상승 (단계별 가속)
            if (!_isCamouflaging)
            {
                // Cooldown 중엔 느리게 상승 (Player에게 숨 쉴 틈 제공)
                float rate = _currentPhase == GimmickPhase.Cooldown
                    ? 3f
                    : GetCurrentSuspicionRate();
                OnIncreaseSuspicion?.Invoke(rate, deltaTime);
            }
            else
            {
                // 의태 중엔 의심도 하락
                OnIncreaseSuspicion?.Invoke(-suspicionDecreaseRateCamouflage, deltaTime);
            }

            // 의태 타겟팅: 위상별 업데이트
            switch (_currentPhase)
            {
                case GimmickPhase.Idle:
                    // Idle: 의태 중이면 Lock-On 타이머 시작
                    if (_isCamouflaging)
                    {
                        _camouflageLockTimer += deltaTime;
#if UNITY_EDITOR
                        if (_camouflageLockTimer >= camouflageLockDelay * 0.5f && _camouflageLockTimer - deltaTime < camouflageLockDelay * 0.5f)
                            Debug.Log($"[DashCharge] 타이머 진행 중: {_camouflageLockTimer:F2}/{camouflageLockDelay:F2}s");
#endif
                        if (_camouflageLockTimer >= camouflageLockDelay)
                        {
                            StartObjectLock();
                        }
                    }
                    break;

                case GimmickPhase.Locking:
                    // Locking: 인디케이터 표시 중 → 타이머 만료 시 돌진
                    _phaseTimer -= deltaTime;

                    // Lock-On 중 Player 의태 오브젝트 감지 → 의심도 상승 (경고 수준)
                    if (_isCamouflaging && _currentCamouflageTarget != null && _targetObject != null
                        && _currentCamouflageTarget == _targetObject.gameObject)
                    {
                        OnIncreaseSuspicion?.Invoke(25f, deltaTime);
#if UNITY_EDITOR
                        Debug.Log($"[DashCharge] ⚠️ Player가 Lock-On 타겟에 숨어있음! 의심도 +{25f * deltaTime:F1}%");
#endif
                    }

#if UNITY_EDITOR
                    if (_phaseTimer > 0f && _phaseTimer <= 0.3f && _phaseTimer + deltaTime > 0.3f)
                        Debug.Log($"[DashCharge] Lock-On 표시 중... 남은 시간: {_phaseTimer:F2}s");
#endif
                    if (_phaseTimer <= 0f)
                    {
                        StartPatrolCharge();
                    }
                    break;

                case GimmickPhase.Charging:
                    // Charging: 돌진 중 → 목표 도착 or 거리 초과 체크
                    UpdatePatrolCharge(deltaTime);
                    break;

                case GimmickPhase.Cooldown:
                    // Cooldown: 잠시 대기 후 Idle 복귀
                    _phaseTimer -= deltaTime;
                    if (_phaseTimer <= 0f)
                    {
                        _currentPhase = GimmickPhase.Idle;
                        OnSpeedOverride?.Invoke(patrolSpeed);
                        // 순찰 중심 최신화
                        if (_playerTransform != null)
                            _patrolCenter = _playerTransform.position;
                    }
                    break;
            }
        }

        public void OnPatrolExit()
        {
            _currentPhase = GimmickPhase.Idle;
            _camouflageLockTimer = 0f;
        }

        // ──────────────────────────────────────────────
        //  Chase
        // ──────────────────────────────────────────────

        public void OnChaseEnter()
        {
            _isChaseMode = true;
            _chasePhase = ChasePhase.Locking;
            _phaseTimer = 0.15f; // Lock-On 시간
            _chaseCooldownTimer = 0f;
            OnSpeedOverride?.Invoke(0f); // Lock-On 중 정지
        }

        public void OnChaseUpdate(float deltaTime)
        {
            if (_chasePhase == ChasePhase.Locking)
            {
                _phaseTimer -= deltaTime;
                if (_phaseTimer <= 0f)
                {
                    StartChaseCharge();
                }
            }
            else if (_chasePhase == ChasePhase.Charging)
            {
                UpdateChaseCharge(deltaTime);
            }
            else if (_chasePhase == ChasePhase.Cooldown)
            {
                _chaseCooldownTimer -= deltaTime;
                if (_chaseCooldownTimer <= 0f)
                {
                    _chasePhase = ChasePhase.Locking;
                    _phaseTimer = 0.3f;
                }
            }
        }

        public void OnChaseExit()
        {
            _chasePhase = ChasePhase.Idle;
        }

        // ──────────────────────────────────────────────
        //  Search
        // ──────────────────────────────────────────────

        public void OnSearchEnter()
        {
            _currentPhase = GimmickPhase.Idle;
        }

        public void OnSearchUpdate(float deltaTime) { }

        public void OnSearchExit() { }

        // ──────────────────────────────────────────────
        //  IEnemyGimmick: HasMovementOverride
        // ──────────────────────────────────────────────

        public bool HasMovementOverride
        {
            get
            {
                // 모든 Patrol 단계: 기믹이 목표 제어 (링 순찰)
                // Idle/Locking/Charging/Cooldown 모두 기믹이 목표 결정
                // Chase: 항상 기믹 제어
                return true;
            }
        }

        /// <summary>
        /// Patrol 상태 이동 목표: Player 기준 각도 기반 링 순찰 (3D 공간)
        /// </summary>
        public Vector3? GetPatrolTarget(Vector3 currentPos, GroundBounds bounds)
        {
            // 돌진 중 (의태 타겟 or Chase)
            if (_currentPhase == GimmickPhase.Charging && _targetObject != null)
            {
                return _targetObject.position;
            }
            if (_chasePhase == ChasePhase.Charging)
            {
                return currentPos + _chargeDirection * 5f;
            }

            // 쿨다운 중이면 정지
            if (_currentPhase == GimmickPhase.Cooldown || _chasePhase == ChasePhase.Cooldown)
            {
                return currentPos;
            }

            // Lock-On 중이면 정지
            if (_currentPhase == GimmickPhase.Locking || _chasePhase == ChasePhase.Locking)
            {
                return currentPos;
            }

            // 각도 기반 링 순찰 (캐싱 적용)
            return GetCachedRingPatrolTarget(currentPos, bounds);
        }

        /// <summary>
        /// Search 상태 이동 목표
        /// </summary>
        public Vector3? GetSearchTarget(Vector3 currentPos, Vector3 lastKnownPos, GroundBounds bounds)
        {
            // 기본 수색: lastKnownPos 주변 랜덤
            float radius = 3f;
            Vector2 offset = Random.insideUnitCircle * radius;
            Vector3 target = new Vector3(
                lastKnownPos.x + offset.x,
                currentPos.y,
                lastKnownPos.z + offset.y
            );

            return target;
        }

        // ──────────────────────────────────────────────
        //  의심도 시스템 (Phase 1)
        // ──────────────────────────────────────────────

        /// <summary>
        /// 현재 의심도 단계에 따른 초당 상승률 반환
        /// </summary>
        private float GetCurrentSuspicionRate()
        {
            float suspicion = _normalizedSuspicion * 100f;

            if (suspicion < 30f)
                return suspicionRateStage1;
            if (suspicion < 60f)
                return suspicionRateStage2;
            if (suspicion < 90f)
                return suspicionRateStage3;
            return suspicionRateStage4;
        }

        // ──────────────────────────────────────────────
        //  의태 타겟팅 (Phase 3에서 상세 구현)
        // ──────────────────────────────────────────────

        private void StartObjectLock()
        {
            _camouflageLockTimer = 0f;
            Transform target = PickRandomTarget();
            if (target == null)
            {
#if UNITY_EDITOR
                Debug.Log("[DashCharge] StartObjectLock → 타겟 없음! (Camouflageable 오브젝트가 범위 내에 없거나 모두 Destroyed)");
#endif
                return;
            }

            _targetObject = target;
            _currentPhase = GimmickPhase.Locking;
            _phaseTimer = indicatorDuration;

#if UNITY_EDITOR
            Debug.Log($"[DashCharge] Lock-On 시작! 타겟: {target.name} (위치: {target.position})");
#endif

            // 정지 (Locking 중엔 PatrolBehavior가 움직이지 않도록)
            OnSpeedOverride?.Invoke(0f);

            // 컨트롤러에게 타겟 위치 알림 (인디케이터 표시용)
            OnDashStarted?.Invoke(target.position);
            OnLockOnTarget?.Invoke(target.gameObject);
        }

        private Transform PickRandomTarget()
        {
            if (_playerTransform == null) return null;

            // "Camouflageable" 태그로 의태 가능 오브젝트 검색
            GameObject[] candidates = GameObject.FindGameObjectsWithTag("Camouflageable");

#if UNITY_EDITOR
            Debug.Log($"[DashCharge] PickRandomTarget → Camouflageable 태그 검색: {candidates.Length}개 발견");
#endif

            if (candidates.Length == 0)
            {
#if UNITY_EDITOR
                Debug.LogWarning("[DashCharge] Camouflageable 태그 오브젝트가 씬에 없습니다!");
#endif
                return null;
            }

            // 유효한 오브젝트만 수집
            List<GameObject> valid = new List<GameObject>();
            foreach (var candidate in candidates)
            {
                if (candidate == null || !candidate.activeInHierarchy) continue;

                // 반경 체크
                float dist = Vector3.Distance(_playerTransform.position, candidate.transform.position);
                if (dist > targetSearchRadius) continue;

                // ObjectHP: 파괴된 오브젝트 제외
                var hp = candidate.GetComponent<ObjectHP>();
                if (hp != null && hp.IsDestroyed) continue;

                valid.Add(candidate);
            }

            if (valid.Count == 0)
            {
#if UNITY_EDITOR
                Debug.Log($"[DashCharge] → 유효한 타겟 없음 (범위 {targetSearchRadius}m 이내 모두 Destroyed)");
#endif
                return null;
            }

            GameObject chosen = valid[Random.Range(0, valid.Count)];
#if UNITY_EDITOR
            var chosenHp = chosen.GetComponent<ObjectHP>();
            Debug.Log($"[DashCharge] → 선택: {chosen.name}" + (chosenHp != null ? $" (HP={chosenHp.CurrentHP}/{chosenHp.MaxHP})" : ""));
#endif
            return chosen.transform;
        }

        // ──────────────────────────────────────────────
        //  Patrol → 의태 돌진
        // ──────────────────────────────────────────────

        private void StartPatrolCharge()
        {
            if (_targetObject == null || IsTargetDestroyed(_targetObject.gameObject))
            {
#if UNITY_EDITOR
                Debug.Log("[DashCharge] StartPatrolCharge → 타겟 무효, Idle 복귀");
#endif
                _currentPhase = GimmickPhase.Idle;
                return;
            }

            _currentPhase = GimmickPhase.Charging;
            _traveledDistance = 0f;
            Vector3 dir = (_targetObject.position - _bossTransform.position).normalized;
            dir.y = 0f;
            _chargeDirection = dir;

#if UNITY_EDITOR
            Debug.Log($"[DashCharge] 돌진 시작! 타겟: {_targetObject.name}, 거리: {Vector3.Distance(_bossTransform.position, _targetObject.position):F1}m, 속도: {chargeSpeedPatrol}");
#endif

            OnSpeedOverride?.Invoke(chargeSpeedPatrol);
            OnDashStarted?.Invoke(dir);
        }

        private void UpdatePatrolCharge(float deltaTime)
        {
            if (_targetObject == null || IsTargetDestroyed(_targetObject.gameObject))
            {
                // 타겟이 이미 파괴됨 → Cooldown 후 Idle
                StartPatrolCooldown();
                return;
            }

            // 진행 거리 갱신
            float moveAmount = chargeSpeedPatrol * deltaTime;
            _traveledDistance += moveAmount;

            float distToTarget = Vector3.Distance(
                _bossTransform.position, _targetObject.position
            );

#if UNITY_EDITOR
            if (Mathf.Approximately(_traveledDistance % 5f, 0f) || _traveledDistance < 0.1f)
                Debug.Log($"[DashCharge] 돌진 중... 거리: {distToTarget:F1}m, 진행: {_traveledDistance:F1}/{maxDashDistance:F1}m");
#endif

            // 최대 거리 초과 or 목표 도착 체크
            if (_traveledDistance >= maxDashDistance)
            {
#if UNITY_EDITOR
                Debug.Log($"[DashCharge] 최대 거리 도달 ({maxDashDistance}m), Cooldown");
#endif
                StartPatrolCooldown();
                return;
            }

            if (distToTarget <= chargeWidth)
            {
                // 목표 도착 → 오브젝트 파괴
                DestroyTargetObject();
                StartPatrolCooldown();
            }
        }

        /// <summary>ObjectHP 기반 파괴 여부 확인 (activeSelf 대체)</summary>
        private bool IsTargetDestroyed(GameObject obj)
        {
            var hp = obj.GetComponent<ObjectHP>();
            return hp != null ? hp.IsDestroyed : !obj.activeSelf;
        }

        private void DestroyTargetObject()
        {
            if (_targetObject == null) return;

            GameObject obj = _targetObject.gameObject;
            if (IsTargetDestroyed(obj)) return;

            // Player가 이 오브젝트 안에 숨어있는지 확인
            bool playerInside = _currentCamouflageTarget == obj;

            // ObjectHP 우선 처리
            var hp = obj.GetComponent<ObjectHP>();
            if (hp != null)
            {
                var result = hp.TakeDamage();
                switch (result)
                {
                    case ObjectHP.HitResult.Damaged:
                        // HP 2→1: 오브젝트 손상 (아직 부서지지 않음)
                        OnObstacleDamaged?.Invoke(obj);
                        Debug.Log("[DashChargeGimmick] 의태 타겟 손상 (HP 2→1): " + obj.name, obj);

                        if (playerInside)
                        {
                            // Player 강제 해제 + 의심도 소폭 상승 (부서진 게 아니므로 가볍게)
                            OnIncreaseSuspicion?.Invoke(10f, 1f);
                            // Controller에서 강제 해제 처리 (OnObstacleDamaged 콜백)
                            Debug.Log("[DashChargeGimmick] ⚠️ Player가 오브젝트 HP 2→1 타격에 노출! 의심도 +10%", obj);
                        }
                        break;

                    case ObjectHP.HitResult.Destroyed:
                        // HP 1→0: 오브젝트 완전 파괴
                        OnObstacleDestroyed?.Invoke(obj);
                        OnObstacleHit?.Invoke(obj);
                        Debug.Log("[DashChargeGimmick] 의태 타겟 파괴 (HP 1→0): " + obj.name, obj);

                        if (playerInside)
                        {
                            // 의심도 100% → 즉시 Chase 발각!
                            OnIncreaseSuspicion?.Invoke(100f, 1f);
                            // Controller에서 Player 피격 처리 (OnObstacleDestroyed 콜백)
                            Debug.Log("[DashChargeGimmick] 💀 Player가 오브젝트와 함께 파괴됨! 의심도 100% 발각!", obj);
                        }
                        break;

                    case ObjectHP.HitResult.AlreadyDead:
                        // 이미 죽은 객체 — 무시
                        break;
                }
            }
            else
            {
                // ObjectHP 없으면 기존 방식 (즉시 파괴)
                obj.SetActive(false);
                OnObstacleHit?.Invoke(obj);
                Debug.Log("[DashChargeGimmick] 의태 타겟 파괴 (legacy): " + obj.name, obj);
            }
        }

        private void StartPatrolCooldown()
        {
            _currentPhase = GimmickPhase.Cooldown;
            _phaseTimer = 0.15f; // 0.15초 쿨다운
            OnSpeedOverride?.Invoke(0f);
        }

        // ──────────────────────────────────────────────
        //  Chase 폭주 모드 (Phase 4에서 상세 구현)
        // ──────────────────────────────────────────────

        private void StartChaseCharge()
        {
            if (_playerTransform == null) return;

            _chasePhase = ChasePhase.Charging;
            _traveledDistance = 0f;
            _chargeStartPosition = _bossTransform.position;

            Vector3 dir = _playerTransform.position - _bossTransform.position;
            dir.y = 0f;

            // ★ 방향 zero 방어: Player와 보스가 겹쳐있으면 fallback 방향 사용
            if (dir.sqrMagnitude < 0.01f)
            {
                dir = _lastChargeDirection.sqrMagnitude > 0.01f
                    ? _lastChargeDirection
                    : Vector3.right;

                _chargeDirection = dir.normalized;
                _lastChargeDirection = _chargeDirection;

                // ★ 근접 Hit-Scan: 겹친 Player 즉시 피격 (기존 OnTriggerEnter로는 감지 불가)
                PerformOverlapHitScan();
            }
            else
            {
                _chargeDirection = dir.normalized;
                _lastChargeDirection = _chargeDirection;
            }

            OnSpeedOverride?.Invoke(chargeSpeedChase);
            OnDashStarted?.Invoke(_chargeDirection);
        }

        private void UpdateChaseCharge(float deltaTime)
        {
            // ★ 실제 이동 거리 기반 판정 (가상 누적 제거 — 겹침 시 허위 종료 방지)
            float actualDistance = Vector3.Distance(_chargeStartPosition, _bossTransform.position);
            if (actualDistance >= maxDashDistance)
            {
                EndChaseCharge();
                return;
            }

            // 경로상 오브젝트 파괴 + Player 피격 (ObjectHP 우선)
            float moveAmount = chargeSpeedChase * deltaTime;
            Vector3 endPos = _bossTransform.position + _chargeDirection * moveAmount;
            Collider[] hits = Physics.OverlapSphere(endPos, chargeWidth / 2f);
            foreach (var hit in hits)
            {
                // ★ Player도 경로상에 있으면 피격 (이동 중 재진입 대비)
                if (hit.CompareTag("Player"))
                {
                    OnPlayerHit?.Invoke();
                    continue;
                }

                if (hit.gameObject.CompareTag("Camouflageable"))
                {
                    var hp = hit.gameObject.GetComponent<ObjectHP>();
                    if (hp != null)
                    {
                        var result = hp.TakeDamage();
                        switch (result)
                        {
                            case ObjectHP.HitResult.Damaged:
                                OnObstacleDamaged?.Invoke(hit.gameObject);
                                break;
                            case ObjectHP.HitResult.Destroyed:
                                OnObstacleDestroyed?.Invoke(hit.gameObject);
                                OnObstacleHit?.Invoke(hit.gameObject);
                                break;
                        }
                    }
                    else
                    {
                        hit.gameObject.SetActive(false);
                        OnObstacleHit?.Invoke(hit.gameObject);
                    }
                }
            }
        }

        private void EndChaseCharge()
        {
            _chasePhase = ChasePhase.Cooldown;
            _chaseCooldownTimer = chargeCooldownChase;
            OnSpeedOverride?.Invoke(0f);
        }

        /// <summary>
        /// StartChaseCharge 시점에 Player가 보스와 겹쳐있는 경우
        /// OnTriggerEnter가 발동하지 않으므로 직접 OverlapSphere로 피격 처리
        /// </summary>
        private void PerformOverlapHitScan()
        {
            Collider[] hits = Physics.OverlapSphere(
                _bossTransform.position,
                chargeWidth * 0.6f
            );

            foreach (var hit in hits)
            {
                if (hit.CompareTag("Player"))
                {
                    OnPlayerHit?.Invoke();
#if UNITY_EDITOR
                    Debug.Log("[DashCharge] 💥 Overlap Hit-Scan: 겹친 Player 즉시 피격!");
#endif
                    return;
                }
            }
        }

        // ──────────────────────────────────────────────
        //  Patrol (각도 기반 링 순찰)
        // ──────────────────────────────────────────────

        private void InitPatrolAngle()
        {
            _patrolAngle = Random.Range(0f, 360f);
            _patrolRadius = Random.Range(minPatrolRadius, maxPatrolRadius);
            if (_playerTransform != null)
                _patrolCenter = _playerTransform.position;
            _patrolRefreshTimer = 0f;
            _cachedPatrolTarget = Vector3.zero; // 무효화
        }

        private Vector3 GetRingPatrolTarget(Vector3 currentPos, GroundBounds bounds)
        {
            if (_playerTransform == null)
                return currentPos;

            // 각도 진행 (계속 회전)
            _patrolAngle += Random.Range(20f, 60f);
            if (_patrolAngle > 360f) _patrolAngle -= 360f;

            // 반경 약간 변동 (자연스러운 움직임)
            _patrolRadius += Random.Range(-1f, 1f);
            _patrolRadius = Mathf.Clamp(_patrolRadius, minPatrolRadius, maxPatrolRadius);

            // 순찰 중심 기준 목표 위치 계산 (Player 스냅샷 위치)
            float rad = _patrolAngle * Mathf.Deg2Rad;
            Vector3 target = new Vector3(
                _patrolCenter.x + Mathf.Cos(rad) * _patrolRadius,
                currentPos.y,
                _patrolCenter.z + Mathf.Sin(rad) * _patrolRadius
            );

            return target;
        }

        /// <summary>
        /// 캐싱된 순찰 목표 반환 (매 프레임 새로 계산하지 않음)
        /// 목표에 가까워지면 갱신
        /// </summary>
        private Vector3 GetCachedRingPatrolTarget(Vector3 currentPos, GroundBounds bounds)
        {
            float distToCached = Vector3.Distance(
                new Vector3(currentPos.x, 0f, currentPos.z),
                new Vector3(_cachedPatrolTarget.x, 0f, _cachedPatrolTarget.z)
            );

            _patrolRefreshTimer -= Time.deltaTime;

            // 첫 호출 or 목표 도착 or 주기 갱신
            if (_cachedPatrolTarget == Vector3.zero ||
                distToCached < 2f ||
                _patrolRefreshTimer <= 0f)
            {
                _cachedPatrolTarget = GetRingPatrolTarget(currentPos, bounds);
                _patrolRefreshTimer = Random.Range(2f, 4f);
#if UNITY_EDITOR
                Debug.Log($"[DashCharge] 새 순찰 목표: ({_cachedPatrolTarget.x:F1}, {_cachedPatrolTarget.z:F1}), 거리 {distToCached:F1}m");
#endif
            }

            return _cachedPatrolTarget;
        }

        // ──────────────────────────────────────────────
        //  Helpers
        // ──────────────────────────────────────────────

        #region IGimmickPlayerAware

        void IGimmickPlayerAware.SetPlayerTransform(Transform playerTransform)
        {
            _playerTransform = playerTransform;
        }

        void IGimmickPlayerAware.SetSuspicionLevel(float normalizedSuspicion)
        {
            _normalizedSuspicion = normalizedSuspicion;
        }

        void IGimmickPlayerAware.SetCamouflageState(bool isCamouflaging)
        {
            bool wasCamouflaging = _isCamouflaging;
            _isCamouflaging = isCamouflaging;

            if (isCamouflaging)
            {
#if UNITY_EDITOR
                Debug.Log($"[DashCharge] Player 의태 시작 → 타이머 대기 ({camouflageLockDelay}s)");
#endif
            }
            else
            {
                // 의태 해제 시:
                // - 의심도 +10% 패널티 (최초 false→false 방지)
                if (wasCamouflaging)
                {
                    OnIncreaseSuspicion?.Invoke(20f, 1f);
#if UNITY_EDITOR
                    Debug.Log("[DashCharge] 의태 해제 → 의심도 +20% 패널티, Lock 유지");
#endif
                }

                // - Lock/Idle/Charging/Cooldown 모두 취소하지 않고 유지
                // - Idle이면 타이머만 초기화 (재진입 대비)
                if (_currentPhase == GimmickPhase.Idle)
                {
                    _camouflageLockTimer = 0f;
                }
                // Locking/Charging/Cooldown은 그대로 진행
            }
        }

        void IGimmickPlayerAware.SetPlayerVisible(bool isVisible)
        {
            _isPlayerVisible = isVisible;
        }

        #endregion

        #region IGimmickViewDirection

        bool IGimmickViewDirection.OverridesViewDirection =>
            _currentPhase == GimmickPhase.Locking ||
            _currentPhase == GimmickPhase.Charging ||
            _chasePhase != ChasePhase.Idle; // Chase 중 모든 위상에서 시선 고정

        Vector3 IGimmickViewDirection.GetViewDirectionVector()
        {
            // Patrol Lock-On 중: 타겟 오브젝트 방향
            if (_currentPhase == GimmickPhase.Locking && _targetObject != null)
            {
                Vector3 dir = _targetObject.position - _bossTransform.position;
                dir.y = 0f;
                return dir.normalized;
            }

            // Chase 모든 위상: Player 방향 (Locking/Charging/Cooldown)
            if (_chasePhase != ChasePhase.Idle && _playerTransform != null)
            {
                Vector3 dir = _playerTransform.position - _bossTransform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f)
                    return dir.normalized;
            }

            // 돌진 중: 마지막 돌진 방향
            if (_chargeDirection.sqrMagnitude > 0.01f)
                return _chargeDirection;

            return Vector3.right;
        }

        bool IGimmickViewDirection.ShowChargeIndicator =>
            _currentPhase == GimmickPhase.Locking || _chasePhase == ChasePhase.Locking;

        #endregion

        #region IGimmickCombatCycle

        bool IGimmickCombatCycle.IsInCombatCycle =>
            _chasePhase != ChasePhase.Idle; // Chase의 모든 위상 (Locking/Charging/Cooldown)에서 전투 중

        bool IGimmickCombatCycle.IsCharging =>
            _chasePhase == ChasePhase.Charging;

        #endregion

        #region IGimmickTransitionOverride

        bool IGimmickTransitionOverride.ShouldSkipSearchOnLostPlayer(float normalizedSuspicion) => false;

        #endregion
    }
}
