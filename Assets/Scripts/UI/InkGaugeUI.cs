using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// InkGauge_Fill의 fillAmount를 PlayerInk에 연동
/// InkGauge_Frame 오브젝트에 연결
/// </summary>
public sealed class InkGaugeUI : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("PlayerInk 컴포넌트 (인스펙터에서 할당, null이면 Instance 자동 연결)")]
    [SerializeField] private PlayerInk playerInk;

    [Header("잉크 게이지")]
    [Tooltip("실제로 fillAmount가 변할 Image (InkGauge_Fill)")]
    [SerializeField] private Image inkFillImage;

    private void Awake()
    {
        Debug.Log($"[InkGaugeUI] Awake - serialized playerInk={(playerInk != null ? playerInk.GetInstanceID().ToString() : "null")}, PlayerInk.Instance={(PlayerInk.Instance != null ? PlayerInk.Instance.GetInstanceID().ToString() : "null")}");

        // 크로스-프리팹 참조 깨짐 보정
        if (playerInk == null || playerInk.GetInstanceID() != PlayerInk.Instance?.GetInstanceID())
        {
            playerInk = PlayerInk.Instance;
        }

        if (playerInk == null)
        {
            Debug.LogError("[InkGaugeUI] PlayerInk를 찾을 수 없습니다.");
            return;
        }

        playerInk.OnInkChanged += OnInkChanged;
        UpdateFill(playerInk.CurrentInk, playerInk.MaxInk);
        Debug.Log($"[InkGaugeUI] Initialized - InstanceID={playerInk.GetInstanceID()}, CurrentInk={playerInk.CurrentInk}, MaxInk={playerInk.MaxInk}");
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
        if (inkFillImage == null) return;
        inkFillImage.fillAmount = max > 0f ? current / max : 0f;
    }
}
