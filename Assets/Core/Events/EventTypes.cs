using UnityEngine;
using HideAndInk.Core.Interfaces;

namespace HideAndInk.Core.Events
{
    // ================================================================
    // Game Events (기존 GameEvents 대체)
    // ================================================================

    /// <summary>플레이어 발각</summary>
    public struct PlayerDetectedEvent { }

    /// <summary>플레이어 사망</summary>
    public struct PlayerDeathEvent { }

    /// <summary>스테이지 클리어</summary>
    public struct StageClearEvent { }

    // ================================================================
    // Camouflage Events (기존 CamouflageEvents 대체)
    // ================================================================

    /// <summary>의태 상태 변경</summary>
    public struct CamouflageStateChangedEvent
    {
        public CamouflageState State;
        public CamouflageStateChangedEvent(CamouflageState state) => State = state;
    }

    /// <summary>의태 시작 (None → Attached)</summary>
    public struct CamouflageStartEvent
    {
        public GameObject Target;
        public CamouflageStartEvent(GameObject target) => Target = target;
    }

    /// <summary>완벽 의태 달성</summary>
    public struct CamouflageCompleteEvent
    {
        public GameObject Target;
        public CamouflageCompleteEvent(GameObject target) => Target = target;
    }

    /// <summary>의태 해제 (→ None)</summary>
    public struct CamouflageEndEvent
    {
        public GameObject Target;
        public CamouflageEndEvent(GameObject target) => Target = target;
    }

    // ================================================================
    // Tide Events (기존 TideEvents 대체)
    // ================================================================

    /// <summary>조류에 플레이어가 밀림</summary>
    public struct PlayerPushedByTideEvent
    {
        public Vector3 PushDirection;
        public float Force;
        public PlayerPushedByTideEvent(Vector3 dir, float force)
        {
            PushDirection = dir;
            Force = force;
        }
    }

    // ================================================================
    // Enemy Events (기존 EnemyEvents 대체)
    // ================================================================

    /// <summary>플레이어 둔화 (성게 등)</summary>
    public struct PlayerSlowedEvent
    {
        public Vector3 SourcePosition;
        public float SlowPercent;
        public float Duration;
        public PlayerSlowedEvent(Vector3 pos, float pct, float dur)
        {
            SourcePosition = pos;
            SlowPercent = pct;
            Duration = dur;
        }
    }
}
