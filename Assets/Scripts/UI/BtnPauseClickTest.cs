using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Btn_Pause에 직접 붙여서 클릭 이벤트가 도달하는지 확인
/// </summary>
public class BtnPauseClickTest : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log($"[BtnPauseClickTest] OnPointerClick! button={eventData.button}, position={eventData.position}");
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        Debug.Log($"[BtnPauseClickTest] OnPointerDown! position={eventData.position}");
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Debug.Log($"[BtnPauseClickTest] OnPointerUp! position={eventData.position}");
    }

    void Update()
    {
        // 마우스가 Btn_Pause 위에 있는지 체크
        var rectTransform = GetComponent<RectTransform>();
        Vector2 localMousePosition;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform, 
            Input.mousePosition, 
            null, 
            out localMousePosition);

        if (rectTransform.rect.Contains(localMousePosition))
        {
            if (Input.GetMouseButtonDown(0))
            {
                Debug.Log($"[BtnPauseClickTest] 마우스 클릭 감지! localPos={localMousePosition}, rect={rectTransform.rect}");
            }
        }
    }
}
