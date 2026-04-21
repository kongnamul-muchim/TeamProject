using UnityEngine;
using HideAndInk.Core.Interfaces;

/// <summary>
/// 위협 신호를 받으면 치치 상태와 두두 잉크 상태를 함께 바꾼다.
/// </summary>
public class ChichiThreatHandler : MonoBehaviour, IThreatHandler
{
    [Header("References")]
    [SerializeField] private ChichiStateMachine stateMachine;
    [SerializeField] private PlayerInk targetInk;

    [Header("Threat")]
    [SerializeField] private float autoResetDelay = 0.1f;

    private bool _isThreatActive;
    private float _timer;

    private void Update()
    {
        if (!_isThreatActive)
            return;

        _timer -= Time.deltaTime;
        if (_timer <= 0f)
        {
            ResetThreat();
        }
    }

    public void SetThreat(bool threat)
    {
        if (threat)
        {
            _isThreatActive = true;
            _timer = autoResetDelay;

            if (stateMachine != null)
                stateMachine.SetThreat(true);

            if (targetInk != null)
                targetInk.SetThreat(true);
        }
        else
        {
            ResetThreat();
        }
    }

    private void ResetThreat()
    {
        _isThreatActive = false;
        _timer = 0f;

        if (stateMachine != null)
            stateMachine.SetThreat(false);

        if (targetInk != null)
            targetInk.SetThreat(false);
    }
}
