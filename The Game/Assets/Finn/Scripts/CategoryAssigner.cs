using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

public static class CategoryAssigner
{
    public static void AssignCategories(
        IList<RoomProfile> rooms,
        ThemeCategoryTable table,
        System.Random rng)
    {
        if (rooms == null || rooms.Count == 0 || table == null) return;

        var eligible = rooms.Where(r => r != null
                                     && r.Properties != null
                                     && !r.IsEntry
                                     && !r.IsInEntryZone)
                            .ToList();

        // reset runtime category
        foreach (var r in eligible) r.Category = RoomCategory.None;

        // build fast lookups
        var byArch = eligible.GroupBy(r => r.Properties.Archetype)
                             .ToDictionary(g => g.Key, g => g.ToList());

        var quotasByCat = new Dictionary<RoomCategory, (int min, int max)>();
        if (table.Quotas != null)
        {
            foreach (var q in table.Quotas)
            {
                if (!quotasByCat.ContainsKey(q.Category))
                    quotasByCat[q.Category] = (Math.Max(0, q.minTotal), q.maxTotal < 1 ? int.MaxValue : q.maxTotal);
                else
                {
                    var cur = quotasByCat[q.Category];
                    quotasByCat[q.Category] = (Math.Max(cur.min, Math.Max(0, q.minTotal)),
                                               Math.Min(cur.max, q.maxTotal < 1 ? int.MaxValue : q.maxTotal));
                }
            }
        }

        var assignedCounts = new Dictionary<RoomCategory, int>();

        // 1) satisfy MIN quotas
        if (table.Quotas != null)
        {
            foreach (var q in table.Quotas)
            {
                var needed = Math.Max(0, q.minTotal - GetCount(assignedCounts, q.Category));
                while (needed > 0)
                {
                    var candidate = PickRoomForQuota(q.Category, byArch, table, rng);
                    if (candidate == null) break;

                    if ((candidate.AllowedCategories == RoomCategory.None) ||
                        ((candidate.AllowedCategories & q.Category) != 0))
                    {
                        candidate.Category = q.Category;
                        Inc(assignedCounts, q.Category);
                        // remove from available pool so we don't assign twice
                        byArch[candidate.Properties.Archetype].Remove(candidate);
                        needed--;
                    }
                    else
                    {
                        // not actually allowed on this room, remove from pool and continue
                        byArch[candidate.Properties.Archetype].Remove(candidate);
                    }
                }
            }
        }

        // 2) fill remaining, respecting MAX quotas and using per-archetype pools
        foreach (var kv in byArch)
        {
            var arch = kv.Key;
            var list = kv.Value;
            var set = table.Get(arch);
            if (set == null || set.Pool == null || set.Pool.Length == 0) continue;

            foreach (var r in list)
            {
                if (r.Category != RoomCategory.None) continue;

                var allowed = ExpandAllowed(r.AllowedCategories);

                // apply max quota caps: drop any categories that have reached max
                var capped = new HashSet<RoomCategory>();
                foreach (var a in allowed)
                {
                    if (quotasByCat.TryGetValue(a, out var mm)
                        && GetCount(assignedCounts, a) >= mm.max)
                        capped.Add(a);
                }
                if (capped.Count > 0)
                    allowed = allowed.Where(c => !capped.Contains(c)).ToArray();

                if (allowed.Length == 0) continue;

                // filter pool to allowed (explicit) + keep wildcard if present
                var pool = set.Pool;
                var explicitPool = pool.Where(w => w.Category != RoomCategory.None
                                               && allowed.Contains(w.Category)).ToArray();
                bool hasWildcard = pool.Any(w => w.Category == RoomCategory.None);

                RoomCategory chosen;
                if (hasWildcard)
                    chosen = PickWeightedWithWildcard(pool, allowed, quotasByCat, assignedCounts, rng);
                else
                    chosen = PickWeighted(explicitPool, rng);

                if (chosen == RoomCategory.None) continue;

                // final max check (defensive)
                if (quotasByCat.TryGetValue(chosen, out var mx)
                    && GetCount(assignedCounts, chosen) >= mx.max)
                    continue;

                r.Category = chosen;
                Inc(assignedCounts, chosen);
            }
        }
    }

