# DI 리팩토링 계획 — 단계별 수정안

> 참조: `Agents.md` (🏗️ DI 컨테이너 아키텍처 섹션)
> 사전 분석: `reports/DI_Compliance_Review.md`
> 마지막 업데이트: 2026-04-29

---

## 개요

현재 DI 컨테이너(DIContainer)는 구현이 완전하지만, 등록된 서비스가 `IGameStateMachine` 단 1개뿐이라 사실상 사용되지 않고 있음.  
본 계획은 4개 Phase로 나누어 **안전하게** DI를 적용하는 것을 목표로 함.

---

## Phase 1: 문서 현실화 (리스크: 없음 ✅)

### 대상 파일
- `Agents.md` (문서)
- `Assets/Core/Enemy/AI/EnemyAIStateMachine.cs` (주석)

### 수정 내용

#### 1-1. Agents.md DI 컨테이너 구조 섹션 업데이트

**수정 전:**
```
Assets/Core/
├── Interfaces/
│   └── IDIContainer.cs
├── Managers/
│   ├── DIContainer.cs
│   └── GameManager.cs
└── (기타 서비스 인터페이스/구현체)
```

**수정 후:** 실제 9개 하위 폴더 구조를 반영
```
Assets/Core/
├── Enemy/              ← 적 AI, Movement, Behaviors
├── Environment/        ← 조류(Tide) 시스템
├── Events/             ← 전역 정적 이벤트
├── Interfaces/         ← 모든 서비스 인터페이스 (14개)
├── Logging/            ← 로그 모듈
├── Managers/           ← DI 컨테이너 + GameManager + GameStateMachine
├── Perception/         ← 의태, 시야, 의심도 시스템
├── Player/             ← 플레이어 이동, 목숨
├── Utilities/          ← Singleton 베이스
└── VFX/                ← 시각 효과
```

**`Interfaces/` 목록 업데이트:**
현재 문서에는 `IDIContainer.cs`만 명시 → 실제 모든 인터페이스 파일 목록으로 교체

**서비스 등록 현황 업데이트:**
등록 필요 서비스 목록을 표로 추가 (Phase 2에서 등록할 항목 명시)

---

#### 1-2. EnemyAIStateMachine 주석 수정

**파일:** `Assets/Core/Enemy/AI/EnemyAIStateMachine.cs` (10-11행)

```diff
- /// 순수 C# 클래스로 DI Container에서 관리
+ /// 순수 C# 클래스 (현재는 BossEnemyController에서 직접 생성)
```

---

### Phase 1 작업 목록

| # | 작업 | 파일 | 예상 시간 |
|---|------|------|----------|
| 1.1 | Agents.md 구조 업데이트 | `Agents.md` | 10분 |
| 1.2 | EnemyAIStateMachine 주석 수정 | `EnemyAIStateMachine.cs` | 1분 |

---

## Phase 2: 서비스 등록 + Adapter 활성화 (리스크: 낮음 ⚠️)

### 대상 파일
- `Assets/Core/Managers/GameManager.cs`
- `Assets/Core/Player/PlayerMovement.cs`
- `Assets/Scripts/Player/PlayerMovementAdapter.cs`
- `Assets/Scripts/Player/CamouflageAdapter.cs`
- `Assets/Core/Perception/CamouflageDetector.cs`
- `Assets/Core/Perception/CamouflageStateMachine.cs`

### 수정 내용

#### 2-1. GameManager.RegisterCoreServices()에 서비스 등록 추가

```csharp
private void RegisterCoreServices()
{
    // 기존
    _gameStateMachine = new GameStateMachine(GameState.Playing);
    _rootContainer.RegisterInstance<IGameStateMachine>(_gameStateMachine, ServiceLifetime.Singleton);

    // 신규 등록 — Transient (매번 새 인스턴스)
    _rootContainer.Register<IPlayerMovement, PlayerMovement>(ServiceLifetime.Transient);
    _rootContainer.Register<ICamouflageDetector, CamouflageDetector>(ServiceLifetime.Transient);
    _rootContainer.Register<ICamouflageStateMachine, CamouflageStateMachine>(ServiceLifetime.Transient);
}
```

#### 2-2. PlayerMovement — 인스펙터 값 전달 문제 해결

**문제:**  
`PlayerMovement` 생성자의 기본값(`5.0f, 4.0f, 10.0f, 0.9f`)만 사용됨 → 인스펙터 설정값이 무시됨.

