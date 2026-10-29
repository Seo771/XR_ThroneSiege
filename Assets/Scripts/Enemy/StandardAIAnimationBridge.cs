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
        private void Awake()
        {
            control = GetComponent<AICharacterControl>();
            agent = GetComponent<NavMeshAgent>();
            body = GetComponent<Rigidbody>();
            animator = GetComponent<Animator>();
        }
        private void Start() => SyncAgent();
        private void LateUpdate() => SyncAgent();
        private void SyncAgent()
        {
            agent.updatePosition = false;
            if (agent.enabled && agent.isOnNavMesh) agent.nextPosition = body.position;
        }
        private void FixedUpdate() => ApplyVelocity();
        private void OnAnimatorMove() { if (body != null) ApplyVelocity(); }
        private void ApplyVelocity()
        {
            if (body.isKinematic) return;
            Vector3 velocity = control.enabled && agent.enabled && agent.isOnNavMesh && !agent.isStopped
                ? agent.desiredVelocity : Vector3.zero;
            velocity.y = body.velocity.y;
            body.velocity = velocity;
            animator.SetFloat("Speed", new Vector2(velocity.x, velocity.z).magnitude, 0.1f, Time.deltaTime);
        }
    }
}
