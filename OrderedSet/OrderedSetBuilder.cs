using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace OrderedSet;

public sealed class OrderedSetBuilder<T>
{
    private readonly List<T> _items;
    private readonly IComparer<T> _comparer;

    public OrderedSetBuilder(IComparer<T>? comparer = null)
    {
        _items = new List<T>();
        _comparer = comparer ?? DefaultOrder.For<T>();
    }

    public OrderedSetBuilder(int capacity, IComparer<T>? comparer = null)
    {
        _items = new List<T>(capacity);
        _comparer = comparer ?? DefaultOrder.For<T>();
    }

    public void Add(T key)
    {
        _items.Add(key);
    }

    public void AddRange(IEnumerable<T> range)
    {
        _items.AddRange(range);
    }

    /// <summary>
    ///     How many elements have been appended. Duplicates are still counted: they are
    ///     compacted by <see cref="Build" />, so this is the number of appends rather than the
    ///     size of the set being built.
    /// </summary>
    public int Count => _items.Count;

    public OrderedSet<T> Build()
    {
        if (_items.Count == 0)
        {
            return OrderedSet<T>.Empty(_comparer);
        }

        // We need a stable sort. Since List<T>.Sort() and Array.Sort() are unstable,
        // we attach the original index to preserve insertion order for duplicate keys.
        var array = new (T item, int index)[_items.Count];
        for (int i = 0; i < _items.Count; i++)
        {
            array[i] = (_items[i], i);
        }

        Array.Sort(array, (a, b) =>
        {
            int cmp = _comparer.Compare(a.item, b.item);
            if (cmp == 0)
            {
                cmp = a.index.CompareTo(b.index);
            }
            return cmp;
        });

        // Compact duplicates (last one wins, based on original insertion order / stable sort)
        int uniqueCount = 0;
        for (int i = 0; i < array.Length; i++)
        {
            if (uniqueCount > 0 && _comparer.Compare(array[i].item, array[uniqueCount - 1].item) == 0)
            {
                // Overwrite the previous one
                array[uniqueCount - 1] = array[i];
            }
            else
            {
                array[uniqueCount++] = array[i];
            }
        }

        var uniqueSpan = new ReadOnlySpan<(T item, int index)>(array, 0, uniqueCount);
        var root = BuildNodes(uniqueSpan);
        return new OrderedSet<T>(root, _comparer, uniqueCount);
    }

    private Node<T> BuildNodes(ReadOnlySpan<(T item, int index)> span)
    {
        // 1. Build leaf nodes
        int numLeaves = (span.Length + LeafNode<T>.Capacity - 1) / LeafNode<T>.Capacity;
        var leaves = new Node<T>[numLeaves];

        for (int i = 0; i < numLeaves; i++)
        {
            int offset = i * LeafNode<T>.Capacity;
            int count = Math.Min(LeafNode<T>.Capacity, span.Length - offset);

            var leaf = new LeafNode<T>(OwnerId.None);
            for (int j = 0; j < count; j++)
            {
                leaf.Keys![j] = span[offset + j].item;
                
            }
            leaf.SetCount(count);
            leaves[i] = leaf;
        }

        // 2. Build internal nodes bottom-up until we have a single root
        Node<T>[] currentLevel = leaves;

        while (currentLevel.Length > 1)
        {
            int numParents = (currentLevel.Length + InternalNode<T>.Capacity - 1) / InternalNode<T>.Capacity;
            var parents = new Node<T>[numParents];

            for (int i = 0; i < numParents; i++)
            {
                int offset = i * InternalNode<T>.Capacity;
                int count = Math.Min(InternalNode<T>.Capacity, currentLevel.Length - offset);

                var internalNode = new InternalNode<T>(OwnerId.None);
                var childrenSpan = MemoryMarshal.CreateSpan(ref internalNode.Children[0]!, count);
                var keysSpan = MemoryMarshal.CreateSpan(ref internalNode.Keys[0], count - 1);

                for (int j = 0; j < count; j++)
                {
                    childrenSpan[j] = currentLevel[offset + j];
                    
                    if (j < count - 1)
                    {
                        // The routing key is the first key of the right child
                        keysSpan[j] = GetFirstKey(currentLevel[offset + j + 1]);
                    }
                }
                
                // An internal node has N children, so Count is N-1
                internalNode.SetCount(count - 1);
                parents[i] = internalNode;
            }

            currentLevel = parents;
        }

        return currentLevel[0];
    }

    private T GetFirstKey(Node<T> node)
    {
        while (!node.IsLeaf)
        {
            var internalNode = node.AsInternal();
            node = internalNode.GetChildren()[0];
        }
        return node.AsLeaf().Keys![0];
    }
}
