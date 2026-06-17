# 몬스터 피격 피드백(Hit FX) 설계 명세

플레이어 공격이 몬스터에 명중했을 때 기존 Hurt 애니메이션만으로는 타격감이 부족하다. 4종 효과를 추가해 "juice"를 강화한다.

1. **화이트 플래시** — 피격 순간 스프라이트 전체가 하얗게 번쩍였다 원복
2. **넉백** — 몬스터가 뒤쪽으로 살짝 밀렸다가 제자리로 복귀
3. **스쿼시 & 스트레치** — 피격 순간 살짝 찌그러졌다(가로↑ 세로↓) 원복
4. **히트 파티클** — 피격 지점에 임팩트 스파크 재생 (전용 AnimationClip 직접 할당)

---

## 설계 원칙

- **단일 트리거:** 4종 모두 기존 `MonsterView.OnHit()`(= `BattleStateMachine.OnMonsterHit` 구독) 한 곳에서 발동. 새 이벤트·컴포넌트 간 직접 참조 추가 없음.
- **순수 비주얼, 클럭 불간섭:** 모든 효과는 `Time.deltaTime` 기반 짧은 코루틴. **Conductor/dspTime·박자 동기화는 절대 건드리지 않는다.** (히트스톱·슬로우모션을 배제한 이유와 동일)
- **연타 안전:** 차지 공격 등으로 피격이 연속될 수 있으므로, 각 효과는 코루틴 핸들을 들고 재진입 시 **정지 → base 값 리셋 → 재시작**한다.
- **짧게:** 모든 효과 지속시간 기본값은 박자 간격보다 짧게(≤0.15초) 잡아 다음 비트로 새지 않게 한다.
- **인스펙터 조정:** 모든 파라미터(세기·거리·시간·색)는 `[SerializeField]`로 노출, 기획자가 조정. 하드코딩 금지.
- **히트 파티클은 AnimationClip 직접 할당:** Sprite 프레임 배열·`VFXPool` 방식이 아니라, 전용 VFX 오브젝트에 `AnimationClip`을 인스펙터로 직접 할당해 재생. 화면에 몬스터는 항상 1마리이므로 풀 없이 몬스터 산하 전용 자식 오브젝트 1개면 충분.

---

## 트리거 지점

`MonsterView.OnHit()` (현재: `_animator.SetTrigger(HurtHash)`만 수행) 에 4종 효과 발동을 추가.

```csharp
private void OnHit()
{
    if (_animator != null) _animator.SetTrigger(HurtHash);

    RestartRoutine(ref _flashRoutine,     FlashRoutine());
    RestartRoutine(ref _knockbackRoutine, KnockbackRoutine());
    RestartRoutine(ref _squashRoutine,    SquashRoutine());
    PlayHitVfx();
}

// 코루틴 재진입 헬퍼: 돌고 있으면 멈추고 다시 시작
private void RestartRoutine(ref Coroutine handle, IEnumerator routine)
{
    if (handle != null) StopCoroutine(handle);
    handle = StartCoroutine(routine);
}
```

> ⚠️ `KnockbackRoutine`은 `transform.localPosition`을, 보스 전환용 `ShakeRoutine`도 같은 값을 만진다. 피격 중에는 쉐이크가 돌지 않으므로 충돌하지 않지만, 두 코루틴 모두 **종료 시 `_baseLocalPos`로 확정 복귀**하도록 유지한다.

---

## 효과별 구현

### 1. 화이트 플래시 — 커스텀 스프라이트 셰이더 필요

SpriteRenderer 색(`color`) 틴트는 곱연산이라 "하얗게"가 안 된다. 솔리드 화이트 오버레이를 위해 **소형 플래시 셰이더**(`_FlashColor`, `_FlashAmount`)를 사용하고 `MaterialPropertyBlock`으로 프레임마다 `_FlashAmount`를 `1→0` 보간한다.

- 신규 에셋: `Assets/Art/Shaders/SpriteFlash.shader` (URP 2D 스프라이트 + flash lerp). 셰이더가 적용된 머티리얼을 몬스터 SpriteRenderer에 할당.
- 필드:
  - `[SerializeField] private Color _flashColor = Color.white`
  - `[SerializeField, Range(0f,1f)] private float _flashStrength = 1f`
  - `[SerializeField] private float _flashDuration = 0.08f`
- `FlashRoutine`: `MaterialPropertyBlock`에 `_FlashColor` 세팅, `_FlashAmount`를 `_flashStrength→0`으로 `_flashDuration` 동안 보간 후 0으로 확정.

> 대안(셰이더 없이): 자식 화이트 실루엣 SpriteRenderer를 매 프레임 스프라이트 복사 + 솔리드 셰이더로 표시 — 결국 솔리드 셰이더가 필요하므로 셰이더 1개 추가가 가장 깔끔. **셰이더 에셋 생성이 이 효과의 유일한 외부 의존.**

### 2. 넉백 — `MonsterView` 코루틴

