using AniTechou.Services;
using Xunit;

namespace AniTechou.Tests;

public class NetworkClientFactoryTests
{
    [Theory]
    [InlineData(null, "System")]
    [InlineData("", "System")]
    [InlineData("system", "System")]
    [InlineData("custom", "Custom")]
    [InlineData("none", "None")]
    [InlineData("garbage", "System")]
    public void NormalizeProxyMode_ReturnsSupportedMode(string input, string expected)
    {
        Assert.Equal(expected, NetworkClientFactory.NormalizeProxyMode(input));
    }

    [Fact]
    public void TryNormalizeProxyAddress_RequiresHttpOrSocksScheme()
    {
        Assert.True(NetworkClientFactory.TryNormalizeProxyAddress("127.0.0.1:7890", out var normalized));
        Assert.Equal("http://127.0.0.1:7890", normalized);

        Assert.True(NetworkClientFactory.TryNormalizeProxyAddress("socks5://127.0.0.1:7891", out normalized));
        Assert.Equal("socks5://127.0.0.1:7891", normalized);

        Assert.False(NetworkClientFactory.TryNormalizeProxyAddress("not a url", out normalized));
        Assert.Equal("", normalized);
    }
}
