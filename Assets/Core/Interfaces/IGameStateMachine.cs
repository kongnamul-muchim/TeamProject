using UnityEngine;
using System;

namespace HideAndInk.Core.Interfaces
{
    /// <summary>
    /// 게임 상태
    /// </summary>
    public enum GameState
    {
        /// <summary>
        /// 플레이 중
        /// </summary>
        Playing,

        /// <summary>
        /// 일시 정지
        /// </summary>
        Paused,

        /// <summary>
        /// 적에게 발견됨
        /// </summary>
        Detected,

        /// <summary>
        /// 탈출 성공
        /// </summary>
        Escaped,

        /// <summary>
        /// 사망
        /// </summary>
        Dead
    }

    /// <summary>
    /// 게임 상태 시스템 인터페이스
    /// </summary>
    public interface IGameStateMachine
    {
        /// <summary>
        /// 현재 게임 상태
        /// </summary>
        GameState CurrentState { get; }

        /// <summary>
        /// 상태 전환
        /// </summary>
        void TransitionTo(GameState newState);

        /// <summary>
        /// 특정 상태로 전환 가능한지 확인
        /// </summary>
        bool CanTransitionTo(GameState newState);

        /// <summary>
        /// 상태 변경 이벤트
        /// </summary>
        event Action<GameState> OnStateChanged;
    }
}
