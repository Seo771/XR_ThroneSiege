using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using ThroneSiege.Player;
using ThroneSiege.AI;
using ThroneSiege.Combat;

namespace ThroneSiege.Editor
{
    public static class PlayerBasicAttackSetup
    {
        private const string PrefabPath = "Assets/Prefab/Player/Player_Necromanser.prefab";
        private const string ClipPath = "Assets/Animations/NecromancerPlayerClips/Attack1.anim";
        private const string ControllerPath = "Assets/Animations/NecromancerPlayerClips/NecromancerPlayer.controller";

        [MenuItem("ThroneSiege/Apply Player Basic Attack")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/MainScene.unity") throw new InvalidOperationException("Open MainScene first.");
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (clip == null || controller == null) throw new InvalidOperationException("Player Attack1 assets are missing.");

            float impactTime = FindSwingTime(clip);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AnimationUtility.SetAnimationEvents(clip, new[] {
                new AnimationEvent { functionName = "BasicAttackImpact", time = impactTime },
                new AnimationEvent { functionName = "BasicAttackFinished", time = clip.length - 0.01f }
            });
            EditorUtility.SetDirty(clip);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            AnimatorState idle = sm.states.First(state => state.state.name == "Grounded").state;
            AnimatorState attack = sm.states.First(state => state.state.name == "Attack1").state;
            attack.motion = clip;
            // Only Idle can initiate a basic attack. Death cannot be interrupted.
            foreach (AnimatorStateTransition transition in sm.anyStateTransitions.ToArray())
                if (transition.destinationState == attack) sm.RemoveAnyStateTransition(transition);
            foreach (AnimatorStateTransition transition in idle.transitions.ToArray())
                if (transition.destinationState == attack) idle.RemoveTransition(transition);
            var begin = idle.AddTransition(attack);
            begin.hasExitTime = false; begin.hasFixedDuration = true; begin.duration = 0.08f;
            begin.AddCondition(AnimatorConditionMode.If, 0, "Attack1");
            begin.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
            foreach (AnimatorStateTransition transition in attack.transitions)
            {
                transition.hasExitTime = true; transition.exitTime = 1f;
                transition.hasFixedDuration = true; transition.duration = 0.08f;
                transition.interruptionSource = TransitionInterruptionSource.None;
            }
            foreach (AnimatorStateTransition transition in sm.anyStateTransitions)
                if (transition.destinationState != null && transition.destinationState.name == "Hit" &&
                    !transition.conditions.Any(condition => condition.parameter == "Dead"))
                    transition.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
            EditorUtility.SetDirty(controller);

            GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try { Configure(contents); PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath); }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            foreach (PlayerStats player in UnityEngine.Object.FindObjectsOfType<PlayerStats>())
                if (player.gameObject.scene == scene) Configure(player.gameObject);
            foreach (BattleManager battle in UnityEngine.Object.FindObjectsOfType<BattleManager>())
            {
                if (battle.gameObject.scene != scene) continue;
                var serialized = new SerializedObject(battle);
                if (serialized.FindProperty("player").objectReferenceValue == null)
                    serialized.FindProperty("player").objectReferenceValue = UnityEngine.Object.FindObjectsOfType<PlayerStats>()
                        .FirstOrDefault(actor => actor.gameObject.scene == scene);
                if (serialized.FindProperty("enemy").objectReferenceValue == null)
                    serialized.FindProperty("enemy").objectReferenceValue = UnityEngine.Object.FindObjectsOfType<CrusaderTankAI>()
                        .FirstOrDefault(actor => actor.gameObject.scene == scene);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Temp");
            File.WriteAllText("Temp/PlayerBasicAttackSetup.txt", "Attack1 length=" + clip.length +
                ", impact=" + impactTime + ", prefab and MainScene configured.");
            Debug.Log("Basic attack configured. Impact at " + impactTime.ToString("F3") + "s / " + clip.length.ToString("F3") + "s.");
        }

        private static void Configure(GameObject player)
        {
            if (player.GetComponent<PlayerAttackHitbox>() == null) player.AddComponent<PlayerAttackHitbox>();
            PlayerCombat combat = player.GetComponent<PlayerCombat>();
            if (combat == null) combat = player.AddComponent<PlayerCombat>();
            EditorUtility.SetDirty(combat);
        }

        internal static float FindSwingTime(AnimationClip clip, float duration = -1f)
        {
            if (duration <= 0f) duration = clip.length;
            GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Transform hand = contents.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(bone => bone.name == "Necromancer_RightHand");
                if (hand == null) throw new InvalidOperationException("Weapon hand bone not found.");
                float bestSpeed = -1f, bestTime = duration * 0.5f;
                Vector3 previous = Vector3.zero;
                const float step = 1f / 120f;
                for (float time = 0f; time <= duration * 0.8f; time += step)
                {
                    clip.SampleAnimation(contents, time);
                    Vector3 position = contents.transform.InverseTransformPoint(hand.position);
                    float speed = (position - previous).sqrMagnitude;
                    if (time > duration * 0.15f && position.z > -0.25f && speed > bestSpeed)
                    { bestSpeed = speed; bestTime = time; }
                    previous = position;
                }
                return Mathf.Clamp(bestTime, 0.1f, duration - 0.15f);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }
    }
}
