using System.Collections.Generic;
using UnityEngine;
using AfterHours.Interaction;
using AfterHours.World;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    public static partial class MallBuilder
    {
        /// <summary>Part 5: turns every DoorSlot marker into a working door / shutter / glass door.</summary>
        static void BuildDoors(Transform parent)
        {
            Physics.SyncTransforms();
            int n = 0;
            foreach (DoorSlot slot in Object.FindObjectsByType<DoorSlot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                int h = MallLayout.Hash((int)(slot.transform.position.x * 10f), (int)(slot.transform.position.z * 10f), (int)slot.kind, 31);
                bool hasPanel = slot.GetComponent<Collider>() != null;

                switch (slot.kind)
                {
                    case DoorKind.Lift:
                        continue; // lift landing doors: Part 12
                    case DoorKind.Shutter:
                        if (hasPanel) BuildShutterFromSlot(slot);
                        continue;
                    case DoorKind.Glass:
                        if (!hasPanel) BuildGlassDoubleDoor(parent, slot);
                        continue; // a glass panel slot is a locked glass shopfront
                    case DoorKind.Toilet:
                        if (slot.note.StartsWith("Passage from concourse")) continue; // public passage: no door
                        break;
                }
                if (hasPanel) continue;

                Vector3 p = slot.transform.position;
                Vector3 axis = InteractiveFactory.DetectWallAxis(p, slot.width);
                Material leafMat;
                SurfaceType surface;
                DoorWeight weight;
                string lockId;
                bool forceable = true, faulty = false;
                switch (slot.kind)
                {
                    case DoorKind.Stairwell: leafMat = plantMat; surface = SurfaceType.Metal; weight = DoorWeight.Heavy; lockId = "key_stairwell"; break;
                    case DoorKind.Fire: leafMat = plantMat; surface = SurfaceType.Metal; weight = DoorWeight.VeryHeavy; lockId = "key_exit"; forceable = false; break;
                    case DoorKind.Maintenance: leafMat = plantMat; surface = SurfaceType.Metal; weight = DoorWeight.Heavy; lockId = "key_maintenance"; break;
                    case DoorKind.Security: leafMat = plantMat; surface = SurfaceType.Metal; weight = DoorWeight.Heavy; lockId = "key_security"; break;
                    case DoorKind.Restricted: leafMat = woodMat; surface = SurfaceType.Wood; weight = DoorWeight.Heavy; lockId = "card_restricted"; break;
                    case DoorKind.Staff: leafMat = woodMat; surface = SurfaceType.Wood; weight = DoorWeight.Normal; lockId = "card_staff"; break;
                    case DoorKind.ShopBack: leafMat = plantMat; surface = SurfaceType.Metal; weight = DoorWeight.Normal; lockId = "key_shop"; break;
                    case DoorKind.Storage: leafMat = woodMat; surface = SurfaceType.Wood; weight = DoorWeight.Light; lockId = "key_storage"; break;
                    case DoorKind.Hidden: leafMat = wallPaint; surface = SurfaceType.Wood; weight = DoorWeight.Heavy; lockId = ""; faulty = true; break;
                    default: leafMat = woodMat; surface = SurfaceType.Wood; weight = DoorWeight.Normal; lockId = ""; break;
                }
                // Escape-route doors are never forced open by anything.
                if (slot.note.Contains("escape route")) { weight = DoorWeight.VeryHeavy; forceable = false; }
                if (slot.kind == DoorKind.Staff || slot.kind == DoorKind.Storage || slot.kind == DoorKind.Maintenance) faulty |= h % 9 == 0;
                bool squeaky = h % 5 == 0;

                InteractiveFactory.HingedDoor(parent, "Door (" + slot.kind + ")", p, axis, slot.width, slot.height, leafMat, surface, metal,
                    slot.kind, weight, slot.startsLocked, slot.startsLocked ? lockId : "", squeaky, faulty, forceable);
                n++;
            }
            Debug.Log("[AfterHours] Doors built: " + n);
        }

        static void BuildShutterFromSlot(DoorSlot slot)
        {
            Bounds b = slot.GetComponent<Collider>().bounds;
            float floorY = MallLayout.FloorY(MallZones.FloorAt(b.center.y));
            if (b.min.y < -0.6f && floorY > -1f) floorY = MallLayout.CarParkY;
            float travel = Mathf.Max(1f, b.size.y - 0.3f);
            bool jammed = slot.note.Contains("jammed");
            float startOpen = Mathf.Clamp01((b.min.y - floorY) / travel);
            InteractiveFactory.MakeShutter(slot.gameObject, slot.startsLocked && !jammed, jammed, startOpen, "key_shop");
        }

        static void BuildGlassDoubleDoor(Transform parent, DoorSlot slot)
        {
            Vector3 p = slot.transform.position;
            Vector3 axis = InteractiveFactory.DetectWallAxis(p, slot.width);
            float half = slot.width * 0.5f;
            InteractiveFactory.HingedDoor(parent, "Glass door L", p - axis * (half * 0.5f), -axis, half, slot.height, glassMat, SurfaceType.Glass, metal,
                DoorKind.Glass, DoorWeight.Light, false, "", false, false, true, 0.02f);
            InteractiveFactory.HingedDoor(parent, "Glass door R", p + axis * (half * 0.5f), axis, half, slot.height, glassMat, SurfaceType.Glass, metal,
                DoorKind.Glass, DoorWeight.Light, false, "", false, false, true, 0.02f);
        }

        /// <summary>Lockers and cupboards in staff rooms, security, maintenance, storage and offices; drawers in desks.</summary>
        static void BuildLockers(Transform parent)
        {
            float o = MallLayout.OuterWall, d = MallLayout.QuadrantDivider;
            foreach (MallFloor f in MallLayout.ShopFloors)
            {
                float y = MallLayout.FloorY(f);
                foreach (var (sx, sz) in MallLayout.Quadrants)
                {
                    PlaceStorageRow(parent, MallLayout.QuadrantRoomB(f, sx, sz), y, sx, sz, d + 0.1f + 0.31f, sz);   // room B, against the divider
                    PlaceStorageRow(parent, MallLayout.QuadrantRoomA(f, sx, sz), y, sx, sz, d - 0.1f - 0.31f, -sz);  // room A, other side
                }
            }
        }

        static void PlaceStorageRow(Transform parent, MallLayout.QuadrantRoom room, float y, int sx, int sz, float zAbs, int faceZ)
        {
            int lockers = 0, cupboards = 0;
            switch (room.type)
            {
                case FacilityType.StaffOnly: lockers = room.label.Contains("Locker") ? 10 : 4; break;
                case FacilityType.Security: lockers = 4; cupboards = 1; break;
                case FacilityType.Maintenance: lockers = 4; cupboards = 2; break;
                case FacilityType.Storage: cupboards = 3; break;
                case FacilityType.Management:
                case FacilityType.Restricted: cupboards = 3; break;
                case FacilityType.LoadingBay: lockers = 3; break;
                default: return;
            }
            Vector3 facing = new Vector3(0f, 0f, faceZ);
            float x = 26f;
            for (int i = 0; i < lockers && x < 32.2f; i++, x += 0.64f)
            {
                InteractiveFactory.Locker(parent, new Vector3(sx * x, y, sz * zAbs), facing, metal, plantMat, metal);
            }
            if (lockers > 0) x += 0.3f;
            for (int i = 0; i < cupboards && x < 32f; i++, x += 1.14f)
            {
                InteractiveFactory.Cupboard(parent, new Vector3(sx * (x + 0.25f), y, sz * zAbs), facing, woodMat, woodMat, metal);
            }
        }
    }
}
