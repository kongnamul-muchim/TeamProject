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
        [Tooltip("PlayerLives 컴포넌트 (인스펙터에서 할당, null이면 Instance 자동 연결)")]
        [SerializeField] private PlayerLives playerLives;

        [Header("하트 이미지")]
        [Tooltip("HP 개수만큼 순서대로 연결")]
        [SerializeField] private Image[] heartImages;

        [Header("깜빡임 설정")]
        [Tooltip("피격 시 하트가 사라지기 전 깜빡이는 시간 (초)")]
        [SerializeField] private float blinkDuration = 1f;
        [Tooltip("깜빡임 주파수")]
        [SerializeField] private float blinkFrequency = 10f;

        private int _lastLives;

        private void Awake()
        {
            // 크로스-프리팹 참조 깨짐 보정
            if (playerLives == null || playerLives.CurrentLives <= 0)
            {
                playerLives = PlayerLives.Instance;
            }

            if (playerLives == null)
            {
                Debug.LogError("[PlayerHPUI] PlayerLives를 찾을 수 없습니다.");
                return;
            }

            _lastLives = playerLives.CurrentLives;
            playerLives.OnLifeChanged += OnLifeChanged;

            // Awake 시점에 PlayerLives가 이미 초기화되어 있음
            UpdateHeartsImmediate(_lastLives);
        }

        private void OnDestroy()
        {
            if (playerLives != null)
                playerLives.OnLifeChanged -= OnLifeChanged;
        }

        private void OnLifeChanged(int currentLives)
        {
            if (currentLives < _lastLives)
            {
                // 체력 감소 → 사라질 하트 깜빡임
                int removedIndex = currentLives; // 3→2면 heart[2]가 사라질 차례
                StartCoroutine(BlinkAndDisable(removedIndex));
            }
            else
            {
                // 체력 증가/초기화 (ResetLives 등) → 깜빡임 취소 + 즉시 적용
                StopAllCoroutines();
                UpdateHeartsImmediate(currentLives);
            }
            _lastLives = currentLives;
        }

        /// <summary>
        /// 모든 하트를 currentLives에 맞춰 즉시 설정
        /// </summary>
        private void UpdateHeartsImmediate(int currentLives)
        {
            for (int i = 0; i < heartImages.Length; i++)
            {
                if (heartImages[i] != null)
                    heartImages[i].enabled = i < currentLives;
            }
        }

        /// <summary>
        /// 지정된 하트를 blinkDuration 동안 깜빡인 후 비활성화
        /// </summary>
        private IEnumerator BlinkAndDisable(int heartIndex)
        {
            if (heartIndex < 0 || heartIndex >= heartImages.Length) yield break;
            Image heart = heartImages[heartIndex];
            if (heart == null) yield break;

            float elapsed = 0f;
            while (elapsed < blinkDuration)
            {
                // Unscaled delta time 사용 (일시정지 중에도 깜빡임)
                elapsed += Time.unscaledDeltaTime;
                heart.enabled = Mathf.Sin(elapsed * blinkFrequency * Mathf.PI * 2) > 0f;
                yield return null;
            }

            heart.enabled = false;
        }
    }
}
