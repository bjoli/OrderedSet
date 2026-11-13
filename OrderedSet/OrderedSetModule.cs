using System.Diagnostics.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace OrderedSet;

/// <summary>
///     The static interface to <see cref="OrderedSet{T}" />.
///
///     Every operation takes the set explicitly, and the higher-order ones take the function
///     first and the set last — the argument order a curried call site wants, and the one
///     <c>SetModule</c> already uses. A fold's callback takes the element first and the
///     accumulator last, as it does everywhere else in this codebase.
/// </summary>
[NonUserCode]
public static class OrderedSetModule
{
    // ---------------------------------------------------------
    // Construction
    // ---------------------------------------------------------

    /// <summary>What the set orders its elements by.</summary>
    public static IComparer<T> Comparer<T>(OrderedSet<T> set) => set.Comparer;

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

    /// <summary>(std parallel)'s chunks: about k sets in order, sharing the nodes.</summary>
    public static OrderedSet<T>[] Split<T>(OrderedSet<T> set, int k) => set.Split(k);

    /// <summary>(std parallel)'s join: sets in order, in one set. See OrderedSet.Concat.</summary>
    public static OrderedSet<T> Concat<T>(OrderedSet<T>[] sets) => OrderedSet<T>.Concat(sets);

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
    // Many sets at once
    //
    // Measured on int keys, against the two-set functions applied two at a
    // time:
    //  - Union is a transient on the biggest set when the others hold few
    //    keys, and else a k-way merge in key order into a builder: 10 sets
    //    of 100k took 25 ms against 67 ms.
    //  - Intersection starts from the smallest set, since the result can
    //    only shrink, and against each other set either probes it for each
    //    key kept or walks both in order: 10 sets took 4 ms against 29 ms.
    //  - Difference removes the others' keys from a transient of the first
    //    set when they are few, and else keeps each key of the first set
    //    that no other set has: a set of 1M less 100 sets of 100 took 2 ms
    //    against 1.2 s.
    // Sets ordered by different comparers are taken two at a time.
    // ---------------------------------------------------------

    /// <summary>
    ///     The k-way union is used when the sets other than the biggest hold at least this share
    ///     of the keys, in percent.
    /// </summary>
    public const int ManyUnionPercent = 30;

    /// <summary>The k-way union is used for at most this many sets.</summary>
    public const int ManyUnionMaxInputs = 32;

    /// <summary>The keys in any of the sets.</summary>
    public static OrderedSet<T> UnionAll<T>(OrderedSet<T> set, OrderedSet<T> other, OrderedSet<T>[] more)
    {
        var inputs = NonEmpty(set, other, more);
        if (inputs.Count == 0) return set;
        if (inputs.Count == 1) return inputs[0];
        if (!SameOrder(inputs)) return Fold(inputs, Merge);

        var biggest = 0;
        long total = 0;
        for (var i = 0; i < inputs.Count; i++)
        {
            total += inputs[i].Count;
            if (inputs[i].Count > inputs[biggest].Count) biggest = i;
        }

        if (inputs.Count > ManyUnionMaxInputs || (total - inputs[biggest].Count) * 100 < total * ManyUnionPercent)
        {
            var transient = inputs[biggest].ToTransient();
            for (var i = 0; i < inputs.Count; i++)
                if (i != biggest)
                    foreach (var key in inputs[i]) transient.Add(key);
            return transient.ToPersistent();
        }

        var comparer = inputs[0].Comparer;
        var walks = new BTreeEnumerator<T>[inputs.Count];
        var heap = new PriorityQueue<int, T>(inputs.Count, comparer);
        for (var i = 0; i < walks.Length; i++)
        {
            walks[i] = inputs[i].GetEnumerator();
            if (walks[i].MoveNext()) heap.Enqueue(i, walks[i].Current);
        }

        var builder = new OrderedSetBuilder<T>((int)Math.Min(total, int.MaxValue), comparer);
        var any = false;
        T last = default!;
        while (heap.TryDequeue(out var input, out var key))
        {
            if (!any || comparer.Compare(key, last) != 0)
            {
                builder.Add(key);
                last = key;
                any = true;
            }
            if (walks[input].MoveNext()) heap.Enqueue(input, walks[input].Current);
        }
        return builder.Build();
    }

