using System.Collections.Generic;
using BeatHero.Data;
using TMPro;
using UnityEngine;

namespace BeatHero.Combat
{
    // 몬스터 말풍선(월드공간). CallPhase 동안 전투 대사 표시, ResponsePhase에 숨김.
    // 사망 대사는 GameManager가 사망 시점에 ShowDeath()로 표시, 다음 층 OnBattleStarted에서 숨김.
    // 선택 규칙(기획서): CallPhase 순번 기준 순차 출력 → 소진 후 랜덤 출력(연속중복 방지 항상 적용).
    public class MonsterSpeechBubble : MonoBehaviour
    {
        [SerializeField] private GameObject _root; // 말풍선 비주얼 루트(SetActive 토글)
        [SerializeField] private TMP_Text   _text;

        private BattleStateMachine _battle;
        private MonsterData        _monster;

        private readonly List<MonsterDialogueLine> _sequential = new();
        private readonly List<MonsterDialogueLine> _randomPool = new();
        private readonly List<MonsterDialogueLine> _pickBuffer = new(); // 연속중복 제외용 재사용 버퍼
        private int                 _callIndex;
        private MonsterDialogueLine _lastShown;

        private void Awake()
        {
            _battle = Object.FindAnyObjectByType<BattleStateMachine>();
            if (_battle != null)
            {
                _battle.OnBattleStarted        += OnBattleStarted;
                _battle.OnCallPhaseStarted     += OnCallPhaseStarted;
                _battle.OnResponsePhaseStarted += Hide;
            }
            Hide();
        }

        private void Start()
        {
            if (_battle != null && _battle.CurrentMonster != null)
                OnBattleStarted(_battle.CurrentMonster);
        }

        private void OnDestroy()
        {
            if (_battle != null)
            {
                _battle.OnBattleStarted        -= OnBattleStarted;
                _battle.OnCallPhaseStarted     -= OnCallPhaseStarted;
                _battle.OnResponsePhaseStarted -= Hide;
            }
        }

        // 새 몬스터(층) 시작 — 대사 리스트 파생 + 순번/직전대사 리셋 + 숨김(직전 사망 말풍선 정리).
        private void OnBattleStarted(MonsterData data)
        {
            _monster   = data;
            _callIndex = 0;
            _lastShown = null;
            _sequential.Clear();
            _randomPool.Clear();
            if (data?.combatDialogues != null)
            {
                foreach (var line in data.combatDialogues)
                {
                    if (line == null || string.IsNullOrEmpty(line.text)) continue;
                    if (line.useInSequential) _sequential.Add(line);
                    if (line.useInRandom)     _randomPool.Add(line);
                }
            }
            Hide();
        }

        private void OnCallPhaseStarted()
        {
            var line = SelectLine();
            _callIndex++;
            if (line == null) { Hide(); return; }
            _lastShown = line;
            Show(line.text);
        }

        private MonsterDialogueLine SelectLine()
        {
            // 1단계: 순차 — n번째 CallPhase = n번 순차 대사.
            if (_callIndex < _sequential.Count) return _sequential[_callIndex];

            // 2단계: 랜덤 — 순차 소진 후.
            if (_randomPool.Count == 0) return null;
            // 연속중복 방지: 후보가 2개 이상일 때만 직전 대사 제외(1개뿐이면 연속 허용).
            if (_randomPool.Count > 1 && _lastShown != null)
            {
                _pickBuffer.Clear();
                foreach (var l in _randomPool)
                    if (l != _lastShown) _pickBuffer.Add(l);
                return _pickBuffer[Random.Range(0, _pickBuffer.Count)];
            }
            return _randomPool[Random.Range(0, _randomPool.Count)];
        }

        // 사망 시점에 GameManager가 호출. 사망 대사 있으면 표시(true), 없으면 미표시(false).
        public bool ShowDeath()
        {
            if (_monster == null || string.IsNullOrEmpty(_monster.deathDialogue))
            {
                Hide();
                return false;
            }
            Show(_monster.deathDialogue);
            return true;
        }

        private void Show(string content)
        {
            if (_text != null) _text.text = content;
            if (_root != null) _root.SetActive(true);
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }
    }
}
