# 코드 수정 위험도 평가

> 각 항목별로 수정 시 기존 동작이 손상될 가능성을 분석
> 평가 기준: HIGH / MEDIUM / LOW

---

## 🔴 Critical 항목

### 1. CamouflageAdapter.Update() — 이벤트 중복 호출 및 플래그 정리

**의존 시스템**: CamouflageEventBridge(VFX), CamouflageToSuspicionLink(의심도), SpriteDirector(스프라이트), PlayerMovementAdapter(이동 잠금), EnemyAIController(의태 감지)

**현재 동작**: 8개의 플래그/타이머가 서로 얽혀서 상태 전환 타이밍을 관리. 버그 가능성은 있지만 **현재는 우연히 잘 돌아가고 있음**.

**수정 방안**: 
- 플래그 통합 → `CamouflageAnimState` enum 도입
- `InvokeStateChanged` 호출 지점 단일화

**🔥 위험도: HIGH**
- 의태 시스템은 VFX, 의심도, 플레이어 이동, 적 AI 등 **4개 이상의 시스템과 연결**되어 있음
- 상태 전환 타이밍이 1프레임만 어긋나도 VFX가 안 나오거나, 의태가 풀리지 않는 현상 발생 가능
- 인스펙터에서 설정하는 `attachDelay`, `lockTime`, `blendTime` 등 타이밍 값과 **코드의 플래그 로직이 강하게 결합**되어 있어, 이를 건드리면 밸런스가 틀어질 수 있음

**권장**: **조심히 접근**. 단위 수정 후 반드시 플레이 모드 테스트 필요. 한 번에 다 고치려고 하지 말고 이벤트 중복 호출만 먼저 수정하는 게 안전.

---

### 2. EnemyAIController — Update() 중복 실행 위험

**의존 시스템**: NormalEnemyController, EliteEnemyController, BossEnemyController (전체 적 AI)

**현재 동작**: `MonoBehaviour.Update()`가 매 프레임 실행됨. `IEnemy.Update()`는 현재 **외부에서 호출하는 곳이 있는지 확인 필요**.

**수정 방안**: 
- A案: `IEnemy.Update()` 제거 (interface contract 변경)
- B案: `MonoBehaviour.Update()`가 `IEnemy.Update()`를 호출하도록 위임

**🔥 위험도: MEDIUM**
- `IEnemy` 인터페이스를 구현한 클래스가 3개 (`NormalEnemyController`, `EliteEnemyController`, `BossEnemyController`)
- `IEnemy.Update()`를 외부에서 호출하는 코드를 찾아야 함 → 안 찾고 지우면 컴파일 에러 또는 동작 안 함
- `IEnemy` 인터페이스 자체를 수정하면 영향 범위가 넓어짐

**권장**: 먼저 `grep`으로 `IEnemy.Update`나 `enemy.Update` 호출하는 곳 있는지 검색부터. 없으면 그냥 `IEnemy.Update()` 제거만으로 해결됨.

---

### 3. NormalEnemyController.OnDestroy() — 조건 버그

**현재 동작**: 비정상 조건으로 인해 Rigidbody 정리가 제대로 안 되고 있음. 하지만 **게임 플레이에 직접적인 영향을 주지 않음** (메모리 릭轻微).

**수정 방안**: `if (_rigidbody != null) Destroy(_rigidbody);`로 단순화

**🔥 위험도: LOW**
- 단순 버그 수정. 오히려 고치면 정상 동작하게 됨.
- 단, `_rigidbody`가 이미 Destroy된 상태에서 접근하면 `null` 비교가 false를 반환하므로 안전.

---

### 4. DIContainer — Scoped 인스턴스 Dispose 누락

**의존 시스템**: DI 컨테이너를 사용하는 모든 시스템 (현재는 사실상 GameStateMachine만)

**현재 동작**: `Scoped` 생명주기가 거의 사용되지 않고 있음. 모든 서비스가 `Transient` 또는 `Singleton`으로 등록되어 있음.

**수정 방안**: `RegisterInstance`에서 `ServiceLifetime.Scoped` 처리 추가

**🔥 위험도: LOW**
- 현재 `Scoped`를 사용하는 서비스가 없음
- Dispose 로직을 건드려도 Singleton/Transient에는 영향 없음