    /// <summary>The keys in every one of the sets.</summary>
    public static OrderedSet<T> IntersectAll<T>(OrderedSet<T> set, OrderedSet<T> other, OrderedSet<T>[] more)
    {
        var inputs = new List<OrderedSet<T>>(more.Length + 2) { set, other };
        inputs.AddRange(more);
        if (!SameOrder(inputs)) return Fold(inputs, Intersect);

        // The result can only shrink, so the smallest set is the first: what it lacks, no
        // answer has.
        inputs.Sort([NonUserCode] (a, b) => a.Count.CompareTo(b.Count));
        if (inputs[0].Count == 0) return inputs[0];

        var comparer = inputs[0].Comparer;
        var kept = new T[inputs[0].Count];
        var n = 0;
        foreach (var key in inputs[0]) kept[n++] = key;

        for (var s = 1; s < inputs.Count && n > 0; s++)
            n = Filter(kept, n, inputs[s], comparer, keep: true);

        if (n == inputs[0].Count) return inputs[0];
        var builder = new OrderedSetBuilder<T>(n, comparer);
        for (var i = 0; i < n; i++) builder.Add(kept[i]);
        return builder.Build();
    }

    /// <summary>The keys of <paramref name="set" /> that none of the other sets has.</summary>
    public static OrderedSet<T> ExceptAll<T>(OrderedSet<T> set, OrderedSet<T> other, OrderedSet<T>[] more)
    {
        var others = new List<OrderedSet<T>>(more.Length + 1);
        if (other.Count > 0) others.Add(other);
        foreach (var s in more)
            if (s.Count > 0) others.Add(s);
        if (set.Count == 0 || others.Count == 0) return set;

        var all = new List<OrderedSet<T>>(others.Count + 1) { set };
        all.AddRange(others);
        if (!SameOrder(all)) return Fold(all, Except);

        // Two ways. Remove the others' keys from a transient of the set: a removal costs a
        // descent and copies of nodes, about three probes. Or filter the set's keys against
        // each other set in turn, by a probe of it for each key kept or a walk of both in order,
        // whichever is less; this counts all the set's keys kept, so it is an upper bound.
        double removing = 0, filtering = 0;
        var depth = Math.Log2(set.Count + 2);
        foreach (var o in others)
        {
            removing += 3.0 * o.Count * depth;
            filtering += Math.Min((double)set.Count * Math.Log2(o.Count + 2), set.Count + (double)o.Count);
        }

        if (removing < filtering)
        {
            var transient = set.ToTransient();
            foreach (var o in others)
                foreach (var key in o) transient.Remove(key);
            return transient.ToPersistent();
        }

        var comparer = set.Comparer;
        var kept = new T[set.Count];
        var n = 0;
        foreach (var key in set) kept[n++] = key;
        foreach (var against in others)
        {
            if (n == 0) break;
            n = Filter(kept, n, against, comparer, keep: false);
        }

        if (n == set.Count) return set;
        var builder = new OrderedSetBuilder<T>(n, comparer);
        for (var i = 0; i < n; i++) builder.Add(kept[i]);
        return builder.Build();
    }

    // Keeps the first n keys of `kept`, in order, that `against` has (keep) or lacks (not
    // keep), at the front of `kept`; answers how many. A probe of `against` costs a descent,
    // about 20 key steps of a walk of it, so few keys are probed and many walked against it.
    private static int Filter<T>(T[] kept, int n, OrderedSet<T> against, IComparer<T> comparer, bool keep)
    {
        var w = 0;
        if ((long)n * 20 < against.Count)
        {
            for (var i = 0; i < n; i++)
                if (against.Contains(kept[i]) == keep) kept[w++] = kept[i];
            return w;
        }

        var walk = against.GetEnumerator();
        var has = walk.MoveNext();
        for (var i = 0; i < n; i++)
        {
            while (has && comparer.Compare(walk.Current, kept[i]) < 0) has = walk.MoveNext();
            var there = has && comparer.Compare(walk.Current, kept[i]) == 0;
            if (there == keep) kept[w++] = kept[i];
        }
        return w;
    }

