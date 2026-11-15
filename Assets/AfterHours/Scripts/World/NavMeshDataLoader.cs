using UnityEngine;
using UnityEngine.AI;

namespace AfterHours.World
{
    /// <summary>Loads the mall's baked walkable-area map (for Elliot's path finding) when the scene starts.</summary>
    [DefaultExecutionOrder(-1000)]
    public class NavMeshDataLoader : MonoBehaviour
    {
        public NavMeshData data;
        NavMeshDataInstance instance;

        void OnEnable()
        {
            if (data != null && !instance.valid) instance = NavMesh.AddNavMeshData(data);
        }

        void OnDisable()
        {
            if (instance.valid) instance.Remove();
        }
    }
}
