# 보스 기믹 구현 Task List v2

> **버전:** v2.0
> **작성일:** 2026-04-24
> **담당자:** AI Agent
> **목표:** Zone 기반 4보스 체계 구현 (청새치 승격 + 기존 보스 개선)

---

## 📌 진행 원칙

- **오브젝트 요소 배제**: 산호, 바위, 해초 등 환경 오브젝트는 추후 구현
- **구현 순서**: 청새치(Boss 전환) → 기존 보스 개선 → 에셋 생성 → Scene 배치
- **기존 코드 최대 활용**: `IEnemyGimmick` 인터페이스 + `ScriptableObject` 구조 유지

---

## Phase 1: 청새치 Boss 전환 (Swordfish Elite → Boss)

### 1.1 GimmickType enum 확장
- [ ] `Assets/Core/Enemy/Interfaces/IEnemyGimmick.cs` — `GimmickType.Swordfish` 추가

### 1.2 SwordfishGimmick ScriptableObject 생성
- [ ] `Assets/Core/Enemy/Boss/Gimmicks/SwordfishGimmick.cs` 생성
  - `ScriptableObject` + `IEnemyGimmick` 구현
  - 기존 `SwordfishBehavior` 핵심 로직 이식:
    - Aim→Charge 2단계 돌진
    - 예측 이동 (predictionFactor)
    - 스턴 처리
  - `[CreateAssetMenu(menuName = "Enemy Gimmicks/Swordfish Gimmick")]`

### 1.3 BossEnemyController 콜백 연결
- [ ] `BossEnemyController.ConnectGimmickCallbacks()`에 `SwordfishGimmick` case 추가
  - `OnSpeedOverride` → 돌진 속도 제어
  - `OnDashStarted` / `OnDashCompleted`
  - `OnStun` → 스턴 상태 처리

### 1.4 기존 SwordfishBehavior 정리
- [ ] `SwordfishBehavior.cs` — Elite용 MonoBehaviour → Boss용 코드로 이식 완료 후 레거시 정리
- [ ] `IEliteBehavior` 인터페이스 — Swordfish 참조 제거

---

## Phase 2: 기존 보스 기믹 개선

### 2.1 가자미 (AmbushGimmick)
- [ ] `Gimmick_Ambush_Flounder.asset` 생성 (ScriptableObject 에셋)
- [ ] 보스 인스펙터 설정값 확정 (Patrol 1.5 / Chase 5 / Search 2 / ViewRadius 4 / ViewAngle 45°)

### 2.2 곰치 (RelentlessChaseGimmick)
- [ ] `Gimmick_Relentless_Moray.asset` — 기존 에셋 설정 검증
- [ ] 의심도 하락률 콜백 연동 확인 (`OnSuspicionDecayRateOverride`)
- [ ] 보스 인스펙터 설정값 확정 (Patrol 1.5 / Chase 6 / Search 3 / ViewRadius 6 / ViewAngle 90°)

### 2.3 백상아리 (DashChargeGimmick)
- [ ] `Gimmick_DashCharge_Shark.asset` 생성
- [ ] 돌진 + 엄폐물 파괴 로직 검증
- [ ] 보스 인스펙터 설정값 확정 (Patrol 3 / Chase 5 / Search 2 / ViewRadius 7 / ViewAngle 120°)

### 2.4 ElectricZoneGimmick 정리
- [ ] `BossEnemyController.ConnectGimmickCallbacks()`에서 `ElectricZoneGimmick` case 제거
- [ ] `ElectricZoneGimmick.cs` — 필요시 보관, 연결만 해제

---

## Phase 3: 기믹 에셋 생성

### 3.1 ScriptableObject 에셋
- [ ] `Assets/ScriptableObjects/Gimmicks/Gimmick_Ambush_Flounder.asset`
- [ ] `Assets/ScriptableObjects/Gimmicks/Gimmick_DashCharge_Shark.asset`
- [ ] `Assets/ScriptableObjects/Gimmicks/Gimmick_Swordfish.asset`
- [ ] `Assets/ScriptableObjects/Gimmicks/Gimmick_Relentless_Moray.asset` (기존, 검증)

---

## Phase 4: Scene 배치 (선택)

### 4.1 각 Zone별 보스 배치
- [ ] Zone 1: 청새치 + ConeVisionSensor + BossSuspicionSystem
- [ ] Zone 2: 가자미 + AmbushGimmick 연결
- [ ] Zone 3: 곰치 + RelentlessChaseGimmick 연결
- [ ] Zone 4: 백상아리 + DashChargeGimmick 연결

---

## 📅 예상 일정

| Phase | 작업 | 예상 시간 |
|-------|------|-----------|
| **Phase 1** | 청새치 Boss 전환 | 2-3시간 |
| **Phase 2** | 기존 보스 개선 | 1-2시간 |
| **Phase 3** | 기믹 에셋 생성 | 30분 |
| **Phase 4** | Scene 배치 | 1시간 |

---

*이 문서는 Zone 기반 4보스 체계 구현을 위한 작업 목록입니다.*
*Phase 1부터 순차적으로 진행합니다.*
