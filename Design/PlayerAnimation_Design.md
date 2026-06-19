# PlayerAnimation 설계 명세

## 개요
플레이어 애니메이션 시스템. Unity Animator 기반 상태 전환 + BPM 싱크 재생.

---

## 1. PlayerAnimationController
`Assets/Scripts/Player/PlayerAnimationController.cs`

Animator 파라미터 래퍼. 전투 시스템·이동 시스템이 이 컴포넌트 하나를 통해 애니메이션 상태를 제어한다.

| 메서드 | 트리거 조건 |
|---|---|
| `SetRunning(bool)` | 층 이동 연출 중 |
| `SetChargeStage(int)` | 차지 단계 변경 (0=꺼짐, 1~4) |
| `TriggerAttack()` | 탭 공격 발동 |
| `TriggerFinalAttack()` | 최종 차지 공격 발동 |
| `TriggerHurt()` | 피격 |
| `TriggerDeath()` | 사망 |
| `ResetState()` | 재도전·초기화 |

---

## 2. IdleBpmSyncBehaviour (StateMachineBehaviour)
`Assets/Scripts/Player/IdleBpmSyncBehaviour.cs`

Animator의 **Idle State에만 부착**하는 StateMachineBehaviour.
Idle 진입·체류 중에는 BPM에 비례한 속도로 재생하고, 위상(phase)을 Conductor와 동기화한다.

### 동작 원리

| 항목 | 내용 |
|---|---|
| 기준 단위 | 8분음표(Eighth Note) |
| 1마디 = | 8 eighth notes = 4 quarter beats |
| 클립 속도 | `animator.speed = Conductor.Bpm / BASE_BPM` |
| 위상 동기화 | Idle 진입 시 현재 곡 위치에서 normalizedTime 계산 후 `animator.Play("Idle", 0, normalizedTime)` |

### 인스펙터 설정

```csharp
[SerializeField] private float _clipEighthNotes = 8f;
```

클립의 총 길이를 8분음표 단위로 지정한다.

| 키프레임 수 | _clipEighthNotes | 결과 |
|---|---|---|
| 4개 | 4 | 1마디에 2회 반복 → 8분음표마다 변경 |
| 8개 | 8 | 1마디에 1회 → 8분음표마다 변경 |

### 코드 구조

```csharp
const float BASE_BPM = 60f; // 클립 제작 기준 BPM

OnStateEnter → animator.speed 설정 + 위상 맞춰 Play
OnStateUpdate → animator.speed 갱신 (보스 페이즈 전환 대응)
OnStateExit  → animator.speed = 1f 원복
```

### 위상 계산

```
songPosInEighths = SongPositionInBeats × 2
normalizedTime   = (songPosInEighths % _clipEighthNotes) / _clipEighthNotes
```

`SongPositionInBeats`는 4분음표 기준이므로 ×2 하면 8분음표 위치가 된다.

### 에디터 설정 (사용자 수동)
Unity Animator의 Idle State 선택 → Inspector → **Add Behaviour → IdleBpmSyncBehaviour**.
