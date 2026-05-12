using HideAndInk.Siyeon1;
using TMPro;
using UnityEngine;

public sealed class ChichiChargeUI : MonoBehaviour
{
    [Header("Position Offset")]
    [Tooltip("치치 오브젝트 기준 Y축 오프셋 (머리 위)")]
    [SerializeField] private float yOffset = 1.8f;

    [Header("Text Style")]
    [Tooltip("텍스트 메시 프로 폰트 에셋")]
    [SerializeField] private TMP_FontAsset fontAsset;
    [Tooltip("폰트 크기")]
    [SerializeField] private float fontSize = 2f;
    [Tooltip("텍스트 색상")]
    [SerializeField] private Color textColor = Color.white;

    [Header("Sorting")]
    [Tooltip("텍스트가 Zone Background 위에 보이도록 Sorting Layer 지정")]
    [SerializeField] private string sortingLayerName = "foreground";
    [Tooltip("동일 Sorting Layer 내에서의 순서")]
    [SerializeField] private int sortingOrder = 100;

    private TextMeshPro _chargeText;
    private ChichiInkTank _inkTank;
    private int _maxUses;
    private int _remainingUses;

    private void Awake()
    {
        CreateTextMeshPro();
    }

    private void CreateTextMeshPro()
    {
        GameObject textGO = new GameObject("ChargeText");
        textGO.transform.SetParent(transform, false);

        _chargeText = textGO.AddComponent<TextMeshPro>();
        if (fontAsset != null)
            _chargeText.font = fontAsset;
        _chargeText.fontSize = fontSize;
        _chargeText.color = textColor;
        _chargeText.alignment = TextAlignmentOptions.Center;
        _chargeText.textWrappingMode = TextWrappingModes.PreserveWhitespaceNoWrap;

        // Renderer Sorting 설정 → Background 위에 표시
        Renderer r = textGO.GetComponent<Renderer>();
        if (r != null)
        {
            r.sortingLayerName = sortingLayerName;
            r.sortingOrder = sortingOrder;
        }

        RectTransform rect = textGO.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(2, 1);
    }

    private void Start()
    {
        _inkTank = GetComponent<ChichiInkTank>();
        if (_inkTank == null)
            _inkTank = ChichiInkTank.Instance;

        if (_inkTank != null)
        {
            _inkTank.ChargeUsesChanged += OnChargeUsesChanged;
            _maxUses = _inkTank.ChargeUsesPerSection;
            _remainingUses = _inkTank.RemainingChargeUses;
            UpdateText();
        }
        else
        {
            Debug.LogError("[ChichiChargeUI] ChichiInkTank not found!", this);
        }
    }

    private void LateUpdate()
    {
        if (_chargeText != null)
        {
            Vector3 worldPos = transform.position + Vector3.up * yOffset;
            _chargeText.transform.position = worldPos;
            _chargeText.transform.rotation = Quaternion.identity;
        }
    }

    private void OnDestroy()
    {
        if (_inkTank != null)
            _inkTank.ChargeUsesChanged -= OnChargeUsesChanged;
    }

    private void OnChargeUsesChanged(int remaining, int max)
    {
        _remainingUses = remaining;
        _maxUses = max;
        UpdateText();
    }

    private void UpdateText()
    {
        if (_chargeText != null)
            _chargeText.text = $"{_remainingUses}/{_maxUses}";
    }
}
