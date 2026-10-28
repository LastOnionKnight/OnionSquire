using System;
using System.IO;
using OnionSquire.Configuration;
using OnionSquire.Models;
using OnionSquire.Services;
using Xunit;

namespace OnionSquire.Tests;

public class RetainerServiceTests
{
    [Fact]
    public void RetainerService_ReadsFromAutoRetainer_WhenAvailable()
    {
        var config = new PluginConfiguration();
        var service = new RetainerService(null, null, config, null);

        bool success = service.TryReadFromAutoRetainer(out var retainers);
        Assert.True(success);
        Assert.Equal(9, retainers.Count);

        // Verify known retainers from user's actual config
        Assert.Contains(retainers, r => r.Name == "Ravey");
        Assert.Contains(retainers, r => r.Name == "Keiphournine");
        Assert.Contains(retainers, r => r.Name == "Marketonion");
        Assert.Contains(retainers, r => r.Name == "Gothonion");
        Assert.Contains(retainers, r => r.Name == "Lathdin");
        Assert.Contains(retainers, r => r.Name == "Minoronion");
        Assert.Contains(retainers, r => r.Name == "Roeganion");
        Assert.Contains(retainers, r => r.Name == "Onionelf");
        Assert.Contains(retainers, r => r.Name == "Onionmucle");
    }

    [Fact]
    public void RetainerService_ReadsFromArr_WhenAvailable()
    {
        var config = new PluginConfiguration();
        var service = new RetainerService(null, null, config, null);

        bool success = service.TryReadFromArr(out var retainers);
        Assert.True(success);
        Assert.Equal(9, retainers.Count);
        Assert.Contains(retainers, r => r.Name == "Ravey");
        Assert.Contains(retainers, r => r.Name == "Onionelf");
    }

    [Theory]
    [InlineData(19, "PLD")]
    [InlineData(21, "WAR")]
    [InlineData(16, "MIN")]
    [InlineData(17, "BTN")]
    [InlineData(18, "FSH")]
    [InlineData(1, "GLA")]
    [InlineData(99, "Job 99")]
    public void RetainerService_ResolvesJobAbbreviations(byte jobId, string expectedAbbrev)
    {
        var config = new PluginConfiguration();
        var service = new RetainerService(null, null, config, null);

        string abbrev = service.GetJobAbbreviation(jobId);
        Assert.Equal(expectedAbbrev, abbrev);
    }

    [Fact]
    public void RetainerService_PersistsDiscoveredRetainers()
    {
        var config = new PluginConfiguration();
        config.RetainerRoles["Retainer 1"] = RetainerRole.FoodAndPotions;

        var service = new RetainerService(null, null, config, null);
        service.RefreshRetainers();

        Assert.Equal(9, service.RetainerCount);
        Assert.Equal(9, config.KnownRetainers.Count);
        // Verify migration of legacy Retainer 1 to first discovered retainer
        Assert.Equal(RetainerRole.FoodAndPotions, config.RetainerRoles["Ravey"]);
    }
}
