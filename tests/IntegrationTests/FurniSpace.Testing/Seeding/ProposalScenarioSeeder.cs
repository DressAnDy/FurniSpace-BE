using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;

namespace FurniSpace.Testing.Seeding;

public sealed record ProposalConsultingScenario(
    Guid ProjectId,
    Guid CustomerAccountId,
    Guid SalesAccountId,
    Guid DesignerAccountId,
    Guid FloorAreaId,
    Guid CategoryId,
    Guid ProductId,
    Guid ProductVersionId);

public sealed record PublishedProposalScenario(
    Guid ProjectId,
    Guid CustomerAccountId,
    Guid SalesAccountId,
    Guid DesignerAccountId,
    Guid ProposalId,
    Guid SceneId,
    Guid ProposalItemId,
    Guid ProductVersionId,
    Guid? SecondProposalId = null);

public static class ProposalScenarioSeeder
{
    public static async Task<ProposalConsultingScenario> SeedProposalConsultingAsync(
        AppDbContext context,
        CancellationToken cancellationToken = default)
    {
        var roles = await CoreAccountSeeder.EnsureRolesAsync(
            context,
            cancellationToken,
            CoreRoles.Customer,
            CoreRoles.Sales,
            CoreRoles.Designer);

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var customer = CoreAccountSeeder.CreateAccount(
            roles[CoreRoles.Customer].RoleId,
            $"proposal-customer-{suffix}@integration.test",
            "Proposal Customer");
        var sales = CoreAccountSeeder.CreateAccount(
            roles[CoreRoles.Sales].RoleId,
            $"proposal-sales-{suffix}@integration.test",
            "Proposal Sales");
        var designer = CoreAccountSeeder.CreateAccount(
            roles[CoreRoles.Designer].RoleId,
            $"proposal-designer-{suffix}@integration.test",
            "Proposal Designer");

        var project = ProjectScenarioSeeder.CreateProject(
            customer.AccountId,
            sales.AccountId,
            $"PRJ-P-{suffix}",
            "Proposal Project",
            ProjectStatus.PROPOSAL_CONSULTING,
            designer.AccountId);

        var floor = MeasurementScenarioSeeder.CreateArea(
            project.ProjectId,
            "Ground Floor",
            ProjectAreaType.FLOOR,
            floorNumber: 1,
            status: ProjectAreaStatus.VERIFIED);

        var (category, product, productVersion) = CreateCatalog(suffix);

        context.AccountSet.AddRange(customer, sales, designer);
        context.ProjectSet.Add(project);
        context.ProjectAreaSet.Add(floor);
        context.CategorySet.Add(category);
        context.ProductSet.Add(product);
        context.ProductVersionSet.Add(productVersion);
        await context.SaveChangesAsync(cancellationToken);

        return new ProposalConsultingScenario(
            project.ProjectId,
            customer.AccountId,
            sales.AccountId,
            designer.AccountId,
            floor.ProjectAreaId,
            category.CategoryId,
            product.ProductId,
            productVersion.ProductVersionId);
    }

    public static async Task<PublishedProposalScenario> SeedPublishedProposalAsync(
        AppDbContext context,
        bool includeSecondPublishedProposal = false,
        CancellationToken cancellationToken = default)
    {
        var baseScenario = await SeedProposalConsultingAsync(context, cancellationToken);
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var proposal = CreateProposal(
            baseScenario.ProjectId,
            baseScenario.DesignerAccountId,
            "Published Option A",
            ProposalStatus.PUBLISHED,
            versionNo: 1);
        proposal.PublishedAt = CoreAccountSeeder.FixedTimestamp;

        var scene = CreateScene(proposal.ProposalId, baseScenario.DesignerAccountId, "Main Layout");
        var sceneArea = new ProposalSceneArea
        {
            ProposalSceneAreaId = Guid.NewGuid(),
            SceneId = scene.SceneId,
            ProjectAreaId = baseScenario.FloorAreaId,
            SortOrder = 1,
            CreatedAt = CoreAccountSeeder.FixedTimestamp
        };
        scene.SceneAreas.Add(sceneArea);

        var item = CreateProposalItem(
            proposal.ProposalId,
            scene.SceneId,
            baseScenario.FloorAreaId,
            baseScenario.ProductVersionId,
            "Office Desk");

        context.ProposalSet.Add(proposal);
        context.ProposalSceneSet.Add(scene);
        context.ProposalItemSet.Add(item);

        Guid? secondProposalId = null;
        if (includeSecondPublishedProposal)
        {
            var second = CreateProposal(
                baseScenario.ProjectId,
                baseScenario.DesignerAccountId,
                "Published Option B",
                ProposalStatus.PUBLISHED,
                versionNo: 2);
            second.PublishedAt = CoreAccountSeeder.FixedTimestamp;
            context.ProposalSet.Add(second);
            secondProposalId = second.ProposalId;
        }

        await context.SaveChangesAsync(cancellationToken);

        return new PublishedProposalScenario(
            baseScenario.ProjectId,
            baseScenario.CustomerAccountId,
            baseScenario.SalesAccountId,
            baseScenario.DesignerAccountId,
            proposal.ProposalId,
            scene.SceneId,
            item.ProposalItemId,
            baseScenario.ProductVersionId,
            secondProposalId);
    }

