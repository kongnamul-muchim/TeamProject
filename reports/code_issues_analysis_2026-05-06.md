# 코드 문제점 분석 보고서

> 분석일: 2026-05-06
> 분석 범위: `Assets/Core/`, `Assets/Scripts/` 전체 C# 스크립트 (약 120개 파일)

---

## 🔴 Critical (즉시 수정 권장)

### 1. CamouflageAdapter.Update() — 중복 InvokeStateChanged 호출

**파일**: `Assets/Scripts/Player/CamouflageAdapter.cs` (lines 268-303)

```csharp
// Line 271: None → Attached에서 첫 번째 호출
CamouflageEvents.InvokeStateChanged(_stateMachine.CurrentState);

// Line 296-303: 상태 변경 시 중복 조건
if (prevState != _stateMachine.CurrentState && _stateMachine.CurrentState != CamouflageState.None)
{
    // Line 299: None→Attached는 건너뛰지만 로직이 복잡
    if (!(prevState == CamouflageState.None && _stateMachine.CurrentState != CamouflageState.None))
    {
        CamouflageEvents.InvokeStateChanged(_stateMachine.CurrentState);
    }
}
```

**문제**: 상태 전환 시 이벤트 호출 로직이 중복 호출될 가능성이 있고, 조건문이 너무 복잡해 유지보수 어려움.
**제안**: 단일 호출 지점으로 통일하고 switch 문으로 명확하게 분기.

---

### 2. CamouflageAdapter.Update() — 상태 관리 플래그 과다

**파일**: `Assets/Scripts/Player/CamouflageAdapter.cs`

다음 8개의 플래그/타이머가 상태 전환 타이밍을 관리:
- `_hasInvokedEndEvent`
- `_justTransitionedFromPerfect`, `_transitionTimer`
- `_isRestoringRate`, `_rateRestoreProgress`
- `_isRestoringOutline`
- `_ignoreMovementTimer`
- `_camouflageCooldown`

**문제**: 너무 많은 플래그가 산재해 있어 상태 전환 간 race condition 발생 가능.
**제안**: 단일 상태 enum(`CamouflageAnimState`)을 도입하여 플래그를 통합 관리.

---

### 3. EnemyAIController.Update() — base.Update()와 IEnemy.Update() 중복 실행 위험

**파일**: `Assets/Core/Enemy/EnemyAIController.cs` (lines 108-113 vs 394-400)

```csharp
// MonoBehaviour.Update() — line 108
protected virtual void Update()
{
    if (!_isActive) return;
    UpdateAI(Time.deltaTime);
    UpdateMovement(Time.deltaTime);
    UpdateViewDirection();
}

// IEnemy.Update() — line 394
public void Update(float deltaTime)
{
    if (!_isActive) return;
    UpdateAI(deltaTime);
    UpdateMovement(deltaTime);
    UpdateViewDirection();
}
```

**문제**: 두 메서드가 동일한 로직을 수행. 외부에서 `IEnemy.Update()`를 호출하면 로직이 2배로 실행됨.
**제안**: `MonoBehaviour.Update()`만 유지하고 `IEnemy.Update()`는 제거하거나, 한쪽이 다른 쪽을 호출하도록 변경.

---

### 4. NormalEnemyController.OnDestroy() — Rigidbody 정리 조건 버그

**파일**: `Assets/Core/Enemy/Normal/NormalEnemyController.cs` (lines 365-372)

```csharp
private void OnDestroy()
{
    if (_rigidbody != null && !GetComponent<Rigidbody>())
    {
        Destroy(_rigidbody);
    }
}
```

**문제**: `!GetComponent<Rigidbody>()`가 null이면 (=컴포넌트가 없으면) true → Destroy 시도. 하지만 `_rigidbody`가 이미 null이거나 컴포넌트가 이미 제거된 상태에서 호출될 수 있음. 게다가 `Destroy`는 씬 전환 시 이미 파괴된 객체에 호출될 수 있음.
**제안**: 조건 자체가 논리적으로 이상함. 단순히 `if (_rigidbody != null) Destroy(_rigidbody);`로 충분.

