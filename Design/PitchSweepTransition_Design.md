# Pitch Sweep Transition 설계

## 목적
보스 페이즈 전환 완충 마디 시작 시, 구 BGM의 pitch를 신 BPM 비율로 즉시 점프시켜
가속/감속 "레코드판" 효과를 연출한다.

## 동작 흐름
1. 전환 완충 마디 시작 시각(nextPhraseStart)에 `SwitchPhaseWithTransition` 호출
2. **pitch 즉시 점프**: `_audioSource.pitch = (float)newBPM / oldBPM`
3. pitch 점프 상태 유지하며 4박 equal-power 페이드아웃 병행
4. bgmStartDsp(4박 후)에 구 BGM 정지, 신 BGM pitch=1.0으로 시작

## 변경 파일

### `Conductor.cs`
- `SwitchPhaseWithTransition`에 `bool pitchSweep = false` 파라미터 추가
- `pitchSweep=true`일 때: clock 업데이트 전 `oldBpm = _bpm` 캡처 후
  `_audioSource.pitch = (float)bpm / oldBpm` 즉시 적용

### `BattleStateMachine.cs`
- `[SerializeField] private bool _pitchSweepOnTransition = true;` 필드 추가
- `SwitchPhaseWithTransition` 호출 시 `_pitchSweepOnTransition` 전달

## 비고
- 신 BGM AudioSource는 기본 pitch=1.0으로 생성되므로 별도 처리 불필요
- Inspector에서 체크박스로 on/off 가능 (기본값 true)
- BPM 변화폭이 클수록 pitch 변화도 크므로 현장 청음 후 조정 권장
