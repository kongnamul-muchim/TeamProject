using HideAndInk.Core.Interfaces;

namespace HideAndInk.Core.Perception
{
    /// <summary>
    /// 의태 상태 머신 설정 구현체
    /// CamouflageAdapter가 인스펙터 값으로 생성하여 DI 컨테이너에 등록
    /// </summary>
    public sealed class CamouflageStateMachineConfig : ICamouflageStateMachineConfig
    {
        public float AttachDelay { get; }
        public float LockTime { get; }
        public float BlendTime { get; }
        public float PerfectTime { get; }

        public CamouflageStateMachineConfig(
            float attachDelay,
            float lockTime,
            float blendTime,
            float perfectTime)
        {
            AttachDelay = attachDelay;
            LockTime = lockTime;
            BlendTime = blendTime;
            PerfectTime = perfectTime;
        }
    }
}