**해결 방법 (택1):**

**방법 A (권장) — RegisterInstance로 인스펙터 값 적용:**
```csharp
// GameManager.RegisterCoreServices()에서
var playerMovement = new PlayerMovement(horizontalSpeed, verticalSpeed, acceleration, friction);
_rootContainer.RegisterInstance<IPlayerMovement>(playerMovement, ServiceLifetime.Singleton);
```

**방법 B — Factory 패턴:**
```csharp
public interface IPlayerMovementFactory
{
    IPlayerMovement Create(float horizontalSpeed, float verticalSpeed, float acceleration, float friction);
}
public class PlayerMovementFactory : IPlayerMovementFactory
{
    public IPlayerMovement Create(float h, float v, float a, float f) => new PlayerMovement(h, v, a, f);
}
// 등록: _rootContainer.Register<IPlayerMovementFactory, PlayerMovementFactory>(Singleton);
```

**방법 C — PlayerMovementAdapter 유지 (수동 생성 + DI Fallback):**  
Adapter가 인스펙터 값을 직접 `new PlayerMovement(...)`에 전달. 등록만 해두고 `IsRegistered` 체크 로직은 유지.  
→ **제일 안전함. 기존 동작 100% 유지 + 이후 점진적 전환 가능**

**권장: Phase 2에서는 C로 가고, 추후 A 또는 B로 전환**

---

### Phase 2 작업 목록

| # | 작업 | 파일 | 비고 |
|---|------|------|------|
| 2.1 | 서비스 3개 등록 추가 | `GameManager.cs` | RegisterCoreServices() |
| 2.2 | PlayerMovementAdapter 확인 | `PlayerMovementAdapter.cs` | IsRegistered 체크 유지 |
| 2.3 | CamouflageAdapter 확인 | `CamouflageAdapter.cs` | IsRegistered 체크 유지 |

---

## Phase 3: SuspicionManager DI 전환 (리스크: 높음 🔴)

### 대상 파일 (7개)
- `Assets/Core/Perception/SuspicionManager.cs`
- `Assets/Core/Perception/SuspicionUIManager.cs`
- `Assets/Core/Perception/EnemyPerception.cs`
- `Assets/Core/Perception/SuspicionToGameStateLink.cs`
- `Assets/Core/Perception/CamouflageToSuspicionLink.cs`
- `Assets/Core/Managers/GameManager.cs` (등록 추가)
- `Assets/Core/Utilities/Singleton.cs` (불필요해지는 부분 정리)

### 수정 내용

#### 3-1. SuspicionManager Singleton 제거 + DI 등록

```csharp
// SuspicionManager.cs
public sealed class SuspicionManager : MonoBehaviour  // ← Singleton<T> 제거
{
    // Instance 프로퍼티 제거
    // Awake()의 base.Awake() 호출 제거
    // 대신 GameManager가 컨테이너에 RegisterInstance로 등록
}
```

```csharp
// GameManager.RegisterCoreServices()
_suspicionManager = FindObjectOfType<SuspicionManager>();
if (_suspicionManager == null)
{
    var go = new GameObject("SuspicionManager");
    _suspicionManager = go.AddComponent<SuspicionManager>();
}
_rootContainer.RegisterInstance<ISuspicionMeter>(_suspicionManager, ServiceLifetime.Singleton);
```

**문제점:** ISuspicionMeter 인터페이스가 SuspicionManager의 모든 public 메서드를 커버하지 않음.

```csharp
// ISuspicionMeter.cs — 현재 메서드
void AddSuspicion(float amount, float deltaTime);
void ReduceSuspicion(float amount, float deltaTime);
void Reset();
void SetSuspicion(float value);
void SetIncreaseSpeed(float speed);
void SetDecreaseSpeed(float speed);
void SetCamouflageState(bool isCamouflaging, bool isPerfect = false);
void OnDetectedTarget(float detectionIntensity = 1f);

// SuspicionManager에만 있는 메서드들 (ISuspicionMeter에 없음)
// - ReportDetection(float)
// - RegisterEnemy(EnemyPerception)
// - UnregisterEnemy(EnemyPerception)
// - BroadcastAlert(...)
// - ResetDetectedCooldown()
// - IsCamouflaging (property)
// - IsPerfectCamouflage (property)
```

