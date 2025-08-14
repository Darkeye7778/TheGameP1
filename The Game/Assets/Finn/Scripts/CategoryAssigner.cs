using System.Collections.Generic;
using UnityEngine;

public static class CategoryAssigner
{
    public static void AssignCategories(
        List<RoomProfile> rooms,
        ThemeCategoryTable table,
        System.Random rng)
    {
        if (rooms == null || table == null) return;

        // Build per-archetype lists we can fill
        var byArch = new Dictionary<RoomArchetype, List<RoomProfile>>();
        foreach (var r in rooms)
        {
            if (!r || r.IsEntry || r.IsInEntryZone) continue; // keep entry neutral
            var arch = r.Properties.Archetype;
            if (!byArch.TryGetValue(arch, out var list)) { list = new List<RoomProfile>(); byArch[arch] = list; }
            list.Add(r);
        }

        // Seed quotas first (ensure required categories exist)
        foreach (var q in table.Quotas)
        {
            if (q.minTotal <= 0) continue;
            for (int i = 0; i < q.minTotal; i++)
            {
                var room = PickRoomForCategory(q.Category, byArch, table, rng);
                if (room == null) break;
                room.Category = q.Category;
            }
        }

        // Fill the rest by archetype weights
        foreach (var kv in byArch)
        {
            var arch = kv.Key;
            var list = kv.Value;
            var set = table.Get(arch);
            if (set == null || set.Pool == null || set.Pool.Length == 0) continue;

            // Shuffle for variety
            Shuffle(list, rng);

            foreach (var r in list)
            {
                if (r.Category != RoomCategory.None) continue; // already filled by quota
                r.Category = PickWeighted(set.Pool, rng);
            }
        }
    }

    // — helpers —
    static void Shuffle<T>(IList<T> a, System.Random rng) { for (int i = a.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (a[i], a[j]) = (a[j], a[i]); } }

    static RoomProfile PickRoomForCategory(RoomCategory c,
        Dictionary<RoomArchetype, List<RoomProfile>> byArch,
        ThemeCategoryTable table, System.Random rng)
    {
        // choose an archetype that *can* host this category (based on weights > 0)
        var candidates = new List<RoomProfile>();
        foreach (var kv in byArch)
        {
            var set = table.Get(kv.Key);
            if (set == null) continue;
            foreach (var w in set.Pool)
                if (w.Category == c && w.Weight > 0f)
                    candidates.AddRange(kv.Value);
        }
        if (candidates.Count == 0) return null;
        return candidates[rng.Next(candidates.Count)];
    }

    static RoomCategory PickWeighted(ThemeCategoryTable.WeightedCategory[] pool, System.Random rng)
    {
        float sum = 0; foreach (var w in pool) sum += Mathf.Max(0, w.Weight);
        if (sum <= 0) return RoomCategory.None;
        float r = (float)(rng.NextDouble() * sum), c = 0;
        foreach (var w in pool) { c += Mathf.Max(0, w.Weight); if (r <= c) return w.Category; }
        return pool[pool.Length - 1].Category;
    }
}