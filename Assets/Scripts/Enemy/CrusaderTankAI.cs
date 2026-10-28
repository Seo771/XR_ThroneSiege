using UnityEngine;
using UnityEngine.AI;
using ThroneSiege.Combat;

namespace ThroneSiege.AI
{
    [RequireComponent(typeof(NavMeshAgent), typeof(Animator), typeof(ActorHealth))]
    [RequireComponent(typeof(EnemyStats))]
    public sealed class CrusaderTankAI : MonoBehaviour
    {
        public enum BehaviourState { Idle, Chase, CombatIdle, Attack, Hit, Death }
        [SerializeField] private Transform target;
        [SerializeField, Min(0f)] private float detectionRange = 15f;
        [SerializeField, Min(0.1f)] private float attackRange = 1.9f;
        [Tooltip("공격 1 / 공격 2 / 방패 공격 순서 (초)")]
        [SerializeField] private float[] attackDurations = { 2f, 2f, 2f };
        [SerializeField] private float hitDuration = 0.6f;
        [field: SerializeField] public BehaviourState State { get; private set; }
        public Transform Target => target;
        private NavMeshAgent agent;
        private Animator animator;
        private ActorHealth health;
        private EnemyStats stats;
        private ActorHealth targetHealth;
        private float nextAttackTime;
        private float stateEndTime;
        private float repathTime;
        private bool impactConsumed;
        private bool facingLocked;
        private int attackIndex;
        private int comboIndex;
        private bool battleActive = true;

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
        }

        public void Configure(Transform player, float[] durations, float reactionDuration)
        { target = player; attackDurations = durations; hitDuration = reactionDuration; }

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
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
            if (!battleActive || State == BehaviourState.Death) return;
            animator.SetFloat("Speed", agent.enabled && agent.isOnNavMesh ? agent.velocity.magnitude : 0f, 0.1f, Time.deltaTime);
            if (State == BehaviourState.Attack)
            {
                if (!facingLocked && target != null) FaceTarget();
                // 애니메이션 이벤트가 누락돼도 제한 시간이 지나면 공격 상태를 종료한다.
                if (Time.time >= stateEndTime) AttackFinished();
                return;
            }
            if (State == BehaviourState.Hit)
            {
                if (Time.time < stateEndTime) return;
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
                if (!agent.enabled || !agent.isOnNavMesh) return;
                agent.speed = distance > 4f ? stats.RunSpeed : stats.WalkSpeed;
                agent.stoppingDistance = attackRange - 0.15f;
                agent.isStopped = false;
                if (Time.time >= repathTime)
                { agent.SetDestination(target.position); repathTime = Time.time + 0.15f; }
                Vector3 direction = agent.desiredVelocity;
                direction.y = 0f;
                if (direction.sqrMagnitude > 0.01f) FaceDirection(direction);
                return;
            }

            Stop();
            State = BehaviourState.CombatIdle;
            animator.SetBool("InCombat", true);
            FaceTarget();
            if (Time.time >= nextAttackTime && distance <= attackRange &&
                Vector3.Angle(transform.forward, target.position - transform.position) < 20f)
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
            stateEndTime = Time.time + attackDurations[attackIndex] + 0.4f;
        }

        // 프로젝트 전용 공격 클립에서 호출한다. 공격 방향을 고정해 플레이어가 피할 틈을 준다.
        public void LockAttackFacing() { if (State == BehaviourState.Attack) facingLocked = true; }

        public void AttackImpact()
        {
            if (!battleActive || State != BehaviourState.Attack || health.IsDead || impactConsumed) return;
            impactConsumed = true;
            if (target == null || targetHealth == null || targetHealth.IsDead) return;
            Vector3 offset = target.position - transform.position;
            if (offset.magnitude > attackRange + 0.25f || Vector3.Angle(transform.forward, offset) > 65f) return;
            // 두 캐릭터 사이에 장애물이 있으면 피해를 막는다. 양측 캐릭터의 콜라이더는 제외한다.
            Vector3 origin = transform.position + Vector3.up;
            foreach (RaycastHit hit in Physics.RaycastAll(origin, offset.normalized, offset.magnitude,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform) && !hit.transform.IsChildOf(target)) return;
            targetHealth.TakeDamage(attackIndex == 2 ? stats.AttackDamage * 0.75f : stats.AttackDamage);
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
            if (!battleActive || State == BehaviourState.Attack || State == BehaviourState.Hit) return;
            Stop(); State = BehaviourState.Hit;
            stateEndTime = Time.time + hitDuration;
            animator.SetTrigger("Hit");
        }

        private void OnDied()
        {
            Stop(); State = BehaviourState.Death;
            animator.ResetTrigger("Attack"); animator.ResetTrigger("Hit");
            animator.SetBool("Dead", true);
            agent.enabled = false;
            foreach (Collider collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
        }

        private void Stop()
        { if (agent.enabled && agent.isOnNavMesh) { agent.isStopped = true; agent.ResetPath(); } }
        private void FaceTarget()
        { Vector3 direction = target.position - transform.position; direction.y = 0f; if (direction.sqrMagnitude > 0.001f) FaceDirection(direction); }
        private void FaceDirection(Vector3 direction)
        { transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 240f * Time.deltaTime); }
        private void OnDestroy() { if (health != null) { health.Damaged -= OnDamaged; health.Died -= OnDied; } }
    }
}
