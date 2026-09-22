using System;
using System.Collections.Generic;

namespace OrderedSet;

/// <summary>
///     The static interface to <see cref="OrderedSet{T}" />.
///
///     Every operation takes the set explicitly, and the higher-order ones take the function
///     first and the set last — the argument order a curried call site wants, and the one
///     <c>SetModule</c> already uses. A fold's callback takes the element first and the
///     accumulator last, as it does everywhere else in this codebase.
/// </summary>
public static class OrderedSetModule
{
    // ---------------------------------------------------------
    // Construction
    // ---------------------------------------------------------

    // orderedset-empty
    public static OrderedSet<T> Empty<T>(IComparer<T>? comparer = null)
    {
        return OrderedSet<T>.Empty(comparer);
    }

    // seq->orderedset
    public static OrderedSet<T> FromEnumerable<T>(IEnumerable<T> source, IComparer<T>? comparer = null)
    {
        var builder = new OrderedSetBuilder<T>(comparer);
        foreach (var item in source) builder.Add(item);
        return builder.Build();
    }

    /// <summary>The elements, in order. A set already is an <see cref="IEnumerable{T}" />.</summary>
    public static IEnumerable<T> AsEnumerable<T>(OrderedSet<T> set) => set;

    // orderedset-clear: the empty set under the same ordering
    public static OrderedSet<T> Clear<T>(OrderedSet<T> set)
    {
        return OrderedSet<T>.Empty(set.Comparer);
    }

    // orderedset->transient-orderedset
    public static TransientOrderedSet<T> ToTransient<T>(OrderedSet<T> set)
    {
        return set.ToTransient();
    }

    // ---------------------------------------------------------
    // Reading
    // ---------------------------------------------------------

    // orderedset-length
    public static int Count<T>(OrderedSet<T> set)
    {
        return set.Count;
    }

    // orderedset-empty?
    public static bool IsEmpty<T>(OrderedSet<T> set)
    {
        return set.Count == 0;
    }

