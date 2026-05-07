# 오디오 시스템 설계서

> 작성: 2026-04-30
> 참조: `문어먹물꿈질음악/` 폴더 사운드 에셋 기준
> 상태: **인터페이스 및 Enum 정의 완료** / AudioManager 구현 미정

---

## 1. 현재까지 완료된 구조

```
Assets/Core/
├── Audio/
│   ├── AudioManager.cs          ← 기존 Singleton (아직 인터페이스 미연동)
│   ├── SfxId.cs                 ← [완료] 효과음 enum (20종)
│   ├── BgmId.cs                 ← [완료] 배경음악 enum (5지역)
│   └── AmbientId.cs             ← [완료] 환경사운드 enum (5지역)
│
├── Interfaces/
│   ├── ISfxService.cs           ← [완료] 효과음 재생 인터페이스
│   ├── IBgmService.cs           ← [완료] BGM 재생 인터페이스
│   ├── IAmbientService.cs       ← [완료] 환경음 재생 인터페이스
│   └── ISoundEffect.cs          ← [수정] Deprecated → ISfxService 사용 권장
│
└── Managers/
    └── GameManager.cs           ← [수정] RegisterAudioServices() 추가 (현재 주석)
```

---

## 2. 오디오 타입별 구성

### 2.1 SfxId (효과음, 20종)

| 카테고리 | SfxId 값 | 대응 파일 | 재생 타이밍 |
|---------|----------|----------|------------|
| UI | `ButtonClick`, `ButtonClick1`, `ButtonClick2` | `버튼효과음0/1/2.wav` | 버튼 클릭 |
| 잉크 발사 | `InkShoot`, `InkShoot1~3` | `먹물발사효과음.wav` (+variants) | 플레이어 잉크 발사 |
| 의태 | `CamouflageAttach`, `CamouflagePerfect`, `CamouflageDetach` | `의태효과음.mp3/.ogg/.wav` | 의태 시작/완료/해제 |
| 충전 | `Charge`, `ChargeLoop` | `충전효과음.wav/.ogg` | 잉크 충전 시작/루프 |
| 게임 상태 | `StageClear`, `GameOver` | `다음스테이지넘어갈때효과음.wav`, `게임오버효과음.wav` | 클리어/사망 |
| 포식자 | `PredatorDetected`, `PredatorUnderwaterPass(0~2)`, `PredatorChase(0~3)` | `포식자*효과음.wav` 등 | 포식자 관련 모든 상황 |

### 2.2 BgmId (배경음악, 5지역)

| BgmId | 대응 파일 | 재생 타이밍 |
|-------|----------|------------|
| `GrasslandCoast` | `초원해안Bgm.mp3` | 스테이지 1 |
| `CoralReef` | `산호초Bgm.wav` | 스테이지 2 |
| `SeaweedForest` | `해초숲Bgm.mp3` | 스테이지 3 |
| `DeepSeaCliff` | `심해절벽Bgm.wav` (+variants) | 스테이지 4 |
| `DeepSeaRuins` | `심해페허Bgm.mp3` | 스테이지 5 |

### 2.3 AmbientId (환경사운드, 5지역)

| AmbientId | 대응 파일 | 설명 |
|-----------|----------|------|
| `GrasslandCoast` | `초원해안 환경사운드.wav` | BGM 위에 겹치는 배경 루프 |
| `CoralReef` | `산호초환경사운드.flac` | |
| `SeaweedForest` | `해초숲환경사운드.mp3` | |
| `DeepSeaCliff` | `심해절벽환경사운드.wav` | |
| `DeepSeaRuins` | `심해페허환경사운드.wav` | |

---

## 3. 의존성 주입 설계도

### 3.1 컴포넌트 → 주입할 오디오 인터페이스 매핑

