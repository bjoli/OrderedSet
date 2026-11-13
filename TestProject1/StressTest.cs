namespace TestProject1;

using OrderedSet;

public class StressTests
{
    

    [Fact]
    public void LargeInsert_SplitsCorrectly()
    {
        var map = BaseOrderedSet<string>.CreateTransient();
        int count = 10_000;

        // 1. Insert 10k items
        for (int i = 0; i < count; i++)
        {
            // Pad with 0s to ensure consistent length sorting for simple debugging
            map.Add($"Key_{i:D6}");
        }
        

        // 2. Read back all items
        for (int i = 0; i < count; i++)
        {
            bool found = map.Contains($"Key_{i:D6}");
            Assert.True(found, $"Failed to find Key_{i:D6}");
            
        }

        // 3. Verify Non-existent
        Assert.False(map.Contains("Key_999999"));
    }

    [Fact]
    public void ReverseInsert_HandlesPrependSplits()
    {
        // Inserting in reverse order triggers the "Left/Right 90/10" split heuristic specific to prepends
        var map = BaseOrderedSet<string>.CreateTransient();
        int count = 5000;

        for (int i = count; i > 0; i--)
        {
            map.Add(i.ToString("D6"));
        }

        for (int i = 1; i <= count; i++)
        {
            Assert.True(map.Contains(i.ToString("D6")));
        }
    }

    [Fact]
    public void Random_InsertDelete_Churn()
    {
        // Fuzzing test to catch edge cases in Rebalance/Merge
        var map = BaseOrderedSet<string>.CreateTransient();
        var rng = new Random(12345);
        var reference = new HashSet<string>();

        for (int i = 0; i < 5000; i++)
        {
            string key = rng.Next(0, 1000).ToString(); // High collision chance
            int op = rng.Next(0, 3); // 0=Set, 1=Remove, 2=Check

            if (op == 0)
            {
                map.Add(key);
                reference.Add(key);
            }
            else if (op == 1)
            {
                map.Remove(key);
                reference.Remove(key);
            }
            else
            {
                bool mapHas = map.Contains(key);
                bool refHas = reference.Contains(key);
                
                Assert.Equal(refHas, mapHas);
            }
        }
        Console.WriteLine("bp");
    }
}