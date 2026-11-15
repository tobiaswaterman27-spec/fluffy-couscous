using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using AfterHours.World;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// Builds the fixed physical mall directory next to the information desk, generated from the same
    /// MallLayout data as the building itself, so it always matches the real layout. It cannot be picked
    /// up, moved or zoomed; the player walks up to it to read it.
    /// </summary>
    public static class DirectoryBuilder
    {
        const string MatFolder = EditorBuild.Root + "/Materials/Directory";
        const float BoardW = 4.8f, BoardH = 2.4f, BoardBottom = 0.35f;
        static readonly Vector2 Centre = new Vector2(-6f, -12f);
        const float FrontZ = -12.1f;

        static Material frame, panel, region, voidMat, here, textBg;
        static readonly Dictionary<UnitCategory, Material> unitMats = new Dictionary<UnitCategory, Material>();
        static readonly Dictionary<FacilityType, Material> facilityMats = new Dictionary<FacilityType, Material>();
        static float layerOffset;

        public static void Build(Transform parent)
        {
            frame = EditorBuild.Material(MatFolder, "Frame", new Color(0.12f, 0.12f, 0.13f), 0.5f, 0.6f);
            panel = EditorBuild.Material(MatFolder, "Panel", new Color(0.86f, 0.85f, 0.8f), 0.2f, 0f, new Color(0.15f, 0.15f, 0.14f));
            region = EditorBuild.Material(MatFolder, "Region", new Color(0.7f, 0.7f, 0.68f), 0.2f);
            voidMat = EditorBuild.Material(MatFolder, "Void", new Color(0.25f, 0.3f, 0.35f), 0.2f);
            here = EditorBuild.Material(MatFolder, "YouAreHere", new Color(0.85f, 0.1f, 0.1f), 0.3f, 0f, new Color(0.6f, 0.05f, 0.05f));
            textBg = EditorBuild.Material(MatFolder, "Header", new Color(0.2f, 0.25f, 0.35f), 0.3f);

            // Stand (solid, collidable).
            EditorBuild.Box("Directory stand", parent, new Vector3(Centre.x, (BoardBottom + BoardH + 0.1f) * 0.5f, Centre.y),
                new Vector3(BoardW + 0.2f, BoardBottom + BoardH + 0.1f, 0.2f), frame, SurfaceType.Metal);

            Transform face = new GameObject("Directory face").transform;
            face.SetParent(parent, false);

            float top = BoardBottom + BoardH;
            Flat(face, "Title bar", Centre.x - BoardW * 0.5f, top - 0.16f, Centre.x + BoardW * 0.5f, top, textBg);
            Text(face, "CENTRE DIRECTORY", new Vector3(Centre.x - BoardW * 0.5f + 0.05f, top - 0.02f, FrontZ - 0.02f), 0.11f, TextAnchor.UpperLeft, Color.white);
            Text(face, "T Toilets    L Lifts    S Stairs    E Escalators    i Information    X Exit    R Restricted    Red dot = You are here",
                new Vector3(Centre.x + BoardW * 0.5f - 0.05f, top - 0.045f, FrontZ - 0.02f), 0.045f, TextAnchor.UpperRight, Color.white);

            float panelW = (BoardW - 0.1f) * 0.5f;
            float panelH = (BoardH - 0.26f) * 0.5f;
            var floors = new[] { MallFloor.Ground, MallFloor.Level1, MallFloor.Level2, MallFloor.CarPark };
            for (int i = 0; i < floors.Length; i++)
            {
                int col = i % 2, row = i / 2;
                float x0 = Centre.x - BoardW * 0.5f + 0.03f + col * (panelW + 0.04f);
                float y1 = top - 0.2f - row * (panelH + 0.04f);
                DrawPanel(face, floors[i], x0, y1 - panelH, x0 + panelW, y1);
            }
        }

        static void DrawPanel(Transform face, MallFloor f, float x0, float y0, float x1, float y1)
        {
            layerOffset = 0f;
            Flat(face, "Panel " + f, x0, y0, x1, y1, panel);
            Next();
            Text(face, MallLayout.FloorName(f), new Vector3(x0 + 0.03f, y1 - 0.01f, Z()), 0.06f, TextAnchor.UpperLeft, new Color(0.1f, 0.1f, 0.15f));

            // Map on the left of the panel.
            float mapSize = Mathf.Min(y1 - y0 - 0.1f, (x1 - x0) * 0.46f);
            float scale = mapSize / 260f;
            Vector2 mapCentre = new Vector2(x0 + 0.03f + mapSize * 0.5f, y0 + 0.02f + mapSize * 0.5f);

            Vector2 P(float wx, float wz) => new Vector2(mapCentre.x + wx * scale, mapCentre.y + wz * scale);
            void MapRect(string name, AreaRect r, Material m)
            {
                Vector2 a = P(r.minX, r.minZ), b = P(r.maxX, r.maxZ);
                Flat(face, name, a.x, a.y, b.x, b.y, m);
            }

            if (f == MallFloor.CarPark)
            {
                MapRect("Car park", new AreaRect(-100f, -100f, 100f, 100f), region);
                MapRect("Service tunnel", new AreaRect(-128f, -20f, -100f, -17f), region);
            }
            else
            {
                foreach (AreaRect r in MallLayout.FloorRegions(f)) MapRect("Region", r, region);
                if (f != MallFloor.Ground) MapRect("Atrium void", new AreaRect(-10f, -10f, 10f, 10f), voidMat);
                else MapRect("Atrium", new AreaRect(-10f, -10f, 10f, 10f), voidMat);
            }
            Next();

            var list = new List<string>();
            foreach (MallUnit u in MallLayout.Units)
            {
                if (u.floor != f) continue;
                MapRect(u.code, u.rect.Expanded(-0.35f), UnitMat(u.category));
                list.Add(u.code + "  " + u.name);
            }
            Next();

            foreach (MallFacility fac in MallLayout.Facilities)
            {
                if (fac.floor != f) continue;
                string letter = Letter(fac.type, fac.restricted);
                if (letter == null) continue;
                MapRect(fac.label, fac.rect, FacilityMat(fac.type, fac.restricted));
                Vector2 c = P(fac.rect.Center.x, fac.rect.Center.y);
                Text(face, letter, new Vector3(c.x, c.y, Z() - 0.002f), 0.03f, TextAnchor.MiddleCenter, Color.white);
                if (f == MallFloor.CarPark) list.Add(letter + "  " + fac.label);
            }
            Next();

            if (f == MallFloor.Ground)
            {
                Vector2 h = P(-6f, -12f);
                Flat(face, "You are here", h.x - 0.012f, h.y - 0.012f, h.x + 0.012f, h.y + 0.012f, here);
            }

            // Store list on the right: two columns.
            list.Sort(System.StringComparer.Ordinal);
            float listX = mapCentre.x + mapSize * 0.5f + 0.04f;
            float colW = (x1 - listX - 0.02f) * 0.5f;
            int perCol = Mathf.CeilToInt(list.Count / 2f);
            float lineH = Mathf.Min(0.03f, (y1 - y0 - 0.1f) / Mathf.Max(1, perCol));
            for (int c = 0; c < 2; c++)
            {
                var sb = new StringBuilder();
                for (int i = c * perCol; i < Mathf.Min(list.Count, (c + 1) * perCol); i++) sb.AppendLine(list[i]);
                Text(face, sb.ToString(), new Vector3(listX + c * colW, y1 - 0.08f, Z()), lineH, TextAnchor.UpperLeft, new Color(0.08f, 0.08f, 0.1f));
            }
        }

        static float Z() => FrontZ - 0.004f - layerOffset;
        static void Next() => layerOffset += 0.003f;

        /// <summary>A flat coloured rectangle on the board face (no collider).</summary>
        static void Flat(Transform parent, string name, float x0, float y0, float x1, float y1, Material m)
        {
            if (Mathf.Abs(x1 - x0) < 0.0005f || Mathf.Abs(y1 - y0) < 0.0005f) return;
            float z = Z();
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, z + 0.001f);
            go.transform.localScale = new Vector3(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0), 0.002f);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = m;
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.isStatic = true;
            ArtLibrary.MarkDetail(m);
        }

        static void Text(Transform parent, string text, Vector3 pos, float lineHeight, TextAnchor anchor, Color color)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.fontSize = 64;
            tm.characterSize = lineHeight * 10f / tm.fontSize; // TextMesh: ~fontSize * characterSize / 10 metres per line
            tm.anchor = anchor;
            tm.alignment = anchor == TextAnchor.UpperRight ? TextAlignment.Right : TextAlignment.Left;
            tm.color = color;
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
            {
                tm.font = font;
                go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
        }

        static Material UnitMat(UnitCategory c)
        {
            if (unitMats.TryGetValue(c, out Material m) && m != null) return m;
            Color col = c == UnitCategory.Vacant ? new Color(0.55f, 0.55f, 0.55f) : Color.HSVToRGB(((int)c * 0.137f) % 1f, 0.35f, 0.75f);
            m = EditorBuild.Material(MatFolder, "Unit_" + c, col, 0.2f);
            unitMats[c] = m;
            return m;
        }

        static Material FacilityMat(FacilityType t, bool restricted)
        {
            if (restricted) t = FacilityType.Restricted;
            if (facilityMats.TryGetValue(t, out Material m) && m != null) return m;
            Color col;
            switch (t)
            {
                case FacilityType.Toilets: col = new Color(0.2f, 0.4f, 0.75f); break;
                case FacilityType.Restricted: col = new Color(0.45f, 0.2f, 0.2f); break;
                case FacilityType.MainEntrance:
                case FacilityType.EmergencyExit:
                case FacilityType.CarParkExit: col = new Color(0.15f, 0.55f, 0.25f); break;
                default: col = new Color(0.25f, 0.25f, 0.3f); break;
            }
            m = EditorBuild.Material(MatFolder, "Facility_" + t, col, 0.2f);
            facilityMats[t] = m;
            return m;
        }

        static string Letter(FacilityType t, bool restricted)
        {
            switch (t)
            {
                case FacilityType.Toilets: return "T";
                case FacilityType.Lift: return "L";
                case FacilityType.ServiceLift: return "L";
                case FacilityType.Stairs:
                case FacilityType.EmergencyStairs: return "S";
                case FacilityType.Escalator: return "E";
                case FacilityType.InfoDesk: return "i";
                case FacilityType.MainEntrance:
                case FacilityType.EmergencyExit:
                case FacilityType.CarParkExit: return "X";
                case FacilityType.Fountain:
                case FacilityType.Directory:
                case FacilityType.FoodCourt:
                case FacilityType.Cinema: return null;
                default: return restricted ? "R" : null;
            }
        }
    }
}
