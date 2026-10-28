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
        [SerializeField] private CrusaderTankAI[] enemies;

        [Header("진행")]
        [SerializeField] private bool startAutomatically = true;

        public BattleState State { get; private set; } = BattleState.Waiting;
        public PlayerStats Player => player;
        public int TotalEnemies => enemyHealth.Count;
        public int RemainingEnemies { get; private set; }
        public bool CanStartBattle => ready && State == BattleState.Waiting;
        public event Action<BattleState> StateChanged;
        public event Action<int> RemainingEnemiesChanged;

        private readonly HashSet<ActorHealth> enemyHealth = new HashSet<ActorHealth>();
        private bool ready;

        private void Awake()
        {
            if (player == null)
                foreach (PlayerStats candidate in FindObjectsOfType<PlayerStats>())
                    if (candidate.gameObject.scene == gameObject.scene) { player = candidate; break; }

            if (enemies == null || enemies.Length == 0)
            {
                var sceneEnemies = new List<CrusaderTankAI>();
                foreach (CrusaderTankAI enemy in FindObjectsOfType<CrusaderTankAI>())
                    if (enemy.gameObject.scene == gameObject.scene) sceneEnemies.Add(enemy);
                enemies = sceneEnemies.ToArray();
            }

            if (player != null && player.Health != null)
            {
                player.EndBattle();
                player.Health.Died += OnPlayerDied;
            }

            foreach (CrusaderTankAI enemy in enemies)
            {
                if (enemy == null) continue;
                enemy.SetBattleActive(false);
                ActorHealth health = enemy.GetComponent<ActorHealth>();
                if (health != null && enemyHealth.Add(health)) health.Died += OnEnemyDied;
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
            foreach (CrusaderTankAI enemy in enemies)
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
            foreach (ActorHealth health in enemyHealth)
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
            if (player != null) player.EndBattle();
            foreach (CrusaderTankAI enemy in enemies)
                if (enemy != null) enemy.SetBattleActive(false);
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
            foreach (ActorHealth health in enemyHealth)
                if (health != null) health.Died -= OnEnemyDied;
        }
    }
}