- 필드:
  - `[SerializeField] private float _knockbackDistance = 0.25f`
  - `[SerializeField] private Vector2 _knockbackDir = new(0f, 1f)` (탑뷰: 몬스터는 위쪽, 플레이어 반대 방향 = +Y가 "뒤")
  - `[SerializeField] private float _knockbackOutTime = 0.05f` (밀려나는 시간)
  - `[SerializeField] private float _knockbackBackTime = 0.12f` (복귀 시간, 약간 길게 → 탄성감)
- `KnockbackRoutine`: `_baseLocalPos` → `_baseLocalPos + dir*dist`로 `_knockbackOutTime` 동안 빠르게, 그 뒤 `_baseLocalPos`로 `_knockbackBackTime` 동안 EaseOut 복귀.

### 3. 스쿼시 & 스트레치 — `MonsterView` 코루틴

- `_baseScale = transform.localScale`를 `Start`/`SetMonster`에서 캐시.
- 필드:
  - `[SerializeField] private Vector2 _squashScale = new(1.15f, 0.85f)` (가로 늘고 세로 눌림)
  - `[SerializeField] private float _squashTime = 0.05f`
  - `[SerializeField] private float _squashBackTime = 0.12f`
- `SquashRoutine`: `_baseScale` → `_baseScale*(squashScale)`로 빠르게, 그 뒤 `_baseScale`로 EaseOut 복귀.

### 4. 히트 파티클 — AnimationClip 직접 할당

전용 자식 VFX 오브젝트(SpriteRenderer + Animator)에 인스펙터로 `AnimationClip`을 직접 할당하고, 피격 시 컨트롤러 없이 그 클립을 단발 재생한다. 클립을 직접 트는 정석은 `AnimationPlayableUtilities.PlayClip(animator, clip, out graph)` (PlayableGraph) — Animator Controller/상태머신 없이 할당된 클립 한 개를 그대로 재생.

- 필드:
  - `[SerializeField] private Animator _hitVfxAnimator` (전용 자식 VFX 오브젝트의 Animator)
  - `[SerializeField] private AnimationClip _hitVfxClip` (인스펙터에서 직접 할당)
  - `[SerializeField] private Vector3 _hitVfxOffset = Vector3.zero` (몬스터 중심 보정)
- `PlayHitVfx`:
  - `_hitVfxAnimator` 또는 `_hitVfxClip` 미할당이면 무동작(가드).
  - VFX 오브젝트 위치 = `transform.position + _hitVfxOffset`, SpriteRenderer 활성화.
  - 이전 그래프가 있으면 `Destroy`, `AnimationPlayableUtilities.PlayClip(_hitVfxAnimator, _hitVfxClip, out _hitVfxGraph)`로 재생(연타 시 처음부터 재시작).
  - `_hitVfxClip.length` 후 SpriteRenderer 비활성화 + 그래프 정리(코루틴 또는 `Invoke`).
- `OnDestroy`에서 `_hitVfxGraph` 유효하면 `Destroy`.

> 전용 자식 오브젝트를 쓰는 이유: AnimationClip은 트랜스폼/스프라이트 트랙을 가질 수 있어 몬스터 본체에 직접 재생하면 본체 비주얼을 덮어쓸 수 있음 → 별도 오브젝트에서 안전하게 재생.

---

## 파일 변경 목록

| 파일 | 변경 |
|---|---|
| `Assets/Scripts/Combat/MonsterView.cs` | `OnHit`에 4종 발동, `RestartRoutine` 헬퍼, `FlashRoutine`/`KnockbackRoutine`/`SquashRoutine`/`PlayHitVfx`, `_baseScale` 캐시, `_hitVfxGraph` 정리, 관련 SerializeField |
| `Assets/Art/Shaders/SpriteFlash.shader` | **신규** — 솔리드 화이트 플래시용 스프라이트 셰이더 |
| (에디터) 몬스터 프리팹: SpriteRenderer 머티리얼=SpriteFlash 머티리얼, **자식 VFX 오브젝트(SpriteRenderer+Animator) 추가**, MonsterView에 `_hitVfxAnimator`/`_hitVfxClip` 인스펙터 할당 | MCP/에디터 작업 |

---

## 작업 순서 (제안)

1. `SpriteFlash.shader` + 머티리얼 생성 → 몬스터 SpriteRenderer에 적용
2. `MonsterView.cs`에 4종 로직 추가 → **MCP 컴파일 검증**
3. 인스펙터 파라미터 기본값 세팅 + 자식 VFX 오브젝트 구성 + `_hitVfxClip` 스파크 애니메이션 할당
4. 플레이모드로 타격 시 4종 동시 발동 확인 → Discord 보고

---

## 스코프 외 / 차후

- **카메라 셰이크 · 데미지 숫자 · 판정(Perfect/Good) 텍스트**: 이번 4종 조합 이후 별도 단계.
- **히트스톱 / 슬로우모션**: 비트 클럭 동기화 위험으로 영구 배제.
- 히트 파티클 스파크 AnimationClip이 없으면 임시 placeholder 클립으로 진행 후 교체.
