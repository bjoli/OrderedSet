using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace OrderedSet;



public struct BTreeEnumerable<T> : IEnumerable<T>
{
    private readonly Node<T> _root;
    private readonly IComparer<T> _comparer;
    private readonly T _min, _max;
    private readonly bool _hasMin, _hasMax;

    public BTreeEnumerable(Node<T> root, IComparer<T> comparer, bool hasMin, T min, bool hasMax, T max)
    {
        _root = root; _comparer = comparer;
        _hasMin = hasMin; _min = min;
        _hasMax = hasMax; _max = max;
    }

    public BTreeEnumerator<T> GetEnumerator()
    {
        return new BTreeEnumerator<T>(_root, _comparer, _hasMin, _min, _hasMax, _max);
    }

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

// Fixed-size buffer for the path. 
// Depth 16 * 32 (branching factor) = Exabytes of capacity.
// The B tree is, theoretically, not limited by the size of an int, like
// all int-indexed data structures (or bit partitioned ones).
// This should be enough for anyone.
[InlineArray(16)]
internal struct IterNodeBuffer<T>
{
    private Node<T> _element0;
}

[InlineArray(16)]
internal struct IterIndexBuffer
{
    private int _element0;
}

public struct BTreeEnumerator<T> : IEnumerator<T>
    {
        private readonly IComparer<T> _comparer;
        private readonly Node<T> _root;
        
        // --- BOUNDS ---
        private readonly bool _hasMax;
        private readonly T _maxKey;
        private readonly bool _hasMin;
        private readonly T _minKey;

        // --- INLINE STACK ---
        private IterNodeBuffer<T> _nodeStack; 
        private IterIndexBuffer _indexStack;
        private int _depth;

        // --- STATE ---
        private LeafNode<T>? _currentLeaf;
        private int _currentLeafIndex;
        private T _current = default!;

        // Unified Constructor
        // We use boolean flags because 'K' might be a struct where 'null' is impossible.
        public BTreeEnumerator(Node<T>? root, IComparer<T> comparer, bool hasMin, T minKey, bool hasMax, T maxKey)
        {
            _root = root!;
            _comparer = comparer;
            _hasMax = hasMax;
            _maxKey = maxKey;
            _hasMin = hasMin;
            _minKey = minKey;
            
            _nodeStack = new IterNodeBuffer<T>();
            _indexStack = new IterIndexBuffer(); // Explicit struct init
            _depth = 0;
            _currentLeaf = null;
            _currentLeafIndex = -1;
            _current = default!;

            if (root != null)
            {
                if (hasMin)
                {
                    Seek(minKey);
                }
                else
                {
                    DiveLeft();
                }
            }
        }

        // Logic 1: Unbounded Start (Go to very first item)
        private void DiveLeft()
        {
            Node<T> node = _root;
            _depth = 0;

            while (!node.IsLeaf)
            {
                var internalNode = node.AsInternal();
                _nodeStack[_depth] = internalNode;
                _indexStack[_depth] = 0; // Always take left-most child
                _depth++;
                node = internalNode.Children[0]!;
            }

            _currentLeaf = node.AsLeaf();
            _currentLeafIndex = -1; // Position before the first element (0)
        }

        // Logic 2: Bounded Start (Go to specific key)
        private void Seek(T key)
        {
            Node<T> node = _root;
            _depth = 0;

            // Dive using Routing
            while (!node.IsLeaf)
            {
                var internalNode = node.AsInternal();
                int idx = BTreeFunctions.FindRoutingIndex(internalNode, key, _comparer);
                
                _nodeStack[_depth] = internalNode;
                _indexStack[_depth] = idx;
                _depth++;

                node = internalNode.Children[idx]!;
            }

            // Find index in Leaf
            _currentLeaf = node.AsLeaf();
            int index = BTreeFunctions.FindIndex(_currentLeaf, key, _comparer);
            
            // Set position to (index - 1) so that the first MoveNext() lands on 'index'
            _currentLeafIndex = index - 1;
        }

        public bool MoveNext()
        {
            if (_currentLeaf == null) return false;

            // 1. Try to advance in current leaf
            if (++_currentLeafIndex < _currentLeaf.Header.Count)
            {
                // OPTIMIZATION: Check Max Bound (if active)
                if (_hasMax)
                {
                     // If Current Key > Max Key, we are done.
                     if (_comparer.Compare(_currentLeaf.Keys![_currentLeafIndex], _maxKey) > 0)
                     {
                         _currentLeaf = null; // Close iterator
                         return false;
                     }
                }

                _current = _currentLeaf.Keys![_currentLeafIndex];
                return true;
            }

            // 2. Leaf exhausted. Find next leaf.
            if (FindNextLeaf())
            {
                // Found new leaf, index reset to 0. 
                // Check Max Bound immediately for the first item
                if (_hasMax)
                {
                    if (_comparer.Compare(_currentLeaf.Keys![0], _maxKey) > 0)
                    {
                        _currentLeaf = null;
                        return false;
                    }
                }

                _current = _currentLeaf.Keys![0];
                return true;
            }

            return false;
        }

        private bool FindNextLeaf()
        {
            while (_depth > 0)
            {
                _depth--;
                var internalNode = _nodeStack[_depth].AsInternal();
                int currentIndex = _indexStack[_depth];

                if (currentIndex < internalNode.Header.Count)
                {
                    int nextIndex = currentIndex + 1;
                    _indexStack[_depth] = nextIndex;
                    _depth++;

                    Node<T> node = internalNode.Children[nextIndex]!;
                    while (!node.IsLeaf)
                    {
                        _nodeStack[_depth] = node;
                        _indexStack[_depth] = 0;
                        _depth++;
                        node = node.AsInternal().Children[0]!;
                    }

                    _currentLeaf = node.AsLeaf();
                    _currentLeafIndex = 0;
                    return true;
                }
            }
            return false;
        }

        public T Current => _current;
        object IEnumerator.Current => _current;
        public void Reset()
        {
            // 1. Clear current state
            _depth = 0;
            _currentLeaf = null;
            _currentLeafIndex = -1;
            _current = default!;

            // 2. Re-initialize based on how the iterator was created
            if (_root != null)
            {
                if (_hasMin)
                {
                    // If we had a start range, find it again
                    Seek(_minKey);
                }
                else
                {
                    // Otherwise, go back to the very first leaf
                    DiveLeft();
                }
            }
        }
        public void Dispose() { }
    }
