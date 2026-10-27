using UnityEngine;

namespace ThroneSiege.Player
{
    /// <summary>
    /// Keeps the Standard Assets third-person controller usable with in-place clips.
    /// ThirdPersonCharacter normally derives horizontal velocity from Animator.deltaPosition.
    /// When the active clip has no root translation, this component supplies that velocity.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    [RequireComponent(typeof(Animator), typeof(Rigidbody), typeof(CapsuleCollider))]
    public sealed class InPlaceRootMotionFallback : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float moveSpeed = 3.5f;
        [SerializeField, Min(0f)] private float groundProbeDistance = 0.35f;
        [SerializeField, Min(0f)] private float rootMotionThreshold = 0.0001f;
        [SerializeField, Min(0f)] private float jumpPower = 12f;

        private Animator animator;
        private Rigidbody body;
        private CapsuleCollider capsule;
        private Transform cameraTransform;
        private bool jumpRequested;

        private void Update()
        {
            // Standard Assets only accepts jumps from a state named Grounded.
            // Support the player's Idle default state without changing that asset.
            jumpRequested |= Input.GetButtonDown("Jump");
        }

        private void FixedUpdate()
        {
            if (jumpRequested && animator.GetCurrentAnimatorStateInfo(0).IsName("Idle")
                && !Input.GetKey(KeyCode.C) && IsGrounded() && body.velocity.y <= 0.1f)
            {
                Vector3 velocity = body.velocity;
                velocity.y = jumpPower;
                body.velocity = velocity;
                animator.SetTrigger("JumpRequested");
            }

            jumpRequested = false;
        }

        private void Awake()
        {
            animator = GetComponent<Animator>();
            body = GetComponent<Rigidbody>();
            capsule = GetComponent<CapsuleCollider>();

            if (Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }
        }

        private void LateUpdate()
        {
            // The asset controller probes from the model pivot, which may be below
            // the floor for this mesh. Use the capsule's actual bottom instead.
            animator.SetBool("OnGround", body.velocity.y <= 0.1f && IsGrounded());
            // Magnitude works in every direction, including while turning toward
            // the camera-relative movement direction. Forward can be zero then.
            float speed = Vector2.ClampMagnitude(new Vector2(
                Input.GetAxis("Horizontal"), Input.GetAxis("Vertical")), 1f).magnitude;
            animator.SetFloat("MoveSpeed", speed, 0.1f, Time.deltaTime);
        }

        private void OnAnimatorMove()
        {
            // Let ThirdPersonCharacter handle clips that contain usable root translation.
            if (animator.deltaPosition.sqrMagnitude > rootMotionThreshold * rootMotionThreshold)
            {
                return;
            }

            if (!IsGrounded())
            {
                return;
            }

            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");

            Vector3 forward = Vector3.forward;
            Vector3 right = Vector3.right;

            if (cameraTransform != null)
            {
                forward = Vector3.Scale(cameraTransform.forward, new Vector3(1f, 0f, 1f)).normalized;
                right = cameraTransform.right;
                right.y = 0f;
                right.Normalize();
            }

            Vector3 desiredMove = Vector3.ClampMagnitude(vertical * forward + horizontal * right, 1f);

            // Match ThirdPersonUserControl: holding Shift selects the slower walk speed.
            if (Input.GetKey(KeyCode.LeftShift))
            {
                desiredMove *= 0.5f;
            }

            Vector3 velocity = body.velocity;
            velocity.x = desiredMove.x * moveSpeed;
            velocity.z = desiredMove.z * moveSpeed;
            body.velocity = velocity;
        }

        private bool IsGrounded()
        {
            Bounds bounds = capsule.bounds;
            Vector3 origin = new Vector3(bounds.center.x, bounds.min.y + 0.1f, bounds.center.z);
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down,
                groundProbeDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
            {
                if (hit.rigidbody != body && !hit.transform.IsChildOf(transform))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
