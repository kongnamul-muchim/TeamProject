using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using HideAndInk.Core.Interfaces;
using HideAndInk.Core.Perception;
using HideAndInk.Core.Player;
using HideAndInk.Core.Events;
using HideAndInk.Core.Logging;
using HideAndInk.Core.Audio;
using HideAndInk.Scripts.Save;

namespace HideAndInk.Core.Managers
{
    /// <summary>
    /// 게임 매니저 - DI 컨테이너와 게임 상태를 관리
    /// GameEvents 발생을 담당 (GameStateMachine은 순수 상태 관리만)
    /// </summary>
    [DefaultExecutionOrder(-100)]
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
        public static bool IsProloguePending => Instance != null && Instance._pendingPrologue;

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
            Debug.Log($"[GameManager] OnSceneLoadedForPrologue called: {scene.name}, mode={mode}, IsContinueMode={SaveManager.IsContinueMode}, PendingZoneIndex={SaveManager.PendingZoneIndex}");

            // 씬 전환 후 SfxManager가 파괴 상태면 재찾거나 재생성 후 DI 갱신
            if (sfxManager == null)
            {
                sfxManager = FindObjectOfType<SfxManager>();

                if (sfxManager == null)
                {
                    var sfxPrefab = Resources.Load<GameObject>("Prefabs/SfxManager");
                    if (sfxPrefab != null)
                    {
                        var sfxGo = Instantiate(sfxPrefab);
                        sfxGo.name = "SfxManager";
                        sfxManager = sfxGo.GetComponent<SfxManager>();
                    }
                }

                if (sfxManager != null)
                {
                    _rootContainer.RegisterInstance<ISfxService>(sfxManager, ServiceLifetime.Singleton);
                }
            }

            if (_pendingPrologue)
            {
                Debug.Log("[GameManager] 씬 로드 완료 → 프롤로그 예약 감지, 실행합니다.");
                StartCoroutine(PlayPrologueDelayed());
            }

