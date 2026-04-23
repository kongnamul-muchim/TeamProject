using UnityEngine;

public class ZoneChanger : MonoBehaviour
{
    [Header("교체할 구역 오브젝트들")]
    public GameObject zone1;
    public GameObject zone2;

    // 3D 콜라이더용 트리거 함수
    private void OnTriggerEnter(Collider other)
    {
        // 대상의 태그 확인
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
            Debug.Log("3D 트리거로 구역이 교체되었습니다!");
        }
    }
}