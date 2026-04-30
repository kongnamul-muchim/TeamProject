# siyeon1 두두 + 치치 X키 충전 설명서

이 설명서는 develop에 이미 있는 두두 로직을 그대로 쓰고, 치치가 X키로 두두에게 다가가 잉크를 충전하는 테스트만 설명합니다.

중요한 약속입니다.

- 두두 로직은 새로 만들지 않습니다.
- `PlayerInk`, `CamouflageAdapter`, `PlayerMovementAdapter`는 develop 기존 스크립트를 씁니다.
- 치치는 `Assets/Scripts/siyeon1/` 안의 스크립트를 씁니다.
- `GameObject.Find()`를 쓰지 않습니다.
- Tag 이름으로 찾지 않습니다.
- 모든 연결은 Inspector에 직접 드래그해서 넣습니다.
- X키를 눌러야 충전을 시작합니다.
- 치치가 두두에게 닿아야 충전 게이지가 찹니다.
- 두두가 의태 중이면 충전되지 않습니다.
- 두두가 도망 중이면 충전되지 않습니다.

---

## 1. 씬 준비

1. Unity를 켭니다.
2. `Assets/Scenes/Test_siyeon.unity`를 엽니다.
3. Hierarchy에 `Player` 오브젝트가 있는지 확인합니다.
4. `Player`가 두두입니다.

---

## 2. 두두 Player 확인

`Player` 오브젝트에 아래 스크립트가 있어야 합니다.

- `PlayerInk`
- `PlayerMovementAdapter`
- `CamouflageAdapter`
- `BoxCollider`

중복으로 붙은 스크립트가 있으면 하나만 남깁니다.

예시입니다.

- `PlayerMovementAdapter`가 2개면 1개 삭제합니다.
- `PlayerInk`는 1개만 둡니다.
- `CamouflageAdapter`는 1개만 둡니다.

`BoxCollider`가 없으면 붙입니다.

- `BoxCollider`를 붙입니다.
- 두두 그림 크기에 맞게 Collider 크기를 조절합니다.
- 이 Collider는 치치가 두두에게 진짜 닿았는지 확인할 때 씁니다.

---

## 3. 두두 충전 어댑터 붙이기

치치는 develop 두두 스크립트를 직접 알면 안 됩니다. 그래서 중간 연결 부품인 `DuduDevelopChargeAdapter`를 Player에 붙입니다.

1. Hierarchy에서 `Player`를 선택합니다.
2. Inspector에서 `Add Component`를 누릅니다.
3. `DuduDevelopChargeAdapter`를 검색해서 추가합니다.

`DuduDevelopChargeAdapter`에서 연결합니다.

- `Player Ink`: 같은 `Player`의 `PlayerInk`를 넣습니다.
- `Camouflage State Provider`: 같은 `Player`의 `CamouflageAdapter`를 넣습니다.
- `Block Passive Recharge`: 체크합니다.
- `Is Fleeing`: 처음에는 체크하지 않습니다.
- `Is Dead`: 처음에는 체크하지 않습니다.

뜻은 이렇습니다.

- `Player Ink`: 두두의 현재 잉크를 읽고 충전해 주는 칸입니다.
- `Camouflage State Provider`: 두두가 의태 중인지 확인하는 칸입니다.
- `Block Passive Recharge`: X키 없이 두두 잉크가 자연 회복되는 것을 막는 칸입니다.
- `Is Fleeing`: 두두가 도망 중인지 테스트할 때 손으로 체크하는 칸입니다.
- `Is Dead`: 지금 테스트에서는 쓰지 않습니다.

---

## 4. 치치 오브젝트 만들기

1. Project 창에서 치치 그림을 찾습니다.
2. 치치 그림을 Scene으로 드래그합니다.
3. 이름을 `Chichi_Robot`으로 바꿉니다.
4. 위치를 Player 근처로 옮깁니다.
5. 크기가 너무 크면 `Transform > Scale`을 줄입니다.

치치 그림 위치 예시입니다.

- `Assets/Art/1_Characters/Sidekick/`

---

## 5. 치치 스크립트 붙이기

`Chichi_Robot`에 아래 컴포넌트/스크립트를 붙입니다.

- `BoxCollider`
- `ChichiStateMachine`
- `ChichiMovementController`
- `ChichiInkTank`
- `ChichiChargeController`
- `ChichiChargeInterruptReceiver`

이번 테스트에서는 아래 기능은 붙이지 않아도 됩니다.

