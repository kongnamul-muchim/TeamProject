using UnityEngine;

namespace HideAndInk.Siyeon1
{
    [DisallowMultipleComponent]
    public sealed class ChichiMovementController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("치치 상태 머신 참조")]
        [SerializeField] private ChichiStateMachine stateMachine;
        [Tooltip("두두(Player) Transform")]
        [SerializeField] private Transform duduTransform;
        [Tooltip("Rigidbody (물리 기반 이동)")]
        [SerializeField] private Rigidbody rb;

        [Header("Movement Speed")]
        [Tooltip("Walk 상태 이동 속도")]
        [SerializeField] private float walkSpeed = 2.5f;
        [Tooltip("가이드 이동 속도")]
        [SerializeField] private float guideMoveSpeed = 2f;
        [Tooltip("충전 접근 속도")]
        [SerializeField] private float chargeApproachSpeed = 3.5f;

        [Header("Charge Approach")]
        [Tooltip("충전 접근 시 멈추는 거리")]
        [SerializeField] private float chargeStopDistance = 0.12f;

        [Header("Position Lock")]
        [Tooltip("Y축(높이) 고정. true면 초기 Y값을 유지하여 땅에 붙어있음")]
        [SerializeField] private bool lockY = true;
        [Tooltip("Z축(깊이) 고정. true면 초기 Z값을 유지")]
        [SerializeField] private bool lockZ = true;

        private float initialY;
        private float initialZ;

        // FixedUpdate에서 사용할 이동 캐시
        private Vector3 moveTarget;
        private float moveSpeed;
        private float moveStopDistance;
        private bool hasMoveTarget;

        private void Awake()
        {
            if (rb == null)
            {
                rb = GetComponent<Rigidbody>();
            }

            initialY = transform.position.y;
            initialZ = transform.position.z;
        }

        private void Update()
        {
            if (stateMachine == null)
            {
                hasMoveTarget = false;
                return;
            }

            hasMoveTarget = false;

            if (stateMachine.CurrentState == ChichiState.Walk)
            {
                SetMoveTarget(duduTransform, walkSpeed, 0f);
            }
            else if (stateMachine.CurrentState == ChichiState.ApproachCharge)
            {
                SetMoveTarget(duduTransform, chargeApproachSpeed, chargeStopDistance);
            }
            else if (stateMachine.CurrentState == ChichiState.Charging)
            {
                SetMoveTarget(duduTransform, chargeApproachSpeed, chargeStopDistance);
            }
        }

        private void FixedUpdate()
        {
            if (!hasMoveTarget || rb == null)
            {
                return;
            }

            Vector3 current = rb.position;
            if (Vector3.Distance(current, moveTarget) <= moveStopDistance)
            {
                return;
            }

            Vector3 next = Vector3.MoveTowards(current, moveTarget, moveSpeed * Time.fixedDeltaTime);
            rb.MovePosition(next);
        }

        private void SetMoveTarget(Transform target, float speed, float stopDist)
        {
            if (target == null)
            {
                return;
            }

            moveTarget = target.position;

            // Y축 고정: 치치가 땅에 박히거나 뜨지 않도록 초기 Y 유지
            if (lockY)
            {
                moveTarget.y = initialY;
            }

            // Z축 고정: 2D 게임에서 앞뒤로 이동하지 않도록 초기 Z 유지
            if (lockZ)
            {
                moveTarget.z = initialZ;
            }

            moveSpeed = speed;
            moveStopDistance = stopDist;
            hasMoveTarget = true;
        }
    }
}