---

### 5. DIContainer.Scoped 인스턴스 Dispose — 루트 컨테이너에서 누락

**파일**: `Assets/Core/Managers/DIContainer.cs` (lines 296-304)

```csharp
// Scoped 인스턴스 정리 (루트 컨테이너엔 _scopedInstances가 없음)
foreach (var scoped in _scopedInstances.Values) { ... }
// Singleton 정리
foreach (var singleton in _singletons.Values) { ... }
```

**문제**: `_scopedInstances`는 `CreateScope()`로 생성된 자식 컨테이너에만 존재. 하지만 루트 컨테이너에서 `RegisterInstance()`에 `ServiceLifetime.Scoped`를 전달해도 `_scopedInstances`에 추가되지 않음 (`RegisterInstance` line 114-117은 `Singleton`일 때만 `_singletons`에 추가). Scoped 인스턴스가 Dispose 누락될 수 있음.
**제안**: `RegisterInstance`에서 Scoped도 `_scopedInstances`에 저장하거나, Scoped만의 별도 딕셔너리 관리.

---

## 🟡 Significant (중요, 수정 권장)

### 6. PlayerMovementAdapter에 Config 등록 타입 충돌

**파일**: 
- `Assets/Core/Managers/GameManager.cs` (line 79): `Register<IPlayerMovement, PlayerMovement>(Transient)`
- `Assets/Scripts/Player/PlayerMovementAdapter.cs` (line 67): `RegisterInstance<IPlayerMovementConfig>(config)`

**문제**: `PlayerMovement` 생성자에는 `IPlayerMovementConfig`가 필요. `RegisterInstance`로 Config를 등록한 후 `Resolve<IPlayerMovement>()`를 호출하면 정상 동작하지만, 만약 **여러 `PlayerMovementAdapter` 인스턴스**가 있다면 마지막 인스턴스의 Config로 모두 덮어써짐.
**제안**: Config 객체는 컨테이너에 직접 등록하지 말고 `PlayerMovement` 생성 시 직접 전달. 또는 Adapter마다 고유 Scope 사용.

---

### 7. GameManager.SubscribeToEvents() — FindObjectOfType 사용

**파일**: `Assets/Core/Managers/GameManager.cs` (lines 121-125, 171-175)

```csharp
// Awake()에서
var suspicionLink = FindObjectOfType<SuspicionToGameStateLink>();
if (suspicionLink != null)
    suspicionLink.OnPlayerDetected += OnPlayerDetected;

// OnDestroy()에서 (다시 Find!)
var suspicionLink = FindObjectOfType<SuspicionToGameStateLink>();
if (suspicionLink != null)
    suspicionLink.OnPlayerDetected -= OnPlayerDetected;
```

**문제**:
1. `FindObjectOfType`를 매번 호출 → 성능 저하
2. `OnDestroy()` 시점에는 이미 `SuspicionToGameStateLink`가 Destroy되었을 가능성 → null 안전성 문제
3. `SuspicionToGameStateLink` 자체가 불필요한 중간 계층 (GameManager가 직접 `SuspicionManager.Instance.OnDetected` 구독 가능)

**제안**: `SuspicionToGameStateLink`의 이벤트 참조를 캐싱하거나, `SuspicionManager.Instance.OnDetected`를 직접 구독.

---

### 8. SuspicionManager.ReportDetection() — deltaTime 전달 방식 이슈

**파일**: `Assets/Core/Perception/SuspicionManager.cs` (line 222)

```csharp
public void ReportDetection(float detectionIntensity = 1f)
{
    _lastDetectionTime = detectionGracePeriod;
    _meterService.AddSuspicion(detectionIntensity, Time.deltaTime);
}
```

