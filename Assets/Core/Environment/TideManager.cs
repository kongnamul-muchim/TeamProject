using UnityEngine;
using HideAndInk.Core.Events;
using HideAndInk.Core.Enemy.Normal;
using HideAndInk.Core.Interfaces;
using HideAndInk.Core.Managers;

namespace HideAndInk.Core.Environment
{
    /// <summary>
    /// 조류 시스템 매니저
    /// 빈 게임오브젝트에 붙여서 사용
    /// 주기적으로 조류 발동, 성게 소환, Player 밀기
    /// </summary>
    public class TideManager : MonoBehaviour
    {
        [Header("조류 설정")]
        [Tooltip("조류 힘 (성게 밀기)")]
        [SerializeField] private float tideForce = 10f;

        [Tooltip("Player 밀기 힘")]
        [SerializeField] private float playerPushForce = 5f;

        [Tooltip("의태 중 Player 밀림 감소 배율 (1=동일, 0.3=30%)")]
        [SerializeField] [Range(0f, 1f)] private float camouflagePushMultiplier = 0.3f;

        [Header("조류 타이밍")]
        [Tooltip("조류 발동 간격 (초)")]
        [SerializeField] private float tideInterval = 8f;

        [Tooltip("조류 지속 시간 (초)")]
        [SerializeField] private float tideDuration = 3f;

        [Header("조류 방향")]
        [Tooltip("true=매번 랜덤, false=항상 오른쪽→왼쪽(Left)")]
        [SerializeField] private bool randomTideDirection = false;

        [Header("성게 풀 설정")]
        [Tooltip("성게 프리팹")]
        [SerializeField] private GameObject seaUrchinPrefab;

        [Tooltip("풀 크기")]
        [SerializeField] private int poolSize = 20;

        [Header("성게 연속 생성")]
        [Tooltip("조류 1초당 생성할 성게 수 (0.2 = 0.2초마다 1마리 = 초당 5마리)")]
        [SerializeField] private float spawnInterval = 0.2f;

        [Header("Player 참조")]
        [Tooltip("Player Rigidbody")]
        [SerializeField] private Rigidbody playerRigidbody;

        [Tooltip("Player CamouflageAdapter")]
        [SerializeField] private HideAndInk.Player.CamouflageAdapter camouflageAdapter;

        // 상태
        private SeaUrchinPool _urchinPool;
        private float _tideTimer;
        private bool _isTideActive;
        private float _tideActiveTimer;
        private float _spawnTimer;
        private TideDirection _currentDirection;

        // Player 의태 상태 캐싱
        private bool _wasCamouflaging;
        private IEventBus _eventBus;

        private void Awake()
        {
            // EventBus 해결
            if (GameManager.Container != null && GameManager.Container.IsRegistered<IEventBus>())
            {
                _eventBus = GameManager.Container.Resolve<IEventBus>();
            }

            // 성게 풀 초기화
            if (seaUrchinPrefab != null)
            {
                Debug.Log($"[TideManager] Initializing SeaUrchinPool with prefab: {seaUrchinPrefab.name}, size: {poolSize}");
                _urchinPool = new GameObject("SeaUrchinPool").AddComponent<SeaUrchinPool>();
                _urchinPool.Initialize(seaUrchinPrefab, poolSize, transform);
                Debug.Log($"[TideManager] SeaUrchinPool initialized successfully.");
            }
            else
            {
                Debug.LogError("[TideManager] Sea Urchin Prefab is NOT assigned! Please assign in Inspector.");
            }

            // Player 참조 자동 탐색
            if (playerRigidbody == null)
            {
                GameObject playerObj = GameObject.FindWithTag("Player");
                if (playerObj != null)
                {
                    playerRigidbody = playerObj.GetComponent<Rigidbody>();
                    Debug.Log($"[TideManager] Found Player Rigidbody: {playerRigidbody.name}");
                }
                else
                {
                    Debug.LogWarning("[TideManager] Player with Tag 'Player' not found!");
                }
            }

            if (camouflageAdapter == null)
            {
                camouflageAdapter = FindObjectOfType<HideAndInk.Player.CamouflageAdapter>();
                if (camouflageAdapter != null)
                {
                    Debug.Log($"[TideManager] Found CamouflageAdapter: {camouflageAdapter.name}");
                }
            }
        }

        private void Update()
        {
            // 조류 타이머
            if (!_isTideActive)
            {
                _tideTimer += Time.deltaTime;
                if (_tideTimer >= tideInterval)
                {
                    StartTide();
                }
            }
            else
            {
                _tideActiveTimer += Time.deltaTime;

                // 연속 생성: spawnInterval 간격으로 1마리씩
                _spawnTimer += Time.deltaTime;
                if (_spawnTimer >= spawnInterval)
                {
                    _spawnTimer = 0f;
                    if (_urchinPool != null)
                        _urchinPool.SpawnFromTide(_currentDirection, tideForce);
                }

                if (_tideActiveTimer >= tideDuration)
                {
                    EndTide();
                }
            }

            // Player 밀기 (조류 활성 중)
            if (_isTideActive && playerRigidbody != null)
            {
                ApplyPlayerPush();
            }
        }

        /// <summary>
        /// 조류 발동 시작
        /// </summary>
        private void StartTide()
        {
            _isTideActive = true;
            _tideActiveTimer = 0f;
            _tideTimer = 0f;

            // 방향 선택 (고정 또는 랜덤)
            _currentDirection = randomTideDirection
                ? (TideDirection)Random.Range(0, 2)
                : TideDirection.Left;

            _spawnTimer = 0f; // 생성 타이머 리셋

#if UNITY_EDITOR
            Debug.Log($"[TideManager] Tide started: {_currentDirection} (Force: {tideForce}, Interval: {spawnInterval}s)");
#endif

            // 이벤트 발생
            TideEvents.InvokeTideStarted(_currentDirection, tideForce);
        }

        /// <summary>
        /// 조류 종료
        /// </summary>
        private void EndTide()
        {
            _isTideActive = false;
            _tideTimer = 0f;

#if UNITY_EDITOR
            Debug.Log("[TideManager] Tide ended.");
#endif

            TideEvents.InvokeTideEnded();
        }

        /// <summary>
        /// Player 밀기 적용
        /// 의태 중이면 감소된 힘 적용
        /// </summary>
        private void ApplyPlayerPush()
        {
            if (playerRigidbody == null) return;

            // 의태 상태 확인
            bool isCamouflaging = camouflageAdapter != null && camouflageAdapter.IsCamouflaging;

            // 적용 힘 계산
            float appliedForce = isCamouflaging ? playerPushForce * camouflagePushMultiplier : playerPushForce;

            // 방향 벡터
            Vector3 pushDirection = _currentDirection == TideDirection.Right ? Vector3.right : Vector3.left;

            // 힘 적용
            playerRigidbody.AddForce(pushDirection * appliedForce, ForceMode.Force);

            // 의태 중일 때 이벤트 발생 (CamouflageAdapter가 구독하여 거리 체크)
            if (isCamouflaging)
            {
                _eventBus?.Publish(new PlayerPushedByTideEvent(pushDirection, appliedForce));
            }
        }

        private void OnDestroy()
        {
            // 풀 정리
            if (_urchinPool != null)
            {
                Destroy(_urchinPool.gameObject);
            }
        }
    }
}
