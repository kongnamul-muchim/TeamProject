using HideAndInk.Core.Audio;

namespace HideAndInk.Core.Interfaces
{
    /// <summary>
    /// [DEPRECATED] 의태 사운드 효과 인터페이스
    /// ISfxService로 대체됩니다. (Play(SfxId.CamouflageAttach) 등으로 사용)
    /// 
    /// 기존 코드와의 호환성을 위해 유지하되,
    /// 신규 코드는 ISfxService를 주입받아 사용하세요.
    /// </summary>
    public interface ISoundEffect
    {
        [System.Obsolete("Use ISfxService.Play(SfxId.CamouflageAttach) instead")]
        void PlayAttachSound();

        [System.Obsolete("Use ISfxService.Play(SfxId.CamouflagePerfect) instead")]
        void PlayPerfectSound();

        [System.Obsolete("Use ISfxService.Play(SfxId.CamouflageDetach) instead")]
        void PlayDetachSound();
    }
}
