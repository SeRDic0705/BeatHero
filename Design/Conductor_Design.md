# Conductor (비트 클럭) 설계 명세

리듬 전투의 타이밍 기준. 모든 박자 진행·입력 판정·비트바가 여기에 의존한다.

---

## 핵심 원칙

**프레임 시간이 아니라 오디오 하드웨어 클럭(`AudioSettings.dspTime`)을 기준으로 삼는다.**
프레임 시간(`Time.time`)은 프레임레이트에 따라 들쭉날쭉해서 ±21ms 판정이 불가능하다.
dspTime은 오디오 샘플 단위로 흐르므로 안정적이다.

---

## 시간 ↔ 박자 수식

```
secPerBeat       = 60.0 / bpm                         // 4분음표(=1박) 하나의 시간
songPositionSec  = (AudioSettings.dspTime - dspSongStartTime) - offset
songPositionBeat = songPositionSec / secPerBeat       // 연속값(double/float)
```

- `bpm`: 1분당 박자(4분음표) 수. 4/4박자에서 1박 = 4분음표.
- `dspSongStartTime`: BGM의 첫 샘플이 실제로 재생되는 dspTime (`PlayScheduled`로 예약 시 그 시각).
- `offset`: 곡 시작 무음 보정(`firstBeatOffsetSec`) + 입력 보정(`inputOffsetSec`, 기본 0, 추후 설정).

### 음표 길이 ↔ 시간 (48단위 체계 연동)

1박 = 12단위 → **1단위 = secPerBeat / 12 = 5 / BPM (초)**
NoteLength enum 값(단위 수) × (5/BPM) = 실제 시간(초).

| 음표 | 단위 | 박 | 시간(초) | BPM 120 예시 |
|---|---|---|---|---|
| Quarter | 12 | 1 | 60/BPM | 0.500 |
| Eighth | 6 | 1/2 | 30/BPM | 0.250 |
| Triplet | 4 | 1/3 | 20/BPM | 0.167 |
| Sixteenth | 3 | 1/4 | 15/BPM | 0.125 |

(BPM 120: 1단위 ≈ 41.7ms)

---

## Conductor가 제공하는 것

### 1. 연속 위치 (프로퍼티)
- `double SongPositionSec`
- `double SongPositionInBeats` — 패턴 재생기가 이 값을 보고 세분박까지 위험타일을 정확히 띄움
- `int Bpm`, `double SecPerBeat`

### 2. 박자 이벤트
- `event Action<int> OnBeat` — 정수 박자 경계를 넘을 때마다 발생. 전투 상태머신이 4박(Call)+4박(Response) 카운트에 사용.

```csharp
// Update 루프 의사코드
int beat = (int)SongPositionInBeats;
while (_lastFiredBeat < beat)
{
    _lastFiredBeat++;
    OnBeat?.Invoke(_lastFiredBeat);
}
```

### 3. 박자 절대 시각 조회 (입력 판정용)
- `double GetBeatDspTime(int beatIndex)` = `dspSongStartTime + firstBeatOffset + beatIndex × secPerBeat`
- 입력 판정기가 각 ResponsePhase 박자에 대해 `[beatTime - 0.021, beatTime + 0.021]` 윈도우를 잡고, 윈도우 종료 시각에 캡처된 입력을 판정. (상세: CombatLogic_Design.md)

---

## 재생 제어 API (개략)

```csharp
public class Conductor : MonoBehaviour
{
    public double SongPositionSec { get; }
    public double SongPositionInBeats { get; }
    public int Bpm { get; private set; }
    public double SecPerBeat { get; private set; }
    public event Action<int> OnBeat;

    // BGM 시작. 살짝 미래 dspTime에 PlayScheduled로 예약해 시작 시각을 확정.
    public void StartSong(AudioClip bgm, int bpm);
    public void Stop();

    // 보스 페이즈 전환: 다음 마디 경계에서 BGM/BPM 교체 (아래 참조)
    public void SwitchPhaseAtNextMeasure(AudioClip bgm, int bpm);

    public double GetBeatDspTime(int beatIndex);
}
```

---

## BGM 루프

