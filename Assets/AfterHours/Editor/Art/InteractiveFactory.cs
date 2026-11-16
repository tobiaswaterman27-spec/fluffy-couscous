using UnityEditor;
using UnityEngine;
using AfterHours.Core;
using AfterHours.Hiding;
using AfterHours.Interaction;
using AfterHours.World;

namespace AfterHours.EditorTools
{
    /// <summary>Builds interactive, physical objects: doors, lockers, cupboards, drawers, shutters, vents, switches.</summary>
    public static class InteractiveFactory
    {
        static void Dynamic(GameObject go)
        {
            GameObjectUtility.SetStaticEditorFlags(go, 0);
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
        }

        static GameObject Part(string name, Transform parent, Vector3 localPos, Vector3 size, Material m, SurfaceType s, bool collider = true)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = m;
            if (collider) go.AddComponent<SurfaceMaterial>().type = s;
            else Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        /// <summary>Which way does the wall run at a doorway? (Casting along the wall hits the jambs.)</summary>
        public static Vector3 DetectWallAxis(Vector3 floorCentre, float width)
        {
            Vector3 origin = floorCentre + Vector3.up;
            float reach = width * 0.5f + 0.35f;
            bool hitX = Physics.Raycast(origin, Vector3.right, reach) && Physics.Raycast(origin, Vector3.left, reach);
            bool hitZ = Physics.Raycast(origin, Vector3.forward, reach) && Physics.Raycast(origin, Vector3.back, reach);
            return hitZ && !hitX ? Vector3.forward : Vector3.right;
        }

        /// <summary>A hinged door filling an opening centred on floorCentre, in a wall running along wallAxis.</summary>
        public static Door HingedDoor(Transform parent, string name, Vector3 floorCentre, Vector3 wallAxis, float width, float height,
                                      Material leafMat, SurfaceType surface, Material handleMat, DoorKind kind, DoorWeight weight,
                                      bool locked, string lockId, bool squeaky, bool faulty, bool forceable, float thickness = 0.045f,
                                      Vector3? opensToward = null)
        {
            Vector3 normal = Vector3.Cross(Vector3.up, wallAxis).normalized;
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            // LookRotation(normal) makes local +X point along -wallAxis, so the hinge goes at the +wallAxis jamb.
            root.transform.position = floorCentre + wallAxis * (width * 0.5f - 0.01f);
            root.transform.rotation = Quaternion.LookRotation(normal);

            GameObject leaf = Part("Leaf", root.transform, new Vector3((width - 0.02f) * 0.5f, height * 0.5f, 0f), new Vector3(width - 0.02f, height - 0.01f, thickness), leafMat, surface);
            EditorBuild.SetLayerRecursive(leaf, GameLayers.InteractableLayer);
            for (int s = -1; s <= 1; s += 2)
            {
                GameObject handle = Part("Handle", root.transform, new Vector3(width - 0.12f, 1.0f, s * (thickness * 0.5f + 0.03f)), new Vector3(0.14f, 0.025f, 0.025f), handleMat, SurfaceType.Metal, false);
                handle.transform.SetParent(leaf.transform, true);
            }

            Rigidbody rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            Door d = root.AddComponent<Door>();
            d.leaf = leaf.transform;
            d.width = width;
            d.height = height;
            d.thickness = thickness;
            d.kind = kind;
            d.weight = weight;
            d.locked = locked;
            d.lockId = lockId ?? "";
            d.squeaky = squeaky;
            d.faulty = faulty;
            d.forceable = forceable;
            d.maxAngle = 88f; // stops just short of the wall beside the opening
            if (opensToward.HasValue)
            {
                // A positive angle swings the leaf towards the door's local -Z (= -normal).
                d.fixedSide = Vector3.Dot(-normal, opensToward.Value) > 0f ? 1 : -1;
            }
            Dynamic(root);
            return d;
        }

        /// <summary>
        /// A tall hollow locker (0.62 m wide) with a light hinged door. Hollow, so it can be hidden in (Part 11)
        /// and can hold an item. 'facing' = direction the door faces.
        /// </summary>
        public static GameObject Locker(Transform parent, Vector3 floorCentre, Vector3 facing, Material body, Material door, Material handle, bool locked = false)
        {
            var root = new GameObject("Locker");
            root.transform.SetParent(parent, false);
            root.transform.position = floorCentre;
            root.transform.rotation = Quaternion.LookRotation(facing);
            const float w = 0.62f, h = 1.95f, dpt = 0.58f, t = 0.02f;
            Part("Back", root.transform, new Vector3(0f, h * 0.5f, -dpt * 0.5f + t * 0.5f), new Vector3(w, h, t), body, SurfaceType.Metal);
            Part("Side", root.transform, new Vector3(-w * 0.5f + t * 0.5f, h * 0.5f, 0f), new Vector3(t, h, dpt), body, SurfaceType.Metal);
            Part("Side", root.transform, new Vector3(w * 0.5f - t * 0.5f, h * 0.5f, 0f), new Vector3(t, h, dpt), body, SurfaceType.Metal);
            Part("Top", root.transform, new Vector3(0f, h - t * 0.5f, 0f), new Vector3(w, t, dpt), body, SurfaceType.Metal);
            Part("Bottom", root.transform, new Vector3(0f, 0.06f, 0f), new Vector3(w, 0.12f, dpt), body, SurfaceType.Metal);
            Part("Vent slots", root.transform, new Vector3(0f, h - 0.2f, dpt * 0.5f + 0.004f), new Vector3(0.3f, 0.12f, 0.004f), handle, SurfaceType.Metal, false);
            Vector3 worldAxis = root.transform.right;
            Door d = HingedDoor(root.transform, "Locker door", floorCentre + facing * (dpt * 0.5f) + Vector3.up * 0.12f, worldAxis, w - 0.02f, h - 0.15f,
                door, SurfaceType.Metal, handle, DoorKind.Storage, DoorWeight.Light, locked, "key_lockers", false, false, true, 0.018f, facing);
            d.maxAngle = 110f;
            CutVentSlits(d, door, h - 0.15f);
            AddHidingSpot(root, floorCentre, facing, w - 0.06f, dpt, 0.12f, 0.35f, 35f, d);
            Dynamic(root);
            return root;
        }

