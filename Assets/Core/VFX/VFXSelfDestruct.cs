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
        [Tooltip("최소 재생 시간 (초) - 첫 프레임 즉시 삭제 방지")]
        [SerializeField] private float minPlayTime = 0.1f;

        private Animator _animator;
        private bool _hasNotifiedCompletion;
        private float _elapsedTime;

        /// <summary>
        /// 애니메이션 완료 시 호출될 콜백 (InkMark 소환용)
        /// </summary>
        public Action OnAnimationComplete;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _elapsedTime = 0f;
            Debug.Log($"[VFXSelfDestruct] Awake on {gameObject.name}, animator={_animator != null}, minPlayTime={minPlayTime}");
        }

        private void Update()
        {
            _elapsedTime += Time.deltaTime;

            if (_animator == null) return;

            // 최소 재생 시간 경과 전에는 삭제하지 않음
            if (_elapsedTime < minPlayTime) return;

            // 트랜지션 중이면 무시
            if (_animator.IsInTransition(0)) return;

            AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);

            Debug.Log($"[VFXSelfDestruct] {gameObject.name}: normalizedTime={stateInfo.normalizedTime:F2}, loop={stateInfo.loop}, elapsed={_elapsedTime:F2}");

            // 애니메이션이 끝났으면 콜백 호출 후 삭제
            if (stateInfo.normalizedTime >= 1.0f && !stateInfo.loop && !_hasNotifiedCompletion)
            {
                _hasNotifiedCompletion = true;
                Debug.Log($"[VFXSelfDestruct] {gameObject.name}: Animation complete, invoking callback");
                OnAnimationComplete?.Invoke();
                Destroy(gameObject);
            }
        }
    }
}
