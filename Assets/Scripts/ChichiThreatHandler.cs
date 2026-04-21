using UnityEngine;
using HideAndInk.Core.Interfaces;

/// <summary>
/// [위협 처리] 위협 신호를 받으면 치치 상태와 두두 잉크 상태를 함께 바꾼다.
/// - SetThreat(true): 충전 중단 + 치치 대피 + autoResetDelay 초 후 자동 해제
/// - VisionBasedSuspicionManager 가 Alert 상태일 때 호출됨
/// </summary>
public class ChichiThreatHandler : MonoBehaviour, IThreatHandler
{
    [Header("🔗 References - 연결할 컴포넌트")]
    [Tooltip("ChichiStateMachine 컴포넌트 참조")]
    [SerializeField] private ChichiStateMachine stateMachine;
    [Tooltip("두두의 PlayerInk 컴포넌트 참조")]
    [SerializeField] private PlayerInk targetInk;

    [Header("⚠️ Threat - 위협 설정")]
    [Tooltip("위협 신호를 받은 후 자동으로 해제될 때까지 지연 시간(초)")]
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