---

## 🟡 Significant 항목

### 5. PlayerMovementAdapter Config 등록 타입 충돌

**의존 시스템**: PlayerMovementAdapter, PlayerMovement, GameManager DI 등록

**현재 동작**: 
- `GameManager`에 `IPlayerMovement → PlayerMovement` (Transient) 등록됨
- `PlayerMovementAdapter.Awake()`에서 `RegisterInstance<IPlayerMovementConfig>(config)` 후 `Resolve<IPlayerMovement>()`
- 현재는 **PlayerMovementAdapter가 1개**라 문제 없음

**수정 방안**: Config 등록/해결 로직을 직접 생성자 전달로 변경

**🔥 위험도: LOW**
- 단일 인스턴스 환경에서는 동작 변화 없음
- 만약 멀티 플레이어/분할 화면이 생기면 문제가 되겠지만 현재는 아님

---

### 6. GameManager FindObjectOfType 반복 호출

**현재 동작**: `Awake()`와 `OnDestroy()`에서 각각 `FindObjectOfType<SuspicionToGameStateLink>()` 호출. OnDestroy 시점에서는 객체가 이미 없을 가능성 있음.

**수정 방안**: 참조를 캐싱하거나, `SubscribeToEvents()`에서 직접 `SuspicionManager.Instance.OnDetected` 구독

**🔥 위험도: MEDIUM**
- `SuspicionToGameStateLink`는 GameManager와 SuspicionManager 사이의 중간 계층
- 이 계층을 제거하고 `SuspicionManager.Instance.OnDetected`를 직접 구독하면 **의존 관계가 변경**됨
- `SuspicionToGameStateLink` 자체가 다른 곳에서도 참조되는지 확인 필요
- 단순 캐싱(멤버 변수 저장)은 위험도 LOW, 아키텍처 변경은 MEDIUM

---

### 7. SuspicionManager.ReportDetection() deltaTime 방식

**현재 동작**: `EnemyPerception.Update()`에서 매 프레임 `ReportDetection()` 호출 → 내부에서 `Time.deltaTime` 사용

**수정 방안**: 누적 감지량을 `SuspicionManager.Update()`에서 일괄 처리

**🔥 위험도: HIGH**
- **의심도 수치는 게임 밸런스와 직결**됨
- deltaTime 처리 방식을 바꾸면 의심도 상승 속도가 미묘하게 달라질 수 있음
- 기존 의심도 임계값(cautionThreshold=30, dangerThreshold=60, detectedThreshold=100)과 속도(increaseSpeed=40)가 이 방식에 맞춰 튜닝되어 있음
- **의심도 로직은 EnemyPerception, VisionBasedSuspicionManager, BossSuspicionSystem 등 여러 곳과 연결**

**권장**: 현재 동작 중이므로 **수정하지 않는** 것도 선택지. 필요하다면 별도 브랜치에서 테스트 후 적용.

---

### 8. BossSuspicionSystem _isPerfectCamouflage 미사용

**현재 동작**: `_isPerfectCamouflage` 필드가 저장만 되고 계산에 사용되지 않음. 즉, 보스전에서 완벽 의태가 일반 의태와 동일한 효과.

**수정 방안**: `ReportVisionDetection()`이나 `ReduceSuspicion()` 로직에 `_isPerfectCamouflage` 반영

**🔥 위험도: MEDIUM**
- **게임 밸런스 변경**임. 완벽 의태 시 보스 의심도 감소율이 달라짐.
- 의도적으로 완벽 의태 보정을 빼놓은 것일 수도 있음 (보스는 일반 적과 다른 규칙으로 설계되었을 가능성)
- 수정 전에 기획 의도 확인 필요

---

### 9. CamouflageAdapter InitOrder 의존성

**현재 동작**: `Awake()`에서 `RegisterInstance` 후 즉시 `Resolve`. 현재는 우연히 하나의 Adapter만 있어서 잘 동작.

**수정 방안**: DI 우회하고 직접 `new CamouflageDetector(detectorConfig)`로 생성

**🔥 위험도: LOW**
- 이미 fallback 로직이 같은 동작을 함. 로직상 변화 없음.
- 오히려 DI 레지스트리 오염을 방지함.

