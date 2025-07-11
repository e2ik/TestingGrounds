using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Interactions;

public class PlayerMovement : MonoBehaviour {

    [Header("Movement Forces")]
    public float acceleration = 10f;
    public float maxSpeed = 10f;
    public float sprintMaxSpeed = 20f;
    public float jumpForce = 10f;
    public float sprintForce = 10f;
    [Range(30f, 70f), SerializeField] private float momentum = 40f;

    [Header("Dash Settings")]
    public bool dashHasGravity = true;
    public float dashDistance = 5f;
    public int dashAmount = 1;

    [Header("Double Jump Settings")]
    public bool canDoubleJump = false;
    private int jumpAmount = 1;
    private int jumpCounter = 0; // can add more jumps in future if needed
    public bool zeroVelocityOnDoubleJump = false;

    [Header("Misc Settings")]
    public bool followCameraRotation = false;
    public float coyoteTime = 0.15f;
    public float jumpMultiplier = 0.5f;

    private Rigidbody rb;
    private PlayerInput playerInput;
    private CollisionCheck collisionCheck;
    private Vector3 dashStartPos;
    private int dashCounter = 0;
    private Vector2 _moveDirection;
    private bool cancelledMovement = false;
    public bool IsMoving => _moveDirection != Vector2.zero;
    private bool isSprinting = false;
    private bool isDashing = false;
    private float lastGroundedTime = 0f;
    private bool inCoyote = false;

    void Awake() {
        playerInput = GetComponent<PlayerInput>();
    }

    void OnEnable() {
        playerInput.actions["Move"].performed += OnMove;
        playerInput.actions["Move"].canceled += OnMove;

        playerInput.actions["Jump"].performed += OnJump;

        playerInput.actions["Sprint"].started += OnSprint;
        playerInput.actions["Sprint"].performed += OnSprint;
        playerInput.actions["Sprint"].canceled += OnSprint;
    }

    void OnDisable() {
        playerInput.actions["Move"].performed -= OnMove;
        playerInput.actions["Move"].canceled -= OnMove;

        playerInput.actions["Jump"].performed -= OnJump;
        
        playerInput.actions["Sprint"].started -= OnSprint;
        playerInput.actions["Sprint"].performed -= OnSprint;
        playerInput.actions["Sprint"].canceled -= OnSprint;
    }

    void Start() {
        rb = GetComponent<Rigidbody>();
        collisionCheck = GetComponent<CollisionCheck>();
    }

    void Update() {
        UpdateJump();
        UpdateDash();
    }

    void FixedUpdate() {
        MovementLogic();
        if (cancelledMovement) StopMovement();
    }

    void OnMove(InputAction.CallbackContext context) {
        if (context.canceled) {
            cancelledMovement = true;
        } else {
            cancelledMovement = false;
            _moveDirection = context.ReadValue<Vector2>();
        }
    }

    void StopMovement() {
        if (_moveDirection.magnitude > 0.01f) {
            _moveDirection = Vector2.Lerp(_moveDirection, Vector2.zero, momentum * Time.fixedDeltaTime);
        } else {
            _moveDirection = Vector2.zero;
            cancelledMovement = false;
        }
    }

    void OnJump(InputAction.CallbackContext context) {;
        if (context.performed) {
            if (collisionCheck.IsGrounded) { ApplyJumpForce(jumpForce); return;}
            else { HandleCoyote(); }

            if (canDoubleJump) { HandleDoubleJump(); }
        }
    }

    void OnSprint(InputAction.CallbackContext context) {
        if (context.started) {
            PerformDash();
        }

        if (context.performed && context.interaction is HoldInteraction) {
            isSprinting = true;
        }

        if (context.canceled) {
            isSprinting = false;

            if (_moveDirection == Vector2.zero) {
                cancelledMovement = true;
            }
        }
    }

    void MovementLogic() {
        Vector3 inputDir = new Vector3(_moveDirection.x, 0, _moveDirection.y).normalized;
        float horizontalSpeed = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z).magnitude;

        if (followCameraRotation) {
            inputDir = TranformToCameraRotation().normalized;
        }

        float targetMaxSpeed = isSprinting ? sprintMaxSpeed : maxSpeed;
        if (horizontalSpeed < targetMaxSpeed) {
            if (collisionCheck.TryGetGroundNormal(out Vector3 normal)) {
                Vector3 slopeDir = Vector3.ProjectOnPlane(inputDir, normal).normalized;
                float adjustForce = (targetMaxSpeed - horizontalSpeed) * acceleration;
                rb.AddForce(slopeDir * adjustForce, ForceMode.Acceleration);
            } else {
                rb.AddForce(inputDir * acceleration, ForceMode.Acceleration);
            }
        }

        if (inputDir != Vector3.zero) {
            Quaternion targetRotation = Quaternion.LookRotation(inputDir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10f * Time.fixedDeltaTime);
        }
    }

    void PerformDash() {
        if (dashCounter >= dashAmount) return;
        if (!collisionCheck.IsGrounded) dashCounter++;

        Vector3 inputDir;

        if (_moveDirection != Vector2.zero) {
            inputDir = new Vector3(_moveDirection.x, 0, _moveDirection.y).normalized;

            if (followCameraRotation) {
                inputDir = TranformToCameraRotation().normalized;
            }
        } else {
            inputDir = transform.forward;
        }

        float horizontalSpeed = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z).magnitude;
        if (horizontalSpeed < sprintMaxSpeed) {
            if (!dashHasGravity) {
                rb.useGravity = false;
                isDashing = true;
                dashStartPos = transform.position;
                Vector3 velocity = rb.linearVelocity;
                velocity.y = 0;
                rb.linearVelocity = velocity;

            }
            rb.AddForce(inputDir * sprintForce, ForceMode.VelocityChange);
        }
    }

    void HandleCoyote() {
        if (Time.time - lastGroundedTime <= coyoteTime) {
            Vector3 velocty = rb.linearVelocity;
            if (velocty.y < 0) {
                ApplyJumpForce(jumpForce);
                inCoyote = true;
            }
        }
    }

    void HandleDoubleJump() {
        if (jumpCounter >= jumpAmount) return;
        if (zeroVelocityOnDoubleJump) {
            Vector3 velocty = rb.linearVelocity;
            velocty.y = 0f;
            rb.linearVelocity = velocty;
        }
        ApplyJumpForce(jumpForce * jumpMultiplier);
        if (!inCoyote) { jumpCounter++; }
        else { inCoyote = false; }
    }

    void UpdateJump() {
        if (collisionCheck.IsGrounded) {
            lastGroundedTime = Time.time;
            jumpCounter = 0;
            inCoyote = false;
        }
    }

    void UpdateDash() {
        if (dashCounter != 0 && collisionCheck.IsGrounded) dashCounter = 0;
        if (isDashing && !dashHasGravity) {
            float dashTravelled = Vector3.Distance(dashStartPos, transform.position);
            if (dashTravelled > dashDistance) {
                rb.useGravity = true;
                isDashing = false;
            } else {
                if (collisionCheck.IsGrounded || collisionCheck.hasCollided) {
                    rb.useGravity = true;
                    isDashing = false;
                }
            }
        }
    }

    // helpers
    Vector3 TranformToCameraRotation() {
        Transform cam = Camera.main.transform;
        Vector3 camForward = cam.forward;
        Vector3 camRight = cam.right;
        camForward.y = 0;
        camRight.y = 0;
        camForward.Normalize();
        camRight.Normalize();
        return camForward * _moveDirection.y + camRight * _moveDirection.x;
    }

    void ApplyJumpForce(float force) {
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, force, rb.linearVelocity.z);
    }
}