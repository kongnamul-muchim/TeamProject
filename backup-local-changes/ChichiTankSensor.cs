using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [충돌 감지] 치치 탱크 트리거에 두두 몸/촉수가 닿았는지만 검사한다.
/// - OnTriggerEnter/Stay/Exit: 트리거 충돌 감지
/// - Player가 치치의 BoxCollider 안에 있으면 isTouchingTank = true
/// - 감지되면 ChichiStateMachine 에 전달하여 Charging 상태로 전환
/// </summary>
public class ChichiTankSensor : MonoBehaviour
{
    [Header("🔗 References - 연결할 컴포넌트")]
    [Tooltip("ChichiStateMachine 컴포넌트 (비워두면 자동 탐색)")]
    [SerializeField] private ChichiStateMachine stateMachine;
    [Tooltip("감지할 두두의 Collider 배열 (비워두면 자동 탐색)")]
    [SerializeField] private Collider[] targetColliders;

    private Collider _sensorCollider;
    private readonly HashSet<Collider> _touchingTargets = new HashSet<Collider>();

    private void Awake()
    {
        _sensorCollider = GetComponent<Collider>();

        // ChichiStateMachine 자동 탐색
        if (stateMachine == null)
        {
            stateMachine = GetComponent<ChichiStateMachine>();
        }

        // targetColliders가 비어있으면 Player의 Collider 자동 탐색
        if (targetColliders == null || targetColliders.Length == 0)
        {
            var playerAdapter = FindObjectOfType<HideAndInk.Player.PlayerMovementAdapter>();
            if (playerAdapter != null)
            {
                Collider playerCollider = playerAdapter.GetComponent<Collider>();
                if (playerCollider != null)
                {
                    targetColliders = new Collider[] { playerCollider };
                }
            }
        }
    }

    private void Update()
    {
        RefreshTouchState();
    }

    private void OnDisable()
    {
        _touchingTargets.Clear();
        if (stateMachine != null)
        {
            stateMachine.SetTouchingTank(false);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsTrackedTarget(other))
            return;

        _touchingTargets.Add(other);
        UpdateTouchState();
    }

    private void OnTriggerStay(Collider other)
    {
        if (!IsTrackedTarget(other))
            return;

        if (_touchingTargets.Add(other))
        {
            UpdateTouchState();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsTrackedTarget(other))
            return;

        _touchingTargets.Remove(other);
        UpdateTouchState();
    }

    private bool IsTrackedTarget(Collider other)
    {
        if (other == null || targetColliders == null)
            return false;

        for (int i = 0; i < targetColliders.Length; i++)
        {
            Collider trackedCollider = targetColliders[i];
            if (trackedCollider == null)
                continue;

            if (trackedCollider == other)
                return true;

            if (other.transform.IsChildOf(trackedCollider.transform) || trackedCollider.transform.IsChildOf(other.transform))
                return true;
        }

        return false;
    }

    private void UpdateTouchState()
    {
        if (stateMachine != null)
        {
            stateMachine.SetTouchingTank(_touchingTargets.Count > 0);
        }
    }

    private void RefreshTouchState()
    {
        if (_sensorCollider == null || targetColliders == null)
            return;

        _touchingTargets.Clear();

        for (int i = 0; i < targetColliders.Length; i++)
        {
            Collider trackedCollider = targetColliders[i];
            if (trackedCollider == null)
                continue;

            if (IsBoundsTouching(trackedCollider))
            {
                _touchingTargets.Add(trackedCollider);
            }
        }

        UpdateTouchState();
    }

    private bool IsBoundsTouching(Collider trackedCollider)
    {
        Bounds sensorBounds = _sensorCollider.bounds;
        Bounds targetBounds = trackedCollider.bounds;

        sensorBounds.Expand(0.05f);
        targetBounds.Expand(0.05f);

        return sensorBounds.Intersects(targetBounds);
    }
}
