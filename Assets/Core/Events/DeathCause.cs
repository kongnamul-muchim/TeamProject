namespace HideAndInk.Core.Events
{
    /// <summary>
    /// 플레이어 사망 원인 분류
    /// 각 값은 대응하는 SfxId, 로그 메시지에 매핑
    /// </summary>
    public enum DeathCause
    {
        /// <summary>알 수 없음 (Fallback)</summary>
        Unknown = 0,

        // ===== 일반 적 =====
        /// <summary>일반 몬스터(게) 접촉</summary>
        CrabAttack,
        /// <summary>정예 몬스터 공격</summary>
        EliteAttack,

        // ===== 보스 =====
        /// <summary>가자미 돌진 (AmbushGimmick)</summary>
        GajamiDash,
        /// <summary>곰치 돌진 (RelentlessChaseGimmick)</summary>
        MorayCharge,
        /// <summary>청새치 돌진 (SwordfishGimmick)</summary>
        SwordfishCharge,
        /// <summary>백상아리 돌진 (DashChargeGimmick)</summary>
        GreatWhiteCharge,
        /// <summary>보스 접촉 (기타/일반)</summary>
        BossCollision,

        // ===== 환경 =====
        /// <summary>의태 중 오브젝트 파괴로 인한 사망</summary>
        CamouflageObstacleDestroyed,
        /// <summary>낙사 / 맵 이탈</summary>
        Drowning,
        /// <summary>독 / 환경 데미지</summary>
        Poisoned,

        // ===== 시스템 =====
        /// <summary>디버그 명령어로 사망</summary>
        Debug,
    }
}
