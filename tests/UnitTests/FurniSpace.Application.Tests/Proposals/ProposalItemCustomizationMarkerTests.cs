#nullable enable

using System;
using System.Collections.Generic;
using FurniSpace.Application.Common.Proposals;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using Xunit;

namespace FurniSpace.Application.Tests.Proposals;

public sealed class ProposalItemCustomizationMarkerTests
{
    private static readonly Guid AcceptedVersionId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("Custom legs", true)]
    public void HasCustomizationNote_String_ReturnsExpected(string? note, bool expected)
    {
        Assert.Equal(expected, ProposalItemCustomizationMarker.HasCustomizationNote(note));
    }

    [Fact]
    public void HasCustomizationNote_ProposalItem_UsesNoteField()
    {
        var item = new ProposalItem { Note = "Oak finish" };
        Assert.True(ProposalItemCustomizationMarker.HasCustomizationNote(item));
    }

    [Fact]
    public void IsAcceptedCustomizationProductVersion_WhenNullOrEmptyProductVersion_ReturnsFalse()
    {
        var accepted = new HashSet<Guid> { AcceptedVersionId };

        Assert.False(ProposalItemCustomizationMarker.IsAcceptedCustomizationProductVersion(
            null,
            ProductVersionType.PROJECT_SPECIFIC,
            accepted));
        Assert.False(ProposalItemCustomizationMarker.IsAcceptedCustomizationProductVersion(
            Guid.Empty,
            ProductVersionType.PROJECT_SPECIFIC,
            accepted));
    }

    [Fact]
    public void IsAcceptedCustomizationProductVersion_WhenNotProjectSpecific_ReturnsFalse()
    {
        var accepted = new HashSet<Guid> { AcceptedVersionId };

        Assert.False(ProposalItemCustomizationMarker.IsAcceptedCustomizationProductVersion(
            AcceptedVersionId,
            ProductVersionType.STANDARD,
            accepted));
    }

    [Fact]
    public void IsAcceptedCustomizationProductVersion_WhenNotInAcceptedSet_ReturnsFalse()
    {
        Assert.False(ProposalItemCustomizationMarker.IsAcceptedCustomizationProductVersion(
            AcceptedVersionId,
            ProductVersionType.PROJECT_SPECIFIC,
            new HashSet<Guid>()));
    }

    [Fact]
    public void IsAcceptedCustomizationProductVersion_WhenProjectSpecificAndAccepted_ReturnsTrue()
    {
        var accepted = new HashSet<Guid> { AcceptedVersionId };

        Assert.True(ProposalItemCustomizationMarker.IsAcceptedCustomizationProductVersion(
            AcceptedVersionId,
            ProductVersionType.PROJECT_SPECIFIC,
            accepted));
    }

    [Fact]
    public void ResolveIsCustomized_WhenNotePresent_ReturnsTrueWithoutCheckingVersion()
    {
        var item = new ProposalItem
        {
            Note = "Note",
            ProductVersionId = AcceptedVersionId,
            IsCustomized = false
        };

        Assert.True(ProposalItemCustomizationMarker.ResolveIsCustomized(
            item,
            ProductVersionType.STANDARD,
            new HashSet<Guid>()));
    }

    [Fact]
    public void ResolveIsCustomized_WhenAcceptedProjectSpecific_ReturnsTrue()
    {
        var item = new ProposalItem
        {
            ProductVersionId = AcceptedVersionId,
            IsCustomized = false
        };
        var accepted = new HashSet<Guid> { AcceptedVersionId };

        Assert.True(ProposalItemCustomizationMarker.ResolveIsCustomized(
            item,
            ProductVersionType.PROJECT_SPECIFIC,
            accepted));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData(null)]
    public void ResolveIsCustomized_WhenNoNoteOrAcceptedVersion_UsesStoredFlag(bool? stored)
    {
        var item = new ProposalItem
        {
            ProductVersionId = Guid.NewGuid(),
            IsCustomized = stored
        };

        Assert.Equal(stored ?? false, ProposalItemCustomizationMarker.ResolveIsCustomized(
            item,
            ProductVersionType.STANDARD,
            new HashSet<Guid>()));
    }

    [Fact]
    public void ResolveIsCustomizedForQuotation_WhenAcceptedVersionId_ReturnsTrue()
    {
        var item = new ProposalItem
        {
            ProductVersionId = AcceptedVersionId,
            IsCustomized = false
        };

        Assert.True(ProposalItemCustomizationMarker.ResolveIsCustomizedForQuotation(
            item,
            new HashSet<Guid> { AcceptedVersionId }));
    }

    [Fact]
    public void ResolveIsCustomizedForQuotation_WhenNoMatch_ReturnsStoredOrFalse()
    {
        var item = new ProposalItem
        {
            ProductVersionId = Guid.NewGuid(),
            IsCustomized = null
        };

        Assert.False(ProposalItemCustomizationMarker.ResolveIsCustomizedForQuotation(
            item,
            new HashSet<Guid>()));
    }
}
