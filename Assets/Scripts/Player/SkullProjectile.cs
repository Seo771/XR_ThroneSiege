using ThroneSiege.Combat;
using UnityEngine;

namespace ThroneSiege.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SphereCollider), typeof(Rigidbody))]
    public sealed class SkullProjectile : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float speed = 8f;
        [SerializeField, Min(0.1f)] private float lifetime = 4f;
        public PlayerStats Owner { get; private set; }
        public float Damage { get; private set; }
        public Vector3 ContactCenter => transform.TransformPoint(trigger.center);
        public float ContactRadius => trigger.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x),
            Mathf.Abs(transform.lossyScale.y), Mathf.Abs(transform.lossyScale.z));
        public bool CanDamage => Owner != null && !Owner.Health.IsDead && Owner.IsBattleActive &&
            (battle == null || battle.State == BattleManager.BattleState.Fighting);
        private BattleManager battle;
        private Rigidbody body;
        private SphereCollider trigger;
        private Vector3 direction;
        private float expiresAt;
        private bool launched, stopped;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            trigger = GetComponent<SphereCollider>();
            body.useGravity = false;
            body.isKinematic = true;
            trigger.isTrigger = true;
        }

        public void Launch(PlayerStats player, BattleManager manager, Vector3 forward, float damage)
        {
            Owner = player; battle = manager; direction = forward.normalized; Damage = damage;
            expiresAt = Time.time + lifetime; launched = true;
            if (Owner != null)
                foreach (var collider in Owner.GetComponentsInChildren<Collider>())
                    Physics.IgnoreCollision(trigger, collider);
        }

        private void FixedUpdate()
        {
            if (!launched || stopped) return;
            if (!CanDamage || Time.time >= expiresAt) { Destroy(gameObject); return; }
            // Impact is detected exclusively by ActivateTrigger.OnTriggerEnter.
            float distance = Mathf.Min(speed * Time.fixedDeltaTime, ContactRadius * 1.8f);
            body.MovePosition(body.position + direction * distance);
        }

        public bool StopForExplosion()
        {
            if (!launched || stopped) return false;
            stopped = true;
            trigger.enabled = false;
            return true;
        }
    }
}
