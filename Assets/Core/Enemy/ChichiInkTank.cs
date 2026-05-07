using System;
using UnityEngine;

namespace HideAndInk.Siyeon1
{
    [DisallowMultipleComponent]
    public sealed class ChichiInkTank : MonoBehaviour
    {
        [Header("Tank Capacity")]
        [Tooltip("탱크 최대 잉크량")]
        [SerializeField] private float tankMaxInk = 300f;
        [Tooltip("탱크 현재 잉크량")]
        [SerializeField] private float tankCurrentInk = 300f;

        [Header("Charge Per Use")]
        [Tooltip("충전 1회당 사용할 잉크량")]
        [SerializeField] private float chargeAmountPerUse = 50f;

        [Header("Section Uses")]
        [Tooltip("한 구간당 최대 충전 횟수")]
        [SerializeField] private int chargeUsesPerSection = 3;
        [Tooltip("남은 충전 횟수")]
        [SerializeField] private int remainingChargeUses = 3;

        public float TankMaxInk => tankMaxInk;
        public float TankCurrentInk => tankCurrentInk;
        public float ChargeAmountPerUse => chargeAmountPerUse;
        public int ChargeUsesPerSection => chargeUsesPerSection;
        public int RemainingChargeUses => remainingChargeUses;
        public bool CanSpendCharge => tankCurrentInk > 0f && remainingChargeUses > 0;

        public static ChichiInkTank Instance { get; private set; }

        public event Action<float, float> TankChanged;
        public event Action<int, int> ChargeUsesChanged;

        private void Awake()
        {
            Instance = this;
            tankCurrentInk = Mathf.Clamp(tankCurrentInk, 0f, tankMaxInk);
            remainingChargeUses = Mathf.Clamp(remainingChargeUses, 0, chargeUsesPerSection);
        }

        public float SpendCharge()
        {
            if (!CanSpendCharge)
            {
                return 0f;
            }

            float amount = Mathf.Min(chargeAmountPerUse, tankCurrentInk);
            tankCurrentInk -= amount;
            remainingChargeUses = Mathf.Max(0, remainingChargeUses - 1);
            TankChanged?.Invoke(tankCurrentInk, tankMaxInk);
            ChargeUsesChanged?.Invoke(remainingChargeUses, chargeUsesPerSection);
            return amount;
        }

        public void ResetSectionUses()
        {
            remainingChargeUses = chargeUsesPerSection;
            ChargeUsesChanged?.Invoke(remainingChargeUses, chargeUsesPerSection);
        }
    }
}
