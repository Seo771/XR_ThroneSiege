using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityStandardAssets.Characters.ThirdPerson;
using ThroneSiege.AI;
using ThroneSiege.Combat;
using ThroneSiege.Player;

namespace ThroneSiege.Editor
{
    [InitializeOnLoad]
    public static class PlayerBasicAttackSmokeTest
    {
        private const string Key = "ThroneSiege.BasicAttackTest";
        private static readonly Stack<IEnumerator> steps = new Stack<IEnumerator>();
        private static readonly List<string> results = new List<string>();
        private static PlayerCombat combat;
        private static PlayerStats player;
        private static InPlaceRootMotionFallback motor;
        private static Animator animator;
        private static CrusaderTankAI enemy;
        private static ActorHealth enemyHealth;
        private static bool accelerated;

        [MenuItem("ThroneSiege/Test Fast Player Attack Timing")]
        public static void RunFast()
        {
            if (EditorApplication.isPlaying) return;
            SessionState.SetBool(Key + ".Fast", true);
            Run();
        }

        static PlayerBasicAttackSmokeTest() { EditorApplication.playModeStateChanged += OnPlayMode; }

        [MenuItem("ThroneSiege/Test Player Basic Attack")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) return;
            SessionState.SetBool(Key, true);
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayMode(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode) EditorApplication.update -= Tick;
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
            SessionState.SetBool(Key, false);
            accelerated = SessionState.GetBool(Key + ".Fast", false);
            SessionState.SetBool(Key + ".Fast", false);
            results.Clear(); steps.Clear(); steps.Push(Sequence());
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
            try
            {
                if (steps.Count == 0) { Finish(true, "All checks passed."); return; }
                IEnumerator current = steps.Peek();
                if (!current.MoveNext()) steps.Pop();
                else if (current.Current is IEnumerator nested) steps.Push(nested);
            }
            catch (Exception exception) { Finish(false, exception.Message); }
        }

        private static IEnumerator Wait(Func<bool> condition, float seconds = 8f)
        {
            double deadline = EditorApplication.timeSinceStartup + seconds;
            while (!condition())
            {
                if (EditorApplication.timeSinceStartup > deadline)
                    throw new InvalidOperationException("Timed out: state=" + (animator != null ? animator.GetCurrentAnimatorStateInfo(0).fullPathHash.ToString() : "none"));
                yield return null;
            }
        }

        private static IEnumerator Delay(float seconds)
        { double until = EditorApplication.timeSinceStartup + seconds; while (EditorApplication.timeSinceStartup < until) yield return null; }

