using UnityEngine;

namespace HideAndInk.Siyeon1
{
    [DisallowMultipleComponent]
    public sealed class ChichiChargeInterruptReceiver : MonoBehaviour
    {
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
