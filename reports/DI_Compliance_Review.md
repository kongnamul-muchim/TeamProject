# DI (Dependency Injection) 준수 여부 검토 보고서

> 검토일: 2026-04-29
> 기준 문서: `Agents.md` (🏗️ DI 컨테이너 아키텍처 섹션)
> 검토 범위: `Assets/Core/`, `Assets/Scripts/` 전체 C# 스크립트

---

## 📋 검토 기준 (Agents.md 발췌)

| 체크리스트 항목 | 설명 |
|---------------|------|
| 인터페이스로 추상화했는가? (DIP) | 모든 의존성은 인터페이스에 의존 |
| 의존성은 생성자로 주입받는가? (DI) | 순수 C# 클래스는 생성자 주입 |
| MonoBehaviour는 생성자 주입이 안 되므로 Adapter 패턴 활용 | Adapter → GameManager.Container.Resolve |
| 서비스 생명주기를 적절히 선택했는가? | Transient/Scoped/Singleton |
| 등록 전에 IsRegistered<T>()로 중복 등록 확인할 것 | 안전장치 |

---

## 1. ✅ DI 컨테이너 코어 — 정상

### `IDIContainer` (Core/Interfaces/)
- 인터페이스 완전: `Register<T,T>`, `Register<T>`, `RegisterInstance<T>`, `Resolve<T>`, `CreateScope()`, `IsRegistered<T>`
- `ServiceLifetime` 열거형 (Transient/Scoped/Singleton)
- ✅ **문제 없음**

### `DIContainer` (Core/Managers/)
- 순수 C# 구현, `UnityEngine` 의존성 없음
- 생성자 파라미터 자동 해결 (리플렉션 기반)
- `InjectAttribute`로 특정 생성자 지정 가능
- Singleton/Scoped 캐싱 및 Dispose 지원
- ✅ **문제 없음**

### `GameManager` (Core/Managers/)
- `Awake()`에서 DIContainer 생성 및 초기화
- 정적 `Container` 프로퍼티로 전역 접근 제공
- ✅ **문제 없음**

---

## 2. ✅ DI Adapter 패턴 사용 — 정상 사례

### `PlayerMovementAdapter` (Scripts/Player/)
```csharp
if (GameManager.Container != null && GameManager.Container.IsRegistered<IPlayerMovement>())
{
    _playerMovement = GameManager.Container.Resolve<IPlayerMovement>();
}
else
{
    _playerMovement = new PlayerMovement(horizontalSpeed, verticalSpeed, acceleration, friction);
}
```
- ✅ `IsRegistered` 확인 후 `Resolve`
- ✅ Fallback (`new`) 처리
- ✅ SerializeField로 `MovementLogger` 주입

### `CamouflageAdapter` (Scripts/Player/)
```csharp
// ICamouflageDetector
if (GameManager.Container.IsRegistered<ICamouflageDetector>())
    _detector = GameManager.Container.Resolve<ICamouflageDetector>();
else
    _detector = new CamouflageDetector(detectionRadius);

// ICamouflageStateMachine
if (GameManager.Container.IsRegistered<ICamouflageStateMachine>())
    _stateMachine = GameManager.Container.Resolve<ICamouflageStateMachine>();
else
    _stateMachine = new CamouflageStateMachine(attachDelay, lockTime, blendTime, perfectTime);
```
- ✅ DI + Fallback 패턴 정확히 구현
- ⚠️ `MaterialCloner`는 `new`로 직접 생성 (DI 미사용)

---

## 3. ❌ 심각한 DI 위반 사례

### 3.1 등록된 서비스가 단 1개뿐

`GameManager.RegisterCoreServices()`:
```csharp
_rootContainer.RegisterInstance<IGameStateMachine>(_gameStateMachine, ServiceLifetime.Singleton);
```

**등록된 서비스**: `IGameStateMachine` **단 하나**

**누락된 서비스 (인터페이스는 있으나 등록 안 됨)**:

