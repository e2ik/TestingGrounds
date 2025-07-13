using UnityEngine;
using Unity.Mathematics;
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
    [SerializeField] private float _sidesOffet = 0.01f;

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
    private RaycastHit[] _boxHits = new RaycastHit[2];
    private RaycastHit[] _rayHitsF1 = new RaycastHit[2];
    private RaycastHit[] _rayHitsF2 = new RaycastHit[2];
    private RaycastHit[] _rayHitsR = new RaycastHit[2];
    private RaycastHit[] _rayHitsL = new RaycastHit[2];
    private PhysicsMaterial _currentMaterial;

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
        
        int hits  = Physics.BoxCastNonAlloc(
            colliderBottom,
            boxHalfExtents,
            Vector3.down,
            _boxHits,
            Quaternion.identity,
            _groundCheckDistance
        );

        if (hits == 0) return false;
        float minDot = math.cos(math.radians(_maxSlopeAngle));

        float bestAlignment = -1f;
        RaycastHit bestHit = new();
        bool foundValid = false;

        for (int i = 0; i < hits; i++) {
            RaycastHit hit = _boxHits[i];
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
                if (stepHeight > 0.01f && stepHeight <= _maxStepHeight && _playerMovement.HasMoveInput()) {
                    StepUp(hit2.point);
                }
            }
        }
    }

    void CheckSideForLowObjects(Vector3 feetPos, float rayDistance) {
        if (CheckSide(feetPos, _playerTransform.right, _rayHitsR, rayDistance) ||
            CheckSide(feetPos, -_playerTransform.right, _rayHitsL, rayDistance)) {
                SlideMaterialChange();
        } else {
            if (!HasCollided) DefaultMaterialChange();
        }
    }

    bool CheckSide(Vector3 origin, Vector3 direction, RaycastHit[] hitsArray, float distance) {
        float extendDistance = distance + _sidesOffet;
        int hitCount = Physics.RaycastNonAlloc(origin, direction, hitsArray, extendDistance);
        bool didHit = TryGetNonPlayerHit(hitsArray, hitCount, out RaycastHit hit);
        DrawRays(origin, direction, extendDistance, didHit);

        if (didHit) {
            float angle = Vector3.Angle(hit.normal, Vector3.up);
            return angle >= _wallAngleThreshold;
        }

        return false;
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

    void SlideMaterialChange() {
        if (_currentMaterial == _slideMaterial) return;
        _capsuleCollider.material = _slideMaterial;
        _currentMaterial = _slideMaterial;
        _playerMovement.MaxSpeed = _playerMovement.DampenedSpeed;
        _playerMovement.Momentum = _playerMovement.NonSlideMomentum;
    }

    void DefaultMaterialChange() {
        if (_currentMaterial == _defaultMaterial) return;
        _capsuleCollider.material = _defaultMaterial;
        _currentMaterial = _defaultMaterial;
        _playerMovement.MaxSpeed = _playerMovement.BaseSpeed;
        _playerMovement.Momentum = _playerMovement.BaseMomentum;
    }

    async UniTaskVoid GravityChangeOnStepAsync(float time) {;
        _rb.useGravity = false;
        await UniTask.Delay((int)(time * 1000));
        _rb.useGravity = true;
    }
}
