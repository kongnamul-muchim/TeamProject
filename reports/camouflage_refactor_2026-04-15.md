# 의태 시스템 리팩토링 보고서

> 작성일: 2026-04-15

---

## 📋 변경 요약

의태 시스템의 색상 조절 방식을 **OriginalRate 기반**으로 전면 개편.

---

## 🔄 변경 전 vs 변경 후

### 기존 방식
- 의태 시 Default-Material → Octopus Material로 변경
- 의태 해제 시 Octopus → Default로 복원
- 색상 보간: 천천히 오브젝트 색으로 변화
- OriginalRate: 사용하지 않음

### 새 방식
- **Octopus Material 고정** (의태/해제 시 변경 없음)
- 색상: `SpriteRenderer.color`를 타겟 색으로 즉시 변경
- **OriginalRate**로 시각적 색상 조절
  - `OriginalRate = 1`: 원본 형태
  - `OriginalRate = 0`: 오브젝트 색상으로 완전히 변경

---

## 📁 변경된 파일

| 파일 | 변경 내용 |
|------|----------|
| `IMaterialCloner.cs` | `SetOriginalRate()`, `GetCurrentMaterialColor()` 인터페이스 추가 |
| `MaterialCloner.cs` | `SetOriginalRate()`, `GetCurrentMaterialColor()` 구현 |
| `CamouflageAdapter.cs` | 의태 로직 전체 수정 (OriginalRate 기반) |

---

## 🎮 동작 방식

| 상태 | SpriteRenderer.color | OriginalRate |
|------|---------------------|--------------|
| Attached | 타겟 색으로 즉시 변경 | 1 (유지) |
| Locked | 유지 | 1 (유지) |
| Partial | 유지 | 1 → 0 감소 (blendProgress 비례) |
| Perfect | 유지 | 0 |
| 해제 → None | **변경 없음** | 0 → 1 복원 (1.4초) |

### 핵심 로직
1. **의태 시작**: `SpriteRenderer.color`를 타겟 오브젝트 색으로 즉시 변경
2. **Partial 상태**: `OriginalRate`를 1에서 0으로 천천히 감소
3. **Perfect 상태**: `OriginalRate = 0` (완전히 오브젝트 색상)
4. **해제 시**: `SpriteRenderer.color` 미변경, `OriginalRate`만 0→1 복원

---

## 🔧 구현 세부사항

### IMaterialCloner.cs
```csharp
// OriginalRate 설정 (의태 강도 조절)
void SetOriginalRate(float rate);

// 현재 Material의 색상 가져오기
Color GetCurrentMaterialColor();
```

### MaterialCloner.cs
```csharp
public void SetOriginalRate(float rate)
{
    if (_renderer != null)
    {
        _renderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetFloat("_OriginalRate", rate);
        _renderer.SetPropertyBlock(_propertyBlock);
    }
}
```

### CamouflageAdapter.cs
- `_isRestoringRate`: OriginalRate 복원 중 여부
- `_rateRestoreProgress`: 복원 진행도
- `RATE_RESTORE_DURATION = 1.4f`: 복원 시간 (lockTime + blendTime)

---

## 📝 참고

- Shader: `Assets/Shader/Test.shadergraph`
- Octopus.mat 기본값: `_OriginalRate = 1`
- 의존성: CamouflageStateMachine, CamouflageDetector

---

*End of Report*
