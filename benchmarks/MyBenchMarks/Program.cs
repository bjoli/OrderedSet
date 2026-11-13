using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using LanguageExt;
using OrderedSet;

namespace MapBenchmarks;

[MemoryDiagnoser]
public class IntSetBenchmarks
{
    [Params(100, 1000, 10000, 100000)]
    public int N { get; set; }

    private int[] _allKeys;
    private int[] _retrieveKeys;
    private int[] _updateKeys;
    private int[] _removeKeys;
    private int[] _mixedKeys;

    private ImmutableSortedSet<int> _immSortedSet;
    private LanguageExt.Set<int> _extSet;
    private OrderedSet<int> _persistentOrderedSet;

    [GlobalSetup]
    public void Setup()
    {
        var rnd = new Random(42);
        
        _allKeys = Enumerable.Range(0, N).ToArray();

        int subsetSize = Math.Max(1, N / 10);
        
        var shuffled = _allKeys.OrderBy(x => rnd.Next()).ToArray();
        _retrieveKeys = shuffled.Take(subsetSize).ToArray();
        _updateKeys = shuffled.Skip(subsetSize).Take(subsetSize).ToArray();
        _removeKeys = shuffled.Skip(subsetSize * 2).Take(subsetSize).ToArray();

        var existingHalf = shuffled.Skip(subsetSize * 3).Take(subsetSize / 2).ToArray();
        var newHalf = Enumerable.Range(N + 1, subsetSize - (subsetSize / 2)).ToArray();
        _mixedKeys = existingHalf.Concat(newHalf).OrderBy(x => rnd.Next()).ToArray();

        _immSortedSet = ImmutableSortedSet.CreateRange(_allKeys);
        
        _extSet = LanguageExt.Set.empty<int>();
        foreach (var k in _allKeys)
        {
            _extSet = _extSet.Add(k);
        }

        var transient = BaseOrderedSet<int>.CreateTransient();
        foreach (var k in _allKeys) transient.Add(k);
        _persistentOrderedSet = transient.ToPersistent();
    }

    // --- 1. BUILD ---

    [Benchmark]
    public ImmutableSortedSet<int> Build_ImmSortedSet()
    {
        var set = ImmutableSortedSet<int>.Empty;
        foreach (var k in _allKeys) set = set.Add(k);
        return set;
    }

    [Benchmark]
    public LanguageExt.Set<int> Build_ExtSet()
    {
        var set = LanguageExt.Set.empty<int>();
        foreach (var k in _allKeys) set = set.Add(k);
        return set;
    }

    [Benchmark]
    public OrderedSet<int> Build_PersistentSet()
    {
        var set = OrderedSet<int>.Create();
        foreach (var k in _allKeys) set = set.Add(k);
        return set;
    }

    [Benchmark]
    public OrderedSet<int> Build_TransientSet()
    {
        var set = BaseOrderedSet<int>.CreateTransient();
        foreach (var k in _allKeys) set.Add(k);
        return set.ToPersistent();
    }

    [Benchmark]
    public OrderedSet<int> Build_OrderedSet_Builder()
    {
        var builder = new OrderedSetBuilder<int>(_allKeys.Length);
        foreach (var k in _allKeys) builder.Add(k);
        return builder.Build();
    }
    
    [Benchmark]
    public ImmutableSortedSet<int> Build_ImmSortedSet_Builder()
    {
        var builder = ImmutableSortedSet.CreateBuilder<int>();
        foreach (var k in _allKeys) builder.Add(k);
        return builder.ToImmutable();
    }

    // --- 2. RETRIEVAL ---

    [Benchmark]
    public int Retrieve_ImmSortedSet()
    {
        int count = 0;
        foreach (var k in _retrieveKeys)
            if (_immSortedSet.Contains(k)) count++;
        return count;
    }

    [Benchmark]
    public int Retrieve_ExtSet()
    {
        int count = 0;
        foreach (var k in _retrieveKeys)
            if (_extSet.Contains(k)) count++;
        return count;
    }

    [Benchmark]
    public int Retrieve_PersistentSet()
    {
        int count = 0;
        foreach (var k in _retrieveKeys)
            if (_persistentOrderedSet.Contains(k)) count++;
        return count;
    }

    // --- 3. ADDING ---

