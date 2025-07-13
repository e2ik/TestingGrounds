using UnityEngine;
using Cysharp.Threading.Tasks;

public class CollisionCheck : MonoBehaviour {

    #region Fields

    [Header("Ground Check")]
    [SerializeField] private float _groundCheckDistance = 0.2f;
    [Range(0f, 89f), SerializeField] private float _maxSlopeAngle = 50f;
    [SerializeField] private float _groundColliderOffset = 0.3f;
    [SerializeField] private float _colliderSize = 0.8f;
    
    [Header("Wall Check")]
    [field: SerializeField] public bool HasCollided { get; private set; } = false;  
    private RaycastHit _lastGroundHit;
    private bool _onWall = false;
    [SerializeField] private float _wallAngleThreshold = 60f;

    [Header("Step Check")]
    [SerializeField] private float _firstStepOffset = 0.01f;
    [SerializeField] private float _stepRayOffset = 0.05f;
    [SerializeField] private float _stepLookHeight = 2f;
    [SerializeField] private float _stepCheckDistance = 2.0f;
    [SerializeField] private float _checkAngle = 3.5f;
    [SerializeField] private float _maxStepHeight = 0.3f;
    [SerializeField] private float _stepGravTime = 0.1f;

    [Header("Misc Settings")]
    public bool DrawGizmo = true;
    [SerializeField] private PhysicsMaterial _defaultMaterial;
    [SerializeField] private PhysicsMaterial _slideMaterial;

    public bool IsGrounded => CheckIsGrounded();
    private PlayerMovement _playerMovement;
    private CapsuleCollider _capsuleCollider;
    private float _halfExtentsOffect = 0.1f;

    // caches
    private Transform _playerTransform;
    private Rigidbody _rb;

    #endregion

    void Start() {
        _capsuleCollider = GetComponent<CapsuleCollider>();
        _capsuleCollider.material = _defaultMaterial;
        _playerMovement = GetComponent<PlayerMovement>();
        _playerTransform = transform;
        _rb = GetComponent<Rigidbody>();
    }

    void Update() {
        MaterialChange();
        CheckRayCasts();
    }

    public bool CheckIsGrounded() {
        if (_capsuleCollider == null) return false;

        Vector3 colliderBottom = new(
            _capsuleCollider.bounds.center.x,
            _capsuleCollider.bounds.min.y + _groundColliderOffset,
            _capsuleCollider.bounds.center.z
        );
        Vector3 boxHalfExtents = new(
            _capsuleCollider.radius * _colliderSize,
            _halfExtentsOffect,
            _capsuleCollider.radius * _colliderSize
        );
        
        RaycastHit[] hits  = Physics.BoxCastAll(
            colliderBottom,
            boxHalfExtents,
            Vector3.down,
            Quaternion.identity,
            _groundCheckDistance
        );

        if (hits.Length == 0) return false;
        float minDot = Mathf.Cos(_maxSlopeAngle * Mathf.Deg2Rad);

        float bestAlignment = -1f;
        RaycastHit bestHit = new();
        bool foundValid = false;

        foreach (var hit in hits) {
            if (hit.collider == _capsuleCollider) continue;
            float alignment = Vector3.Dot(hit.normal, Vector3.up);
            if (alignment >= minDot && alignment > bestAlignment) {
                bestAlignment = alignment;
                bestHit = hit;
                foundValid = true;
            }
        }

        if (foundValid) _lastGroundHit = bestHit;

        return foundValid;
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
        CheckSideForLowObjects(feetPos, rayDistance);
        CheckSideForLowObjects(feetPos, rayDistance);
    }

    void CheckForStep(Vector3 feetPos, Vector3 rayDir, float rayDistance) {
        RaycastHit[] hits1 = Physics.RaycastAll(feetPos, rayDir, rayDistance);
        bool didHit1 = TryGetNonPlayerHit(hits1, out RaycastHit hit1);
        DrawRays(feetPos, rayDir, rayDistance, didHit1);
        float hitAngle = Vector3.Angle(hit1.normal, Vector3.up);

        if (didHit1 && hitAngle >= _wallAngleThreshold) {
            Vector3 diagonalOrigin = feetPos + Vector3.up * _stepLookHeight + rayDir * 0.2f;
            Vector3 diagonalDir = (Vector3.down * _checkAngle + rayDir).normalized;
            float diagonalDistance = _stepCheckDistance + _stepRayOffset;

            RaycastHit[] hits2 = Physics.RaycastAll(diagonalOrigin, diagonalDir, diagonalDistance);
            bool didHit2 = TryGetNonPlayerHit(hits2, out RaycastHit hit2);
            DrawRays(diagonalOrigin, diagonalDir, diagonalDistance, didHit2);

            if (didHit2) {
                float stepHeight = hit2.point.y - feetPos.y;
                if (stepHeight > 0.01f && stepHeight <= _maxStepHeight && _playerMovement.HasMoveInput()) {
                    StepUp(hit2.point);
                }
            }
        }
    }

