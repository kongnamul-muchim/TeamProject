using UnityEngine;
using HideAndInk.Core.Interfaces;

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
        }

        private void OnDestroy()
        {
            _rootContainer?.Dispose();
        }
    }
}