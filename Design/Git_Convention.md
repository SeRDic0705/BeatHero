# Git 컨벤션

2주차 자율 작업 포함, BeatHero의 깃 운영 규칙.

---

## 1. 브랜치 전략 (git-flow)

| 브랜치 | 역할 |
|---|---|
| `main` | 안정 릴리스. 직접 커밋 금지. |
| `develop` | 통합 브랜치. feature가 PR로 병합되는 대상. |
| `feature/<이름>` | 기능 개발. **develop에서 분기**, 완료 시 **PR로 develop에 병합**. |
| `release/*`, `hotfix/*` | git-flow 표준. 프로토타입 단계에선 미사용, 필요 시 도입. |

- **feature 브랜치 = 백로그 Phase 1개 = PR 1개.** 그 Phase 안의 각 체크박스 항목이 **커밋 1개**가 된다.
  - 예: `feature/phase3-combat-core` 브랜치 안에서 그리드 / 상태머신 / 판정 / 장애물 / 보스페이즈를 각각 커밋 → Phase 3 전체를 하나의 PR로 develop에 병합.
  - 네이밍: `feature/phase<N>-<요약>` (예: `feature/phase0-scaffolding`, `feature/phase1-data`, `feature/phase3-combat-core`).
- 작업 시작 시 develop 최신화 후 분기: `git switch develop && git pull && git switch -c feature/phase<N>-<요약>`.
- main에는 절대 직접 작업하지 않는다.
- **예외:** Phase가 너무 커서(예: Phase 1 Data 레이어는 항목이 많음) PR이 비대해지면 논리 단위로 PR을 2~3개로 쪼갤 수 있다. 이 경우 Discord로 먼저 알린다. 기본은 Phase당 PR 1개.

---

## 2. 커밋 컨벤션

**Conventional Commits + 한국어 설명** (기존 히스토리 스타일 유지).

```
<type>: <한국어 요약>

(필요 시 본문 — 무엇을/왜)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
```

**type 종류:**
| type | 용도 |
|---|---|
| `feat` | 새 기능 |
| `fix` | 버그 수정 |
| `refactor` | 동작 변화 없는 구조 개선 |
| `build` | 빌드/패키지/에셋 임포트 |
| `docs` | 문서(설계·CLAUDE 등) |
| `test` | 테스트 |
| `chore` | 잡무(설정·정리) |
| `style` | 포맷팅 |
| `perf` | 성능 |

**커밋 단위:**
- **백로그 체크박스 항목(모듈) 단위로 잘게 분리.** 하나의 Phase(=PR) 안에서 항목마다 커밋.
  - 예: Phase 3 PR 안에서 `feat: 전투 그리드 시스템 추가` / `feat: Call-Response 상태머신 추가` / `feat: 입력 윈도우 판정 추가` / `feat: 장애물 시스템 추가` / `feat: 보스 페이즈 전환 추가` 를 각각 커밋.
- 각 커밋은 **가능하면 컴파일이 통과하는 상태**로 만든다.
- 요약은 한 줄 50자 내외 권장, 명령형/현재형.

---

## 3. 검증 게이트 (필수)

- **커밋 전: Unity MCP로 컴파일 통과 확인.** (CLAUDE.md §7 워크플로우)
- 컴파일이 깨진 상태로 커밋하지 않는다. (모듈 단위 커밋이 일시적으로 깨질 수밖에 없으면 본문에 명시하고 같은 PR 내에서 곧 해소)

---

## 4. 푸시 / PR 워크플로우

- **단위:** 백로그 **Phase 1개 = feature 브랜치 1개 = PR 1개** (develop 대상). Phase 안의 항목별 커밋들이 이 PR에 모임.
- **푸시 권한:** 사용자가 허가함. **단, 기본값(푸시 전 확인)은 유지** — 푸시·PR 생성 직전 **Discord로 알리고 진행**한다. 묻지 않고 조용히 자동 푸시하지 않는다.
- 완료 조건: 해당 작업 컴파일 검증 통과 → 커밋 정리 → Discord 확인 → `git push` → PR 생성.
- **PR 본문 형식:**
  - 무엇을/왜
  - 관련 백로그 항목 (Implementation_Backlog.md의 Phase·항목)
  - 검증 결과 (컴파일 통과 여부, 플레이모드 테스트 여부)
  - 끝에:
    ```
    🤖 Generated with [Claude Code](https://claude.com/claude-code)
    ```
- PR 병합은 사용자가 검토 후 수행 (출장 중 폰으로 GitHub에서 승인 가능). 자동 병합하지 않는다.

---

## 5. 추적 대상

- `Design/`, `CLAUDE.md` 는 추적·커밋한다 (설계 변경도 `docs:` 커밋).
- Unity 표준 `.gitignore` 적용 중 (Library/·Temp/·UserSettings/ 등 제외). 새 무시 규칙 필요 시 `.gitignore` 갱신.
- `.asset`/`.meta`/`.prefab` 등 Unity 직렬화 파일은 커밋 (에셋 추적 필수).

---

## 6. 요약 흐름

```
develop 최신화
  → feature/phase<N>-<요약> 분기
  → Phase 안의 항목(모듈)별로 커밋 (각 커밋 전 컴파일 검증)
  → Phase 완료 + 검증 통과
  → Discord로 푸시/PR 알림
  → push → PR(develop 대상) 1개 생성
  → 사용자 검토·병합
```
