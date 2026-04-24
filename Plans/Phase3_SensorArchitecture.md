# Phase 3: 센서 아키텍처 리팩토링

> **버전:** v1.1
> **작성일:** 2026-04-24
> **문서 상태:** 계획 중
> **변경:** 정예 몬스터 항목 업데이트 — 청새치+바다거북
> **참조:** `Plans/Phase2_BossGimmicks_Design.md`, `Plans/TaskList_EnemyAI.md`

---

## 📌 설계 원칙

- **일반 몬스터**: `ConeVisionSensor` 유지 (부채꼴 + Boss 알림)
- **보스 몬스터**: `BossSuspicionSystem` 허브 + `ISuspicionModule` 기믹별 분리
- **정예 몬스터**: 센서 없음, `IEliteBehavior` 행동 패턴 기반
- **호환성**: 기존 Inspector 설정값 유지, 기존 에셋 깨짐 방지

---

## Phase 3.1: 일반 몬스터 센서 정리

### 목표
일반 몬스터가 부채꼴 시야로 Player 감지 → Boss에게 위치 알림

### 작업 항목
- [ ] `NormalEnemyController.cs` 생성
  - 단순 이동 (AI 상태 머신 없음)
  - `ConeVisionSensor` 연동
  - Player 감지 시 `BossEnemyController.AlertPlayerPosition()` 호출
- [ ] `ConeVisionSensor` 인터페이스 준수 확인
  - `IVisionSensor` 메서드 완전 구현
  - `SetDistanceOnlyMode`, `SetViewRadius`, `SetViewAngle` 인터페이스 추가 검토
- [ ] DI Container 연동
  - `GameManager.cs`에 NormalEnemy 관련 서비스 등록

---

## Phase 3.2: 보스 의심도 시스템 리팩토링

### 목표
보스 기믹별 감지 방식을 `ISuspicionModule`로 분리, `BossSuspicionSystem`이 허브 역할

### 작업 항목
- [ ] `ISuspicionModule.cs` 인터페이스 정의
  - `Update(deltaTime, bossPos, playerPos)`
  - `GetCurrentSuspicion()`
  - `OnSuspicionChanged` 이벤트
- [ ] `BossSuspicionSystem.cs` 리팩토링
  - `ISuspicionModule` 주입 방식 추가
  - 기존 의심도 로직 모듈 호환 처리
- [ ] `AmbushSuspicionModule.cs` 생성
  - `AmbushGimmick`에서 의심도 계산 로직 추출
  - 거리 기반 2단계 의심도 상승
- [ ] `BossEnemyController.cs` 수정
  - 기믹 활성화 시 해당 모듈 자동 주입
  - `ConeVisionSensor` 직접 제어 → `BossSuspicionSystem` 통합

---

## Phase 3.3: 정예 몬스터 프레임워크

### 목표
정예 몬스터는 센서 없이 행동 패턴 기반 기믹 구현

### 작업 항목
- [x] `IEliteBehavior.cs` 인터페이스 정의
  - `OnPlayerApproached(distance)`
  - `OnUpdate(deltaTime)`
- [x] `EliteEnemyController.cs` 생성
  - 기믹형 몬스터 베이스
  - 대기 → Player 접근 시 기믹 발동
- [x] 청새치: `SwordfishBehavior.cs` — 2단계 돌진(조준→돌진) + 스태미나 3종 패턴 개선 완료
- [x] 바다거북: `SeaTurtleBehavior.cs` — 해초 감지 → 섭취 기믹 (현행 유지)

---

## Phase 3.4: 문서화 및 테스트

### 작업 항목
- [ ] `TaskList_EnemyAI.md` 업데이트
- [ ] 센서 아키텍처 다이어그램 추가
- [ ] 각 모듈 단위 테스트 (에디터 플레이)
- [ ] 호환성 검증 (기존 에셋 깨짐 확인)

---

## 📅 진행 일정

| Phase | 작업 | 예상 소요 |
|-------|------|-----------|
| **3.1** | 일반 몬스터 센서 정리 | 1-2시간 |
| **3.2** | 보스 의심도 시스템 리팩토링 | 2-3시간 |
| **3.3** | 정예 몬스터 프레임워크 | 2-3시간 |
| **3.4** | 문서화 및 테스트 | 1시간 |

---

*이 문서는 Phase 3 센서 아키텍처 리팩토링 작업 목록입니다.*
*Phase 3.1부터 순차적으로 진행합니다.*
