using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using HideAndInk.Core.Events;
using HideAndInk.Core.Interfaces;
using HideAndInk.Core.Managers;

/// <summary>
/// DialogeUI 실제 제어 + 타자기 효과 담당 MonoBehaviour
/// Canvas_Ingame 오브젝트에 부착하여 사용
/// </summary>
public class DialogueUIAdapter : MonoBehaviour
{
    [Header("DialogeUI References")]
    [SerializeField] private GameObject dialogeUIRoot;      // DialogeUI 최상위 오브젝트
    [SerializeField] private TextMeshProUGUI textDialoge;   // 대사 텍스트 (Text_dialoge)
    [SerializeField] private TextMeshProUGUI textSpeaker;   // 화자 이름 텍스트 (Panel_name > Text)
    [SerializeField] private GameObject endDialogueSign;    // "이어 하기" 표시 (EndDialogueSign)

    [Header("Cutscene References")]
    [SerializeField] private Image storyCutscene;           // StoryCutScene Image (컷씬 배경)
    [SerializeField] private Image panelBgImage;            // Panel > Image (대화창 배경 이미지)
    [SerializeField] private GameObject playerInfoPanel;    // Panel_PlayerInfo (대화 중 숨김)
    [SerializeField] private GameObject pauseButton;        // Btn_Pause (대화 중 비활성화)

    [Header("Typewriter Settings")]
    [SerializeField] private float charDelay = 0.04f;       // 글자당 지연 시간 (초)
    [SerializeField] private float punctuationDelay = 0.15f; // 마침표/물음표/느낌표 지연 시간

    // ─── 상태 ──────────────────────────────────────────────────
    private IStoryManager _storyManager;
    private Coroutine _typewriterCoroutine;
    private bool _isCurrentlyTyping;
    private string _currentFullText;
    private bool _isDialogueActive;

    // ─── 초기화 ────────────────────────────────────────────────
    private void Awake()
    {
        TryFindReferences();
        SubscribeToEvents();

        // 시작 시 DialogeUI 비활성화
        if (dialogeUIRoot != null)
            dialogeUIRoot.SetActive(false);

        // Panel 배경 이미지 기본 활성화 (프리팹 기본값 보정)
        if (panelBgImage != null)
            panelBgImage.enabled = true;
    }

    private void Start()
    {
        ResolveStoryManager();
    }

    /// <summary>
    /// 인스펙터 미할당 시 자동 탐색
    /// </summary>
    private void TryFindReferences()
    {
        if (dialogeUIRoot == null)
            dialogeUIRoot = FindDialogeUI();

        if (dialogeUIRoot == null)
        {
            Debug.LogError("[DialogueUIAdapter] DialogeUI를 찾을 수 없습니다. Canvas_Ingame 구조를 확인하세요.");
            return;
        }

        // Text_dialoge 찾기
        if (textDialoge == null)
        {
            var textObj = dialogeUIRoot.transform.Find("Panel/Text_dialoge");
            if (textObj != null)
                textDialoge = textObj.GetComponent<TextMeshProUGUI>();
        }

        // Panel_name > Text (TMP) 찾기
        if (textSpeaker == null)
        {
            var namePanel = dialogeUIRoot.transform.Find("Panel_name");
            if (namePanel != null)
            {
                var nameTextObj = namePanel.Find("Text (TMP)");
                if (nameTextObj != null)
                    textSpeaker = nameTextObj.GetComponent<TextMeshProUGUI>();
            }
        }

        // EndDialogueSign 찾기
        if (endDialogueSign == null)
        {
            var signObj = dialogeUIRoot.transform.Find("EndDialogueSign");
            if (signObj != null)
                endDialogueSign = signObj.gameObject;
        }

        // StoryCutScene (Image) 찾기
        if (storyCutscene == null)
        {
            var cutObj = dialogeUIRoot.transform.Find("StoryCutScene");
            if (cutObj != null)
                storyCutscene = cutObj.GetComponent<Image>();
        }

        // Panel > Image (대화창 배경 이미지) 찾기
        if (panelBgImage == null)
        {
            var panelObj = dialogeUIRoot.transform.Find("Panel");
            if (panelObj != null)
                panelBgImage = panelObj.GetComponent<Image>();
        }

        // Canvas 직속 자식들 찾기 (Panel_PlayerInfo, Btn_Pause)
        if (playerInfoPanel == null || pauseButton == null)
        {
            var canvasRoot = dialogeUIRoot.transform.parent;
            if (canvasRoot != null)
            {
                if (playerInfoPanel == null)
                {
                    var infoObj = canvasRoot.Find("Panel_PlayerInfo");
                    if (infoObj != null)
                        playerInfoPanel = infoObj.gameObject;
                }

                if (pauseButton == null)
                {
                    var btnObj = canvasRoot.Find("Btn_Pause");
                    if (btnObj != null)
                        pauseButton = btnObj.gameObject;
                }
            }
        }

        // 자동 탐색 결과 로그
        if (textDialoge == null) Debug.LogWarning("[DialogueUIAdapter] textDialoge를 찾을 수 없습니다.");
        if (textSpeaker == null) Debug.LogWarning("[DialogueUIAdapter] textSpeaker를 찾을 수 없습니다.");
        if (endDialogueSign == null) Debug.LogWarning("[DialogueUIAdapter] endDialogueSign을 찾을 수 없습니다.");
        if (storyCutscene == null) Debug.LogWarning("[DialogueUIAdapter] StoryCutScene(Image)을 찾을 수 없습니다. DialogeUI 자식에 StoryCutScene 오브젝트가 필요합니다.");
        if (panelBgImage == null) Debug.LogWarning("[DialogueUIAdapter] Panel > Image를 찾을 수 없습니다.");
        if (playerInfoPanel == null) Debug.LogWarning("[DialogueUIAdapter] Panel_PlayerInfo를 찾을 수 없습니다.");
        if (pauseButton == null) Debug.LogWarning("[DialogueUIAdapter] Btn_Pause를 찾을 수 없습니다.");
    }

