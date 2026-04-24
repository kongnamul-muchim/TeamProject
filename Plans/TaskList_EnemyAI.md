# Enemy 이동 AI Task List

> **버전:** v4.0
> **작성일:** 2026-04-24
> **담당자:** AI Agent
> **목표:** 챕터 2 몬스터 3종 구현 (곰치, 성게, 바다거북) + 조류 시스템

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
- [x] `Assets/Core/Enemy/Normal/NormalEnemyController.cs` 생성
  - 부채꼴 시야(ConeVisionSensor)로 Player 감지
  - Player 감지 시 보스에게 위치 알림
  - Layer "Player" 기반 감지

### 7. DI Container 연동
- [ ] `GameManager.cs`에 Enemy 관련 서비스 등록
  - IEnemyMovement → EnemyMovement
  - EnemyAIStateMachine (Boss용)
  - 각 Enemy 타입별 Behavior

---

## Phase 2: 보스 기믹 이동 제어권 (Phase 1 완료 후)

### 8. 보스별 특수 기믹 (이동 제어권 확장)
- [ ] `IEnemyGimmick.cs` 확장
  - `HasMovementOverride` 속성 추가
  - `GetPatrolTarget(currentPos, bounds)` 메서드 추가
  - `GetSearchTarget(currentPos, lastKnownPos, bounds)` 메서드 추가
- [ ] `PatrolBehavior.cs` 수정
  - 생성자에 `IEnemyGimmick` 주입 추가
  - `OnUpdate()`에서 기믹 이동 제어권 확인
  - 기믹이 제어하면 X-Z 평면 목표 사용 (Z축 제한)
- [ ] `SearchBehavior.cs` 수정
  - 생성자에 `IEnemyGimmick` 주입 추가
  - `OnUpdate()`에서 기믹 이동 제어권 확인
- [ ] `BossEnemyController.cs` 수정
  - Behavior 생성 시 `_activeGimmick` 전달
- [ ] Ch.1 가자미 (AmbushGimmick): 매복 위치 이동 (Z 고정)
- [ ] Ch.2 곰치 (RelentlessChaseGimmick): 집중 순찰 영역 내 X-Z 이동 (Z ±2m)
- [ ] Ch.3 전기뱀장어 (ElectricZoneGimmick): X-Z 순찰 (Z ±1m)
- [ ] Ch.4 아귀 (LureBaitGimmick): 미끼 순회 이동 (Z ±2m)
- [ ] Ch.5 백상아리 (DashChargeGimmick): 절벽 구간 X축 순찰 (Z 고정)

### 9. 정예 몬스터 기믹 (Phase 2 완료 후)

### 9. 정예 몬스터 컨트롤러
- [x] `Assets/Core/Enemy/Elite/EliteEnemyController.cs` 생성
  - 기믹형 몬스터 베이스
  - 센서 없이 행동 패턴(IEliteBehavior) 기반
  - 대기 → Player 접근 시 기믹 발동
- [ ] Ch.1 청새치: 직선 돌진 기믹
- [ ] Ch.2 바다거북: 해초 먹어치움 기믹
- [ ] Ch.3 복어: 몸 부풀려 길 막기 기믹

---

## Phase 3: 센서 아키텍처 리팩토링 (완료)

### 10. 일반 몬스터 센서 정리
- [x] `NormalEnemyController.cs`에 `IVisionSensor` 연동
- [x] `IVisionSensor` 인터페이스에 setter 메서드 추가 (`SetDistanceOnlyMode`, `SetViewRadius`, `SetViewAngle`)

### 11. 보스 의심도 시스템 리팩토링
- [x] `ISuspicionModule.cs` 인터페이스 정의
- [x] `BossSuspicionSystem.cs` 리팩토링 (모듈 주입 방식)
- [x] `AmbushSuspicionModule.cs` 생성 (의심도 계산 추출)
- [x] `BossEnemyController.cs` 수정 (센서 직접 제어 → 시스템 통합)

### 12. 정예 몬스터 프레임워크
- [x] `IEliteBehavior.cs` 인터페이스 정의
- [x] `EliteEnemyController.cs` 생성

---

## Phase 4: 챕터 2 몬스터 구현 (완료)

### 13. 조류 시스템 (Tide System)
- [x] `Assets/Core/Environment/TideDirection.cs` 생성
  - 조류 방향 enum (Left, Right)
- [x] `Assets/Core/Events/TideEvents.cs` 생성
  - 조류 발동/종료, Player 밀림 이벤트
- [x] `Assets/Core/Environment/TideManager.cs` 생성
  - 빈 게임오브젝트에 붙이는 독립 매니저
  - 주기적 조류 발동, 성게 풀 연동, Player 밀기
  - 의태 중 밀림 감소 (`camouflagePushMultiplier`)

### 14. 보스 - 곰치 (Moray Eel)
- [x] `Assets/Core/Enemy/Boss/Gimmicks/RelentlessChaseGimmick.cs` 생성
  - ScriptableObject + IEnemyGimmick + CreateAssetMenu
  - 의심도 하락률 감소 (`suspicionDecayMultiplier`)
  - 집중 순찰 영역 (`patrolCenter`, `patrolRadius`)
  - 수색 반경 확대 (`searchRadiusMultiplier`)
  - BossEnemyController 콜백 연결 코드 이미 존재 (라인 318-345)

### 15. 일반 - 성게 (Sea Urchin)
- [x] `Assets/Core/Events/EnemyEvents.cs` 생성
  - `OnPlayerSlowed` 정적 이벤트 (위치, 둔부율, 지속시간)
- [x] `Assets/Core/Enemy/Normal/SeaUrchinPool.cs` 생성
  - 성게 오브젝트 풀 관리 (기본 10개)
  - `SpawnFromTide(direction)` → 카메라 밖에서 소환
- [x] `Assets/Core/Enemy/Normal/SeaUrchinController.cs` 생성
  - 조류 힘 적용 (Rigidbody.AddForce)
  - Player 접촉 시 둔부 이벤트 발생
  - 성게 간 충돌 시 반대 방향 튕겨냄
  - 카메라 밖 비활성화 (`deactivateDelay` 초 후 풀 반환)

### 16. 정예 - 바다거북 (Sea Turtle)
- [x] `Assets/Core/Enemy/Elite/Behaviors/SeaTurtleBehavior.cs` 생성
  - IEliteBehavior 구현
  - 시작 시 Camouflageable 태그 오브젝트 캐싱
  - 활동 범위 내 랜덤 순회 (`patrolRadius`, 기즈모 표시)
  - 먹이 발견 시 접근 → `consumeTime` 동안 섭취 → `SetActive(false)`
  - 먹는 중 이동 중지

### 17. 의태 시스템 연동
- [x] `CamouflageAdapter.cs` 수정
  - `TideEvents.OnPlayerPushed` 이벤트 구독
  - 조류에 밀려 타겟과 거리가 `detectionRadius` 초과 시 의태 해제

---

## 📅 진행 일정

| Phase | 작업 | 예상 소요 | 상태 |
|-------|------|-----------|------|
| **Phase 1** | 코어 Enemy AI | 3-4시간 | 진행 중 |
| **Phase 2** | 보스 기믹 | 2-3시간 | 진행 중 |
| **Phase 3** | 센서 아키텍처 리팩토링 | 2-3시간 | 완료 |
| **Phase 4** | 챕터 2 몬스터 구현 | 3-4시간 | 완료 |

---

*이 문서는 Enemy 이동 AI 구현을 위한 작업 목록입니다.*
*Phase 1부터 순차적으로 진행합니다.*
