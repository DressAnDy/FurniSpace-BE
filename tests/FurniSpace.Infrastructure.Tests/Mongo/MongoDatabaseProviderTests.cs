using FurniSpace.Infrastructure.Common.Mongo;
using FurniSpace.Infrastructure.Mongo;
using Microsoft.Extensions.Options;
using Xunit;

namespace FurniSpace.Infrastructure.Tests.Mongo;

public sealed class MongoDatabaseProviderTests
{
    [Fact]
    public void GetDatabase_WhenSettingsAreMissing_ThrowsConnectionFailed()
    {
        var provider = new MongoDatabaseProvider(Options.Create(new MongoDbSettings()));

        var exception = Assert.Throws<MongoRoomPlannerException>(provider.GetDatabase);

        Assert.Equal(RoomPlannerMongoErrorCodes.ConnectionFailed, exception.ErrorCode);
    }
}
