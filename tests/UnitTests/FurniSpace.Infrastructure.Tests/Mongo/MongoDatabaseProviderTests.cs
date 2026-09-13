using FurniSpace.Infrastructure.Common.Mongo;
using FurniSpace.Infrastructure.Data.Mongo;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
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

    [Fact]
    public void GetDatabase_WhenConnectionStringIsInvalid_ThrowsConnectionFailed()
    {
        var provider = new MongoDatabaseProvider(Options.Create(new MongoDbSettings
        {
            ConnectionString = "not-a-valid-mongo-connection-string",
            DatabaseName = "furnispace_room_planner"
        }));

        var exception = Assert.Throws<MongoRoomPlannerException>(provider.GetDatabase);

        Assert.Equal(RoomPlannerMongoErrorCodes.ConnectionFailed, exception.ErrorCode);
        Assert.NotNull(exception.InnerException);
    }

    [Fact]
    public void GetDatabase_WhenSettingsAreValid_ReturnsLazyDatabase()
    {
        var provider = new MongoDatabaseProvider(Options.Create(new MongoDbSettings
        {
            ConnectionString = "mongodb://localhost:27018",
            DatabaseName = "furnispace_room_planner"
        }));

        var database = provider.GetDatabase();
        var sameDatabase = provider.GetDatabase();

        Assert.IsAssignableFrom<IMongoDatabase>(database);
        Assert.Same(database, sameDatabase);
        Assert.Equal("furnispace_room_planner", database.DatabaseNamespace.DatabaseName);
    }
}
