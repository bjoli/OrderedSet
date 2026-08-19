using System;
using System.Collections.Generic;

namespace OrderedSet;

public static class OrderedSetModule
{
    // orderedset-map: function set
    public static OrderedSet<T2> Map<T1, T2>(
        Func<T1, T2> mapper,
        OrderedSet<T1> set,
        IComparer<T2>? comparer = null)
    {
        var builder = new OrderedSetBuilder<T2>(set.Count, comparer);
        foreach (var item in set)
        {
            var mapped = mapper(item);
            builder.Add(mapped);
        }
        return builder.Build();
    }

    // orderedset-filter: predicate set
    public static OrderedSet<T> Filter<T>(
        Func<T, bool> predicate,
        OrderedSet<T> set)
    {
        var builder = new OrderedSetBuilder<T>(set.Count, set.Comparer);
        foreach (var item in set)
        {
            if (predicate(item))
            {
                builder.Add(item);
            }
        }
        return builder.Build();
    }

    // orderedset-fold: folder state set
    public static TState Fold<T, TState>(
        Func<T, TState, TState> folder,
        TState state,
        OrderedSet<T> set)
    {
        var currentState = state;
        foreach (var item in set)
        {
            currentState = folder(item, currentState);
        }
        return currentState;
    }

    // orderedset-for-each: action set
    public static void ForEach<T>(
        Action<T> action,
        OrderedSet<T> set)
    {
        foreach (var item in set)
        {
            action(item);
        }
    }

    // orderedset-ref: set key
    public static T Ref<T>(OrderedSet<T> set, T key)
    {
        if (set.Contains(key))
        {
            return key;
        }
        throw new KeyNotFoundException($"Key '{key}' not found in the set.");
    }
    
    // orderedset-set: set key
    public static OrderedSet<T> Add<T>(OrderedSet<T> set, T key)
    {
        return set.Add(key);
    }

    // orderedset-delete: set key
    public static OrderedSet<T> Remove<T>(OrderedSet<T> set, T key)
    {
        return set.Remove(key);
    }

    // orderedset-contains?: set key
    public static bool Contains<T>(OrderedSet<T> set, T key)
    {
        return set.Contains(key);
    }

    // orderedset-empty: 
    public static OrderedSet<T> Empty<T>(IComparer<T>? comparer = null)
    {
        return OrderedSet<T>.Empty(comparer);
    }
}
