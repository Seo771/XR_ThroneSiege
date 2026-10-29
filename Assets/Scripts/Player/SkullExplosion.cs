using System.Linq;
using ThroneSiege.AI;
using UnityEngine;

namespace ThroneSiege.Player
{
    [DisallowMultipleComponent]
    public sealed class SkullExplosion : MonoBehaviour
    {
        [SerializeField] private GameObject skullVisual;
        private void OnEnable()
        {
            var projectile = GetComponentInParent<SkullProjectile>();
            if (projectile == null || !projectile.StopForExplosion()) return;
            if (skullVisual != null) skullVisual.SetActive(false);
            foreach (var force in GetComponentsInChildren<UnityStandardAssets.Effects.ExplosionPhysicsForce>(true))
                force.enabled = false;
            float duration = 0.1f;
            foreach (var particle in GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = particle.main;
                main.loop = false;
                duration = Mathf.Max(duration, main.startDelay.constantMax + main.duration + main.startLifetime.constantMax);
            }
            ApplyContactDamage(projectile);
            Destroy(projectile.gameObject, duration);
        }

        private static void ApplyContactDamage(SkullProjectile projectile)
        {
            if (!projectile.CanDamage) return;
            Vector3 origin = projectile.ContactCenter;
            // ActivateTrigger supplies no collider argument. Resolve contact locally.
            foreach (var collider in Physics.OverlapSphere(origin, projectile.ContactRadius + 0.03f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                .OrderBy(c => (c.ClosestPoint(origin) - origin).sqrMagnitude))
            {
                var enemy = collider.GetComponentInParent<EnemyStats>();
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead) continue;
                Vector3 offset = collider.bounds.center - origin;
                bool blocked = Physics.RaycastAll(origin, offset.normalized, offset.magnitude,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore).Any(hit =>
                        !hit.transform.IsChildOf(projectile.transform) &&
                        !hit.transform.IsChildOf(projectile.Owner.transform) &&
                        !hit.transform.IsChildOf(enemy.transform));
                if (blocked) continue;
                enemy.Health.TakeDamage(projectile.Damage);
                return;
            }
        }
    }
}