| 인터페이스 | 구현체 | 위치 |
|-----------|--------|------|
| `IPlayerMovement` | `PlayerMovement` | `Core/Player/` |
| `ICamouflageStateMachine` | `CamouflageStateMachine` | `Core/Perception/` |
| `ICamouflageDetector` | `CamouflageDetector` | `Core/Perception/` |
| `IMaterialCloner` | `MaterialCloner` | `Core/Perception/` |
| `IEnemyMovement` | `EnemyMovement` | `Core/Enemy/Movement/` |
| `ISuspicionMeter` | (미구현) | `Core/Interfaces/` |
| `ISpriteDirector` | `SpriteDirector` | `Scripts/Player/` |

→ **Adapter들이 `new`로 직접 생성하는 fallback에 항상 의존하고 있음**

---

### 3.2 SuspicionManager — Singleton 패턴 사용 (DI 미준수)

**파일**: `Core/Perception/SuspicionManager.cs`
```csharp
public sealed class SuspicionManager : Singleton<SuspicionManager>
{
    // ...
    protected override void Awake()
    {
        base.Awake();
        ResetSuspicion();
    }
}
```

**문제점**:
- `Singleton<SuspicionManager>`를 사용하여 DI 컨테이너를 **완전히 우회**
- 여러 곳에서 `SuspicionManager.Instance`로 직접 접근
  - `EnemyPerception.Awake()` → `_suspicionManager = SuspicionManager.Instance`
  - `SuspicionToGameStateLink` → `SuspicionManager.Instance.OnDetected`
  - `CamouflageToSuspicionLink` → `SuspicionManager.Instance.SetCamouflageState`

**영향**: Medium
- 의심도 시스템의 핵심이 DI 컨테이너 밖에서 관리됨
- 테스트/모킹이 어려움

---

### 3.3 SuspicionUIManager — Singleton 패턴 사용

**파일**: `Core/Perception/SuspicionUIManager.cs`
```csharp
public sealed class SuspicionUIManager : Singleton<SuspicionUIManager>
```

- `BossSuspicionSystem.Awake()`에서 `SuspicionUIManager.Instance?.Register(this)`로 접근
- `BossSuspicionSystem.OnDestroy()`에서 `SuspicionUIManager.Instance?.Unregister(this)`로 접근

---

### 3.4 LogModule — Singleton 패턴 사용

**파일**: `Core/Logging/LogModule.cs`
```csharp
public class LogModule : Singleton<LogModule>
```

- Agents.md에서 `LogModule.Instance.Log()` 사용을 명시하므로 **의도된 예외**로 간주
- 하지만 다른 시스템들이 `LogModule.Instance`를 직접 호출하는 것은 DI 원칙 위반

---

### 3.5 EnemyAIStateMachine — "DI Container에서 관리"라지만 실제로는 `new`로 생성

**파일**: `Core/Enemy/AI/EnemyAIStateMachine.cs` (10-11행)
```csharp
/// <summary>
/// Enemy AI 상태 머신
/// 순수 C# 클래스로 DI Container에서 관리
/// </summary>
```

**실제 사용 (BossEnemyController.cs, 398-404행)**:
```csharp
private void InitializeStateMachine()
{
    _stateMachine = new EnemyAIStateMachine(_patrolBehavior, _chaseBehavior, _searchBehavior);
    _stateMachine.OnStateChanged += OnAIStateChanged;
    _stateMachine.Initialize(EnemyAIState.Patrol);
    _movement.Speed = patrolSpeed;
}
```

- 주석에는 "DI Container에서 관리"라고 쓰여있지만 **실제로는 직접 `new`로 생성**
- 생성자 주입 패턴 (`IEnemyAIState` 3개를 파라미터로 받음)은 DI-friendly하지만 **컨테이너를 통해 생성되지 않음**

---

### 3.6 EnemyMovement — 모든 곳에서 `new`로 직접 생성

