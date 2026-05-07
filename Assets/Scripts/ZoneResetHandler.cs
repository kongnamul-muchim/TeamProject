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
        if (PlayerLives.Instance != null)
            PlayerLives.Instance.ResetLives();

        if (PlayerInk.Instance != null)
            PlayerInk.Instance.AddInk(PlayerInk.Instance.MaxInk);

        if (ChichiInkTank.Instance != null)
            ChichiInkTank.Instance.ResetSectionUses();
    }
}