            // ContinueZoneHandler가 씬에 없으면 직접 Zone 활성화 처리
            HandleContinueZoneFallback(scene);
        }

        /// <summary>
        /// ContinueZoneHandler가 씬에 없을 때, SaveManager의 이어하기 정보를 바탕으로 직접 Zone을 활성화합니다.
        /// ZoneChanger와 동일하게 Ground, Underwater Effects, 치메라, Canvas_Ingame도 함께 처리합니다.
        /// </summary>
        private void HandleContinueZoneFallback(Scene scene)
        {
            if (!SaveManager.IsContinueMode)
            {
                Debug.Log("[GameManager] 이어하기 모드 아님 → Zone fallback 스킵");
                return;
            }

            // 씬에 ContinueZoneHandler가 있는지 확인
            var continueHandler = FindObjectOfType<ContinueZoneHandler>();
            if (continueHandler != null)
            {
                Debug.Log("[GameManager] ContinueZoneHandler 존재 → fallback 스킵");
                return;
            }

            int targetZone = SaveManager.PendingZoneIndex;
            Debug.Log($"[GameManager] ContinueZoneHandler 없음 → 직접 Zone_{targetZone} 활성화");

            // 씬 내 모든 Zone 오브젝트 찾기 (비활성 포함, 에셋/프리팹 제외)
            List<GameObject> allZones = new List<GameObject>();
            HashSet<int> addedIds = new HashSet<int>();
            var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (var go in allObjects)
            {
                if (go == null) continue;
                if (go.hideFlags != HideFlags.None) continue;
                if (!go.scene.IsValid() || !go.scene.isLoaded) continue;
                if (addedIds.Contains(go.GetInstanceID())) continue;
                if (!go.name.StartsWith("Zone_")) continue;
                
                allZones.Add(go);
                addedIds.Add(go.GetInstanceID());
            }

            Debug.Log($"[GameManager] 찾은 Zone 오브젝트 수: {allZones.Count}");

            // targetZone과 일치하는 것만 활성화, 나머지는 비활성화
            int activatedCount = 0;
            GameObject activatedZone = null;
            foreach (var zone in allZones)
            {
                string[] parts = zone.name.Split('_');
                if (parts.Length >= 2 && int.TryParse(parts[1], out int zoneNum))
                {
                    bool isTarget = zoneNum == targetZone;
                    zone.SetActive(isTarget);
                    if (isTarget)
                    {
                        activatedCount++;
                        activatedZone = zone;
                    }
                    Debug.Log($"[GameManager] {zone.name} → {(isTarget ? "활성화" : "비활성화")}");
                }
            }

            if (activatedCount == 0)
            {
                Debug.LogWarning($"[GameManager] Zone_{targetZone} 오브젝트를 찾을 수 없음!");
            }

            // === Ground 동기화 ===
            ZoneChanger.SyncGroundObjects(targetZone);

            // === Underwater Effects 설정 ===
            if (ZoneChanger.IsFogZone(targetZone))
            {
                bool isDark = ZoneChanger.IsDarkFogZone(targetZone);
                ZoneChanger.SetUnderwaterEffect(true, isDark);
                Debug.Log($"[GameManager] Underwater Effects {(isDark ? "어둡게" : "밝게")} 활성화 (Zone {targetZone})");
            }
            else
            {
                ZoneChanger.SetUnderwaterEffect(false);
                Debug.Log($"[GameManager] Underwater Effects 비활성화 (Zone {targetZone})");
            }

            // === 치메라 이동 ===
            MoveCameraToZone(targetZone, activatedZone);

            // === Canvas_Ingame 설정 ===
            UpdateCanvasIngame(targetZone);

            // === Player 위치 복원 ===
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                Vector3 targetPos = SaveManager.PendingPlayerPosition;

                // 저장된 위치가 zero이면 활성화된 Zone의 위치로 fallback
                if (targetPos == Vector3.zero)
                {
                    if (activatedZone != null)
                    {
                        targetPos = activatedZone.transform.position;
                        targetPos.y = player.transform.position.y;
                        Debug.Log($"[GameManager] 저장 위치가 zero → {activatedZone.name} 위치({targetPos})로 fallback");
                    }
                }

                if (targetPos != Vector3.zero)
                {
                    player.transform.position = targetPos;
                    var rb = player.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.position = targetPos;
                        rb.linearVelocity = Vector3.zero;
                    }
                    Debug.Log($"[GameManager] Player 위치 복원: {targetPos}");
                }
                else
                {
                    Debug.LogWarning("[GameManager] Player 위치를 복원할 수 없음 (targetPos가 zero)");
                }
            }
            else
            {
                Debug.LogWarning("[GameManager] Player 태그 오브젝트를 찾을 수 없음");
            }

            // 이어하기 정보 초기화
            SaveManager.ClearContinueZone();
            Debug.Log("[GameManager] Zone fallback 처리 완료");
        }

        /// <summary>
        /// 활성화된 Zone 위치로 치메라를 이동합니다.
        /// </summary>
        private static void MoveCameraToZone(int zoneNumber, GameObject zoneObj)
        {
            Camera mainCam = Camera.main;
            if (mainCam == null) return;

            // Zone 오브젝트의 위치로 치메라 이동
            if (zoneObj != null)
            {
                Vector3 newPos = mainCam.transform.position;
                newPos.x = zoneObj.transform.position.x;
                newPos.y = zoneObj.transform.position.y;
                mainCam.transform.position = newPos;
                Debug.Log($"[GameManager] 치메라 이동: {newPos} (Zone_{zoneNumber})");
            }

            // CameraFollow가 있으면 타겟 위치로 스냅
            var cameraFollow = Object.FindObjectOfType<HideAndInk.CameraSystem.CameraFollow>();
            if (cameraFollow != null)
            {
                cameraFollow.enabled = true;
                Debug.Log("[GameManager] CameraFollow 활성화");
            }

            // ParallaxController 재초기화 (이어하기 시 치메라 위치가 바뀌면서 배경이 엉망이 되는 문제 방지)
            var parallax = Object.FindObjectOfType<HideAndInk.ParallaxSystem.ParallaxController>();
            if (parallax != null)
            {
                parallax.SetTargetCamera(mainCam);
                Debug.Log("[GameManager] ParallaxController 재초기화");
            }
        }

        /// <summary>
        /// Canvas_Ingame을 Zone에 따라 활성화/비활성화합니다.
        /// </summary>
        private static void UpdateCanvasIngame(int zoneNumber)
        {
            var canvasIngame = GameObject.Find("Canvas_Ingame");
            if (canvasIngame != null)
            {
                bool shouldBeActive = zoneNumber >= 1 && zoneNumber <= 6;
                canvasIngame.SetActive(shouldBeActive);
                Debug.Log($"[GameManager] Canvas_Ingame = {shouldBeActive} (Zone {zoneNumber})");
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
            _pendingPrologue = false;
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
            // sfxManager가 null이거나 파괴 상태면 씬에서 먼저 찾기
            if (sfxManager == null)
            {
                sfxManager = FindObjectOfType<SfxManager>();
            }

            // 그래도 없으면 Resources에서 Prefab 로드
            if (sfxManager == null)
            {
                var sfxPrefab = Resources.Load<GameObject>("Prefabs/SfxManager");
                if (sfxPrefab != null)
                {
                    var sfxGo = Instantiate(sfxPrefab);
                    sfxGo.name = "SfxManager";
                    sfxManager = sfxGo.GetComponent<SfxManager>();
                }
            }

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
                // 게임 오버 효과음 재생 (DI 실패 시 Instance 직접 사용)
                ISfxService sfx = null;
                if (Container.IsRegistered<ISfxService>())
                {
                    sfx = Container.Resolve<ISfxService>();
                    // 유니티 오브젝트가 파괴 상태인지 확인 (null 비교로 가능)
                    if (sfx == null)
                        sfx = SfxManager.Instance;
                }
                else
                {
                    sfx = SfxManager.Instance;
                }
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

                // === 이어하기용 위치 저장 ===
                // 사망 직전 위치를 임시 저장 (SaveManager static이므로 씬 리로드 후에도 유지)
                if (deathPos != Vector3.zero)
                {
                    SaveManager.PendingPlayerPosition = deathPos;
                    Debug.Log($"[GameManager] 사망 위치 저장: {deathPos}");
                }

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