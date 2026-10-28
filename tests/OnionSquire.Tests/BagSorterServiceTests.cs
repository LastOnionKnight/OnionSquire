using System;
using System.Collections.Generic;
using OnionSquire.Configuration;
using OnionSquire.Models;
using OnionSquire.Services;
using Xunit;

namespace OnionSquire.Tests;

public class BagSorterServiceTests
{
    [Fact]
    public void BagSorter_InitializesInReadyState()
    {
        var config = new PluginConfiguration();
        var categorizer = new ItemCategorizer(null);
        var armoireResolver = new ArmoireResolver(null);
        var retainerService = new RetainerService(null, null, config, null);
        var sorter = new BagSorterService(config, categorizer, armoireResolver, retainerService, new AutoRetainerIpc(null));

        Assert.False(sorter.IsSorting);
        Assert.Equal("Ready", sorter.StatusMessage);
    }

    [Fact]
    public void BagSorter_ExecuteSort_WhenNoRetainerSummoned_ReportsHelpfulStatus()
    {
        var config = new PluginConfiguration();
        config.RetainerRoles["Ravey"] = RetainerRole.FoodAndPotions;

        var categorizer = new ItemCategorizer(null);
        var armoireResolver = new ArmoireResolver(null);
        var retainerService = new RetainerService(null, null, config, null);
        var sorter = new BagSorterService(config, categorizer, armoireResolver, retainerService, new AutoRetainerIpc(null));

        bool started = sorter.ExecuteSort();
        Assert.False(started);
        Assert.Contains("No items in your bags", sorter.StatusMessage);
    }

    [Fact]
    public void BagSorter_ArmoireWithdrawal_WhenNoRetainerSummoned_ReturnsZero()
    {
        var config = new PluginConfiguration();
        var categorizer = new ItemCategorizer(null);
        var armoireResolver = new ArmoireResolver(null);
        var retainerService = new RetainerService(null, null, config, null);
        var sorter = new BagSorterService(config, categorizer, armoireResolver, retainerService, new AutoRetainerIpc(null));

        int drawn = sorter.DrawArmoireFromActiveRetainer();
        Assert.Equal(0, drawn);
    }

    [Theory]
    [InlineData(43, RetainerRole.FoodAndPotions)] // Medicine
    [InlineData(45, RetainerRole.FoodAndPotions)] // Meals
    [InlineData(46, RetainerRole.Gathering)]      // Seafood
    [InlineData(47, RetainerRole.Crafting)]       // Stone
    [InlineData(48, RetainerRole.Crafting)]       // Metal
    [InlineData(49, RetainerRole.Crafting)]       // Lumber
    [InlineData(50, RetainerRole.Crafting)]       // Cloth
    [InlineData(51, RetainerRole.Crafting)]       // Leather
    [InlineData(57, RetainerRole.Miscellaneous)]  // Materia
    [InlineData(31, RetainerRole.GearAndGlamour)]  // Head
    [InlineData(10, RetainerRole.GearAndGlamour)]  // Gladiator's Arms
    public void ItemCategorizer_CoversFullCategoryMatrix(uint searchCat, RetainerRole expected)
    {
        var categorizer = new ItemCategorizer(null);
        var role = categorizer.CategorizeItem(searchCat);
        Assert.Equal(expected, role);
    }
}
