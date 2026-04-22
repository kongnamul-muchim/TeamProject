# 📄 작업 완료 보고서: 2D 팝업북 전용 수중 커스텀 쉐이더
- **작업일자**: 2026-04-17
- **담당자**: AI 어시스턴트 (Antigravity) 
- **작업 목표**: "Hide & Ink" 프로젝트의 바닷속 분위기 연출용 URP 커스텀 쉐이더 구축

## 1. 주요 수행 내역 (Action Taken)
- **URP 환경 오류 해결**:
  * URP 핵심 패키지(`com.unity.render-pipelines.universal`) 누락 설치 및 에러 복구.
  * `HideAndInk.asmdef`에 URP 렌더링 의존성(Reference)을 주입하여 에러 해결.
  * 깃허브 원본 `Underwater.cs` 코드의 구버전 문법(`RenderTargetHandle` 등)을 유니티 6000(URP 17+)용 신형 `RTHandle` 코드로 리팩토링 및 정상 컴파일 확인.
- **`PopupBookObject.shader` 커스텀 쉐이더 작성**:
  * 투명 컷아웃(Alpha Test) 기반의 양면 렌더링 + 팝업북 형태용 `ShadowCaster` 구현.
  * ZWrite 처리를 통해 원경 포그(Fog) 시스템과 유령 현상(Overdraw 충돌) 없이 동기화.
  * 오브젝트 상단부(UV.y 기준)에만 적용되는 찰랑이는 수중 윤슬(Caustics) 이펙트 알고리즘 추가.

## 2. 향후 과제 및 참고사항 (Notes)
- 본 쉐이더에 대한 세부 설정값 조절 가이드는 [walkthrough.md](file:///C:/Users/admin/.gemini/antigravity/brain/d701a585-0c52-41df-a5d9-71826dad0a92/walkthrough.md) 아티팩트에 기록해두었으므로, 아티스트 팀원(박미초 님)은 이를 참고하여 룩뎁(Look-Dev)을 진행하시기 바랍니다.
- 쉐이더 작업 중 발견된 URP 버전업 코드 파편화 문제들은 코드상에서 처리되었으며, 추후 URP 관련 추가 작업 시 `RenderingUtils` API 참조를 숙지할 것을 권장합니다.
