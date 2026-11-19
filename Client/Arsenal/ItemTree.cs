#nullable enable
using System;
using System.Collections.Generic;

namespace Perun.Client.Arsenal;

internal static class ItemTree
{
    public static List<List<T>> ChildSubtrees<T>(IReadOnlyList<T> items, Func<T, string> id, Func<T, string?> parent)
    {
        List<List<T>> result = new();
        if (items == null || items.Count == 0)
        {
            return result;
        }

        Dictionary<string, List<T>> children = new();
        foreach (T item in items)
        {
            string? parentId = parent(item);
            if (string.IsNullOrEmpty(parentId))
            {
                continue;
            }
            if (!children.TryGetValue(parentId, out List<T> list))
            {
                children[parentId] = list = new List<T>();
            }
            list.Add(item);
        }

        if (!children.TryGetValue(id(items[0]), out List<T> top))
        {
            return result;
        }

        foreach (T first in top)
        {
            List<T> subtree = new() { first };
            HashSet<string> seen = new() { id(first) };
            Queue<string> pending = new();
            pending.Enqueue(id(first));
            while (pending.Count > 0)
            {
                if (!children.TryGetValue(pending.Dequeue(), out List<T> below))
                {
                    continue;
                }
                foreach (T child in below)
                {
                    if (seen.Add(id(child)))
                    {
                        subtree.Add(child);
                        pending.Enqueue(id(child));
                    }
                }
            }
            result.Add(subtree);
        }
        return result;
    }
}