- `AudioSource.loop = true`. 클럭은 dspTime 기준이라 박자 카운터에 드리프트가 없다.
- **요구사항:** 들리는 음악과 박자 격자가 어긋나지 않도록 **BGM 클립을 정수 마디 길이로 제작**한다(루프 지점이 마디 경계).

---

## 보스 페이즈 전환 — 다음 마디 경계 전환 (확정: A안)

페이즈 전환 요청 시 즉시 끊지 않고 **다음 마디 시작(다운비트)에 맞춰** 새 BGM/BPM으로 교체한다. 박자가 끊기지 않아 리듬 체감이 유지된다.

절차:
1. 현재 `SongPositionInBeats` 기준 다음 마디 시작 박자 인덱스 계산 (4/4 → 4박 배수).
2. 그 박자의 dspTime = `GetBeatDspTime(nextMeasureBeat)` 계산.
3. 새 BGM을 `PlayScheduled(그 dspTime)`로 예약, 기존 BGM은 같은 시각에 `SetScheduledEndTime`으로 정지.
4. 전환 시각에 `dspSongStartTime`/`bpm`/`secPerBeat`를 새 값으로 리셋, 박자 카운터 재기준화.
5. 전환과 함께 패턴 풀 교체는 전투 상태머신이 처리.

> 전환 중에도 dspTime 연속성을 유지해, 예약된 시각에 클럭 파라미터만 원자적으로 교체한다.

---

## 지연/보정 — 싱크 오프셋 (구현 완료)

`Assets/Scripts/Core/SyncSettings.cs`(static, PlayerPrefs 기반)에 두 오프셋을 독립적으로 보관한다. **서로 다른 지점에 주입되어 간섭하지 않는다:**

- `SyncSettings.JudgmentOffsetSec` (판정 싱크): 입력 판정 기준 시각에만 더해진다. `BattleStateMachine.HandleResponsePhrase()`에서 `judgedNoteDsp = noteStartDsp + JudgmentOffsetSec`를 만들어 `beatTime`/`preJudgStart`/`preFailStart`/`windowCloseDsp`/`_lateZoneEndDsp` 계산에 사용. 그리드 표시 타이밍 기준인 `noteStartDsp` 자체는 건드리지 않으므로 시각 연출은 그대로 유지된다.
- `SyncSettings.AudioOffsetSec` (오디오 싱크): 실제 BGM이 스피커로 나오는 시각에만 더해진다. `Conductor`의 `StartFloor()`/`SwitchPhaseAt()`/`SwitchPhaseWithTransition()`/`Resume()`에서 `AudioSource.PlayScheduled`/`SetScheduledEndTime`에 넘기는 dspTime에만 적용. 클럭 기준(`_dspSongStartTime`, `_firstBeatOffsetSec`)은 그대로 유지되므로 `SongPositionInBeats`/`OnBeat`/`GetBeatDspTime`(판정·시각 모두의 기준)은 영향받지 않는다.

**중요한 제약 (설계상 트레이드오프):** 오디오 오프셋은 `PlayScheduled` 호출 시점에만 값이 고정된다. 이 호출은 **층 진입(StartFloor)** 과 **보스 페이즈 전환**(`SwitchPhaseAt`/`SwitchPhaseWithTransition`, 이때 `_switchDspTime`도 함께 오프셋 반영)에서만 일어나므로, 같은 페이즈 안에서 값을 바꿔도 이미 재생 예약된 BGM에는 소급 적용되지 않는다 — 다음 보스 페이즈 전환이나 다음 층부터 반영된다. 판정 오프셋은 매 박자(BeatUnit)마다 값을 새로 읽으므로 다음 박자부터 바로 반영된다.

설정 UI/PlayerPrefs 스펙은 `Settings_Design.md` 참조.

---

## 의존 관계 요약

```
Conductor (dspTime 기준 클럭)
├─ OnBeat ──────────────→ 전투 상태머신 (Call 4박 / Response 4박 카운트)
├─ SongPositionInBeats ─→ PatternPlayer (세분박 위험타일 타이밍)
├─ GetBeatDspTime ──────→ 입력 판정기 (±21ms 윈도우)
└─ SongPositionInBeats ─→ 비트바 UI (4분음표 시각 메트로놈)
```
