using BeatHero.Combat;
using BeatHero.Core;
using BeatHero.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BeatHero.UI
{
    public class CombatHUD : MonoBehaviour
    {
        [Header("Player")]
        [SerializeField] private Slider _playerHpSlider;
        [SerializeField] private Image[] _manaDots; // 5

        [Header("Monster")]
        [SerializeField] private Slider _monsterHpSlider;

        [Header("Floor")]
        [SerializeField] private TMP_Text _floorText;

        [Header("Beat")]
        [SerializeField] private BeatBar _beatBar;

        private PlayerController _player;
        private BattleStateMachine _battle;
        private Conductor _conductor;

        private void Awake()
        {
            var player = Object.FindAnyObjectByType<PlayerController>();
            if (player != null) BindPlayer(player);

            var battle = Object.FindAnyObjectByType<BattleStateMachine>();
            if (battle != null) BindBattle(battle);

            var conductor = Object.FindAnyObjectByType<Conductor>();
            if (conductor != null) BindConductor(conductor);

            if (battle != null) RebindBeatBarBattle(battle);
        }

        private void Start()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnFloorChanged += UpdateFloor;
                UpdateFloor(GameManager.Instance.CurrentFloor);
            }
        }

        private void OnDestroy()
        {
            if (_player != null)
            {
                _player.OnHpChanged   -= UpdatePlayerHp;
                _player.OnManaChanged -= UpdateMana;
            }
            if (_battle != null)
                _battle.OnMonsterHpChanged -= UpdateMonsterHp;
            if (GameManager.Instance != null)
                GameManager.Instance.OnFloorChanged -= UpdateFloor;
        }

        public void BindPlayer(PlayerController player)
        {
            if (_player != null)
            {
                _player.OnHpChanged   -= UpdatePlayerHp;
                _player.OnManaChanged -= UpdateMana;
            }
            _player = player;
            _player.OnHpChanged   += UpdatePlayerHp;
            _player.OnManaChanged += UpdateMana;
            UpdatePlayerHp(_player.Hp, _player.MaxHp);
            UpdateMana(_player.Mana);
        }

        public void BindBattle(BattleStateMachine battle)
        {
            if (_battle != null)
                _battle.OnMonsterHpChanged -= UpdateMonsterHp;
            _battle = battle;
            _battle.OnMonsterHpChanged += UpdateMonsterHp;
        }

        public void BindConductor(Conductor conductor)
        {
            _conductor = conductor;
            if (_beatBar != null)
                _beatBar.Bind(conductor);
        }

        public void RebindBeatBarBattle(BattleStateMachine battle)
        {
            if (_beatBar != null)
                _beatBar.BindBattle(battle);
        }

        private void UpdatePlayerHp(int current, int max)
        {
            if (_playerHpSlider == null) return;
            _playerHpSlider.maxValue = max;
            _playerHpSlider.value    = current;
        }

        private void UpdateMonsterHp(int current, int max)
        {
            if (_monsterHpSlider == null) return;
            _monsterHpSlider.maxValue = max;
            _monsterHpSlider.value    = current;
        }

        private void UpdateMana(int mana)
        {
            if (_manaDots == null) return;
            for (int i = 0; i < _manaDots.Length; i++)
                _manaDots[i].color = i < mana ? Color.yellow : new Color(1f, 1f, 1f, 0.2f);
        }

        private void UpdateFloor(int floor)
        {
            if (_floorText != null) _floorText.text = $"Floor {floor}";
        }
    }
}
