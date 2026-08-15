#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.Common.Projects;
using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Application.Interfaces.ProjectChats;
using FurniSpace.Application.Interfaces.Search;
using FurniSpace.Application.Services.Projects;
using FurniSpace.Application.Tests.TestDoubles;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Interfaces;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.ReadModels.Payments;
using FurniSpace.Infrastructure.ReadModels.Projects;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.Extensions.Options;

namespace FurniSpace.Application.Tests.Projects;

internal sealed class ProjectServiceFactoryOptions
{
    public INotificationDispatcher? Dispatcher { get; init; }

    public IProjectChatService? ProjectChats { get; init; }

    public ProjectServiceTransitionFakes? TransitionFakes { get; init; }

    public ISearchIndexService? Search { get; init; }

    public IProjectSearchIndexer? ProjectSearchIndexer { get; init; }

    public IPaymentRepository? Payments { get; init; }

    public IOrderRepository? Orders { get; init; }

    public IQuotationRepository? Quotations { get; init; }

    public IProposalRepository? Proposals { get; init; }

    public IProductionRequestRepository? ProductionRequests { get; init; }

    public IProjectScheduleRepository? Schedules { get; init; }
}

internal static class ProjectServiceTestFactory
{
    internal static ProjectService Create(
        IProjectRepository repository,
        IUnitOfWork unitOfWork,
        ProjectServiceFactoryOptions? options = null)
    {
        options ??= new ProjectServiceFactoryOptions();
        var transitionFakes = options.TransitionFakes ?? new ProjectServiceTransitionFakes();
        var evaluator = new ProjectStatusTransitionEvaluator(
            transitionFakes.Schedules,
            transitionFakes.Files,
            transitionFakes.Proposals,
            Options.Create(transitionFakes.Settings));

        return new ProjectService(
            repository,
            new ProjectServiceDependencies(
                unitOfWork,
                evaluator,
                options.Dispatcher,
                Logger: null,
                options.ProjectChats,
                options.Search,
                options.ProjectSearchIndexer,
                options.Payments ?? new FakeProjectPaymentRepository(),
                options.Orders ?? new FakeProjectOrderRepository(),
                options.Quotations ?? new FakeProjectQuotationRepository(),
                options.Proposals ?? new FakeProjectReopenProposalRepository(),
                options.ProductionRequests ?? new FakeProjectProductionRequestRepository(),
                options.Schedules ?? transitionFakes.Schedules));
    }
}

internal sealed class FakeReopenProjectRepository : IProjectRepository
{
    private readonly string? _roleName;
    private readonly List<Project> _projects;

    public FakeReopenProjectRepository(string? roleName, IReadOnlyList<Project> projects)
    {
        _roleName = roleName;
        _projects = projects.ToList();
    }

    public int SaveChangesCallCount { get; private set; }

