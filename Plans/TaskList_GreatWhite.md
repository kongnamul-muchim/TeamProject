# 백상아리 (Great White Shark) 구현 Task List

> **버전:** v1.0
> **작성일:** 2026-04-29
> **담당자:** AI Agent
> **목표:** Zone 4 최종 보스 백상아리 기믹 구현

---

## 📌 진행 원칙

- **기존 코드 최대 재활용**: `IEnemyGimmick` 인터페이스 + `ScriptableObject` 구조 유지
- **구현 순서**: Patrol → 의심도 → 의태 타겟팅 → Chase → 인디케이터 → 콜백 → 에셋
- **참조 문서**: `Plans/GreatWhite_Design_v1.md`, `Plans/GreatWhite_Gimmick.md`

---

## Phase 1: DashChargeGimmick.cs — 의심도 시스템

### 1.1 의심도 단계별 가속 상승
- [ ] `_normalizedSuspicion` 기반 현재 단계 판별 함수
- [ ] `GetCurrentSuspicionRate()` — 현재 의심도에 따른 상승률 반환
  - 0~30%: `suspicionRateStage1` (3%/s)
  - 30~60%: `suspicionRateStage2` (8%/s)
  - 60~90%: `suspicionRateStage3` (15%/s)
  - 90~100%: `suspicionRateStage4` (25%/s)
- [ ] `UpdateSuspicion(float deltaTime)` — Patrol 중 매 프레임 의심도 자동 상승
- [ ] 의태 중 의심도 하락 (-10%/s, `suspicionDecreaseRateCamouflage`)
- [ ] Chase 중 의심도 100% 고정 (하락 차단)

### 1.2 IGimmickPlayerAware 구현
- [ ] `SetPlayerTransform()` — Player 위치 수신
- [ ] `SetCamouflageState()` — 의태 상태 수신 (true = 의태 중)
- [ ] `SetSuspicionLevel()` — 외부 의심도 동기화

---

## Phase 2: DashChargeGimmick.cs — Patrol 시스템

### 2.1 각도 기반 링 순찰
- [ ] `_patrolAngle` — 현재 각도 (0~360°)
- [ ] `_patrolRadius` — Player와의 거리 (5~12m)
- [ ] `InitPatrol()` — Patrol 진입 시 각도/반경 초기화
- [ ] `GetPatrolTarget()` — Player 위치 기준 목표 좌표 계산
  - `x = Player.x + cos(각도) × 반경`
  - `z = Player.z + sin(각도) × 반경`
  - GroundBounds 내로 클램프
- [ ] 각도 매 프레임 진행 (20~60°씩 회전)
- [ ] 반경 매 프레임 변동 (-1~+1m, 4~12m 클램프)
- [ ] `HasMovementOverride` → Patrol 중 true

---

## Phase 3: DashChargeGimmick.cs — 의태 타겟팅 + 돌진

### 3.1 오브젝트 탐색
- [ ] `PickRandomTarget()` — Player 근처 무작위 오브젝트 선정
  - `Physics.OverlapSphere` + `CamouflageTarget` 레이어
  - 이미 파괴된 오브젝트 제외 (`activeSelf`)
  - 반경: `targetSearchRadius` (12m)
  - 오브젝트 없음 → null 반환 (의태 불가)
- [ ] `camouflageLockDelay` (0.5s) 후 타겟 선정

### 3.2 오브젝트 머리 위 공격 표식
- [ ] `ShowTargetIndicator(Transform target)` — 타겟 오브젝트 머리 위에 붉은 표식 표시
  - 구현 방식: LineRenderer 원형 or 빌보드 스프라이트
  - 지속 시간: `indicatorDuration` (0.8s)
- [ ] 인디케이터 활성화 시 `IGimmickViewDirection.ShowChargeIndicator` → true

### 3.3 Patrol 돌진 실행
- [ ] `StartPatrolCharge()` — 선정된 오브젝트 방향 돌진
  - 속도: `chargeSpeedPatrol` (15)
  - 도착 조건: 오브젝트 위치 도달 or maxDashDistance 초과
- [ ] `UpdatePatrolCharge()` — 돌진 중 프레임 업데이트
  - 오브젝트 위치로 이동
  - 도착 시 오브젝트 파괴 + Patrol 복귀
- [ ] 충돌 시 Player 데미지 체크 (Player가 붙어있는 오브젝트인지)

### 3.4 오브젝트 파괴
- [ ] `DestroyTargetObject()` — `SetActive(false)`
  - 기존 `DashChargeGimmick`의 CamouflageTarget 파괴 로직 재활용
  - `OnObstacleHit` 콜백 호출
- [ ] Player가 해당 오브젝트에 붙어있었다면 `OnPlayerHit` 콜백 호출

---

## Phase 4: DashChargeGimmick.cs — Chase 폭주 모드

