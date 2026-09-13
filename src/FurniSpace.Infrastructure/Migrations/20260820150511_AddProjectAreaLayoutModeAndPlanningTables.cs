using System;
using FurniSpace.Domain.Enums;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurniSpace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectAreaLayoutModeAndPlanningTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:account_status", "ACTIVE,INACTIVE,SUSPENDED")
                .Annotation("Npgsql:Enum:customization_status", "SUBMITTED,REVIEWING,ACCEPTED,CANCELLED")
                .Annotation("Npgsql:Enum:customization_version_status", "DRAFT,REVIEWING,PRODUCTION_REJECTED,ACCEPTED,WITHDRAWN")
                .Annotation("Npgsql:Enum:file_status", "ACTIVE,ARCHIVED")
                .Annotation("Npgsql:Enum:file_type", "SPACE_IMAGE,FLOOR_PLAN,REFERENCE_IMAGE,BRAND_ASSET,CAD_FILE,PDF_DRAWING,MEASUREMENT_REPORT,LIDAR_SCAN,MODEL_3D,TEXTURE,PRODUCT_PREVIEW,PROPOSAL_PREVIEW,PROPOSAL_FILE,QUOTATION_FILE,ORDER_DOCUMENT,PRODUCTION_FILE,DELIVERY_PHOTO,DELIVERY_NOTE,REVIEW_IMAGE,OTHER")
                .Annotation("Npgsql:Enum:file_visibility", "CUSTOMER_VISIBLE,STAFF_ONLY,PRIVATE")
                .Annotation("Npgsql:Enum:layout_asset_status", "ACTIVE,INACTIVE,ARCHIVED")
                .Annotation("Npgsql:Enum:layout_asset_type", "WALL_MATERIAL,FLOOR_MATERIAL,STAIR,DOOR,WINDOW,COLUMN,BEAM,DECORATIVE_WALL,DECORATIVE_FLOOR,DECORATIVE_OBJECT,OTHER")
                .Annotation("Npgsql:Enum:notification_status", "UNREAD,READ")
                .Annotation("Npgsql:Enum:order_item_status", "PENDING,IN_PRODUCTION,READY,UNAVAILABLE,DELIVERED,CANCELLED")
                .Annotation("Npgsql:Enum:order_status", "CREATED,DEPOSIT_PENDING,DEPOSIT_PAID,IN_PRODUCTION,READY_FOR_DELIVERY,DELIVERING,DELIVERED,FINAL_PAYMENT_PENDING,COMPLETED,CANCELLED")
                .Annotation("Npgsql:Enum:payment_method", "PAYMENT_LINK,QR_CODE,BANK_TRANSFER,CASH,OTHER")
                .Annotation("Npgsql:Enum:payment_provider", "PAYOS,SEPAY,CASH,MANUAL_BANK_TRANSFER,OTHER")
                .Annotation("Npgsql:Enum:payment_status", "PENDING,PROCESSING,PAID,CANCELLED,EXPIRED,REFUNDED")
                .Annotation("Npgsql:Enum:payment_transaction_status", "PENDING,SUCCESS,FAILED,CANCELLED")
                .Annotation("Npgsql:Enum:payment_transaction_type", "CHARGE,REFUND,ADJUSTMENT")
                .Annotation("Npgsql:Enum:payment_type", "PROJECT_START_FEE,DEPOSIT,REMAINING_PAYMENT,FULL_PAYMENT,REFUND,OTHER")
                .Annotation("Npgsql:Enum:product_status", "ACTIVE,INACTIVE,ARCHIVED")
                .Annotation("Npgsql:Enum:product_version_type", "STANDARD,CUSTOM,PROJECT_SPECIFIC")
                .Annotation("Npgsql:Enum:production_feasibility_status", "PENDING,FEASIBLE,NOT_FEASIBLE")
                .Annotation("Npgsql:Enum:production_item_status", "PENDING,IN_PRODUCTION,COMPLETED,CANCELLED")
                .Annotation("Npgsql:Enum:production_request_status", "PENDING_REVIEW,FEASIBLE,IN_PRODUCTION,COMPLETED,CANCELLED")
                .Annotation("Npgsql:Enum:project_area_status", "DRAFT,NEED_MEASUREMENT,MEASURED,VERIFIED,CANCELLED")
                .Annotation("Npgsql:Enum:project_area_type", "STORE,FLOOR,ROOM,ZONE,OUTDOOR_AREA,OTHER")
                .Annotation("Npgsql:Enum:project_chat_message_type", "TEXT,FILE,SYSTEM")
                .Annotation("Npgsql:Enum:project_chat_status", "OPEN,CLOSED,ARCHIVED")
                .Annotation("Npgsql:Enum:project_chat_type", "SALES,DESIGNER,PRODUCTION,DELIVERY,GENERAL,INTERNAL")
                .Annotation("Npgsql:Enum:project_phase_type", "CONSULTATION,MEASUREMENT,PROPOSAL,QUOTATION,PRODUCTION,DELIVERY,HANDOVER")
                .Annotation("Npgsql:Enum:project_schedule_status", "PENDING_CONFIRMATION,CONFIRMED,COMPLETED,CANCELLED")
                .Annotation("Npgsql:Enum:project_schedule_type", "MEASUREMENT,CONSULTATION,DESIGN_REVIEW,DELIVERY,HANDOVER,OTHER")
                .Annotation("Npgsql:Enum:project_status", "SUBMITTED,IN_CONSULTATION,NEED_BASIC_INFORMATION,WAITING_FOR_DESIGNER_ASSIGNMENT,MEASUREMENT_REQUIRED,SPACE_VERIFIED,PROPOSAL_CONSULTING,PROPOSAL_SELECTED,QUOTATION_SENT,QUOTATION_REVISION_REQUESTED,ORDER_CONFIRMED,IN_PRODUCTION,READY_FOR_DELIVERY,DELIVERING,DELIVERED,COMPLETED,REJECTED")
                .Annotation("Npgsql:Enum:proposal_scene_type", "TWO_D,THREE_D,ROOM_PLANNER")
                .Annotation("Npgsql:Enum:proposal_scene_variant_status", "DRAFT,SUBMITTED,ACCEPTED,REJECTED,APPLIED")
                .Annotation("Npgsql:Enum:proposal_scene_variant_type", "CUSTOMER_SUGGESTION,DESIGNER_REVISION")
                .Annotation("Npgsql:Enum:proposal_status", "DRAFT,PUBLISHED,SELECTED,REVISION_REQUESTED,REJECTED,ARCHIVED")
                .Annotation("Npgsql:Enum:quotation_status", "DRAFT,SENT,REVISION_REQUESTED,REVISED,ACCEPTED,REJECTED,EXPIRED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:account_status", "ACTIVE,INACTIVE,SUSPENDED")
                .OldAnnotation("Npgsql:Enum:customization_status", "SUBMITTED,REVIEWING,ACCEPTED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:customization_version_status", "DRAFT,REVIEWING,PRODUCTION_REJECTED,ACCEPTED,WITHDRAWN")
                .OldAnnotation("Npgsql:Enum:file_status", "ACTIVE,ARCHIVED")
                .OldAnnotation("Npgsql:Enum:file_type", "SPACE_IMAGE,FLOOR_PLAN,REFERENCE_IMAGE,BRAND_ASSET,CAD_FILE,PDF_DRAWING,MEASUREMENT_REPORT,LIDAR_SCAN,MODEL_3D,TEXTURE,PRODUCT_PREVIEW,PROPOSAL_PREVIEW,PROPOSAL_FILE,QUOTATION_FILE,ORDER_DOCUMENT,PRODUCTION_FILE,DELIVERY_PHOTO,DELIVERY_NOTE,REVIEW_IMAGE,OTHER")
                .OldAnnotation("Npgsql:Enum:file_visibility", "CUSTOMER_VISIBLE,STAFF_ONLY,PRIVATE")
                .OldAnnotation("Npgsql:Enum:notification_status", "UNREAD,READ")
                .OldAnnotation("Npgsql:Enum:order_item_status", "PENDING,IN_PRODUCTION,READY,UNAVAILABLE,DELIVERED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:order_status", "CREATED,DEPOSIT_PENDING,DEPOSIT_PAID,IN_PRODUCTION,READY_FOR_DELIVERY,DELIVERING,DELIVERED,FINAL_PAYMENT_PENDING,COMPLETED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:payment_method", "PAYMENT_LINK,QR_CODE,BANK_TRANSFER,CASH,OTHER")
                .OldAnnotation("Npgsql:Enum:payment_provider", "PAYOS,SEPAY,CASH,MANUAL_BANK_TRANSFER,OTHER")
                .OldAnnotation("Npgsql:Enum:payment_status", "PENDING,PROCESSING,PAID,CANCELLED,EXPIRED,REFUNDED")
                .OldAnnotation("Npgsql:Enum:payment_transaction_status", "PENDING,SUCCESS,FAILED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:payment_transaction_type", "CHARGE,REFUND,ADJUSTMENT")
                .OldAnnotation("Npgsql:Enum:payment_type", "PROJECT_START_FEE,DEPOSIT,REMAINING_PAYMENT,FULL_PAYMENT,REFUND,OTHER")
                .OldAnnotation("Npgsql:Enum:product_status", "ACTIVE,INACTIVE,ARCHIVED")
                .OldAnnotation("Npgsql:Enum:product_version_type", "STANDARD,CUSTOM,PROJECT_SPECIFIC")
                .OldAnnotation("Npgsql:Enum:production_feasibility_status", "PENDING,FEASIBLE,NOT_FEASIBLE")
                .OldAnnotation("Npgsql:Enum:production_item_status", "PENDING,IN_PRODUCTION,COMPLETED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:production_request_status", "PENDING_REVIEW,FEASIBLE,IN_PRODUCTION,COMPLETED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:project_area_status", "DRAFT,NEED_MEASUREMENT,MEASURED,VERIFIED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:project_area_type", "STORE,FLOOR,ROOM,ZONE,OUTDOOR_AREA,OTHER")
                .OldAnnotation("Npgsql:Enum:project_chat_message_type", "TEXT,FILE,SYSTEM")
                .OldAnnotation("Npgsql:Enum:project_chat_status", "OPEN,CLOSED,ARCHIVED")
                .OldAnnotation("Npgsql:Enum:project_chat_type", "SALES,DESIGNER,PRODUCTION,DELIVERY,GENERAL,INTERNAL")
                .OldAnnotation("Npgsql:Enum:project_schedule_status", "PENDING_CONFIRMATION,CONFIRMED,COMPLETED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:project_schedule_type", "MEASUREMENT,CONSULTATION,DESIGN_REVIEW,DELIVERY,HANDOVER,OTHER")
                .OldAnnotation("Npgsql:Enum:project_status", "SUBMITTED,IN_CONSULTATION,NEED_BASIC_INFORMATION,WAITING_FOR_DESIGNER_ASSIGNMENT,MEASUREMENT_REQUIRED,SPACE_VERIFIED,PROPOSAL_CONSULTING,PROPOSAL_SELECTED,QUOTATION_SENT,QUOTATION_REVISION_REQUESTED,ORDER_CONFIRMED,IN_PRODUCTION,READY_FOR_DELIVERY,DELIVERING,DELIVERED,COMPLETED,REJECTED")
                .OldAnnotation("Npgsql:Enum:proposal_scene_type", "TWO_D,THREE_D,ROOM_PLANNER")
                .OldAnnotation("Npgsql:Enum:proposal_scene_variant_status", "DRAFT,SUBMITTED,ACCEPTED,REJECTED,APPLIED")
                .OldAnnotation("Npgsql:Enum:proposal_scene_variant_type", "CUSTOMER_SUGGESTION,DESIGNER_REVISION")
                .OldAnnotation("Npgsql:Enum:proposal_status", "DRAFT,PUBLISHED,SELECTED,REVISION_REQUESTED,REJECTED,ARCHIVED")
                .OldAnnotation("Npgsql:Enum:quotation_status", "DRAFT,SENT,REVISION_REQUESTED,REVISED,ACCEPTED,REJECTED,EXPIRED,CANCELLED");

            migrationBuilder.AddColumn<bool>(
                name: "is_special_layout",
                table: "project_areas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "layout_assets",
                columns: table => new
                {
                    layout_asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_code = table.Column<string>(type: "varchar(50)", nullable: false),
                    asset_name = table.Column<string>(type: "varchar(150)", nullable: false),
                    asset_type = table.Column<LayoutAssetType>(type: "layout_asset_type", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<LayoutAssetStatus>(type: "layout_asset_status", nullable: false, defaultValueSql: "'ACTIVE'::layout_asset_status"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_layout_assets", x => x.layout_asset_id);
                    table.ForeignKey(
                        name: "FK_layout_assets_accounts_created_by",
                        column: x => x.created_by,
                        principalTable: "accounts",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "project_phase_timelines",
                columns: table => new
                {
                    project_phase_timeline_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    phase = table.Column<ProjectPhaseType>(type: "project_phase_type", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_phase_timelines", x => x.project_phase_timeline_id);
                    table.ForeignKey(
                        name: "FK_project_phase_timelines_accounts_created_by",
                        column: x => x.created_by,
                        principalTable: "accounts",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_phase_timelines_accounts_updated_by",
                        column: x => x.updated_by,
                        principalTable: "accounts",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_phase_timelines_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "project_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_layout_assets_asset_code",
                table: "layout_assets",
                column: "asset_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_layout_assets_asset_type",
                table: "layout_assets",
                column: "asset_type");

            migrationBuilder.CreateIndex(
                name: "IX_layout_assets_created_by",
                table: "layout_assets",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_layout_assets_status",
                table: "layout_assets",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_project_phase_timelines_created_by",
                table: "project_phase_timelines",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_project_phase_timelines_due_date",
                table: "project_phase_timelines",
                column: "due_date");

            migrationBuilder.CreateIndex(
                name: "IX_project_phase_timelines_phase",
                table: "project_phase_timelines",
                column: "phase");

            migrationBuilder.CreateIndex(
                name: "IX_project_phase_timelines_project_id",
                table: "project_phase_timelines",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_phase_timelines_project_id_phase",
                table: "project_phase_timelines",
                columns: new[] { "project_id", "phase" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_phase_timelines_updated_by",
                table: "project_phase_timelines",
                column: "updated_by");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "layout_assets");

            migrationBuilder.DropTable(
                name: "project_phase_timelines");

            migrationBuilder.DropColumn(
                name: "is_special_layout",
                table: "project_areas");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:account_status", "ACTIVE,INACTIVE,SUSPENDED")
                .Annotation("Npgsql:Enum:customization_status", "SUBMITTED,REVIEWING,ACCEPTED,CANCELLED")
                .Annotation("Npgsql:Enum:customization_version_status", "DRAFT,REVIEWING,PRODUCTION_REJECTED,ACCEPTED,WITHDRAWN")
                .Annotation("Npgsql:Enum:file_status", "ACTIVE,ARCHIVED")
                .Annotation("Npgsql:Enum:file_type", "SPACE_IMAGE,FLOOR_PLAN,REFERENCE_IMAGE,BRAND_ASSET,CAD_FILE,PDF_DRAWING,MEASUREMENT_REPORT,LIDAR_SCAN,MODEL_3D,TEXTURE,PRODUCT_PREVIEW,PROPOSAL_PREVIEW,PROPOSAL_FILE,QUOTATION_FILE,ORDER_DOCUMENT,PRODUCTION_FILE,DELIVERY_PHOTO,DELIVERY_NOTE,REVIEW_IMAGE,OTHER")
                .Annotation("Npgsql:Enum:file_visibility", "CUSTOMER_VISIBLE,STAFF_ONLY,PRIVATE")
                .Annotation("Npgsql:Enum:notification_status", "UNREAD,READ")
                .Annotation("Npgsql:Enum:order_item_status", "PENDING,IN_PRODUCTION,READY,UNAVAILABLE,DELIVERED,CANCELLED")
                .Annotation("Npgsql:Enum:order_status", "CREATED,DEPOSIT_PENDING,DEPOSIT_PAID,IN_PRODUCTION,READY_FOR_DELIVERY,DELIVERING,DELIVERED,FINAL_PAYMENT_PENDING,COMPLETED,CANCELLED")
                .Annotation("Npgsql:Enum:payment_method", "PAYMENT_LINK,QR_CODE,BANK_TRANSFER,CASH,OTHER")
                .Annotation("Npgsql:Enum:payment_provider", "PAYOS,SEPAY,CASH,MANUAL_BANK_TRANSFER,OTHER")
                .Annotation("Npgsql:Enum:payment_status", "PENDING,PROCESSING,PAID,CANCELLED,EXPIRED,REFUNDED")
                .Annotation("Npgsql:Enum:payment_transaction_status", "PENDING,SUCCESS,FAILED,CANCELLED")
                .Annotation("Npgsql:Enum:payment_transaction_type", "CHARGE,REFUND,ADJUSTMENT")
                .Annotation("Npgsql:Enum:payment_type", "PROJECT_START_FEE,DEPOSIT,REMAINING_PAYMENT,FULL_PAYMENT,REFUND,OTHER")
                .Annotation("Npgsql:Enum:product_status", "ACTIVE,INACTIVE,ARCHIVED")
                .Annotation("Npgsql:Enum:product_version_type", "STANDARD,CUSTOM,PROJECT_SPECIFIC")
                .Annotation("Npgsql:Enum:production_feasibility_status", "PENDING,FEASIBLE,NOT_FEASIBLE")
                .Annotation("Npgsql:Enum:production_item_status", "PENDING,IN_PRODUCTION,COMPLETED,CANCELLED")
                .Annotation("Npgsql:Enum:production_request_status", "PENDING_REVIEW,FEASIBLE,IN_PRODUCTION,COMPLETED,CANCELLED")
                .Annotation("Npgsql:Enum:project_area_status", "DRAFT,NEED_MEASUREMENT,MEASURED,VERIFIED,CANCELLED")
                .Annotation("Npgsql:Enum:project_area_type", "STORE,FLOOR,ROOM,ZONE,OUTDOOR_AREA,OTHER")
                .Annotation("Npgsql:Enum:project_chat_message_type", "TEXT,FILE,SYSTEM")
                .Annotation("Npgsql:Enum:project_chat_status", "OPEN,CLOSED,ARCHIVED")
                .Annotation("Npgsql:Enum:project_chat_type", "SALES,DESIGNER,PRODUCTION,DELIVERY,GENERAL,INTERNAL")
                .Annotation("Npgsql:Enum:project_schedule_status", "PENDING_CONFIRMATION,CONFIRMED,COMPLETED,CANCELLED")
                .Annotation("Npgsql:Enum:project_schedule_type", "MEASUREMENT,CONSULTATION,DESIGN_REVIEW,DELIVERY,HANDOVER,OTHER")
                .Annotation("Npgsql:Enum:project_status", "SUBMITTED,IN_CONSULTATION,NEED_BASIC_INFORMATION,WAITING_FOR_DESIGNER_ASSIGNMENT,MEASUREMENT_REQUIRED,SPACE_VERIFIED,PROPOSAL_CONSULTING,PROPOSAL_SELECTED,QUOTATION_SENT,QUOTATION_REVISION_REQUESTED,ORDER_CONFIRMED,IN_PRODUCTION,READY_FOR_DELIVERY,DELIVERING,DELIVERED,COMPLETED,REJECTED")
                .Annotation("Npgsql:Enum:proposal_scene_type", "TWO_D,THREE_D,ROOM_PLANNER")
                .Annotation("Npgsql:Enum:proposal_scene_variant_status", "DRAFT,SUBMITTED,ACCEPTED,REJECTED,APPLIED")
                .Annotation("Npgsql:Enum:proposal_scene_variant_type", "CUSTOMER_SUGGESTION,DESIGNER_REVISION")
                .Annotation("Npgsql:Enum:proposal_status", "DRAFT,PUBLISHED,SELECTED,REVISION_REQUESTED,REJECTED,ARCHIVED")
                .Annotation("Npgsql:Enum:quotation_status", "DRAFT,SENT,REVISION_REQUESTED,REVISED,ACCEPTED,REJECTED,EXPIRED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:account_status", "ACTIVE,INACTIVE,SUSPENDED")
                .OldAnnotation("Npgsql:Enum:customization_status", "SUBMITTED,REVIEWING,ACCEPTED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:customization_version_status", "DRAFT,REVIEWING,PRODUCTION_REJECTED,ACCEPTED,WITHDRAWN")
                .OldAnnotation("Npgsql:Enum:file_status", "ACTIVE,ARCHIVED")
                .OldAnnotation("Npgsql:Enum:file_type", "SPACE_IMAGE,FLOOR_PLAN,REFERENCE_IMAGE,BRAND_ASSET,CAD_FILE,PDF_DRAWING,MEASUREMENT_REPORT,LIDAR_SCAN,MODEL_3D,TEXTURE,PRODUCT_PREVIEW,PROPOSAL_PREVIEW,PROPOSAL_FILE,QUOTATION_FILE,ORDER_DOCUMENT,PRODUCTION_FILE,DELIVERY_PHOTO,DELIVERY_NOTE,REVIEW_IMAGE,OTHER")
                .OldAnnotation("Npgsql:Enum:file_visibility", "CUSTOMER_VISIBLE,STAFF_ONLY,PRIVATE")
                .OldAnnotation("Npgsql:Enum:layout_asset_status", "ACTIVE,INACTIVE,ARCHIVED")
                .OldAnnotation("Npgsql:Enum:layout_asset_type", "WALL_MATERIAL,FLOOR_MATERIAL,STAIR,DOOR,WINDOW,COLUMN,BEAM,DECORATIVE_WALL,DECORATIVE_FLOOR,DECORATIVE_OBJECT,OTHER")
                .OldAnnotation("Npgsql:Enum:notification_status", "UNREAD,READ")
                .OldAnnotation("Npgsql:Enum:order_item_status", "PENDING,IN_PRODUCTION,READY,UNAVAILABLE,DELIVERED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:order_status", "CREATED,DEPOSIT_PENDING,DEPOSIT_PAID,IN_PRODUCTION,READY_FOR_DELIVERY,DELIVERING,DELIVERED,FINAL_PAYMENT_PENDING,COMPLETED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:payment_method", "PAYMENT_LINK,QR_CODE,BANK_TRANSFER,CASH,OTHER")
                .OldAnnotation("Npgsql:Enum:payment_provider", "PAYOS,SEPAY,CASH,MANUAL_BANK_TRANSFER,OTHER")
                .OldAnnotation("Npgsql:Enum:payment_status", "PENDING,PROCESSING,PAID,CANCELLED,EXPIRED,REFUNDED")
                .OldAnnotation("Npgsql:Enum:payment_transaction_status", "PENDING,SUCCESS,FAILED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:payment_transaction_type", "CHARGE,REFUND,ADJUSTMENT")
                .OldAnnotation("Npgsql:Enum:payment_type", "PROJECT_START_FEE,DEPOSIT,REMAINING_PAYMENT,FULL_PAYMENT,REFUND,OTHER")
                .OldAnnotation("Npgsql:Enum:product_status", "ACTIVE,INACTIVE,ARCHIVED")
                .OldAnnotation("Npgsql:Enum:product_version_type", "STANDARD,CUSTOM,PROJECT_SPECIFIC")
                .OldAnnotation("Npgsql:Enum:production_feasibility_status", "PENDING,FEASIBLE,NOT_FEASIBLE")
                .OldAnnotation("Npgsql:Enum:production_item_status", "PENDING,IN_PRODUCTION,COMPLETED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:production_request_status", "PENDING_REVIEW,FEASIBLE,IN_PRODUCTION,COMPLETED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:project_area_status", "DRAFT,NEED_MEASUREMENT,MEASURED,VERIFIED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:project_area_type", "STORE,FLOOR,ROOM,ZONE,OUTDOOR_AREA,OTHER")
                .OldAnnotation("Npgsql:Enum:project_chat_message_type", "TEXT,FILE,SYSTEM")
                .OldAnnotation("Npgsql:Enum:project_chat_status", "OPEN,CLOSED,ARCHIVED")
                .OldAnnotation("Npgsql:Enum:project_chat_type", "SALES,DESIGNER,PRODUCTION,DELIVERY,GENERAL,INTERNAL")
                .OldAnnotation("Npgsql:Enum:project_phase_type", "CONSULTATION,MEASUREMENT,PROPOSAL,QUOTATION,PRODUCTION,DELIVERY,HANDOVER")
                .OldAnnotation("Npgsql:Enum:project_schedule_status", "PENDING_CONFIRMATION,CONFIRMED,COMPLETED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:project_schedule_type", "MEASUREMENT,CONSULTATION,DESIGN_REVIEW,DELIVERY,HANDOVER,OTHER")
                .OldAnnotation("Npgsql:Enum:project_status", "SUBMITTED,IN_CONSULTATION,NEED_BASIC_INFORMATION,WAITING_FOR_DESIGNER_ASSIGNMENT,MEASUREMENT_REQUIRED,SPACE_VERIFIED,PROPOSAL_CONSULTING,PROPOSAL_SELECTED,QUOTATION_SENT,QUOTATION_REVISION_REQUESTED,ORDER_CONFIRMED,IN_PRODUCTION,READY_FOR_DELIVERY,DELIVERING,DELIVERED,COMPLETED,REJECTED")
                .OldAnnotation("Npgsql:Enum:proposal_scene_type", "TWO_D,THREE_D,ROOM_PLANNER")
                .OldAnnotation("Npgsql:Enum:proposal_scene_variant_status", "DRAFT,SUBMITTED,ACCEPTED,REJECTED,APPLIED")
                .OldAnnotation("Npgsql:Enum:proposal_scene_variant_type", "CUSTOMER_SUGGESTION,DESIGNER_REVISION")
                .OldAnnotation("Npgsql:Enum:proposal_status", "DRAFT,PUBLISHED,SELECTED,REVISION_REQUESTED,REJECTED,ARCHIVED")
                .OldAnnotation("Npgsql:Enum:quotation_status", "DRAFT,SENT,REVISION_REQUESTED,REVISED,ACCEPTED,REJECTED,EXPIRED,CANCELLED");
        }
    }
}
