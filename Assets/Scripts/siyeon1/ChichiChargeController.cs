using System;
using HideAndInk.Core.Interfaces;
using HideAndInk.Core.Managers;
using UnityEngine;

namespace HideAndInk.Siyeon1
{
    [DisallowMultipleComponent]
    public sealed class ChichiChargeController : MonoBehaviour
    {
        [Header("DI References")]
        [Tooltip("치치 상태 머신 참조")]
        [SerializeField] private ChichiStateMachine stateMachine;
        [Tooltip("치치 잉크 탱크 참조")]
        [SerializeField] private ChichiInkTank inkTank;
        [Tooltip("두두(Player) Transform")]
        [SerializeField] private Transform duduTransform;

        [Header("Contact Check")]
        [Tooltip("치치 접촉 감지 Collider")]
        [SerializeField] private Collider chichiContactCollider;
        [Tooltip("두두 접촉 감지 Collider")]
        [SerializeField] private Collider duduContactCollider;

        [Header("Input")]
        [Tooltip("충전 키 (Inspector에서 설정)")]
        [SerializeField] private KeyCode chargeKey = KeyCode.X;

        [Header("Charge Rules")]
        [Tooltip("충전을 시작할 접근 거리")]
        [SerializeField] private float approachDistance = 1.2f;
        [Tooltip("실제 충전이 가능한 접촉 거리")]
        [SerializeField] private float contactDistance = 0.05f;
        [Tooltip("충전 게이지가 100%까지 차는 시간(초)")]
        [SerializeField] private float chargeDuration = 2f;
        [Tooltip("두두가 의태 중일 때도 충전 가능")]
        [SerializeField] private bool canChargeWhileDuduCamouflaged;
        [Tooltip("두두가 도망 중일 때도 충전 가능")]
        [SerializeField] private bool canChargeWhileDuduFleeing;
        [Tooltip("접촉이 끊기면 충전 게이지 초기화")]
        [SerializeField] private bool resetProgressWhenContactLost = true;

        private bool isApproaching;
        private bool isCharging;
        private float chargeTimer;
        private IDuduInkReceiver inkReceiver;
        private IDuduStateProvider stateProvider;
        private IDuduContactChargeSession contactChargeSession;

        public KeyCode ChargeKey => chargeKey;
        public float ChargeProgress01 => isCharging && chargeDuration > 0f ? Mathf.Clamp01(chargeTimer / chargeDuration) : 0f;
        public bool IsApproaching => isApproaching;
        public bool IsCharging => isCharging;
        public string ChargeStatus { get; private set; } = "대기";

        private IDuduInkReceiver InkReceiver => inkReceiver;
        private IDuduStateProvider StateProvider => stateProvider;
        private IDuduContactChargeSession ContactChargeSession => contactChargeSession;

        public event Action ChargeRequested;
        public event Action ChargeApproachStarted;
        public event Action ChargingStarted;
        public event Action<float> ChargeProgressChanged;
        public event Action<float> ChargeCompleted;
        public event Action ChargeInterrupted;
        public event Action ChargeEnded;

        private void Start()
        {
            ResolveDependencies();
        }

        private void ResolveDependencies()
        {
            if (GameManager.Container == null)
            {
                Debug.LogWarning("[ChichiChargeController] GameManager.Container가 null! DI 서비스 해석 불가.", this);
                return;
            }

            if (GameManager.Container.IsRegistered<IDuduInkReceiver>())
            {
                inkReceiver = GameManager.Container.Resolve<IDuduInkReceiver>();
            }
            else
            {
                Debug.LogWarning("[ChichiChargeController] IDuduInkReceiver가 DI에 등록되지 않음! DuduDevelopChargeAdapter가 Player에 있는지 확인.", this);
            }

            if (GameManager.Container.IsRegistered<IDuduStateProvider>())
            {
                stateProvider = GameManager.Container.Resolve<IDuduStateProvider>();
            }
            else
            {
                Debug.LogWarning("[ChichiChargeController] IDuduStateProvider가 DI에 등록되지 않음!", this);
            }

            if (GameManager.Container.IsRegistered<IDuduContactChargeSession>())
            {
                contactChargeSession = GameManager.Container.Resolve<IDuduContactChargeSession>();
            }
            else
            {
                Debug.LogWarning("[ChichiChargeController] IDuduContactChargeSession이 DI에 등록되지 않음!", this);
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(chargeKey))
            {
                TryStartApproach();
            }

            if (isApproaching)
            {
                UpdateApproach();
            }

            if (isCharging)
            {
                UpdateCharging();
            }
        }

