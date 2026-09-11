using System.Net;
using System.Net.Http.Json;
using FurniSpace.API.IntegrationTests.Fixtures;
using FurniSpace.API.IntegrationTests.Support;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.ProjectSchedules;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Testing.Seeding;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.API.IntegrationTests.ProjectSchedules;

[Collection(ApiIntegrationCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "Core")]
public sealed class MeasurementScheduleApiIntegrationTests : IAsyncLifetime
{
    private readonly ApiIntegrationFixture _fixture;

    public MeasurementScheduleApiIntegrationTests(ApiIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.Database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreateMeasurement_ThenCustomerConfirm_ThenComplete_PersistsLifecycle()
    {
        MeasurementScenario scenario;
        await using (var context = _fixture.Database.CreateDbContext())
        {
            scenario = await MeasurementScenarioSeeder.SeedMeasurementRequiredAsync(context);
        }

        var start = ScheduleTestClock.VietnamLocalAsUtc(dayOffset: 1, hour: 8);
        using var createRequest = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Post,
            $"/projects/{scenario.ProjectId}/schedules",
            scenario.SalesAccountId,
            CoreRoles.Sales,
            new CreateProjectScheduleRequestDto
            {
                ScheduleType = ProjectScheduleType.MEASUREMENT,
                Title = "Site measurement",
                AssignedStaffId = scenario.DesignerAccountId,
                ScheduledStart = start,
                ScheduledEnd = ScheduleTestClock.VietnamLocalAsUtc(dayOffset: 1, hour: 10),
                Location = "12 Nguyen Hue"
            });
        var createResponse = await _fixture.Client.SendAsync(createRequest);
        var created = await createResponse.Content
            .ReadFromJsonAsync<ServiceResult<ProjectScheduleDto>>(IntegrationHttp.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.Equal(ProjectScheduleStatus.PENDING_CONFIRMATION, created?.Data?.Status);
        Assert.NotNull(created?.Data);

        using var confirmRequest = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Patch,
            $"/project-schedules/{created.Data.ScheduleId}/status",
            scenario.CustomerAccountId,
            CoreRoles.Customer,
            new UpdateProjectScheduleStatusRequestDto
            {
                Status = ProjectScheduleStatus.CONFIRMED
            });
        var confirmResponse = await _fixture.Client.SendAsync(confirmRequest);
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);

        await using (var advanceContext = _fixture.Database.CreateDbContext())
        {
            var scheduleToAdvance = await advanceContext.ProjectScheduleSet
                .SingleAsync(schedule => schedule.ScheduleId == created.Data.ScheduleId);
            scheduleToAdvance.ScheduledStart = DateTime.UtcNow.AddHours(-2);
            scheduleToAdvance.ScheduledEnd = DateTime.UtcNow.AddHours(-1);
            await advanceContext.SaveChangesAsync();
        }

        using var completeRequest = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Patch,
            $"/project-schedules/{created.Data.ScheduleId}/status",
            scenario.SalesAccountId,
            CoreRoles.Sales,
            new UpdateProjectScheduleStatusRequestDto
            {
                Status = ProjectScheduleStatus.COMPLETED
            });
        var completeResponse = await _fixture.Client.SendAsync(completeRequest);
        var completed = await completeResponse.Content
            .ReadFromJsonAsync<ServiceResult<ProjectScheduleDto>>(IntegrationHttp.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);
        Assert.Equal(ProjectScheduleStatus.COMPLETED, completed?.Data?.Status);
        Assert.True(completed?.Data?.CanMoveToProposalConsulting);

        await using var verification = _fixture.Database.CreateDbContext();
        var schedule = await verification.ProjectScheduleSet.SingleAsync();
        Assert.Equal(ProjectScheduleStatus.COMPLETED, schedule.Status);
        Assert.Equal(ProjectScheduleType.MEASUREMENT, schedule.ScheduleType);
        Assert.Equal(scenario.DesignerAccountId, schedule.AssignedStaffId);
    }

    [Fact]
    public async Task Confirm_AsOtherCustomer_ReturnsForbidden()
    {
        MeasurementScenario scenario;
        Guid scheduleId;
        await using (var context = _fixture.Database.CreateDbContext())
        {
            scenario = await MeasurementScenarioSeeder.SeedMeasurementRequiredAsync(
                context,
                includeOtherCustomer: true);
            var schedule = new ProjectSchedule
            {
                ScheduleId = Guid.NewGuid(),
                ProjectId = scenario.ProjectId,
                ScheduleType = ProjectScheduleType.MEASUREMENT,
                Title = "Measurement",
                AssignedStaffId = scenario.DesignerAccountId,
                ScheduledStart = DateTime.UtcNow.AddDays(1),
                ScheduledEnd = DateTime.UtcNow.AddDays(1).AddHours(2),
                Status = ProjectScheduleStatus.PENDING_CONFIRMATION,
                CreatedAt = CoreAccountSeeder.FixedTimestamp
            };
            scheduleId = schedule.ScheduleId;
            context.ProjectScheduleSet.Add(schedule);
            await context.SaveChangesAsync();
        }

        using var request = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Patch,
            $"/project-schedules/{scheduleId}/status",
            scenario.OtherCustomerAccountId!.Value,
            CoreRoles.Customer,
            new UpdateProjectScheduleStatusRequestDto
            {
                Status = ProjectScheduleStatus.CONFIRMED
            });

        var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var verification = _fixture.Database.CreateDbContext();
        var persisted = await verification.ProjectScheduleSet.SingleAsync();
        Assert.Equal(ProjectScheduleStatus.PENDING_CONFIRMATION, persisted.Status);
    }

    [Fact]
    public async Task CreateMeasurement_WhenProjectNotMeasurementRequired_ReturnsBadRequest()
    {
        ProjectConsultationScenario scenario;
        SeededAccount designer;
        await using (var context = _fixture.Database.CreateDbContext())
        {
            scenario = await ProjectScenarioSeeder.SeedInConsultationAsync(context);
            designer = await CoreAccountSeeder.SeedAccountAsync(context, CoreRoles.Designer);
        }

        using var request = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Post,
            $"/projects/{scenario.ProjectId}/schedules",
            scenario.SalesAccountId,
            CoreRoles.Sales,
            new CreateProjectScheduleRequestDto
            {
                ScheduleType = ProjectScheduleType.MEASUREMENT,
                Title = "Invalid",
                AssignedStaffId = designer.AccountId,
                ScheduledStart = ScheduleTestClock.VietnamLocalAsUtc(dayOffset: 1, hour: 8),
                ScheduledEnd = ScheduleTestClock.VietnamLocalAsUtc(dayOffset: 1, hour: 10)
            });

        var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var verification = _fixture.Database.CreateDbContext();
        Assert.Equal(0, await verification.ProjectScheduleSet.CountAsync());
    }
}