**해결:** ISuspicionMeter 확장 or `SuspicionManager` 자체를 인터페이스로 등록

```csharp
// 방법 A: ISuspicionMeter 확장해서 필요한 메서드 추가
// 방법 B: SuspicionManager 자체 타입으로 등록
_rootContainer.RegisterInstance<SuspicionManager>(_suspicionManager, ServiceLifetime.Singleton);
```

→ **방법 B가 간단함. EnemyPerception 등은 `SuspicionManager` 타입으로 Resolve하면 됨.**

#### 3-2. SuspicionUIManager 동일 처리

```csharp
// Singleton<T> 제거
public sealed class SuspicionUIManager : MonoBehaviour
```

GameManager에서 등록:
```csharp
var uiManager = FindObjectOfType<SuspicionUIManager>();
if (uiManager == null)
{
    var go = new GameObject("SuspicionUIManager");
    uiManager = go.AddComponent<SuspicionUIManager>();
}
_rootContainer.RegisterInstance<SuspicionUIManager>(uiManager, ServiceLifetime.Singleton);
```

#### 3-3. 의존하는 5개 파일 모두 수정

각 파일에서 `SuspicionManager.Instance` → `GameManager.Container.Resolve<SuspicionManager>()`로 교체

**영향 파일:**
| 파일 | 변경 전 | 변경 후 |
|------|---------|---------|
| `EnemyPerception.Awake()` | `SuspicionManager.Instance` | `GameManager.Container.Resolve<SuspicionManager>()` |
| `SuspicionToGameStateLink` | `SuspicionManager.Instance.OnDetected` | Resolve 후 이벤트 구독 |
| `CamouflageToSuspicionLink` | `SuspicionManager.Instance.SetCamouflageState` | Resolve 후 호출 |
| `BossSuspicionSystem` | `SuspicionUIManager.Instance?.Register(this)` | Resolve 후 호출 |

---

### Phase 3 작업 목록

| # | 작업 | 파일 | 위험도 |
|---|------|------|--------|
| 3.1 | SuspicionManager Singleton 제거 | `SuspicionManager.cs` | 🔴 |
| 3.2 | SuspicionUIManager Singleton 제거 | `SuspicionUIManager.cs` | 🔴 |
| 3.3 | GameManager에 등록 코드 추가 | `GameManager.cs` | 🟡 |
| 3.4 | EnemyPerception 수정 | `EnemyPerception.cs` | 🟡 |
| 3.5 | SuspicionToGameStateLink 수정 | `SuspicionToGameStateLink.cs` | 🟡 |
| 3.6 | CamouflageToSuspicionLink 수정 | `CamouflageToSuspicionLink.cs` | 🟡 |
| 3.7 | BossSuspicionSystem 수정 | `BossSuspicionSystem.cs` | 🟡 |

---

## Phase 4: EnemyMovement + Behaviors DI 적용 (리스크: 높음 🔴)

### 대상 파일 (5~6개)
- `Assets/Core/Enemy/EnemyAIController.cs`
- `Assets/Core/Enemy/Normal/NormalEnemyController.cs`
- `Assets/Core/Enemy/Elite/EliteEnemyController.cs`
- `Assets/Core/Enemy/Boss/BossEnemyController.cs`
- `Assets/Core/Enemy/Movement/EnemyMovement.cs`
- `Assets/Core/Enemy/AI/EnemyAIStateMachine.cs`
- `Assets/Core/Enemy/AI/Behaviors/PatrolBehavior.cs`
- `Assets/Core/Enemy/AI/Behaviors/ChaseBehavior.cs`
- `Assets/Core/Enemy/AI/Behaviors/SearchBehavior.cs`

### 수정 내용

#### 4-1. EnemyMovement — Factory 패턴 도입

**문제:** `EnemyMovement` 생성자는 `IEnemy enemy`(MonoBehaviour)를 받음 → DI 컨테이너가 자동 생성 불가

**해결:**
```csharp
public interface IEnemyMovementFactory
{
    IEnemyMovement Create(IEnemy enemy, float speed, float acceleration, float friction, float maxSpeed, 
                          LayerMask groundLayer, float groundCheckDistance, float groundCheckRadius);
}

public class EnemyMovementFactory : IEnemyMovementFactory
{
    public IEnemyMovement Create(IEnemy enemy, float speed, float acceleration, float friction, 
                                  float maxSpeed, LayerMask groundLayer, float groundCheckDistance, 
                                  float groundCheckRadius)
    {
        return new EnemyMovement(enemy, speed, acceleration, friction, maxSpeed, 
                                 groundLayer, groundCheckDistance, groundCheckRadius);
    }
}
```