    /// <summary>
    /// Canvas_Ingame 자식에서 DialogeUI 탐색
    /// </summary>
    private GameObject FindDialogeUI()
    {
        Transform parent = transform;
        var diaUI = parent.Find("DialogeUI");
        if (diaUI != null) return diaUI.gameObject;

        if (parent.parent != null)
        {
            diaUI = parent.parent.Find("DialogeUI");
            if (diaUI != null) return diaUI.gameObject;
        }

        var found = GameObject.Find("DialogeUI");
        return found;
    }

    private void ResolveStoryManager()
    {
        if (GameManager.Container != null && GameManager.Container.IsRegistered<IStoryManager>())
        {
            _storyManager = GameManager.Container.Resolve<IStoryManager>();
        }
        else
        {
            Debug.LogError("[DialogueUIAdapter] IStoryManager가 DI 컨테이너에 등록되지 않았습니다.");
        }
    }

    private void SubscribeToEvents()
    {
        StoryEvents.OnDialogueLineChanged += OnDialogueLineChanged;
        StoryEvents.OnCutsceneBackgroundChanged += OnCutsceneBackgroundChanged;
        StoryEvents.OnDialogueEnd += OnDialogueEnd;
    }

    private void OnDestroy()
    {
        StoryEvents.OnDialogueLineChanged -= OnDialogueLineChanged;
        StoryEvents.OnCutsceneBackgroundChanged -= OnCutsceneBackgroundChanged;
        StoryEvents.OnDialogueEnd -= OnDialogueEnd;

        if (_typewriterCoroutine != null)
            StopCoroutine(_typewriterCoroutine);
    }

    // ─── 이벤트 핸들러 ─────────────────────────────────────────

    /// <summary>
    /// 새 대사가 시작될 때 호출됨
    /// </summary>
    private void OnDialogueLineChanged(string speaker, string text)
    {
        _isDialogueActive = true;

        // DialogeUI 활성화
        if (dialogeUIRoot != null)
            dialogeUIRoot.SetActive(true);

        // PlayerInfo UI 숨김
        if (playerInfoPanel != null)
            playerInfoPanel.SetActive(false);

        // 일시정지 버튼 숨김
        if (pauseButton != null)
            pauseButton.SetActive(false);

        // 화자 이름 설정
        if (textSpeaker != null)
            textSpeaker.text = speaker;

        // EndDialogueSign 숨김
        if (endDialogueSign != null)
            endDialogueSign.SetActive(false);

        // 타자기 효과 시작
        _currentFullText = text;
        StartTypewriter(text);
    }

