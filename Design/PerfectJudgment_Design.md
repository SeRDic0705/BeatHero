# 퍼펙트 판정 설계 명세

## 목적

기존 입력 판정창(`_judgmentWindowSec`) 안에서도 비트에 더 정확히 맞춘 입력에 추가 보상을 준다.
판정창을 통과한 입력 = "성공", 그중 비트에 `_perfectWindowSec` 이내로 맞춘 입력 = "퍼펙트".

---

## 타이밍 구간 (갱신)

```
|-- preFailStart --|-- preJudgStart --|== Perfect(±42ms) ==BEAT==|-- windowClose --|-- lateZoneEnd --|
      (Fast zone)     (판정창 시작, ±84ms)                          (판정창 끝)     (Slow zone, ±42ms)
```

| 상수 | 값 | 의미 |
|---|---|---|
| `_judgmentWindowSec` | **0.084** (기존 0.042에서 변경) | 판정창 반폭 — 이 안에 들어오면 "성공" |
| `_perfectWindowSec` | **0.042** (신규) | 퍼펙트 반폭 — 판정창의 부분집합, 이 안이면 "퍼펙트" |
| `_failZoneSec` | 0.042 (변경 없음) | Fast/Slow 미스존 반폭 |

퍼펙트 판정: `abs(입력시각 - 비트시각) <= _perfectWindowSec`. 판정창 안이지만 퍼펙트 폭 밖이면 "성공(일반)".

---

## 액션별 효과

### 1. 공격 (기본공격 J / 차지공격 K 릴리즈)
- 퍼펙트 시 최종 데미지 × **1.4** (40% 추가)
- 차지 유무와 무관하게 최종 데미지에 곱함
- 판정 기준: J는 press 시각, K는 release 시각

### 2. 방어 (L)
- **일반 성공**: 데미지 50% 경감 (관통 데미지 절반 적용)
- **퍼펙트**: 완전 무효화 (기존 동작과 동일)
- 판정 기준: L press 시각
- 피해 없는 타일(빈 칸)에서는 기존처럼 퍼펙트 여부와 무관하게 마나만 소모하고 끝

### 3. 이동
- **일반 이동(안전 칸 → 이동)**: 퍼펙트일 때만 마나 +1. 퍼펙트 아니면 이동은 되지만 마나 0.
- **아슬아슬 회피(위험 칸에서 이동)**: 퍼펙트 여부 **무관하게** 항상 마나 +1 (기존 +2에서 변경 — 일반 이동과 동일 보상으로 통일, 대신 무조건 지급)
- 판정 기준: 이동 입력(방향키) 시각

---

## Perfect 인디케이터 (TimingIndicator 확장)

- `TimingResult` enum에 `Perfect` 추가: `{ Fast, Slow, Perfect }`
- 발생 조건: 해당 비트에 소비된 액션(공격/방어/이동) 중 하나라도 퍼펙트였으면 `OnTimingMissed?.Invoke(TimingResult.Perfect)` (이벤트명 유지, `Fast`/`Slow`와 동일 배타적 처리 — Fast가 우선)
- 표시: 텍스트 "Perfect!", 색상 골드(`#FFD700`), 사운드 없음(요청에 따라 생략)
- 기존 "정상 히트(비퍼펙트) 시 이벤트 미발화" 규칙은 유지 — 퍼펙트가 아닌 일반 성공은 여전히 조용함

---

## BattleStateMachine 구현 메모

- `IsPerfect(double inputDsp, double beatDsp)` 헬퍼로 판정 (입력 타임스탬프는 기존 `_lastMoveTime`/`_lastBasicAttackPressTime`/`_lastAttackReleaseTime`/`_lastBlockPressTime` 재사용)
- 프리버퍼 판정부는 코루틴 로컬 `beatTime`을 그대로 기준으로 사용
- 판정창 내 즉시 처리(이벤트 핸들러)는 코루틴 밖에서 호출되므로, 매 서브비트 시작 시 `_currentBeatDsp` 필드에 `beatTime`을 저장해두고 이벤트 핸들러에서 참조
- 이동은 프리버퍼 처리 중 원본 타임스탬프(`_lastMoveTime`)가 리셋되므로, 판정에 쓸 시각을 `_pendingMoveInputDsp`에 별도 보존
- `FireAttack(bool isPerfect)` / `TryActivateBlock(bool isPerfect)` / `ProcessMovement(Vector2, bool isPerfect)`로 시그니처 확장
- 방어 데미지 처리는 `ApplyMonsterDamage(bool blocked, bool perfectBlock)` 헬퍼로 통합 (기존 3곳의 중복 분기 정리)

---

## 문서 반영

- `Design/CombatLogic_Design.md`: 마나 시스템 표, 방어 시스템, 공격 시스템 섹션에 퍼펙트 규칙 반영, 타이밍 미스 인디케이터 섹션에 Perfect 추가
- `Design/TimingIndicator_Design.md`: "(미래) Perfect" 항목을 확정 스펙으로 갱신

---

## 커밋 계획

브랜치: `feature/perfect-judgment` (develop에서 분기, PR 1개)

| 커밋 | 내용 |
|---|---|
| `docs: 퍼펙트 판정 설계 명세 추가` | 이 문서 + CombatLogic_Design.md·TimingIndicator_Design.md 갱신 |
| `feat: BattleStateMachine — 퍼펙트 판정 로직 추가` | perfectWindowSec, IsPerfect, 공격/방어/이동 보상 분기 |
| `feat: TimingIndicator — Perfect 표시 추가` | enum 확장, Show() 분기, 골드 색상 |