| 컴포넌트 | 파일 위치 | 주입할 Interface | 사용할 SfxId/BgmId |
|---------|----------|-----------------|-------------------|
| **CamouflageAdapter** | `Scripts/Player/CamouflageAdapter.cs` | `ISfxService` | `CamouflageAttach`, `CamouflagePerfect`, `CamouflageDetach` |
| **PlayerMovementAdapter** | `Scripts/Player/PlayerMovementAdapter.cs` | `ISfxService` | `InkShoot(0~3)` (랜덤) |
| **PlayerInk** | `Scripts/PlayerInk.cs` | `ISfxService` | `Charge`, `ChargeLoop` |
| **ChichiChargeController** | `Scripts/siyeon1/ChichiChargeController.cs` | `ISfxService` | `Charge` |
| **SuspicionMeterUI** | `Core/Perception/SuspicionMeterUI.cs` | `ISfxService` | `PredatorDetected` |
| **EnemyAIController** | `Core/Enemy/EnemyAIController.cs` | `ISfxService` | `PredatorChase(0~3)`, `PredatorUnderwaterPass(0~2)` |
| **SuspicionToGameStateLink** | `Core/Perception/SuspicionToGameStateLink.cs` | `ISfxService` | `GameOver`, `StageClear` |
| **UI 버튼** | `Scripts/UI/SettingsPopup.cs` 등 | `ISfxService` | `ButtonClick(0~2)` |
| **StageManager** (미구현) | — | `IBgmService` | 지역별 BGM 전환 |
| **StageManager** (미구현) | — | `IAmbientService` | 지역별 Ambient 전환 |

### 3.2 주입 예시 코드

**Non-MonoBehaviour (생성자 주입):**
```csharp
public class PlayerMovement : IPlayerMovement
{
    private readonly ISfxService _sfx;

    public PlayerMovement(ISfxService sfxService)  // ← DIContainer가 자동 주입
    {
        _sfx = sfxService;
    }

    public void ShootInk()
    {
        _sfx.Play(SfxId.InkShoot);  // 또는 랜덤 variant
        // _sfx.Play(GetRandomInkShootVariant());
    }
}
```

**MonoBehaviour (GameManager.Container 경유):**
```csharp
public class CamouflageAdapter : MonoBehaviour
{
    private ISfxService _sfx;

    private void Awake()
    {
        // DI 컨테이너에서 서비스 해결
        if (GameManager.Container.IsRegistered<ISfxService>())
            _sfx = GameManager.Container.Resolve<ISfxService>();
    }

    private void OnCamouflageStart()
    {
        _sfx?.Play(SfxId.CamouflageAttach);
    }

    private void OnCamouflagePerfect()
    {
        _sfx?.Play(SfxId.CamouflagePerfect);
    }

    private void OnCamouflageEnd()
    {
        _sfx?.Play(SfxId.CamouflageDetach);
    }
}
```

---

## 4. DI 등록 현황 (GameManager.cs)

```csharp
// GameManager.RegisterCoreServices() — 86~88번째 줄
// 오디오 서비스 등록 (TODO: AudioManager가 인터페이스 구현 후 활성화)
// RegisterAudioServices();

// GameManager.RegisterAudioServices() — 100~110번째 줄 (현재 주석)
private void RegisterAudioServices()
{
    // _rootContainer.Register<ISfxService, AudioManager>(ServiceLifetime.Singleton);
    // _rootContainer.Register<IBgmService, AudioManager>(ServiceLifetime.Singleton);
    // _rootContainer.Register<IAmbientService, AudioManager>(ServiceLifetime.Singleton);
}
```

---

## 5. 결정 사항: AudioManager를 만들지 말지

### 현황
- `Assets/Core/Audio/AudioManager.cs` — **Singleton MonoBehaviour**가 이미 존재
- 볼륨 조절, 음소거, BGM/SFX 재생 기본 기능 보유
- **아직** `ISfxService`, `IBgmService`, `IAmbientService`를 구현하지 않음

### 선택지

#### A. AudioManager가 인터페이스 구현 (권장)
```
AudioManager : MonoBehaviour, ISfxService, IBgmService, IAmbientService
```
- **장점**: 기존 코드 재활용, Singleton 그대로 사용
- **단점**: 하나의 클래스가 3개 인터페이스 구현 → 파일이 커짐
- **DI 등록**: AudioManager 자체를 Singleton으로 등록

#### B. 전용 Player 클래스 분리
```
SfxPlayer : ISfxService     ← AudioManager가 내부에서 사용
BgmPlayer : IBgmService     ← AudioManager가 내부에서 사용
AmbientPlayer : IAmbientService  ← AudioManager가 내부에서 사용
```
- **장점**: 관심사 분리 (SRP), 각각 독립적으로 테스트 가능
- **단점**: 클래스 수 증가, AudioManager와의 관계 설정 필요
- **추천**: 프로젝트 규모가 커지면 이쪽으로 리팩토링

