using UnityEngine;
using AfterHours.Audio;
using AfterHours.Interaction;

namespace AfterHours.Puzzles
{
    /// <summary>Unlocks (and optionally swings open) a door.</summary>
    public class UnlockDoorOutput : PuzzleOutput
    {
        public Door door;
        public bool alsoOpen = true;
        bool wasLocked;

        public override void Activate(GameObject actor)
        {
            if (door == null) return;
            wasLocked = door.locked;
            door.locked = false;
            InteractionFx.Play(ProceduralSounds.Sfx.Unlock, door.transform.position + Vector3.up, 1f, 8f, actor, NoiseKind.Machinery);
            if (alsoOpen) door.Open(door.transform.position - door.transform.forward, 0.6f, actor);
        }

        public override void ResetOutput()
        {
            if (door == null) return;
            door.Close(1f, null);
            door.locked = wasLocked || door.locked;
        }
    }

    /// <summary>Drives a shutter all the way up with its motor (slow, very loud).</summary>
    public class RaiseShutterOutput : PuzzleOutput
    {
        public RollerShutter shutter;
        public float speed = 0.3f;

        public override void Activate(GameObject actor)
        {
            if (shutter != null) shutter.MotorRaise(speed, actor);
        }

        public override void ResetOutput()
        {
            if (shutter != null) shutter.ResetToStart();
        }
    }

    /// <summary>Slides heavy panels (automatic entrance doors) sideways, smoothly.</summary>
    public class SlidePanelsOutput : PuzzleOutput
    {
        public Transform[] panels;
        public Vector3[] offsets;
        public float seconds = 3f;
        Vector3[] start;
        float t = -1f;

        public override void Activate(GameObject actor)
        {
            if (panels == null) return;
            start = new Vector3[panels.Length];
            for (int i = 0; i < panels.Length; i++) start[i] = panels[i].position;
            t = 0f;
            if (panels.Length > 0) InteractionFx.Play(ProceduralSounds.Sfx.ShutterRoll, panels[0].position, 1f, 18f, actor, NoiseKind.Machinery);
        }

        void Update()
        {
            if (t < 0f) return;
            t += Time.deltaTime / seconds;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            for (int i = 0; i < panels.Length; i++) panels[i].position = start[i] + offsets[i] * k;
            if (t >= 1f) t = -1f;
        }

        public override void ResetOutput()
        {
            if (start == null) return;
            t = -1f;
            for (int i = 0; i < panels.Length; i++) panels[i].position = start[i];
        }
    }

    /// <summary>Makes the escape exit live.</summary>
    public class EnableEscapeOutput : PuzzleOutput
    {
        public EscapeTrigger trigger;

        public override void Activate(GameObject actor)
        {
            if (trigger != null) trigger.open = true;
        }

        public override void ResetOutput()
        {
            if (trigger != null) trigger.open = false;
        }
    }

    /// <summary>A machine starts up: a heavy clunk and a loud noise event (it can attract Elliot).</summary>
    public class MachineStartOutput : PuzzleOutput
    {
        public float noiseRadius = 25f;

        public override void Activate(GameObject actor)
        {
            InteractionFx.Play(ProceduralSounds.Sfx.Slam, transform.position, 1f, noiseRadius, actor, NoiseKind.Machinery);
        }
    }
}
