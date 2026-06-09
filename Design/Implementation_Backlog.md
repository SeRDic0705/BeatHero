# BeatHero 구현 작업 백로그

2주차 자율 진행용 우선순위 작업 목록. **위에서부터 의존성 순서대로** 진행한다.
각 작업은 "작성 → Unity MCP 컴파일 검증 → 수정" 사이클을 거치고, 완료 시 `[x]` 체크 + Discord 보고.
상세 스펙은 각 항목의 참조 설계 문서를 먼저 읽는다.

진행 규칙:
- 한 Phase 안에서도 위에서부터. 막히면 Discord로 질문 후 다음 항목으로 넘어가지 말 것(의존성 때문).
- 설계에 빈틈이 보이면 임의 결정하지 말고 Discord로 확인.

---

## Phase 0 — 프로젝트 스캐폴딩
> 코드 폴더·어셈블리·에셋 폴더 골격. (CLAUDE.md §4)

- [x] `Assets/Scripts/` 하위 폴더 생성: Core, Data, Combat, Player, Audio, UI, Editor
- [x] 런타임 asmdef `BeatHero.asmdef` (Assets/Scripts) — Odin DLL auto-reference
- [x] 에디터 asmdef `BeatHero.Editor.asmdef` (Assets/Scripts/Editor, Editor 플랫폼 한정) — BeatHero GUID 참조
- [x] `Assets/GameData/` 하위 폴더 생성: Patterns, Monsters, GridShapes, Feedback
- [x] 컴파일 통과 확인 (빈 어셈블리) — 에러/경고 0건

---

## Phase 1 — Data 레이어 (ScriptableObject)
> 모든 시스템의 기반. (PatternData_Design.md, MonsterData_Design.md)

- [ ] `NoteLength` enum (Quarter=12 / Eighth=6 / Triplet=4 / Sixteenth=3)
- [ ] `CellEffect` (abstract SO) + `CellEffectFeedback feedback` 필드
- [ ] `DamageEffect` : CellEffect
- [ ] `PersistentHazardEffect` : CellEffect (`int durationResponsePhases`)
- [ ] `ShieldEffect` : CellEffect
- [ ] `CellEffectFeedback` (SO): activateSfx, vfxPrefab
- [ ] `GridEffectShape` (abstract SO) + `GridEffectShape3x3`(CellEffect[3,3]) / `GridEffectShape5x5`(CellEffect[5,5])
- [ ] **(Editor)** GridEffectShape용 Odin `[TableMatrix]` 커스텀 셀 렌더러 — 격자 클릭 편집
- [ ] `BeatUnit` (NoteLength + GridEffectShape 참조, 쉼표=null)
- [ ] `PatternData` (SO): `List<BeatUnit>` + `damageMultiplier` + `[ValidateInput]` 합=48 검증
- [ ] `GridType` enum (Normal3x3 / Boss5x5)
- [ ] `MonsterData` (abstract SO): maxHp, attackPower, gridType, 프레젠테이션(sprite/animator/hitSfx/deathSfx/deathVfx) + `abstract GetCurrentPhase(float hpPercent)`
- [ ] `CombatPhaseData` (class): bpm, bgm, patterns
- [ ] `NormalMonsterData` : MonsterData (단일 CombatPhaseData)
- [ ] `BossPhase` : CombatPhaseData (+hpThreshold)
- [ ] `BossMonsterData` : MonsterData (List<BossPhase>, HP 기준 전환)
- [ ] `PlayerConfig` (SO 또는 상수): maxHp=100, maxMana=5 등 임시값 — 인스펙터 조정 가능

---

## Phase 2 — 코어 / 인프라
> 전투가 올라탈 토대. (CombatLogic_Design.md, SceneFlow_Design.md)

- [ ] **Conductor (비트 클럭)** — `AudioSettings.dspTime` 기반 BPM→박자 타이밍. SongPositionInBeats(연속)·OnBeat 이벤트·GetBeatDspTime 제공. 보스 전환은 다음 마디 경계(A안). 상세: `Conductor_Design.md`
- [ ] Input 래핑 — New Input System 액션맵(이동/공격) 연결 (`InputSystem_Actions.inputactions` 활용/확장)
- [ ] `GameManager` / 런 진행 — 층 카운트, HP 층간 유지, 사망 시 1층 재시작
- [ ] 씬 전환 매니저 — Title↔Game, 페이드 연출, 층 이동(씬 전환 없이 데이터 교체)