**문제**: `SuspicionManager.Update()`가 아닌 `ReportDetection()` 호출 시점의 `Time.deltaTime`을 사용. `EnemyPerception.Update()`에서 매 프레임 호출되므로 큰 문제는 아니지만, `ReportDetection`이 여러 번 중첩 호출되면 (같은 프레임에 여러 적이 감지) deltaTime이 중복 적용될 수 있음.
**제안**: SuspicionManager.Update()에서 누적된 detection intensity를 일괄 처리하는 방식으로 변경.

---

### 9. BossSuspicionSystem.SetCamouflageState() — _isPerfectCamouflage 저장만 하고 사용 안 함

**파일**: `Assets/Core/Perception/BossSuspicionSystem.cs` (lines 278-282)

```csharp
public void SetCamouflageState(bool isCamouflaging, bool isPerfect = false)
{
    _isCamouflaging = isCamouflaging;
    _isPerfectCamouflage = isPerfect;  // 저장만 하고 사용 안 함
}
```

**문제**: `_isPerfectCamouflage`가 저장만 되고 실제 의심도 계산 로직에서 사용되지 않음 (`ReportVisionDetection()`에서는 `_isCamouflaging`만 체크). SuspicionMeterService와 달리 완벽 의태 추가 감소 효과가 Boss에서는 미적용.
**제안**: `ReduceSuspicion()` 로직 추가 시 `_isPerfectCamouflage`를 반영하거나, 불필요한 필드 제거.

---

### 10. CamouflageAdapter.Update() — InitOrder 의존성 (Awake에서 DI 등록 후 같은 Awake에서 Resolve)

**파일**: `Assets/Scripts/Player/CamouflageAdapter.cs` (lines 98-118)

```csharp
// Awake()에서
GameManager.Container.RegisterInstance<ICamouflageDetectorConfig>(detectorConfig);
_detector = GameManager.Container.Resolve<ICamouflageDetector>();
```

**문제**: `RegisterInstance`로 Config를 등록한 직후 `Resolve`를 호출하면, `DIContainer.CreateInstance()`는 Config 생성자 파라미터가 이미 등록되어 있어야만 정상 동작. 순서가 보장되긴 하지만, **다른 Adapter가 먼저 실행되어 Config가 덮어써질 위험**이 있음.
**제안**: Config를 별도 등록하지 말고 직접 생성자에 전달: `new CamouflageDetector(detectorConfig)` (DI 우회가 더 안전한 케이스).

---

## 🔵 Minor (가벼운 문제, 필요시 수정)

### 11. CamouflageStateMachine.CancelCamouflage() — force 파라미터 무시

**파일**: `Assets/Core/Perception/CamouflageStateMachine.cs` (line 98)

```csharp
public void CancelCamouflage(bool force = false)
{
    Reset();  // force 파라미터 무시하고 항상 동일 동작
}
```

**문제**: `force` 파라미터가 선언만 되어 있고 실제 로직에서 사용 안 함. 코드만 봐서는 어떤 차이가 있는지 알 수 없음.
**제안**: `force=true`일 때"와 "force=false"일 때의 차이를 로직에 반영하거나, 파라미터 제거.

---

### 12. LogModule.DisposeWriters() — Close() + Dispose() 중복 호출

**파일**: `Assets/Core/Logging/LogModule.cs` (lines 130-137)

```csharp
private void DisposeWriters()
{
    // ...
    writer.Close();   // 내부적으로 Dispose() 호출
    writer.Dispose(); // 중복
}
```

**문제**: `StreamWriter.Close()`는 내부적으로 `Dispose(true)`를 호출하므로 `Dispose()`가 중복됨. `Close()`만으로 충분.
**제안**: `Close()`만 호출하거나 `Dispose()`만 호출.

---

### 13. PlayerMovementAdapter.SetIgnoreWallCollision() — null 레이어마스크 위험

**파일**: `Assets/Scripts/Player/PlayerMovementAdapter.cs` (lines 208-221)