- 적/보스
- 의심도
- 포식자 시야
- 전체 HUD
- 사망 시스템
- GuidePoint 안내

---

## 6. ChichiStateMachine 연결

`Chichi_Robot`의 `ChichiStateMachine`에서 연결합니다.

- `Dudu Transform`: Hierarchy의 `Player`를 넣습니다.
- `Guide Controller`: 비워둡니다.
- `Charge Controller`: 같은 `Chichi_Robot`의 `ChichiChargeController`를 넣습니다.
- `Idle Distance`: `1.5`
- `Walk Start Distance`: `3`
- `Walk Stop Distance`: `1.8`

뜻은 이렇습니다.

- 치치가 평소에는 두두와 너무 멀어지지 않게 따라옵니다.
- X키를 누르면 `ApproachCharge` 상태가 됩니다.
- 두두에게 닿으면 `Charging` 상태가 됩니다.
- 충전이 끊기면 `ChargeInterrupted` 상태가 됩니다.

---

## 7. ChichiMovementController 연결

`Chichi_Robot`의 `ChichiMovementController`에서 연결합니다.

- `State Machine`: 같은 `Chichi_Robot`의 `ChichiStateMachine`을 넣습니다.
- `Guide Controller`: 비워둡니다.
- `Dudu Transform`: Hierarchy의 `Player`를 넣습니다.
- `Walk Speed`: `2.5`
- `Charge Approach Speed`: `3.5`
- `Charge Stop Distance`: `0.12`
- `Fixed Z`: 치치의 현재 Z값을 넣습니다. 보통 `0`입니다.

뜻은 이렇습니다.

- 평소에는 두두를 따라갑니다.
- X키 충전 중에는 더 빠르게 두두에게 다가갑니다.
- `Charge Stop Distance`가 작을수록 치치가 두두에게 더 바짝 붙습니다.
- Z값은 고정해서 그림이 앞뒤로 이상하게 움직이지 않게 합니다.

---

## 8. ChichiInkTank 설정

`Chichi_Robot`의 `ChichiInkTank`에서 설정합니다.

- `Tank Max Ink`: `300`
- `Tank Current Ink`: `300`
- `Charge Amount Per Use`: `50`
- `Charge Uses Per Section`: `3`
- `Remaining Charge Uses`: `3`

뜻은 이렇습니다.

- 치치 탱크에는 총 300만큼 잉크가 있습니다.
- 한 번 충전 성공하면 50만큼 두두에게 줍니다.
- 한 구간에서는 3번만 충전할 수 있습니다.
- `Remaining Charge Uses`가 0이 되면 더 이상 충전할 수 없습니다.

---

## 9. ChichiChargeController 연결

`Chichi_Robot`의 `ChichiChargeController`에서 연결합니다.

- `State Machine`: 같은 `Chichi_Robot`의 `ChichiStateMachine`을 넣습니다.
- `Ink Tank`: 같은 `Chichi_Robot`의 `ChichiInkTank`를 넣습니다.
- `Dudu Transform`: Hierarchy의 `Player`를 넣습니다.
- `Chichi Contact Collider`: 같은 `Chichi_Robot`의 `BoxCollider`를 넣습니다.
- `Dudu Contact Collider`: `Player`의 `BoxCollider`를 넣습니다.
- `Charge Key`: `X`
- `Approach Distance`: `1.2`
- `Contact Distance`: `0.05`
- `Charge Duration`: `2`
- `Can Charge While Dudu Camouflaged`: 체크하지 않습니다.
- `Can Charge While Dudu Fleeing`: 체크하지 않습니다.
- `Reset Progress When Contact Lost`: 체크합니다.

뜻은 이렇습니다.

- X키를 누르면 충전 요청을 합니다.
- 치치가 두두에게 걸어갑니다.
- 두 Collider가 닿거나 거의 닿으면 게이지가 찹니다.
- 2초 동안 붙어 있으면 충전 성공입니다.
- 중간에 떨어지면 게이지가 0으로 돌아갑니다.

`Dudu Ink Receiver`, `Dudu State Provider`는 Inspector에서 연결하지 않습니다. `DuduDevelopChargeAdapter`가 DI 컨테이너에 등록하고 `ChichiChargeController`가 자동으로 받아옵니다.

먹물을 쓴 직후에는 두두가 `먹물 사용 중` 상태라서 충전이 막힙니다. Space키를 누른 뒤에는 2초 정도 기다려서 `먹물 사용 중: OFF`가 된 다음 X키를 누르세요.

---

## 10. ChichiChargeInterruptReceiver 연결

