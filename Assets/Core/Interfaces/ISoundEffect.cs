namespace HideAndInk.Core.Interfaces
{
    /// <summary>
    /// 의태 사운드 효과 인터페이스
    /// SendMessage 대신 타입 안전한 직접 호출을 위한 인터페이스
    /// </summary>
    public interface ISoundEffect
    {
        void PlayAttachSound();
        void PlayPerfectSound();
        void PlayDetachSound();
    }
}
