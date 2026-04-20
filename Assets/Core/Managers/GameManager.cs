using UnityEngine;
using HideAndInk.Core.Interfaces;
using HideAndInk.Core.Perception;
using HideAndInk.Core.Events;

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

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

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
        }

        /// <summary>
        /// 이벤트 구독
        /// </summary>
        private void SubscribeToEvents()
        {
            // GameStateMachine 상태 변경 구독 → GameEvents 발생
            _gameStateMachine.OnStateChanged += OnGameStateChanged;
            
            // SuspicionToGameStateLink에서 발각 이벤트 구독
            var suspicionLink = FindObjectOfType<SuspicionToGameStateLink>();
            if (suspicionLink != null)
            {
                suspicionLink.OnPlayerDetected += OnPlayerDetected;
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
            
            var suspicionLink = FindObjectOfType<SuspicionToGameStateLink>();
            if (suspicionLink != null)
            {
                suspicionLink.OnPlayerDetected -= OnPlayerDetected;
            }

            _rootContainer?.Dispose();
        }
    }
}