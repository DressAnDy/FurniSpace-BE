using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurniSpace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialFurniSpaceSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:account_status", "ACTIVE,INACTIVE,SUSPENDED")
                .Annotation("Npgsql:Enum:customization_feasibility_status", "PENDING,FEASIBLE,NOT_FEASIBLE,NEED_REVISION")
                .Annotation("Npgsql:Enum:customization_status", "SUBMITTED,DESIGN_REVIEWING,PRODUCTION_REVIEWING,WAITING_FOR_CUSTOMER_APPROVAL,ACCEPTED,REJECTED_BY_CUSTOMER,NOT_FEASIBLE,CANCELLED,CONVERTED_TO_VERSION")
                .Annotation("Npgsql:Enum:delivery_item_status", "PENDING,LOADED,DELIVERED,FAILED,CANCELLED")
                .Annotation("Npgsql:Enum:delivery_status", "PENDING_SCHEDULE,SCHEDULED,DELIVERING,DELIVERED,FAILED,RESCHEDULED,CANCELLED")
                .Annotation("Npgsql:Enum:file_type", "SPACE_IMAGE,FLOOR_PLAN,REFERENCE_IMAGE,BRAND_ASSET,CAD_FILE,PDF_DRAWING,MEASUREMENT_REPORT,LIDAR_SCAN,MODEL_3D,TEXTURE,PRODUCT_PREVIEW,PROPOSAL_PREVIEW,PROPOSAL_FILE,QUOTATION_FILE,ORDER_DOCUMENT,PRODUCTION_FILE,DELIVERY_PHOTO,DELIVERY_NOTE,REVIEW_IMAGE,OTHER")
                .Annotation("Npgsql:Enum:file_visibility", "CUSTOMER_VISIBLE,INTERNAL,STAFF_ONLY,PRIVATE")
                .Annotation("Npgsql:Enum:measurement_appointment_status", "PENDING_CONFIRMATION,CONFIRMED,RESCHEDULED,COMPLETED,CANCELLED,NO_SHOW")
                .Annotation("Npgsql:Enum:notification_status", "UNREAD,READ")
                .Annotation("Npgsql:Enum:order_status", "CREATED,CONFIRMED,PENDING_PAYMENT,IN_PRODUCTION,READY_FOR_DELIVERY,DELIVERING,DELIVERED,COMPLETED,CANCELLED")
                .Annotation("Npgsql:Enum:payment_status", "PENDING,PAID,FAILED,REFUNDED,CANCELLED")
                .Annotation("Npgsql:Enum:payment_type", "DEPOSIT,REMAINING_PAYMENT,FULL_PAYMENT,MEASUREMENT_FEE,DESIGN_FEE,CUSTOMIZATION_FEE,DELIVERY_FEE,CANCELLATION_FEE,REFUND,OTHER")
                .Annotation("Npgsql:Enum:product_status", "ACTIVE,INACTIVE,ARCHIVED")
                .Annotation("Npgsql:Enum:product_version_status", "ACTIVE,INACTIVE,ARCHIVED")
                .Annotation("Npgsql:Enum:product_version_type", "STANDARD,CUSTOM,PROJECT_SPECIFIC")
                .Annotation("Npgsql:Enum:production_item_status", "PENDING,IN_PRODUCTION,COMPLETED,BLOCKED,CANCELLED")
                .Annotation("Npgsql:Enum:production_request_status", "PENDING_REVIEW,FEASIBLE,IN_PRODUCTION,COMPLETED,BLOCKED,CANCELLED")
                .Annotation("Npgsql:Enum:project_area_status", "DRAFT,NEED_MEASUREMENT,MEASURED,VERIFIED,DESIGNING,DESIGNED,APPROVED,CANCELLED")
                .Annotation("Npgsql:Enum:project_area_type", "STORE,FLOOR,ROOM,ZONE,OUTDOOR_AREA,OTHER")
                .Annotation("Npgsql:Enum:project_assignment_role", "SALES_CONSULTANT,DESIGNER_STAFF,PRODUCTION_STAFF,DELIVERY_STAFF,ADMIN_SUPPORT")
                .Annotation("Npgsql:Enum:project_assignment_status", "ACTIVE,INACTIVE,REASSIGNED,CANCELLED")
                .Annotation("Npgsql:Enum:project_chat_message_type", "TEXT,FILE,SYSTEM")
                .Annotation("Npgsql:Enum:project_chat_status", "OPEN,CLOSED,ARCHIVED")
                .Annotation("Npgsql:Enum:project_chat_type", "SALES,DESIGNER,PRODUCTION,DELIVERY,GENERAL,INTERNAL")
                .Annotation("Npgsql:Enum:project_status", "DRAFT,SUBMITTED,SALES_ASSIGNED,IN_CONSULTATION,NEED_BASIC_INFORMATION,WAITING_FOR_DESIGNER_ASSIGNMENT,SPACE_INPUT_REVIEW,MEASUREMENT_REQUIRED,WAITING_FOR_SCHEDULE_CONFIRMATION,MEASUREMENT_SCHEDULED,MEASUREMENT_COMPLETED,SPACE_VERIFICATION_REQUIRED,SPACE_VERIFIED,LAYOUT_DESIGNING,PROPOSAL_DRAFTING,WAITING_FOR_CUSTOMER_REVIEW,REVISION_REQUESTED,PROPOSAL_SELECTED,QUOTATION_PREPARING,QUOTATION_SENT,QUOTATION_REVISION_REQUESTED,QUOTATION_ACCEPTED,ORDER_CONFIRMED,IN_PRODUCTION,PRODUCTION_BLOCKED,READY_FOR_DELIVERY,DELIVERY_SCHEDULED,DELIVERING,DELIVERED,COMPLETED,REJECTED,CANCELLED")
                .Annotation("Npgsql:Enum:proposal_scene_type", "TWO_D,THREE_D")
                .Annotation("Npgsql:Enum:proposal_status", "DRAFT,PUBLISHED,VIEWED,SELECTED,REVISION_REQUESTED,REJECTED,ARCHIVED")
                .Annotation("Npgsql:Enum:quotation_status", "DRAFT,SENT,REVISION_REQUESTED,REVISED,ACCEPTED,REJECTED,EXPIRED,CANCELLED");

            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_name = table.Column<string>(type: "varchar(100)", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "product_status", nullable: true, defaultValue: "ACTIVE"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categories", x => x.category_id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_name = table.Column<string>(type: "varchar(50)", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.role_id);
                });

            migrationBuilder.CreateTable(
                name: "accounts",
                columns: table => new
                {
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "varchar(100)", nullable: false),
                    password_hash = table.Column<string>(type: "varchar(255)", nullable: false),
                    full_name = table.Column<string>(type: "varchar(100)", nullable: false),
                    phone = table.Column<string>(type: "varchar(20)", nullable: true),
                    avatar_url = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "account_status", nullable: true, defaultValue: "ACTIVE"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts", x => x.account_id);
                    table.ForeignKey(
                        name: "FK_accounts_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "role_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "files",
                columns: table => new
                {
                    file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "varchar(255)", nullable: false),
                    file_url = table.Column<string>(type: "text", nullable: false),
                    mime_type = table.Column<string>(type: "varchar(100)", nullable: true),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: true),
                    uploaded_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_files", x => x.file_id);
                    table.ForeignKey(
                        name: "FK_files_accounts_uploaded_by",
                        column: x => x.uploaded_by,
                        principalTable: "accounts",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    product_code = table.Column<string>(type: "varchar(50)", nullable: true),
                    product_name = table.Column<string>(type: "varchar(150)", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "product_status", nullable: true, defaultValue: "ACTIVE"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.product_id);
                    table.ForeignKey(
                        name: "FK_products_accounts_created_by",
                        column: x => x.created_by,
                        principalTable: "accounts",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_products_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "category_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "projects",
                columns: table => new
                {
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_sales_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assigned_designer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    project_code = table.Column<string>(type: "varchar(50)", nullable: true),
                    project_name = table.Column<string>(type: "varchar(150)", nullable: false),
                    business_type = table.Column<string>(type: "varchar(100)", nullable: true),
                    project_address = table.Column<string>(type: "text", nullable: true),
                    business_purpose = table.Column<string>(type: "text", nullable: true),
                    preferred_style = table.Column<string>(type: "varchar(100)", nullable: true),
                    furniture_requirement = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    total_area_sqm = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    number_of_floors = table.Column<int>(type: "integer", nullable: true),
                    budget_min = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    budget_max = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    expected_start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    expected_completion_date = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "project_status", nullable: true, defaultValue: "DRAFT"),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    sales_assigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    designer_assigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancelled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejection_reason = table.Column<string>(type: "text", nullable: true),
                    cancellation_reason = table.Column<string>(type: "text", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projects", x => x.project_id);
                    table.ForeignKey(
                        name: "FK_projects_accounts_assigned_designer_id",
                        column: x => x.assigned_designer_id,
                        principalTable: "accounts",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_projects_accounts_assigned_sales_id",
                        column: x => x.assigned_sales_id,
                        principalTable: "accounts",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_projects_accounts_created_by",
                        column: x => x.created_by,
                        principalTable: "accounts",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_projects_accounts_customer_id",
                        column: x => x.customer_id,
                        principalTable: "accounts",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "file_links",
                columns: table => new
                {
                    file_link_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_type = table.Column<string>(type: "varchar(50)", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_type = table.Column<string>(type: "file_type", nullable: true, defaultValue: "OTHER"),
                    visibility = table.Column<string>(type: "file_visibility", nullable: true, defaultValue: "INTERNAL"),
                    description = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_file_links", x => x.file_link_id);
                    table.ForeignKey(
                        name: "FK_file_links_files_file_id",
                        column: x => x.file_id,
                        principalTable: "files",
                        principalColumn: "file_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    notification_id = table.Column<Guid>(type: "uuid", nullable: false),
                    receiver_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(type: "varchar(150)", nullable: false),
                    message = table.Column<string>(type: "text", nullable: true),
                    notification_type = table.Column<string>(type: "varchar(50)", nullable: true),
                    status = table.Column<string>(type: "notification_status", nullable: true, defaultValue: "UNREAD"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    read_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.notification_id);
                    table.ForeignKey(
                        name: "FK_notifications_accounts_receiver_id",
                        column: x => x.receiver_id,
                        principalTable: "accounts",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_notifications_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "project_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "product_versions",
                columns: table => new
                {
                    product_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    version_code = table.Column<string>(type: "varchar(50)", nullable: true),
                    version_name = table.Column<string>(type: "varchar(150)", nullable: false),
                    version_type = table.Column<string>(type: "product_version_type", nullable: true, defaultValue: "STANDARD"),
                    material = table.Column<string>(type: "varchar(100)", nullable: true),
                    color = table.Column<string>(type: "varchar(100)", nullable: true),
                    finish = table.Column<string>(type: "varchar(100)", nullable: true),
                    width = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    height = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    depth = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    estimated_price = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    production_note = table.Column<string>(type: "text", nullable: true),
                    technical_note = table.Column<string>(type: "text", nullable: true),
                    is_default = table.Column<bool>(type: "boolean", nullable: true, defaultValue: false),
                    is_public = table.Column<bool>(type: "boolean", nullable: true, defaultValue: true),
                    is_project_specific = table.Column<bool>(type: "boolean", nullable: true, defaultValue: false),
                    status = table.Column<string>(type: "product_version_status", nullable: true, defaultValue: "ACTIVE"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_versions", x => x.product_version_id);
                    table.ForeignKey(
                        name: "FK_product_versions_accounts_created_by",
                        column: x => x.created_by,
                        principalTable: "accounts",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_versions_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "product_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_versions_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "project_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "project_areas",
                columns: table => new
                {
                    project_area_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_area_id = table.Column<Guid>(type: "uuid", nullable: true),
                    area_name = table.Column<string>(type: "varchar(150)", nullable: false),
                    area_type = table.Column<string>(type: "project_area_type", nullable: true, defaultValue: "ZONE"),
                    floor_number = table.Column<int>(type: "integer", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    area_sqm = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    width = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    length = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    height = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    current_condition = table.Column<string>(type: "text", nullable: true),
                    requirement_note = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "project_area_status", nullable: true, defaultValue: "DRAFT"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_areas", x => x.project_area_id);
                    table.ForeignKey(
                        name: "FK_project_areas_accounts_created_by",
                        column: x => x.created_by,
                        principalTable: "accounts",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_areas_project_areas_parent_area_id",
                        column: x => x.parent_area_id,
                        principalTable: "project_areas",
                        principalColumn: "project_area_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_areas_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "project_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "project_assignments",
                columns: table => new
                {
                    assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assignment_role = table.Column<string>(type: "project_assignment_role", nullable: false),
                    status = table.Column<string>(type: "project_assignment_status", nullable: true, defaultValue: "ACTIVE"),
                    assigned_by = table.Column<Guid>(type: "uuid", nullable: true),
                    assigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    unassigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_assignments", x => x.assignment_id);
                    table.ForeignKey(
                        name: "FK_project_assignments_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_assignments_accounts_assigned_by",
                        column: x => x.assigned_by,
                        principalTable: "accounts",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_assignments_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "project_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "project_chats",
                columns: table => new
                {
                    chat_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chat_type = table.Column<string>(type: "project_chat_type", nullable: false),
                    staff_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(type: "varchar(150)", nullable: true),
                    status = table.Column<string>(type: "project_chat_status", nullable: true, defaultValue: "OPEN"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    closed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_chats", x => x.chat_id);
                    table.ForeignKey(
                        name: "FK_project_chats_accounts_staff_id",
                        column: x => x.staff_id,
                        principalTable: "accounts",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_chats_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "project_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "proposals",
                columns: table => new
                {
                    proposal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_proposal_id = table.Column<Guid>(type: "uuid", nullable: true),
                    proposal_name = table.Column<string>(type: "varchar(150)", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    design_concept = table.Column<string>(type: "text", nullable: true),
                    version_no = table.Column<int>(type: "integer", nullable: true, defaultValue: 1),
                    estimated_price = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    status = table.Column<string>(type: "proposal_status", nullable: true, defaultValue: "DRAFT"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    selected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_proposals", x => x.proposal_id);
                    table.ForeignKey(
                        name: "FK_proposals_accounts_created_by",
                        column: x => x.created_by,
                        principalTable: "accounts",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_proposals_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "project_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_proposals_proposals_parent_proposal_id",
                        column: x => x.parent_proposal_id,
                        principalTable: "proposals",
                        principalColumn: "proposal_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "measurement_appointments",
                columns: table => new
                {
                    appointment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_area_id = table.Column<Guid>(type: "uuid", nullable: true),
                    scheduled_by_sales_id = table.Column<Guid>(type: "uuid", nullable: true),
                    designer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    appointment_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    appointment_address = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "measurement_appointment_status", nullable: true, defaultValue: "PENDING_CONFIRMATION"),
                    customer_note = table.Column<string>(type: "text", nullable: true),
                    internal_note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancelled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_measurement_appointments", x => x.appointment_id);
                    table.ForeignKey(
                        name: "FK_measurement_appointments_accounts_designer_id",
                        column: x => x.designer_id,
                        principalTable: "accounts",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_measurement_appointments_accounts_scheduled_by_sales_id",
                        column: x => x.scheduled_by_sales_id,
                        principalTable: "accounts",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_measurement_appointments_project_areas_project_area_id",
                        column: x => x.project_area_id,
                        principalTable: "project_areas",
                        principalColumn: "project_area_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_measurement_appointments_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "project_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "project_chat_messages",
                columns: table => new
                {
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chat_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sender_id = table.Column<Guid>(type: "uuid", nullable: true),
                    message_type = table.Column<string>(type: "project_chat_message_type", nullable: true, defaultValue: "TEXT"),
                    content = table.Column<string>(type: "text", nullable: true),
                    attachment_file_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    edited_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    read_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_chat_messages", x => x.message_id);
                    table.ForeignKey(
                        name: "FK_project_chat_messages_accounts_sender_id",
                        column: x => x.sender_id,
                        principalTable: "accounts",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_chat_messages_files_attachment_file_id",
                        column: x => x.attachment_file_id,
                        principalTable: "files",
                        principalColumn: "file_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_chat_messages_project_chats_chat_id",
                        column: x => x.chat_id,
                        principalTable: "project_chats",
                        principalColumn: "chat_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "proposal_scenes",
                columns: table => new
                {
                    scene_id = table.Column<Guid>(type: "uuid", nullable: false),
                    proposal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_area_id = table.Column<Guid>(type: "uuid", nullable: true),
                    scene_name = table.Column<string>(type: "varchar(150)", nullable: true),
                    scene_type = table.Column<string>(type: "proposal_scene_type", nullable: true, defaultValue: "THREE_D"),
                    mongo_scene_id = table.Column<string>(type: "varchar(100)", nullable: true),
                    preview_file_id = table.Column<Guid>(type: "uuid", nullable: true),
                    version_no = table.Column<int>(type: "integer", nullable: true, defaultValue: 1),
                    is_active = table.Column<bool>(type: "boolean", nullable: true, defaultValue: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_proposal_scenes", x => x.scene_id);
                    table.ForeignKey(
                        name: "FK_proposal_scenes_accounts_created_by",
                        column: x => x.created_by,
                        principalTable: "accounts",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_proposal_scenes_files_preview_file_id",
                        column: x => x.preview_file_id,
                        principalTable: "files",
                        principalColumn: "file_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_proposal_scenes_project_areas_project_area_id",
                        column: x => x.project_area_id,
                        principalTable: "project_areas",
                        principalColumn: "project_area_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_proposal_scenes_proposals_proposal_id",
                        column: x => x.proposal_id,
                        principalTable: "proposals",
                        principalColumn: "proposal_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "quotations",
                columns: table => new
                {
                    quotation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    proposal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quotation_code = table.Column<string>(type: "varchar(50)", nullable: false),
                    version_no = table.Column<int>(type: "integer", nullable: true, defaultValue: 1),
                    subtotal_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: true, defaultValue: 0m),
                    discount_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: true, defaultValue: 0m),
                    tax_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: true, defaultValue: 0m),
                    service_fee = table.Column<decimal>(type: "numeric(12,2)", nullable: true, defaultValue: 0m),
                    customization_fee = table.Column<decimal>(type: "numeric(12,2)", nullable: true, defaultValue: 0m),
                    delivery_fee = table.Column<decimal>(type: "numeric(12,2)", nullable: true, defaultValue: 0m),
                    total_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: true, defaultValue: 0m),
                    status = table.Column<string>(type: "quotation_status", nullable: true, defaultValue: "DRAFT"),
                    valid_until = table.Column<DateOnly>(type: "date", nullable: true),
                    customer_note = table.Column<string>(type: "text", nullable: true),
                    sales_note = table.Column<string>(type: "text", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    accepted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quotations", x => x.quotation_id);
                    table.ForeignKey(
                        name: "FK_quotations_accounts_created_by",
                        column: x => x.created_by,
                        principalTable: "accounts",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_quotations_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "project_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_quotations_proposals_proposal_id",
                        column: x => x.proposal_id,
                        principalTable: "proposals",
                        principalColumn: "proposal_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "proposal_items",
                columns: table => new
                {
                    proposal_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    proposal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scene_id = table.Column<Guid>(type: "uuid", nullable: true),
                    project_area_id = table.Column<Guid>(type: "uuid", nullable: true),
                    product_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    item_name = table.Column<string>(type: "varchar(150)", nullable: false),
                    item_type = table.Column<string>(type: "varchar(50)", nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: true, defaultValue: 1),
                    width = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    height = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    depth = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    material = table.Column<string>(type: "varchar(100)", nullable: true),
                    color = table.Column<string>(type: "varchar(100)", nullable: true),
                    finish = table.Column<string>(type: "varchar(100)", nullable: true),
                    unit_price_snapshot = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_proposal_items", x => x.proposal_item_id);
                    table.ForeignKey(
                        name: "FK_proposal_items_product_versions_product_version_id",
                        column: x => x.product_version_id,
                        principalTable: "product_versions",
                        principalColumn: "product_version_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_proposal_items_project_areas_project_area_id",
                        column: x => x.project_area_id,
                        principalTable: "project_areas",
                        principalColumn: "project_area_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_proposal_items_proposal_scenes_scene_id",
                        column: x => x.scene_id,
                        principalTable: "proposal_scenes",
                        principalColumn: "scene_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_proposal_items_proposals_proposal_id",
                        column: x => x.proposal_id,
                        principalTable: "proposals",
                        principalColumn: "proposal_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "orders",
                columns: table => new
                {
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    proposal_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quotation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_code = table.Column<string>(type: "varchar(50)", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sales_id = table.Column<Guid>(type: "uuid", nullable: true),
                    total_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    paid_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: true, defaultValue: 0m),
                    remaining_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: true, defaultValue: 0m),
                    status = table.Column<string>(type: "order_status", nullable: true, defaultValue: "CREATED"),
                    confirmed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    confirmed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancelled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancellation_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_orders", x => x.order_id);
                    table.ForeignKey(
                        name: "FK_orders_accounts_confirmed_by",
                        column: x => x.confirmed_by,
                        principalTable: "accounts",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_orders_accounts_customer_id",
                        column: x => x.customer_id,
                        principalTable: "accounts",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_orders_accounts_sales_id",
                        column: x => x.sales_id,
                        principalTable: "accounts",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_orders_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "project_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_orders_proposals_proposal_id",
                        column: x => x.proposal_id,
                        principalTable: "proposals",
                        principalColumn: "proposal_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_orders_quotations_quotation_id",
                        column: x => x.quotation_id,
                        principalTable: "quotations",
                        principalColumn: "quotation_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "customization_requests",
                columns: table => new
                {
                    customization_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    proposal_id = table.Column<Guid>(type: "uuid", nullable: true),
                    proposal_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    requested_by_customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    request_title = table.Column<string>(type: "varchar(150)", nullable: false),
                    request_description = table.Column<string>(type: "text", nullable: true),
                    requested_width = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    requested_height = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    requested_depth = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    requested_material = table.Column<string>(type: "varchar(100)", nullable: true),
                    requested_color = table.Column<string>(type: "varchar(100)", nullable: true),
                    requested_finish = table.Column<string>(type: "varchar(100)", nullable: true),
                    requested_change_note = table.Column<string>(type: "text", nullable: true),
                    designer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    designer_spec_note = table.Column<string>(type: "text", nullable: true),
                    production_review_by = table.Column<Guid>(type: "uuid", nullable: true),
                    feasibility_status = table.Column<string>(type: "customization_feasibility_status", nullable: true, defaultValue: "PENDING"),
                    feasibility_note = table.Column<string>(type: "text", nullable: true),
                    estimated_production_days = table.Column<int>(type: "integer", nullable: true),
                    estimated_additional_cost = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    material_available = table.Column<bool>(type: "boolean", nullable: true),
                    production_risk_note = table.Column<string>(type: "text", nullable: true),
                    sales_review_by = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_product_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "customization_status", nullable: true, defaultValue: "SUBMITTED"),
                    customer_accepted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    customer_rejected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customization_requests", x => x.customization_request_id);
                    table.ForeignKey(
                        name: "FK_customization_requests_accounts_designer_id",
                        column: x => x.designer_id,
                        principalTable: "accounts",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_customization_requests_accounts_production_review_by",
                        column: x => x.production_review_by,
                        principalTable: "accounts",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_customization_requests_accounts_requested_by_customer_id",
                        column: x => x.requested_by_customer_id,
                        principalTable: "accounts",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_customization_requests_accounts_sales_review_by",
                        column: x => x.sales_review_by,
                        principalTable: "accounts",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_customization_requests_product_versions_approved_product_ve~",
                        column: x => x.approved_product_version_id,
                        principalTable: "product_versions",
                        principalColumn: "product_version_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_customization_requests_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "project_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_customization_requests_proposal_items_proposal_item_id",
                        column: x => x.proposal_item_id,
                        principalTable: "proposal_items",
                        principalColumn: "proposal_item_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_customization_requests_proposals_proposal_id",
                        column: x => x.proposal_id,
                        principalTable: "proposals",
                        principalColumn: "proposal_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "quotation_items",
                columns: table => new
                {
                    quotation_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quotation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    proposal_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    product_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    product_name_snapshot = table.Column<string>(type: "varchar(150)", nullable: true),
                    product_version_name_snapshot = table.Column<string>(type: "varchar(150)", nullable: true),
                    product_version_code_snapshot = table.Column<string>(type: "varchar(50)", nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: true, defaultValue: 1),
                    unit_price = table.Column<decimal>(type: "numeric(12,2)", nullable: true, defaultValue: 0m),
                    customization_fee = table.Column<decimal>(type: "numeric(12,2)", nullable: true, defaultValue: 0m),
                    discount_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: true, defaultValue: 0m),
                    subtotal_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: true, defaultValue: 0m),
                    note = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quotation_items", x => x.quotation_item_id);
                    table.ForeignKey(
                        name: "FK_quotation_items_product_versions_product_version_id",
                        column: x => x.product_version_id,
                        principalTable: "product_versions",
                        principalColumn: "product_version_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_quotation_items_proposal_items_proposal_item_id",
                        column: x => x.proposal_item_id,
                        principalTable: "proposal_items",
                        principalColumn: "proposal_item_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_quotation_items_quotations_quotation_id",
                        column: x => x.quotation_id,
                        principalTable: "quotations",
                        principalColumn: "quotation_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "deliveries",
                columns: table => new
                {
                    delivery_id = table.Column<Guid>(type: "uuid", nullable: false),
                    delivery_code = table.Column<string>(type: "varchar(50)", nullable: true),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_delivery_staff_id = table.Column<Guid>(type: "uuid", nullable: true),
                    delivery_address = table.Column<string>(type: "text", nullable: true),
                    receiver_name = table.Column<string>(type: "varchar(150)", nullable: true),
                    receiver_phone = table.Column<string>(type: "varchar(20)", nullable: true),
                    scheduled_delivery_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "delivery_status", nullable: true, defaultValue: "PENDING_SCHEDULE"),
                    delivery_note = table.Column<string>(type: "text", nullable: true),
                    failed_reason = table.Column<string>(type: "text", nullable: true),
                    delivered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deliveries", x => x.delivery_id);
                    table.ForeignKey(
                        name: "FK_deliveries_accounts_assigned_delivery_staff_id",
                        column: x => x.assigned_delivery_staff_id,
                        principalTable: "accounts",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_deliveries_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "order_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_deliveries_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "project_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quotation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    paid_by = table.Column<Guid>(type: "uuid", nullable: true),
                    payment_type = table.Column<string>(type: "payment_type", nullable: true, defaultValue: "OTHER"),
                    amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    payment_method = table.Column<string>(type: "varchar(50)", nullable: true),
                    transaction_reference = table.Column<string>(type: "varchar(150)", nullable: true),
                    status = table.Column<string>(type: "payment_status", nullable: true, defaultValue: "PENDING"),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.payment_id);
                    table.ForeignKey(
                        name: "FK_payments_accounts_paid_by",
                        column: x => x.paid_by,
                        principalTable: "accounts",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payments_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "order_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payments_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "project_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payments_quotations_quotation_id",
                        column: x => x.quotation_id,
                        principalTable: "quotations",
                        principalColumn: "quotation_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "production_requests",
                columns: table => new
                {
                    production_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    production_code = table.Column<string>(type: "varchar(50)", nullable: true),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_to = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "production_request_status", nullable: true, defaultValue: "PENDING_REVIEW"),
                    priority = table.Column<string>(type: "varchar(30)", nullable: true),
                    estimated_start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    estimated_completion_date = table.Column<DateOnly>(type: "date", nullable: true),
                    actual_start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    actual_completion_date = table.Column<DateOnly>(type: "date", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_production_requests", x => x.production_request_id);
                    table.ForeignKey(
                        name: "FK_production_requests_accounts_assigned_to",
                        column: x => x.assigned_to,
                        principalTable: "accounts",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_production_requests_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "order_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_production_requests_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "project_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "project_reviews",
                columns: table => new
                {
                    review_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rating = table.Column<int>(type: "integer", nullable: true),
                    design_quality_rating = table.Column<int>(type: "integer", nullable: true),
                    service_quality_rating = table.Column<int>(type: "integer", nullable: true),
                    delivery_rating = table.Column<int>(type: "integer", nullable: true),
                    comment = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_reviews", x => x.review_id);
                    table.ForeignKey(
                        name: "FK_project_reviews_accounts_customer_id",
                        column: x => x.customer_id,
                        principalTable: "accounts",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_reviews_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "order_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_reviews_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "project_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "order_items",
                columns: table => new
                {
                    order_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quotation_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    product_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    product_name_snapshot = table.Column<string>(type: "varchar(150)", nullable: true),
                    product_version_name_snapshot = table.Column<string>(type: "varchar(150)", nullable: true),
                    product_version_code_snapshot = table.Column<string>(type: "varchar(50)", nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: true, defaultValue: 1),
                    unit_price = table.Column<decimal>(type: "numeric(12,2)", nullable: true, defaultValue: 0m),
                    customization_fee = table.Column<decimal>(type: "numeric(12,2)", nullable: true, defaultValue: 0m),
                    discount_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: true, defaultValue: 0m),
                    subtotal_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: true, defaultValue: 0m),
                    production_note = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_items", x => x.order_item_id);
                    table.ForeignKey(
                        name: "FK_order_items_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "order_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_order_items_product_versions_product_version_id",
                        column: x => x.product_version_id,
                        principalTable: "product_versions",
                        principalColumn: "product_version_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_order_items_quotation_items_quotation_item_id",
                        column: x => x.quotation_item_id,
                        principalTable: "quotation_items",
                        principalColumn: "quotation_item_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "delivery_items",
                columns: table => new
                {
                    delivery_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    delivery_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_area_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: true, defaultValue: 1),
                    status = table.Column<string>(type: "delivery_item_status", nullable: true, defaultValue: "PENDING"),
                    note = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delivery_items", x => x.delivery_item_id);
                    table.ForeignKey(
                        name: "FK_delivery_items_deliveries_delivery_id",
                        column: x => x.delivery_id,
                        principalTable: "deliveries",
                        principalColumn: "delivery_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_delivery_items_order_items_order_item_id",
                        column: x => x.order_item_id,
                        principalTable: "order_items",
                        principalColumn: "order_item_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_delivery_items_project_areas_project_area_id",
                        column: x => x.project_area_id,
                        principalTable: "project_areas",
                        principalColumn: "project_area_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "production_items",
                columns: table => new
                {
                    production_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    production_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    product_name_snapshot = table.Column<string>(type: "varchar(150)", nullable: true),
                    product_version_name_snapshot = table.Column<string>(type: "varchar(150)", nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: true, defaultValue: 1),
                    status = table.Column<string>(type: "production_item_status", nullable: true, defaultValue: "PENDING"),
                    material_note = table.Column<string>(type: "text", nullable: true),
                    production_note = table.Column<string>(type: "text", nullable: true),
                    estimated_completion_date = table.Column<DateOnly>(type: "date", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_production_items", x => x.production_item_id);
                    table.ForeignKey(
                        name: "FK_production_items_order_items_order_item_id",
                        column: x => x.order_item_id,
                        principalTable: "order_items",
                        principalColumn: "order_item_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_production_items_product_versions_product_version_id",
                        column: x => x.product_version_id,
                        principalTable: "product_versions",
                        principalColumn: "product_version_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_production_items_production_requests_production_request_id",
                        column: x => x.production_request_id,
                        principalTable: "production_requests",
                        principalColumn: "production_request_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_accounts_email",
                table: "accounts",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_accounts_role_id",
                table: "accounts",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_customization_requests_approved_product_version_id",
                table: "customization_requests",
                column: "approved_product_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_customization_requests_designer_id",
                table: "customization_requests",
                column: "designer_id");

            migrationBuilder.CreateIndex(
                name: "IX_customization_requests_production_review_by",
                table: "customization_requests",
                column: "production_review_by");

            migrationBuilder.CreateIndex(
                name: "IX_customization_requests_project_id",
                table: "customization_requests",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_customization_requests_proposal_id",
                table: "customization_requests",
                column: "proposal_id");

            migrationBuilder.CreateIndex(
                name: "IX_customization_requests_proposal_item_id",
                table: "customization_requests",
                column: "proposal_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_customization_requests_requested_by_customer_id",
                table: "customization_requests",
                column: "requested_by_customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_customization_requests_sales_review_by",
                table: "customization_requests",
                column: "sales_review_by");

            migrationBuilder.CreateIndex(
                name: "IX_deliveries_assigned_delivery_staff_id",
                table: "deliveries",
                column: "assigned_delivery_staff_id");

            migrationBuilder.CreateIndex(
                name: "IX_deliveries_delivery_code",
                table: "deliveries",
                column: "delivery_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_deliveries_order_id",
                table: "deliveries",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "IX_deliveries_project_id",
                table: "deliveries",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_items_delivery_id",
                table: "delivery_items",
                column: "delivery_id");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_items_order_item_id",
                table: "delivery_items",
                column: "order_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_items_project_area_id",
                table: "delivery_items",
                column: "project_area_id");

            migrationBuilder.CreateIndex(
                name: "IX_file_links_file_id",
                table: "file_links",
                column: "file_id");

            migrationBuilder.CreateIndex(
                name: "IX_files_uploaded_by",
                table: "files",
                column: "uploaded_by");

            migrationBuilder.CreateIndex(
                name: "IX_measurement_appointments_designer_id",
                table: "measurement_appointments",
                column: "designer_id");

            migrationBuilder.CreateIndex(
                name: "IX_measurement_appointments_project_area_id",
                table: "measurement_appointments",
                column: "project_area_id");

            migrationBuilder.CreateIndex(
                name: "IX_measurement_appointments_project_id",
                table: "measurement_appointments",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_measurement_appointments_scheduled_by_sales_id",
                table: "measurement_appointments",
                column: "scheduled_by_sales_id");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_project_id",
                table: "notifications",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_receiver_id",
                table: "notifications",
                column: "receiver_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_items_order_id",
                table: "order_items",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_items_product_version_id",
                table: "order_items",
                column: "product_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_items_quotation_item_id",
                table: "order_items",
                column: "quotation_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_orders_confirmed_by",
                table: "orders",
                column: "confirmed_by");

            migrationBuilder.CreateIndex(
                name: "IX_orders_customer_id",
                table: "orders",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_orders_order_code",
                table: "orders",
                column: "order_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_orders_project_id",
                table: "orders",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_orders_proposal_id",
                table: "orders",
                column: "proposal_id");

            migrationBuilder.CreateIndex(
                name: "IX_orders_quotation_id",
                table: "orders",
                column: "quotation_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_orders_sales_id",
                table: "orders",
                column: "sales_id");

            migrationBuilder.CreateIndex(
                name: "IX_payments_order_id",
                table: "payments",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "IX_payments_paid_by",
                table: "payments",
                column: "paid_by");

            migrationBuilder.CreateIndex(
                name: "IX_payments_project_id",
                table: "payments",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_payments_quotation_id",
                table: "payments",
                column: "quotation_id");

            migrationBuilder.CreateIndex(
                name: "IX_product_versions_created_by",
                table: "product_versions",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_product_versions_product_id",
                table: "product_versions",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_product_versions_project_id",
                table: "product_versions",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_product_versions_version_code",
                table: "product_versions",
                column: "version_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_production_items_order_item_id",
                table: "production_items",
                column: "order_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_production_items_product_version_id",
                table: "production_items",
                column: "product_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_production_items_production_request_id",
                table: "production_items",
                column: "production_request_id");

            migrationBuilder.CreateIndex(
                name: "IX_production_requests_assigned_to",
                table: "production_requests",
                column: "assigned_to");

            migrationBuilder.CreateIndex(
                name: "IX_production_requests_order_id",
                table: "production_requests",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "IX_production_requests_production_code",
                table: "production_requests",
                column: "production_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_production_requests_project_id",
                table: "production_requests",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_category_id",
                table: "products",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_created_by",
                table: "products",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_products_product_code",
                table: "products",
                column: "product_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_areas_created_by",
                table: "project_areas",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_project_areas_parent_area_id",
                table: "project_areas",
                column: "parent_area_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_areas_project_id",
                table: "project_areas",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_assignments_account_id",
                table: "project_assignments",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_assignments_assigned_by",
                table: "project_assignments",
                column: "assigned_by");

            migrationBuilder.CreateIndex(
                name: "IX_project_assignments_project_id",
                table: "project_assignments",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_chat_messages_attachment_file_id",
                table: "project_chat_messages",
                column: "attachment_file_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_chat_messages_chat_id",
                table: "project_chat_messages",
                column: "chat_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_chat_messages_sender_id",
                table: "project_chat_messages",
                column: "sender_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_chats_project_id",
                table: "project_chats",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_chats_staff_id",
                table: "project_chats",
                column: "staff_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_reviews_customer_id",
                table: "project_reviews",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_reviews_order_id",
                table: "project_reviews",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_reviews_project_id",
                table: "project_reviews",
                column: "project_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_projects_assigned_designer_id",
                table: "projects",
                column: "assigned_designer_id");

            migrationBuilder.CreateIndex(
                name: "IX_projects_assigned_sales_id",
                table: "projects",
                column: "assigned_sales_id");

            migrationBuilder.CreateIndex(
                name: "IX_projects_created_by",
                table: "projects",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_projects_customer_id",
                table: "projects",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_projects_project_code",
                table: "projects",
                column: "project_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_proposal_items_product_version_id",
                table: "proposal_items",
                column: "product_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_proposal_items_project_area_id",
                table: "proposal_items",
                column: "project_area_id");

            migrationBuilder.CreateIndex(
                name: "IX_proposal_items_proposal_id",
                table: "proposal_items",
                column: "proposal_id");

            migrationBuilder.CreateIndex(
                name: "IX_proposal_items_scene_id",
                table: "proposal_items",
                column: "scene_id");

            migrationBuilder.CreateIndex(
                name: "IX_proposal_scenes_created_by",
                table: "proposal_scenes",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_proposal_scenes_preview_file_id",
                table: "proposal_scenes",
                column: "preview_file_id");

            migrationBuilder.CreateIndex(
                name: "IX_proposal_scenes_project_area_id",
                table: "proposal_scenes",
                column: "project_area_id");

            migrationBuilder.CreateIndex(
                name: "IX_proposal_scenes_proposal_id",
                table: "proposal_scenes",
                column: "proposal_id");

            migrationBuilder.CreateIndex(
                name: "IX_proposals_created_by",
                table: "proposals",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_proposals_parent_proposal_id",
                table: "proposals",
                column: "parent_proposal_id");

            migrationBuilder.CreateIndex(
                name: "IX_proposals_project_id",
                table: "proposals",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_quotation_items_product_version_id",
                table: "quotation_items",
                column: "product_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_quotation_items_proposal_item_id",
                table: "quotation_items",
                column: "proposal_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_quotation_items_quotation_id",
                table: "quotation_items",
                column: "quotation_id");

            migrationBuilder.CreateIndex(
                name: "IX_quotations_created_by",
                table: "quotations",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_quotations_project_id",
                table: "quotations",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_quotations_proposal_id",
                table: "quotations",
                column: "proposal_id");

            migrationBuilder.CreateIndex(
                name: "IX_quotations_quotation_code",
                table: "quotations",
                column: "quotation_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_roles_role_name",
                table: "roles",
                column: "role_name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "customization_requests");

            migrationBuilder.DropTable(
                name: "delivery_items");

            migrationBuilder.DropTable(
                name: "file_links");

            migrationBuilder.DropTable(
                name: "measurement_appointments");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropTable(
                name: "production_items");

            migrationBuilder.DropTable(
                name: "project_assignments");

            migrationBuilder.DropTable(
                name: "project_chat_messages");

            migrationBuilder.DropTable(
                name: "project_reviews");

            migrationBuilder.DropTable(
                name: "deliveries");

            migrationBuilder.DropTable(
                name: "order_items");

            migrationBuilder.DropTable(
                name: "production_requests");

            migrationBuilder.DropTable(
                name: "project_chats");

            migrationBuilder.DropTable(
                name: "quotation_items");

            migrationBuilder.DropTable(
                name: "orders");

            migrationBuilder.DropTable(
                name: "proposal_items");

            migrationBuilder.DropTable(
                name: "quotations");

            migrationBuilder.DropTable(
                name: "product_versions");

            migrationBuilder.DropTable(
                name: "proposal_scenes");

            migrationBuilder.DropTable(
                name: "products");

            migrationBuilder.DropTable(
                name: "files");

            migrationBuilder.DropTable(
                name: "project_areas");

            migrationBuilder.DropTable(
                name: "proposals");

            migrationBuilder.DropTable(
                name: "categories");

            migrationBuilder.DropTable(
                name: "projects");

            migrationBuilder.DropTable(
                name: "accounts");

            migrationBuilder.DropTable(
                name: "roles");
        }
    }
}
