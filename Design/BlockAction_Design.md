# 방어(Block) 기능 설계

## 개요

ResponsePhase 박자 타이밍에 L키를 눌러 마나를 소모하고 공격 1회를 무효화하는 방어 액션.
이동·공격과 동일한 입력 윈도우(±21ms) 판정을 사용하며, 박자 슬롯을 소모해 배타적으로 동작한다.

---

## 입력

| 항목 | 값 |
|---|---|
| 키 | L (Keyboard) |
| InputSystem 액션명 | `Block` (Game 맵) |
| 판정 | `started` (누를 때) |
| 유효 상태 | ResponsePhase만 |

---

## 동작 규칙

1. **타이밍 판정:** 기본공격과 동일 — 박자 ±21ms 윈도우 안에 L키 pressed
2. **마나 비용:** `_blockManaCost` (인스펙터 조정, 기본값 1) — `SpendMana` 실패 시 방어 불발
3. **효과:** 해당 박자 `JudgeTile()` 판정에서 피해 1회 무효화 (DamageEffect, PersistentHazardEffect 모두)
4. **배타성:** `_beatInputConsumed` 슬롯 소모 → 같은 박에 이동·공격 불가
5. **차지 중:** 방어 불가 (`_attackHeld == true` 이면 무시)
6. **블록 유효 기간:** 해당 박자 `JudgeTile()` 1회만 — 다음 박으로 이어지지 않음
7. **프리버퍼:** 지원 (`_lastBlockPressTime`) — 윈도우 직전 입력도 인정

---

## 마나 수지

| 상황 | 마나 변화 |
|---|---|
| 방어 발동 성공 | -`_blockManaCost` |
| 피해 없는 타일에서 방어 | -`_blockManaCost` (소모만 되고 판정 없음) |
| 마나 부족 | 0 (방어 불발) |

---

## 구현 위치

| 파일 | 변경 내용 |
|---|---|
| `InputSystem_Actions.inputactions` | Block Button 액션 + L키 바인딩 추가 |
| `InputSystem_Actions.cs` | Block 액션 필드·프로퍼티·콜백·인터페이스 추가 |
| `InputReader.cs` | `OnBlockPressed` 이벤트 + `OnBlock` 콜백 구현 |
| `BattleStateMachine.cs` | `_blockManaCost`(인스펙터), `_blockActive`, `_lastBlockPressTime` 추가; `OnBlockPressed` 핸들러; `JudgeTile()` 블록 체크 |
| `CombatLogic_Design.md` | 방어 시스템 섹션 추가 |
