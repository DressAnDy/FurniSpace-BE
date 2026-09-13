#nullable enable

using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using FurniSpace.Infrastructure.Common.Storage;
using Xunit;

namespace FurniSpace.Infrastructure.Tests.Storage;

public sealed class FirebaseStorageServiceTests
{
    [Fact]
    public async Task UploadAndDelete_WhenBucketMissing_ThrowBeforeCallingStorageClient()
    {
        var service = CreateServiceWithoutConstructor(new FirebaseStorageSettings());

        var uploadException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UploadAsync(new StorageUploadRequest
            {
                Content = Stream.Null,
                ObjectName = "projects/file.pdf",
                ContentType = "application/pdf"
            }));
        var deleteException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DeleteAsync("projects/file.pdf"));

        Assert.Contains("Firebase storage bucket is missing", uploadException.Message);
        Assert.Contains("Firebase storage bucket is missing", deleteException.Message);
    }

    [Fact]
    public void BuildFirebaseDownloadUrl_EncodesObjectName()
    {
        var url = InvokePrivateStatic<string>(
            "BuildFirebaseDownloadUrl",
            "bucket-name",
            "projects/a b/file.pdf",
            "token-123");

        Assert.Equal(
            "https://firebasestorage.googleapis.com/v0/b/bucket-name/o/projects%2Fa%20b%2Ffile.pdf?alt=media&token=token-123",
            url);
    }

    [Fact]
    public void GetEnvironmentValue_TrimsQuotesAndWhitespace()
    {
        const string name = "FURNISPACE_FIREBASE_TEST_VALUE";
        try
        {
            Environment.SetEnvironmentVariable(name, " 'quoted value' ");

            var value = InvokePrivateStatic<string?>("GetEnvironmentValue", name);

            Assert.Equal("quoted value", value);
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    [Fact]
    public void FirebaseStorageSettings_DefaultsMatchExpectedPrefixes()
    {
        var settings = new FirebaseStorageSettings();

        Assert.Equal("FirebaseStorage", FirebaseStorageSettings.SectionName);
        Assert.Equal("projects", settings.ProjectFilesPrefix);
        Assert.Equal("products", settings.ProductFilesPrefix);
        Assert.Equal("product-versions", settings.ProductVersionFilesPrefix);
        Assert.Equal(50 * 1024 * 1024, settings.MaxFileSizeBytes);
    }

    private static FirebaseStorageService CreateServiceWithoutConstructor(FirebaseStorageSettings settings)
    {
        var service = (FirebaseStorageService)RuntimeHelpers.GetUninitializedObject(typeof(FirebaseStorageService));
        typeof(FirebaseStorageService)
            .GetField("_settings", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(service, settings);
        return service;
    }

    private static T InvokePrivateStatic<T>(string methodName, params object[] args)
    {
        var value = typeof(FirebaseStorageService)
            .GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, args);
        return (T)value!;
    }
}
