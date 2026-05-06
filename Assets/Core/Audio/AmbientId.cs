namespace HideAndInk.Core.Audio
{
    /// <summary>
    /// 환경사운드(Ambient) 식별자
    /// 문어먹물꿈질음악/ 폴더의 환경사운드 파일명 기준
    /// BGM 위에 깔리는 배경 루프 사운드 (BGM과 쌍)
    /// </summary>
    public enum AmbientId
    {
        /// <summary>무음</summary>
        None = 0,

        /// <summary>초원해안 환경사운드.wav</summary>
        GrasslandCoast,

        /// <summary>산호초환경사운드.flac</summary>
        CoralReef,

        /// <summary>해초숲환경사운드.mp3</summary>
        SeaweedForest,

        /// <summary>심해절벽환경사운드.wav</summary>
        DeepSeaCliff,

        /// <summary>심해페허환경사운드.wav</summary>
        DeepSeaRuins,
    }
}
