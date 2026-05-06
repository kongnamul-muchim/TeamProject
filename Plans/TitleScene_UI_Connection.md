# TitleScene UI 연결 문서

> 마지막 업데이트: 2026-04-30

---

## 🔗 연결 현황

### TitleController (Canvas에 부착)

| 버튼 | 연결된 메서드 | 상태 |
|------|-------------|------|
| Btn_NewGame | `TitleController.OnNewGameClicked()` | ✅ 연결됨 |
| Btn_Continue | `TitleController.OnContinueClicked()` | ✅ 연결됨 |
| Btn_ExitGame | `TitleController.OnExitGameClicked()` | ✅ 연결됨 |
| Btn_Setting | `TitleController.OnSettingClicked()` | ✅ 연결됨 |
| Btn_ExitPopup | `TitleController.OnExitPopupClicked()` | ✅ 연결됨 |

### TitleController Inspector 참조

| 필드 | 대상 | 상태 |
|------|------|------|
| popupSetting | Popup_Setting | ✅ 설정됨 (Scene 파일에 하드코딩) |
| btnContinue | Btn_Continue (Button) | ⚠️ Inspector에서 수동 할당 필요 |
| btnContinueText | Btn_Continue > Text (TMP) | ⚠️ Inspector에서 수동 할당 필요 |
| fadeInObj | FadeInObj 인스턴스 (선택) | ❌ 필요시 할당 |

### SettingsPopup (Popup_Setting에 부착)

| 컨트롤 | 연결된 메서드 | 상태 |
|--------|-------------|------|
| BGMToggle | `SettingsPopup.SetBgmMute(bool)` | ✅ 연결됨 |
| BGM_Slider | `SettingsPopup.SetBgmVolume(float)` | ✅ 연결됨 |
| FXToggle | `SettingsPopup.SetSfxMute(bool)` | ✅ 연결됨 |
| FX_Slider | `SettingsPopup.SetSfxVolume(float)` | ✅ 연결됨 |

### 진입 트랜지션 (TitleController.Start → 코루틴)
- **`useEntryTransition` (권장)**: PatternTransitionController.PlayOut() — 셰이더 기반
  - 씬 로드 직후 `SetFull()`로 화면을 덮고 → `PlayOut()`으로 걷기
  - 별도 프리팹 필요 없음, Shader_PatternTransition이 씬에 있으면 자동 동작
- **`fadeInObj`**: FadeInObj 프리팹 인스턴스 할당 시 Animator 기반 스프라이트 효과
  - TitleController가 자동으로 활성화 → 애니메이션 재생 → 완료 후 비활성화
  - `Assets/Prefabs/FadeInObj.prefab`을 씬의 Canvas 자식으로 넣고 슬롯에 할당
- 둘 다 true면 순차 재생 (PatternTransition → FadeInObj)

### 씬 전환
- `useSceneTransition` 토글로 PatternTransitionController 사용 여부 선택 가능
- `newGameSceneIndex`로 로드할 씬의 Build Index 지정 가능 (기본값: 1)

---

## 💾 저장 시스템 구성

### 파일 구조
```
Assets/Scripts/Save/
├── SaveData.cs                 ← 저장 데이터 클래스 (Zone 번호 + Waypoint)
├── SaveManager.cs              ← JSON 저장/불러오기/삭제
├── ZoneSaveHandler.cs          ← ZoneChanger에 부착, 구역 전환 시 자동 저장
└── ContinueZoneHandler.cs      ← 게임 씬에 부착, 이어하기 시 Zone 활성화
```

### SaveManager
- 저장 경로: `Application.persistentDataPath/Saves/save.json`
- `Save(zoneIndex)` — 특정 Zone 번호 저장
- `Load()` — 저장 데이터 불러오기
- `HasSaveData()` — 저장 파일 존재 여부

### 저장 타이밍
- ZoneChanger가 구역 전환 완료 시 `onZoneChanged` 이벤트 발생
- ZoneSaveHandler가 이벤트 받아서 `SaveManager.Save(toZoneNumber)` 호출

### 이어하기 흐름
```
1. TitleController.Start() → SaveManager.HasSaveData() 체크
2. 없으면 → Btn_Continue 비활성화 (interactable=false, 텍스트 회색)
3. Btn_Continue 클릭 → 저장 데이터 로드 → PendingZoneIndex 설정
4. PatternTransition → Test_Jieun 씬 로드
5. ContinueZoneHandler.Start() → PendingZoneIndex 확인
6. 해당 Zone 활성화 + 플레이어를 Zone 시작점으로 이동
```

---

## 📋 수동 설정 필요 사항 (Inspector)

### TitleScene
1. Canvas 선택 → TitleController 컴포넌트 확인
2. `btnContinue` 슬롯에 `Btn_Continue` (Button) 드래그
3. `btnContinueText` 슬롯에 `Btn_Continue > Text (TMP)` 드래그

### 게임 씬 (Test_Jieun)
1. 아무 GameObject에 `ContinueZoneHandler` 컴포넌트 추가
2. (선택) `zoneContainer`에 Zone 오브젝트들의 부모 Transform 할당
3. 각 ZoneChanger에 `ZoneSaveHandler` 컴포넌트 추가
