using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Interactions;

[RequireComponent(typeof(CollisionCheck))]
public class PlayerMovement : MonoBehaviour {

    #region Fields

    [Header("Movement Forces")]
    [SerializeField] private float _acceleration = 9f;
    [field: SerializeField] public float BaseSpeed { get; private set; } = 7f;
    [field: SerializeField] public float MaxSpeed { get; set; } = 7f;
    [field: SerializeField] public float DampenedSpeed { get; set; } = 5f;
    [field: SerializeField] public float SprintMaxSpeed { get; private set; } = 10f;
    [SerializeField] private float _sprintForce = 10f;
    [field: SerializeField, Range(30f, 100f)] public float BaseMomentum { get; private set; } = 60f;
    [field: SerializeField, Range(30f, 100f)] public float Momentum { get; set; } = 60f;
    [field: SerializeField, Range(30f, 100f)] public float NonSlideMomentum { get; set; } = 80f;

    [Header("Dash Settings")]
    public bool DashHasGravity = true;
    [SerializeField] private float _dashDistance = 5f;
    [field: SerializeField] public int DashAmount { get; private set; } = 1;
    private int _dashCounter = 0;

    [Header("Jump Settings")]
    [SerializeField] private float _jumpForce = 8f;
    [SerializeField] private float _jumpHoldForce = 3f;
    [SerializeField] private float _jumpHoldDuration = 0.25f;
    public bool CanHoldJump = false;
    public bool CanDoubleJump = false;
    private int _jumpAmount = 1;
    private int _jumpCounter = 0; // can add more jumps in future if needed
    private bool _isJumping;
    private float _jumpHoldTimer;
    private bool _isJumpHeld;
    private float _decayJumpForce;

    public bool ZeroVelocityOnDoubleJump = false;
    [SerializeField] private float _jumpBufferTime = 0.15f;
    private float _jumpBufferCounter = 0f;

    [Header("Misc Settings")]
    public bool FollowCameraRotation = false;
    [SerializeField] private float _coyoteTime = 0.15f;
    [SerializeField] private float _jumpMultiplier = 0.5f;

    private Rigidbody _rb;
    private PlayerInput _playerInput;
    private CollisionCheck _collisionCheck;
    private Vector3 _dashStartPos;
    private Vector2 _moveDirection;
    private bool _cancelledMovement = false;
    public bool IsMoving => _moveDirection != Vector2.zero;
    private bool _isSprinting = false;
    private bool _isDashing = false;
    private float _lastGroundedTime = 0f;
    private bool _inCoyote = false;

    // caches
    private Transform _playerTransform;
    private Transform _cameraTransform;

    #endregion

    void Awake() {
        _playerInput = GetComponent<PlayerInput>();
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
    }

    void Start() {
        _rb = GetComponent<Rigidbody>();
        _collisionCheck = GetComponent<CollisionCheck>();
        _playerTransform = transform;
        _cameraTransform = Camera.main.transform;
    }

    void FixedUpdate() {
        UpdateJump();
        UpdateDash();
        MovementLogic();
        if (_cancelledMovement) StopMovement();
        HandleJumpHoldForce();
    }

    void OnMove(InputAction.CallbackContext context) {
        if (context.canceled) _cancelledMovement = true;
        else {
            _cancelledMovement = false;
            _moveDirection = context.ReadValue<Vector2>();
        }
    }

    void StopMovement() {
        if (_moveDirection.magnitude > 0.01f) {
            _moveDirection = Vector2.Lerp(_moveDirection, Vector2.zero, Momentum * Time.fixedDeltaTime);
        } else {
            _moveDirection = Vector2.zero;
            _cancelledMovement = false;
        }
    }

    void OnJump(InputAction.CallbackContext context) {
        if (context.started) _jumpBufferCounter = _jumpBufferTime;
        if (context.performed) {
            _isJumpHeld = true;
            if (_collisionCheck.IsGrounded) {
                ApplyJumpForce(_jumpForce);
                StartJumpHold();
            } else {
                HandleCoyote();
                if (CanDoubleJump) HandleDoubleJump();
            }
        }
        if (context.canceled) {
            _isJumpHeld = false;
        }
    }

    void StartJumpHold() {
        _isJumping = true;
        _jumpHoldTimer = 0f;
        _decayJumpForce = _jumpHoldForce;
    }

    void HandleJumpHoldForce() {
        if (!CanHoldJump || !_isJumping || !_isJumpHeld) return;

        if (_jumpHoldTimer < _jumpHoldDuration) {
            _jumpHoldTimer += Time.fixedDeltaTime;
            float t = _jumpHoldTimer / _jumpHoldDuration;
            float curvedT = math.sin(t * math.PI);
            float currentForce = _jumpHoldForce * curvedT;
            _rb.AddForce(Vector3.up * currentForce, ForceMode.VelocityChange);
        } else {
            _isJumping = false;
        }
    }

