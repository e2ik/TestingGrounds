using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Interactions;

public class PlayerMovement : MonoBehaviour {

    private Rigidbody rb;
    private PlayerInput playerInput;
    private CollisionCheck collisionCheck;
    public float acceleration = 10f;
    public float maxSpeed = 10f;
    public float jumpForce = 10f;
    private Vector2 _moveDirection;
    public bool followCameraRotation = false;
    public bool IsMoving => _moveDirection != Vector2.zero;
    private bool cancelledMovement = false;
    [Range(30f, 50f)] private float momemtum = 40f;

    void Awake() {
        playerInput = GetComponent<PlayerInput>();
    }

    void OnEnable() {
        playerInput.actions["Move"].performed += OnMove;
        playerInput.actions["Move"].canceled += OnMove;
        playerInput.actions["Jump"].performed += OnJump;
        playerInput.actions["Sprint"].performed += OnSprint;
    }

    void OnDisable() {
        playerInput.actions["Move"].performed -= OnMove;
        playerInput.actions["Move"].canceled -= OnMove;
        playerInput.actions["Jump"].performed -= OnJump;
        playerInput.actions["Sprint"].performed -= OnSprint;
    }

    void Start() {
        rb = GetComponent<Rigidbody>();
        collisionCheck = GetComponent<CollisionCheck>();
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
            _moveDirection = Vector2.Lerp(_moveDirection, Vector2.zero, momemtum * Time.fixedDeltaTime);
        } else {
            _moveDirection = Vector2.zero;
            cancelledMovement = false;
        }
    }

    void OnJump(InputAction.CallbackContext context) {
        if (context.performed && collisionCheck.IsGrounded) {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce, rb.linearVelocity.z);
        }
    }

    void OnSprint(InputAction.CallbackContext context) {
        if (context.started) {
            Debug.Log("Sprinting!");
        }

        if (context.performed) {
            if (context.interaction is HoldInteraction) {
                Debug.Log("Hold performed");
            } else if (context.interaction is PressInteraction) {
                Debug.Log("Press performed");
            } else {
                Debug.Log("Unknown interaction");
            }
        }
    }

    void MovementLogic() {
        Transform cam = Camera.main.transform;
        Vector3 camForward = cam.forward;
        Vector3 camRight = cam.right;
        camForward.y = 0;
        camRight.y = 0;
        camForward.Normalize();
        camRight.Normalize();

        Vector3 inputDir = new Vector3(_moveDirection.x, 0, _moveDirection.y).normalized;
        float horizontalSpeed = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z).magnitude;

        if (followCameraRotation) {
            inputDir = (camForward * _moveDirection.y + camRight * _moveDirection.x).normalized;
        }

        if (horizontalSpeed < maxSpeed) {
            if (collisionCheck.TryGetGroundNormal(out Vector3 normal)) {
                Vector3 slopeDir = Vector3.ProjectOnPlane(inputDir, normal).normalized;
                float adjustForce = (maxSpeed - horizontalSpeed) * acceleration;
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
}