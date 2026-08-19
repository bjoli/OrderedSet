using System;
using System.Collections.Generic;

namespace OrderedSet;

public static class OrderedSetBuilderModule
{
    // orderedmap-builder
    public static OrderedSetBuilder<T> Empty<T>(IComparer<T>? comparer = null)
    {
        return new OrderedSetBuilder<T>(comparer);
    }

    // orderedmap-builder
    public static OrderedSetBuilder<T> Empty<T>(int capacity, IComparer<T>? comparer = null)
    {
        return new OrderedSetBuilder<T>(capacity, comparer);
    }

    // orderedmap-builder-add!
    public static OrderedSetBuilder<T> Add<T>(OrderedSetBuilder<T> builder, T key, T value)
    {
        builder.Add(key);
        return builder;
    }

    // orderedmap-builder->orderedmap
    public static OrderedSet<T> Build<T>(OrderedSetBuilder<T> builder)
    {
        return builder.Build();
    }
}
