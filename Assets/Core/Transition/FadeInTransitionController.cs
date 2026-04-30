using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HideAndInk.Core.Transition
{
    /// <summary>
    /// FadeInObj 프리팹을 이용한 씬/구역 전환 화면 덮기 컨트롤러.
    /// DontDestroyOnLoad 싱글톤으로 씬 간에 유지됩니다.
    /// 씬에 배치하지 않아도 코드에서 자동으로 생성됩니다.
    /// </summary>
    public class FadeInTransitionController : MonoBehaviour
    {
        private static FadeInTransitionController _instance;
        public static FadeInTransitionController Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<FadeInTransitionController>();
                    if (_instance == null)
                    {
                        // 씬에 없으면 자동 생성
                        GameObject go = new GameObject("FadeInTransitionController");
                        _instance = go.AddComponent<FadeInTransitionController>();
                        _instance.InitializeFadeInObject();
                        SceneManager.sceneLoaded += _instance.OnSceneLoaded;
                        DontDestroyOnLoad(go);
                        Debug.Log("[FadeInTransitionController] 씬에 없어서 자동 생성했습니다.");
                    }
                }
                return _instance;
            }
        }

        [Header("Fade In 프리팹")]
        [Tooltip("화면을 덮을 FadeInObj 프리팹 (Canvas 기반). 비워두면 Resources/Prefabs/FadeInObj 에서 자동 로드")]
        [SerializeField] private GameObject fadeInPrefab;

        [Header("애니메이션 설정")]
        [Tooltip("Transition_Enter.anim 클립의 재생 길이 (초)")]
        [SerializeField] private float transitionDuration = 1.1333333f;

        [Header("디버그")]
        [Tooltip("디버그 로그 출력 여부")]
        [SerializeField] private bool showDebugLogs = true;

        private GameObject _fadeInObject;
        private Animator _animator;
        private Coroutine _currentTransition;
        private bool _initialized = false;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeFadeInObject();
                SceneManager.sceneLoaded += OnSceneLoaded;
            }
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_instance == this)
            {
                _instance = null;
            }
        }

        /// <summary>
        /// 프리팹을 인스턴스화하고 Canvas를 동적으로 생성하여 초기 상태를 설정합니다.
        /// </summary>
        public void InitializeFadeInObject()
        {
            if (_initialized) return;
            _initialized = true;

            // 1. 프리팹 확보 (Inspector > Resources.Load)
            if (fadeInPrefab == null)
            {
                fadeInPrefab = Resources.Load<GameObject>("Prefabs/FadeInObj");
                if (fadeInPrefab == null)
                {
                    Debug.LogError("[FadeInTransitionController] fadeInPrefab이 Inspector에도, Resources/Prefabs/FadeInObj 에도 없습니다.");
                    return;
                }
                if (showDebugLogs) Debug.Log("[FadeInTransitionController] Resources에서 FadeInObj 프리팹을 자동 로드했습니다.");
            }

            // 2. Canvas 생성 (ScreenSpaceOverlay, 최상단)
            GameObject canvasGO = new GameObject("FadeInCanvas");
            canvasGO.transform.SetParent(transform, false);
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;
            canvasGO.AddComponent<GraphicRaycaster>();

            // CanvasScaler 추가 (화면 비율 대응)
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // 3. FadeInObj를 Canvas의 자식으로 생성
            _fadeInObject = Instantiate(fadeInPrefab, canvasGO.transform, false);
            _fadeInObject.SetActive(false);

            // RectTransform을 전체 화면으로 스트레치
            var rectTransform = _fadeInObject.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.offsetMin = Vector2.zero;
                rectTransform.offsetMax = Vector2.zero;
                rectTransform.localScale = Vector3.one;
            }

            _animator = _fadeInObject.GetComponent<Animator>();

            if (_animator == null)
            {
                Debug.LogWarning("[FadeInTransitionController] 프리팹에 Animator가 없습니다.");
            }
            else
            {
                if (showDebugLogs) Debug.Log("[FadeInTransitionController] 초기화 완료. FadeInObj가 Canvas 아래에 생성되었습니다.");
            }
        }

        /// <summary>
        /// 씬 로드 완료 시 자동으로 화면을 엽니다.
        /// </summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_fadeInObject != null && _fadeInObject.activeSelf)
            {
                // 씬 로드 직후 다른 Canvas/UI가 초기화될 시간을 약간 주고 PlayOut
                StartCoroutine(DelayedPlayOut(0.1f));
            }
        }

        private IEnumerator DelayedPlayOut(float delay)
        {
            yield return new WaitForSeconds(delay);
            PlayOut();
        }

        /// <summary>
        /// 화면을 덮는 트랜지션을 재생합니다.
        /// 애니메이션 종료 후 onComplete 콜백이 실행됩니다.
        /// </summary>
        /// <param name="onComplete">화면이 완전히 덮인 후 실행할 콜백</param>
        public void PlayIn(System.Action onComplete = null)
        {
            if (_fadeInObject == null)
            {
                Debug.LogWarning("[FadeInTransitionController] PlayIn 실패: fadeInObject가 초기화되지 않았습니다.");
                onComplete?.Invoke();
                return;
            }

            if (_currentTransition != null)
            {
                StopCoroutine(_currentTransition);
            }

            if (showDebugLogs) Debug.Log("[FadeInTransitionController] PlayIn 시작 - 화면 덮기");

            _fadeInObject.SetActive(true);

            // Animator가 활성화된 후 1프레임 뒤에 애니메이션 재생 (초기화 타이밍 문제 방지)
            _currentTransition = StartCoroutine(PlayInRoutine(onComplete));
        }

        private IEnumerator PlayInRoutine(System.Action onComplete)
        {
            // Animator 초기화 대기
            yield return null;

            if (_animator != null)
            {
                _animator.Play("Transition_Enter", 0, 0f);
                if (showDebugLogs) Debug.Log("[FadeInTransitionController] Transition_Enter 애니메이션 재생");
            }
            else
            {
                Debug.LogWarning("[FadeInTransitionController] Animator가 null입니다.");
            }

            yield return new WaitForSeconds(transitionDuration);
            _currentTransition = null;

            if (showDebugLogs) Debug.Log("[FadeInTransitionController] PlayIn 완료 - 콜백 실행");
            onComplete?.Invoke();
        }

        /// <summary>
        /// 화면을 열어(페이드 아웃) 컨텐츠를 노출합니다.
        /// Exit 애니메이션이 없으므로 즉시 비활성화합니다.
        /// </summary>
        public void PlayOut()
        {
            if (_fadeInObject == null) return;

            if (_currentTransition != null)
            {
                StopCoroutine(_currentTransition);
                _currentTransition = null;
            }

            _fadeInObject.SetActive(false);
            if (showDebugLogs) Debug.Log("[FadeInTransitionController] PlayOut - 화면 열기");
        }

        /// <summary>
        /// 현재 트랜지션이 진행 중인지 여부
        /// </summary>
        public bool IsPlaying => _currentTransition != null;
    }
}
