#nullable enable

using System;
using System.Reflection;
using FurniSpace.Infrastructure.Common.Storage;
using Xunit;

namespace FurniSpace.Infrastructure.Tests.Storage;

public sealed class FirebaseStorageClientFactoryTests
{
    [Fact]
    public void GetEnvironmentValue_WithDoubleQuotes_StripsQuotes()
    {
        const string name = "FURNISPACE_FIREBASE_DOUBLE_QUOTED";
        try
        {
            Environment.SetEnvironmentVariable(name, "\"double-quoted\"");

            var value = InvokePrivateStatic<string?>("GetEnvironmentValue", name);

            Assert.Equal("double-quoted", value);
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    [Fact]
    public void GetEnvironmentValue_WhenMissing_ReturnsNull()
    {
        var value = InvokePrivateStatic<string?>("GetEnvironmentValue", "FURNISPACE_FIREBASE_MISSING_VALUE");

        Assert.Null(value);
    }

    [Fact]
    public void CreateCredentialFromEnvironment_WhenRequiredFieldsMissing_ReturnsNull()
    {
        const string projectIdKey = "FIREBASE_PROJECT_ID";
        const string privateKeyKey = "FIREBASE_PRIVATE_KEY";
        const string clientEmailKey = "FIREBASE_CLIENT_EMAIL";
        var previousProjectId = Environment.GetEnvironmentVariable(projectIdKey);
        var previousPrivateKey = Environment.GetEnvironmentVariable(privateKeyKey);
        var previousClientEmail = Environment.GetEnvironmentVariable(clientEmailKey);

        try
        {
            Environment.SetEnvironmentVariable(projectIdKey, null);
            Environment.SetEnvironmentVariable(privateKeyKey, null);
            Environment.SetEnvironmentVariable(clientEmailKey, null);

            var credential = InvokePrivateStatic<object?>("CreateCredentialFromEnvironment");

            Assert.Null(credential);
        }
        finally
        {
            Environment.SetEnvironmentVariable(projectIdKey, previousProjectId);
            Environment.SetEnvironmentVariable(privateKeyKey, previousPrivateKey);
            Environment.SetEnvironmentVariable(clientEmailKey, previousClientEmail);
        }
    }

    [Fact]
    public void CreateUrlSigner_WhenCredentialsMissing_ThrowsInvalidOperationException()
    {
        const string credentialsPathKey = "GOOGLE_APPLICATION_CREDENTIALS";
        const string firebaseCredentialsPathKey = "FIREBASE_CREDENTIALS_PATH";
        var previousGooglePath = Environment.GetEnvironmentVariable(credentialsPathKey);
        var previousFirebasePath = Environment.GetEnvironmentVariable(firebaseCredentialsPathKey);

        try
        {
            Environment.SetEnvironmentVariable(credentialsPathKey, null);
            Environment.SetEnvironmentVariable(firebaseCredentialsPathKey, null);

            var exception = Assert.Throws<TargetInvocationException>(() =>
                typeof(FirebaseStorageClientFactory)
                    .GetMethod("CreateUrlSigner", BindingFlags.Static | BindingFlags.NonPublic)!
                    .Invoke(null, [new FirebaseStorageSettings { Bucket = "test-bucket" }]));

            var inner = Assert.IsType<InvalidOperationException>(exception.InnerException);
            Assert.Contains("Firebase credentials are required", inner.Message, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(credentialsPathKey, previousGooglePath);
            Environment.SetEnvironmentVariable(firebaseCredentialsPathKey, previousFirebasePath);
        }
    }

    private static T InvokePrivateStatic<T>(string methodName, params object[] args)
    {
        var value = typeof(FirebaseStorageClientFactory)
            .GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, args);
        return (T)value!;
    }
}
