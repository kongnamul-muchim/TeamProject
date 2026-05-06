using System;
using System.Collections.Generic;
using HideAndInk.Core.Interfaces;

namespace HideAndInk.Core.Managers
{
    /// <summary>
    /// 중앙 집중식 이벤트 버스 구현체.
    /// 모든 이벤트 발행/구독을 이곳에서 관리.
    /// Singleton 생명주기로 DI 컨테이너에 등록.
    /// </summary>
    public sealed class EventBus : IEventBus
    {
        // 타입별 핸들러 목록. object 타입으로 저장 후 내부 캐스팅.
        private readonly Dictionary<Type, Delegate> _handlers = new Dictionary<Type, Delegate>();
        private readonly object _lock = new object();

        /// <summary>
        /// 특정 타입의 이벤트 구독
        /// </summary>
        public void Subscribe<T>(Action<T> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            lock (_lock)
            {
                var type = typeof(T);
                if (_handlers.TryGetValue(type, out var existing))
                {
                    _handlers[type] = Delegate.Combine(existing, handler);
                }
                else
                {
                    _handlers[type] = handler;
                }
            }
        }

        /// <summary>
        /// 특정 타입의 이벤트 구독 해제
        /// </summary>
        public void Unsubscribe<T>(Action<T> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            lock (_lock)
            {
                var type = typeof(T);
                if (_handlers.TryGetValue(type, out var existing))
                {
                    var result = Delegate.Remove(existing, handler);
                    if (result == null)
                    {
                        _handlers.Remove(type);
                    }
                    else
                    {
                        _handlers[type] = result;
                    }
                }
            }
        }

        /// <summary>
        /// 이벤트 발행 (모든 구독자에게 동기 전달)
        /// </summary>
        public void Publish<T>(T args)
        {
            Delegate handler;
            lock (_lock)
            {
                if (!_handlers.TryGetValue(typeof(T), out handler) || handler == null)
                {
                    return;
                }
            }

            // lock 밖에서 실행 (핸들러 내에서 재구독 가능하도록)
            if (handler is Action<T> typedHandler)
            {
                typedHandler.Invoke(args);
            }
        }
    }
}
