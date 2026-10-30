using UnityEngine;
using ThroneSiege.Combat;
using ThroneSiege.AI;
using UnityStandardAssets.Characters.ThirdPerson;

namespace ThroneSiege.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerStats), typeof(Animator), typeof(PlayerAttackHitbox))]
    public sealed class PlayerCombat : MonoBehaviour
    {
        public bool IsAttacking { get; private set; }
        public bool IsTransforming { get; private set; }
        public bool IsPhaseTwoPending => phasePending;
        public float CooldownRemaining => Mathf.Max(0f, nextAttackTime - Time.time);
        [Header("2페이즈 전환 밀어내기")]
        [SerializeField, Min(0f)] private float phasePushRadius = 4f;
        [SerializeField, Min(0f)] private float phasePushDistance = 2f;
        [SerializeField, Min(0.05f)] private float phasePushDuration = 0.3f;
        [SerializeField] private AudioSource attackAudio;
        [SerializeField] private AudioSource phaseTwoAudio;

        private PlayerStats stats;
        private Animator animator;
        private PlayerAttackHitbox hitbox;
        private float nextAttackTime;
        private readonly AnimatorActionProgress attackProgress = new AnimatorActionProgress();
        private readonly AnimatorActionProgress phaseProgress = new AnimatorActionProgress();
        private bool phasePending;
        private bool hitApplied;
        private ThirdPersonUserControl input;
        private bool restoreInput;
        private Rigidbody body;
        private BattleManager battle;

        private void Awake()
        {
            stats = GetComponent<PlayerStats>();
            animator = GetComponent<Animator>();
            hitbox = GetComponent<PlayerAttackHitbox>();
            input = GetComponent<ThirdPersonUserControl>();
            body = GetComponent<Rigidbody>();
            foreach (BattleManager candidate in FindObjectsOfType<BattleManager>())
                if (candidate.gameObject.scene == gameObject.scene) { battle = candidate; break; }
        }

        private void OnEnable()
        {
            stats.Health.Died += CancelActions;
            stats.BattleEnded += CancelActions;
            stats.PhaseTwoEntered += QueuePhaseTwo;
            stats.BattleReset += ResetCombat;
        }

        private void OnDisable()
        {
            stats.Health.Died -= CancelActions;
            stats.BattleEnded -= CancelActions;
            stats.PhaseTwoEntered -= QueuePhaseTwo;
            stats.BattleReset -= ResetCombat;
            CancelActions();
        }

        private void Update()
        {
            if (!CanFight)
            {
                CancelActions();
                return;
            }

            if (IsTransforming)
            {
                if (phaseProgress.HasFinished(animator)) FinishPhaseTwo();
                return;
            }
            if (phasePending && !IsAttacking && !animator.IsInTransition(0) &&
                animator.GetBool("OnGround") && animator.GetCurrentAnimatorStateInfo(0).IsName("Grounded"))
            {
                phasePending = false;
                IsTransforming = true;
                stats.Health.SetInvulnerable(true);
                if (battle != null) battle.SetPhaseTransitionPaused(true);
                PushNearbyEnemies();
                phaseProgress.Begin("PhaseTwo");
                restoreInput = input != null && input.enabled;
                if (input != null) input.enabled = false;
                if (body != null) body.velocity = new Vector3(0f, body.velocity.y, 0f);
                animator.SetFloat("MoveSpeed", 0f);
                animator.SetFloat("Forward", 0f);
                animator.SetFloat("Turn", 0f);
                animator.ResetTrigger("Hit");
                animator.ResetTrigger("JumpRequested");
                animator.SetTrigger("PhaseTwo");
                if (phaseTwoAudio != null && phaseTwoAudio.clip != null) phaseTwoAudio.Play();
                return;
            }

            if (IsAttacking)
            {
                if (attackProgress.HasFinished(animator)) CancelAttack();
            }

            if (Input.GetMouseButtonDown(0)) TryAttack();
        }

        public bool TryAttack()
        {
            if (!isActiveAndEnabled || !CanFight || IsTransforming || phasePending ||
                (GetComponent<PlayerSkullSkill>() != null && GetComponent<PlayerSkullSkill>().UsedThisFrame) ||
                IsAttacking || Time.time < nextAttackTime || !animator.isActiveAndEnabled ||
                animator.runtimeAnimatorController == null || animator.IsInTransition(0) ||
                !animator.GetBool("OnGround") ||
                !animator.GetCurrentAnimatorStateInfo(0).IsName("Grounded"))
                return false;

            IsAttacking = true;
            hitApplied = false;
            attackProgress.Begin("Attack1");
            nextAttackTime = Time.time + stats.AttackCooldown;
            restoreInput = input != null && input.enabled;
            if (input != null) input.enabled = false;
            if (body != null) body.velocity = new Vector3(0f, body.velocity.y, 0f);
            animator.ResetTrigger("JumpRequested");
            animator.SetFloat("MoveSpeed", 0f);
            animator.SetFloat("Forward", 0f);
            animator.SetFloat("Turn", 0f);
            animator.SetTrigger("Attack1");
            return true;
        }

        private bool CanFight => stats.IsBattleActive && !stats.Health.IsDead &&
            (battle == null || battle.State == BattleManager.BattleState.Fighting);

        // Called only by the project-owned Attack1 animation.
        public void BasicAttackImpact()
        {
            if (!isActiveAndEnabled || !IsAttacking || hitApplied || !CanFight) return;
            hitApplied = true;
            if (attackAudio != null && attackAudio.clip != null) attackAudio.PlayOneShot(attackAudio.clip);
            hitbox.ApplyDamage(stats.AttackDamage);
        }

        public void BasicAttackFinished()
        {
            if (IsAttacking) CancelAttack();
        }

        private void CancelAttack()
        {
            if (!IsAttacking) return;
            IsAttacking = false;
            animator.ResetTrigger("Attack1");
            if (restoreInput && input != null && !stats.Health.IsDead) input.enabled = true;
            restoreInput = false;
        }

        private void ResetCombat()
        {
            CancelActions();
            nextAttackTime = 0f;
        }

        private void QueuePhaseTwo()
        {
            phasePending = true;
            // The threshold hit has already queued Hit in ActorHealth.
            animator.ResetTrigger("Hit");
        }

        private void PushNearbyEnemies()
        {
            if (battle == null) return;
            foreach (var enemy in battle.Enemies)
            {
                if (enemy == null || enemy.GetComponent<ActorHealth>().IsDead) continue;
                Vector3 direction = enemy.transform.position - transform.position;
                direction.y = 0f;
                if (direction.sqrMagnitude > phasePushRadius * phasePushRadius) continue;
                Vector3 origin = transform.position + Vector3.up;
                bool blocked = false;
                foreach (var hit in Physics.RaycastAll(origin, direction.normalized, direction.magnitude,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    if (!hit.transform.IsChildOf(transform) && hit.collider.GetComponentInParent<CrusaderTankAI>() == null)
                    { blocked = true; break; }
                if (!blocked) enemy.GetComponent<StandardAIAnimationBridge>()?.RepelFrom(
                    transform.position, phasePushDistance, phasePushDuration);
            }
        }

        private void FinishPhaseTwo()
        {
            IsTransforming = false;
            if (phaseTwoAudio != null) phaseTwoAudio.Stop();
            stats.Health.SetInvulnerable(false);
            if (battle != null) battle.SetPhaseTransitionPaused(false);
            animator.ResetTrigger("PhaseTwo");
            if (restoreInput && input != null && !stats.Health.IsDead) input.enabled = true;
            restoreInput = false;
        }

        private void CancelActions()
        {
            phasePending = false;
            CancelAttack();
            if (IsTransforming) FinishPhaseTwo();
            animator.ResetTrigger("PhaseTwo");
        }
    }
}
