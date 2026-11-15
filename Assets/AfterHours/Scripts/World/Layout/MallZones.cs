using System.Collections.Generic;
using UnityEngine;

namespace AfterHours.World.Layout
{
    public enum ZoneKind
    {
        Outside, Atrium, Concourse, Shop, Passage, ServiceCorridor, Anchor, BackOfHouse, Toilets,
        Stairwell, LiftShaft, CarPark, ServiceTunnel, ExitRamp, Roof, Cinema, FoodCourt, EntranceHall
    }

    public struct ZoneInfo
    {
        public ZoneKind kind;
        public MallFloor floor;
        public WingId wing;
        public float u, v;      // wing coordinates (only meaningful inside a wing)
        public MallUnit unit;   // shop / anchor at this point, if any
        public bool restricted;
    }

    /// <summary>
    /// Answers "what kind of place is this point in?" from the fixed master plan.
    /// Used for materials, detail placement, ambience and reverb now; Elliot's AI and sound
    /// propagation use it later (Parts 9-10).
    /// </summary>
    public static class MallZones
    {
        static Dictionary<int, List<MallUnit>> unitsByRow;

        public static MallFloor FloorAt(float y)
        {
            if (y < -0.6f) return MallFloor.CarPark;
            if (y >= MallLayout.RoofY - 0.6f) return MallFloor.Roof;
            int i = Mathf.Clamp(Mathf.FloorToInt((y + 0.6f) / MallLayout.FloorHeight), 0, 2);
            return (MallFloor)i;
        }

        public static ZoneInfo Classify(Vector3 p)
        {
            var info = new ZoneInfo { floor = FloorAt(p.y), kind = ZoneKind.Outside };
            float ax = Mathf.Abs(p.x), az = Mathf.Abs(p.z);

            if (info.floor == MallFloor.CarPark)
            {
                if (p.x < -MallLayout.CarParkHalf) { info.kind = ZoneKind.ServiceTunnel; info.restricted = true; return info; }
                if (MallLayout.CarParkExitRamp.Contains(p.x, p.z)) { info.kind = ZoneKind.ExitRamp; return info; }
                foreach (var (sx, sz) in MallLayout.Quadrants)
                {
                    if (MallLayout.CoreRect(sx, sz).Contains(p.x, p.z)) { info.kind = ZoneKind.Stairwell; return info; }
                }
                if (ax <= MallLayout.CarParkHalf && az <= MallLayout.CarParkHalf) info.kind = ZoneKind.CarPark;
                return info;
            }
            if (info.floor == MallFloor.Roof)
            {
                info.kind = ZoneKind.Roof;
                return info;
            }

            foreach (var (sx, sz) in MallLayout.Quadrants)
            {
                if (MallLayout.CoreRect(sx, sz).Contains(p.x, p.z)) { info.kind = ZoneKind.Stairwell; return info; }
            }
            foreach (AreaRect lift in MallLayout.LiftShafts())
            {
                if (lift.Contains(p.x, p.z)) { info.kind = ZoneKind.LiftShaft; return info; }
            }

            // Back-of-house blocks between the wings.
            if (ax >= MallLayout.OuterWall && az >= MallLayout.OuterWall && ax <= MallLayout.QuadrantOuter && az <= MallLayout.QuadrantOuter)
            {
                int sx = p.x > 0 ? 1 : -1, sz = p.z > 0 ? 1 : -1;
                MallLayout.QuadrantRoom room = az < MallLayout.QuadrantDivider
                    ? MallLayout.QuadrantRoomA(info.floor, sx, sz)
                    : MallLayout.QuadrantRoomB(info.floor, sx, sz);
                info.kind = room.type == FacilityType.Toilets ? ZoneKind.Toilets : ZoneKind.BackOfHouse;
                info.restricted = room.restricted;
                return info;
            }

            if (ax <= MallLayout.AtriumHalf && az <= MallLayout.AtriumHalf)
            {
                info.kind = ZoneKind.Atrium;
                return info;
            }

            // Which wing?
            if (az > MallLayout.WingStart && ax <= MallLayout.OuterWall)
            {
                info.wing = p.z > 0 ? WingId.North : WingId.South;
            }
            else if (ax > MallLayout.WingStart && az <= MallLayout.OuterWall)
            {
                info.wing = p.x > 0 ? WingId.East : WingId.West;
            }
            else
            {
                return info; // outside the building
            }

            Vector3 fwd = MallLayout.WingForward(info.wing), right = MallLayout.WingRight(info.wing);
            info.u = Vector3.Dot(new Vector3(p.x, 0f, p.z), fwd);
            info.v = Vector3.Dot(new Vector3(p.x, 0f, p.z), right);
            float av = Mathf.Abs(info.v);
            if (av > MallLayout.OuterWall) return info;

            if (info.u >= MallLayout.WingEnd)
            {
                if (info.u > MallLayout.AnchorEndFor(info.floor, info.wing)) return info;
                info.unit = FindUnit(info.floor, info.wing, 0, info.u);
                if (info.floor == MallFloor.Level2 && info.wing == WingId.East) info.kind = ZoneKind.Cinema;
                else if (info.floor == MallFloor.Level2 && info.wing == WingId.West) info.kind = ZoneKind.FoodCourt;
                else if (info.floor == MallFloor.Ground && info.wing == WingId.South) info.kind = ZoneKind.EntranceHall;
                else if (info.floor == MallFloor.Ground && info.wing == WingId.West && info.v < -2.1f) info.kind = ZoneKind.Toilets;
                else info.kind = ZoneKind.Anchor;
                return info;
            }

            if (av <= MallLayout.ConcourseHalf) { info.kind = ZoneKind.Concourse; return info; }
            if (av <= MallLayout.ShopBack)
            {
                info.unit = FindUnit(info.floor, info.wing, info.v > 0 ? 1 : -1, info.u);
                info.kind = info.unit != null ? ZoneKind.Shop : ZoneKind.Passage;
                return info;
            }
            info.kind = ZoneKind.ServiceCorridor;
            info.restricted = true;
            return info;
        }

        static MallUnit FindUnit(MallFloor f, WingId w, int side, float u)
        {
            if (unitsByRow == null)
            {
                unitsByRow = new Dictionary<int, List<MallUnit>>();
                foreach (MallUnit unit in MallLayout.Units)
                {
                    int k = Key(unit.floor, unit.wing, unit.side);
                    if (!unitsByRow.TryGetValue(k, out var list)) unitsByRow[k] = list = new List<MallUnit>();
                    list.Add(unit);
                }
            }
            if (!unitsByRow.TryGetValue(Key(f, w, side), out var row)) return null;
            foreach (MallUnit unit in row)
            {
                if (u >= unit.u0 && u <= unit.u1) return unit;
            }
            return null;
        }

        static int Key(MallFloor f, WingId w, int side) => ((int)f + 1) * 100 + (int)w * 10 + side + 1;
    }
}
