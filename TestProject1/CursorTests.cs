using System;
using System.Collections.Generic;
using System.Linq;
using OrderedSet;
using Xunit;

/// <summary>
///     The struct cursor must give the elements the enumerator gives, in order, for every
///     shape of tree: an empty root leaf, a full root leaf, several levels, and leaves that
///     removals left small.
/// </summary>
public class CursorTests
{
    private static List<T> Walk<T>(OrderedSet<T> set)
    {
        var seen = new List<T>();
        for (var c = OrderedSetModule.Cursor(set); !OrderedSetModule.CursorDone(c); c = OrderedSetModule.CursorNext(c))
            seen.Add(OrderedSetModule.CursorCurrent(c));
        return seen;
    }

    private static void AssertSameWalk<T>(OrderedSet<T> set)
    {
        var walked = Walk(set);
        Assert.Equal(set.ToList(), walked);
        Assert.Equal(set.Count, walked.Count);
    }

    private static OrderedSet<int> Ints(IEnumerable<int> elements)
    {
        var set = BaseOrderedSet<int>.CreateTransient();
        foreach (var x in elements) set.Add(x);
        return set.ToPersistent();
    }

    [Fact]
    public void Empty()
    {
        Assert.True(OrderedSetModule.CursorDone(OrderedSetModule.Cursor(OrderedSet<int>.Empty(null))));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(63)]
    [InlineData(64)]
    [InlineData(65)]
    [InlineData(1000)]
    [InlineData(200_000)]
    public void InOrder(int n)
    {
        // Shuffled, so the leaves are not all full.
        var set = Ints(Enumerable.Range(0, n).OrderBy(x => (x * 7919) % 10007));
        AssertSameWalk(set);
        Assert.Equal(Enumerable.Range(0, n), Walk(set));
    }

    [Fact]
    public void Persistent()
    {
        var set = OrderedSet<string>.Empty(null);
        for (var i = 0; i < 5000; i++) set = set.Add("k" + i);
        AssertSameWalk(set);
    }

    [Fact]
    public void AfterRemovals()
    {
        var set = Ints(Enumerable.Range(0, 20_000));
        for (var i = 0; i < 20_000; i += 3) set = set.Remove(i);
        AssertSameWalk(set);
        for (var i = 1; i < 20_000; i += 2) set = set.Remove(i);
        AssertSameWalk(set);
        foreach (var x in set.ToList()) set = set.Remove(x);
        Assert.True(OrderedSetModule.CursorDone(OrderedSetModule.Cursor(set)));
    }

    [Fact]
    public void ACursorIsAValue()
    {
        var set = Ints(Enumerable.Range(0, 500));
        var first = OrderedSetModule.Cursor(set);
        var c = first;
        for (var i = 0; i < 100; i++) c = OrderedSetModule.CursorNext(c);

        Assert.Equal(0, OrderedSetModule.CursorCurrent(first));
        Assert.Equal(100, OrderedSetModule.CursorCurrent(c));
    }
}
