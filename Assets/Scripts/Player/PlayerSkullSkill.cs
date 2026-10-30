using ThroneSiege.Combat;
using UnityEngine;

namespace ThroneSiege.Player
{
    [DefaultExecutionOrder(-10)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerStats), typeof(PlayerCombat))]
    public sealed class PlayerSkullSkill : MonoBehaviour
    {
        [SerializeField] private SkullProjectile projectilePrefab;
        [SerializeField, Min(0f)] private float damage = 40f;
        [SerializeField, Min(0f)] private float cooldown = 5f;
        [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 1.2f, 0.7f);
        [SerializeField, Range(0f, 60f)] private float phaseTwoSpreadAngle = 15f;
        public float CooldownRemaining => Mathf.Max(0f, nextUseTime - Time.time);
        public bool UsedThisFrame => lastUseFrame == Time.frameCount;
        private PlayerStats stats;
        private PlayerCombat combat;
        private Animator animator;
        private BattleManager battle;
        private float nextUseTime;
        private int lastUseFrame = -1;

        private void Awake()
        {
            stats = GetComponent<PlayerStats>();
            combat = GetComponent<PlayerCombat>();
            animator = GetComponent<Animator>();
            foreach (var candidate in FindObjectsOfType<BattleManager>())
                if (candidate.gameObject.scene == gameObject.scene) { battle = candidate; break; }
        }
        private void OnEnable() => stats.BattleReset += ResetCooldown;
        private void OnDisable() => stats.BattleReset -= ResetCooldown;
        private void ResetCooldown() { nextUseTime = 0f; lastUseFrame = -1; }
        private void Update() { if (Input.GetKeyDown(KeyCode.Q)) TryUse(); }

        public bool TryUse()
        {
            if (!isActiveAndEnabled || projectilePrefab == null || !stats.IsBattleActive || stats.Health.IsDead ||
                combat.IsAttacking || combat.IsTransforming || combat.IsPhaseTwoPending || Time.time < nextUseTime ||
                (battle != null && battle.State != BattleManager.BattleState.Fighting) ||
                animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null ||
                animator.IsInTransition(0) || !animator.GetCurrentAnimatorStateInfo(0).IsName("Grounded") ||
                !animator.GetBool("OnGround")) return false;

            // Prevent spawning beyond a wall immediately in front of the player.
            Vector3 origin = transform.position + Vector3.up * spawnOffset.y;
            Vector3 offset = transform.TransformDirection(new Vector3(spawnOffset.x, 0f, spawnOffset.z));
            foreach (var hit in Physics.RaycastAll(origin, offset.normalized, offset.magnitude,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform)) return false;
            nextUseTime = Time.time + cooldown;
            lastUseFrame = Time.frameCount;
            int count = stats.IsPhaseTwo ? 3 : 1;
            var projectiles = new SkullProjectile[count];
            for (int i = 0; i < count; i++)
            {
                float angle = count == 1 ? 0f : (i - 1) * phaseTwoSpreadAngle;
                Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * transform.forward;
                var projectile = Instantiate(projectilePrefab, origin + offset, Quaternion.LookRotation(direction));
                projectile.Launch(stats, battle, direction, damage, 1f / count);
                // All shots begin at the same point. Prevent the original
                // ActivateTrigger from firing on the other shots in this volley.
                for (int j = 0; j < i; j++)
                    foreach (var current in projectile.GetComponentsInChildren<Collider>())
                        foreach (var previous in projectiles[j].GetComponentsInChildren<Collider>())
                            Physics.IgnoreCollision(current, previous);
                projectiles[i] = projectile;
            }
            return true;
        }
    }
}
