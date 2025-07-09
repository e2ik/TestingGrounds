using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour {

    private Rigidbody rb;
    private CollisionCheck collisionCheck;
    public float acceleration = 10f;
    public float maxSpeed = 10f;
    public float jumpForce = 10f;
    private Vector2 _moveDirection;

    void Start() {
        rb = GetComponent<Rigidbody>();
        collisionCheck = GetComponent<CollisionCheck>();
    }

    void FixedUpdate() {
        Vector3 inputDir = new Vector3(_moveDirection.x, 0, _moveDirection.y).normalized;
        float horizontalSpeed = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z).magnitude;

        if (horizontalSpeed < maxSpeed) {
            if (collisionCheck.TryGetGroundNormal(out Vector3 normal)) {
                Vector3 slopeDir = Vector3.ProjectOnPlane(inputDir, normal).normalized;
                float adjustForce = (maxSpeed - horizontalSpeed) * acceleration;
                rb.AddForce(slopeDir * adjustForce, ForceMode.Acceleration);
            } else {
                rb.AddForce(inputDir * acceleration, ForceMode.Acceleration);
            }
        }
    }

    void OnMove(InputValue value) {
        _moveDirection = value.Get<Vector2>();
    }

    void OnJump(InputValue value) {
        if (value.isPressed && collisionCheck.IsGrounded) {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce, rb.linearVelocity.z);
        }
    }
}