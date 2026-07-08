using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    private const string UuidColumnType = "uuid";
    private const string TimestampWithTimeZoneColumnType = "timestamp with time zone";
    private const string TextColumnType = "text";
    private const string Decimal12ColumnType = "numeric(12,2)";
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
    private const string ProjectScheduleTypeColumnType = "project_schedule_type";
    private const string ProjectScheduleStatusColumnType = "project_schedule_status";
    private const string ProposalStatusColumnType = "proposal_status";
    private const string ProposalSceneTypeColumnType = "proposal_scene_type";
    private const string ProposalSceneVariantStatusColumnType = "proposal_scene_variant_status";
    private const string ProposalSceneVariantTypeColumnType = "proposal_scene_variant_type";
    private const string CustomizationStatusColumnType = "customization_status";
    private const string QuotationStatusColumnType = "quotation_status";
    private const string QuotationItemTypeColumnType = "quotation_item_type";
    private const string OrderStatusColumnType = "order_status";
    private const string OrderItemStatusColumnType = "order_item_status";
    private const string PaymentStatusColumnType = "payment_status";
    private const string PaymentTypeColumnType = "payment_type";
    private const string PaymentProviderColumnType = "payment_provider";
    private const string PaymentMethodColumnType = "payment_method";
    private const string PaymentTransactionTypeColumnType = "payment_transaction_type";
    private const string PaymentTransactionStatusColumnType = "payment_transaction_status";
    private const string ProductionRequestStatusColumnType = "production_request_status";
    private const string ProductionItemStatusColumnType = "production_item_status";

    private const string ProjectChatTypeColumnType = "project_chat_type";
    private const string ProjectChatStatusColumnType = "project_chat_status";
    private const string ProjectChatMessageTypeColumnType = "project_chat_message_type";
    private const string FileStatusColumnType = "file_status";
    private const string FileVisibilityColumnType = "file_visibility";
    private const string FileTypeColumnType = "file_type";
    private const string ProductStatusColumnType = "product_status";
    private const string ProductVersionTypeColumnType = "product_version_type";
    private const string CreatedAtColumnName = "created_at";
    private const string UpdatedAtColumnName = "updated_at";
    private const string StatusColumnName = "status";
    private const string ProjectIdColumnName = "project_id";
    private const string DescriptionColumnName = "description";
    private const string ProposalIdColumnName = "proposal_id";
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
    private const string RejectedAtColumnName = "rejected_at";
    private const string ProposalItemIdColumnName = "proposal_item_id";
    private const string ProductVersionNameSnapshotColumnName = "product_version_name_snapshot";
    private const string ProductNameSnapshotColumnName = "product_name_snapshot";
    private const string HeightColumnName = "height";
    private const string DiscountAmountColumnName = "discount_amount";
    private const string CustomizationFeeColumnName = "customization_fee";
    private const string CustomerIdColumnName = "customer_id";

    public DbSet<Role> RoleSet => Set<Role>();
    public DbSet<Account> AccountSet => Set<Account>();
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
    public DbSet<ProjectSchedule> ProjectScheduleSet => Set<ProjectSchedule>();
    public DbSet<Proposal> ProposalSet => Set<Proposal>();
    public DbSet<ProposalScene> ProposalSceneSet => Set<ProposalScene>();
    public DbSet<ProposalItem> ProposalItemSet => Set<ProposalItem>();
    public DbSet<ProposalSceneVariant> ProposalSceneVariantSet => Set<ProposalSceneVariant>();
    public DbSet<CustomizationRequest> CustomizationRequestSet => Set<CustomizationRequest>();
    public DbSet<Quotation> QuotationSet => Set<Quotation>();
    public DbSet<QuotationItem> QuotationItemSet => Set<QuotationItem>();
    public DbSet<Order> OrderSet => Set<Order>();
    public DbSet<OrderItem> OrderItemSet => Set<OrderItem>();
    public DbSet<Payment> PaymentSet => Set<Payment>();
    public DbSet<PaymentTransaction> PaymentTransactionSet => Set<PaymentTransaction>();
    public DbSet<ProductionRequest> ProductionRequestSet => Set<ProductionRequest>();
    public DbSet<ProductionItem> ProductionItemSet => Set<ProductionItem>();
    public DbSet<ProjectReview> ProjectReviewSet => Set<ProjectReview>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasAnnotation("Npgsql:Enum:account_status", "ACTIVE,INACTIVE,SUSPENDED");
        modelBuilder.HasAnnotation("Npgsql:Enum:project_status", "SUBMITTED,IN_CONSULTATION,NEED_BASIC_INFORMATION,WAITING_FOR_DESIGNER_ASSIGNMENT,MEASUREMENT_REQUIRED,SPACE_VERIFIED,PROPOSAL_DRAFTING,WAITING_FOR_CUSTOMER_REVIEW,REVISION_REQUESTED,PROPOSAL_SELECTED,QUOTATION_SENT,QUOTATION_REVISION_REQUESTED,ORDER_CONFIRMED,IN_PRODUCTION,PRODUCTION_BLOCKED,READY_FOR_DELIVERY,DELIVERING,DELIVERED,COMPLETED,REJECTED");
        modelBuilder.HasAnnotation("Npgsql:Enum:project_area_type", "STORE,FLOOR,ROOM,ZONE,OUTDOOR_AREA,OTHER");
        modelBuilder.HasAnnotation("Npgsql:Enum:project_area_status", "DRAFT,NEED_MEASUREMENT,MEASURED,VERIFIED,CANCELLED");
        modelBuilder.HasAnnotation("Npgsql:Enum:project_schedule_type", "MEASUREMENT,CONSULTATION,DESIGN_REVIEW,DELIVERY,HANDOVER,OTHER");
        modelBuilder.HasAnnotation("Npgsql:Enum:project_schedule_status", "PENDING_CONFIRMATION,CONFIRMED,COMPLETED,CANCELLED");
        modelBuilder.HasAnnotation("Npgsql:Enum:proposal_status", "DRAFT,PUBLISHED,VIEWED,SELECTED,REVISION_REQUESTED,REJECTED,ARCHIVED");
        modelBuilder.HasAnnotation("Npgsql:Enum:proposal_scene_type", "TWO_D,THREE_D");
        modelBuilder.HasAnnotation("Npgsql:Enum:proposal_scene_variant_status", "DRAFT,SUBMITTED,ACCEPTED,REJECTED,APPLIED");
        modelBuilder.HasAnnotation("Npgsql:Enum:proposal_scene_variant_type", "CUSTOMER_SUGGESTION,DESIGNER_REVISION");
        modelBuilder.HasAnnotation("Npgsql:Enum:customization_status", "SUBMITTED,DESIGN_REVIEWING,PRODUCTION_REVIEWING,WAITING_FOR_CUSTOMER_FINAL_APPROVAL,NOT_FEASIBLE,ACCEPTED,REJECTED_BY_CUSTOMER,CANCELLED");
        modelBuilder.HasAnnotation("Npgsql:Enum:quotation_status", "DRAFT,SENT,REVISION_REQUESTED,REVISED,ACCEPTED,REJECTED,EXPIRED,CANCELLED");
        modelBuilder.HasAnnotation("Npgsql:Enum:quotation_item_type", "PRODUCT_ITEM,MANUAL_ITEM");
        modelBuilder.HasAnnotation("Npgsql:Enum:order_status", "CREATED,DEPOSIT_PENDING,DEPOSIT_PAID,IN_PRODUCTION,PRODUCTION_PARTIALLY_FAILED,PRODUCTION_COMPLETED,READY_FOR_DELIVERY,DELIVERY_SCHEDULED,DELIVERING,DELIVERED,FINAL_PAYMENT_PENDING,COMPLETED,CANCELLED");
        modelBuilder.HasAnnotation("Npgsql:Enum:order_item_status", "PENDING,IN_PRODUCTION,READY,UNAVAILABLE,DELIVERED,CANCELLED");
        modelBuilder.HasAnnotation("Npgsql:Enum:payment_status", "PENDING,PROCESSING,PARTIALLY_PAID,PAID,FAILED,CANCELLED,EXPIRED,REFUNDED");
        modelBuilder.HasAnnotation("Npgsql:Enum:payment_type", "PROJECT_START_FEE,DEPOSIT,REMAINING_PAYMENT,FULL_PAYMENT,REFUND,OTHER");
        modelBuilder.HasAnnotation("Npgsql:Enum:payment_provider", "PAYOS,SEPAY,CASH,MANUAL_BANK_TRANSFER,OTHER");
        modelBuilder.HasAnnotation("Npgsql:Enum:payment_method", "PAYMENT_LINK,QR_CODE,BANK_TRANSFER,CASH,OTHER");
        modelBuilder.HasAnnotation("Npgsql:Enum:payment_transaction_type", "CHARGE,REFUND,ADJUSTMENT");
        modelBuilder.HasAnnotation("Npgsql:Enum:payment_transaction_status", "PENDING,SUCCESS,FAILED,CANCELLED");
        modelBuilder.HasAnnotation("Npgsql:Enum:production_request_status", "PENDING_REVIEW,FEASIBLE,IN_PRODUCTION,COMPLETED,BLOCKED,CANCELLED");
        modelBuilder.HasAnnotation("Npgsql:Enum:production_item_status", "PENDING,IN_PRODUCTION,COMPLETED,BLOCKED,CANCELLED");
        modelBuilder.HasAnnotation("Npgsql:Enum:notification_status", "UNREAD,READ");
        modelBuilder.HasAnnotation("Npgsql:Enum:project_chat_type", "SALES,DESIGNER,PRODUCTION,DELIVERY,GENERAL,INTERNAL");
        modelBuilder.HasAnnotation("Npgsql:Enum:project_chat_status", "OPEN,CLOSED,ARCHIVED");
        modelBuilder.HasAnnotation("Npgsql:Enum:project_chat_message_type", "TEXT,FILE,SYSTEM");
        modelBuilder.HasAnnotation("Npgsql:Enum:file_status", "ACTIVE,ARCHIVED");
        modelBuilder.HasAnnotation("Npgsql:Enum:file_visibility", "CUSTOMER_VISIBLE,STAFF_ONLY,PRIVATE");
        modelBuilder.HasAnnotation("Npgsql:Enum:file_type", "SPACE_IMAGE,FLOOR_PLAN,REFERENCE_IMAGE,BRAND_ASSET,CAD_FILE,PDF_DRAWING,MEASUREMENT_REPORT,LIDAR_SCAN,MODEL_3D,TEXTURE,PRODUCT_PREVIEW,PROPOSAL_PREVIEW,PROPOSAL_FILE,QUOTATION_FILE,ORDER_DOCUMENT,PRODUCTION_FILE,DELIVERY_PHOTO,DELIVERY_NOTE,REVIEW_IMAGE,OTHER");
        modelBuilder.HasAnnotation("Npgsql:Enum:product_status", "ACTIVE,INACTIVE,ARCHIVED");
        modelBuilder.HasAnnotation("Npgsql:Enum:product_version_type", "STANDARD,CUSTOM,PROJECT_SPECIFIC");

        ConfigureRoles(modelBuilder);
        ConfigureAccounts(modelBuilder);
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
        ConfigureProjectSchedules(modelBuilder);
        ConfigureProposals(modelBuilder);
        ConfigureProposalScenes(modelBuilder);
        ConfigureProposalItems(modelBuilder);
        ConfigureProposalSceneVariants(modelBuilder);
        ConfigureCustomizationRequests(modelBuilder);
        ConfigureQuotations(modelBuilder);
        ConfigureQuotationItems(modelBuilder);
        ConfigureOrders(modelBuilder);
        ConfigureOrderItems(modelBuilder);
        ConfigurePayments(modelBuilder);
        ConfigurePaymentTransactions(modelBuilder);
        ConfigureProductionRequests(modelBuilder);
        ConfigureProductionItems(modelBuilder);
        ConfigureProjectReviews(modelBuilder);
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
            entity.Property(e => e.ProductCode).HasColumnName("product_code").HasColumnType(Varchar50ColumnType);
            entity.Property(e => e.ProductName).HasColumnName("product_name").HasColumnType(Varchar150ColumnType).IsRequired();
            entity.Property(e => e.Description).HasColumnName(DescriptionColumnName).HasColumnType(TextColumnType);
            entity.Property(e => e.Status).HasColumnName(StatusColumnName).HasColumnType(ProductStatusColumnType).HasDefaultValueSql("'ACTIVE'::product_status");
            entity.Property(e => e.CreatedAt).HasColumnName(CreatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.UpdatedAt).HasColumnName(UpdatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.HasIndex(e => e.ProductCode).IsUnique();
            entity.HasOne<Category>().WithMany().HasForeignKey(e => e.CategoryId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureProductVersions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProductVersion>(entity =>
        {
            entity.ToTable("product_versions");
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
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at").HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.RejectedAt).HasColumnName(RejectedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.RejectionReason).HasColumnName("rejection_reason").HasColumnType(TextColumnType);
            entity.Property(e => e.CreatedAt).HasColumnName(CreatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.UpdatedAt).HasColumnName(UpdatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.HasIndex(e => e.ProjectCode).IsUnique();
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
            entity.HasOne<Project>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProjectArea>().WithMany().HasForeignKey(e => e.ParentAreaId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.CreatedBy).OnDelete(DeleteBehavior.Restrict);
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
            entity.Property(e => e.CancelledAt).HasColumnName("cancelled_at").HasColumnType(TimestampWithTimeZoneColumnType);
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
            entity.Property(e => e.CreatedAt).HasColumnName(CreatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.UpdatedAt).HasColumnName(UpdatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
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
            entity.Property(e => e.SceneId).HasColumnName("scene_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.ProposalId).HasColumnName(ProposalIdColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.ProjectAreaId).HasColumnName(ProjectAreaIdColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.SceneName).HasColumnName("scene_name").HasColumnType(Varchar150ColumnType);
            entity.Property(e => e.SceneType).HasColumnName("scene_type").HasColumnType(ProposalSceneTypeColumnType).HasDefaultValueSql("'THREE_D'::proposal_scene_type");
            entity.Property(e => e.MongoSceneId).HasColumnName("mongo_scene_id").HasColumnType(Varchar100ColumnType);
            entity.Property(e => e.PreviewFileId).HasColumnName("preview_file_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.VersionNo).HasColumnName(VersionNoColumnName).HasColumnType(IntegerColumnType).HasDefaultValue(1);
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasColumnType(BooleanColumnType).HasDefaultValue(true);
            entity.Property(e => e.CreatedBy).HasColumnName(CreatedByColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.CreatedAt).HasColumnName(CreatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.UpdatedAt).HasColumnName(UpdatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.HasOne<Proposal>().WithMany().HasForeignKey(e => e.ProposalId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProjectArea>().WithMany().HasForeignKey(e => e.ProjectAreaId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<StoredFile>().WithMany().HasForeignKey(e => e.PreviewFileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.CreatedBy).OnDelete(DeleteBehavior.Restrict);
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
            entity.Property(e => e.SceneId).HasColumnName("scene_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.SceneObjectId).HasColumnName("scene_object_id").HasColumnType("character varying(100)");
            entity.Property(e => e.ProjectAreaId).HasColumnName(ProjectAreaIdColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.ProductVersionId).HasColumnName(ProductVersionIdColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.ApprovedProductVersionId).HasColumnName("approved_product_version_id").HasColumnType(UuidColumnType);
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
            entity.HasOne<ProductVersion>().WithMany().HasForeignKey(e => e.ApprovedProductVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.SceneId, e.SceneObjectId }).HasDatabaseName("idx_proposal_items_scene_object");
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
            entity.Property(e => e.SceneId).HasColumnName("scene_id").HasColumnType(UuidColumnType).IsRequired();
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
            entity.ToTable("customization_requests", table =>
            {
                table.HasCheckConstraint(
                    "ck_customization_requests_additional_cost_reason",
                    "estimated_additional_cost IS NULL OR estimated_additional_cost <= 0 OR additional_cost_reason IS NOT NULL AND btrim(additional_cost_reason) <> ''");
            });
            entity.HasKey(e => e.CustomizationRequestId);
            entity.Property(e => e.CustomizationRequestId).HasColumnName("customization_request_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.ProjectId).HasColumnName(ProjectIdColumnName).HasColumnType(UuidColumnType).IsRequired();
            entity.Property(e => e.ProposalId).HasColumnName(ProposalIdColumnName).HasColumnType(UuidColumnType).IsRequired();
            entity.Property(e => e.ProposalItemId).HasColumnName(ProposalItemIdColumnName).HasColumnType(UuidColumnType).IsRequired();
            entity.Property(e => e.RequestedByCustomerId).HasColumnName("requested_by_customer_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.RequestTitle).HasColumnName("request_title").HasColumnType(Varchar150ColumnType).IsRequired();
            entity.Property(e => e.RequestDescription).HasColumnName("request_description").HasColumnType(TextColumnType);
            entity.Property(e => e.RequestedWidth).HasColumnName("requested_width").HasColumnType(Decimal10ColumnType);
            entity.Property(e => e.RequestedHeight).HasColumnName("requested_height").HasColumnType(Decimal10ColumnType);
            entity.Property(e => e.RequestedDepth).HasColumnName("requested_depth").HasColumnType(Decimal10ColumnType);
            entity.Property(e => e.RequestedMaterial).HasColumnName("requested_material").HasColumnType(Varchar100ColumnType);
            entity.Property(e => e.RequestedColor).HasColumnName("requested_color").HasColumnType(Varchar100ColumnType);
            entity.Property(e => e.RequestedChangeNote).HasColumnName("requested_change_note").HasColumnType(TextColumnType);
            entity.Property(e => e.DesignerId).HasColumnName("designer_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.DesignerSpecNote).HasColumnName("designer_spec_note").HasColumnType(TextColumnType);
            entity.Property(e => e.ProductionReviewBy).HasColumnName("production_review_by").HasColumnType(UuidColumnType);
            entity.Property(e => e.FeasibilityNote).HasColumnName("feasibility_note").HasColumnType(TextColumnType);
            entity.Property(e => e.EstimatedProductionDays).HasColumnName("estimated_production_days").HasColumnType(IntegerColumnType);
            entity.Property(e => e.EstimatedAdditionalCost).HasColumnName("estimated_additional_cost").HasColumnType(Decimal12ColumnType);
            entity.Property(e => e.AdditionalCostReason).HasColumnName("additional_cost_reason").HasColumnType(TextColumnType);
            entity.Property(e => e.MaterialAvailable).HasColumnName("material_available").HasColumnType(BooleanColumnType);
            entity.Property(e => e.ProductionRiskNote).HasColumnName("production_risk_note").HasColumnType(TextColumnType);
            entity.Property(e => e.SalesReviewBy).HasColumnName("sales_review_by").HasColumnType(UuidColumnType);
            entity.Property(e => e.ApprovedProductVersionId).HasColumnName("approved_product_version_id").HasColumnType(UuidColumnType);
            entity.Property(e => e.Status).HasColumnName(StatusColumnName).HasColumnType(CustomizationStatusColumnType).HasDefaultValueSql("'SUBMITTED'::customization_status");
            entity.Property(e => e.CustomerAcceptedAt).HasColumnName("customer_accepted_at").HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.CustomerRejectedAt).HasColumnName("customer_rejected_at").HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.CreatedAt).HasColumnName(CreatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.UpdatedAt).HasColumnName(UpdatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.HasOne<Project>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Proposal>().WithMany().HasForeignKey(e => e.ProposalId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProposalItem>().WithMany().HasForeignKey(e => e.ProposalItemId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.RequestedByCustomerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.DesignerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.ProductionReviewBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.SalesReviewBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProductVersion>().WithMany().HasForeignKey(e => e.ApprovedProductVersionId).OnDelete(DeleteBehavior.Restrict);
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
            entity.Property(e => e.SubtotalAmount).HasColumnName(SubtotalAmountColumnName).HasColumnType(Decimal12ColumnType).HasDefaultValue(0m);
            entity.Property(e => e.DiscountAmount).HasColumnName(DiscountAmountColumnName).HasColumnType(Decimal12ColumnType).HasDefaultValue(0m);
            entity.Property(e => e.TaxAmount).HasColumnName("tax_amount").HasColumnType(Decimal12ColumnType).HasDefaultValue(0m);
            entity.Property(e => e.TotalAmount).HasColumnName("total_amount").HasColumnType(Decimal12ColumnType).HasDefaultValue(0m);
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
            entity.Property(e => e.ItemType).HasColumnName("item_type").HasColumnType(QuotationItemTypeColumnType).HasDefaultValueSql("'PRODUCT_ITEM'::quotation_item_type");
            entity.Property(e => e.ProposalItemId).HasColumnName(ProposalItemIdColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.ProductVersionId).HasColumnName(ProductVersionIdColumnName).HasColumnType(UuidColumnType);
            entity.Property(e => e.ProductNameSnapshot).HasColumnName(ProductNameSnapshotColumnName).HasColumnType(Varchar150ColumnType);
            entity.Property(e => e.ProductVersionNameSnapshot).HasColumnName(ProductVersionNameSnapshotColumnName).HasColumnType(Varchar150ColumnType);
            entity.Property(e => e.ProductVersionCodeSnapshot).HasColumnName("product_version_code_snapshot").HasColumnType(Varchar50ColumnType);
            entity.Property(e => e.ItemName).HasColumnName("item_name").HasColumnType(Varchar150ColumnType);
            entity.Property(e => e.Description).HasColumnName(DescriptionColumnName).HasColumnType(TextColumnType);
            entity.Property(e => e.Quantity).HasColumnName(QuantityColumnName).HasColumnType(IntegerColumnType).HasDefaultValue(1);
            entity.Property(e => e.UnitPrice).HasColumnName("unit_price").HasColumnType(Decimal12ColumnType).HasDefaultValue(0m);
            entity.Property(e => e.CustomizationAdditionalCost).HasColumnName("customization_additional_cost").HasColumnType(Decimal12ColumnType).HasDefaultValue(0m);
            entity.Property(e => e.DiscountAmount).HasColumnName(DiscountAmountColumnName).HasColumnType(Decimal12ColumnType).HasDefaultValue(0m);
            entity.Property(e => e.SubtotalAmount).HasColumnName(SubtotalAmountColumnName).HasColumnType(Decimal12ColumnType).HasDefaultValue(0m);
            entity.Property(e => e.IsCustomized).HasColumnName("is_customized").HasColumnType(BooleanColumnType).HasDefaultValue(false);
            entity.Property(e => e.CustomizationNote).HasColumnName("customization_note").HasColumnType(TextColumnType);
            entity.Property(e => e.Note).HasColumnName(NoteColumnName).HasColumnType(TextColumnType);
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
            entity.Property(e => e.OriginalTotalAmount).HasColumnName("original_total_amount").HasColumnType(Decimal12ColumnType);
            entity.Property(e => e.ItemAdjustmentAmount).HasColumnName("item_adjustment_amount").HasColumnType(Decimal12ColumnType).HasDefaultValue(0m);
            entity.Property(e => e.AdditionalDiscountAmount).HasColumnName("additional_discount_amount").HasColumnType(Decimal12ColumnType).HasDefaultValue(0m);
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
            entity.Property(e => e.CancelledAt).HasColumnName("cancelled_at").HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.CancellationReason).HasColumnName("cancellation_reason").HasColumnType(TextColumnType);
            entity.Property(e => e.CreatedAt).HasColumnName(CreatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.UpdatedAt).HasColumnName(UpdatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.HasIndex(e => e.QuotationId).IsUnique();
            entity.HasIndex(e => e.OrderCode).IsUnique();
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
            entity.Property(e => e.Quantity).HasColumnName(QuantityColumnName).HasColumnType(IntegerColumnType).HasDefaultValue(1);
            entity.Property(e => e.DeliveredQuantity).HasColumnName("delivered_quantity").HasColumnType(IntegerColumnType).HasDefaultValue(0);
            entity.Property(e => e.Status).HasColumnName(StatusColumnName).HasColumnType(OrderItemStatusColumnType).HasDefaultValueSql("'PENDING'::order_item_status");
            entity.Property(e => e.UnitPrice).HasColumnName("unit_price").HasColumnType(Decimal12ColumnType).HasDefaultValue(0m);
            entity.Property(e => e.CustomizationFee).HasColumnName(CustomizationFeeColumnName).HasColumnType(Decimal12ColumnType).HasDefaultValue(0m);
            entity.Property(e => e.DiscountAmount).HasColumnName(DiscountAmountColumnName).HasColumnType(Decimal12ColumnType).HasDefaultValue(0m);
            entity.Property(e => e.SubtotalAmount).HasColumnName(SubtotalAmountColumnName).HasColumnType(Decimal12ColumnType).HasDefaultValue(0m);
            entity.Property(e => e.AdjustmentAmount).HasColumnName("adjustment_amount").HasColumnType(Decimal12ColumnType).HasDefaultValue(0m);
            entity.Property(e => e.UnavailableReason).HasColumnName("unavailable_reason").HasColumnType(TextColumnType);
            entity.Property(e => e.ProductionNote).HasColumnName("production_note").HasColumnType(TextColumnType);
            entity.Property(e => e.DeliveryNote).HasColumnName("delivery_note").HasColumnType(TextColumnType);
            entity.Property(e => e.LastDeliveredAt).HasColumnName("last_delivered_at").HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.LastDeliveredBy).HasColumnName("last_delivered_by").HasColumnType(UuidColumnType);
            entity.Property(e => e.CustomerConfirmedAt).HasColumnName("customer_confirmed_at").HasColumnType(TimestampWithTimeZoneColumnType);
            entity.HasOne<Order>().WithMany().HasForeignKey(e => e.OrderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<QuotationItem>().WithMany().HasForeignKey(e => e.QuotationItemId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProductVersion>().WithMany().HasForeignKey(e => e.ProductVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.LastDeliveredBy).OnDelete(DeleteBehavior.Restrict);
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
            entity.Property(e => e.PaidAmount).HasColumnName("paid_amount").HasColumnType(Decimal12ColumnType).HasDefaultValue(0m).IsRequired();
            entity.Property(e => e.RemainingAmount).HasColumnName("remaining_amount").HasColumnType(Decimal12ColumnType).HasDefaultValue(0m).IsRequired();
            entity.Property(e => e.Currency).HasColumnName("currency").HasColumnType(Varchar10ColumnType).HasDefaultValue("VND").IsRequired();
            entity.Property(e => e.Status).HasColumnName(StatusColumnName).HasColumnType(PaymentStatusColumnType).HasDefaultValueSql("'PENDING'::payment_status");
            entity.Property(e => e.ExpiredAt).HasColumnName("expired_at").HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.PaidAt).HasColumnName("paid_at").HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.CancelledAt).HasColumnName("cancelled_at").HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.Note).HasColumnName(NoteColumnName).HasColumnType(TextColumnType);
            entity.Property(e => e.CreatedAt).HasColumnName(CreatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.UpdatedAt).HasColumnName(UpdatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.HasOne<Project>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Order>().WithMany().HasForeignKey(e => e.OrderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Quotation>().WithMany().HasForeignKey(e => e.QuotationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.PaidBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.PaymentCode).IsUnique().HasDatabaseName("uq_payments_payment_code");
            entity.HasIndex(e => new { e.ProjectId, e.CreatedAt }).HasDatabaseName("idx_payments_project_time");
            entity.HasIndex(e => new { e.OrderId, e.PaymentType }).HasDatabaseName("idx_payments_order_type");
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
            entity.Property(e => e.Status).HasColumnName(StatusColumnName).HasColumnType(ProductionRequestStatusColumnType).HasDefaultValueSql("'PENDING_REVIEW'::production_request_status");
            entity.Property(e => e.Priority).HasColumnName("priority").HasColumnType(Varchar30ColumnType);
            entity.Property(e => e.EstimatedStartDate).HasColumnName("estimated_start_date").HasColumnType(DateColumnType);
            entity.Property(e => e.EstimatedCompletionDate).HasColumnName("estimated_completion_date").HasColumnType(DateColumnType);
            entity.Property(e => e.ActualStartDate).HasColumnName("actual_start_date").HasColumnType(DateColumnType);
            entity.Property(e => e.ActualCompletionDate).HasColumnName("actual_completion_date").HasColumnType(DateColumnType);
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
            entity.Property(e => e.TransactionTime).HasColumnName("transaction_time").HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.ConfirmedBy).HasColumnName("confirmed_by").HasColumnType(UuidColumnType);
            entity.Property(e => e.ConfirmedAt).HasColumnName("confirmed_at").HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.FailureReason).HasColumnName("failure_reason").HasColumnType(TextColumnType);
            entity.Property(e => e.RawProviderPayload).HasColumnName("raw_provider_payload").HasColumnType(JsonbColumnType);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType(TimestampWithTimeZoneColumnType).IsRequired();
            entity.HasIndex(e => e.TransactionCode).IsUnique();
            entity.HasIndex(e => e.PaymentId).HasDatabaseName("idx_payment_transactions_payment_id");
            entity.HasIndex(e => new { e.ProjectId, e.CreatedAt }).HasDatabaseName("idx_payment_transactions_project_time");
            entity.HasIndex(e => new { e.OrderId, e.CreatedAt }).HasDatabaseName("idx_payment_transactions_order_time");
            entity.HasIndex(e => e.ProviderTransactionId).HasDatabaseName("idx_payment_transactions_provider_transaction_id");
            entity.HasIndex(e => e.ProviderReferenceCode).HasDatabaseName("idx_payment_transactions_provider_reference_code");
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
            entity.Property(e => e.Status).HasColumnName(StatusColumnName).HasColumnType(ProductionItemStatusColumnType).HasDefaultValueSql("'PENDING'::production_item_status");
            entity.Property(e => e.MaterialNote).HasColumnName("material_note").HasColumnType(TextColumnType);
            entity.Property(e => e.ProductionNote).HasColumnName("production_note").HasColumnType(TextColumnType);
            entity.Property(e => e.EstimatedCompletionDate).HasColumnName("estimated_completion_date").HasColumnType(DateColumnType);
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at").HasColumnType(TimestampWithTimeZoneColumnType);
            entity.HasOne<ProductionRequest>().WithMany().HasForeignKey(e => e.ProductionRequestId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<OrderItem>().WithMany().HasForeignKey(e => e.OrderItemId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProductVersion>().WithMany().HasForeignKey(e => e.ProductVersionId).OnDelete(DeleteBehavior.Restrict);
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
            entity.Property(e => e.CreatedAt).HasColumnName(CreatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.Property(e => e.UpdatedAt).HasColumnName(UpdatedAtColumnName).HasColumnType(TimestampWithTimeZoneColumnType);
            entity.HasIndex(e => e.ProjectId).IsUnique();
            entity.HasOne<Project>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Order>().WithMany().HasForeignKey(e => e.OrderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.CustomerId).OnDelete(DeleteBehavior.Restrict);
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