#### C. AudioManager 유지 + Adapter/Wrapper
```
AudioManager : MonoBehaviour (Singleton, 변경 없음)
AudioServiceAdapter : ISfxService, IBgmService, IAmbientService
    → 내부에서 AudioManager.Instance 호출
```
- **장점**: AudioManager 변경 불필요, 기존 코드 영향 없음
- **단점**: Adapter 계층 하나 더 생김

### 권장: **A → 나중에 B로 리팩토링**

1. 지금은 **A (AudioManager에 인터페이스 구현)** 로 빠르게 연동
2. AudioManager가 너무 커지면 B로 분리

### 활성화 순서

```
1. AudioManager에 : ISfxService, IBgmService, IAmbientService 추가
   → using HideAndInk.Core.Audio; using HideAndInk.Core.Interfaces;
   
2. 각 인터페이스 메서드 구현
   → Play(SfxId id) { Resources.Load<AudioClip>($"SFX/{id}") → PlayOneShot }
   → Play(BgmId id) { Resources.Load<AudioClip>($"BGM/{id}") → Play }
   
3. GameManager.RegisterAudioServices() 주석 해제
   → _rootContainer.Register<ISfxService, AudioManager>(ServiceLifetime.Singleton);
   → _rootContainer.Register<IBgmService, AudioManager>(ServiceLifetime.Singleton);
   → _rootContainer.Register<IAmbientService, AudioManager>(ServiceLifetime.Singleton);

4. 각 컴포넌트에서 ISfxService/IBgmService 주입받아 사용
```

---

## 6. 사운드 에셋 배치 제안

사운드 파일을 `Resources/` 폴더에 넣을 경우 로딩이 간편:

```
Assets/Resources/
└── Audio/
    ├── SFX/
    │   ├── ButtonClick.wav
    │   ├── ButtonClick1.wav
    │   ├── InkShoot.wav
    │   ├── InkShoot1.wav
    │   ├── CamouflageAttach.wav
    │   ├── CamouflagePerfect.wav
    │   ├── CamouflageDetach.wav
    │   ├── Charge.wav
    │   ├── StageClear.wav
    │   ├── GameOver.wav
    │   ├── PredatorDetected.wav
    │   └── ...
    ├── BGM/
    │   ├── GrasslandCoast.wav
    │   ├── CoralReef.wav
    │   └── ...
    └── Ambient/
        ├── GrasslandCoast.wav
        └── ...
```

> **참고**: Resources.Load는 편리하지만 성능 이슈가 있을 수 있음.
> 차후 Addressables 또는 직접 AudioClip 참조 방식으로 전환 가능.

---

## 7. 파일 정리 (중복 제거)

`문어먹물꿈질음악/` 폴더에 같은 사운드가 여러 확장자로 중복됨.
다음 기준으로 정리 권장:

| 구분 | 선택 확장자 | 이유 |
|------|-----------|------|
| 효과음(SFX) | `.wav` | 짧고 고품질, 무손실 |
| 배경음악(BGM) | `.ogg` | 길고 압축 필요, 무료 라이선스 |
| 환경사운드(Ambient) | `.wav` 또는 `.ogg` | 취향껏 |

---

## 8. 타임라인

| 단계 | 내용 | 담당 |
|-----|------|------|
| ✅ Phase 0 | Enum + Interface 정의 | 완료 |
| ✅ Phase 0 | DI 등록 구멍 | 완료 |
| ⬜ Phase 1 | AudioManager 인터페이스 구현 | AudioManager에 `: ISfxService, IBgmService, IAmbientService` |
| ⬜ Phase 2 | GameManager.RegisterAudioServices() 활성화 | 주석 해제 |
| ⬜ Phase 3 | 사운드 파일 Resources/로 이동 + 이름 정리 | 파일 복사 |
| ⬜ Phase 4 | 각 컴포넌트에 사운드 주입 | CamouflageAdapter 등 |
| ⬜ Phase 5 | 볼륨/음소거 UI 연동 (SettingsPopup) | AudioManager와 연동 확인 |

---

*이 문서는 AI(Orchestrator)가 작성했습니다.*
*문의: siyeon1 브랜치의 문어먹물꿈질음악/ 폴더 참조*
