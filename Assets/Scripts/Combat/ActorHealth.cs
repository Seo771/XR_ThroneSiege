using System;
using System.Collections.Generic;
using UnityEngine;

namespace ThroneSiege.Combat
{
    [DisallowMultipleComponent]
    public sealed class ActorHealth : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [Tooltip("플레이어 피격·사망 연동")]
        [SerializeField] private bool drivePlayerAnimator;
        [SerializeField] private float currentHealth;
        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public float HealthRatio => currentHealth / maxHealth;
        public bool IsDead => CurrentHealth <= 0f;
        public event Action HealthChanged;
        public event Action Damaged;
        public event Action Died;
        private bool initialized;
        private readonly List<MonoBehaviour> stoppedPlayerComponents = new List<MonoBehaviour>();

        private void Awake()
        {
            // 능력치 컴포넌트가 먼저 초기화한 체력을 유지한다.
            if (!initialized) ResetHealth(maxHealth);
        }

        public void ResetHealth(float maximum)
        {
            maxHealth = Mathf.Max(1f, maximum);
            currentHealth = maxHealth;
            initialized = true;
            foreach (MonoBehaviour component in stoppedPlayerComponents)
                if (component != null) component.enabled = true;
            stoppedPlayerComponents.Clear();

            if (drivePlayerAnimator)
            {
                Animator animator = GetComponent<Animator>();
                if (animator != null && animator.runtimeAnimatorController != null)
                {
                    // 사망 상태에는 나가는 전이가 없으므로 기본 애니메이션 상태로 복구한다.
                    animator.Rebind();
                    animator.Update(0f);
                }
            }
            HealthChanged?.Invoke();
        }

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
                    // 플레이어가 사망하면 Standard Assets 이동 컴포넌트도 중지한다.
                    foreach (MonoBehaviour component in GetComponents<MonoBehaviour>())
                        if (component is UnityStandardAssets.Characters.ThirdPerson.ThirdPersonCharacter ||
                            component is UnityStandardAssets.Characters.ThirdPerson.ThirdPersonUserControl ||
                            component is ThroneSiege.Player.InPlaceRootMotionFallback)
                        {
                            if (component.enabled) stoppedPlayerComponents.Add(component);
                            component.enabled = false;
                        }
                    Rigidbody body = GetComponent<Rigidbody>();
                    if (body != null) body.velocity = Vector3.zero;
                }
            }
            else
            {
                Damaged?.Invoke();
                if (drivePlayerAnimator) GetComponent<Animator>()?.SetTrigger("Hit");
            }
            HealthChanged?.Invoke();
        }

        public void ConfigurePlayer() { drivePlayerAnimator = true; }
    }
}
