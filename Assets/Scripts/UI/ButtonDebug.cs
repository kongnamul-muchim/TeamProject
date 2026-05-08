using UnityEngine;
using UnityEngine.UI;

public class ButtonDebug : MonoBehaviour
{
    private void Awake()
    {
        var btn = GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.AddListener(() => {
                Debug.Log("[ButtonDebug] ✅ 버튼 클릭됨!");
            });
        }
        else
        {
            Debug.LogWarning("[ButtonDebug] Button 컴포넌트가 없습니다!");
        }
    }
}
