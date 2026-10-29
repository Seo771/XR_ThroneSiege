using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ThroneSiege.Editor
{
    public static class SkullSkillSetup
    {
        private const string ModelPath = "Assets/Prefab/Skills/Skull/SkullSkillMesh.obj";
        private const string MeshPath = "Assets/Prefab/Skills/Skull/SkullSkillMesh.asset";
        private const string PrefabPath = "Assets/Prefab/Skills/Skull/Skull.prefab";

        [InitializeOnLoadMethod]
        private static void OnReload() => EditorApplication.delayCall += Repair;

        [MenuItem("ThroneSiege/Repair Skull Skill Mesh")]
        public static void Repair()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
                !File.Exists(ModelPath) || !File.Exists(PrefabPath)) return;
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (mesh == null)
            {
                var importedMesh = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Mesh>().FirstOrDefault();
                if (importedMesh == null)
                {
                    AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport);
                    importedMesh = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Mesh>().FirstOrDefault();
                }
                if (importedMesh == null)
                {
                    Debug.LogError("SkullSkillMesh.obj did not produce a Mesh.");
                    return;
                }
                mesh = Object.Instantiate(importedMesh);
                mesh.name = "SkullSkillMesh";
                AssetDatabase.CreateAsset(mesh, MeshPath);
            }

            var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var filter = contents.GetComponent<MeshFilter>();
                if (filter == null) filter = contents.AddComponent<MeshFilter>();
                if (filter.sharedMesh != mesh)
                {
                    filter.sharedMesh = mesh;
                    PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Temp");
            File.WriteAllText("Temp/SkullSkillMeshCheck.txt", "PASS: SkullSkill uses native mesh asset; vertices=" + mesh.vertexCount);
            Debug.Log("SkullSkill Mesh repaired and verified: " + mesh.vertexCount + " vertices.");
        }
    }
}
