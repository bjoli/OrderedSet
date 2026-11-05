namespace TestProject1;

using OrderedSet;

// OrderedSet.Split: the parts hold each element once in order, are sets of their own, and
// leave the set as it was.
public class SplitTests
{
    private static OrderedSet<int> Built(int n) =>
        OrderedSetModule.FromEnumerable(Enumerable.Range(0, n).Select(i => i * 3), null);

    // Inserted in a shuffled order and thinned out, so nodes are not full and not equal.
    private static (OrderedSet<int> Map, int[] Keys) Grown(int n, int seed)
    {
        var rng = new Random(seed);
        var map = OrderedSet<int>.Empty();
        var keys = Enumerable.Range(0, n).Select(i => i * 3).OrderBy(_ => rng.Next()).ToArray();
        foreach (var key in keys) map = map.Add(key);
        foreach (var key in keys.Where((_, i) => i % 3 == 0)) map = map.Remove(key);
        return (map, map.ToList().ToArray());
    }

    private static List<int> CursorKeys(OrderedSet<int> map)
    {
        var keys = new List<int>();
        for (var c = OrderedSetModule.Cursor(map); !OrderedSetModule.CursorDone(c); c = OrderedSetModule.CursorNext(c))
            keys.Add(OrderedSetModule.CursorCurrent(c));
        return keys;
    }

    private static void CheckParts(OrderedSet<int> map, int k)
    {
        var all = map.ToList().ToList();
        var parts = map.Split(k);
        Assert.InRange(parts.Length, 1, Math.Max(1, k));

        var joined = new List<int>();
        foreach (var part in parts)
        {
            var keys = part.ToList().ToList();
            Assert.Equal(keys.Count, part.Count);
            Assert.Equal(keys, CursorKeys(part));
            foreach (var key in keys.Where((_, i) => i % 17 == 0))
            {
                Assert.True(part.Contains(key));
                Assert.False(part.Contains(key + 1));
            }

            if (keys.Count > 0)
            {
                Assert.True(part.TryGetMin(out var min));
                Assert.True(part.TryGetMax(out var max));
                Assert.Equal(keys[0], min);
                Assert.Equal(keys[^1], max);
                Assert.Equal(keys.Where(x => x >= keys[keys.Count / 2]), part.From(keys[keys.Count / 2]));
            }

            if (joined.Count > 0 && keys.Count > 0) Assert.True(joined[^1] < keys[0]);
            joined.AddRange(keys);
        }

        Assert.Equal(all, joined);

        // A key of another part is not found.
        if (parts.Length > 1)
        {
            var first = parts[0].First();
            Assert.False(parts[1].Contains(first));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(64)]
    [InlineData(65)]
    [InlineData(2000)]
    [InlineData(5000)]
    [InlineData(100_000)]
    [InlineData(300_000)]
    public void Split_PartsHoldEachEntryOnceInOrder(int n)
    {
        var map = Built(n);
        foreach (var k in new[] { 1, 2, 3, 7, 96, 1000 }) CheckParts(map, k);
    }

    [Theory]
    [InlineData(3000, 1)]
    [InlineData(50_000, 2)]
    [InlineData(200_000, 3)]
    public void Split_OfAGrownTree(int n, int seed)
    {
        var (map, _) = Grown(n, seed);
        foreach (var k in new[] { 2, 5, 96, 1000 }) CheckParts(map, k);
    }

    [Fact]
    public void Split_PartsAreClose()
    {
        var map = Built(300_000);
        foreach (var k in new[] { 2, 8, 96 })
        {
            var parts = map.Split(k);
            Assert.Equal(k, parts.Length);
            Assert.All(parts, p => Assert.InRange(p.Count, 300_000 / k / 2, 300_000 / k * 2));
        }
    }

    [Fact]
    public void Split_OneOrALeafIsTheSet()
    {
        var map = Built(1000);
        Assert.Same(map, Assert.Single(map.Split(1)));
        Assert.Same(map, Assert.Single(map.Split(0)));
        var small = Built(40);
        Assert.Same(small, Assert.Single(small.Split(8)));
        var empty = OrderedSet<int>.Empty();
        Assert.Same(empty, Assert.Single(empty.Split(8)));
    }

    [Fact]
    public void Split_PartsChangeLikeSets()
    {
        var (map, keys) = Grown(60_000, 7);
        var parts = map.Split(16);
        Assert.True(parts.Length > 1);
        foreach (var part in parts)
        {
            var own = part.ToList().ToArray();
            var p = part;
            foreach (var key in own.Where((_, i) => i % 2 == 0)) p = p.Remove(key);
            p = p.Add(-1).Add(int.MaxValue);
            var expected = own.Where((_, i) => i % 2 != 0).Prepend(-1).Append(int.MaxValue).ToList();
            Assert.Equal(expected, p.ToList());
            Assert.Equal(expected.Count, p.Count);

            // Down to nothing and back, in a transient.
            var t = part.ToTransient();
            foreach (var key in own) t.Remove(key);
            Assert.Equal(0, t.Count);
            foreach (var key in own) t.Add(key);
            Assert.Equal(own, t.ToPersistent().ToList());
            Assert.Equal(own, part.ToList());
        }

        Assert.Equal(keys, map.ToList());
    }
}
