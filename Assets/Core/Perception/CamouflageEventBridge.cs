using UnityEngine;
using HideAndInk.Core.Events;

namespace HideAndInk.Core.Perception
{
    /// <summary>
    /// 의태 이벤트를 효과 시스템에 연결하는 브릿지 (반장 역할)
    /// Member C의 파티클/사운드 시스템과 연동
    /// </summary>
    public class CamouflageEventBridge : MonoBehaviour
    {
        [Header("효과 시스템 참조 (Member C가 작성한 컴포넌트 할당)")]
        [Tooltip("의태 시작/종료 시 파티클 효과를 재생하는 컴포넌트")]
        [SerializeField] private MonoBehaviour inkParticleEffect;
        
        [Tooltip("의태 상태에 따라 사운드를 재생하는 컴포넌트")]
        [SerializeField] private MonoBehaviour soundEffect;
        
        private void OnEnable()
        {
            // 의태 이벤트 구독
            CamouflageEvents.OnCamouflageStart += HandleCamouflageStart;
            CamouflageEvents.OnCamouflageComplete += HandleCamouflageComplete;
            CamouflageEvents.OnCamouflageEnd += HandleCamouflageEnd;
        }
        
        private void OnDisable()
        {
            // 의태 이벤트 구독 해제 (메모리 누수 방지)
            CamouflageEvents.OnCamouflageStart -= HandleCamouflageStart;
            CamouflageEvents.OnCamouflageComplete -= HandleCamouflageComplete;
            CamouflageEvents.OnCamouflageEnd -= HandleCamouflageEnd;
        }
        
        /// <summary>
        /// 의태 시작 시 호출 (None → Attached)
        /// </summary>
        private void HandleCamouflageStart(GameObject target)
        {
            Debug.Log($"[EventBridge] Camouflage started on {target?.name ?? "null"}");
            
            // Member C의 파티클 시스템 호출
            if (inkParticleEffect != null)
            {
                inkParticleEffect.SendMessage("PlayAttachEffect", target, SendMessageOptions.DontRequireReceiver);
            }
            
            // Member C의 사운드 시스템 호출
            if (soundEffect != null)
            {
                soundEffect.SendMessage("PlayAttachSound", SendMessageOptions.DontRequireReceiver);
            }
        }
        
        /// <summary>
        /// 완벽 의태 달성 시 호출 (Perfect 도달)
        /// </summary>
        private void HandleCamouflageComplete(GameObject target)
        {
            Debug.Log($"[EventBridge] Camouflage completed on {target?.name ?? "null"}");
            
            if (inkParticleEffect != null)
            {
                inkParticleEffect.SendMessage("PlayPerfectEffect", target, SendMessageOptions.DontRequireReceiver);
            }
            
            if (soundEffect != null)
            {
                soundEffect.SendMessage("PlayPerfectSound", SendMessageOptions.DontRequireReceiver);
            }
        }
        
        /// <summary>
        /// 의태 해제 시 호출 (→ None)
        /// </summary>
        private void HandleCamouflageEnd(GameObject target)
        {
            Debug.Log($"[EventBridge] Camouflage ended on {target?.name ?? "null"}");
            
            if (inkParticleEffect != null)
            {
                inkParticleEffect.SendMessage("PlayDetachEffect", target, SendMessageOptions.DontRequireReceiver);
            }
            
            if (soundEffect != null)
            {
                soundEffect.SendMessage("PlayDetachSound", SendMessageOptions.DontRequireReceiver);
            }
        }
    }
}
