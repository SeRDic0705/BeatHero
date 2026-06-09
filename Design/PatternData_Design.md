# PatternData 설계 명세

## 개요
몬스터가 4박 동안 제시하는 리듬 패턴의 데이터 구조.
패턴은 재사용 가능한 최소 단위(BeatUnit)를 순서대로 조합해 구성한다.

---

## 클래스 구조

### 1. NoteLength (enum)
음표 길이를 부동소수점 오차 없이 정수 단위로 표현.
12를 기본 단위로 사용 (1, 2, 3, 4의 LCM = 12).

```csharp
public enum NoteLength
{
    Quarter   = 12,  // 4분음표
    Eighth    = 6,   // 8분음표
    Triplet   = 4,   // 셋잇단음표
    Sixteenth = 3,   // 16분음표
}
```

4박(한 마디) = 4 × 12 = **48 단위**

---

### 2. CellEffect (abstract ScriptableObject)
그리드 칸에서 발생하는 효과의 기반 클래스. 전략 패턴으로 확장성 확보.

```
CellEffect (abstract)
├─ DamageEffect              — 해당 판정 시 그 칸에 있으면 데미지 (일회성)
│                              formula: MonsterData.attackPower × PatternData.damageMultiplier
├─ PersistentHazardEffect    — 지속형 장애물. N ResponsePhase 동안 해당 칸을 이동불가 구역으로 지정
│   └─ int durationResponsePhases
│   규칙:
│   · 장애물 칸으로는 이동할 수 없음 (이동 입력 차단)
│   · 장애물 지정 시 플레이어가 그 칸에 있었다면 → 그 ResponsePhase 판정 시 데미지
│   · 이후 매 ResponsePhase 판정마다 그 칸에 남아있으면 계속 데미지
│   · 플레이어는 장애물 칸에서 벗어날 수 있지만, 벗어난 후 장애물 칸으로 다시 이동 불가
└─ ShieldEffect              — 밟으면 1회성 보호막 획득
    └─ (추가 파라미터 필요 시 확장)
```

Odin `[SerializeReference]`로 인스펙터에서 구체 타입 선택 가능.

---

### 3. GridEffectShape (ScriptableObject)
3x3 또는 5x5 그리드의 각 칸에 CellEffect를 1:1 대응시킨 재사용 가능한 에셋.

```
GridEffectShape (abstract SO)
├─ GridEffectShape3x3   — CellEffect[3, 3]
└─ GridEffectShape5x5   — CellEffect[5, 5]  (보스 전용)
```

- Odin `[TableMatrix]` + 커스텀 셀 렌더러로 격자 클릭 편집 지원
- **런타임 중 데이터 수정 금지** (SO는 참조 공유 — 읽기 전용으로만 사용)
- 같은 SO를 여러 BeatUnit에서 참조해 재사용 가능

---

### 4. BeatUnit
음표 하나와 그리드 효과를 묶은 최소 조합 단위.
쉼표는 모든 칸이 `null`(효과 없음)인 GridEffectShape로 표현.

```csharp
[Serializable]
public class BeatUnit
{
    public NoteLength noteLength;
    public GridEffectShape gridEffectShape; // SO 참조, null 가능(쉼표)
}
```

---

### 5. PatternData (ScriptableObject)
BeatUnit 리스트로 구성된 전체 패턴. 순서대로 재생.

```csharp
public class PatternData : SerializedScriptableObject
{
    [ValidateInput("ValidateLength", "총 음표 길이가 48단위(4박)와 맞지 않습니다.")]
    public List<BeatUnit> beatUnits;

    // 이 패턴의 DamageEffect 피해 배율. 1.0 = 기본, 특수 패턴은 더 높게 설정
    [Range(0.1f, 5f)]
    public float damageMultiplier = 1f;

    private bool ValidateLength()
        => beatUnits != null && beatUnits.Sum(b => (int)b.noteLength) == 48;
}
```

- 멀티 마디 지원 시: `sum % 48 == 0` 조건으로 변경

---

## 런타임 재생

PatternData SO는 정적 데이터 — 절대 수정하지 않는다.
재생 위치는 런타임 컴포넌트가 별도로 관리한다.

```csharp
public class PatternPlayer : MonoBehaviour
{
    private PatternData pattern;
    private int currentIndex;  // 현재 재생 중인 BeatUnit 인덱스

    public BeatUnit Current => pattern.beatUnits[currentIndex];
    public void Advance() => currentIndex++;
    public void Reset() => currentIndex = 0;
}
```

---

## 요약

| 클래스 | 역할 |
|---|---|
| `NoteLength` | 음표 길이 (정수 단위, 오차 없음) |
| `CellEffect` | 칸 효과 기반 클래스 (상속으로 확장) |
| `GridEffectShape` | 그리드 칸별 효과 지정, SO로 재사용 |
| `BeatUnit` | 음표 길이 + 그리드 효과 조합 단위 |
| `PatternData` | BeatUnit 리스트, 4박 합산 검증 포함, 패턴별 데미지 배율 |
| `PatternPlayer` | 런타임 재생 인덱스 관리 (SO 불변 유지) |
