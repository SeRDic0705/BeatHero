# 패턴 선택 순서(랜덤/순차) 기능 계획

## 배경 / 요구
기획자 요청: **튜토리얼용으로 몬스터가 고정된 순서의 패턴을 내도록** 하는 옵션 필요.
현재는 매 프레이즈 패턴 리스트에서 균등 랜덤(`Random.Range`)으로만 선택한다.

## 설계 결정
- "고정 순서" 여부는 **몬스터 단위 성격**(튜토리얼 몬스터인가)이므로 페이즈가 아니라 **개별 `MonsterData` SO**가 플래그를 갖는다.
- `BattleStateMachine`이 이미 `_monster`(MonsterData)를 보유하므로 `_monster.isRandom`만 읽으면 된다. `CombatPhaseData`/`GetCurrentPhase` 배관 변경 불필요.
- 순차 인덱스는 **런타임 가변 상태**이므로 SO가 아니라 `BattleStateMachine`이 보유 (CLAUDE.md §6: SO 런타임 수정 금지).
- 기본값 `isRandom = true` → 기존 몬스터 에셋 동작 변화 없음.
- bool 채택 (튜토리얼 용도엔 충분; 셔플·가중치 모드가 추후 필요하면 enum으로 승격).

## 변경 사항
1. **`MonsterData`(base SO)**: `[BoxGroup("Pattern")] public bool isRandom = true;` 추가. `NormalMonsterData`·`BossMonsterData` 자동 상속.
2. **`BattleStateMachine`**:
   - `private int _patternIndex;` 추가.
   - `SelectRandomPattern()` → `SelectNextPattern()`으로 일반화.
     - `isRandom == true` → 기존처럼 `Random.Range`.
     - `false` → `_patternIndex` 순차 사용 후 `(_patternIndex + 1) % count` 순환.
   - `_patternIndex` 리셋: 층 시작(`SetFloorData`), 보스 페이즈 전환(`CheckBossPhaseTransition`에서 페이즈 변경 시) → 새 페이즈는 0번 패턴부터 순서대로.
   - 호출부 두 곳(`SetFloorData`, `TransitionToNextPattern`) 갱신.

## 동작
- 랜덤(`true`): 매 프레이즈 균등 랜덤 — 현행 유지.
- 순차(`false`): 0 → 1 → … → last → 0 순환. 보스는 모든 페이즈가 같은 모드를 공유하되 페이즈 전환 시 인덱스 리셋.

## 검증
- Unity MCP 컴파일 통과 확인.
- 가능 시 플레이모드에서 순차 몬스터 동작 확인.
