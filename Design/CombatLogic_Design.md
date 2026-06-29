# 전투 로직 설계 명세

## 전투 진입 흐름

```
층 입장
  → BGM/BPM 시작
  → 몬스터 등장 연출
  → CallPhase 시작
```

---

## 전투 상태 머신

### 1. CallPhase (패턴 제시 — 4박)
- 박자마다 현재 BeatUnit의 GridEffectShape을 그리드에 표시
- 플레이어 이동 **완전 잠금**
- 4박이 지나면 ResponsePhase로 전환
- 차지 중인 경우: 키 유지 시 차지 상태 유지 (마나 소모/배율 누적 없음)

### 2. ResponsePhase (플레이어 대응 — 4박)
각 박자마다 **입력 유효 구간(±21ms, 상수 — 플레이 테스트 후 조정 가능)**이 열린다.

**구간 내 입력 선택지 (이동/공격 배타적):**
- 이동 (방향키) → 1칸 이동
- J키 (기본공격, 누를 때 판정) → 즉시 기본 공격 (차지 중 무시)
- K키 유지 → 차지 시작 또는 차지 유지 (마나 소모 + 배율 누적)
- K키 릴리즈 (구간 내) → 차지 발동

**구간 외 입력:** 무시. 차지 중 K키를 구간 외에서 놓으면 차지 취소.

**구간 종료 직후 판정:**
- 플레이어 현재 타일의 CellEffect 발동 여부 체크
- 위험 타일 위 → CellEffect.Apply(player)

### 3. 패턴 전환
- ResponsePhase 4박 종료 → 패턴 풀에서 **랜덤**으로 다음 PatternData 선택
- CallPhase 재시작

### 4. BattleEnd
- 몬스터 HP ≤ 0 → 클리어, 다음 층으로
- 플레이어 HP ≤ 0 → 게임오버, 1층부터 재시작

---

## 마나 시스템

| 상황 | 마나 변화 |
|---|---|
| 이동 (다른 칸으로) | +1 |
| 아슬아슬 회피 (위험 타일에서 입력 구간 내 이동) | +2 |
| 제자리 유지 | 0 |
| 차지 입력 구간 통과 (ResponsePhase 입력 구간에서 키 유지) | -1 (1박당) |
| 차지 릴리즈 (공격 발동) | -1 (릴리즈 박자) |

- **최대 마나: 5**
- **층 클리어 시 리셋 (0으로)**

### 아슬아슬 회피 판정 로직
1. 입력 구간 열릴 때: 플레이어 현재 타일이 위험 칸인지 기록
2. 입력 구간 내 이동 발생 시: 출발 타일이 위험 칸이었으면 → +2마나 (아슬아슬 회피)
3. 출발 타일이 안전 칸이었으면 → +1마나 (일반 이동)

---

## 공격 시스템

### 기본 공격 (J키) / 차지 공격 (K키) — 키 분리
- **J키 (기본공격):** 구간 내 누를 때 즉시 판정 → 기본 공격 (이동과 동일한 Press 판정)
- **K키 (차지공격):** 구간 내 유지 → 차지 누적, 구간 내 릴리즈 → 공격 발동

### 차지 메커니즘 (K키)
- ResponsePhase 입력 구간에서 K키 유지 → 마나 1 소모 + 배율 누적
- CallPhase 동안 K키 유지 → 차지 상태만 유지 (마나 소모 없음, 배율 누적 없음)
- 입력 구간 내 릴리즈 → 공격 발동
- 입력 구간 외 릴리즈 → 차지 취소, 이전 소모 마나 반환 없음 (유예 구간 없음)
- 차지 중 피격 → 차지 취소, 이전 소모 마나 반환 없음
- 차지 중 J키 입력 → 무시

### 공격 타깃
- 자동 타깃 (몬스터 단일 대상)

### 공격 스킬 구조 (확장 가능)
- 공격 데미지 = 기본 데미지 × 누적 배율 (배율 공식은 튜닝 값)
- 마나 비용 = 차지 박자 수 × 1 (릴리즈 박자 포함)

---

## 플레이어 스탯

| 항목 | 임시값 | 비고 |
|---|---|---|
| 최대 HP | 100 | 인스펙터 조정 가능 |
| 최대 마나 | 5 | 확정 |

---

## 데미지 공식

```
실제 피해 = MonsterData.attackPower × PatternData.damageMultiplier
```

- `attackPower`: 몬스터 기본 공격력 (MonsterData 인스펙터에서 설정)
- `damageMultiplier`: 패턴별 배율 (PatternData 인스펙터, 기본값 1.0, 특수 패턴은 높게)

---

## PersistentHazardEffect 라이프사이클

PersistentHazardEffect 칸은 PatternData SO와 별개로 **런타임에서 독립 관리**된다.
전투 시스템이 `List<ActiveHazard>` 를 유지하며 그리드 위에 겹쳐 렌더링.

```
ActiveHazard {
    GridPosition position
    PersistentHazardEffect effect
    int remainingResponsePhases
}
```

**등록 시점:** BeatUnit 재생 시 해당 칸의 CellEffect가 PersistentHazardEffect이면 ActiveHazard 목록에 추가

**이동 차단:** 플레이어 이동 입력 처리 시 목표 칸이 ActiveHazard 목록에 있으면 이동 거부

**판정 (ResponsePhase 구간 종료 후):**
1. 플레이어가 ActiveHazard 칸 위 → 데미지 적용 (attackPower × patternMultiplier)
2. 모든 ActiveHazard.remainingResponsePhases -= 1
3. remainingResponsePhases == 0 → 목록에서 제거, 해당 칸 이동 가능 복구

**이탈/재진입:**
- 플레이어는 ActiveHazard 칸에서 다른 칸으로 이동 가능 (탈출)
- 탈출 후 ActiveHazard가 남아있는 동안 그 칸으로 다시 이동 불가

---

## 비트 UI (시각적 메트로놈)

- 화면 하단에 4분음표 기준으로만 박자 표시 (크립트 오브 더 네크로맨서 참고)
- 세분박(8분음표/16분음표 등) BeatUnit은 별도 표시 없음 — 플레이어가 패턴 시각 피드백으로 체감
- 입력 유효 구간(±21ms)은 UI에 별도 표시하지 않음

---

## 보스 페이즈 전환

- 매 판정 후 몬스터 현재 HP% 체크
- `BossMonsterData.GetCurrentPhase(hpPercent)` 로 현재 페이즈 데이터 조회
- 페이즈 변경 시: BGM 전환, BPM 변경, 패턴 풀 교체

---

## 추후 계획 (비필수)

- [ ] 차지 단계에 따라 플레이어 주위에 빛이 모이는 이펙트 (차지 박자 수에 비례해 강도 증가)
- [ ] 일정 박자 이상(3박+) 모으기 공격 성공 시 강력한 컷씬 연출 — 연출 중 BGM 정지, Call+Response 페이즈 일시 중단, 연출 종료 후 전투 재개
