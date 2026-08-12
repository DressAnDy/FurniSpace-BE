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
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

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
                Payments = payments
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
            Status = ProjectStatus.PROPOSAL_CONSULTING
        };
        var repository = new FakeReopenProjectRepository("CUSTOMER", [project]);
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.ReopenProposalAsync(projectId, customerId);

        Assert.Equal(400, result.Status);
        Assert.Equal(ProjectReopenProposalErrorCodes.ReopenNotAllowed, result.ErrorCode);
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
