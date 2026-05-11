using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using HideAndInk.Core.Managers;
using HideAndInk.Core.Interfaces;
using HideAndInk.Core.Events;

namespace HideAndInk.Gameplay
{
    [RequireComponent(typeof(BoxCollider))]
    public class EpilogueTrigger : MonoBehaviour
    {
        [Header("페이드 설정")]
        [Tooltip("화면을 가려줄 검은색 UI 이미지")]
        public Image fadeImage;
        [Tooltip("페이드에 걸리는 시간 (초)")]
        public float fadeDuration = 1.5f;

        [Header("씬 전환 설정")]
        [Tooltip("에필로그 종료 후 이동할 타이틀 씬의 이름")]
        public string titleSceneName = "Title";

        [Header("크레딧 설정")]
        [SerializeField] private HideAndInk.UI.CreditsUIAdapter creditsUI;
        [TextArea(10, 20)]
        [SerializeField] private string creditsContent; 

        private bool _isTriggered = false;

        private void OnEnable()
        {
            StoryEvents.OnEpilogueWillEnd += HandleEpilogueWillEnd;
        }

        private void OnDisable()
        {
            StoryEvents.OnEpilogueWillEnd -= HandleEpilogueWillEnd;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_isTriggered && other.CompareTag("Player"))
            {
                _isTriggered = true;
                StartCoroutine(Sequence_ArriveEpilogue());
            }
        }

        // [흐름 1] 도착 -> 페이드아웃 -> 에필로그 세팅 -> 페이드인
        private IEnumerator Sequence_ArriveEpilogue()
        {
            // 연출 중 스페이스바 연타로 대사가 스킵되는 것 방지
            StoryEvents.IsInputBlocked = true;

            // 1. 페이드 아웃 (화면 까매짐)
            if (fadeImage != null)
            {
                fadeImage.gameObject.SetActive(true);
                yield return StartCoroutine(FadeRoutine(0f, 1f));
            }

            // 2. 에필로그 컷신/대사 시스템 가동
            // (이 시점에 화면이 까만 상태이므로 컷신이 팟! 하고 뜨는게 가려집니다)
            if (GameManager.Container != null && GameManager.Container.IsRegistered<IStoryManager>())
            {
                var storyManager = GameManager.Container.Resolve<IStoryManager>();
                storyManager.PlayEpilogue(); 
            }

            // 3. 페이드 인 (화면 밝아지며 컷신과 대사창 등장)
            if (fadeImage != null)
            {
                yield return StartCoroutine(FadeRoutine(1f, 0f));
                
                // 화면 터치(클릭)를 방해하지 않도록 페이드 이미지를 비활성화
                fadeImage.gameObject.SetActive(false); 
            }

            // 연출이 끝났으므로 대사 넘기기 입력 허용
            StoryEvents.IsInputBlocked = false;
        }

        // UI가 켜져있는 채로 대사가 끝나기 직전에 호출됨
        private void HandleEpilogueWillEnd()
        {
            StartCoroutine(Sequence_EndAndTitle());
        }

        // [흐름 2] 페이드아웃 -> 스토리 시스템(UI) 끄기 -> 크레딧 재생 -> 타이틀 씬 로드
        private IEnumerator Sequence_EndAndTitle()
        {
            // 연출 시작 시 입력 다시 차단 (마지막 연출이므로 다시 풀지 않음)
            StoryEvents.IsInputBlocked = true;

            // 1. UI(컷신/대사)가 보이는 상태에서 그 위에 페이드 아웃 (화면 까매짐)
            if (fadeImage != null)
            {
                fadeImage.gameObject.SetActive(true);
                yield return StartCoroutine(FadeRoutine(0f, 1f));
            }

            // 2. 화면이 까매졌으므로 이제 UI를 뒤에서 끕니다.
            if (GameManager.Container != null && GameManager.Container.IsRegistered<IStoryManager>())
            {
                var storyManager = GameManager.Container.Resolve<IStoryManager>();
                storyManager.StopStory(); 
            }

            // 3. 크레딧 재생
            if (creditsUI != null)
            {
                bool creditsFinished = false;
                
                // 기본 크레딧 내용이 비어있다면 에셋에 미리 정의된 텍스트를 사용하거나 수동 입력 가능
                string finalContent = string.IsNullOrEmpty(creditsContent) ? GetDefaultCreditsText() : creditsContent;
                
                creditsUI.StartCredits(finalContent, () => {
                    creditsFinished = true;
                });

                // 크레딧이 끝날 때까지 대기
                while (!creditsFinished)
                {
                    yield return null;
                }
            }
            else
            {
                // 크레딧 UI가 없는 경우 최소한의 대기 시간 부여
                yield return new WaitForSecondsRealtime(2f);
            }

            // 4. 타이틀 씬으로 넘어가기
            SceneManager.LoadScene(titleSceneName);
        }

        private string GetDefaultCreditsText()
        {
            // Credits_Draft.md의 최신 내용을 기반으로 한 텍스트
            return "[ Hide & Ink : 문어의 먹물꿈질 ]\n\n\n" +
                   "--- STAFF ---\n\n" +
                   "Lead Programmer & System Architect\n김동열 (Dongyeol Kim)\n\n" +
                   "Sidekick System & Sound Sourcing\n박시연 (Siyeon Park)\n\n" +
                   "Art Director & Technical Artist\n박미초 (Micho Park)\n\n" +
                   "Environment Artist & Level Design\n정지은 (Jieun Jung)\n\n\n" +
                   "--- THIRD-PARTY ASSETS ---\n\n" +
                   "TextMesh Pro - Unity Technologies\n" +
                   "Universal RP - Unity Technologies\n" +
                   "Fonts - 꾸불림체 (Kkubullim Font)\n\n\n" +
                   "--- MUSIC & SOUND ---\n\n" +
                   "Original Sound Track\n" +
                   "- Grassland Coast (Theme of Shore)\n" +
                   "- Coral Reef (Vibrant Sea)\n" +
                   "- Seaweed Forest (Shadows in Kelp)\n" +
                   "- Deep Sea Cliff (Abyss Call)\n" +
                   "- Deep Sea Ruins (Echoes of Ancient)\n\n" +
                   "Sound Effects\n" +
                   "- Interaction & UI Feedback\n" +
                   "- Ink Ability & Camouflage Suite\n" +
                   "- Environmental Ambience\n\n\n" +
                   "--- SPECIAL THANKS ---\n\n" +
                   "Advisors: 프로젝트에 소중한 조언을 주신 모든 분들\n" +
                   "Beta Testers: 안정적인 플레이를 위해 도움 주신 테스터분들\n" +
                   "Players: 두두의 여정을 끝까지 지켜봐 주신 플레이어 여러분\n\n\n\n" +
                   "© 2026 Team 미지동시. All rights reserved.\n" +
                   "Powered by Unity Engine 2022.3 LTS";
        }

        // 공통 페이드 애니메이션 로직 (대사 중 시간정지 상태에서도 작동하도록 설정)
        private IEnumerator FadeRoutine(float startAlpha, float endAlpha)
        {
            float timer = 0f;
            Color color = fadeImage.color;
            
            while (timer < fadeDuration)
            {
                // Time.timeScale = 0 상태에서도 애니메이션이 작동하도록 unscaledDeltaTime 사용
                timer += Time.unscaledDeltaTime; 
                color.a = Mathf.Lerp(startAlpha, endAlpha, timer / fadeDuration);
                fadeImage.color = color;
                yield return null;
            }
            
            color.a = endAlpha;
            fadeImage.color = color;
        }
    }
}
