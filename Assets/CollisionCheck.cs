using UnityEditor;
using UnityEngine;

public class CollisionCheck : MonoBehaviour {

    public float groundCheckDistance = 1.5f;
    public LayerMask groundLayer;
    public bool DrawGizmo = true;
    public bool IsGrounded => CheckIsGrounded();
    private CapsuleCollider capsuleCollider;
    [SerializeField] private float groundColliderOffset = 0.2f;
    private RaycastHit lastGroundHit;
    [Range(0f, 89f)]
    public float maxSlopeAngle = 45f;

    public PhysicsMaterial defaultMaterial;
    public PhysicsMaterial slideMaterial;
    public float wallAngleThreshold;


    void Start() {
        capsuleCollider = GetComponent<CapsuleCollider>();
        capsuleCollider.material = defaultMaterial;
    }

    void Update() {
        capsuleCollider.material = CheckIsGrounded() ? defaultMaterial : slideMaterial;
    }

    public bool CheckIsGrounded() {
        if (capsuleCollider == null) return false;

        Vector3 colliderBottom = new(
            capsuleCollider.bounds.center.x,
            capsuleCollider.bounds.min.y + groundColliderOffset,
            capsuleCollider.bounds.center.z
        );
        Vector3 boxHalfExtents = new(capsuleCollider.radius * 0.7f, 0.1f, capsuleCollider.radius * 0.7f);
        
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
            capsuleCollider.radius * 2f * 0.7f,
            0.1f,
            capsuleCollider.radius * 2f * 0.7f
        );

        Vector3 boxCenter = bottom + Vector3.down * groundCheckDistance;
        Gizmos.color = (CheckIsGrounded()) ? Color.green : Color.red;
        Gizmos.DrawWireCube(boxCenter, boxSize);
    }

    // void OnCollisionStay(Collision collision) {
    //     bool onWall = false;

    //     foreach (ContactPoint contact in collision.contacts) {
    //         Vector3 normal = contact.normal;
    //         float angle = Vector3.Angle(normal, Vector3.up);
            
    //         if (angle >= wallAngleThreshold) {
    //             onWall = true;
    //             break;
    //         }
    //     }
    //     capsuleCollider.material = onWall ? slideMaterial : defaultMaterial;
    // }

    // void OnCollisionExit(Collision collision) {
    //     capsuleCollider.material = defaultMaterial;        
    // }

}
