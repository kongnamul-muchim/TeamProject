# 백상아리 (Great White Shark) — 프리팹 설계 도면

> **참조:** `MorayEel.prefab`, `Swordfish.prefab` 구조 기반
> **기준:** SpriteRenderer 기반 2.5D (기존 보스와 동일)

---

## 1. GreatWhite Boss 프리팹

### 1.1 계층 구조

```
GreatWhite (GameObject)
├── SpriteRenderer (보스 이미지)
├── Animator (선택 — Idle/Walk/Hit 애니메이션)
├── BoxCollider (충돌 판정)
├── Rigidbody (Kinematic, Freeze All Rotation)
│
├── BossEnemyController (MonoBehaviour)
├── ConeVisionSensor (MonoBehaviour) — 시야 불필요하지만 일단 유지
├── BossSuspicionSystem (MonoBehaviour)
├── VisionConeRenderer (MonoBehaviour) — 시야 불필요하지만 일단 유지
│
└── (자식) ChargeIndicator — 돌진 Lock-On 선 표시용 (런타임 생성)
```

### 1.2 컴포넌트별 설정값

#### Transform
| 필드 | 값 | 설명 |
|:----|:---|:------|
| Position | (0, 0.5, 0) | 지면 위 |
| Scale | (0.3, 0.3, 0.3) | 기존 보스와 동일 |

#### SpriteRenderer
| 필드 | 값 |
|:----|:---|
| Sprite | 백상아리 이미지 에셋 (할당 필요) |
| Color | White (1,1,1,1) |

#### BoxCollider
| 필드 | 값 |
|:----|:---|
| Is Trigger | false |
| Size | (10, 2.5, 1) — 기존 MorayEel과 유사 |

#### Rigidbody
| 필드 | 값 |
|:----|:---|
| Is Kinematic | true |
| Use Gravity | false |
| Constraints | Freeze Position Y, Freeze Rotation XZ |

#### BossEnemyController
| 필드 | 값 | 비고 |
|:----|:---|:-----|
| moveSpeed | 3 | 기본 이동 속도 |
| acceleration | 20 | 가속도 |
| friction | 0.9 | 마찰력 |
| maxSpeed | 5 | 최대 속도 |
| groundLayer | Ground (Layer) | 기존과 동일 |
| visionSensor | ConeVisionSensor 참조 | 있어도 무시됨 (의심도 자체 관리) |
| suspicionSystem | BossSuspicionSystem 참조 | |
| visionConeRenderer | VisionConeRenderer 참조 | 있어도 무시됨 |
| proximityChaseDistance | 0 (비활성화) | 시야 기반 감지 안 함 |
| patrolSpeed | 2 | Patrol 이동 속도 |
| chaseSpeed | 5 | Chase 상태 기본 속도 |
| searchSpeed | 3 | Search 상태 속도 |
| gimmickAsset | DashChargeGimmick.asset (**필수 할당**) | ScriptableObject |
| customGimmick | null | 사용 안 함 |
| bossSprite | SpriteRenderer 참조 | flipX 방향 전환용 |
| sandPitPrefab | null | 가자미 전용 |
| chargeDirector | null | 곰치 전용 |
| knockbackForce | 12 | 넉백 힘 |
| defaultFacingLeft | true | 스프라이트 기본 방향 |

#### ConeVisionSensor
| 필드 | 값 | 비고 |
|:----|:---|:-----|
| viewRadius | 5 | 있어도 사용 안 함 |
| viewAngle | 60 | 있어도 사용 안 함 |
| raisesSuspicion | false | 의심도 자체 관리하므로 끔 |
| targetLayer | Player | |
| obstacleLayer | Obstacle | |

#### BossSuspicionSystem
| 필드 | 값 | 비고 |
|:----|:---|:-----|
| cautionThreshold | 30 | |
| dangerThreshold | 60 | |
| detectedThreshold | 100 | |
| visionIncreaseSpeed | 0 | **0** — gimmick이 자체 관리 |
| normalDecreaseSpeed | 0 | **0** — gimmick이 자체 관리 |
| camouflageDecreaseSpeed | 0 | **0** — gimmick이 자체 관리 |
| floorVisibilityMode | Hidden | 시야 표시 불필요 |

#### VisionConeRenderer
| 필드 | 값 | 비고 |
|:----|:---|:-----|
| visibilityMode | Hidden | 시야 표시 불필요 |

### 1.3 Gimmick Asset 할당

```
Unity 메뉴: 우클릭 → Create → Enemy Gimmicks → Dash Charge Gimmick
생성 위치: Assets/ScriptableObjects/Gimmicks/DashChargeGimmick.asset
```

#### DashChargeGimmick.asset 권장 설정값

