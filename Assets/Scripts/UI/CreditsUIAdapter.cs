using UnityEngine;
using UnityEngine.UI;
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

            // 코루틴 시작을 위해 오브젝트 자체를 활성화
            gameObject.SetActive(true);

            // 크레딧 재생 중에는 일시정지 버튼 숨김
            SetPauseButtonVisible(false);

            _onComplete = onComplete;
            _isCreditsPlaying = true;

            if (textCredits != null)
                textCredits.text = content;

            if (creditsRoot != null)
                creditsRoot.SetActive(true);

            // 레이아웃 강제 갱신으로 텍스트 길이에 따른 높이 즉시 계산
            Canvas.ForceUpdateCanvases();
            if (scrollContent != null)
            {
                // ContentSizeFitter가 있다면 설정을 강제로 PreferredSize로 변경하여 높이 자동 조절 보장
                var fitter = scrollContent.GetComponent<ContentSizeFitter>();
                if (fitter != null)
                {
                    fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                }

                LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContent);
                
                // [수정] X와 Z는 현재 설정(인스펙터 값)을 유지하고 Y만 0으로 초기화하여 튀는 현상 방지
                Vector3 currentPos = scrollContent.localPosition;
                scrollContent.localPosition = new Vector3(currentPos.x, 0, currentPos.z);
            }

            StartCoroutine(CreditsRoutine());
        }

        private IEnumerator CreditsRoutine()
        {
            if (scrollContent == null)
            {
                FinishCredits();
                yield break;
            }

            // 텍스트 컴포넌트가 계산한 실제 컨텐츠 높이
            float contentHeight = textCredits.preferredHeight;
            float screenHeight = Screen.height;
            
            // targetY 정밀 계산 (피벗 0.5 기준)
            // 텍스트의 절반 + 화면의 절반이 이동하면 텍스트 하단이 화면 상단 끝에 닿음
            // 여기에 200픽셀 정도의 여백만 더해 즉시 종료되도록 설정
            float targetY = (contentHeight * 0.5f) + (screenHeight * 0.5f) + 200f; 
            float currentY = scrollContent.anchoredPosition.y;

            Debug.Log($"[Credits] Scroll Start. Height: {contentHeight}, TargetY: {targetY}");

            while (currentY < targetY)
            {
                // ESC 키를 누르면 즉시 종료
                if (Input.GetKeyDown(KeyCode.Escape)) break;

                // 스페이스바/클릭 시 가속 배수를 10배로 상향하여 답답함 해소
                float currentSpeed = scrollSpeed;
                if (Input.GetMouseButton(0) || Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.Return))
                {
                    currentSpeed *= 10f; 
                }

                currentY += currentSpeed * Time.unscaledDeltaTime;
                scrollContent.anchoredPosition = new Vector2(scrollContent.anchoredPosition.x, currentY);
                
                yield return null;
            }

            Debug.Log("[Credits] Scroll Finished! Transitioning to Title...");
            yield return new WaitForSecondsRealtime(endWaitTime);
            FinishCredits();
        }

        private void FinishCredits()
        {
            _isCreditsPlaying = false;
            
            // 크레딧 종료 시 일시정지 버튼 다시 보이게 설정
            SetPauseButtonVisible(true);
            
            // [수정] 콜백을 실행하여 종료를 알리지만, 화면에서 즉시 사라지지는 않음 (페이드 아웃 연출을 위해)
            _onComplete?.Invoke();
            _onComplete = null;
        }

        /// <summary>
        /// 일시정지 버튼(Btn_Pause)의 활성화 상태를 설정합니다.
        /// </summary>
        private void SetPauseButtonVisible(bool visible)
        {
            var pauseBtn = GameObject.Find("Btn_Pause");
            if (pauseBtn != null)
            {
                pauseBtn.SetActive(visible);
                Debug.Log($"[Credits] Pause button set to {visible}");
            }
            else
            {
                Debug.LogWarning("[Credits] Btn_Pause not found in scene.");
            }
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
