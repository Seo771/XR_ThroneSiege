using System;
using UnityEngine;

namespace ThroneSiege.Combat
{
    public sealed class ActorHealth : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [SerializeField] private bool drivePlayerAnimator;
        [SerializeField] private float currentHealth;
        public float CurrentHealth => currentHealth;
        public bool IsDead => CurrentHealth <= 0f;
        public event Action Damaged;
        public event Action Died;

        private void Awake() { currentHealth = maxHealth; }

        public void TakeDamage(float amount)
        {
            if (IsDead || amount <= 0f) return;
            currentHealth = Mathf.Max(0f, CurrentHealth - amount);
            if (IsDead)
            {
                Died?.Invoke();
                if (drivePlayerAnimator)
                {
                    GetComponent<Animator>()?.SetBool("Dead", true);
                    // Stop the Standard Assets player motor when the player dies.
                    foreach (MonoBehaviour component in GetComponents<MonoBehaviour>())
                        if (component is UnityStandardAssets.Characters.ThirdPerson.ThirdPersonCharacter ||
                            component is UnityStandardAssets.Characters.ThirdPerson.ThirdPersonUserControl ||
                            component is ThroneSiege.Player.InPlaceRootMotionFallback)
                            component.enabled = false;
                    Rigidbody body = GetComponent<Rigidbody>();
                    if (body != null) body.velocity = Vector3.zero;
                }
            }
            else
            {
                Damaged?.Invoke();
                if (drivePlayerAnimator) GetComponent<Animator>()?.SetTrigger("Hit");
            }
        }

        public void ConfigurePlayer() { drivePlayerAnimator = true; }
    }
}
