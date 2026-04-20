using UnityEngine;

namespace HideAndInk.Core.VFX
{
    /// <summary>
    /// VFX 프리팹이 애니메이션 재생 완료 후 자동으로 삭제되도록 하는 컴포넌트
    /// FX_CamouflageStart, FX_CamouflageEnd에 부착
    /// </summary>
    public class VFXSelfDestruct : MonoBehaviour
    {
        private Animator _animator;
        private SpriteRenderer _spriteRenderer;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void Update()
        {
            if (_animator == null) return;

            // Animator가 현재 재생 중인지 확인
            AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
            
            // 애니메이션이 끝났으면 삭제
            if (stateInfo.normalizedTime >= 1.0f && !stateInfo.loop)
            {
                Destroy(gameObject);
            }
        }
    }
}
