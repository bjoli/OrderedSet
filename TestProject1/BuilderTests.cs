using System;
using System.Linq;
using Xunit;
using OrderedSet;

namespace TestProject1;

public class BuilderTests
{
    [Fact]
    public void TestBuilderCreatesCorrectTree()
    {
        var builder = new OrderedSetBuilder<int>();
        
        for (int i = 0; i < 1000; i++)
        {
            builder.Add(i);
        }

        var map = builder.Build();
        Assert.Equal(1000, map.Count);
        
        for (int i = 0; i < 1000; i++)
        {
            Assert.True(map.Contains(i));
        }
    }

    [Fact]
    public void TestBuilderHandlesDuplicatesWithStableSort()
    {
        var builder = new OrderedSetBuilder<int>();
        
        // Add duplicates out of order
        builder.Add(1);
        builder.Add(3);
        builder.Add(2);
        builder.Add(1); // Should overwrite First-1
        builder.Add(2); // Should overwrite First-2

        var map = builder.Build();
        
        Assert.Equal(3, map.Count);
        
        Assert.True(map.Contains(1));
        Assert.True(map.Contains(2));
        Assert.True(map.Contains(3));
    }
}
