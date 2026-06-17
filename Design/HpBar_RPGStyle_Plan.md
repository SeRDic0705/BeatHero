# RPG식 테두리 체력바 계획

## 요구
GameScene의 PlayerHP/MonsterHP를 Slider(Handle 드래그식)에서 **테두리 있는 RPG식 체력바**로 변경.

## 확정 결정
1. **방법 B — Image.fillAmount** 사용(Slider 제거). CombatHUD 코드 수정.
2. 테두리 프레임 에셋은 아직 미import → **픽셀아트 9-slice 플레이스홀더** 생성(아티스트 교체 전제).
3. HP 숫자(현재/최대) 텍스트 가운데 표시.
4. 색상: 플레이어 초록 / 몬스터 빨강.

## 구조 (각 바)
- Root(RectTransform)
  - `Background` Image — 어두운 빈 바(소진 영역)
  - `Fill` Image — type=Filled, Horizontal, Origin Left, 색(초록/빨강), fillAmount = HP/Max
  - `Border` Image — 9-slice 프레임(중앙 투명), raycastTarget off, 최상단
  - `HPText` TMP_Text — "현재/최대" 가운데

## 코드 — CombatHUD.cs
- `_playerHpSlider`(Slider) → `_playerHpFill`(Image) + `_playerHpText`(TMP_Text)
- `_monsterHpSlider`(Slider) → `_monsterHpFill`(Image) + `_monsterHpText`(TMP_Text)
- `UpdatePlayerHp/UpdateMonsterHp`: `fill.fillAmount = max>0 ? (float)current/max : 0; text = current/max`.

## 씬 작업 (GameScene)
- 기존 PlayerHPSlider/MonsterHPSlider에서 Slider + Handle Slide Area 제거.
- Background/Fill 재구성(Fill = Filled Image), Border 프레임 + HPText 추가.
- CombatHUD의 새 필드(`_playerHpFill/_playerHpText`, `_monsterHpFill/_monsterHpText`)에 와이어링.

## 검증
- MCP 컴파일(에러 0). 플레이모드에서 fillAmount/텍스트 갱신 확인.
