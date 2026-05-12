
using HideAndInk.Core.Interfaces;
using HideAndInk.Core.Managers;
using HideAndInk.Core.Player;
using HideAndInk.Siyeon1;
using UnityEngine;

[RequireComponent(typeof(ZoneChanger))]
public class ZoneResetHandler : MonoBehaviour
{
    private ZoneChanger _zoneChanger;

    private void Awake()
    {
        _zoneChanger = GetComponent<ZoneChanger>();
    }

    private void OnEnable()
    {
        if (_zoneChanger != null)
            _zoneChanger.onZoneChanged.AddListener(OnZoneChanged);
    }

    private void OnDisable()
    {
        if (_zoneChanger != null)
            _zoneChanger.onZoneChanged.RemoveListener(OnZoneChanged);
    }

    private void OnZoneChanged(int toZoneNumber)
    {
        // Playing 상태에서만 초기화 (전투/사망 중에는 초기화하지 않음)
        var stateMachine = GameManager.Container?.Resolve<IGameStateMachine>();
        if (stateMachine == null || stateMachine.CurrentState != GameState.Playing)
            return;

        PlayerLives.Instance?.ResetLives();
        PlayerInk.Instance?.AddInk(PlayerInk.Instance.MaxInk);
        ChichiInkTank.Instance?.ResetSectionUses();
    }
}