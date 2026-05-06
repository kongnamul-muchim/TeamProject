using UnityEngine;
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
        // Awake에서 구독: Start보다 먼저 등록되어 이벤트를 놓치지 않음
        SubscribeToEvents();

        // 시작 시 DialogeUI 비활성화
        if (dialogeUIRoot != null)
            dialogeUIRoot.SetActive(false);
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

        // 자동 탐색 결과 로그
        if (textDialoge == null) Debug.LogWarning("[DialogueUIAdapter] textDialoge를 찾을 수 없습니다.");
        if (textSpeaker == null) Debug.LogWarning("[DialogueUIAdapter] textSpeaker를 찾을 수 없습니다.");
        if (endDialogueSign == null) Debug.LogWarning("[DialogueUIAdapter] endDialogueSign을 찾을 수 없습니다.");
    }

    /// <summary>
    /// Canvas_Ingame 자식에서 DialogeUI 탐색
    /// </summary>
    private GameObject FindDialogeUI()
    {
        Transform parent = transform;
        // DialogeUI는 Canvas 바로 아래나 현재 오브젝트 자식에 위치
        var diaUI = parent.Find("DialogeUI");
        if (diaUI != null) return diaUI.gameObject;

        // 한 단계 더 위에서 찾기
        if (parent.parent != null)
        {
            diaUI = parent.parent.Find("DialogeUI");
            if (diaUI != null) return diaUI.gameObject;
        }

        // 전체 씬에서 찾기
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
        StoryEvents.OnDialogueEnd += OnDialogueEnd;
    }

    private void OnDestroy()
    {
        StoryEvents.OnDialogueLineChanged -= OnDialogueLineChanged;
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

        // DialogeUI 비활성화
        if (dialogeUIRoot != null)
            dialogeUIRoot.SetActive(false);
    }

    // ─── 타자기 효과 ────────────────────────────────────────────

    private void StartTypewriter(string text)
    {
        // 이전 코루틴 중단
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

            // 문장 부호는 약간 더 길게 대기
            // '...'(말줄임표)는 개별 '.'가 각각 매칭되므로 별도 처리 불필요
            if (c == '.' || c == '?' || c == '!' || c == ',')
            {
                yield return new WaitForSecondsRealtime(punctuationDelay);
            }
            else
            {
                yield return new WaitForSecondsRealtime(charDelay);
            }

            // 타자기가 종료되었으면 즉시 중단
            if (!_isCurrentlyTyping)
                yield break;
        }

        // 타자기 완료
        _isCurrentlyTyping = false;
        _typewriterCoroutine = null;

        // EndDialogueSign 표시 ("이어 하기")
        if (endDialogueSign != null)
            endDialogueSign.SetActive(true);

        // 전체 출력 완료 이벤트
        StoryEvents.InvokeLineFullyRevealed();
    }

    // ─── 입력 처리 ─────────────────────────────────────────────

    private void Update()
    {
        if (!_isDialogueActive) return;

        // 클릭 또는 Space/Enter 입력 감지
        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
        {
            HandleAdvanceInput();
        }
    }

    /// <summary>
    /// 대사 진행 입력 처리
    /// - 타자기 중이면 → 전체 문장 즉시 표시
    /// - 타자기 완료면 → 다음 대사로 진행
    /// </summary>
    private void HandleAdvanceInput()
    {
        if (_isCurrentlyTyping)
        {
            // 타자기 즉시 완료
            SkipToFullText();
        }
        else
        {
            // 다음 대사 또는 종료
            _storyManager?.NextLine();
        }
    }

    /// <summary>
    /// 현재 대사를 전체 표시로 스킵
    /// </summary>
    private void SkipToFullText()
    {
        if (_typewriterCoroutine != null)
        {
            StopCoroutine(_typewriterCoroutine);
            _typewriterCoroutine = null;
        }

        _isCurrentlyTyping = false;

        // 전체 텍스트 즉시 표시
        if (textDialoge != null)
            textDialoge.text = _currentFullText;

        // EndDialogueSign 표시
        if (endDialogueSign != null)
            endDialogueSign.SetActive(true);

        // 전체 출력 완료 이벤트
        StoryEvents.InvokeLineFullyRevealed();
    }
}
