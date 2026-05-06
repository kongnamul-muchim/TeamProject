using System;
using UnityEngine;

namespace HideAndInk.Core.Enemy.AI
{
    /// <summary>
    /// Enemy AI 상태 머신
    /// BossEnemyController에서 직접 생성하여 사용 (DI 컨테이너 미사용 — 복잡한 생성자 의존성)
    /// </summary>
    public sealed class EnemyAIStateMachine
    {
        // 상태 인스턴스
        private readonly IEnemyAIState _patrolState;
        private readonly IEnemyAIState _chaseState;
        private readonly IEnemyAIState _searchState;

        // 현재 상태
        private IEnemyAIState _currentState;
        private EnemyAIState _currentStateType;

        // 이벤트
        public event Action<EnemyAIState, EnemyAIState> OnStateChanged; // (이전, 새로운)

        /// <summary>
        /// 현재 AI 상태
        /// </summary>
        public EnemyAIState CurrentState => _currentStateType;

        /// <summary>
        /// 생성자 (DI 주입)
        /// </summary>
        public EnemyAIStateMachine(
            IEnemyAIState patrolState,
            IEnemyAIState chaseState,
            IEnemyAIState searchState)
        {
            _patrolState = patrolState ?? throw new ArgumentNullException(nameof(patrolState));
            _chaseState = chaseState ?? throw new ArgumentNullException(nameof(chaseState));
            _searchState = searchState ?? throw new ArgumentNullException(nameof(searchState));

            // 초기 상태: Patrol
            _currentState = _patrolState;
            _currentStateType = EnemyAIState.Patrol;
        }

        /// <summary>
        /// 초기 상태 설정 (기본: Patrol)
        /// </summary>
        public void Initialize(EnemyAIState initialState = EnemyAIState.Patrol)
        {
            TransitionTo(initialState);
        }

        /// <summary>
        /// 상태 업데이트 (매 프레임 호출)
        /// </summary>
        public void Update(float deltaTime)
        {
            _currentState?.OnUpdate(deltaTime);
        }

        /// <summary>
        /// 특정 상태로 전환 시도
        /// </summary>
        public bool TryTransitionTo(EnemyAIState newState)
        {
            if (_currentStateType == newState)
                return false;

            if (!_currentState.CanTransitionTo(newState))
                return false;

            TransitionTo(newState);
            return true;
        }

        /// <summary>
        /// 강제 상태 전환 (전환 규칙 무시)
        /// </summary>
        public void ForceTransitionTo(EnemyAIState newState)
        {
            TransitionTo(newState);
        }

        /// <summary>
        /// 실제 전환 로직
        /// </summary>
        private void TransitionTo(EnemyAIState newState)
        {
            var previousState = _currentStateType;

            // 현재 상태 이탈
            _currentState?.OnExit();

            // 새 상태 설정
            _currentState = GetStateInstance(newState);
            _currentStateType = newState;

            // 새 상태 진입
            _currentState?.OnEnter();

            // 이벤트 발생
            OnStateChanged?.Invoke(previousState, newState);

#if UNITY_EDITOR
            Debug.Log($"[EnemyAIStateMachine] {previousState} → {newState}");
#endif
        }

        /// <summary>
        /// 상태 enum → 인스턴스 매핑
        /// </summary>
        private IEnemyAIState GetStateInstance(EnemyAIState state)
        {
            return state switch
            {
                EnemyAIState.Patrol => _patrolState,
                EnemyAIState.Chase => _chaseState,
                EnemyAIState.Search => _searchState,
                _ => _patrolState
            };
        }

        /// <summary>
        /// 현재 상태가 특정 상태인지 확인
        /// </summary>
        public bool IsInState(EnemyAIState state)
        {
            return _currentStateType == state;
        }

        /// <summary>
        /// Patrol 상태인지 확인
        /// </summary>
        public bool IsPatrol => _currentStateType == EnemyAIState.Patrol;

        /// <summary>
        /// Chase 상태인지 확인
        /// </summary>
        public bool IsChase => _currentStateType == EnemyAIState.Chase;

        /// <summary>
        /// Search 상태인지 확인
        /// </summary>
        public bool IsSearch => _currentStateType == EnemyAIState.Search;
    }
}
