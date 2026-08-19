using System;
using System.Collections;
using System.Collections.Generic;

namespace OrderedSet;

public abstract class BaseOrderedSet<T> : IEnumerable<T>
{
    internal Node<T> Root;
    internal readonly IComparer<T> Comparer;
    
    public int Count { get; protected set; }

    protected BaseOrderedSet(Node<T> root, IComparer<T> comparer, int count)
    {
        Root = root ?? throw new ArgumentNullException(nameof(root));
        Comparer = comparer ?? Comparer<T>.Default;
        Count = count;
    }

    // ---------------------------------------------------------
    // Read Operations (Shared)
    // ---------------------------------------------------------

    public bool Contains(T key)
    {
        return BTreeFunctions.Contains(Root, key, Comparer);
    }

    
    
    // ---------------------------------------------------------
    // Bootstrap / Factory Helpers
    // ---------------------------------------------------------
        
    public static OrderedSet<T> Create(IComparer<T>? comparer = null)
    {
        // Start with an empty leaf owned by None so the first write triggers CoW.
        var emptyRoot = new LeafNode<T>(OwnerId.None);
        return new OrderedSet<T>(emptyRoot, comparer ?? Comparer<T>.Default, 0);
    }

    public static TransientOrderedSet<T> CreateTransient(IComparer<T>? comparer = null)
    {
        var emptyRoot = new LeafNode<T>(OwnerId.None);
        return new TransientOrderedSet<T>(emptyRoot, comparer ?? Comparer<T>.Default, 0);
    }
    
    
    public BTreeEnumerator<T> GetEnumerator()
    {
        return AsEnumerable().GetEnumerator();
    }

    IEnumerator<T> IEnumerable<T>.GetEnumerator()
    {
        return GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
    
    // 1. Full Scan
    public BTreeEnumerable<T> AsEnumerable() 
        => new(Root, Comparer, false, default!, false, default!);

    // 2. Exact Range
    public BTreeEnumerable<T> Range(T min, T max) 
        => new(Root, Comparer, true, min, true, max);

    // 3. Start From (Open Ended)
    public BTreeEnumerable<T> From(T min) => new(Root, Comparer, true, min, false, default!);

    // 4. Until (Start at beginning)
    public BTreeEnumerable<T> Until(T max) 
        => new(Root, Comparer, false, default!, true, max);

    // ---------------------------------------------------------
    // Navigation Operations
    // ---------------------------------------------------------

    public bool TryGetMin(out T key) => BTreeFunctions.TryGetMin(Root, out key);

    public bool TryGetMax(out T key) => BTreeFunctions.TryGetMax(Root, out key);

    public bool TryGetSuccessor(T key, out T nextKey) => BTreeFunctions.TryGetSuccessor(Root, key, Comparer, out nextKey);

    public bool TryGetPredecessor(T key, out T prevKey) => BTreeFunctions.TryGetPredecessor(Root, key, Comparer, out prevKey);

    // ---------------------------------------------------------
    // Set Operations (Linear Merge O(N+M))
    // ---------------------------------------------------------

    public IEnumerable<T> Intersect(BaseOrderedSet<T> other)
    {
        using var enum1 = this.GetEnumerator();
        using var enum2 = other.GetEnumerator();
        
        bool has1 = enum1.MoveNext();
        bool has2 = enum2.MoveNext();

        while (has1 && has2)
        {
            int cmp = Comparer.Compare(enum1.Current, enum2.Current);
            if (cmp == 0)
            {
                yield return enum1.Current;
                has1 = enum1.MoveNext();
                has2 = enum2.MoveNext();
            }
            else if (cmp < 0) has1 = enum1.MoveNext();
            else has2 = enum2.MoveNext();
        }
    }

    public IEnumerable<T> Except(BaseOrderedSet<T> other)
    {
        using var enum1 = this.GetEnumerator();
        using var enum2 = other.GetEnumerator();
        
        bool has1 = enum1.MoveNext();
        bool has2 = enum2.MoveNext();

        while (has1 && has2)
        {
            int cmp = Comparer.Compare(enum1.Current, enum2.Current);
            if (cmp == 0)
            {
                has1 = enum1.MoveNext();
                has2 = enum2.MoveNext();
            }
            else if (cmp < 0)
            {
                yield return enum1.Current;
                has1 = enum1.MoveNext();
            }
            else
            {
                has2 = enum2.MoveNext();
            }
        }

        while (has1)
        {
            yield return enum1.Current;
            has1 = enum1.MoveNext();
        }
    }

    public IEnumerable<T> SymmetricExcept(BaseOrderedSet<T> other)
    {
        using var enum1 = this.GetEnumerator();
        using var enum2 = other.GetEnumerator();
        
        bool has1 = enum1.MoveNext();
        bool has2 = enum2.MoveNext();

        while (has1 && has2)
        {
            int cmp = Comparer.Compare(enum1.Current, enum2.Current);
            if (cmp == 0)
            {
                has1 = enum1.MoveNext();
                has2 = enum2.MoveNext();
            }
            else if (cmp < 0)
            {
                yield return enum1.Current;
                has1 = enum1.MoveNext();
            }
            else
            {
                yield return enum2.Current;
                has2 = enum2.MoveNext();
            }
        }

        while (has1)
        {
            yield return enum1.Current;
            has1 = enum1.MoveNext();
        }
        while (has2)
        {
            yield return enum2.Current;
            has2 = enum2.MoveNext();
        }
    }
}
