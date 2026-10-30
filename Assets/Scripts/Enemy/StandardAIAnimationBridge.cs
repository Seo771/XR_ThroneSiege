using UnityEngine;
using UnityEngine.AI;
using UnityStandardAssets.Characters.ThirdPerson;

namespace ThroneSiege.AI
{
    // AICharacterControl supplies the path and calls ThirdPersonCharacter.Move.
    // This adapter supplies translation for in-place clips; only physics moves the body.
    [DefaultExecutionOrder(1000)]
    [RequireComponent(typeof(AICharacterControl), typeof(Rigidbody), typeof(Animator))]
    public sealed class StandardAIAnimationBridge : MonoBehaviour
    {
        private AICharacterControl control;
        private NavMeshAgent agent;
        private Rigidbody body;
        private Animator animator;
        private Vector3 repelTarget;
        private float repelUntil, repelSpeed;
        private Vector3 previousPosition;
        private float actualSpeed;

        public void RepelFrom(Vector3 origin, float distance, float duration)
        {
            if (body.isKinematic || !agent.enabled || !agent.isOnNavMesh || distance <= 0f) return;
            Vector3 direction = body.position - origin;
            direction.y = 0f;
            direction = direction.sqrMagnitude > 0.001f ? direction.normalized : transform.forward;
            Vector3 target = agent.nextPosition + direction * distance;
            if (NavMesh.Raycast(agent.nextPosition, target, out NavMeshHit edge, agent.areaMask))
                target = edge.position - direction * 0.1f;
            if (!NavMesh.SamplePosition(target, out NavMeshHit point, 0.3f, agent.areaMask)) return;
            Vector3 offset = point.position - body.position;
            if (Mathf.Abs(offset.y) > 0.5f) return;
            offset.y = 0f;
            if (Vector3.Dot(offset, direction) <= 0f) return;
            float travel = Mathf.Min(distance, offset.magnitude);
            foreach (var hit in body.SweepTestAll(direction, travel, QueryTriggerInteraction.Ignore))
                if (Vector3.Dot(hit.normal, direction) < -0.1f &&
                    hit.collider.GetComponentInParent<CrusaderTankAI>() == null)
                    travel = Mathf.Min(travel, Mathf.Max(0f, hit.distance - 0.05f));
            repelTarget = body.position + direction * travel;
            repelSpeed = travel / Mathf.Max(0.05f, duration);
            repelUntil = Time.time + Mathf.Max(0.05f, duration);
        }

        public void CancelRepel() { repelUntil = 0f; }
        private void Awake()
        {
            control = GetComponent<AICharacterControl>();
            agent = GetComponent<NavMeshAgent>();
            body = GetComponent<Rigidbody>();
            animator = GetComponent<Animator>();
            previousPosition = body.position;
        }
        private void Start() => SyncAgent();
        private void LateUpdate()
        {
            SyncAgent();
            animator.SetFloat("Speed", control.enabled && !body.isKinematic ? actualSpeed : 0f, 0.1f, Time.deltaTime);
        }
        private void SyncAgent()
        {
            agent.updatePosition = false;
            if (agent.enabled && agent.isOnNavMesh) agent.nextPosition = body.position;
        }
        private void FixedUpdate()
        {
            Vector3 displacement = body.position - previousPosition;
            displacement.y = 0f;
            previousPosition = body.position;
            float measured = displacement.magnitude / Time.fixedDeltaTime;
            // Ignore Warp/teleport jumps. Queueing agents animate from actual
            // displacement, so blocked enemies do not run on the spot.
            actualSpeed = measured < Mathf.Max(5f, agent.speed * 2f) ? measured : 0f;
            ApplyVelocity();
        }
        private void OnAnimatorMove() { if (body != null) ApplyVelocity(); }
        private void ApplyVelocity()
        {
            if (body.isKinematic) return;
            Vector3 velocity = control.enabled && agent.enabled && agent.isOnNavMesh && !agent.isStopped
                ? agent.desiredVelocity : Vector3.zero;
            if (Time.time < repelUntil)
            {
                Vector3 offset = repelTarget - body.position;
                offset.y = 0f;
                velocity = Vector3.ClampMagnitude(offset / Mathf.Max(0.02f, repelUntil - Time.time), repelSpeed);
                if (!agent.enabled || !agent.isOnNavMesh || NavMesh.Raycast(agent.nextPosition,
                    body.position + velocity * Time.fixedDeltaTime, out _, agent.areaMask))
                { velocity = Vector3.zero; CancelRepel(); }
            }
            velocity.y = body.velocity.y;
            body.velocity = velocity;
        }
    }
}
