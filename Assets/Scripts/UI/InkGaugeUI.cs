using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// InkGauge_Fill의 fillAmount를 PlayerInk에 연동
/// InkGauge_Frame 오브젝트에 연결
/// </summary>
public sealed class InkGaugeUI : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("PlayerInk 컴포넌트 (인스펙터 할당은 참고용, 런타임에는 Instance 우선)")]
    [SerializeField] private PlayerInk playerInk;

    [Header("잉크 게이지")]
    [Tooltip("실제로 fillAmount가 변할 Image (InkGauge_Fill)")]
    [SerializeField] private Image inkFillImage;

    private bool _initialized;

    private void Awake()
    {
        Debug.Log($"[InkGaugeUI] Awake - serialized playerInk={(playerInk != null ? "OK" : "NULL")}, Instance={(PlayerInk.Instance != null ? "OK" : "NULL")}, inkFillImage={(inkFillImage != null ? $"OK(type={inkFillImage.type}, fillMethod={inkFillImage.fillMethod})" : "NULL")}");
        TryBind();
    }

    private void Start()
    {
        if (!_initialized)
        {
            Debug.Log("[InkGaugeUI] Start - retrying");
            TryBind();
        }

        if (!_initialized)
        {
            Debug.LogError("[InkGaugeUI] PlayerInk not found. (Start retry failed)");
        }
        else
        {
            Debug.Log($"[InkGaugeUI] Start done - CurrentInk={playerInk?.CurrentInk}, MaxInk={playerInk?.MaxInk}, fillAmount={inkFillImage?.fillAmount}");
        }
    }

    private void TryBind()
    {
        if (_initialized) return;

        if (PlayerInk.Instance != null)
        {
            playerInk = PlayerInk.Instance;
            Debug.Log("[InkGaugeUI] TryBind - using Instance");
        }

        if (playerInk == null)
        {
            Debug.Log("[InkGaugeUI] TryBind - playerInk null, deferring");
            return;
        }

        Debug.Log($"[InkGaugeUI] TryBind - CurrentInk={playerInk.CurrentInk}, MaxInk={playerInk.MaxInk}, subscribing OnInkChanged");
        playerInk.OnInkChanged += OnInkChanged;
        UpdateFill(playerInk.CurrentInk, playerInk.MaxInk);
        _initialized = true;
    }

    private void OnDestroy()
    {
        if (playerInk != null)
            playerInk.OnInkChanged -= OnInkChanged;
    }

    private void OnInkChanged(float current, float max)
    {
        Debug.Log($"[InkGaugeUI] OnInkChanged({current}, {max})");
        UpdateFill(current, max);
    }

    private void UpdateFill(float current, float max)
    {
        if (inkFillImage == null)
        {
            Debug.LogWarning("[InkGaugeUI] inkFillImage is NULL!");
            return;
        }
        float amount = max > 0f ? current / max : 0f;
        Debug.Log($"[InkGaugeUI] UpdateFill - fillAmount={inkFillImage.fillAmount} -> {amount}");
        inkFillImage.fillAmount = amount;
    }
}
