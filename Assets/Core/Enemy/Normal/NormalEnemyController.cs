using System.Collections;
using UnityEngine;
using HideAndInk.Core.Enemy.Interfaces;
using HideAndInk.Core.Enemy.Movement;
using HideAndInk.Core.Interfaces;

namespace HideAndInk.Core.Enemy.Normal
{
    /// <summary>
    /// 일반 몬스터 컨트롤러
    /// 부채꼴 시야(ConeVisionSensor)로 Player 감지 → Boss에게 위치 알림
    /// X-Z 평면 이동
    /// </summary>
    public class NormalEnemyController : EnemyAIController
    {
        public override EnemyType Type => EnemyType.Normal;

        [Header("일반 몬스터 설정")]
        [SerializeField] private float moveSpeed = 1.5f;
        [SerializeField] private float moveInterval = 2f;
        [SerializeField] private float moveDistance = 2f;
        [SerializeField] private float idleTime = 1f;

        [Header("시야 설정")]
        [Tooltip("부채꼴 시야 센서 (IVisionSensor 구현체)")]
        [SerializeField] private IVisionSensor visionSensor;

        [Header("보스 알림 설정")]
        [Tooltip("알림 대상 보스 (비워두면 자동 탐색)")]
        [SerializeField] private Boss.BossEnemyController bossTarget;
        [Tooltip("알림 쿨타임 (초)")]
        [SerializeField] private float alertCooldown = 3f;

        // 상태
        private float _stateTimer;
        private Vector3 _targetPosition;
        private bool _isMoving;
        private float _alertTimer; // 알림 쿨타임

        protected override void InitializeMovement()
        {
            _movement = new EnemyMovement(
                enemy: this,
                speed: moveSpeed,
                acceleration: 5f,
                friction: 0.8f,
                maxSpeed: moveSpeed,
                groundLayer: groundLayer,
                groundCheckDistance: groundCheckDistance,
                groundCheckRadius: groundCheckRadius);
        }

        protected override void Start()
        {
            base.Start();

            // Boss 자동 탐색 (할당되지 않은 경우)
            if (bossTarget == null)
            {
                bossTarget = FindObjectOfType<Boss.BossEnemyController>();
            }

            // VisionSensor 자동 탐색 (할당되지 않은 경우)
            if (visionSensor == null)
            {
                visionSensor = GetComponent<IVisionSensor>();
            }
        }

        protected override void UpdateAI(float deltaTime)
        {
            // 시야 기반 Player 감지
            CheckVision(deltaTime);

            // 이동 상태 머신
            _stateTimer -= deltaTime;

            if (_stateTimer <= 0f)
            {
                if (_isMoving)
                {
                    // 이동 완료 → 대기
                    _movement.Stop();
                    _isMoving = false;
                    _stateTimer = idleTime;
                }
                else
                {
                    // 대기 완료 → 이동
                    PickNewTarget();
                    _isMoving = true;
                    _stateTimer = moveInterval;
                }
            }

            if (_isMoving)
            {
                _movement.MoveTo(_targetPosition);
            }
        }

        /// <summary>
        /// 시야 기반 Player 감지 (부채꼴 센서)
        /// </summary>
        private void CheckVision(float deltaTime)
        {
            if (visionSensor == null || _playerTransform == null) return;
            if (_alertTimer > 0f)
            {
                _alertTimer -= deltaTime;
                return;
            }

            if (visionSensor.CanSee(_playerTransform.gameObject))
            {
                AlertBoss(_playerTransform.position);
            }
        }

        /// <summary>
        /// 새로운 이동 목표 지점 선택 (X-Z 평면)
        /// Ground 범위 내에서만 목표 설정
        /// </summary>
        private void PickNewTarget()
        {
            Vector2 randomDirection = Random.insideUnitCircle.normalized;
            Vector3 currentPos = transform.position;
            Vector3 target = new Vector3(
                currentPos.x + randomDirection.x * moveDistance,
                currentPos.y, // Y축 고정
                currentPos.z + randomDirection.y * moveDistance);

            // Ground 범위 내로 제한
            _targetPosition = ClampToGroundBounds(target);
        }

        /// <summary>
        /// 보스에게 Player 위치 알림
        /// </summary>
        private void AlertBoss(Vector3 playerPosition)
        {
            _alertTimer = alertCooldown;

            if (bossTarget != null)
            {
                bossTarget.AlertPlayerPosition(playerPosition);
#if UNITY_EDITOR
                Debug.Log($"[NormalEnemy] Alerted boss: Player at {playerPosition}");
#endif
            }
        }
    }
}
