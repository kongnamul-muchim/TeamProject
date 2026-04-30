using HideAndInk.Scripts.Save;
using UnityEngine;

/// <summary>
/// ZoneChanger의 구역 전환 이벤트를 받아 자동 저장합니다.
/// ZoneChanger와 같은 GameObject에 부착하거나, 
/// ZoneChanger를 참조하도록 Inspector에서 연결하세요.
/// 
/// ZoneChanger의 onZoneChanged 이벤트에 
/// ZoneSaveHandler.OnZoneChanged 를 연결하여 사용합니다.
/// </summary>
[RequireComponent(typeof(ZoneChanger))]
public class ZoneSaveHandler : MonoBehaviour
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

    /// <summary>
    /// ZoneChanger.onZoneChanged 콜백.
    /// 구역이 변경될 때마다 SaveManager.Save()를 호출합니다.
    /// </summary>
    private void OnZoneChanged(int toZoneNumber)
    {
        Debug.Log($"[ZoneSaveHandler] Zone_{toZoneNumber} 도착 → 자동 저장");
        SaveManager.Save(toZoneNumber);
    }
}
