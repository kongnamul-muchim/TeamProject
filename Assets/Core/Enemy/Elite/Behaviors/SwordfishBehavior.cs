using UnityEngine;
using HideAndInk.Core.Enemy.Elite;

namespace HideAndInk.Core.Enemy.Elite.Behaviors
{
    /// <summary>
    /// Ch.1 청새치 정예몬스터 행동 패턴
    /// Player 감지 시 고속 돌진 (Charge) → 쿨타임 → 대기 반복
    /// </summary>
    public class SwordfishBehavior : MonoBehaviour, IEliteBehavior
    {
        public string BehaviorName => "Swordfish Charge";

        [Header("청새치 설정")]
        [Tooltip("돌진 속도")]
        [SerializeField] private float chargeSpeed = 10f;
        [Tooltip("돌진 지속 시간 (초)")]
        [SerializeField] private float chargeDuration = 1f;
        [Tooltip("돌진 쿨타임 (초)")]
        [SerializeField] private float chargeCooldown = 3f;
        [Tooltip("Player 감지 후 돌진까지 지연 시간 (초)")]
        [SerializeField] private float chargeDelay = 0.5f;
        [Tooltip("돌진 시 Player 위치 예측 계수 (0=현재위치, 1=완전예측)")]
        [Range(0f, 1f)]
        [SerializeField] private float predictionFactor = 0.7f;

        [Header("애니메이션")]
        [Tooltip("애니메이터 (비워두면 자동 탐색)")]
        [SerializeField] private Animator enemyAnimator;
        [Tooltip("돌진 애니메이션 트리거 이름")]
        [SerializeField] private string chargeTriggerName = "OnCharge";
        [Tooltip("돌진 애니메이션 길이 (초). 속도 계산에 사용됨")]
        [SerializeField] private float chargeAnimationLength = 1f;

        // 상태
        private enum State { Idle, Charging, Cooldown, PostChargePatrol }
        private State _currentState = State.Idle;
        private float _stateTimer;
        private Vector3 _chargeTarget;
        private Vector3 _chargeStartPos;
        private Vector3 _chargeDirection;
        private Vector3 _lastChargeTarget; // 돌진 목표 위치 기억 (쿨타임 후 이동용)
        private bool _hasPostChargeTarget; // 기억된 목표 위치 유효 여부

        /// <summary>
        /// 현재 돌진 중인지 여부 (컨트롤러에서 확인용)
        /// </summary>
        public bool IsCharging => _currentState == State.Charging;

        /// <summary>
        /// 이동 제어권 보유 여부 (돌진 중 + 돌진 후 기억된 위치 이동 중)
        /// </summary>
        public bool IsControllingMovement => _currentState == State.Charging || _currentState == State.PostChargePatrol;

        // 외부 참조
        private EliteEnemyController _controller;
        private Transform _playerTransform;
        private HideAndInk.Player.PlayerMovementAdapter _playerMovementAdapter;

        public void OnActivate()
        {
            _controller = GetComponent<EliteEnemyController>();
            CachePlayerTransform();
            CachePlayerMovementAdapter();
            CacheAnimator();
            _currentState = State.Idle;
        }

        public void OnDeactivate()
        {
            _currentState = State.Idle;
        }

        public void OnPlayerApproached(float distance, Vector3 playerPos)
        {
            // Player가 감지 반경 내에 있을 때 호출됨
            // 돌진 가능 상태면 돌진 시작
            if (_currentState == State.Idle && distance <= GetDetectionRadius())
            {
                StartChargeDelay();
            }
        }

