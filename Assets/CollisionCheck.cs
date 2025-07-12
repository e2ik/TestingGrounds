using System.Collections;
using UnityEngine;

public class CollisionCheck : MonoBehaviour {

    [Header("Ground Check")]
    public float groundCheckDistance = 1.5f;
    [Range(0f, 89f)]
    public float maxSlopeAngle = 45f;
    [SerializeField] private float groundColliderOffset = 0.2f;
    [SerializeField] private float colliderSize = 0.8f;
    
    [Header("Wall Check")]
    public bool hasCollided = false;  
    private RaycastHit lastGroundHit;
    private bool onWall = false;
    public float wallAngleThreshold;

    [Header("Step Check")]
    public float firstStepOffset = 0.1f;
    public float stepSmooth = 0.1f;
    public float stepRayOffset = 0.1f;
    public float stepHeightOffet = 1.5f;
    public float stepCheckDistance = 1.0f;
    public float checkAngle = 1.5f;
    public float maxStepHeight = 0.5f;
    public float stepGravTime = 0.2f;

    [Header("Misc Settings")]
    public bool DrawGizmo = true;
    public PhysicsMaterial defaultMaterial;
    public PhysicsMaterial slideMaterial;

    public bool IsGrounded => CheckIsGrounded();
    private PlayerMovement playerMovement;
    private CapsuleCollider capsuleCollider;
    private float halfExtentsOffect = 0.1f;

    void Start() {
        capsuleCollider = GetComponent<CapsuleCollider>();
        capsuleCollider.material = defaultMaterial;
        playerMovement = GetComponent<PlayerMovement>();
    }

    void Update() {
        if (IsGrounded && onWall) {
            if (playerMovement.IsMoving) capsuleCollider.material = slideMaterial;
            else capsuleCollider.material = defaultMaterial;
        } else if (!IsGrounded) {
            capsuleCollider.material = slideMaterial;
        } else if (IsGrounded && !onWall) {
            capsuleCollider.material = defaultMaterial;
        }
        CheckForStep();
    }

    public bool CheckIsGrounded() {
        if (capsuleCollider == null) return false;

        Vector3 colliderBottom = new(
            capsuleCollider.bounds.center.x,
            capsuleCollider.bounds.min.y + groundColliderOffset,
            capsuleCollider.bounds.center.z
        );
        Vector3 boxHalfExtents = new(capsuleCollider.radius * colliderSize, halfExtentsOffect, capsuleCollider.radius * colliderSize);
        
        RaycastHit[] hits  = Physics.BoxCastAll(
            colliderBottom,
            boxHalfExtents,
            Vector3.down,
            Quaternion.identity,
            groundCheckDistance
        );

        if (hits.Length == 0) return false;
        float minDot = Mathf.Cos(maxSlopeAngle * Mathf.Deg2Rad);

        float bestAlignment = -1f;
        RaycastHit bestHit = new RaycastHit();
        bool foundValid = false;

        foreach (var hit in hits) {
            if (hit.collider == capsuleCollider) continue;
            float alignment = Vector3.Dot(hit.normal, Vector3.up);
            if (alignment >= minDot && alignment > bestAlignment) {
                bestAlignment = alignment;
                bestHit = hit;
                foundValid = true;
            }
        }

        if (foundValid) {
            lastGroundHit = bestHit;
            return true;
        }

        return false;
    }

    public bool TryGetGroundNormal(out Vector3 normal) {
        if (IsGrounded) {
            normal = lastGroundHit.normal;
            return true;
        }

        normal = Vector3.up;
        return false;        
    }

    void CheckForStep() {
        if (!IsGrounded) return;
        if (capsuleCollider == null) return;

        Vector3 feetPos = capsuleCollider.bounds.center;
        feetPos.y = capsuleCollider.bounds.min.y + firstStepOffset;

        Vector3 rayDir = transform.forward;
        float rayDistance = capsuleCollider.radius + stepRayOffset;

        RaycastHit[] hits1 = Physics.RaycastAll(feetPos, rayDir, rayDistance);
        bool didHit1 = TryGetNonPlayerHit(hits1, out RaycastHit hit1);
        DrawRays(feetPos, rayDir, rayDistance, didHit1);
        float hitAngle = Vector3.Angle(hit1.normal, Vector3.up);

        if (didHit1 && hitAngle >= wallAngleThreshold) {
            Vector3 diagonalOrigin = feetPos + Vector3.up * stepHeightOffet + rayDir * 0.2f;
            Vector3 diagonalDir = (Vector3.down * checkAngle + rayDir).normalized;
            float diagonalDistance = stepCheckDistance + stepRayOffset;

            RaycastHit[] hits2 = Physics.RaycastAll(diagonalOrigin, diagonalDir, diagonalDistance);
            bool didHit2 = TryGetNonPlayerHit(hits2, out RaycastHit hit2);
            DrawRays(diagonalOrigin, diagonalDir, diagonalDistance, didHit2);

            if (didHit2) {
                float stepHeight = hit2.point.y - feetPos.y;
                Debug.Log("has Movement input: " + playerMovement.HasMoveInput());
                if (stepHeight > 0.01f && stepHeight <= maxStepHeight && playerMovement.HasMoveInput()) {
                    StepUp(hit2.point);
                }
            }
        }
    }

    void StepUp(Vector3 targetPoint) {;
        StartCoroutine(GravityChangeOnStep(stepGravTime));

        float heightOffset = capsuleCollider.height * 0.5f;
        float worldOffset = heightOffset * transform.lossyScale.y;

        Vector3 newPos = new Vector3(transform.position.x, targetPoint.y + worldOffset, transform.position.z);
        transform.position = newPos;

        Vector3 forwardOffset = transform.forward * 0.02f;
        transform.position += forwardOffset;
    }

    void OnDrawGizmos() {
        if (!DrawGizmo || capsuleCollider == null) return;

        Vector3 bottom = new(
            capsuleCollider.bounds.center.x,
            capsuleCollider.bounds.min.y + groundColliderOffset,
            capsuleCollider.bounds.center.z
        );

        Vector3 boxSize = new Vector3(
            capsuleCollider.radius * 2f * colliderSize,
            0.1f,
            capsuleCollider.radius * 2f * colliderSize
        );

        Vector3 boxCenter = bottom + Vector3.down * groundCheckDistance;
        Gizmos.color = (CheckIsGrounded()) ? Color.green : Color.red;
        Gizmos.DrawWireCube(boxCenter, boxSize);
    }

    // ? Below could be useful for wall climbing later
    void OnCollisionStay(Collision collision) {

        foreach (ContactPoint contact in collision.contacts) {
            Vector3 normal = contact.normal;
            float angle = Vector3.Angle(normal, Vector3.up);
            
            if (angle >= wallAngleThreshold) {
                onWall = true;
                hasCollided = true;
                break;
            }
        }
    }

    void OnCollisionExit(Collision collision) {
        onWall = false;       
        hasCollided = false;
    }

    // helpers
    void DrawRays(Vector3 origin, Vector3 direction, float distance, bool hit) {
        if (!DrawGizmo) return;
        Color rayColor = hit ? Color.yellow : Color.red;
        Debug.DrawRay(origin, direction * distance, rayColor);
    }

    bool TryGetNonPlayerHit(RaycastHit[] hits, out RaycastHit validHit) {
        foreach (var hit in hits) {
            if (hit.collider != capsuleCollider) {
                validHit = hit;
                return true;
            }
        }
        validHit = default;
        return false;
    }

    IEnumerator GravityChangeOnStep(float time) {
        Rigidbody rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        yield return new WaitForSeconds(time);
        rb.useGravity = true;
    }
}
