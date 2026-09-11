using System;
using System.Globalization;
using System.Linq;
using Xunit;
using OrderedSet;

namespace TestProject1;

/// A string set enumerates in ordinal order whatever the ambient culture is.
/// `Comparer<string>.Default` is culture-sensitive: under `sv-SE` it puts
/// "zebra" before "ärlig", under `en-US` after.
public class StringOrderTests
{
    private static readonly string[] Words = { "zebra", "ärlig", "apple", "Apple", "Ödla" };

    private static readonly string[] Ordinal = { "Apple", "apple", "zebra", "Ödla", "ärlig" };

    private static string[] MembersUnder(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var set = OrderedSetModule.Empty<string>();
            foreach (var w in Words) set = set.Add(w);
            return set.ToArray();
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void OrdinalUnderEveryCulture()
    {
        Assert.Equal(Ordinal, MembersUnder("en-US"));
        Assert.Equal(Ordinal, MembersUnder("sv-SE"));
        Assert.Equal(Ordinal, MembersUnder("tr-TR"));
    }

    [Fact]
    public void BuilderAgreesWithTheTree()
    {
        var builder = new OrderedSetBuilder<string>();
        foreach (var w in Words) builder.Add(w);
        Assert.Equal(Ordinal, builder.Build().ToArray());
    }

    /// A named comparer still wins — the ordinal default is only the default.
    [Fact]
    public void AnExplicitComparerIsHonoured()
    {
        var set = OrderedSetModule.Empty<string>(StringComparer.OrdinalIgnoreCase);
        set = set.Add("Apple").Add("apple");
        Assert.Equal(1, set.Count);
    }
}
