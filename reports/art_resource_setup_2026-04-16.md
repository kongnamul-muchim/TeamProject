# 아트 리소스 초기 세팅 및 구조화 보고서

> 작성일: 2026-04-16
> 작성자: 박미초 (리드 아티스트)

---

## 📋 작업 요약
프로젝트의 시각적 기틀을 마련하고, 효율적인 협업을 위해 `Assets/Art` 폴더 구조화 및 공식 리소스 세팅을 완료함.

---

## 📁 폴더 및 리소스 구조 (`Assets/Art/`)

### 1. 네이밍 컨벤션 (Naming Convention)
팀원 간 혼선을 방지하기 위해 다음과 같은 접두사 규칙을 적용함:
- `Spr_`: 일반 스프라이트 (Character, Sidekick 등)
- `Mask_`: 셰이더용 마스킹 이미지 (GreyScale)
- `Tex_`: 환경/바닥용 텍스처 (Seamless)

### 2. 세부 작업 내역
| 카테고리 | 경로 | 설정 상세 |
| :--- | :--- | :--- |
| **Player (두두)** | `1_Characters/Player` | 512x512, Mesh Type: Tight 적용 |
| **Sidekick (치치)** | `1_Characters/Sidekick` | 512x512, Mesh Type: Tight 적용 |
| **Tiles (종이 질감)** | `2_Environments/Tiles` | 512x512, Wrap Mode: Repeat (Seamless) |
| **Masking** | `1_Characters/Player` | 셰이더 의태용 그레이스케일 마스크 이미지 추가 |

---

## ⚙️ 최적화 및 시스템 세팅

### 1. 스프라이트 아틀라스 (Sprite Atlas) 제작
- **위치:** `Assets/Art/Atlas_Player.spriteatlasv2`
- **대상:** 플레이어 및 사이드킥의 모든 스프라이트
- **목적:** 드로우 콜(Draw Call) 최적화 및 메모리 관리 효율 증대

### 2. 테스트용 씬 생성
- **위치:** `Assets/Scenes/Test_Micho.unity`
- **목적:** 비주얼 디렉팅 및 룩뎁(Look-Dev) 확인을 위한 아티스트 전용 테스트 공간 확보

---

## 🗣️ 협업 참고 사항
- 모든 신규 리소스는 `README.md` 가이드라인에 따라 지정된 폴더에 추가 후 GitHub Push로 공유 예정.
- 기획안 및 마일스톤 문서는 `plans/` 폴더에 최신본 버전 동기화 완료.

---
*End of Report*
