using UnityEngine;
using Unity.Mathematics;
using Cysharp.Threading.Tasks;

public class CollisionCheck : MonoBehaviour {

    #region Fields

    [Header("Ground Check")]
    [SerializeField] private float _groundCheckDistance = 0.2f;
    [field: SerializeField, Range(0f, 89f)] public float MaxSlopeAngle { get; private set; } = 50f;
    [SerializeField] private float _colliderSize = 0.8f;
    
    [Header("Wall Check")]
    private RaycastHit _lastGroundHit;
    [SerializeField] private bool _hasCollided = false;
    public bool HasCollided => _hasCollided;
    [SerializeField] private float _wallAngleThreshold = 60f;

    [Header("Step Check")]
    [SerializeField] private float _firstStepOffset = 0.01f;
    [SerializeField] private float _stepRayOffset = 0.05f;
    [SerializeField] private float _stepLookHeight = 2f;
    [SerializeField] private float _stepCheckDistance = 2.0f;
    [SerializeField] private float _checkAngle = 3.5f;
    [SerializeField] private float _maxStepHeight = 0.3f;

    [Header("Misc Settings")]
    public bool DrawGizmo = true;

    public bool IsGrounded => CheckIsGrounded();
    public Vector3 GroundNormal => _lastGroundHit.normal;
    public Vector3 LastGroundPoint => _lastGroundHit.point;
    private PlayerMovement _playerMovement;
    private CapsuleCollider _capsuleCollider;

    // caches
    private Transform _playerTransform;
    private Rigidbody _rb;
    private RaycastHit[] _sphereHits = new RaycastHit[2];
    private RaycastHit[] _rayHitsF1 = new RaycastHit[2];
    private RaycastHit[] _rayHitsF2 = new RaycastHit[2];
    private bool _playerHasInput = false;

    #endregion

    void Start() {
        _capsuleCollider = GetComponent<CapsuleCollider>();
        _playerMovement = GetComponent<PlayerMovement>();
        _playerTransform = transform;
        _rb = GetComponent<Rigidbody>();
    }

    void Update() {
        CheckPlayerInput();
    }

    void FixedUpdate() {
        CheckRayCasts();
        GroundedAndCollided();
    }

    public bool CheckIsGrounded() {
        if (_capsuleCollider == null) return false;

        Vector3 feetPos = _capsuleCollider.bounds.center;
        feetPos.y = _capsuleCollider.bounds.min.y + _firstStepOffset;
        Vector3 forwardDir = _playerTransform.forward;
        float forwardDistance = _capsuleCollider.radius + 0.1f;
        bool goingUphill = Physics.Raycast(feetPos, forwardDir, forwardDistance);

        return GroundCastSmart(goingUphill);
    }

    public bool GroundCastSmart(bool goingUphill) {
        Vector3 origin = _capsuleCollider.bounds.center;
        float radius = _capsuleCollider.radius * _colliderSize;
        origin.y = _capsuleCollider.bounds.min.y + radius;

        int hits = Physics.SphereCastNonAlloc(
            origin,
            radius,
            Vector3.down,
            _sphereHits,
            _groundCheckDistance
        );

        if (hits == 0) return false;

        float minDot = math.cos(math.radians(MaxSlopeAngle));

        Vector3 normalSum = Vector3.zero;
        int validHitCount = 0;
        RaycastHit fallbackHit = default;
        float bestAlignment = -1f;

        for (int i = 0; i < hits; i++) {
            RaycastHit hit = _sphereHits[i];
            if (hit.collider == _capsuleCollider) continue;

            float alignment = Vector3.Dot(hit.normal, Vector3.up);

            // Debug.Log("Alignment: " + alignment);

            if (alignment >= minDot) {
                if (goingUphill) {
                    _lastGroundHit = hit;
                    return true;
                } else {
                    normalSum += hit.normal;
                    validHitCount++;

                    if (alignment > bestAlignment) {
                        fallbackHit = hit;
                        bestAlignment = alignment;
                    }
                }
            }
        }

        if (validHitCount > 0) {
            _lastGroundHit = fallbackHit;
            _lastGroundHit.normal = normalSum.normalized;
            return true;
        }

        return false;
    }

    public bool TryGetGroundNormal(out Vector3 normal) {
        if (IsGrounded) {
            normal = _lastGroundHit.normal;
            return true;
        }

        normal = Vector3.up;
        return false;        
    }

    void CheckRayCasts() {
        if (!IsGrounded) return;
        if (_capsuleCollider == null) return;

        Vector3 feetPos = _capsuleCollider.bounds.center;
        feetPos.y = _capsuleCollider.bounds.min.y + _firstStepOffset;
        Vector3 rayDir = _playerTransform.forward;
        float rayDistance = _capsuleCollider.radius + _stepRayOffset;

        CheckForStep(feetPos, rayDir, rayDistance);
    }

