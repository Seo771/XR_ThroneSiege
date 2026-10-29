using System.Linq;
using ThroneSiege.Combat;
using ThroneSiege.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ThroneSiege.Editor
{
    public static class HealthBarSetup
    {
        public static void ValidateConnections()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
            Repair();
            var bars = Object.FindObjectsOfType<HealthBarUI>(true);
            if (bars.Length != 1) throw new System.InvalidOperationException("Expected one player health bar.");
            foreach (var bar in bars)
            {
                var data = new SerializedObject(bar);
                if (data.FindProperty("health").objectReferenceValue == null ||
                    data.FindProperty("fill").objectReferenceValue == null)
                    throw new System.InvalidOperationException("Missing health bar reference: " + bar.name);
            }
            var group = Object.FindObjectOfType<EnemyGroupHealthBar>(true);
            if (group == null) throw new System.InvalidOperationException("Enemy group bar missing.");
            var groupData = new SerializedObject(group);
            if (groupData.FindProperty("battle").objectReferenceValue == null ||
                groupData.FindProperty("fill").objectReferenceValue == null)
                throw new System.InvalidOperationException("Enemy group bar reference missing.");
            Debug.Log("PASS: Player health bar and enemy group bar references resolved.");
        }

        [InitializeOnLoadMethod]
        private static void OnReload() => EditorApplication.delayCall += Repair;

        [MenuItem("ThroneSiege/Connect Health Bars")]
        public static void Repair()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/MainScene.unity") return;
            var objects = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            bool changed = false;
            changed |= Connect(objects, "Hp", "Player_Necromanser");
            changed |= Connect(objects, "EnemtHp", "AI_CrusaderTank");
            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("Player and enemy health bars connected.");
            }
        }

        private static bool Connect(Transform[] objects, string barName, string actorName)
        {
            Transform bar = objects.FirstOrDefault(t => t.name == barName);
            bool group = barName == "EnemtHp";
            Transform actor = group ? objects.FirstOrDefault(t => t.GetComponent<BattleManager>() != null)
                : objects.FirstOrDefault(t => t.name == actorName);
            if (bar == null || actor == null) return false;
            Transform fillTransform = bar.Find("Fill");
            var image = fillTransform != null ? fillTransform.GetComponent<Image>() : null;
            if (image == null) return false;
            bool changed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(bar.gameObject) > 0;
            if (group && bar.GetComponent<HealthBarUI>() != null)
            {
                Undo.DestroyObjectImmediate(bar.GetComponent<HealthBarUI>());
                changed = true;
            }
            MonoBehaviour ui = group ? (MonoBehaviour)bar.GetComponent<EnemyGroupHealthBar>() : bar.GetComponent<HealthBarUI>();
            changed |= ui == null;
            if (ui == null) ui = group ? (MonoBehaviour)Undo.AddComponent<EnemyGroupHealthBar>(bar.gameObject)
                : Undo.AddComponent<HealthBarUI>(bar.gameObject);
            var data = new SerializedObject(ui);
            data.FindProperty(group ? "battle" : "health").objectReferenceValue = group
                ? (Object)actor.GetComponent<BattleManager>() : actor.GetComponent<ActorHealth>();
            data.FindProperty("fill").objectReferenceValue = image;
            changed |= data.ApplyModifiedProperties();
            if (image.type != Image.Type.Filled || image.fillMethod != Image.FillMethod.Horizontal || image.fillOrigin != 0)
            {
                Undo.RecordObject(image, "Connect health bar");
                image.type = Image.Type.Filled;
                image.fillMethod = Image.FillMethod.Horizontal;
                image.fillOrigin = 0;
                EditorUtility.SetDirty(image);
                changed = true;
            }
            return changed;
        }
    }
}
