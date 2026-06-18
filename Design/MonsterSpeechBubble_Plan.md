# 몬스터 말풍선 기능 계획 (기획서 기준)

레퍼런스: 언더테일 몬스터 대사. 출처: 기획서 "몬스터 말풍선 출력 기능 처리".

## 1. 개요
몬스터 위에 출력되는 대사 UI. 두 종류:
| 구분 | 출력 시점 | 역할 |
|---|---|---|
| 전투 중 말풍선 | CallPhase 중 | 공격 예고, 튜토리얼 안내, 캐릭터성 |
| 사망 말풍선 | 몬스터 사망 후·화면전환 전 | 처치 피드백, 캐릭터성 |

## 2. 출력 기준 / 타이밍
- 전투 중 말풍선: **CallPhase 시작 시 표시, CallPhase 전체 동안 유지, ResponsePhase 진입 시 제거.**
- 사망 말풍선: 몬스터 사망(PlayDeath) 직후 표시 → **다음 층 SetFloorData(OnBattleStarted)에서 제거**(고정 시간 없음). 사망 말풍선 출력 중 입력/판정은 이미 중단됨(`SwitchToUIMap` + 전투 종료).

## 3. 전투 중 말풍선 선택 규칙 (CallPhase 순번 기반, 2단계)
- 각 대사에 `useInSequential`(순차 사용), `useInRandom`(랜덤 후보 포함) 플래그.
- 런타임 파생: `순차리스트`=useInSequential 항목(작성 순서), `랜덤후보`=useInRandom 항목.
- `_callIndex`(0부터, 매 CallPhase 증가, 새 몬스터 시 0). 매 CallPhase:
  1. `_callIndex < 순차리스트.Count` → `순차리스트[_callIndex]` (순차)
  2. 아니면 → 랜덤후보에서 선택
  3. 둘 다 비면 미표시
- **연속중복 방지(항상 적용):** 랜덤 선택 시 직전 출력 대사 제외. 단, **후보가 1개뿐이면 제외 없이 그대로 허용**(기획서 6.B). 후보가 2개 이상일 때만 직전 제외 → 빈 후보 발생 불가.

### 예외 처리 (기획서 9장)
- 전투 대사 없음 → 미표시 / 사망 대사 없음 → 사망 말풍선 스킵
- 순차 없이 랜덤만 → 1번째 CallPhase부터 랜덤 (위 로직 자연 충족)
- 순차 소진 후 랜덤 후보 없음 → 이후 미표시

## 4. 데이터 구조
### MonsterDialogueLine (신규, Data, 직렬화 클래스)
```csharp
[Serializable]
public class MonsterDialogueLine
{
    [TextArea] public string text;
    public bool useInSequential = true;
    public bool useInRandom     = false;
}
```
### MonsterData (base SO) 추가
```csharp
[BoxGroup("Dialogue"), TableList]
public List<MonsterDialogueLine> combatDialogues = new();
[BoxGroup("Dialogue"), TextArea]
public string deathDialogue;   // 1개, 비면 미표시
```
- 순번 카운터·직전대사·연속중복 제외는 런타임 컴포넌트가 보유(SO 미수정).

## 5. 이벤트 — BattleStateMachine
```csharp
public event System.Action OnCallPhaseStarted;     // HandleCallPhrase 시작
public event System.Action OnResponsePhaseStarted; // HandleResponsePhrase에서 SetResponsePhase(true) 직후
```

## 6. UI 컴포넌트 — MonsterSpeechBubble (신규, Combat, 월드공간)
- 몬스터 자식 오브젝트. 직렬화: `_root`(말풍선 비주얼 루트, SetActive 토글), `_text`(월드공간 TMP), `_charsPerSecond`(타이핑 속도, 기본 30, 인스펙터 조정).
- 구독: `OnBattleStarted`(대사 파생+카운터 리셋+숨김), `OnCallPhaseStarted`(선택+표시), `OnResponsePhaseStarted`(숨김).
- `ShowDeath()` 공개 메서드: 사망 대사 있으면 표시(true)/없으면 미표시(false).
- **타이핑 연출**: 표시 시 `maxVisibleCharacters`를 0→전체로 늘려 한 글자씩 노출(고정 cps). 새 대사/숨김 시 진행 코루틴 중단. `_charsPerSecond<=0`이면 즉시 전체 표시. 전투·사망 말풍선 공통.

## 7. 사망 흐름 연동 — GameManager.CompleteFloorRoutine
- `PlayDeath()` + deathSfx 직후 `MonsterSpeechBubble.ShowDeath()` 호출.
- 다음 층 `SetFloorData`의 `OnBattleStarted`에서 숨김. 최종 층은 결과창이 덮고 씬 언로드로 자연 소멸.
- 플레이어 사망(RestartRunRoutine)에서도 전투 말풍선 잔상 방지 위해 `Hide()` 호출.

## 8. 씬 작업
- 몬스터 GameObject 하위에 말풍선 오브젝트(배경 SpriteRenderer 플레이스홀더 + 자식 월드 TMP) 생성, `MonsterSpeechBubble` 부착·와이어링. 위치는 몬스터 상단. 배경 스프라이트는 아티스트 교체 전제.

## 9. 검증
- Unity MCP 컴파일(에러 0).
- 플레이모드: CallPhase 표시 / ResponsePhase 숨김 / 순차→랜덤 전환 / 연속중복 방지 / 사망 대사 표시·소멸 확인.