```csharp
[SerializeField] private LayerMask wallLayer;  // 기본값 = 0

private void SetIgnoreWallCollision(bool ignore)
{
    int playerLayer = gameObject.layer;
    for (int i = 0; i < 32; i++)
    {
        if ((wallLayer & (1 << i)) != 0)  // wallLayer가 0이면 모든 레이어 False
        {
            Physics.IgnoreLayerCollision(playerLayer, i, ignore);
        }
    }
}
```

**문제**: `wallLayer`가 인스펙터에서 설정되지 않으면 (기본값 0) 아무 레이어와도 충돌 무시/복원이 안 됨 → 의도한 동작 불발. 그리고 `IgnoreLayerCollision` 복원 시에도 같은 wallLayer 값을 사용하므로, 설정 안 하면 복원도 안 됨.
**제안**: 기본값을 `-1`(Everything)로 설정하거나, 복원 시 LayerMask 전용 API 사용.

---

### 14. GameManager — GameStateMachine 중복 상태 전환 이벤트

**파일**: 
- `GameManager.OnGameStateChanged()` (lines 131-142) → GameEvents.InvokePlayerDetected()
- `SuspicionToGameStateLink.OnSuspicionMax()` (lines 37-43) → OnPlayerDetected 이벤트
- `GameManager.OnPlayerDetected()` (lines 147-153) → _gameStateMachine.TransitionTo(GameState.Detected)

**흐름**: 
1. `SuspicionManager.OnDetected` → `SuspicionToGameStateLink.OnSuspicionMax()` → `GameManager.OnPlayerDetected()` → `GameStateMachine.TransitionTo(Detected)` → `GameManager.OnGameStateChanged()` → `GameEvents.InvokePlayerDetected()`
2. Detected → 다시 `GameEvents.InvokePlayerDetected()` 호출

**문제**: Player가 발각될 때 `GameEvents.InvokePlayerDetected()`가 2번 호출됨 (한 번은 `OnPlayerDetected`를 통해, 한 번은 상태 전환 후 `OnGameStateChanged`에서).
**제안**: 이벤트 호출을 단일 지점으로 통일. 중복 구독자가 예상치 못한 부작용을 일으킬 수 있음.

---

### 15. PlayerInk — 싱글톤 Instance 덮어쓰기 문제

**파일**: `Assets/Scripts/PlayerInk.cs` (lines 57-61)

```csharp
private void Awake()
{
    Instance = this;  // 이전 Instance를 그냥 덮어씀
    currentInk = Mathf.Clamp(currentInk, 0f, maxInk);
}
```

**문제**: 씬에 `PlayerInk`가 여러 개 있거나 중복 생성된 경우, 나중에 Awake()가 실행된 인스턴스가 기존 Instance를 덮어씀. 이전 Instance를 참조하던 객체들은 고아 참조가 됨.
**제안**: `if (Instance != null && Instance != this) { Destroy(gameObject); return; }` 패턴 적용.

---

### 16. PauseHandler — 비정상 종료 시 Time.timeScale 0 유지 위험

**파일**: `Assets/Scripts/UI/PauseHandler.cs`

**문제**: `Time.timeScale = 0f`으로 설정된 상태에서 게임이 비정상 종료되거나 Alt+F4로 종료되면 timeScale이 0으로 남아 다음 실행에 영향을 줌. Unity는 종료 시 timeScale을 자동 복원하지 않음.
**제안**: `TitleController`나 `GameManager.Awake()`에서 `Time.timeScale = 1f`로 복원하는 로직 추가.

---

### 17. SandPit — Destroy 후에도 같은 프레임의 Update 계속 실행

**파일**: `Assets/Core/Enemy/Boss/SandPit.cs` (lines 28-47)

```csharp
private void Update()
{
    if (_hasTriggered) return;  // Destroy 후에도 실행될 수 있음

    // ...OverlapSphere...
    if (_overlapResult[i].CompareTag("Player"))
    {
        _hasTriggered = true;
        Destroy(gameObject);  // 즉시 파괴
        return;
    }
}
```

