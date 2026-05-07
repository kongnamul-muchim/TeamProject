# 오디오 시스템 설계서

> 작성: 2026-04-30 / 갱신: 2026-05-07
> 참조: `문어먹물꿈질음악/` 폴더 사운드 에셋 기준
> 상태: **SfxManager / BgmManager 분리 완료 (Inspector 할당 방식)**

---

## 1. 현재 구조

```
Assets/Core/
├── Audio/
│   ├── SfxManager.cs             ← [신규] ISfxService 구현, Inspector 할당
│   ├── BgmManager.cs             ← [신규] IBgmService 구현, Inspector 할당
│   ├── SfxId.cs                  ← [완료] 효과음 enum (20종)
│   ├── BgmId.cs                  ← [완료] 배경음악 enum (5지역)
│   └── AmbientId.cs              ← [완료] 환경사운드 enum (5지역)
│
├── Interfaces/
│   ├── ISfxService.cs            ← [수정] Volume/Muted 프로퍼티 추가
│   ├── IBgmService.cs            ← [수정] Volume/Muted 프로퍼티 추가
│   ├── IAmbientService.cs        ← [완료]
│   └── ISoundEffect.cs           ← [DEPRECATED] → ISfxService 사용 권장
│
└── Managers/
    └── GameManager.cs            ← [수정] SfxManager/BgmManager SerializeField + DI 등록
```

**AudioManager.cs는 제거됨** — SfxManager와 BgmManager로 완전 분리

---

## 2. Inspector 구성

### SfxManager (씬에 배치)

```
SfxManager (GameObject)
└── SfxManager (Component)
    ├── SFX Clips (size: 11)
    │   ├── [0]  id: ButtonClick          clips[3]: 🎵🎵🎵  ← 랜덤 재생
    │   ├── [1]  id: InkShoot            clips[4]: 🎵🎵🎵🎵
    │   ├── [2]  id: CamouflageAttach    clips[1]: 🎵
    │   ├── [3]  id: CamouflagePerfect   clips[1]: 🎵
    │   ├── [4]  id: CamouflageDetach    clips[1]: 🎵
    │   ├── [5]  id: Charge              clips[1]: 🎵
    │   ├── [6]  id: StageClear          clips[1]: 🎵
    │   ├── [7]  id: GameOver            clips[1]: 🎵
    │   ├── [8]  id: PredatorDetected     clips[1]: 🎵
    │   ├── [9]  id: PredatorUnderwaterPass clips[3]: 🎵🎵🎵
    │   └── [10] id: PredatorChase       clips[4]: 🎵🎵🎵🎵
    └── Default Volume: 0.5
```

> **여러 클립 배열**: 같은 Id에 여러 클립을 Inspector에서 배열로 넣으면 재생 시 랜덤 선택

### BgmManager (씬에 배치)

```
BgmManager (GameObject)
└── BgmManager (Component)
    ├── BGM Clips (size: 5)
    │   ├── [0] id: GrasslandCoast  clip: 🎵
    │   ├── [1] id: CoralReef       clip: 🎵
    │   ├── [2] id: SeaweedForest   clip: 🎵
    │   ├── [3] id: DeepSeaCliff    clip: 🎵
    │   └── [4] id: DeepSeaRuins    clip: 🎵
    └── Default Volume: 0.5
```

---

## 3. 생명주기

```
씬에 미리 배치
    ↓
Awake() → DontDestroyOnLoad (씬 전환 후에도 유지)
    ↓
GameManager.Awake() → SerializeField 참조 → DI 컨테이너에 등록
    ↓
다른 컴포넌트 → ISfxService / IBgmService 주입받아 사용
```

**중요**: 각 매니저는 씬에 미리 배치되어 있어야 Inspector 할당 가능.
GameManager가 SerializeField로 참조하여 DI 등록.

---

## 4. DI 등록 (GameManager.cs)

