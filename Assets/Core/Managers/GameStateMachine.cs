using UnityEngine;
using System;
using System.Collections.Generic;
using HideAndInk.Core.Interfaces;
using HideAndInk.Core.Events;

namespace HideAndInk.Core.Managers
{
    /// <summary>
    /// 게임 상태 시스템 구현체
    /// </summary>
    public sealed class GameStateMachine : IGameStateMachine
    {
        // 유효한 상태 전환 목록
        private static readonly Dictionary<GameState, HashSet<GameState>> ValidTransitions = new()
        {
            { GameState.Playing, new HashSet<GameState> { GameState.Paused, GameState.Detected } },
            { GameState.Paused, new HashSet<GameState> { GameState.Playing } },
            { GameState.Detected, new HashSet<GameState> { GameState.Escaped, GameState.Dead, GameState.Playing } },
            { GameState.Escaped, new HashSet<GameState> { GameState.Playing } },
            { GameState.Dead, new HashSet<GameState> { GameState.Playing } }
        };

        private GameState _currentState;

        /// <summary>
        /// 현재 게임 상태
        /// </summary>
        public GameState CurrentState => _currentState;

        /// <summary>
        /// 상태 변경 이벤트
        /// </summary>
        public event Action<GameState> OnStateChanged;

        /// <summary>
        /// 생성자
        /// </summary>
        /// <param name="initialState">초기 상태 (기본: Playing)</param>
        public GameStateMachine(GameState initialState = GameState.Playing)
        {
            _currentState = initialState;
        }

        /// <summary>
        /// 상태 전환
        /// </summary>
        public void TransitionTo(GameState newState)
        {
            if (!CanTransitionTo(newState))
            {
                return;
            }

            GameState previousState = _currentState;
            _currentState = newState;

            // [이벤트] 게임 상태 전환에 따른 전역 이벤트 발생
            if (newState == GameState.Detected)
            {
                GameEvents.InvokePlayerDetected();
            }
            else if (newState == GameState.Dead)
            {
                GameEvents.InvokePlayerDeath();
            }

            OnStateChanged?.Invoke(_currentState);
        }

        /// <summary>
        /// 특정 상태로 전환 가능한지 확인
        /// </summary>
        public bool CanTransitionTo(GameState newState)
        {
            if (_currentState == newState) return false;

            if (ValidTransitions.TryGetValue(_currentState, out var validTargets))
            {
                return validTargets.Contains(newState);
            }

            return false;
        }

        /// <summary>
        /// 현재 상태가 Playing인지 확인
        /// </summary>
        public bool IsPlaying => _currentState == GameState.Playing;

        /// <summary>
        /// 현재 상태가 일시 정지인지 확인
        /// </summary>
        public bool IsPaused => _currentState == GameState.Paused;

        /// <summary>
        /// 현재 상태가 Detected인지 확인
        /// </summary>
        public bool IsDetected => _currentState == GameState.Detected;

        /// <summary>
        /// 발견 상태로 전환 (의심도 100% 도달 시 호출)
        /// </summary>
        public void OnPlayerDetected()
        {
            if (CanTransitionTo(GameState.Detected))
            {
                TransitionTo(GameState.Detected);
            }
        }

        /// <summary>
        /// 탈출 시도
        /// </summary>
        public void TryEscape()
        {
            if (_currentState == GameState.Detected)
            {
                TransitionTo(GameState.Escaped);
            }
        }

        /// <summary>
        /// 사망 처리
        /// </summary>
        public void OnPlayerDead()
        {
            if (CanTransitionTo(GameState.Dead))
            {
                TransitionTo(GameState.Dead);
            }
        }

        /// <summary>
        /// 일시 정지/재개
        /// </summary>
        public void TogglePause()
        {
            if (_currentState == GameState.Playing)
            {
                TransitionTo(GameState.Paused);
            }
            else if (_currentState == GameState.Paused)
            {
                TransitionTo(GameState.Playing);
            }
        }

        /// <summary>
        /// 게임 재시작
        /// </summary>
        public void Restart()
        {
            TransitionTo(GameState.Playing);
        }
    }
}
