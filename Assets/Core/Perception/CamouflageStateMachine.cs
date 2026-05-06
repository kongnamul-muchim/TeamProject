using UnityEngine;
using System;
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

        /// <summary>
        /// 상태 전환 시 발생 (이전 상태, 새 상태). Adapter가 polling 없이 반응 가능.
        /// </summary>
        public event Action<CamouflageState, CamouflageState> OnStateChanged;

        public CamouflageState CurrentState => _currentState;
        public GameObject TargetObject => _targetObject;
        public bool IsAttachedComplete => _isAttachedComplete;
        public bool IsLockComplete => _isLockComplete;
        public bool IsPerfectReached => _isPerfectReached;

        /// <summary>
        /// 생성자
        /// </summary>
        /// <param name="config">상태 머신 설정 (인스펙터 값이 Config 객체로 주입됨)</param>
        public CamouflageStateMachine(ICamouflageStateMachineConfig config)
        {
            _attachDelay = config.AttachDelay;
            _lockTime = config.LockTime;
            _blendTime = config.BlendTime;
            _perfectTime = config.PerfectTime;

            Reset();
        }

        /// <summary>
        /// 상태 전환 시 매번 호출되는 통일 지점. OnStateChanged 이벤트 발행.
        /// </summary>
        private void SetState(CamouflageState newState)
        {
            if (_currentState == newState) return;
            var prev = _currentState;
            _currentState = newState;
            OnStateChanged?.Invoke(prev, newState);
        }

        /// <summary>
        /// 초기 상태로 리셋
        /// </summary>
        private void Reset()
        {
            SetState(CamouflageState.None);
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
            SetState(CamouflageState.Attached);
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
        public void CancelCamouflage()
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
                    SetState(CamouflageState.Partial);
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
                SetState(CamouflageState.Locked);
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
                SetState(CamouflageState.Approaching);
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
                SetState(CamouflageState.Perfect);
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