        public void OnUpdate(float deltaTime)
        {
            switch (_currentState)
            {
                case State.Idle:
                    // 대기 중 (돌진 지연 타이머)
                    if (_stateTimer > 0f)
                    {
                        _stateTimer -= deltaTime;
                        if (_stateTimer <= 0f)
                        {
                            StartCharge();
                        }
                    }
                    break;

                case State.Charging:
                    // 돌진 중
                    _stateTimer -= deltaTime;
                    if (_stateTimer <= 0f)
                    {
                        EndCharge();
                    }
                    break;

                case State.Cooldown:
                    // 쿨타임 중
                    _stateTimer -= deltaTime;
                    if (_stateTimer <= 0f)
                    {
                        // 쿨타임 종료 → 돌진했던 위치로 먼저 이동
                        StartPostChargePatrol();
                    }
                    break;

                case State.PostChargePatrol:
                    // 돌진 후 기억된 위치로 이동 중
                    _stateTimer -= deltaTime;
                    
                    // 목표 도달 확인
                    if (_controller != null && _hasPostChargeTarget)
                    {
                        Vector3 currentPos = transform.position;
                        float distanceToTarget = Vector3.Distance(
                            new Vector3(currentPos.x, 0, currentPos.z),
                            new Vector3(_lastChargeTarget.x, 0, _lastChargeTarget.z));
                        
                        if (distanceToTarget < 1f || _stateTimer <= 0f)
                        {
                            // 목표 도달 또는 시간 초과 → 일반 Idle로 복귀
                            _currentState = State.Idle;
                            _hasPostChargeTarget = false;
                            
                            // 순찰 재개
                            _controller?.SetSpeed(_controller.GetDefaultSpeed());
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// 돌진 지연 시작 (Player 감지 후 바로 돌진하지 않고 약간 대기)
        /// 이동 중 감지 시 즉시 돌진, 대기 중 감지 시 딜레이 적용
        /// </summary>
        private void StartChargeDelay()
        {
            // 순찰 이동 즉시 중단
            _controller?.Stop();
            
            // 이동 중이었다면 딜레이 없이 바로 돌진
            bool wasMoving = _controller != null && _controller.IsMoving;
            
            if (wasMoving)
            {
                StartCharge();
            }
            else
            {
                _currentState = State.Idle;
                _stateTimer = chargeDelay;
            }
        }

        /// <summary>
        /// 돌진 시작
        /// </summary>
        private void StartCharge()
        {
            if (_playerTransform == null) return;

            _currentState = State.Charging;
            _stateTimer = chargeDuration;
            _chargeStartPos = transform.position;

            // 순찰 이동 완전 중단 (돌진 시작 시 중복 호출 방지)
            _controller?.Stop();

            // Player 이동 예측
            Vector3 playerPos = _playerTransform.position;
            Vector3 playerVelocity = Vector3.zero;
            
            if (_playerMovementAdapter != null)
            {
                Vector2 vel2D = _playerMovementAdapter.CurrentVelocity;
                playerVelocity = new Vector3(vel2D.x, 0f, vel2D.y);
            }

            // 예측 위치 계산
            float timeToReach = Vector3.Distance(transform.position, playerPos) / chargeSpeed;
            Vector3 predictedPos = playerPos + (playerVelocity * timeToReach * predictionFactor);
            
            _chargeTarget = predictedPos;
            _chargeDirection = (predictedPos - transform.position).normalized;

            // 이동 시작
            if (_controller != null)
            {
                _controller.SetSpeed(chargeSpeed);
                _controller.MoveTo(_chargeTarget);
            }

            // 돌진 애니메이션 트리거
            PlayChargeAnimation();

#if UNITY_EDITOR
            Debug.Log($"[SwordfishBehavior] 돌진 시작! 목표: {_chargeTarget}");
#endif
        }

        /// <summary>
        /// 돌진 종료
        /// </summary>
        private void EndCharge()
        {
            _currentState = State.Cooldown;
            _stateTimer = chargeCooldown;

            // 돌진 목표 위치 기억 (쿨타임 후 이동용)
            _lastChargeTarget = _chargeTarget;
            _hasPostChargeTarget = true;

            // 정지 및 속도 복원
            if (_controller != null)
            {
                _controller.Stop();
                _controller.SetSpeed(_controller.GetDefaultSpeed());
            }

            // 일반 애니메이션으로 복원
            ResetAnimation();

#if UNITY_EDITOR
            Debug.Log($"[SwordfishBehavior] 돌진 종료 → 쿨타임 ({chargeCooldown:F1}초) → 기억된 위치: {_lastChargeTarget}");
#endif
        }

        /// <summary>
        /// 돌진 후 기억된 위치로 이동 시작
        /// </summary>
        private void StartPostChargePatrol()
        {
            if (!_hasPostChargeTarget)
            {
                _currentState = State.Idle;
                return;
            }

            _currentState = State.PostChargePatrol;
            _stateTimer = 5f; // 최대 5초 동안 이동 시도

            // 기억된 위치로 이동
            if (_controller != null)
            {
                _controller.SetSpeed(_controller.GetDefaultSpeed() * 1.2f); // 약간 빠르게
                _controller.MoveTo(_lastChargeTarget);
            }

#if UNITY_EDITOR
            Debug.Log($"[SwordfishBehavior] 쿨타임 종료 → 기억된 위치로 이동: {_lastChargeTarget}");
#endif
        }

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
        /// PlayerMovementAdapter 캐싱
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
        /// 감지 반경 가져오기 (컨트롤러에서 설정한 값 사용)
        /// </summary>
        private float GetDetectionRadius()
        {
            return _controller?.GetDetectionRadius() ?? 8f;
        }

        /// <summary>
        /// Animator 캐싱
        /// </summary>
        private void CacheAnimator()
        {
            if (enemyAnimator == null)
            {
                enemyAnimator = GetComponentInChildren<Animator>();
            }
        }

        /// <summary>
        /// 돌진 애니메이션 재생 (속도 조절로 chargeDuration과 동기화)
        /// </summary>
        private void PlayChargeAnimation()
        {
            if (enemyAnimator != null && !string.IsNullOrEmpty(chargeTriggerName))
            {
                // 애니메이션 속도를 돌진 시간에 맞춰 조절
                // Speed = AnimationLength / Duration
                enemyAnimator.speed = chargeAnimationLength / chargeDuration;
                enemyAnimator.SetTrigger(chargeTriggerName);
            }
        }

        /// <summary>
        /// 일반 애니메이션 상태로 복원 (속도 복원)
        /// </summary>
        private void ResetAnimation()
        {
            if (enemyAnimator != null)
            {
                // 애니메이션 속도 복원
                enemyAnimator.speed = 1f;
                enemyAnimator.ResetTrigger(chargeTriggerName);
            }
        }
    }
}
