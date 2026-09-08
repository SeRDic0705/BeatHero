# BeatHero — 프로젝트 가이드 (CLAUDE.md)

이 문서는 클로드가 BeatHero 프로젝트에서 작업할 때 따르는 규칙·구조·워크플로우를 정의한다.
**설계의 단일 진실 원천(Single Source of Truth)은 `Design/` 폴더의 명세 문서들이다.** 구현 전 항상 해당 문서를 먼저 읽는다.

---

## 1. 프로젝트 개요

- **장르:** 탑뷰 리듬 전투 RPG (Call & Response). 4박 패턴 관찰 → 4박 대응.
- **구조:** 슬레이 더 스파이어식 일직선 탑 오르기 = 보스러시. 한 층 = 한 몬스터, 처치 시 다음 층.
- **성장 없음:** 순수 실력 챌린지. 레벨업/장비/스킬 없음. HP는 층 넘어갈 때 유지, 사망 시 1층부터 재시작.
- **목표 규모:** 프로토타입 약 20층. 몬스터·패턴은 데이터로 조합해 확장.

자세한 게임 룰·데이터 구조는 `Design/` 참조 (아래 §3).

---

## 2. 기술 스택 / 환경

| 항목 | 값 |
|---|---|
| Unity | **6000.3.13f1** (Unity 6.3) |
| 렌더 | URP 17.3 (2D) |
| 입력 | **New Input System** 1.19 (`Assets/InputSystem_Actions.inputactions`) — 레거시 Input 클래스 사용 금지 |
| 인스펙터/직렬화 | **Odin Inspector + Serializer** (설치됨). `SerializedScriptableObject`, `[TableMatrix]`, `[SerializeReference]`, `[ValidateInput]`, `[BoxGroup]` 활용 |
| 오디오 | Unity AudioMixer (Master/BGM/SFX 버스) |
| 검증 도구 | **Unity MCP** (CoplayDev) — 컴파일 확인·콘솔 로그·플레이모드·에셋/씬 조작 |
| 프로젝트 경로 | `D:\Unity\BeatHero` |

---

## 3. 설계 문서 (구현 전 필독)

| 파일 | 내용 |
|---|---|
| `Design/PatternData_Design.md` | NoteLength, CellEffect 계층, GridEffectShape, BeatUnit, PatternData, PatternPlayer |
| `Design/MonsterData_Design.md` | MonsterData(base) / Normal·Boss / CombatPhaseData / BossPhase / CellEffectFeedback |
| `Design/CombatLogic_Design.md` | Call/Response 상태머신, 입력 윈도우(±21ms), 마나, 차지 공격, 장애물 라이프사이클, 데미지 공식, 플레이어 스탯 |
| `Design/SceneFlow_Design.md` | 씬 2개(Title/Game) 구조, 일시정지 메뉴, HUD 레이아웃 |
| `Design/Settings_Design.md` | 해상도, AudioMixer 볼륨, PlayerPrefs |
| `Design/Conductor_Design.md` | 비트 클럭(dspTime 기반), 시간↔박자 수식, OnBeat/박자시각, 보스 전환(다음 마디) |
| `Design/Implementation_Backlog.md` | 구현 작업 백로그 (Phase 0~7, 의존성 순서) |
| `Design/Git_Convention.md` | 브랜치(git-flow)·커밋(Conventional+한국어)·PR/푸시 워크플로우 |

**설계 변경이 필요하면 코드부터 바꾸지 말고 해당 .md를 먼저 갱신**한 뒤 구현한다. 문서와 코드가 어긋나면 문서가 기준.

---

## 4. 폴더 구조 & 네임스페이스

모든 게임 코드는 `Assets/Scripts/` 아래. 루트 네임스페이스는 `BeatHero`.

```
Assets/Scripts/
├─ Core/        # 게임 매니저, 씬 전환, 런 진행(층 관리)      → BeatHero.Core
├─ Data/        # ScriptableObject 정의 (불변 데이터)         → BeatHero.Data
├─ Combat/      # 전투 상태머신, 판정, 그리드, 장애물 관리     → BeatHero.Combat
├─ Player/      # 플레이어 이동·공격·마나·차지                → BeatHero.Player
├─ Audio/       # AudioMixer 제어, BGM/SFX 재생               → BeatHero.Audio
├─ UI/          # HUD, 타이틀, 일시정지, 설정 패널            → BeatHero.UI
└─ Editor/      # 커스텀 에디터·Odin 셀 렌더러 (Editor asmdef) → BeatHero.Editor
```

- **Assembly Definition(asmdef)**을 둔다: 런타임용 `BeatHero.asmdef`(Assets/Scripts), 에디터용 `BeatHero.Editor.asmdef`(Assets/Scripts/Editor, `Editor` 플랫폼 한정). Odin 어셈블리 참조 추가.
- 생성한 ScriptableObject 에셋(.asset)은 `Assets/GameData/` 아래 종류별 폴더(Patterns/, Monsters/, GridShapes/, Feedback/)에 저장.
- 폴더/네임스페이스가 1:1 대응되도록 유지.

---

## 5. 코딩 컨벤션

