using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using ThroneSiege.AI;
using ThroneSiege.Combat;

namespace ThroneSiege.Editor
{
    public static class CrusaderTankBuilder
    {
        private const string Folder = "Assets/ThroneSiege/Animations/CrusaderTank";
        private const string ControllerPath = Folder + "/CrusaderTankAI.controller";
        private const string PrefabPath = "Assets/ThroneSiege/Prefabs/CrusaderTankEnemy.prefab";

        [MenuItem("ThroneSiege")]
        public static void Build()
        {
            EnsureFolder(Folder);
            EnsureFolder("Assets/ThroneSiege/Prefabs");
            AnimationClip idle = Clip("idle1", "Idle", true);
            AnimationClip walk = Clip("walk", "Walk", true);
            AnimationClip run = Clip("run", "Run", true);
            AnimationClip combat = Clip("battle poze", "CombatIdle", true);
            AnimationClip hit = Clip("gethit", "Hit", false);
            AnimationClip death = Clip("death", "Death", false);
            AnimationClip[] attacks = {
                Clip("atack1", "Attack1", false), Clip("atack2", "Attack2", false),
                Clip("atack shield", "ShieldAttack", false)
            };
            foreach (AnimationClip attack in attacks)
                AnimationUtility.SetAnimationEvents(attack, new[] {
                    new AnimationEvent { functionName = "LockAttackFacing", time = attack.length * 0.3f },
                    new AnimationEvent { functionName = "AttackImpact", time = attack.length * 0.5f },
                    new AnimationEvent { functionName = "AttackFinished", time = attack.length * 0.98f }
                });

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            else
            {
                // GUID를 유지하면서 프로젝트 전용 컨트롤러의 내부 구성만 다시 만든다.
                foreach (UnityEngine.Object child in AssetDatabase.LoadAllAssetsAtPath(ControllerPath))
                    if (child != controller) UnityEngine.Object.DestroyImmediate(child, true);
                controller.layers = new AnimatorControllerLayer[0];
                controller.parameters = new AnimatorControllerParameter[0];
                controller.AddLayer("Base Layer");
            }
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("InCombat", AnimatorControllerParameterType.Bool);
            controller.AddParameter("AttackIndex", AnimatorControllerParameterType.Int);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);
            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            var locomotion = new BlendTree { name = "Idle Walk Run", blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed", useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(locomotion, controller);
            locomotion.AddChild(idle, 0f); locomotion.AddChild(walk, 1.4f); locomotion.AddChild(run, 3f);
            AnimatorState idleState = State(sm, "Idle", locomotion, 250, 100);
            AnimatorState combatState = State(sm, "CombatIdle", combat, 500, 100);
            AnimatorState hitState = State(sm, "Hit", hit, 250, 350);
            AnimatorState deathState = State(sm, "Death", death, 750, 350);
            sm.defaultState = idleState;
            Condition(idleState.AddTransition(combatState), "InCombat", AnimatorConditionMode.If);
            Condition(combatState.AddTransition(idleState), "InCombat", AnimatorConditionMode.IfNot);
            // 사망을 먼저 검사하고, 사망 후에는 피격이나 공격 상태로 전이하지 않게 한다.
            Condition(sm.AddAnyStateTransition(deathState), "Dead", AnimatorConditionMode.If, 0.05f);
            AnimatorStateTransition hitTransition = sm.AddAnyStateTransition(hitState);
            Condition(hitTransition, "Hit", AnimatorConditionMode.If, 0.05f);
            hitTransition.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
            Exit(hitState, idleState);
            for (int i = 0; i < attacks.Length; i++)
            {
                AnimatorState attack = State(sm, attacks[i].name, attacks[i], 750, 30 + i * 90);
                AnimatorStateTransition transition = combatState.AddTransition(attack);
                Condition(transition, "Attack", AnimatorConditionMode.If, 0.07f);
                transition.AddCondition(AnimatorConditionMode.Equals, i, "AttackIndex");
                transition.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
                Exit(attack, combatState);
            }
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/SampleScene.unity")
                throw new InvalidOperationException("Open SampleScene before building the AI.");
            GameObject player = scene.GetRootGameObjects().FirstOrDefault(go => go.CompareTag("Player"));
            GameObject floor = scene.GetRootGameObjects().FirstOrDefault(go => go.name == "Plane");
            if (player == null || floor == null || floor.GetComponent<MeshCollider>() == null)
                throw new InvalidOperationException("SampleScene needs Player and Plane with MeshCollider.");
            ActorHealth playerHealth = player.GetComponent<ActorHealth>();
            if (playerHealth == null) playerHealth = player.AddComponent<ActorHealth>();
            playerHealth.ConfigurePlayer();
            EditorUtility.SetDirty(playerHealth);
            RuntimeArenaNavMesh navigation = floor.GetComponent<RuntimeArenaNavMesh>();
            if (navigation == null) navigation = floor.AddComponent<RuntimeArenaNavMesh>();
            navigation.SetFloors(new[] { floor.GetComponent<MeshCollider>() });
            EditorUtility.SetDirty(navigation);

            GameObject enemy = scene.GetRootGameObjects().FirstOrDefault(go =>
                go.name == "CrusaderTankEnemy" || go.name == "Crusader tank_prefab");
            if (enemy == null)
                enemy = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/AssetStore/Crusader tank/prefab/Crusader tank_prefab.prefab"));
            enemy.name = "CrusaderTankEnemy";
            // 이미 배치된 Standard Assets AI 캐릭터의 컴포넌트는 유지하되,
            // 이동은 이 NavMeshAgent만 담당하도록 기존 이동 컴포넌트를 비활성화한다.
            foreach (MonoBehaviour component in enemy.GetComponents<MonoBehaviour>())
                if (component is UnityStandardAssets.Characters.ThirdPerson.ThirdPersonCharacter ||
                    component is UnityStandardAssets.Characters.ThirdPerson.AICharacterControl ||
                    component is UnityStandardAssets.Characters.ThirdPerson.ThirdPersonUserControl ||
                    component is ThroneSiege.Player.InPlaceRootMotionFallback) component.enabled = false;
            Rigidbody enemyBody = enemy.GetComponent<Rigidbody>();
            if (enemyBody != null) { enemyBody.isKinematic = true; enemyBody.useGravity = false; }
            enemy.transform.position = player.transform.position + new Vector3(3f, 0f, 2.5f);
            enemy.transform.rotation = Quaternion.LookRotation(player.transform.position - enemy.transform.position);
            Animator animator = enemy.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            Bounds bounds = enemy.GetComponentsInChildren<Renderer>().First().bounds;
            foreach (Renderer renderer in enemy.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
            // 가져온 모델의 크기를 루트 로컬 좌표로 환산해 충돌 캡슐의 크기를 맞춘다.
            enemy.transform.position += Vector3.up * (floor.GetComponent<Collider>().bounds.max.y - bounds.min.y);
            CapsuleCollider capsule = enemy.GetComponent<CapsuleCollider>();
            if (capsule == null) capsule = enemy.AddComponent<CapsuleCollider>();
            capsule.center = enemy.transform.InverseTransformPoint(bounds.center +
                Vector3.up * (floor.GetComponent<Collider>().bounds.max.y - bounds.min.y));
            capsule.height = bounds.size.y / Mathf.Abs(enemy.transform.lossyScale.y);
            capsule.radius = 0.3f / Mathf.Abs(enemy.transform.lossyScale.x);
            NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
            if (agent == null) agent = enemy.AddComponent<NavMeshAgent>();
            agent.radius = 0.35f; agent.height = Mathf.Clamp(bounds.size.y, 1.5f, 2.5f);
            agent.baseOffset = 0f; agent.speed = 3f; agent.acceleration = 12f;
            agent.angularSpeed = 240f; agent.stoppingDistance = 1.75f;
            agent.updateRotation = false;
            if (enemy.GetComponent<ActorHealth>() == null) enemy.AddComponent<ActorHealth>();
            if (enemy.GetComponent<EnemyStats>() == null) enemy.AddComponent<EnemyStats>();
            CrusaderTankAI ai = enemy.GetComponent<CrusaderTankAI>();
            if (ai == null) ai = enemy.AddComponent<CrusaderTankAI>();
            float[] durations = attacks.Select(clip => clip.length).ToArray();
            ai.Configure(null, durations, hit.length);
            // 재사용할 적 프리팹에는 현재 씬의 플레이어 참조를 저장하지 않는다.
            PrefabUtility.SaveAsPrefabAsset(enemy, PrefabPath);
            ai.Configure(player.transform, durations, hit.length);
            EditorUtility.SetDirty(ai);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = enemy;
            Debug.Log("[ThroneSiege] Crusader AI ready. Controller, prefab, player health and arena NavMesh configured.");
        }

        private static AnimationClip Clip(string take, string name, bool loop)
        {
            string path = Folder + "/" + name + ".anim";
            AnimationClip source = AssetDatabase.LoadAllAssetsAtPath(
                "Assets/AssetStore/Crusader tank/animation/crusadershield@" + take + ".fbx")
                .OfType<AnimationClip>().First(clip => !clip.name.StartsWith("__preview__"));
            AnimationClip clipAsset = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clipAsset == null) { clipAsset = new AnimationClip(); AssetDatabase.CreateAsset(clipAsset, path); }
            EditorUtility.CopySerialized(source, clipAsset); clipAsset.name = name;
            var serialized = new SerializedObject(clipAsset);
            serialized.FindProperty("m_AnimationClipSettings.m_LoopTime").boolValue = loop;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AnimationUtility.SetAnimationEvents(clipAsset, new AnimationEvent[0]);
            EditorUtility.SetDirty(clipAsset);
            return clipAsset;
        }

        private static AnimatorState State(AnimatorStateMachine sm, string name, Motion motion, float x, float y)
        { AnimatorState state = sm.AddState(name, new Vector3(x,y)); state.motion = motion; return state; }
        private static void Condition(AnimatorStateTransition t, string parameter, AnimatorConditionMode mode, float duration = 0.1f)
        { t.hasExitTime = false; t.hasFixedDuration = true; t.duration = duration; t.canTransitionToSelf = false; t.AddCondition(mode, 0, parameter); }
        private static void Exit(AnimatorState source, AnimatorState destination)
        { var t = source.AddTransition(destination); t.hasExitTime = true; t.exitTime = 1f; t.hasFixedDuration = true; t.duration = 0.08f; t.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead"); }
        private static void EnsureFolder(string folder)
        { if (AssetDatabase.IsValidFolder(folder)) return; string parent = folder.Substring(0, folder.LastIndexOf('/')); EnsureFolder(parent); AssetDatabase.CreateFolder(parent, folder.Substring(folder.LastIndexOf('/') + 1)); }
    }
}
