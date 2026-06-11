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
        public event Action OnAttackPressed;
        public event Action OnAttackReleased;
        public event Action OnPausePressed;

        private InputSystem_Actions _actions;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _actions = new InputSystem_Actions();
            _actions.Game.SetCallbacks(this);
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

        void InputSystem_Actions.IGameActions.OnAttack(InputAction.CallbackContext ctx)
        {
            if (ctx.started) OnAttackPressed?.Invoke();
            if (ctx.canceled) OnAttackReleased?.Invoke();
        }

        void InputSystem_Actions.IGameActions.OnPause(InputAction.CallbackContext ctx)
        {
            if (ctx.started) OnPausePressed?.Invoke();
        }
    }
}
