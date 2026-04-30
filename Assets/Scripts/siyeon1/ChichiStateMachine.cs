using System;
using HideAndInk.Core.Interfaces;
using HideAndInk.Core.Managers;
using UnityEngine;

namespace HideAndInk.Siyeon1
{
    [DisallowMultipleComponent]
    public sealed class ChichiStateMachine : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("두두(Player) Transform")]
        [SerializeField] private Transform duduTransform;
        [Tooltip("충전 컨트롤러 참조")]
        [SerializeField] private ChichiChargeController chargeController;

        [Header("Distance")]
        [Tooltip("Idle 상태 유지 거리")]
        [SerializeField] private float idleDistance = 1.5f;
        [Tooltip("이 거리 이상 멀어지면 Walk 상태로 전환")]
        [SerializeField] private float walkStartDistance = 3f;
        [Tooltip("Walk 상태에서 이 거리 이내면 Idle로 복귀")]
        [SerializeField] private float walkStopDistance = 1.8f;

        private ChichiState currentState = ChichiState.Idle;
        private bool predatorInspectIgnored;
        private IDuduStateProvider duduStateProvider;

        public Transform DuduTransform => duduTransform;
        public ChichiState CurrentState => currentState;
        public float IdleDistance => idleDistance;
        public float WalkStartDistance => walkStartDistance;
        public float WalkStopDistance => walkStopDistance;

        public event Action<ChichiState> StateChanged;

        private void Start()
        {
            ResolveDependencies();
            ResolvePlayerTransform();

            if (duduTransform == null)
            {
                Debug.LogWarning("[ChichiStateMachine] duduTransform을 찾을 수 없음! PlayerInk가 씬에 있는지 확인.", this);
            }

            if (chargeController == null)
            {
                Debug.LogWarning("[ChichiStateMachine] chargeController가 할당되지 않음! X키 충전이 동작하지 않음.", this);
            }
        }

        private void ResolvePlayerTransform()
        {
            // PlayerInk.Instance 싱글톤으로 Player Transform 확보 (크로스-프리팹 참조 문제 회피)
            if (PlayerInk.Instance != null)
            {
                duduTransform = PlayerInk.Instance.transform;
            }
        }

        private void ResolveDependencies()
        {
            if (GameManager.Container != null && GameManager.Container.IsRegistered<IDuduStateProvider>())
            {
                duduStateProvider = GameManager.Container.Resolve<IDuduStateProvider>();
            }
        }

        private void OnEnable()
        {
            if (chargeController != null)
            {
                chargeController.ChargeApproachStarted += OnChargeApproachStarted;
                chargeController.ChargingStarted += OnChargingStarted;
                chargeController.ChargeInterrupted += OnChargeInterrupted;
                chargeController.ChargeEnded += OnChargeEnded;
            }
        }

        private void OnDisable()
        {
            if (chargeController != null)
            {
                chargeController.ChargeApproachStarted -= OnChargeApproachStarted;
                chargeController.ChargingStarted -= OnChargingStarted;
                chargeController.ChargeInterrupted -= OnChargeInterrupted;
                chargeController.ChargeEnded -= OnChargeEnded;
            }
        }

        private void Update()
        {
            if (predatorInspectIgnored || currentState == ChichiState.ApproachCharge || currentState == ChichiState.Charging)
            {
                return;
            }

            if (duduTransform == null)
            {
                ChangeState(ChichiState.Idle);
                return;
            }

            float distance = Vector3.Distance(transform.position, duduTransform.position);
            if (currentState == ChichiState.Walk)
            {
                ChangeState(distance <= walkStopDistance ? ChichiState.Idle : ChichiState.Walk);
                return;
            }

            ChangeState(distance >= walkStartDistance ? ChichiState.Walk : ChichiState.Idle);
        }

        public bool CanChargeDudu(bool canChargeWhileCamouflaged, bool canChargeWhileFleeing)
        {
            if (duduStateProvider == null)
            {
                return true;
            }

            if (!canChargeWhileCamouflaged && (duduStateProvider.CurrentState == DuduState.AutoCamouflaging || duduStateProvider.CurrentState == DuduState.PerfectCamouflage))
            {
                return false;
            }

            if (!canChargeWhileFleeing && duduStateProvider.CurrentState == DuduState.Fleeing)
            {
                return false;
            }

            return duduStateProvider.CurrentState != DuduState.Dead;
        }

        public void SetInspectIgnored(bool value)
        {
            predatorInspectIgnored = value;
            if (value)
            {
                ChangeState(ChichiState.InspectIgnored);
            }
        }

        private void OnChargeApproachStarted()
        {
            ChangeState(ChichiState.ApproachCharge);
        }

        private void OnChargingStarted()
        {
            ChangeState(ChichiState.Charging);
        }

        private void OnChargeInterrupted()
        {
            ChangeState(ChichiState.ChargeInterrupted);
        }

        private void OnChargeEnded()
        {
            if (currentState == ChichiState.ApproachCharge || currentState == ChichiState.Charging)
            {
                ChangeState(ChichiState.Idle);
            }
        }

        private void ChangeState(ChichiState nextState)
        {
            if (currentState == nextState)
            {
                return;
            }

            ChichiState prev = currentState;
            currentState = nextState;
            StateChanged?.Invoke(currentState);

            Debug.Log($"[ChichiStateMachine] {prev} → {nextState} (dist={Vector3.Distance(transform.position, duduTransform != null ? duduTransform.position : transform.position):F2})");
        }
    }
}
