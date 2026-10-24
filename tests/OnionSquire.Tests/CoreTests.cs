using OnionSquire.Models;
using OnionSquire.Services;
using Xunit;

namespace OnionSquire.Tests;

public class CoreTests
{
    [Fact]
    public void ArmoireResolver_CorrectlyMatchesRegisteredItem()
    {
        var resolver = new ArmoireResolver(null);
        resolver.RegisterTestItem(12345);

        Assert.True(resolver.IsArmoireEligible(12345));
        Assert.False(resolver.IsArmoireEligible(99999));
    }

    [Theory]
    [InlineData(44, RetainerRole.Crafting)]
    [InlineData(45, RetainerRole.Crafting)]
    [InlineData(42, RetainerRole.FoodAndPotions)]
    [InlineData(43, RetainerRole.FoodAndPotions)]
    [InlineData(55, RetainerRole.Gathering)]
    [InlineData(56, RetainerRole.Gathering)]
    [InlineData(1, RetainerRole.None)]
    public void ItemCategorizer_CorrectlyMapsCategoriesToRoles(uint searchCatId, RetainerRole expectedRole)
    {
        var categorizer = new ItemCategorizer(null);
        var role = categorizer.CategorizeItem(searchCatId);

        Assert.Equal(expectedRole, role);
    }
}
