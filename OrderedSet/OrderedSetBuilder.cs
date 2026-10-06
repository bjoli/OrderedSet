using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace OrderedSet;

public sealed class OrderedSetBuilder<T>
{
    private readonly List<T> _items;
    private readonly IComparer<T> _comparer;

    // Every element so far was greater than the one before it. Walking a set, or mapping
    // over one with an order-keeping function, appends in order, and Build then needs
    // neither a sort nor a pass for duplicates.
    private bool _ascending = true;

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
        if (_ascending && _items.Count > 0 && _comparer.Compare(_items[^1], key) >= 0)
        {
            _ascending = false;
        }
        _items.Add(key);
    }

    public void AddRange(IEnumerable<T> range)
    {
        foreach (var key in range) Add(key);
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

        if (_ascending)
        {
            ReadOnlySpan<T> items = CollectionsMarshal.AsSpan(_items);
            return new OrderedSet<T>(BuildNodes(items), _comparer, items.Length);
        }

        // Array.Sort is unstable, so the original index breaks ties: among equal elements
        // the last one appended sorts last, and wins below.
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

        var unique = new T[array.Length];
        int uniqueCount = 0;
        for (int i = 0; i < array.Length; i++)
        {
            if (uniqueCount > 0 && _comparer.Compare(array[i].item, unique[uniqueCount - 1]) == 0)
            {
                unique[uniqueCount - 1] = array[i].item;
            }
            else
            {
                unique[uniqueCount++] = array[i].item;
            }
        }

        var uniqueSpan = new ReadOnlySpan<T>(unique, 0, uniqueCount);
        return new OrderedSet<T>(BuildNodes(uniqueSpan), _comparer, uniqueCount);
    }

    /// <summary>The tree over <paramref name="span" />, which is in strictly ascending order.</summary>
    private Node<T> BuildNodes(ReadOnlySpan<T> span)
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
                leaf.Keys![j] = span[offset + j];
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
