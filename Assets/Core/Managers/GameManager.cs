using UnityEngine;
using HideAndInk.Core.Interfaces;
using HideAndInk.Core.Perception;
using HideAndInk.Core.Events;

namespace HideAndInk.Core.Managers
{
    /// <summary>
    /// 게임 매니저 - DI 컨테이너와 게임 상태를 관리
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

        // 게임 상태 머신 (Singleton으로 유지)
        private IGameStateMachine _gameStateMachine;

        [Header("연동할 스크립트")]
        [SerializeField] private SuspicionToGameStateLink suspicionToGameStateLink;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            // LogModule 초기화 (자동으로 LogModule GameObject 생성)
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

            Debug.Log("[GameManager] Core services registered.");
        }

        /// <summary>
        /// 외부 이벤트 구독 (이벤트 기반 통신)
        /// </summary>
        private void SubscribeToEvents()
        {
            // SuspicionToGameStateLink에서 발각 이벤트 구독
            if (suspicionToGameStateLink != null)
            {
                suspicionToGameStateLink.OnPlayerDetected += OnPlayerDetected;
            }
            
            // GameEvents 구독 (UI, 사운드 등 추가 처리용)
            GameEvents.OnPlayerDetected += HandlePlayerDetected;
            GameEvents.OnPlayerDeath += HandlePlayerDeath;
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
        /// GameEvents.OnPlayerDetected 핸들러 (추가 처리: UI, 사운드 등)
        /// </summary>
        private void HandlePlayerDetected()
        {
            // 추가 처리 (UI, 사운드 등) 가능
        }
        
        /// <summary>
        /// GameEvents.OnPlayerDeath 핸들러
        /// </summary>
        private void HandlePlayerDeath()
        {
            // 추가 처리 (게임 오버 화면, 사운드 등) 가능
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
            if (suspicionToGameStateLink != null)
            {
                suspicionToGameStateLink.OnPlayerDetected -= OnPlayerDetected;
            }
            
            // GameEvents 구독 해제
            GameEvents.OnPlayerDetected -= HandlePlayerDetected;
            GameEvents.OnPlayerDeath -= HandlePlayerDeath;

            _rootContainer?.Dispose();
        }
    }
}