`Chichi_Robot`의 `ChichiChargeInterruptReceiver`에서 연결합니다.

- `Charge Controller`: 같은 `Chichi_Robot`의 `ChichiChargeController`를 넣습니다.

이 스크립트는 충전을 강제로 끊는 테스트용 입구입니다.

Unity Editor에서는 컴포넌트 오른쪽 위 메뉴에서 `Test Interrupt Charge`를 눌러 충전을 끊어볼 수 있습니다.

---

## 11. 작은 Text로 탱크 표시하기

1. Hierarchy에서 오른쪽 클릭합니다.
2. `UI > Text`를 만듭니다.
3. 이름을 `Chichi_Tank_Text`로 바꿉니다.
4. Text 위치를 화면 왼쪽 위로 옮깁니다.
5. `Chichi_Tank_Text`에 `ChichiInkTankStatusText`를 붙입니다.

`ChichiInkTankStatusText`에서 연결합니다.

- `Status Text`: 자기 자신의 `Text` 컴포넌트를 넣습니다.
- `Ink Tank`: `Chichi_Robot`의 `ChichiInkTank`를 넣습니다.
- `Charge Controller`: `Chichi_Robot`의 `ChichiChargeController`를 넣습니다.

화면에 이런 내용이 보이면 됩니다.

- 치치 탱크 잔량
- 남은 충전 횟수
- 충전 상태
- 충전 게이지
- 충전 키

의태 중 충전 금지를 확인할 때는 `충전 상태` 줄을 봅니다.

- 의태 중 X키를 누르면 `충전 불가: 두두가 의태 중`이 보여야 합니다.
- 먹물 사용 중 X키를 누르면 `충전 불가: 두두 잉크/먹물/상태 확인`이 보여야 합니다.

---

## 12. 의태 오브젝트 만들기

의태 중 충전 금지 테스트를 하려면 두두가 숨을 수 있는 물건이 필요합니다.

### 12.1 의태 물건 만들기

1. Project 창에서 바위, 산호, 조개 같은 그림을 찾습니다.
2. 그림을 Scene으로 드래그합니다.
3. 이름을 `Camouflage_Rock_Test`처럼 바꿉니다.
4. `Transform > Position`을 Player 근처로 옮깁니다.
5. 너무 크면 `Transform > Scale`을 줄입니다.

의태 그림 위치 예시입니다.

- `Assets/Art/3_Props_Gimmick/CamouflageObj/`
- `Assets/Prefabs/`

### 12.2 의태 물건에 붙일 것

`Camouflage_Rock_Test`에 아래 컴포넌트를 붙입니다.

- `SpriteRenderer`
- `BoxCollider2D`
- `DuduCamouflageTarget`

중요합니다.

- Tag는 설정하지 않아도 됩니다.
- `DuduCamouflageTarget` 컴포넌트가 붙어 있으면 의태 가능한 물건으로 인식합니다.
- `BoxCollider2D` 크기는 그림 크기와 비슷하게 맞춥니다.

### 12.3 DuduCamouflageTarget 설정

`DuduCamouflageTarget`에서 설정합니다.

- `Source Renderer`: 같은 오브젝트의 `SpriteRenderer`를 넣습니다.
- `Camouflage Sprite`: 비워둬도 됩니다.
- `Camouflage Material`: 비워둬도 됩니다.

`Source Renderer`만 연결하면 스크립트가 Sprite와 Material을 자동으로 가져옵니다.

### 12.4 Player의 CamouflageAdapter 확인

`Player`의 `CamouflageAdapter`를 봅니다.

- `Detection Radius`: `1` 또는 `1.5`
- `Camouflage Layer`: `Everything`으로 테스트하면 쉽습니다.
- `Camouflage Key`: `C`

처음 테스트할 때는 `Camouflage Layer`를 `Everything`으로 두는 것이 제일 쉽습니다.

### 12.5 의태하는 방법

1. Play 버튼을 누릅니다.
2. Player를 `Camouflage_Rock_Test` 가까이 옮깁니다.
3. C키를 누릅니다.
4. 두두가 의태 상태로 들어갑니다.
5. 의태 중에는 X키를 눌러도 치치 충전이 시작되면 안 됩니다.

---

## 13. 테스트 방법

### 13.1 기본 충전 테스트

