using HideAndInk.Core.Audio;
using HideAndInk.Core.Interfaces;
using HideAndInk.Core.Managers;
using HideAndInk.Core.Transition;
using HideAndInk.Scripts.Save;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HideAndInk.Scripts.UI
{
    /// <summary>
    /// 타이틀 화면 전체를 제어하는 컨트롤러.
    /// Canvas 오브젝트에 부착하여 사용합니다.
    /// 
    /// 연결 가이드 (Inspector):
    /// - popupSetting: Popup_Setting 오브젝트
    /// - btnContinue: Btn_Continue (Button)
    /// - btnContinueText: Btn_Continue의 Text (TMP) — 저장 유무에 따라 색상 변경
    /// - fadeExit: FadeInObjController (FadeInObj 루트, 출구 전환용)
    /// 
    /// 버튼 OnClick 연결:
    /// - Btn_NewGame  → TitleController.OnNewGameClicked
    /// - Btn_Continue → TitleController.OnContinueClicked
    /// - Btn_ExitGame → TitleController.OnExitGameClicked
    /// - Btn_Setting  → TitleController.OnSettingClicked
    /// - Btn_ExitPopup → TitleController.OnExitPopupClicked
    /// 
    /// 진입 트랜지션:
    /// - useEntryTransition = true: PatternTransitionController.PlayOut() (셰이더)
    /// 
    /// 출구 트랜지션 (2페이즈 분할 재생):
    /// useFadeExit=true:
    ///   Phase 1 (타이틀 씬): PlayCover(30프레임 역재생) → 화면 덮는 중
    ///                          ↓ DontDestroyOnLoad + SceneManager.LoadScene
    ///   Phase 2 (게임 씬):   PlayReveal(30프레임 정재생) → 화면 열림 → 자동 Destroy
    /// 
    /// fallback: useSceneTransition=true → Shader PatternTransition
    /// </summary>
    public sealed class TitleController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("설정 팝업 오브젝트 (Popup_Setting)")]
        [SerializeField] private GameObject popupSetting;

        [Tooltip("이어하기 버튼 (저장 데이터 유무에 따라 interactable 제어)")]
        [SerializeField] private Button btnContinue;

        [Tooltip("이어하기 버튼의 텍스트 (색상 변경용)")]
        [SerializeField] private TMPro.TextMeshProUGUI btnContinueText;

        [Tooltip("FadeInObjController (출구 전환 - 화면 덮기, FadeInObj 프리팹 루트에 부착)")]
        [SerializeField] private FadeInObjController fadeExit;

        [Header("Transition Settings")]
        [Tooltip("씬 진입 시 PatternTransitionController로 PlayOut (권장)")]
        [SerializeField] private bool useEntryTransition = true;

        [Tooltip("씬 전환 시 Shader_PatternTransition 사용")]
        [SerializeField] private bool useSceneTransition = true;

        [Tooltip("씬 전환 시 FadeInObjController.PlayExit() 사용 (60프레임 역재생)")]
        [SerializeField] private bool useFadeExit = true;

        [Header("Scene Config")]
        [Tooltip("새 게임 / 이어하기 시작 시 로드할 씬의 Build Index (기본: 1)")]
        [SerializeField] private int newGameSceneIndex = 1;

        [Tooltip("Continue 로드할 씬의 Build Index (기본: 1)")]
        [SerializeField] private int continueSceneIndex = 1;

        private ISfxService _sfxService;
        private PatternTransitionController _transition;
        private bool _isTransitioning = false;

        private void Awake()
        {
            // 팝업 초기 상태: 닫힘
            if (popupSetting != null)
                popupSetting.SetActive(false);

            // SFX 서비스 해결
            if (GameManager.Container != null && GameManager.Container.IsRegistered<ISfxService>())
            {
                _sfxService = GameManager.Container.Resolve<ISfxService>();
            }
        }

        private void Start()
        {
            // PatternTransitionController 인스턴스 캐싱
            _transition = PatternTransitionController.Instance;

            // 이어하기 버튼 상태 업데이트
            UpdateContinueButton();

            // FadeInObj 트랜지션으로 진입 중이면 entry transition 스킵 (SettingsPopup에서 이미 처리함)
            if (SettingsPopup.IsFadeInTransitionActive)
            {
                Debug.Log("[TitleController] FadeInObj 트랜지션 진입 감지 → entry transition 스킵");
                SettingsPopup.IsFadeInTransitionActive = false;
            }
            else
            {
                // 씬 진입 트랜지션 실행
                StartCoroutine(PlayEntryTransition());
            }
        }

        /// <summary>
        /// 저장 데이터 유무에 따라 이어하기 버튼 상태를 업데이트합니다.
        /// </summary>
        private void UpdateContinueButton()
        {
            bool hasSave = SaveManager.HasSaveData();
            Debug.Log($"[TitleController] 저장 데이터 존재: {hasSave}");

            if (btnContinue != null)
                btnContinue.interactable = hasSave;

            if (btnContinueText != null)
                btnContinueText.color = hasSave ? Color.white : Color.gray;
        }

        /// <summary>
        /// 씬 진입 시 트랜지션 효과를 재생합니다.
        /// useEntryTransition + FadeInObj 둘 다 설정 가능 (순차 재생).
        /// </summary>
        private IEnumerator PlayEntryTransition()
        {
            // 1프레임 대기 (모든 컴포넌트 초기화 완료)
            yield return null;

            // ---- Phase 1: PatternTransitionController 셰이더 트랜지션 ----
            if (useEntryTransition && _transition != null)
            {
                // 씬 로드 중에 이미 보일 수 있으므로 즉시 덮고 시작
                _transition.SetFull();
                yield return null;

                // 화면 걷기 (0.5 → 1.0)
                _transition.PlayOut();

                // 트랜지션 완료 대기 (duration + 여유)
                float waitTime = (_transition != null) ? 0.6f : 0f;
                yield return new WaitForSecondsRealtime(waitTime);
            }

            // ---- Phase 2: (FadeInObj 출구 전환 제거)
            // FadeInObj는 PlayEntryTransition(진입)에서 재생하지 않음.
            // NewGame/Continue 버튼 → StartSceneTransition → FadeInObjController.PlayExit()로만 동작
            // (useFadeExit + fadeExit 참조)
        }

        // =====================================================
        // Button Callbacks (Inspector OnClick 연결)
        // =====================================================

        /// <summary>
        /// Btn_NewGame → 인스펙터 OnClick 연결
        /// 세이브 데이터를 초기화하고 새 게임을 시작합니다.
        /// 우선순위: FadeInObjController 출구 > PatternTransitionController > 바로 로드
        /// </summary>
        public void OnNewGameClicked()
        {
            if (_isTransitioning) return;
            _isTransitioning = true;
            _sfxService?.Play(SfxId.ButtonClick2);

            // 저장 데이터 초기화
            SaveManager.DeleteSave();
            SaveManager.ClearContinueZone();

            StartSceneTransition(newGameSceneIndex);
        }

        /// <summary>
        /// Btn_Continue → 인스펙터 OnClick 연결
        /// 저장된 데이터가 있을 경우 해당 Zone으로 씬 전환.
        /// </summary>
        public void OnContinueClicked()
        {
            if (_isTransitioning) return;
            _sfxService?.Play(SfxId.ButtonClick2);

            if (!SaveManager.HasSaveData())
            {
                Debug.Log("[TitleController] 저장된 데이터가 없습니다. Continue 불가.");
                return;
            }

            _isTransitioning = true;

            // 저장된 데이터에서 마지막 Zone 인덱스 로드
            SaveData data = SaveManager.Load();
            if (data == null)
            {
                _isTransitioning = false;
                return;
            }

            SaveManager.SetContinueZone(data.lastZoneIndex, data.GetPlayerPosition(), data.GetSquidPosition());
            StartSceneTransition(continueSceneIndex);
        }

        /// <summary>
        /// 출구 트랜지션 후 씬을 로드합니다.
        /// 
        /// 1순위: FadeInObjController.PlayCoverAndTransition()
        ///   → 내부에서 DontDestroyOnLoad + 씬 로드 + 자동 Phase2 Reveal 처리
        /// 2순위: Shader PatternTransition
        /// 3순위: 즉시 로드
        /// </summary>
        private void StartSceneTransition(int sceneIndex)
        {
            // 1순위: FadeInObjController 풀 시퀀스
            if (useFadeExit && fadeExit != null)
            {
                fadeExit.PlayCoverAndTransition(sceneIndex);
            }
            // 2순위: Shader PatternTransition
            else if (useSceneTransition && _transition != null)
            {
                _transition.PlayIn(() =>
                {
                    DontDestroyOnLoad(_transition.gameObject);

                    SceneManager.sceneLoaded += OnSceneLoaded;
                    SceneManager.LoadScene(sceneIndex);
                });
            }
            // 3순위: 바로 로드
            else
            {
                SceneManager.LoadScene(sceneIndex);
            }
        }

        /// <summary>
        /// Btn_ExitGame → 인스펙터 OnClick 연결
        /// </summary>
        public void OnExitGameClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>
        /// Btn_Setting → 인스펙터 OnClick 연결
        /// </summary>
        public void OnSettingClicked()
        {
            _sfxService?.Play(SfxId.ButtonClick2);
            if (popupSetting != null)
            {
                bool isActive = popupSetting.activeSelf;
                popupSetting.SetActive(!isActive);
            }
        }

        /// <summary>
        /// Btn_ExitPopup → 인스펙터 OnClick 연결
        /// </summary>
        public void OnExitPopupClicked()
        {
            _sfxService?.Play(SfxId.ButtonClick2);
            if (popupSetting != null)
                popupSetting.SetActive(false);
        }

        // =====================================================
        // Scene Transition Callbacks
        // =====================================================

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;

            if (_transition != null)
            {
                _transition.PlayOut();
            }

            _isTransitioning = false;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }
}
