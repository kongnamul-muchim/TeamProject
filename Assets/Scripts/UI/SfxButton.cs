using UnityEngine;
using UnityEngine.UI;
using HideAndInk.Core.Audio;

/// <summary>
/// 버튼 클릭 시 효과음을 자동으로 재생하는 컴포넌트
/// Button 컴포넌트가 있는 오브젝트에 추가하여 사용
/// </summary>
[RequireComponent(typeof(Button))]
public class SfxButton : MonoBehaviour
{
    [SerializeField] private SfxId sfxId = SfxId.ButtonClick;

    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _button.onClick.AddListener(OnButtonClicked);
    }

    private void OnButtonClicked()
    {
        if (SfxManager.Instance != null)
        {
            SfxManager.Instance.Play(sfxId);
        }
    }

    private void OnDestroy()
    {
        if (_button != null)
            _button.onClick.RemoveListener(OnButtonClicked);
    }
}