        private static bool Idle() => animator.GetCurrentAnimatorStateInfo(0).IsName("Idle") && !animator.IsInTransition(0);
        private static bool StartAttack()
        {
            bool started = combat.TryAttack();
            if (started && accelerated) animator.speed = 2f;
            return started;
        }
        private static void Check(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); results.Add("PASS: " + message); }

        private static IEnumerator Ready()
        { yield return Wait(() => Idle() && animator.GetBool("OnGround") && !combat.IsAttacking && combat.CooldownRemaining <= 0); }

        private static void PlaceEnemy(bool inFront)
        {
            enemy.transform.position = player.transform.position + (inFront ? Vector3.forward : Vector3.back) * 1.7f;
            enemy.transform.rotation = Quaternion.LookRotation(player.transform.position - enemy.transform.position);
            Physics.SyncTransforms();
        }

        private static IEnumerator Sequence()
        {
            yield return Delay(1f);
            player = UnityEngine.Object.FindObjectsOfType<PlayerStats>().First();
            combat = player.GetComponent<PlayerCombat>();
            motor = player.GetComponent<InPlaceRootMotionFallback>();
            animator = player.GetComponent<Animator>();
            if (accelerated)
            {
                AnimationClip source = animator.runtimeAnimatorController.animationClips.First(c => c.name == "Attack1");
                var copy = UnityEngine.Object.Instantiate(source);
                AnimationUtility.SetAnimationEvents(copy, AnimationUtility.GetAnimationEvents(copy)
                    .Where(e => e.functionName != "BasicAttackFinished").ToArray());
                var overrides = new AnimatorOverrideController(animator.runtimeAnimatorController);
                overrides[source.name] = copy;
                animator.runtimeAnimatorController = overrides;
                results.Add("Timing stress check: 2x attack playback, with completion event removed.");
            }
            enemy = UnityEngine.Object.FindObjectsOfType<CrusaderTankAI>().First();
            enemyHealth = enemy.GetComponent<ActorHealth>();
            enemy.SetBattleActive(false); enemy.enabled = false;
            enemy.GetComponent<NavMeshAgent>().enabled = false;
            // A temporary isolated floor keeps map walls and enemy navigation out of
            // the combat assertions. Exiting Play Mode discards every test object.
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "BasicAttackTestFloor";
            floor.transform.position = new Vector3(10000f, 19.5f, 10000f);
            floor.transform.localScale = new Vector3(40f, 1f, 40f);
            Rigidbody body = player.GetComponent<Rigidbody>();
            body.position = new Vector3(10000f, 20.05f, 10000f);
            body.rotation = Quaternion.identity; body.velocity = Vector3.zero;
            player.transform.SetPositionAndRotation(body.position, Quaternion.identity);
            Physics.SyncTransforms();
            yield return Ready();
            PlaceEnemy(true);
            float before = enemyHealth.CurrentHealth;
            Check(StartAttack(), "Attack1 can start from grounded Idle");
            Check(!combat.TryAttack() && motor.MovementLocked && !player.GetComponent<ThirdPersonUserControl>().enabled,
                "Duplicate attacks and movement input blocked");
            float y = body.velocity.y;
            motor.RequestJump();
            yield return Delay(0.12f);
            Check(body.velocity.y <= Mathf.Max(0.5f, y + 0.5f), "Jump request ignored during attack");
            Check(enemyHealth.CurrentHealth == before, "No damage at attack input time");
            yield return Wait(() => enemyHealth.CurrentHealth < before);
            Check(Mathf.Approximately(before - enemyHealth.CurrentHealth, player.AttackDamage), "Animation impact applies configured damage");
            combat.BasicAttackImpact(); combat.BasicAttackImpact();
            Check(Mathf.Approximately(before - enemyHealth.CurrentHealth, player.AttackDamage), "Repeated impact callback cannot double-hit");
            yield return Ready();
            Check(!motor.MovementLocked && player.GetComponent<ThirdPersonUserControl>().enabled, "Attack end restores movement input");
            animator.SetFloat("MoveSpeed", 1f);
            animator.Update(0.2f);
            Check(animator.GetCurrentAnimatorClipInfo(0).Any(info => info.clip.name == "Walk" && info.weight > 0.9f),
                "Movement parameter selects Walk after attack");
            animator.SetFloat("MoveSpeed", 0f);
            animator.Update(0.1f);

            PlaceEnemy(false); before = enemyHealth.CurrentHealth;
            Check(StartAttack(), "Second attack starts after cooldown");
            yield return Ready();
            Check(Mathf.Approximately(before, enemyHealth.CurrentHealth), "Enemy behind player is missed");

            PlaceEnemy(true); before = enemyHealth.CurrentHealth;
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = player.transform.position + new Vector3(0f, 1f, 0.85f);
            wall.transform.localScale = new Vector3(3f, 3f, 0.2f);
            Physics.SyncTransforms();
            Check(StartAttack(), "Attack can play facing wall");
            yield return Ready();
            Check(Mathf.Approximately(before, enemyHealth.CurrentHealth), "Wall blocks melee damage");
            wall.SetActive(false);

            PlaceEnemy(true); before = enemyHealth.CurrentHealth;
            Check(StartAttack(), "Attack starts for uninterrupted damage check");
            yield return Wait(() => animator.GetCurrentAnimatorStateInfo(0).IsName("Attack1"));
            float playerHealthBefore = player.Health.CurrentHealth;
            player.Health.TakeDamage(1f);
            Check(Mathf.Approximately(playerHealthBefore - player.Health.CurrentHealth, 1f),
                "Damage still reduces player health during attack");
            animator.Update(0.05f);
            Check(combat.IsAttacking && motor.MovementLocked && !player.GetComponent<ThirdPersonUserControl>().enabled &&
                animator.GetCurrentAnimatorStateInfo(0).IsName("Attack1") &&
                (!animator.IsInTransition(0) || !animator.GetNextAnimatorStateInfo(0).IsName("Hit")),
                "Damage during attack suppresses Hit and preserves attack movement lock");
            yield return Ready();
            Check(Mathf.Approximately(before - enemyHealth.CurrentHealth, player.AttackDamage),
                "Attack continues and deals damage once after taking damage");
            Check(!motor.MovementLocked && player.GetComponent<ThirdPersonUserControl>().enabled,
                "Attack completion restores input after taking damage");

            player.Health.TakeDamage(1f);
            yield return Wait(() => animator.GetCurrentAnimatorStateInfo(0).IsName("Hit"));
            Check(true, "Damage outside attack still plays Hit");
            yield return Ready();

            motor.RequestJump();
            yield return Wait(() => animator.GetCurrentAnimatorStateInfo(0).IsName("Jump"));
            yield return Ready();
            Check(true, "Jump and landing work after taking damage");


            player.EndBattle();
            Check(!combat.TryAttack(), "Ended battle rejects attack input");
            player.ResetForNewBattle();
            yield return Ready();
            // Activate damage reactions without allowing the enemy's Update to attack.
            enemy.SetBattleActive(true);
            PlaceEnemy(true);
            Check(StartAttack(), "Attack starts for enemy reaction check");
            yield return Wait(() => enemy.State == CrusaderTankAI.BehaviourState.Hit);
            Check(true, "Enemy damage invokes hit reaction");
            yield return Ready();
            enemyHealth.ResetHealth(1f);
            PlaceEnemy(true);
            Check(StartAttack(), "Lethal attack starts");
            yield return Wait(() => enemyHealth.IsDead);
            Check(enemy.State == CrusaderTankAI.BehaviourState.Death && enemy.GetComponent<Animator>().GetBool("Dead") &&
                enemy.GetComponentsInChildren<Collider>().All(c => !c.enabled), "Lethal damage invokes enemy death and collider shutdown");
            yield return Wait(() => !combat.IsAttacking && !motor.MovementLocked);
            // Isolate the final player death assertion from the completed battle.
            UnityEngine.Object.DestroyImmediate(UnityEngine.Object.FindObjectOfType<BattleManager>());
            player.ResetForNewBattle();
            yield return Ready();
            Check(StartAttack(), "Attack starts for player death check");
            yield return Wait(() => animator.GetCurrentAnimatorStateInfo(0).IsName("Attack1"));
            player.Health.TakeDamage(player.Health.CurrentHealth);
            Check(player.Health.IsDead && !combat.IsAttacking && animator.GetBool("Dead"),
                "Lethal damage during attack still cancels attack and triggers death");
        }

        private static void Finish(bool success, string message)
        {
            EditorApplication.update -= Tick;
            results.Add((success ? "PASS: " : "FAIL: ") + message);
            Directory.CreateDirectory("Temp");
            File.WriteAllLines("Temp/PlayerBasicAttackSmokeTest.txt", results);
            if (success) Debug.Log(message); else Debug.LogError(message);
            EditorApplication.isPlaying = false;
        }
    }
}
