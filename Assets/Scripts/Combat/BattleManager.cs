using System;
using System.Collections.Generic;
using ThroneSiege.AI;
using ThroneSiege.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ThroneSiege.Combat
{
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class BattleManager : MonoBehaviour
    {
        public enum BattleState { Waiting, Fighting, Victory, Defeat, Restarting }

        [Header("전투 참가자")]
        [Tooltip("미지정 시 같은 씬에서 검색")]
        [SerializeField] private PlayerStats player;
        [Tooltip("비어 있으면 같은 씬의 활성 적을 검색")]
        [SerializeField] private CrusaderTankAI[] enemies = Array.Empty<CrusaderTankAI>();

        [Header("진행")]
        [SerializeField] private bool startAutomatically = true;

        public BattleState State { get; private set; } = BattleState.Waiting;
        public PlayerStats Player => player;
        public IReadOnlyList<CrusaderTankAI> Enemies => participants;
        public int TotalEnemies => enemyHealths.Count;
        public int RemainingEnemies { get; private set; }
        public bool CanStartBattle => ready && State == BattleState.Waiting;
        public bool IsPhaseTransitionPaused { get; private set; }
        public event Action<BattleState> StateChanged;
        public event Action<int> RemainingEnemiesChanged;

        private readonly List<CrusaderTankAI> participants = new List<CrusaderTankAI>();
        private readonly List<ActorHealth> enemyHealths = new List<ActorHealth>();
        private bool ready;

        private void Awake()
        {
            if (player == null)
                foreach (PlayerStats candidate in FindObjectsOfType<PlayerStats>())
                    if (candidate.gameObject.scene == gameObject.scene) { player = candidate; break; }

            if (player != null && player.Health != null)
            {
                player.EndBattle();
                player.Health.Died += OnPlayerDied;
            }

            var candidates = enemies != null && enemies.Length > 0 ? enemies : FindObjectsOfType<CrusaderTankAI>();
            foreach (var enemy in candidates)
            {
                if (enemy == null || enemy.gameObject.scene != gameObject.scene || participants.Contains(enemy)) continue;
                var health = enemy.GetComponent<ActorHealth>();
                if (health == null || health.IsDead || enemyHealths.Contains(health)) continue;
                participants.Add(enemy);
                enemyHealths.Add(health);
                enemy.Configure(player != null ? player.transform : null);
                enemy.SetBattleActive(false);
                health.Died += OnEnemyDied;
                // Spread avoidance priorities without changing Standard Assets.
                var agent = enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null) agent.avoidancePriority = 30 + (participants.Count - 1) % 40;
            }

            RemainingEnemies = CountLivingEnemies();
            ready = player != null && player.Health != null && !player.Health.IsDead && RemainingEnemies > 0;
            if (!ready) Debug.LogError("BattleManager에 살아 있는 플레이어와 적을 연결해야 합니다.", this);
        }

        private void Start()
        {
            RemainingEnemiesChanged?.Invoke(RemainingEnemies);
            StateChanged?.Invoke(State);
            if (startAutomatically) StartBattle();
        }

        public void StartBattle()
        {
            if (!CanStartBattle) return;
            State = BattleState.Fighting;
            player.ResetForNewBattle();
            if (State != BattleState.Fighting) return;
            foreach (var enemy in participants)
                if (enemy != null) enemy.SetBattleActive(true);
            StateChanged?.Invoke(State);
            EvaluateResult();
        }

        private void OnPlayerDied() { EvaluateResult(); }

        private void OnEnemyDied()
        {
            int remaining = CountLivingEnemies();
            bool changed = RemainingEnemies != remaining;
            RemainingEnemies = remaining;
            EvaluateResult();
            if (changed) RemainingEnemiesChanged?.Invoke(RemainingEnemies);
        }

        private int CountLivingEnemies()
        {
            int count = 0;
            foreach (var health in enemyHealths)
                if (health != null && !health.IsDead) count++;
            return count;
        }

        private void EvaluateResult()
        {
            if (State != BattleState.Fighting) return;
            if (player.Health.IsDead) FinishBattle(BattleState.Defeat);
            else if (RemainingEnemies == 0) FinishBattle(BattleState.Victory);
        }

        private void FinishBattle(BattleState result)
        {
            State = result;
            StopCombat();
            StateChanged?.Invoke(State);
        }

        private void StopCombat()
        {
            SetPhaseTransitionPaused(false);
            if (player != null) player.EndBattle();
            foreach (var enemy in participants)
                if (enemy != null) enemy.SetBattleActive(false);
        }

        public void SetPhaseTransitionPaused(bool paused)
        {
            if (paused && State != BattleState.Fighting) return;
            if (IsPhaseTransitionPaused == paused) return;
            IsPhaseTransitionPaused = paused;
            foreach (var enemy in participants)
                if (enemy != null) enemy.SetCinematicPaused(paused);
        }

        public void RestartBattle()
        {
            if (State == BattleState.Restarting) return;
            string scenePath = gameObject.scene.path;
            if (string.IsNullOrEmpty(scenePath) || !Application.CanStreamedLevelBeLoaded(scenePath))
            {
                Debug.LogError("재시작하려면 현재 씬을 Build Settings에 등록해야 합니다.", this);
                return;
            }

            try
            {
                if (SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Single) == null) return;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                return;
            }

            State = BattleState.Restarting;
            StopCombat();
            StateChanged?.Invoke(State);
        }

        private void OnDestroy()
        {
            if (player != null && player.Health != null) player.Health.Died -= OnPlayerDied;
            foreach (var health in enemyHealths)
                if (health != null) health.Died -= OnEnemyDied;
        }
    }
}
