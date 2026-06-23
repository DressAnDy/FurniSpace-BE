#nullable enable

using System;
using FurniSpace.Application.Common.Storage;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Storage;
using FurniSpace.Infrastructure.Storage;
using Xunit;

namespace FurniSpace.Application.Tests.Common.Storage;

public sealed class ProjectFileUploadSupportTests
{
    [Fact]
    public void BuildProjectObjectName_UsesDefaultPrefixWhenMissing()
    {
        var projectId = Guid.NewGuid();
        var settings = new FirebaseStorageSettings();

        var objectName = ProjectFileUploadSupport.BuildProjectObjectName(
            settings,
            projectId,
            "file.pdf");

        Assert.Equal($"projects/{projectId:D}/file.pdf", objectName);
    }

    [Fact]
    public void BuildProjectObjectName_TrimsCustomPrefix()
    {
        var projectId = Guid.NewGuid();
        var settings = new FirebaseStorageSettings { ProjectFilesPrefix = " /custom/ " };

        var objectName = ProjectFileUploadSupport.BuildProjectObjectName(
            settings,
            projectId,
            "file.pdf");

        Assert.Equal($"custom/{projectId:D}/file.pdf", objectName);
    }

    [Fact]
    public void BuildGeneratedFileName_UsesLowercaseExtension()
    {
        var fileId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var generated = ProjectFileUploadSupport.BuildGeneratedFileName(fileId, "Plan.PDF");

        Assert.EndsWith(".pdf", generated);
    }

    [Fact]
    public void NormalizeExtension_WithMissingExtension_ReturnsNull()
    {
        var extension = ProjectFileUploadSupport.NormalizeExtension("file");

        Assert.Null(extension);
    }

    [Fact]
    public void NormalizeExtension_TrimsLeadingDotAndLowercases()
    {
        var extension = ProjectFileUploadSupport.NormalizeExtension("Plan.PDF");

        Assert.Equal("pdf", extension);
    }

    [Fact]
    public void NormalizeContentType_WithEmptyValue_ReturnsOctetStream()
    {
        var contentType = ProjectFileUploadSupport.NormalizeContentType(null);

        Assert.Equal("application/octet-stream", contentType);
    }

    [Fact]
    public void NormalizeContentType_TrimsProvidedValue()
    {
        var contentType = ProjectFileUploadSupport.NormalizeContentType(" application/pdf ");

        Assert.Equal("application/pdf", contentType);
    }

    [Fact]
    public void NormalizeOptionalText_WithWhitespace_ReturnsNull()
    {
        var value = ProjectFileUploadSupport.NormalizeOptionalText("   ");

        Assert.Null(value);
    }

    [Fact]
    public void NormalizeOptionalText_TrimsNonEmptyValue()
    {
        var value = ProjectFileUploadSupport.NormalizeOptionalText("  note  ");

        Assert.Equal("note", value);
    }

    [Fact]
    public void ResolveVisibility_WithRequestedValue_ReturnsRequestedValue()
    {
        var visibility = ProjectFileUploadSupport.ResolveVisibility(
            FileVisibility.STAFF_ONLY,
            "SALES",
            "CUSTOMER");

        Assert.Equal(FileVisibility.STAFF_ONLY, visibility);
    }

    [Fact]
    public void ResolveVisibility_ForCustomerRole_ReturnsCustomerVisible()
    {
        var visibility = ProjectFileUploadSupport.ResolveVisibility(
            null,
            "CUSTOMER",
            "CUSTOMER");

        Assert.Equal(FileVisibility.CUSTOMER_VISIBLE, visibility);
    }

    [Fact]
    public void ResolveVisibility_ForStaffRole_ReturnsStaffOnly()
    {
        var visibility = ProjectFileUploadSupport.ResolveVisibility(
            null,
            "DESIGNER",
            "CUSTOMER");

        Assert.Equal(FileVisibility.STAFF_ONLY, visibility);
    }

    [Fact]
    public void CreateStoredFile_MapsUploadResultAndMetadata()
    {
        var fileId = Guid.NewGuid();
        var uploadedBy = Guid.NewGuid();
        var uploadedAt = DateTime.UtcNow;
        var uploadResult = new StorageUploadResult
        {
            ObjectName = "projects/file.pdf",
            PublicUrl = "https://files.example/file.pdf",
            Bucket = "bucket"
        };

        var storedFile = ProjectFileUploadSupport.CreateStoredFile(
            new StoredFileCreationRequest(
                fileId,
                uploadedBy,
                "Plan.PDF",
                $"{fileId:N}.pdf",
                uploadResult,
                "application/pdf",
                1024,
                uploadedAt));

        Assert.Equal(fileId, storedFile.FileId);
        Assert.Equal(uploadedBy, storedFile.UploadedBy);
        Assert.Equal("Plan.PDF", storedFile.OriginalFileName);
        Assert.Equal(uploadResult.PublicUrl, storedFile.FileUrl);
        Assert.Equal("pdf", storedFile.FileExtension);
        Assert.Equal(FileStatus.ACTIVE, storedFile.Status);
    }

    [Fact]
    public void CreateProjectFileLink_MapsProjectReference()
    {
        var fileLinkId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;

        var fileLink = ProjectFileUploadSupport.CreateProjectFileLink(
            new ProjectFileLinkCreationRequest(
                fileLinkId,
                fileId,
                projectId,
                FileType.FLOOR_PLAN,
                FileVisibility.CUSTOMER_VISIBLE,
                "Floor plan",
                createdBy,
                createdAt));

        Assert.Equal(fileLinkId, fileLink.FileLinkId);
        Assert.Equal(fileId, fileLink.FileId);
        Assert.Equal(ProjectFileUploadSupport.ProjectReferenceType, fileLink.ReferenceType);
        Assert.Equal(projectId, fileLink.ReferenceId);
        Assert.Equal(FileType.FLOOR_PLAN, fileLink.FileType);
        Assert.Equal("Floor plan", fileLink.Description);
    }
}
