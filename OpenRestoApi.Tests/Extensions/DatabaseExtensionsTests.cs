using Microsoft.Extensions.Configuration;
using OpenRestoApi.Extensions;

namespace OpenRestoApi.Tests.Extensions;

public class DatabaseExtensionsTests
{
    [Fact]
    public void GetAppConnectionString_UsesConfigValue_WhenPresent()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = "ConnStr" })
            .Build();

        Assert.Equal("ConnStr", config.GetAppConnectionString());
    }

    [Fact]
    public void GetAppConnectionString_UsesEnvVar_WhenConfigMissing()
    {
        var config = new ConfigurationBuilder().Build();
        Environment.SetEnvironmentVariable("CONNECTION_STRING", "EnvStr");

        try
        {
            Assert.Equal("EnvStr", config.GetAppConnectionString());
        }
        finally
        {
            Environment.SetEnvironmentVariable("CONNECTION_STRING", null);
        }
    }

    [Fact]
    public void GetAppConnectionString_FailsFast_NamingBothSources_WhenNeitherIsSet()
    {
        var config = new ConfigurationBuilder().Build();
        Environment.SetEnvironmentVariable("CONNECTION_STRING", null);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => config.GetAppConnectionString());

        Assert.Contains("ConnectionStrings:DefaultConnection", ex.Message, StringComparison.Ordinal);
        Assert.Contains("CONNECTION_STRING", ex.Message, StringComparison.Ordinal);
    }
}
