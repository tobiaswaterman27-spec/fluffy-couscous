using UnityEngine;
using AfterHours.Audio;
using AfterHours.World;

namespace AfterHours.EditorTools
{
    /// <summary>A looping hum/bubble/whirr on a prop that is still running (freezers, fish tanks, a projector...).</summary>
    public static partial class MallBuilder
    {
        static void PropHum(Transform g, Vector3 pos, ProceduralSounds.Ambience kind, float volume, float maxDistance, float masking = 0.15f)
        {
            var go = new GameObject("Prop sound " + kind);
            go.transform.SetParent(g, false);
            go.transform.localPosition = pos;
            go.AddComponent<AudioSource>().playOnAwake = false;
            AmbientSound a = go.AddComponent<AmbientSound>();
            a.kind = kind;
            a.volume = volume;
            a.maxDistance = maxDistance;
            a.maskingLevel = masking;
        }
    }
}
