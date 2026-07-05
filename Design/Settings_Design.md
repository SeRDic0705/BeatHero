# 설정 시스템 설계 명세

## 설정 항목

### 화면
- 해상도 선택 (드롭다운 — `Screen.resolutions`로 지원 해상도 목록 자동 수집)
- 전체화면 / 창 모드 토글

### 오디오
- 마스터 볼륨 (슬라이더, 0~1)
- BGM 볼륨 (슬라이더, 0~1)
- SFX 볼륨 (슬라이더, 0~1)

### 싱크
게임 중 ESC(일시정지) → 설정에서도 동일하게 조작 가능(타이틀과 프리팹 공유).

| 항목 | UI | 범위/스텝 | 라벨 표기 |
|---|---|---|---|
| 판정 싱크 | Slider(wholeNumbers) + [-]/[+] 버튼 | -5~+5, 1스텝=10ms | 단위 없이 정수만: `"-3"`, `"0"`, `"+2"` |
| 오디오 싱크 | Slider(wholeNumbers) + [-]/[+] 버튼 | -40~+40, 1스텝=0.05초(±2.0초) | 초 단위: `"+0.50s"` |

- 저장: `PlayerPrefs`에 ms 단위 float(`JudgmentOffsetMs`, `AudioOffsetMs`) — `SyncSettings.cs`가 관리.
- 판정 싱크는 다음 박자부터, 오디오 싱크는 다음 보스 페이즈 전환/다음 층부터 반영(상세: `Design/Conductor_Design.md`).
- [-]/[+] 버튼은 `slider.value ±= 1` — Slider 자체 min/max로 자동 클램프.

---

## 구현 방식

### 오디오
Unity AudioMixer 사용.
```
Master Bus
├─ BGM Bus
└─ SFX Bus
```
각 버스의 볼륨 파라미터를 Expose해 슬라이더와 연결.
값은 선형(0~1) → dB 변환: `Mathf.Log10(value) * 20`

### 설정 저장
`PlayerPrefs`로 저장/불러오기.
- `PlayerPrefs.SetFloat("MasterVolume", value)` 등
- 게임 시작 시 자동 적용

---

## 추후 계획 (비필수)
- [ ] 입력 유효 구간 시각화 옵션 (판정 윈도우 표시 온/오프)
