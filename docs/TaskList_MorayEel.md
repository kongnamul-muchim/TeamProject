# Moray Eel (곰치) 보스 구현 Task List

> **버전:** v1.0
> **작성일:** 2026-04-28
> **참조:** `Plans/Moray_Design_v1.md`
> **목표:** Zone 3 곰치 보스 화면 밖 돌진 기믹 구현

---

## Phase 0: 설계 확정 및 준비

- [x] `Plans/Moray_Design_v1.md` 작성 완료
- [ ] `RelentlessChaseGimmick.cs` 기존 코드 구조 분석 (의심도 하락/수색 확대 제거 대상 식별)
- [ ] 기존 `RelentlessChaseGimmick.asset` Inspector 파라미터 확인 및 백업
- [ ] 신규 인터페이스 멤버 구현 필요 여부 확인 (IGimmickPlayerAware 등)

---

## Phase 1: Patrol 의심도 시스템

**목표**: 구역 내 자동 상승 + 최초 1회 강제 + 종료 후 30 고정

### 1.1 구역 내 자동 의심도 상승
- [ ] `RelentlessChaseGimmick.OnPatrolUpdate()`에서 Player 거리 체크
- [ ] 구역(GroundBounds) 내 Player 있으면 `suspicionIncreaseRate`(15%/s)씩 의심도 상승
- [ ] 구역 밖 Player는 의심도 하락 (5%/s)
- [ ] BossSuspicionSystem 연동 (IncreaseSuspicion 호출)

### 1.2 최초 1회 강제 Chase 발동
- [ ] `BossEnemyController`에 Zone 진입 감지 로직 추가
- [ ] 최초 진입 시 의심도 100% 즉시 설정
- [ ] Chase 강제 발동 (1회차, 돌진 1회)

### 1.3 Chase 종료 후 의심도 30 고정
- [ ] `RelentlessChaseGimmick.OnChaseExit()` 또는 종료 콜백에서 의심도 30 설정
- [ ] `BossSuspicionSystem.SetSuspicion(value)` 메서드 필요 시 추가
- [ ] 재상승 사이클 확인 (30 → 15%/s → 100 → N+1회차 Chase)

---

## Phase 2: 화면 밖 돌진 시스템

**목표**: 8방향 진입점 → GroundBounds 내 경로 → 고속 돌진

### 2.1 8방향 진입점 계산
- [ ] `Camera.main.ViewportToWorldPoint()` 기반 화면 4개 모서리 좌표 계산
- [ ] 8방향 진입점 리스트 생성 (좌/우/상/하/좌상/우상/좌하/우하)
- [ ] `screenEdgeOffset` 만큼 화면 경계 밖으로 오프셋 적용

### 2.2 GroundBounds 내 경로 검증
- [ ] 시작점/종료점 모두 GroundBounds 내인지 확인
- [ ] 벗어나면 최대 3회 재선택 (fallback: 좌→우)
- [ ] 유효 경로 캐싱 (Prepare Phase에서 선계산)

### 2.3 곰치 화면 밖 이동
- [ ] `EnemyMovement`에 `TeleportTo(Vector3 position)` 메서드 추가
- [ ] Prepare Phase 시작 시 곰치를 화면 밖 시작점으로 순간 이동
- [ ] Charge Phase 종료 시 Patrol 복귀 위치로 순간 이동
- [ ] 화면 밖 동안 Patrol 이동 로직 정지 (HasMovementOverride)

### 2.4 돌진 충돌 판정
- [ ] `Physics.OverlapBox` 기반 돌진 경로 충돌 체크
- [ ] Box 크기: `(chargeWidth, 1, 경로길이)`, 회전: 돌진 방향
- [ ] Player Layer 충돌 시 체력 데미지 콜백
- [ ] 매 프레임 체크 (FixedUpdate 권장)

---

## Phase 3: 붉은 네모 (MorayChargeIndicator)

**목표**: 돌진 경로 시각화 + 순차 생성/소멸

### 3.1 MorayChargeIndicator.cs 생성
- [ ] `Assets/Core/Enemy/Boss/Gimmicks/MorayChargeIndicator.cs` 생성
- [ ] `LineRenderer` 리스트 관리 (`List<LineRenderer>`)
- [ ] `indicatorMaterial`, `indicatorWidth`, `warningColor` SerializeField

### 3.2 네모 생성
- [ ] `SpawnIndicator(Vector3 start, Vector3 end, int index)` 구현
- [ ] `baseSquareSpawnInterval` 간격으로 순차 생성 코루틴
- [ ] 회차별 간격 단축 적용 (`max(0.5 - (N-1) * 0.05, 0.2)`)
- [ ] 최대 5개 동시 표시 (maxChargesPerCycle)

### 3.3 네모 소멸
- [ ] `DespawnIndicator(int index)` 구현
- [ ] 해당 경로 돌진 완료 시 즉시 소멸
- [ ] `ClearAll()` 구현 (Chase 강제 종료 등 예외 상황)

### 3.4 시각 효과
- [ ] 생성 직후: 연한 붉은색 (alpha 0.4)
- [ ] 돌진 임박: 진한 붉은색 (alpha 0.8) + 펄스 효과
- [ ] 화면 밖 구간은 렌더링 안 함 (Clipping)

---

## Phase 4: Chase 에스컬레이션

**목표**: 돌진 횟수 증가 + 방향 다양화 + 간격 단축