        /// <summary>A hollow double-door cupboard / wardrobe (1.1 m wide).</summary>
        public static GameObject Cupboard(Transform parent, Vector3 floorCentre, Vector3 facing, Material body, Material doors, Material handle)
        {
            var root = new GameObject("Cupboard");
            root.transform.SetParent(parent, false);
            root.transform.position = floorCentre;
            root.transform.rotation = Quaternion.LookRotation(facing);
            const float w = 1.1f, h = 2.0f, dpt = 0.62f, t = 0.025f;
            Part("Back", root.transform, new Vector3(0f, h * 0.5f, -dpt * 0.5f + t * 0.5f), new Vector3(w, h, t), body, SurfaceType.Wood);
            Part("Side", root.transform, new Vector3(-w * 0.5f + t * 0.5f, h * 0.5f, 0f), new Vector3(t, h, dpt), body, SurfaceType.Wood);
            Part("Side", root.transform, new Vector3(w * 0.5f - t * 0.5f, h * 0.5f, 0f), new Vector3(t, h, dpt), body, SurfaceType.Wood);
            Part("Top", root.transform, new Vector3(0f, h - t * 0.5f, 0f), new Vector3(w, t, dpt), body, SurfaceType.Wood);
            Part("Bottom", root.transform, new Vector3(0f, 0.05f, 0f), new Vector3(w, 0.1f, dpt), body, SurfaceType.Wood);
            Vector3 right = root.transform.right;
            Vector3 front = floorCentre + facing * (dpt * 0.5f) + Vector3.up * 0.1f;
            Door dl = HingedDoor(root.transform, "Cupboard door L", front - right * (w * 0.25f), -right, w * 0.5f - 0.02f, h - 0.12f, doors, SurfaceType.Wood, handle,
                DoorKind.Storage, DoorWeight.Light, false, "", false, false, true, 0.02f, facing);
            Door dr = HingedDoor(root.transform, "Cupboard door R", front + right * (w * 0.25f), right, w * 0.5f - 0.02f, h - 0.12f, doors, SurfaceType.Wood, handle,
                DoorKind.Storage, DoorWeight.Light, false, "", false, false, true, 0.02f, facing);
            AddHidingSpot(root, floorCentre, facing, w - 0.1f, dpt, 0.1f, 0.6f, 50f, dl, dr);
            Dynamic(root);
            return root;
        }

        /// <summary>
        /// Real locker doors have louvred slits at eye height: you can peek out through them while hiding (and see
        /// a sliver of light, or something walking past). The door keeps one solid collider; only its look changes.
        /// </summary>
        static void CutVentSlits(Door d, Material mat, float doorHeight)
        {
            Transform leaf = d.leaf;
            if (leaf == null) return;
            Renderer r = leaf.GetComponent<Renderer>();
            if (r == null) return;
            r.enabled = false;
            // In the leaf's unit-cube space: y from -0.5 (bottom) to 0.5 (top).
            float slitTop = (1.72f / doorHeight) - 0.5f, slitBottom = (1.48f / doorHeight) - 0.5f;
            void Piece(string n, float y0, float y1, float zScale = 1f)
            {
                GameObject p = GameObject.CreatePrimitive(PrimitiveType.Cube);
                p.name = n;
                Object.DestroyImmediate(p.GetComponent<Collider>());
                p.transform.SetParent(leaf, false);
                p.transform.localPosition = new Vector3(0f, (y0 + y1) * 0.5f, 0f);
                p.transform.localScale = new Vector3(1f, y1 - y0, zScale);
                p.GetComponent<Renderer>().sharedMaterial = mat;
            }
            Piece("Door lower", -0.5f, slitBottom);
            Piece("Door upper", slitTop, 0.5f);
            int slats = 5;
            float step = (slitTop - slitBottom) / slats;
            for (int i = 0; i < slats; i++)
            {
                float y = slitBottom + i * step;
                Piece("Louvre", y, y + step * 0.55f, 1.6f); // angled-looking lip: a bit thicker than the door
            }
        }

