using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BeatHero.Core
{
    // New Input System 액션을 이벤트로 노출하는 래퍼. 게임플레이 시스템은 이 클래스만 구독.
    public class InputReader : MonoBehaviour, InputSystem_Actions.IGameActions
    {
        public static InputReader Instance { get; private set; }

        public event Action<Vector2> OnMoveInput;
        public event Action OnBasicAttackPressed;
        public event Action OnChargeAttackPressed;
        public event Action OnChargeAttackReleased;
        public event Action OnBlockPressed;
        public event Action OnPausePressed;

        private InputSystem_Actions _actions;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _actions = new InputSystem_Actions();
            _actions.Game.SetCallbacks(this);
            _actions.Game.Enable();
        }

        private void OnDestroy() => _actions?.Disable();

        public void SwitchToGameMap()
        {
            _actions.UI.Disable();
            _actions.Game.Enable();
        }

        public void SwitchToUIMap()
        {
            _actions.Game.Disable();
            _actions.UI.Enable();
        }

        void InputSystem_Actions.IGameActions.OnMove(InputAction.CallbackContext ctx)
            => OnMoveInput?.Invoke(ctx.ReadValue<Vector2>());

        void InputSystem_Actions.IGameActions.OnBasicAttack(InputAction.CallbackContext ctx)
        {
            if (ctx.started) OnBasicAttackPressed?.Invoke();
        }

        void InputSystem_Actions.IGameActions.OnChargeAttack(InputAction.CallbackContext ctx)
        {
            if (ctx.started) OnChargeAttackPressed?.Invoke();
            if (ctx.canceled) OnChargeAttackReleased?.Invoke();
        }

        void InputSystem_Actions.IGameActions.OnBlock(InputAction.CallbackContext ctx)
        {
            if (ctx.started) OnBlockPressed?.Invoke();
        }

        void InputSystem_Actions.IGameActions.OnPause(InputAction.CallbackContext ctx)
        {
            if (ctx.started) OnPausePressed?.Invoke();
        }
    }
}
