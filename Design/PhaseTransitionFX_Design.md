# 보스 페이즈 전환 연출 설계 명세

보스 페이즈가 바뀌는 순간, 플레이어가 리듬감 있게 인식할 수 있도록 **전환 완충 마디(Transition Measure)**를 삽입하고 화면·그리드·비트바·몬스터에 연출을 추가한다.

---

## 전체 흐름

```
[구 BPM] CallPhase 4박 / ResponsePhase 4박 (여기서 HP가 임계값 이하로 떨어짐)
[신 BPM] Transition Measure 4박 — 비트마다 전환 연출 재생 (BGM 없음)
[신 BPM + 신 BGM 동시 시작] CallPhase 4박 / ResponsePhase 4박 ...
```

예시 (Dragon_Boss: 1페이즈 155 BPM → 2페이즈 180 BPM):
```
155BPM: CallPhase 쿵쿵쿵쿵 / ResponsePhase 쿵(여기서 HP 50%↓)쿵쿵쿵
180BPM: [플래시+그리드+쉐이크] 쿵 쿵 쿵 쿵  ← 전환 완충 마디
180BPM + 신BGM: CallPhase 쿵쿵쿵쿵 ...
```

---

## 설계 원칙

- HP 임계값은 ResponsePhase가 끝난 뒤에 체크 → 현재 구현 그대로.
- 텍스트 팝업 없이 시각·비트 피드백만으로 전달.
- 모든 연출은 `OnBossPhaseChanged` 이벤트로 트리거 — 컴포넌트끼리 직접 참조하지 않음.
- 연출 파라미터는 인스펙터에서 조정 가능하도록 SerializeField 노출.

---

## 이벤트 훅

`BattleStateMachine`에 `event Action OnBossPhaseChanged` 추가.
전환 완충 마디에서 **박자마다**(4회) 발동.

---

## Transition Measure (전환 완충 마디)

### 발동 조건

`HandlePhrasePair` 끝에서 `_pendingPhaseSwitch`가 세팅돼 있을 때.

### 처리 순서

1. `_conductor.SwitchPhaseWithTransition(nextPhraseStart, newBgm, newBpm)` 호출
   - 구 BGM을 `nextPhraseStart`에 정지 예약
   - BPM·dspSongStartTime 즉시 교체 (BeatBar가 신 BPM 마커 표시 시작)
   - 신 BGM을 `nextPhraseStart + 4 * newSecPerBeat`에 시작 예약 (CallPhase와 동시)
   - `OnSongScheduled` 발동
2. `StartCoroutine(TransitionMeasureRoutine(nextPhraseStart))` 실행
   - 4박 동안 박자마다 `WaitUntil` → `OnBossPhaseChanged` 발동
   - `_state = State.PhaseTransition` 유지 (OnBeat에서 새 프레이즈 시작 방지)
3. 4박 완료 → `HandlePhrasePair(nextPhraseStart + 4 * newSecPerBeat)` 시작
   - 이 시각 = 신 BGM 시작 시각 = CallPhase 시작 시각 → 일치

```csharp
private IEnumerator TransitionMeasureRoutine(double startDsp)
{
    _state = State.PhaseTransition;
    double secPerBeat = _conductor.SecPerBeat; // 이미 신 BPM
    for (int i = 0; i < BEATS_PER_PHASE; i++)
    {
        double beatDsp = startDsp + i * secPerBeat;
        yield return new WaitUntil(() => !_paused && AudioSettings.dspTime >= beatDsp + _pauseDelta);
        OnBossPhaseChanged?.Invoke();
    }
}
```

### State 열거형 추가

```csharp
private enum State { Idle, CallPhase, ResponsePhase, PhaseTransition, BattleEnd }
```

---

## Conductor 변경

### `SwitchPhaseWithTransition(double dspTime, AudioClip bgm, int bpm, int transitionBeats = 4)`

```csharp
public void SwitchPhaseWithTransition(double dspTime, AudioClip bgm, int bpm, int transitionBeats = 4)
{
    double newSecPerBeat = 60.0 / bpm;
    double bgmStartDsp   = dspTime + transitionBeats * newSecPerBeat;
    double fadeDuration  = (float)(bgmStartDsp - dspTime); // 전환 마디 전체 길이

    // 구 BGM: 페이드아웃 시작(dspTime)~CallPhase 직전(bgmStartDsp)에 걸쳐 볼륨 0으로
    //         SetScheduledEndTime으로 bgmStartDsp에 정확히 정지
    _audioSource.SetScheduledEndTime(bgmStartDsp);
    StartCoroutine(FadeOutSource(_audioSource, fadeDuration));

    // 신 BGM 예약 (CallPhase 시작 시각)
    _switchDspTime = bgmStartDsp;
    _nextBgm = bgm;
    _nextBpm  = bpm;
    _switchPending = true;
    var nextSource = gameObject.AddComponent<AudioSource>();
    nextSource.clip = bgm;
    nextSource.loop = true;
    nextSource.outputAudioMixerGroup = _bgmMixerGroup;
    nextSource.PlayScheduled(bgmStartDsp);

    // 클럭 파라미터는 전환 완충 마디 시작 시각(dspTime) 기준으로 즉시 교체
    _bpm              = bpm;
    _secPerBeat       = newSecPerBeat;
    _dspSongStartTime = dspTime;
    _firstBeatOffsetSec = 0;
    _lastFiredBeat    = 0;

    OnSongScheduled?.Invoke(dspTime, bpm);
}

// 지정 AudioSource를 duration초 동안 볼륨 0으로 페이드아웃
private IEnumerator FadeOutSource(AudioSource src, float duration)
{
    float startVolume = src.volume;
    float elapsed = 0f;
    while (elapsed < duration && src != null)
    {
        elapsed += Time.deltaTime;
        src.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
        yield return null;
    }
    if (src != null) src.volume = 0f;
}
```

