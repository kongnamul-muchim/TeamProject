# Day 4: 의태 시스템 이벤트 브릿지 설계도

> **버전:** v1.0
> **작성일:** 2026-04-17
> **담당자:** Member A (코어 프로그래머)
> **목표:** 의태/발각/사망 상태 전환 시 C# 이벤트를 발생시켜 Member C의 파티클/셰이더 효과와 연결

---

## 1. 설계 원칙

### 1.1 DI (의존성 주입) 필수
- 절대 `GameObject.Find()`나 하드코딩된 태그 사용 금지
- 외부 요소 참조는 `[SerializeField]` 또는 인터페이스로만 접근

### 1.2 C# 이벤트 적극 활용
- 코어 시스템이 다른 시스템을 직접 호출하지 않음
- 상태 전환 시 `Action`/`UnityEvent` 발생만 담당
- 처리는 구독자(파티클/UI/사운드 시스템)가 알아서 담당

### 1.3 SRP (단일 책임 원칙)
- `CamouflageAdapter`: 입력 처리 + 상태 전환 + 이벤트 발생
- `CamouflageEventBridge`: 이벤트 구독 → 효과 시스템 연결
- 파티클/셰이더 시스템: 이벤트 수신 → 효과 재생 (Member C 담당)

---

## 2. 이벤트 아키텍처

### 2.1 이벤트 정의 위치

```
Assets/Core/Events/CamouflageEvents.cs      ← 의태 관련 이벤트 정의
Assets/Core/Events/GameEvents.cs            ← 게임 전반 이벤트 정의
```

### 2.2 이벤트 구조

```csharp
// CamouflageEvents.cs
public static class CamouflageEvents
{
    // 의태 상태 전환 이벤트
    public static event Action<CamouflageState> OnStateChanged;
    
    // 의태 시작 (None → Attached)
    public static event Action<GameObject> OnCamouflageStart;
    
    // 의태 완료 (Perfect 도달)
    public static event Action<GameObject> OnCamouflageComplete;
    
    // 의태 해제 (→ None)
    public static event Action<GameObject> OnCamouflageEnd;
    
    // 발각 감지 (의심도 100%)
    public static event Action OnDetected;
}
```

### 2.3 이벤트 흐름도

```
[CamouflageAdapter]
    ↓ 상태 전환 감지
    ↓ CamouflageEvents.OnStateChanged.Invoke(newState)
    ↓ CamouflageEvents.OnCamouflageStart.Invoke(target)
    ↓ CamouflageEvents.OnCamouflageComplete.Invoke(target)
    ↓ CamouflageEvents.OnCamouflageEnd.Invoke(target)
    │
    ├─→ [CamouflageEventBridge] (Member A)
    │       ↓ 이벤트 구독
    │       ↓ 효과 시스템에 전달
    │
    ├─→ [InkParticleEffect] (Member C)
    │       ↓ 파티클 재생
    │       ↓ 셰이더 효과 트리거
    │
    └─→ [SuspicionMeter] (기존)
            ↓ 의심도 조절
```

---

## 3. 구현 상세

### 3.1 CamouflageEvents.cs (신규)

```csharp
using System;
using UnityEngine;

namespace HideAndInk.Core.Events
{
    /// <summary>
    /// 의태 시스템 전역 이벤트
    /// 다른 시스템은 이 이벤트를 구독하여 상태 변화에 반응
    /// </summary>
    public static class CamouflageEvents
    {
        /// <summary> 의태 상태가 변경될 때 발생 </summary>
        public static event Action<CamouflageState> OnStateChanged;
        
        /// <summary> 의태 시작 시 발생 (None → Attached) </summary>
        public static event Action<GameObject> OnCamouflageStart;
        
        /// <summary> 완벽 의태 달성 시 발생 (Perfect 도달) </summary>
        public static event Action<GameObject> OnCamouflageComplete;
        
        /// <summary> 의태 해제 시 발생 (→ None) </summary>
        public static event Action<GameObject> OnCamouflageEnd;
        
        // 내부 호출 메서드 (CamouflageAdapter에서만 호출)
        internal static void InvokeStateChanged(CamouflageState state) 
            => OnStateChanged?.Invoke(state);
        
        internal static void InvokeCamouflageStart(GameObject target) 
            => OnCamouflageStart?.Invoke(target);
        
        internal static void InvokeCamouflageComplete(GameObject target) 
            => OnCamouflageComplete?.Invoke(target);
        
        internal static void InvokeCamouflageEnd(GameObject target) 
            => OnCamouflageEnd?.Invoke(target);
    }
}
```

