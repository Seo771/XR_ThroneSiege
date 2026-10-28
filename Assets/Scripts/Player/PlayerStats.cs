using System;
using ThroneSiege.Combat;
using UnityEngine;

namespace ThroneSiege.Player
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ActorHealth))]
    public sealed class PlayerStats : MonoBehaviour
    {
        [Header("기본 능력치")]
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [SerializeField, Min(0f)] private float baseAttackDamage = 25f;
        [SerializeField, Min(0f)] private float attackCooldown = 1f;

        [Header("2페이즈 강화")]
        [Tooltip("발동 체력 비율 (0.5 = 50%)")]
        [SerializeField, Range(0f, 1f)] private float phaseTwoHealthRatio = 0.5f;
        [SerializeField, Min(1f)] private float phaseTwoDamageMultiplier = 1.5f;

        public float MaxHealth => maxHealth;
        public float BaseAttackDamage => baseAttackDamage;
        public float AttackCooldown => attackCooldown;
        public float AttackDamage => baseAttackDamage * (IsPhaseTwo ? phaseTwoDamageMultiplier : 1f);
        public ActorHealth Health { get; private set; }
        public bool IsPhaseTwo { get; private set; }
        public bool IsBattleActive { get; private set; }
        public event Action PhaseTwoEntered;
        public event Action StatsChanged;
        public event Action BattleEnded;
        public event Action BattleReset;

        private void Awake()
        {
            Health = GetComponent<ActorHealth>();
            Health.ConfigurePlayer();
            ResetForNewBattle();
        }

        private void OnEnable()
        {
            Health.HealthChanged += OnHealthChanged;
            OnHealthChanged();
        }

        private void OnDisable() { Health.HealthChanged -= OnHealthChanged; }

        public void EndBattle()
        {
            if (!IsBattleActive) return;
            IsBattleActive = false;
            BattleEnded?.Invoke();
        }

        public void ResetForNewBattle()
        {
            IsBattleActive = false;
            IsPhaseTwo = false;
            Health.ResetHealth(maxHealth);
            IsBattleActive = true;
            BattleReset?.Invoke();
            StatsChanged?.Invoke();
        }

        private void OnHealthChanged()
        {
            if (!IsBattleActive || IsPhaseTwo || Health.IsDead || Health.HealthRatio > phaseTwoHealthRatio)
                return;

            IsPhaseTwo = true;
            PhaseTwoEntered?.Invoke();
            StatsChanged?.Invoke();
        }
    }
}
