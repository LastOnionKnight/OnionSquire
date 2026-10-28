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
    [InlineData(43, RetainerRole.FoodAndPotions)] // Medicine
    [InlineData(44, RetainerRole.FoodAndPotions)] // Ingredients
    [InlineData(45, RetainerRole.FoodAndPotions)] // Meals
    [InlineData(46, RetainerRole.Gathering)]      // Seafood
    [InlineData(30, RetainerRole.Gathering)]      // Fishing Tackle
    [InlineData(48, RetainerRole.Crafting)]       // Metal
    [InlineData(49, RetainerRole.Crafting)]       // Lumber
    [InlineData(1, RetainerRole.None)]
    public void ItemCategorizer_CorrectlyMapsCategoriesToRoles(uint searchCatId, RetainerRole expectedRole)
    {
        var categorizer = new ItemCategorizer(null);
        var role = categorizer.CategorizeItem(searchCatId);

        Assert.Equal(expectedRole, role);
    }
}