    void CheckForStep(Vector3 feetPos, Vector3 rayDir, float rayDistance) {
        int hitsCounts1 = Physics.RaycastNonAlloc(feetPos, rayDir, _rayHitsF1, rayDistance);
        bool didHit1 = TryGetNonPlayerHit(_rayHitsF1, hitsCounts1, out RaycastHit hit1);
        DrawRays(feetPos, rayDir, rayDistance, didHit1);
        float hitAngle = Vector3.Angle(hit1.normal, Vector3.up);

        if (didHit1 && hitAngle >= _wallAngleThreshold) {
            Vector3 diagonalOrigin = feetPos + Vector3.up * _stepLookHeight + rayDir * 0.2f;
            Vector3 diagonalDir = (Vector3.down * _checkAngle + rayDir).normalized;
            float diagonalDistance = _stepCheckDistance + _stepRayOffset;

            int hitsCounts2 = Physics.RaycastNonAlloc(diagonalOrigin, diagonalDir, _rayHitsF2, diagonalDistance);
            bool didHit2 = TryGetNonPlayerHit(_rayHitsF2, hitsCounts2, out RaycastHit hit2);
            DrawRays(diagonalOrigin, diagonalDir, diagonalDistance, didHit2);

            if (didHit2) {
                float stepHeight = hit2.point.y - feetPos.y;
                if (stepHeight > 0.01f && stepHeight <= _maxStepHeight && _playerHasInput) {
                    StepUp(hit2.point);
                }
            }
        }
    }

    void StepUp(Vector3 targetPoint) {
        float heightOffset = _capsuleCollider.height * 0.5f;
        float worldOffset = heightOffset * _playerTransform.lossyScale.y;

        Vector3 newPos = new(
            _playerTransform.position.x,
            targetPoint.y + worldOffset,
            _playerTransform.position.z
        );

        Vector3 forwardOffset = _playerTransform.forward * 0.03f;
        Vector3 upwardOffset = Vector3.up * 0.015f;
        newPos += forwardOffset;
        newPos += upwardOffset;

        _rb.MovePosition(newPos);
    }

        void GroundedAndCollided() {
            if (_capsuleCollider == null) return;

            if (HasCollided && IsGrounded) {
                Vector3 backwardDir = -_playerTransform.forward;
                _rb.AddForce(backwardDir * 0.01f, ForceMode.VelocityChange);
            }
        }

    void OnDrawGizmos() {
        if (!DrawGizmo || _capsuleCollider == null) return;

        float radius = _capsuleCollider.radius * _colliderSize;
        Vector3 origin = _capsuleCollider.bounds.center;
        origin.y = _capsuleCollider.bounds.min.y + radius;

        Gizmos.color = CheckIsGrounded() ? Color.green : Color.red;
        Gizmos.DrawWireSphere(origin, radius);

        Vector3 endPoint = origin + Vector3.down * _groundCheckDistance;
        Gizmos.DrawWireSphere(endPoint, radius);

        Gizmos.DrawLine(origin + Vector3.forward * radius, endPoint + Vector3.forward * radius);
        Gizmos.DrawLine(origin - Vector3.forward * radius, endPoint - Vector3.forward * radius);
        Gizmos.DrawLine(origin + Vector3.right * radius, endPoint + Vector3.right * radius);
        Gizmos.DrawLine(origin - Vector3.right * radius, endPoint - Vector3.right * radius);
    }

    // ? Below could be useful for wall climbing later
    void OnCollisionStay(Collision collision) {

        foreach (ContactPoint contact in collision.contacts) {
            Vector3 normal = contact.normal;
            float angle = Vector3.Angle(normal, Vector3.up);
            
            if (angle >= _wallAngleThreshold) {
                _hasCollided = true;
                break;
            }
        }
    }

    void OnCollisionExit(Collision collision) {
        _hasCollided = false;       
    }

    // helpers
    void DrawRays(Vector3 origin, Vector3 direction, float distance, bool hit) {
        if (!DrawGizmo) return;
        Color rayColor = hit ? Color.yellow : Color.red;
        Debug.DrawRay(origin, direction * distance, rayColor);
    }

    bool TryGetNonPlayerHit(RaycastHit[] hits, int hitCount, out RaycastHit validHit) {
        for (int i = 0; i < hitCount; i++) {
            if (hits[i].collider != _capsuleCollider) {
                validHit = hits[i];
                return true;
            }
        }
        validHit = default;
        return false;
    }

    bool CheckPlayerInput() {
        return _playerHasInput = _playerMovement.HasMoveInput();
    }
}