    // orderedset-contains?: set key
    public static bool Contains<T>(OrderedSet<T> set, T key)
    {
        return set.Contains(key);
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

    // ---------------------------------------------------------
    // Writing
    // ---------------------------------------------------------

    // orderedset-add: set key
    public static OrderedSet<T> Add<T>(OrderedSet<T> set, T key)
    {
        return set.Add(key);
    }

    // orderedset-remove: set key
    public static OrderedSet<T> Remove<T>(OrderedSet<T> set, T key)
    {
        return set.Remove(key);
    }

    /// <summary>
    ///     Every element of <paramref name="range" />, added in one pass. Through a transient, so
    ///     the tree is copied once rather than once per element.
    /// </summary>
    public static OrderedSet<T> AddRange<T>(OrderedSet<T> set, IEnumerable<T> range)
    {
        var transient = set.ToTransient();
        foreach (var key in range) transient.Add(key);
        return transient.ToPersistent();
    }

    public static OrderedSet<T> RemoveRange<T>(OrderedSet<T> set, IEnumerable<T> range)
    {
        var transient = set.ToTransient();
        foreach (var key in range) transient.Remove(key);
        return transient.ToPersistent();
    }

    // ---------------------------------------------------------
    // Set algebra
    //
    // The three below are the linear merges on `BaseOrderedSet`, collected back into a set.
    // They walk both trees once, which beats the "remove what is missing" spelling by the
    // depth of the tree per element.
    // ---------------------------------------------------------

    // orderedset-merge / union
    public static OrderedSet<T> Merge<T>(OrderedSet<T> set, OrderedSet<T> other)
    {
        if (set.Count == 0) return other;
        if (other.Count == 0) return set;
        return AddRange(set, other);
    }

    public static OrderedSet<T> Intersect<T>(OrderedSet<T> set, OrderedSet<T> other)
    {
        return FromEnumerable(set.Intersect(other), set.Comparer);
    }

    public static OrderedSet<T> Except<T>(OrderedSet<T> set, OrderedSet<T> other)
    {
        return FromEnumerable(set.Except(other), set.Comparer);
    }

    public static OrderedSet<T> SymmetricExcept<T>(OrderedSet<T> set, OrderedSet<T> other)
    {
        return FromEnumerable(set.SymmetricExcept(other), set.Comparer);
    }

    // ---------------------------------------------------------
    // Navigation — what the ordering buys over a hashed set
    // ---------------------------------------------------------

    // A pair rather than an `out` parameter, which is the convention every
    // partial answer in this interface follows: an `out` is a C# idiom and
    // nothing else can call it, while a tuple is a value in any language. The
    // empty set has no smallest element, and that is an answer.

    public static (bool found, T key) TryGetMin<T>(OrderedSet<T> set)
    {
        var found = set.TryGetMin(out var key);
        return (found, key);
    }

    public static (bool found, T key) TryGetMax<T>(OrderedSet<T> set)
    {
        var found = set.TryGetMax(out var key);
        return (found, key);
    }

    /// <summary>
    ///     The smallest element greater than <paramref name="key" />, which need not itself be in
    ///     the set.
    /// </summary>
    public static (bool found, T key) TryGetSuccessor<T>(OrderedSet<T> set, T key)
    {
        var found = set.TryGetSuccessor(key, out var next);
        return (found, next);
    }

    public static (bool found, T key) TryGetPredecessor<T>(OrderedSet<T> set, T key)
    {
        var found = set.TryGetPredecessor(key, out var previous);
        return (found, previous);
    }

    /// <summary>The elements from <paramref name="min" /> to <paramref name="max" />, inclusive.</summary>
    public static IEnumerable<T> Range<T>(OrderedSet<T> set, T min, T max) => set.Range(min, max);

    public static IEnumerable<T> From<T>(OrderedSet<T> set, T min) => set.From(min);

    public static IEnumerable<T> Until<T>(OrderedSet<T> set, T max) => set.Until(max);

    // ---------------------------------------------------------
    // Higher-order — function first, set last
    // ---------------------------------------------------------

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

    /// <summary>
    ///     Walks until <paramref name="action" /> answers false, and reports whether it ran to the
    ///     end. The early-exit walk every predicate below is written in terms of.
    /// </summary>
    public static bool Iter<T>(Func<T, bool> action, OrderedSet<T> set)
    {
        foreach (var item in set)
        {
            if (!action(item)) return false;
        }
        return true;
    }

    public static bool Exists<T>(Func<T, bool> predicate, OrderedSet<T> set)
    {
        return !Iter<T>(k => !predicate(k), set);
    }

    /// <summary>
    ///     The first element in order satisfying <paramref name="predicate" />, if there is one.
    /// </summary>
    public static (bool found, T key) TryFindKey<T>(Func<T, bool> predicate, OrderedSet<T> set)
    {
        foreach (var item in set)
        {
            if (predicate(item))
            {
                return (true, item);
            }
        }

        return (false, default!);
    }

    // ---------------------------------------------------------
    // Walking
    // ---------------------------------------------------------

    public static OrderedSetCursor<T> Cursor<T>(OrderedSet<T> set) => new OrderedSetCursor<T>(set);

    /// <summary>
    ///     Advances the cursor, and answers whether it ran off the end.
    ///
    ///     The advance happens here rather than in a step of its own: a walk asks "is there
    ///     more?" exactly once per element, so folding the two together is what lets the whole
    ///     traversal allocate nothing after the cursor itself.
    /// </summary>
    public static bool CursorDone<T>(OrderedSetCursor<T> cursor) => !cursor.Enumerator.MoveNext();

    public static T CursorCurrent<T>(OrderedSetCursor<T> cursor) => cursor.Enumerator.Current;
}

/// <summary>
///     A position in a walk of an <see cref="OrderedSet{T}" />, in order.
///
///     <see cref="BTreeEnumerator{T}" /> is a struct, which is what keeps a <c>foreach</c>
///     allocation-free — and exactly what makes it useless to a caller that has to *hold* the
///     position, since every copy advances independently. This is that struct in a heap cell:
///     one allocation for the walk, none per element.
/// </summary>
public sealed class OrderedSetCursor<T>
{
    public BTreeEnumerator<T> Enumerator;

    public OrderedSetCursor(OrderedSet<T> set)
    {
        Enumerator = set.GetEnumerator();
    }
}
