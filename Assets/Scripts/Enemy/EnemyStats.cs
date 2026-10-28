using ThroneSiege.Combat;
using UnityEngine;

namespace ThroneSiege.AI
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ActorHealth))]
    public sealed class EnemyStats : MonoBehaviour
    {
        [Header("기본 능력치")]
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [SerializeField, Min(0f)] private float attackDamage = 12f;
        [SerializeField, Min(0f)] private float attackCooldown = 1.3f;

        [Header("이동")]
        [SerializeField, Min(0f)] private float walkSpeed = 1.4f;
        [SerializeField, Min(0f)] private float runSpeed = 3f;

        public float MaxHealth => maxHealth;
        public float AttackDamage => attackDamage;
        public float AttackCooldown => attackCooldown;
        public float WalkSpeed => walkSpeed;
        public float RunSpeed => runSpeed;
        public ActorHealth Health { get; private set; }

        private void Awake()
        {
            Health = GetComponent<ActorHealth>();
            Health.ResetHealth(maxHealth);
        }
    }
}
