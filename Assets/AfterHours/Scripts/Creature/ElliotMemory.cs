using System.Collections.Generic;
using UnityEngine;
using AfterHours.Interaction;
using AfterHours.Player;

namespace AfterHours.Creature
{
    /// <summary>
    /// What Elliot remembers: where it saw which player (and which way they were going), places where noise keeps
    /// coming from, places it has been (its territory), doors it could not get through, and hiding places players
    /// used (Part 11). Memories fade: useful, but not perfect. Each creature has its own memory.
    /// </summary>
    public class ElliotMemory : MonoBehaviour
    {
        public struct Sighting
        {
            public PlayerController who;
            public Vector3 position;
            public Vector3 velocity;
            public float time;
        }

        public float sightingHalfLife = 90f;
        public float noiseHalfLife = 300f;

        readonly List<Sighting> sightings = new List<Sighting>();
        readonly Dictionary<Vector2Int, float> noiseHeat = new Dictionary<Vector2Int, float>();
        readonly Dictionary<Vector2Int, float> noiseTime = new Dictionary<Vector2Int, float>();
        readonly List<Vector3> territory = new List<Vector3>();
        readonly Dictionary<Door, float> blockedDoors = new Dictionary<Door, float>();
        readonly List<(Vector3 pos, float time)> hidingSpots = new List<(Vector3, float)>();

        public IReadOnlyList<Vector3> Territory => territory;

        public void RememberSighting(PlayerController who, Vector3 pos, Vector3 vel)
        {
            sightings.Add(new Sighting { who = who, position = pos, velocity = vel, time = Time.time });
            if (sightings.Count > 60) sightings.RemoveAt(0);
            RememberPlace(pos);
        }

        public bool LastSighting(PlayerController who, out Sighting s)
        {
            for (int i = sightings.Count - 1; i >= 0; i--)
            {
                if (who == null || sightings[i].who == who) { s = sightings[i]; return true; }
            }
            s = default;
            return false;
        }

        public float Confidence(float time, float halfLife) => Mathf.Pow(0.5f, (Time.time - time) / halfLife);

        static Vector2Int Cell(Vector3 p) => new Vector2Int(Mathf.FloorToInt(p.x / 8f), Mathf.FloorToInt(p.z / 8f) * 1000 + Mathf.FloorToInt(p.y / 5f));

        public void RememberNoise(Vector3 pos, float strength)
        {
            Vector2Int c = Cell(pos);
            float heat = NoiseHeat(c) + strength;
            noiseHeat[c] = heat;
            noiseTime[c] = Time.time;
        }

        float NoiseHeat(Vector2Int c)
        {
            if (!noiseHeat.TryGetValue(c, out float h)) return 0f;
            return h * Confidence(noiseTime[c], noiseHalfLife);
        }

        public float NoiseHeatAt(Vector3 pos) => NoiseHeat(Cell(pos));

        public void RememberPlace(Vector3 pos)
        {
            foreach (Vector3 t in territory) if ((t - pos).sqrMagnitude < 15f * 15f) return;
            territory.Add(pos);
            if (territory.Count > 40) territory.RemoveAt(0);
        }

        public void RememberHidingSpot(Vector3 pos)
        {
            hidingSpots.Add((pos, Time.time));
            if (hidingSpots.Count > 20) hidingSpots.RemoveAt(0);
        }

        /// <summary>Remembered hiding places near a point, most confident first.</summary>
        public List<Vector3> HidingSpotsNear(Vector3 pos, float radius)
        {
            var result = new List<Vector3>();
            for (int i = hidingSpots.Count - 1; i >= 0; i--)
            {
                if (Vector3.Distance(hidingSpots[i].pos, pos) < radius && Random.value < Confidence(hidingSpots[i].time, 600f) + 0.2f)
                {
                    result.Add(hidingSpots[i].pos);
                }
            }
            return result;
        }

        public void MarkBlocked(Door d) { if (d != null) blockedDoors[d] = Time.time; }
        public bool IsBlocked(Door d) => d != null && blockedDoors.TryGetValue(d, out float t) && Time.time - t < 120f;

        public void Forget()
        {
            sightings.Clear();
            noiseHeat.Clear();
            noiseTime.Clear();
            territory.Clear();
            blockedDoors.Clear();
            hidingSpots.Clear();
        }
    }
}