**구 BGM 처리 요약:**
- 전환 마디 시작(`dspTime`): 페이드아웃 코루틴 시작, 볼륨 1→0
- CallPhase 시작(`bgmStartDsp`): `SetScheduledEndTime`으로 정확히 정지 + 신 BGM 시작
- 전환 마디 전체에 걸쳐 구 BGM이 자연스럽게 사라지고, 신 BGM이 풀 볼륨으로 시작

---

## 연출 항목

### 1. 화면 플래시 — `PhaseTransitionFX.cs` (신규, `UI/`)

- `OnBossPhaseChanged` 구독 (4회 발동).
- 박자마다: Image 알파 0 → `_flashAlpha` → 0, `_flashDuration / 4`초 보간.
- 필드:
  - `[SerializeField] private Image _flashImage`
  - `[SerializeField] private Color _flashColor = Color.white`
  - `[SerializeField] private float _flashAlpha = 0.6f`
  - `[SerializeField] private float _flashDuration = 0.25f`

### 2. BeatBar 강화 커서 펄스 — `BeatBar.cs` 확장

- `BindBattle(BattleStateMachine)` 메서드 추가, `OnBossPhaseChanged` 구독.
- 발동 시 `_cursorPulseScale`을 `_phasePulseScale`(2.5×)로 override, `_cursorPulseDuration`을 `_phasePulseDuration`으로 override.
- 필드 추가:
  - `[SerializeField] private float _phasePulseScale = 2.5f`
  - `[SerializeField] private float _phasePulseDuration = 0.3f`

### 3. 그리드 순간 플래시 — `GridManager.cs` 확장

- `public void FlashTransition(Color color, float duration)` 코루틴 추가.
- 내부 `_flashOverride` 플래그: true일 때 `RefreshVisuals`가 모든 타일을 `_flashColor`로 렌더.
- `BattleStateMachine`이 `OnBossPhaseChanged`에서 `_grid.FlashTransition(...)` 직접 호출.
- 필드 추가:
  - `[SerializeField] private Color _transitionFlashColor = Color.white`
  - `[SerializeField] private float _transitionFlashDuration = 0.2f`

### 4. 몬스터 쉐이크 — `MonsterView.cs` 확장

- `OnBossPhaseChanged` 구독.
- 발동 시 `ShakeRoutine()`: `_baseLocalPos` 기준 ±`_shakeAmount` 랜덤 오프셋 `_shakeDuration` 동안 반복.
- Animator가 있으면 `"PhaseChange"` 트리거 발동.
- 필드 추가:
  - `[SerializeField] private float _shakeAmount = 0.15f`
  - `[SerializeField] private float _shakeDuration = 0.2f`

---

## 컴포넌트 연결 구조

```
BattleStateMachine
├─ TransitionMeasureRoutine (4박 루프)
│   └─ OnBossPhaseChanged (박자마다 발동)
│       ├─→ PhaseTransitionFX   (화면 플래시)
│       ├─→ BeatBar             (강화 커서 펄스)
│       ├─→ GridManager         (타일 플래시, BSM이 직접 호출)
│       └─→ MonsterView         (몬스터 쉐이크)
└─ HandlePhrasePair (4박 후 시작) ← 신 BGM 시작과 동시
```

---

## 파일 변경 목록

| 파일 | 변경 |
|---|---|
| `Assets/Scripts/Combat/BattleStateMachine.cs` | `OnBossPhaseChanged` 이벤트, `State.PhaseTransition`, `TransitionMeasureRoutine`, `_grid.FlashTransition` 호출 |
| `Assets/Scripts/Core/Conductor.cs` | `SwitchPhaseWithTransition` 추가 |
| `Assets/Scripts/UI/PhaseTransitionFX.cs` | 신규 |
| `Assets/Scripts/UI/BeatBar.cs` | `BindBattle`, 강화 펄스 로직 |
| `Assets/Scripts/Combat/GridManager.cs` | `FlashTransition` 코루틴 |
| `Assets/Scripts/Combat/MonsterView.cs` | `OnBossPhaseChanged` 구독, `ShakeRoutine` |

---

## 스코프 외

- 카메라 쉐이크: 별도 컴포넌트 신규 제작 필요 → 이번 스코프 제외.
- `phaseChangeSfx`: 실제 오디오 클립 없음 → 이번 스코프 제외 (추후 `BossPhase`에 필드 추가 가능).
- 텍스트 팝업: 제외.
