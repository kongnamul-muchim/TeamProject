# Camouflage 시스템 수정 내용

## 수정 일시
- 2026-04-17 (수정 재적용: 2026-04-17)

## 수정 파일
- `Assets/Scripts/Player/CamouflageAdapter.cs`

---

## 1. Outline 색상 어두운 계열로 변경

**변경 전:**
- 오브젝트 색상과 동일한 색상으로 Outline 변경

**변경 후:**
```csharp
Color darkOutlineColor = new Color(
    targetColor.r * 0.55f,
    targetColor.g * 0.55f,
    targetColor.b * 0.55f
);
```

**사유:** 오브젝트 색상과 동일하면 시각적으로 구분이 안 되므로, 같은 계열의 어두운 색상으로 변경

---

## 2. 의태 거리 조정 (0.5f → 0.2f)

**변경 전:** `BACK_OFFSET = 0.5f`
**변경 후:** `BACK_OFFSET = 0.2f`

**사유:** 의태 시 오브젝트에 더 가깝게 달라붙도록 거리 감소

---

## 3. 이동 방식 변경 (Z값만 선형 보간)

**변경 전:**
- X, Y, Z 모두 Lerp로 보간 (동시 이동)
- `Mathf.Lerp(currentZ, targetZ, progress)` 방식 (비선형)

**변경 후:**
```csharp
// attachMoveSpeed = 도달까지 걸리는 시간(초)
_positionLerpProgress += Time.deltaTime / attachMoveSpeed;
_positionLerpProgress = Mathf.Clamp01(_positionLerpProgress);

float startZ = _originalPosition.z;
float targetZ = targetPos.z;
float newZ = startZ + ((targetZ - startZ) * _positionLerpProgress);
transform.position = new Vector3(transform.position.x, transform.position.y, newZ);
```

**사유:**
- X, Y값은 Player 이동에 따라 자유롭게 유지
- Z값만 오브젝트 위치로 **선형 보간**하여 부드러운 의태 연출
- `attachMoveSpeed` 값이 도달까지 걸리는 시간(초)으로 설정됨

---

## 4. UpdatePosition() 실행时机 수정 (버그 수정)

**문제:** Attached 상태에서 `IsAttachedComplete=False`일 때 `return`되어 Z 보간이 실행 안 됨

**변경 전:**
```csharp
// Attached 완료 전에는 추가 처리 안 함
if (_stateMachine.CurrentState == CamouflageState.Attached && !_stateMachine.IsAttachedComplete)
{
    return;  // ← UpdatePosition()이 실행 안 됨
}

// 색상 보간 업데이트
UpdateBlend();

// 의태 상태에 따른 위치 조정
UpdatePosition();  // ← 도달하지 못함
```

**변경 후:**
```csharp
// 의태 상태에 따른 위치 조정 (Attached 상태에서도 실행되어야 함)
UpdatePosition();

// Attached 완료 전에는 색상/Outline 처리 안 함
if (_stateMachine.CurrentState == CamouflageState.Attached && !_stateMachine.IsAttachedComplete)
{
    return;
}

// 색상 보간 업데이트
UpdateBlend();

// Outline 색상 업데이트 (뒷면 의태 시)
UpdateOutlineColor();
```

**사유:** Attached 상태에서도 Z 보간이 진행되어야 부드러운 이동 연출 가능

---

## 5. Outline 변경 기능 추가

**새로 추가된 메서드:**
- `SetupOutlineForTarget()`: 앞면/뒷면 판정 및 Outline 초기화
- `UpdateOutlineColor()`: 뒷면 접근 시 Outline 색상을 오브젝트 색상(어둡게)으로 변경
- `RestoreOutline()`: 의태 해제 시 Outline을 흰색으로 복원

**앞면/뒷면 판정 로직:**
```csharp
Vector3 dirToPlayer = (transform.position - target.transform.position).normalized;
Vector3 targetForward = target.transform.forward;
float dot = Vector3.Dot(dirToPlayer, targetForward);
_isAttachingFromBehind = dot < 0f;  // 음수면 뒷면
```

---

## 참고

- Outline 변경은 뒷면 접근 시에만 적용 (앞면 접근 시 Outline 변경 없음)
- 의태 거리 `BACK_OFFSET`은 앞면/뒷면 모두 동일하게 적용 (0.2f)
- Z값 보간은 `Attached` 상태에서만 적용, `Locked` 이후에는 X,Y 유지하며 Z만 타겟으로 고정
- `attachMoveSpeed` 기본값은 Inspector에서 5로 설정 (5초에 도달)
- 의태 해제 시 Outline은 즉시 흰색으로 복원됨
