using System;
using System.Linq;
using Xunit;
using OrderedSet;

namespace TestProject1;

// The vector scans take a different path with and without AVX-512. Run these once more
// under DOTNET_EnableAVX512=0 to cover the 256-bit one.
public class KeyEdgeTests
{
    // Enough elements that every leaf scan has a full vector to load.
    private static OrderedSet<T> WithSmallest<T>(T smallest, Func<int, T> element)
    {
        var set = OrderedSet<T>.Empty().Add(smallest);
        for (int i = 1; i <= 40; i++) set = set.Add(element(i));
        return set;
    }

    [Fact]
    public void TheSmallestValueOfEachIntegerTypeIsFound()
    {
        Assert.True(WithSmallest(int.MinValue, i => i).Contains(int.MinValue));
        Assert.True(WithSmallest(long.MinValue, i => (long)i).Contains(long.MinValue));
        Assert.True(WithSmallest(short.MinValue, i => (short)i).Contains(short.MinValue));
        Assert.True(WithSmallest(0u, i => (uint)i).Contains(0u));
        Assert.True(WithSmallest(0ul, i => (ulong)i).Contains(0ul));
        Assert.True(WithSmallest((ushort)0, i => (ushort)i).Contains((ushort)0));
    }

    [Fact]
    public void AddingTheSmallestValueAgainKeepsOneCopy()
    {
        Assert.Equal(41, WithSmallest(0u, i => (uint)i).Add(0u).Count);
        Assert.Equal(41, WithSmallest(int.MinValue, i => i).Add(int.MinValue).Count);
    }

    [Fact]
    public void NaNIsOneElementAndIsFound()
    {
        var set = OrderedSet<double>.Empty();
        for (int i = 0; i < 40; i++) set = set.Add(i);
        set = set.Add(double.NaN).Add(double.NaN);

        Assert.Equal(41, set.Count);
        Assert.True(set.Contains(double.NaN));
        Assert.Equal(double.NaN, set.First());
        Assert.True(set.Contains(5.0));

        set = set.Remove(double.NaN);
        Assert.Equal(40, set.Count);
        Assert.False(set.Contains(double.NaN));
    }

    [Fact]
    public void FloatNaNIsOneElementAndIsFound()
    {
        var set = OrderedSet<float>.Empty();
        for (int i = 0; i < 40; i++) set = set.Add(i);
        set = set.Add(float.NaN).Add(float.NaN);

        Assert.Equal(41, set.Count);
        Assert.True(set.Contains(float.NaN));
    }
}

public class BuilderOrderTests
{
    [Fact]
    public void AscendingInputBuildsTheSameSetAsShuffledInput()
    {
        var elements = Enumerable.Range(0, 5000).ToArray();
        var ascending = new OrderedSetBuilder<int>();
        foreach (var e in elements) ascending.Add(e);

        var shuffled = new OrderedSetBuilder<int>();
        foreach (var e in elements.OrderBy(e => (e * 7919) % 5003)) shuffled.Add(e);

        var a = ascending.Build();
        var b = shuffled.Build();
        Assert.Equal(5000, a.Count);
        Assert.Equal(elements, a);
        Assert.Equal(elements, b);
    }

    [Fact]
    public void ARepeatedElementAtTheEndIsCountedOnce()
    {
        var builder = new OrderedSetBuilder<int>();
        for (int i = 0; i < 100; i++) builder.Add(i);
        builder.Add(99);

        var set = builder.Build();
        Assert.Equal(100, set.Count);
        Assert.Equal(Enumerable.Range(0, 100), set);
    }

    [Fact]
    public void FilteringAndMappingKeepOrder()
    {
        var builder = new OrderedSetBuilder<int>();
        for (int i = 0; i < 3000; i++) builder.Add(i);
        var set = builder.Build();

        var evens = OrderedSetModule.Filter((int e) => e % 2 == 0, set);
        var negated = OrderedSetModule.Map((int e) => -e, set);

        Assert.Equal(Enumerable.Range(0, 1500).Select(i => i * 2), evens);
        Assert.Equal(Enumerable.Range(0, 3000).Select(i => i - 2999), negated);
    }

    [Fact]
    public void AddRangeTracksOrderToo()
    {
        var builder = new OrderedSetBuilder<int>();
        builder.Add(5);
        builder.AddRange(new[] { 1 });

        Assert.Equal(new[] { 1, 5 }, builder.Build());
    }
}