### 4.1 Chase 진입
- [ ] 의심도 100% 도달 시 `OnChaseEnter()` → Chase 폭주 모드 플래그 설정
- [ ] `_chasePhase` enum: `Locking`, `Charging`

### 4.2 무한 연속 돌진
- [ ] `Locking` 단계: Player 방향 Lock-On 선 표시 (0.3~0.5s)
  - 청새치 `UpdateChargeIndicator()` 재활용 (얇은 붉은 선)
- [ ] `Charging` 단계: Player 방향 돌진 실행
  - 속도: `chargeSpeedChase` (22)
  - 돌진 중 경로상 `CamouflageTarget` 전부 파괴 (기존 로직 재활용)
- [ ] 돌진 종료 조건: `maxDashDistance` or GroundBounds 끝 → 즉시 재 `Locking`
  - 스턴 없음, 쿨다운 없음
  - `chargeCooldownChase` (1.5s) 만 적용
- [ ] Player 사망 시까지 Chase 해제 ❌
  - `IGimmickCombatCycle.IsInCombatCycle` → 항상 true
  - `IGimmickCombatCycle.IsCharging` → Charging 단계에서 true

### 4.3 벽 관통 처리
- [ ] Wall Layer 충돌 시 멈추지 않고 통과
  - 청새치 `CheckWallCollision()` 참조하되, 스턴 없이 계속 진행
  - Wall을 만나도 `_hasHitWall = false` 유지

---

## Phase 5: BossEnemyController.cs 콜백 연결

### 5.1 ConnectGimmickCallbacks() 확장
- [ ] `DashChargeGimmick` case에 다음 콜백 연결:
  - `OnSpeedOverride` → `_movement.Speed` 설정
  - `OnDashStarted` → 돌진 방향 시선 고정
  - `OnObstacleHit` → 오브젝트 파괴 로그
  - `OnMoveTo` → `_movement.MoveTo()` 호출
  - `OnMovementStop` → `_movement.Stop()` 호출
  - `OnPlayerHit` → `PlayerLives.TakeDamage()`
- [ ] 의심도 시스템 연동 (BossSuspicionSystem)
  - Patrol 중 자동 의심도 상승 (단계별 가속)
  - 의태 중 의심도 하락
  - Chase 중 의심도 고정
- [ ] 의태 감지 시 `IGimmickPlayerAware.SetCamouflageState()` 호출 확인

### 5.2 상태 전환 처리
- [ ] `OnAIStateChanged`에서 DashChargeGimmick 의심도 모드 설정
- [ ] Chase → Patrol 전환 차단 (`IsInCombatCycle` 활용)
- [ ] Player 사망 감지 → 의심도 리셋, Patrol 복귀

---

## Phase 6: DashChargeGimmick.asset 생성

### 6.1 ScriptableObject 에셋
- [ ] `Assets/ScriptableObjects/Gimmicks/DashChargeGimmick.asset` 업데이트
  - 신규 파라미터 인스펙터 반영
  - 의심도 단계별 상승률 4종
  - 돌진 속도 Patrol/Chase 분리
  - 의태/타겟팅 파라미터

---

## Phase 7: 테스트 및 검증

### 7.1 단위 동작 테스트
- [ ] Patrol: Player 주변 각도 기반 순찰 정상 동작
- [ ] 의심도: 단계별 가속 상승 + 의태 시 하락
- [ ] 의태 타겟팅: Player 근처 오브젝트 선정 + 머리 위 표식
- [ ] Patrol 돌진: 오브젝트 도착 후 파괴 + Patrol 복귀
- [ ] Chase 폭주: 무한 연속 돌진 + 사망 시 종료
- [ ] 오브젝트 파괴: CamouflageTarget `SetActive(false)` + 재생성 ❌

### 7.2 통합 테스트
- [ ] 의태 → 오브젝트 타겟팅 → 돌진 → 데미지 루프
- [ ] 의심도 100% → Chase 전환 → 사망까지 무한 돌진
- [ ] 모든 오브젝트 파괴 시 의태 불가 → Chase 직행
- [ ] 기존 3종 보스와 충돌 없음

---

## 📅 예상 일정

| Phase | 작업 | 예상 시간 |
|-------|------|-----------|
| **Phase 1** | 의심도 시스템 | 30분 |
| **Phase 2** | Patrol 시스템 | 30분 |
| **Phase 3** | 의태 타겟팅 + 돌진 | 1시간 |
| **Phase 4** | Chase 폭주 모드 | 1시간 |
| **Phase 5** | 콜백 연결 | 30분 |
| **Phase 6** | 에셋 생성 | 15분 |
| **Phase 7** | 테스트 | 30분 |
| **총합** | | **4시간 15분** |

---

*이 문서는 백상아리(Great White Shark) 최종 보스 구현을 위한 작업 목록입니다.*
*Phase 1부터 순차적으로 진행합니다.*
*참조: `Plans/GreatWhite_Design_v1.md`, `Plans/GreatWhite_Gimmick.md`*
