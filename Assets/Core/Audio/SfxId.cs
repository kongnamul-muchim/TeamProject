namespace HideAndInk.Core.Audio
{
    public enum SfxId
    {
        None = 0,

        // ===== UI =====
        ButtonClick,

        // ===== Player - Ink =====
        InkShoot,

        // ===== Player - Camouflage =====
        CamouflageAttach,
        CamouflagePerfect,
        CamouflageDetach,

        // ===== Player - Charge =====
        Charge,

        // ===== Game State =====
        StageClear,
        GameOver,

        // ===== Predator (포식자) =====
        PredatorDetected,
        PredatorUnderwaterPass,
        PredatorChase,
    }
}
