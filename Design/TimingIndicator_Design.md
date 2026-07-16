# TimingIndicator 설계 명세

## 목적

ResponsePhase에서 플레이어의 입력이 판정윈도우 밖으로 무시되었을 때,
타이밍 미스 여부와 방향(빨랐는지/늦었는지)을 BeatBar 위쪽 텍스트로 즉시 피드백.

---

## 판정 분류

```
|-- preFailStart --|-- preJudgStart --|==BEAT==|-- windowClose --|-- lateZoneEnd --|
      (Fast zone)     (pre-buffer OK)    (판정창)     (Slow zone)
```

| 판정 | 조건 | 표시 |
|---|---|---|
| **Fast** | 입력 시각이 Fast Zone (`preFailStart ~ preJudgStart`)에 해당 | "Fast" |
| **Perfect** | 판정창 안에서 소비된 입력이 비트 기준 `_perfectWindowSec`(±42ms) 이내 | "Perfect!" |
| **Great** | 판정창 안에서 소비된 입력이 있으나 퍼펙트 폭 밖 (일반 성공) | "Great" |
| **Slow** | 입력 시각이 Slow Zone (`windowClose ~ windowClose + failZoneSec`)에 해당 | "Slow" |

퍼펙트 판정은 공격/방어/이동 보상에도 쓰인다 (상세: `Design/PerfectJudgment_Design.md`). 우선순위는 Fast > Perfect > Great (비트당 배타적 1회 발화, 상세: `Design/GreatJudgment_Design.md`).

- 플레이어가 아무것도 입력하지 않은 경우 이벤트 미발화

---

## BattleStateMachine 변경

### 추가 enum

```csharp
// Combat 네임스페이스 또는 BattleStateMachine 중첩 — TimingResult
public enum TimingResult { Fast, Slow, Perfect, Great }
```

### 추가 이벤트

```csharp
public event Action<TimingResult> OnTimingMissed;
```

### 추가 필드

```csharp
private double _lateZoneEndDsp;     // 현재 비트의 Late Zone 종료 DSP 절대시각
private bool   _slowInputReceived;  // Late Zone 구간 내 입력 수신 여부
```

### Fast 감지 (기존 변수 재활용)

```csharp
bool fastHappened = preMovesFail || preBasicAttackFail || preChargeFail || preBlockFail;
```

### Slow 감지 흐름

```
1. 창 닫힌 직후:
   _lateZoneEndDsp   = windowCloseDsp + _failZoneSec
   _slowInputReceived = false
   bool hadInWindowPress = _beatInputConsumed (창 닫힌 시점 값 저장)
   _inputWindowOpen   = false
   _beatInputConsumed = false

2. 입력 핸들러 내 (OnMoveInput 등):
   if (!_inputWindowOpen && AudioSettings.dspTime < _lateZoneEndDsp)
       _slowInputReceived = true;

3. JudgeTile() 직후:
   if (fastHappened)
       OnTimingMissed?.Invoke(TimingResult.Fast);
   else if (_perfectThisBeat)
       OnTimingMissed?.Invoke(TimingResult.Perfect);
   else if (_hitThisBeat)
       OnTimingMissed?.Invoke(TimingResult.Great);

   (Late Zone 종료 후, 별도)
   if (!fastHappened && _slowInputReceived && !hadInWindowPress)
       OnTimingMissed?.Invoke(TimingResult.Slow);
```

Great 판정(`_hitThisBeat`) 상세: `Design/GreatJudgment_Design.md`

---

## TimingIndicator 컴포넌트

**위치:** `Assets/Scripts/UI/TimingIndicator.cs`
**네임스페이스:** `BeatHero.UI`

### 직렬화 필드

| 필드 | 기본값 | 설명 |
|---|---|---|
| `_text` | — | TMP_Text 참조 |
| `_battleSM` | — | BattleStateMachine 참조 (이벤트 구독) |
| `_fastColor` | `#FF8C00` (주황) | Fast 판정 텍스트 색상 |
| `_slowColor` | `#00BFFF` (하늘) | Slow 판정 텍스트 색상 |
| `_perfectColor` | `#FFD700` (골드) | Perfect 판정 텍스트 색상 |
| `_greatColor` | `#32CD32` (라임그린) | Great 판정 텍스트 색상 |
| `_driftY` | `60f` | 위로 이동할 픽셀 거리 (RectTransform 기준) |
| `_duration` | `0.8f` | 애니메이션 총 시간(초) |

### 메서드

```csharp
private void OnEnable()  → _battleSM.OnTimingMissed += Show
private void OnDisable() → _battleSM.OnTimingMissed -= Show

public void Show(TimingResult result)
{
    // 진행 중인 코루틴 인터럽트
    if (_animRoutine != null) StopCoroutine(_animRoutine);
    // RectTransform.anchoredPosition Y를 기준 위치로 리셋
    // 텍스트·색상 세팅
    _animRoutine = StartCoroutine(PlayAnim());
}

private IEnumerator PlayAnim()
{
    // 시작: alpha = 1, Y = _baseY
    // 매 프레임: Y += _driftY * Time.deltaTime / _duration
    //            alpha = 1 - (elapsed / _duration)
    // 종료: alpha = 0, 텍스트 비활성화
}
```

---

## 씬 배치

- **GameObject**: `Canvas/HUD/BeatBar/TimingIndicator`
  - RectTransform: BeatBar 위쪽 중앙 배치
  - `TMP_Text` 컴포넌트: Bold, 사이즈 36, 중앙 정렬
  - `TimingIndicator` 컴포넌트: 위 필드 와이어링
- **시작 상태**: 알파 = 0 (숨김)

---

## Perfect 반영 메모

- `Perfect`는 `OnTimingMissed` 이벤트를 그대로 재사용 (이름 변경 없음 — 구독부 변경 최소화)
- `Show()`는 `TimingResult` 3종 분기로 텍스트/색상 결정 (현재 3개라 if/else 유지, 추가 확장 시 딕셔너리 전환 고려)

---

## 브랜치 / 커밋 계획

| 커밋 | 내용 |
|---|---|
| `docs: TimingIndicator 설계 명세 추가` | 이 문서 + CombatLogic_Design.md 업데이트 |
| `feat: BattleStateMachine — Fast/Slow 타이밍 미스 이벤트 추가` | enum·이벤트·감지 로직 |
| `feat: TimingIndicator UI 컴포넌트 추가` | TimingIndicator.cs + 씬 세팅 |
