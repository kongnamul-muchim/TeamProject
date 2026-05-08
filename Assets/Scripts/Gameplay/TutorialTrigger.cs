using UnityEngine;
using HideAndInk.Core.Managers;
using HideAndInk.Core.Interfaces;

namespace HideAndInk.Gameplay
{
    public enum StoryTriggerType
    {
        Tutorial,
        Boss
    }

    [RequireComponent(typeof(BoxCollider))]
    public class TutorialTrigger : MonoBehaviour
    {
        [Header("이벤트 설정")]
        [Tooltip("어떤 종류의 대사를 띄울지 선택하세요 (튜토리얼 힌트 / 보스 조우)")]
        public StoryTriggerType triggerType;

        [Tooltip("위에서 Tutorial을 선택했다면 여기서 종류를 고르세요")]
        public TutorialType tutorialType; 

        [Tooltip("위에서 Boss를 선택했다면 여기서 종류를 고르세요")]
        public BossType bossType;
        
        private bool _isTriggered = false;

        private void OnTriggerEnter(Collider collision)
        {
            // 플레이어가 닿았고, 아직 실행되지 않았다면
            if (!_isTriggered && collision.CompareTag("Player"))
            {
                ExecuteTrigger();
            }
        }

        /// <summary>
        /// 트리거를 외부에서 직접 실행 (ZoneChanger 등에서 호출)
        /// </summary>
        public void ExecuteTrigger()
        {
            if (_isTriggered) return;
            _isTriggered = true;
            
            // 코어 시스템(DI 컨테이너)에서 StoryManager를 불러옵니다.
            if (GameManager.Container != null && GameManager.Container.IsRegistered<IStoryManager>())
            {
                var storyManager = GameManager.Container.Resolve<IStoryManager>();
                
                // 인스펙터에서 설정한 타입에 따라 알맞은 대사를 출력합니다.
                if (triggerType == StoryTriggerType.Tutorial)
                {
                    storyManager.ShowTutorialHint(tutorialType);
                }
                else if (triggerType == StoryTriggerType.Boss)
                {
                    storyManager.ShowBossDialogue(bossType);
                }
            }
            else
            {
                Debug.LogWarning("[TutorialTrigger] StoryManager를 찾을 수 없습니다.");
            }
        }
    }
}
