using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace HideAndInk.Core.Transition
{
    /// <summary>
    /// 프랙탈 노이즈 트랜지션 셰이더를 제어하는 컨트롤러.
    /// Shader_PatternTransition 프리팹에 부착하여 사용합니다.
    /// 
    /// 사용 방법:
    /// 1. Shader_PatternTransition 프리팝에 이 스크립트를 부착
    /// 2. RawImage를 인스펙터에 드래그 할당
    /// 3. 다른 스크립트에서 PatternTransitionController.Instance.PlayIn/PlayOut 호출
    /// 
    /// 예시:
    ///   PatternTransitionController.Instance.PlayIn();      // 화면 덮기
    ///   PatternTransitionController.Instance.PlayOut();      // 화면 걷기
    ///   PatternTransitionController.Instance.PlayIn(() => { SceneManager.LoadScene("NextScene"); });
    /// 
    /// Progress 값의 의미:
    ///   0.0 = 완전 투명 (효과 없음)
    ///   0.5 = 완전 덮임 (화면 가림)
    ///   1.0 = 다시 투명 (효과 없음)
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
        [Tooltip("트랜지션 진행 시간 (초) - PlayIn/PlayOut 각각의 시간")]
        [SerializeField] private float _duration = 0.5f;

        [Tooltip("진행 곡선 (0→1)")]
        [SerializeField] private AnimationCurve _curve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(1f, 1f)
        );

        [Header("Shader Properties")]
        [SerializeField] private string _progressProperty = "_Progress";
        [SerializeField] private string _seedProperty = "_Seed";

        [Header("Visual Settings")]
        [Tooltip("노이즈 애니메이션 속도")]
        [SerializeField] private float _speed = 0.1f;
        [Tooltip("픽셀화 크기 (작을수록 더 픽셀화됨)")]
        [SerializeField] private Vector2 _pixelation = new Vector2(2f, 2f);
        [Tooltip("노이즈 줌 레벨")]
        [SerializeField] private float _zoom = 2f;
        [Tooltip("트랜지션 색상 (이미지 없을 때 단색으로 사용, 이미지 있을 때 틴트 역할)")]
        [SerializeField] private Color _color = Color.black;
        [Tooltip("트랜지션 이미지 (None이면 _Color 단색 사용)")]
        [SerializeField] private Texture2D _transitionImage;

        private Material _material;
        private Coroutine _currentTransition;

        /// <summary>현재 트랜지션이 진행 중인지 여부</summary>
        public bool IsPlaying => _currentTransition != null;

        /// <summary>현재 Progress 값 (0 = 투명, 0.5 = 완전 덮임, 1.0 = 투명)</summary>
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
            ApplyVisualSettings();
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
        /// 셰이더의 시각 설정을 현재 인스펙터 값으로 동기화합니다.
        /// </summary>
private void ApplyVisualSettings()
        {
            if (_material == null) return;

            _material.SetFloat("_Speed", _speed);
            _material.SetVector("_Pixelation", _pixelation);
            _material.SetFloat("_Zoom", _zoom);
            _material.SetColor("_Color", _color);
            if (_transitionImage != null)
            {
                _material.SetTexture("_TransitionImage", _transitionImage);
            }
        }

        /// <summary>
        /// 화면 덮기 트랜지션 (Progress 0 → 0.5)
        /// 프랙탈 노이즈가 대각선으로 화면을 덮습니다.
        /// </summary>
        /// <param name="onComplete">트랜지션 완료 후 호출될 콜백</param>
        public void PlayIn(System.Action onComplete = null)
        {
            if (_material == null) return;

            if (_currentTransition != null)
            {
                StopCoroutine(_currentTransition);
            }

            // 새 트랜지션마다 다른 노이즈 패턴을 위해 시드 랜덤화
            _material.SetFloat(_seedProperty, Random.value);

            // 시각 설정 동기화
            ApplyVisualSettings();

            _currentTransition = StartCoroutine(TransitionRoutine(0f, 0.5f, onComplete));
        }

        /// <summary>
        /// 화면 걷기 트랜지션 (Progress 0.5 → 1.0)
        /// 프랙탈 노이즈가 대각선으로 화면에서 사라집니다.
        /// </summary>
        /// <param name="onComplete">트랜지션 완료 후 호출될 콜백</param>
        public void PlayOut(System.Action onComplete = null)
        {
            if (_material == null) return;

            if (_currentTransition != null)
            {
                StopCoroutine(_currentTransition);
            }

            _currentTransition = StartCoroutine(TransitionRoutine(0.5f, 1.0f, onComplete));
        }

        /// <summary>
        /// 즉시 화면 덮기 (애니메이션 없이 Progress = 0.5)
        /// </summary>
        public void SetFull()
        {
            if (_currentTransition != null)
            {
                StopCoroutine(_currentTransition);
                _currentTransition = null;
            }
            Progress = 0.5f;
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