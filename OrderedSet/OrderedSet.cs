using System;
using System.Collections.Generic;

namespace OrderedSet;

public sealed class OrderedSet<T> : BaseOrderedSet<T>, IEquatable<OrderedSet<T>>
{
    internal OrderedSet(Node<T> root, IComparer<T> comparer, int count) 
        : base(root, comparer, count) { }

    // ---------------------------------------------------------
    // Structural equality
    // ---------------------------------------------------------
    //
    // Here for the same reason `Set<T>` has it: a persistent value that two
    // routes can build separately is one nobody expects to compare by
    // reference. It also has to be here rather than only in `std/orderedset` —
    // `=` is that module's implementation, and this is what every hash-based
    // collection and every .NET consumer asks instead. The two answering
    // differently is the one thing equality must never do.
    //
    // `EqualityComparer<T>` and not `Comparer<T>`: the tree's ordering says
    // which slot an element goes in, not whether two elements are the same
    // thing. For a type the Bjolang compiler emitted, `EqualityComparer` *is*
    // that type's own `Eq` implementation.
    //
    // A paired walk, which is what the ordering buys: both trees enumerate in
    // the same order, so this is element against element with no lookup.
    // `std/orderedset` writes the same comparison the same way.
    public bool Equals(OrderedSet<T>? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other is null || Count != other.Count) return false;

        var comparer = EqualityComparer<T>.Default;
        using var mine = GetEnumerator();
        using var theirs = other.GetEnumerator();

        while (mine.MoveNext() && theirs.MoveNext())
            if (!comparer.Equals(mine.Current, theirs.Current))
                return false;

        return true;
    }

    public override bool Equals(object? obj) => obj is OrderedSet<T> other && Equals(other);

    // The same fold `eq-hash` performs in `std/orderedset`, seed and all, so
    // that the two cannot answer different numbers for one set.
    public override int GetHashCode()
    {
        var comparer = EqualityComparer<T>.Default;
        var hash = 17;

        foreach (var item in this)
            hash = HashCode.Combine(hash, item is null ? 0 : comparer.GetHashCode(item));

        return hash;
    }

    // ---------------------------------------------------------
    // Immutable Write API (Returns new Map)
    // ---------------------------------------------------------
    public OrderedSet<T> Add(T key)
    {
        // OPTIMIZATION: Use OwnerId.None (0).
        // This signals EnsureEditable to always copy the root path, 
        // producing a new tree of nodes that also have OwnerId.None.
        var newRoot = BTreeFunctions.Add(Root, key, Comparer, OwnerId.None, out bool countChanged);
        return new OrderedSet<T>(newRoot, Comparer, countChanged ? Count + 1 : Count);
    }
   
    public static OrderedSet<T> Empty(IComparer<T>? comparer = null)
    {
        // Create an empty Leaf Node.
        // 'default(OwnerId)' (usually 0) marks this node as Immutable/Persistent.
        // This ensures that any subsequent Set/Remove will clone this node 
        // instead of modifying it in place.
        var emptyRoot = new LeafNode<T>(default(OwnerId));
    
        return new OrderedSet<T>(emptyRoot, comparer ?? DefaultOrder.For<T>(), 0);
    }

    public OrderedSet<T> Remove(T key)
    {
        var newRoot = BTreeFunctions.Remove<T>(Root, key, Comparer, OwnerId.None, out bool removed);
        if (!removed) return this;
        return new OrderedSet<T>(newRoot, Comparer, Count - 1);
    }

    public TransientOrderedSet<T> ToTransient()
    {
        return new TransientOrderedSet<T>(Root, Comparer, Count);
    }
}
