#nullable enable

using System;
using System.Linq;
using System.Threading.Tasks;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.Repositories.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurniSpace.Infrastructure.Tests.Repositories;

public sealed class OperationalDelayReportRepositoryTests
{
    [Fact]
    public async Task GetByProjectAsync_ReturnsMatchingPhaseOrderedByReportedAtDesc()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new OperationalDelayReportRepository(context);

        var reports = await repository.GetByProjectAsync(data.ProjectId, OperationalDelayPhase.PRODUCTION);

        Assert.Equal(2, reports.Count);
        Assert.Equal(data.NewerReportId, reports[0].OperationalDelayReportId);
        Assert.Equal("Reporter Two", reports[0].ReporterName);
        Assert.Equal(data.OlderReportId, reports[1].OperationalDelayReportId);
    }

    [Fact]
    public async Task GetDetailAsync_ReturnsProjectAndReporterNames()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new OperationalDelayReportRepository(context);

        var detail = await repository.GetDetailAsync(data.NewerReportId);

        Assert.NotNull(detail);
        Assert.Equal("Delay Project", detail!.ProjectName);
        Assert.Equal("Reporter Two", detail.ReporterName);
        Assert.Equal(OperationalDelayPhase.PRODUCTION, detail.ReportPhase);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<SeededData> SeedAsync(AppDbContext context)
    {
        var projectId = Guid.NewGuid();
        var reporterOneId = Guid.NewGuid();
        var reporterTwoId = Guid.NewGuid();
        var olderReportId = Guid.NewGuid();
        var newerReportId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        context.ProjectSet.Add(new Project
        {
            ProjectId = projectId,
            CustomerId = Guid.NewGuid(),
            ProjectName = "Delay Project",
            Status = ProjectStatus.IN_PRODUCTION
        });
        context.AccountSet.AddRange(
            new Account
            {
                AccountId = reporterOneId,
                RoleId = Guid.NewGuid(),
                Email = "reporter1@example.com",
                PasswordHash = "hash",
                FullName = "Reporter One",
                Status = AccountStatus.ACTIVE
            },
            new Account
            {
                AccountId = reporterTwoId,
                RoleId = Guid.NewGuid(),
                Email = "reporter2@example.com",
                PasswordHash = "hash",
                FullName = "Reporter Two",
                Status = AccountStatus.ACTIVE
            });
        context.OperationalDelayReportSet.AddRange(
            new OperationalDelayReport
            {
                OperationalDelayReportId = olderReportId,
                ProjectId = projectId,
                ReportPhase = OperationalDelayPhase.PRODUCTION,
                DeadlineSnapshot = new DateOnly(2026, 9, 1),
                DelayState = OperationalDelayState.AT_RISK,
                ProductionReasonCode = ProductionDelayReasonCode.OTHER,
                ReasonDetail = "Older delay",
                ReportedBy = reporterOneId,
                ReportedAt = now.AddHours(-2),
                CreatedAt = now.AddHours(-2)
            },
            new OperationalDelayReport
            {
                OperationalDelayReportId = newerReportId,
                ProjectId = projectId,
                ReportPhase = OperationalDelayPhase.PRODUCTION,
                DeadlineSnapshot = new DateOnly(2026, 9, 1),
                DelayState = OperationalDelayState.OVERDUE,
                ProductionReasonCode = ProductionDelayReasonCode.MATERIAL_DELAY,
                ReasonDetail = "Newer delay",
                ReportedBy = reporterTwoId,
                ReportedAt = now.AddHours(-1),
                CreatedAt = now.AddHours(-1)
            },
            new OperationalDelayReport
            {
                OperationalDelayReportId = Guid.NewGuid(),
                ProjectId = projectId,
                ReportPhase = OperationalDelayPhase.DELIVERY,
                DeadlineSnapshot = new DateOnly(2026, 10, 1),
                DelayState = OperationalDelayState.AT_RISK,
                DeliveryReasonCode = DeliveryDelayReasonCode.WEATHER,
                ReasonDetail = "Delivery delay",
                ReportedBy = reporterOneId,
                ReportedAt = now,
                CreatedAt = now
            });

        await context.SaveChangesAsync();
        return new SeededData(projectId, olderReportId, newerReportId);
    }

    private sealed record SeededData(Guid ProjectId, Guid OlderReportId, Guid NewerReportId);
}
