using System.Diagnostics.CodeAnalysis;
using System;
using System.Collections.Generic;

namespace OrderedSet;

/// <summary>
///     The static interface to <see cref="TransientOrderedSet{T}" />.
///
///     Deliberately <see cref="OrderedSetModule" />'s interface with the writes turned inside
///     out: the same names in the same argument order, except that a write mutates in place and
///     answers nothing rather than answering a new set. Reading is identical, because a
///     transient is a set — it is the *ownership* of the nodes that differs, which is what lets
///     an edit skip the copy and what makes <see cref="ToPersistent{T}" /> O(1).
/// </summary>
public static class TransientOrderedSetModule
{
    // ---------------------------------------------------------
    // Construction
    // ---------------------------------------------------------

    // transientorderedset-empty
    public static TransientOrderedSet<T> Empty<T>(IComparer<T>? comparer = null)
    {
        return BaseOrderedSet<T>.CreateTransient(comparer);
    }

    // orderedset->transientorderedset
    public static TransientOrderedSet<T> FromPersistent<T>(OrderedSet<T> set)
    {
        return set.ToTransient();
    }

    // transientorderedset->orderedset
    public static OrderedSet<T> ToPersistent<T>(TransientOrderedSet<T> set)
    {
        return set.ToPersistent();
    }

    public static TransientOrderedSet<T> FromEnumerable<T>(IEnumerable<T> source, IComparer<T>? comparer = null)
    {
        var transient = BaseOrderedSet<T>.CreateTransient(comparer);
        foreach (var item in source) transient.Add(item);
        return transient;
    }

    public static IEnumerable<T> AsEnumerable<T>(TransientOrderedSet<T> set) => set;

    // ---------------------------------------------------------
    // Reading
    // ---------------------------------------------------------

    public static int Count<T>(TransientOrderedSet<T> set) => set.Count;

    public static bool IsEmpty<T>(TransientOrderedSet<T> set) => set.Count == 0;

    // transientorderedset-contains?: set key
    public static bool Contains<T>(TransientOrderedSet<T> set, T key)
    {
        return set.Contains(key);
    }

    // transientorderedset-ref: set key
    public static T Ref<T>(TransientOrderedSet<T> set, T key)
    {
        if (set.Contains(key))
        {
            return key;
        }
        throw new KeyNotFoundException($"Key '{key}' not found in the set.");
    }

    public static bool TryGetMin<T>(TransientOrderedSet<T> set, [MaybeNullWhen(false)] out T key) =>
        set.TryGetMin(out key);

    public static bool TryGetMax<T>(TransientOrderedSet<T> set, [MaybeNullWhen(false)] out T key) =>
        set.TryGetMax(out key);

    public static bool TryGetSuccessor<T>(TransientOrderedSet<T> set, T key, [MaybeNullWhen(false)] out T next) =>
        set.TryGetSuccessor(key, out next);

    public static bool TryGetPredecessor<T>(TransientOrderedSet<T> set, T key, [MaybeNullWhen(false)] out T previous) =>
        set.TryGetPredecessor(key, out previous);

    public static IEnumerable<T> Range<T>(TransientOrderedSet<T> set, T min, T max) => set.Range(min, max);

    public static IEnumerable<T> From<T>(TransientOrderedSet<T> set, T min) => set.From(min);

    public static IEnumerable<T> Until<T>(TransientOrderedSet<T> set, T max) => set.Until(max);

    // ---------------------------------------------------------
    // Writing — in place, answering nothing
    // ---------------------------------------------------------

    // transientorderedset-add!: set key
    public static void Add<T>(TransientOrderedSet<T> set, T key)
    {
        set.Add(key);
    }

    // transientorderedset-remove!: set key
    public static void Remove<T>(TransientOrderedSet<T> set, T key)
    {
        set.Remove(key);
    }

    public static void AddRange<T>(TransientOrderedSet<T> set, IEnumerable<T> range)
    {
        foreach (var key in range) set.Add(key);
    }

    public static void RemoveRange<T>(TransientOrderedSet<T> set, IEnumerable<T> range)
    {
        foreach (var key in range) set.Remove(key);
    }

    /// <summary>
    ///     Drops everything the predicate rejects, in place. The elements to remove are collected
    ///     first: removing during a walk would invalidate the walk.
    /// </summary>
    public static void FilterInPlace<T>(Func<T, bool> predicate, TransientOrderedSet<T> set)
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
    }

    // ---------------------------------------------------------
    // Higher-order — function first, set last
    // ---------------------------------------------------------

    // transientorderedset-map: function set
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

    // transientorderedset-filter: predicate set
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

    // transientorderedset-fold: folder state set
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

    // transientorderedset-for-each: action set
    public static void ForEach<T>(
        Action<T> action,
        TransientOrderedSet<T> set)
    {
        foreach (var item in set)
        {
            action(item);
        }
    }

    public static bool Iter<T>(Func<T, bool> action, TransientOrderedSet<T> set)
    {
        foreach (var item in set)
        {
            if (!action(item)) return false;
        }
        return true;
    }

    public static bool Exists<T>(Func<T, bool> predicate, TransientOrderedSet<T> set)
    {
        return !Iter<T>(k => !predicate(k), set);
    }

    public static bool TryFindKey<T>(Func<T, bool> predicate, TransientOrderedSet<T> set, [MaybeNullWhen(false)] out T key)
    {
        foreach (var item in set)
        {
            if (predicate(item))
            {
                key = item;
                return true;
            }
        }

        key = default;
        return false;
    }

    /// <summary>
    ///     A walk of the set as it stands. Finish it before the next write: a transient is edited
    ///     in place, so a cursor into one is live in a way a persistent set's never is.
    /// </summary>
    public static TransientOrderedSetCursor<T> Cursor<T>(TransientOrderedSet<T> set) =>
        new TransientOrderedSetCursor<T>(set);

    public static bool CursorDone<T>(TransientOrderedSetCursor<T> cursor) => !cursor.Enumerator.MoveNext();

    public static T CursorCurrent<T>(TransientOrderedSetCursor<T> cursor) => cursor.Enumerator.Current;
}

/// <summary>
///     A position in a walk of a <see cref="TransientOrderedSet{T}" />. The struct enumerator in
///     a heap cell, for the reason <see cref="OrderedSetCursor{T}" /> is.
/// </summary>
public sealed class TransientOrderedSetCursor<T>
{
    public BTreeEnumerator<T> Enumerator;

    public TransientOrderedSetCursor(TransientOrderedSet<T> set)
    {
        Enumerator = set.GetEnumerator();
    }
}
