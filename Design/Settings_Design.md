# 설정 시스템 설계 명세

## 설정 항목

### 화면
- 해상도 선택 (드롭다운 — `Screen.resolutions`로 지원 해상도 목록 자동 수집)
- 전체화면 / 창 모드 토글

### 오디오
- 마스터 볼륨 (슬라이더, 0~1)
- BGM 볼륨 (슬라이더, 0~1)
- SFX 볼륨 (슬라이더, 0~1)

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
- [ ] 입력 오프셋 조정 (오디오 레이턴시 보정)
