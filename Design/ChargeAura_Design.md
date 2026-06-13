# 차지 후광 이펙트 설계 (ChargeAura)

> 작성일: 2026-06-13  
> 관련 설계: `CombatLogic_Design.md` § 차지 메커니즘 / § 추후 계획

---

## 개요

차지 중 플레이어 **뒤에** 후광(광원+파티클) 이펙트를 재생한다.  
차지 단계(`_chargeBeats` 기준)가 높아질수록 후광이 밝고 커진다.

---

## 차지 단계 정의

| 단계 | 조건 | 설명 |
|------|------|------|
| 0 | 차지 없음 | 이펙트 꺼짐 |
| 1 | 차지 시작 (`_attackHeld = true`, `_chargeBeats = 0`) | 약한 후광 시작 |
| 2 | `_chargeBeats == 1` (1박 소모 후) | 중간 강도 |
| 3 | `_chargeBeats == 2` (2박 소모 후) | 강한 후광 |
| 4 | `_chargeBeats >= 3` (3박+ 소모 후) | 최대 후광 (색상 변화 포함) |

> **마나 상한(5)** 기준 현실적 최대 차지는 4박이지만, 이펙트는 3박에서 포화시켜 연출 여유 확보.

---

## 구현 구조

### 신규 컴포넌트: `ChargeAuraEffect.cs`
위치: `Assets/Scripts/Player/ChargeAuraEffect.cs`

- `MonoBehaviour`
- `[SerializeField]` 직렬화 필드: 단계별 파라미터 배열 `StageConfig[4]` (인스펙터 조정 가능)
- `StageConfig` 구조:
  - `Color color` — 후광 색상 (0단계는 무시)
  - `float size` — 파티클/후광 크기
  - `float emissionRate` — 파티클 방출 속도
- 외부 호출 메서드: `SetStage(int stage)` — `ParticleSystem` 파라미터를 단계에 맞게 즉시 전환

```
ChargeAuraEffect
├─ ParticleSystem (자식 컴포넌트)
└─ StageConfig[]  — 인스펙터에서 단계별 색상·크기·속도 설정
```

### 수정: `PlayerAnimationController.cs`
- `[SerializeField] ChargeAuraEffect _chargeAura` 필드 추가
- `SetChargeStage(int stage)` 메서드 추가 → `_chargeAura?.SetStage(stage)` 호출
- 기존 `SetCharging(bool)` → 내부적으로 `SetChargeStage(charging ? 1 : 0)` 으로 변환

### 수정: `BattleStateMachine.cs`
아래 2곳에 `_playerAnim?.SetChargeStage(...)` 추가:

| 위치 | 호출 | 이유 |
|------|------|------|
| `OnAttackPressed` / `preAttackJudge` 블록 — `_attackHeld = true` 세팅 직후 | `SetChargeStage(1)` | 차지 시작 |
| 윈도우 클로즈 후 `continuingCharge && _attackHeld && _attackKeyDown` 블록 — `_chargeBeats++` 직후 | `SetChargeStage(Mathf.Min(_chargeBeats, 3) + 1)` | 단계 상승 |
| `ResetCharge()` — 기존 `SetCharging(false)` 자리 | `SetChargeStage(0)` | 차지 종료 |

---

## 씬 구성

```
Player (GameObject)
├─ SpriteRenderer  (Order in Layer: 1)
├─ PlayerAnimationController
└─ ChargeAura (자식 GameObject)
    ├─ ParticleSystem  (Order in Layer: 0 — 스프라이트 뒤)
    └─ ChargeAuraEffect
```

- `ChargeAura` 오브젝트의 `ParticleSystem` → **Sorting Layer**: 플레이어와 동일 레이어, **Order in Layer**는 스프라이트보다 낮게 설정
- 파티클 방향: 방사형(Radial), 플레이어 중심 기준 퍼짐
- `ParticleSystem.Stop(clear: true)` — 단계 0(꺼짐) 시 기존 파티클 즉시 제거

---

## 변경 파일 목록

| 유형 | 파일 |
|------|------|
| 신규 | `Assets/Scripts/Player/ChargeAuraEffect.cs` |
| 수정 | `Assets/Scripts/Player/PlayerAnimationController.cs` |
| 수정 | `Assets/Scripts/Combat/BattleStateMachine.cs` |
| 씬 | `Assets/Scenes/GameScene.unity` — ChargeAura 자식 오브젝트 추가 |

---

## 충돌·주의사항

- `SetCharging(bool)` 은 외부 호출부(BattleStateMachine)에서 `SetChargeStage`로 교체.  
  `PlayerAnimationController` 내부에서 `SetCharging`은 `SetChargeStage`의 래퍼로 유지해 애니메이션 블렌딩 로직은 건드리지 않음.
- ParticleSystem SO 에셋은 불필요 — 모든 파라미터는 런타임에 C#으로 세팅하므로 씬 내 컴포넌트만으로 동작.
- 향후 "빛이 모이는 이펙트(인력형)"로 고도화 시 `ChargeAuraEffect`만 교체하면 됨 (BattleStateMachine 변경 불필요).
