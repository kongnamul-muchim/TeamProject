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
    private void Awake()
    {
        // Panel_PlayerInfo의 RaycastTarget 끄기 (클릭 가로채기 방지)
        var panelPlayerInfo = GameObject.Find("Panel_PlayerInfo");
        if (panelPlayerInfo != null)
        {
            var img = panelPlayerInfo.GetComponent<Image>();
            if (img != null)
            {
                img.raycastTarget = false;
                Debug.Log("[PauseButtonFixer] Panel_PlayerInfo의 RaycastTarget을 OFF로 설정");
            }
        }

        var btn = GetComponent<Button>();
        
        // 기존 OnClick 모두 제거
        btn.onClick.RemoveAllListeners();
        
        // PauseHandler 찾기
        var pauseHandler = FindObjectOfType<HideAndInk.Scripts.UI.PauseHandler>();
        if (pauseHandler != null)
        {
            btn.onClick.AddListener(pauseHandler.TogglePause);
            Debug.Log("[PauseButtonFixer] PauseHandler.TogglePause 연결 완료");
        }
        else
        {
            Debug.LogError("[PauseButtonFixer] PauseHandler를 찾을 수 없습니다!");
        }
    }
}
