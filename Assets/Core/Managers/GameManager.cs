using UnityEngine;
using UnityEngine.SceneManagement;
using HideAndInk.Core.Interfaces;
using HideAndInk.Core.Perception;
using HideAndInk.Core.Player;
using HideAndInk.Core.Events;
using HideAndInk.Core.Logging;
using HideAndInk.Core.Audio;

namespace HideAndInk.Core.Managers
{
    /// <summary>
    /// 게임 매니저 - DI 컨테이너와 게임 상태를 관리
    /// GameEvents 발생을 담당 (GameStateMachine은 순수 상태 관리만)
    /// </summary>
    public sealed class GameManager : MonoBehaviour
    {
        private static GameManager _instance;
        public static GameManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<GameManager>();
                    if (_instance == null)
                    {
                        var go = new GameObject("GameManager");
                        _instance = go.AddComponent<GameManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }

        private IDIContainer _rootContainer;
        public static IDIContainer Container => Instance._rootContainer;

        // 게임 상태 머신
        private IGameStateMachine _gameStateMachine;
        // 이벤트 버스 (DI에서 해결)
        private IEventBus _eventBus;

        // 사망 카운터 (로그용)
        private int _deathCount;

        // 스토리 데이터베이스 (Inspector에서 할당)
        [SerializeField] private StoryDatabaseSO storyDatabase;

        [Header("Audio")]
        [SerializeField] private SfxManager sfxManager;
        [SerializeField] private BgmManager bgmManager;

        // ─── 프롤로그 트리거 ──────────────────────────────────────
        private bool _pendingPrologue;      // Title → NewGame 시 예약됨

        /// <summary>
        /// TitleController.OnNewGameClicked()에서 호출
        /// 다음 씬 로드 완료 시 프롤로그를 자동 실행하도록 예약
        /// </summary>
        public static void SchedulePrologue()
        {
            if (Instance != null)
                Instance._pendingPrologue = true;
        }

        // 테스트: Play 누르면 바로 프롤로그 실행 (에디터 전용)
        [Header("Debug")]
        [SerializeField] private bool playPrologueOnStart = false;
        public bool WillPlayPrologueOnStart => playPrologueOnStart;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;

            // DontDestroyOnLoad는 root GameObject에서만 동작하므로
            // 씬에 child로 배치된 경우를 대비해 부모를 제거하고 root로 만듦
            if (transform.parent != null)
            {
                transform.SetParent(null);
            }
            DontDestroyOnLoad(gameObject);

            // Time.timeScale 복원 (비정상 종료 후 0으로 남아있는 경우 방지)
            Time.timeScale = 1f;

            // LogModule 초기화
            _ = LogModule.Instance;

            // StoryDatabaseSO 미할당 시 Resources에서 자동 로드
            if (storyDatabase == null)
            {
                storyDatabase = Resources.Load<StoryDatabaseSO>("StoryData/StoryDatabase");
                if (storyDatabase != null)
                    Debug.Log($"[GameManager] StoryDatabaseSO loaded from Resources: {storyDatabase.name}");
            }

            // 그래도 없으면 기본 데이터로 생성 (cutsceneBg 없음)
            if (storyDatabase == null)
            {
                Debug.LogWarning("[GameManager] StoryDatabaseSO not assigned and not found in Resources. Creating default instance (no cutscene sprites). Assign StoryDatabase.asset to Inspector for cutscene support.");
                storyDatabase = ScriptableObject.CreateInstance<StoryDatabaseSO>();
                StoryDatabase.PopulateDefaults(storyDatabase);
            }

            // 씬 로드 완료 시 프롤로그 트리거 감지
            SceneManager.sceneLoaded += OnSceneLoadedForPrologue;

            InitializeContainer();
            SubscribeToEvents();
        }

        private void Start()
        {
            // 에디터 테스트용: Inspector에서 playPrologueOnStart = true
            if (playPrologueOnStart)
            {
                StartCoroutine(PlayPrologueDelayed());
            }
        }

        /// <summary>
        /// 씬 로드 완료 시 프롤로그 예약이 있으면 실행
        /// Title → NewGame → 씬 전환 완료 시 자동 호출됨
        /// </summary>
        private void OnSceneLoadedForPrologue(Scene scene, LoadSceneMode mode)
        {
            if (_pendingPrologue)
            {
                _pendingPrologue = false;
                Debug.Log("[GameManager] 씬 로드 완료 → 프롤로그 예약 감지, 실행합니다.");
                StartCoroutine(PlayPrologueDelayed());
            }
        }

