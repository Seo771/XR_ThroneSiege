using UnityEngine;
using UnityEngine.AI;
using ThroneSiege.Combat;
using UnityStandardAssets.Characters.ThirdPerson;

namespace ThroneSiege.AI
{
    [DefaultExecutionOrder(-20)]
    [RequireComponent(typeof(NavMeshAgent), typeof(Animator), typeof(ActorHealth))]
    [RequireComponent(typeof(EnemyStats))]
    public sealed class CrusaderTankAI : MonoBehaviour
    {
        public enum BehaviourState { Idle, Chase, CombatIdle, Attack, Hit, Death }
        [SerializeField] private Transform target;
        [SerializeField, Min(0f)] private float detectionRange = 15f;
        [SerializeField, Min(0.1f)] private float attackRange = 1.9f;
        [field: SerializeField] public BehaviourState State { get; private set; }
        public Transform Target => target;
        private NavMeshAgent agent;
        private Animator animator;
        private ActorHealth health;
        private EnemyStats stats;
        private ActorHealth targetHealth;
        private float nextAttackTime;
        private readonly AnimatorActionProgress actionProgress = new AnimatorActionProgress();
        private AICharacterControl movement;
        private bool impactConsumed;
        private bool facingLocked;
        private int attackIndex;
        private int comboIndex;
        private bool battleActive = true;
        private bool cinematicPaused;

        public void SetCinematicPaused(bool paused)
        {
            if (cinematicPaused == paused) return;
            cinematicPaused = paused;
            if (health == null || health.IsDead) return;
            if (paused)
            {
                Stop();
                impactConsumed = true;
                facingLocked = true;
                State = BehaviourState.Idle;
                animator.ResetTrigger("Attack");
                animator.ResetTrigger("Hit");
                animator.SetFloat("Speed", 0f);
                animator.SetBool("InCombat", false);
                animator.CrossFadeInFixedTime("Idle", 0.08f, 0);
            }
            else
            {
                // Interrupted attacks never resume their old impact window.
                nextAttackTime = Mathf.Max(nextAttackTime, Time.time + stats.AttackCooldown);
            }
        }

        public void SetBattleActive(bool active)
        {
            battleActive = active;
            if (active || health.IsDead) return;
            Stop();
            State = BehaviourState.Idle;
            animator.ResetTrigger("Attack");
            animator.ResetTrigger("Hit");
            animator.SetBool("InCombat", false);
            animator.SetFloat("Speed", 0f);
            if (animator.isActiveAndEnabled && animator.runtimeAnimatorController != null)
                animator.CrossFadeInFixedTime("Idle", 0.08f, 0);
        }

