using FurniSpace.Infrastructure.Repositories.Repository;
using Xunit;

namespace FurniSpace.Infrastructure.Tests.Repositories;

public sealed class CatalogKeywordMatcherTests
{
    [Fact]
    public void Matches_IsCaseInsensitiveForNameAndCode()
    {
        Assert.True(CatalogKeywordMatcher.Matches("Coffee Counter", "PM-001", "coffee"));
        Assert.True(CatalogKeywordMatcher.Matches("Coffee Counter", "PM-001", "pm-001"));
        Assert.False(CatalogKeywordMatcher.Matches("Coffee Counter", "PM-001", "stool"));
    }
}
