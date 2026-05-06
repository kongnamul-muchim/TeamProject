using System;
using HideAndInk.Core.Interfaces;
using HideAndInk.Core.Managers;
using UnityEngine;

namespace HideAndInk.Siyeon1
{
    [DisallowMultipleComponent]
    public sealed class DuduDevelopChargeAdapter : MonoBehaviour, IDuduStateProvider, IDuduInkReceiver, IDuduContactChargeSession
    {
        [Header("Develop Dudu References")]
        [SerializeField] private global::PlayerInk playerInk;
        [SerializeField] private MonoBehaviour camouflageStateProvider;

        [Header("Charge Test Rules")]
        [SerializeField] private bool blockPassiveRecharge = true;

        [Header("Manual Test State")]
        [SerializeField] private bool isFleeing;
        [SerializeField] private bool isDead;

        private DuduState previousState;
        private bool contactChargeSessionActive;

        public DuduState CurrentState => GetCurrentState();
        public bool IsMoving => false;
        public bool IsSmokeActive => playerInk != null && playerInk.IsDashing;
        public float CurrentInk => playerInk != null ? playerInk.CurrentInk : 0f;
        public float MaxInk => playerInk != null ? playerInk.MaxInk : 0f;
        public bool CanReceiveInk => playerInk != null && playerInk.CanReceiveContactCharge() && CurrentState == DuduState.Normal;

        public event Action<DuduState> StateChanged;

        private void Start()
        {
            ResolveLocalReferences();
            RegisterInContainer();
            previousState = CurrentState;
        }

        private void OnEnable()
        {
            ResolveLocalReferences();
            RegisterInContainer();
            ApplyContactChargingFlag();
        }

        private void OnValidate()
        {
            ResolveLocalReferences();
        }

        private void OnDisable()
        {
            playerInk?.SetContactCharging(false);
        }

        private void RegisterInContainer()
        {
            if (GameManager.Container == null)
            {
                return;
            }

            GameManager.Container.RegisterInstance<IDuduStateProvider>(this);
            GameManager.Container.RegisterInstance<IDuduInkReceiver>(this);
            GameManager.Container.RegisterInstance<IDuduContactChargeSession>(this);
        }

        private void ResolveLocalReferences()
        {
            if (playerInk == null)
            {
                playerInk = GetComponent<global::PlayerInk>();
            }

            if (camouflageStateProvider == null)
            {
                MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
                for (int i = 0; i < behaviours.Length; i++)
                {
                    if (behaviours[i] is ICamouflageStateProvider)
                    {
                        camouflageStateProvider = behaviours[i];
                        break;
                    }
                }
            }
        }

        private void Update()
        {
            DuduState state = CurrentState;
            if (state == previousState)
            {
                return;
            }

            previousState = state;
            StateChanged?.Invoke(state);
        }

        public float AddInk(float amount)
        {
            if (!CanReceiveInk || amount <= 0f)
            {
                return 0f;
            }

            float before = playerInk.CurrentInk;
            playerInk.AddInk(amount);
            return Mathf.Max(0f, playerInk.CurrentInk - before);
        }

        public void SetFleeing(bool value)
        {
            isFleeing = value;
        }

        public void SetDead(bool value)
        {
            isDead = value;
        }

        public void SetContactCharging(bool value)
        {
            contactChargeSessionActive = value;
            ApplyContactChargingFlag();
        }

        private void ApplyContactChargingFlag()
        {
            playerInk?.SetContactCharging(blockPassiveRecharge || contactChargeSessionActive);
        }

        private DuduState GetCurrentState()
        {
            if (isDead)
            {
                return DuduState.Dead;
            }

            if (isFleeing)
            {
                return DuduState.Fleeing;
            }

            ICamouflageStateProvider camouflage = camouflageStateProvider as ICamouflageStateProvider;
            if (camouflage != null)
            {
                try
                {
                    if (camouflage.IsPerfect)
                    {
                        return DuduState.PerfectCamouflage;
                    }

                    if (camouflage.IsCamouflaging)
                    {
                        return DuduState.AutoCamouflaging;
                    }
                }
                catch (NullReferenceException)
                {
                }
            }

            return DuduState.Normal;
        }
    }
}
