using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HideAndInk.Scripts.UI
{
    /// <summary>
    /// FadeInObj 프리팹을 제어하는 컨트롤러.
    /// 
    /// [2페이즈 정방향 분할 재생]
    /// Phase 1 (타이틀 씬): Transition_Enter 정방향 재생 (coverFrameCount 프레임)
    ///   → DontDestroyOnLoad를 위해 독립 Canvas 생성 + 부모 분리
    /// Phase 2 (게임 씬):   Transition_Enter 정방향 이어재생 → 완료 후 비활성화
    /// 
    /// 사용법:
    /// 1. FadeInObj 프리팹 루트에 이 컴포넌트를 추가
    /// 2. Hierarchy에서 기본 비활성화 (타이틀 Canvas 자식)
    /// 3. TitleController에서 fadeExit.PlayCoverAndTransition(sceneIndex) 호출
    /// </summary>
    public sealed class FadeInObjController : MonoBehaviour
    {
        [Header("Timing")]
        [Tooltip("Phase1 (씬 전환 전) 재생 프레임 수")]
        [SerializeField] private int coverFrameCount = 30;

        [Tooltip("Phase2 (씬 전환 후) 재생 프레임 수")]
        [SerializeField] private int revealFrameCount = 30;

        [Tooltip("애니메이터 스테이트 이름 (기본: Transition_Enter)")]
        [SerializeField] private string animationStateName = "Transition_Enter";

        private Animator _animator;
        private AnimationClip _clip;
        private bool _isPlaying;
        private float _savedNormalizedTime;
        private Canvas _standaloneCanvas;

        private void Awake()
        {
            _animator = GetComponent<Animator>();

            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                var clips = _animator.runtimeAnimatorController.animationClips;
                if (clips != null && clips.Length > 0)
                {
                    _clip = clips[0];
                }
            }

            // 비활성 상태는 Hierarchy에서 유저가 직접 설정
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoadedForReveal;
        }

        // =====================================================
        // Phase 1 → Scene Load → Phase 2 (올인원)
        // =====================================================

        /// <summary>
        /// 풀 시퀀스 실행:
        ///   정방향 재생(coverFrameCount) → 독립 Canvas로 분리 → DontDestroyOnLoad → LoadScene
        ///   → (새 씬) 이어재생(revealFrameCount) → 비활성화
        /// </summary>
        public void PlayCoverAndTransition(int sceneIndex)
        {
            if (_isPlaying) return;
            _isPlaying = true;

            if (_animator != null)
                _animator.enabled = true;

            gameObject.SetActive(true);
            StartCoroutine(CoverAndTransitionRoutine(sceneIndex));
        }

        private IEnumerator CoverAndTransitionRoutine(int sceneIndex)
        {
            if (_animator == null || _clip == null)
            {
                Debug.LogWarning("[FadeInObjController] Animator or clip missing. Loading scene directly.");
                SceneManager.LoadScene(sceneIndex);
                yield break;
            }

            // ---- 독립 Canvas 생성 (Phase 1 시작 전에 해야 UI Image가 렌더링됨) ----
            CreateStandaloneCanvas();
            transform.SetParent(_standaloneCanvas.transform, worldPositionStays: false);
            DontDestroyOnLoad(_standaloneCanvas.gameObject);

            // speed 0으로 고정 + t=0에서 시작
            _animator.speed = 0f;
            _animator.Play(animationStateName, 0, 0f);

            yield return null;

            // ---- Phase 1: 정방향 재생 (normalized 0.0 → coverRatio) ----
            int totalFrames = coverFrameCount + revealFrameCount;
            float coverRatio = (float)coverFrameCount / totalFrames;

            for (int i = 1; i <= coverFrameCount; i++)
            {
                float t = (float)i / totalFrames;
                _animator.Play(animationStateName, 0, Mathf.Clamp01(t));
                yield return null;
            }

            // Phase 1 완료 시점에 고정
            _savedNormalizedTime = coverRatio;
            _animator.Play(animationStateName, 0, _savedNormalizedTime);

            // 새 씬 로드 완료 시 Phase 2 실행
            SceneManager.sceneLoaded += OnSceneLoadedForReveal;
            SceneManager.LoadScene(sceneIndex);
        }

        // =====================================================
        // Phase 2: 이어재생 (새 씬에서 자동 실행)
        // =====================================================

        private void OnSceneLoadedForReveal(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= OnSceneLoadedForReveal;
            StartCoroutine(RevealRoutine());
        }

        private IEnumerator RevealRoutine()
        {
            if (_animator == null || _clip == null)
            {
                Debug.LogWarning("[FadeInObjController] Animator or clip missing after scene load.");
                Cleanup();
                yield break;
            }

            _animator.enabled = true;

            yield return null;

            _animator.speed = 0f;

            // ---- Phase 2: 정방향 이어재생 (savedNormalizedTime → 1.0) ----
            int totalFrames = coverFrameCount + revealFrameCount;
            int startStep = Mathf.RoundToInt(_savedNormalizedTime * totalFrames);

            for (int i = startStep; i <= totalFrames; i++)
            {
                float t = (float)i / totalFrames;
                _animator.Play(animationStateName, 0, Mathf.Clamp01(t));
                yield return null;
            }

            // 완전히 열린 상태
            _animator.Play(animationStateName, 0, 1f);
            _animator.enabled = false;

            _isPlaying = false;

            // 정리
            Cleanup();
        }

        // =====================================================
        // Helpers
        // =====================================================

        /// <summary>
        /// 독립 Canvas를 생성하여 FadeInObj가 DontDestroyOnLoad 후에도
        /// UI로 렌더링될 수 있도록 합니다.
        /// </summary>
        private void CreateStandaloneCanvas()
        {
            var canvasGO = new GameObject("FadeInObj_StandaloneCanvas");
            _standaloneCanvas = canvasGO.AddComponent<Canvas>();
            _standaloneCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _standaloneCanvas.sortingOrder = 999; // 최상위 렌더링

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();
        }

        /// <summary>
        /// 생성한 독립 Canvas와 함께 자신을 정리합니다.
        /// </summary>
        private void Cleanup()
        {
            if (_standaloneCanvas != null)
            {
                Destroy(_standaloneCanvas.gameObject);
                _standaloneCanvas = null;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public bool IsPlaying => _isPlaying;
    }
}
