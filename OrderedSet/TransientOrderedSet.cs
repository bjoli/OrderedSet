using System.Collections.Generic;

namespace OrderedSet;

public sealed class TransientOrderedSet<T> : BaseOrderedSet<T>
{
    // This is mutable, but we treat it as readonly for the ID generation logic usually.
    private OwnerId _transactionId;

    public TransientOrderedSet(Node<T> root, IComparer<T> comparer, int count) 
        : base(root, comparer, count) 
    {
        _transactionId = OwnerId.Next();
    }

    public void Add(T key)
    {
        Root = BTreeFunctions.Add(Root, key, Comparer, _transactionId, out bool countChanged);
        if (countChanged) Count++;
    }

    public void Remove(T key)
    {
        Root = BTreeFunctions.Remove<T>(Root, key, Comparer, _transactionId, out bool removed);
        if (removed) Count--;
    }

    public OrderedSet<T> ToPersistent()
    {
        // 1. Create the snapshot by copying all relevant information
            
        var snapshot = new OrderedSet<T>(Root, Comparer, Count);

        // 2. Protect the snapshot from THIS TransientOrderedSet by getting a new ownerId 
        // so that future edits will be done by CoW
        _transactionId = OwnerId.Next();
            
        return snapshot;
    }
}