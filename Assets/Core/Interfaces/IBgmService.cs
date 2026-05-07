using System;
using HideAndInk.Core.Audio;

namespace HideAndInk.Core.Interfaces
{
    /// <summary>
    /// 배경음악(BGM) 재생 서비스 인터페이스
    /// 컴포넌트에서 이 인터페이스를 주입받아 BGM 전환
    /// </summary>
    public interface IBgmService
    {
        /// <summary>현재 재생 중인 BGM</summary>
        BgmId CurrentBgm { get; }

        /// <summary>BGM 재생 (즉시 전환)</summary>
        void Play(BgmId id);

        /// <summary>BGM 재생 (페이드 인/아웃)</summary>
        void Play(BgmId id, float fadeDuration);

        /// <summary>BGM 정지 (페이드 아웃)</summary>
        void Stop(float fadeDuration = 0);

        /// <summary>일시 정지</summary>
        void Pause();

        /// <summary>재개</summary>
        void Resume();

        /// <summary>볼륨 설정 (0.0 ~ 1.0)</summary>
        void SetVolume(float volume);

        /// <summary>BGM 볼륨 (0.0 ~ 1.0)</summary>
        float Volume { get; set; }

        /// <summary>BGM 음소거 여부</summary>
        bool Muted { get; set; }

        /// <summary>볼륨 변경 이벤트</summary>
        event Action<float> OnVolumeChanged;

        /// <summary>음소거 변경 이벤트</summary>
        event Action<bool> OnMutedChanged;
    }
}
