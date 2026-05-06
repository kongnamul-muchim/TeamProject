# 남은 시스템 설계서

> 작성: 2026-05-06
> 참조: AudioSystem_Design.md, Spec.md, Agents.md
> 상태: **설계 단계** - 구현 전 설계 문서

---

## 목차
1. [사망원인별 로그 출력 시스템](#1-사망원인별-로그-출력-시스템)
2. [오디오매니저 (AudioManager)](#2-오디오매니저-audiomanager)
3. [스토리매니저 (StoryManager)](#3-스토리매니저-storymanager)

---

# 1. 사망원인별 로그 출력 시스템

## 1.1 개요

현재 `PlayerDeathEvent`는 존재하지만 사망 원인을 구분하지 않음.
사망 시점에 **어떤 원인으로 죽었는지**를 로그 파일에 기록하고,
필요시 UI/분석에서 활용할 수 있도록 시스템 확장.

## 1.2 사망 원인 (DeathCause) Enum

```csharp
// Assets/Core/Events/DeathCause.cs
namespace HideAndInk.Core.Events
{
    /// <summary>
    /// 플레이어 사망 원인 분류
    /// 각 값은 대응하는 사운드(SfxId), 로그 메시지, 사망 화면 메시지에 매핑
    /// </summary>
    public enum DeathCause
    {
        Unknown = 0,                    // 알 수 없음 (Fallback)

        // ===== 적 =====
        CrabAttack,                     // 일반 몬스터(게) 접촉
        EliteAttack,                    // 정예 몬스터 공격
        SeaUrchinContact,               // 성게 접촉 (현재는 Slow only, 추후 대비)

        // ===== 보스 =====
        GajamiDash,                     // 가자미 돌진 (AmbushGimmick)
        MorayCharge,                    // 곰치 돌진 (RelentlessChaseGimmick)
        SwordfishCharge,                // 청새치 돌진 (SwordfishGimmick / DashChargeGimmick)
        GreatWhiteCharge,               // 백상아리 돌진 (DashChargeGimmick)

        // ===== 환경 =====
        CamouflageObstacleDestroyed,    // 의태 중 오브젝트 파괴로 인한 사망
        Drowning,                       // 낙사 / 맵 이탈
        Poisoned,                       // 독 / 환경 데미지

        // ===== 시스템 =====
        Debug,                          // 디버그 명령어로 사망
    }
}
```

## 1.3 사망 이벤트 확장

### EventTypes.cs 수정

```csharp
// 기존: public struct PlayerDeathEvent { }
// 수정:
public struct PlayerDeathEvent
{
    public DeathCause Cause;          // 사망 원인
    public string SourceName;         // 사망 원인 오브젝트 이름 (디버깅용)
    public Vector3 DeathPosition;     // 사망 위치
    public float GameTime;            // 게임 플레이 시간 (초)

    public PlayerDeathEvent(DeathCause cause, string sourceName = "",
        Vector3 deathPosition = default, float gameTime = 0f)
    {
        Cause = cause;
        SourceName = sourceName;
        DeathPosition = deathPosition;
        GameTime = gameTime;
    }
}
```

## 1.4 사망 로그 기록 시스템

### DeathLogger.cs - 새 파일

```csharp
// Assets/Core/Logging/DeathLogger.cs
namespace HideAndInk.Core.Logging
{
    /// <summary>
    /// 사망 원인 로그 기록 시스템
    /// EventBus를 통해 PlayerDeathEvent를 구독 → 로그 파일 기록
    /// </summary>
    public sealed class DeathLogger : IDisposable
    {
        private readonly IEventBus _eventBus;
        private StreamWriter _logWriter;
        private bool _isInitialized;
        private int _deathCount;

        // 각 사망 원인별 통계
        private Dictionary<DeathCause, int> _deathStats;

        public DeathLogger(IEventBus eventBus) { ... }
        public void Initialize() { ... }
        private void OnPlayerDeath(PlayerDeathEvent evt) { ... }
        public void Dispose() { ... }
    }
}
```

### 로그 파일 형식

```
Logs/{yyyy-MM-dd}/
├── INFO.md
├── WARN.md
├── ERROR.md
├── DEBUG.md
├── FATAL.md
├── MOVE.md                  ← 기존 이동 로그
└── DEATH.md                 ← [신규] 사망 로그
```

### DEATH.md 출력 예시

```markdown
# DEATH Log
---
## 14:23:45
[DEATH] 사망 #3 | 원인: CrabAttack | 발생위치: (12.5, 0.0, -3.2) | 플레이시간: 452.3초

## 14:25:12
[DEATH] 사망 #4 | 원인: GajamiDash | 대상: Gajami_Boss | 발생위치: (45.1, 0.0, 12.8) | 플레이시간: 523.7초

## 통계 (세션 종료 시)
| 사망 원인 | 횟수 |
|-----------|------|
| CrabAttack | 2 |
| GajamiDash | 1 |
| Drowning | 1 |
```

## 1.5 DeathCause → SfxId 매핑

사망 시 사운드 재생을 위한 매핑:

| DeathCause | 재생할 SfxId |
|------------|-------------|
| CrabAttack | GameOver |
| EliteAttack | GameOver |
| GajamiDash | GameOver |
| MorayCharge | GameOver |
| SwordfishCharge | GameOver |
| GreatWhiteCharge | GameOver |
| Drowning | GameOver |
| Poisoned | GameOver |

향후 사망 원인별 효과음 추가 시 SfxId 확장 가능.

## 1.6 PlayerLives 연동

`PlayerLives.TakeDamage()`에서 사망 시 DeathCause를 받도록 수정:

```csharp
// PlayerLives.cs - 기존
public void TakeDamage() { ... }

// → 수정: 사망 원인을 인자로 받음
public void TakeDamage(DeathCause cause = DeathCause.Unknown, string sourceName = "")
{
    // ... (기존 로직)
    if (_currentLives <= 0)
    {
        // 사망 이벤트 발행 (DeathCause 포함)
        var deathEvent = new PlayerDeathEvent(cause, sourceName,
            transform.position, Time.timeSinceLevelLoad);
        _eventBus?.Publish(deathEvent);

        // GameManager → Dead 상태 전환 (기존 로직 유지)
    }
}
```

### 각 Enemy 호출부 수정 예시

```csharp
// NormalEnemyController.cs (게)
_playerLives.TakeDamage(DeathCause.CrabAttack, gameObject.name);

// BossEnemyController.cs
// - AmbushGimmick (가자미) 돌진 시
_playerLives.TakeDamage(DeathCause.GajamiDash, gameObject.name);

// - DashChargeGimmick (청새치/백상아리) 오브젝트 파괴 시
_playerLives.TakeDamage(DeathCause.CamouflageObstacleDestroyed, obj.name);
```

## 1.7 GameManager 구독 추가

```csharp
// GameManager.cs - OnPlayerDeath에서 DeathCause 로깅
private void OnPlayerDeath(PlayerDeathEvent evt)
{
    // 1. 로그 기록
    LogModule.Instance.Log(
        $"사망 #{_deathCount} | 원인: {evt.Cause} | 위치: {evt.DeathPosition} | 시간: {evt.GameTime:F1}초",
        "DEATH");

    // 2. EventBus 발행 (기존)
    _eventBus?.Publish(evt);

    // 3. GameStateMachine 전환 (기존)
    if (_gameStateMachine.CanTransitionTo(GameState.Dead))
        _gameStateMachine.TransitionTo(GameState.Dead);
}
```

## 1.8 파일 구조

```
Assets/Core/
├── Events/
│   ├── DeathCause.cs           ← [신규] 사망 원인 Enum
│   └── EventTypes.cs           ← [수정] PlayerDeathEvent 확장
├── Logging/
│   ├── DeathLogger.cs          ← [신규] 사망 로그 기록 시스템
│   └── LogModule.cs            ← [수정] "DEATH" 태그 지원
└── Managers/
    └── GameManager.cs          ← [수정] 사망 이벤트 구독
```

---

# 2. 오디오매니저 (AudioManager)

## 2.1 현재 상황

| 항목 | 상태 |
|------|------|
| AudioManager.cs | ✅ Singleton MonoBehaviour 존재 (기본 PlayBgm/PlaySfx) |
| SfxId/BgmId/AmbientId Enum | ✅ 정의 완료 |
| ISfxService/IBgmService/IAmbientService | ✅ 인터페이스 정의 완료 |
| DI 등록 | ❌ 주석 처리 |
| 인터페이스 구현 | ❌ AudioManager가 구현 안 함 |
| Resources 폴더 사운드 | ❌ 미배치 |
| 컴포넌트 사운드 주입 | ❌ 미연동 |

## 2.2 구현 전략

**AudioManager.cs 하나로 3개 인터페이스 모두 구현 (권장안 A)**

```
AudioManager : MonoBehaviour, ISfxService, IBgmService, IAmbientService
```

- 기존 Singleton 패턴 유지
- 3개 인터페이스 메서드 구현
- DI 컨테이너에 Singleton으로 등록
- Resources.Load 기반 임시 로딩 (추후 Addressables 전환 가능)

## 2.3 AudioManager 인터페이스 구현 상세

### ISfxService 구현

```csharp
// AudioManager.cs에 추가
public void Play(SfxId id)
{
    Play(id, 1f);
}

public void Play(SfxId id, float volumeScale)
{
    AudioClip clip = LoadSfxClip(id);
    if (clip != null)
    {
        sfxSource.PlayOneShot(clip, volumeScale);
    }
}

public void PlayAtPoint(SfxId id, Vector3 position)
{
    PlayAtPoint(id, position, 1f);
}

public void PlayAtPoint(SfxId id, Vector3 position, float volumeScale)
{
    AudioClip clip = LoadSfxClip(id);
    if (clip != null)
    {
        AudioSource.PlayClipAtPoint(clip, position, volumeScale * _sfxVolume);
    }
}

public bool IsPlaying(SfxId id)
{
    return sfxSource.isPlaying;
}

public void StopAll()
{
    sfxSource.Stop();
}
```

### IBgmService 구현

```csharp
private BgmId _currentBgm = BgmId.None;

public BgmId CurrentBgm => _currentBgm;

public void Play(BgmId id)
{
    Play(id, 0f);
}

public void Play(BgmId id, float fadeDuration)
{
    AudioClip clip = LoadBgmClip(id);
    if (clip == null) return;

    if (fadeDuration > 0f)
    {
        StartCoroutine(FadeBgm(clip, fadeDuration));
    }
    else
    {
        bgmSource.clip = clip;
        bgmSource.Play();
    }
    _currentBgm = id;
}

public void Stop(float fadeDuration = 0)
{
    if (fadeDuration > 0f)
        StartCoroutine(FadeOutBgm(fadeDuration));
    else
        bgmSource.Stop();
    _currentBgm = BgmId.None;
}

public void Pause()
{
    bgmSource.Pause();
}

public void Resume()
{
    bgmSource.UnPause();
}

public void SetVolume(float volume)
{
    BgmVolume = volume;
}
```

### IAmbientService 구현

```csharp
// Ambient도 별도 AudioSource 사용
[SerializeField] private AudioSource ambientSource;
private AmbientId _currentAmbient = AmbientId.None;

public AmbientId CurrentAmbient => _currentAmbient;

public void Play(AmbientId id)
{
    Play(id, 1f);
}

public void Play(AmbientId id, float fadeDuration)
{
    AudioClip clip = LoadAmbientClip(id);
    if (clip == null) return;

    if (ambientSource == null) CreateAmbientSource();

    if (fadeDuration > 0f)
        StartCoroutine(FadeAmbient(clip, fadeDuration));
    else
    {
        ambientSource.clip = clip;
        ambientSource.Play();
    }
    _currentAmbient = id;
}

public void Stop(float fadeDuration = 0)
{
    if (fadeDuration > 0f)
        StartCoroutine(FadeOutAmbient(fadeDuration));
    else
        ambientSource.Stop();
    _currentAmbient = AmbientId.None;
}

public void SetVolume(float volume)
{
    if (ambientSource != null)
        ambientSource.volume = volume;
}
```

### Resources.Load 매핑

```csharp
private AudioClip LoadSfxClip(SfxId id)
{
    string path = $"Audio/SFX/{id}";
    return Resources.Load<AudioClip>(path);
}

private AudioClip LoadBgmClip(BgmId id)
{
    string path = $"Audio/BGM/{id}";
    return Resources.Load<AudioClip>(path);
}

private AudioClip LoadAmbientClip(AmbientId id)
{
    string path = $"Audio/Ambient/{id}";
    return Resources.Load<AudioClip>(path);
}
```

### 페이드 코루틴

```csharp
private IEnumerator FadeBgm(AudioClip newClip, float duration)
{
    // 현재 BGM 페이드 아웃
    float startVolume = bgmSource.volume;
    for (float t = 0; t < duration; t += Time.deltaTime)
    {
        bgmSource.volume = Mathf.Lerp(startVolume, 0f, t / duration);
        yield return null;
    }
    bgmSource.Stop();

    // 새 BGM 재생 + 페이드 인
    bgmSource.clip = newClip;
    bgmSource.Play();
    for (float t = 0; t < duration; t += Time.deltaTime)
    {
        bgmSource.volume = Mathf.Lerp(0f, _bgmVolume, t / duration);
        yield return null;
    }
    bgmSource.volume = _bgmVolume;
}

private IEnumerator FadeOutBgm(float duration)
{
    float startVolume = bgmSource.volume;
    for (float t = 0; t < duration; t += Time.deltaTime)
    {
        bgmSource.volume = Mathf.Lerp(startVolume, 0f, t / duration);
        yield return null;
    }
    bgmSource.Stop();
    bgmSource.volume = _bgmVolume;
}
```

## 2.4 DI 등록 해제

```csharp
// GameManager.RegisterAudioServices() - 주석 해제
private void RegisterAudioServices()
{
    // AudioManager는 MonoBehaviour이므로 Instance를 직접 등록
    var audioManager = AudioManager.Instance;
    _rootContainer.RegisterInstance<ISfxService>(audioManager, ServiceLifetime.Singleton);
    _rootContainer.RegisterInstance<IBgmService>(audioManager, ServiceLifetime.Singleton);
    _rootContainer.RegisterInstance<IAmbientService>(audioManager, ServiceLifetime.Singleton);
}
```

## 2.5 컴포넌트별 사운드 주입 현황

| 컴포넌트 | 주입할 Interface | 사용할 SfxId/BgmId |
|---------|-----------------|-------------------|
| CamouflageAdapter | ISfxService | CamouflageAttach, CamouflagePerfect, CamouflageDetach |
| PlayerMovementAdapter | ISfxService | InkShoot(0~3) 랜덤 |
| PlayerInk | ISfxService | Charge, ChargeLoop |
| ChichiChargeController | ISfxService | Charge |
| SuspicionMeterUI | ISfxService | PredatorDetected |
| EnemyAIController | ISfxService | PredatorChase(0~3), PredatorUnderwaterPass(0~2) |
| PlayerLives | ISfxService | GameOver (사망 시) |
| StageManager (신규) | IBgmService, IAmbientService | 지역별 BGM/Ambient 전환 |
| UI 버튼들 | ISfxService | ButtonClick(0~2) |

## 2.6 Resources 폴더 배치안

```
Assets/Resources/Audio/
├── SFX/
│   ├── ButtonClick.wav
│   ├── ButtonClick1.wav
│   ├── ButtonClick2.wav
│   ├── InkShoot.wav         (먹물발사효과음)
│   ├── InkShoot1.wav
│   ├── CamouflageAttach.wav (의태효과음.mp3)
│   ├── CamouflagePerfect.wav
│   ├── CamouflageDetach.wav
│   ├── Charge.wav
│   ├── ChargeLoop.wav
│   ├── StageClear.wav
│   ├── GameOver.wav
│   ├── PredatorDetected.wav
│   ├── PredatorChase.wav
│   └── ... (나머지)
├── BGM/
│   ├── GrasslandCoast.ogg
│   ├── CoralReef.ogg
│   ├── SeaweedForest.ogg
│   ├── DeepSeaCliff.ogg
│   └── DeepSeaRuins.ogg
└── Ambient/
    ├── GrasslandCoast.wav
    ├── CoralReef.wav
    ├── SeaweedForest.wav
    ├── DeepSeaCliff.wav
    └── DeepSeaRuins.wav
```

> **주의**: Resources.Load는 `/` 경로 구분자를 사용하며,
> 확장자 없이 이름만으로 로드함. 파일명은 enum 값과 일치해야 함.
> 중복 파일 제거(`문어먹물꿈질음악/` 폴더) 후 복사.

## 2.7 AudioManager 최종 파일 구조

```csharp
// Assets/Core/Audio/AudioManager.cs
public sealed class AudioManager : MonoBehaviour,
    ISfxService, IBgmService, IAmbientService
{
    // === 기존 Singleton, 볼륨, 설정 코드 유지 ===

    // === [신규] ISfxService 구현 ===
    public void Play(SfxId id) { ... }
    public void Play(SfxId id, float volumeScale) { ... }
    public void PlayAtPoint(SfxId id, Vector3 position) { ... }
    public void PlayAtPoint(SfxId id, Vector3 position, float volumeScale) { ... }
    public bool IsPlaying(SfxId id) { ... }
    public void StopAll() { ... }

    // === [신규] IBgmService 구현 ===
    public BgmId CurrentBgm { get; private set; }
    public void Play(BgmId id) { ... }
    public void Play(BgmId id, float fadeDuration) { ... }
    public void Stop(float fadeDuration = 0) { ... }
    public void Pause() { ... }
    public void Resume() { ... }
    public void SetVolume(float volume) { ... }

    // === [신규] IAmbientService 구현 ===
    public AmbientId CurrentAmbient { get; private set; }
    public void Play(AmbientId id) { ... }
    public void Play(AmbientId id, float fadeDuration) { ... }
    public void Stop(float fadeDuration = 0) { ... }
    public void SetVolume(float volume) { ... }

    // === [신규] 내부 메서드 ===
    private AudioClip LoadSfxClip(SfxId id) { ... }
    private AudioClip LoadBgmClip(BgmId id) { ... }
    private AudioClip LoadAmbientClip(AmbientId id) { ... }
    private void CreateAmbientSource() { ... }
    private IEnumerator FadeBgm(AudioClip newClip, float duration) { ... }
    private IEnumerator FadeOutBgm(float duration) { ... }
    private IEnumerator FadeAmbient(AudioClip newClip, float duration) { ... }
    private IEnumerator FadeOutAmbient(float duration) { ... }
}
```

## 2.8 파일 정리 (중복 제거)

| 원본 파일 | 대상 경로 |
|----------|----------|
| `문어먹물꿈질음악/버튼효과음0.wav` | `Resources/Audio/SFX/ButtonClick.wav` |
| `문어먹물꿈질음악/먹물발사효과음.wav` | `Resources/Audio/SFX/InkShoot.wav` |
| `문어먹물꿈질음악/초원해안Bgm.mp3` | `Resources/Audio/BGM/GrasslandCoast.ogg` |
| ... (전체 매핑은 AudioSystem_Design.md 6장 참조) | |

---

# 3. 스토리매니저 (StoryManager)

## 3.1 개요

게임의 스토리 진행을 관리하는 코어 시스템.
스테이지/Zone 전환 시 스토리 컷씬, 대사, 연출을 트리거하고,
스토리 진행 상태를 저장/불러오기 가능하게 함.

## 3.2 게임 스토리 흐름도

```
[타이틀 화면]
    ↓ New Game / Continue
[오프닝 스토리]                ← 챕터 0: 도입부
    ↓
Zone 0 (스테이지 1: 초원해안)   ← 챕터 1
    ↓ ZoneChanger
Zone 1 (스테이지 2: 산호초)     ← 챕터 2
    ↓ ZoneChanger
Zone 2 (스테이지 3: 해초숲)     ← 챕터 3
    ↓ ZoneChanger
Zone 3 (스테이지 4: 심해절벽)   ← 챕터 4
    ↓ ZoneChanger
Zone 4 (스테이지 5: 심해페허)   ← 챕터 5 (보스)
    ↓
[엔딩 스토리]
```

## 3.3 핵심 인터페이스

### IStoryManager

```csharp
// Assets/Core/Interfaces/IStoryManager.cs
namespace HideAndInk.Core.Interfaces
{
    /// <summary>
    /// 스토리 매니저 인터페이스
    /// 스토리 진행, 대사, 컷씬을 관리
    /// </summary>
    public interface IStoryManager
    {
        /// <summary>현재 챕터 인덱스 (0부터 시작)</summary>
        int CurrentChapter { get; }

        /// <summary>현재 진행 중인 StorySequence ID (없으면 -1)</summary>
        int CurrentSequenceId { get; }

        /// <summary>특정 챕터의 스토리 완료 여부</summary>
        bool IsChapterCompleted(int chapterIndex);

        /// <summary>스토리 데이터 로드 (저장된 진행도 복원)</summary>
        void LoadStoryProgress(StorySaveData data);

        /// <summary>현재 스토리 진행도 저장</summary>
        StorySaveData GetStoryProgress();

        /// <summary>스토리 시퀀스 시작 (시퀀스 ID 기준)</summary>
        void StartSequence(int sequenceId);

        /// <summary>특정 챕터 시작 시 스토리 트리거</summary>
        void OnChapterStart(int chapterIndex);

        /// <summary>특정 이벤트 발생 시 스토리 체크</summary>
        void OnGameEvent(string eventName);

        // === 이벤트 ===
        event Action<StorySequence> OnSequenceStarted;
        event Action<StorySequence> OnSequenceCompleted;
        event Action<int> OnChapterChanged;
    }
}
```

## 3.4 스토리 데이터 모델

### StorySequence

```csharp
// Assets/Core/Interfaces/StoryTypes.cs
namespace HideAndInk.Core.Interfaces
{
    /// <summary>
    /// 스토리 시퀀스 - 하나의 스토리 이벤트 단위
    /// (예: Zone 진입 시 대사, 보스전 전 컷씬, 엔딩 등)
    /// </summary>
    [Serializable]
    public struct StorySequence
    {
        public int Id;                  // 시퀀스 고유 ID
        public int ChapterIndex;        // 소속 챕터
        public string SequenceName;     // 시퀀스 이름 (디버깅용)
        public StoryTriggerType TriggerType;  // 트리거 조건
        public string TriggerParam;     // 트리거 파라미터 (Zone 번호 등)

        public bool IsCutscene;         // 컷씬 여부
        public bool IsDialogue;         // 대사 여부
        public StoryDialogueLine[] Dialogues;  // 대사 배열
        public bool IsSkippable;        // 스킵 가능 여부

        // 완료 후 동작
        public string OnCompleteEvent;  // 완료 시 발행할 이벤트 이름
        public bool MarkChapterComplete; // 챕터 완료 표시
    }

    /// <summary>
    /// 스토리 트리거 조건
    /// </summary>
    public enum StoryTriggerType
    {
        Manual,             // 수동 호출
        OnChapterStart,     // 챕터 시작 시 자동
        OnZoneEnter,        // 특정 Zone 입장 시 (TriggerParam = Zone 번호)
        OnBossDefeated,     // 보스 처치 시
        OnPlayerDeath,      // 첫 사망 시
        OnItemCollected,    // 아이템 획득 시
        OnGameEvent,        // 게임 이벤트 발생 시
    }

    /// <summary>
    /// 스토리 대사 한 줄
    /// </summary>
    [Serializable]
    public struct StoryDialogueLine
    {
        public string SpeakerName;      // 화자 이름
        public string DialogueText;     // 대사 내용
        public float DisplayDuration;   // 표시 시간 (0 = 수동 진행)
        public string Emotion;          // 감정 표현 (happy, sad, angry...)
    }
}

/// <summary>
/// 스토리 진행도 저장 데이터
/// </summary>
[Serializable]
public struct StorySaveData
{
    public int CurrentChapter;             // 현재 챕터
    public int LastCompletedSequenceId;    // 마지막 완료 시퀀스
    public List<int> CompletedSequences;   // 완료된 시퀀스 ID 목록
    public Dictionary<string, bool> StoryFlags;   // 스토리 플래그
}
```

## 3.5 StoryManager 구현

```csharp
// Assets/Core/Managers/StoryManager.cs
namespace HideAndInk.Core.Managers
{
    /// <summary>
    /// 스토리 매니저 구현체
    /// 순수 C#. DI 컨테이너에 Singleton으로 등록.
    /// </summary>
    public sealed class StoryManager : IStoryManager
    {
        private readonly IEventBus _eventBus;
        private readonly IGameStateMachine _gameStateMachine;

        private int _currentChapter;
        private int _currentSequenceId = -1;
        private HashSet<int> _completedSequences;
        private Dictionary<string, bool> _storyFlags;

        // 시퀀스 데이터 (하드코딩 또는 ScriptableObject에서 로드)
        private Dictionary<int, StorySequence> _sequenceDatabase;

        public int CurrentChapter => _currentChapter;
        public int CurrentSequenceId => _currentSequenceId;

        public event Action<StorySequence> OnSequenceStarted;
        public event Action<StorySequence> OnSequenceCompleted;
        public event Action<int> OnChapterChanged;

        // 생성자 (DI)
        public StoryManager(IEventBus eventBus, IGameStateMachine stateMachine)
        {
            _eventBus = eventBus;
            _gameStateMachine = stateMachine;
            _completedSequences = new HashSet<int>();
            _storyFlags = new Dictionary<string, bool>();
            InitializeSequenceDatabase();
            SubscribeToEvents();
        }

        /// <summary>
        /// 시퀀스 데이터베이스 초기화
        /// </summary>
        private void InitializeSequenceDatabase()
        {
            _sequenceDatabase = new Dictionary<int, StorySequence>();

            // 챕터 0: 오프닝
            RegisterSequence(new StorySequence
            {
                Id = 0,
                ChapterIndex = 0,
                SequenceName = "Opening",
                TriggerType = StoryTriggerType.OnChapterStart,
                Dialogues = new[]
                {
                    new StoryDialogueLine { SpeakerName = "...", DialogueText = "어디지... 여기는...", DisplayDuration = 2f },
                    new StoryDialogueLine { SpeakerName = "치치", DialogueText = "크...잉?", DisplayDuration = 1.5f },
                },
                IsDialogue = true,
            });

            // 챕터 1: 초원해안
            RegisterSequence(new StorySequence
            {
                Id = 10,
                ChapterIndex = 1,
                SequenceName = "GrasslandCoast_Enter",
                TriggerType = StoryTriggerType.OnZoneEnter,
                TriggerParam = "0",
                Dialogues = new[]
                {
                    new StoryDialogueLine { SpeakerName = "주인공", DialogueText = "여긴... 초원해안?", DisplayDuration = 2f },
                },
                IsDialogue = true,
            });

            // ... (나머지 시퀀스 정의)
        }

        private void RegisterSequence(StorySequence seq)
        {
            _sequenceDatabase[seq.Id] = seq;
        }

        /// <summary>
        /// Zone 입장 이벤트 구독
        /// </summary>
        private void SubscribeToEvents()
        {
            _eventBus.Subscribe<ZoneChangedEvent>(OnZoneChanged);
            _eventBus.Subscribe<PlayerDeathEvent>(OnPlayerDeath);
        }

        /// <summary>
        /// Zone 변경 시 스토리 체크
        /// </summary>
        private void OnZoneChanged(ZoneChangedEvent evt)
        {
            // Zone 번호에 해당하는 시퀀스 검색
            foreach (var seq in _sequenceDatabase.Values)
            {
                if (seq.TriggerType == StoryTriggerType.OnZoneEnter &&
                    seq.TriggerParam == evt.ZoneIndex.ToString() &&
                    !_completedSequences.Contains(seq.Id))
                {
                    StartSequence(seq.Id);
                    break;
                }
            }
        }

        public void StartSequence(int sequenceId)
        {
            if (!_sequenceDatabase.TryGetValue(sequenceId, out var seq)) return;
            if (_completedSequences.Contains(sequenceId)) return; // 중복 방지

            _currentSequenceId = sequenceId;

            // 게임 일시 정지 (컷씬/대사 중)
            if (seq.IsCutscene || seq.IsDialogue)
            {
                _gameStateMachine.TransitionTo(GameState.Paused);
            }

            OnSequenceStarted?.Invoke(seq);
        }

        /// <summary>
        /// 시퀀스 완료 처리
        /// </summary>
        public void CompleteCurrentSequence()
        {
            if (_currentSequenceId < 0) return;

            var seq = _sequenceDatabase[_currentSequenceId];
            _completedSequences.Add(_currentSequenceId);

            // 챕터 완료 표시
            if (seq.MarkChapterComplete)
            {
                _currentChapter = seq.ChapterIndex + 1;
                OnChapterChanged?.Invoke(_currentChapter);
            }

            // 게임 재개
            if (seq.IsCutscene || seq.IsDialogue)
            {
                _gameStateMachine.TransitionTo(GameState.Playing);
            }

            OnSequenceCompleted?.Invoke(seq);
            _currentSequenceId = -1;
        }

        public bool IsChapterCompleted(int chapterIndex)
        {
            // 해당 챕터의 모든 시퀀스가 완료되었는지 확인
            foreach (var seq in _sequenceDatabase.Values)
            {
                if (seq.ChapterIndex == chapterIndex &&
                    seq.MarkChapterComplete &&
                    !_completedSequences.Contains(seq.Id))
                {
                    return false;
                }
            }
            return true;
        }

        public void OnChapterStart(int chapterIndex)
        {
            _currentChapter = chapterIndex;

            // 자동 트리거 시퀀스 실행
            foreach (var seq in _sequenceDatabase.Values)
            {
                if (seq.TriggerType == StoryTriggerType.OnChapterStart &&
                    seq.ChapterIndex == chapterIndex &&
                    !_completedSequences.Contains(seq.Id))
                {
                    StartSequence(seq.Id);
                    break;
                }
            }
        }

        public void LoadStoryProgress(StorySaveData data)
        {
            _currentChapter = data.CurrentChapter;
            _completedSequences = new HashSet<int>(data.CompletedSequences);
            _storyFlags = new Dictionary<string, bool>(data.StoryFlags);
        }

        public StorySaveData GetStoryProgress()
        {
            return new StorySaveData
            {
                CurrentChapter = _currentChapter,
                LastCompletedSequenceId = _completedSequences.Count > 0
                    ? _completedSequences.Max() : -1,
                CompletedSequences = _completedSequences.ToList(),
                StoryFlags = new Dictionary<string, bool>(_storyFlags),
            };
        }

        public void OnGameEvent(string eventName)
        {
            // 게임 이벤트 기반 시퀀스 트리거
            foreach (var seq in _sequenceDatabase.Values)
            {
                if (seq.TriggerType == StoryTriggerType.OnGameEvent &&
                    seq.TriggerParam == eventName &&
                    !_completedSequences.Contains(seq.Id))
                {
                    StartSequence(seq.Id);
                    break;
                }
            }
        }
    }
}
```

## 3.6 ZoneChangedEvent 추가

```csharp
// EventTypes.cs에 추가
/// <summary>Zone 전환 시 발생 (ZoneChanger에서 발행)</summary>
public struct ZoneChangedEvent
{
    public int ZoneIndex;
    public int FromZoneIndex;

    public ZoneChangedEvent(int from, int to)
    {
        FromZoneIndex = from;
        ZoneIndex = to;
    }
}
```

## 3.7 StoryUI (대사 UI)

```csharp
// Assets/Scripts/UI/StoryUI.cs
namespace HideAndInk.UI
{
    /// <summary>
    /// 스토리 대사 표시 UI (Canvas)
    /// IStoryManager.OnSequenceStarted를 구독하여 대사 표시
    /// </summary>
    public class StoryUI : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private GameObject dialoguePanel;
        [SerializeField] private Text speakerNameText;
        [SerializeField] private Text dialogueText;
        [SerializeField] private Image speakerPortrait;
        [SerializeField] private Button skipButton;
        [SerializeField] private Button nextButton;

        [Header("Typing Effect")]
        [SerializeField] private float typingSpeed = 0.05f;

        private IStoryManager _storyManager;
        private StorySequence _currentSequence;
        private int _currentLineIndex;
        private Coroutine _typingCoroutine;
        private bool _isTyping;

        private void Start()
        {
            if (GameManager.Container.IsRegistered<IStoryManager>())
            {
                _storyManager = GameManager.Container.Resolve<IStoryManager>();
                _storyManager.OnSequenceStarted += OnSequenceStarted;
                _storyManager.OnSequenceCompleted += OnSequenceCompleted;
            }
        }

        private void OnSequenceStarted(StorySequence seq)
        {
            if (!seq.IsDialogue) return;

            _currentSequence = seq;
            _currentLineIndex = 0;
            dialoguePanel.SetActive(true);
            ShowDialogueLine(seq.Dialogues[0]);
        }

        private void ShowDialogueLine(StoryDialogueLine line)
        {
            speakerNameText.text = line.SpeakerName;
            dialogueText.text = "";

            if (_typingCoroutine != null)
                StopCoroutine(_typingCoroutine);

            _typingCoroutine = StartCoroutine(TypeText(line.DialogueText));
        }

        private IEnumerator TypeText(string text)
        {
            _isTyping = true;
            foreach (char c in text)
            {
                dialogueText.text += c;
                yield return new WaitForSeconds(typingSpeed);
            }
            _isTyping = false;
        }

        public void OnNextClicked()
        {
            if (_isTyping)
            {
                // 타이핑 중이면 즉시 완료
                StopCoroutine(_typingCoroutine);
                dialogueText.text = _currentSequence.Dialogues[_currentLineIndex].DialogueText;
                _isTyping = false;
                return;
            }

            _currentLineIndex++;
            if (_currentLineIndex < _currentSequence.Dialogues.Length)
            {
                ShowDialogueLine(_currentSequence.Dialogues[_currentLineIndex]);
            }
            else
            {
                // 대사 종료 → 시퀀스 완료
                _storyManager.CompleteCurrentSequence();
            }
        }

        public void OnSkipClicked()
        {
            if (_currentSequence.IsSkippable)
            {
                _storyManager.CompleteCurrentSequence();
            }
        }

        private void OnSequenceCompleted(StorySequence seq)
        {
            dialoguePanel.SetActive(false);
        }
    }
}
```

## 3.8 SaveData 확장

```csharp
// SaveData.cs에 스토리 진행도 추가
[Serializable]
public class SaveData
{
    // ... (기존 필드)

    // [신규] 스토리 진행도
    public int storyChapter;
    public List<int> completedSequences;
    public List<string> storyFlagKeys;
    public List<string> storyFlagValues;
}
```

## 3.9 StageManager (Zone 사운드 연동)

스토리매니저 + 오디오매니저를 함께 사용하는 StageManager 제안:

```csharp
// Assets/Core/Managers/StageManager.cs (선택사항)
namespace HideAndInk.Core.Managers
{
    /// <summary>
    /// 스테이지(Zone) 진입 시 BGM/Ambient 전환 + 스토리 트리거
    /// ZoneChanger의 onZoneChanged 이벤트에 연결
    /// </summary>
    public class StageManager : MonoBehaviour
    {
        [Header("Zone → BGM/Ambient 매핑")]
        [SerializeField] private StageAudioMapping[] stageMappings;

        private IStoryManager _storyManager;
        private IBgmService _bgmService;
        private IAmbientService _ambientService;
        private int _currentZone = -1;

        [System.Serializable]
        private struct StageAudioMapping
        {
            public int ZoneIndex;
            public BgmId Bgm;
            public AmbientId Ambient;
            public int ChapterIndex;
        }

        private void Start()
        {
            // DI 서비스 해결
            var container = GameManager.Container;
            _storyManager = container.Resolve<IStoryManager>();
            _bgmService = container.Resolve<IBgmService>();
            _ambientService = container.Resolve<IAmbientService>();

            // ZoneChangedEvent 구독
            var eventBus = container.Resolve<IEventBus>();
            eventBus.Subscribe<ZoneChangedEvent>(OnZoneChanged);
        }

        private void OnZoneChanged(ZoneChangedEvent evt)
        {
            _currentZone = evt.ZoneIndex;

            // 해당 Zone의 오디오 매핑 찾기
            foreach (var mapping in stageMappings)
            {
                if (mapping.ZoneIndex == evt.ZoneIndex)
                {
                    // BGM 전환 (페이드 1초)
                    _bgmService.Play(mapping.Bgm, 1f);
                    _ambientService.Play(mapping.Ambient, 1f);

                    // 스토리 챕터 진행
                    _storyManager.OnChapterStart(mapping.ChapterIndex);
                    break;
                }
            }
        }
    }
}
```

## 3.10 파일 구조

```
Assets/Core/
├── Interfaces/
│   ├── IStoryManager.cs          ← [신규] 스토리 매니저 인터페이스
│   └── StoryTypes.cs             ← [신규] 스토리 데이터 타입
├── Events/
│   └── EventTypes.cs             ← [수정] ZoneChangedEvent 추가
├── Managers/
│   ├── StoryManager.cs           ← [신규] 스토리 매니저 구현
│   └── StageManager.cs           ← [선택] 스테이지 오디오 연동 매니저
├── Audio/
│   └── AudioManager.cs           ← [수정] 3개 인터페이스 구현
└── Logging/
    └── DeathLogger.cs            ← [신규] 사망 로그

Assets/Scripts/
├── UI/
│   └── StoryUI.cs                ← [신규] 스토리 대사 UI
└── Save/
    └── SaveData.cs               ← [수정] 스토리 진행도 포함
```

---

## 4. 통합 DI 등록

```csharp
// GameManager.RegisterCoreServices()에 추가
private void RegisterCoreServices()
{
    // ... (기존 등록)

    // [신규] 오디오 서비스 등록
    RegisterAudioServices();

    // [신규] 스토리 매니저 (Singleton)
    var storyManager = new StoryManager(
        _rootContainer.Resolve<IEventBus>(),
        _rootContainer.Resolve<IGameStateMachine>()
    );
    _rootContainer.RegisterInstance<IStoryManager>(storyManager, ServiceLifetime.Singleton);
}
```

---

## 5. 구현 우선순위

| 순위 | 시스템 | 세부 작업 | 예상 난이도 |
|-----|--------|---------|-----------|
| 1 | **AudioManager** | 인터페이스 구현 + DI 등록 | ★★☆ |
| 2 | **AudioManager** | Resources 폴더 배치 + 사운드 파일 복사 | ★☆☆ |
| 3 | **AudioManager** | 각 컴포넌트 ISfxService 주입 | ★★☆ |
| 4 | **Death Logger** | DeathCause enum + PlayerDeathEvent 확장 | ★☆☆ |
| 5 | **Death Logger** | GameManager 연동 + 로그 기록 | ★☆☆ |
| 6 | **Death Logger** | 각 Enemy 호출부 DeathCause 전달 | ★☆☆ |
| 7 | **StoryManager** | 데이터 모델 + 인터페이스 + 구현체 | ★★★ |
| 8 | **StoryManager** | StoryUI (대사 창) | ★★☆ |
| 9 | **StoryManager** | StageManager (오디오 연동) | ★☆☆ |
| 10 | **StoryManager** | SaveData 확장 | ★☆☆ |

---

## 6. 핵심 아키텍처 결정사항

| 항목 | 결정 | 이유 |
|------|------|------|
| AudioManager 인터페이스 구현 | 단일 클래스 3개 구현 | 기존 코드 재활용, 빠른 연동 |
| 사운드 로딩 방식 | Resources.Load (임시) | 간편함. 추후 Addressables 전환 가능 |
| DeathCause 전달 | PlayerLives.TakeDamage() 파라미터 추가 | 최소한의 변경으로 모든 사망 포착 |
| 스토리 데이터 | 하드코딩 → 추후 ScriptableObject | Phase 1은 빠른 구현, Phase 2에 데이터 분리 |
| StoryManager 생명주기 | Singleton (DI) | 전역 상태 관리 |
| 스토리 UI | 별도 MonoBehaviour (Canvas) | 디자이너가 Inspector 편집 가능 |

---

*이 문서는 AI(Orchestrator)가 프로젝트 분석을 바탕으로 작성했습니다.*
*실제 구현 전 사용자 승인이 필요합니다.*
