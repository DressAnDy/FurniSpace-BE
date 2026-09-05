using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    private const string CancelledAtColumnName = "cancelled_at";
    private const string CancellationReasonColumnName = "cancellation_reason";
    private const string UuidColumnType = "uuid";
    private const string TimestampWithTimeZoneColumnType = "timestamp with time zone";
    private const string TextColumnType = "text";
    private const string Decimal12ColumnType = "numeric(12,2)";
    private const string Decimal14ColumnType = "numeric(14,2)";
    private const string TaxRateColumnType = "numeric(7,4)";
    private const string Decimal10ColumnType = "numeric(10,2)";
    private const string IntegerColumnType = "integer";
    private const string DateColumnType = "date";
    private const string BooleanColumnType = "boolean";
    private const string BigIntColumnType = "bigint";
    private const string JsonbColumnType = "jsonb";
    private const string Varchar10ColumnType = "varchar(10)";
    private const string Varchar20ColumnType = "varchar(20)";
    private const string Varchar30ColumnType = "varchar(30)";
    private const string Varchar50ColumnType = "varchar(50)";
    private const string Varchar100ColumnType = "varchar(100)";
    private const string Varchar150ColumnType = "varchar(150)";
    private const string Varchar255ColumnType = "varchar(255)";
    private const string AccountStatusColumnType = "account_status";
    private const string ProjectStatusColumnType = "project_status";
    private const string ProjectAreaTypeColumnType = "project_area_type";
    private const string ProjectAreaStatusColumnType = "project_area_status";
    private const string ProjectPhaseTypeColumnType = "project_phase_type";
    private const string ProjectScheduleTypeColumnType = "project_schedule_type";
    private const string ProjectScheduleStatusColumnType = "project_schedule_status";
    private const string ProposalStatusColumnType = "proposal_status";
    private const string ProposalSceneTypeColumnType = "proposal_scene_type";
    private const string ProposalSceneVariantStatusColumnType = "proposal_scene_variant_status";
    private const string ProposalSceneVariantTypeColumnType = "proposal_scene_variant_type";
    private const string CustomizationStatusColumnType = "customization_status";
    private const string CustomizationVersionStatusColumnType = "customization_version_status";
    private const string ProductionFeasibilityStatusColumnType = "production_feasibility_status";
    private const string SourceProductVersionIdColumnName = "source_product_version_id";
    private const string AcceptedRequestVersionIdColumnName = "accepted_request_version_id";
    private const string CustomizationRequestVersionIdColumnName = "customization_request_version_id";
    private const string CustomizationRequestIdColumnName = "customization_request_id";
    private const string QuotationStatusColumnType = "quotation_status";
    private const string QuotationItemTypeColumnType = "quotation_item_type";
    private const string OrderStatusColumnType = "order_status";
    private const string OrderItemStatusColumnType = "order_item_status";
    private const string DeliveryStatusColumnType = "delivery_status";
    private const string PaymentStatusColumnType = "payment_status";
    private const string PaymentTypeColumnType = "payment_type";
    private const string PaymentProviderColumnType = "payment_provider";
    private const string PaymentMethodColumnType = "payment_method";
    private const string PaymentTransactionTypeColumnType = "payment_transaction_type";
    private const string PaymentTransactionStatusColumnType = "payment_transaction_status";
    private const string ProductionRequestStatusColumnType = "production_request_status";
    private const string ProductionItemStatusColumnType = "production_item_status";
    private const string OperationalDelayPhaseColumnType = "operational_delay_phase";
    private const string OperationalDelayStateColumnType = "operational_delay_state";
    private const string DeliveryProductIssueTypeColumnType = "delivery_product_issue_type";

    private const string ProjectChatTypeColumnType = "project_chat_type";
    private const string ProjectChatStatusColumnType = "project_chat_status";
    private const string ProjectChatMessageTypeColumnType = "project_chat_message_type";
    private const string FileStatusColumnType = "file_status";
    private const string FileVisibilityColumnType = "file_visibility";
    private const string FileTypeColumnType = "file_type";
    private const string ProductStatusColumnType = "product_status";
    private const string ProductVersionTypeColumnType = "product_version_type";
    private const string LayoutAssetTypeColumnType = "layout_asset_type";
    private const string LayoutAssetStatusColumnType = "layout_asset_status";
    private const string ProjectShowcaseStatusColumnType = "project_showcase_status";
    private const string ProjectShowcaseMediaTypeColumnType = "project_showcase_media_type";
    private const string CreatedAtColumnName = "created_at";
    private const string UpdatedAtColumnName = "updated_at";
    private const string CompletedAtColumnName = "completed_at";
    private const string StatusColumnName = "status";
    private const string ProjectIdColumnName = "project_id";
    private const string DescriptionColumnName = "description";
    private const string ProposalIdColumnName = "proposal_id";
    private const string SceneIdColumnName = "scene_id";
    private const string ProductVersionIdColumnName = "product_version_id";
    private const string OrderIdColumnName = "order_id";
    private const string CreatedByColumnName = "created_by";
    private const string QuotationIdColumnName = "quotation_id";
    private const string QuantityColumnName = "quantity";
    private const string ProjectAreaIdColumnName = "project_area_id";
    private const string NoteColumnName = "note";
    private const string WidthColumnName = "width";
    private const string VersionNoColumnName = "version_no";
    private const string TitleColumnName = "title";
    private const string SubtotalAmountColumnName = "subtotal_amount";
    private const string TotalDiscountAmountColumnName = "total_discount_amount";
    private const string PreVatAmountColumnName = "pre_vat_amount";
    private const string VatRateColumnName = "vat_rate";
    private const string VatAmountColumnName = "vat_amount";
    private const string TotalAmountColumnName = "total_amount";
    private const string GrossAmountColumnName = "gross_amount";
    private const string TaxRateColumnName = "tax_rate";
    private const string CustomizationUnitAdditionalCostColumnName = "customization_unit_additional_cost";
    private const string RejectedAtColumnName = "rejected_at";
    private const string ProposalItemIdColumnName = "proposal_item_id";
    private const string ProductVersionNameSnapshotColumnName = "product_version_name_snapshot";
    private const string ProductNameSnapshotColumnName = "product_name_snapshot";
    private const string HeightColumnName = "height";
    private const string DiscountAmountColumnName = "discount_amount";
    private const string CustomerIdColumnName = "customer_id";

    public DbSet<Role> RoleSet => Set<Role>();
    public DbSet<Account> AccountSet => Set<Account>();
    public DbSet<BusinessType> BusinessTypeSet => Set<BusinessType>();
    public DbSet<Category> CategorySet => Set<Category>();
    public DbSet<Product> ProductSet => Set<Product>();
    public DbSet<ProductVersion> ProductVersionSet => Set<ProductVersion>();
    public DbSet<StoredFile> StoredFileSet => Set<StoredFile>();
    public DbSet<FileLink> FileLinkSet => Set<FileLink>();
    public DbSet<Project> ProjectSet => Set<Project>();
    public DbSet<Notification> NotificationSet => Set<Notification>();
    public DbSet<ProjectChat> ProjectChatSet => Set<ProjectChat>();
    public DbSet<ProjectChatMessage> ProjectChatMessageSet => Set<ProjectChatMessage>();
    public DbSet<ProjectArea> ProjectAreaSet => Set<ProjectArea>();
    public DbSet<ProjectPhaseTimeline> ProjectPhaseTimelineSet => Set<ProjectPhaseTimeline>();
    public DbSet<ProjectSchedule> ProjectScheduleSet => Set<ProjectSchedule>();
    public DbSet<Proposal> ProposalSet => Set<Proposal>();
    public DbSet<ProposalScene> ProposalSceneSet => Set<ProposalScene>();
    public DbSet<ProposalSceneArea> ProposalSceneAreaSet => Set<ProposalSceneArea>();
    public DbSet<ProposalItem> ProposalItemSet => Set<ProposalItem>();
    public DbSet<ProposalSceneVariant> ProposalSceneVariantSet => Set<ProposalSceneVariant>();
    public DbSet<CustomizationRequest> CustomizationRequestSet => Set<CustomizationRequest>();
    public DbSet<CustomizationRequestVersion> CustomizationRequestVersionSet => Set<CustomizationRequestVersion>();
    public DbSet<Quotation> QuotationSet => Set<Quotation>();
    public DbSet<QuotationItem> QuotationItemSet => Set<QuotationItem>();
    public DbSet<Order> OrderSet => Set<Order>();
    public DbSet<OrderItem> OrderItemSet => Set<OrderItem>();
    public DbSet<Delivery> DeliverySet => Set<Delivery>();
    public DbSet<DeliveryItem> DeliveryItemSet => Set<DeliveryItem>();
    public DbSet<Payment> PaymentSet => Set<Payment>();
    public DbSet<PaymentTransaction> PaymentTransactionSet => Set<PaymentTransaction>();
    public DbSet<ProductionRequest> ProductionRequestSet => Set<ProductionRequest>();
    public DbSet<ProductionItem> ProductionItemSet => Set<ProductionItem>();
    public DbSet<OperationalDelayReport> OperationalDelayReportSet => Set<OperationalDelayReport>();
    public DbSet<DeliveryProductIssueReport> DeliveryProductIssueReportSet => Set<DeliveryProductIssueReport>();
    public DbSet<ProjectReview> ProjectReviewSet => Set<ProjectReview>();
    public DbSet<ProjectShowcase> ProjectShowcaseSet => Set<ProjectShowcase>();
    public DbSet<ProjectShowcaseMedia> ProjectShowcaseMediaSet => Set<ProjectShowcaseMedia>();
    public DbSet<LayoutAsset> LayoutAssetSet => Set<LayoutAsset>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasAnnotation("Npgsql:Enum:account_status", "ACTIVE,INACTIVE,SUSPENDED");
        modelBuilder.HasAnnotation("Npgsql:Enum:project_status", "SUBMITTED,IN_CONSULTATION,NEED_BASIC_INFORMATION,WAITING_FOR_DESIGNER_ASSIGNMENT,MEASUREMENT_REQUIRED,SPACE_VERIFIED,PROPOSAL_CONSULTING,PROPOSAL_SELECTED,QUOTATION_SENT,QUOTATION_REVISION_REQUESTED,ORDER_CONFIRMED,IN_PRODUCTION,READY_FOR_DELIVERY,DELIVERING,DELIVERED,COMPLETED,REJECTED");
        modelBuilder.HasAnnotation("Npgsql:Enum:project_area_type", "STORE,FLOOR,ROOM,ZONE,OUTDOOR_AREA,OTHER");
        modelBuilder.HasAnnotation("Npgsql:Enum:project_area_status", "DRAFT,NEED_MEASUREMENT,MEASURED,VERIFIED,CANCELLED");
        modelBuilder.HasAnnotation("Npgsql:Enum:project_phase_type", "CONSULTATION,MEASUREMENT,PROPOSAL,QUOTATION,PRODUCTION,DELIVERY,HANDOVER");
        modelBuilder.HasAnnotation("Npgsql:Enum:project_schedule_type", "MEASUREMENT,CONSULTATION,DESIGN_REVIEW,DELIVERY,HANDOVER,OTHER");
        modelBuilder.HasAnnotation("Npgsql:Enum:project_schedule_status", "PENDING_CONFIRMATION,CONFIRMED,COMPLETED,CANCELLED");
        modelBuilder.HasAnnotation("Npgsql:Enum:proposal_status", "DRAFT,PUBLISHED,SELECTED,REVISION_REQUESTED,REJECTED,ARCHIVED");
        modelBuilder.HasAnnotation("Npgsql:Enum:proposal_scene_type", "TWO_D,THREE_D,ROOM_PLANNER");
        modelBuilder.HasAnnotation("Npgsql:Enum:proposal_scene_variant_status", "DRAFT,SUBMITTED,ACCEPTED,REJECTED,APPLIED");
        modelBuilder.HasAnnotation("Npgsql:Enum:proposal_scene_variant_type", "CUSTOMER_SUGGESTION,DESIGNER_REVISION");
        modelBuilder.HasAnnotation("Npgsql:Enum:customization_status", "SUBMITTED,REVIEWING,ACCEPTED,CANCELLED");
        modelBuilder.HasAnnotation("Npgsql:Enum:customization_version_status", "DRAFT,REVIEWING,PRODUCTION_REJECTED,ACCEPTED,WITHDRAWN");
        modelBuilder.HasAnnotation("Npgsql:Enum:production_feasibility_status", "PENDING,FEASIBLE,NOT_FEASIBLE");
        modelBuilder.HasAnnotation("Npgsql:Enum:quotation_status", "DRAFT,SENT,REVISION_REQUESTED,REVISED,ACCEPTED,REJECTED,EXPIRED,CANCELLED");
        modelBuilder.HasAnnotation("Npgsql:Enum:order_status", "CREATED,DEPOSIT_PENDING,DEPOSIT_PAID,IN_PRODUCTION,READY_FOR_DELIVERY,DELIVERING,DELIVERED,FINAL_PAYMENT_PENDING,COMPLETED,CANCELLED");
        modelBuilder.HasAnnotation("Npgsql:Enum:order_item_status", "PENDING,IN_PRODUCTION,READY,PARTIALLY_DELIVERED,UNAVAILABLE,DELIVERED,CANCELLED");
        modelBuilder.HasAnnotation("Npgsql:Enum:delivery_status", "IN_PROGRESS,COMPLETED");
        modelBuilder.HasAnnotation("Npgsql:Enum:payment_status", "PENDING,PROCESSING,PAID,CANCELLED,EXPIRED,REFUNDED");
        modelBuilder.HasAnnotation("Npgsql:Enum:payment_type", "PROJECT_START_FEE,DEPOSIT,REMAINING_PAYMENT,FULL_PAYMENT,REFUND,OTHER");
        modelBuilder.HasAnnotation("Npgsql:Enum:payment_provider", "PAYOS,SEPAY,CASH,MANUAL_BANK_TRANSFER,OTHER");
        modelBuilder.HasAnnotation("Npgsql:Enum:payment_method", "PAYMENT_LINK,QR_CODE,BANK_TRANSFER,CASH,OTHER");
        modelBuilder.HasAnnotation("Npgsql:Enum:payment_transaction_type", "CHARGE,REFUND,ADJUSTMENT");
        modelBuilder.HasAnnotation("Npgsql:Enum:payment_transaction_status", "PENDING,SUCCESS,FAILED,CANCELLED");
        modelBuilder.HasAnnotation("Npgsql:Enum:production_request_status", "PENDING,IN_PRODUCTION,COMPLETED,CANCELLED");
        modelBuilder.HasAnnotation("Npgsql:Enum:production_item_status", "PENDING,IN_PRODUCTION,COMPLETED,CANCELLED");
        modelBuilder.HasAnnotation("Npgsql:Enum:operational_delay_phase", "PRODUCTION,DELIVERY");
        modelBuilder.HasAnnotation("Npgsql:Enum:operational_delay_state", "AT_RISK,OVERDUE");
        modelBuilder.HasAnnotation("Npgsql:Enum:delivery_product_issue_type", "DAMAGED,WRONG_ITEM,WRONG_SPECIFICATION,MISSING_PART,QUALITY_DEFECT,INSTALLATION_ISSUE,QUANTITY_MISMATCH,OTHER");
        modelBuilder.HasAnnotation("Npgsql:Enum:notification_status", "UNREAD,READ");
        modelBuilder.HasAnnotation("Npgsql:Enum:project_chat_type", "SALES,DESIGNER,PRODUCTION,DELIVERY,GENERAL,INTERNAL");
        modelBuilder.HasAnnotation("Npgsql:Enum:project_chat_status", "OPEN,CLOSED,ARCHIVED");
        modelBuilder.HasAnnotation("Npgsql:Enum:project_chat_message_type", "TEXT,FILE,SYSTEM");
        modelBuilder.HasAnnotation("Npgsql:Enum:file_status", "ACTIVE,ARCHIVED");
        modelBuilder.HasAnnotation("Npgsql:Enum:file_visibility", "CUSTOMER_VISIBLE,STAFF_ONLY,PRIVATE");
        modelBuilder.HasAnnotation("Npgsql:Enum:file_type", "SPACE_IMAGE,FLOOR_PLAN,REFERENCE_IMAGE,BRAND_ASSET,CAD_FILE,PDF_DRAWING,MEASUREMENT_REPORT,LIDAR_SCAN,MODEL_3D,TEXTURE,PREVIEW,PRODUCT_PREVIEW,PROPOSAL_PREVIEW,PROPOSAL_FILE,QUOTATION_FILE,ORDER_DOCUMENT,PRODUCTION_FILE,DELIVERY_PHOTO,DELIVERY_NOTE,PRODUCT_ISSUE_EVIDENCE,REVIEW_IMAGE,PORTFOLIO_IMAGE,OTHER");
        modelBuilder.HasAnnotation("Npgsql:Enum:product_status", "ACTIVE,INACTIVE,ARCHIVED");
        modelBuilder.HasAnnotation("Npgsql:Enum:product_version_type", "STANDARD,CUSTOM,PROJECT_SPECIFIC");
        modelBuilder.HasAnnotation("Npgsql:Enum:layout_asset_type", "WALL_MATERIAL,FLOOR_MATERIAL,STAIR,DOOR,WINDOW,COLUMN,BEAM,DECORATIVE_WALL,DECORATIVE_FLOOR,DECORATIVE_OBJECT,OTHER");
        modelBuilder.HasAnnotation("Npgsql:Enum:layout_asset_status", "ACTIVE,INACTIVE,ARCHIVED");
        modelBuilder.HasAnnotation("Npgsql:Enum:project_showcase_status", "DRAFT,PENDING_REVIEW,PUBLISHED,ARCHIVED");
        modelBuilder.HasAnnotation("Npgsql:Enum:project_showcase_media_type", "BEFORE,AFTER,FINAL,DETAIL,OTHER");

        ConfigureRoles(modelBuilder);
        ConfigureAccounts(modelBuilder);
        ConfigureBusinessTypes(modelBuilder);
        ConfigureCategories(modelBuilder);
        ConfigureProducts(modelBuilder);
        ConfigureProductVersions(modelBuilder);
        ConfigureFiles(modelBuilder);
        ConfigureFileLinks(modelBuilder);
        ConfigureProjects(modelBuilder);
        ConfigureNotifications(modelBuilder);
        ConfigureProjectChats(modelBuilder);
        ConfigureProjectChatMessages(modelBuilder);
        ConfigureProjectAreas(modelBuilder);
        ConfigureProjectPhaseTimelines(modelBuilder);
        ConfigureProjectSchedules(modelBuilder);
        ConfigureProposals(modelBuilder);
        ConfigureProposalScenes(modelBuilder);
        ConfigureProposalSceneAreas(modelBuilder);
        ConfigureProposalItems(modelBuilder);
        ConfigureProposalSceneVariants(modelBuilder);
        ConfigureCustomizationRequests(modelBuilder);
        ConfigureCustomizationRequestVersions(modelBuilder);
        ConfigureQuotations(modelBuilder);
        ConfigureQuotationItems(modelBuilder);
        ConfigureOrders(modelBuilder);
        ConfigureOrderItems(modelBuilder);
        ConfigureDeliveries(modelBuilder);
        ConfigureDeliveryItems(modelBuilder);
        ConfigurePayments(modelBuilder);
        ConfigurePaymentTransactions(modelBuilder);
        ConfigureProductionRequests(modelBuilder);
        ConfigureProductionItems(modelBuilder);
        ConfigureOperationalDelayReports(modelBuilder);
        ConfigureDeliveryProductIssueReports(modelBuilder);
        ConfigureProjectReviews(modelBuilder);
        ConfigureProjectShowcases(modelBuilder);
        ConfigureProjectShowcaseMedia(modelBuilder);
        ConfigureLayoutAssets(modelBuilder);
    }

    private static void ConfigureRoles(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("roles");
            entity.HasKey(e => e.RoleId);
            entity.Property(e => e.RoleId).HasColumnName("role_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.RoleName).HasColumnName("role_name").HasColumnType(Varchar50ColumnType).IsRequired();
            entity.Property(e => e.Description).HasColumnName(DescriptionColumnName).HasColumnType(TextColumnType);
            entity.Property(e => e.CreatedAt).HasColumnName(CreatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.UpdatedAt).HasColumnName(UpdatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.HasIndex(e => e.RoleName).IsUnique();
        });
    }

    private static void ConfigureAccounts(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(entity =>
        {
            entity.ToTable("accounts");
            entity.HasKey(e => e.AccountId);
            entity.Property(e => e.AccountId).HasColumnName("account_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.RoleId).HasColumnName("role_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.Email).HasColumnName("email").HasColumnType(Varchar100ColumnType).IsRequired();
            entity.Property(e => e.PasswordHash).HasColumnName("password_hash").HasColumnType(Varchar255ColumnType).IsRequired();
            entity.Property(e => e.FullName).HasColumnName("full_name").HasColumnType(Varchar100ColumnType).IsRequired();
            entity.Property(e => e.Phone).HasColumnName("phone").HasColumnType(Varchar20ColumnType);
            entity.Property(e => e.AvatarUrl).HasColumnName("avatar_url").HasColumnType(TextColumnType);
            entity.Property(e => e.Status).HasColumnName(StatusColumnName).HasColumnType(AccountStatusColumnType).HasDefaultValueSql("'ACTIVE'::account_status");
            entity.Property(e => e.CreatedAt).HasColumnName(CreatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.UpdatedAt).HasColumnName(UpdatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at").HasColumnType(TimestampWithTimeZoneColumnType);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => new { e.CreatedAt, e.Email })
                .HasDatabaseName("idx_accounts_active_list_sort")
                .HasFilter("deleted_at IS NULL")
                .IsDescending(true, false);
            entity.HasIndex(e => new { e.Status, e.CreatedAt, e.Email })
                .HasDatabaseName("idx_accounts_active_status_list_sort")
                .HasFilter("deleted_at IS NULL")
                .IsDescending(false, true, false);
            entity.HasOne<Role>().WithMany().HasForeignKey(e => e.RoleId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureBusinessTypes(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BusinessType>(entity =>
        {
            entity.ToTable("business_types");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasColumnType(IntegerColumnType);
            entity.Property(e => e.Code).HasColumnName("code").HasColumnType(Varchar50ColumnType).IsRequired();
            entity.Property(e => e.Name).HasColumnName("name").HasColumnType(Varchar150ColumnType).IsRequired();
            entity.Property(e => e.Description).HasColumnName(DescriptionColumnName).HasColumnType(TextColumnType);
            entity.Property(e => e.Status).HasColumnName(StatusColumnName).HasColumnType(BooleanColumnType).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName(CreatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.UpdatedAt).HasColumnName(UpdatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasIndex(e => e.Status);
        });
    }

    private static void ConfigureCategories(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("categories");
            entity.HasKey(e => e.CategoryId);
            entity.Property(e => e.CategoryId).HasColumnName("category_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.CategoryName).HasColumnName("category_name").HasColumnType(Varchar100ColumnType).IsRequired();
            entity.Property(e => e.Description).HasColumnName(DescriptionColumnName).HasColumnType(TextColumnType);
            entity.Property(e => e.Status).HasColumnName(StatusColumnName).HasColumnType(ProductStatusColumnType).HasDefaultValueSql("'ACTIVE'::product_status");
            entity.Property(e => e.CreatedAt).HasColumnName(CreatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.UpdatedAt).HasColumnName(UpdatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.HasIndex(e => new { e.CategoryName, e.CategoryId })
                .HasDatabaseName("idx_categories_list_sort");
        });
    }

    private static void ConfigureProducts(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("products");
            entity.HasKey(e => e.ProductId);
            entity.Property(e => e.ProductId).HasColumnName("product_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.CategoryId).HasColumnName("category_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.BusinessTypeIds).HasColumnName("business_type_ids").HasColumnType("integer[]");
            entity.Property(e => e.ProductCode).HasColumnName("product_code").HasColumnType(Varchar50ColumnType);
            entity.Property(e => e.ProductName).HasColumnName("product_name").HasColumnType(Varchar150ColumnType).IsRequired();
            entity.Property(e => e.Description).HasColumnName(DescriptionColumnName).HasColumnType(TextColumnType);
            entity.Property(e => e.Status).HasColumnName(StatusColumnName).HasColumnType(ProductStatusColumnType).HasDefaultValueSql("'ACTIVE'::product_status");
            entity.Property(e => e.CreatedAt).HasColumnName(CreatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.UpdatedAt).HasColumnName(UpdatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.HasIndex(e => e.ProductCode).IsUnique();
            entity.HasIndex(e => e.BusinessTypeIds)
                .HasDatabaseName("idx_products_business_type_ids")
                .HasMethod("gin");
            entity.HasIndex(e => new { e.CreatedAt, e.ProductName })
                .HasDatabaseName("idx_products_list_sort")
                .IsDescending(true, false);
            entity.HasIndex(e => new { e.CategoryId, e.CreatedAt, e.ProductName })
                .HasDatabaseName("idx_products_category_list_sort")
                .IsDescending(false, true, false);
            entity.HasOne<Category>().WithMany().HasForeignKey(e => e.CategoryId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureProductVersions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProductVersion>(entity =>
        {
            entity.ToTable("product_versions", table =>
            {
                table.HasCheckConstraint(
                    "ck_product_versions_project_specific",
                    "(version_type = 'PROJECT_SPECIFIC'::product_version_type AND project_id IS NOT NULL AND is_project_specific = TRUE AND is_public = FALSE AND is_default = FALSE) OR version_type <> 'PROJECT_SPECIFIC'::product_version_type");
            });
            entity.HasKey(e => e.ProductVersionId);
            entity.Property(e => e.ProductVersionId).HasColumnName(ProductVersionIdColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.ProductId).HasColumnName("product_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.ProjectId).HasColumnName(ProjectIdColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.DimensionUnit).HasColumnName("dimension_unit").HasColumnType(Varchar10ColumnType).HasDefaultValue("cm");
            entity.Property(e => e.VersionCode).HasColumnName("version_code").HasColumnType(Varchar50ColumnType).IsRequired();
            entity.Property(e => e.VersionName).HasColumnName("version_name").HasColumnType(Varchar150ColumnType).IsRequired();
            entity.Property(e => e.VersionType).HasColumnName("version_type").HasColumnType(ProductVersionTypeColumnType).HasDefaultValueSql("'STANDARD'::product_version_type");
            entity.Property(e => e.Material).HasColumnName("material").HasColumnType(Varchar100ColumnType);
            entity.Property(e => e.Color).HasColumnName("color").HasColumnType(Varchar100ColumnType);
            entity.Property(e => e.Width).HasColumnName(WidthColumnName).HasColumnType(Decimal10ColumnType);
            entity.Property(e => e.Height).HasColumnName(HeightColumnName).HasColumnType(Decimal10ColumnType);
            entity.Property(e => e.Depth).HasColumnName("depth").HasColumnType(Decimal10ColumnType);
            entity.Property(e => e.EstimatedPrice).HasColumnName("estimated_price").HasColumnType(Decimal12ColumnType);
            entity.Property(e => e.IsDefault).HasColumnName("is_default").HasColumnType(BooleanColumnType).HasDefaultValue(false);
            entity.Property(e => e.IsPublic).HasColumnName("is_public").HasColumnType(BooleanColumnType).HasDefaultValue(true);
            entity.Property(e => e.IsProjectSpecific).HasColumnName("is_project_specific").HasColumnType(BooleanColumnType).HasDefaultValue(false);
            entity.Property(e => e.Status).HasColumnName(StatusColumnName).HasColumnType(ProductStatusColumnType).HasDefaultValueSql("'ACTIVE'::product_status");
            entity.Property(e => e.CreatedAt).HasColumnName(CreatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.UpdatedAt).HasColumnName(UpdatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.HasIndex(e => e.VersionCode).IsUnique();
            entity.HasOne<Product>().WithMany().HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Project>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureFiles(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StoredFile>(entity =>
        {
            entity.ToTable("files");
            entity.HasKey(e => e.FileId);
            entity.Property(e => e.FileId).HasColumnName("file_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.UploadedBy).HasColumnName("uploaded_by").HasColumnType(UuidColumnType).IsRequired();
            entity.Property(e => e.OriginalFileName).HasColumnName("original_file_name").HasColumnType(Varchar255ColumnType).IsRequired();
            entity.Property(e => e.StoredFileName).HasColumnName("stored_file_name").HasColumnType(Varchar255ColumnType).IsRequired();
            entity.Property(e => e.FileUrl).HasColumnName("file_url").HasColumnType(TextColumnType).IsRequired();
            entity.Property(e => e.StoragePath).HasColumnName("storage_path").HasColumnType(TextColumnType).IsRequired();
            entity.Property(e => e.MimeType).HasColumnName("mime_type").HasColumnType(Varchar100ColumnType).IsRequired();
            entity.Property(e => e.FileExtension).HasColumnName("file_extension").HasColumnType(Varchar20ColumnType);
            entity.Property(e => e.FileSizeBytes).HasColumnName("file_size_bytes").HasColumnType(BigIntColumnType).IsRequired();
            entity.Property(e => e.Checksum).HasColumnName("checksum").HasColumnType(Varchar255ColumnType);
            entity.Property(e => e.Status).HasColumnName(StatusColumnName).HasColumnType(FileStatusColumnType).HasDefaultValueSql("'ACTIVE'::file_status");
            entity.Property(e => e.UploadedAt).HasColumnName("uploaded_at").HasColumnType(TimestampWithTimeZoneColumnType).IsRequired();
            entity.Property(e => e.ArchivedAt).HasColumnName("archived_at").HasColumnType(TimestampWithTimeZoneColumnType);
            entity.HasIndex(e => e.UploadedBy).HasDatabaseName("idx_files_uploaded_by");
            entity.HasIndex(e => e.Status).HasDatabaseName("idx_files_status");
            entity.HasIndex(e => e.StoragePath).IsUnique().HasDatabaseName("uq_files_storage_path");
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.UploadedBy).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureFileLinks(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FileLink>(entity =>
        {
            entity.ToTable("file_links");
            entity.HasKey(e => e.FileLinkId);
            entity.Property(e => e.FileLinkId).HasColumnName("file_link_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.FileId).HasColumnName("file_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.ReferenceType).HasColumnName("reference_type").HasColumnType(Varchar50ColumnType).IsRequired();
            entity.Property(e => e.ReferenceId).HasColumnName("reference_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.FileType).HasColumnName("file_type").HasColumnType(FileTypeColumnType).HasDefaultValueSql("'OTHER'::file_type");
            entity.Property(e => e.Visibility).HasColumnName("visibility").HasColumnType(FileVisibilityColumnType).HasDefaultValueSql("'CUSTOMER_VISIBLE'::file_visibility");
            entity.Property(e => e.IsPrimary).HasColumnName("is_primary").HasColumnType(BooleanColumnType).HasDefaultValue(false);
            entity.Property(e => e.DisplayOrder).HasColumnName("display_order").HasColumnType(IntegerColumnType).HasDefaultValue(0);
            entity.Property(e => e.Description).HasColumnName(DescriptionColumnName).HasColumnType(TextColumnType);
            entity.Property(e => e.CreatedBy).HasColumnName("created_by").HasColumnType(UuidColumnType);
            entity.Property(e => e.CreatedAt).HasColumnName(CreatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.HasIndex(e => new { e.ReferenceType, e.ReferenceId }).HasDatabaseName("idx_file_links_reference");
            entity.HasIndex(e => e.FileId).HasDatabaseName("idx_file_links_file_id");
            entity.HasIndex(e => new { e.FileId, e.ReferenceType, e.ReferenceId, e.FileType }).IsUnique().HasDatabaseName("uq_file_links_unique_reference");
            entity.HasIndex(e => new { e.ReferenceType, e.ReferenceId, e.FileType, e.DisplayOrder })
                .HasDatabaseName("idx_file_links_reference_type_order");
            entity.HasOne<StoredFile>().WithMany().HasForeignKey(e => e.FileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureProjects(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Project>(entity =>
        {
            entity.ToTable("projects");
            entity.HasKey(e => e.ProjectId);
            entity.Property(e => e.ProjectId).HasColumnName(ProjectIdColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.CustomerId).HasColumnName(CustomerIdColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.AssignedSalesId).HasColumnName("assigned_sales_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.AssignedDesignerId).HasColumnName("assigned_designer_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.ProjectCode).HasColumnName("project_code").HasColumnType(Varchar50ColumnType);
            entity.Property(e => e.ProjectName).HasColumnName("project_name").HasColumnType(Varchar150ColumnType).IsRequired();
            entity.Property(e => e.BusinessType).HasColumnName("business_type").HasColumnType(Varchar100ColumnType);
            entity.Property(e => e.ProjectAddress).HasColumnName("project_address").HasColumnType(TextColumnType);
            entity.Property(e => e.BusinessPurpose).HasColumnName("business_purpose").HasColumnType(TextColumnType);
            entity.Property(e => e.FurnitureRequirement).HasColumnName("furniture_requirement").HasColumnType(TextColumnType);
            entity.Property(e => e.Description).HasColumnName(DescriptionColumnName).HasColumnType(TextColumnType);
            entity.Property(e => e.TotalAreaSqm).HasColumnName("total_area_sqm").HasColumnType(Decimal10ColumnType);
            entity.Property(e => e.NumberOfFloors).HasColumnName("number_of_floors").HasColumnType(IntegerColumnType);
            entity.Property(e => e.BudgetMin).HasColumnName("budget_min").HasColumnType(Decimal12ColumnType);
            entity.Property(e => e.BudgetMax).HasColumnName("budget_max").HasColumnType(Decimal12ColumnType);
            entity.Property(e => e.TargetCompletionDate).HasColumnName("target_completion_date").HasColumnType(DateColumnType);
            entity.Property(e => e.Status).HasColumnName(StatusColumnName).HasColumnType(ProjectStatusColumnType).HasDefaultValueSql("'SUBMITTED'::project_status");
            entity.Property(e => e.SubmittedAt).HasColumnName("submitted_at").HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.SalesAssignedAt).HasColumnName("sales_assigned_at").HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.ApprovedAt).HasColumnName("approved_at").HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.DesignerAssignedAt).HasColumnName("designer_assigned_at").HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.CompletedAt).HasColumnName(CompletedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.RejectedAt).HasColumnName(RejectedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.RejectionReason).HasColumnName("rejection_reason").HasColumnType(TextColumnType);
            entity.Property(e => e.CreatedAt).HasColumnName(CreatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.UpdatedAt).HasColumnName(UpdatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.HasIndex(e => e.ProjectCode).IsUnique();
            entity.HasIndex(e => new { e.Status, e.SubmittedAt, e.ProjectId })
                .HasDatabaseName("idx_projects_status_list_sort")
                .IsDescending(false, true, true);
            entity.HasIndex(e => new { e.CustomerId, e.SubmittedAt, e.ProjectId })
                .HasDatabaseName("idx_projects_customer_list_sort")
                .IsDescending(false, true, true);
            entity.HasIndex(e => new { e.AssignedSalesId, e.SubmittedAt, e.ProjectId })
                .HasDatabaseName("idx_projects_sales_list_sort")
                .IsDescending(false, true, true);
            entity.HasIndex(e => new { e.AssignedDesignerId, e.SubmittedAt, e.ProjectId })
                .HasDatabaseName("idx_projects_designer_list_sort")
                .IsDescending(false, true, true);
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.CustomerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.AssignedSalesId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.AssignedDesignerId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureNotifications(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("notifications");
            entity.HasKey(e => e.NotificationId);
            entity.Property(e => e.NotificationId).HasColumnName("notification_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.ReceiverId).HasColumnName("receiver_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.ProjectId).HasColumnName(ProjectIdColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.Title).HasColumnName(TitleColumnName).HasColumnType(Varchar150ColumnType).IsRequired();
            entity.Property(e => e.Message).HasColumnName("message").HasColumnType(TextColumnType);
            entity.Property(e => e.NotificationType).HasColumnName("notification_type").HasColumnType(Varchar50ColumnType);
            entity.Property(e => e.ReferenceType).HasColumnName("reference_type").HasColumnType(Varchar50ColumnType);
            entity.Property(e => e.ReferenceId).HasColumnName("reference_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.IsRead).HasColumnName("is_read").HasColumnType(BooleanColumnType).HasDefaultValue(false).IsRequired();
            entity.Property(e => e.ReadAt).HasColumnName("read_at").HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at").HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.CreatedAt).HasColumnName(CreatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType).IsRequired();
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.ReceiverId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Project>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.ReceiverId, e.IsRead }).HasDatabaseName("idx_notifications_receiver_read");
            entity.HasIndex(e => new { e.ReceiverId, e.CreatedAt }).HasDatabaseName("idx_notifications_receiver_created");
            entity.HasIndex(e => new { e.ReferenceType, e.ReferenceId }).HasDatabaseName("idx_notifications_reference");
            entity.HasIndex(e => e.ProjectId).HasDatabaseName("idx_notifications_project_id");
        });
    }

    private static void ConfigureProjectChats(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProjectChat>(entity =>
        {
            entity.ToTable("project_chats");
            entity.HasKey(e => e.ChatId);
            entity.Property(e => e.ChatId).HasColumnName("chat_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.ProjectId).HasColumnName(ProjectIdColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.ChatType).HasColumnName("chat_type").HasColumnType(ProjectChatTypeColumnType).IsRequired();
            entity.Property(e => e.StaffId).HasColumnName("staff_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.Title).HasColumnName(TitleColumnName).HasColumnType(Varchar150ColumnType);
            entity.Property(e => e.Status).HasColumnName(StatusColumnName).HasColumnType(ProjectChatStatusColumnType).HasDefaultValueSql("'OPEN'::project_chat_status");
            entity.Property(e => e.CreatedAt).HasColumnName(CreatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.ClosedAt).HasColumnName("closed_at").HasColumnType(TimestampWithTimeZoneColumnType);
            entity.HasIndex(e => new { e.ProjectId, e.CreatedAt, e.ChatId })
                .HasDatabaseName("idx_project_chats_project_list_sort")
                .IsDescending(false, true, true);
            entity.HasOne<Project>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.StaffId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureProjectChatMessages(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProjectChatMessage>(entity =>
        {
            entity.ToTable("project_chat_messages");
            entity.HasKey(e => e.MessageId);
            entity.Property(e => e.MessageId).HasColumnName("message_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.ChatId).HasColumnName("chat_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.SenderId).HasColumnName("sender_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.MessageType).HasColumnName("message_type").HasColumnType(ProjectChatMessageTypeColumnType).HasDefaultValueSql("'TEXT'::project_chat_message_type");
            entity.Property(e => e.Content).HasColumnName("content").HasColumnType(TextColumnType);
            entity.Property(e => e.AttachmentFileId).HasColumnName("attachment_file_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.CreatedAt).HasColumnName(CreatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.EditedAt).HasColumnName("edited_at").HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at").HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.ReadAt).HasColumnName("read_at").HasColumnType(TimestampWithTimeZoneColumnType);
            entity.HasIndex(e => new { e.ChatId, e.CreatedAt, e.MessageId })
                .HasDatabaseName("idx_chat_messages_chat_list_sort")
                .IsDescending(false, true, true);
            entity.HasOne<ProjectChat>().WithMany().HasForeignKey(e => e.ChatId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.SenderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<StoredFile>().WithMany().HasForeignKey(e => e.AttachmentFileId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureProjectAreas(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProjectArea>(entity =>
        {
            entity.ToTable("project_areas");
            entity.HasKey(e => e.ProjectAreaId);
            entity.Property(e => e.ProjectAreaId).HasColumnName(ProjectAreaIdColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.ProjectId).HasColumnName(ProjectIdColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.ParentAreaId).HasColumnName("parent_area_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.AreaName).HasColumnName("area_name").HasColumnType(Varchar150ColumnType).IsRequired();
            entity.Property(e => e.AreaType).HasColumnName("area_type").HasColumnType(ProjectAreaTypeColumnType).HasDefaultValueSql("'ZONE'::project_area_type");
            entity.Property(e => e.FloorNumber).HasColumnName("floor_number").HasColumnType(IntegerColumnType);
            entity.Property(e => e.IsSpecialLayout).HasColumnName("is_special_layout").HasColumnType(BooleanColumnType).HasDefaultValue(false).IsRequired();
            entity.Property(e => e.Description).HasColumnName(DescriptionColumnName).HasColumnType(TextColumnType);
            entity.Property(e => e.AreaSqm).HasColumnName("area_sqm").HasColumnType(Decimal10ColumnType);
            entity.Property(e => e.Width).HasColumnName(WidthColumnName).HasColumnType(Decimal10ColumnType);
            entity.Property(e => e.Length).HasColumnName("length").HasColumnType(Decimal10ColumnType);
            entity.Property(e => e.Height).HasColumnName(HeightColumnName).HasColumnType(Decimal10ColumnType);
            entity.Property(e => e.CurrentCondition).HasColumnName("current_condition").HasColumnType(TextColumnType);
            entity.Property(e => e.RequirementNote).HasColumnName("requirement_note").HasColumnType(TextColumnType);
            entity.Property(e => e.Status).HasColumnName(StatusColumnName).HasColumnType(ProjectAreaStatusColumnType).HasDefaultValueSql("'DRAFT'::project_area_status");
            entity.Property(e => e.CreatedBy).HasColumnName(CreatedByColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.CreatedAt).HasColumnName(CreatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.UpdatedAt).HasColumnName(UpdatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.HasIndex(e => e.ProjectId);
            entity.HasIndex(e => new { e.ProjectId, e.FloorNumber })
                .IsUnique()
                .HasDatabaseName("uq_project_active_floor_number")
                .HasFilter("area_type = 'FLOOR' AND status <> 'CANCELLED'");
            entity.HasOne<Project>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProjectArea>().WithMany().HasForeignKey(e => e.ParentAreaId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureProjectPhaseTimelines(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProjectPhaseTimeline>(entity =>
        {
            entity.ToTable("project_phase_timelines");
            entity.HasKey(e => e.ProjectPhaseTimelineId);
            entity.Property(e => e.ProjectPhaseTimelineId).HasColumnName("project_phase_timeline_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.ProjectId).HasColumnName(ProjectIdColumnName).HasColumnType(UuidColumnType).IsRequired();
            entity.Property(e => e.Phase).HasColumnName("phase").HasColumnType(ProjectPhaseTypeColumnType).IsRequired();
            entity.Property(e => e.DueDate).HasColumnName("due_date").HasColumnType(DateColumnType).IsRequired();
            entity.Property(e => e.StartedAt).HasColumnName("started_at").HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.CompletedAt).HasColumnName(CompletedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.CreatedBy).HasColumnName(CreatedByColumnName).HasColumnType(UuidColumnType).IsRequired();
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by").HasColumnType(UuidColumnType);
            entity.Property(e => e.CreatedAt).HasColumnName(CreatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType).IsRequired();
            entity.Property(e => e.UpdatedAt).HasColumnName(UpdatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType).IsRequired();
            entity.HasIndex(e => e.ProjectId);
            entity.HasIndex(e => e.Phase);
            entity.HasIndex(e => e.DueDate);
            entity.HasIndex(e => new { e.ProjectId, e.Phase }).IsUnique();
            entity.HasOne<Project>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.CreatedBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.UpdatedBy).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureProjectSchedules(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProjectSchedule>(entity =>
        {
            entity.ToTable("project_schedules");
            entity.HasKey(e => e.ScheduleId);
            entity.Property(e => e.ScheduleId).HasColumnName("schedule_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.ProjectId).HasColumnName(ProjectIdColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.ProjectAreaId).HasColumnName(ProjectAreaIdColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.ScheduleType).HasColumnName("schedule_type").HasColumnType(ProjectScheduleTypeColumnType).HasDefaultValueSql("'MEASUREMENT'::project_schedule_type");
            entity.Property(e => e.Title).HasColumnName(TitleColumnName).HasColumnType(Varchar150ColumnType);
            entity.Property(e => e.Description).HasColumnName(DescriptionColumnName).HasColumnType(TextColumnType);
            entity.Property(e => e.CreatedBy).HasColumnName(CreatedByColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.AssignedStaffId).HasColumnName("assigned_staff_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.ScheduledStart).HasColumnName("scheduled_start").HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.ScheduledEnd).HasColumnName("scheduled_end").HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.Location).HasColumnName("location").HasColumnType(TextColumnType);
            entity.Property(e => e.Status).HasColumnName(StatusColumnName).HasColumnType(ProjectScheduleStatusColumnType).HasDefaultValueSql("'PENDING_CONFIRMATION'::project_schedule_status");
            entity.Property(e => e.CustomerNote).HasColumnName("customer_note").HasColumnType(TextColumnType);
            entity.Property(e => e.InternalNote).HasColumnName("internal_note").HasColumnType(TextColumnType);
            entity.Property(e => e.CreatedAt).HasColumnName(CreatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.UpdatedAt).HasColumnName(UpdatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.CancelledAt).HasColumnName(CancelledAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at").HasColumnType(TimestampWithTimeZoneColumnType);
            entity.HasIndex(e => new { e.ProjectId, e.ScheduledStart })
                .HasDatabaseName("idx_project_schedules_project_sort")
                .IsDescending(false, true);
            entity.HasIndex(e => new { e.AssignedStaffId, e.ScheduledStart })
                .HasDatabaseName("idx_project_schedules_staff_sort")
                .IsDescending(false, true);
            entity.HasOne<Project>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProjectArea>().WithMany().HasForeignKey(e => e.ProjectAreaId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.CreatedBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.AssignedStaffId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureProposals(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Proposal>(entity =>
        {
            entity.ToTable("proposals");
            entity.HasKey(e => e.ProposalId);
            entity.Property(e => e.ProposalId).HasColumnName(ProposalIdColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.ProjectId).HasColumnName(ProjectIdColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.ParentProposalId).HasColumnName("parent_proposal_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.ProposalName).HasColumnName("proposal_name").HasColumnType(Varchar150ColumnType).IsRequired();
            entity.Property(e => e.Description).HasColumnName(DescriptionColumnName).HasColumnType(TextColumnType);
            entity.Property(e => e.DesignConcept).HasColumnName("design_concept").HasColumnType(TextColumnType);
            entity.Property(e => e.VersionNo).HasColumnName(VersionNoColumnName).HasColumnType(IntegerColumnType).HasDefaultValue(1);
            entity.Property(e => e.EstimatedPrice).HasColumnName("estimated_price").HasColumnType(Decimal12ColumnType);
            entity.Property(e => e.Status).HasColumnName(StatusColumnName).HasColumnType(ProposalStatusColumnType).HasDefaultValueSql("'DRAFT'::proposal_status");
            entity.Property(e => e.CreatedBy).HasColumnName(CreatedByColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.PublishedAt).HasColumnName("published_at").HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.SelectedAt).HasColumnName("selected_at").HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.RejectedAt).HasColumnName(RejectedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.RevisionNote).HasColumnName("revision_note").HasColumnType(TextColumnType);
            entity.Property(e => e.CreatedAt).HasColumnName(CreatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.UpdatedAt).HasColumnName(UpdatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.HasIndex(e => new { e.ProjectId, e.VersionNo, e.CreatedAt, e.ProposalId })
                .HasDatabaseName("idx_proposals_project_list_sort")
                .IsDescending(false, true, true, true);
            entity.HasOne<Project>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Proposal>().WithMany().HasForeignKey(e => e.ParentProposalId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureProposalScenes(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProposalScene>(entity =>
        {
            entity.ToTable("proposal_scenes");
            entity.HasKey(e => e.SceneId);
            entity.Property(e => e.SceneId).HasColumnName(SceneIdColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.ProposalId).HasColumnName(ProposalIdColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.SceneName).HasColumnName("scene_name").HasColumnType(Varchar150ColumnType);
            entity.Property(e => e.SceneType).HasColumnName("scene_type").HasColumnType(ProposalSceneTypeColumnType).HasDefaultValueSql("'THREE_D'::proposal_scene_type");
            entity.Property(e => e.MongoSceneId).HasColumnName("mongo_scene_id").HasColumnType(Varchar100ColumnType);
            entity.Property(e => e.PreviewFileId).HasColumnName("preview_file_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.VersionNo).HasColumnName(VersionNoColumnName).HasColumnType(IntegerColumnType).HasDefaultValue(1);
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasColumnType(BooleanColumnType).HasDefaultValue(true);
            entity.Property(e => e.CreatedBy).HasColumnName(CreatedByColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.CreatedAt).HasColumnName(CreatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.UpdatedAt).HasColumnName(UpdatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.HasIndex(e => new { e.ProposalId, e.VersionNo, e.CreatedAt, e.SceneId })
                .HasDatabaseName("idx_proposal_scenes_proposal_list_sort");
            entity.HasOne<Proposal>().WithMany().HasForeignKey(e => e.ProposalId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<StoredFile>().WithMany().HasForeignKey(e => e.PreviewFileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureProposalSceneAreas(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProposalSceneArea>(entity =>
        {
            entity.ToTable("proposal_scene_areas");
            entity.HasKey(e => e.ProposalSceneAreaId);
            entity.Property(e => e.ProposalSceneAreaId).HasColumnName("proposal_scene_area_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.SceneId).HasColumnName(SceneIdColumnName).HasColumnType(UuidColumnType).IsRequired();
            entity.Property(e => e.ProjectAreaId).HasColumnName(ProjectAreaIdColumnName).HasColumnType(UuidColumnType).IsRequired();
            entity.Property(e => e.SortOrder).HasColumnName("sort_order").HasColumnType(IntegerColumnType).HasDefaultValue(0);
            entity.Property(e => e.CreatedAt).HasColumnName(CreatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType).IsRequired();
            entity.Property(e => e.UpdatedAt).HasColumnName(UpdatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.HasIndex(e => e.SceneId).HasDatabaseName("idx_proposal_scene_areas_scene_id");
            entity.HasIndex(e => e.ProjectAreaId).HasDatabaseName("idx_proposal_scene_areas_project_area_id");
            entity.HasIndex(e => new { e.SceneId, e.ProjectAreaId })
                .IsUnique()
                .HasDatabaseName("uq_proposal_scene_areas_scene_project_area");
            entity.HasOne(e => e.Scene).WithMany(e => e.SceneAreas).HasForeignKey(e => e.SceneId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.ProjectArea).WithMany(e => e.ProposalSceneAreas).HasForeignKey(e => e.ProjectAreaId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureProposalItems(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProposalItem>(entity =>
        {
            entity.ToTable("proposal_items");
            entity.HasKey(e => e.ProposalItemId);
            entity.Property(e => e.ProposalItemId).HasColumnName(ProposalItemIdColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.ProposalId).HasColumnName(ProposalIdColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.SceneId).HasColumnName(SceneIdColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.SceneObjectId).HasColumnName("scene_object_id").HasColumnType("character varying(100)");
            entity.Property(e => e.ProjectAreaId).HasColumnName(ProjectAreaIdColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.ProductVersionId).HasColumnName(ProductVersionIdColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.ItemName).HasColumnName("item_name").HasColumnType(Varchar150ColumnType).IsRequired();
            entity.Property(e => e.ItemType).HasColumnName("item_type").HasColumnType(Varchar50ColumnType);
            entity.Property(e => e.Quantity).HasColumnName(QuantityColumnName).HasColumnType(IntegerColumnType).HasDefaultValue(1);
            entity.Property(e => e.Width).HasColumnName(WidthColumnName).HasColumnType(Decimal10ColumnType);
            entity.Property(e => e.Height).HasColumnName(HeightColumnName).HasColumnType(Decimal10ColumnType);
            entity.Property(e => e.Depth).HasColumnName("depth").HasColumnType(Decimal10ColumnType);
            entity.Property(e => e.Material).HasColumnName("material").HasColumnType(Varchar100ColumnType);
            entity.Property(e => e.Color).HasColumnName("color").HasColumnType(Varchar100ColumnType);
            entity.Property(e => e.IsCustomized).HasColumnName("is_customized").HasColumnType(BooleanColumnType).HasDefaultValue(false);
            entity.Property(e => e.UnitPriceSnapshot).HasColumnName("unit_price_snapshot").HasColumnType(Decimal12ColumnType);
            entity.Property(e => e.TotalPriceSnapshot).HasColumnName("total_price_snapshot").HasColumnType(Decimal12ColumnType);
            entity.Property(e => e.Note).HasColumnName(NoteColumnName).HasColumnType(TextColumnType);
            entity.Property(e => e.CreatedAt).HasColumnName(CreatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.UpdatedAt).HasColumnName(UpdatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.HasOne<Proposal>().WithMany().HasForeignKey(e => e.ProposalId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProposalScene>().WithMany().HasForeignKey(e => e.SceneId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProjectArea>().WithMany().HasForeignKey(e => e.ProjectAreaId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProductVersion>().WithMany().HasForeignKey(e => e.ProductVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.SceneId, e.SceneObjectId })
                .IsUnique()
                .HasFilter("scene_id IS NOT NULL AND scene_object_id IS NOT NULL")
                .HasDatabaseName("uq_proposal_items_scene_object");
            entity.HasIndex(e => new { e.ProposalId, e.ItemName })
                .HasDatabaseName("idx_proposal_items_proposal_list_sort");
        });
    }

    private static void ConfigureProposalSceneVariants(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProposalSceneVariant>(entity =>
        {
            entity.ToTable("proposal_scene_variants");
            entity.HasKey(e => e.VariantId);
            entity.Property(e => e.VariantId).HasColumnName("variant_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.ProposalId).HasColumnName(ProposalIdColumnName).HasColumnType(UuidColumnType).IsRequired();
            entity.Property(e => e.SceneId).HasColumnName(SceneIdColumnName).HasColumnType(UuidColumnType).IsRequired();
            entity.Property(e => e.CreatedBy).HasColumnName(CreatedByColumnName).HasColumnType(UuidColumnType).IsRequired();
            entity.Property(e => e.VariantType).HasColumnName("variant_type").HasColumnType(ProposalSceneVariantTypeColumnType).HasDefaultValueSql("'CUSTOMER_SUGGESTION'::proposal_scene_variant_type");
            entity.Property(e => e.Status).HasColumnName(StatusColumnName).HasColumnType(ProposalSceneVariantStatusColumnType).HasDefaultValueSql("'DRAFT'::proposal_scene_variant_status");
            entity.Property(e => e.MongoVariantSceneId).HasColumnName("mongo_variant_scene_id").HasColumnType(Varchar100ColumnType).IsRequired();
            entity.Property(e => e.Note).HasColumnName(NoteColumnName).HasColumnType(TextColumnType);
            entity.Property(e => e.SubmittedAt).HasColumnName("submitted_at").HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.ReviewedBy).HasColumnName("reviewed_by").HasColumnType(UuidColumnType);
            entity.Property(e => e.ReviewedAt).HasColumnName("reviewed_at").HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.ReviewNote).HasColumnName("review_note").HasColumnType(TextColumnType);
            entity.Property(e => e.AppliedAt).HasColumnName("applied_at").HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.AppliedBy).HasColumnName("applied_by").HasColumnType(UuidColumnType);
            entity.Property(e => e.CreatedAt).HasColumnName(CreatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.UpdatedAt).HasColumnName(UpdatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.HasIndex(e => e.ProposalId).HasDatabaseName("idx_scene_variants_proposal");
            entity.HasIndex(e => e.SceneId).HasDatabaseName("idx_scene_variants_scene");
            entity.HasIndex(e => e.CreatedBy).HasDatabaseName("idx_scene_variants_created_by");
            entity.HasIndex(e => e.Status).HasDatabaseName("idx_scene_variants_status");
            entity.HasIndex(e => new { e.SceneId, e.Status }).HasDatabaseName("idx_scene_variants_scene_status");
            entity.HasOne<Proposal>().WithMany().HasForeignKey(e => e.ProposalId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProposalScene>().WithMany().HasForeignKey(e => e.SceneId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.CreatedBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.ReviewedBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.AppliedBy).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureCustomizationRequests(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CustomizationRequest>(entity =>
        {
            entity.ToTable("customization_requests");
            entity.HasKey(e => e.CustomizationRequestId);
            entity.Property(e => e.CustomizationRequestId).HasColumnName(CustomizationRequestIdColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.ProjectId).HasColumnName(ProjectIdColumnName).HasColumnType(UuidColumnType).IsRequired();
            entity.Property(e => e.ProposalId).HasColumnName(ProposalIdColumnName).HasColumnType(UuidColumnType).IsRequired();
            entity.Property(e => e.SourceProductVersionId).HasColumnName(SourceProductVersionIdColumnName).HasColumnType(UuidColumnType).IsRequired();
            entity.Property(e => e.RequestedByCustomerId).HasColumnName("requested_by_customer_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.RequestTitle).HasColumnName("request_title").HasColumnType(Varchar150ColumnType).IsRequired();
            entity.Property(e => e.RequestDescription).HasColumnName("request_description").HasColumnType(TextColumnType);
            entity.Property(e => e.RequestedWidth).HasColumnName("requested_width").HasColumnType(Decimal10ColumnType);
            entity.Property(e => e.RequestedHeight).HasColumnName("requested_height").HasColumnType(Decimal10ColumnType);
            entity.Property(e => e.RequestedDepth).HasColumnName("requested_depth").HasColumnType(Decimal10ColumnType);
            entity.Property(e => e.RequestedMaterial).HasColumnName("requested_material").HasColumnType(Varchar100ColumnType);
            entity.Property(e => e.RequestedColor).HasColumnName("requested_color").HasColumnType(Varchar100ColumnType);
            entity.Property(e => e.RequestedChangeNote).HasColumnName("requested_change_note").HasColumnType(TextColumnType);
            entity.Property(e => e.AcceptedRequestVersionId).HasColumnName(AcceptedRequestVersionIdColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.Status).HasColumnName(StatusColumnName).HasColumnType(CustomizationStatusColumnType).HasDefaultValueSql("'SUBMITTED'::customization_status");
            entity.Property(e => e.CreatedAt).HasColumnName(CreatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.UpdatedAt).HasColumnName(UpdatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.HasOne<Project>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Proposal>().WithMany().HasForeignKey(e => e.ProposalId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProductVersion>().WithMany().HasForeignKey(e => e.SourceProductVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.RequestedByCustomerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<CustomizationRequestVersion>().WithMany().HasForeignKey(e => e.AcceptedRequestVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.ProjectId);
            entity.HasIndex(e => e.ProposalId);
            entity.HasIndex(e => e.SourceProductVersionId);
            entity.HasIndex(e => e.RequestedByCustomerId);
            entity.HasIndex(e => e.Status);
        });
    }

    private static void ConfigureCustomizationRequestVersions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CustomizationRequestVersion>(entity =>
        {
            entity.ToTable("customization_request_versions");
            entity.HasKey(e => e.CustomizationRequestVersionId);
            entity.Property(e => e.CustomizationRequestVersionId).HasColumnName(CustomizationRequestVersionIdColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.CustomizationRequestId).HasColumnName(CustomizationRequestIdColumnName).HasColumnType(UuidColumnType).IsRequired();
            entity.Property(e => e.ProductVersionId).HasColumnName(ProductVersionIdColumnName).HasColumnType(UuidColumnType).IsRequired();
            entity.Property(e => e.VersionNo).HasColumnName(VersionNoColumnName).HasColumnType(IntegerColumnType).IsRequired();
            entity.Property(e => e.CreatedByDesignerId).HasColumnName("created_by_designer_id").HasColumnType(UuidColumnType).IsRequired();
            entity.Property(e => e.VersionTitle).HasColumnName("version_title").HasColumnType(Varchar150ColumnType);
            entity.Property(e => e.DesignerNote).HasColumnName("designer_note").HasColumnType(TextColumnType);
            entity.Property(e => e.Status).HasColumnName(StatusColumnName).HasColumnType(CustomizationVersionStatusColumnType).HasDefaultValueSql("'DRAFT'::customization_version_status");
            entity.Property(e => e.ProductionReviewedBy).HasColumnName("production_reviewed_by").HasColumnType(UuidColumnType);
            entity.Property(e => e.FeasibilityStatus).HasColumnName("feasibility_status").HasColumnType(ProductionFeasibilityStatusColumnType).HasDefaultValueSql("'PENDING'::production_feasibility_status");
            entity.Property(e => e.FeasibilityNote).HasColumnName("feasibility_note").HasColumnType(TextColumnType);
            entity.Property(e => e.EstimatedProductionDays).HasColumnName("estimated_production_days").HasColumnType(IntegerColumnType);
            entity.Property(e => e.EstimatedAdditionalCost).HasColumnName("estimated_additional_cost").HasColumnType(Decimal12ColumnType);
            entity.Property(e => e.AdditionalCostReason).HasColumnName("additional_cost_reason").HasColumnType(TextColumnType);
            entity.Property(e => e.MaterialAvailable).HasColumnName("material_available").HasColumnType(BooleanColumnType);
            entity.Property(e => e.ProductionRiskNote).HasColumnName("production_risk_note").HasColumnType(TextColumnType);
            entity.Property(e => e.AlternativeMaterialNote).HasColumnName("alternative_material_note").HasColumnType(TextColumnType);
            entity.Property(e => e.SubmittedForReviewAt).HasColumnName("submitted_for_review_at").HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.ProductionReviewedAt).HasColumnName("production_reviewed_at").HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.ProductionRejectedAt).HasColumnName("production_rejected_at").HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.AcceptedAt).HasColumnName("accepted_at").HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.WithdrawnAt).HasColumnName("withdrawn_at").HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.CreatedAt).HasColumnName(CreatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType).IsRequired();
            entity.Property(e => e.UpdatedAt).HasColumnName(UpdatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType).IsRequired();
            entity.HasOne(e => e.CustomizationRequest).WithMany(r => r.Versions).HasForeignKey(e => e.CustomizationRequestId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ProductVersion).WithMany().HasForeignKey(e => e.ProductVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.CreatedByDesignerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.ProductionReviewedBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.CustomizationRequestId, e.VersionNo }).IsUnique();
            entity.HasIndex(e => new { e.CustomizationRequestId, e.ProductVersionId }).IsUnique();
            entity.HasIndex(e => e.CustomizationRequestId);
            entity.HasIndex(e => e.ProductVersionId);
            entity.HasIndex(e => e.CreatedByDesignerId);
            entity.HasIndex(e => e.ProductionReviewedBy);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.FeasibilityStatus);
        });
    }

    private static void ConfigureQuotations(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Quotation>(entity =>
        {
            entity.ToTable("quotations");
            entity.HasKey(e => e.QuotationId);
            entity.Property(e => e.QuotationId).HasColumnName(QuotationIdColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.ProjectId).HasColumnName(ProjectIdColumnName).HasColumnType(UuidColumnType).IsRequired();
            entity.Property(e => e.ProposalId).HasColumnName(ProposalIdColumnName).HasColumnType(UuidColumnType).IsRequired();
            entity.Property(e => e.QuotationCode).HasColumnName("quotation_code").HasColumnType(Varchar50ColumnType).IsRequired();
            entity.Property(e => e.VersionNo).HasColumnName(VersionNoColumnName).HasColumnType(IntegerColumnType).HasDefaultValue(1);
            entity.Property(e => e.SubtotalAmount).HasColumnName(SubtotalAmountColumnName).HasColumnType(Decimal14ColumnType).HasDefaultValue(0m).IsRequired();
            entity.Property(e => e.TotalDiscountAmount).HasColumnName(TotalDiscountAmountColumnName).HasColumnType(Decimal14ColumnType).HasDefaultValue(0m).IsRequired();
            entity.Property(e => e.PreVatAmount).HasColumnName(PreVatAmountColumnName).HasColumnType(Decimal14ColumnType).HasDefaultValue(0m).IsRequired();
            entity.Property(e => e.VatRate).HasColumnName(VatRateColumnName).HasColumnType(TaxRateColumnType).HasDefaultValue(0.08m).IsRequired();
            entity.Property(e => e.VatAmount).HasColumnName(VatAmountColumnName).HasColumnType(Decimal14ColumnType).HasDefaultValue(0m).IsRequired();
            entity.Property(e => e.TotalAmount).HasColumnName(TotalAmountColumnName).HasColumnType(Decimal14ColumnType).HasDefaultValue(0m).IsRequired();
            entity.Property(e => e.DepositAmount).HasColumnName("deposit_amount").HasColumnType(Decimal14ColumnType).HasDefaultValue(0m).IsRequired();
            entity.Property(e => e.Currency).HasColumnName("currency").HasColumnType(Varchar10ColumnType).HasDefaultValue("VND").IsRequired();
            entity.Property(e => e.Status).HasColumnName(StatusColumnName).HasColumnType(QuotationStatusColumnType).HasDefaultValueSql("'DRAFT'::quotation_status");
            entity.Property(e => e.ValidUntil).HasColumnName("valid_until").HasColumnType(DateColumnType);
            entity.Property(e => e.CustomerNote).HasColumnName("customer_note").HasColumnType(TextColumnType);
            entity.Property(e => e.SalesNote).HasColumnName("sales_note").HasColumnType(TextColumnType);
            entity.Property(e => e.RevisionReason).HasColumnName("revision_reason").HasColumnType(TextColumnType);
            entity.Property(e => e.RejectReason).HasColumnName("reject_reason").HasColumnType(TextColumnType);
            entity.Property(e => e.CreatedBy).HasColumnName(CreatedByColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.SentAt).HasColumnName("sent_at").HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.AcceptedAt).HasColumnName("accepted_at").HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.RejectedAt).HasColumnName(RejectedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.CreatedAt).HasColumnName(CreatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.UpdatedAt).HasColumnName(UpdatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.HasIndex(e => e.QuotationCode).IsUnique();
            entity.HasOne<Project>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Proposal>().WithMany().HasForeignKey(e => e.ProposalId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureQuotationItems(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<QuotationItem>(entity =>
        {
            entity.ToTable("quotation_items");
            entity.HasKey(e => e.QuotationItemId);
            entity.Property(e => e.QuotationItemId).HasColumnName("quotation_item_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.QuotationId).HasColumnName(QuotationIdColumnName).HasColumnType(UuidColumnType).IsRequired();
            entity.Property(e => e.ProposalItemId).HasColumnName(ProposalItemIdColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.ProductVersionId).HasColumnName(ProductVersionIdColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.ProductNameSnapshot).HasColumnName(ProductNameSnapshotColumnName).HasColumnType(Varchar150ColumnType);
            entity.Property(e => e.ProductVersionNameSnapshot).HasColumnName(ProductVersionNameSnapshotColumnName).HasColumnType(Varchar150ColumnType);
            entity.Property(e => e.ProductVersionCodeSnapshot).HasColumnName("product_version_code_snapshot").HasColumnType(Varchar50ColumnType);
            entity.Property(e => e.ItemName).HasColumnName("item_name").HasColumnType(Varchar150ColumnType);
            entity.Property(e => e.Description).HasColumnName(DescriptionColumnName).HasColumnType(TextColumnType);
            entity.Property(e => e.DisplayOrder).HasColumnName("display_order").HasColumnType(IntegerColumnType).HasDefaultValue(0);
            entity.Property(e => e.Quantity).HasColumnName(QuantityColumnName).HasColumnType(IntegerColumnType).HasDefaultValue(1).IsRequired();
            entity.Property(e => e.UnitPrice).HasColumnName("unit_price").HasColumnType(Decimal14ColumnType).HasDefaultValue(0m).IsRequired();
            entity.Property(e => e.GrossAmount).HasColumnName(GrossAmountColumnName).HasColumnType(Decimal14ColumnType).HasDefaultValue(0m).IsRequired();
            entity.Property(e => e.DiscountAmount).HasColumnName(DiscountAmountColumnName).HasColumnType(Decimal14ColumnType).HasDefaultValue(0m).IsRequired();
            entity.Property(e => e.TotalAmount).HasColumnName(TotalAmountColumnName).HasColumnType(Decimal14ColumnType).HasDefaultValue(0m).IsRequired();
            entity.Property(e => e.IsCustomized).HasColumnName("is_customized").HasColumnType(BooleanColumnType).HasDefaultValue(false);
            entity.Property(e => e.CustomizationNote).HasColumnName("customization_note").HasColumnType(TextColumnType);
            entity.Property(e => e.Note).HasColumnName(NoteColumnName).HasColumnType(TextColumnType);
            entity.Property(e => e.CreatedAt).HasColumnName(CreatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.UpdatedAt).HasColumnName(UpdatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.HasOne<Quotation>().WithMany().HasForeignKey(e => e.QuotationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProposalItem>().WithMany().HasForeignKey(e => e.ProposalItemId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProductVersion>().WithMany().HasForeignKey(e => e.ProductVersionId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureOrders(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("orders");
            entity.HasKey(e => e.OrderId);
            entity.Property(e => e.OrderId).HasColumnName(OrderIdColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.ProjectId).HasColumnName(ProjectIdColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.ProposalId).HasColumnName(ProposalIdColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.QuotationId).HasColumnName(QuotationIdColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.OrderCode).HasColumnName("order_code").HasColumnType(Varchar50ColumnType).IsRequired();
            entity.Property(e => e.CustomerId).HasColumnName(CustomerIdColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.SalesId).HasColumnName("sales_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.VatRate).HasColumnName(VatRateColumnName).HasColumnType(TaxRateColumnType).HasDefaultValue(0.08m).IsRequired();
            entity.Property(e => e.VatAmount).HasColumnName(VatAmountColumnName).HasColumnType(Decimal14ColumnType).HasDefaultValue(0m).IsRequired();
            entity.Property(e => e.FinalTotalAmount).HasColumnName("final_total_amount").HasColumnType(Decimal12ColumnType);
            entity.Property(e => e.DepositAmount).HasColumnName("deposit_amount").HasColumnType(Decimal12ColumnType).HasDefaultValue(0m);
            entity.Property(e => e.PaidAmount).HasColumnName("paid_amount").HasColumnType(Decimal12ColumnType).HasDefaultValue(0m);
            entity.Property(e => e.RemainingAmount).HasColumnName("remaining_amount").HasColumnType(Decimal12ColumnType).HasDefaultValue(0m);
            entity.Property(e => e.Status).HasColumnName(StatusColumnName).HasColumnType(OrderStatusColumnType).HasDefaultValueSql("'CREATED'::order_status");
            entity.Property(e => e.DeliveryAddress).HasColumnName("delivery_address").HasColumnType(TextColumnType);
            entity.Property(e => e.ReceiverName).HasColumnName("receiver_name").HasColumnType(Varchar150ColumnType);
            entity.Property(e => e.ReceiverPhone).HasColumnName("receiver_phone").HasColumnType(Varchar20ColumnType);
            entity.Property(e => e.DeliveryNote).HasColumnName("delivery_note").HasColumnType(TextColumnType);
            entity.Property(e => e.CustomerDeliveryNote).HasColumnName("customer_delivery_note").HasColumnType(TextColumnType);
            entity.Property(e => e.CustomerConfirmedDeliveryAt).HasColumnName("customer_confirmed_delivery_at").HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.ConfirmedBy).HasColumnName("confirmed_by").HasColumnType(UuidColumnType);
            entity.Property(e => e.ConfirmedAt).HasColumnName("confirmed_at").HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.CancelledAt).HasColumnName(CancelledAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.CancellationReason).HasColumnName(CancellationReasonColumnName).HasColumnType(TextColumnType);
            entity.Property(e => e.CreatedAt).HasColumnName(CreatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.UpdatedAt).HasColumnName(UpdatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.HasIndex(e => e.QuotationId).IsUnique();
            entity.HasIndex(e => e.OrderCode).IsUnique();
            entity.HasIndex(e => new { e.ProjectId, e.ConfirmedAt, e.OrderId })
                .HasDatabaseName("idx_fin_orders_project_confirmed");
            entity.HasIndex(e => new { e.Status, e.ConfirmedAt, e.ProjectId })
                .HasDatabaseName("idx_fin_orders_receivable_status_confirmed")
                .HasFilter("remaining_amount > 0");
            entity.HasOne<Project>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Proposal>().WithMany().HasForeignKey(e => e.ProposalId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Quotation>().WithMany().HasForeignKey(e => e.QuotationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.CustomerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.SalesId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.ConfirmedBy).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureOrderItems(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.ToTable("order_items");
            entity.HasKey(e => e.OrderItemId);
            entity.Property(e => e.OrderItemId).HasColumnName("order_item_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.OrderId).HasColumnName(OrderIdColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.QuotationItemId).HasColumnName("quotation_item_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.ProductVersionId).HasColumnName(ProductVersionIdColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.ProductNameSnapshot).HasColumnName(ProductNameSnapshotColumnName).HasColumnType(Varchar150ColumnType);
            entity.Property(e => e.ProductVersionNameSnapshot).HasColumnName(ProductVersionNameSnapshotColumnName).HasColumnType(Varchar150ColumnType);
            entity.Property(e => e.ProductVersionCodeSnapshot).HasColumnName("product_version_code_snapshot").HasColumnType(Varchar50ColumnType);
            entity.Property(e => e.Quantity).HasColumnName(QuantityColumnName).HasColumnType(IntegerColumnType).HasDefaultValue(1).IsRequired();
            entity.Property(e => e.DeliveredQuantity).HasColumnName("delivered_quantity").HasColumnType(IntegerColumnType).HasDefaultValue(0).IsRequired();
            entity.Property(e => e.Status).HasColumnName(StatusColumnName).HasColumnType(OrderItemStatusColumnType).HasDefaultValueSql("'PENDING'::order_item_status");
            entity.Property(e => e.UnitPrice).HasColumnName("unit_price").HasColumnType(Decimal14ColumnType).HasDefaultValue(0m).IsRequired();
            entity.Property(e => e.DiscountAmount).HasColumnName(DiscountAmountColumnName).HasColumnType(Decimal14ColumnType).HasDefaultValue(0m).IsRequired();
            entity.Property(e => e.SubtotalAmount).HasColumnName(SubtotalAmountColumnName).HasColumnType(Decimal14ColumnType).HasDefaultValue(0m).IsRequired();
            entity.Property(e => e.AdjustmentAmount).HasColumnName("adjustment_amount").HasColumnType(Decimal12ColumnType).HasDefaultValue(0m);
            entity.Property(e => e.UnavailableReason).HasColumnName("unavailable_reason").HasColumnType(TextColumnType);
            entity.Property(e => e.UnavailableConfirmedBy).HasColumnName("unavailable_confirmed_by").HasColumnType(UuidColumnType);
            entity.Property(e => e.UnavailableConfirmedAt).HasColumnName("unavailable_confirmed_at").HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.ProductionNote).HasColumnName("production_note").HasColumnType(TextColumnType);
            entity.Property(e => e.DeliveryNote).HasColumnName("delivery_note").HasColumnType(TextColumnType);
            entity.Property(e => e.DeliveredAt).HasColumnName("delivered_at").HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.DeliveredBy).HasColumnName("delivered_by").HasColumnType(UuidColumnType);
            entity.HasOne<Order>().WithMany().HasForeignKey(e => e.OrderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<QuotationItem>().WithMany().HasForeignKey(e => e.QuotationItemId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProductVersion>().WithMany().HasForeignKey(e => e.ProductVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.DeliveredBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.UnavailableConfirmedBy).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureDeliveries(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Delivery>(entity =>
        {
            entity.ToTable("deliveries");
            entity.HasKey(e => e.DeliveryId);
            entity.Property(e => e.DeliveryId).HasColumnName("delivery_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.OrderId).HasColumnName(OrderIdColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.ProjectScheduleId).HasColumnName("project_schedule_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.Status).HasColumnName(StatusColumnName).HasColumnType(DeliveryStatusColumnType).HasDefaultValueSql("'IN_PROGRESS'::delivery_status");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by").HasColumnType(UuidColumnType);
            entity.Property(e => e.CompletedBy).HasColumnName("completed_by").HasColumnType(UuidColumnType);
            entity.Property(e => e.Note).HasColumnName(NoteColumnName).HasColumnType(TextColumnType);
            entity.Property(e => e.CreatedAt).HasColumnName(CreatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.UpdatedAt).HasColumnName(UpdatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.CompletedAt).HasColumnName(CompletedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.HasIndex(e => e.OrderId).HasDatabaseName("idx_deliveries_order_id");
            entity.HasIndex(e => e.ProjectScheduleId).HasDatabaseName("idx_deliveries_project_schedule_id");
            entity.HasIndex(e => e.ProjectScheduleId)
                .IsUnique()
                .HasDatabaseName("ux_deliveries_project_schedule_id")
                .HasFilter("project_schedule_id IS NOT NULL");
            entity.HasOne<Order>().WithMany().HasForeignKey(e => e.OrderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProjectSchedule>().WithMany().HasForeignKey(e => e.ProjectScheduleId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.CreatedBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.CompletedBy).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureDeliveryItems(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DeliveryItem>(entity =>
        {
            entity.ToTable("delivery_items");
            entity.HasKey(e => e.DeliveryItemId);
            entity.Property(e => e.DeliveryItemId).HasColumnName("delivery_item_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.DeliveryId).HasColumnName("delivery_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.OrderItemId).HasColumnName("order_item_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.Quantity).HasColumnName(QuantityColumnName).HasColumnType(IntegerColumnType).IsRequired();
            entity.Property(e => e.Note).HasColumnName(NoteColumnName).HasColumnType(TextColumnType);
            entity.HasIndex(e => e.DeliveryId).HasDatabaseName("idx_delivery_items_delivery_id");
            entity.HasIndex(e => e.OrderItemId).HasDatabaseName("idx_delivery_items_order_item_id");
            entity.HasOne<Delivery>().WithMany().HasForeignKey(e => e.DeliveryId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<OrderItem>().WithMany().HasForeignKey(e => e.OrderItemId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigurePayments(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("payments");
            entity.HasKey(e => e.PaymentId);
            entity.Property(e => e.PaymentId).HasColumnName("payment_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.ProjectId).HasColumnName(ProjectIdColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.OrderId).HasColumnName(OrderIdColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.QuotationId).HasColumnName(QuotationIdColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.PaymentCode).HasColumnName("payment_code").HasColumnType(Varchar100ColumnType).IsRequired();
            entity.Property(e => e.PaidBy).HasColumnName("paid_by").HasColumnType(UuidColumnType);
            entity.Property(e => e.PaymentType).HasColumnName("payment_type").HasColumnType(PaymentTypeColumnType).HasDefaultValueSql("'OTHER'::payment_type");
            entity.Property(e => e.Amount).HasColumnName("amount").HasColumnType(Decimal12ColumnType).IsRequired();
            entity.Property(e => e.Currency).HasColumnName("currency").HasColumnType(Varchar10ColumnType).HasDefaultValue("VND").IsRequired();
            entity.Property(e => e.Status).HasColumnName(StatusColumnName).HasColumnType(PaymentStatusColumnType).HasDefaultValueSql("'PENDING'::payment_status");
            entity.Property(e => e.ExpiredAt).HasColumnName("expired_at").HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.PaidAt).HasColumnName("paid_at").HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.CancelledAt).HasColumnName(CancelledAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.Note).HasColumnName(NoteColumnName).HasColumnType(TextColumnType);
            entity.Property(e => e.CreatedAt).HasColumnName(CreatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.UpdatedAt).HasColumnName(UpdatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.HasOne<Project>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Order>().WithMany().HasForeignKey(e => e.OrderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Quotation>().WithMany().HasForeignKey(e => e.QuotationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.PaidBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.PaymentCode).IsUnique().HasDatabaseName("uq_payments_payment_code");
            entity.HasIndex(e => e.ProjectId).HasDatabaseName("idx_payments_project_id");
            entity.HasIndex(e => e.OrderId).HasDatabaseName("idx_payments_order_id");
            entity.HasIndex(e => e.QuotationId).HasDatabaseName("idx_payments_quotation_id");
            entity.HasIndex(e => new { e.ProjectId, e.CreatedAt }).HasDatabaseName("idx_payments_project_time");
            entity.HasIndex(e => new { e.OrderId, e.PaymentType }).HasDatabaseName("idx_payments_order_type");
            entity.HasIndex(e => new { e.Status, e.PaidAt, e.PaymentType, e.Currency })
                .HasDatabaseName("idx_fin_payments_paid_reporting")
                .HasFilter("status = 'PAID' AND paid_at IS NOT NULL");
            entity.HasIndex(e => new { e.Status, e.ExpiredAt, e.CreatedAt, e.PaymentType, e.OrderId })
                .HasDatabaseName("idx_fin_payments_active_obligations")
                .HasFilter("status IN ('PENDING', 'PROCESSING')");
            entity.HasIndex(e => new { e.OrderId, e.PaymentType })
                .IsUnique()
                .HasDatabaseName("uq_payments_active_order_type")
                .HasFilter("order_id IS NOT NULL AND status IN ('PENDING', 'PROCESSING')");
        });
    }

    private static void ConfigureProductionRequests(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProductionRequest>(entity =>
        {
            entity.ToTable("production_requests");
            entity.HasKey(e => e.ProductionRequestId);
            entity.Property(e => e.ProductionRequestId).HasColumnName("production_request_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.ProductionCode).HasColumnName("production_code").HasColumnType(Varchar50ColumnType);
            entity.Property(e => e.ProjectId).HasColumnName(ProjectIdColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.OrderId).HasColumnName(OrderIdColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.AssignedTo).HasColumnName("assigned_to").HasColumnType(UuidColumnType);
            entity.Property(e => e.Status).HasColumnName(StatusColumnName).HasColumnType(ProductionRequestStatusColumnType).HasDefaultValueSql("'PENDING'::production_request_status");
            entity.Property(e => e.Priority).HasColumnName("priority").HasColumnType(Varchar30ColumnType);
            entity.Property(e => e.ActualStartDate).HasColumnName("actual_start_date").HasColumnType(DateColumnType);
            entity.Property(e => e.ActualCompletionDate).HasColumnName("actual_completion_date").HasColumnType(DateColumnType);
            entity.Property(e => e.CancellationReason).HasColumnName(CancellationReasonColumnName).HasColumnType(TextColumnType);
            entity.Property(e => e.Note).HasColumnName(NoteColumnName).HasColumnType(TextColumnType);
            entity.Property(e => e.CreatedAt).HasColumnName(CreatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.UpdatedAt).HasColumnName(UpdatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.HasIndex(e => e.ProductionCode).IsUnique();
            entity.HasOne<Project>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Order>().WithMany().HasForeignKey(e => e.OrderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.AssignedTo).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigurePaymentTransactions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PaymentTransaction>(entity =>
        {
            entity.ToTable("payment_transactions");
            entity.HasKey(e => e.PaymentTransactionId);
            entity.Property(e => e.PaymentTransactionId).HasColumnName("payment_transaction_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.PaymentId).HasColumnName("payment_id").HasColumnType(UuidColumnType).IsRequired();
            entity.Property(e => e.ProjectId).HasColumnName("project_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.OrderId).HasColumnName("order_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.TransactionCode).HasColumnName("transaction_code").HasColumnType(Varchar100ColumnType).IsRequired();
            entity.Property(e => e.TransactionType).HasColumnName("transaction_type").HasColumnType(PaymentTransactionTypeColumnType).HasDefaultValueSql("'CHARGE'::payment_transaction_type").IsRequired();
            entity.Property(e => e.Amount).HasColumnName("amount").HasColumnType(Decimal12ColumnType).IsRequired();
            entity.Property(e => e.Currency).HasColumnName("currency").HasColumnType(Varchar10ColumnType).HasDefaultValue("VND").IsRequired();
            entity.Property(e => e.PaymentProvider).HasColumnName("payment_provider").HasColumnType(PaymentProviderColumnType);
            entity.Property(e => e.PaymentMethod).HasColumnName("payment_method").HasColumnType(PaymentMethodColumnType);
            entity.Property(e => e.ProviderTransactionId).HasColumnName("provider_transaction_id").HasColumnType(Varchar255ColumnType);
            entity.Property(e => e.ProviderReferenceCode).HasColumnName("provider_reference_code").HasColumnType(Varchar255ColumnType);
            entity.Property(e => e.Status).HasColumnName("status").HasColumnType(PaymentTransactionStatusColumnType).HasDefaultValueSql("'PENDING'::payment_transaction_status").IsRequired();
            entity.Property(e => e.PaymentUrl).HasColumnName("payment_url").HasColumnType(TextColumnType);
            entity.Property(e => e.QrContent).HasColumnName("qr_content").HasColumnType(TextColumnType);
            entity.Property(e => e.TransactionTime).HasColumnName("transaction_time").HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.ConfirmedBy).HasColumnName("confirmed_by").HasColumnType(UuidColumnType);
            entity.Property(e => e.ConfirmedAt).HasColumnName("confirmed_at").HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.FailureReason).HasColumnName("failure_reason").HasColumnType(TextColumnType);
            entity.Property(e => e.RawProviderPayload).HasColumnName("raw_provider_payload").HasColumnType(JsonbColumnType);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType(TimestampWithTimeZoneColumnType).IsRequired();
            entity.HasIndex(e => e.TransactionCode).IsUnique().HasDatabaseName("uq_payment_transactions_code");
            entity.HasIndex(e => e.PaymentId)
                .IsUnique()
                .HasDatabaseName("uq_payment_transactions_one_success")
                .HasFilter("status = 'SUCCESS'");
            entity.HasIndex(e => e.PaymentId).HasDatabaseName("idx_payment_transactions_payment_id");
            entity.HasIndex(e => new { e.ProjectId, e.CreatedAt }).HasDatabaseName("idx_payment_transactions_project_time");
            entity.HasIndex(e => new { e.OrderId, e.CreatedAt }).HasDatabaseName("idx_payment_transactions_order_time");
            entity.HasIndex(e => e.ProviderTransactionId).HasDatabaseName("idx_payment_transactions_provider_transaction_id");
            entity.HasIndex(e => e.ProviderReferenceCode).HasDatabaseName("idx_payment_transactions_provider_reference_code");
            entity.HasIndex(e => new { e.Status, e.CreatedAt, e.Currency })
                .HasDatabaseName("idx_fin_payment_transactions_failed_reporting")
                .HasFilter("status = 'FAILED'");
            entity.HasIndex(e => new { e.PaymentId, e.Status, e.CreatedAt })
                .HasDatabaseName("idx_fin_payment_transactions_payment_failed_time")
                .HasFilter("status = 'FAILED'");
            entity.HasIndex(e => new { e.PaymentProvider, e.ProviderTransactionId })
                .IsUnique()
                .HasDatabaseName("uq_payment_transactions_provider_txn");
            entity.HasOne<Payment>().WithMany().HasForeignKey(e => e.PaymentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Project>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Order>().WithMany().HasForeignKey(e => e.OrderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.ConfirmedBy).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureProductionItems(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProductionItem>(entity =>
        {
            entity.ToTable("production_items");
            entity.HasKey(e => e.ProductionItemId);
            entity.Property(e => e.ProductionItemId).HasColumnName("production_item_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.ProductionRequestId).HasColumnName("production_request_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.OrderItemId).HasColumnName("order_item_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.ProductVersionId).HasColumnName(ProductVersionIdColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.ProductNameSnapshot).HasColumnName(ProductNameSnapshotColumnName).HasColumnType(Varchar150ColumnType);
            entity.Property(e => e.ProductVersionNameSnapshot).HasColumnName(ProductVersionNameSnapshotColumnName).HasColumnType(Varchar150ColumnType);
            entity.Property(e => e.Quantity).HasColumnName(QuantityColumnName).HasColumnType(IntegerColumnType).HasDefaultValue(1);
            entity.Property(e => e.StartedAt).HasColumnName("started_at").HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.Status).HasColumnName(StatusColumnName).HasColumnType(ProductionItemStatusColumnType).HasDefaultValueSql("'PENDING'::production_item_status");
            entity.Property(e => e.MaterialNote).HasColumnName("material_note").HasColumnType(TextColumnType);
            entity.Property(e => e.ProductionNote).HasColumnName("production_note").HasColumnType(TextColumnType);
            entity.Property(e => e.CancellationReason).HasColumnName(CancellationReasonColumnName).HasColumnType(TextColumnType);
            entity.Property(e => e.CompletedAt).HasColumnName(CompletedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.HasIndex(e => new { e.ProductionRequestId, e.OrderItemId }).IsUnique();
            entity.HasOne<ProductionRequest>().WithMany().HasForeignKey(e => e.ProductionRequestId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<OrderItem>().WithMany().HasForeignKey(e => e.OrderItemId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProductVersion>().WithMany().HasForeignKey(e => e.ProductVersionId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureOperationalDelayReports(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OperationalDelayReport>(entity =>
        {
            entity.ToTable("operational_delay_reports");
            entity.HasKey(e => e.OperationalDelayReportId);
            entity.Property(e => e.OperationalDelayReportId).HasColumnName("operational_delay_report_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.ProjectId).HasColumnName(ProjectIdColumnName).HasColumnType(UuidColumnType).IsRequired();
            entity.Property(e => e.ReportPhase).HasColumnName("report_phase").HasColumnType(OperationalDelayPhaseColumnType).IsRequired();
            entity.Property(e => e.ProductionRequestId).HasColumnName("production_request_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.OrderId).HasColumnName(OrderIdColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.DeliveryId).HasColumnName("delivery_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.DeadlineSnapshot).HasColumnName("deadline_snapshot").HasColumnType(DateColumnType).IsRequired();
            entity.Property(e => e.DelayState).HasColumnName("delay_state").HasColumnType(OperationalDelayStateColumnType).IsRequired();
            entity.Property(e => e.ReasonCode).HasColumnName("reason_code").HasColumnType(Varchar100ColumnType);
            entity.Property(e => e.ReasonDetail).HasColumnName("reason_detail").HasColumnType(TextColumnType).IsRequired();
            entity.Property(e => e.ReportedBy).HasColumnName("reported_by").HasColumnType(UuidColumnType).IsRequired();
            entity.Property(e => e.ReportedAt).HasColumnName("reported_at").HasColumnType(TimestampWithTimeZoneColumnType).IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName(CreatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType).IsRequired();
            entity.HasIndex(e => new { e.ProjectId, e.ReportPhase }).HasDatabaseName("idx_operational_delay_reports_project_phase");
            entity.HasIndex(e => e.ProductionRequestId).HasDatabaseName("idx_operational_delay_reports_production_request");
            entity.HasIndex(e => e.OrderId).HasDatabaseName("idx_operational_delay_reports_order");
            entity.HasIndex(e => e.ReportedAt).HasDatabaseName("idx_operational_delay_reports_reported_at");
            entity.HasOne<Project>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProductionRequest>().WithMany().HasForeignKey(e => e.ProductionRequestId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Order>().WithMany().HasForeignKey(e => e.OrderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Delivery>().WithMany().HasForeignKey(e => e.DeliveryId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.ReportedBy).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureDeliveryProductIssueReports(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DeliveryProductIssueReport>(entity =>
        {
            entity.ToTable("delivery_product_issue_reports");
            entity.HasKey(e => e.DeliveryProductIssueReportId);
            entity.Property(e => e.DeliveryProductIssueReportId).HasColumnName("delivery_product_issue_report_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.ProjectId).HasColumnName(ProjectIdColumnName).HasColumnType(UuidColumnType).IsRequired();
            entity.Property(e => e.OrderId).HasColumnName(OrderIdColumnName).HasColumnType(UuidColumnType).IsRequired();
            entity.Property(e => e.OrderItemId).HasColumnName("order_item_id").HasColumnType(UuidColumnType).IsRequired();
            entity.Property(e => e.DeliveryItemId).HasColumnName("delivery_item_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.IssueType).HasColumnName("issue_type").HasColumnType(DeliveryProductIssueTypeColumnType).IsRequired();
            entity.Property(e => e.Description).HasColumnName(DescriptionColumnName).HasColumnType(TextColumnType).IsRequired();
            entity.Property(e => e.AffectedQuantity).HasColumnName("affected_quantity").HasColumnType(IntegerColumnType);
            entity.Property(e => e.ReportedBy).HasColumnName("reported_by").HasColumnType(UuidColumnType).IsRequired();
            entity.Property(e => e.ReportedAt).HasColumnName("reported_at").HasColumnType(TimestampWithTimeZoneColumnType).IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName(CreatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType).IsRequired();
            entity.HasIndex(e => e.ProjectId).HasDatabaseName("idx_delivery_product_issue_reports_project");
            entity.HasIndex(e => e.OrderId).HasDatabaseName("idx_delivery_product_issue_reports_order");
            entity.HasIndex(e => e.OrderItemId).HasDatabaseName("idx_delivery_product_issue_reports_order_item");
            entity.HasIndex(e => e.DeliveryItemId).HasDatabaseName("idx_delivery_product_issue_reports_delivery_item");
            entity.HasIndex(e => e.ReportedAt).HasDatabaseName("idx_delivery_product_issue_reports_reported_at");
            entity.HasOne<Project>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Order>().WithMany().HasForeignKey(e => e.OrderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<OrderItem>().WithMany().HasForeignKey(e => e.OrderItemId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<DeliveryItem>().WithMany().HasForeignKey(e => e.DeliveryItemId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.ReportedBy).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureProjectReviews(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProjectReview>(entity =>
        {
            entity.ToTable("project_reviews");
            entity.HasKey(e => e.ReviewId);
            entity.Property(e => e.ReviewId).HasColumnName("review_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.ProjectId).HasColumnName(ProjectIdColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.OrderId).HasColumnName(OrderIdColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.CustomerId).HasColumnName(CustomerIdColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.Rating).HasColumnName("rating").HasColumnType(IntegerColumnType);
            entity.Property(e => e.DesignQualityRating).HasColumnName("design_quality_rating").HasColumnType(IntegerColumnType);
            entity.Property(e => e.ServiceQualityRating).HasColumnName("service_quality_rating").HasColumnType(IntegerColumnType);
            entity.Property(e => e.DeliveryRating).HasColumnName("delivery_rating").HasColumnType(IntegerColumnType);
            entity.Property(e => e.Comment).HasColumnName("comment").HasColumnType(TextColumnType);
            entity.Property(e => e.AllowPublicDisplay).HasColumnName("allow_public_display").HasColumnType(BooleanColumnType).HasDefaultValue(false).IsRequired();
            entity.Property(e => e.PublicDisplayConsentAt).HasColumnName("public_display_consent_at").HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.CreatedAt).HasColumnName(CreatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.UpdatedAt).HasColumnName(UpdatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.HasIndex(e => e.ProjectId).IsUnique();
            entity.HasOne<Project>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Order>().WithMany().HasForeignKey(e => e.OrderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.CustomerId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureLayoutAssets(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LayoutAsset>(entity =>
        {
            entity.ToTable("layout_assets");
            entity.HasKey(e => e.LayoutAssetId);
            entity.Property(e => e.LayoutAssetId).HasColumnName("layout_asset_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.AssetCode).HasColumnName("asset_code").HasColumnType(Varchar50ColumnType).IsRequired();
            entity.Property(e => e.AssetName).HasColumnName("asset_name").HasColumnType(Varchar150ColumnType).IsRequired();
            entity.Property(e => e.AssetType).HasColumnName("asset_type").HasColumnType(LayoutAssetTypeColumnType).IsRequired();
            entity.Property(e => e.Description).HasColumnName(DescriptionColumnName).HasColumnType(TextColumnType);
            entity.Property(e => e.Status).HasColumnName(StatusColumnName).HasColumnType(LayoutAssetStatusColumnType).HasDefaultValueSql("'ACTIVE'::layout_asset_status").IsRequired();
            entity.Property(e => e.CreatedBy).HasColumnName(CreatedByColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.CreatedAt).HasColumnName(CreatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType).IsRequired();
            entity.Property(e => e.UpdatedAt).HasColumnName(UpdatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType).IsRequired();
            entity.HasIndex(e => e.AssetCode).IsUnique();
            entity.HasIndex(e => e.AssetType);
            entity.HasIndex(e => e.Status);
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureProjectShowcases(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProjectShowcase>(entity =>
        {
            entity.ToTable("project_showcases");
            entity.HasKey(e => e.ProjectShowcaseId);
            entity.Property(e => e.ProjectShowcaseId).HasColumnName("project_showcase_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.ProjectId).HasColumnName(ProjectIdColumnName).HasColumnType(UuidColumnType).IsRequired();
            entity.Property(e => e.FeaturedReviewId).HasColumnName("featured_review_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.Title).HasColumnName(TitleColumnName).HasColumnType(Varchar150ColumnType).IsRequired();
            entity.Property(e => e.Slug).HasColumnName("slug").HasColumnType(Varchar255ColumnType).IsRequired();
            entity.Property(e => e.Summary).HasColumnName("summary").HasColumnType(TextColumnType);
            entity.Property(e => e.Description).HasColumnName(DescriptionColumnName).HasColumnType(TextColumnType);
            entity.Property(e => e.Status).HasColumnName(StatusColumnName).HasColumnType(ProjectShowcaseStatusColumnType).HasDefaultValueSql("'DRAFT'::project_showcase_status").IsRequired();
            entity.Property(e => e.CreatedBy).HasColumnName(CreatedByColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.ApprovedBy).HasColumnName("approved_by").HasColumnType(UuidColumnType);
            entity.Property(e => e.PublishedBy).HasColumnName("published_by").HasColumnType(UuidColumnType);
            entity.Property(e => e.ApprovedAt).HasColumnName("approved_at").HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.PublishedAt).HasColumnName("published_at").HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.ArchivedAt).HasColumnName("archived_at").HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.CreatedAt).HasColumnName(CreatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType).IsRequired();
            entity.Property(e => e.UpdatedAt).HasColumnName(UpdatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType).IsRequired();
            entity.HasIndex(e => e.ProjectId).IsUnique();
            entity.HasIndex(e => e.Slug).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasOne<Project>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProjectReview>().WithMany().HasForeignKey(e => e.FeaturedReviewId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.CreatedBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.ApprovedBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.PublishedBy).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureProjectShowcaseMedia(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProjectShowcaseMedia>(entity =>
        {
            entity.ToTable("project_showcase_media");
            entity.HasKey(e => e.ProjectShowcaseMediaId);
            entity.Property(e => e.ProjectShowcaseMediaId).HasColumnName("project_showcase_media_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.ProjectShowcaseId).HasColumnName("project_showcase_id").HasColumnType(UuidColumnType).IsRequired();
            entity.Property(e => e.FileId).HasColumnName("file_id").HasColumnType(UuidColumnType).IsRequired();
            entity.Property(e => e.MediaType).HasColumnName("media_type").HasColumnType(ProjectShowcaseMediaTypeColumnType).IsRequired();
            entity.Property(e => e.Title).HasColumnName(TitleColumnName).HasColumnType(Varchar150ColumnType);
            entity.Property(e => e.Caption).HasColumnName("caption").HasColumnType(TextColumnType);
            entity.Property(e => e.IsCover).HasColumnName("is_cover").HasColumnType(BooleanColumnType).HasDefaultValue(false).IsRequired();
            entity.Property(e => e.DisplayOrder).HasColumnName("display_order").HasColumnType(IntegerColumnType).IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName(CreatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType).IsRequired();
            entity.Property(e => e.UpdatedAt).HasColumnName(UpdatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType).IsRequired();
            entity.HasIndex(e => new { e.ProjectShowcaseId, e.DisplayOrder });
            entity.HasIndex(e => new { e.ProjectShowcaseId, e.FileId }).IsUnique();
            entity.HasIndex(e => e.ProjectShowcaseId)
                .IsUnique()
                .HasFilter("is_cover = true")
                .HasDatabaseName("ux_project_showcase_media_one_cover");
            entity.HasOne<ProjectShowcase>().WithMany().HasForeignKey(e => e.ProjectShowcaseId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<StoredFile>().WithMany().HasForeignKey(e => e.FileId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        ApplyAuditTimestamps();
        return base.SaveChanges();
    }

    private void ApplyAuditTimestamps()
    {
        foreach (var entry in ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            var now = DateTime.UtcNow;

            if (entry.State == EntityState.Added)
            {
                SetCreatedAtIfEmpty(entry, now);
            }

            SetUpdatedAtIfPresent(entry, now);
            NormalizeDateTimeProperties(entry);
        }
    }

    private static void SetCreatedAtIfEmpty(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry, DateTime now)
    {
        var property = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "CreatedAt");
        if (property is null)
        {
            return;
        }

        if (property.CurrentValue is null ||
            property.CurrentValue is DateTime value && value == default)
        {
            property.CurrentValue = now;
        }
    }

    private static void SetUpdatedAtIfPresent(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry, DateTime now)
    {
        var property = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "UpdatedAt");
        if (property is not null)
        {
            property.CurrentValue = now;
        }
    }

    private static void NormalizeDateTimeProperties(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        foreach (var property in entry.Properties.Where(p => p.Metadata.ClrType == typeof(DateTime) || p.Metadata.ClrType == typeof(DateTime?)))
        {
            if (property.CurrentValue is DateTime value && value.Kind != DateTimeKind.Utc)
            {
                property.CurrentValue = DateTime.SpecifyKind(value, DateTimeKind.Utc);
            }
        }
    }
}
