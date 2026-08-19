using System;
using System.Collections.Generic;

namespace OrderedSet;

public static class TransientOrderedSetModule
{
    // transient-orderedset-map: function set
    public static TransientOrderedSet<T2> Map<T1, T2>(
        Func<T1, T2> mapper,
        TransientOrderedSet<T1> set,
        IComparer<T2>? comparer = null)
    {
        var transient = BaseOrderedSet<T2>.CreateTransient(comparer);
        foreach (var item in set)
        {
            var mapped = mapper(item);
            transient.Add(mapped);
        }
        return transient;
    }

    // transient-orderedset-filter: predicate set
    public static TransientOrderedSet<T> Filter<T>(
        Func<T, bool> predicate,
        TransientOrderedSet<T> set)
    {
        var transient = BaseOrderedSet<T>.CreateTransient(set.Comparer);
        foreach (var item in set)
        {
            if (predicate(item))
            {
                transient.Add(item);
            }
        }
        return transient;
    }

    // transient-orderedset-filter!: predicate set (in-place)
    public static TransientOrderedSet<T> FilterInPlace<T>(
        Func<T, bool> predicate,
        TransientOrderedSet<T> set)
    {
        var toRemove = new List<T>();
        foreach (var item in set)
        {
            if (!predicate(item))
            {
                toRemove.Add(item);
            }
        }
        foreach (var key in toRemove)
        {
            set.Remove(key);
        }
        return set;
    }

    // transient-orderedset-fold: folder state set
    public static TState Fold<T, TState>(
        Func<T, TState, TState> folder,
        TState state,
        TransientOrderedSet<T> set)
    {
        var currentState = state;
        foreach (var item in set)
        {
            currentState = folder(item, currentState);
        }
        return currentState;
    }

    // transient-orderedset-for-each: action set
    public static void ForEach<T>(
        Action<T> action,
        TransientOrderedSet<T> set)
    {
        foreach (var item in set)
        {
            action(item);
        }
    }

    // transient-orderedset-ref: set key
    public static T Ref<T>(TransientOrderedSet<T> set, T key)
    {
        if (set.Contains(key))
        {
            return key;
        }
        throw new KeyNotFoundException($"Key '{key}' not found in the set.");
    }
    
    // transient-orderedset-set!: set key
    // Mutates in-place. Returns the set for convenience/chaining.
    public static TransientOrderedSet<T> Add<T>(TransientOrderedSet<T> set, T key)
    {
        set.Add(key);
        return set;
    }

    // transient-orderedset-delete!: set key
    // Mutates in-place. Returns the set for convenience/chaining.
    public static TransientOrderedSet<T> Remove<T>(TransientOrderedSet<T> set, T key)
    {
        set.Remove(key);
        return set;
    }

    // transient-orderedset-contains?: set key
    public static bool Contains<T>(TransientOrderedSet<T> set, T key)
    {
        return set.Contains(key);
    }

    // transient-orderedset-empty
    public static TransientOrderedSet<T> Empty<T>(IComparer<T>? comparer = null)
    {
        return BaseOrderedSet<T>.CreateTransient(comparer);
    }

    // orderedset->transient-orderedset
    public static TransientOrderedSet<T> FromPersistent<T>(OrderedSet<T> set)
    {
        return set.ToTransient();
    }

    // transient-orderedset->orderedset
    public static OrderedSet<T> ToPersistent<T>(TransientOrderedSet<T> set)
    {
        return set.ToPersistent();
    }
}