### 3.2 CamouflageAdapter 수정 (이벤트 발생 추가)

```csharp
// CamouflageAdapter.cs - 상태 전환 시 이벤트 발생

// None → Attached 전환 시
if (prevState == CamouflageState.None && _stateMachine.CurrentState != CamouflageState.None)
{
    CamouflageEvents.InvokeStateChanged(_stateMachine.CurrentState);
    CamouflageEvents.InvokeCamouflageStart(_stateMachine.TargetObject);
}

// Perfect 도달 시
if (_stateMachine.CurrentState == CamouflageState.Perfect && prevState != CamouflageState.Perfect)
{
    CamouflageEvents.InvokeCamouflageComplete(_stateMachine.TargetObject);
}

// → None 복귀 시
if (wasNotNone && _stateMachine.CurrentState == CamouflageState.None)
{
    CamouflageEvents.InvokeCamouflageEnd(_stateMachine.TargetObject);
}
```

### 3.3 CamouflageEventBridge.cs (신규)

```csharp
using UnityEngine;
using HideAndInk.Core.Events;

namespace HideAndInk.Core.Perception
{
    /// <summary>
    /// 의태 이벤트를 효과 시스템에 연결하는 브릿지
    /// Member C의 파티클/셰이더 시스템과 연동
    /// </summary>
    public class CamouflageEventBridge : MonoBehaviour
    {
        [Header("효과 시스템 참조 (DI)")]
        [SerializeField] private InkParticleEffect inkParticleEffect;
        [SerializeField] private CamouflageShaderEffect shaderEffect;
        
        private void OnEnable()
        {
            CamouflageEvents.OnCamouflageStart += HandleCamouflageStart;
            CamouflageEvents.OnCamouflageComplete += HandleCamouflageComplete;
            CamouflageEvents.OnCamouflageEnd += HandleCamouflageEnd;
        }
        
        private void OnDisable()
        {
            CamouflageEvents.OnCamouflageStart -= HandleCamouflageStart;
            CamouflageEvents.OnCamouflageComplete -= HandleCamouflageComplete;
            CamouflageEvents.OnCamouflageEnd -= HandleCamouflageEnd;
        }
        
        private void HandleCamouflageStart(GameObject target)
        {
            inkParticleEffect?.PlayAttachEffect(target);
            shaderEffect?.TriggerAttachShader(target);
        }
        
        private void HandleCamouflageComplete(GameObject target)
        {
            inkParticleEffect?.PlayPerfectEffect(target);
            shaderEffect?.TriggerPerfectShader(target);
        }
        
        private void HandleCamouflageEnd(GameObject target)
        {
            inkParticleEffect?.PlayDetachEffect(target);
            shaderEffect?.TriggerDetachShader(target);
        }
    }
}
```

### 3.4 GameEvents.cs (발각/사망 이벤트)

```csharp
using System;

namespace HideAndInk.Core.Events
{
    /// <summary>
    /// 게임 전반 전역 이벤트
    /// </summary>
    public static class GameEvents
    {
        /// <summary> 플레이어 발각 시 발생 </summary>
        public static event Action OnPlayerDetected;
        
        /// <summary> 플레이어 사망 시 발생 </summary>
        public static event Action OnPlayerDeath;
        
        /// <summary> 스테이지 클리어 시 발생 </summary>
        public static event Action OnStageClear;
        
        internal static void InvokePlayerDetected() => OnPlayerDetected?.Invoke();
        internal static void InvokePlayerDeath() => OnPlayerDeath?.Invoke();
        internal static void InvokeStageClear() => OnStageClear?.Invoke();
    }
}
```

### 3.5 GameStateMachine 수정 (발각 이벤트 연동)

