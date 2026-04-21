using UnityEngine;
using HideAndInk.Core.Interfaces;

namespace HideAndInk.Core.Perception
{
    /// <summary>
    /// 의태 상태 시스템 구현체
    /// 토글 방식: 누르고 있으면 진행, 떼면 Perfect 아니면 취소
    /// </summary>
    public sealed class CamouflageStateMachine : ICamouflageStateMachine
    {
        private readonly float _attachDelay;   // Attached 상태 유지 시간
        private readonly float _lockTime;      // Lock 시간
        private readonly float _blendTime;     // 색상 보간 시간
        private readonly float _perfectTime;   // Perfect 도달 시간

        private CamouflageState _currentState;
        private GameObject _targetObject;
        private float _stateTimer;
        private float _blendProgress;
        private bool _isAttachedComplete;
        private bool _isLockComplete;
        private bool _isPerfectReached;
        private bool _wasKeyReleasedBeforePerfect; // Perfect 도달 전 키가 떼졌는지

        public CamouflageState CurrentState => _currentState;
        public GameObject TargetObject => _targetObject;
        public bool IsAttachedComplete => _isAttachedComplete;
        public bool IsLockComplete => _isLockComplete;
        public bool IsPerfectReached => _isPerfectReached;

        /// <summary>
        /// 생성자
        /// </summary>
        /// <param name="attachDelay">Attached 상태 유지 시간 (0.2~0.3초)</param>
        /// <param name="lockTime">Lock 시간</param>
        /// <param name="blendTime">색상 보간 시간</param>
        /// <param name="perfectTime">완벽 의태까지 걸리는 시간</param>
        public CamouflageStateMachine(
            float attachDelay = 0.3f,
            float lockTime = 0.4f,
            float blendTime = 1.0f,
            float perfectTime = 2.0f)
        {
            _attachDelay = attachDelay;
            _lockTime = lockTime;
            _blendTime = blendTime;
            _perfectTime = perfectTime;

            Reset();
        }

        /// <summary>
        /// 초기 상태로 리셋
        /// </summary>
        private void Reset()
        {
            _currentState = CamouflageState.None;
            _targetObject = null;
            _stateTimer = 0f;
            _blendProgress = 0f;
            _isAttachedComplete = false;
            _isLockComplete = false;
            _isPerfectReached = false;
            _wasKeyReleasedBeforePerfect = false;
        }

        /// <summary>
        /// Attached 상태로 시작 (C Down 시 호출)
        /// </summary>
        public void StartAttach(GameObject target)
        {
            if (target == null)
            {
                Debug.LogWarning("[Camouflage] Target is null!");
                return;
            }

            _targetObject = target;
            _currentState = CamouflageState.Attached;
            _stateTimer = 0f;
            _blendProgress = 0f;
            _isAttachedComplete = false;
            _isLockComplete = false;
            _isPerfectReached = false;
            _wasKeyReleasedBeforePerfect = false;
        }

        /// <summary>
        /// 키가 떼어진 시점 기록
        /// </summary>
        public void RecordKeyRelease()
        {
            if (_currentState != CamouflageState.None)
            {
                _wasKeyReleasedBeforePerfect = !_isPerfectReached;
            }
        }

        /// <summary>
        /// 의태 해제
        /// </summary>
        /// <param name="force">강제 취소 (키 입력으로 인한 취소)</param>
        public void CancelCamouflage(bool force = false)
        {
            Reset();
        }

        /// <summary>
        /// 업데이트
        /// </summary>
        public void Update(float deltaTime, bool isMoving)
        {
            // None 상태에서는 업데이트 안 함
            if (_currentState == CamouflageState.None)
            {
                return;
            }

            // 이동하면 즉시 취소 (Perfect도 관계없음)
            if (isMoving)
            {
                CancelCamouflage();
                return;
            }

            _stateTimer += deltaTime;

            switch (_currentState)
            {
                case CamouflageState.Attached:
                    UpdateAttachedState();
                    break;

                case CamouflageState.Locked:
                    UpdateLockedState();
                    break;

                case CamouflageState.Approaching:
                    // Approaching은 Locked → Partial 전환을 위한 과도기 상태
                    // 즉시 Partial로 전환
                    _currentState = CamouflageState.Partial;
                    _stateTimer = 0f;
                    break;

                case CamouflageState.Partial:
                    UpdatePartialState();
                    break;

                case CamouflageState.Perfect:
                    // Perfect 상태에서는 상태 유지 (이동 관련 처리는 위에서)
                    break;
            }
        }

        /// <summary>
        /// Attached 상태 업데이트
        /// </summary>
        private void UpdateAttachedState()
        {
            if (_stateTimer >= _attachDelay)
            {
                _isAttachedComplete = true;
                _currentState = CamouflageState.Locked;
                _stateTimer = 0f;
            }
        }

        /// <summary>
        /// Lock 상태 업데이트
        /// </summary>
        private void UpdateLockedState()
        {
            if (_stateTimer >= _lockTime)
            {
                _isLockComplete = true;
                _currentState = CamouflageState.Approaching;
                _stateTimer = 0f;
            }
        }

        /// <summary>
        /// Partial 상태 업데이트 (색상 보간 중)
        /// </summary>
        private void UpdatePartialState()
        {
            // 색상 보간 진행
            _blendProgress = Mathf.Clamp01(_stateTimer / _blendTime);

            if (_blendProgress >= 1f)
            {
                _isPerfectReached = true;
                _currentState = CamouflageState.Perfect;
                _stateTimer = 0f;
            }
        }

        /// <summary>
        /// Perfect 도달 전에 키가 떼졌는지 확인 (외부에서 호출)
        /// </summary>
        public bool ShouldCancelOnKeyRelease()
        {
            return _wasKeyReleasedBeforePerfect;
        }

        /// <summary>
        /// 보간 진행도 (0~1)
        /// </summary>
        public float BlendProgress => _blendProgress;
    }
}