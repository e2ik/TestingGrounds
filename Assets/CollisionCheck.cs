using UnityEditor;
using UnityEngine;

public class CollisionCheck : MonoBehaviour {

    public float groundCheckDistance = 1.5f;
    public LayerMask groundLayer;
    public bool DrawGizmo = true;
    public bool IsGrounded => CheckIsGrounded();
    private CapsuleCollider capsuleCollider;
    [SerializeField] private float groundColliderOffset = 0.2f;
    [SerializeField] private float colliderSize = 0.8f;
    private RaycastHit lastGroundHit;
    [Range(0f, 89f)]
    public float maxSlopeAngle = 45f;
    private bool onWall = false;

    public PhysicsMaterial defaultMaterial;
    public PhysicsMaterial slideMaterial;
    public float wallAngleThreshold;

    private PlayerMovement playerMovement;


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
    }

    public bool CheckIsGrounded() {
        if (capsuleCollider == null) return false;

        Vector3 colliderBottom = new(
            capsuleCollider.bounds.center.x,
            capsuleCollider.bounds.min.y + groundColliderOffset,
            capsuleCollider.bounds.center.z
        );
        Vector3 boxHalfExtents = new(capsuleCollider.radius * colliderSize, 0.1f, capsuleCollider.radius * colliderSize);
        
        RaycastHit[] hits  = Physics.BoxCastAll(
            colliderBottom,
            boxHalfExtents,
            Vector3.down,
            Quaternion.identity,
            groundCheckDistance,
            groundLayer
        );

        if (hits.Length == 0) return false;
        float minDot = Mathf.Cos(maxSlopeAngle * Mathf.Deg2Rad);

        float bestAlignment = -1f;
        RaycastHit bestHit = new RaycastHit();
        bool foundValid = false;

        foreach (var hit in hits) {
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
        bool grounded = CheckIsGrounded();
        if (grounded) {
            normal = lastGroundHit.normal;
            return true;
        }

        normal = Vector3.up;
        return false;        
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
                break;
            }
        }
    }

    void OnCollisionExit(Collision collision) {
        onWall = false;       
    }
}
