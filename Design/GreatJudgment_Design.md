# Great 판정 설계 명세

## 목적

판정창(`_judgmentWindowSec`) 안에서 성공한 입력 중 퍼펙트(`_perfectWindowSec`)가 아닌 "일반 성공"에도
피드백을 준다. 기존에는 정상 히트(비퍼펙트) 시 `TimingIndicator`가 조용히 넘어갔음(`PerfectJudgment_Design.md` §"Perfect 인디케이터" 참조) —
이 규칙을 변경해 "Great"로 표시한다.

---

## 판정 우선순위 (배타적)

```
|-- preFailStart --|-- preJudgStart --|== Perfect(±42ms) ==BEAT==|-- windowClose --|-- lateZoneEnd --|
      (Fast zone)     (판정창 시작, ±84ms)                          (판정창 끝)     (Slow zone, ±42ms)
```

| 판정 | 조건 | 표시 | 우선순위 |
|---|---|---|---|
| **Fast** | 판정창 밖 이른 입력 (`preFailStart ~ preJudgStart`) | "Fast" | 1 (최우선) |
| **Perfect** | 판정창 안에서 소비된 액션 중 `_perfectWindowSec`(±42ms) 이내 | "Perfect!" | 2 |
| **Great** | 판정창 안에서 소비된 액션이 있지만 퍼펙트 폭 밖 | "Great" | 3 |
| **Slow** | 판정창 안에 아무 액션도 없고, Late Zone에 뒤늦은 입력 수신 | "Slow" | (별도 — 액션 无 시에만) |

Fast > Perfect > Great 순으로 배타적 표시(하나의 비트당 이벤트 1회). Slow는 "이번 비트에 유효 액션이 전혀 없었던" 경우에만 별도로 판정되므로 위 셋과 겹치지 않는다.

---

## BattleStateMachine 변경

### enum 확장

```csharp
public enum TimingResult { Fast, Slow, Perfect, Great }
```

### 신규 필드

```csharp
private bool _hitThisBeat; // 이번 비트에 퍼펙트 여부와 무관하게 유효 액션이 발생했는지 (Great 판정용)
```

`_perfectThisBeat`와 동일하게 매 서브비트 시작 시 `false`로 리셋한다.

### 세팅 지점

퍼펙트 여부와 무관하게, 액션이 실제로 "성립"하는 3곳에서 세팅(해당 함수의 마나 체크 등 조기 반환 이후):

- `FireAttack(bool isPerfect)` — 마나 소비 성공 후 (탭 공격) 또는 차지 공격이면 무조건.
- `ProcessMovement(Vector2, bool isPerfect)` — `if (moved)` 블록 안.
- `TryActivateBlock(bool isPerfect)` — 마나 소비 성공 후.

즉 `_perfectThisBeat = true;`를 세팅하는 조건문과 나란히, `isPerfect` 여부와 무관하게 `_hitThisBeat = true;`를 추가한다.

### 판정 발화 (JudgeTile 직후, 기존 위치)

```csharp
if (fastHappened)
    OnTimingMissed?.Invoke(TimingResult.Fast);
else if (_perfectThisBeat)
    OnTimingMissed?.Invoke(TimingResult.Perfect);
else if (_hitThisBeat)
    OnTimingMissed?.Invoke(TimingResult.Great);
```

Slow 감지 흐름(Late Zone, `hadInWindowPress` 체크)은 변경 없음 — 액션이 전혀 없었을 때만 발화하므로 Great와 상호 배타적이다.

---

## 엣지케이스 — 차지 공격

- 차지 시작(K 프레스, 마나 소비하고 `_chargeBeats++`)과 홀드 유지 중인 비트는 `FireAttack`/`ProcessMovement`/`TryActivateBlock`을 거치지 않으므로 `_hitThisBeat`가 세팅되지 않는다 → **Great 안 뜸.**
- 기존 `Perfect`도 릴리즈 시점에만 판정되어 차지 시작/유지 중엔 뜨지 않았음 — 동일한 동작으로 통일(홀드 중 매 비트 표시되는 스팸 방지).
- 차지 릴리즈(공격 발동) 비트에서만 Perfect 또는 Great 중 하나가 뜬다.

---

## TimingIndicator 변경 (`Assets/Scripts/UI/TimingIndicator.cs`)

| 필드 | 값 | 설명 |
|---|---|---|
| `_greatColor` | `#32CD32` (연두/라임그린, 잠정) | Great 판정 텍스트 색상 — Fast(주황)/Slow(하늘)/Perfect(골드)와 구분 |

`Show(TimingResult result)`의 switch에 분기 추가:

```csharp
case TimingResult.Great: _text.text = "Great"; _text.color = _greatColor; break;
```

애니메이션(드리프트+페이드, `_duration`/`_driftY`)은 기존 로직 재사용, 신규 로직 없음.

---

## 문서 반영

- `Design/CombatLogic_Design.md`: "타이밍 미스 인디케이터" 표에 Great 행 추가, "정상 히트 시 이벤트 미발화" 문구를 Great 발화로 수정.
- `Design/TimingIndicator_Design.md`: 판정 분류 표·직렬화 필드 표·판정 발화 코드 스니펫에 Great 반영.

---

## 커밋 계획

브랜치: `feature/great-judgment` (develop에서 분기, PR 1개)

| 커밋 | 내용 |
|---|---|
| `docs: Great 판정 설계 명세 추가` | 이 문서 + CombatLogic_Design.md·TimingIndicator_Design.md 갱신 |
| `feat: BattleStateMachine — Great 판정 로직 추가` | enum 확장, `_hitThisBeat`, 판정 발화 분기 |
| `feat: TimingIndicator — Great 표시 추가` | switch 분기, 라임그린 색상 |
