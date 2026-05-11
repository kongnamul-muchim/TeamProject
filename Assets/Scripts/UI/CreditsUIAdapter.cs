using UnityEngine;
using TMPro;
using System.Collections;
using System;
using HideAndInk.Core.Events;

namespace HideAndInk.UI
{
    /// <summary>
    /// 크레딧 연출 및 제어를 담당하는 어댑터
    /// </summary>
    public class CreditsUIAdapter : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject creditsRoot;       // 크레딧 최상위 오브젝트
        [SerializeField] private RectTransform scrollContent;   // 스크롤될 텍스트 부모
        [SerializeField] private TextMeshProUGUI textCredits;   // 크레딧 텍스트 (TMP)

        [Header("Settings")]
        [SerializeField] private float scrollSpeed = 50f;       // 스크롤 속도
        [SerializeField] private float fastScrollMultiplier = 5f; // 입력 시 가속 배수
        [SerializeField] private float endWaitTime = 2f;        // 스크롤 종료 후 대기 시간

        private bool _isCreditsPlaying = false;
        private Vector3 _startPosition;
        private Action _onComplete;

        private void Awake()
        {
            if (creditsRoot != null)
                creditsRoot.SetActive(false);

            if (scrollContent != null)
                _startPosition = scrollContent.localPosition;
        }

        /// <summary>
        /// 크레딧 재생 시작
        /// </summary>
        public void StartCredits(string content, Action onComplete)
        {
            if (_isCreditsPlaying) return;

            _onComplete = onComplete;
            _isCreditsPlaying = true;

            if (textCredits != null)
                textCredits.text = content;

            if (creditsRoot != null)
                creditsRoot.SetActive(true);

            if (scrollContent != null)
                scrollContent.localPosition = _startPosition;

            StartCoroutine(CreditsRoutine());
        }

        private IEnumerator CreditsRoutine()
        {
            if (scrollContent == null)
            {
                FinishCredits();
                yield break;
            }

            // 텍스트가 화면 위로 다 올라갈 때까지 스크롤
            // 텍스트 높이 계산 (Layout 등 강제 갱신 필요할 수 있음)
            Canvas.ForceUpdateCanvases();
            float contentHeight = scrollContent.rect.height;
            float screenHeight = Screen.height;
            float targetY = contentHeight + screenHeight;

            while (scrollContent.localPosition.y < targetY)
            {
                float currentSpeed = scrollSpeed;
                
                // 입력(클릭/스페이스) 시 가속
                if (Input.GetMouseButton(0) || Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.Return))
                {
                    currentSpeed *= fastScrollMultiplier;
                }

                scrollContent.localPosition += Vector3.up * (currentSpeed * Time.unscaledDeltaTime);
                yield return null;
            }

            yield return new WaitForSecondsRealtime(endWaitTime);
            FinishCredits();
        }

        private void FinishCredits()
        {
            _isCreditsPlaying = false;
            if (creditsRoot != null)
                creditsRoot.SetActive(false);

            _onComplete?.Invoke();
        }

        /// <summary>
        /// 강제 종료 (씬 전환 시 등)
        /// </summary>
        public void StopCredits()
        {
            StopAllCoroutines();
            FinishCredits();
        }
    }
}
