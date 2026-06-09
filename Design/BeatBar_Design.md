# BeatBar — NecroDancer 스타일 어프로치 방식 설계

## 개요

기존 BeatBar(4개 점 점등)를 NecroDancer/Guitar Hero 스타일로 전면 재설계.  
화면 양끝에서 마커가 생성되어 중앙 커서를 향해 이동하고,  
**마커가 커서에 도달하는 순간 = 박자**이다.

---

## 동작 사양

| # | 사양 |
|---|------|
| 1 | 커서는 BeatBar 중앙에 고정 |
| 2 | 마커는 양끝(LeftSpawn/RightSpawn)에서 동시 생성, 커서 방향으로 이동 |
| 3 | 마커 1개 = 4분음표 1박 / 4개 = 1마디 |
| 4 | 한쪽 끝 → 커서 이동 시간 = 1마디 (BPM 120 → 2초) |
| 5 | 생성 간격 = 60 / BPM 초 (BPM 120 → 0.5초마다 1쌍) |
| 6 | 마커 속도 = distEdgeToCenter / measureDuration |
| 7 | Canvas Scale with Screen Size, LeftSpawn/RightSpawn은 RectTransform으로 배치 |
| 8 | 첫 마커가 커서에 도달하는 순간 BGM 재생 시작 |

---

## 타이밍 수학

```
secPerBeat      = 60.0 / bpm
measureDuration = secPerBeat × 4
spawnInterval   = secPerBeat
dspStartTime    = AudioSettings.dspTime + measureDuration

→ Conductor가 오디오를 dspStartTime에 스케줄
→ BeatBar는 OnSongScheduled 수신 즉시 스폰 시작
→ 첫 마커가 measureDuration 후 도착 = BGM 시작 시각과 일치
```

---

## 오브젝트 풀링

마커는 매 비트(BPM 120 기준 0.5초)마다 2개씩 스폰/회수되므로 풀링 적용.

| 항목 | 내용 |
|------|------|
| 프리팹 | `Assets/Prefabs/UI/BeatMarker.prefab` — Image(폭 4px, 전체 높이) |
| 풀 구조 | `Queue<RectTransform> _pool` (BeatBar 컴포넌트 내부) |
| GetMarker() | 풀 비어있으면 Instantiate, 있으면 Dequeue + SetActive(true) |
| ReturnMarker(rt) | SetActive(false) + Enqueue(rt) |
| 회수 타이밍 | 마커가 커서 통과 시 / 페이즈 전환 시 전체 회수 |

---

## 변경 파일

### `Assets/Scripts/Core/Conductor.cs`
- `public event Action<double, int> OnSongScheduled` 추가
- `StartSong()` 딜레이: `+0.1s` → `+measureDuration`
- 스케줄 후 `OnSongScheduled?.Invoke(dspStartTime, bpm)` 발행
- `SwitchPhaseAtNextMeasure()`: 페이즈 전환 시에도 동일 이벤트 발행

### `Assets/Scripts/UI/BeatBar.cs` (전면 재작성)

**직렬화 필드**
```csharp
[SerializeField] RectTransform _cursor         // 중앙 고정
[SerializeField] RectTransform _leftSpawn      // 왼쪽 끝
[SerializeField] RectTransform _rightSpawn     // 오른쪽 끝
[SerializeField] RectTransform _markerPrefab   // 풀 프리팹
[SerializeField] Color  _markerColor           = Color.white
[SerializeField] float  _markerWidth           = 4f
[SerializeField] float  _cursorPulseScale      = 1.4f
[SerializeField] float  _cursorPulseDuration   = 0.1f
```

**핵심 로직**
- `Bind(Conductor)`: 기존 이벤트 구독 해제 → 재구독
- `OnSongScheduled(dsp, bpm)`: 마커 전체 회수 → 속도/간격 계산 → `SpawnPair()` 즉시 호출
- `SpawnPair()`: `GetMarker()` × 2, LeftSpawn(dir=+1) / RightSpawn(dir=-1) 위치 설정
- `Update()`: 마커 이동 + 커서 통과 검사(ReturnMarker) + 스폰 타이머 + 커서 펄스 감쇠
- `OnBeat(int)`: `_cursorPulseTimer = _cursorPulseDuration` 트리거

### `Assets/Scripts/UI/CombatHUD.cs`
- 변경 없음 (`_beatBar.Bind(conductor)` 이미 호출 중)

### `GameScene` (MCP execute_code)
```
BeatBar (RectTransform, BeatBar script, stretch to full width)
  ├── Cursor      (Image, 6px wide, 전체 높이, anchorMin/Max x=0.5)
  ├── LeftSpawn   (RectTransform, anchorMin/Max x=0, y=0.5)
  └── RightSpawn  (RectTransform, anchorMin/Max x=1, y=0.5)
```
- 기존 Beat_0..3 삭제
- `_cursor`, `_leftSpawn`, `_rightSpawn`, `_markerPrefab` 인스펙터 연결

### `Assets/Prefabs/UI/BeatMarker.prefab`
- RectTransform + Image(흰색, 4px × full height)
- BeatBar 하위로 Instantiate

---

## 검증 체크리스트

1. Play Mode 진입 — Console 에러 없음
2. 전투 시작 → `measureDuration`(BPM 120 → 2초) 후 BGM 재생
3. 마커가 커서와 정확히 일치하는 타이밍에 OnBeat 발생 (Console 로그 확인)
4. BPM 변경 시(페이즈 전환) 마커 속도가 즉시 갱신됨
5. 오브젝트 풀 — Hierarchy에 BeatMarker 인스턴스가 계속 누적되지 않음
