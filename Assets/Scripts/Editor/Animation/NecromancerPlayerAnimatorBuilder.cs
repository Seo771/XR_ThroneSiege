using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ThroneSiege.Player;

namespace ThroneSiege.Editor
{
    public static class NecromancerPlayerAnimatorBuilder
    {
        private const string OutputFolder = "Assets/ThroneSiege/Animations";
        private const string ClipFolder = OutputFolder + "/NecromancerPlayerClips";
        private const string ControllerPath = OutputFolder + "/NecromancerPlayer.controller";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string SourceFolder = "Assets/AssetStore/Necromanser/Animations";

        [MenuItem("ThroneSiege/네크로맨서 플레이어 Animator 구성")]
        public static void BuildFromMenu()
        {
            Build();
        }

        // Unity 명령줄의 -executeMethod 옵션에서 호출하는 진입점이다.
        public static void BuildFromCommandLine()
        {
            try
            {
                Build();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }

            EditorApplication.Exit(0);
        }

        private static void Build()
        {
            EnsureFolder("Assets/ThroneSiege");
            EnsureFolder(OutputFolder);
            EnsureFolder(ClipFolder);

            AssetDatabase.DeleteAsset(ControllerPath);

            AnimationClip idle = CopyClip("Necromancer@idle1.fbx", "Idle", true);
            AnimationClip walk = CopyClip("Necromancer@walk.fbx", "Walk", true);
            AnimationClip jump = CopyClip("Necromancer@jump.fbx", "Jump", false);
            AnimationClip attack1 = CopyClip("Necromancer@atack1.fbx", "Attack1", false);
            AnimationClip attack2 = CopyClip("Necromancer@atack2.fbx", "Attack2", false);
            AnimationClip hit = CopyClip("Necromancer@getgit.fbx", "Hit", false);
            AnimationClip phaseTwo = CopyClip("Necromancer@roar.fbx", "PhaseTwo", false);
            AnimationClip death = CopyClip("Necromancer@death.fbx", "Death", false);

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            AddParameters(controller);

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            stateMachine.name = "Player Locomotion and Combat";

            AnimatorState grounded = CreateGroundedState(controller, stateMachine, idle, walk);
            AnimatorState jumpState = CreateState(stateMachine, "Jump", jump, new Vector3(300, -20));
            AnimatorState attack1State = CreateState(stateMachine, "Attack1", attack1, new Vector3(300, 90));
            AnimatorState attack2State = CreateState(stateMachine, "Attack2", attack2, new Vector3(300, 170));
            AnimatorState hitState = CreateState(stateMachine, "Hit", hit, new Vector3(300, 250));
            AnimatorState phaseTwoState = CreateState(stateMachine, "PhaseTwo", phaseTwo, new Vector3(300, 330));
            AnimatorState deathState = CreateState(stateMachine, "Death", death, new Vector3(300, 420));

            stateMachine.defaultState = grounded;

            AnimatorStateTransition jumpTransition = grounded.AddTransition(jumpState);
            ConfigureConditionalTransition(jumpTransition, AnimatorConditionMode.If, "JumpRequested");

            AnimatorStateTransition landTransition = jumpState.AddTransition(grounded);
            ConfigureConditionalTransition(landTransition, AnimatorConditionMode.If, "OnGround");

            AddAnyStateTrigger(stateMachine, attack1State, "Attack1", 0.08f);
            AddAnyStateTrigger(stateMachine, attack2State, "Attack2", 0.08f);
            AddAnyStateTrigger(stateMachine, hitState, "Hit", 0.05f);
            AddAnyStateTrigger(stateMachine, phaseTwoState, "PhaseTwo", 0.08f);

            AnimatorStateTransition deathTransition = stateMachine.AddAnyStateTransition(deathState);
            ConfigureConditionalTransition(deathTransition, AnimatorConditionMode.If, "Dead", 0.05f);

            AddExitTransition(attack1State, grounded, 0.9f, 0.1f);
            AddExitTransition(attack2State, grounded, 0.9f, 0.1f);
            AddExitTransition(hitState, grounded, 0.9f, 0.08f);
            AddExitTransition(phaseTwoState, grounded, 0.95f, 0.12f);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            AssignControllerToSampleScene(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[ThroneSiege] Necromancer player Animator built and assigned: " + ControllerPath);
        }

        private static void AddParameters(AnimatorController controller)
        {
            controller.AddParameter("Forward", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveSpeed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Turn", AnimatorControllerParameterType.Float);
            controller.AddParameter("Crouch", AnimatorControllerParameterType.Bool);
            controller.AddParameter("OnGround", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Jump", AnimatorControllerParameterType.Float);
            controller.AddParameter("JumpRequested", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("JumpLeg", AnimatorControllerParameterType.Float);
            controller.AddParameter("Attack1", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Attack2", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("PhaseTwo", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);
        }

        private static AnimatorState CreateGroundedState(
            AnimatorController controller,
            AnimatorStateMachine stateMachine,
            AnimationClip idle,
            AnimationClip walk)
        {
            AnimatorState grounded = stateMachine.AddState("Grounded", new Vector3(30, 80));
            BlendTree blendTree = new BlendTree
            {
                name = "Idle Walk",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "MoveSpeed",
                useAutomaticThresholds = false
            };

            AssetDatabase.AddObjectToAsset(blendTree, controller);
            blendTree.AddChild(idle, 0f);
            blendTree.AddChild(walk, 1f);
            grounded.motion = blendTree;
            return grounded;
        }

        private static AnimatorState CreateState(
            AnimatorStateMachine stateMachine,
            string name,
            AnimationClip clip,
            Vector3 position)
        {
            AnimatorState state = stateMachine.AddState(name, position);
            state.motion = clip;
            return state;
        }

        private static void AddAnyStateTrigger(
            AnimatorStateMachine stateMachine,
            AnimatorState destination,
            string trigger,
            float duration)
        {
            AnimatorStateTransition transition = stateMachine.AddAnyStateTransition(destination);
            ConfigureConditionalTransition(transition, AnimatorConditionMode.If, trigger, duration);
        }

        private static void ConfigureConditionalTransition(
            AnimatorStateTransition transition,
            AnimatorConditionMode mode,
            string parameter,
            float duration = 0.1f)
        {
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = duration;
            transition.canTransitionToSelf = false;
            transition.AddCondition(mode, 0f, parameter);
        }

        private static void AddExitTransition(
            AnimatorState source,
            AnimatorState destination,
            float exitTime,
            float duration)
        {
            AnimatorStateTransition transition = source.AddTransition(destination);
            transition.hasExitTime = true;
            transition.exitTime = exitTime;
            transition.hasFixedDuration = true;
            transition.duration = duration;
            transition.canTransitionToSelf = false;
        }

        private static AnimationClip CopyClip(string sourceFile, string outputName, bool loop)
        {
            string sourcePath = SourceFolder + "/" + sourceFile;
            AnimationClip source = AssetDatabase.LoadAllAssetsAtPath(sourcePath)
                .OfType<AnimationClip>()
                .FirstOrDefault(clip => !clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase));

            if (source == null)
            {
                throw new InvalidOperationException("Animation clip not found in " + sourcePath);
            }

            string destinationPath = ClipFolder + "/" + outputName + ".anim";
            AssetDatabase.DeleteAsset(destinationPath);

            AnimationClip copy = new AnimationClip();
            EditorUtility.CopySerialized(source, copy);
            copy.name = outputName;
            AssetDatabase.CreateAsset(copy, destinationPath);

            SerializedObject serializedClip = new SerializedObject(copy);
            SerializedProperty loopTime = serializedClip.FindProperty("m_AnimationClipSettings.m_LoopTime");
            if (loopTime != null)
            {
                loopTime.boolValue = loop;
                serializedClip.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorUtility.SetDirty(copy);
            return copy;
        }

        private static void AssignControllerToSampleScene(RuntimeAnimatorController controller)
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject player = scene.GetRootGameObjects()
                .FirstOrDefault(root => root.CompareTag("Player"));

            if (player == null)
            {
                throw new InvalidOperationException("A root object tagged Player was not found in " + ScenePath);
            }

            Animator animator = player.GetComponent<Animator>();
            if (animator == null)
            {
                throw new InvalidOperationException("The Player object has no Animator component.");
            }

            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = true;
            animator.updateMode = AnimatorUpdateMode.AnimatePhysics;
            player.GetComponent<Rigidbody>().interpolation = RigidbodyInterpolation.Interpolate;

            if (player.GetComponent<InPlaceRootMotionFallback>() == null)
            {
                player.AddComponent<InPlaceRootMotionFallback>();
            }

            EditorUtility.SetDirty(animator);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent))
            {
                throw new InvalidOperationException("Cannot create asset folder: " + path);
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
