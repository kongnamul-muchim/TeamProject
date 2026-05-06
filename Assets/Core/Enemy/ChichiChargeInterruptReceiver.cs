using UnityEngine;

namespace HideAndInk.Siyeon1
{
    [DisallowMultipleComponent]
    public sealed class ChichiChargeInterruptReceiver : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("충전 컨트롤러 참조")]
        [SerializeField] private ChichiChargeController chargeController;

        public void InterruptCharge()
        {
            chargeController?.InterruptCharge();
        }

#if UNITY_EDITOR
        [ContextMenu("Test Interrupt Charge")]
        private void TestInterruptCharge()
        {
            InterruptCharge();
        }
#endif
    }
}