    public Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_projects.FirstOrDefault(project => project.ProjectId == id));

    public Task<string?> GetAccountRoleNameAsync(Guid accountId, CancellationToken cancellationToken = default)
        => Task.FromResult(_roleName);

    public void Update(Project entity)
    {
        var index = _projects.FindIndex(project => project.ProjectId == entity.ProjectId);
        if (index >= 0)
        {
            _projects[index] = entity;
        }
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;
        return Task.FromResult(1);
    }

    public IQueryable<Project> Query() => _projects.AsQueryable();
    public Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Project>>(_projects);
    public Task AddAsync(Project entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task AddRangeAsync(IEnumerable<Project> entities, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Remove(Project entity) { }
    public Task<ProjectDetailReadModel?> GetDetailAsync(Guid projectId, CancellationToken cancellationToken = default) => Task.FromResult<ProjectDetailReadModel?>(null);
    public Task<IReadOnlyList<ProjectListItemReadModel>> GetListAsync(ProjectListQueryReadModel query, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProjectListItemReadModel>>([]);
    public Task<int> CountAsync(ProjectListQueryReadModel query, CancellationToken cancellationToken = default) => Task.FromResult(0);
    public Task<int> CountSubmittedInYearAsync(int year, CancellationToken cancellationToken = default) => Task.FromResult(0);
    public Task<IReadOnlyList<ProjectByUserItemReadModel>> GetByUserAsync(ProjectByUserQueryReadModel query, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProjectByUserItemReadModel>>([]);
    public Task<int> CountByUserAsync(ProjectByUserQueryReadModel query, CancellationToken cancellationToken = default) => Task.FromResult(0);
    public Task<DesignerAccountReadModel?> GetActiveDesignerAsync(Guid designerId, CancellationToken cancellationToken = default) => Task.FromResult<DesignerAccountReadModel?>(null);
    public Task<ProjectSearchIndexItemReadModel?> GetSearchIndexItemAsync(Guid projectId, CancellationToken cancellationToken = default) => Task.FromResult<ProjectSearchIndexItemReadModel?>(null);
    public Task<IReadOnlyList<ProjectSearchIndexItemReadModel>> GetSearchIndexPageAsync(int page, int limit, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProjectSearchIndexItemReadModel>>([]);
    public Task<IReadOnlyList<Guid>> GetActiveAccountIdsByRoleNamesAsync(IReadOnlyCollection<string> roleNames, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Guid>>([]);
    public Task<string?> GetAccountFullNameAsync(Guid accountId, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
}

internal sealed class FakeProjectPaymentRepository : IPaymentRepository
{
    public Payment? ProjectStartFeePayment { get; set; } = CreatePaidProjectStartFee();

    public Task<Payment?> GetByIdAsync(Guid paymentId, CancellationToken cancellationToken = default)
        => Task.FromResult<Payment?>(null);

    public Task<PaymentDetailReadModel?> GetDetailAsync(Guid paymentId, CancellationToken cancellationToken = default)
        => Task.FromResult<PaymentDetailReadModel?>(null);

    public Task<PaymentDetailReadModel?> GetDetailByPaymentCodeAsync(string paymentCode, CancellationToken cancellationToken = default)
        => Task.FromResult<PaymentDetailReadModel?>(null);

    public Task<PaymentStatusByCodeReadModel?> GetStatusByPaymentCodeAsync(string paymentCode, CancellationToken cancellationToken = default)
        => Task.FromResult<PaymentStatusByCodeReadModel?>(null);

    public Task<IReadOnlyList<PaymentListItemReadModel>> GetListAsync(PaymentQueryReadModel query, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<PaymentListItemReadModel>>([]);

    public Task<IReadOnlyList<PaymentTransactionReadModel>> GetTransactionsByPaymentIdAsync(Guid paymentId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<PaymentTransactionReadModel>>([]);

    public Task<bool> PaymentCodeExistsAsync(string paymentCode, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<bool> TransactionCodeExistsAsync(string transactionCode, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<bool> ProviderTransactionExistsAsync(PaymentProvider provider, string providerTransactionId, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<bool> PayOsOrderCodeExistsAsync(string orderCode, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<PaymentTransaction?> GetTransactionByProviderReferenceAsync(
        PaymentProvider provider,
        string providerReferenceCode,
        CancellationToken cancellationToken = default)
        => Task.FromResult<PaymentTransaction?>(null);

    public Task AddPaymentAsync(Payment payment, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task AddTransactionAsync(PaymentTransaction transaction, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<Payment?> GetByOrderAndTypeAsync(
        Guid orderId,
        PaymentType paymentType,
        CancellationToken cancellationToken = default)
        => Task.FromResult<Payment?>(null);

    public Task<IReadOnlyList<Payment>> GetAllByOrderAndTypeAsync(
        Guid orderId,
        PaymentType paymentType,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Payment>>([]);

    public Task<IReadOnlyList<PaymentTransaction>> GetTransactionEntitiesByPaymentIdAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<PaymentTransaction>>([]);

    public Task<Payment?> GetByProjectAndTypeAsync(
        Guid projectId,
        PaymentType paymentType,
        CancellationToken cancellationToken = default)
    {
        if (paymentType == PaymentType.PROJECT_START_FEE)
        {
            return Task.FromResult(ProjectStartFeePayment);
        }

        return Task.FromResult<Payment?>(null);
    }

    public Task<decimal> SumOrderScopedPaidAmountAsync(Guid orderId, CancellationToken cancellationToken = default)
        => Task.FromResult(0m);

    public Task<bool> HasSuccessfulTransactionAsync(Guid paymentId, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<int> CountAsync(PaymentQueryReadModel query, CancellationToken cancellationToken = default)
        => PaymentRepositoryStubMethods.CountAsync(query, cancellationToken);

    public Task<PaymentSummaryReadModel> GetSummaryAsync(
        PaymentQueryReadModel query,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
        => PaymentRepositoryStubMethods.GetSummaryAsync(query, utcNow, cancellationToken);

    public Task<IReadOnlyList<Payment>> GetExpiredPaymentsForSyncAsync(
        PaymentQueryReadModel query,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
        => PaymentRepositoryStubMethods.GetExpiredPaymentsForSyncAsync(query, utcNow, cancellationToken);

    public Task<PaymentTransaction?> GetTransactionByIdAsync(
        Guid paymentTransactionId,
        CancellationToken cancellationToken = default)
        => PaymentRepositoryStubMethods.GetTransactionByIdAsync(paymentTransactionId, cancellationToken);

    public Task<PaymentTransactionReadModel?> GetLatestPendingTransactionAsync(
        Guid paymentId,
        PaymentProvider provider,
        PaymentMethod method,
        CancellationToken cancellationToken = default)
        => PaymentRepositoryStubMethods.GetLatestPendingTransactionAsync(
            paymentId,
            provider,
            method,
            cancellationToken);

    public Task<PaymentTransactionReadModel?> GetLatestTransactionAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default)
        => PaymentRepositoryStubMethods.GetLatestTransactionAsync(paymentId, cancellationToken);

    public Task<IReadOnlySet<Guid>> GetPaymentIdsWithSuccessfulTransactionAsync(
        IReadOnlyCollection<Guid> paymentIds,
        CancellationToken cancellationToken = default)
        => PaymentRepositoryStubMethods.GetPaymentIdsWithSuccessfulTransactionAsync(paymentIds, cancellationToken);

    public void UpdatePayment(Payment payment)
    {
    }

    public void UpdateTransaction(PaymentTransaction transaction)
    {
    }

    private static Payment CreatePaidProjectStartFee()
    {
        return new Payment
        {
            PaymentId = Guid.NewGuid(),
            PaymentType = PaymentType.PROJECT_START_FEE,
            Status = PaymentStatus.PAID
        };
    }
}

internal sealed class ProjectServiceTransitionFakes
{
    public FakeProjectScheduleRepository Schedules { get; } = new();

    public FakeProjectFileRepository Files { get; } = new();

    public FakeProposalRepository Proposals { get; } = new();

    public ProjectWorkflowSettings Settings { get; set; } = new();
}

internal sealed class FakeProjectScheduleRepository : IProjectScheduleRepository
{
    public bool HasCompletedMeasurement { get; set; }
    public bool HasAssignedSchedule { get; set; }

    public Task<bool> HasCompletedMeasurementScheduleAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(HasCompletedMeasurement);

    public Task<bool> ExistsMeasurementScheduleAsync(
        Guid projectId,
        ProjectScheduleStatus? status,
        CancellationToken cancellationToken = default)
        => Task.FromResult(HasCompletedMeasurement && status == ProjectScheduleStatus.COMPLETED);

    public Task<bool> HasAssignedScheduleAsync(
        Guid projectId,
        Guid staffId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(HasAssignedSchedule);

    public Task<Infrastructure.ReadModels.ProjectSchedules.ProjectScheduleDetailReadModel?> GetDetailAsync(
        Guid scheduleId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<Infrastructure.ReadModels.ProjectSchedules.ProjectScheduleDetailReadModel?>(null);

    public Task<(IReadOnlyList<Infrastructure.ReadModels.ProjectSchedules.ProjectScheduleListItemReadModel> Items, int Total)> GetListByProjectAsync(
        Guid projectId,
        Infrastructure.ReadModels.ProjectSchedules.ProjectScheduleListQueryReadModel query,
        CancellationToken cancellationToken = default)
        => Task.FromResult<(IReadOnlyList<Infrastructure.ReadModels.ProjectSchedules.ProjectScheduleListItemReadModel>, int)>(
            (Array.Empty<Infrastructure.ReadModels.ProjectSchedules.ProjectScheduleListItemReadModel>(), 0));

    public Task<(IReadOnlyList<Infrastructure.ReadModels.ProjectSchedules.ProjectScheduleListItemReadModel> Items, int Total)> GetMyAssignedAsync(
        Guid? staffId,
        Infrastructure.ReadModels.ProjectSchedules.ProjectScheduleListQueryReadModel query,
        CancellationToken cancellationToken = default)
        => Task.FromResult<(IReadOnlyList<Infrastructure.ReadModels.ProjectSchedules.ProjectScheduleListItemReadModel>, int)>(
            (Array.Empty<Infrastructure.ReadModels.ProjectSchedules.ProjectScheduleListItemReadModel>(), 0));

    public IQueryable<ProjectSchedule> Query() => Enumerable.Empty<ProjectSchedule>().AsQueryable();
    public Task<ProjectSchedule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult<ProjectSchedule?>(null);
    public Task<IReadOnlyList<ProjectSchedule>> ListAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ProjectSchedule>>([]);
    public Task AddAsync(ProjectSchedule entity, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
    public Task AddRangeAsync(IEnumerable<ProjectSchedule> entities, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
    public void Update(ProjectSchedule entity) { }
    public void Remove(ProjectSchedule entity) { }
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(0);
}

internal sealed class FakeProjectFileRepository : IProjectFileRepository
{
    public bool HasMeasurementFiles { get; set; }

    public Task<bool> HasProjectFileWithTypesAsync(
        Guid projectId,
        IReadOnlyCollection<FileType> fileTypes,
        CancellationToken cancellationToken = default)
        => Task.FromResult(HasMeasurementFiles);

    public Task<Infrastructure.ReadModels.ProjectFiles.ProjectFileAccessReadModel?> GetProjectAccessAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<Infrastructure.ReadModels.ProjectFiles.ProjectFileAccessReadModel?>(null);

    public Task<Infrastructure.ReadModels.ProjectFiles.ProjectFileAccessReadModel?> GetReferenceProjectAccessAsync(
        string referenceType,
        Guid referenceId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<Infrastructure.ReadModels.ProjectFiles.ProjectFileAccessReadModel?>(null);

    public Task<string?> GetAccountRoleNameAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(null);

    public Task AddFileLinkAsync(FileLink fileLink, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<Infrastructure.ReadModels.ProjectFiles.FileMetadataReadModel?> GetFileMetadataAsync(
        Guid fileId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<Infrastructure.ReadModels.ProjectFiles.FileMetadataReadModel?>(null);

    public Task<Infrastructure.ReadModels.ProjectFiles.FileReferencePageReadModel> GetFilesByReferenceAsync(
        Infrastructure.ReadModels.ProjectFiles.FileReferenceQueryReadModel query,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new Infrastructure.ReadModels.ProjectFiles.FileReferencePageReadModel());

    public Task<Infrastructure.ReadModels.ProjectFiles.FileLinkReadModel?> GetFileLinkAsync(
        Guid fileLinkId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<Infrastructure.ReadModels.ProjectFiles.FileLinkReadModel?>(null);

    public Task<IReadOnlyList<FileLink>> GetFileLinkEntitiesByFileIdAsync(
        Guid fileId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<FileLink>>([]);

    public void RemoveFileLinks(IEnumerable<FileLink> fileLinks) { }

    public Task<IReadOnlyList<Infrastructure.ReadModels.Products.CatalogFileReadModel>> GetCatalogFilesByReferencesAsync(
        string referenceType,
        IReadOnlyList<Guid> referenceIds,
        bool customerVisibleOnly,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Infrastructure.ReadModels.Products.CatalogFileReadModel>>([]);

    public Task<int> CountProductPreviewFilesAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(0);

    public Task<IReadOnlyList<Infrastructure.ReadModels.Products.ProductPreviewImageReadModel>> GetProductPreviewFilesAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Infrastructure.ReadModels.Products.ProductPreviewImageReadModel>>([]);

    public Task<Infrastructure.ReadModels.Products.ProductPreviewImageReadModel?> GetProductPreviewFileAsync(
        Guid productId,
        Guid fileId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<Infrastructure.ReadModels.Products.ProductPreviewImageReadModel?>(null);

    public Task<IReadOnlyList<FileLink>> GetProductPreviewFileLinkEntitiesAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<FileLink>>([]);

    public Task<int> CountProductVersionPreviewFilesAsync(
        Guid productVersionId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(0);

    public Task<IReadOnlyList<FileLink>> GetProductVersionPreviewFileLinkEntitiesAsync(
        Guid productVersionId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<FileLink>>([]);

    public Task<Infrastructure.ReadModels.ProjectFiles.ProjectFileSearchIndexItemReadModel?> GetSearchIndexItemAsync(
        Guid fileId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<Infrastructure.ReadModels.ProjectFiles.ProjectFileSearchIndexItemReadModel?>(null);

    public Task<IReadOnlyList<Infrastructure.ReadModels.ProjectFiles.ProjectFileSearchIndexItemReadModel>> GetSearchIndexPageAsync(
        int page,
        int limit,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Infrastructure.ReadModels.ProjectFiles.ProjectFileSearchIndexItemReadModel>>([]);

    public Task<IReadOnlyList<Infrastructure.ReadModels.ProjectFiles.ProjectFileSearchIndexItemReadModel>> SearchByProjectAsync(
        Guid projectId,
        string query,
        int page,
        int limit,
        bool customerVisibleOnly,
        Guid? customerAccountId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Infrastructure.ReadModels.ProjectFiles.ProjectFileSearchIndexItemReadModel>>([]);

    public Task<int> CountSearchByProjectAsync(
        Guid projectId,
        string query,
        bool customerVisibleOnly,
        Guid? customerAccountId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(0);

    public IQueryable<StoredFile> Query() => Enumerable.Empty<StoredFile>().AsQueryable();
    public Task<StoredFile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult<StoredFile?>(null);
    public Task<IReadOnlyList<StoredFile>> ListAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<StoredFile>>([]);
    public Task AddAsync(StoredFile entity, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
    public Task AddRangeAsync(IEnumerable<StoredFile> entities, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
    public void Update(StoredFile entity) { }
    public void Remove(StoredFile entity) { }
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(0);
}

internal sealed class FakeProposalRepository : IProposalRepository
{
    public bool HasProposalWithActiveScene { get; set; }

    public bool HasSelectedFinalProposal { get; set; }

    public Task<bool> HasProposalWithActiveSceneAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(HasProposalWithActiveScene);

    public Task<bool> HasSelectedFinalProposalAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(HasSelectedFinalProposal);

    public Task<Infrastructure.ReadModels.Proposals.ProposalProjectAccessReadModel?> GetProjectAccessAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<Infrastructure.ReadModels.Proposals.ProposalProjectAccessReadModel?>(null);

    public Task<Infrastructure.ReadModels.Proposals.ProposalContextReadModel?> GetProposalContextAsync(
        Guid proposalId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<Infrastructure.ReadModels.Proposals.ProposalContextReadModel?>(null);

    public Task<int> CountByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        => Task.FromResult(0);

    public Task<IReadOnlyList<Infrastructure.ReadModels.Proposals.ProposalReadModel>> GetListAsync(
        Infrastructure.ReadModels.Proposals.ProposalListQueryReadModel query,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Infrastructure.ReadModels.Proposals.ProposalReadModel>>([]);

    public Task<int> CountListAsync(
        Infrastructure.ReadModels.Proposals.ProposalListQueryReadModel query,
        CancellationToken cancellationToken = default)
        => Task.FromResult(0);

    public Task<int> CountScenesAsync(Guid proposalId, CancellationToken cancellationToken = default)
        => Task.FromResult(0);

    public Task<int> CountScenesAsync(
        Infrastructure.ReadModels.Proposals.ProposalSceneListQueryReadModel query,
        CancellationToken cancellationToken = default)
        => Task.FromResult(0);

    public Task AddSceneAsync(ProposalScene scene, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<List<Infrastructure.ReadModels.Proposals.ProposalProjectAreaReadModel>> GetProjectAreasByIdsAsync(
        List<Guid> projectAreaIds,
        CancellationToken cancellationToken = default)
        => Task.FromResult<List<Infrastructure.ReadModels.Proposals.ProposalProjectAreaReadModel>>([]);

    public Task ReplaceSceneAreasAsync(
        Guid sceneId,
        List<Guid> projectAreaIds,
        DateTime now,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<Infrastructure.ReadModels.Proposals.ProposalDetailReadModel?> GetDetailAsync(
        Guid proposalId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<Infrastructure.ReadModels.Proposals.ProposalDetailReadModel?>(null);

    public Task<Infrastructure.ReadModels.Proposals.ProposalDetailReadModel?> GetLatestPublishedByProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<Infrastructure.ReadModels.Proposals.ProposalDetailReadModel?>(null);

    public Task<IReadOnlyList<Infrastructure.ReadModels.Proposals.ProposalSceneReadModel>> GetScenesAsync(
        Infrastructure.ReadModels.Proposals.ProposalSceneListQueryReadModel query,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Infrastructure.ReadModels.Proposals.ProposalSceneReadModel>>([]);

    public Task<Infrastructure.ReadModels.Proposals.ProposalSceneDetailReadModel?> GetSceneDetailAsync(
        Guid sceneId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<Infrastructure.ReadModels.Proposals.ProposalSceneDetailReadModel?>(null);

    public Task<Infrastructure.ReadModels.Proposals.ProposalSceneContextReadModel?> GetSceneContextAsync(
        Guid proposalId,
        Guid sceneId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<Infrastructure.ReadModels.Proposals.ProposalSceneContextReadModel?>(null);

    public Task<Infrastructure.ReadModels.Proposals.ProposalSceneContextReadModel?> GetSceneContextBySceneIdAsync(
        Guid sceneId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<Infrastructure.ReadModels.Proposals.ProposalSceneContextReadModel?>(null);

    public Task<ProposalScene?> GetSceneEntityAsync(Guid sceneId, CancellationToken cancellationToken = default)
        => Task.FromResult<ProposalScene?>(null);

    public Task<IReadOnlyList<ProposalItem>> GetItemsBySceneAsync(
        Guid proposalId,
        Guid sceneId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ProposalItem>>([]);

    public Task<IReadOnlyList<Infrastructure.ReadModels.Proposals.ProposalItemReadModel>> GetItemsAsync(
        Infrastructure.ReadModels.Proposals.ProposalItemListQueryReadModel query,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Infrastructure.ReadModels.Proposals.ProposalItemReadModel>>([]);

    public Task<int> CountItemsAsync(
        Infrastructure.ReadModels.Proposals.ProposalItemListQueryReadModel query,
        CancellationToken cancellationToken = default)
        => Task.FromResult(0);

    public Task<Infrastructure.ReadModels.Proposals.ProposalItemDetailReadModel?> GetItemDetailAsync(
        Guid proposalItemId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<Infrastructure.ReadModels.Proposals.ProposalItemDetailReadModel?>(null);

    public Task<ProposalItem?> GetItemEntityAsync(
        Guid proposalItemId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<ProposalItem?>(null);

    public Task AddItemAsync(ProposalItem item, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public void RemoveItem(ProposalItem item) { }

    public Task<Proposal?> GetProposalEntityAsync(Guid proposalId, CancellationToken cancellationToken = default)
        => Task.FromResult<Proposal?>(null);

    public Task RejectOtherActiveProposalsAsync(
        Guid projectId,
        Guid selectedProposalId,
        DateTime rejectedAt,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<Proposal?> GetSelectedProposalByProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<Proposal?>(null);

    public Task<int> RestoreAutoRejectedProposalsAsync(
        Guid projectId,
        DateTime autoRejectedAt,
        DateTime restoredAt,
        CancellationToken cancellationToken = default)
        => Task.FromResult(0);

    public Task<bool> HasActiveSceneAsync(Guid proposalId, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<bool> FileExistsAsync(Guid fileId, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<bool> ProjectAreaBelongsToProjectAsync(
        Guid projectAreaId,
        Guid projectId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public IQueryable<Proposal> Query() => Enumerable.Empty<Proposal>().AsQueryable();
    public Task<Proposal?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult<Proposal?>(null);
    public Task<IReadOnlyList<Proposal>> ListAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Proposal>>([]);
    public Task AddAsync(Proposal entity, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
    public Task AddRangeAsync(IEnumerable<Proposal> entities, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
    public void Update(Proposal entity) { }
    public void Remove(Proposal entity) { }
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(0);
}

internal sealed class FakeProjectOrderRepository : IOrderRepository
{
    public Order? Order { get; set; }

    public Task<Order?> GetLatestByProjectInStatusesAsync(
        Guid projectId,
        IReadOnlyCollection<OrderStatus> statuses,
        CancellationToken cancellationToken = default)
    {
        if (Order is null ||
            Order.ProjectId != projectId ||
            !Order.Status.HasValue ||
            !statuses.Contains(Order.Status.Value))
        {
            return Task.FromResult<Order?>(null);
        }

        return Task.FromResult<Order?>(Order);
    }

    public Task<IReadOnlyList<Infrastructure.ReadModels.Orders.OrderListItemReadModel>> GetByProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Infrastructure.ReadModels.Orders.OrderListItemReadModel>>([]);

    public Task<Infrastructure.ReadModels.Orders.OrderDetailReadModel?> GetDetailAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<Infrastructure.ReadModels.Orders.OrderDetailReadModel?>(null);

    public Task<Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken = default)
        => Task.FromResult(Order?.OrderId == orderId ? Order : null);

    public Task<bool> ExistsForQuotationAsync(Guid quotationId, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task AddAsync(Order order, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task AddItemAsync(OrderItem item, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public void Update(Order order)
    {
        Order = order;
    }

    public IQueryable<Order> Query() => Order is null ? Enumerable.Empty<Order>().AsQueryable() : new[] { Order }.AsQueryable();
    public Task<IReadOnlyList<Order>> ListAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Order>>(Order is null ? [] : new List<Order> { Order });
    public Task AddRangeAsync(IEnumerable<Order> entities, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
    public void Remove(Order entity) { }
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(0);
}

internal sealed class FakeProjectQuotationRepository : IQuotationRepository
{
    public Quotation? Quotation { get; set; }

    public Task<Quotation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(Quotation?.QuotationId == id ? Quotation : null);

    public void Update(Quotation entity)
    {
        Quotation = entity;
    }

    public Task<Quotation?> GetLatestByProjectAndProposalInStatusesAsync(
        Guid projectId,
        Guid proposalId,
        IReadOnlyCollection<QuotationStatus> statuses,
        CancellationToken cancellationToken = default)
    {
        if (Quotation is null ||
            Quotation.ProjectId != projectId ||
            Quotation.ProposalId != proposalId ||
            !Quotation.Status.HasValue ||
            !statuses.Contains(Quotation.Status.Value))
        {
            return Task.FromResult<Quotation?>(null);
        }

        return Task.FromResult<Quotation?>(Quotation);
    }

    public Task<IReadOnlyList<Infrastructure.ReadModels.Quotations.QuotationReadModel>> GetByProjectAsync(
        Infrastructure.ReadModels.Quotations.QuotationQueryReadModel query,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Infrastructure.ReadModels.Quotations.QuotationReadModel>>([]);

    public Task<Infrastructure.ReadModels.Quotations.QuotationDetailReadModel?> GetDetailAsync(
        Guid quotationId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<Infrastructure.ReadModels.Quotations.QuotationDetailReadModel?>(null);

    public Task<Infrastructure.ReadModels.Quotations.SelectedProposalForQuotationReadModel?> GetSelectedProposalAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<Infrastructure.ReadModels.Quotations.SelectedProposalForQuotationReadModel?>(null);

    public Task<bool> HasQuotationForProposalAsync(Guid proposalId, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<IReadOnlyList<ProposalItem>> GetProposalItemsAsync(Guid proposalId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ProposalItem>>([]);

    public Task<IReadOnlyList<QuotationItem>> GetItemsByQuotationAsync(Guid quotationId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<QuotationItem>>([]);

    public Task<QuotationItem?> GetItemAsync(Guid quotationItemId, CancellationToken cancellationToken = default)
        => Task.FromResult<QuotationItem?>(null);

    public Task AddItemAsync(QuotationItem item, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public void UpdateItem(QuotationItem item) { }
    public void RemoveItem(QuotationItem item) { }
    public Task AddOrderAsync(Order order, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task AddOrderItemAsync(OrderItem item, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public IQueryable<Quotation> Query() => Quotation is null ? Enumerable.Empty<Quotation>().AsQueryable() : new[] { Quotation }.AsQueryable();
    public Task<IReadOnlyList<Quotation>> ListAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Quotation>>(Quotation is null ? [] : new List<Quotation> { Quotation });
    public Task AddAsync(Quotation entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task AddRangeAsync(IEnumerable<Quotation> entities, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Remove(Quotation entity) { }
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
}

internal sealed class FakeProjectReopenProposalRepository : IProposalRepository
{
    public Proposal? SelectedProposal { get; set; }
    public List<Proposal> AutoRejectedProposals { get; } = [];

    public Task<Proposal?> GetSelectedProposalByProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        if (SelectedProposal is null || SelectedProposal.ProjectId != projectId)
        {
            return Task.FromResult<Proposal?>(null);
        }

        return Task.FromResult<Proposal?>(SelectedProposal);
    }

    public Task<int> RestoreAutoRejectedProposalsAsync(
        Guid projectId,
        DateTime autoRejectedAt,
        DateTime restoredAt,
        CancellationToken cancellationToken = default)
    {
        var restored = 0;
        foreach (var proposal in AutoRejectedProposals.Where(item =>
                     item.ProjectId == projectId &&
                     item.Status == ProposalStatus.REJECTED &&
                     item.RejectedAt == autoRejectedAt))
        {
            proposal.Status = ProposalStatus.PUBLISHED;
            proposal.RejectedAt = null;
            proposal.UpdatedAt = restoredAt;
            restored++;
        }

        return Task.FromResult(restored);
    }

    public void Update(Proposal entity)
    {
        if (SelectedProposal?.ProposalId == entity.ProposalId)
        {
            SelectedProposal = entity;
        }
    }

    public Task<Infrastructure.ReadModels.Proposals.ProposalProjectAccessReadModel?> GetProjectAccessAsync(Guid projectId, CancellationToken cancellationToken = default)
        => Task.FromResult<Infrastructure.ReadModels.Proposals.ProposalProjectAccessReadModel?>(null);
    public Task<Infrastructure.ReadModels.Proposals.ProposalContextReadModel?> GetProposalContextAsync(Guid proposalId, CancellationToken cancellationToken = default)
        => Task.FromResult<Infrastructure.ReadModels.Proposals.ProposalContextReadModel?>(null);
    public Task<int> CountByProjectAsync(Guid projectId, CancellationToken cancellationToken = default) => Task.FromResult(0);
    public Task<IReadOnlyList<Infrastructure.ReadModels.Proposals.ProposalReadModel>> GetListAsync(Infrastructure.ReadModels.Proposals.ProposalListQueryReadModel query, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Infrastructure.ReadModels.Proposals.ProposalReadModel>>([]);
    public Task<int> CountListAsync(Infrastructure.ReadModels.Proposals.ProposalListQueryReadModel query, CancellationToken cancellationToken = default) => Task.FromResult(0);
    public Task<int> CountScenesAsync(Guid proposalId, CancellationToken cancellationToken = default) => Task.FromResult(0);
    public Task<int> CountScenesAsync(Infrastructure.ReadModels.Proposals.ProposalSceneListQueryReadModel query, CancellationToken cancellationToken = default) => Task.FromResult(0);
    public Task AddSceneAsync(ProposalScene scene, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<List<Infrastructure.ReadModels.Proposals.ProposalProjectAreaReadModel>> GetProjectAreasByIdsAsync(List<Guid> projectAreaIds, CancellationToken cancellationToken = default)
        => Task.FromResult<List<Infrastructure.ReadModels.Proposals.ProposalProjectAreaReadModel>>([]);
    public Task ReplaceSceneAreasAsync(Guid sceneId, List<Guid> projectAreaIds, DateTime now, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<Infrastructure.ReadModels.Proposals.ProposalDetailReadModel?> GetDetailAsync(Guid proposalId, CancellationToken cancellationToken = default)
        => Task.FromResult<Infrastructure.ReadModels.Proposals.ProposalDetailReadModel?>(null);
    public Task<Infrastructure.ReadModels.Proposals.ProposalDetailReadModel?> GetLatestPublishedByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        => Task.FromResult<Infrastructure.ReadModels.Proposals.ProposalDetailReadModel?>(null);
    public Task<IReadOnlyList<Infrastructure.ReadModels.Proposals.ProposalSceneReadModel>> GetScenesAsync(Infrastructure.ReadModels.Proposals.ProposalSceneListQueryReadModel query, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Infrastructure.ReadModels.Proposals.ProposalSceneReadModel>>([]);
    public Task<Infrastructure.ReadModels.Proposals.ProposalSceneDetailReadModel?> GetSceneDetailAsync(Guid sceneId, CancellationToken cancellationToken = default)
        => Task.FromResult<Infrastructure.ReadModels.Proposals.ProposalSceneDetailReadModel?>(null);
    public Task<Infrastructure.ReadModels.Proposals.ProposalSceneContextReadModel?> GetSceneContextAsync(Guid proposalId, Guid sceneId, CancellationToken cancellationToken = default)
        => Task.FromResult<Infrastructure.ReadModels.Proposals.ProposalSceneContextReadModel?>(null);
    public Task<Infrastructure.ReadModels.Proposals.ProposalSceneContextReadModel?> GetSceneContextBySceneIdAsync(Guid sceneId, CancellationToken cancellationToken = default)
        => Task.FromResult<Infrastructure.ReadModels.Proposals.ProposalSceneContextReadModel?>(null);
    public Task<ProposalScene?> GetSceneEntityAsync(Guid sceneId, CancellationToken cancellationToken = default) => Task.FromResult<ProposalScene?>(null);
    public Task<IReadOnlyList<ProposalItem>> GetItemsBySceneAsync(Guid proposalId, Guid sceneId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ProposalItem>>([]);
    public Task<IReadOnlyList<Infrastructure.ReadModels.Proposals.ProposalItemReadModel>> GetItemsAsync(Infrastructure.ReadModels.Proposals.ProposalItemListQueryReadModel query, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Infrastructure.ReadModels.Proposals.ProposalItemReadModel>>([]);
    public Task<int> CountItemsAsync(Infrastructure.ReadModels.Proposals.ProposalItemListQueryReadModel query, CancellationToken cancellationToken = default) => Task.FromResult(0);
    public Task<Infrastructure.ReadModels.Proposals.ProposalItemDetailReadModel?> GetItemDetailAsync(Guid proposalItemId, CancellationToken cancellationToken = default)
        => Task.FromResult<Infrastructure.ReadModels.Proposals.ProposalItemDetailReadModel?>(null);
    public Task<ProposalItem?> GetItemEntityAsync(Guid proposalItemId, CancellationToken cancellationToken = default) => Task.FromResult<ProposalItem?>(null);
    public Task AddItemAsync(ProposalItem item, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void RemoveItem(ProposalItem item) { }
    public Task<Proposal?> GetProposalEntityAsync(Guid proposalId, CancellationToken cancellationToken = default)
        => Task.FromResult<Proposal?>(SelectedProposal?.ProposalId == proposalId ? SelectedProposal : null);
    public Task RejectOtherActiveProposalsAsync(Guid projectId, Guid selectedProposalId, DateTime rejectedAt, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<bool> HasProposalWithActiveSceneAsync(Guid projectId, CancellationToken cancellationToken = default) => Task.FromResult(false);
    public Task<bool> HasSelectedFinalProposalAsync(Guid projectId, CancellationToken cancellationToken = default) => Task.FromResult(SelectedProposal?.Status == ProposalStatus.SELECTED);
    public Task<bool> HasActiveSceneAsync(Guid proposalId, CancellationToken cancellationToken = default) => Task.FromResult(false);
    public Task<bool> FileExistsAsync(Guid fileId, CancellationToken cancellationToken = default) => Task.FromResult(false);
    public Task<bool> ProjectAreaBelongsToProjectAsync(Guid projectAreaId, Guid projectId, CancellationToken cancellationToken = default) => Task.FromResult(false);
    public IQueryable<Proposal> Query() => Enumerable.Empty<Proposal>().AsQueryable();
    public Task<Proposal?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Proposal?>(null);
    public Task<IReadOnlyList<Proposal>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Proposal>>([]);
    public Task AddAsync(Proposal entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task AddRangeAsync(IEnumerable<Proposal> entities, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Remove(Proposal entity) { }
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
}

internal sealed class FakeProjectProductionRequestRepository : IProductionRequestRepository
{
    public bool HasProductionRequest { get; set; }

    public Task<bool> ExistsForOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
        => Task.FromResult(HasProductionRequest);

    public Task<bool> HasActiveRequestForOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
        => Task.FromResult(HasProductionRequest);

    public Task<int> CountCreatedOnAsync(DateOnly date, CancellationToken cancellationToken = default) => Task.FromResult(0);
    public Task<List<OrderItem>> GetProductOrderItemsAsync(Guid orderId, CancellationToken cancellationToken = default) => Task.FromResult(new List<OrderItem>());
    public Task AddItemsAsync(List<ProductionItem> items, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<bool> IsActiveProductionStaffAsync(Guid accountId, CancellationToken cancellationToken = default) => Task.FromResult(false);
    public Task<Infrastructure.ReadModels.Production.ProductionAssigneeReadModel?> GetAssigneeAsync(Guid accountId, CancellationToken cancellationToken = default)
        => Task.FromResult<Infrastructure.ReadModels.Production.ProductionAssigneeReadModel?>(null);
    public Task<List<Infrastructure.ReadModels.Production.AvailableProductionStaffReadModel>> GetAvailableStaffAsync(string? search, CancellationToken cancellationToken = default)
        => Task.FromResult(new List<Infrastructure.ReadModels.Production.AvailableProductionStaffReadModel>());
    public Task<List<Infrastructure.ReadModels.Production.ProductionRequestListItemReadModel>> GetQueueAsync(Infrastructure.ReadModels.Production.ProductionRequestQueueReadModel query, CancellationToken cancellationToken = default)
        => Task.FromResult(new List<Infrastructure.ReadModels.Production.ProductionRequestListItemReadModel>());
    public Task<bool> HasViewableAssignedRequestAsync(Guid projectId, Guid productionAccountId, CancellationToken cancellationToken = default) => Task.FromResult(false);
    public Task<Infrastructure.ReadModels.Production.ProductionRequestDetailReadModel?> GetDetailAsync(Guid productionRequestId, CancellationToken cancellationToken = default)
        => Task.FromResult<Infrastructure.ReadModels.Production.ProductionRequestDetailReadModel?>(null);
    public Task<ProductionItem?> GetItemByIdAsync(Guid productionItemId, CancellationToken cancellationToken = default) => Task.FromResult<ProductionItem?>(null);
    public Task<Infrastructure.ReadModels.Production.ProductionRequestDetailReadModel?> GetDetailByItemIdAsync(Guid productionItemId, CancellationToken cancellationToken = default)
        => Task.FromResult<Infrastructure.ReadModels.Production.ProductionRequestDetailReadModel?>(null);
    public void UpdateItem(ProductionItem item) { }
    public IQueryable<ProductionRequest> Query() => Enumerable.Empty<ProductionRequest>().AsQueryable();
    public Task<ProductionRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<ProductionRequest?>(null);
    public Task<IReadOnlyList<ProductionRequest>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProductionRequest>>([]);
    public Task AddAsync(ProductionRequest entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task AddRangeAsync(IEnumerable<ProductionRequest> entities, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Update(ProductionRequest entity) { }
    public void Remove(ProductionRequest entity) { }
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
}

internal sealed class FakeProjectReopenPaymentRepository : IPaymentRepository
{
    public List<Payment> DepositPayments { get; } = [];
    public List<PaymentTransaction> Transactions { get; } = [];
    public bool HasSuccessfulTransaction { get; set; }

    public Task<IReadOnlyList<Payment>> GetAllByOrderAndTypeAsync(
        Guid orderId,
        PaymentType paymentType,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Payment> payments = DepositPayments
            .Where(payment => payment.OrderId == orderId && payment.PaymentType == paymentType)
            .ToList();
        return Task.FromResult(payments);
    }

    public Task<IReadOnlyList<PaymentTransaction>> GetTransactionEntitiesByPaymentIdAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<PaymentTransaction> transactions = Transactions
            .Where(transaction => transaction.PaymentId == paymentId)
            .ToList();
        return Task.FromResult(transactions);
    }

    public Task<bool> HasSuccessfulTransactionAsync(Guid paymentId, CancellationToken cancellationToken = default)
        => Task.FromResult(HasSuccessfulTransaction);

    public void UpdatePayment(Payment payment) { }
    public void UpdateTransaction(PaymentTransaction transaction) { }

    public Task<Payment?> GetByIdAsync(Guid paymentId, CancellationToken cancellationToken = default) => Task.FromResult<Payment?>(null);
    public Task<PaymentDetailReadModel?> GetDetailAsync(Guid paymentId, CancellationToken cancellationToken = default) => Task.FromResult<PaymentDetailReadModel?>(null);
    public Task<PaymentDetailReadModel?> GetDetailByPaymentCodeAsync(string paymentCode, CancellationToken cancellationToken = default) => Task.FromResult<PaymentDetailReadModel?>(null);
    public Task<PaymentStatusByCodeReadModel?> GetStatusByPaymentCodeAsync(string paymentCode, CancellationToken cancellationToken = default) => Task.FromResult<PaymentStatusByCodeReadModel?>(null);
    public Task<IReadOnlyList<PaymentListItemReadModel>> GetListAsync(PaymentQueryReadModel query, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PaymentListItemReadModel>>([]);
    public Task<int> CountAsync(PaymentQueryReadModel query, CancellationToken cancellationToken = default) => Task.FromResult(0);
    public Task<PaymentSummaryReadModel> GetSummaryAsync(PaymentQueryReadModel query, DateTime utcNow, CancellationToken cancellationToken = default) => Task.FromResult(new PaymentSummaryReadModel());
    public Task<IReadOnlyList<Payment>> GetExpiredPaymentsForSyncAsync(PaymentQueryReadModel query, DateTime utcNow, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Payment>>([]);
    public Task<IReadOnlyList<PaymentTransactionReadModel>> GetTransactionsByPaymentIdAsync(Guid paymentId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PaymentTransactionReadModel>>([]);
    public Task<PaymentTransaction?> GetTransactionByIdAsync(Guid paymentTransactionId, CancellationToken cancellationToken = default) => Task.FromResult<PaymentTransaction?>(null);
    public Task<PaymentTransactionReadModel?> GetLatestPendingTransactionAsync(Guid paymentId, PaymentProvider provider, PaymentMethod method, CancellationToken cancellationToken = default) => Task.FromResult<PaymentTransactionReadModel?>(null);
    public Task<PaymentTransactionReadModel?> GetLatestTransactionAsync(Guid paymentId, CancellationToken cancellationToken = default) => Task.FromResult<PaymentTransactionReadModel?>(null);
    public Task<IReadOnlySet<Guid>> GetPaymentIdsWithSuccessfulTransactionAsync(IReadOnlyCollection<Guid> paymentIds, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>());
    public Task<bool> PaymentCodeExistsAsync(string paymentCode, CancellationToken cancellationToken = default) => Task.FromResult(false);
    public Task<bool> TransactionCodeExistsAsync(string transactionCode, CancellationToken cancellationToken = default) => Task.FromResult(false);
    public Task<bool> ProviderTransactionExistsAsync(PaymentProvider provider, string providerTransactionId, CancellationToken cancellationToken = default) => Task.FromResult(false);
    public Task<bool> PayOsOrderCodeExistsAsync(string orderCode, CancellationToken cancellationToken = default) => Task.FromResult(false);
    public Task<PaymentTransaction?> GetTransactionByProviderReferenceAsync(PaymentProvider provider, string providerReferenceCode, CancellationToken cancellationToken = default) => Task.FromResult<PaymentTransaction?>(null);
    public Task AddPaymentAsync(Payment payment, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task AddTransactionAsync(PaymentTransaction transaction, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<Payment?> GetByOrderAndTypeAsync(Guid orderId, PaymentType paymentType, CancellationToken cancellationToken = default) => Task.FromResult<Payment?>(null);
    public Task<Payment?> GetByProjectAndTypeAsync(Guid projectId, PaymentType paymentType, CancellationToken cancellationToken = default) => Task.FromResult<Payment?>(null);
    public Task<decimal> SumOrderScopedPaidAmountAsync(Guid orderId, CancellationToken cancellationToken = default) => Task.FromResult(0m);
}
