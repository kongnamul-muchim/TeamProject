using UnityEngine;
using HideAndInk.Core.Interfaces;

namespace HideAndInk.Core.Perception
{
    /// <summary>
    /// 의태 상태 시스템 구현체
    /// 상태 흐름: None → Locked → Approaching → Partial → Perfect
    /// </summary>
    public sealed class CamouflageStateMachine : ICamouflageStateMachine
    {
        private readonly float _lockTime;
        private readonly float _blendTime;
        private readonly float _perfectTime;

        private CamouflageState _currentState;
        private GameObject _targetObject;
        private float _stateTimer;
        private float _blendProgress;
        private bool _isLockComplete;

        public CamouflageState CurrentState => _currentState;
        public GameObject TargetObject => _targetObject;
        public bool IsLockComplete => _isLockComplete;

        /// <summary>
        /// 생성자
        /// </summary>
        /// <param name="lockTime">Lock 시간 (움직임 불가)</param>
        /// <param name="blendTime">색상 보간 시간</param>
        /// <param name="perfectTime">완벽 의태까지 걸리는 시간</param>
        public CamouflageStateMachine(
            float lockTime = 0.4f,
            float blendTime = 1.0f,
            float perfectTime = 2.0f)
        {
            _lockTime = lockTime;
            _blendTime = blendTime;
            _perfectTime = perfectTime;

            _currentState = CamouflageState.None;
            _targetObject = null;
            _stateTimer = 0f;
            _blendProgress = 0f;
            _isLockComplete = false;
        }

        /// <summary>
        /// 의태 시작
        /// </summary>
        public void StartCamouflage(GameObject target)
        {
            if (target == null) return;

            _targetObject = target;
            _currentState = CamouflageState.Locked;
            _stateTimer = 0f;
            _blendProgress = 0f;
            _isLockComplete = false;
        }

        /// <summary>
        /// 의태 해제
        /// </summary>
        public void CancelCamouflage()
        {
            _currentState = CamouflageState.None;
            _targetObject = null;
            _stateTimer = 0f;
            _blendProgress = 0f;
            _isLockComplete = false;
        }

        /// <summary>
        /// 업데이트
        /// </summary>
        public void Update(float deltaTime, bool isMoving)
        {
            // 이동하면 즉시 해제
            if (isMoving && _currentState != CamouflageState.None)
            {
                CancelCamouflage();
                return;
            }

            _stateTimer += deltaTime;

            switch (_currentState)
            {
                case CamouflageState.Locked:
                    UpdateLockedState(deltaTime);
                    break;

                case CamouflageState.Approaching:
                    UpdateApproachingState(deltaTime);
                    break;

                case CamouflageState.Partial:
                    UpdatePartialState(deltaTime);
                    break;

                case CamouflageState.Perfect:
                    // Perfect 상태에서는 상태 유지
                    break;
            }
        }

        /// <summary>
        /// Lock 상태 업데이트
        /// </summary>
        private void UpdateLockedState(float deltaTime)
        {
            if (_stateTimer >= _lockTime)
            {
                _isLockComplete = true;
                _currentState = CamouflageState.Approaching;
                _stateTimer = 0f;
            }
        }

        /// <summary>
        /// Approaching 상태 업데이트
        /// </summary>
        private void UpdateApproachingState(float deltaTime)
        {
            // Approaching에서 Partial로 바로 전환 (스냅 위치에서 색상 보간 시작)
            _currentState = CamouflageState.Partial;
            _stateTimer = 0f;
        }

        /// <summary>
        /// Partial 상태 업데이트 (색상 보간 중)
        /// </summary>
        private void UpdatePartialState(float deltaTime)
        {
            // 색상 보간 진행
            _blendProgress = Mathf.Clamp01(_stateTimer / _blendTime);

            if (_blendProgress >= 1f)
            {
                _currentState = CamouflageState.Perfect;
                _stateTimer = 0f;
            }
        }

        /// <summary>
        /// 보간 진행도 (0~1)
        /// </summary>
        public float BlendProgress => _blendProgress;
    }
}