        /// <summary>
        /// Makes a hollow furniture piece a hiding place: a thin trigger at the back wall (so the ray reaches it only
        /// when the door is open, and items on the floor of the locker are still reachable).
        /// </summary>
        public static HidingSpot AddHidingSpot(GameObject root, Vector3 floorCentre, Vector3 facing, float innerWidth, float depth,
                                               float floorHeight, float quality, float lookRange, params Door[] doors)
        {
            var go = new GameObject("Hiding place");
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = new Vector3(0f, 1.15f, -depth * 0.5f + 0.09f);
            BoxCollider box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(innerWidth, 1.3f, 0.1f);
            go.layer = GameLayers.InteractableLayer >= 0 ? GameLayers.InteractableLayer : 0;
            HidingSpot spot = go.AddComponent<HidingSpot>();
            spot.doors = doors;
            spot.insidePoint = floorCentre + Vector3.up * floorHeight - facing * 0.03f;
            spot.outsidePoint = floorCentre + facing * (depth * 0.5f + 0.55f);
            spot.facingYaw = Quaternion.LookRotation(facing).eulerAngles.y;
            spot.quality = quality;
            spot.lookRange = lookRange;
            return spot;
        }

        /// <summary>A drawer whose front sits flush at closedCentre and slides out along slideDir.</summary>
        public static Drawer AddDrawer(Transform parent, Vector3 closedCentre, Vector3 size, Vector3 slideDir, Material m)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Drawer";
            go.transform.SetParent(parent, false);
            go.transform.position = closedCentre;
            go.transform.rotation = Quaternion.LookRotation(slideDir);
            go.transform.localScale = new Vector3(size.x, size.y, size.z);
            go.GetComponent<Renderer>().sharedMaterial = m;
            go.AddComponent<SurfaceMaterial>().type = SurfaceType.Wood;
            Drawer d = go.AddComponent<Drawer>();
            d.travel = size.z * 0.75f;
            EditorBuild.SetLayerRecursive(go, GameLayers.InteractableLayer);
            Dynamic(go);
            return d;
        }

        /// <summary>Turns an existing shutter panel into a working roller shutter.</summary>
        public static RollerShutter MakeShutter(GameObject panel, bool locked, bool jammed, float startOpen, string lockId)
        {
            RollerShutter r = panel.AddComponent<RollerShutter>();
            r.panel = panel.transform;
            r.locked = locked;
            r.jammed = jammed;
            r.startOpen = startOpen;
            r.lockId = lockId ?? "";
            r.travel = Mathf.Max(1f, panel.transform.lossyScale.y - 0.3f);
            EditorBuild.SetLayerRecursive(panel, GameLayers.InteractableLayer);
            Dynamic(panel);
            return r;
        }

        /// <summary>
        /// A vent opening with a removable grille. centre = middle of the opening; outward = the direction a person
        /// stands in front of it. The opening itself (duct) is built separately.
        /// </summary>
        public static VentCover Vent(Transform parent, Vector3 centre, Vector3 outward, Vector2 size, VentCover.State state, Material grille)
        {
            var root = new GameObject("Vent opening");
            root.transform.SetParent(parent, false);
            root.transform.position = centre;
            root.transform.rotation = Quaternion.LookRotation(outward, Mathf.Abs(outward.y) > 0.9f ? Vector3.forward : Vector3.up);
            BoxCollider trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(size.x, size.y, 0.3f);
            root.layer = GameLayers.InteractableLayer >= 0 ? GameLayers.InteractableLayer : 0;

            GameObject cover = Part("Vent cover", root.transform, new Vector3(0f, 0f, 0.012f), new Vector3(size.x + 0.04f, size.y + 0.04f, 0.02f), grille, SurfaceType.Metal);
            EditorBuild.SetLayerRecursive(cover, GameLayers.InteractableLayer);
            VentCover v = root.AddComponent<VentCover>();
            v.cover = cover.transform;
            v.state = state;
            Dynamic(root);
            return v;
        }

        public static PhysicalSwitch Switch(Transform parent, Vector3 position, Vector3 facing, string label, Material box, Material handleMat)
        {
            var root = new GameObject("Switch: " + label);
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            root.transform.rotation = Quaternion.LookRotation(facing);
            Part("Switch box", root.transform, new Vector3(0f, 0f, 0.04f), new Vector3(0.25f, 0.35f, 0.08f), box, SurfaceType.Metal);
            var pivot = new GameObject("Handle pivot");
            pivot.transform.SetParent(root.transform, false);
            pivot.transform.localPosition = new Vector3(0f, 0f, 0.09f);
            Part("Handle", pivot.transform, new Vector3(0f, 0.08f, 0f), new Vector3(0.04f, 0.16f, 0.04f), handleMat, SurfaceType.Metal, false);
            PhysicalSwitch s = root.AddComponent<PhysicalSwitch>();
            s.handle = pivot.transform;
            s.label = label;
            EditorBuild.SetLayerRecursive(root, GameLayers.InteractableLayer);
            Dynamic(root);
            return s;
        }
    }
}
