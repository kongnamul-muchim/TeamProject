
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
        // ※ 목숨 리셋 제거: Zone 변경 시마다 ResetLives()가 호출되면
        //    전투 중 Zone 경계를 넘나들 때 플레이어가 죽지 않는 버그 발생.
        //    목숨은 사망/재시작 시 GameManager.OnGameStateChanged에서만 리셋됨.

        if (PlayerInk.Instance != null)
            PlayerInk.Instance.AddInk(PlayerInk.Instance.MaxInk);

        if (ChichiInkTank.Instance != null)
            ChichiInkTank.Instance.ResetSectionUses();
    }
}