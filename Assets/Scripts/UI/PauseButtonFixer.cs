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
        var btn = GetComponent<Button>();
        
        // 기존 OnClick 모두 제거
        btn.onClick.RemoveAllListeners();
        
        // PauseHandler 찾기
        var pauseHandler = FindObjectOfType<HideAndInk.Scripts.UI.PauseHandler>();
        if (pauseHandler != null)
        {
            btn.onClick.AddListener(pauseHandler.TogglePause);
        }
        else
        {
            Debug.LogError("[PauseButtonFixer] PauseHandler를 찾을 수 없습니다!");
        }
    }
}
