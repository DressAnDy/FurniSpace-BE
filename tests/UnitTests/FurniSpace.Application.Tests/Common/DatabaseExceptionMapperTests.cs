using System;
using FurniSpace.Application.Common;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurniSpace.Application.Tests.Common;

public sealed class DatabaseExceptionMapperTests
{
    [Fact]
    public void IsProjectShowcaseCoverUniqueViolation_WhenConstraintNamePresent_ReturnsTrue()
    {
        var exception = CreateDbUpdateException(
            "duplicate key value violates unique constraint \"ux_project_showcase_media_one_cover\"");

        var result = DatabaseExceptionMapper.IsProjectShowcaseCoverUniqueViolation(exception);

        Assert.True(result);
    }

    [Fact]
    public void IsProjectShowcaseCoverUniqueViolation_WhenMessageMissing_ReturnsFalse()
    {
        var exception = new DbUpdateException(string.Empty, new Exception(string.Empty));

        var result = DatabaseExceptionMapper.IsProjectShowcaseCoverUniqueViolation(exception);

        Assert.False(result);
    }

    [Fact]
    public void IsProjectShowcaseCoverUniqueViolation_WhenDifferentConstraint_ReturnsFalse()
    {
        var exception = CreateDbUpdateException(
            "duplicate key value violates unique constraint \"file_links_file_id_reference_type_reference_id_file_type_key\"");

        var result = DatabaseExceptionMapper.IsProjectShowcaseCoverUniqueViolation(exception);

        Assert.False(result);
    }

    private static DbUpdateException CreateDbUpdateException(string innerMessage)
    {
        return new DbUpdateException("save failed", new Exception(innerMessage));
    }
}