    void OnSprint(InputAction.CallbackContext context) {
        if (context.started) PerformDash();

        if (context.performed && context.interaction is HoldInteraction) {
            _isSprinting = true;
        }

        if (context.canceled) {
            _isSprinting = false;

            if (_moveDirection == Vector2.zero) {
                _cancelledMovement = true;
            }
        }
    }

    void MovementLogic() {
        Vector3 inputDir = new Vector3(_moveDirection.x, 0, _moveDirection.y).normalized;
        float horizontalSpeed = new Vector3(_rb.linearVelocity.x, 0, _rb.linearVelocity.z).magnitude;

        if (FollowCameraRotation) {
            inputDir = TranformToCameraRotation().normalized;
        }

        float targetMaxSpeed = _isSprinting ? SprintMaxSpeed : MaxSpeed;
        if (horizontalSpeed < targetMaxSpeed) {
            if (_collisionCheck.TryGetGroundNormal(out Vector3 normal)) {
                Vector3 slopeDir = Vector3.ProjectOnPlane(inputDir, normal).normalized;
                float adjustForce = (targetMaxSpeed - horizontalSpeed) * _acceleration;
                _rb.AddForce(slopeDir * adjustForce, ForceMode.Acceleration);
            } else {
                _rb.AddForce(inputDir * _acceleration, ForceMode.Acceleration);
            }
        }

        if (inputDir != Vector3.zero) {
            Quaternion targetRotation = Quaternion.LookRotation(inputDir, Vector3.up);
            _playerTransform.rotation = Quaternion.Slerp(_playerTransform.rotation, targetRotation, 10f * Time.fixedDeltaTime);
        }
    }

    void PerformDash() {
        if (_dashCounter >= DashAmount) return;
        if (!_collisionCheck.IsGrounded) _dashCounter++;

        Vector3 inputDir;

        if (_moveDirection != Vector2.zero) {
            inputDir = new Vector3(_moveDirection.x, 0, _moveDirection.y).normalized;

            if (FollowCameraRotation) {
                inputDir = TranformToCameraRotation().normalized;
            }
        } else {
            inputDir = _playerTransform.forward;
        }

        float horizontalSpeed = new Vector3(_rb.linearVelocity.x, 0, _rb.linearVelocity.z).magnitude;
        if (horizontalSpeed < SprintMaxSpeed) {
            if (!DashHasGravity) {
                _rb.useGravity = false;
                _isDashing = true;
                _dashStartPos = _playerTransform.position;
                Vector3 velocity = _rb.linearVelocity;
                velocity.y = 0;
                _rb.linearVelocity = velocity;

            }
            _rb.AddForce(inputDir * _sprintForce, ForceMode.VelocityChange);
        }
    }

    void HandleCoyote() {
        if (Time.time - _lastGroundedTime <= _coyoteTime) {
            Vector3 velocty = _rb.linearVelocity;
            if (velocty.y < 0) {
                ApplyJumpForce(_jumpForce);
                _inCoyote = true;
            }
        }
    }

    void HandleDoubleJump() {
        if (_jumpCounter >= _jumpAmount) return;
        if (ZeroVelocityOnDoubleJump) {
            Vector3 velocty = _rb.linearVelocity;
            velocty.y = 0f;
            _rb.linearVelocity = velocty;
        }
        ApplyJumpForce(_jumpForce * _jumpMultiplier);
        if (!_inCoyote) { _jumpCounter++; }
        else { _inCoyote = false; }
    }

    void UpdateJump() {
        if (_jumpBufferCounter > 0) {
            _jumpBufferCounter -= Time.fixedDeltaTime;
        }
        
        if (_collisionCheck.IsGrounded) {
            _lastGroundedTime = Time.time;
            _jumpCounter = 0;
            _inCoyote = false;

            if (_jumpBufferCounter > 0) {
                ApplyJumpForce(_jumpForce);
                _jumpBufferCounter = 0f;
            }
        }
    }

    void UpdateDash() {
        if (_dashCounter != 0 && _collisionCheck.IsGrounded) _dashCounter = 0;
        if (_isDashing && !DashHasGravity) {
            float dashTravelled = Vector3.Distance(_dashStartPos, _playerTransform.position);
            if (dashTravelled > _dashDistance) {
                _rb.useGravity = true;
                _isDashing = false;
            } else {
                if (_collisionCheck.IsGrounded || _collisionCheck.HasCollided) {
                    _rb.useGravity = true;
                    _isDashing = false;
                }
            }
        }
    }

    // helpers
    Vector3 TranformToCameraRotation() {
        Vector3 camForward = _cameraTransform.forward;
        Vector3 camRight = _cameraTransform.right;
        camForward.y = 0;
        camRight.y = 0;
        camForward.Normalize();
        camRight.Normalize();
        return camForward * _moveDirection.y + camRight * _moveDirection.x;
    }

    void ApplyJumpForce(float force) {
        _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, force, _rb.linearVelocity.z);
    }

    public bool HasMoveInput() {
        return _moveDirection.sqrMagnitude > 0.01f;
    }
}