using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Interactions;

[RequireComponent(typeof(PlayerInput))]
public class InputManager : MonoBehaviour {

    private PlayerInput _playerInput;

    public Vector2 MoveInput { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool SprintPressed { get; private set; }
    public bool SprintHeld { get; private set; }
    public bool CursorLockPressed { get; private set; }

    void Awake() {
        _playerInput = GetComponent<PlayerInput>();
        ServiceLocator.Register<InputManager>(this);
    }

    void OnDestroy() {
        ServiceLocator.Unregister<InputManager>();
    }

    void OnEnable() {
        _playerInput.actions["Move"].performed += OnMove;
        _playerInput.actions["Move"].canceled += OnMove;

        _playerInput.actions["Jump"].started += OnJump;
        _playerInput.actions["Jump"].performed += OnJump;
        _playerInput.actions["Jump"].canceled += OnJump;

        _playerInput.actions["Sprint"].started += OnSprint;
        _playerInput.actions["Sprint"].performed += OnSprint;
        _playerInput.actions["Sprint"].canceled += OnSprint;

        _playerInput.actions["CursorLock"].performed += OnCursorLock;
    }

    void OnDisable() {
        _playerInput.actions["Move"].performed -= OnMove;
        _playerInput.actions["Move"].canceled -= OnMove;

        _playerInput.actions["Jump"].started -= OnJump;
        _playerInput.actions["Jump"].performed -= OnJump;
        _playerInput.actions["Jump"].canceled -= OnJump;
        
        _playerInput.actions["Sprint"].started -= OnSprint;
        _playerInput.actions["Sprint"].performed -= OnSprint;
        _playerInput.actions["Sprint"].canceled -= OnSprint;

        _playerInput.actions["CursorLock"].performed -= OnCursorLock;
    }

    void OnMove(InputAction.CallbackContext context) {
        if (context.performed) {
            MoveInput = context.ReadValue<Vector2>();
        } else if (context.canceled) {
            MoveInput = Vector2.zero;
        }
    }

    void OnJump(InputAction.CallbackContext context) {
        if (context.performed) JumpPressed = true;
        else if (context.canceled) JumpPressed = false;
    }

    void OnSprint(InputAction.CallbackContext context) {
        if (context.started) SprintPressed = true;
        else if (context.performed && context.interaction is HoldInteraction) SprintHeld = true;
        else if (context.canceled) {
            SprintPressed = false;
            SprintHeld = false;
        }
    }

    void OnCursorLock(InputAction.CallbackContext context) {
        if (context.performed) CursorLockPressed = true;
    }

}
