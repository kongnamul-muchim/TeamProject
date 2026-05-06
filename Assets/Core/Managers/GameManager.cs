using UnityEngine;
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

            InitializeContainer();
            SubscribeToEvents();
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

            // 오디오 서비스 등록
            RegisterAudioServices();
        }

        /// <summary>
        /// 오디오 서비스 DI 등록
        /// AudioManager가 ISfxService / IBgmService / IAmbientService를 구현한 후 활성화
        /// 
        /// 사용 예:
        ///   _rootContainer.RegisterSingleton&lt;ISfxService, AudioManager&gt;();
        ///   _rootContainer.RegisterSingleton&lt;IBgmService, AudioManager&gt;();
        ///   _rootContainer.RegisterSingleton&lt;IAmbientService, AudioManager&gt;();
        /// </summary>
        private void RegisterAudioServices()
        {
            // AudioManager는 MonoBehaviour Singleton이므로 RegisterInstance 사용
            var audioManager = AudioManager.Instance;

            // SFX 서비스 (Singleton — 전역 AudioManager 인스턴스)
            _rootContainer.RegisterInstance<ISfxService>(audioManager, ServiceLifetime.Singleton);

            // BGM 서비스 (Singleton) — TODO: BGM 구현 후 활성화
            // _rootContainer.RegisterInstance<IBgmService>(audioManager, ServiceLifetime.Singleton);

            // Ambient 서비스 (Singleton) — TODO: Ambient 구현 후 활성화
            // _rootContainer.RegisterInstance<IAmbientService>(audioManager, ServiceLifetime.Singleton);
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
        /// UI_SuspicionVinette의 fillAmount를 의심도 최대치(1.0)로 고정
        /// SuspicionMeterUI가 평소에 조절하는 방식(fillAmount)과 동일하게 적용
        /// </summary>
        private void SetSuspicionVignetteToMax()
        {
            // UI_SuspicionVinette는 Canvas_Ingame의 자식
            var canvasObj = GameObject.Find("Canvas_Ingame");
            if (canvasObj == null) return;

            var vignette = canvasObj.transform.Find("UI_SuspicionVinette");
            if (vignette == null) return;

            var img = vignette.GetComponent<UnityEngine.UI.Image>();
            if (img != null)
            {
                // fillAmount = 1.0 = 의심도 100% 기준 이미지 fill (alpha는 프리팹 기본값 유지)
                img.fillAmount = 1f;
            }
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