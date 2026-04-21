# Enemy 이동 AI Task List

> **버전:** v2.0
> **작성일:** 2026-04-21
> **담당자:** AI Agent
> **목표:** Enemy 이동 AI 시스템 구축 (순차적 진행: 몬스터 AI → 보스 기믹 → 정예 기믹)

---

## 📌 진행 원칙

- **Player 위치**: `Layer "Player"` 기준 탐색
- **보스**: 1챕터당 1개, 의심도 개별 관리
- **알림 방식**: DI Container + C# 이벤트 (원칙 준수)
- **구현 순서**: 코어 AI 먼저 → 보스 기믹 → 정예 기믹

---

## Phase 1: 코어 Enemy AI (현재 진행)

### 1. 인터페이스 정의
- [ ] `Assets/Core/Enemy/Interfaces/IEnemy.cs` 생성
  - Enemy 공통 속성 (Transform, IsActive, EnemyType 등)
- [ ] `Assets/Core/Enemy/Interfaces/IEnemyAIState.cs` 생성
  - `EnemyAIState` enum (Patrol, Chase, Search)
  - `IEnemyAIState` 인터페이스 (OnEnter, OnUpdate, OnExit)
- [ ] `Assets/Core/Enemy/Interfaces/IEnemyMovement.cs` 생성
  - Velocity, Direction, IsMoving 속성
  - Move(), Stop(), Update() 메서드

### 2. 보스 AI 상태 머신
- [ ] `Assets/Core/Enemy/AI/EnemyAIStateMachine.cs` 생성
  - 상태 전환 로직 (Patrol ↔ Chase ↔ Search)
  - 의심도 기반 자동 전환 (Danger 이상 → Chase)
  - 시야 기반 전환 (Player 발견 → Chase, 사라짐 → Search)

### 3. AI Behavior 구현
- [ ] `Assets/Core/Enemy/AI/Behaviors/PatrolBehavior.cs` 생성
  - 랜덤 순찰 구현 (랜덤 방향 + 거리)
  - 느린 이동 속도
  - 일정 시간마다 방향 변경
- [ ] `Assets/Core/Enemy/AI/Behaviors/ChaseBehavior.cs` 생성
  - Player 추적 (Layer "Player" 기반 탐색)
  - 예측 이동: Player 현재 위치 + 이동 방향 × 0.5초
  - 부드러운 가속 (Lerp 기반)
- [ ] `Assets/Core/Enemy/AI/Behaviors/SearchBehavior.cs` 생성
  - 마지막 Player 위치로 이동
  - 주변 랜덤 탐색 (원형 패턴)
  - 시간 초과 시 Patrol로 복귀
  - Player 재발견 시 Chase로 전환

### 4. Enemy 이동 시스템
- [ ] `Assets/Core/Enemy/Movement/EnemyMovement.cs` 생성
  - 가속도/마찰력 기반 부드러운 이동
  - 속도 제한 (상태별: Patrol < Search < Chase)
  - 방향 전환 시 자연스러운 회전

### 5. 보스 몬스터 컨트롤러
- [ ] `Assets/Core/Enemy/EnemyAIController.cs` 생성 (공통 베이스)
  - MonoBehaviour 기반 컨트롤러
  - ConeVisionSensor 연동
  - SuspicionMeter 연동
- [ ] `Assets/Core/Enemy/Boss/BossEnemyController.cs` 생성
  - 보스 몬스터 전용 컨트롤러
  - 의심도 상승/하락 처리
  - AI 상태 머신 + Behavior 실행
  - EnemyMovement 연동

### 6. 일반 몬스터 컨트롤러
- [ ] `Assets/Core/Enemy/Normal/NormalEnemyController.cs` 생성
  - 단순 이동 (AI 상태 머신 없음)
  - Player 접촉 시 보스에게 위치 알림 (이벤트 기반)
  - Layer "Player" 기반 감지

### 7. DI Container 연동
- [ ] `GameManager.cs`에 Enemy 관련 서비스 등록
  - IEnemyMovement → EnemyMovement
  - EnemyAIStateMachine (Boss용)
  - 각 Enemy 타입별 Behavior

---

## Phase 2: 보스 기믹 (Phase 1 완료 후)

### 8. 보스별 특수 기믹
- [ ] `Assets/Core/Enemy/Boss/Gimmicks/` 폴더 생성
- [ ] Ch.1 가자미: 매복 기믹 (모래에 숨었다 기습)
- [ ] Ch.2 곰치: 집요한 추격 기믹 (의태 감지 시 추적 강화)
- [ ] Ch.3 전기뱀장어: 감전 구역 생성 기믹
- [ ] Ch.4 아귀: 발광 미끼 배치 기믹
- [ ] Ch.5 백상아리: 초고속 돌진 + 엄폐물 파괴 기믹

---

## Phase 3: 정예 몬스터 기믹 (Phase 2 완료 후)

### 9. 정예 몬스터 컨트롤러
- [ ] `Assets/Core/Enemy/Elite/EliteEnemyController.cs` 생성
  - 기믹형 몬스터 베이스
  - 대기 → Player 접근 시 기믹 발동
- [ ] Ch.1 청새치: 직선 돌진 기믹
- [ ] Ch.2 바다거북: 해초 먹어치움 기믹
- [ ] Ch.3 복어: 몸 부풀려 길 막기 기믹

---

## 📅 진행 일정

| Phase | 작업 | 예상 소요 |
|-------|------|-----------|
| **Phase 1** | 코어 Enemy AI | 3-4시간 |
| **Phase 2** | 보스 기믹 | 2-3시간 |
| **Phase 3** | 정예 기믹 | 2-3시간 |

---

*이 문서는 Enemy 이동 AI 구현을 위한 작업 목록입니다.*
*Phase 1부터 순차적으로 진행합니다.*
