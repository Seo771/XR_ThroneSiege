using UnityEngine;

namespace ThroneSiege.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerStats), typeof(Animator), typeof(PlayerAttackHitbox))]
    public sealed class PlayerCombat : MonoBehaviour
    {
        [Header("기본 공격")]
        [Tooltip("Attack1 기준 임시 타격 시점 (초)")]
        [SerializeField, Min(0f)] private float hitDelay = 2.5f;
        [SerializeField, Min(0.1f)] private float attackDuration = 4.5f;

        public bool IsAttacking { get; private set; }
        public float CooldownRemaining => Mathf.Max(0f, nextAttackTime - Time.time);

        private PlayerStats stats;
        private Animator animator;
        private PlayerAttackHitbox hitbox;
        private float nextAttackTime;
        private float hitTime;
        private float finishTime;
        private bool hitApplied;

        private void Awake()
        {
            stats = GetComponent<PlayerStats>();
            animator = GetComponent<Animator>();
            hitbox = GetComponent<PlayerAttackHitbox>();
        }

        private void OnEnable()
        {
            stats.Health.Damaged += CancelAttack;
            stats.Health.Died += CancelAttack;
            stats.BattleEnded += CancelAttack;
            stats.BattleReset += ResetCombat;
        }

        private void OnDisable()
        {
            stats.Health.Damaged -= CancelAttack;
            stats.Health.Died -= CancelAttack;
            stats.BattleEnded -= CancelAttack;
            stats.BattleReset -= ResetCombat;
            CancelAttack();
        }

        private void Update()
        {
            if (!stats.IsBattleActive || stats.Health.IsDead)
            {
                CancelAttack();
                return;
            }

            if (IsAttacking)
            {
                if (!hitApplied && Time.time >= hitTime)
                {
                    hitApplied = true;
                    hitbox.ApplyDamage(stats.AttackDamage);
                }
                if (Time.time >= finishTime) IsAttacking = false;
            }

            if (Input.GetMouseButtonDown(0)) TryAttack();
        }

        public bool TryAttack()
        {
            if (!isActiveAndEnabled || !stats.IsBattleActive || stats.Health.IsDead ||
                IsAttacking || Time.time < nextAttackTime || !animator.isActiveAndEnabled ||
                animator.runtimeAnimatorController == null || animator.IsInTransition(0) ||
                !animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"))
                return false;

            IsAttacking = true;
            hitApplied = false;
            hitTime = Time.time + Mathf.Clamp(hitDelay, 0f, attackDuration);
            finishTime = Time.time + attackDuration;
            nextAttackTime = Time.time + stats.AttackCooldown;
            animator.SetTrigger("Attack1");
            return true;
        }

        private void CancelAttack()
        {
            if (!IsAttacking) return;
            IsAttacking = false;
            animator.ResetTrigger("Attack1");
        }

        private void ResetCombat()
        {
            CancelAttack();
            nextAttackTime = 0f;
        }
    }
}
