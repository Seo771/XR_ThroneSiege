using System.Collections.Generic;
using ThroneSiege.AI;
using ThroneSiege.Combat;
using UnityEngine;

namespace ThroneSiege.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerStats))]
    public sealed class PlayerAttackHitbox : MonoBehaviour
    {
        [Header("공격 범위")]
        [SerializeField, Min(0.1f)] private float range = 2.5f;
        [SerializeField, Range(0f, 360f)] private float angle = 100f;
        [SerializeField, Min(0f)] private float originHeight = 1f;

        private readonly HashSet<ActorHealth> hitTargets = new HashSet<ActorHealth>();
        private PlayerStats stats;

        private void Awake() { stats = GetComponent<PlayerStats>(); }

        public void ApplyDamage(float damage)
        {
            if (damage <= 0f || !stats.IsBattleActive || stats.Health.IsDead) return;
            hitTargets.Clear();
            Vector3 origin = transform.position + Vector3.up * originHeight;
            foreach (Collider collider in Physics.OverlapSphere(origin, range,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                // 사망 이벤트에서 승패가 결정되면 남은 대상에게 피해를 주지 않는다.
                if (!stats.IsBattleActive || stats.Health.IsDead) break;
                EnemyStats enemy = collider.GetComponentInParent<EnemyStats>();
                if (enemy == null || enemy.transform.IsChildOf(transform) ||
                    enemy.Health == null || enemy.Health.IsDead || hitTargets.Contains(enemy.Health))
                    continue;

                Vector3 direction = enemy.transform.position - transform.position;
                direction.y = 0f;
                if (Vector3.Angle(transform.forward, direction) > angle * 0.5f) continue;

                Vector3 offset = collider.bounds.center - origin;
                bool blocked = false;
                foreach (RaycastHit hit in Physics.RaycastAll(origin, offset.normalized, offset.magnitude,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    if (hit.transform.IsChildOf(transform) || hit.transform.IsChildOf(enemy.transform)) continue;
                    blocked = true;
                    break;
                }
                if (blocked) continue;

                hitTargets.Add(enemy.Health);
                enemy.Health.TakeDamage(damage);
            }
        }
    }
}
