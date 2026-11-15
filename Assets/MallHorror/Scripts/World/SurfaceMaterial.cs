using UnityEngine;

namespace MallHorror.World
{
    /// <summary>Put this on any collider (or its parent) to say what it is made of.</summary>
    public class SurfaceMaterial : MonoBehaviour
    {
        public SurfaceType type = SurfaceType.Tile;
    }
}
