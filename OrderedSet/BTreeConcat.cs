/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 *
 * Copyright (c) 2026 Linus Björnstam
 *
 */

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace OrderedSet;

// Joins trees in key order into one tree, for (std parallel)'s join of the parts of a set. The
// leaves are kept, and new internal nodes are made over them, as BTreeSplit.Over makes them.
//
// - The leaves are found by a descent of each tree that visits its internal nodes only.
// - An empty leaf is dropped. A leaf below MergeThreshold is copied together with its
//   neighbour when the two fit in one leaf, so that the parts of a filter that kept little
//   make few small leaves.
// - Each tree must have only elements less than the next tree's first element. That is checked at the
//   trees' edges, one compare for each tree.
[NonUserCode]
internal static class BTreeConcat<T>
{
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static (Node<T> Root, int Count) Concat(Node<T>[] roots, IComparer<T> comparer)
    {
        var leaves = new List<Node<T>>();
        foreach (var root in roots)
        {
            var first = leaves.Count;
            Collect(root, leaves);
            if (leaves.Count == first || first == 0) continue;

            var before = leaves[first - 1].AsLeaf();
            var after = leaves[first].AsLeaf();
            if (comparer.Compare(before.Keys![before.Header.Count - 1], after.Keys![0]) >= 0)
                throw new ArgumentException("The sets are not in order: an element of one is not less than every element of the next.");
        }

        if (leaves.Count == 0) return (new LeafNode<T>(OwnerId.None), 0);

        var joined = new List<Node<T>>(leaves.Count);
        var count = 0;
        foreach (var node in leaves)
        {
            var leaf = node.AsLeaf();
            count += leaf.Header.Count;
            if (joined.Count > 0)
            {
                var last = joined[^1].AsLeaf();
                var small = last.Header.Count < LeafNode<T>.MergeThreshold
                            || leaf.Header.Count < LeafNode<T>.MergeThreshold;
                if (small && last.Header.Count + leaf.Header.Count <= LeafNode<T>.Capacity)
                {
                    joined[^1] = Both(last, leaf);
                    continue;
                }
            }

            joined.Add(leaf);
        }

        return (BTreeSplit<T>.Over(joined), count);
    }

    // The leaves under a node, in order, but for empty ones.
    private static void Collect(Node<T> node, List<Node<T>> leaves)
    {
        if (node.IsLeaf)
        {
            if (node.Header.Count > 0) leaves.Add(node);
            return;
        }

        foreach (var child in node.AsInternal().GetChildren()) Collect(child, leaves);
    }

    // A new leaf with the elements of a and then those of b.
    private static LeafNode<T> Both(LeafNode<T> a, LeafNode<T> b)
    {
        var leaf = new LeafNode<T>(OwnerId.None);
        int na = a.Header.Count, nb = b.Header.Count;
        Array.Copy(a.Keys!, 0, leaf.Keys!, 0, na);
        Array.Copy(b.Keys!, 0, leaf.Keys!, na, nb);
        leaf.SetCount(na + nb);
        return leaf;
    }
}