```csharp
// GameStateMachine.cs - Detected 상태 전환 시
public void TransitionTo(GameState newState)
{
    if (CanTransitionTo(newState))
    {
        _currentState = newState;
        
        if (newState == GameState.Detected)
        {
            GameEvents.InvokePlayerDetected();
        }
        else if (newState == GameState.Dead)
        {
            GameEvents.InvokePlayerDeath();
        }
    }
}
```

---

## 4. 파일 구조

```
Assets/Core/
├── Events/                          ← 신규 폴더
│   ├── CamouflageEvents.cs          ← 의태 이벤트 정의
│   ├── GameEvents.cs                ← 게임 전반 이벤트 정의
│   └── *.meta
├── Perception/
│   ├── CamouflageEventBridge.cs     ← 이벤트 브릿지 (신규)
│   ├── CamouflageTarget.cs          ← 기존 (수정 없음)
│   └── MaterialCloner.cs            ← 기존 (수정 없음)
├── Managers/
│   ├── GameManager.cs               ← 기존 (이벤트 구독 추가)
│   └── GameStateMachine.cs          ← 기존 (이벤트 발생 추가)
└── Interfaces/                      ← 기존 (수정 없음)

Assets/Scripts/Player/
└── CamouflageAdapter.cs             ← 기존 (이벤트 발생 추가)
```

---

## 5. Member C 연동 가이드

### 5.1 파티클 시스템 연동 방법

```csharp
// Member C가 작성할 파티클 시스템 예시
public class InkParticleEffect : MonoBehaviour
{
    [SerializeField] private ParticleSystem attachParticle;
    [SerializeField] private ParticleSystem perfectParticle;
    [SerializeField] private ParticleSystem detachParticle;
    
    public void PlayAttachEffect(GameObject target)
    {
        // 타겟 위치에서 부착 파티클 재생
        attachParticle.transform.position = target.transform.position;
        attachParticle.Play();
    }
    
    public void PlayPerfectEffect(GameObject target)
    {
        // 완벽 의태 파티클 재생
        perfectParticle.Play();
    }
    
    public void PlayDetachEffect(GameObject target)
    {
        // 의태 해제 파티클 재생
        detachParticle.Play();
    }
}
```

### 5.2 셰이더 효과 연동 방법

```csharp
// Member C가 작성할 셰이더 효과 예시
public class CamouflageShaderEffect : MonoBehaviour
{
    [SerializeField] private Material inkSpreadMaterial;
    
    public void TriggerAttachShader(GameObject target)
    {
        inkSpreadMaterial.SetFloat("_InkSpread", 1f);
    }
    
    public void TriggerPerfectShader(GameObject target)
    {
        inkSpreadMaterial.SetFloat("_InkSpread", 0f);
    }
    
    public void TriggerDetachShader(GameObject target)
    {
        inkSpreadMaterial.SetFloat("_InkSpread", 0.5f);
    }
}
```

---

## 6. 테스트 시나리오

| 테스트 케이스 | 기대 결과 |
|---------------|-----------|
| C 키 누름 (의태 시작) | `OnCamouflageStart` 발생 → 부착 파티클 재생 |
| 2초 유지 (완벽 의태) | `OnCamouflageComplete` 발생 → 완벽 의태 파티클 재생 |
| C 키 뗌 (의태 해제) | `OnCamouflageEnd` 발생 → 해제 파티클 재생 |
| 의심도 100% (발각) | `OnPlayerDetected` 발생 → 발각 효과 재생 |
| 이벤트 구독 해제 | `OnDisable`에서 정상 해제 (메모리 누수 없음) |

---

## 7. 주의사항

1. **이벤트 구독 해제 필수**: `OnDisable`에서 `-=`로 구독 해제
2. **null 체크**: `[SerializeField]` 참조가 없어도 이벤트 발생은 정상 작동
3. **성능**: 정적 이벤트이므로 과도한 구독은 메모리 누수 유발 가능
4. **스레드 안전**: Unity 메인 스레드에서만 호출 가정

---

> **문서 작성자:** Member A
> **최종 업데이트:** 2026-04-17
