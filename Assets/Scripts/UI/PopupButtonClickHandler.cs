using UnityEngine;
using UnityEngine.UI;
using HideAndInk.Scripts.UI;

/// <summary>
/// 팝업 버튼 개별 클릭 처리 (Time.timeScale = 0 대응)
/// Btn_Continue, Btn_GoTitle에 각각 붙여서 사용
/// </summary>
[RequireComponent(typeof(Button))]
public class PopupButtonClickHandler : MonoBehaviour
{
    [Tooltip("버튼 기능 타입")]
    [SerializeField] private ButtonType buttonType = ButtonType.None;
    
    public enum ButtonType
    {
        None,
        Continue,    // 게임재게
        GoTitle      // 타이틀로 돌아가기
    }
    
    private Button _button;
    private RectTransform _rectTransform;
    private PauseHandler _pauseHandler;
    
    private void Awake()
    {
        _button = GetComponent<Button>();
        _rectTransform = GetComponent<RectTransform>();
        _pauseHandler = FindObjectOfType<PauseHandler>();
        
        // 기존 OnClick 리스너 제거 (중복 방지)
        _button.onClick.RemoveAllListeners();
    }
    
    private void Update()
    {
        // Time.timeScale = 0일 때도 Update는 실행됨
        if (!Input.GetMouseButtonDown(0)) return;
        if (_pauseHandler == null)
        {
            Debug.LogWarning($"[PopupButtonClickHandler] {gameObject.name}: PauseHandler가 null입니다!");
            return;
        }
        
        // 마우스가 이 버튼 위에 있는지 체크
        Vector2 localMousePosition;
        bool isMouseOver = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rectTransform, 
            Input.mousePosition, 
            null, 
            out localMousePosition);
            
        Debug.Log($"[PopupButtonClickHandler] {gameObject.name}: 마우스오버={isMouseOver}, localPos={localMousePosition}, rect={_rectTransform.rect}");
        
        if (isMouseOver && _rectTransform.rect.Contains(localMousePosition))
        {
            Debug.Log($"[PopupButtonClickHandler] {gameObject.name}: 클릭 감지! HandleClick 호출");
            HandleClick();
        }
    }
    
    private void HandleClick()
    {
        switch (buttonType)
        {
            case ButtonType.Continue:
                Debug.Log($"[PopupButtonClickHandler] {gameObject.name} - ResumeGame 호출");
                _pauseHandler.ResumeGame();
                break;
                
            case ButtonType.GoTitle:
                Debug.Log($"[PopupButtonClickHandler] {gameObject.name} - GoToTitleScene 호출");
                _pauseHandler.GoToTitleScene();
                break;
                
            default:
                Debug.LogWarning($"[PopupButtonClickHandler] {gameObject.name} - 버튼 타입이 설정되지 않음!");
                break;
        }
    }
}
