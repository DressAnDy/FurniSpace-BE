#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.DTOs.Projects;
using FurniSpace.Application.Tests.TestDoubles;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Persistence;
using Xunit;

namespace FurniSpace.Application.Tests.Projects;

public sealed class ProjectServiceReopenProposalTests
{
    [Fact]
    public async Task ReopenProposalAsync_WithEligibleProject_ReopensProposalConsultation()
    {
        var customerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var quotationId = Guid.NewGuid();
        var selectedProposalId = Guid.NewGuid();
        var selectedAt = DateTime.UtcNow.AddDays(-1);

        var project = new Project
        {
            ProjectId = projectId,
            CustomerId = customerId,
            Status = ProjectStatus.ORDER_CONFIRMED
        };
        var order = new Order
        {
            OrderId = orderId,
            ProjectId = projectId,
            QuotationId = quotationId,
            Status = OrderStatus.DEPOSIT_PENDING
        };
        var quotation = new Quotation
        {
            QuotationId = quotationId,
            ProjectId = projectId,
            Status = QuotationStatus.ACCEPTED
        };
        var selectedProposal = new Proposal
        {
            ProposalId = selectedProposalId,
            ProjectId = projectId,
            Status = ProposalStatus.SELECTED,
            SelectedAt = selectedAt
        };
        var rejectedProposal = new Proposal
        {
            ProposalId = Guid.NewGuid(),
            ProjectId = projectId,
            Status = ProposalStatus.REJECTED,
            RejectedAt = selectedAt
        };

        var repository = new FakeReopenProjectRepository("CUSTOMER", [project]);
        var orders = new FakeProjectOrderRepository { Order = order };
        var quotations = new FakeProjectQuotationRepository { Quotation = quotation };
        var proposals = new FakeProjectReopenProposalRepository
        {
            SelectedProposal = selectedProposal,
            AutoRejectedProposals = { rejectedProposal }
        };
        var payments = new FakeProjectReopenPaymentRepository
        {
            DepositPayments =
            {
                new Payment
                {
                    PaymentId = Guid.NewGuid(),
                    OrderId = orderId,
                    PaymentType = PaymentType.DEPOSIT,
                    Status = PaymentStatus.PENDING
                }
            }
        };
        var unitOfWork = CreateTrackingUnitOfWork(repository.SaveChangesAsync);
        var service = ProjectServiceTestFactory.Create(
            repository,
            unitOfWork,
            new ProjectServiceFactoryOptions
            {
                Orders = orders,
                Quotations = quotations,
                Proposals = proposals,
                Payments = payments
            });

        var result = await service.ReopenProposalAsync(projectId, customerId);

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(ProjectStatus.PROPOSAL_CONSULTING, result.Data.NewStatus);
        Assert.Equal(ProjectStatus.ORDER_CONFIRMED, result.Data.OldStatus);
        Assert.Equal(OrderStatus.CANCELLED, result.Data.OrderStatus);
        Assert.Equal(QuotationStatus.CANCELLED, result.Data.QuotationStatus);
        Assert.Equal(ProposalStatus.PUBLISHED, result.Data.SelectedProposalStatus);
        Assert.Equal(1, result.Data.RestoredProposalCount);
        Assert.Equal(ProjectStatus.PROPOSAL_CONSULTING, project.Status);
        Assert.Equal(OrderStatus.CANCELLED, order.Status);
        Assert.Equal(QuotationStatus.CANCELLED, quotation.Status);
        Assert.Equal(ProposalStatus.PUBLISHED, selectedProposal.Status);
        Assert.Null(selectedProposal.SelectedAt);
        Assert.Equal(ProposalStatus.PUBLISHED, rejectedProposal.Status);
        Assert.Null(rejectedProposal.RejectedAt);
        Assert.Equal(1, unitOfWork.BeginTransactionCallCount);
        Assert.Equal(1, unitOfWork.CommitTransactionCallCount);
    }

    private static TrackingUnitOfWork CreateTrackingUnitOfWork(Func<CancellationToken, Task<int>> saveChanges)
    {
        return new TrackingUnitOfWork(saveChanges);
    }

    private sealed class TrackingUnitOfWork : IUnitOfWork
    {
        private readonly Func<CancellationToken, Task<int>> _saveChanges;

        public TrackingUnitOfWork(Func<CancellationToken, Task<int>> saveChanges)
        {
            _saveChanges = saveChanges;
        }