    public static Proposal CreateProposal(
        Guid projectId,
        Guid createdBy,
        string name,
        ProposalStatus status,
        int versionNo = 1) =>
        new()
        {
            ProposalId = Guid.NewGuid(),
            ProjectId = projectId,
            ProposalName = name,
            Description = "Integration proposal",
            VersionNo = versionNo,
            Status = status,
            CreatedBy = createdBy,
            CreatedAt = CoreAccountSeeder.FixedTimestamp
        };

    public static ProposalScene CreateScene(
        Guid proposalId,
        Guid createdBy,
        string sceneName) =>
        new()
        {
            SceneId = Guid.NewGuid(),
            ProposalId = proposalId,
            SceneName = sceneName,
            SceneType = ProposalSceneType.ROOM_PLANNER,
            VersionNo = 1,
            IsActive = true,
            CreatedBy = createdBy,
            CreatedAt = CoreAccountSeeder.FixedTimestamp
        };

    public static ProposalItem CreateProposalItem(
        Guid proposalId,
        Guid? sceneId,
        Guid? projectAreaId,
        Guid productVersionId,
        string itemName,
        int quantity = 2,
        decimal unitPrice = 5_000_000m) =>
        new()
        {
            ProposalItemId = Guid.NewGuid(),
            ProposalId = proposalId,
            SceneId = sceneId,
            ProjectAreaId = projectAreaId,
            ProductVersionId = productVersionId,
            ItemName = itemName,
            ItemType = "PRODUCT",
            Quantity = quantity,
            UnitPriceSnapshot = unitPrice,
            TotalPriceSnapshot = unitPrice * quantity,
            CreatedAt = CoreAccountSeeder.FixedTimestamp
        };

    public static (Category Category, Product Product, ProductVersion ProductVersion) CreateCatalog(
        string suffix)
    {
        var category = new Category
        {
            CategoryId = Guid.NewGuid(),
            CategoryName = $"Office-{suffix}",
            Status = ProductStatus.ACTIVE
        };
        var product = new Product
        {
            ProductId = Guid.NewGuid(),
            CategoryId = category.CategoryId,
            ProductCode = $"PRD-{suffix}",
            ProductName = "Office Desk",
            Status = ProductStatus.ACTIVE,
            CreatedAt = CoreAccountSeeder.FixedTimestamp
        };
        var productVersion = new ProductVersion
        {
            ProductVersionId = Guid.NewGuid(),
            ProductId = product.ProductId,
            VersionCode = $"STD-{suffix}",
            VersionName = "Standard Desk",
            VersionType = ProductVersionType.STANDARD,
            DimensionUnit = "cm",
            Material = "Oak",
            Color = "Natural",
            Width = 120,
            Height = 75,
            Depth = 60,
            EstimatedPrice = 5_000_000m,
            IsDefault = true,
            IsPublic = true,
            IsProjectSpecific = false,
            Status = ProductStatus.ACTIVE,
            CreatedAt = CoreAccountSeeder.FixedTimestamp
        };

        return (category, product, productVersion);
    }
}
