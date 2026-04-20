using System;
using UnityEngine;

namespace HideAndInk.Core.VFX
{
    /// <summary>
    /// VFX 프리팹이 애니메이션 재생 완료 후 자동으로 삭제되는 컴포넌트
    /// FX_CamouflageStart, FX_CamouflageEnd에 부착
    /// </summary>
    public class VFXSelfDestruct : MonoBehaviour
    {
        private Animator _animator;
        private bool _hasNotifiedCompletion;

        /// <summary>
        /// 애니메이션 완료 시 호출될 콜백 (InkMark 소환용)
        /// </summary>
        public Action OnAnimationComplete;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
        }

        private void Update()
        {
            if (_animator == null) return;

            AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);

            // 애니메이션이 끝났으면 콜백 호출 후 삭제
            if (stateInfo.normalizedTime >= 1.0f && !stateInfo.loop && !_hasNotifiedCompletion)
            {
                _hasNotifiedCompletion = true;
                OnAnimationComplete?.Invoke();
                Destroy(gameObject);
            }
        }
    }
}