| 파라미터 | 기본값 | 상세 |
|:---------|:------:|:-----|
| suspicionRateStage1 | 3 | 0~30%: +3%/s (여유) |
| suspicionRateStage2 | 8 | 30~60%: +8%/s (주의) |
| suspicionRateStage3 | 15 | 60~90%: +15%/s (위험) |
| suspicionRateStage4 | 25 | 90~100%: +25%/s (치명) |
| suspicionDecreaseRateCamouflage | 10 | 의태 중 -10%/s |
| postChaseSuspicion | 0 | Chase 종료 후 0% |
| chargeSpeedPatrol | 15 | 의태 돌진 속도 |
| chargeSpeedChase | 22 | Chase 돌진 속도 |
| chargeWidth | 2.5 | 돌진 판정 너비 |
| maxDashDistance | 25 | 최대 돌진 거리 |
| chargeCooldownChase | 1.5 | Chase 돌진 사이 쿨타임 |
| camouflageLockDelay | 0.5 | 의태→타겟 선정 딜레이 |
| indicatorDuration | 0.8 | 공격 표식 지속 시간 |
| targetSearchRadius | 12 | 오브젝트 탐색 반경 |
| minPatrolRadius | 5 | Player 최소 거리 |
| maxPatrolRadius | 12 | Player 최대 거리 |

---

## 2. Target Indicator (공격 표식) 프리팹

### 2.1 용도

Patrol 중 샤크가 의태 오브젝트를 타겟팅했을 때,
해당 오브젝트 **머리 위**에 붉은 경고 표시를 띄우기 위한 프리팹.

### 2.2 구현 방식

**방식 A — 빌보드 Sprite (추천)**

```
TargetIndicator (GameObject)
├── SpriteRenderer
│   ├── Sprite: 붉색 원형 또는 VShape 경고 아이콘
│   ├── Sorting Order: UI보다 1단계 아래
│   └── Color: (1, 0.2, 0.2, 0.8)
│
└── Billboard脚本 (간단한 LookAt Camera)
```

**동작 방식:**
```
1. BossEnemyController가 오브젝트 타겟 시 해당 위치에 Instantiate
2. indicatorDuration(0.8s) 동안 머리 위에 표시
3. 타겟 오브젝트를 따라다님 (자식으로 넣거나 위치 동기화)
4. 시간 만료 or 돌진 시작 시 Destroy
```

**방식 B — 기존 LineRenderer 재활용**

```
BossEnemyController.InitializeChargeIndicator()에서 만든 LineRenderer 활용
→ 타겟 오브젝트 위치에 원형 인디케이터 그림
→ 단점: 3D 공간에서 머리 위 원은 각도에 따라 안 보일 수 있음
```

**→ 방식 A 추천.** SpriteRenderer 빌보드가 가장 직관적이고 구현도 간단.

### 2.3 TargetIndicator 스크립트 (신규)

```csharp
// 위치: Assets/Core/Enemy/Boss/Gimmicks/TargetIndicator.cs

public class TargetIndicator : MonoBehaviour
{
    [SerializeField] private float lifetime = 0.8f;
    [SerializeField] private AnimationCurve fadeCurve; // 점점 사라짐

    private Transform _target;
    private float _heightOffset = 2f; // 오브젝트 머리 위
    private float _timer;

    public void Initialize(Transform target, float duration)
    {
        _target = target;
        lifetime = duration;
        _timer = 0f;
        UpdatePosition();
    }

    private void Update()
    {
        _timer += Time.deltaTime;
        UpdatePosition();

        // 페이드 아웃
        float alpha = 1f - (_timer / lifetime);
        // SpriteRenderer alpha 조절

        if (_timer >= lifetime)
            Destroy(gameObject);
    }

    private void UpdatePosition()
    {
        if (_target != null)
        {
            Vector3 pos = _target.position;
            pos.y += _heightOffset;
            transform.position = pos;
        }
    }
}
```

---

## 2.4 ObjectHP 컴포넌트 (신규) — 오브젝트 체력 시스템

백상아리의 돌진이 오브젝트를 **즉시 파괴하지 않고** 체력을 깎아가며 점진적으로 부숨.

### 체력 단계

| HP | 상태 | 의태 가능 | 시각적 특색 |
|:--:|:----|:---------:|:-----------|
| **2** | 기본 (정상) | ✅ | 원본 상태 |
| **1** | 약간 부서짐 | ✅ | 균열 + 기울어짐 + 미세 떨림 |
| **0** | 완전 파괴 | ❌ | `SetActive(false)` |

### ObjectHP 스크립트

