# MonsterData 설계 명세

## 개요
일반 몬스터와 보스 몬스터의 데이터 구조.
상속으로 분리하되, 공통 프레젠테이션/스탯은 기반 클래스에, 전투 데이터는 서브클래스에 위치.

---

## 클래스 구조

### 1. MonsterData (base ScriptableObject)
몬스터 공통 데이터 — 애니메이션 클립, 이펙트, 기본 스탯.

```csharp
public abstract class MonsterData : SerializedScriptableObject
{
    [BoxGroup("Stats")]
    public int maxHp;
    public int attackPower;    // 피해 공식: attackPower × PatternData.damageMultiplier
    public GridType gridType;  // Normal3x3 / Boss5x5

    // 모든 몬스터는 MonsterBaseAnimator 상태머신을 공유하고 클립만 교체한다.
    // MonsterView가 런타임에 AnimatorOverrideController를 생성해 각 클립을 덮어씌운다.
    [BoxGroup("Animation")]
    public AnimationClip idleClip;   // BPM 비례 speed 제어
    public AnimationClip hurtClip;   // 피격 시 hurt Trigger
    public AnimationClip deathClip;  // 층 클리어 시네마틱 death Trigger

    [BoxGroup("Effects")]
    public Dictionary<CellEffect, CellEffectFeedback> effectFeedbacks = new();
    public AudioClip hitSfx;
    public AudioClip deathSfx;
    public GameObject deathVfxPrefab;

    // 전투 시스템이 타입 구분 없이 현재 페이즈 데이터를 요청하는 인터페이스
    public abstract CombatPhaseData GetCurrentPhase(float hpPercent);
}
```

---

## 애니메이션 아키텍처

### MonsterBaseAnimator (공유 상태머신)
`Assets/Animations/Monster/MonsterBaseAnimator.controller`

| 상태 | 전환 조건 | 우선순위 |
|---|---|---|
| **Idle** (기본) | — | — |
| **Hurt** | `hurt` Trigger (Any→Hurt) | 2 |
| **Death** | `death` Trigger (Any→Death) | 1 |

- Hurt → Idle : exitTime=1f (피격 모션 완료 후 자동 복귀)
- Any → Death : canTransitionToSelf=false

### AnimatorOverrideController (런타임)
`MonsterView.SetMonster(MonsterData)` 호출 시 매번 생성:

```
new AnimatorOverrideController(_baseController)
overrideCtrl["Idle"]  = data.idleClip;
overrideCtrl["Hurt"]  = data.hurtClip;
overrideCtrl["Death"] = data.deathClip;
_animator.runtimeAnimatorController = overrideCtrl;
```

- `_baseController` : Inspector에서 MonsterBaseAnimator 연결 (MonsterView SerializeField)
- Idle 클립 길이는 `animator.Play("Idle")+Update(0f)`로 자동 추출 → 런타임 계산에 사용

### Idle BPM 동기화
```
animator.speed = idleClipLength / beatDurationSec
```
- `beatDurationSec` = BattleStateMachine의 `OnBeatUnitFired(double)` 인자
- BeatUnit마다 Idle을 0f normalizedTime으로 재시작 → 매 비트마다 클립 처음부터 재생

---

### 2. CombatPhaseData
페이즈 하나의 전투 데이터 묶음 (값 타입 또는 일반 클래스).

```csharp
[Serializable]
public class CombatPhaseData
{
    public int bpm;
    public AudioClip bgm;
    public List<PatternData> patterns;
}
```

---

### 3. NormalMonsterData : MonsterData
단일 페이즈 일반 몬스터.

```csharp
public class NormalMonsterData : MonsterData
{
    [BoxGroup("Combat")]
    public int bpm;
    public AudioClip bgm;
    public List<PatternData> patterns;

    public override CombatPhaseData GetCurrentPhase(float hpPercent)
        => new CombatPhaseData { bpm = bpm, bgm = bgm, patterns = patterns };
}
```

---

### 4. BossPhase
보스 페이즈 전환 조건과 해당 페이즈의 전투 데이터.

```csharp
[Serializable]
public class BossPhase : CombatPhaseData
{
    // hpThreshold: 현재 HP% 가 이 값 이하로 떨어지면 이 페이즈로 전환
    // 예: 1.0 = 전투 시작(1페이즈), 0.5 = HP 50% 이하(2페이즈)
    public float hpThreshold;
}
```

---

### 5. BossMonsterData : MonsterData
다중 페이즈 보스 몬스터. phases를 hpThreshold 내림차순으로 정렬해 관리.

```csharp
public class BossMonsterData : MonsterData
{
    // hpThreshold 내림차순으로 정렬 (1페이즈 먼저)
    public List<BossPhase> phases;

    public override CombatPhaseData GetCurrentPhase(float hpPercent)
    {
        // HP% 이하인 가장 마지막 페이즈 반환
        for (int i = phases.Count - 1; i >= 0; i--)
            if (hpPercent <= phases[i].hpThreshold)
                return phases[i];
        return phases[0];
    }
}
```

---

### 6. CellEffectFeedback (ScriptableObject)
CellEffect 간 재사용 가능한 연출 에셋.

```csharp
public class CellEffectFeedback : ScriptableObject
{
    public AudioClip activateSfx;
    public GameObject vfxPrefab;
}
```

CellEffect (abstract SO) 에 `CellEffectFeedback feedback` 필드를 참조로 포함.
동일한 CellEffectFeedback SO를 여러 CellEffect가 공유해 재사용 가능.

---

## 요약

| 클래스 | 역할 |
|---|---|
| `MonsterData` | 공통 스탯 + Animation 클립 3종 + Effects + `GetCurrentPhase()` 인터페이스 |
| `CombatPhaseData` | 페이즈 단위 전투 데이터 (bpm, bgm, patterns) |
| `NormalMonsterData` | 단일 페이즈 몬스터, 직접 CombatPhaseData 보유 |
| `BossPhase` | CombatPhaseData + HP 전환 임계값 |
| `BossMonsterData` | 다중 BossPhase 리스트, HP 기준 페이즈 전환 |
| `CellEffectFeedback` | VFX/SFX 연출 SO, CellEffect들이 공유해 재사용 |
| `MonsterBaseAnimator` | 공유 Animator Controller (Idle/Hurt/Death 상태머신) |
