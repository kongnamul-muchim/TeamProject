using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI 클릭 디버깅용 임시 스크립트
/// Canvas_Ingame에 붙여서 실행
/// </summary>
public class UIClickDebugger : MonoBehaviour
{
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            CheckClickedUI();
        }
    }

    void CheckClickedUI()
    {
        // GraphicRaycaster로 UI 레이캐스트
        var canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[UIClickDebugger] Canvas 컴포넌트가 없습니다!");
            return;
        }

        var raycaster = GetComponent<GraphicRaycaster>();
        if (raycaster == null)
        {
            Debug.LogError("[UIClickDebugger] GraphicRaycaster가 없습니다!");
            return;
        }

        var pointerEventData = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
        pointerEventData.position = Input.mousePosition;

        var results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
        raycaster.Raycast(pointerEventData, results);

        Debug.Log($"[UIClickDebugger] 클릭 위치: {Input.mousePosition}, 충돌 UI 개수: {results.Count}");

        for (int i = 0; i < results.Count; i++)
        {
            var result = results[i];
            var go = result.gameObject;
            var img = go.GetComponent<Image>();
            var tmp = go.GetComponent<TextMeshProUGUI>();
            var btn = go.GetComponent<Button>();

            string info = $"[{i}] {go.name}";
            if (img != null) info += $" | Image(Raycast:{img.raycastTarget})";
            if (tmp != null) info += $" | TMP(Raycast:{tmp.raycastTarget})";
            if (btn != null) info += $" | Button";
            info += $" | depth:{result.depth}";

            Debug.Log(info);
        }
    }

    [ContextMenu("모든 RaycastTarget 상태 출력")]
    void PrintAllRaycastTargets()
    {
        Debug.Log("=== Canvas_Ingame 아래 모든 Graphic의 RaycastTarget 상태 ===");
        var graphics = GetComponentsInChildren<Graphic>(true);
        foreach (var graphic in graphics)
        {
            if (graphic.raycastTarget)
            {
                Debug.Log($"[ON] {graphic.gameObject.name} ({graphic.GetType().Name})");
            }
        }
        Debug.Log($"총 {graphics.Length}개 중 RaycastTarget ON인 Graphic 개수 확인 완료");
    }
}
