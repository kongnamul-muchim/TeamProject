# Member C 전달용: 의태 효과 시스템 연동 가이드

> **버전:** v1.1
> **작성일:** 2026-04-17
> **대상:** Member C (이펙트/사운드 담당)

---

## 📦 전달해야 할 파일 목록 (2개 .cs)

Member C는 아래 **2개의 C# 스크립트**를 만들어서 Member A에게 전달해야 합니다.
각 스크립트에는 **지정된 메서드**가 반드시 포함되어야 합니다.

| 파일명 | 역할 | Inspector 연결 위치 |
|--------|------|---------------------|
| `InkVFX.cs` | 파티클/시각 효과 | `Ink Particle Effect` |
| `InkSFX.cs` | 효과음 재생 | `Sound Effect` |

---

## 🛠️ 구현해야 할 메서드 (6개)

각 스크립트에는 아래 메서드가 **public**으로 구현되어 있어야 합니다.
(내부 로직은 Member C가 자유롭게 구현하면 됩니다.)

### 1. `InkVFX.cs` (파티클/시각 효과)
```csharp
// 의태 시작 시 (C 키 누름) - 타겟 위치에서 잉크 튀는 효과
public void PlayAttachEffect(GameObject target) { }

// 완벽 의태 시 (2초 유지) - 타겟 색으로 완전히 변하는 효과 (예: 잉크 번짐 마무리)
public void PlayPerfectEffect(GameObject target) { }

// 의태 해제 시 (C 키 뗌) - 다시 나타나는 효과
public void PlayDetachEffect(GameObject target) { }
```

### 2. `InkSFX.cs` (사운드)
```csharp
// 의태 시작 시 - "슈웅~" 소리
public void PlayAttachSound() { }

// 완벽 의태 시 - "띵!" 소리
public void PlayPerfectSound() { }

// 의태 해제 시 - "빠빅!" 소리
public void PlayDetachSound() { }
```

---

## 📋 예시 코드 템플릿

Member C가 바로 복사해서 쓸 수 있는 템플릿입니다.

### `InkVFX.cs`
```csharp
using UnityEngine;

public class InkVFX : MonoBehaviour
{
    [SerializeField] private ParticleSystem attachParticle;
    [SerializeField] private ParticleSystem perfectParticle;
    [SerializeField] private ParticleSystem detachParticle;

    public void PlayAttachEffect(GameObject target)
    {
        if (attachParticle != null)
        {
            attachParticle.transform.position = target.transform.position;
            attachParticle.Play();
        }
    }

    public void PlayPerfectEffect(GameObject target)
    {
        if (perfectParticle != null) perfectParticle.Play();
    }

    public void PlayDetachEffect(GameObject target)
    {
        if (detachParticle != null) detachParticle.Play();
    }
}
```

### `InkSFX.cs`
```csharp
using UnityEngine;

public class InkSFX : MonoBehaviour
{
    [SerializeField] private AudioClip attachClip;
    [SerializeField] private AudioClip perfectClip;
    [SerializeField] private AudioClip detachClip;
    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
    }

    public void PlayAttachSound()
    {
        if (attachClip != null) audioSource.PlayOneShot(attachClip);
    }

    public void PlayPerfectSound()
    {
        if (perfectClip != null) audioSource.PlayOneShot(perfectClip);
    }

    public void PlayDetachSound()
    {
        if (detachClip != null) audioSource.PlayOneShot(detachClip);
    }
}
```

---

## 🔗 Inspector 연결 방법 (Member A 담당)

Member C가 위 2개 파일을 전달하면, Member A는 Unity Inspector에서 다음과 같이 연결합니다.

1. Hierarchy에서 `EventBridge` 오브젝트 선택
2. `CamouflageEventBridge` 컴포넌트 확인
3. 각 슬롯에 Member C의 컴포넌트를 드래그앤드롭:
   - `Ink Particle Effect` → `InkVFX` 컴포넌트
   - `Sound Effect` → `InkSFX` 컴포넌트

**코드 수정 없이 드래그앤드롭만으로 연동 완료!**

---

## ⚠️ 주의사항

1. 메서드 이름은 **정확히 일치**해야 합니다. (`PlayAttachEffect` 등)
2. 메서드는 **public**이어야 합니다.
3. `GameObject target` 파라미터는 선택 사항이지만, 위치 기반 효과에 필요합니다.
4. 컴포넌트는 **씬에 존재하는 오브젝트**에 부착되어 있어야 합니다.

---

> **문의:** Member A (코어 프로그래머)
> **최종 업데이트:** 2026-04-17
