using UnityEngine;
using HideAndInk.Core.Enemy.Interfaces;

namespace HideAndInk.Core.Enemy.Boss.Gimmicks
{
    /// <summary>
    /// 보스 기믹 타입
    /// </summary>
    public enum GimmickType
    {
        Ambush,             // Ch.1 가자미: 매복 → 기습
        RelentlessChase,    // Ch.2 곰치: 집요한 추격
        ElectricZone,       // Ch.3 전기뱀장어: 감전 구역
        DashCharge          // Ch.4 상어: 초고속 돌진
    }

    /// <summary>
    /// 보스 기믹 공통 인터페이스
    /// 각 보스 고유 능력을 캡슐화하여 BossEnemyController에 주입
    /// </summary>
    public interface IEnemyGimmick
    {
        /// <summary>
        /// 기믹 타입
        /// </summary>
        GimmickType Type { get; }

        /// <summary>
        /// 기믹 활성화 (보스 시작 시 호출)
        /// </summary>
        void OnActivate(Transform bossTransform);

        /// <summary>
        /// 기믹 비활성화 (보스 종료 시 호출)
        /// </summary>
        void OnDeactivate();

        /// <summary>
        /// Patrol 상태 진입 시 호출
        /// </summary>
        void OnPatrolEnter();

        /// <summary>
        /// Patrol 상태 업데이트
        /// </summary>
        void OnPatrolUpdate(float deltaTime);

        /// <summary>
        /// Patrol 상태 종료 시 호출
        /// </summary>
        void OnPatrolExit();

        /// <summary>
        /// Chase 상태 진입 시 호출
        /// </summary>
        void OnChaseEnter();

        /// <summary>
        /// Chase 상태 업데이트
        /// </summary>
        void OnChaseUpdate(float deltaTime);

        /// <summary>
        /// Chase 상태 종료 시 호출
        /// </summary>
        void OnChaseExit();

        /// <summary>
        /// Search 상태 진입 시 호출
        /// </summary>
        void OnSearchEnter();

        /// <summary>
        /// Search 상태 업데이트
        /// </summary>
        void OnSearchUpdate(float deltaTime);

        /// <summary>
        /// Search 상태 종료 시 호출
        /// </summary>
        void OnSearchExit();

        /// <summary>
        /// 이동 제어권 여부 (true = 기믹이 이동 목표 계산, false = Behavior가 기본 순찰)
        /// </summary>
        bool HasMovementOverride { get; }

        /// <summary>
        /// Patrol 상태 이동 목표 계산 (Z축 제한 적용)
        /// </summary>
        /// <param name="currentPos">보스 현재 위치</param>
        /// <param name="bounds">Ground 경계</param>
        /// <returns>목표 위치 (null = Behavior 기본 로직 사용)</returns>
        Vector3? GetPatrolTarget(Vector3 currentPos, GroundBounds bounds);

        /// <summary>
        /// Search 상태 이동 목표 계산 (Z축 제한 적용)
        /// </summary>
        /// <param name="currentPos">보스 현재 위치</param>
        /// <param name="lastKnownPos">Player 마지막 발견 위치</param>
        /// <param name="bounds">Ground 경계</param>
        /// <returns>목표 위치 (null = Behavior 기본 로직 사용)</returns>
        Vector3? GetSearchTarget(Vector3 currentPos, Vector3 lastKnownPos, GroundBounds bounds);
    }
}
