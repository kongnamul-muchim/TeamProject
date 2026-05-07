# SFX 사용 현황 및 출력 타이밍

> 갱신: 2026-05-07
> 기준: SfxId 11종

---

## ✅ 사용 중 (7종)

| SfxId | 출력 파일 | 출력 상황 | 상태 |
|-------|----------|----------|------|
| `ButtonClick` | `TitleController.cs:169,188,260,273` | 타이틀 - 새게임/이어하기/설정열기/설정닫기 | ✅ |
| `ButtonClick` | `SettingsPopup.cs:62` | 일시정지 - 타이틀로가기 버튼 | ✅ |
| `ButtonClick` | `PauseHandler.cs:60,70` | 일시정지 On/Off | ✅ |
| `ButtonClick` | `GameOverUI.cs:205,225,238` | 게임오버 - 이어하기/재시작/타이틀 | ✅ |
| `ButtonClick` | `DeathScreenUI.cs:176,186` | 사망화면 - 재시작/타이틀 | ✅ |
| `InkShoot` | `PlayerInk.cs:153` | 플레이어 대시/먹물 발사 | ✅ |
| `CamouflageAttach` | `CamouflageEventBridge.cs:213` | 의태 부착 시작 | ✅ |
| `CamouflagePerfect` | `CamouflageEventBridge.cs:221` | 완벽 의태 도달 | ✅ |
| `CamouflageDetach` | `CamouflageEventBridge.cs:319` | 의태 해제 | ✅ |
| `StageClear` | `ZoneChanger.cs:491` | 구역 전환 완료 | ✅ |
| `GameOver` | `GameManager.cs:245` | 플레이어 사망 (GameState.Dead) | ✅ |

---

## ❌ 미사용 (4종) — 사운드 주입 필요

| SfxId | 들어가야 할 파일 | 출력되어야 할 상황 |
|-------|----------------|------------------|
| `Charge` | `PlayerInk.cs` 또는 `ChichiChargeController.cs` | 잉크 충전 시작 |
| `PredatorDetected` | `SuspicionMeterUI.cs` | 의심도 100% 발각 |
| `PredatorUnderwaterPass` | `EnemyAIController` 계열 | 포식자가 스치고 지나감 |
| `PredatorChase` | `EnemyAIController` 계열 | 포식자 추격 시작 |

---

## ⬜ BGM (전체 미구현)

| BgmId | 들어가야 할 파일 | 출력되어야 할 상황 |
|-------|----------------|------------------|
| `GrasslandCoast` | `StageManager` (미구현) | 스테이지 1 |
| `CoralReef` | `StageManager` (미구현) | 스테이지 2 |
| `SeaweedForest` | `StageManager` (미구현) | 스테이지 3 |
| `DeepSeaCliff` | `StageManager` (미구현) | 스테이지 4 |
| `DeepSeaRuins` | `StageManager` (미구현) | 스테이지 5 |

---

## 참고

- SFX 7종은 정상 출력 중
- BGM 및 Ambient는 `StageManager` 구현 이후 작업 필요
- Charge/Predator 계열은 담당 컴포넌트에 `ISfxService` 주입만 하면 즉시 사용 가능
