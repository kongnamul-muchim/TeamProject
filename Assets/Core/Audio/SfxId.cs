namespace HideAndInk.Core.Audio
{
    /// <summary>
    /// 효과음(SFX) 식별자
    /// 문어먹물꿈질음악/ 폴더의 파일명을 기준으로 분류
    /// </summary>
    public enum SfxId
    {
        None = 0,

        // ===== UI =====
        /// <summary>버튼효과음0.wav</summary>
        ButtonClick,
        /// <summary>버튼효과음1.wav</summary>
        ButtonClick1,
        /// <summary>버튼효과음2.wav</summary>
        ButtonClick2,

        // ===== Player - Ink =====
        /// <summary>먹물발사효과음.wav / 먹물발사효과음.flac</summary>
        InkShoot,
        /// <summary>먹물발사효과음0.wav</summary>
        InkShoot1,
        /// <summary>먹물발사효과음1.wav</summary>
        InkShoot2,
        /// <summary>먹물발사효과음2.wav</summary>
        InkShoot3,

        // ===== Player - Camouflage =====
        /// <summary>의태효과음.mp3 — 붙을 때</summary>
        CamouflageAttach,
        /// <summary>의태효과음0.ogg — 완벽 의태</summary>
        CamouflagePerfect,
        /// <summary>의태효과음3.wav — 떨어질 때</summary>
        CamouflageDetach,

        // ===== Player - Charge =====
        /// <summary>충전효과음.wav</summary>
        Charge,
        /// <summary>충전효과음0.ogg</summary>
        ChargeLoop,

        // ===== Game State =====
        /// <summary>다음스테이지넘어갈때효과음.wav</summary>
        StageClear,
        /// <summary>게임오버효과음.wav</summary>
        GameOver,

        // ===== Predator (포식자) =====
        /// <summary>포식자발견및경고음.wav — 포식자 발견</summary>
        PredatorDetected,
        /// <summary>포식자수중통과음.mp3 — 포식자 스치고 지나감</summary>
        PredatorUnderwaterPass,
        /// <summary>포식자수중통과음0.wav</summary>
        PredatorUnderwaterPass1,
        /// <summary>포식자수중통과음1.wav</summary>
        PredatorUnderwaterPass2,
        /// <summary>포식자추격효과음.wav — 포식자 추격 시작</summary>
        PredatorChase,
        /// <summary>포식자추격효과음0.wav</summary>
        PredatorChase1,
        /// <summary>포식자추격효과음1.wav</summary>
        PredatorChase2,
        /// <summary>포식자추격효과음2.wav</summary>
        PredatorChase3,
    }
}
