using System.Collections.Generic;

namespace OrderedSet;

/// <summary>
///     The static interface to <see cref="OrderedSetBuilder{T}" />: append, then build once.
///
///     A builder is the bulk path. It collects into a flat list and sorts it into a B-tree in
///     one go, where adding to the set itself pays a copy down the tree per element.
/// </summary>
public static class OrderedSetBuilderModule
{
    // orderedsetbuilder-empty
    public static OrderedSetBuilder<T> Empty<T>(IComparer<T>? comparer = null)
    {
        return new OrderedSetBuilder<T>(comparer);
    }

    // orderedsetbuilder-empty, with room reserved
    public static OrderedSetBuilder<T> Empty<T>(int capacity, IComparer<T>? comparer = null)
    {
        return new OrderedSetBuilder<T>(capacity, comparer);
    }

    // orderedset->orderedsetbuilder
    public static OrderedSetBuilder<T> FromSet<T>(OrderedSet<T> set)
    {
        var builder = new OrderedSetBuilder<T>(set.Count, set.Comparer);
        builder.AddRange(set);
        return builder;
    }

    // orderedsetbuilder-add!
    public static void Add<T>(OrderedSetBuilder<T> builder, T key)
    {
        builder.Add(key);
    }

    // orderedsetbuilder-add-range!
    public static void AddRange<T>(OrderedSetBuilder<T> builder, IEnumerable<T> range)
    {
        builder.AddRange(range);
    }

    // orderedsetbuilder-length
    public static int Count<T>(OrderedSetBuilder<T> builder)
    {
        return builder.Count;
    }

    // orderedsetbuilder->orderedset
    public static OrderedSet<T> Build<T>(OrderedSetBuilder<T> builder)
    {
        return builder.Build();
    }
}