    static RoomProfile PickRoomForQuota(
        RoomCategory c,
        Dictionary<RoomArchetype, List<RoomProfile>> byArch,
        ThemeCategoryTable table,
        System.Random rng)
    {
        var candidates = new List<RoomProfile>();

        // prefer archetypes whose set actually mentions this category
        foreach (var kv in byArch)
        {
            var set = table.Get(kv.Key);
            if (set == null || set.Pool == null) continue;
            bool setMentions = set.Pool.Any(w => w.Category == c || w.Category == RoomCategory.None);
            if (!setMentions) continue;

            foreach (var r in kv.Value)
            {
                if (r.Category != RoomCategory.None) continue;
                if (r.AllowedCategories != RoomCategory.None &&
                    (r.AllowedCategories & c) == 0) continue;
                candidates.Add(r);
            }
        }

        // fallback: any remaining if nothing matched sets
        if (candidates.Count == 0)
        {
            foreach (var kv in byArch)
            {
                foreach (var r in kv.Value)
                {
                    if (r.Category != RoomCategory.None) continue;
                    if (r.AllowedCategories != RoomCategory.None &&
                        (r.AllowedCategories & c) == 0) continue;
                    candidates.Add(r);
                }
            }
        }

        if (candidates.Count == 0) return null;
        return candidates[rng.Next(candidates.Count)];
    }

    static RoomCategory[] ExpandAllowed(RoomCategory mask)
    {
        if (mask == RoomCategory.None)
        {
            return Enum.GetValues(typeof(RoomCategory))
                       .Cast<RoomCategory>()
                       .Where(c => c != RoomCategory.None)
                       .ToArray();
        }
        else
        {
            return Enum.GetValues(typeof(RoomCategory))
                       .Cast<RoomCategory>()
                       .Where(c => c != RoomCategory.None && (mask & c) != 0)
                       .ToArray();
        }
    }

    // original style picker (unchanged behavior)
    static RoomCategory PickWeighted(ThemeCategoryTable.WeightedCategory[] pool, System.Random rng)
    {
        if (pool == null || pool.Length == 0) return RoomCategory.None;
        float total = 0f;
        for (int i = 0; i < pool.Length; i++)
            total += Mathf.Max(0f, pool[i].Weight);
        if (total <= 0f) return pool[pool.Length - 1].Category;

        double roll = rng.NextDouble() * total;
        for (int i = 0; i < pool.Length; i++)
        {
            roll -= Mathf.Max(0f, pool[i].Weight);
            if (roll <= 0.0) return pool[i].Category;
        }
        return pool[pool.Length - 1].Category;
    }

    // wildcard-aware picker using Category=None as “Everything else”
    static RoomCategory PickWeightedWithWildcard(
        ThemeCategoryTable.WeightedCategory[] pool,
        RoomCategory[] allowed,
        Dictionary<RoomCategory, (int min, int max)> quotas,
        Dictionary<RoomCategory, int> counts,
        System.Random rng)
    {
        var allowedSet = new HashSet<RoomCategory>(allowed);

        // explicit, allowed, and not maxed
        var explicitEntries = pool.Where(w =>
                                w.Category != RoomCategory.None
                                && allowedSet.Contains(w.Category)
                                && !ReachedMax(w.Category, quotas, counts))
                                  .ToArray();

        float sumExplicit = explicitEntries.Sum(w => Mathf.Max(0f, w.Weight));
        float wildcardWeight = pool.Where(w => w.Category == RoomCategory.None)
                                   .Select(w => Mathf.Max(0f, w.Weight))
                                   .DefaultIfEmpty(0f)
                                   .First();

        var explicitCats = new HashSet<RoomCategory>(explicitEntries.Select(w => w.Category));
        var fallbackCats = allowedSet.Where(c => c != RoomCategory.None
                                              && !explicitCats.Contains(c)
                                              && !ReachedMax(c, quotas, counts))
                                     .ToArray();

        float total = sumExplicit + wildcardWeight;
        if (total <= 0f)
        {
            return explicitEntries.Length > 0 ? explicitEntries.Last().Category : RoomCategory.None;
        }

        double roll = rng.NextDouble() * total;

        if (roll < sumExplicit)
        {
            foreach (var w in explicitEntries)
            {
                roll -= Mathf.Max(0f, w.Weight);
                if (roll <= 0) return w.Category;
            }
            return explicitEntries.Last().Category;
        }

        if (fallbackCats.Length > 0)
            return fallbackCats[rng.Next(fallbackCats.Length)];

        return explicitEntries.Length > 0 ? explicitEntries.Last().Category : RoomCategory.None;
    }

    static bool ReachedMax(RoomCategory c,
                           Dictionary<RoomCategory, (int min, int max)> quotas,
                           Dictionary<RoomCategory, int> counts)
    {
        if (!quotas.TryGetValue(c, out var mm)) return false;
        return GetCount(counts, c) >= mm.max;
    }

    static int GetCount(Dictionary<RoomCategory, int> counts, RoomCategory c)
    {
        return counts.TryGetValue(c, out var v) ? v : 0;
    }

    static void Inc(Dictionary<RoomCategory, int> counts, RoomCategory c)
    {
        if (counts.TryGetValue(c, out var v)) counts[c] = v + 1;
        else counts[c] = 1;
    }
}
