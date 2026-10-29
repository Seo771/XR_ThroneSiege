using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using ThroneSiege.AI;
using ThroneSiege.Combat;

namespace ThroneSiege.Editor
{
    // 실행 중인 테스트 씬만 검사하며, 플레이 모드 종료 시 씬 오브젝트는 원래 상태로 돌아간다.
    [InitializeOnLoad]
    public static class CrusaderAISmokeTest
    {
        private const string Key = "ThroneSiege.CrusaderSmoke";
        private static CrusaderTankAI ai;
        private static ActorHealth player;
        private static double deadline;
        private static int phase;
        private static bool sawChase;
        private static float startingHealth;
        private static float hitStartedAt;
        private static float maximumHitTime;
        private static bool accelerated;

        [MenuItem("ThroneSiege/Test Fast Enemy Animation Timing")]
        public static void RunFast()
        {
            if (EditorApplication.isPlaying) return;
            SessionState.SetBool(Key + ".Fast", true);
            Run();
        }

        static CrusaderAISmokeTest() { EditorApplication.playModeStateChanged += OnPlayMode; }

        [MenuItem("ThroneSiege")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before running the test.");
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
            ai = UnityEngine.Object.FindObjectOfType<CrusaderTankAI>();
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (ai == null || playerObject == null) { Finish(false, "Missing AI or player."); return; }
            foreach (var other in UnityEngine.Object.FindObjectsOfType<CrusaderTankAI>())
                if (other != ai) { other.SetBattleActive(false); other.enabled = false; }
            player = playerObject.GetComponent<ActorHealth>();
            if (player == null) { Finish(false, "Missing player health."); return; }
            foreach (MonoBehaviour component in playerObject.GetComponents<MonoBehaviour>())
                if (component is UnityStandardAssets.Characters.ThirdPerson.ThirdPersonCharacter ||
                    component is UnityStandardAssets.Characters.ThirdPerson.ThirdPersonUserControl ||
                    component is ThroneSiege.Player.InPlaceRootMotionFallback) component.enabled = false;
            Rigidbody body = playerObject.GetComponent<Rigidbody>();
            body.velocity = Vector3.zero; body.isKinematic = true;
            playerObject.GetComponent<Animator>().applyRootMotion = false;
            if (accelerated)
            {
                Animator enemyAnimator = ai.GetComponent<Animator>();
                enemyAnimator.speed = 2f;
                maximumHitTime = enemyAnimator.runtimeAnimatorController.animationClips.First(c => c.name == "Hit").length * 0.5f + 0.4f;
                if (NavMesh.SamplePosition(playerObject.transform.position, out NavMeshHit playerPoint, 10f, NavMesh.AllAreas))
                {
                    body.position = playerPoint.position + Vector3.up * 0.05f;
                    playerObject.transform.position = body.position;
                }
                if (NavMesh.SamplePosition(playerObject.transform.position + Vector3.forward * 4f, out NavMeshHit enemyPoint, 8f, NavMesh.AllAreas))
                    ai.GetComponent<NavMeshAgent>().Warp(enemyPoint.position);
            }
            startingHealth = player.CurrentHealth;
            phase = 0; sawChase = false;
            deadline = EditorApplication.timeSinceStartup + 25;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying || ai == null) return;
            if (EditorApplication.timeSinceStartup > deadline)
            { Finish(false, "Timed out, phase=" + phase + ", AI=" + ai.State + ", HP=" + player.CurrentHealth + ", onNavMesh=" + ai.GetComponent<NavMeshAgent>().isOnNavMesh); return; }
            if (ai.State == CrusaderTankAI.BehaviourState.Chase) sawChase = true;
            if (phase == 0 && player.CurrentHealth < startingHealth)
            {
                if (!sawChase) { Finish(false, "No chase observed before attack."); return; }
                if (startingHealth - player.CurrentHealth > 12.01f) { Finish(false, "Repeated damage in first attack."); return; }
                phase = 1;
            }
            if (phase == 1 && ai.State == CrusaderTankAI.BehaviourState.CombatIdle)
            {
                ai.GetComponent<ActorHealth>().TakeDamage(5f);
                hitStartedAt = Time.time;
                if (ai.State != CrusaderTankAI.BehaviourState.Hit) { Finish(false, "Hit reaction failed."); return; }
                phase = 2;
            }
            if (phase == 2 && ai.State != CrusaderTankAI.BehaviourState.Hit)
            {
                if (accelerated && Time.time - hitStartedAt > maximumHitTime)
                { Finish(false, "Hit recovery still waits for the old clip duration."); return; }
                ai.GetComponent<ActorHealth>().TakeDamage(1000f);
                bool valid = ai.State == CrusaderTankAI.BehaviourState.Death &&
                    !ai.GetComponent<NavMeshAgent>().enabled &&
                    ai.GetComponentsInChildren<Collider>().All(collider => !collider.enabled) &&
                    ai.GetComponent<Animator>().GetBool("Dead");
                Finish(valid, "Chase, attack damage, cooldown, hit recovery, death, agent/collider shutdown checked.");
            }
        }

        private static void Finish(bool success, string details)
        {
            EditorApplication.update -= Tick;
            string result = (success ? "PASS: " : "FAIL: ") + details;
            Directory.CreateDirectory("Temp");
            File.WriteAllText("Temp/CrusaderAISmokeTest.txt", result);
            if (success) Debug.Log(result); else Debug.LogError(result);
            EditorApplication.isPlaying = false;
        }
    }
}