**문제**: `Destroy(gameObject)` 호출 후에도 같은 프레임의 `Update()`는 계속 실행될 수 있음 (Destroy는 프레임 끝까지 지연됨). 다행히 `_hasTriggered` 플래그로 가드되어 있음.
**제안**: 현재 로직은 안전하나, 만약을 위해 `enabled = false`를 추가하거나 `DestroyImmediate` 고려 (단, 권장되지는 않음).

---

### 18. SpriteDirector.LoadSprite() — Editor 전용 AssetDatabase 의존성

**파일**: `Assets/Scripts/Player/SpriteDirector.cs` (lines 173-188)

```csharp
private Sprite LoadSprite(string path)
{
#if UNITY_EDITOR
    Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path + ".png");  // Editor 전용
    if (sprite == null)
    {
        sprite = Resources.Load<Sprite>(path);  // 폴백
    }
    return sprite;
#else
    return Resources.Load<Sprite>(path);
#endif
}
```

**문제**: Editor에서 `AssetDatabase`로 로드 시도 후 실패하면 `Resources.Load`로 폴백. 하지만 `AssetDatabase` 경로와 `Resources` 경로가 달라서 혼란 초래 (전자는 `.png` 포함, 후자는 미포함). 빌드에서는 `Resources.Load`만 사용.
**제안**: 경로 체계를 통일하거나, Editor에서도 Resources.Load만 사용 (AssetDatabase 우회 제거).

---

### 19. GameEvents / CamouflageEvents — 정적 이벤트 메모리 누수 위험

**파일**: 
- `Assets/Core/Events/GameEvents.cs`
- `Assets/Core/Events/CamouflageEvents.cs`

**문제**: 정적 이벤트에 구독한 후 해제하지 않으면 구독한 객체가 GC 대상이 되어도 메모리가 해제되지 않음. 대부분 `OnDisable()`에서 해제하고 있어 현재는 문제없지만, 일부 누락된 곳이 없는지 확인 필요.
**영향**: 현재 대부분 해제 패턴이 지켜지고 있으나, 지속적 모니터링 필요.

---

### 20. EnemyAIStateMachine 주석-실제 구현 불일치

**파일**: `Assets/Core/Enemy/AI/EnemyAIStateMachine.cs` (lines 8-9 주석)

```
/// 순수 C# 클래스로 DI Container에서 관리
```
**실제**: `BossEnemyController`에서 `new EnemyAIStateMachine(...)`로 직접 생성
**문제**: 문서와 코드가 불일치하여 유지보수 혼란 초래.

---

## 📊 종합 통계

| 구분 | 건수 | 심각도 |
|------|------|--------|
| 🔴 Critical | 5 | 즉시 수정 권장 |
| 🟡 Significant | 5 | 중요, 수정 권장 |
| 🔵 Minor | 10 | 가벼운 문제 |
| **Total** | **20** | |

---

## 🔑 핵심 요약

1. **가장 위험**: `EnemyAIController.Update()`가 `MonoBehaviour.Update()`와 `IEnemy.Update()`에서 **중복 실행**될 수 있는 구조
2. **버그 가능성**: `CamouflageAdapter`는 8개의 플래그/타이머가 상태 전환을 관리하는데, 그 중 일부는 상호 간섭 가능
3. **성능**: `FindObjectOfType`/`FindWithTag`가 여러 곳에서 반복 호출 (DI가 제대로 동작하면 해결됨)
4. **일관성**: DI 프레임워크는 구현되어 있으나 실제 서비스 등록은 1개뿐이라 실질적으로 `new` fallback에 항상 의존
5. **메모리/안정성**: 정적 이벤트 구독 해제, Singleton Instance 덮어쓰기 등 리소스 관리 이슈 존재

> ...별로 신경 쓴 건 아니야. 근데 코드가 위험해 보여서. 네가 수정할 거면 말해.

---

*분석 도구: 수동 코드 리뷰 | 분석 시스템: Assets/Core + Assets/Scripts 전체 약 120개 C# 파일*
