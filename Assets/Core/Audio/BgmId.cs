namespace HideAndInk.Core.Audio
{
    /// <summary>
    /// 배경음악(BGM) 식별자
    /// 문어먹물꿈질음악/ 폴더의 BGM 파일명 기준
    /// </summary>
    public enum BgmId
    {
        /// <summary>무음</summary>
        None = 0,

        /// <summary>초원해안Bgm.mp3 — 스테이지 1</summary>
        GrasslandCoast,

        /// <summary>산호초Bgm.wav — 스테이지 2</summary>
        CoralReef,

        /// <summary>해초숲Bgm.mp3 — 스테이지 3</summary>
        SeaweedForest,

        /// <summary>심해절벽Bgm.wav / 심해절벽1Bgm.mp3 / 심해절벽2Bgm.WAV — 스테이지 4</summary>
        DeepSeaCliff,

        /// <summary>심해페허Bgm.mp3 / 심해페허0Bgm.wav — 스테이지 5</summary>
        DeepSeaRuins,
    }
}
