using Unity.Mathematics;
using UnityEngine;

[RequireComponent(typeof(CollisionCheck))]
public class PlayerMovement : MonoBehaviour {

    #region Fields

    [Header("Movement Forces")]
    [SerializeField] private float _acceleration = 9f;
    [SerializeField] private float _airAcceleration = 3f;
    [field: SerializeField] public float BaseSpeed { get; private set; } = 7f;
    [field: SerializeField] public float MaxSpeed { get; set; } = 7f;
    [field: SerializeField] public float SprintMaxSpeed { get; private set; } = 10f;
    [SerializeField] private float _sprintForce = 10f;
    [SerializeField] private float _brakingForce = 10f;
    [SerializeField] private float _turnSharpness = 10f;
    [SerializeField] private float _airTurnSharpness = 3f;

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

    public bool ZeroVelocityOnDoubleJump = false;
    [SerializeField] private float _jumpBufferTime = 0.15f;
    private float _jumpBufferCounter = 0f;

    [Header("Misc Settings")]
    public bool FollowCameraRotation = false;
    public bool AllowSlidingOnLanding = false;
    [SerializeField] private float _coyoteTime = 0.15f;
    [SerializeField] private float _jumpMultiplier = 0.8f;
    [SerializeField] private float _customGravity = 9.81f;
    private bool _useCustomGravity = true;
    private bool _wasGroundedLastFrame = false;

    private Rigidbody _rb;
    private CollisionCheck _collisionCheck;
    private Vector3 _dashStartPos;
    public bool IsMoving => _moveDirection != Vector2.zero;
    private bool _isSprinting = false;
    private bool _isDashing = false;
    private float _lastGroundedTime = 0f;
    private bool _inCoyote = false;

    // Inputs
    private InputManager _input;
    private Vector2 _moveDirection;
    private bool _isJumpPressed;
    private bool _wasJumpPressedLastFrame;
    private bool _isJumpHeld;
    private bool _isSprintPressed;
    private bool _wasSprintPressedLastFrame;
    private bool _isSprintHeld;


    // caches
    private Transform _playerTransform;
    private Transform _cameraTransform;

    #endregion

    void Start() {
        _input = ServiceLocator.Get<InputManager>();
        _rb = GetComponent<Rigidbody>();
        _collisionCheck = GetComponent<CollisionCheck>();
        _playerTransform = transform;
        _cameraTransform = Camera.main.transform;
    }

    void Update() {
        MapAndHandleInputs();
    }

    void FixedUpdate() {
        CheckForLastLandedFrame();
        ApplyCustomGravity();
        UpdateJump();
        UpdateDash();
        MovementLogic();
        HandleJumpHoldForce();
    }

    void MapAndHandleInputs() {
        _moveDirection = _input.MoveInput;
        _isJumpPressed = _input.JumpPressed;
        _isSprintPressed = _input.SprintPressed;
        _isSprintHeld = _input.SprintHeld;

        if (_isJumpPressed && !_wasJumpPressedLastFrame) OnJump();
        _wasJumpPressedLastFrame = _isJumpPressed;
        _isJumpHeld = _isJumpPressed;

        if (_isSprintPressed && !_wasSprintPressedLastFrame) PerformDash();
        _wasSprintPressedLastFrame = _isSprintPressed;
        _isSprinting = _isSprintHeld;
    }

    void ApplyCustomGravity() {
        if (!_useCustomGravity) return;
        if (!_collisionCheck.IsGrounded || _collisionCheck.HasCollided) {
            float verticalVelocity = _rb.linearVelocity.y;
            float gravityMultiplier = verticalVelocity < 0 ? math.clamp(1f + math.abs(verticalVelocity), 1f, 2f) : 1f;
            _rb.AddForce(Vector3.down * _customGravity * gravityMultiplier, ForceMode.Acceleration);
        } else if (_collisionCheck.IsGrounded && _collisionCheck.TryGetGroundNormal(out Vector3 normal)) {
            Vector3 velocity = _rb.linearVelocity;
            float verticalIntoGround = Vector3.Dot(velocity, normal);

            if (verticalIntoGround < 0.5f) {
                velocity -= normal * verticalIntoGround;
                _rb.linearVelocity = velocity;
            }
        }
    }