        private System.Collections.IEnumerator PlayPrologueDelayed()
        {
            // 한 프레임 대기 → 모든 Start()가 실행된 후 안전하게 호출
            yield return null;

            var story = Container.Resolve<IStoryManager>();
            if (story != null)
            {
                Debug.Log("[GameManager] PlayPrologueOnStart: 프롤로그를 시작합니다.");
                story.PlayPrologue();
            }
        }

        /// <summary>
        /// DI 컨테이너 초기화 및 서비스 등록
        /// </summary>
        private void InitializeContainer()
        {
            _rootContainer = new DIContainer();
            RegisterCoreServices();
        }

        /// <summary>
        /// 핵심 서비스 등록
        /// </summary>
        private void RegisterCoreServices()
        {
            // 이벤트 버스 (Singleton — 전역 이벤트 중앙화)
            _rootContainer.RegisterInstance<IEventBus>(new EventBus(), ServiceLifetime.Singleton);

            // 게임 상태 머신 (Singleton)
            _gameStateMachine = new GameStateMachine(GameState.Playing);
            _rootContainer.RegisterInstance<IGameStateMachine>(_gameStateMachine, ServiceLifetime.Singleton);

            // 플레이어 이동 (Transient — 각 Adapter가 Config 등록 후 Resolve)
            _rootContainer.Register<IPlayerMovement, PlayerMovement>(ServiceLifetime.Transient);

            // 의태 탐지기 (Transient)
            _rootContainer.Register<ICamouflageDetector, CamouflageDetector>(ServiceLifetime.Transient);

            // 의태 상태 머신 (Transient)
            _rootContainer.Register<ICamouflageStateMachine, CamouflageStateMachine>(ServiceLifetime.Transient);

            // 스토리 데이터베이스 (Singleton — Inspector에서 할당한 SO 인스턴스)
            if (storyDatabase == null)
                storyDatabase = ScriptableObject.CreateInstance<StoryDatabaseSO>();
            _rootContainer.RegisterInstance<StoryDatabaseSO>(storyDatabase, ServiceLifetime.Singleton);

            // 스토리 매니저 (Singleton — 생성자에서 StoryDatabaseSO 자동 주입)
            _rootContainer.Register<IStoryManager, StoryManager>(ServiceLifetime.Singleton);

            // 오디오 서비스 등록
            RegisterAudioServices();
        }

        /// <summary>
        /// 오디오 서비스 DI 등록
        /// SfxManager / BgmManager를 씬에서 찾거나 자동 생성하여 등록
        /// </summary>
        private void RegisterAudioServices()
        {
            if (sfxManager != null)
                _rootContainer.RegisterInstance<ISfxService>(sfxManager, ServiceLifetime.Singleton);

            if (bgmManager != null)
                _rootContainer.RegisterInstance<IBgmService>(bgmManager, ServiceLifetime.Singleton);
        }

        /// <summary>
        /// 이벤트 구독
        /// </summary>
        private void SubscribeToEvents()
        {
            // EventBus 해결
            _eventBus = _rootContainer.Resolve<IEventBus>();

            // GameStateMachine 상태 변경 구독 → EventBus 발행
            _gameStateMachine.OnStateChanged += OnGameStateChanged;

            // SuspicionManager 직접 구독 (SuspicionToGameStateLink 중간 계층 제거)
            // 주의: SuspicionManager가 아직 Awake되지 않았을 수 있으므로 Instance 접근
            var suspicionMgr = SuspicionManager.Instance;
            if (suspicionMgr != null)
            {
                suspicionMgr.OnDetected += OnPlayerDetected;
            }
        }

