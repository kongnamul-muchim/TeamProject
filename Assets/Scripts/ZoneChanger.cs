using UnityEngine;

public class ZoneChanger : MonoBehaviour
{
    [Header("교체할 구역 오브젝트들")]
    public GameObject zone1;
    public GameObject zone2;

    [Header("시작 시 zone2 비활성화")]
    [Tooltip("true면 게임 시작 시 zone1만 켜고 zone2는 끕니다")]
    public bool deactivateZone2OnStart = true;

    private void Start()
    {
        if (deactivateZone2OnStart)
        {
            if (zone1 != null) zone1.SetActive(true);
            if (zone2 != null) zone2.SetActive(false);
        }
    }

    // 3D 콜라이더용 트리거 함수
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            ChangeZone();
        }
    }

    // 2D 콜라이더용 트리거 함수 (혹시 2D 콜라이더 사용 시)
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            ChangeZone();
        }
    }

    void ChangeZone()
    {
        if (zone1 != null && zone2 != null)
        {
            zone1.SetActive(false);
            zone2.SetActive(true);
            Debug.Log("구역이 교체되었습니다! " + zone1.name + " → " + zone2.name);
        }
    }
}