        public int BeginTransactionCallCount { get; private set; }
        public int CommitTransactionCallCount { get; private set; }

        public Task BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            BeginTransactionCallCount++;
            return Task.CompletedTask;
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => _saveChanges(cancellationToken);

        public Task CommitTransactionAsync(CancellationToken cancellationToken = default)
        {
            CommitTransactionCallCount++;
            return Task.CompletedTask;
        }

        public Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    [Fact]
    public async Task ReopenProposalAsync_WithoutEligibleOrder_ReturnsNoAcceptedOrder()
    {
        var customerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var project = new Project
        {
            ProjectId = projectId,
            CustomerId = customerId,
            Status = ProjectStatus.ORDER_CONFIRMED
        };
        var repository = new FakeReopenProjectRepository("CUSTOMER", [project]);
        var service = ProjectServiceTestFactory.Create(
            repository,
            TestUnitOfWork.Instance,
            new ProjectServiceFactoryOptions
            {
                Proposals = new FakeProjectReopenProposalRepository
                {
                    SelectedProposal = new Proposal
                    {
                        ProposalId = Guid.NewGuid(),
                        ProjectId = projectId,
                        Status = ProposalStatus.SELECTED,
                        SelectedAt = DateTime.UtcNow.AddDays(-1)
                    }
                }
            });

        var result = await service.ReopenProposalAsync(projectId, customerId);

        Assert.Equal(400, result.Status);
        Assert.Equal(ProjectReopenProposalErrorCodes.NoAcceptedOrder, result.ErrorCode);
    }

    [Fact]
    public async Task ReopenProposalAsync_WithPaidDeposit_ReturnsDepositAlreadyPaid()
    {
        var customerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var project = new Project
        {
            ProjectId = projectId,
            CustomerId = customerId,
            Status = ProjectStatus.ORDER_CONFIRMED
        };
        var repository = new FakeReopenProjectRepository("CUSTOMER", [project]);
        var payments = new FakeProjectReopenPaymentRepository
        {
            DepositPayments =
            {
                new Payment
                {
                    PaymentId = Guid.NewGuid(),
                    OrderId = orderId,
                    PaymentType = PaymentType.DEPOSIT,
                    Status = PaymentStatus.PAID
                }
            }
        };
        var service = ProjectServiceTestFactory.Create(
            repository,
            TestUnitOfWork.Instance,
            new ProjectServiceFactoryOptions
            {
                Orders = new FakeProjectOrderRepository
                {
                    Order = new Order
                    {
                        OrderId = orderId,
                        ProjectId = projectId,
                        QuotationId = Guid.NewGuid(),
                        Status = OrderStatus.DEPOSIT_PENDING
                    }
                },
                Payments = payments,
                Proposals = new FakeProjectReopenProposalRepository
                {
                    SelectedProposal = new Proposal
                    {
                        ProposalId = Guid.NewGuid(),
                        ProjectId = projectId,
                        Status = ProposalStatus.SELECTED,
                        SelectedAt = DateTime.UtcNow.AddDays(-1)
                    }
                }
            });

        var result = await service.ReopenProposalAsync(projectId, customerId);

        Assert.Equal(400, result.Status);
        Assert.Equal(ProjectReopenProposalErrorCodes.DepositAlreadyPaid, result.ErrorCode);
    }

    [Fact]
    public async Task ReopenProposalAsync_WithProductionRequest_ReturnsProductionAlreadyCreated()
    {
        var customerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var project = new Project
        {
            ProjectId = projectId,
            CustomerId = customerId,
            Status = ProjectStatus.ORDER_CONFIRMED
        };
        var repository = new FakeReopenProjectRepository("CUSTOMER", [project]);
        var service = ProjectServiceTestFactory.Create(
            repository,
            TestUnitOfWork.Instance,
            new ProjectServiceFactoryOptions
            {
                Orders = new FakeProjectOrderRepository
                {
                    Order = new Order
                    {
                        OrderId = orderId,
                        ProjectId = projectId,
                        QuotationId = Guid.NewGuid(),
                        Status = OrderStatus.CREATED
                    }
                },
                ProductionRequests = new FakeProjectProductionRequestRepository
                {
                    HasProductionRequest = true
                },
                Proposals = new FakeProjectReopenProposalRepository
                {
                    SelectedProposal = new Proposal
                    {
                        ProposalId = Guid.NewGuid(),
                        ProjectId = projectId,
                        Status = ProposalStatus.SELECTED,
                        SelectedAt = DateTime.UtcNow.AddDays(-1)
                    }
                }
            });

        var result = await service.ReopenProposalAsync(projectId, customerId);

        Assert.Equal(400, result.Status);
        Assert.Equal(ProjectReopenProposalErrorCodes.ProductionAlreadyCreated, result.ErrorCode);
    }