---

## 🔵 Minor 항목

### 10~12. 파라미터 정리 / 중복 호출 / 기본값

**🔥 위험도: LOW (전체)**

| 항목 | 위험도 | 사유 |
|------|--------|------|
| `CancelCamouflage force` 파라미터 정리 | LOW | 사용 안 하는 파라미터 제거, 호출부만 변경 |
| `LogModule Close+Dispose` 중복 | LOW | Dispose 로직만 정리, 동작 변화 없음 |
| `wallLayer` 기본값 | LOW | 인스펙터 기본값만 변경 |
| `PlayerInk` 싱글톤 보호 | LOW | 방어 로직만 추가 |
| `Time.timeScale` 복원 | LOW | 시작 시 1로 초기화만 추가 |
| `SandPit` Destroy 처리 | LOW | `enabled=false` 추가 |
| `SpriteDirector` AssetDatabase | LOW | Editor 전용 |

### 13. GameManager 이벤트 2번 호출

**🔥 위험도: HIGH**
- `OnPlayerDetected` → `GameStateMachine.TransitionTo(Detected)` → `OnGameStateChanged()` → `GameEvents.InvokePlayerDetected()` 체인
- 중복 호출을 제거하면 이 이벤트를 구독하는 시스템들이 영향을 받을 수 있음
- VFX, UI, 사운드 등이 "Detected" 이벤트를 1번만 받게 되어 동작이 달라질 수 있음

**권장**: 현재 이벤트 구독자들을 모두 확인하고, 의도된 2번 호출인지(일부러?) 판단 후 수정.

---

## 📊 최종 위험도 매트릭스

| # | 항목 | 위험도 | 바로 수정? |
|---|------|--------|-----------|
| 1 | CamouflageAdapter 플래그/이벤트 정리 | 🔴 HIGH | ❌ 신중하게 |
| 2 | EnemyAIController Update 중복 | 🟡 MEDIUM | ⚠️ 호출부 확인 후 |
| 3 | NormalEnemyController.OnDestroy 조건 | 🟢 LOW | ✅ 가능 |
| 4 | DIContainer Scoped Dispose | 🟢 LOW | ✅ 가능 |
| 5 | Config 등록 타입 충돌 | 🟢 LOW | ✅ 가능 |
| 6 | GameManager FindObjectOfType | 🟡 MEDIUM | ⚠️ 단순 캐싱만 |
| 7 | SuspicionManager deltaTime | 🔴 HIGH | ❌ 건드리지 말 것 |
| 8 | BossSuspicionSystem 완벽 의태 미적용 | 🟡 MEDIUM | ⚠️ 기획 확인 후 |
| 9 | InitOrder 의존성 | 🟢 LOW | ✅ 가능 |
| 10 | 파라미터/중복/기본값 등 | 🟢 LOW | ✅ 가능 |
| 11 | 이벤트 2번 호출 | 🔴 HIGH | ❌ 구독자 확인 후 |

---

## 🎯 결론 — 수정 가능한 것과 조심해야 할 것

**바로 수정해도 안전한 항목 (7개)**: `#3`, `#4`, `#5`, `#9`, `#10`, `#12` (minor), `#11` (일부)

**사전 검증 후 수정해야 할 항목 (2개)**: `#2` (grep 검색 후), `#6` (단순 캐싱만)

**밸런스/테스트 필요한 항목 (3개)**: `#1`, `#7`, `#8`, `#13`

**말하자면... 수정할 때 어디가 터질지 모르는 게 4개 정도 있어.**

1. **CamouflageAdapter.Update()** — 의태 시스템 전체가 걸려있음. VFX, 의심도, 이동 연동 다 연결됨
2. **SuspicionManager deltaTime** — 밸런스 수치 건드리는 거라 예민함
3. **BossSuspicionSystem 완벽 의태** — 기획 의도를 모르면 수정했다가 오히려 잘못된 동작
4. **이벤트 2번 호출** — 구독자들이 2번 오는 걸 가정하고 있을 수도 있음

...별로 신경 쓰는 것 같지? 그냥 혹시나 해서 말해본 거야. 진행할까?
