#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FurniSpace.Application.DTOs.Projects;
using FurniSpace.Application.Services.Projects;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.Repositories.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurniSpace.Application.Tests.Projects;

public sealed class ProjectPhaseDeadlineServiceTests
{
    private static readonly Guid AdminId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid CustomerId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid SalesId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid DesignerId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid ProductionId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

    [Fact]
    public async Task UpsertAsync_ReturnsDeprecatedBadRequest()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context, ProjectStatus.IN_CONSULTATION);
        var service = CreateService(context);

        var result = await service.UpsertAsync(project.ProjectId, SalesId, ValidLegacyRequest());

        Assert.Equal(400, result.Status);
        Assert.Equal(ProjectPhaseDeadlineErrorCodes.PhaseDeadlineUpsertDeprecated, result.ErrorCode);
    }

    [Fact]
    public async Task UpsertProductionDeadlineAsync_AssignedSalesCreatesAndUpdatesWithoutDuplicatingRows()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context, ProjectStatus.ORDER_CONFIRMED);
        var orderId = await SeedOrderAsync(context, project.ProjectId);
        var service = CreateService(context);

        var createResult = await service.UpsertProductionDeadlineAsync(
            project.ProjectId,
            SalesId,
            new UpsertProductionPhaseDeadlineRequestDto
            {
                ProductionDeadline = new DateOnly(2026, 9, 25)
            });
        var createdRows = await context.ProjectPhaseTimelineSet.ToListAsync();

        var updateResult = await service.UpsertProductionDeadlineAsync(
            project.ProjectId,
            SalesId,
            new UpsertProductionPhaseDeadlineRequestDto
            {
                ProductionDeadline = new DateOnly(2026, 9, 26)
            });
        var updatedRows = await context.ProjectPhaseTimelineSet.ToListAsync();

        Assert.Equal(200, createResult.Status);
        Assert.Equal(200, updateResult.Status);
        Assert.Equal(orderId, createResult.Data!.OrderId);
        Assert.Equal(ProjectPhaseType.PRODUCTION, createResult.Data.Phase);
        Assert.Single(createdRows);
        Assert.Single(updatedRows);
        Assert.Contains(updatedRows, row => row is { Phase: ProjectPhaseType.PRODUCTION, DueDate: { Year: 2026, Month: 9, Day: 26 } });
    }

    [Fact]
    public async Task UpsertProductionDeadlineAsync_ReturnsBadRequest_WhenProductionDueDateExceedsTarget()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context, ProjectStatus.ORDER_CONFIRMED);
        await SeedOrderAsync(context, project.ProjectId);
        var service = CreateService(context);

        var result = await service.UpsertProductionDeadlineAsync(
            project.ProjectId,
            SalesId,
            new UpsertProductionPhaseDeadlineRequestDto
            {
                ProductionDeadline = new DateOnly(2026, 10, 1)
            });

        Assert.Equal(400, result.Status);
        Assert.Equal(ProjectPhaseDeadlineErrorCodes.ProductionDeadlineInvalid, result.ErrorCode);
    }

    [Fact]
    public async Task UpsertProductionDeadlineAsync_ReturnsForbidden_WhenRequesterCannotManageProject()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context, ProjectStatus.ORDER_CONFIRMED);
        await SeedOrderAsync(context, project.ProjectId);
        var service = CreateService(context);

        var result = await service.UpsertProductionDeadlineAsync(
            project.ProjectId,
            DesignerId,
            new UpsertProductionPhaseDeadlineRequestDto
            {
                ProductionDeadline = new DateOnly(2026, 9, 25)
            });

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task UpsertProductionDeadlineAsync_ReturnsInvalidStatus_WhenProjectIsPreOrder()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context, ProjectStatus.PROPOSAL_CONSULTING);
        var service = CreateService(context);

        var result = await service.UpsertProductionDeadlineAsync(
            project.ProjectId,
            SalesId,
            new UpsertProductionPhaseDeadlineRequestDto
            {
                ProductionDeadline = new DateOnly(2026, 9, 25)
            });

        Assert.Equal(400, result.Status);
        Assert.Equal(ProjectPhaseDeadlineErrorCodes.InvalidProjectStatus, result.ErrorCode);
    }

    [Fact]
    public async Task StageProposalDeadlineForDesignerAssignmentAsync_UpsertsProposalTimeline()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context, ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT);
        var service = CreateService(context);

        var result = await service.StageProposalDeadlineForDesignerAssignmentAsync(
            project.ProjectId,
            SalesId,
            new DateOnly(2026, 9, 15),
            project.TargetCompletionDate);

        await context.SaveChangesAsync();
        var timeline = await context.ProjectPhaseTimelineSet.SingleAsync();

        Assert.Equal(200, result.Status);
        Assert.Equal(new DateOnly(2026, 9, 15), timeline.DueDate);
        Assert.Equal(ProjectPhaseType.PROPOSAL, timeline.Phase);
    }

    [Fact]
    public async Task HasProductionDeadlineAsync_ReturnsFalseUntilProductionTimelineExists()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context, ProjectStatus.ORDER_CONFIRMED);
        var service = CreateService(context);

        Assert.False(await service.HasProductionDeadlineAsync(project.ProjectId));

        context.ProjectPhaseTimelineSet.Add(CreateDeadline(
            project.ProjectId,
            ProjectPhaseType.PRODUCTION,
            new DateOnly(2026, 9, 25)));
        await context.SaveChangesAsync();

        Assert.True(await service.HasProductionDeadlineAsync(project.ProjectId));
    }

    [Fact]
    public async Task GetAsync_ReturnsDerivedOpenStatuses()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context, ProjectStatus.IN_CONSULTATION);
        context.ProjectPhaseTimelineSet.AddRange(
            CreateDeadline(project.ProjectId, ProjectPhaseType.PROPOSAL, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2)),
            CreateDeadline(project.ProjectId, ProjectPhaseType.PRODUCTION, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5)));
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var result = await service.GetAsync(project.ProjectId, SalesId);

        Assert.Equal(200, result.Status);
        Assert.Contains(result.Data!.Deadlines, item => item is { Phase: ProjectPhaseType.PROPOSAL, Status: "OVERDUE", OverdueDays: 2 });
        Assert.Contains(result.Data.Deadlines, item => item is { Phase: ProjectPhaseType.PRODUCTION, Status: "PLANNED", OverdueDays: 0 });
    }

    [Fact]
    public async Task GetAsync_ReturnsCompletedLateAndOnTimeStatuses()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context, ProjectStatus.IN_CONSULTATION);
        context.ProjectPhaseTimelineSet.AddRange(
            CreateDeadline(
                project.ProjectId,
                ProjectPhaseType.PROPOSAL,
                new DateOnly(2026, 9, 10),
                new DateTime(2026, 9, 10, 8, 0, 0, DateTimeKind.Utc)),
            CreateDeadline(
                project.ProjectId,
                ProjectPhaseType.PRODUCTION,
                new DateOnly(2026, 9, 25),
                new DateTime(2026, 9, 27, 8, 0, 0, DateTimeKind.Utc)));
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var result = await service.GetAsync(project.ProjectId, CustomerId);

        Assert.Equal(200, result.Status);
        Assert.Contains(result.Data!.Deadlines, item => item is { Phase: ProjectPhaseType.PROPOSAL, Status: "COMPLETED_ON_TIME", OverdueDays: 0 });
        Assert.Contains(result.Data.Deadlines, item => item is { Phase: ProjectPhaseType.PRODUCTION, Status: "COMPLETED_LATE", OverdueDays: 2 });
    }

    [Fact]
    public async Task GetAsync_AllowsAssignedProductionToRead()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context, ProjectStatus.IN_PRODUCTION);
        context.ProductionRequestSet.Add(new ProductionRequest
        {
            ProductionRequestId = Guid.NewGuid(),
            ProjectId = project.ProjectId,
            OrderId = Guid.NewGuid(),
            AssignedTo = ProductionId,
            Status = ProductionRequestStatus.IN_PRODUCTION
        });
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var result = await service.GetAsync(project.ProjectId, ProductionId);

        Assert.Equal(200, result.Status);
    }

    [Fact]
    public async Task MarkCompletedOnceAsync_SetsCompletionOnlyOnce()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context, ProjectStatus.PROPOSAL_CONSULTING);
        var deadline = CreateDeadline(project.ProjectId, ProjectPhaseType.PROPOSAL, new DateOnly(2026, 9, 10));
        context.ProjectPhaseTimelineSet.Add(deadline);
        await context.SaveChangesAsync();
        var service = CreateService(context);
        var firstCompletion = new DateTime(2026, 9, 9, 10, 0, 0, DateTimeKind.Utc);

        await service.MarkCompletedOnceAsync(project.ProjectId, ProjectPhaseType.PROPOSAL, firstCompletion);
        await context.SaveChangesAsync();
        await service.MarkCompletedOnceAsync(project.ProjectId, ProjectPhaseType.PROPOSAL, firstCompletion.AddDays(3));
        await context.SaveChangesAsync();

        Assert.Equal(firstCompletion, deadline.CompletedAt);
    }

    [Fact]
    public async Task UpsertProductionDeadlineAsync_ReturnsRequired_WhenMissingDeadline()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context, ProjectStatus.ORDER_CONFIRMED);
        await SeedOrderAsync(context, project.ProjectId);
        var service = CreateService(context);

        var result = await service.UpsertProductionDeadlineAsync(
            project.ProjectId,
            SalesId,
            new UpsertProductionPhaseDeadlineRequestDto());

        Assert.Equal(400, result.Status);
        Assert.Equal(ProjectPhaseDeadlineErrorCodes.ProductionDeadlineRequired, result.ErrorCode);
    }

    [Fact]
    public async Task UpsertProductionDeadlineAsync_ReturnsOrderRequired_WhenNoOrder()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context, ProjectStatus.ORDER_CONFIRMED);
        var service = CreateService(context);

        var result = await service.UpsertProductionDeadlineAsync(
            project.ProjectId,
            SalesId,
            new UpsertProductionPhaseDeadlineRequestDto
            {
                ProductionDeadline = new DateOnly(2026, 9, 25)
            });

        Assert.Equal(400, result.Status);
        Assert.Equal(ProjectPhaseDeadlineErrorCodes.OrderRequired, result.ErrorCode);
    }

    [Fact]
    public async Task UpsertProductionDeadlineAsync_ReturnsInvalid_WhenBeforeProposalDeadline()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context, ProjectStatus.ORDER_CONFIRMED);
        await SeedOrderAsync(context, project.ProjectId);
        context.ProjectPhaseTimelineSet.Add(CreateDeadline(
            project.ProjectId,
            ProjectPhaseType.PROPOSAL,
            new DateOnly(2026, 9, 20)));
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var result = await service.UpsertProductionDeadlineAsync(
            project.ProjectId,
            SalesId,
            new UpsertProductionPhaseDeadlineRequestDto
            {
                ProductionDeadline = new DateOnly(2026, 9, 15)
            });

        Assert.Equal(400, result.Status);
        Assert.Equal(ProjectPhaseDeadlineErrorCodes.ProductionDeadlineInvalid, result.ErrorCode);
    }

    [Fact]
    public async Task UpsertProductionDeadlineAsync_AdminCanSet()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context, ProjectStatus.ORDER_CONFIRMED);
        await SeedOrderAsync(context, project.ProjectId);
        var service = CreateService(context);

        var result = await service.UpsertProductionDeadlineAsync(
            project.ProjectId,
            AdminId,
            new UpsertProductionPhaseDeadlineRequestDto
            {
                ProductionDeadline = new DateOnly(2026, 9, 25)
            });

        Assert.Equal(200, result.Status);
        Assert.Equal("Production deadline saved successfully.", result.Message);
    }

    [Fact]
    public async Task UpsertProductionDeadlineAsync_ReturnsOnTrackStatus_WhenTimelineStarted()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context, ProjectStatus.ORDER_CONFIRMED);
        await SeedOrderAsync(context, project.ProjectId);
        var service = CreateService(context);
        var startedAt = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);

        await service.UpsertProductionDeadlineAsync(
            project.ProjectId,
            SalesId,
            new UpsertProductionPhaseDeadlineRequestDto
            {
                ProductionDeadline = new DateOnly(2026, 9, 25)
            });
        var timeline = await context.ProjectPhaseTimelineSet.SingleAsync();
        timeline.StartedAt = startedAt;
        await context.SaveChangesAsync();

        var result = await service.UpsertProductionDeadlineAsync(
            project.ProjectId,
            SalesId,
            new UpsertProductionPhaseDeadlineRequestDto
            {
                ProductionDeadline = new DateOnly(2026, 9, 26)
            });

        Assert.Equal(200, result.Status);
        Assert.Equal("ON_TRACK", result.Data!.Status);
    }

    [Fact]
    public async Task UpsertProductionDeadlineAsync_ReturnsCompletedOnTimeStatus_WhenTimelineCompleted()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context, ProjectStatus.ORDER_CONFIRMED);
        await SeedOrderAsync(context, project.ProjectId);
        var service = CreateService(context);
        var completedAt = new DateTime(2026, 9, 24, 8, 0, 0, DateTimeKind.Utc);

        await service.UpsertProductionDeadlineAsync(
            project.ProjectId,
            SalesId,
            new UpsertProductionPhaseDeadlineRequestDto
            {
                ProductionDeadline = new DateOnly(2026, 9, 25)
            });
        var timeline = await context.ProjectPhaseTimelineSet.SingleAsync();
        timeline.CompletedAt = completedAt;
        await context.SaveChangesAsync();

        var result = await service.UpsertProductionDeadlineAsync(
            project.ProjectId,
            SalesId,
            new UpsertProductionPhaseDeadlineRequestDto
            {
                ProductionDeadline = new DateOnly(2026, 9, 25)
            });

        Assert.Equal(200, result.Status);
        Assert.Equal("COMPLETED_ON_TIME", result.Data!.Status);
    }

    [Fact]
    public async Task StageProposalDeadlineForDesignerAssignmentAsync_ReturnsInvalid_WhenExceedsTarget()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context, ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT);
        var service = CreateService(context);

        var result = await service.StageProposalDeadlineForDesignerAssignmentAsync(
            project.ProjectId,
            SalesId,
            new DateOnly(2026, 10, 1),
            project.TargetCompletionDate);

        Assert.Equal(400, result.Status);
        Assert.Equal(ProjectPhaseDeadlineErrorCodes.ProposalDeadlineInvalid, result.ErrorCode);
    }

    [Fact]
    public async Task GetProductionDeadlineAsync_ReturnsDueDate()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context, ProjectStatus.ORDER_CONFIRMED);
        var dueDate = new DateOnly(2026, 9, 25);
        context.ProjectPhaseTimelineSet.Add(CreateDeadline(project.ProjectId, ProjectPhaseType.PRODUCTION, dueDate));
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var result = await service.GetProductionDeadlineAsync(project.ProjectId);

        Assert.Equal(dueDate, result);
    }

    [Fact]
    public async Task GetAsync_ReturnsForbidden_WhenRequesterCannotViewProject()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context, ProjectStatus.IN_CONSULTATION);
        var service = CreateService(context);
        var outsiderId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        var designerRoleId = (await context.RoleSet.FirstAsync(role => role.RoleName == "DESIGNER")).RoleId;
        context.AccountSet.Add(CreateAccount(outsiderId, designerRoleId, "outsider@furnispace.local"));
        await context.SaveChangesAsync();

        var result = await service.GetAsync(project.ProjectId, outsiderId);

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task GetAsync_ReturnsNotFound_WhenProjectMissing()
    {
        await using var context = CreateContext();
        await SeedProjectAsync(context, ProjectStatus.IN_CONSULTATION);
        var service = CreateService(context);

        var result = await service.GetAsync(Guid.NewGuid(), SalesId);

        Assert.Equal(404, result.Status);
    }

    [Fact]
    public async Task GetAsync_ReturnsNotPlannedMessage_WhenNoTimelinesExist()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context, ProjectStatus.IN_CONSULTATION);
        var service = CreateService(context);

        var result = await service.GetAsync(project.ProjectId, SalesId);

        Assert.Equal(200, result.Status);
        Assert.Equal("Project phase deadlines have not been planned.", result.Message);
        Assert.Empty(result.Data!.Deadlines);
    }

    [Fact]
    public async Task GetAsync_ReturnsOnTrackStatus_ForFirstOpenPhase()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context, ProjectStatus.IN_CONSULTATION);
        context.ProjectPhaseTimelineSet.Add(CreateDeadline(
            project.ProjectId,
            ProjectPhaseType.PROPOSAL,
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5)));
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var result = await service.GetAsync(project.ProjectId, SalesId);

        Assert.Equal(200, result.Status);
        Assert.Contains(result.Data!.Deadlines, item => item is { Phase: ProjectPhaseType.PROPOSAL, Status: "ON_TRACK" });
    }

    [Fact]
    public async Task UpsertProductionDeadlineAsync_ReturnsBadRequest_WhenProjectIdEmpty()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        var result = await service.UpsertProductionDeadlineAsync(
            Guid.Empty,
            SalesId,
            new UpsertProductionPhaseDeadlineRequestDto
            {
                ProductionDeadline = new DateOnly(2026, 9, 25)
            });

        Assert.Equal(400, result.Status);
    }

    [Fact]
    public async Task MarkStartedOnceAsync_SetsStartedAtOnlyOnce()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context, ProjectStatus.ORDER_CONFIRMED);
        var deadline = CreateDeadline(project.ProjectId, ProjectPhaseType.PRODUCTION, new DateOnly(2026, 9, 25));
        context.ProjectPhaseTimelineSet.Add(deadline);
        await context.SaveChangesAsync();
        var service = CreateService(context);
        var firstStartedAt = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);

        await service.MarkStartedOnceAsync(project.ProjectId, ProjectPhaseType.PRODUCTION, firstStartedAt);
        await context.SaveChangesAsync();
        await service.MarkStartedOnceAsync(project.ProjectId, ProjectPhaseType.PRODUCTION, firstStartedAt.AddDays(2));
        await context.SaveChangesAsync();

        Assert.Equal(firstStartedAt, deadline.StartedAt);
    }

    private static ProjectPhaseDeadlineService CreateService(AppDbContext context)
    {
        return new ProjectPhaseDeadlineService(
            new ProjectRepository(context),
            new ProjectPhaseTimelineRepository(context),
            new ProductionRequestRepository(context),
            new OrderRepository(context),
            new UnitOfWork(context));
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<Project> SeedProjectAsync(AppDbContext context, ProjectStatus status)
    {
        var roleIds = new Dictionary<string, Guid>
        {
            ["ADMIN"] = Guid.NewGuid(),
            ["CUSTOMER"] = Guid.NewGuid(),
            ["SALES"] = Guid.NewGuid(),
            ["DESIGNER"] = Guid.NewGuid(),
            ["PRODUCTION"] = Guid.NewGuid()
        };
        context.RoleSet.AddRange(roleIds.Select(role => new Role { RoleId = role.Value, RoleName = role.Key }));
        context.AccountSet.AddRange(
            CreateAccount(AdminId, roleIds["ADMIN"], "admin@furnispace.local"),
            CreateAccount(CustomerId, roleIds["CUSTOMER"], "customer@furnispace.local"),
            CreateAccount(SalesId, roleIds["SALES"], "sales@furnispace.local"),
            CreateAccount(DesignerId, roleIds["DESIGNER"], "designer@furnispace.local"),
            CreateAccount(ProductionId, roleIds["PRODUCTION"], "production@furnispace.local"));

        var project = new Project
        {
            ProjectId = Guid.NewGuid(),
            CustomerId = CustomerId,
            AssignedSalesId = SalesId,
            AssignedDesignerId = DesignerId,
            ProjectName = "Phase deadline project",
            FurnitureRequirement = "Counters and lights",
            BusinessType = "Cafe",
            TargetCompletionDate = new DateOnly(2026, 9, 30),
            Status = status
        };
        context.ProjectSet.Add(project);
        await context.SaveChangesAsync();
        return project;
    }

    private static async Task<Guid> SeedOrderAsync(AppDbContext context, Guid projectId)
    {
        var orderId = Guid.NewGuid();
        context.OrderSet.Add(new Order
        {
            OrderId = orderId,
            ProjectId = projectId,
            CustomerId = CustomerId,
            QuotationId = Guid.NewGuid(),
            OrderCode = "ORD-TEST-001",
            Status = OrderStatus.CREATED,
            DepositAmount = 100m,
            FinalTotalAmount = 500m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        return orderId;
    }

    private static Account CreateAccount(Guid accountId, Guid roleId, string email)
    {
        return new Account
        {
            AccountId = accountId,
            RoleId = roleId,
            Email = email,
            PasswordHash = "hash",
            FullName = email,
            Status = AccountStatus.ACTIVE
        };
    }

    private static UpsertProjectPhaseDeadlinesRequestDto ValidLegacyRequest()
    {
        return new UpsertProjectPhaseDeadlinesRequestDto
        {
            ProposalDueDate = new DateOnly(2026, 9, 10),
            ProductionDueDate = new DateOnly(2026, 9, 25)
        };
    }

    private static ProjectPhaseTimeline CreateDeadline(
        Guid projectId,
        ProjectPhaseType phase,
        DateOnly dueDate,
        DateTime? completedAt = null)
    {
        return new ProjectPhaseTimeline
        {
            ProjectPhaseTimelineId = Guid.NewGuid(),
            ProjectId = projectId,
            Phase = phase,
            DueDate = dueDate,
            CompletedAt = completedAt,
            CreatedBy = SalesId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