```csharp
[Header("Audio")]
[SerializeField] private SfxManager sfxManager;
[SerializeField] private BgmManager bgmManager;

private void RegisterAudioServices()
{
    if (sfxManager != null)
        _rootContainer.RegisterInstance<ISfxService>(sfxManager, ServiceLifetime.Singleton);
    if (bgmManager != null)
        _rootContainer.RegisterInstance<IBgmService>(bgmManager, ServiceLifetime.Singleton);
}
```

---

## 5. 각 매니저 특징

| 항목 | SfxManager | BgmManager |
|------|-----------|------------|
| 인터페이스 | `ISfxService` | `IBgmService` |
| AudioSource | 1개 (PlayOneShot 겹침 재생) | 1개 (Loop) |
| 볼륨/음소거 | 독립 저장 (PlayerPrefs) | 독립 저장 (PlayerPrefs) |
| 페이드 | 없음 | 지원 (Play/Stop 시 fadeDuration) |
| 3D 사운드 | `PlayAtPoint()` 지원 | 해당 없음 |
| Inspector | SfxId 20종 + AudioClip | BgmId 5종 + AudioClip |

---

## 6. 사용 예시

### Non-MonoBehaviour (생성자 주입)
```csharp
public class PlayerMovement : IPlayerMovement
{
    private readonly ISfxService _sfx;
    public PlayerMovement(ISfxService sfxService)
    {
        _sfx = sfxService;
    }
    public void ShootInk()
    {
        _sfx.Play(SfxId.InkShoot);
    }
}
```

### MonoBehaviour (GameManager.Container 경유)
```csharp
public class CamouflageAdapter : MonoBehaviour
{
    private ISfxService _sfx;
    private void Awake()
    {
        if (GameManager.Container.IsRegistered<ISfxService>())
            _sfx = GameManager.Container.Resolve<ISfxService>();
    }
    private void OnCamouflageStart()
    {
        _sfx?.Play(SfxId.CamouflageAttach);
    }
}
```

### BGM 전환 (StageManager 등)
```csharp
public class StageManager
{
    private readonly IBgmService _bgm;
    public StageManager(IBgmService bgmService)
    {
        _bgm = bgmService;
    }
    public void EnterStage(BgmId bgmId)
    {
        _bgm.Play(bgmId, 1.5f); // 1.5초 페이드 전환
    }
}
```

---

## 7. AmbientId (향후 확장)

현재 `IAmbientService` / `AmbientId`는 정의만 되어 있고 미구현.
필요 시 BgmManager와 동일한 패턴으로 AmbientManager 추가 예정.

---

## 8. 남은 작업

| 단계 | 내용 | 상태 |
|-----|------|------|
| ✅ Phase 0 | Enum + Interface 정의 | 완료 |
| ✅ Phase 1 | SfxManager / BgmManager 분리 구현 | 완료 |
| ✅ Phase 2 | GameManager DI 등록 + SettingsPopup 연동 | 완료 |
| ⬜ Phase 3 | 씬에 SfxManager/BgmManager 배치 + 클립 할당 | 팀원 작업 |
| ⬜ Phase 4 | 각 컴포넌트에 사운드 주입 (CamouflageAdapter 등) | 개발 필요 |
| ⬜ Phase 5 | AmbientManager 구현 (필요시) | 추후 |

---

## 9. 씬 설정 가이드 (팀원용)

1. Hierarchy에서 우클릭 → Create Empty → 이름 `SfxManager`
2. `SfxManager` 선택 → Add Component → `SfxManager`
3. SFX Clips 배열에 각 SfxId별 AudioClip 드래그
4. 위와 동일하게 `BgmManager` 생성 및 BGM 클립 할당
5. `GameManager` 선택 → Inspector의 `Sfx Manager` / `Bgm Manager` 슬롯에 연결
6. 씬 저장

---

*이 문서는 AI가 작성했습니다.*
