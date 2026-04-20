using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HideAndInk.Core.Interfaces;

namespace HideAndInk.Core.Managers
{
    /// <summary>
    /// 서비스 설명자 (내부용)
    /// </summary>
    internal sealed class ServiceDescriptor
    {
        public Type ServiceType { get; set; }
        public Type ImplementationType { get; set; }
        public ServiceLifetime Lifetime { get; set; }
        public object Instance { get; set; }
    }

    /// <summary>
    /// DI 컨테이너 구현체
    /// 순수 C#으로 작성되어 Unity에서 사용 가능
    /// </summary>
    public sealed class DIContainer : IDIContainer
    {
        private readonly Dictionary<Type, ServiceDescriptor> _services = new Dictionary<Type, ServiceDescriptor>();
        private readonly Dictionary<Type, object> _singletons = new Dictionary<Type, object>();
        private readonly Dictionary<Type, object> _scopedInstances = new Dictionary<Type, object>(); // Scoped 인스턴스 캐시
        private readonly DIContainer _parentContainer; // 부모 컨테이너 참조 (스코프 체이닝)
        private readonly bool _isScope;
        private bool _disposed;

        /// <summary>
        /// 기본 생성자 (루트 컨테이너)
        /// </summary>
        public DIContainer()
        {
            _isScope = false;
            _parentContainer = null;
        }

        /// <summary>
        /// 스코프 생성자 (내부용)
        /// </summary>
        private DIContainer(DIContainer parent)
        {
            _isScope = true;
            _parentContainer = parent;
        }

        /// <summary>
        /// 서비스 등록
        /// </summary>
        public void Register<TInterface, TImplementation>(ServiceLifetime lifetime = ServiceLifetime.Transient) 
            where TInterface : class 
            where TImplementation : class, TInterface
        {
            var interfaceType = typeof(TInterface);
            var implementationType = typeof(TImplementation);

            ValidateRegistration(interfaceType, implementationType);

            var descriptor = new ServiceDescriptor
            {
                ServiceType = interfaceType,
                ImplementationType = implementationType,
                Lifetime = lifetime
            };

            _services[interfaceType] = descriptor;
        }

        /// <summary>
        /// 구현체 직접 등록
        /// </summary>
        public void Register<TImplementation>(ServiceLifetime lifetime = ServiceLifetime.Transient) 
            where TImplementation : class
        {
            var implementationType = typeof(TImplementation);

            var descriptor = new ServiceDescriptor
            {
                ServiceType = implementationType,
                ImplementationType = implementationType,
                Lifetime = lifetime
            };

            _services[implementationType] = descriptor;
        }

