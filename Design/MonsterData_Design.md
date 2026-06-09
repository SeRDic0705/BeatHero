# MonsterData 설계 명세

## 개요
일반 몬스터와 보스 몬스터의 데이터 구조.
상속으로 분리하되, 공통 프레젠테이션/스탯은 기반 클래스에, 전투 데이터는 서브클래스에 위치.

---

## 클래스 구조

### 1. MonsterData (base ScriptableObject)
몬스터 공통 데이터 — 프레젠테이션 및 기본 스탯.

```csharp
public abstract class MonsterData : SerializedScriptableObject
{
    [BoxGroup("Stats")]
    public int maxHp;
    public int attackPower;    // 피해 공식: attackPower × PatternData.damageMultiplier
    public GridType gridType;  // Normal3x3 / Boss5x5

    [BoxGroup("Presentation")]
    public Sprite sprite;
    public RuntimeAnimatorController animator;
    public AudioClip hitSfx;
    public AudioClip deathSfx;
    public GameObject deathVfxPrefab;

    // 전투 시스템이 타입 구분 없이 현재 페이즈 데이터를 요청하는 인터페이스
    public abstract CombatPhaseData GetCurrentPhase(float hpPercent);
}
```

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
| `MonsterData` | 공통 프레젠테이션/스탯 + `GetCurrentPhase()` 인터페이스 |
| `CombatPhaseData` | 페이즈 단위 전투 데이터 (bpm, bgm, patterns) |
| `NormalMonsterData` | 단일 페이즈 몬스터, 직접 CombatPhaseData 보유 |
| `BossPhase` | CombatPhaseData + HP 전환 임계값 |
| `BossMonsterData` | 다중 BossPhase 리스트, HP 기준 페이즈 전환 |
| `CellEffectFeedback` | VFX/SFX 연출 SO, CellEffect들이 공유해 재사용 |
