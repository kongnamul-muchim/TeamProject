# 보스(곰치) 이동 로직 수정 보고서

> 날짜: 2026-04-24
> 대상: `Assets/Core/Enemy/Boss/BossEnemyController.cs`
> 커밋: `1d157ff`

---

## 📋 요구사항

1. **Patrol**: Player가 시야각에 보이면 따라가야 함 (의태 무관)
2. **의태 시 탐색**: Player의 마지막 위치를 기억하고 그 주변을 계속 탐색
3. **Chase**: 의태를 무시하고 Player를 추격
4. **Search**: Player가 의태를 핟도 시야각에 보이면 Player에게 다가가야 함

---

## 🔧 수정 내용

### 1. `CanSeePlayer()` - 순수 시야각 감지로 변경

**변경 전:**
```csharp
private bool CanSeePlayer()
{
    if (visionSensor == null || _playerTransform == null) return false;
    if (IsPlayerCamouflaging()) return false;  // ← 의태 중이면 감지 불가
    return visionSensor.CanSee(_playerTransform.gameObject);
}
```

**변경 후:**
```csharp
private bool CanSeePlayer()
{
    if (visionSensor == null || _playerTransform == null) return false;
    return visionSensor.CanSee(_playerTransform.gameObject);  // ← 의태 무관
}
```

- 상태 전환용 시야 감지는 **의태 여부와 관계없이** ConeVisionSensor의 거리/각도/장애물만 체크
- 의심도 상승용 `CanSeePlayerForSuspicion()`은 기존처럼 의태 체크 유지

### 2. `Patrol → Chase` 전환 조건 단순화

**변경 전:** `canSeePlayer && suspicionLevel >= SuspicionLevel.Danger`

**변경 후:** `canSeePlayer`

- 시야각에 Player가 보이면 **즉시 Chase로 전환** (의심도와 무관)

### 3. `Chase → Search` 전환을 거리 기반으로 변경

**변경 전:** `!canSeePlayer` (시야각 기반, 의태 중이면 false)

**변경 후:** `_chaseBehavior.IsPlayerOutOfRange()` (추적 실패 거리 기반)

- Chase 상태에서는 **의태를 무시**하고 `ChaseBehavior`의 `_loseDistance`로 Player 놓침 판단
- Player가 추적 범위를 벗어나면 Search로 전환, 이때 마지막 위치 저장

### 4. `Search → Chase` 전환

- `canSeePlayer` 사용 (이미 `CanSeePlayer()` 수정으로 의태 무관하게 동작)
- Search 중 시야각에 Player가 보이면 즉시 Chase로 재전환

---

## ✅ 상태별 동작 요약

| 상태 | Player 감지 방식 | 의태 처리 | 전환 조건 |
|------|----------------|----------|----------|
| **Patrol** | ConeVisionSensor (시야각/거리/장애물) | 무시 | 시야에 보이면 → Chase |
| **Chase** | `_playerTransform` 직접 추적 | 무시 | `_loseDistance` 초과 → Search |
| **Search** | ConeVisionSensor (시야각/거리/장애물) | 무시 | 시야에 보이면 → Chase |

---

## 📝 참고

- `SearchBehavior`는 이미 `_lastKnownPosition` 기반 주변 탐색을 수행 중 (별도 수정 불필요)
- `ChaseBehavior`는 이미 `_playerTransform`을 직접 추적 중 (별도 수정 불필요)
- AmbushGimmick(가자미) 전용 로직은 기존과 동일하게 유지
