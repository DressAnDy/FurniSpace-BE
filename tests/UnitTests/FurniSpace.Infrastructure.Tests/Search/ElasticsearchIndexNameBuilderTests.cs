#nullable enable

using System;
using FurniSpace.Infrastructure.Common.Search;
using Xunit;

namespace FurniSpace.Infrastructure.Tests.Search;

public sealed class ElasticsearchIndexNameBuilderTests
{
    [Theory]
    [InlineData("accounts", "furnispace-accounts")]
    [InlineData("furnispace-accounts", "furnispace-accounts")]
    public void Build_ReturnsExpectedIndexName(string input, string expected)
    {
        var builder = new ElasticsearchIndexNameBuilder(new ElasticsearchSettings
        {
            IndexPrefix = "furnispace"
        });

        var actual = builder.Build(input);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Build_ThrowsWhenIndexNameMissing()
    {
        var builder = new ElasticsearchIndexNameBuilder(new ElasticsearchSettings
        {
            IndexPrefix = "furniSpace"
        });

        Assert.Throws<ArgumentException>(() => builder.Build(" "));
    }
}
