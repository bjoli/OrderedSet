namespace TestProject1;
using OrderedSet;
public class PersistenceTests
{
    

    [Fact]
    public void PersistentMap_IsTrulyImmutable()
    {
        var v0 = BaseOrderedSet<string>.Create();
        var v1 = v0.Add("A");
        var v2 = v1.Add("B");

        // v0 should be empty
        Assert.False(v0.Contains("A"));
        
        // v1 should have A but not B
        Assert.True(v1.Contains("A"));
        Assert.False(v1.Contains("B"));

        // v2 should have both
        Assert.True(v2.Contains("A"));
        Assert.True(v2.Contains("B"));
    }

    [Fact]
    public void TransientToPersistent_CreatesSnapshot()
    {
        var tMap = BaseOrderedSet<string>.CreateTransient();
        tMap.Add("A");

        // Create Snapshot
        var pMap = tMap.ToPersistent();

        // Mutate Transient Map further
        tMap.Add("B");
        tMap.Remove("A");

        // Assert Transient State
        Assert.False(tMap.Contains("A"));
        Assert.True(tMap.Contains("B"));

        // Assert Persistent Snapshot (Should be isolated)
        Assert.True(pMap.Contains("A")); // A should still be here
        Assert.False(pMap.Contains("B")); // B should not exist
    }

    [Fact]
    public void PersistentToTransient_DoesNotCorruptSource()
    {
        var pMap = BaseOrderedSet<string>.Create();
        pMap = pMap.Add("Fixed");

        var tMap = pMap.ToTransient();
        tMap.Add("Fixed"); // Modify shared key

        // pMap should remain 1
        Assert.True(pMap.Contains("Fixed"));
        
    }
}