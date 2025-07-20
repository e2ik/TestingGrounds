using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Interactions;

[RequireComponent(typeof(PlayerInput))]
public class InputManager : MonoBehaviour {

    private PlayerInput _playerInput;
    [SerializeField] private CinemachineCamera _freeLookCam;
    private CinemachineInputAxisController _axisController;
    private CinemachineFollowZoom _followZoom;
    [SerializeField, Range(0, 5)] private float _mouseSensitivity = 1;
    private float _lastMouseSensitivity;
    [SerializeField] private bool invertSensitivity = false;
    private float _zoomIncrement = 1.1f;
    private float _minFOV = 40f;
    private float _maxFOV = 80f;
    private float _currentFOV = 60f;

    public Vector2 MoveInput { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool SprintPressed { get; private set; }
    public bool SprintHeld { get; private set; }
    public bool CursorLockPressed { get; private set; }
    public bool ZoomHeld { get; private set; }

    private float _zoomInput;
    private string _lastZoomDevice;

    void Awake() {
        _playerInput = GetComponent<PlayerInput>();
        ServiceLocator.Register<InputManager>(this);
    }

    void Start() {
        _axisController = _freeLookCam.GetComponent<CinemachineInputAxisController>();
        if (_axisController != null) {
            UpdateLookSensitivity(_mouseSensitivity);
        } else {
            Debug.LogWarning("CinemachineInputAxisController not found on " + _freeLookCam.name);
        }

        // TODO: Hook this up to allow zooming
        _followZoom = _freeLookCam.GetComponent<CinemachineFollowZoom>();
        if (_followZoom != null) {
            _followZoom.FovRange = new Vector2(_currentFOV, _currentFOV);
        }

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
        _playerInput.actions["CursorLock"].canceled += OnCursorLock;

        _playerInput.actions["Zoom"].performed += OnZoom;
        _playerInput.actions["Zoom"].canceled += OnZoom;
        _playerInput.actions["ZoomHeld"].performed += OnZoomHeld;
        _playerInput.actions["ZoomHeld"].canceled += OnZoomHeld;
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
        _playerInput.actions["CursorLock"].canceled -= OnCursorLock;

        _playerInput.actions["Zoom"].performed -= OnZoom;
        _playerInput.actions["Zoom"].canceled -= OnZoom;
        _playerInput.actions["ZoomHeld"].performed -= OnZoomHeld;
        _playerInput.actions["ZoomHeld"].canceled -= OnZoomHeld;
    }

    void Update() {
        if (!Mathf.Approximately(_mouseSensitivity, _lastMouseSensitivity)) {
            UpdateLookSensitivity(_mouseSensitivity);
            _lastMouseSensitivity = _mouseSensitivity;
        }

        if (Mathf.Abs(_zoomInput) > 0.1f) {
            float clampedInput = Mathf.Sign(_zoomInput);
            float zoomIncrement = (_lastZoomDevice != "Mouse") ? _zoomIncrement * 0.1f : _zoomIncrement;

            float newFOV = Mathf.Clamp(_currentFOV + zoomIncrement * clampedInput, _minFOV, _maxFOV);
            _currentFOV = newFOV;

            if (ZoomHeld || _lastZoomDevice == "Mouse")
                _followZoom.FovRange = new Vector2(newFOV, newFOV);

            bool usingController = _lastZoomDevice != "Mouse";
            _axisController.enabled = !(usingController && ZoomHeld);
        }
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
        else if (context.canceled) CursorLockPressed = false;
    }

    void OnZoom(InputAction.CallbackContext context) {
        _zoomInput = -_playerInput.actions["Zoom"].ReadValue<float>();
        var device = context.control.device;
        _lastZoomDevice = device.name;
    }

    void OnZoomHeld(InputAction.CallbackContext context) {
        if (context.performed) {
            ZoomHeld = true;
        }
        else if (context.canceled) {
            ZoomHeld = false;
        }
    }

    void UpdateLookSensitivity(float sensitivity) {
        if (_axisController != null) {
            foreach (var axis in _axisController.Controllers) {
                if (axis.Name == "Look Orbit X") {
                    axis.Input.Gain = sensitivity;
                }
                if (axis.Name == "Look Orbit Y") {
                    float checkInvert = invertSensitivity ? sensitivity : -sensitivity;
                    axis.Input.Gain = checkInvert;
                }
            }
        }
    }

}
