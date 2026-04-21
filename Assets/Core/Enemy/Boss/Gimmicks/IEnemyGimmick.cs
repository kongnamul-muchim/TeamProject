using UnityEngine;

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
        LureBait,           // Ch.4 아귀: 발광 미끼
        DashCharge          // Ch.5 백상아리: 초고속 돌진
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
    }
}