- C# 표준 + Unity 관례. `public` 필드보다 `[SerializeField] private` 선호하되, 데이터 SO는 인스펙터 편집 편의상 `public` 허용(설계 문서 코드 스타일 따름).
- 네이밍: 클래스/메서드 `PascalCase`, 지역변수/파라미터 `camelCase`, **private 필드는 `_camelCase`(언더스코어 접두사)**, **상수는 `UPPER_SNAKE_CASE`로 통일**.
- 한 파일 = 한 주요 타입. 파일명 = 타입명.
- 주석은 한국어, 식별자는 영어. 주변 코드의 주석 밀도에 맞춘다.
- 매직 넘버는 상수로(예: 입력 윈도우 `±21ms`, 최대 마나 `5`, NoteLength 단위 `48`).
- 인스펙터 노출 수치(HP·attackPower·bpm·damageMultiplier 등)는 **임시값이며 기획자가 인스펙터에서 조정**한다는 전제로 하드코딩하지 말고 직렬화 필드로.

---

## 6. 아키텍처 원칙

1. **ScriptableObject = 읽기 전용 데이터.** SO는 참조 공유되므로 **런타임에 절대 수정 금지.** 재생 위치·HP·마나 등 가변 상태는 MonoBehaviour 등 런타임 컴포넌트가 보유 (예: `PatternPlayer.currentIndex`).
2. **데이터와 로직 분리.** PatternData/MonsterData는 데이터, 재생·판정은 시스템 컴포넌트.
3. **전략 패턴 / 상속으로 확장.** CellEffect(abstract) 파생, MonsterData(abstract) 파생. 전투 시스템은 부모 타입·`GetCurrentPhase()` 인터페이스로만 다룬다.
4. **장애물(PersistentHazardEffect)은 런타임에서 독립 관리.** PatternData와 별개로 `List<ActiveHazard>`(위치 + 잔여 ResponsePhase). 이동 차단·매 판정 데미지·재진입 금지는 이 목록으로. (상세: CombatLogic_Design.md)
5. **데미지 공식:** `MonsterData.attackPower × PatternData.damageMultiplier`. 특수 패턴은 damageMultiplier를 높게.
6. **씬 전환 최소화.** 층 이동은 씬 전환 없이 GameScene 내 데이터 교체 + 연출. 씬은 Title/Game 둘뿐. Settings 패널은 프리팹으로 Title·일시정지 공유.

---

## 7. 작업 워크플로우 (중요)

**코드는 "작성 → 컴파일 검증 → 수정"을 한 사이클로 본다. 작성만 하고 끝내지 않는다.**

0. **구현 착수 전 사전 허가 필수.** 계획을 세운 뒤 Discord로 보고하고, 마스터의 명시적 허가 문구("진행해"/"구현해"/"허가")를 받은 뒤에만 코드/MCP/커밋/푸시/PR을 시작한다. "계획 진행하자"처럼 목적어가 계획/설계 논의인 문장은 구현 착수 허가가 아니다 — 애매하면 "구현 착수해도 될까요?"로 한 번 더 확인. 설계 질문에 답했거나 열린 질문이 다 정리됐다는 것 자체는 허가가 아니다.
1. 구현 전 관련 `Design/*.md`를 읽는다.
2. 컨벤션(§4~6)에 맞춰 코드 작성.
3. **Unity MCP로 컴파일 확인.** 에러가 나면 콘솔 로그를 읽고 스스로 수정 후 재확인. 컴파일 통과를 확인하기 전엔 "완료"라고 보고하지 않는다.
4. 가능하면 플레이모드/에셋 생성으로 동작까지 검증.
5. 한 작업 단위가 끝나면 진행 상황을 Discord 채널에 보고. **작업 완료 보고는 반드시 터미널이 아니라 Discord 메시지로 보낸다** (사용자는 터미널을 못 봄 — 터미널 출력만으로는 보고로 인정하지 않음). 보고·지시는 전용 채널 `1513355366908428409` 사용.
6. 막히거나 선택이 필요하면 **터미널 프롬프트가 아니라 Discord 메시지로 질문**한다. 되돌리기 어렵거나 설계에 영향 주는 결정은 임의 진행하지 말고 Discord로 확인.

> Unity MCP가 연결돼 있어야 3~4단계가 가능하다 (`http://127.0.0.1:8080/mcp`). 연결이 끊겨 있으면 먼저 사용자에게 Unity 에디터/브리지 상태를 확인 요청한다. **검증 불가 상태에서 대량의 코드를 작성하지 말 것.**

---

## 8. 하지 말 것 (Constraints)

- 레거시 `UnityEngine.Input` 사용 금지 — New Input System 사용.
- 런타임에 ScriptableObject 필드 수정 금지.
- 설계 문서와 다른 임의 설계 변경 금지 — 바꾸려면 문서부터 갱신 후 Discord 확인.
- 컴파일 검증 없이 "완료" 보고 금지.
- **마스터의 명시적 허가("진행해"/"구현해"/"허가") 없이 코드/MCP/커밋/푸시/PR 착수 금지.**
- **작업 완료 보고를 터미널 출력으로만 끝내지 말 것 — 반드시 Discord 메시지로 전송.**
- `Design/`·`CLAUDE.md`·메모리 파일을 사용자 확인 없이 대규모로 갈아엎지 말 것 (점진적 갱신은 OK).
- 한글이 들어가는 생성 스크립트(.ps1 등)는 UTF-8 BOM 필수 (cp949 깨짐 방지).
