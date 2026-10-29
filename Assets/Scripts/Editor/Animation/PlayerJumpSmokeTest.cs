using System.IO;
using UnityEditor;
using UnityEngine;
using ThroneSiege.Player;

namespace ThroneSiege.Editor
{
    [InitializeOnLoad]
    public static class PlayerJumpSmokeTest
    {
        private const string Key = "ThroneSiege.PlayerJumpTest";
        private static InPlaceRootMotionFallback motor;
        private static Animator animator;
        private static double deadline;
        private static int jumps;
        private static bool sawJump;
        private static double nextJump;

        static PlayerJumpSmokeTest() { EditorApplication.playModeStateChanged += OnPlayMode; }

        [MenuItem("ThroneSiege/플레이어 점프 테스트")]
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
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            motor = player != null ? player.GetComponent<InPlaceRootMotionFallback>() : null;
            if (motor == null) { Finish(false, "Missing player motor."); return; }
            animator = player.GetComponent<Animator>();
            foreach (ThroneSiege.AI.CrusaderTankAI enemy in Object.FindObjectsOfType<ThroneSiege.AI.CrusaderTankAI>())
                enemy.gameObject.SetActive(false);
            jumps = 0; sawJump = false;
            nextJump = EditorApplication.timeSinceStartup + 1;
            deadline = EditorApplication.timeSinceStartup + 8;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying || motor == null) return;
            if (EditorApplication.timeSinceStartup > deadline)
            { Finish(false, "Jump/landing timeout; OnGround=" + animator.GetBool("OnGround") + ", velocity=" + motor.GetComponent<Rigidbody>().velocity); return; }
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            if (state.IsName("Jump")) sawJump = true;
            if (sawJump && state.IsName("Grounded") && !animator.IsInTransition(0) && animator.GetBool("OnGround"))
            {
                jumps++;
                if (Mathf.Abs(animator.GetFloat("Jump")) > 0.001f) { Finish(false, "Jump float not reset on landing."); return; }
                if (jumps == 3) { Finish(true, "Three repeated jumps entered Jump and returned to Idle; OnGround true and Jump float reset after each landing."); return; }
                sawJump = false;
                nextJump = EditorApplication.timeSinceStartup + 0.5;
            }
            if (!sawJump && nextJump > 0 && EditorApplication.timeSinceStartup >= nextJump && state.IsName("Grounded"))
            { motor.RequestJump(); nextJump = -1; deadline = EditorApplication.timeSinceStartup + 8; }
        }

        private static void Finish(bool success, string details)
        {
            EditorApplication.update -= Tick;
            Directory.CreateDirectory("Temp");
            string result = (success ? "PASS: " : "FAIL: ") + details;
            File.WriteAllText("Temp/PlayerJumpSmokeTest.txt", result);
            if (success) Debug.Log(result); else Debug.LogError(result);
            EditorApplication.isPlaying = false;
        }
    }
}
