using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FallGuyClone
{
    /// <summary>
    /// Physics based third person controller.
    /// WASD / arrows move relative to the camera, Space jumps, Left Mouse / Left Ctrl dives.
    /// Obstacles with a <see cref="Hazard"/> knock the player back and stun them briefly.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        public float moveSpeed = 7.5f;
        public float groundAcceleration = 70f;
        public float airAcceleration = 22f;
        public float turnSpeed = 14f;

        [Header("Jump & Dive")]
        public float jumpSpeed = 9f;
        public float diveForwardSpeed = 11f;
        public float diveUpSpeed = 4.5f;
        public float fallGravityMultiplier = 1.3f;

        [Header("References")]
        public Transform cameraTransform;

        public bool ControlEnabled { get; set; }
        public bool IsGrounded => grounded;
        public bool IsDiving => diving;
        public bool IsStunned => stunTimer > 0f;
        public float PlanarSpeed { get; private set; }
        public float VerticalSpeed => rb.linearVelocity.y;

        public event Action Jumped;
        public event Action Knocked;
        public event Action Landed;

        const float JumpBufferTime = 0.15f;
        const float CoyoteTime = 0.12f;

        Rigidbody rb;
        CapsuleCollider capsule;
        Vector2 moveInput;
        bool diveQueued;
        float lastJumpPressTime = -10f;
        float lastGroundedTime = -10f;
        float stunTimer;
        float knockCooldown;
        float groundIgnoreTimer;
        float diveRecoverTimer;
        bool grounded;
        bool wasGrounded;
        bool diving;
        Vector3 groundNormal = Vector3.up;
        Vector3 groundVelocity;
        int groundMask;

        void Awake()
        {
            gameObject.layer = Layers.Player;
            groundMask = ~(1 << Layers.Player);

            rb = GetComponent<Rigidbody>();
            rb.mass = 1f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.constraints = RigidbodyConstraints.FreezeRotation;

            capsule = GetComponent<CapsuleCollider>();
            capsule.height = 1.6f;
            capsule.radius = 0.4f;
            capsule.center = new Vector3(0f, 0.8f, 0f);
            capsule.sharedMaterial = new PhysicsMaterial("PlayerNoFriction")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounciness = 0f,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
        }

        void Update()
        {
            moveInput = Vector2.zero;
            if (!ControlEnabled)
            {
                diveQueued = false;
                return;
            }

            var kb = Keyboard.current;
            if (kb != null)
            {
                float x = 0f, y = 0f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) y -= 1f;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) y += 1f;
                moveInput = Vector2.ClampMagnitude(new Vector2(x, y), 1f);

                if (kb.spaceKey.wasPressedThisFrame) lastJumpPressTime = Time.time;
                if (kb.leftCtrlKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame) diveQueued = true;
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState == CursorLockMode.Locked)
                diveQueued = true;
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            stunTimer -= dt;
            knockCooldown -= dt;
            groundIgnoreTimer -= dt;
            diveRecoverTimer -= dt;

            CheckGround();
            if (grounded)
            {
                lastGroundedTime = Time.time;
                if (!wasGrounded) Landed?.Invoke();
            }
            wasGrounded = grounded;

            if (diving && grounded)
            {
                diving = false;
                diveRecoverTimer = 0.4f;
            }

            Vector3 vel = rb.linearVelocity;
            bool canControl = ControlEnabled && stunTimer <= 0f && !diving && diveRecoverTimer <= 0f;

            Vector3 desired = Vector3.zero;
            if (canControl && moveInput.sqrMagnitude > 0.0001f)
            {
                Vector3 fwd = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
                fwd.y = 0f;
                if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
                fwd.Normalize();
                Vector3 right = Vector3.Cross(Vector3.up, fwd);
                desired = (fwd * moveInput.y + right * moveInput.x) * moveSpeed;
            }

            if (grounded && stunTimer <= 0f)
            {
                // Walk along the ground plane and ride moving platforms.
                rb.useGravity = false;
                Vector3 relative = Vector3.ProjectOnPlane(vel - groundVelocity, groundNormal);
                Vector3 target = Vector3.ProjectOnPlane(desired, groundNormal);
                float accel = diveRecoverTimer > 0f ? 18f : groundAcceleration;
                relative = Vector3.MoveTowards(relative, target, accel * dt);
                vel = relative + groundVelocity - groundNormal * 1f;
            }
            else
            {
                rb.useGravity = true;
                Vector3 planar = new Vector3(vel.x, 0f, vel.z);
                if (grounded)
                {
                    // Stunned on the ground: slide to a stop.
                    Vector3 gv = new Vector3(groundVelocity.x, 0f, groundVelocity.z);
                    planar = Vector3.MoveTowards(planar, gv, 10f * dt);
                }
                else if (canControl && desired.sqrMagnitude > 0.0001f)
                {
                    planar = Vector3.MoveTowards(planar, desired, airAcceleration * dt);
                }
                vel.x = planar.x;
                vel.z = planar.z;
                if (vel.y < 0f) vel += Physics.gravity * ((fallGravityMultiplier - 1f) * dt);
            }

            // Jump (with buffering and coyote time).
            if (canControl && Time.time - lastJumpPressTime <= JumpBufferTime && Time.time - lastGroundedTime <= CoyoteTime)
            {
                vel.y = jumpSpeed + Mathf.Max(0f, groundVelocity.y);
                lastJumpPressTime = -10f;
                lastGroundedTime = -10f;
                LeaveGround();
                Jumped?.Invoke();
            }

            // Dive.
            if (diveQueued)
            {
                diveQueued = false;
                if (ControlEnabled && stunTimer <= 0f && !diving && diveRecoverTimer <= 0f)
                {
                    Vector3 dir = desired.sqrMagnitude > 0.01f ? desired.normalized : transform.forward;
                    vel = dir * diveForwardSpeed + Vector3.up * (grounded ? diveUpSpeed : diveUpSpeed * 0.6f);
                    diving = true;
                    LeaveGround();
                }
            }

            rb.linearVelocity = vel;
            PlanarSpeed = new Vector3(vel.x - groundVelocity.x, 0f, vel.z - groundVelocity.z).magnitude;

            // Face the movement direction.
            Vector3 face = diving ? new Vector3(vel.x, 0f, vel.z) : desired;
            if (face.sqrMagnitude > 0.01f && stunTimer <= 0f)
            {
                Quaternion look = Quaternion.LookRotation(face.normalized, Vector3.up);
                rb.MoveRotation(Quaternion.Slerp(rb.rotation, look, 1f - Mathf.Exp(-turnSpeed * dt)));
            }
        }

        void LeaveGround()
        {
            grounded = false;
            wasGrounded = false;
            groundIgnoreTimer = 0.15f;
            rb.useGravity = true;
        }

        void CheckGround()
        {
            grounded = false;
            groundNormal = Vector3.up;
            groundVelocity = Vector3.zero;
            if (groundIgnoreTimer > 0f) return;

            float r = capsule.radius * 0.9f;
            Vector3 origin = rb.position + Vector3.up * (r + 0.05f);
            if (Physics.SphereCast(origin, r, Vector3.down, out var hit, 0.22f, groundMask, QueryTriggerInteraction.Ignore)
                && hit.normal.y > 0.55f)
            {
                grounded = true;
                groundNormal = hit.normal;
                var mover = hit.collider.GetComponentInParent<IKinematicMover>();
                if (mover != null) groundVelocity = mover.GetPointVelocity(hit.point);
            }
        }

        void OnCollisionEnter(Collision c)
        {
            HandleHazard(c);
        }
        void OnCollisionStay(Collision c) => HandleHazard(c);

        void HandleHazard(Collision c)
        {
            if (knockCooldown > 0f || c.contactCount == 0) return;
            var hazard = c.collider.GetComponentInParent<Hazard>();
            if (hazard == null) return;
            Vector3 point = c.GetContact(0).point;
            Knock(hazard.ComputeKnock(point, rb.position, c.rigidbody), hazard.stunTime);
        }

        public void Knock(Vector3 velocity, float stun)
        {
            if (knockCooldown > 0f) return;
            rb.linearVelocity = velocity;
            stunTimer = stun;
            knockCooldown = 0.4f;
            diving = false;
            LeaveGround();
            Knocked?.Invoke();
        }

        public void Launch(float upSpeed)
        {
            Vector3 v = rb.linearVelocity;
            v.y = upSpeed;
            rb.linearVelocity = v;
            diving = false;
            LeaveGround();
            Jumped?.Invoke();
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            rb.linearVelocity = Vector3.zero;
            rb.position = position;
            rb.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            stunTimer = 0f;
            diving = false;
            diveRecoverTimer = 0f;
            groundIgnoreTimer = 0f;
        }
    }
}
