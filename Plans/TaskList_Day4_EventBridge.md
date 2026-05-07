# Day 4: 의태 시스템 이벤트 브릿지 Task List

> **버전:** v1.0
> **작성일:** 2026-04-17
> **담당자:** Member A (코어 프로그래머)
> **목표:** 의태/발각/사망 상태 전환 시 C# 이벤트 발생 브릿지 구축

---

## 1. 이벤트 시스템 구축 (Day 4-1)

### 1.1 이벤트 정의 파일 생성
- [ ] `Assets/Core/Events/CamouflageEvents.cs` 생성
  - `OnStateChanged`, `OnCamouflageStart`, `OnCamouflageComplete`, `OnCamouflageEnd` 정의
- [ ] `Assets/Core/Events/GameEvents.cs` 생성
  - `OnPlayerDetected`, `OnPlayerDeath`, `OnStageClear` 정의

### 1.2 CamouflageAdapter 수정
- [ ] `CamouflageAdapter.cs`에 이벤트 발생 로직 추가
  - None → Attached: `OnCamouflageStart.Invoke(target)`
  - Perfect 도달: `OnCamouflageComplete.Invoke(target)`
  - → None 복귀: `OnCamouflageEnd.Invoke(target)`
  - 상태 변경: `OnStateChanged.Invoke(newState)`

### 1.3 GameStateMachine 수정
- [ ] `GameStateMachine.cs`에 이벤트 발생 로직 추가
  - Detected 전환: `GameEvents.OnPlayerDetected.Invoke()`
  - Dead 전환: `GameEvents.OnPlayerDeath.Invoke()`

---

## 2. 이벤트 브릿지 구현 (Day 4-2)

### 2.1 CamouflageEventBridge 생성
- [ ] `Assets/Core/Perception/CamouflageEventBridge.cs` 생성
- [ ] `[SerializeField]`로 효과 시스템 참조 필드 정의
  - `InkParticleEffect` (Member C 작성)
  - `CamouflageShaderEffect` (Member C 작성)
- [ ] `OnEnable`/`OnDisable`에서 이벤트 구독/해제 구현
- [ ] 핸들러 메서드 구현 (`HandleCamouflageStart`, `HandleCamouflageComplete`, `HandleCamouflageEnd`)

### 2.2 GameManager 수정
- [ ] `GameManager.cs`에 `GameEvents` 구독 추가
  - `OnPlayerDetected` → 기존 `OnPlayerDetected` 핸들러와 통합
  - `OnPlayerDeath` → 사망 처리 핸들러 연결

---

## 3. 테스트 및 검증 (Day 4-3)

### 3.1 단위 테스트
- [ ] 의태 시작 시 `OnCamouflageStart` 발생 확인 (Debug.Log)
- [ ] 완벽 의태 시 `OnCamouflageComplete` 발생 확인
- [ ] 의태 해제 시 `OnCamouflageEnd` 발생 확인
- [ ] 발각 시 `OnPlayerDetected` 발생 확인

### 3.2 통합 테스트
- [ ] `CamouflageEventBridge`가 이벤트를 정상 수신하는지 확인
- [ ] 이벤트 구독 해제가 정상 작동하는지 확인 (메모리 누수 없음)
- [ ] 씬 전환 후 이벤트 재구독 정상 작동 확인

### 3.3 Member C 연동 준비
- [ ] `InkParticleEffect` 인터페이스 정의 (더미 구현)
- [ ] `CamouflageShaderEffect` 인터페이스 정의 (더미 구현)
- [ ] Member C에게 연동 가이드 제공

---

## 4. 완료 체크리스트

| 항목 | 상태 |
|------|------|
| CamouflageEvents.cs 생성 | ❌ |
| GameEvents.cs 생성 | ❌ |
| CamouflageAdapter 이벤트 발생 추가 | ❌ |
| GameStateMachine 이벤트 발생 추가 | ❌ |
| CamouflageEventBridge.cs 생성 | ❌ |
| GameManager 이벤트 구독 추가 | ❌ |
| 단위 테스트 통과 | ❌ |
| 통합 테스트 통과 | ❌ |
| Member C 연동 가이드 제공 | ❌ |

---

## 5. 진행 순서

```
1. CamouflageEvents.cs/GameEvents.cs 생성
    ↓
2. CamouflageAdapter 수정 (이벤트 발생)
    ↓
3. GameStateMachine 수정 (이벤트 발생)
    ↓
4. CamouflageEventBridge.cs 생성
    ↓
5. GameManager 수정 (이벤트 구독)
    ↓
6. 테스트 및 검증
    ↓
7. Member C 연동 가이드 제공
```

---

> **문서 작성자:** Member A
> **최종 업데이트:** 2026-04-17
