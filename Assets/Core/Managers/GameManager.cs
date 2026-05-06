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
        // SuspicionToGameStateLink 캐싱 (FindObjectOfType 반복 방지)
        private SuspicionToGameStateLink _suspicionLink;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
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
            // 게임 상태 머신 (Singleton)
            _gameStateMachine = new GameStateMachine(GameState.Playing);
            _rootContainer.RegisterInstance<IGameStateMachine>(_gameStateMachine, ServiceLifetime.Singleton);

            // 플레이어 이동 (Transient — 각 Adapter가 Config 등록 후 Resolve)
            _rootContainer.Register<IPlayerMovement, PlayerMovement>(ServiceLifetime.Transient);

            // 의태 탐지기 (Transient)
            _rootContainer.Register<ICamouflageDetector, CamouflageDetector>(ServiceLifetime.Transient);

            // 의태 상태 머신 (Transient)
            _rootContainer.Register<ICamouflageStateMachine, CamouflageStateMachine>(ServiceLifetime.Transient);

            // 오디오 서비스 등록 (TODO: AudioManager가 인터페이스 구현 후 활성화)
            // RegisterAudioServices();
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
            // SFX 서비스 (Singleton — 전역 AudioManager 인스턴스)
            // _rootContainer.Register<ISfxService, AudioManager>(ServiceLifetime.Singleton);

            // BGM 서비스 (Singleton)
            // _rootContainer.Register<IBgmService, AudioManager>(ServiceLifetime.Singleton);

            // Ambient 서비스 (Singleton)
            // _rootContainer.Register<IAmbientService, AudioManager>(ServiceLifetime.Singleton);
        }

        /// <summary>
        /// 이벤트 구독
        /// </summary>
        private void SubscribeToEvents()
        {
            // GameStateMachine 상태 변경 구독 → GameEvents 발생
            _gameStateMachine.OnStateChanged += OnGameStateChanged;
            
            // SuspicionToGameStateLink에서 발각 이벤트 구독 (참조 캐싱)
            _suspicionLink = FindObjectOfType<SuspicionToGameStateLink>();
            if (_suspicionLink != null)
            {
                _suspicionLink.OnPlayerDetected += OnPlayerDetected;
            }
        }

        /// <summary>
        /// 게임 상태 변경 처리 (GameEvents 발생 담당)
        /// </summary>
        private void OnGameStateChanged(GameState previous, GameState current)
        {
            // 상태 전환에 따른 전역 이벤트 발생
            if (current == GameState.Detected)
            {
                GameEvents.InvokePlayerDetected();
            }
            else if (current == GameState.Dead)
            {
                GameEvents.InvokePlayerDeath();
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
            
            if (_suspicionLink != null)
            {
                _suspicionLink.OnPlayerDetected -= OnPlayerDetected;
            }

            _rootContainer?.Dispose();
        }
    }
}