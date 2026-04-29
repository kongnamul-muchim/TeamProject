using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace HideAndInk.Core.Transition
{
    /// <summary>
    /// 패턴 트랜지션 셰이더를 제어하는 컨트롤러.
    /// Shader_PatternTransition 프리팹에 부착하여 사용합니다.
    /// 
    /// 사용 방법:
    /// 1. Shader_PatternTransition 프리팝에 이 스크립트를 부착
    /// 2. RawImage를 인스펙터에 드래그 할당
    /// 3. 다른 스크립트에서 PatternTransitionController.Instance.PlayIn/PlayOut 호출
    /// 
    /// 예시:
    ///   PatternTransitionController.Instance.PlayIn();      // 화면 덮기
    ///   PatternTransitionController.Instance.PlayOut();       // 화면 걷기
    ///   PatternTransitionController.Instance.PlayIn(() => { SceneManager.LoadScene("NextScene"); });
    /// </summary>
    public class PatternTransitionController : MonoBehaviour
    {
        private static PatternTransitionController _instance;
        public static PatternTransitionController Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<PatternTransitionController>();
                }
                return _instance;
            }
        }

        [Header("References")]
        [SerializeField] private RawImage _rawImage;

        [Header("Transition Settings")]
        [Tooltip("트랜지션 진행 시간 (초)")]
        [SerializeField] private float _duration = 1.0f;

        [Tooltip("진행 곡선 (0→1)")]
        [SerializeField] private AnimationCurve _curve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(1f, 1f)
        );

        [Header("Shader Properties")]
        [SerializeField] private string _progressProperty = "_Progress";

        private Material _material;
        private Coroutine _currentTransition;

        /// <summary>현재 트랜지션이 진행 중인지 여부</summary>
        public bool IsPlaying => _currentTransition != null;

        /// <summary>현재 Progress 값 (0 = 투명, 1 = 완전 덮임)</summary>
        public float Progress
        {
            get => _material != null ? _material.GetFloat(_progressProperty) : 0f;
            set { if (_material != null) _material.SetFloat(_progressProperty, value); }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }
            _instance = this;

            if (_rawImage == null)
            {
                _rawImage = GetComponentInChildren<RawImage>();
            }

            if (_rawImage != null)
            {
                _material = _rawImage.material;
            }

            // 초기 상태: 투명 (Progress = 0)
            if (_material != null)
            {
                _material.SetFloat(_progressProperty, 0f);
            }
        }

        private void OnDestroy()
        {
            if (_currentTransition != null)
            {
                StopCoroutine(_currentTransition);
                _currentTransition = null;
            }

            if (_instance == this)
            {
                _instance = null;
            }
        }

        /// <summary>
        /// 화면 덮기 트랜지션 (Progress 0 → 1)
        /// 셰이더 패턴이 화면을 점점 덮습니다.
        /// </summary>
        /// <param name="onComplete">트랜지션 완료 후 호출될 콜백</param>
        public void PlayIn(System.Action onComplete = null)
        {
            if (_material == null) return;

            if (_currentTransition != null)
            {
                StopCoroutine(_currentTransition);
            }

            _currentTransition = StartCoroutine(TransitionRoutine(0f, 1f, onComplete));
        }

        /// <summary>
        /// 화면 걷기 트랜지션 (Progress 1 → 0)
        /// 셰이더 패턴이 화면에서 점점 사라집니다.
        /// </summary>
        /// <param name="onComplete">트랜지션 완료 후 호출될 콜백</param>
        public void PlayOut(System.Action onComplete = null)
        {
            if (_material == null) return;

            if (_currentTransition != null)
            {
                StopCoroutine(_currentTransition);
            }

            _currentTransition = StartCoroutine(TransitionRoutine(1f, 0f, onComplete));
        }

        /// <summary>
        /// 즉시 화면 덮기 (애니메이션 없이 Progress = 1)
        /// </summary>
        public void SetFull()
        {
            if (_currentTransition != null)
            {
                StopCoroutine(_currentTransition);
                _currentTransition = null;
            }
            Progress = 1f;
        }

        /// <summary>
        /// 즉시 투명 (애니메이션 없이 Progress = 0)
        /// </summary>
        public void SetClear()
        {
            if (_currentTransition != null)
            {
                StopCoroutine(_currentTransition);
                _currentTransition = null;
            }
            Progress = 0f;
        }

        /// <summary>
        /// 커스텀 시작/끝 값으로 트랜지션 실행
        /// </summary>
        /// <param name="from">시작 Progress 값</param>
        /// <param name="to">끝 Progress 값</param>
        /// <param name="onComplete">완료 콜백</param>
        public void PlayCustom(float from, float to, System.Action onComplete = null)
        {
            if (_material == null) return;

            if (_currentTransition != null)
            {
                StopCoroutine(_currentTransition);
            }

            _currentTransition = StartCoroutine(TransitionRoutine(from, to, onComplete));
        }

        private IEnumerator TransitionRoutine(float from, float to, System.Action onComplete)
        {
            float elapsed = 0f;

            while (elapsed < _duration)
            {
                // unscaledDeltaTime 사용: 씬 로딩 중에도 작동
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / _duration);
                float progress = Mathf.Lerp(from, to, _curve.Evaluate(t));
                _material.SetFloat(_progressProperty, progress);
                yield return null;
            }

            _material.SetFloat(_progressProperty, to);
            _currentTransition = null;
            onComplete?.Invoke();
        }
    }
}