### 4.1 Chase 진입 횟수 저장
- [ ] `RelentlessChaseGimmick` 내 `int chaseEntryCount` 필드
- [ ] Chase 진입 시 `chaseEntryCount++`
- [ ] 게임 재시작 시 리셋 (OnActivate에서 0 초기화)

### 4.2 돌진 횟수 결정
- [ ] `Mathf.Min(chaseEntryCount, maxChargesPerCycle)`로 돌진 횟수 계산
- [ ] 5회차 이후론 계속 5회 돌진 (최대치 유지)

### 4.3 돌진 방향 다양화
- [ ] 8방향 중 랜덤 선택 (System.Random or Unity.Random)
- [ ] 같은 방향 연속 금지 (이전 방향 저장 → 제외)
- [ ] N회차 = N개 방향만큼 랜덤 선택

### 4.4 간격 단축
- [ ] `GetSpawnInterval()` 구현: `max(baseInterval - (chaseEntryCount-1) * intervalStep, minInterval)`
- [ ] 1회차: 0.5s, 2회차: 0.45s, ..., 5회차: 0.3s

---

## Phase 5: BossEnemyController 연동

**목표**: 기존 콜백 시스템에 곰치 기믹 연결

### 5.1 ConnectGimmickCallbacks() 수정
- [ ] `GimmickType.RelentlessChase` 케이스 콜백 연결
- [ ] `OnSpeedOverride` → Chase 시 속도 제어
- [ ] `OnChasePauseRequest` → ChaseBehavior 정지

### 5.2 최초 강제 Chase 발동
- [ ] `OnPlayerEnterZone3` 이벤트 또는 Trigger 감지
- [ ] `BossSuspicionSystem.ForceSetSuspicion(100)` 호출
- [ ] Player가 Zone 3 GroundBounds 진입 시 1회만 실행

### 5.3 의심도 30 고정
- [ ] Chase 종료 콜백에서 `suspicionMeter.SetValue(30)` 호출
- [ ] `SuspicionManager`에 SetValue(float value) 메서드 추가 (없으면)

### 5.4 RelentlessChaseGimmick.asset 업데이트
- [ ] Inspector에 신규 파라미터 노출
- [ ] 기본값 설정 (chargeSpeed=18, suspicionIncreaseRate=15 등)

---

## Phase 6: 테스트 및 검증

### 6.1 단위 테스트
- [ ] 최초 1회 강제 Chase 발동 확인
- [ ] Patrol 중 의심도 15%/s 상승 확인 (30→100≈4.7s)
- [ ] Chase 종료 후 의심도 30 고정 확인
- [ ] 구역 이탈 시 의심도 하락 확인
- [ ] Chase 진입 횟수 증가 확인 (1→2→3)
- [ ] 돌진 횟수 = Chase 횟수 일치 확인

### 6.2 시각 테스트
- [ ] 붉은 네모 순차 생성 확인 (0.5s 간격)
- [ ] 회차별 간격 단축 확인 (0.5→0.45→0.4...)
- [ ] 돌진 완료 시 네모 순차 소멸 확인
- [ ] 8방향 다양하게 나타나는지 확인
- [ ] 화면 밖 구간 네모 미표시 확인

### 6.3 통합 테스트
- [ ] Patrol → Chase → Charge → Patrol 사이클 완전 확인
- [ ] Chase 중 Player 피격 시 체력 데미지 확인
- [ ] 모든 돌진 종료 후 Patrol 정상 복귀 확인
- [ ] 다회차 Chase (3회 이상) 안정성 확인
- [ ] GroundBounds 이탈 없이 모든 돌진 동작 확인

### 6.4 엣지 케이스
- [ ] Chase 중 Player Zone 이탈 → Chase 중단 or 강제 종료
- [ ] 돌진 방향 3회 재선택 실패 시 fallback (좌→우)
- [ ] maxChargesPerCycle=5 도달 후 계속 5회 유지 확인

---

## 완료 체크리스트

| 항목 | 상태 | 비고 |
|:----|:----:|:----:|
| Phase 0: 설계 확정 | ✅ | Moray_Design_v1.md 작성 |
| Phase 1: Patrol 의심도 | ❌ | |
| Phase 2: 화면 밖 돌진 | ❌ | |
| Phase 3: 붉은 네모 | ❌ | |
| Phase 4: 에스컬레이션 | ❌ | |
| Phase 5: BossEnemyController 연동 | ❌ | |
| Phase 6: 테스트 및 검증 | ❌ | |

---

## 진행 순서

```
Phase 0: 기존 코드 분석
    ↓
Phase 1: Patrol 의심도 시스템 (의존성 없음, 먼저 구현)
    ↓
Phase 3: 붉은 네모 (MorayChargeIndicator) (독립적, 먼저 구현 가능)
    ↓
Phase 2: 화면 밖 돌진 시스템 (네모 필요)
    ↓
Phase 4: Chase 에스컬레이션 (돌진 + 네모 필요)
    ↓
Phase 5: BossEnemyController 연동 (전체 필요)
    ↓
Phase 6: 테스트 및 검증
```

> **의존성 관계:**
> - Phase 1 → Phase 5 (의심도 연동)
> - Phase 3 → Phase 2 → Phase 4 (순차 의존)
> - Phase 2 + Phase 4 → Phase 5 (통합)
> - Phase 1~5 완료 → Phase 6

---

*이 문서는 Moray Eel (곰치) 보스 구현을 위한 작업 목록입니다.*
*각 Phase는 순서대로 진행하는 것을 권장하나, Phase 1과 Phase 3은 병렬 구현 가능.*
