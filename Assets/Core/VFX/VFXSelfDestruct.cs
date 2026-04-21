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

            // 디버그: 현재 상태 정보 로깅
            Debug.Log($"[VFXSelfDestruct] {gameObject.name} | elapsed: {_elapsedTime:F3}s | normalizedTime: {stateInfo.normalizedTime:F3} | loop: {stateInfo.loop} | stateNameHash: {stateInfo.shortNameHash}");

            // 애니메이션이 아직 시작되지 않았거나 재생 중이면 삭제하지 않음
            // (normalizedTime이 0에 가까우면 Rebind 직후이므로 대기)
            if (stateInfo.normalizedTime < 0.01f && _elapsedTime < minPlayTime * 2f) return;

            // 애니메이션이 끝났으면 콜백 호출 후 삭제
            if (stateInfo.normalizedTime >= 1.0f && !stateInfo.loop && !_hasNotifiedCompletion)
            {
                Debug.Log($"[VFXSelfDestruct] {gameObject.name} DESTROYED at elapsed: {_elapsedTime:F3}s");
                _hasNotifiedCompletion = true;
                OnAnimationComplete?.Invoke();
                Destroy(gameObject);
            }
        }
    }
}