    [Fact]
    public async Task ReopenProposalAsync_WithProcessingDeposit_ReturnsActiveDepositCannotBeCancelled()
    {
        var customerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var quotationId = Guid.NewGuid();
        var project = new Project
        {
            ProjectId = projectId,
            CustomerId = customerId,
            Status = ProjectStatus.ORDER_CONFIRMED
        };
        var repository = new FakeReopenProjectRepository("CUSTOMER", [project]);
        var payments = new FakeProjectReopenPaymentRepository
        {
            DepositPayments =
            {
                new Payment
                {
                    PaymentId = Guid.NewGuid(),
                    OrderId = orderId,
                    PaymentType = PaymentType.DEPOSIT,
                    Status = PaymentStatus.PROCESSING
                }
            }
        };
        var service = ProjectServiceTestFactory.Create(
            repository,
            TestUnitOfWork.Instance,
            new ProjectServiceFactoryOptions
            {
                Orders = new FakeProjectOrderRepository
                {
                    Order = new Order
                    {
                        OrderId = orderId,
                        ProjectId = projectId,
                        QuotationId = quotationId,
                        Status = OrderStatus.DEPOSIT_PENDING
                    }
                },
                Quotations = new FakeProjectQuotationRepository
                {
                    Quotation = new Quotation
                    {
                        QuotationId = quotationId,
                        ProjectId = projectId,
                        Status = QuotationStatus.ACCEPTED
                    }
                },
                Proposals = new FakeProjectReopenProposalRepository
                {
                    SelectedProposal = new Proposal
                    {
                        ProposalId = Guid.NewGuid(),
                        ProjectId = projectId,
                        Status = ProposalStatus.SELECTED,
                        SelectedAt = DateTime.UtcNow.AddDays(-1)
                    }
                },
                Payments = payments
            });

        var result = await service.ReopenProposalAsync(projectId, customerId);

        Assert.Equal(400, result.Status);
        Assert.Equal(ProjectReopenProposalErrorCodes.ActiveDepositCannotBeCancelled, result.ErrorCode);
    }

    [Fact]
    public async Task ReopenProposalAsync_WithWrongProjectStatus_ReturnsReopenNotAllowed()
    {
        var customerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var project = new Project
        {
            ProjectId = projectId,
            CustomerId = customerId,
            Status = ProjectStatus.IN_PRODUCTION
        };
        var repository = new FakeReopenProjectRepository("CUSTOMER", [project]);
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.ReopenProposalAsync(projectId, customerId);

        Assert.Equal(400, result.Status);
        Assert.Equal(ProjectReopenProposalErrorCodes.ReopenNotAllowed, result.ErrorCode);
    }

    [Fact]
    public async Task ReopenProposalAsync_WhenAlreadyReopened_ReturnsStableSuccess()
    {
        var customerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var project = new Project
        {
            ProjectId = projectId,
            CustomerId = customerId,
            Status = ProjectStatus.PROPOSAL_CONSULTING,
            UpdatedAt = DateTime.UtcNow.AddHours(-2)
        };
        var repository = new FakeReopenProjectRepository("CUSTOMER", [project]);
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.ReopenProposalAsync(projectId, customerId);

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(ProjectStatus.PROPOSAL_CONSULTING, result.Data.NewStatus);
        Assert.Equal(ProjectStatus.PROPOSAL_CONSULTING, result.Data.OldStatus);
    }

