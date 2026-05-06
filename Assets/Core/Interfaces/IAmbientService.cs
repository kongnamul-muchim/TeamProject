using HideAndInk.Core.Audio;

namespace HideAndInk.Core.Interfaces
{
    /// <summary>
    /// 환경사운드(Ambient) 재생 서비스 인터페이스
    /// BGM 위에 자연스럽게 겹쳐 재생되는 배경 루프 사운드
    /// </summary>
    public interface IAmbientService
    {
        /// <summary>현재 재생 중인 Ambient</summary>
        AmbientId CurrentAmbient { get; }

        /// <summary>Ambient 재생 (페이드 인)</summary>
        void Play(AmbientId id);

        /// <summary>Ambient 재생 (페이드 인/아웃 지정)</summary>
        void Play(AmbientId id, float fadeDuration);

        /// <summary>Ambient 정지</summary>
        void Stop(float fadeDuration = 0);

        /// <summary>볼륨 설정</summary>
        void SetVolume(float volume);
    }
}
