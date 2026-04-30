using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HideAndInk.Core.Transition
{
    /// <summary>
    /// FadeInObj 프리팹을 이용한 씬/구역 전환 화면 덮기 컨트롤러.
    /// DontDestroyOnLoad 싱글톤으로 씬 간에 유지됩니다.
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
                }
                return _instance;
            }
        }

        [Header("Fade In 프리팹")]
        [Tooltip("화면을 덮을 FadeInObj 프리팹 (Canvas 기반)")]
        [SerializeField] private GameObject fadeInPrefab;

        [Header("애니메이션 설정")]
        [Tooltip("Transition_Enter.anim 클립의 재생 길이 (초)")]
        [SerializeField] private float transitionDuration = 1.1333333f;

        private GameObject _fadeInObject;
        private Animator _animator;
        private Coroutine _currentTransition;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeFadeInObject();
            SceneManager.sceneLoaded += OnSceneLoaded;
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
        /// 프리팹을 인스턴스화하고 초기 상태를 설정합니다.
        /// </summary>
        private void InitializeFadeInObject()
        {
            if (fadeInPrefab == null)
            {
                Debug.LogError("[FadeInTransitionController] fadeInPrefab이 Inspector에 할당되지 않았습니다.");
                return;
            }

            _fadeInObject = Instantiate(fadeInPrefab, transform);
            _fadeInObject.SetActive(false);
            _animator = _fadeInObject.GetComponent<Animator>();

            if (_animator == null)
            {
                Debug.LogWarning("[FadeInTransitionController] 프리팹에 Animator가 없습니다.");
            }
        }

        /// <summary>
        /// 씬 로드 완료 시 자동으로 화면을 엽니다.
        /// </summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_fadeInObject != null && _fadeInObject.activeSelf)
            {
                PlayOut();
            }
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

            _fadeInObject.SetActive(true);
            _animator?.Play("Transition_Enter", 0, 0f);

            _currentTransition = StartCoroutine(TransitionInRoutine(onComplete));
        }

        private IEnumerator TransitionInRoutine(System.Action onComplete)
        {
            yield return new WaitForSeconds(transitionDuration);
            _currentTransition = null;
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
        }

        /// <summary>
        /// 현재 트랜지션이 진행 중인지 여부
        /// </summary>
        public bool IsPlaying => _currentTransition != null;
    }
}