    void CheckSideForLowObjects(Vector3 feetPos, float rayDistance) {
        Vector3 rightDir = _playerTransform.right;
        Vector3 leftDir = -rightDir;

        RaycastHit[] hits1 = Physics.RaycastAll(feetPos, rightDir, rayDistance);
        bool didHitRight = TryGetNonPlayerHit(hits1, out RaycastHit hitR);
        DrawRays(feetPos, rightDir, rayDistance, didHitRight);

        RaycastHit[] hits2 = Physics.RaycastAll(feetPos, leftDir, rayDistance);
        bool didHitLeft = TryGetNonPlayerHit(hits2, out RaycastHit hitL);
        DrawRays(feetPos, leftDir, rayDistance, didHitLeft);

        if (didHitRight || didHitLeft) _onWall = true;
        else _onWall = false;
    }

    void StepUp(Vector3 targetPoint) {
        GravityChangeOnStepAsync(_stepGravTime).Forget();

        float heightOffset = _capsuleCollider.height * 0.5f;
        float worldOffset = heightOffset * _playerTransform.lossyScale.y;

        Vector3 newPos = new(
            _playerTransform.position.x,
            targetPoint.y + worldOffset,
            _playerTransform.position.z
        );

        _playerTransform.position = newPos;
        Vector3 forwardOffset = _playerTransform.forward * 0.02f;
        _playerTransform.position += forwardOffset;
    }

    void OnDrawGizmos() {
        if (!DrawGizmo || _capsuleCollider == null) return;

        Vector3 bottom = new(
            _capsuleCollider.bounds.center.x,
            _capsuleCollider.bounds.min.y + _groundColliderOffset,
            _capsuleCollider.bounds.center.z
        );

        Vector3 boxSize = new(
            _capsuleCollider.radius * 2f * _colliderSize,
            0.1f,
            _capsuleCollider.radius * 2f * _colliderSize
        );

        Vector3 boxCenter = bottom + Vector3.down * _groundCheckDistance;
        Gizmos.color = CheckIsGrounded() ? Color.green : Color.red;
        Gizmos.DrawWireCube(boxCenter, boxSize);
    }

    void MaterialChange() {
        if (IsGrounded && _onWall) {
            if (_playerMovement.IsMoving) SlideMaterialChange();
            else DefaultMaterialChange();
        } else if (!IsGrounded) {
            SlideMaterialChange();
        } else if (IsGrounded && !_onWall) {
            DefaultMaterialChange();
        }
    }

    // ? Below could be useful for wall climbing later
    void OnCollisionStay(Collision collision) {

        foreach (ContactPoint contact in collision.contacts) {
            Vector3 normal = contact.normal;
            float angle = Vector3.Angle(normal, Vector3.up);
            
            if (angle >= _wallAngleThreshold) {
                _onWall = true;
                HasCollided = true;
                break;
            }
        }
    }

    void OnCollisionExit(Collision collision) {
        _onWall = false;       
        HasCollided = false;
    }

    // helpers
    void DrawRays(Vector3 origin, Vector3 direction, float distance, bool hit) {
        if (!DrawGizmo) return;
        Color rayColor = hit ? Color.yellow : Color.red;
        Debug.DrawRay(origin, direction * distance, rayColor);
    }

    bool TryGetNonPlayerHit(RaycastHit[] hits, out RaycastHit validHit) {
        foreach (var hit in hits) {
            if (hit.collider != _capsuleCollider) {
                validHit = hit;
                return true;
            }
        }
        validHit = default;
        return false;
    }

    void SlideMaterialChange() {
        _capsuleCollider.material = _slideMaterial;
        _playerMovement.MaxSpeed = _playerMovement.DampenedSpeed;
    }

    void DefaultMaterialChange() {
        _capsuleCollider.material = _defaultMaterial;
        _playerMovement.MaxSpeed = _playerMovement.BaseSpeed;
    }

    async UniTaskVoid GravityChangeOnStepAsync(float time) {;
        _rb.useGravity = false;
        await UniTask.Delay((int)(time * 1000));
        _rb.useGravity = true;
    }
}
