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

// Filter by the tree's leaves, with no list of all the elements. A builder of the set's size
// keeps such a list, and for a set of more than about 10000 elements it is in the large object
// heap, which made the parts of (std parallel)'s par-filter slow on many threads.
//
// - It fills new leaves one after the other with the elements it keeps, and makes internal
//   nodes over them, as BTreeSplit.Over does. When it keeps every element, it returns null and
//   the caller returns the set.
// - It visits the elements in order, so the test is called in that order.
[NonUserCode]
internal static class BTreeRebuild<T>
{
    /// <summary>The kept elements' tree and their number, or null when every element is kept.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static (Node<T>? Root, int Count) Filter(Node<T> root, Func<T, bool> keep)
    {
        var filter = new Filtering(keep);
        filter.Visit(root);
        if (!filter.Dropped) return (null, filter.Count);
        if (filter.Current.Header.Count > 0 || filter.Leaves.Count == 0) filter.Leaves.Add(filter.Current);
        return (BTreeSplit<T>.Over(filter.Leaves), filter.Count);
    }

    [NonUserCode]
    private sealed class Filtering(Func<T, bool> keep)
    {
        public readonly List<Node<T>> Leaves = new();
        public LeafNode<T> Current = new(OwnerId.None);
        public int Count;
        public bool Dropped;

        public void Visit(Node<T> node)
        {
            if (!node.IsLeaf)
            {
                foreach (var child in node.AsInternal().GetChildren()) Visit(child);
                return;
            }

            var keys = node.AsLeaf().Keys!;
            for (var i = 0; i < node.Header.Count; i++)
            {
                if (!keep(keys[i]))
                {
                    Dropped = true;
                    continue;
                }

                var at = Current.Header.Count;
                if (at == LeafNode<T>.Capacity)
                {
                    Leaves.Add(Current);
                    Current = new LeafNode<T>(OwnerId.None);
                    at = 0;
                }

                Current.Keys![at] = keys[i];
                Current.SetCount(at + 1);
                Count++;
            }
        }
    }
}
