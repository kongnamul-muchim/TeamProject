using UnityEngine;
using UnityEngine.UI;
using HideAndInk.Core.Audio;

/// <summary>
/// Btn_Pause에 직접 부착하여 PauseHandler.TogglePause를 코드로 연결
/// 씬 데이터 꼬임 문제 해결용
/// </summary>
[RequireComponent(typeof(Button))]
public class PauseButtonFixer : MonoBehaviour
{
    private HideAndInk.Scripts.UI.PauseHandler _pauseHandler;
    private RectTransform _rectTransform;

    private void Awake()
    {
        // Panel_PlayerInfo 본체 + 모든 자식 Image의 RaycastTarget OFF (클릭 가로채기 방지)
        var panelPlayerInfo = GameObject.Find("Panel_PlayerInfo");
        if (panelPlayerInfo != null)
        {
            var images = panelPlayerInfo.GetComponentsInChildren<Image>(true);
            foreach (var img in images)
            {
                img.raycastTarget = false;
            }
            Debug.Log($"[PauseButtonFixer] Panel_PlayerInfo 및 {images.Length}개 자식 Image의 RaycastTarget을 OFF로 설정");
        }

        // EndingFadeImage RaycastTarget OFF (전체 화면 덮는 Image가 클릭 가로채기 방지)
        var endingFadeImage = GameObject.Find("EndingFadeImage");
        if (endingFadeImage != null)
        {
            var img = endingFadeImage.GetComponent<Image>();
            if (img != null)
            {
                img.raycastTarget = false;
                Debug.Log("[PauseButtonFixer] EndingFadeImage의 RaycastTarget을 OFF로 설정");
            }
        }

        // Btn_Pause를 Canvas의 마지막 자식으로 이동 (Raycast 우선순위 최상위)
        var canvasIngame = GameObject.Find("Canvas_Ingame");
        if (canvasIngame != null)
        {
            transform.SetParent(canvasIngame.transform, false);
            transform.SetAsLastSibling();
            Debug.Log("[PauseButtonFixer] Btn_Pause를 Canvas 최상위로 이동");
        }

        var btn = GetComponent<Button>();
        
        // 기존 OnClick 모두 제거
        btn.onClick.RemoveAllListeners();
        
        // PauseHandler 찾기
        _pauseHandler = FindObjectOfType<HideAndInk.Scripts.UI.PauseHandler>();
        _rectTransform = GetComponent<RectTransform>();
        
        if (_pauseHandler != null)
        {
            btn.onClick.AddListener(_pauseHandler.TogglePause);
            Debug.Log("[PauseButtonFixer] PauseHandler.TogglePause 연결 완료");
        }
        else
        {
            Debug.LogError("[PauseButtonFixer] PauseHandler를 찾을 수 없습니다!");
        }
    }

    private void Update()
    {
        // EventSystem Raycast가 실패하는 경우 대체: 직접 마우스 위치 체크
        if (Input.GetMouseButtonDown(0) && _pauseHandler != null && _rectTransform != null)
        {
            Vector2 localMousePosition;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rectTransform, 
                Input.mousePosition, 
                null, 
                out localMousePosition);

            if (_rectTransform.rect.Contains(localMousePosition))
            {
                Debug.Log("[PauseButtonFixer] 마우스 클릭 감지! TogglePause 호출");
                _pauseHandler.TogglePause();
            }
        }
    }
}