---

## Phase 3 — 전투 코어
> Call & Response 루프. (CombatLogic_Design.md)

- [ ] 그리드 시스템 — 타일 표현, 플레이어 위치, GridEffectShape 렌더링
- [ ] `PatternPlayer` (런타임 재생 인덱스, SO 불변 유지)
- [ ] 전투 상태머신 — CallPhase(이동잠금/패턴제시) → ResponsePhase(입력) → 패턴전환(랜덤) 반복
- [ ] 입력 윈도우 판정 — ±21ms, 구간 종료 직후 위험타일 판정
- [ ] CellEffect 적용 — DamageEffect 데미지 / ShieldEffect 보호막
- [ ] 장애물 시스템 — `List<ActiveHazard>`, 이동 차단, 매 판정 데미지, 재진입 금지, 잔여 카운트 차감
- [ ] 보스 페이즈 전환 — 판정 후 HP% 체크, `GetCurrentPhase()`, BGM/BPM/패턴풀 교체

---

## Phase 4 — 플레이어
> 이동·마나·공격. (CombatLogic_Design.md)

- [ ] 그리드 이동 — ResponsePhase 입력 구간에서 1칸, CallPhase 잠금
- [ ] 마나 시스템 — 이동+1 / 아슬아슬회피+2 / 제자리0 / 최대5 / 층클리어 리셋
- [ ] 아슬아슬 회피 판정 — 구간 열릴 때 위험칸 기록 → 이동 시 출발칸 위험 여부로 +1/+2
- [ ] 기본 공격 — 탭(누르고 뗌), 자동 타깃, 데미지=attackPower×damageMultiplier
- [ ] 차지 공격 — 가변 길이, ResponsePhase 구간마다 마나1+배율누적, CallPhase 유지는 무비용, 구간 밖 릴리즈=취소
- [ ] 차지 취소 처리 — 피격 시/구간 밖 릴리즈 시 취소, 소모 마나 미환급
- [ ] 플레이어 HP / 피격 — 데미지 수용, 0 이하 게임오버

---

## Phase 5 — 오디오
> (Settings_Design.md)

- [ ] AudioMixer 셋업 — Master/BGM/SFX 버스, 파라미터 Expose
- [ ] BGM 재생 — 전투/페이즈 동기, Conductor와 연동
- [ ] SFX 재생 — hit/death/CellEffect feedback

---

## Phase 6 — UI
> (SceneFlow_Design.md §HUD, Settings_Design.md)

- [ ] 전투 HUD — 좌상 플레이어HP / 상단중앙 층수 / 우상 몬스터HP / 하단좌 마나 / 하단중앙 비트바
- [ ] 비트바 (시각적 메트로놈) — 4분음표 기준, Conductor 동기
- [ ] 타이틀 화면 — 시작/설정 버튼
- [ ] 일시정지 메뉴 — ESC, 재개/설정/메인으로
- [ ] 설정 패널 (프리팹) — 해상도 드롭다운, 볼륨 슬라이더3, PlayerPrefs 저장/적용, Title·일시정지 공유

---

## Phase 7 — 콘텐츠 & 폴리시
> 테스트 데이터·확장.

- [ ] 샘플 GridEffectShape 몇 종 제작 (테스트용)
- [ ] 샘플 PatternData 제작 (일반/특수 각각)
- [ ] 샘플 몬스터 1~2종 (Normal/Boss) + 페이즈
- [ ] 1~3층 플레이 가능한 수직 슬라이스 검증
- [ ] FloorData 테이블 (기획자 작성 예정 — 임시 스텁만)

### 비필수 (추후, 우선순위 최하)
- [ ] 차지 단계별 빛 모이는 VFX
- [ ] 3박+ 차지 성공 컷씬 연출 (BGM 정지·페이즈 일시중단)
- [ ] 입력 오프셋 보정 옵션 (오디오 레이턴시)
- [ ] 입력 윈도우 시각화 옵션

---

## 진행 현황 메모
- **2026-06-09** Phase 0 완료 — 폴더 구조(Scripts 7개, GameData 4개) + asmdef 2개 생성, 컴파일 에러 0건. Odin DLL은 Plugin auto-reference로 처리(별도 참조 불필요 확인).
