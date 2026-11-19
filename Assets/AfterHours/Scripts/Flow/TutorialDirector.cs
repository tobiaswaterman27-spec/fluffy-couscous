using UnityEngine;
using AfterHours.Core;
using AfterHours.Creature;
using AfterHours.Hiding;
using AfterHours.Items;
using AfterHours.Player;
using AfterHours.World;

namespace AfterHours.Flow
{
    /// <summary>
    /// The tutorial (main menu > Tutorial): the mall at night with nobody else in it. One thing at a time - look,
    /// walk, sprint, crouch, crawl, pick up, throw, hide, hold your breath, push something heavy - each step ticks
    /// off when you've done it. Nothing is saved; at the end it goes back to the menu.
    /// </summary>
    public class TutorialDirector : MonoBehaviour
    {
        public static bool Running { get; private set; }

        struct Step { public string title, text; }

        static readonly Step[] Steps =
        {
            new Step { title = "Look around", text = "Move the mouse to look around." },
            new Step { title = "Walk", text = "Walk with W A S D." },
            new Step { title = "Sprint", text = "Double-tap W (or any direction key) and keep holding it to sprint.\nRunning is loud: Elliot hears it a long way off." },
            new Step { title = "Crouch", text = "Press Left Shift to crouch. Quieter, slower, and harder to see." },
            new Step { title = "Crawl", text = "Press Left Ctrl to crawl - under desks, counters and into vents.\nPress it again to get up." },
            new Step { title = "Pick something up", text = "There's something on the floor in front of you. Look at it and press E." },
            new Step { title = "Throw it", text = "Right mouse button throws what you're holding (G just drops it).\nIt lands with a noise - a way to send Elliot to the wrong place." },
            new Step { title = "Hide", text = "Get into a hiding place and press E: under a desk or counter, a changing room, a locker.\n" },
            new Step { title = "Hold your breath", text = "While hidden, hold Left Shift to hold your breath - he can hear you panting.\nNot for too long." },
            new Step { title = "Come out", text = "Press E to come out." },
            new Step { title = "Push something heavy", text = "Vending machines can be moved. Look at one, hold E and walk.\nPush one against a door and Elliot has to smash his way through.\n" },
        };

        PlayerController player;
        int step;
        float progress, doneT = -1f, stepDoneT = -1f;
        Vector3 lastPos;
        float lastYaw;
        bool hadItem;
        GUIStyle titleStyle, textStyle, smallStyle;

        /// <summary>Starts the tutorial with the player already placed and in control.</summary>
        public void Begin(PlayerController p)
        {
            player = p;
            Running = true;
            step = 0;
            progress = 0f;
            doneT = -1f;
            if (player != null) { lastPos = player.transform.position; lastYaw = CamYaw(); }
            // nobody else in the building tonight
            foreach (ElliotBrain b in ElliotBrain.All) if (b != null) { b.enabled = false; b.gameObject.SetActive(false); }
            foreach (AfterHours.Machines.MalfunctionDirector m in FindObjectsByType<AfterHours.Machines.MalfunctionDirector>()) m.enabled = false;
            // something to pick up, right in front of you
            ItemPickup nearest = null; float best = float.MaxValue;
            foreach (ItemPickup it in FindObjectsByType<ItemPickup>())
            {
                if (it == null || it.IsHeld || !it.gameObject.activeInHierarchy) continue;
                float d = (it.transform.position - player.transform.position).sqrMagnitude;
                if (d < best) { best = d; nearest = it; }
            }
            if (nearest != null && player != null)
            {
                Vector3 fwd = player.transform.forward; fwd.y = 0f; fwd.Normalize();
                Vector3 at = player.transform.position + fwd * 1.6f + Vector3.up * 0.4f;
                Rigidbody rb = nearest.GetComponent<Rigidbody>();
                if (rb != null) { rb.linearVelocity = Vector3.zero; rb.position = at; }
                nearest.transform.position = at;
            }
        }

        float CamYaw() => Camera.main != null ? Camera.main.transform.eulerAngles.y : 0f;

        void Update()
        {
            if (!Running || player == null || GamePause.IsPaused) return;
            float dt = Time.deltaTime;
            if (doneT >= 0f)
            {
                doneT += dt;
                if (doneT > 7f) { Running = false; GameFlow.Instance?.EndTutorial(); }
                return;
            }
            if (stepDoneT >= 0f)
            {
                // a moment to see the tick before the next step
                stepDoneT += dt;
                if (stepDoneT > 1.2f) { stepDoneT = -1f; step++; progress = 0f; if (step >= Steps.Length) { doneT = 0f; Stats.Event("tutorial"); } }
                return;
            }
            LocomotionState l = player.Motor.Locomotion;
            Vector3 p = player.transform.position;
            float moved = new Vector2(p.x - lastPos.x, p.z - lastPos.z).magnitude;
            lastPos = p;
            float yaw = CamYaw();
            float turned = Mathf.Abs(Mathf.DeltaAngle(lastYaw, yaw));
            lastYaw = yaw;
            PlayerInventory inv = player.GetComponent<PlayerInventory>();
            PlayerGrab grab = player.GetComponent<PlayerGrab>();
            bool done = false;
            switch (step)
            {
                case 0: progress += turned / 270f; done = progress >= 1f; break;
                case 1: progress += moved / 6f; done = progress >= 1f; break;
                case 2: if (l.mode == MoveMode.Sprint) progress += dt / 1.5f; done = progress >= 1f; break;
                case 3: if (l.stance == Stance.Crouching) progress += dt / 1f; done = progress >= 1f; break;
                case 4: if (l.stance == Stance.Crawling) progress += dt / 1.5f; done = progress >= 1f; break;
                case 5: done = inv != null && inv.Held != null; hadItem = done; break;
                case 6: done = hadItem && inv != null && inv.Held == null; if (inv != null && inv.Held != null) hadItem = true; break;
                case 7: done = player.IsHidden; break;
                case 8:
                    if (!player.IsHidden) break;
                    if (HoldingBreath()) progress += dt / 1.5f;
                    done = progress >= 1f;
                    break;
                case 9: done = !player.IsHidden; break;
                case 10: if (grab != null && grab.Held != null) progress += moved / 1.2f; done = progress >= 1f; break;
            }
            if (done)
            {
                progress = 1f;
                stepDoneT = 0f;
                AfterHours.Audio.AudioPool.Play2D(AfterHours.Audio.ProceduralSounds.Effect(AfterHours.Audio.ProceduralSounds.Sfx.Click), 0.5f, 1.4f);
            }
        }