    [Benchmark]
    public OrderedSet<int> Add_PersistentSet()
    {
        var set = _persistentOrderedSet;
        foreach (var k in _updateKeys) set = set.Add(k);
        return set;
    }

    [Benchmark]
    public OrderedSet<int> Add_TransientSet()
    {
        var transient = _persistentOrderedSet.ToTransient();
        foreach (var k in _updateKeys) transient.Add(k);
        return transient.ToPersistent();
    }
    
    [Benchmark]
    public ImmutableSortedSet<int> Add_ImmSortedSet()
    {
        var set = _immSortedSet;
        foreach (var k in _updateKeys) set = set.Add(k);
        return set;
    }

    [Benchmark]
    public ImmutableSortedSet<int> Add_ImmSortedSet_Builder()
    {
        var builder = _immSortedSet.ToBuilder();
        foreach (var k in _updateKeys) builder.Add(k);
        return builder.ToImmutable();
    }

    [Benchmark]
    public LanguageExt.Set<int> Add_ExtSet()
    {
        var set = _extSet;
        foreach (var k in _updateKeys) set = set.Add(k);
        return set;
    }

    // --- 4. ADD (MIXED) ---

    [Benchmark]
    public OrderedSet<int> AddMixed_PersistentSet()
    {
        var set = _persistentOrderedSet;
        foreach (var k in _mixedKeys) set = set.Add(k);
        return set;
    }

    [Benchmark]
    public OrderedSet<int> AddMixed_TransientSet()
    {
        var transient = _persistentOrderedSet.ToTransient();
        foreach (var k in _mixedKeys) transient.Add(k);
        return transient.ToPersistent();
    }
    
    [Benchmark]
    public ImmutableSortedSet<int> AddMixed_ImmSortedSet()
    {
        var set = _immSortedSet;
        foreach (var k in _mixedKeys) set = set.Add(k);
        return set;
    }

    [Benchmark]
    public ImmutableSortedSet<int> AddMixed_ImmSortedSet_Builder()
    {
        var builder = _immSortedSet.ToBuilder();
        foreach (var k in _mixedKeys) builder.Add(k);
        return builder.ToImmutable();
    }

    [Benchmark]
    public LanguageExt.Set<int> AddMixed_ExtSet()
    {
        var set = _extSet;
        foreach (var k in _mixedKeys) set = set.Add(k);
        return set;
    }

    // --- 5. ITERATION ---

    [Benchmark]
    public int Iterate_PersistentSet()
    {
        int sum = 0;
        foreach (var k in _persistentOrderedSet) sum += k;
        return sum;
    }
    
    [Benchmark]
    public int Iterate_ImmSortedSet()
    {
        int sum = 0;
        foreach (var k in _immSortedSet) sum += k;
        return sum;
    }

    [Benchmark]
    public int Iterate_ExtSet()
    {
        int sum = 0;
        foreach (var k in _extSet) sum += k;
        return sum;
    }

    // --- 6. REMOVAL ---

    [Benchmark]
    public OrderedSet<int> Remove_PersistentSet()
    {
        var set = _persistentOrderedSet;
        foreach (var k in _removeKeys) set = set.Remove(k);
        return set;
    }
    
    [Benchmark]
    public OrderedSet<int> Remove_TransientSet()
    {
        var transient = _persistentOrderedSet.ToTransient();
        foreach (var k in _removeKeys) transient.Remove(k);
        return transient.ToPersistent();
    }
    
    [Benchmark]
    public ImmutableSortedSet<int> Remove_ImmSortedSet()
    {
        var set = _immSortedSet;
        foreach (var k in _removeKeys) set = set.Remove(k);
        return set;
    }

    [Benchmark]
    public ImmutableSortedSet<int> Remove_ImmSortedSet_Builder()
    {
        var builder = _immSortedSet.ToBuilder();
        foreach (var k in _removeKeys) builder.Remove(k);
        return builder.ToImmutable();
    }

    [Benchmark]
    public LanguageExt.Set<int> Remove_ExtSet()
    {
        var set = _extSet;
        foreach (var k in _removeKeys) set = set.Remove(k);
        return set;
    }
    
    public static void Main(string[] args)
    {
        BenchmarkSwitcher
            .FromAssembly(typeof(IntSetBenchmarks).Assembly)
            .Run(args);
    }
}