        public bool TryStartApproach()
        {
            if (isApproaching || isCharging)
            {
                return true;
            }

            ChargeStatus = "충전 요청";
            ChargeRequested?.Invoke();

            if (!CanBeginChargeRequest())
            {
                return false;
            }

            isApproaching = true;
            isCharging = false;
            chargeTimer = 0f;
            ContactChargeSession?.SetContactCharging(true);
            ChargeStatus = "두두에게 이동 중";
            ChargeApproachStarted?.Invoke();
            return true;
        }

        public void InterruptCharge()
        {
            if (!isApproaching && !isCharging)
            {
                return;
            }

            StopChargeFlow(true);
        }

        private bool CanBeginChargeRequest()
        {
            if (duduTransform == null || inkTank == null || !inkTank.CanSpendCharge)
            {
                ChargeStatus = "충전 불가: 치치 탱크/두두 위치 확인";
                return false;
            }

            IDuduStateProvider provider = StateProvider;
            if (provider != null)
            {
                if (!canChargeWhileDuduCamouflaged && (provider.CurrentState == DuduState.AutoCamouflaging || provider.CurrentState == DuduState.PerfectCamouflage))
                {
                    ChargeStatus = "충전 불가: 두두가 의태 중";
                    return false;
                }

                if (!canChargeWhileDuduFleeing && provider.CurrentState == DuduState.Fleeing)
                {
                    ChargeStatus = "충전 불가: 두두가 도망 중";
                    return false;
                }

                if (provider.CurrentState == DuduState.Dead)
                {
                    ChargeStatus = "충전 불가: 두두 사망";
                    return false;
                }
            }

            IDuduInkReceiver receiver = InkReceiver;
            if (receiver == null || !receiver.CanReceiveInk)
            {
                ChargeStatus = "충전 불가: 두두 잉크/먹물/상태 확인";
                return false;
            }

            return stateMachine == null || stateMachine.CanChargeDudu(canChargeWhileDuduCamouflaged, canChargeWhileDuduFleeing);
        }

        private void UpdateApproach()
        {
            if (!CanBeginChargeRequest())
            {
                StopChargeFlow(true);
                return;
            }

            if (IsTouchingDudu())
            {
                isApproaching = false;
                isCharging = true;
                chargeTimer = 0f;
                ChargeStatus = "충전 중";
                ChargingStarted?.Invoke();
            }
        }

        private void UpdateCharging()
        {
            if (!CanBeginChargeRequest() || !IsTouchingDudu())
            {
                if (resetProgressWhenContactLost)
                {
                    StopChargeFlow(true);
                }
                return;
            }

            chargeTimer += Time.deltaTime;
            ChargeStatus = $"충전 중 {Mathf.RoundToInt(ChargeProgress01 * 100f)}%";
            ChargeProgressChanged?.Invoke(ChargeProgress01);

            if (chargeTimer < chargeDuration)
            {
                return;
            }

            float tankAmount = inkTank.SpendCharge();
            float added = InkReceiver?.AddInk(tankAmount) ?? 0f;
            ChargeStatus = $"충전 완료: +{Mathf.RoundToInt(added)}";
            ChargeCompleted?.Invoke(added);
            StopChargeFlow(false);
        }

        private bool IsTouchingDudu()
        {
            if (chichiContactCollider != null && duduContactCollider != null)
            {
                Bounds chichiBounds = chichiContactCollider.bounds;
                Bounds duduBounds = duduContactCollider.bounds;

                return chichiBounds.min.x <= duduBounds.max.x &&
                       chichiBounds.max.x >= duduBounds.min.x &&
                       chichiBounds.min.y <= duduBounds.max.y &&
                       chichiBounds.max.y >= duduBounds.min.y;
            }

            return duduTransform != null && Vector3.Distance(transform.position, duduTransform.position) <= contactDistance;
        }

        private void StopChargeFlow(bool interrupted)
        {
            bool wasActive = isApproaching || isCharging;
            isApproaching = false;
            isCharging = false;
            chargeTimer = 0f;
            ContactChargeSession?.SetContactCharging(false);
            ChargeProgressChanged?.Invoke(0f);
            if (wasActive)
            {
                if (interrupted)
                {
                    ChargeStatus = "충전 중단";
                    ChargeInterrupted?.Invoke();
                }

                ChargeEnded?.Invoke();
            }
        }
    }
}