        /// <summary>
        /// 게임 상태 변경 처리 (GameEvents 발생 담당)
        /// </summary>
        private void OnGameStateChanged(GameState previous, GameState current)
        {
            // 상태 전환에 따른 이벤트 발행 (EventBus 통해)
            if (current == GameState.Detected)
            {
                _eventBus?.Publish(new PlayerDetectedEvent());
            }
            else if (current == GameState.Dead)
            {
                // 게임 오버 효과음 재생
                var sfx = Container.IsRegistered<ISfxService>()
                    ? Container.Resolve<ISfxService>() : null;
                sfx?.Play(SfxId.GameOver);

                // PlayerLives에서 사망 원인 + 의태 상태 읽기
                var playerLives = PlayerLives.Instance;
                DeathCause cause = DeathCause.Unknown;
                string sourceName = "";
                Vector3 deathPos = Vector3.zero;
                bool wasCamouflaged = false;
                if (playerLives != null)
                {
                    cause = playerLives.LastDeathCause;
                    sourceName = playerLives.LastDeathSourceName;
                    deathPos = playerLives.transform.position;
                    wasCamouflaged = playerLives.LastWasCamouflaged;
                }

                _deathCount++;

                // 사망 멘트 선택 (의태 상태 고려)
                string deathMessage = DeathMessages.GetRandom(cause, wasCamouflaged);

                // DEATH 로그 기록 (멘트 + 의태 상태 포함)
                LogModule.Instance.Log(
                    $"사망 #{_deathCount}" +
                    (wasCamouflaged ? " [의태 중]" : "") +
                    $" | 원인: {cause}" +
                    (string.IsNullOrEmpty(sourceName) ? "" : $" | 대상: {sourceName}") +
                    $" | 위치: ({deathPos.x:F1}, {deathPos.y:F1}, {deathPos.z:F1})" +
                    $" | 플레이시간: {Time.timeSinceLevelLoad:F1}초" +
                    $"\n▶ {deathMessage}",
                    "DEATH");

                // PlayerDeathEvent 발행 (사망 원인 + 멘트 + 의태 상태 포함)
                _eventBus?.Publish(new PlayerDeathEvent(
                    cause, sourceName, deathPos, Time.timeSinceLevelLoad,
                    deathMessage, wasCamouflaged
                ));

                // === 사망 처리 ===

                // 의심도 비네트를 의심도 최대치 fill로 먼저 고정 (시간 정지 전에 UI 확정)
                SetSuspicionVignetteToMax();

                // 시간 정지 (모든 적/기믹 활동 중단)
                Time.timeScale = 0f;
            }
            else if (current == GameState.Playing && previous == GameState.Dead)
            {
                // 재시작: 시간 복원
                Time.timeScale = 1f;

                // PlayerLives 초기화
                var lives = PlayerLives.Instance;
                if (lives != null)
                    lives.ResetLives();
            }
        }

        /// <summary>
        /// UI_SuspicionVinette의 alpha를 의심도 최대치(200/255)로 고정
        /// SuspicionMeterUI가 평소에 alpha를 조절하는 방식과 동일
        /// (최대 의심도 100% → alpha = 200/255 ≈ 0.784)
        /// </summary>
        private void SetSuspicionVignetteToMax()
        {
            var canvasObj = GameObject.Find("Canvas_Ingame");
            if (canvasObj == null)
            {
                Debug.LogError("[GameManager] Canvas_Ingame not found!");
                return;
            }

            var vignette = canvasObj.transform.Find("UI_SuspicionVinette");
            if (vignette == null)
            {
                Debug.LogError("[GameManager] UI_SuspicionVinette not found under Canvas_Ingame!");
                return;
            }

            var img = vignette.GetComponent<UnityEngine.UI.Image>();
            if (img == null)
            {
                Debug.LogError("[GameManager] UI_SuspicionVinette has no Image component!");
                return;
            }

            float beforeAlpha = img.color.a;
            var color = img.color;
            color.a = 200f / 255f;
            img.color = color;

            // SuspicionMeterUI 비활성화 (OnValueChanged 이벤트로 alpha가 덮어써지는 것 방지)
            var suspicionUI = canvasObj.GetComponentInChildren<Perception.SuspicionMeterUI>(true);
            if (suspicionUI != null && suspicionUI.enabled)
            {
                suspicionUI.enabled = false;
                Debug.Log("[GameManager] SuspicionMeterUI disabled to prevent alpha override.");
            }

            Debug.Log($"[GameManager] Vignette alpha: {beforeAlpha:F3} → {img.color.a:F3} (GameObject.activeSelf={vignette.gameObject.activeSelf}, Image.enabled={img.enabled})");
        }

        /// <summary>
        /// 플레이어 발각 시 호출 (GameStateMachine.Detected 전환)
        /// </summary>
        private void OnPlayerDetected()
        {
            if (_gameStateMachine != null && _gameStateMachine.CanTransitionTo(GameState.Detected))
            {
                _gameStateMachine.TransitionTo(GameState.Detected);
            }
        }

        /// <summary>
        /// 게임 상태 머신 가져오기
        /// </summary>
        public IGameStateMachine GetGameStateMachine()
        {
            return _gameStateMachine;
        }

        private void OnDestroy()
        {
            // 이벤트 구독 해제
            SceneManager.sceneLoaded -= OnSceneLoadedForPrologue;

            if (_gameStateMachine != null)
            {
                _gameStateMachine.OnStateChanged -= OnGameStateChanged;
            }

            var suspicionMgr = SuspicionManager.Instance;
            if (suspicionMgr != null)
            {
                suspicionMgr.OnDetected -= OnPlayerDetected;
            }

            _rootContainer?.Dispose();
        }
    }
}