    [Fact]
    public async Task ReopenProposalAsync_FromProposalSelected_WithDraftQuotation_Succeeds()
    {
        var customerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var quotationId = Guid.NewGuid();
        var selectedProposalId = Guid.NewGuid();
        var selectedAt = DateTime.UtcNow.AddDays(-1);

        var project = new Project
        {
            ProjectId = projectId,
            CustomerId = customerId,
            Status = ProjectStatus.PROPOSAL_SELECTED
        };
        var quotation = new Quotation
        {
            QuotationId = quotationId,
            ProjectId = projectId,
            ProposalId = selectedProposalId,
            Status = QuotationStatus.DRAFT
        };
        var selectedProposal = new Proposal
        {
            ProposalId = selectedProposalId,
            ProjectId = projectId,
            Status = ProposalStatus.SELECTED,
            SelectedAt = selectedAt
        };

        var repository = new FakeReopenProjectRepository("CUSTOMER", [project]);
        var quotations = new FakeProjectQuotationRepository { Quotation = quotation };
        var proposals = new FakeProjectReopenProposalRepository { SelectedProposal = selectedProposal };
        var unitOfWork = CreateTrackingUnitOfWork(repository.SaveChangesAsync);
        var service = ProjectServiceTestFactory.Create(
            repository,
            unitOfWork,
            new ProjectServiceFactoryOptions
            {
                Quotations = quotations,
                Proposals = proposals
            });

        var result = await service.ReopenProposalAsync(projectId, customerId);

        Assert.Equal(200, result.Status);
        Assert.Null(result.Data!.OrderId);
        Assert.Equal(QuotationStatus.CANCELLED, result.Data.QuotationStatus);
        Assert.Equal(ProposalStatus.PUBLISHED, result.Data.SelectedProposalStatus);
        Assert.Equal(ProjectStatus.PROPOSAL_CONSULTING, project.Status);
        Assert.Equal(QuotationStatus.CANCELLED, quotation.Status);
    }

    [Fact]
    public async Task ReopenProposalAsync_FromQuotationSent_Succeeds()
    {
        var customerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var quotationId = Guid.NewGuid();
        var selectedProposalId = Guid.NewGuid();

        var project = new Project
        {
            ProjectId = projectId,
            CustomerId = customerId,
            Status = ProjectStatus.QUOTATION_SENT
        };
        var quotation = new Quotation
        {
            QuotationId = quotationId,
            ProjectId = projectId,
            ProposalId = selectedProposalId,
            Status = QuotationStatus.SENT
        };
        var selectedProposal = new Proposal
        {
            ProposalId = selectedProposalId,
            ProjectId = projectId,
            Status = ProposalStatus.SELECTED,
            SelectedAt = DateTime.UtcNow.AddDays(-1)
        };

        var repository = new FakeReopenProjectRepository("CUSTOMER", [project]);
        var service = ProjectServiceTestFactory.Create(
            repository,
            CreateTrackingUnitOfWork(repository.SaveChangesAsync),
            new ProjectServiceFactoryOptions
            {
                Quotations = new FakeProjectQuotationRepository { Quotation = quotation },
                Proposals = new FakeProjectReopenProposalRepository { SelectedProposal = selectedProposal }
            });

        var result = await service.ReopenProposalAsync(projectId, customerId);

        Assert.Equal(200, result.Status);
        Assert.Equal(QuotationStatus.CANCELLED, quotation.Status);
        Assert.Equal(ProjectStatus.PROPOSAL_CONSULTING, project.Status);
    }

    [Fact]
    public async Task ReopenProposalAsync_FromOrderConfirmed_WithCreatedOrder_Succeeds()
    {
        var customerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var quotationId = Guid.NewGuid();
        var selectedProposalId = Guid.NewGuid();

        var project = new Project
        {
            ProjectId = projectId,
            CustomerId = customerId,
            Status = ProjectStatus.ORDER_CONFIRMED
        };
        var order = new Order
        {
            OrderId = orderId,
            ProjectId = projectId,
            QuotationId = quotationId,
            Status = OrderStatus.CREATED
        };
        var quotation = new Quotation
        {
            QuotationId = quotationId,
            ProjectId = projectId,
            Status = QuotationStatus.ACCEPTED
        };
        var selectedProposal = new Proposal
        {
            ProposalId = selectedProposalId,
            ProjectId = projectId,
            Status = ProposalStatus.SELECTED,
            SelectedAt = DateTime.UtcNow.AddDays(-1)
        };

        var repository = new FakeReopenProjectRepository("CUSTOMER", [project]);
        var service = ProjectServiceTestFactory.Create(
            repository,
            CreateTrackingUnitOfWork(repository.SaveChangesAsync),
            new ProjectServiceFactoryOptions
            {
                Orders = new FakeProjectOrderRepository { Order = order },
                Quotations = new FakeProjectQuotationRepository { Quotation = quotation },
                Proposals = new FakeProjectReopenProposalRepository { SelectedProposal = selectedProposal }
            });

        var result = await service.ReopenProposalAsync(projectId, customerId);

        Assert.Equal(200, result.Status);
        Assert.Equal(OrderStatus.CANCELLED, order.Status);
        Assert.Equal(QuotationStatus.CANCELLED, quotation.Status);
    }

