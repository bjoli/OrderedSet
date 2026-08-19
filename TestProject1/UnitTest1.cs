namespace TestProject1;

using Xunit;
using OrderedSet;

public class BasicTests
{
    

    [Fact]
    public void Transient_InsertAndGet_Works()
    {
        var map = BaseOrderedSet<string>.CreateTransient();

        map.Add("Apple");
        map.Add("Banana");
        map.Add("Cherry");

        Assert.True(map.Contains("Apple"));
        
        
        Assert.True(map.Contains("Banana"));
        

        Assert.False(map.Contains("Date"));
    }

    [Fact]
    public void Transient_Update_Works()
    {
        var map = BaseOrderedSet<string>.CreateTransient();
        map.Add("Key");
        map.Add("Key"); // Overwrite

        map.Contains("Key");
        
    }

    [Fact]
    public void Transient_Remove_Works()
    {
        var map = BaseOrderedSet<string>.CreateTransient();
        map.Add("A");
        map.Add("B");
        map.Add("C");

        map.Remove("B");

        Assert.True(map.Contains("A"));
        Assert.False(map.Contains("B"));
        Assert.True(map.Contains("C"));
    }

    [Fact]
    public void Transient_PrefixCollision_HandlesCollision()
    {
        // UnicodeStrategy only packs the first 4 chars.
        // "Test1" and "Test2" have the same prefix "Test".
        var map = BaseOrderedSet<string>.CreateTransient();

        map.Add("Test1");
        map.Add("Test2");

        Assert.True(map.Contains("Test1"));
        

        Assert.True(map.Contains("Test2"));
        
    }
}