        bool HoldingBreath()
        {
            AfterHours.Audio.PlayerAudio a = player.GetComponentInChildren<AfterHours.Audio.PlayerAudio>();
            return a != null && a.HoldingBreath;
        }

        /// <summary>"12 m ahead, upstairs" - where the nearest one of something is, from where you're looking.</summary>
        string Where<T>() where T : Component
        {
            Camera cam = Camera.main;
            if (cam == null || player == null) return "";
            T best = null; float bd = float.MaxValue;
            foreach (T c in FindObjectsByType<T>())
            {
                if (c == null || !c.gameObject.activeInHierarchy) continue;
                float d = (c.transform.position - player.transform.position).sqrMagnitude;
                if (d < bd) { bd = d; best = c; }
            }
            if (best == null) return "";
            Vector3 to = best.transform.position - player.transform.position;
            float dist = new Vector2(to.x, to.z).magnitude;
            Vector3 f = cam.transform.forward; f.y = 0f;
            float ang = Vector3.SignedAngle(f, new Vector3(to.x, 0f, to.z), Vector3.up);
            string dir = Mathf.Abs(ang) < 35f ? "ahead" : Mathf.Abs(ang) > 140f ? "behind you" : ang > 0f ? "to your right" : "to your left";
            string floor = to.y > 2.5f ? ", upstairs" : to.y < -2.5f ? ", downstairs" : "";
            return $"The nearest is about {Mathf.Max(1, Mathf.RoundToInt(dist))} m {dir}{floor}.";
        }

        void OnGUI()
        {
            if (!Running || GamePause.IsPaused) return;
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter };
                titleStyle.normal.textColor = new Color(0.95f, 0.93f, 0.88f);
                textStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.UpperCenter, wordWrap = true };
                textStyle.normal.textColor = new Color(0.82f, 0.8f, 0.76f);
                smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.UpperCenter };
                smallStyle.normal.textColor = new Color(0.6f, 0.58f, 0.55f);
            }
            float scale = Screen.height / 1080f;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            float w = Screen.width / scale;
            Rect box = new Rect(w * 0.5f - 480f, 1080f - 280f, 960f, 200f);
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = Color.white;
            if (doneT >= 0f)
            {
                GUI.Label(new Rect(box.x, box.y + 24f, box.width, 40f), "That's everything", titleStyle);
                GUI.Label(new Rect(box.x + 30f, box.y + 74f, box.width - 60f, 100f),
                    "On a real night: listen for him, keep out of the light, and hide when you hear him running.\nBack to the menu in a moment...", textStyle);
            }
            else
            {
                int s = Mathf.Min(step, Steps.Length - 1);
                GUI.Label(new Rect(box.x, box.y + 12f, box.width, 24f), $"TUTORIAL   {s + 1} / {Steps.Length}", smallStyle);
                GUI.Label(new Rect(box.x, box.y + 36f, box.width, 40f), (stepDoneT >= 0f ? "✓ " : "") + Steps[s].title, titleStyle);
                string text = Steps[s].text;
                if (s == 7) text += Where<HidingSpot>();
                if (s == 10) text += Where<HeavyMovable>();
                GUI.Label(new Rect(box.x + 30f, box.y + 84f, box.width - 60f, 90f), text, textStyle);
                // progress bar
                GUI.color = new Color(1f, 1f, 1f, 0.15f);
                GUI.DrawTexture(new Rect(box.x + 30f, box.yMax - 14f, box.width - 60f, 4f), Texture2D.whiteTexture);
                GUI.color = new Color(0.95f, 0.8f, 0.45f, 0.9f);
                GUI.DrawTexture(new Rect(box.x + 30f, box.yMax - 14f, (box.width - 60f) * Mathf.Clamp01(stepDoneT >= 0f ? 1f : progress), 4f), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(box.x, box.yMax + 8f, box.width, 24f), "Esc: pause / quit the tutorial", smallStyle);
            }
            GUI.matrix = Matrix4x4.identity;
        }

        void OnDestroy() => Running = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Running = false;
    }
}