1. Play 버튼을 누릅니다.
2. 두두의 잉크가 꽉 차 있으면 충전이 안 됩니다.
3. 테스트하려면 `PlayerInk > Current Ink`를 `0`이나 `50`으로 낮춥니다.
4. X키를 누릅니다.
5. 치치가 두두에게 다가갑니다.
6. 치치가 두두에게 닿으면 충전 게이지가 올라갑니다.
7. 게이지가 100%가 되면 두두 잉크가 늘어납니다.
8. 치치 탱크 잔량과 남은 충전 횟수가 줄어듭니다.

### 13.2 접촉 끊김 테스트

1. X키를 눌러 충전을 시작합니다.
2. 충전 게이지가 오를 때 치치를 두두에게서 멀리 옮깁니다.
3. 게이지가 0으로 돌아가야 합니다.
4. 충전 성공 처리가 되면 안 됩니다.

### 13.3 의태 중 충전 금지 테스트

1. `Camouflage_Rock_Test`를 Player 근처에 둡니다.
2. Play 버튼을 누릅니다.
3. Player를 의태 물건 가까이 옮깁니다.
4. C키를 눌러 의태합니다.
5. `Dudu_Status_Text` 또는 `GameplayStatusHud`에서 `의태 중인가?: YES`를 봅니다.
6. `두두 상태 설명`이 `의태 중 - 충전 금지` 또는 `완벽 의태 - 충전 금지`인지 봅니다.
7. 이 상태에서 X키를 누릅니다.
8. `Chichi_Tank_Text` 또는 `GameplayStatusHud`에서 `충전 상태: 충전 불가: 두두가 의태 중`을 봅니다.
9. 치치가 충전을 시작하지 않아야 합니다.
10. `두두 잉크`, `치치 탱크`, `남은 충전 횟수`가 줄거나 늘지 않아야 합니다.
11. C키를 다시 눌러 의태를 풉니다.
12. `의태 중인가?: NO`가 되면 X키를 다시 눌러 충전이 시작되는지 봅니다.

### 13.4 도망 중 충전 금지 테스트

1. Play 중 `Player`를 선택합니다.
2. `DuduDevelopChargeAdapter > Is Fleeing`을 체크합니다.
3. X키를 누릅니다.
4. 치치가 충전을 시작하지 않아야 합니다.
5. `Is Fleeing` 체크를 끕니다.
6. X키를 다시 누르면 충전을 시작할 수 있습니다.

### 13.5 남은 충전 횟수 테스트

1. 충전을 3번 성공시킵니다.
2. `Remaining Charge Uses`가 0이 됩니다.
3. X키를 눌러도 더 이상 충전되지 않아야 합니다.
4. 새 구간 테스트를 하려면 `ChichiInkTank`의 `Reset Section Uses`를 코드나 버튼에서 호출해야 합니다.

---

## 14. 문제가 생겼을 때 확인할 것

X키를 눌러도 치치가 안 움직이면 확인합니다.

- `ChichiChargeController > Dudu Transform`에 `Player`가 들어갔는지 봅니다.
- `ChichiStateMachine > Charge Controller`에 `ChichiChargeController`가 들어갔는지 봅니다.
- `PlayerInk > Current Ink`가 `Max Ink`보다 작은지 봅니다.
- `DuduDevelopChargeAdapter > Is Fleeing`이 꺼져 있는지 봅니다.
- `DuduDevelopChargeAdapter > Block Passive Recharge`가 켜져 있는지 봅니다.
- 두두가 의태 중이 아닌지 봅니다.

게이지가 안 오르면 확인합니다.

- 치치와 두두 거리가 `Contact Distance` 안인지 봅니다.
- `Chichi Contact Collider`에 치치 Collider가 들어갔는지 봅니다.
- `Dudu Contact Collider`에 Player Collider가 들어갔는지 봅니다.
- Collider를 쓰는 경우 `Contact Distance`는 `0.05`부터 테스트합니다.
- `Charge Duration`을 `1`로 낮춰 테스트해 봅니다.

의태가 안 되면 확인합니다.

- 의태 물건에 `DuduCamouflageTarget`이 붙어 있는지 봅니다.
- 의태 물건에 `BoxCollider2D`가 붙어 있는지 봅니다.
- `Player > CamouflageAdapter > Detection Radius` 안에 의태 물건이 있는지 봅니다.
- `Player > CamouflageAdapter > Camouflage Layer`가 `Everything`인지 봅니다.
- 의태 키는 `C`인지 봅니다.

Text가 안 보이면 확인합니다.

- Canvas가 있는지 봅니다.
- `Chichi_Tank_Text > Text` 색이 배경과 같은지 봅니다.
- `ChichiInkTankStatusText > Status Text`에 자기 Text가 들어갔는지 봅니다.