        /// <summary>
        /// 인스턴스 직접 등록
        /// </summary>
        public void RegisterInstance<TInterface>(TInterface instance, ServiceLifetime lifetime = ServiceLifetime.Singleton) 
            where TInterface : class
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance), "인스턴스가 null일 수 없습니다.");
            }

            var interfaceType = typeof(TInterface);

            var descriptor = new ServiceDescriptor
            {
                ServiceType = interfaceType,
                ImplementationType = interfaceType,
                Lifetime = lifetime,
                Instance = instance
            };

            _services[interfaceType] = descriptor;

            if (lifetime == ServiceLifetime.Singleton)
            {
                _singletons[interfaceType] = instance;
            }
        }

        /// <summary>
        /// 서비스 해결
        /// </summary>
        public T Resolve<T>() where T : class
        {
            return (T)Resolve(typeof(T));
        }

        /// <summary>
        /// 스코프 생성
        /// </summary>
        public IDIContainer CreateScope()
        {
            var scopeContainer = new DIContainer(parent: this);
            
            foreach (var kvp in _services)
            {
                scopeContainer._services[kvp.Key] = kvp.Value;
            }

            return scopeContainer;
        }

        /// <summary>
        /// 등록 여부 확인
        /// </summary>
        public bool IsRegistered<T>() where T : class
        {
            return _services.ContainsKey(typeof(T));
        }

        /// <summary>
        /// 실제 해결 로직
        /// </summary>
        private object Resolve(Type serviceType)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(DIContainer), "이미 삭제된 컨테이너입니다.");
            }

            if (!_services.TryGetValue(serviceType, out var descriptor))
            {
                // 부모 컨테이너에서 탐색 (스코프 체이닝)
                if (_parentContainer != null)
                {
                    return _parentContainer.Resolve(serviceType);
                }

                throw new InvalidOperationException(
                    $"서비스 '{serviceType.Name}'이(가) 등록되어 있지 않습니다. " +
                    $"Register<TInterface, TImplementation>()으로 등록해주세요.");
            }

            return CreateInstance(descriptor);
        }

        /// <summary>
        /// 인스턴스 생성
        /// </summary>
        private object CreateInstance(ServiceDescriptor descriptor)
        {
            // Singleton: 전역 인스턴스 재사용
            if (descriptor.Lifetime == ServiceLifetime.Singleton && descriptor.Instance != null)
            {
                return descriptor.Instance;
            }

            if (descriptor.Lifetime == ServiceLifetime.Singleton && _singletons.TryGetValue(descriptor.ServiceType, out var existingSingleton))
            {
                return existingSingleton;
            }

            // Scoped: 스코프 내 인스턴스 재사용
            if (descriptor.Lifetime == ServiceLifetime.Scoped && _scopedInstances.TryGetValue(descriptor.ServiceType, out var existingScoped))
            {
                return existingScoped;
            }

            var constructor = GetInjectableConstructor(descriptor.ImplementationType);
            if (constructor == null)
            {
                throw new InvalidOperationException(
                    $"'{descriptor.ImplementationType.Name}'에 주입 가능한 생성자가 없습니다. " +
                    $"하나의 public 생성자를 정의하거나 [Inject] 특성을 사용해주세요.");
            }

            var parameters = constructor.GetParameters();
            var parameterInstances = new List<object>();

            foreach (var parameter in parameters)
            {
                var parameterInstance = Resolve(parameter.ParameterType);
                parameterInstances.Add(parameterInstance);
            }

            var instance = constructor.Invoke(parameterInstances.ToArray());

            // Singleton 캐싱
            if (descriptor.Lifetime == ServiceLifetime.Singleton)
            {
                _singletons[descriptor.ServiceType] = instance;
            }

            // Scoped 캐싱
            if (descriptor.Lifetime == ServiceLifetime.Scoped)
            {
                _scopedInstances[descriptor.ServiceType] = instance;
            }

            return instance;
        }

        /// <summary>
        /// 주입 가능한 생성자 찾기
        /// </summary>
        private ConstructorInfo GetInjectableConstructor(Type implementationType)
        {
            var constructors = implementationType.GetConstructors();

            if (constructors.Length == 0)
            {
                return null;
            }

            if (constructors.Length == 1)
            {
                return constructors[0];
            }

            foreach (var constructor in constructors)
            {
                var attributes = constructor.GetCustomAttributes(typeof(InjectAttribute), true);
                if (attributes.Length > 0)
                {
                    return constructor;
                }
            }

            return constructors.OrderByDescending(c => c.GetParameters().Length).First();
        }

        /// <summary>
        /// 등록 검증
        /// </summary>
        private void ValidateRegistration(Type interfaceType, Type implementationType)
        {
            if (interfaceType == null)
            {
                throw new ArgumentNullException(nameof(interfaceType));
            }

            if (implementationType == null)
            {
                throw new ArgumentNullException(nameof(implementationType));
            }

            if (!interfaceType.IsAssignableFrom(implementationType))
            {
                throw new InvalidOperationException(
                    $"'{implementationType.Name}'은(는) '{interfaceType.Name}'을(를) 상속하지 않습니다.");
            }
        }

        /// <summary>
        /// IDisposable 구현
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            // Scoped 인스턴스 먼저 정리
            foreach (var scoped in _scopedInstances.Values)
            {
                if (scoped is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            _scopedInstances.Clear();

            foreach (var singleton in _singletons.Values)
            {
                if (singleton is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            _singletons.Clear();
            _services.Clear();
        }
    }

    /// <summary>
    /// 주입 특성을 표시하여 특정 생성자를 선택
    /// </summary>
    [AttributeUsage(AttributeTargets.Constructor)]
    public sealed class InjectAttribute : Attribute
    {
    }
}