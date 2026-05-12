using HideAndInk.Core.Player;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace HideAndInk.Scripts.UI
{
    /// <summary>
    /// 플레이어 HP를 하트 이미지 On/Off로 표시
    /// 피격 시 하트가 1초간 깜빡인 후 비활성화
    /// </summary>
    public sealed class PlayerHPUI : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("PlayerLives 컴포넌트 (인스펙터 할당은 참고용, 런타임에는 Instance 우선)")]
        [SerializeField] private PlayerLives playerLives;

        [Header("하트 이미지")]
        [Tooltip("HP 개수만큼 순서대로 연결")]
        [SerializeField] private Image[] heartImages;

        [Header("깜빡임 설정")]
        [Tooltip("피격 시 하트가 사라지기 전 깜빡이는 시간 (초)")]
        [SerializeField] private float blinkDuration = 1f;
        [Tooltip("깜빡임 주파수")]
        [SerializeField] private float blinkFrequency = 10f;

        /// <summary>
        /// 씬의 유일한 PlayerHPUI 인스턴스
        /// </summary>
        public static PlayerHPUI Instance { get; private set; }

        private int _lastLives;
        private bool _initialized;

        private void Awake()
        {
            Instance = this;
            Debug.Log($"[PlayerHPUI] Awake - serialized playerLives={(playerLives != null ? "OK" : "NULL")}, Instance={(PlayerLives.Instance != null ? "OK" : "NULL")}");
            TryBind();
        }

        /// <summary>
        /// 모든 하트 이미지가 비활성화되었는지 확인
        /// </summary>
        public bool AreAllHeartsDisabled()
        {
            if (heartImages == null || heartImages.Length == 0) return false;
            for (int i = 0; i < heartImages.Length; i++)
            {
                if (heartImages[i] != null && heartImages[i].enabled)
                    return false;
            }
            return true;
        }

        private void OnDestroy()
        {
            if (playerLives != null)
                playerLives.OnLifeChanged -= OnLifeChanged;
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            if (!_initialized)
            {
                Debug.Log("[PlayerHPUI] Start - retrying");
                TryBind();
            }

            if (!_initialized)
            {
                Debug.LogError("[PlayerHPUI] PlayerLives not found. (Start retry failed)");
            }
            else
            {
                Debug.Log($"[PlayerHPUI] Start done - _lastLives={_lastLives}, heartImages.Length={heartImages?.Length}");
            }
        }

        private void TryBind()
        {
            if (_initialized) return;

            // Always prefer Instance over serialized cross-prefab ref
            if (PlayerLives.Instance != null)
            {
                playerLives = PlayerLives.Instance;
                Debug.Log("[PlayerHPUI] TryBind - using Instance");
            }

            if (playerLives == null)
            {
                Debug.Log("[PlayerHPUI] TryBind - playerLives null, deferring");
                return;
            }

            _lastLives = playerLives.CurrentLives;
            Debug.Log($"[PlayerHPUI] TryBind - CurrentLives={_lastLives}, subscribing OnLifeChanged, heartImages[]={heartImages?.Length}");
            playerLives.OnLifeChanged += OnLifeChanged;
            UpdateHeartsImmediate(_lastLives);
            _initialized = true;
        }

        private void OnLifeChanged(int currentLives)
        {
            Debug.Log($"[PlayerHPUI] OnLifeChanged({currentLives}) - _lastLives={_lastLives}");
            if (currentLives < _lastLives)
            {
                int removedIndex = currentLives;
                Debug.Log($"[PlayerHPUI] 하트 감소: {removedIndex}번 깜빡임 시작");
                StartCoroutine(BlinkAndDisable(removedIndex));
            }
            else
            {
                Debug.Log($"[PlayerHPUI] 하트 증가/초기화: UpdateHeartsImmediate({currentLives})");
                StopAllCoroutines();
                UpdateHeartsImmediate(currentLives);
            }
            _lastLives = currentLives;
        }

        private void UpdateHeartsImmediate(int currentLives)
        {
            for (int i = 0; i < heartImages.Length; i++)
            {
                if (heartImages[i] != null)
                {
                    bool newState = i < currentLives;
                    Debug.Log($"[PlayerHPUI] heart[{i}].enabled = {newState} (현재: {heartImages[i].enabled})");
                    heartImages[i].enabled = i < currentLives;
                }
                else
                {
                    Debug.LogWarning($"[PlayerHPUI] heart[{i}] is NULL!");
                }
            }
        }

        private IEnumerator BlinkAndDisable(int heartIndex)
        {
            if (heartIndex < 0 || heartIndex >= heartImages.Length) yield break;
            Image heart = heartImages[heartIndex];
            if (heart == null) yield break;

            float elapsed = 0f;
            while (elapsed < blinkDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                heart.enabled = Mathf.Sin(elapsed * blinkFrequency * Mathf.PI * 2) > 0f;
                yield return null;
            }

            heart.enabled = false;
        }
    }
}
