# Hide and Ink - Member A 2주차 Task List

> **버전:** v1.0
> **작성일:** 2026-04-16
> **담당자:** Member A (코어 프로그래머)
> **목표:** 시야 감지 + 의심도 시스템 + 게임 상태 관리

---

## 2주차 작업 개요

```
1. 시야 감지 시스템 (Day 1-2)
2. 의심도 시스템 (Day 2-3)
3. 게임 상태 관리 (Day 4)
4. 시스템 통합 (Day 5)
```

---

## 1. 시야 감지 시스템 (Day 1) ✅ 완료

### 1.1 부채꼴 시야 구현

#### 1.1.1 IVisionSensor 인터페이스
- [x] `IVisionSensor` 인터페이스 작성
- [x] `VisionPatternType` enum (Patrol/Observe/Guard)

#### 1.1.2 ConeVisionSensor 클래스
- [x] 원점, 반지름, 각도(부채꼴 범위) 파라미터
- [x] 대상이 시야 내 있는지 확인 (각도 + 거리)
- [x] Raycast로 가려짐 감지

### 1.2 시야 패턴
- [x] 순찰형 (Patrol): 빠르게 이동, 좁은 시야 (45°, 4m)
- [x] 관찰형 (Observe): 천천히 이동, 넓은 시야 (90°, 6m)
- [x] 경계형 (Guard): 한 구역 내, 빠르고 넓은 시야 (120°, 5m)

---

## 2. 의심도 시스템 (Day 2) ✅ 완료

### 2.1 ISuspicionMeter 인터페이스
- [x] `ISuspicionMeter` 인터페이스 작성
- [x] `SuspicionLevel` enum (Safe/Caution/Danger/Critical/Detected)

### 2.2 의심도 상승/하락 규칙

| 상태 | 의심도 변화 |
|------|------------|
| 시야 내 적 감지 | 상승 속도 + |
| 의태 상태 | 상승 속도 감소 50% |
| 완벽 의태 | -6%/초 |
| 시야에서 완전히 사라짐 | -10%/초 |

### 2.3 연동 시스템
- [x] `VisionBasedSuspicionManager`: 시야 감지 → 의심도 상승
- [x] `CamouflageToSuspicionLink`: 의태 상태 → 의심도 조절
- [x] `SuspicionToGameStateLink`: 의심도 100% → Detected 상태

---

## 3. 게임 상태 관리 (Day 3) ✅ 완료

### 3.1 IGameStateMachine 인터페이스
- [x] `IGameStateMachine` 인터페이스 작성
- [x] `GameState` enum (Playing/Paused/Detected/Escaped/Dead)

### 3.2 상태 전환 규칙
```
Playing ←→ Paused
    ↓
Detected ←→ Escaped / Dead
```

---

## 4. 시스템 통합 (Day 4) ⚠️ 진행 중

### 4.1 연동 구조
```
VisionSensor (적) ──감지──→ SuspicionMeter (상승)
                                    ↑
                            CamouflageAdapter (하락)
                                    ↓
                         SuspicionMeter 100%
                                    ↓
                         GameStateMachine.Detected
```

### 4.2 DI 서비스 등록
- [x] `GameManager.RegisterCoreServices()` 완성
- [x] GameStateMachine Singleton 등록

### 4.3 UI 시스템
- [x] `SuspicionMeterUI`: 의심도 게이지 화면 표시

---

## 5. 완료 체크리스트

| 항목 | 상태 |
|------|------|
| 부채꼴 시야 감지 (ConeVisionSensor) | ✅ 완료 |
| 시야 패턴 3종 (Patrol/Observe/Guard) | ✅ 완료 |
| 의심도 시스템 (SuspicionMeter) | ✅ 완료 |
| 의심도上升/하락 규칙 | ✅ 완료 |
| Vision → Suspicion 연동 | ✅ 완료 |
| Camouflage → Suspicion 연동 | ✅ 완료 |
| 게임 상태 관리 (GameStateMachine) | ✅ 완료 |
| DI 서비스 등록 완료 | ✅ 완료 |
| 의심도 UI (SuspicionMeterUI) | ✅ 완료 |
| 통합 빌드 성공 | ❌ 미완료 |

---

## 6. 순차 진행 TODO

### Day 1 ✅
- [x] `IVisionSensor` 인터페이스 작성
- [x] `ConeVisionSensor` 클래스 작성
- [x] 시야 패턴 enum 정의

### Day 2 ✅
- [x] `ISuspicionMeter` 인터페이스 작성
- [x] `SuspicionMeter` 클래스 작성
- [x] Vision → Suspicion 연동 코드
- [x] Camouflage → Suspicion 연동 코드

### Day 3 ✅
- [x] 의심도 시스템 테스트 (버그 수정 포함)
- [x] 100% 도달 시 GameState 통지 코드
- [x] `IGameStateMachine` 인터페이스 작성
- [x] `GameStateMachine` 클래스 작성

### Day 4 ✅
- [x] 전체 시스템 통합
- [x] DI 서비스 등록 완료
- [x] 의심도 UI 추가

### Day 5 ⏳
- [ ] 통합 빌드 테스트
- [ ] 버그 수정
- [ ] TaskList 완료 보고서 작성

---

> **문서 작성자:** Member A
> **최종 업데이트:** 2026-04-16
> **비고:** 2주차 핵심 기능 구현 완료. 통합 테스트 및 빌드 남음.
