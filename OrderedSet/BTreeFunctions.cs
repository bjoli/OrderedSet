using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace OrderedSet
{
    public static class BTreeFunctions
    {
        // ---------------------------------------------------------
        // Public API
        // ---------------------------------------------------------

        public static bool Contains<T>(Node<T> root, T key, IComparer<T> comparer)
        {
            Node<T> current = root;
            while (true)
            {
                if (current.IsLeaf)
                {
                    var leaf = current.AsLeaf();
                    int index = FindIndex(leaf, key, comparer);
                    if (index < leaf.Header.Count && comparer.Compare(leaf.Keys![index], key) == 0)
                    {
                        return true;
                    }
                    return false;
                }
                else
                {
                    var internalNode = current.AsInternal();
                    int index = FindRoutingIndex(internalNode, key, comparer);
                    current = internalNode.Children[index]!;
                }
            }
        }

        public static Node<T> Add<T>(Node<T> root, T key, IComparer<T> comparer, OwnerId owner, out bool countChanged)
        {
            root = root.EnsureEditable(owner);

            var (newNode, sep) = InsertRecursive(root, key, comparer, owner, out countChanged);

            if (newNode != null)
            {
                var newRoot = new InternalNode<T>(owner);
                
                newRoot.Keys[0] = sep;
                newRoot.Children[0] = root;
                newRoot.Children[1] = newNode;
                newRoot.SetCount(1);
                
                return newRoot;
            }

            return root;
        }

        private static (Node<T>? newNode, T separator ) InsertRecursive<T>(Node<T> node, T key, IComparer<T> comparer, OwnerId owner, out bool added)
        {
            if (node.IsLeaf)
            {
                var leaf = node.AsLeaf();
                int index = FindIndex(leaf, key, comparer);

                if (index < leaf.Header.Count && comparer.Compare(leaf.Keys![index], key) == 0)
                {
                    added = false;
                    return (null, default)!;
                }

                added = true;
                if (leaf.Header.Count < LeafNode<T>.Capacity)
                {
                    InsertIntoLeaf(leaf, index, key);
                    return (null, default)!;
                }
                else
                {
                    return SplitLeaf(leaf, index, key, owner);
                }
            }
            else
            {
                var internalNode = node.AsInternal();
                int index = FindRoutingIndex(internalNode, key, comparer);

                var child = internalNode.Children[index]!.EnsureEditable(owner);
                internalNode.Children[index] = child;

                var (newNode, sep) = InsertRecursive(child, key, comparer, owner, out added);

                if (newNode != null)
                {
                    if (internalNode.Header.Count < InternalNode<T>.Capacity - 1)
                    {
                        InsertIntoInternal(internalNode, index, sep, newNode);
                        return (null, default)!;
                    }
                    
                    return SplitInternal(internalNode, index, sep, newNode, owner);
                }
                return (null, default)!;
            }
        }

        public static Node<T> Remove<T>(Node<T> root, T key, IComparer<T> comparer, OwnerId owner, out bool countChanged)
        {
            root = root.EnsureEditable(owner);

            bool rebalanceNeeded = RemoveRecursive<T>(root, key, comparer, owner, out countChanged);

            if (rebalanceNeeded)
            {
                if (!root.IsLeaf)
                {
                    var internalRoot = root.AsInternal();
                    if (internalRoot.Header.Count == 0)
                    {
                        return internalRoot.Children[0]!;
                    }
                }
            }

            return root;
        }

        private static bool RemoveRecursive<T>(Node<T> node, T key, IComparer<T> comparer, OwnerId owner, out bool removed)
        {
            if (node.IsLeaf)
            {
                var leaf = node.AsLeaf();
                int index = FindIndex(leaf, key, comparer);

                if (index < leaf.Header.Count && comparer.Compare(leaf.Keys![index], key) == 0)
                {
                    RemoveFromLeaf(leaf, index);
                    removed = true;
                    return leaf.Header.Count < LeafNode<T>.MergeThreshold;
                }

                removed = false;
                return false;
            }
            else
            {
                var internalNode = node.AsInternal();
                int index = FindRoutingIndex(internalNode, key, comparer);

                var child = internalNode.Children[index]!.EnsureEditable(owner);
                internalNode.Children[index] = child;

                bool childUnderflow = RemoveRecursive<T>(child, key, comparer, owner, out removed);

                if (removed && childUnderflow)
                {
                    return HandleUnderflow<T>(internalNode, index, owner);
                }
                return false;
            }
        }

        // ---------------------------------------------------------
        // Internal Helpers: Search
        // ---------------------------------------------------------

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int FindIndex<T>(LeafNode<T> node, T key, IComparer<T> comparer)
        {
            if (typeof(T) == typeof(int))
            {
                Span<T> keys = node.GetKeys();
                ref T firstKeyRef = ref MemoryMarshal.GetReference(keys);
                ref int firstIntRef = ref Unsafe.As<T, int>(ref firstKeyRef);
                ReadOnlySpan<int> intKeys = MemoryMarshal.CreateReadOnlySpan(ref firstIntRef, keys.Length);
                int intKey = Unsafe.As<T, int>(ref key);
                return Scanners.FindFirstGreaterOrEqualInt(intKeys, intKey);
            }
            if (typeof(T) == typeof(uint))
            {
                Span<T> keys = node.GetKeys();
                ref T firstKeyRef = ref MemoryMarshal.GetReference(keys);
                ref uint firstIntRef = ref Unsafe.As<T, uint>(ref firstKeyRef);
                ReadOnlySpan<uint> intKeys = MemoryMarshal.CreateReadOnlySpan(ref firstIntRef, keys.Length);
                uint intKey = Unsafe.As<T, uint>(ref key);
                return Scanners.FindFirstGreaterOrEqualInt(intKeys, intKey);
            }
            if (typeof(T) == typeof(long))
            {
                Span<T> keys = node.GetKeys();
                ref T firstKeyRef = ref MemoryMarshal.GetReference(keys);
                ref long firstIntRef = ref Unsafe.As<T, long>(ref firstKeyRef);
                ReadOnlySpan<long> intKeys = MemoryMarshal.CreateReadOnlySpan(ref firstIntRef, keys.Length);
                long intKey = Unsafe.As<T, long>(ref key);
                return Scanners.FindFirstGreaterOrEqualInt(intKeys, intKey);
            }
            if (typeof(T) == typeof(ulong))
            {
                Span<T> keys = node.GetKeys();
                ref T firstKeyRef = ref MemoryMarshal.GetReference(keys);
                ref ulong firstIntRef = ref Unsafe.As<T, ulong>(ref firstKeyRef);
                ReadOnlySpan<ulong> intKeys = MemoryMarshal.CreateReadOnlySpan(ref firstIntRef, keys.Length);
                ulong intKey = Unsafe.As<T, ulong>(ref key);
                return Scanners.FindFirstGreaterOrEqualInt(intKeys, intKey);
            }
            if (typeof(T) == typeof(short))
            {
                Span<T> keys = node.GetKeys();
                ref T firstKeyRef = ref MemoryMarshal.GetReference(keys);
                ref short firstIntRef = ref Unsafe.As<T, short>(ref firstKeyRef);
                ReadOnlySpan<short> intKeys = MemoryMarshal.CreateReadOnlySpan(ref firstIntRef, keys.Length);
                short intKey = Unsafe.As<T, short>(ref key);
                return Scanners.FindFirstGreaterOrEqualInt(intKeys, intKey);
            }
            if (typeof(T) == typeof(ushort))
            {
                Span<T> keys = node.GetKeys();
                ref T firstKeyRef = ref MemoryMarshal.GetReference(keys);
                ref ushort firstIntRef = ref Unsafe.As<T, ushort>(ref firstKeyRef);
                ReadOnlySpan<ushort> intKeys = MemoryMarshal.CreateReadOnlySpan(ref firstIntRef, keys.Length);
                ushort intKey = Unsafe.As<T, ushort>(ref key);
                return Scanners.FindFirstGreaterOrEqualInt(intKeys, intKey);
            }
            if (typeof(T) == typeof(float))
            {
                Span<T> keys = node.GetKeys();
                ref T firstKeyRef = ref MemoryMarshal.GetReference(keys);
                ref float firstIntRef = ref Unsafe.As<T, float>(ref firstKeyRef);
                ReadOnlySpan<float> intKeys = MemoryMarshal.CreateReadOnlySpan(ref firstIntRef, keys.Length);
                float intKey = Unsafe.As<T, float>(ref key);
                return Scanners.FindFirstGreaterOrEqualFloat(intKeys, intKey);
            }
            if (typeof(T) == typeof(double))
            {
                Span<T> keys = node.GetKeys();
                ref T firstKeyRef = ref MemoryMarshal.GetReference(keys);
                ref double firstIntRef = ref Unsafe.As<T, double>(ref firstKeyRef);
                ReadOnlySpan<double> intKeys = MemoryMarshal.CreateReadOnlySpan(ref firstIntRef, keys.Length);
                double intKey = Unsafe.As<T, double>(ref key);
                return Scanners.FindFirstGreaterOrEqualFloat(intKeys, intKey);
            }

            return BinarySearchKeys(node.GetKeys(), key, comparer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int FindRoutingIndex<T>(InternalNode<T> node, T key, IComparer<T> comparer)
        {   
            if (typeof(T) == typeof(int))
            {
                Span<T> keys = node.GetKeys();
                ref T firstKeyRef = ref MemoryMarshal.GetReference(keys);
                ref int firstIntRef = ref Unsafe.As<T, int>(ref firstKeyRef);
                ReadOnlySpan<int> intKeys = MemoryMarshal.CreateReadOnlySpan(ref firstIntRef, keys.Length);
                int intKey = Unsafe.As<T, int>(ref key);
                return Scanners.FindFirstGreaterInt(intKeys, intKey);
            }
            if (typeof(T) == typeof(uint))
            {
                Span<T> keys = node.GetKeys();
                ref T firstKeyRef = ref MemoryMarshal.GetReference(keys);
                ref uint firstIntRef = ref Unsafe.As<T, uint>(ref firstKeyRef);
                ReadOnlySpan<uint> intKeys = MemoryMarshal.CreateReadOnlySpan(ref firstIntRef, keys.Length);
                uint intKey = Unsafe.As<T, uint>(ref key);
                return Scanners.FindFirstGreaterInt(intKeys, intKey);
            }
            if (typeof(T) == typeof(long))
            {
                Span<T> keys = node.GetKeys();
                ref T firstKeyRef = ref MemoryMarshal.GetReference(keys);
                ref long firstIntRef = ref Unsafe.As<T, long>(ref firstKeyRef);
                ReadOnlySpan<long> intKeys = MemoryMarshal.CreateReadOnlySpan(ref firstIntRef, keys.Length);
                long intKey = Unsafe.As<T, long>(ref key);
                return Scanners.FindFirstGreaterInt(intKeys, intKey);
            }
            if (typeof(T) == typeof(ulong))
            {
                Span<T> keys = node.GetKeys();
                ref T firstKeyRef = ref MemoryMarshal.GetReference(keys);
                ref ulong firstIntRef = ref Unsafe.As<T, ulong>(ref firstKeyRef);
                ReadOnlySpan<ulong> intKeys = MemoryMarshal.CreateReadOnlySpan(ref firstIntRef, keys.Length);
                ulong intKey = Unsafe.As<T, ulong>(ref key);
                return Scanners.FindFirstGreaterInt(intKeys, intKey);
            }
            if (typeof(T) == typeof(short))
            {
                Span<T> keys = node.GetKeys();
                ref T firstKeyRef = ref MemoryMarshal.GetReference(keys);
                ref short firstIntRef = ref Unsafe.As<T, short>(ref firstKeyRef);
                ReadOnlySpan<short> intKeys = MemoryMarshal.CreateReadOnlySpan(ref firstIntRef, keys.Length);
                short intKey = Unsafe.As<T, short>(ref key);
                return Scanners.FindFirstGreaterInt(intKeys, intKey);
            }
            if (typeof(T) == typeof(ushort))
            {
                Span<T> keys = node.GetKeys();
                ref T firstKeyRef = ref MemoryMarshal.GetReference(keys);
                ref ushort firstIntRef = ref Unsafe.As<T, ushort>(ref firstKeyRef);
                ReadOnlySpan<ushort> intKeys = MemoryMarshal.CreateReadOnlySpan(ref firstIntRef, keys.Length);
                ushort intKey = Unsafe.As<T, ushort>(ref key);
                return Scanners.FindFirstGreaterInt(intKeys, intKey);
            }
            if (typeof(T) == typeof(float))
            {
                Span<T> keys = node.GetKeys();
                ref T firstKeyRef = ref MemoryMarshal.GetReference(keys);
                ref float firstIntRef = ref Unsafe.As<T, float>(ref firstKeyRef);
                ReadOnlySpan<float> intKeys = MemoryMarshal.CreateReadOnlySpan(ref firstIntRef, keys.Length);
                float intKey = Unsafe.As<T, float>(ref key);
                return Scanners.FindFirstGreaterFloat(intKeys, intKey);
            }
            if (typeof(T) == typeof(double))
            {
                Span<T> keys = node.GetKeys();
                ref T firstKeyRef = ref MemoryMarshal.GetReference(keys);
                ref double firstIntRef = ref Unsafe.As<T, double>(ref firstKeyRef);
                ReadOnlySpan<double> intKeys = MemoryMarshal.CreateReadOnlySpan(ref firstIntRef, keys.Length);
                double intKey = Unsafe.As<T, double>(ref key);
                return Scanners.FindFirstGreaterFloat(intKeys, intKey);
            }

            return BinaryRoutingKeys(node.GetKeys(), key, comparer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int BinarySearchKeys<T>(ReadOnlySpan<T> keys, T key, IComparer<T> comparer)
        {
            int low = 0;
            int high = keys.Length - 1;
            ref T keysRef = ref MemoryMarshal.GetReference(keys);

            while (low <= high)
            {
                int mid = low + ((high - low) >> 1);
                T midKey = Unsafe.Add(ref keysRef, mid);
                int cmp = comparer.Compare(midKey, key);
        
                if (cmp == 0) return mid;
                if (cmp < 0) low = mid + 1;
                else high = mid - 1;
            }
            return low;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int BinaryRoutingKeys<T>(ReadOnlySpan<T> keys, T key, IComparer<T> comparer)
        {
            int low = 0;
            int high = keys.Length - 1;
            ref T keysRef = ref MemoryMarshal.GetReference(keys);
            
            while (low <= high)
            {
                int mid = low + ((high - low) >> 1);
                T midKey = Unsafe.Add(ref keysRef, mid);
                int cmp = comparer.Compare(midKey, key);
                
                if (cmp <= 0) low = mid + 1;
                else high = mid - 1;
            }
            return low;
        }
        
        // ---------------------------------------------------------
        // Insertion Logic
        // ---------------------------------------------------------

        private static void InsertIntoLeaf<T>(LeafNode<T> leaf, int index, T key)
        {
            int count = leaf.Header.Count;
            if (index < count)
            {
                int moveCount = count - index;
                leaf.Keys.AsSpan(index, moveCount).CopyTo(leaf.Keys.AsSpan(index + 1));
                
            }

            leaf.Keys![index] = key;
            
            leaf.SetCount(count + 1);
        }

        private static  (Node<T>, T) SplitLeaf<T>(LeafNode<T> left, int insertIndex, T key, OwnerId owner)
        {
            var right = new LeafNode<T>(owner);
            int totalCount = left.Header.Count;

            int splitPoint = (insertIndex == totalCount) ? totalCount : (insertIndex == 0 ? 0 : totalCount / 2);
            int moveCount = totalCount - splitPoint;

            if (moveCount > 0)
            {
                left.Keys.AsSpan(splitPoint, moveCount).CopyTo(right.Keys.AsSpan(0));
                
            }

            left.SetCount(splitPoint);
            right.SetCount(moveCount);

            if (insertIndex < splitPoint || (splitPoint == 0 && insertIndex == 0))
            {
                InsertIntoLeaf(left, insertIndex, key);
            }
            else
            {
                InsertIntoLeaf(right, insertIndex - splitPoint, key);
            }

            return (right, right.Keys![0]);
        }

        private static void InsertIntoInternal<T>(InternalNode<T> node, int index, T separator, Node<T> newChild)
        {
            int count = node.Header.Count;

            if (index < count)
            {
                int moveCount = count - index;
                Span<T> keysSpan = node.Keys;
                keysSpan.Slice(index, moveCount).CopyTo(keysSpan.Slice(index + 1));
                
                Span<Node<T>> childrenSpan = node.Children!;
                childrenSpan.Slice(index + 1, moveCount).CopyTo(childrenSpan.Slice(index + 2));
            }

            node.Keys[index] = separator;
            node.Children[index + 1] = newChild;
            node.SetCount(count + 1);
        }

        private static (Node<T>, T) SplitInternal<T>(InternalNode<T> left, int insertIndex, T separator, Node<T> newChild, OwnerId owner)
        {
            var right = new InternalNode<T>(owner);
            
            int count = left.Header.Count;
            int splitPoint = count / 2;
            T upKey = left.Keys[splitPoint];
            int moveCount = count - splitPoint - 1; 

            if (moveCount > 0)
            {
                Span<T> leftKeys = left.Keys;
                Span<T> rightKeys = right.Keys;
                leftKeys.Slice(splitPoint + 1, moveCount).CopyTo(rightKeys); 

                Span<Node<T>> leftChildren = left.Children!;
                Span<Node<T>> rightChildren = right.Children!;
                leftChildren.Slice(splitPoint + 1, moveCount + 1).CopyTo(rightChildren);
            }

            left.SetCount(splitPoint);
            right.SetCount(moveCount);

            if (insertIndex <= splitPoint)
            {
                InsertIntoInternal(left, insertIndex, separator, newChild);
            }
            else
            {
                InsertIntoInternal(right, insertIndex - (splitPoint + 1), separator, newChild);
            }

            return (right, upKey);
        }

        // ---------------------------------------------------------
        // Removal Logic
        // ---------------------------------------------------------

        private static void RemoveFromLeaf<T>(LeafNode<T> leaf, int index)
        {
            int count = leaf.Header.Count;
            int moveCount = count - index - 1;

            if (moveCount > 0)
            {
                leaf.Keys.AsSpan(index + 1, moveCount).CopyTo(leaf.Keys.AsSpan(index));
                
            }

            leaf.SetCount(count - 1);
        }

        private static bool HandleUnderflow<T>(InternalNode<T> parent, int childIndex, OwnerId owner)
        {
            if (childIndex < parent.Header.Count)
            {
                var rightSibling = parent.Children[childIndex + 1]!.EnsureEditable(owner);
                parent.Children[childIndex + 1] = rightSibling;
                var leftChild = parent.Children[childIndex]!;

                if (CanBorrow(rightSibling))
                {
                    RotateLeft<T>(parent, childIndex, leftChild, rightSibling);
                    return false;
                }
                else
                {
                    Merge<T>(parent, childIndex, leftChild, rightSibling);
                    return parent.Header.Count < LeafNode<T>.MergeThreshold;
                }
            }
            else if (childIndex > 0)
            {
                var leftSibling = parent.Children[childIndex - 1]!.EnsureEditable(owner);
                parent.Children[childIndex - 1] = leftSibling;
                var rightChild = parent.Children[childIndex]!;

                if (CanBorrow(leftSibling))
                {
                    RotateRight<T>(parent, childIndex - 1, leftSibling, rightChild);
                    return false;
                }
                else
                {
                    Merge<T>(parent, childIndex - 1, leftSibling, rightChild);
                    return parent.Header.Count < LeafNode<T>.MergeThreshold;
                }
            }

            return true;
        }

        private static bool CanBorrow<T>(Node<T> node)
        {
            return node.Header.Count > 8 + 1;
        }

        private static void Merge<T>(InternalNode<T> parent, int separatorIndex, Node<T> left, Node<T> right)
        {
            if (left.IsLeaf)
            {
                var leftLeaf = left.AsLeaf();
                var rightLeaf = right.AsLeaf();

                int lCount = leftLeaf.Header.Count;
                int rCount = rightLeaf.Header.Count;
                
                rightLeaf.Keys.AsSpan(0, rCount).CopyTo(leftLeaf.Keys.AsSpan(lCount));
                

                leftLeaf.SetCount(lCount + rCount);
            }
            else
            {
                var leftInternal = left.AsInternal();
                var rightInternal = right.AsInternal();

                T separator = parent.Keys[separatorIndex];

                int lCount = leftInternal.Header.Count;
                leftInternal.Keys[lCount] = separator;

                int rCount = rightInternal.Header.Count;
                Span<T> rightKeys = rightInternal.Keys;
                Span<T> leftKeys = leftInternal.Keys;
                rightKeys.Slice(0, rCount).CopyTo(leftKeys.Slice(lCount + 1));

                Span<Node<T>> rightChildren = rightInternal.Children!;
                Span<Node<T>> leftChildren = leftInternal.Children!;
                rightChildren.Slice(0, rCount + 1).CopyTo(leftChildren.Slice(lCount + 1));

                leftInternal.SetCount(lCount + 1 + rCount);
            }

            int pCount = parent.Header.Count;
            int moveCount = pCount - separatorIndex - 1;

            if (moveCount > 0)
            {
                Span<T> parentKeys = parent.Keys;
                parentKeys.Slice(separatorIndex + 1, moveCount).CopyTo(parentKeys.Slice(separatorIndex));

                Span<Node<T>> parentChildren = parent.Children!;
                parentChildren.Slice(separatorIndex + 2, moveCount).CopyTo(parentChildren.Slice(separatorIndex + 1));
            }

            parent.SetCount(pCount - 1);
        }

        private static void RotateLeft<T>(InternalNode<T> parent, int separatorIndex, Node<T> left, Node<T> right)
        {
            if (left.IsLeaf)
            {
                var leftLeaf = left.AsLeaf();
                var rightLeaf = right.AsLeaf();

                InsertIntoLeaf(leftLeaf, leftLeaf.Header.Count, rightLeaf.Keys![0]);
                RemoveFromLeaf(rightLeaf, 0);

                parent.Keys[separatorIndex] = rightLeaf.Keys[0];
            }
            else
            {
                var leftInternal = left.AsInternal();
                var rightInternal = right.AsInternal();

                T sep = parent.Keys[separatorIndex];
                InsertIntoInternal(leftInternal, leftInternal.Header.Count, sep, rightInternal.Children[0]!);

                parent.Keys[separatorIndex] = rightInternal.Keys[0];

                int rCount = rightInternal.Header.Count;

                Span<Node<T>> rightChildren = rightInternal.Children!;
                rightChildren.Slice(1, rCount).CopyTo(rightChildren);

                if (rCount > 1)
                {
                    Span<T> rightKeys = rightInternal.Keys;
                    rightKeys.Slice(1, rCount - 1).CopyTo(rightKeys); 
                }

                rightInternal.SetCount(rCount - 1);
            }
        }

        private static void RotateRight<T>(InternalNode<T> parent, int separatorIndex, Node<T> left, Node<T> right)
        {
            if (left.IsLeaf)
            {
                var leftLeaf = left.AsLeaf();
                var rightLeaf = right.AsLeaf();
                int last = leftLeaf.Header.Count - 1;

                InsertIntoLeaf(rightLeaf, 0, leftLeaf.Keys![last]);
                RemoveFromLeaf(leftLeaf, last);

                parent.Keys[separatorIndex] = rightLeaf.Keys![0];
            }
            else
            {
                var leftInternal = left.AsInternal();
                var rightInternal = right.AsInternal();
                int last = leftInternal.Header.Count - 1;

                T sep = parent.Keys[separatorIndex];

                int rCount = rightInternal.Header.Count;

                // Shift keys right by 1
                Span<T> rightKeys = rightInternal.Keys;
                rightKeys.Slice(0, rCount).CopyTo(rightKeys.Slice(1));
                rightKeys[0] = sep;

                // Shift children right by 1
                Span<Node<T>> rightChildren = rightInternal.Children!;
                rightChildren.Slice(0, rCount + 1).CopyTo(rightChildren.Slice(1));
                rightChildren[0] = leftInternal.Children[last + 1]!;

                rightInternal.SetCount(rCount + 1);
                parent.Keys[separatorIndex] = leftInternal.Keys[last];
                leftInternal.SetCount(last);
            }
        }

        public static bool TryGetMin<T>(Node<T> root, out T key)
        {
            var current = root;
            while (!current.IsLeaf)
            {
                current = current.AsInternal().Children[0]!;
            }

            var leaf = current.AsLeaf();
            if (leaf.Header.Count == 0)
            {
                key = default!;
                return false;
            }

            key = leaf.Keys![0];
            
            return true;
        }

        public static bool TryGetMax<T>(Node<T> root, out T key)
        {
            var current = root;
            while (!current.IsLeaf)
            {
                var internalNode = current.AsInternal();
                current = internalNode.Children[internalNode.Header.Count]!;
            }

            var leaf = current.AsLeaf();
            if (leaf.Header.Count == 0)
            {
                key = default!;
                return false;
            }

            int last = leaf.Header.Count - 1;
            key = leaf.Keys![last];
            
            return true;
        }

        public static bool TryGetSuccessor<T>(Node<T> root, T key, IComparer<T> comparer, out T nextKey)
        {
            InternalNode<T>[] path = new InternalNode<T>[32];
            int[] indices = new int[32];
            int depth = 0;

            var current = root;
            while (!current.IsLeaf)
            {
                var internalNode = current.AsInternal();
                int idx = FindRoutingIndex(internalNode, key, comparer);
                path[depth] = internalNode;
                indices[depth] = idx;
                depth++;
                current = internalNode.Children[idx]!;
            }

            var leaf = current.AsLeaf();
            int index = FindIndex(leaf, key, comparer);

            if (index < leaf.Header.Count && comparer.Compare(leaf.Keys![index], key) == 0) index++;

            if (index < leaf.Header.Count)
            {
                nextKey = leaf.Keys![index];
                
                return true;
            }

            for (int i = depth - 1; i >= 0; i--)
            {
                if (indices[i] < path[i].Header.Count)
                {
                    current = path[i].Children[indices[i] + 1]!;
                    while (!current.IsLeaf)
                    {
                        current = current.AsInternal().Children[0]!;
                    }

                    var targetLeaf = current.AsLeaf();
                    nextKey = targetLeaf.Keys![0];
                    
                    return true;
                }
            }

            nextKey = default!;
            
            return false;
        }

        public static bool TryGetPredecessor<T>(Node<T> root, T key, IComparer<T> comparer, out T prevKey)
        {
            InternalNode<T>[] path = new InternalNode<T>[32];
            int[] indices = new int[32];
            int depth = 0;

            var current = root;
            while (!current.IsLeaf)
            {
                var internalNode = current.AsInternal();
                int idx = FindRoutingIndex(internalNode, key, comparer);
                path[depth] = internalNode;
                indices[depth] = idx;
                depth++;
                current = internalNode.Children[idx]!;
            }

            var leaf = current.AsLeaf();
            int index = FindIndex(leaf, key, comparer);

            if (index > 0)
            {
                prevKey = leaf.Keys![index - 1];
                
                return true;
            }

            for (int i = depth - 1; i >= 0; i--)
            {
                if (indices[i] > 0)
                {
                    current = path[i].Children[indices[i] - 1]!;
                    while (!current.IsLeaf)
                    {
                        var internalNode = current.AsInternal();
                        current = internalNode.Children[internalNode.Header.Count]!;
                    }

                    var targetLeaf = current.AsLeaf();
                    int last = targetLeaf.Header.Count - 1;
                    prevKey = targetLeaf.Keys![last];
                    
                    return true;
                }
            }

            prevKey = default!;
            
            return false;
        }
    }
}
