using System;

namespace HideAndInk.Core.Interfaces
{
    /// <summary>
    /// 중앙 집중식 이벤트 버스 인터페이스.
    /// 정적 이벤트 클래스 대신 DI로 주입받아 사용.
    /// </summary>
    public interface IEventBus
    {
        /// <summary>
        /// 특정 타입의 이벤트 구독
        /// </summary>
        void Subscribe<T>(Action<T> handler);

        /// <summary>
        /// 특정 타입의 이벤트 구독 해제
        /// </summary>
        void Unsubscribe<T>(Action<T> handler);

        /// <summary>
        /// 이벤트 발행 (모든 구독자에게 전달)
        /// </summary>
        void Publish<T>(T args);
    }
}
