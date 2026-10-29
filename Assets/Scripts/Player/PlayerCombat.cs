using UnityEngine;
using ThroneSiege.Combat;
using UnityStandardAssets.Characters.ThirdPerson;

namespace ThroneSiege.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerStats), typeof(Animator), typeof(PlayerAttackHitbox))]
    public sealed class PlayerCombat : MonoBehaviour
    {
        public bool IsAttacking { get; private set; }
        public float CooldownRemaining => Mathf.Max(0f, nextAttackTime - Time.time);

        private PlayerStats stats;
        private Animator animator;
        private PlayerAttackHitbox hitbox;
        private float nextAttackTime;
        private readonly AnimatorActionProgress attackProgress = new AnimatorActionProgress();
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
            stats.Health.Died += CancelAttack;
            stats.BattleEnded += CancelAttack;
            stats.BattleReset += ResetCombat;
        }

        private void OnDisable()
        {
            stats.Health.Died -= CancelAttack;
            stats.BattleEnded -= CancelAttack;
            stats.BattleReset -= ResetCombat;
            CancelAttack();
        }

        private void Update()
        {
            if (!CanFight)
            {
                CancelAttack();
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
            if (!isActiveAndEnabled || !CanFight ||
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
            GetComponent<InPlaceRootMotionFallback>()?.ClearJumpRequest();
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
            GetComponent<InPlaceRootMotionFallback>()?.ClearJumpRequest();
            if (restoreInput && input != null && !stats.Health.IsDead) input.enabled = true;
            restoreInput = false;
        }

        private void ResetCombat()
        {
            CancelAttack();
            nextAttackTime = 0f;
        }
    }
}
