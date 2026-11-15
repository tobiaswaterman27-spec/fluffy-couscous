using UnityEngine;

namespace MallHorror.World
{
    public enum SurfaceType
    {
        Tile,
        Carpet,
        Concrete,
        Metal,
        VentMetal,
        Wood,
        Glass
    }

    /// <summary>How loud / how audible each floor material is.</summary>
    public static class SurfaceAcoustics
    {
        /// <summary>Multiplier on how far a footstep can be heard (the creature's hearing uses this).</summary>
        public static float NoiseMultiplier(SurfaceType t)
        {
            switch (t)
            {
                case SurfaceType.Carpet: return 0.45f;
                case SurfaceType.Tile: return 1.0f;
                case SurfaceType.Concrete: return 0.9f;
                case SurfaceType.Metal: return 1.6f;
                case SurfaceType.VentMetal: return 1.45f;
                case SurfaceType.Wood: return 0.95f;
                case SurfaceType.Glass: return 1.2f;
                default: return 1f;
            }
        }

        /// <summary>Playback volume multiplier for footstep audio.</summary>
        public static float FootstepVolume(SurfaceType t)
        {
            switch (t)
            {
                case SurfaceType.Carpet: return 0.45f;
                case SurfaceType.Metal: return 1.0f;
                case SurfaceType.VentMetal: return 0.95f;
                default: return 0.8f;
            }
        }
    }

    public static class SurfaceLookup
    {
        public static SurfaceType Get(Collider c, SurfaceType fallback = SurfaceType.Concrete)
        {
            if (c == null) return fallback;
            var m = c.GetComponentInParent<SurfaceMaterial>();
            return m != null ? m.type : fallback;
        }
    }
}