    void OnJump() {
        _jumpBufferCounter = _jumpBufferTime;
        _isJumpHeld = true;
        if (_collisionCheck.IsGrounded) {
            ApplyJumpForce(_jumpForce);
            StartJumpHold();
        } else {
            HandleCoyote();
            if (CanDoubleJump) HandleDoubleJump();
        }
    }

    void StartJumpHold() {
        _isJumping = true;
        _jumpHoldTimer = 0f;
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

    void HandleCoyote() {
        if (Time.time - _lastGroundedTime <= _coyoteTime &&
            _rb.linearVelocity.y < 0 &&
            _isJumpHeld) 
        {
            StartJumpHold();
            _inCoyote = true;
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
        if (_isJumpHeld) StartJumpHold();
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
            if (!_collisionCheck.IsGrounded) _rb.AddForce(inputDir * _airAcceleration, ForceMode.Acceleration);
        }

        if (_collisionCheck.IsGrounded) {
            if (_collisionCheck.TryGetGroundNormal(out Vector3 normal)) {
                Vector3 velocity = _rb.linearVelocity;
                Vector3 horizontalVelocity = Vector3.ProjectOnPlane(velocity, normal);

                if (inputDir == Vector3.zero) {
                    float speed = horizontalVelocity.magnitude;
                    if (speed > 0.5f) {
                        float _checkBrakeForce = _isDashing ? 1f : _brakingForce;
                        Vector3 newVelocity = horizontalVelocity * _checkBrakeForce;
                        Vector3 verticalVelocity = Vector3.Project(velocity, normal);
                        _rb.linearVelocity = newVelocity + verticalVelocity;
                    } else {
                        Vector3 verticalVelocity = Vector3.Project(velocity, normal);
                        _rb.linearVelocity = verticalVelocity;
                        _rb.linearVelocity = Vector3.Project(_rb.linearVelocity, normal);
                    }
                } else {
                    Vector3 desiredVelocity = Vector3.ProjectOnPlane(inputDir, normal).normalized * horizontalVelocity.magnitude;
                    Vector3 turnCorrection = desiredVelocity - horizontalVelocity;
                    _rb.AddForce(turnCorrection * _turnSharpness, ForceMode.Acceleration);
                }
            }
        } else {
            if (inputDir != Vector3.zero) {
                Vector3 velocity = _rb.linearVelocity;
                Vector3 horizontalVelocity = new Vector3(velocity.x, 0, velocity.z);

                Vector3 desiredVelocity = inputDir.normalized * horizontalVelocity.magnitude;
                Vector3 turnCorrection = desiredVelocity - horizontalVelocity;

                _rb.AddForce(turnCorrection * _airTurnSharpness, ForceMode.Acceleration);
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
                _useCustomGravity = false;
                _isDashing = true;
                _dashStartPos = _playerTransform.position;
                Vector3 velocity = _rb.linearVelocity;
                velocity.y = 0;
                _rb.linearVelocity = velocity;

            }
            _rb.AddForce(inputDir * _sprintForce, ForceMode.VelocityChange);
        }
    }

    void UpdateDash() {
        if (_dashCounter != 0 && _collisionCheck.IsGrounded) _dashCounter = 0;
        if (_isDashing && !DashHasGravity) {
            float dashTravelled = Vector3.Distance(_dashStartPos, _playerTransform.position);
            if (dashTravelled > _dashDistance) {
                _useCustomGravity = true;
                _isDashing = false;
            }
        }
        if (_collisionCheck.HasCollided) {
            _useCustomGravity = true;
            _isDashing = false;
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
        Vector3 velocity = _rb.linearVelocity;
        velocity.y = 0f;
        _rb.linearVelocity = velocity;
        _rb.AddForce(Vector3.up * force, ForceMode.VelocityChange);
    }

    public bool HasMoveInput() {
        return _moveDirection.sqrMagnitude > 0.01f;
    }

    void CheckForLastLandedFrame() {
        if (AllowSlidingOnLanding) return;
        bool justLanded = _collisionCheck.IsGrounded && !_wasGroundedLastFrame;
        if (justLanded) _rb.linearVelocity *= 0;
        _wasGroundedLastFrame = _collisionCheck.IsGrounded;
    }
}