**파일**:
- `EnemyAIController.InitializeMovement()` (128-136행)
- `NormalEnemyController.InitializeMovement()` (64-72행)
- `EliteEnemyController.InitializeMovement()` (66-74행)

```csharp
_movement = new EnemyMovement(
    enemy: this,
    speed: moveSpeed,
    acceleration: acceleration,
    // ...
);
```

- 생성자 자체는 DI-friendly (`IEnemy`를 받음)
- **DI 컨테이너를 전혀 사용하지 않음**

---

### 3.7 Boss Behavior들 — `new`로 직접 생성

**파일**: `BossEnemyController.InitializeBehaviors()` (386-396행)
```csharp
_patrolBehavior = new PatrolBehavior(this, _movement, _activeGimmick);
_chaseBehavior = new ChaseBehavior(this, _movement, _playerTransform, predictionTime: 0.5f);
_searchBehavior = new SearchBehavior(this, _movement, _activeGimmick, searchDuration, searchDistance);
```

- `PatrolBehavior`, `ChaseBehavior`, `SearchBehavior`가 DI 없이 `new`로 생성됨
- 이들은 Behavior 인터페이스(`IEnemyAIState`)를 구현하므로 DI 컨테이너에 등록하여 관리 가능

---

## 4. ⚠️ 중간 수준 문제

### 4.1 FindObjectOfType / FindWithTag 남용

여러 MonoBehaviour에서 DI 대신 `FindObjectOfType`/`FindWithTag`로 의존성 탐색:

| 위치 | 코드 |
|------|------|
| `EnemyAIController.FindPlayer()` | `GameObject.FindWithTag("Player")` |
| `EnemyAIController.CacheCamouflageAdapter()` | `FindObjectOfType<CamouflageAdapter>()` |
| `NormalEnemyController.CachePlayerComponents()` | `FindObjectOfType<PlayerLives>()` |
| `BossEnemyController.CacheBossPlayerComponents()` | `FindObjectOfType<PlayerLives>()` |
| `BossSuspicionSystem.FindPlayerPosition()` | `GameObject.FindWithTag("Player")` |
| `GameManager.SubscribeToEvents()` | `FindObjectOfType<SuspicionToGameStateLink>()` |

→ 런타임 성능 저하 + DI 원칙 위반

### 4.2 Static Event Classes

- `GameEvents`, `CamouflageEvents`, `TideEvents`, `EnemyEvents`
- 전역 정적 이벤트는 Unity에서 널리 쓰이는 패턴이지만, DI 관점에서는 서비스로 등록하여 주입받는 것이 더 바람직
- 현재는 모든 시스템이 정적 이벤트에 직접 의존

---

## 5. 📊 종합 평가표

| 파일 | DI 준수 | 비고 |
|------|---------|------|
| `DIContainer.cs` | ✅ | 순수 C#, 생성자 주입, Dispose |
| `IDIContainer.cs` | ✅ | 인터페이스 완전 |
| `GameManager.cs` | ✅** | 단 1개 서비스만 등록 (**큰 문제**) |
| `PlayerMovementAdapter.cs` | ✅ | Adapter + DI + Fallback |
| `CamouflageAdapter.cs` | ✅ | Adapter + DI + Fallback |
| `PlayerMovement.cs` | ✅ | 순수 C#, 생성자 주입 |
| `CamouflageStateMachine.cs` | ✅ | 순수 C#, 생성자 주입 |
| `CamouflageDetector.cs` | ✅ | 순수 C#, 생성자 주입 |
| `GameStateMachine.cs` | ✅ | 순수 C#, 생성자 주입 |
| `EnemyAIStateMachine.cs` | ❌ | 주석과 달리 `new`로 생성 |
| `EnemyMovement.cs` | ❌ | 모든 곳에서 `new`로 생성 |
| `SuspicionManager.cs` | ❌ | Singleton 패턴, DI 우회 |
| `SuspicionUIManager.cs` | ❌ | Singleton 패턴, DI 우회 |
| `LogModule.cs` | ⚠️ | Singleton 패턴 (의도된 예외) |
| `EnemyAIController.cs` | ❌ | `new EnemyMovement()` |
| `NormalEnemyController.cs` | ❌ | `new EnemyMovement()` |
| `EliteEnemyController.cs` | ❌ | `new EnemyMovement()` |
| `BossEnemyController.cs` | ❌ | Behaviors/StateMachine 모두 `new` |
| `PlayerLives.cs` | ⚠️ | GameManager.Instance 직접 접근 |
| `BossSuspicionSystem.cs` | ⚠️ | Singleton + FindWithTag |
| `EnemyPerception.cs` | ⚠️ | SuspicionManager.Instance 직접 접근 |
| `MaterialCloner.cs` | ⚠️ | `new`로 생성, DI 미사용 |

