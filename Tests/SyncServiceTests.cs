using AniTechou.Services.SearchProviders;
using Xunit;

namespace AniTechou.Tests;

public class SyncServiceTests
{
    // === BangumiSearchProvider.MapBangumiType ===

    [Fact]
    public void MapBangumiType_1_ReturnsManga()
    {
        Assert.Equal("Manga", BangumiSearchProvider.MapBangumiType(1));
    }

    [Fact]
    public void MapBangumiType_2_ReturnsAnime()
    {
        Assert.Equal("Anime", BangumiSearchProvider.MapBangumiType(2));
    }

    [Fact]
    public void MapBangumiType_3_ReturnsAnime()
    {
        Assert.Equal("Anime", BangumiSearchProvider.MapBangumiType(3));
    }

    [Fact]
    public void MapBangumiType_4_ReturnsGame()
    {
        Assert.Equal("Game", BangumiSearchProvider.MapBangumiType(4));
    }

    [Fact]
    public void MapBangumiType_6_ReturnsAnime()
    {
        Assert.Equal("Anime", BangumiSearchProvider.MapBangumiType(6));
    }

    [Fact]
    public void MapBangumiType_Unknown_ReturnsAnime()
    {
        Assert.Equal("Anime", BangumiSearchProvider.MapBangumiType(99));
    }
}