```csharp
// 위치: Assets/Core/Enemy/Boss/Gimmicks/ObjectHP.cs

public class ObjectHP : MonoBehaviour
{
    [Header("체력")]
    [SerializeField] private int maxHP = 2;
    [SerializeField] private int currentHP;

    [Header("약간 부서짐 시각 효과")]
    [SerializeField] private Sprite damagedSprite;      // 균열 스프라이트
    [SerializeField] private float tiltAngle = 8f;       // 기울어짐 각도
    [SerializeField] private Vector3 shakeStrength;      // 미세 떨림 강도
    [SerializeField] private ParticleSystem debrisEffect; // 파편

    public int CurrentHP => currentHP;
    public bool IsDestroyed => currentHP <= 0;
    public bool IsDamaged => currentHP < maxHP && currentHP > 0;

    private void Awake() => currentHP = maxHP;

    /// <summary>데미지. true = 완전 파괴</summary>
    public bool TakeDamage()
    {
        if (IsDestroyed) return true;
        currentHP--;

        if (IsDestroyed)
        {
            gameObject.SetActive(false);
            return true;
        }

        ApplyDamagedVisual();
        return false;
    }

    private void ApplyDamagedVisual()
    {
        // 균열 스프라이트
        if (damagedSprite != null)
            GetComponent<SpriteRenderer>().sprite = damagedSprite;

        // 약간 기울어짐 (불안정)
        transform.rotation = Quaternion.Euler(0, 0, tiltAngle);

        // 파편 효과
        if (debrisEffect != null) debrisEffect.Play();
    }

    private void Update()
    {
        // HP=1일 때 미세 떨림
        if (currentHP == 1)
        {
            transform.position += new Vector3(
                Mathf.Sin(Time.time * 10f) * shakeStrength.x,
                Mathf.Sin(Time.time * 8f) * shakeStrength.y,
                0
            );
        }
    }
}
```

### 약간 부서진 오브젝트의 특색

| 요소 | 설명 | 이유 |
|:----|:-----|:------|
| **균열 스프라이트** | 표면에 금이 간 이미지 | "한 대 더 맞으면 부서진다" 직관적 전달 |
| **약간 기울어짐** | 8도 기울어 불안정함 표현 | 자연스러운 손상 |
| **미세 떨림** | 사인파로 계속 흔들림 | "버티고 있다"는 긴장감 |
| **파편 이펙트** | 충격 시 파티클 방출 | 즉각적 피드백 |

### DashChargeGimmick 연동 (DestroyTargetObject 수정)

```csharp
private void DestroyTargetObject()
{
    if (_targetObject == null) return;

    var hp = _targetObject.GetComponent<ObjectHP>();
    if (hp != null)
    {
        hp.TakeDamage(); // 2→1 또는 1→0
    }
    else
    {
        _targetObject.gameObject.SetActive(false); // 구형 호환
    }

    OnObstacleHit?.Invoke(_targetObject.gameObject);
}
```

---

## 3. 씬 배치 가이드

### 3.1 Zone 4 — 심해 절벽

```
Hierarchy:
└── Zone4_Map
    ├── Ground (Ground 레이어)
    ├── Obstacles (Obstacle 레이어)
    │   ├── Rock_1
    │   ├── Rock_2
    │   ├── Coral_1
    │   ├── Coral_2
    │   └── Chell (CamouflageTarget 레이어) ← 의태 가능 오브젝트
    │
    └── GreatWhite (Boss 프리팹)
         ├── BossEnemyController
         │   └── Gimmick Asset → DashChargeGimmick.asset
         ├── ConeVisionSensor
         ├── BossSuspicionSystem
         └── SpriteRenderer
```

### 3.2 CamouflageTarget 레이어 설정

의태 가능한 오브젝트는 전부 **"CamouflageTarget" 레이어** 할당 필수.
이 레이어로 `PickRandomTarget()`의 `Physics.OverlapSphere`가 탐색함.

| 오브젝트 | 레이어 | 비고 |
|:--------|:------|:-----|
| Chell | CamouflageTarget | ✅ 유저 지정 |
| Coral_1 | CamouflageTarget | 환경 오브젝트 |
| Coral_2 | CamouflageTarget | 환경 오브젝트 |
| Rock_1 | Obstacle or CamouflageTarget | 필요시 변경 |

---

## 4. Inspector 설정 참고 (MorayEel 대비 차이)

| 항목 | MorayEel | GreatWhite | 이유 |
|:----|:---------|:-----------|:-----|
| chargeDirector | ✅ 있음 | **❌ 없음** | 곰치 전용 |
| sandPitPrefab | ❌ 없음 | **❌ 없음** | 가자미 전용 |
| gimmickAsset | RelentlessChaseGimmick | **DashChargeGimmick** | |
| visionSensor.raisesSuspicion | true (곰치는 시야 사용) | **false** | DashCharge 자체 의심도 |
| suspicionSystem.visionIncreaseSpeed | 15 (곰치는 자체 상승) | **0** | DashCharge 자체 관리 |

---

*이 문서는 백상아리 최종 보스의 프리팹 설계 도면입니다.*
*참조: `MorayEel.prefab` 구조를 기준으로 하되, 불필요한 컴포넌트는 제외*
*구현 시 Unity 에디터에서 Prefab을 직접 생성하여 설정하세요.*
