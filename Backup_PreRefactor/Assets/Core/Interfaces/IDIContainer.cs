using System;
using System.Collections.Generic;
using System.Linq;

namespace HideAndInk.Core.Interfaces
{
    /// <summary>
    /// DI 컨테이너에서 지원하는 생명주기 유형
    /// </summary>
    public enum ServiceLifetime
    {
        /// <summary>
        /// 매번 새로운 인스턴스 생성
        /// </summary>
        Transient,
        
        /// <summary>
        /// 같은 스코프 내에서 하나의 인스턴스 공유
        /// </summary>
        Scoped,
        
        /// <summary>
        /// 애플리케이션 전역에서 하나의 인스턴스 공유
        /// </summary>
        Singleton
    }

    /// <summary>
    /// DI 컨테이너 인터페이스
    /// </summary>
    public interface IDIContainer : IDisposable
    {
        /// <summary>
        /// 서비스 등록
        /// </summary>
        /// <typeparam name="TInterface">인터페이스 타입</typeparam>
        /// <typeparam name="TImplementation">구현체 타입</typeparam>
        /// <param name="lifetime">생명주기</param>
        void Register<TInterface, TImplementation>(ServiceLifetime lifetime = ServiceLifetime.Transient) 
            where TInterface : class 
            where TImplementation : class, TInterface;

        /// <summary>
        /// 구현체 직접 등록 (인터페이스 없이)
        /// </summary>
        /// <typeparam name="TImplementation">구현체 타입</typeparam>
        /// <param name="lifetime">생명주기</param>
        void Register<TImplementation>(ServiceLifetime lifetime = ServiceLifetime.Transient) 
            where TImplementation : class;

        /// <summary>
        /// 인스턴스 직접 등록
        /// </summary>
        /// <typeparam name="TInterface">인터페이스 타입</typeparam>
        /// <param name="instance">인스턴스</param>
        /// <param name="lifetime">생명주기</param>
        void RegisterInstance<TInterface>(TInterface instance, ServiceLifetime lifetime = ServiceLifetime.Singleton) 
            where TInterface : class;

        /// <summary>
        /// 서비스 해결
        /// </summary>
        /// <typeparam name="T">해결할 타입</typeparam>
        /// <returns>인스턴스</returns>
        T Resolve<T>() where T : class;

        /// <summary>
        /// 스코프 생성
        /// </summary>
        /// <returns>새 스코프 컨테이너</returns>
        IDIContainer CreateScope();

        /// <summary>
        /// 특정 타입이 등록되어 있는지 확인
        /// </summary>
        /// <typeparam name="T">확인할 타입</typeparam>
        /// <returns>등록 여부</returns>
        bool IsRegistered<T>() where T : class;
    }
}