    // The sets that have keys, in order.
    private static List<OrderedSet<T>> NonEmpty<T>(OrderedSet<T> set, OrderedSet<T> other, OrderedSet<T>[] more)
    {
        var inputs = new List<OrderedSet<T>>(more.Length + 2);
        if (set.Count > 0) inputs.Add(set);
        if (other.Count > 0) inputs.Add(other);
        foreach (var s in more)
            if (s.Count > 0) inputs.Add(s);
        return inputs;
    }

    private static bool SameOrder<T>(List<OrderedSet<T>> inputs)
    {
        var first = inputs[0].Comparer;
        foreach (var s in inputs)
            if (!ReferenceEquals(s.Comparer, first) && !s.Comparer.Equals(first)) return false;
        return true;
    }

    private static OrderedSet<T> Fold<T>(List<OrderedSet<T>> inputs, Func<OrderedSet<T>, OrderedSet<T>, OrderedSet<T>> combine)
    {
        var result = inputs[0];
        for (var i = 1; i < inputs.Count; i++) result = combine(result, inputs[i]);
        return result;
    }

    // ---------------------------------------------------------
    // Navigation — what the ordering buys over a hashed set
    // ---------------------------------------------------------

    // A partial answer is `bool TryXyz(..., out ...)`, the out marked
    // `[MaybeNullWhen(false)]`: the empty set has no smallest element, and that
    // is an answer. Bjolang imports these with `(out T)` and gets an `Option`.

    public static bool TryGetMin<T>(OrderedSet<T> set, [MaybeNullWhen(false)] out T key) =>
        set.TryGetMin(out key);

    public static bool TryGetMax<T>(OrderedSet<T> set, [MaybeNullWhen(false)] out T key) =>
        set.TryGetMax(out key);

    /// <summary>
    ///     The smallest element greater than <paramref name="key" />, which need not itself be in
    ///     the set.
    /// </summary>
    public static bool TryGetSuccessor<T>(OrderedSet<T> set, T key, [MaybeNullWhen(false)] out T next) =>
        set.TryGetSuccessor(key, out next);

    public static bool TryGetPredecessor<T>(OrderedSet<T> set, T key, [MaybeNullWhen(false)] out T previous) =>
        set.TryGetPredecessor(key, out previous);

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
        // The set itself when every element is kept; see BTreeRebuild.
        var (root, count) = BTreeRebuild<T>.Filter(set.Root, predicate);
        if (root == null) return set;
        return count == 0 ? OrderedSet<T>.Empty(set.Comparer) : new OrderedSet<T>(root, set.Comparer, count);
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
        return !Iter<T>([NonUserCode] (k) => !predicate(k), set);
    }

    /// <summary>
    ///     The first element in order satisfying <paramref name="predicate" />, if there is one.
    /// </summary>
    public static bool TryFindKey<T>(Func<T, bool> predicate, OrderedSet<T> set, [MaybeNullWhen(false)] out T key)
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

    // ---------------------------------------------------------
    // Walking
    // ---------------------------------------------------------

    /// <summary>The cursor on the first element of the set, or a done cursor.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static OrderedSetCursor<T> Cursor<T>(OrderedSet<T> set) => OrderedSetCursor<T>.Start(set.Root);

    /// <summary>True when the cursor is past the last element. Does not move the cursor.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CursorDone<T>(OrderedSetCursor<T> cursor) => cursor.Done;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T CursorCurrent<T>(OrderedSetCursor<T> cursor) => cursor.Current;

    /// <summary>The cursor on the next element. The cursor given does not change.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static OrderedSetCursor<T> CursorNext<T>(OrderedSetCursor<T> cursor) => cursor.Next();
}
