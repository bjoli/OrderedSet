using System.Collections.Generic;

namespace OrderedSet;

public sealed class OrderedSet<T> : BaseOrderedSet<T>
{
    internal OrderedSet(Node<T> root, IComparer<T> comparer, int count) 
        : base(root, comparer, count) { }

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
    
        return new OrderedSet<T>(emptyRoot, comparer ?? Comparer<T>.Default, 0);
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
