using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BeatHero.Core
{
    // New Input System 액션을 이벤트로 노출하는 래퍼. 게임플레이 시스템은 이 클래스만 구독.
    public class InputReader : MonoBehaviour, InputSystem_Actions.IPlayerActions
    {
        public event Action<Vector2> OnMoveInput;
        public event Action OnAttackPressed;
        public event Action OnAttackReleased;

        private InputSystem_Actions _actions;

        private void Awake()
        {
            _actions = new InputSystem_Actions();
            _actions.Player.SetCallbacks(this);
        }

        private void OnEnable() => _actions.Player.Enable();
        private void OnDisable() => _actions.Player.Disable();

        void InputSystem_Actions.IPlayerActions.OnMove(InputAction.CallbackContext ctx)
        {
            OnMoveInput?.Invoke(ctx.ReadValue<Vector2>());
        }

        void InputSystem_Actions.IPlayerActions.OnAttack(InputAction.CallbackContext ctx)
        {
            if (ctx.started) OnAttackPressed?.Invoke();
            if (ctx.canceled) OnAttackReleased?.Invoke();
        }

        // 사용하지 않는 액션 — 빈 구현으로 인터페이스 충족
        void InputSystem_Actions.IPlayerActions.OnLook(InputAction.CallbackContext ctx) { }
        void InputSystem_Actions.IPlayerActions.OnInteract(InputAction.CallbackContext ctx) { }
        void InputSystem_Actions.IPlayerActions.OnCrouch(InputAction.CallbackContext ctx) { }
        void InputSystem_Actions.IPlayerActions.OnJump(InputAction.CallbackContext ctx) { }
        void InputSystem_Actions.IPlayerActions.OnPrevious(InputAction.CallbackContext ctx) { }
        void InputSystem_Actions.IPlayerActions.OnNext(InputAction.CallbackContext ctx) { }
        void InputSystem_Actions.IPlayerActions.OnSprint(InputAction.CallbackContext ctx) { }
    }
}