    /// <summary>
    /// 컷씬 배경 이미지가 변경될 때 호출됨
    /// null = 이미지 제거 (대화 종료 시)
    /// not null = 컷씬 모드 전환 (Panel 배경 이미지만 끄고 Text는 유지)
    /// </summary>
    private void OnCutsceneBackgroundChanged(Sprite sprite)
    {
        if (storyCutscene == null)
        {
            Debug.LogWarning("[DialogueUIAdapter] OnCutsceneBackgroundChanged: storyCutscene(Image) 참조가 null입니다.");
            return;
        }
        if (panelBgImage == null)
        {
            Debug.LogWarning("[DialogueUIAdapter] OnCutsceneBackgroundChanged: panelBgImage(Panel > Image) 참조가 null입니다.");
            return;
        }

        if (sprite != null)
        {
            // 컷씬 모드: Panel 배경 이미지만 끄고, 컷씬 이미지 표시 (Text는 유지)
            storyCutscene.sprite = sprite;
            storyCutscene.color = Color.white;
            storyCutscene.gameObject.SetActive(true);
            panelBgImage.enabled = false;

            Debug.Log($"[DialogueUIAdapter] 컷씬 배경 변경: {sprite.name}");
        }
        else
        {
            // 일반 모드: 컷씬 이미지 숨기고, Panel 배경 이미지 복원
            storyCutscene.gameObject.SetActive(false);
            panelBgImage.enabled = true;

            Debug.Log("[DialogueUIAdapter] 컷씬 배경 제거 → 일반 모드");
        }
    }

    /// <summary>
    /// 모든 대화가 종료될 때 호출됨
    /// </summary>
    private void OnDialogueEnd()
    {
        _isDialogueActive = false;

        // 타자기 중단
        if (_typewriterCoroutine != null)
        {
            StopCoroutine(_typewriterCoroutine);
            _typewriterCoroutine = null;
        }

        _isCurrentlyTyping = false;

        // PlayerInfo UI 복원
        if (playerInfoPanel != null)
            playerInfoPanel.SetActive(true);

        // 일시정지 버튼 복원
        if (pauseButton != null)
            pauseButton.SetActive(true);

        // 잔여 Space 입력으로 회피 발동 방지
        if (PlayerInk.Instance != null)
            PlayerInk.Instance.BlockDashInputTemporarily(0.3f);

        // DialogeUI 비활성화
        if (dialogeUIRoot != null)
            dialogeUIRoot.SetActive(false);
    }

    // ─── 타자기 효과 ────────────────────────────────────────────

    private void StartTypewriter(string text)
    {
        if (_typewriterCoroutine != null)
            StopCoroutine(_typewriterCoroutine);

        _isCurrentlyTyping = true;
        _typewriterCoroutine = StartCoroutine(TypewriterRoutine(text));
    }

    private IEnumerator TypewriterRoutine(string fullText)
    {
        if (textDialoge == null) yield break;

        textDialoge.text = "";
        int length = fullText.Length;

        for (int i = 0; i < length; i++)
        {
            char c = fullText[i];
            textDialoge.text += c;

            if (c == '.' || c == '?' || c == '!' || c == ',')
            {
                yield return new WaitForSecondsRealtime(punctuationDelay);
            }
            else
            {
                yield return new WaitForSecondsRealtime(charDelay);
            }

            if (!_isCurrentlyTyping)
                yield break;
        }

        _isCurrentlyTyping = false;
        _typewriterCoroutine = null;

        if (endDialogueSign != null)
            endDialogueSign.SetActive(true);

        StoryEvents.InvokeLineFullyRevealed();
    }

    // ─── 입력 처리 ─────────────────────────────────────────────

    private void Update()
    {
        if (!_isDialogueActive) return;

        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
        {
            HandleAdvanceInput();
        }
    }

    private void HandleAdvanceInput()
    {
        if (_isCurrentlyTyping)
        {
            SkipToFullText();
        }
        else
        {
            _storyManager?.NextLine();
        }
    }

    private void SkipToFullText()
    {
        if (_typewriterCoroutine != null)
        {
            StopCoroutine(_typewriterCoroutine);
            _typewriterCoroutine = null;
        }

        _isCurrentlyTyping = false;

        if (textDialoge != null)
            textDialoge.text = _currentFullText;

        if (endDialogueSign != null)
            endDialogueSign.SetActive(true);

        StoryEvents.InvokeLineFullyRevealed();
    }
}
