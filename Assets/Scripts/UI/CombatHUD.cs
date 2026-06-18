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
        [SerializeField] private Image    _playerHpFill;     // type=Filled(Horizontal)
        [SerializeField] private TMP_Text _playerHpText;     // "현재/최대"
        [SerializeField] private TMP_Text _manaCurrentText;  // 현재 마나
        [SerializeField] private TMP_Text _manaMaxText;      // "/최대마나"

        [Header("Monster")]
        [SerializeField] private Image    _monsterHpFill;
        [SerializeField] private TMP_Text _monsterHpText;

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
            if (_playerHpFill != null)
                _playerHpFill.fillAmount = max > 0 ? (float)current / max : 0f;
            if (_playerHpText != null)
                _playerHpText.text = $"{current}/{max}";
        }

        private void UpdateMonsterHp(int current, int max)
        {
            if (_monsterHpFill != null)
                _monsterHpFill.fillAmount = max > 0 ? (float)current / max : 0f;
            if (_monsterHpText != null)
                _monsterHpText.text = $"{current}/{max}";
        }

        private void UpdateMana(int mana)
        {
            if (_manaCurrentText != null) _manaCurrentText.text = mana.ToString();
            if (_manaMaxText != null && _player != null) _manaMaxText.text = "/" + _player.MaxMana;
        }

        private void UpdateFloor(int floor)
        {
            if (_floorText != null) _floorText.text = $"Floor {floor}";
        }
    }
}
