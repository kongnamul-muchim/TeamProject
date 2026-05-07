# SFX/BGM 사용 현황 및 출력 타이밍

> 갱신: 2026-05-07

---

## SfxId 8종

| SfxId | 출력 파일 | 출력 상황 | 상태 |
|-------|----------|----------|------|
| `ButtonClick` | TitleController, SettingsPopup, PauseHandler, GameOverUI, DeathScreenUI | 버튼 클릭 (UI 전반) | ✅ |
| `InkShoot` | PlayerInk.cs:153 | 대시/먹물 발사 (Space) | ✅ |
| `CamouflageAttach` | CamouflageEventBridge.cs:213 | 의태 부착 시작 | ✅ |
| `CamouflagePerfect` | CamouflageEventBridge.cs:221 | 완벽 의태 도달 | ✅ |
| `CamouflageDetach` | CamouflageEventBridge.cs:319 | 의태 해제 | ✅ |
| `Charge` | ChichiChargeController.cs | 치치 접촉 충전 시작 (X키) | ✅ |
| `StageClear` | ZoneChanger.cs:494 | 구역 전환 완료 | ✅ |
| `GameOver` | GameManager.cs:245 | 플레이어 사망 | ✅ |
| `PredatorChase` | BossEnemyController, NormalEnemyController | 적이 Chase 상태 진입 | ✅ |

---

## BgmId 5종

| BgmId | Zone 번호 | 출력 파일 | 출력 상황 | 상태 |
|-------|-----------|----------|----------|------|
| `GrasslandCoast` | Zone 1 | ZoneChanger.cs | 구역 전환 시 자동 재생 | ✅ |
| `CoralReef` | Zone 2 | ZoneChanger.cs | 구역 전환 시 자동 재생 | ✅ |
| `SeaweedForest` | Zone 3 | ZoneChanger.cs | 구역 전환 시 자동 재생 | ✅ |
| `DeepSeaCliff` | Zone 4 | ZoneChanger.cs | 구역 전환 시 자동 재생 | ✅ |
| `DeepSeaRuins` | Zone 5 | ZoneChanger.cs | 구역 전환 시 자동 재생 | ✅ |

---

## 제거된 SfxId

| 제거된 Id | 이유 |
|----------|------|
| `ButtonClick1`, `ButtonClick2` | `ButtonClick`으로 통합 (Inspector에 clips[] 배열) |
| `InkShoot1~3` | `InkShoot`으로 통합 |
| `ChargeLoop` | `Charge`로 통합 |
| `PredatorDetected` | 불필요 |
| `PredatorUnderwaterPass`, `PredatorUnderwaterPass1~2` | 불필요 |
| `PredatorChase1~3` | `PredatorChase`로 통합 |
