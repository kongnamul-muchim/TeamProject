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
        [Tooltip("Rigidbody (충돌 감지용)")]
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

        [Header("Move Away")]
        [Tooltip("충전 완료 후 멀어지는 속도")]
        [SerializeField] private float moveAwaySpeed = 3f;

        [Header("Position Lock")]
        [Tooltip("Y축(높이) 고정. true면 초기 Y값을 유지하여 땅에 붙어있음")]
        [SerializeField] private bool lockY = true;
        [Tooltip("Z축(깊이) 고정. true면 초기 Z값을 유지")]
        [SerializeField] private bool lockZ = false;

        private float initialY;
        private float initialZ;

        private void Awake()
        {
            if (rb == null)
            {
                rb = GetComponent<Rigidbody>();
            }

            initialY = transform.position.y;
            initialZ = transform.position.z;

            ResolvePlayerTransform();
        }

        private void ResolvePlayerTransform()
        {
            // PlayerInk.Instance 싱글톤으로 Player Transform 확보
            // (크로스-프리팹 참조가 깨져서 위치가 업데이트되지 않는 문제 회피)
            if (PlayerInk.Instance != null)
            {
                duduTransform = PlayerInk.Instance.transform;
            }

            if (duduTransform == null)
            {
                Debug.LogError("[ChichiMovementController] duduTransform을 찾을 수 없음! PlayerInk가 씬에 있는지 확인.", this);
            }
        }

        private void Update()
        {
            if (stateMachine == null)
            {
                return;
            }

            float speed = 0f;
            float stopDist = 0f;
            bool shouldMove = false;

            if (stateMachine.CurrentState == ChichiState.Walk)
            {
                speed = walkSpeed;
                stopDist = 0f;
                shouldMove = true;
            }
            else if (stateMachine.CurrentState == ChichiState.ApproachCharge)
            {
                speed = chargeApproachSpeed;
                stopDist = chargeStopDistance;
                shouldMove = true;
            }
            else if (stateMachine.CurrentState == ChichiState.Charging)
            {
                speed = chargeApproachSpeed;
                stopDist = chargeStopDistance;
                shouldMove = true;
            }
            else if (stateMachine.CurrentState == ChichiState.MoveAway)
            {
                speed = moveAwaySpeed;
                stopDist = 0f;
                shouldMove = true;
            }

            if (!shouldMove)
            {
                return;
            }

            Vector3 current = transform.position;
            Vector3 targetPos;

            if (stateMachine.CurrentState == ChichiState.MoveAway)
            {
                // MoveAway: Player 반대 방향으로 미리 계산된 목표 위치로 이동
                targetPos = stateMachine.MoveAwayTarget;
            }
            else
            {
                // Walk / ApproachCharge / Charging: Player 위치로 이동
                if (duduTransform == null) return;
                targetPos = duduTransform.position;
            }

            // Y축 고정: 치치가 땅에 박히거나 뜨지 않도록 초기 Y 유지
            if (lockY)
            {
                targetPos.y = initialY;
            }

            // Z축 고정: 2D 게임에서 앞뒤로 이동하지 않도록 초기 Z 유지
            if (lockZ)
            {
                targetPos.z = initialZ;
            }

            if (Vector3.Distance(current, targetPos) <= stopDist)
            {
                return;
            }

            Vector3 next = Vector3.MoveTowards(current, targetPos, speed * Time.deltaTime);
            transform.position = next;

            // Rigidbody 위치 동기화 (충돌 감지 유지)
            if (rb != null)
            {
                rb.position = next;
            }
        }
    }
}