    [Fact]
    public async Task ReopenProposalAsync_RestoresOnlyAutoRejectedSiblingProposals()
    {
        var customerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var selectedProposalId = Guid.NewGuid();
        var selectedAt = DateTime.UtcNow.AddDays(-1);
        var historicalRejectedAt = selectedAt.AddDays(-5);

        var project = new Project
        {
            ProjectId = projectId,
            CustomerId = customerId,
            Status = ProjectStatus.PROPOSAL_SELECTED
        };
        var selectedProposal = new Proposal
        {
            ProposalId = selectedProposalId,
            ProjectId = projectId,
            Status = ProposalStatus.SELECTED,
            SelectedAt = selectedAt
        };
        var autoRejectedSibling = new Proposal
        {
            ProposalId = Guid.NewGuid(),
            ProjectId = projectId,
            Status = ProposalStatus.REJECTED,
            RejectedAt = selectedAt
        };
        var historicalRejected = new Proposal
        {
            ProposalId = Guid.NewGuid(),
            ProjectId = projectId,
            Status = ProposalStatus.REJECTED,
            RejectedAt = historicalRejectedAt
        };
        var archivedProposal = new Proposal
        {
            ProposalId = Guid.NewGuid(),
            ProjectId = projectId,
            Status = ProposalStatus.ARCHIVED
        };

        var proposals = new FakeProjectReopenProposalRepository
        {
            SelectedProposal = selectedProposal,
            AutoRejectedProposals = { autoRejectedSibling, historicalRejected, archivedProposal }
        };
        var repository = new FakeReopenProjectRepository("CUSTOMER", [project]);
        var service = ProjectServiceTestFactory.Create(
            repository,
            CreateTrackingUnitOfWork(repository.SaveChangesAsync),
            new ProjectServiceFactoryOptions
            {
                Quotations = new FakeProjectQuotationRepository
                {
                    Quotation = new Quotation
                    {
                        QuotationId = Guid.NewGuid(),
                        ProjectId = projectId,
                        ProposalId = selectedProposalId,
                        Status = QuotationStatus.DRAFT
                    }
                },
                Proposals = proposals
            });

        var result = await service.ReopenProposalAsync(projectId, customerId);

        Assert.Equal(200, result.Status);
        Assert.Equal(1, result.Data!.RestoredProposalCount);
        Assert.Equal(ProposalStatus.PUBLISHED, autoRejectedSibling.Status);
        Assert.Equal(ProposalStatus.REJECTED, historicalRejected.Status);
        Assert.Equal(ProposalStatus.ARCHIVED, archivedProposal.Status);
    }

    [Fact]
    public async Task ReopenProposalAsync_CustomerOutsideProject_ReturnsForbidden()
    {
        var outsiderId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var project = new Project
        {
            ProjectId = projectId,
            CustomerId = Guid.NewGuid(),
            Status = ProjectStatus.PROPOSAL_SELECTED
        };
        var repository = new FakeReopenProjectRepository("CUSTOMER", [project]);
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.ReopenProposalAsync(projectId, outsiderId);

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task ReopenProposalAsync_WithoutSelectedProposal_ReturnsSelectedProposalNotFound()
    {
        var customerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var project = new Project
        {
            ProjectId = projectId,
            CustomerId = customerId,
            Status = ProjectStatus.PROPOSAL_SELECTED
        };
        var repository = new FakeReopenProjectRepository("CUSTOMER", [project]);
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.ReopenProposalAsync(projectId, customerId);

        Assert.Equal(400, result.Status);
        Assert.Equal(ProjectReopenProposalErrorCodes.SelectedProposalNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ReopenProposalAsync_WithUnassignedSales_ReturnsForbidden()
    {
        var salesId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var project = new Project
        {
            ProjectId = projectId,
            CustomerId = Guid.NewGuid(),
            AssignedSalesId = Guid.NewGuid(),
            Status = ProjectStatus.ORDER_CONFIRMED
        };
        var repository = new FakeReopenProjectRepository("SALES", [project]);
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.ReopenProposalAsync(projectId, salesId);

        Assert.Equal(403, result.Status);
    }
}