---

## 6. 🔧 권장 수정 사항

### 필수 (High Priority)

1. **서비스 등록 확대** — `GameManager.RegisterCoreServices()`에 다음 추가:
   ```csharp
   _rootContainer.Register<IPlayerMovement, PlayerMovement>(ServiceLifetime.Transient);
   _rootContainer.Register<ICamouflageDetector, CamouflageDetector>(ServiceLifetime.Transient);
   _rootContainer.Register<ICamouflageStateMachine, CamouflageStateMachine>(ServiceLifetime.Transient);
   _rootContainer.Register<IEnemyMovement, EnemyMovement>(ServiceLifetime.Transient);
   ```

2. **SuspicionManager → DI 컨테이너**:
   - `Singleton<SuspicionManager>` 제거
   - `GameManager.Container`에 Singleton으로 등록
   - `SuspicionManager.Instance` → DI Resolve로 대체

3. **EnemyAIStateMachine DI 적용**:
   - `BossEnemyController.InitializeStateMachine()`에서 `new` 대신 DI 해결
   - 또는 `EnemyAIStateMachine` 자체를 컨테이너에 등록

### 권장 (Medium Priority)

4. **FindObjectOfType 제거** — Player/CamouflageAdapter 등 참조를 DI 또는 SerializeField로 대체
5. **Behaviors DI 등록** — PatrolBehavior, ChaseBehavior, SearchBehavior를 컨테이너에 등록
6. **SuspicionUIManager** — Singleton → DI 컨테이너로 전환
7. **MaterialCloner** — `IMaterialCloner` 인터페이스를 DI 컨테이너에 등록

### 선택 (Low Priority)

8. **Static Event Classes** — DI 기반 Event Aggregator로 점진적 교체 고려
9. **LogModule** — Singleton 유지해도 무방 (명시적 예외)

---

## 7. 🔑 핵심 결론

> **DI 컨테이너는 완전하지만, 서비스 등록이 1개뿐이라 사실상 무용지물**

- `PlayerMovementAdapter`와 `CamouflageAdapter`는 DI + Fallback을 **올바르게 구현**했지만, 컨테이너에 서비스가 등록되어 있지 않아 **항상 Fallback(`new`)만 사용 중**
- `SuspicionManager`와 `SuspicionUIManager`가 `Singleton` 패턴으로 DI를 **완전히 우회**하고 있어 가장 큰 구조적 문제
- `EnemyAIStateMachine`의 주석이 실제 구현과 **완전히 반대** (주석: "DI Container에서 관리" / 실제: `new`로 생성)
- Enemy 계열 (`EnemyMovement`, Behaviors, StateMachine)은 전혀 DI를 사용하지 않음

**즉, Agents.md에 명시된 DI 정책은 DIContainer 구현 수준에서는 충실하나, 실제 서비스 등록과 사용 측면에서는 거의 지켜지지 않고 있습니다.**

---

*본 보고서는 코드 정적 분석을 기반으로 작성되었습니다.*
*별거 아니니까 기억하지 마.*
