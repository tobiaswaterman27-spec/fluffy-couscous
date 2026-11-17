using System.Collections.Generic;
using UnityEngine;
using AfterHours.Core;

namespace AfterHours.Player
{
    /// <summary>
    /// Turns a player's body into a physics ragdoll (when Elliot drops them) and back again (on respawn).
    /// Built at runtime from the body's own skeleton: capsules sized to each bone, joints with human-ish limits,
    /// so the body falls, slumps against walls and floors, and jerks when it's bitten - until the respawn.
    /// </summary>
    public static class PlayerRagdoll
    {
        struct Part { public string bone, child, parent; public float mass, radius; }

        static readonly Part[] Parts =
        {
            new Part { bone = "Pelvis",    child = "Spine",    parent = null,        mass = 12f, radius = 0.13f },
            new Part { bone = "Spine",     child = "Neck",     parent = "Pelvis",    mass = 16f, radius = 0.13f },
            new Part { bone = "Head",      child = null,       parent = "Spine",     mass = 5f,  radius = 0.105f },
            new Part { bone = "ThighL",    child = "ShinL",    parent = "Pelvis",    mass = 8f,  radius = 0.075f },
            new Part { bone = "ShinL",     child = "FootL",    parent = "ThighL",    mass = 4f,  radius = 0.055f },
            new Part { bone = "ThighR",    child = "ShinR",    parent = "Pelvis",    mass = 8f,  radius = 0.075f },
            new Part { bone = "ShinR",     child = "FootR",    parent = "ThighR",    mass = 4f,  radius = 0.055f },
            new Part { bone = "UpperArmL", child = "ForeArmL", parent = "Spine",     mass = 2.5f, radius = 0.048f },
            new Part { bone = "ForeArmL",  child = "HandL",    parent = "UpperArmL", mass = 1.8f, radius = 0.04f },
            new Part { bone = "UpperArmR", child = "ForeArmR", parent = "Spine",     mass = 2.5f, radius = 0.048f },
            new Part { bone = "ForeArmR",  child = "HandR",    parent = "UpperArmR", mass = 1.8f, radius = 0.04f },
        };

        static readonly Dictionary<PlayerBody, List<Component>> active = new Dictionary<PlayerBody, List<Component>>();

        public static bool IsActive(PlayerBody b) => b != null && active.ContainsKey(b);

        /// <summary>Goes limp. 'velocity' is how it was moving (thrown down, dropped); 'spin' a tumble.</summary>
        public static void Begin(PlayerBody body, Vector3 velocity, Vector3 spin)
        {
            if (body == null || !body.Ready || active.ContainsKey(body)) return;
            var made = new List<Component>();
            var rbs = new Dictionary<string, Rigidbody>();
            var cols = new List<Collider>();
            int layer = GameLayers.PropLayer >= 0 ? GameLayers.PropLayer : 0;
            foreach (Part p in Parts)
            {
                Transform b = body.Bone(p.bone);
                if (b == null) continue;
                Rigidbody rb = b.gameObject.AddComponent<Rigidbody>();
                rb.mass = p.mass; rb.linearDamping = 0.05f; rb.angularDamping = 0.6f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                rb.solverIterations = 12;
                made.Add(rb);
                rbs[p.bone] = rb;
                b.gameObject.layer = layer;

                Transform c = p.child != null ? body.Bone(p.child) : null;
                Collider col;
                if (c != null)
                {
                    Vector3 d = b.InverseTransformPoint(c.position);
                    float len = d.magnitude;
                    var cap = b.gameObject.AddComponent<CapsuleCollider>();
                    Vector3 a = new Vector3(Mathf.Abs(d.x), Mathf.Abs(d.y), Mathf.Abs(d.z));
                    cap.direction = a.x > a.y && a.x > a.z ? 0 : a.y > a.z ? 1 : 2;
                    cap.center = d * 0.5f;
                    cap.radius = p.radius;
                    cap.height = Mathf.Max(len + p.radius * 1.2f, p.radius * 2f);
                    col = cap;
                }
                else
                {
                    // the head: a ball above the neck
                    Transform neck = body.Bone("Neck");
                    Vector3 upW = neck != null ? (b.position - neck.position).normalized : Vector3.up;
                    var sph = b.gameObject.AddComponent<SphereCollider>();
                    sph.center = b.InverseTransformPoint(b.position + upW * 0.09f);
                    sph.radius = p.radius;
                    col = sph;
                }
                made.Add(col);
                cols.Add(col);
            }
            // joints, with limits loose enough to slump, tight enough not to fold the wrong way
            foreach (Part p in Parts)
            {
                if (p.parent == null || !rbs.ContainsKey(p.bone) || !rbs.ContainsKey(p.parent)) continue;
                var j = rbs[p.bone].gameObject.AddComponent<CharacterJoint>();
                j.connectedBody = rbs[p.parent];
                Transform c = p.child != null ? body.Bone(p.child) : null;
                Vector3 along = c != null ? rbs[p.bone].transform.InverseTransformPoint(c.position).normalized : Vector3.up;
                j.axis = Vector3.Cross(along, Mathf.Abs(along.x) < 0.9f ? Vector3.right : Vector3.forward).normalized;
                j.swingAxis = along;
                bool hinge = p.bone.StartsWith("Shin") || p.bone.StartsWith("ForeArm");
                bool neck = p.bone == "Head";
                j.lowTwistLimit = new SoftJointLimit { limit = hinge ? -70f : neck ? -35f : -25f };
                j.highTwistLimit = new SoftJointLimit { limit = hinge ? 70f : neck ? 35f : 25f };
                j.swing1Limit = new SoftJointLimit { limit = hinge ? 8f : neck ? 40f : p.bone.StartsWith("UpperArm") ? 75f : 40f };
                j.swing2Limit = new SoftJointLimit { limit = hinge ? 8f : neck ? 30f : p.bone.StartsWith("UpperArm") ? 60f : 30f };
                j.enableProjection = true;
                j.projectionDistance = 0.05f;
                j.enablePreprocessing = false;
                made.Add(j);
            }
            for (int i = 0; i < cols.Count; i++)
                for (int k = i + 1; k < cols.Count; k++) Physics.IgnoreCollision(cols[i], cols[k]);
            foreach (var kv in rbs)
            {
                kv.Value.linearVelocity = velocity;
                kv.Value.angularVelocity = spin;
            }
            var skin = body.GetComponentInChildren<SkinnedMeshRenderer>();
            if (skin != null) skin.updateWhenOffscreen = true;
            active[body] = made;
        }

        /// <summary>A hit to the limp body (a bite tearing at it, being dragged).</summary>
        public static void Push(PlayerBody body, string bone, Vector3 impulse)
        {
            if (!IsActive(body)) return;
            Transform b = body.Bone(bone);
            Rigidbody rb = b != null ? b.GetComponent<Rigidbody>() : null;
            if (rb != null) rb.AddForce(impulse, ForceMode.Impulse);
        }

        /// <summary>Back to an animated body (respawn).</summary>
        public static void End(PlayerBody body)
        {
            if (body == null || !active.TryGetValue(body, out List<Component> made)) return;
            active.Remove(body);
            // joints first, then colliders, then bodies
            foreach (Component c in made) if (c is Joint) Object.Destroy(c);
            foreach (Component c in made) if (c is Collider) Object.Destroy(c);
            foreach (Component c in made) if (c is Rigidbody) Object.Destroy(c);
            var skin = body.GetComponentInChildren<SkinnedMeshRenderer>();
            if (skin != null) skin.updateWhenOffscreen = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => active.Clear();
    }
}
