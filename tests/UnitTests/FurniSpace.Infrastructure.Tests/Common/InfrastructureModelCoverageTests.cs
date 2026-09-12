#nullable enable

using System;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Caching;
using FurniSpace.Infrastructure.Common.Email;
using FurniSpace.Infrastructure.ReadModels.ProjectFiles;
using Xunit;

namespace FurniSpace.Infrastructure.Tests.Common;

public sealed class InfrastructureModelCoverageTests
{
    [Fact]
    public void Settings_ExposeSectionNamesDefaultsAndAssignedValues()
    {
        var redis = new RedisSettings { ConnectionString = "localhost:6379" };
        var gmailApi = new GmailApiSettings
        {
            ClientId = "client-id",
            ClientSecret = "client-secret",
            RefreshToken = "refresh-token",
            BaseUrl = "https://gmail.example.com/v1/",
            TokenUrl = "https://oauth.example.com/token",
            SenderEmail = "sender@gmail.com",
            SenderName = "FurniSpace Test",
            ResetPasswordUrl = "https://example.com/reset",
            TimeoutSeconds = 15
        };

        Assert.Equal("Redis", RedisSettings.SectionName);
        Assert.Equal("localhost:6379", redis.ConnectionString);
        Assert.Equal("GmailApi", GmailApiSettings.SectionName);
        Assert.Equal("client-id", gmailApi.ClientId);
        Assert.Equal("client-secret", gmailApi.ClientSecret);
        Assert.Equal("refresh-token", gmailApi.RefreshToken);
        Assert.Equal("https://gmail.example.com/v1/", gmailApi.BaseUrl);
        Assert.Equal("https://oauth.example.com/token", gmailApi.TokenUrl);
        Assert.Equal("sender@gmail.com", gmailApi.SenderEmail);
        Assert.Equal("FurniSpace Test", gmailApi.SenderName);
        Assert.Equal("https://example.com/reset", gmailApi.ResetPasswordUrl);
        Assert.Equal(15, gmailApi.TimeoutSeconds);
    }

    [Fact]
    public void FileLinkReadModel_StoresValues()
    {
        var model = new FileLinkReadModel
        {
            FileLinkId = Guid.NewGuid(),
            FileId = Guid.NewGuid(),
            ReferenceType = "PROJECT",
            ReferenceId = Guid.NewGuid(),
            FileType = FileType.MEASUREMENT_REPORT,
            Visibility = FileVisibility.CUSTOMER_VISIBLE,
            CreatedBy = Guid.NewGuid(),
            UploadedBy = Guid.NewGuid(),
            ProjectAccess = new ProjectFileAccessReadModel { ProjectId = Guid.NewGuid() }
        };

        Assert.Equal("PROJECT", model.ReferenceType);
        Assert.Equal(FileType.MEASUREMENT_REPORT, model.FileType);
        Assert.Equal(FileVisibility.CUSTOMER_VISIBLE, model.Visibility);
        Assert.NotEqual(Guid.Empty, model.FileLinkId);
        Assert.NotEqual(Guid.Empty, model.FileId);
        Assert.NotEqual(Guid.Empty, model.ReferenceId);
        Assert.NotNull(model.CreatedBy);
        Assert.NotEqual(Guid.Empty, model.UploadedBy);
        Assert.NotNull(model.ProjectAccess);
    }
}