        public void Configure(Transform player) { target = player; }

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            movement = GetComponent<AICharacterControl>();
            if (movement != null) movement.enabled = false;
            animator = GetComponent<Animator>();
            health = GetComponent<ActorHealth>();
            stats = GetComponent<EnemyStats>();
            animator.applyRootMotion = false;
            agent.updateRotation = false;
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        private void Start()
        {
            if (target == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) target = player.transform;
            }
            if (target != null) targetHealth = target.GetComponent<ActorHealth>();
            if (!agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3f, agent.areaMask))
                agent.Warp(hit.position);
            State = BehaviourState.Idle;
        }

        private void Update()
        {
            if (!battleActive || cinematicPaused || State == BehaviourState.Death) return;
            if (State == BehaviourState.Attack)
            {
                if (!facingLocked && target != null) FaceTarget();
                // 잘린 종료 이벤트 대신 실제 애니메이션의 종료/전이도 감지한다.
                if (actionProgress.HasFinished(animator)) AttackFinished();
                return;
            }
            if (State == BehaviourState.Hit)
            {
                if (!actionProgress.HasFinished(animator)) return;
                State = BehaviourState.CombatIdle;
            }
            if (target == null || (targetHealth != null && targetHealth.IsDead))
            { Stop(); animator.SetBool("InCombat", false); State = BehaviourState.Idle; return; }

            float distance = Vector3.Distance(transform.position, target.position);
            if (distance > detectionRange)
            { Stop(); animator.SetBool("InCombat", false); State = BehaviourState.Idle; return; }

            // 추적 시작과 종료 거리에 여유를 두어 경계에서 이동과 정지가 반복되지 않게 한다.
            float range = State == BehaviourState.Chase ? attackRange : attackRange + 0.3f;
            if (distance > range)
            {
                State = BehaviourState.Chase;
                animator.SetBool("InCombat", false);
                if (!agent.enabled || !agent.isOnNavMesh) { Stop(); return; }
                agent.speed = distance > 4f ? stats.RunSpeed : stats.WalkSpeed;
                agent.stoppingDistance = attackRange - 0.15f;
                agent.isStopped = false;
                if (movement != null)
                {
                    movement.SetTarget(target);
                    movement.enabled = true;
                }
                return;
            }

            Stop();
            State = BehaviourState.CombatIdle;
            animator.SetBool("InCombat", true);
            FaceTarget();
            if (Time.time >= nextAttackTime && distance <= attackRange &&
                Vector3.Angle(transform.forward, target.position - transform.position) < 20f && HasClearAttackPath())
                BeginAttack(distance);
        }

        private void BeginAttack(float distance)
        {
            State = BehaviourState.Attack;
            impactConsumed = false;
            facingLocked = false;
            attackIndex = distance < 1.35f && comboIndex % 3 == 2 ? 2 : comboIndex % 2;
            comboIndex++;
            animator.SetInteger("AttackIndex", attackIndex);
            animator.SetTrigger("Attack");
            actionProgress.Begin(attackIndex == 2 ? "ShieldAttack" : attackIndex == 1 ? "Attack2" : "Attack1");
        }

        // 프로젝트 전용 공격 클립에서 호출한다. 공격 방향을 고정해 플레이어가 피할 틈을 준다.
        public void LockAttackFacing() { if (isActiveAndEnabled && State == BehaviourState.Attack) facingLocked = true; }

        public void AttackImpact()
        {
            if (!isActiveAndEnabled || !battleActive || cinematicPaused || State != BehaviourState.Attack || health.IsDead || impactConsumed) return;
            impactConsumed = true;
            if (target == null || targetHealth == null || targetHealth.IsDead) return;
            Vector3 offset = target.position - transform.position;
            if (offset.magnitude > attackRange + 0.25f || Vector3.Angle(transform.forward, offset) > 65f) return;
            if (!HasClearAttackPath()) return;
            targetHealth.TakeDamage(attackIndex == 2 ? stats.AttackDamage * 0.75f : stats.AttackDamage);
        }

        private bool HasClearAttackPath()
        {
            if (target == null) return false;
            Vector3 origin = transform.position + Vector3.up;
            Vector3 offset = target.position + Vector3.up - origin;
            // Detect other bodies already overlapping the ray origin as well.
            foreach (var collider in Physics.OverlapSphere(origin, 0.03f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (!collider.transform.IsChildOf(transform) && !collider.transform.IsChildOf(target)) return false;
            foreach (RaycastHit hit in Physics.RaycastAll(origin, offset.normalized, offset.magnitude,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform) && !hit.transform.IsChildOf(target)) return false;
            return true;
        }

        public void AttackFinished()
        {
            if (State != BehaviourState.Attack) return;
            State = BehaviourState.CombatIdle;
            nextAttackTime = Time.time + stats.AttackCooldown;
        }

        private void OnDamaged()
        {
            // 일반 피격으로 이미 시작한 공격을 중단하지 않는다.
            if (!isActiveAndEnabled || !battleActive || cinematicPaused || State == BehaviourState.Attack || State == BehaviourState.Hit) return;
            Stop(); State = BehaviourState.Hit;
            actionProgress.Begin("Hit");
            animator.SetTrigger("Hit");
        }

        private void OnDied()
        {
            Stop(); State = BehaviourState.Death;
            animator.ResetTrigger("Attack"); animator.ResetTrigger("Hit");
            animator.SetBool("Dead", true);
            agent.enabled = false;
            var body = GetComponent<Rigidbody>();
            if (body != null)
            {
                body.velocity = Vector3.zero;
                body.useGravity = false;
                body.isKinematic = true;
            }
            foreach (Collider collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
        }

        private void Stop()
        {
            GetComponent<StandardAIAnimationBridge>()?.CancelRepel();
            if (movement != null) { movement.enabled = false; movement.SetTarget(null); }
            if (agent.enabled && agent.isOnNavMesh) { agent.isStopped = true; agent.ResetPath(); }
            var body = GetComponent<Rigidbody>();
            if (body != null && !body.isKinematic) body.velocity = new Vector3(0f, body.velocity.y, 0f);
        }
        private void FaceTarget()
        { Vector3 direction = target.position - transform.position; direction.y = 0f; if (direction.sqrMagnitude > 0.001f) FaceDirection(direction); }
        private void FaceDirection(Vector3 direction)
        { transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 240f * Time.deltaTime); }
        private void OnDisable() { if (agent != null) Stop(); }
        private void OnDestroy() { if (health != null) { health.Damaged -= OnDamaged; health.Died -= OnDied; } }
    }
}
