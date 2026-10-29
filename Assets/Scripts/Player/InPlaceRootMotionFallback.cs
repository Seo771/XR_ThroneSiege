using UnityEngine;

namespace ThroneSiege.Player
{
    /// <summary>
    /// 제자리 애니메이션에서도 Standard Assets 3인칭 컨트롤러가 이동하도록 보완한다.
    /// ThirdPersonCharacter는 원래 Animator.deltaPosition으로 수평 속도를 계산한다.
    /// 클립에 루트 이동이 없으면 이 컴포넌트가 수평 이동 속도를 제공한다.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    [RequireComponent(typeof(Animator), typeof(Rigidbody), typeof(CapsuleCollider))]
    public sealed class InPlaceRootMotionFallback : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float moveSpeed = 3.5f;
        [SerializeField, Min(0f)] private float groundProbeDistance = 0.35f;
        [Tooltip("이 값 이하의 루트 이동은 입력으로 보완")]
        [SerializeField, Min(0f)] private float rootMotionThreshold = 0.0001f;
        [SerializeField, Min(0f)] private float jumpPower = 12f;

        private Animator animator;
        private Rigidbody body;
        private CapsuleCollider capsule;
        private Transform cameraTransform;
        private bool jumpRequested;
        private PlayerCombat combat;
        public bool MovementLocked => combat != null && combat.IsAttacking;

        public void RequestJump() { if (!MovementLocked) jumpRequested = true; }
        public void ClearJumpRequest() { jumpRequested = false; }

        private void Update()
        {
            // Standard Assets는 Grounded 상태에서만 점프를 허용한다.
            // 원본을 수정하지 않고 플레이어의 기본 Idle 상태에서도 점프하도록 보완한다.
            if (MovementLocked) jumpRequested = false;
            else jumpRequested |= Input.GetButtonDown("Jump");
        }

        private void FixedUpdate()
        {
            if (!MovementLocked && jumpRequested && animator.GetCurrentAnimatorStateInfo(0).IsName("Idle")
                && !Input.GetKey(KeyCode.C) && IsGrounded() && body.velocity.y <= 0.1f)
            {
                Vector3 velocity = body.velocity;
                velocity.y = jumpPower;
                body.velocity = velocity;
                animator.SetTrigger("JumpRequested");
            }

            jumpRequested = false;
            // ThirdPersonUserControl과 ThirdPersonCharacter 처리 후,
            // AnimatePhysics가 전이를 평가하기 전에 지면 상태를 반영한다.
            // LateUpdate에서 처리하면 다음 물리 갱신 때 이동 컴포넌트가 OnGround를 덮어쓴다.
            UpdateGroundParameters();
        }

        private void Awake()
        {
            animator = GetComponent<Animator>();
            body = GetComponent<Rigidbody>();
            capsule = GetComponent<CapsuleCollider>();
            combat = GetComponent<PlayerCombat>();

            if (Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }
        }

        private void LateUpdate()
        {
            // 원본 컨트롤러의 검사 기준인 모델 피벗이 바닥 아래에 있을 수 있으므로
            // 캡슐 콜라이더의 실제 아래쪽을 기준으로 지면을 검사한다.
            if (animator.updateMode != AnimatorUpdateMode.AnimatePhysics)
                UpdateGroundParameters();
            // 카메라 기준 이동 방향으로 회전하는 동안 Forward가 0이 될 수 있으므로
            // 입력 벡터의 크기로 방향에 관계없이 이동 애니메이션을 갱신한다.
            float speed = Vector2.ClampMagnitude(new Vector2(
                Input.GetAxis("Horizontal"), Input.GetAxis("Vertical")), 1f).magnitude;
            animator.SetFloat("MoveSpeed", MovementLocked ? 0f : speed, 0.1f, Time.deltaTime);
        }

        private void UpdateGroundParameters()
        {
            bool grounded = body.velocity.y <= 0.1f && IsGrounded();
            animator.SetBool("OnGround", grounded);
            animator.SetFloat("Jump", grounded ? 0f : body.velocity.y);
        }

        private void OnAnimatorMove()
        {
            // PlayerStats can rebind/update the Animator before this Awake runs.
            if (animator == null || body == null || capsule == null) return;
            if (MovementLocked)
            {
                body.velocity = new Vector3(0f, body.velocity.y, 0f);
                return;
            }
            // 유효한 루트 이동이 있는 클립은 ThirdPersonCharacter가 처리한다.
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

            // ThirdPersonUserControl과 동일하게 Shift를 누르면 느린 걷기 속도를 적용한다.
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
