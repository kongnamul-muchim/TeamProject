# TitleScene UI 연결 문서

> 마지막 업데이트: 2026-04-30

---

## 🔗 연결 현황

### TitleController (Canvas에 부착)

| 버튼 | 연결된 메서드 | 상태 |
|------|-------------|------|
| Btn_NewGame | `TitleController.OnNewGameClicked()` | ✅ 연결됨 |
| Btn_Continue | *저장/로드 시스템 완성 시 연결 필요* | ⏸️ 보류 |
| Btn_ExitGame | `TitleController.OnExitGameClicked()` | ✅ 연결됨 |
| Btn_Setting | `TitleController.OnSettingClicked()` | ✅ 연결됨 |
| Btn_ExitPopup | `TitleController.OnExitPopupClicked()` | ✅ 연결됨 |

### SettingsPopup (Popup_Setting에 부착)

| 컨트롤 | 연결된 메서드 | 상태 |
|--------|-------------|------|
| BGMToggle | `SettingsPopup.SetBgmMute(bool)` | ✅ 연결됨 |
| BGM_Slider | `SettingsPopup.SetBgmVolume(float)` | ✅ 연결됨 |
| FXToggle | `SettingsPopup.SetSfxMute(bool)` | ✅ 연결됨 |
| FX_Slider | `SettingsPopup.SetSfxVolume(float)` | ✅ 연결됨 |

### FadeInObj
- TitleController에 `fadeInObj` 필드가 있음
- `useEntryFadeIn` 토글로 on/off 가능
- 사용하려면 `Assets/Prefabs/FadeInObj.prefab`을 씬의 Canvas 자식으로 인스턴스화한 후
  TitleController의 `fadeInObj` 슬롯에 할당

### 씬 전환
- `useSceneTransition` 토글로 PatternTransitionController 사용 여부 선택 가능
- `newGameSceneIndex`로 로드할 씬의 Build Index 지정 가능 (기본값: 1)

---

## ⏸️ Btn_Continue (이어하기) — 향후 작업

**목적:** 저장된 게임 데이터를 불러와 마지막 Zone의 시작점에서 게임 재개

**현재 상태:** UI는 배치되어 있으나 onClick 연결되지 않음 (의도적)

**향후 연결 시 참고사항:**
1. `Test_Jieun` 씬에 Zone 시스템(`ZoneChanger`)이 구현되어 있음
2. 저장 데이터에는 마지막으로 활성화된 Zone 번호를 저장
3. Btn_Continue 클릭 시:
   - 저장 데이터 로드
   - `SceneManager.LoadScene("Test_Jieun")` 
   - 저장된 Zone 번호로 해당 Zone 활성화
4. 연결할 메서드 예시: `TitleController.OnContinueClicked()`
