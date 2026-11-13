using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using OrderedSet;

// UnionAll, IntersectAll and ExceptAll give what the two-set functions give
// applied two at a time, for sets of every shape: few and many (past the
// k-way limit), small and big, the biggest anywhere, disjoint and overlapping
// keys, empty ones, and sets ordered by different comparers.
public class ManyInputsTests
{
    private static OrderedSet<int> Set(Random rng, int size, int range, IComparer<int>? order = null)
    {
        var t = OrderedSetModule.Empty(order).ToTransient();
        for (var i = 0; i < size; i++) t.Add(rng.Next(range));
        return t.ToPersistent();
    }

    private static void Same(OrderedSet<int> want, OrderedSet<int> got, string what)
    {
        Assert.True(want.Count == got.Count, $"{what}: count {got.Count}, want {want.Count}");
        Assert.True(want.SequenceEqual(got), $"{what}: keys differ");
        foreach (var k in want) Assert.True(got.Contains(k), $"{what}: key {k}");
    }

    private static List<OrderedSet<int>> Inputs(Random rng)
    {
        var count = rng.Next(4) == 0 ? rng.Next(2, 4) : rng.Next(2, 60);
        // A small range gives common keys, which an intersection needs.
        var range = rng.Next(3) == 0 ? 1_000_000 : rng.Next(50, 5000);
        var big = rng.Next(count);
        var hasBig = rng.Next(2) == 0;
        var sets = new List<OrderedSet<int>>();
        for (var i = 0; i < count; i++)
        {
            var size = hasBig && i == big ? rng.Next(5000, 20000)
                : rng.Next(6) == 0 ? 0
                : rng.Next(3) == 0 ? rng.Next(1, 10) : rng.Next(10, 3000);
            sets.Add(Set(rng, size, range));
        }
        return sets;
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void ManyGiveWhatTwoAtATimeGive(int seed)
    {
        var rng = new Random(seed);
        for (var round = 0; round < 60; round++)
        {
            var sets = Inputs(rng);
            var rest = sets.Skip(2).ToArray();
            var what = $"seed {seed} round {round}: {sets.Count} sets of {string.Join(",", sets.Select(s => s.Count))}";

            Same(sets.Aggregate(OrderedSetModule.Merge), OrderedSetModule.UnionAll(sets[0], sets[1], rest), what + ", union");
            Same(sets.Aggregate(OrderedSetModule.Intersect), OrderedSetModule.IntersectAll(sets[0], sets[1], rest), what + ", intersection");
            Same(sets.Aggregate(OrderedSetModule.Except), OrderedSetModule.ExceptAll(sets[0], sets[1], rest), what + ", difference");

            // Few sets after a big first one, where a difference removes from a transient.
            var big = Set(rng, 20000, 30000);
            Same(new[] { big, sets[0], sets[1] }.Aggregate(OrderedSetModule.Except),
                 OrderedSetModule.ExceptAll(big, sets[0], new[] { sets[1] }), what + ", difference from a big set");
        }
    }

    [Fact]
    public void SetsInAnotherOrderAreTakenTwoAtATime()
    {
        var rng = new Random(7);
        var reverse = Comparer<int>.Create((a, b) => b.CompareTo(a));
        var sets = new[] { Set(rng, 300, 500), Set(rng, 300, 500, reverse), Set(rng, 300, 500) };
        Same(sets.Aggregate(OrderedSetModule.Merge), OrderedSetModule.UnionAll(sets[0], sets[1], sets[2..]), "union");
        Same(sets.Aggregate(OrderedSetModule.Intersect), OrderedSetModule.IntersectAll(sets[0], sets[1], sets[2..]), "intersection");
        Same(sets.Aggregate(OrderedSetModule.Except), OrderedSetModule.ExceptAll(sets[0], sets[1], sets[2..]), "difference");
    }

    [Fact]
    public void EmptySets()
    {
        var empty = OrderedSetModule.Empty<int>();
        var one = Set(new Random(1), 100, 1000);
        var none = Array.Empty<OrderedSet<int>>();
        Assert.Same(one, OrderedSetModule.UnionAll(empty, one, none));
        Assert.Equal(0, OrderedSetModule.IntersectAll(one, empty, none).Count);
        Assert.Same(one, OrderedSetModule.ExceptAll(one, empty, new[] { empty }));
        Assert.Equal(0, OrderedSetModule.ExceptAll(empty, one, none).Count);
    }
}
