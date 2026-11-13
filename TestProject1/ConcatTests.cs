namespace TestProject1;

using OrderedSet;

// OrderedSet.Concat: sets in order make one set that holds each element once, in order, and
// that changes like any set. The sets it took are not changed.
public class ConcatTests
{
    private static OrderedSet<int> Built(int n) =>
        OrderedSetModule.FromEnumerable(Enumerable.Range(0, n).Select(i => i * 3), null);

    private static List<int> CursorKeys(OrderedSet<int> set)
    {
        var keys = new List<int>();
        for (var c = OrderedSetModule.Cursor(set); !OrderedSetModule.CursorDone(c); c = OrderedSetModule.CursorNext(c))
            keys.Add(OrderedSetModule.CursorCurrent(c));
        return keys;
    }

    // The set holds exactly `expected`, by every way to read it, and changes like a set.
    internal static void CheckSet(OrderedSet<int> set, List<int> expected)
    {
        Assert.Equal(expected.Count, set.Count);
        Assert.Equal(expected, set);
        Assert.Equal(expected, CursorKeys(set));
        var all = expected.ToHashSet();
        foreach (var x in expected.Where((_, i) => i % 13 == 0))
        {
            Assert.True(set.Contains(x));
            Assert.Equal(all.Contains(x + 1), set.Contains(x + 1));
        }

        if (expected.Count > 0)
        {
            var mid = expected[expected.Count / 2];
            Assert.Equal(expected.Where(x => x >= mid), set.From(mid));
        }

        var s = set;
        foreach (var x in expected.Where((_, i) => i % 2 == 0)) s = s.Remove(x);
        Assert.Equal(expected.Where((_, i) => i % 2 != 0), s);
        var t = set.ToTransient();
        foreach (var x in expected) t.Remove(x);
        Assert.Equal(0, t.Count);
        foreach (var x in expected) t.Add(x);
        Assert.Equal(expected, t.ToPersistent());
        Assert.Equal(expected, set);
    }

    [Theory]
    [InlineData(0, 4)]
    [InlineData(40, 4)]
    [InlineData(2000, 7)]
    [InlineData(100_000, 96)]
    [InlineData(300_000, 1000)]
    public void Concat_OfTheParts_IsTheSet(int n, int k)
    {
        var set = Built(n);
        CheckSet(OrderedSet<int>.Concat(set.Split(k)), set.ToList());
    }

    [Theory]
    [InlineData(100_000, 96, 2)]
    [InlineData(100_000, 96, 500)]
    [InlineData(100_000, 96, 100_000)]
    [InlineData(5000, 7, 3)]
    public void Concat_OfFilteredParts(int n, int k, int every)
    {
        var set = Built(n);
        var parts = set.Split(k).Select(p => OrderedSetModule.Filter(x => x % every == 0, p)).ToArray();
        CheckSet(OrderedSet<int>.Concat(parts), set.Where(x => x % every == 0).ToList());
    }

    [Fact]
    public void Concat_OfSmallSets_JoinsTheirLeaves()
    {
        var sets = Enumerable.Range(0, 500).Select(i => Built(0).Add(i * 2).Add(i * 2 + 1)).ToArray();
        var joined = OrderedSet<int>.Concat(sets);
        CheckSet(joined, Enumerable.Range(0, 1000).ToList());
        Assert.True(OrderedSetModule.Split(joined, 1000).Length <= 1000 / 8);
    }

    [Fact]
    public void Concat_OfOneSet_IsIt()
    {
        var set = Built(100);
        Assert.Same(set, OrderedSet<int>.Concat([set]));
    }

    [Fact]
    public void Concat_OfEmptySets_IsEmpty()
    {
        var empty = OrderedSet<int>.Empty();
        var joined = OrderedSet<int>.Concat([empty, empty, empty]);
        Assert.Equal(0, joined.Count);
        Assert.Empty(joined);
        Assert.Equal(1, joined.Add(1).Count);
    }

    [Fact]
    public void Concat_RejectsSetsNotInOrder()
    {
        Assert.Throws<ArgumentException>(() => OrderedSet<int>.Concat([Built(100), Built(100)]));
        Assert.Throws<ArgumentException>(() => OrderedSet<int>.Concat([]));
    }
}