**등록:**
```csharp
_rootContainer.Register<IEnemyMovementFactory, EnemyMovementFactory>(ServiceLifetime.Singleton);
```

**사용 (EnemyAIController):**
```csharp
var factory = GameManager.Container.Resolve<IEnemyMovementFactory>();
_movement = factory.Create(this, moveSpeed, acceleration, friction, maxSpeed, 
                           groundLayer, groundCheckDistance, groundCheckRadius);
```

#### 4-2. EnemyAIStateMachine — DI 적용

**등록:**
```csharp
_rootContainer.Register<EnemyAIStateMachine>(ServiceLifetime.Transient);
```

**BossEnemyController 수정:**
```csharp
// Before:
_stateMachine = new EnemyAIStateMachine(_patrolBehavior, _chaseBehavior, _searchBehavior);

// After:
_stateMachine = GameManager.Container.Resolve<EnemyAIStateMachine>();
// BUT: _patrolBehavior, _chaseBehavior, _searchBehavior는 여전히 수동 생성이 필요
//     (MonoBehaviour 참조 때문)
```

**현실적 한계:** Behaviors는 `this`(BossEnemyController 자신)와 `_playerTransform`(동적 Transform)을 생성자로 받음 → **완전한 DI 전환이 거의 불가능**  

**권장:** EnemyAIStateMachine 자체는 DI로 생성하되, Behaviors는 현행 유지(Factory 패턴으로만 개선)

---

### Phase 4 작업 목록

| # | 작업 | 파일 | 비고 |
|---|------|------|------|
| 4.1 | IEnemyMovementFactory + 구현 | `EnemyMovement.cs` | 새 인터페이스 + Factory |
| 4.2 | 3개 컨트롤러 수정 | `EnemyAIController.cs`, `NormalEnemyController.cs`, `EliteEnemyController.cs` | Factory 사용 |
| 4.3 | EnemyAIStateMachine DI 등록 | `GameManager.cs`, `BossEnemyController.cs` | 제한적 적용 |
| 4.4 | Behaviors 현행 유지 결정 | `BossEnemyController.cs` | 주석으로 이유 명시 |

---

## 전체 로드맵

```
Phase 1: 문서 현실화
  └─ Agents.md + 주석 수정
       │
       ▼
Phase 2: 서비스 등록 + Adapter 활성화
  └─ GameManager + PlayerMovement + Camouflage 등록
       │
       ▼
Phase 3: SuspicionManager DI 전환 ★ 핵심
  └─ Singleton 제거 → 7개 파일 동시 수정
       │
       ▼
Phase 4: EnemyMovement/Behaviors
  └─ Factory 패턴 + 제한적 DI 적용
```

---

## Phase별 리스크 요약

| Phase | 리스크 | 영향 범위 | 검증 방법 |
|-------|--------|-----------|----------|
| 1 | ✅ 없음 | 문서만 | 눈으로 확인 |
| 2 | 🟡 낮음 | GameManager + Adapter | Unity Play Mode |
| 3 | 🔴 높음 | 7개 파일 동시 수정 | Unity Play + 모든 감지 시나리오 |
| 4 | 🔴 높음 | 9개 파일 수정 | Unity Play + 적 AI 전반 |

---

## 권장 실행 순서

```
1. Phase 1 실행 → Git Commit ("Docs: DI 구조 문서 현실화")
2. Phase 2 실행 → Git Commit ("Add: DI 서비스 등록 3종")
3. Phase 3 실행 → Git Commit ("Refactor: SuspicionManager DI 전환")
4. Phase 4 실행 → Git Commit ("Refactor: EnemyMovement Factory 패턴 도입")
```

각 Phase 완료 후 **Unity Play Mode에서 기본 동작 확인 필수.**  
특히 Phase 3는 감지/의태/의심도 시스템 전체가 영향을 받으므로 집중 테스트 필요.

---

*이 계획은 DI_Compliance_Review.md 분석 결과를 기반으로 작성됨*
*...별로 신경 써서 만든 건 아니야. 필요하면 말해.*
