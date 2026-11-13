namespace TestProject1;

using OrderedSet;

// Filter, which rebuilds the tree by its leaves: the result holds the right elements in order,
// changes like any set, and leaves the set as it was.
public class RebuildTests
{
    // Inserted in a shuffled order and thinned out, so nodes are not full and not equal.
    private static OrderedSet<int> Grown(int n, int seed)
    {
        var rng = new Random(seed);
        var set = OrderedSet<int>.Empty();
        var keys = Enumerable.Range(0, n).Select(i => i * 3).OrderBy(_ => rng.Next()).ToArray();
        foreach (var key in keys) set = set.Add(key);
        foreach (var key in keys.Where((_, i) => i % 3 == 0)) set = set.Remove(key);
        return set;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(64)]
    [InlineData(65)]
    [InlineData(2000)]
    [InlineData(100_000)]
    public void Filter_KeepsTheElementsInOrder(int n)
    {
        var set = Grown(n, n);
        var before = set.ToList();
        foreach (var every in new[] { 2, 7, 100, 1_000_000 })
            ConcatTests.CheckSet(OrderedSetModule.Filter(x => x % every == 0, set), before.Where(x => x % every == 0).ToList());
        Assert.Equal(before, set);
    }

    [Fact]
    public void Filter_ThatKeepsAll_IsTheSet()
    {
        var set = Grown(5000, 3);
        Assert.Same(set, OrderedSetModule.Filter(_ => true, set));
    }

    [Fact]
    public void Filter_ThatKeepsNone_IsEmpty()
    {
        var kept = OrderedSetModule.Filter(_ => false, Grown(5000, 3));
        Assert.Equal(0, kept.Count);
        Assert.Empty(kept);
    }

    [Fact]
    public void Filter_CallsTheTestInOrder()
    {
        var set = Grown(10_000, 5);
        var seen = new List<int>();
        OrderedSetModule.Filter(x => { seen.Add(x); return x % 2 == 0; }, set);
        Assert.Equal(set, seen);
    }
}
