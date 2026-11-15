using System.Collections.Generic;
using UnityEngine;
using AfterHours.Audio;
using AfterHours.Interaction;

namespace AfterHours.Items
{
    /// <summary>
    /// A physical item lying in the world. Pick it up (hold E), carry one at a time, drop or throw it.
    /// Items stay exactly where they are left. Every item has a unique id (saves, Elliot's memory) and remembers
    /// where it last came to rest, so it can never be lost for good (falling out of the world returns it there).
    /// Elliot takes / moves items through the same Attach / Release calls (Part 9).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ItemPickup : Interactable, AfterHours.Puzzles.IResettable
    {
        Quaternion homeRotation;

        /// <summary>Run reset: back where it started (also out of anyone's hands).</summary>
        public void ResetState()
        {
            if (Holder != null)
            {
                AfterHours.Items.PlayerInventory inv = Holder.GetComponent<AfterHours.Items.PlayerInventory>();
                if (inv != null && inv.Held == this) inv.TakeHeld();
            }
            Release(HomePosition, Vector3.zero);
            transform.rotation = homeRotation;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            gameObject.SetActive(true);
        }

        public string itemId;
        public string instanceId;

        static readonly List<ItemPickup> all = new List<ItemPickup>();
        public static IReadOnlyList<ItemPickup> All => all;

        public ItemDefinition Definition => ItemCatalog.Get(itemId);
        public string DisplayName => Definition != null ? Definition.displayName : itemId;
        public GameObject Holder { get; private set; }
        public Vector3 LastRestPosition { get; private set; }
        public Vector3 HomePosition { get; private set; }
        public bool IsHeld => Holder != null;

        Rigidbody body;
        Collider[] colliders;
        Renderer[] renderers;
        Transform anchor;
        float attachT = -1f;
        Vector3 attachFromPos;
        Quaternion attachFromRot;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            colliders = GetComponentsInChildren<Collider>(true);
            renderers = GetComponentsInChildren<Renderer>(true);
            HomePosition = LastRestPosition = transform.position;
            homeRotation = transform.rotation;
        }

        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        public override float HoldTime => 0.2f;

        public override string GetPrompt(IInteractor who)
        {
            if (IsHeld) return null;
            PlayerInventory inv = who.Actor != null ? who.Actor.GetComponent<PlayerInventory>() : null;
            if (inv != null && inv.Held != null) return "Swap for " + DisplayName;
            return "Pick up " + DisplayName;
        }

        public override void Interact(IInteractor who)
        {
            PlayerInventory inv = who.Actor != null ? who.Actor.GetComponent<PlayerInventory>() : null;
            if (inv != null) inv.PickUp(this);
        }

        /// <summary>Picked up: the item moves smoothly into the holder's hand and stops colliding.</summary>
        public void AttachTo(GameObject holder, Transform handAnchor)
        {
            Holder = holder;
            anchor = handAnchor;
            body.isKinematic = true;
            body.detectCollisions = false;
            foreach (Collider c in colliders) c.enabled = false;
            foreach (Renderer r in renderers) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            attachFromPos = transform.position;
            attachFromRot = transform.rotation;
            attachT = 0f;
            transform.SetParent(null, true);
        }

        /// <summary>LAN co-op client: items are simulated by the host; this copy just follows.</summary>
        public static bool NetDriven;
        Vector3 netPos;
        Quaternion netRot;
        bool hasNetPose;

        public void NetSetPose(Vector3 position, Quaternion rotation)
        {
            if (IsHeld) return;
            body.isKinematic = true;
            netPos = position;
            netRot = rotation;
            if (!hasNetPose || (transform.position - position).sqrMagnitude > 9f) transform.SetPositionAndRotation(position, rotation);
            hasNetPose = true;
        }

        void Update()
        {
            if (!NetDriven || !hasNetPose || IsHeld) return;
            float k = 1f - Mathf.Exp(-15f * Time.deltaTime);
            transform.SetPositionAndRotation(Vector3.Lerp(transform.position, netPos, k), Quaternion.Slerp(transform.rotation, netRot, k));
        }

        /// <summary>Loading a save: lying exactly here, at rest.</summary>
        public void PlaceAt(Vector3 position, Quaternion rotation)
        {
            Release(position, Vector3.zero);
            transform.rotation = rotation;
            body.position = position;
            body.rotation = rotation;
            body.angularVelocity = Vector3.zero;
        }

        /// <summary>Let go at the current position with a velocity (0 = just let it fall).</summary>
        public void Release(Vector3 position, Vector3 velocity)
        {
            Holder = null;
            anchor = null;
            attachT = -1f;
            transform.SetParent(null, true);
            transform.position = position;
            foreach (Collider c in colliders) c.enabled = true;
            foreach (Renderer r in renderers) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            body.detectCollisions = true;
            body.isKinematic = NetDriven;
            if (NetDriven) { netPos = position; netRot = transform.rotation; hasNetPose = true; return; }
            body.linearVelocity = velocity;
            body.angularVelocity = Random.insideUnitSphere * (velocity.magnitude * 0.8f);
        }

        void LateUpdate()
        {
            if (anchor == null) return;
            if (attachT >= 0f)
            {
                attachT += Time.deltaTime / 0.25f;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(attachT));
                transform.SetPositionAndRotation(Vector3.Lerp(attachFromPos, anchor.position, t), Quaternion.Slerp(attachFromRot, anchor.rotation, t));
                if (attachT >= 1f) attachT = -1f;
            }
            else
            {
                transform.SetPositionAndRotation(anchor.position, anchor.rotation);
            }
        }

        void FixedUpdate()
        {
            if (IsHeld) return;
            if (transform.position.y < -60f)
            {
                // Fell out of the world (should never happen): bring it back to where it last rested.
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.position = LastRestPosition + Vector3.up * 0.2f;
                return;
            }
            if (body.IsSleeping() || body.linearVelocity.sqrMagnitude < 0.0004f) LastRestPosition = transform.position;
        }
    }
}
