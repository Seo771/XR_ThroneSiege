using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ThroneSiege.AI
{
    // Small test-arena NavMesh, built from explicitly assigned floor colliders only.
    [DefaultExecutionOrder(-500)]
    public sealed class RuntimeArenaNavMesh : MonoBehaviour
    {
        [SerializeField] private MeshCollider[] floors;
        private NavMeshData data;
        private NavMeshDataInstance instance;
        public void SetFloors(MeshCollider[] value) { floors = value; }

        private void Awake()
        {
            var sources = new List<NavMeshBuildSource>();
            Bounds bounds = new Bounds(transform.position, Vector3.one);
            foreach (MeshCollider floor in floors)
            {
                if (floor == null || floor.sharedMesh == null) continue;
                sources.Add(new NavMeshBuildSource {
                    shape = NavMeshBuildSourceShape.Mesh, sourceObject = floor.sharedMesh,
                    transform = floor.transform.localToWorldMatrix, area = 0
                });
                bounds.Encapsulate(floor.bounds);
            }
            if (sources.Count == 0) { Debug.LogError("Arena NavMesh has no floor sources.", this); return; }
            bounds.Expand(new Vector3(2f, 10f, 2f));
            data = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0), sources,
                bounds, Vector3.zero, Quaternion.identity);
            if (data != null) instance = NavMesh.AddNavMeshData(data);
        }

        private void OnDestroy()
        {
            if (instance.valid) instance.Remove();
            if (data != null) Destroy(data);
        }
    }
}
