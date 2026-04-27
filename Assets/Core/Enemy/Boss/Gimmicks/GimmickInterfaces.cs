using UnityEngine;

namespace HideAndInk.Core.Enemy.Boss.Gimmicks
{
    /// <summary>
    /// Player Transform 및 의심도 정보를 기믹에 전달
    /// BossEnemyController가 매 프레임 호출
    /// </summary>
    public interface IGimmickPlayerAware
    {
        /// <summary>Player Transform 설정 (매 프레임)</summary>
        void SetPlayerTransform(Transform playerTransform);

        /// <summary>현재 의심도 (0~1 정규화) 전달</summary>
        void SetSuspicionLevel(float normalizedSuspicion);

        /// <summary>Player 의태 상태 전달 (true=숨음, 위치 업데이트 차단)</summary>
        void SetCamouflageState(bool isCamouflaging);

        /// <summary>Player가 현재 시야에 보이는지 전달 (true=보임, false=안 보임)</summary>
        void SetPlayerVisible(bool isVisible);
    }

    /// <summary>
    /// 기믹이 ViewDirection(시선 방향)을 직접 제어
    /// 돌진 인디케이터 표시 여부도 함께 관리
    /// </summary>
    public interface IGimmickViewDirection
    {
        /// <summary>true면 Controller가 이 기믹의 방향을 따라야 함</summary>
        bool OverridesViewDirection { get; }

        /// <summary>바라볼 방향 벡터 (OverridesViewDirection=true일 때)</summary>
        Vector3 GetViewDirectionVector();

        /// <summary>돌진 예고 인디케이터 표시 여부</summary>
        bool ShowChargeIndicator { get; }
    }

    /// <summary>
    /// 기믹이 전투 사이클 중인지 여부를 컨트롤러에 알림
    /// Search 전환 차단 + 데미지 판정에 사용
    /// </summary>
    public interface IGimmickCombatCycle
    {
        /// <summary>true면 Controller는 Search 전환을 막음</summary>
        bool IsInCombatCycle { get; }

        /// <summary>true면 현재 돌진 중 = 데미지 입힐 수 있음</summary>
        bool IsCharging { get; }
    }

    /// <summary>
    /// Player를 잃었을 때 Search를 건너뛰고 Patrol로 직행할지 결정
    /// </summary>
    public interface IGimmickTransitionOverride
    {
        /// <summary>Player 상실 시 Search 대신 Patrol로 전환할지 여부</summary>
        bool ShouldSkipSearchOnLostPlayer(float normalizedSuspicion);
    }
}
