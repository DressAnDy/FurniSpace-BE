using System;
using FurniSpace.Domain.Enums;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurniSpace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AlignSchemaWithNewDb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TYPE payment_status ADD VALUE IF NOT EXISTS 'PROCESSING' AFTER 'PENDING';
                ALTER TYPE payment_status ADD VALUE IF NOT EXISTS 'PARTIALLY_PAID' AFTER 'PAID';

                DO $$
                BEGIN
                    CREATE TYPE proposal_scene_variant_status AS ENUM ('DRAFT', 'SUBMITTED', 'ACCEPTED', 'REJECTED', 'APPLIED');
                EXCEPTION
                    WHEN duplicate_object THEN NULL;
                END $$;

                DO $$
                BEGIN
                    CREATE TYPE proposal_scene_variant_type AS ENUM ('CUSTOMER_SUGGESTION', 'DESIGNER_REVISION');
                EXCEPTION
                    WHEN duplicate_object THEN NULL;
                END $$;

                UPDATE project_areas
                SET status = CASE
                    WHEN status::text = 'DESIGNING' THEN 'MEASURED'::project_area_status
                    WHEN status::text IN ('DESIGNED', 'APPROVED') THEN 'VERIFIED'::project_area_status
                    ELSE status
                END
                WHERE status::text IN ('DESIGNING', 'DESIGNED', 'APPROVED');

                ALTER TABLE project_areas ALTER COLUMN status DROP DEFAULT;
                ALTER TYPE project_area_status RENAME TO project_area_status_old;
                CREATE TYPE project_area_status AS ENUM ('DRAFT', 'NEED_MEASUREMENT', 'MEASURED', 'VERIFIED', 'CANCELLED');
                ALTER TABLE project_areas
                    ALTER COLUMN status TYPE project_area_status
                    USING status::text::project_area_status;
                ALTER TABLE project_areas ALTER COLUMN status SET DEFAULT 'DRAFT'::project_area_status;
                DROP TYPE project_area_status_old;
                """);

            migrationBuilder.DropColumn(
                name: "finish",
                table: "proposal_items");

            migrationBuilder.DropColumn(
                name: "payment_method",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "transaction_reference",
                table: "payments");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:account_status", "ACTIVE,INACTIVE,SUSPENDED")
                .Annotation("Npgsql:Enum:customization_status", "SUBMITTED,DESIGN_REVIEWING,WAITING_FOR_DESIGN_APPROVAL,DESIGN_REVISION_REQUESTED,PRODUCTION_REVIEWING,NOT_FEASIBLE,ACCEPTED,REJECTED_BY_CUSTOMER,CANCELLED")
                .Annotation("Npgsql:Enum:file_status", "ACTIVE,ARCHIVED")
                .Annotation("Npgsql:Enum:file_type", "SPACE_IMAGE,FLOOR_PLAN,REFERENCE_IMAGE,BRAND_ASSET,CAD_FILE,PDF_DRAWING,MEASUREMENT_REPORT,LIDAR_SCAN,MODEL_3D,TEXTURE,PRODUCT_PREVIEW,PROPOSAL_PREVIEW,PROPOSAL_FILE,QUOTATION_FILE,ORDER_DOCUMENT,PRODUCTION_FILE,DELIVERY_PHOTO,DELIVERY_NOTE,REVIEW_IMAGE,OTHER")
                .Annotation("Npgsql:Enum:file_visibility", "CUSTOMER_VISIBLE,STAFF_ONLY,PRIVATE")
                .Annotation("Npgsql:Enum:notification_status", "UNREAD,READ")
                .Annotation("Npgsql:Enum:order_item_status", "PENDING,IN_PRODUCTION,READY,UNAVAILABLE,DELIVERED,CANCELLED")
                .Annotation("Npgsql:Enum:order_status", "CREATED,DEPOSIT_PENDING,DEPOSIT_PAID,IN_PRODUCTION,PRODUCTION_PARTIALLY_FAILED,PRODUCTION_COMPLETED,READY_FOR_DELIVERY,DELIVERY_SCHEDULED,DELIVERING,DELIVERED,FINAL_PAYMENT_PENDING,COMPLETED,CANCELLED")
                .Annotation("Npgsql:Enum:payment_status", "PENDING,PAID,FAILED,REFUNDED,CANCELLED")
                .Annotation("Npgsql:Enum:payment_transaction_status", "PENDING,SUCCESS,FAILED,CANCELLED")
                .Annotation("Npgsql:Enum:payment_transaction_type", "CHARGE,REFUND,ADJUSTMENT")
                .Annotation("Npgsql:Enum:payment_type", "DEPOSIT,REMAINING_PAYMENT,FULL_PAYMENT,MEASUREMENT_FEE,DESIGN_FEE,CUSTOMIZATION_FEE,DELIVERY_FEE,CANCELLATION_FEE,REFUND,OTHER")
                .Annotation("Npgsql:Enum:product_status", "ACTIVE,INACTIVE,ARCHIVED")
                .Annotation("Npgsql:Enum:product_version_type", "STANDARD,CUSTOM,PROJECT_SPECIFIC")
                .Annotation("Npgsql:Enum:production_item_status", "PENDING,IN_PRODUCTION,COMPLETED,BLOCKED,CANCELLED")
                .Annotation("Npgsql:Enum:production_request_status", "PENDING_REVIEW,FEASIBLE,IN_PRODUCTION,COMPLETED,BLOCKED,CANCELLED")
                .Annotation("Npgsql:Enum:project_area_status", "DRAFT,NEED_MEASUREMENT,MEASURED,VERIFIED,DESIGNING,DESIGNED,APPROVED,CANCELLED")
                .Annotation("Npgsql:Enum:project_area_type", "STORE,FLOOR,ROOM,ZONE,OUTDOOR_AREA,OTHER")
                .Annotation("Npgsql:Enum:project_chat_message_type", "TEXT,FILE,SYSTEM")
                .Annotation("Npgsql:Enum:project_chat_status", "OPEN,CLOSED,ARCHIVED")
                .Annotation("Npgsql:Enum:project_chat_type", "SALES,DESIGNER,PRODUCTION,DELIVERY,GENERAL,INTERNAL")
                .Annotation("Npgsql:Enum:project_schedule_status", "PENDING_CONFIRMATION,CONFIRMED,COMPLETED,CANCELLED")
                .Annotation("Npgsql:Enum:project_schedule_type", "MEASUREMENT,CONSULTATION,DESIGN_REVIEW,DELIVERY,HANDOVER,OTHER")
                .Annotation("Npgsql:Enum:project_status", "SUBMITTED,IN_CONSULTATION,NEED_BASIC_INFORMATION,WAITING_FOR_DESIGNER_ASSIGNMENT,MEASUREMENT_REQUIRED,SPACE_VERIFIED,PROPOSAL_DRAFTING,WAITING_FOR_CUSTOMER_REVIEW,REVISION_REQUESTED,PROPOSAL_SELECTED,QUOTATION_SENT,QUOTATION_REVISION_REQUESTED,ORDER_CONFIRMED,IN_PRODUCTION,PRODUCTION_BLOCKED,READY_FOR_DELIVERY,DELIVERING,DELIVERED,COMPLETED,REJECTED")
                .Annotation("Npgsql:Enum:proposal_scene_type", "TWO_D,THREE_D")
                .Annotation("Npgsql:Enum:proposal_scene_variant_status", "DRAFT,SUBMITTED,ACCEPTED,REJECTED,APPLIED")
                .Annotation("Npgsql:Enum:proposal_scene_variant_type", "CUSTOMER_SUGGESTION,DESIGNER_REVISION")
                .Annotation("Npgsql:Enum:proposal_status", "DRAFT,PUBLISHED,VIEWED,SELECTED,REVISION_REQUESTED,REJECTED,ARCHIVED")
                .Annotation("Npgsql:Enum:quotation_status", "DRAFT,SENT,REVISION_REQUESTED,REVISED,ACCEPTED,REJECTED,EXPIRED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:account_status", "ACTIVE,INACTIVE,SUSPENDED")
                .OldAnnotation("Npgsql:Enum:customization_status", "SUBMITTED,DESIGN_REVIEWING,WAITING_FOR_DESIGN_APPROVAL,DESIGN_REVISION_REQUESTED,PRODUCTION_REVIEWING,NOT_FEASIBLE,ACCEPTED,REJECTED_BY_CUSTOMER,CANCELLED")
                .OldAnnotation("Npgsql:Enum:file_status", "ACTIVE,ARCHIVED")
                .OldAnnotation("Npgsql:Enum:file_type", "SPACE_IMAGE,FLOOR_PLAN,REFERENCE_IMAGE,BRAND_ASSET,CAD_FILE,PDF_DRAWING,MEASUREMENT_REPORT,LIDAR_SCAN,MODEL_3D,TEXTURE,PRODUCT_PREVIEW,PROPOSAL_PREVIEW,PROPOSAL_FILE,QUOTATION_FILE,ORDER_DOCUMENT,PRODUCTION_FILE,DELIVERY_PHOTO,DELIVERY_NOTE,REVIEW_IMAGE,OTHER")
                .OldAnnotation("Npgsql:Enum:file_visibility", "CUSTOMER_VISIBLE,STAFF_ONLY,PRIVATE")
                .OldAnnotation("Npgsql:Enum:notification_status", "UNREAD,READ")
                .OldAnnotation("Npgsql:Enum:order_item_status", "PENDING,IN_PRODUCTION,READY,UNAVAILABLE,DELIVERED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:order_status", "CREATED,DEPOSIT_PENDING,DEPOSIT_PAID,IN_PRODUCTION,PRODUCTION_PARTIALLY_FAILED,PRODUCTION_COMPLETED,READY_FOR_DELIVERY,DELIVERY_SCHEDULED,DELIVERING,DELIVERED,FINAL_PAYMENT_PENDING,COMPLETED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:payment_status", "PENDING,PAID,FAILED,REFUNDED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:payment_transaction_status", "PENDING,SUCCESS,FAILED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:payment_transaction_type", "CHARGE,REFUND,ADJUSTMENT")
                .OldAnnotation("Npgsql:Enum:payment_type", "DEPOSIT,REMAINING_PAYMENT,FULL_PAYMENT,MEASUREMENT_FEE,DESIGN_FEE,CUSTOMIZATION_FEE,DELIVERY_FEE,CANCELLATION_FEE,REFUND,OTHER")
                .OldAnnotation("Npgsql:Enum:product_status", "ACTIVE,INACTIVE,ARCHIVED")
                .OldAnnotation("Npgsql:Enum:product_version_type", "STANDARD,CUSTOM,PROJECT_SPECIFIC")
                .OldAnnotation("Npgsql:Enum:production_item_status", "PENDING,IN_PRODUCTION,COMPLETED,BLOCKED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:production_request_status", "PENDING_REVIEW,FEASIBLE,IN_PRODUCTION,COMPLETED,BLOCKED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:project_area_status", "DRAFT,NEED_MEASUREMENT,MEASURED,VERIFIED,DESIGNING,DESIGNED,APPROVED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:project_area_type", "STORE,FLOOR,ROOM,ZONE,OUTDOOR_AREA,OTHER")
                .OldAnnotation("Npgsql:Enum:project_chat_message_type", "TEXT,FILE,SYSTEM")
                .OldAnnotation("Npgsql:Enum:project_chat_status", "OPEN,CLOSED,ARCHIVED")
                .OldAnnotation("Npgsql:Enum:project_chat_type", "SALES,DESIGNER,PRODUCTION,DELIVERY,GENERAL,INTERNAL")
                .OldAnnotation("Npgsql:Enum:project_schedule_status", "PENDING_CONFIRMATION,CONFIRMED,COMPLETED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:project_schedule_type", "MEASUREMENT,CONSULTATION,DESIGN_REVIEW,DELIVERY,HANDOVER,OTHER")
                .OldAnnotation("Npgsql:Enum:project_status", "SUBMITTED,IN_CONSULTATION,NEED_BASIC_INFORMATION,WAITING_FOR_DESIGNER_ASSIGNMENT,MEASUREMENT_REQUIRED,SPACE_VERIFIED,PROPOSAL_DRAFTING,WAITING_FOR_CUSTOMER_REVIEW,REVISION_REQUESTED,PROPOSAL_SELECTED,QUOTATION_SENT,QUOTATION_REVISION_REQUESTED,ORDER_CONFIRMED,IN_PRODUCTION,PRODUCTION_BLOCKED,READY_FOR_DELIVERY,DELIVERING,DELIVERED,COMPLETED,REJECTED")
                .OldAnnotation("Npgsql:Enum:proposal_scene_type", "TWO_D,THREE_D")
                .OldAnnotation("Npgsql:Enum:proposal_scene_variant_status", "DRAFT,SUBMITTED,ACCEPTED,REJECTED,APPLIED")
                .OldAnnotation("Npgsql:Enum:proposal_scene_variant_type", "CUSTOMER_SUGGESTION,DESIGNER_REVISION")
                .OldAnnotation("Npgsql:Enum:proposal_status", "DRAFT,PUBLISHED,VIEWED,SELECTED,REVISION_REQUESTED,REJECTED,ARCHIVED")
                .OldAnnotation("Npgsql:Enum:quotation_status", "DRAFT,SENT,REVISION_REQUESTED,REVISED,ACCEPTED,REJECTED,EXPIRED,CANCELLED");

            migrationBuilder.AddColumn<Guid>(
                name: "approved_product_version_id",
                table: "proposal_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "total_price_snapshot",
                table: "proposal_items",
                type: "numeric(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "dimension_unit",
                table: "product_versions",
                type: "varchar(10)",
                nullable: true,
                defaultValue: "cm");

            migrationBuilder.AddColumn<Guid>(
                name: "project_id",
                table: "product_versions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "cancelled_at",
                table: "payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "currency",
                table: "payments",
                type: "varchar(10)",
                nullable: false,
                defaultValue: "VND");

            migrationBuilder.AddColumn<decimal>(
                name: "paid_amount",
                table: "payments",
                type: "numeric(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "remaining_amount",
                table: "payments",
                type: "numeric(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql(
                """
                UPDATE payments
                SET paid_amount = CASE WHEN status::text = 'PAID' THEN amount ELSE 0 END,
                    remaining_amount = CASE WHEN status::text = 'PAID' THEN 0 ELSE amount END;
                """);

            migrationBuilder.AddColumn<int>(
                name: "display_order",
                table: "file_links",
                type: "integer",
                nullable: true,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "is_primary",
                table: "file_links",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "proposal_scene_variants",
                columns: table => new
                {
                    variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    proposal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scene_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    variant_type = table.Column<ProposalSceneVariantType>(type: "proposal_scene_variant_type", nullable: true, defaultValueSql: "'CUSTOMER_SUGGESTION'::proposal_scene_variant_type"),
                    status = table.Column<ProposalSceneVariantStatus>(type: "proposal_scene_variant_status", nullable: true, defaultValueSql: "'DRAFT'::proposal_scene_variant_status"),
                    mongo_variant_scene_id = table.Column<string>(type: "varchar(100)", nullable: false),
                    note = table.Column<string>(type: "text", nullable: true),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    review_note = table.Column<string>(type: "text", nullable: true),
                    applied_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    applied_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_proposal_scene_variants", x => x.variant_id);
                    table.ForeignKey(
                        name: "FK_proposal_scene_variants_accounts_applied_by",
                        column: x => x.applied_by,
                        principalTable: "accounts",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_proposal_scene_variants_accounts_created_by",
                        column: x => x.created_by,
                        principalTable: "accounts",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_proposal_scene_variants_accounts_reviewed_by",
                        column: x => x.reviewed_by,
                        principalTable: "accounts",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_proposal_scene_variants_proposal_scenes_scene_id",
                        column: x => x.scene_id,
                        principalTable: "proposal_scenes",
                        principalColumn: "scene_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_proposal_scene_variants_proposals_proposal_id",
                        column: x => x.proposal_id,
                        principalTable: "proposals",
                        principalColumn: "proposal_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_proposal_items_approved_product_version_id",
                table: "proposal_items",
                column: "approved_product_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_product_versions_project_id",
                table: "product_versions",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "idx_scene_variants_created_by",
                table: "proposal_scene_variants",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "idx_scene_variants_proposal",
                table: "proposal_scene_variants",
                column: "proposal_id");

            migrationBuilder.CreateIndex(
                name: "idx_scene_variants_scene",
                table: "proposal_scene_variants",
                column: "scene_id");

            migrationBuilder.CreateIndex(
                name: "idx_scene_variants_scene_status",
                table: "proposal_scene_variants",
                columns: new[] { "scene_id", "status" });

            migrationBuilder.CreateIndex(
                name: "idx_scene_variants_status",
                table: "proposal_scene_variants",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_proposal_scene_variants_applied_by",
                table: "proposal_scene_variants",
                column: "applied_by");

            migrationBuilder.CreateIndex(
                name: "IX_proposal_scene_variants_reviewed_by",
                table: "proposal_scene_variants",
                column: "reviewed_by");

            migrationBuilder.AddForeignKey(
                name: "FK_product_versions_projects_project_id",
                table: "product_versions",
                column: "project_id",
                principalTable: "projects",
                principalColumn: "project_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_proposal_items_product_versions_approved_product_version_id",
                table: "proposal_items",
                column: "approved_product_version_id",
                principalTable: "product_versions",
                principalColumn: "product_version_id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_product_versions_projects_project_id",
                table: "product_versions");

            migrationBuilder.DropForeignKey(
                name: "FK_proposal_items_product_versions_approved_product_version_id",
                table: "proposal_items");

            migrationBuilder.DropTable(
                name: "proposal_scene_variants");

            migrationBuilder.DropIndex(
                name: "IX_proposal_items_approved_product_version_id",
                table: "proposal_items");

            migrationBuilder.DropIndex(
                name: "IX_product_versions_project_id",
                table: "product_versions");

            migrationBuilder.DropColumn(
                name: "approved_product_version_id",
                table: "proposal_items");

            migrationBuilder.DropColumn(
                name: "total_price_snapshot",
                table: "proposal_items");

            migrationBuilder.DropColumn(
                name: "dimension_unit",
                table: "product_versions");

            migrationBuilder.DropColumn(
                name: "project_id",
                table: "product_versions");

            migrationBuilder.DropColumn(
                name: "cancelled_at",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "currency",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "paid_amount",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "remaining_amount",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "display_order",
                table: "file_links");

            migrationBuilder.DropColumn(
                name: "is_primary",
                table: "file_links");

            migrationBuilder.Sql(
                """
                UPDATE payments
                SET status = CASE
                    WHEN status::text = 'PROCESSING' THEN 'PENDING'::payment_status
                    WHEN status::text = 'PARTIALLY_PAID' THEN 'PAID'::payment_status
                    ELSE status
                END
                WHERE status::text IN ('PROCESSING', 'PARTIALLY_PAID');

                ALTER TABLE payments ALTER COLUMN status DROP DEFAULT;
                ALTER TYPE payment_status RENAME TO payment_status_new;
                CREATE TYPE payment_status AS ENUM ('PENDING', 'PAID', 'FAILED', 'REFUNDED', 'CANCELLED');
                ALTER TABLE payments
                    ALTER COLUMN status TYPE payment_status
                    USING status::text::payment_status;
                ALTER TABLE payments ALTER COLUMN status SET DEFAULT 'PENDING'::payment_status;
                DROP TYPE payment_status_new;

                ALTER TABLE project_areas ALTER COLUMN status DROP DEFAULT;
                ALTER TYPE project_area_status RENAME TO project_area_status_new;
                CREATE TYPE project_area_status AS ENUM ('DRAFT', 'NEED_MEASUREMENT', 'MEASURED', 'VERIFIED', 'DESIGNING', 'DESIGNED', 'APPROVED', 'CANCELLED');
                ALTER TABLE project_areas
                    ALTER COLUMN status TYPE project_area_status
                    USING status::text::project_area_status;
                ALTER TABLE project_areas ALTER COLUMN status SET DEFAULT 'DRAFT'::project_area_status;
                DROP TYPE project_area_status_new;

                DROP TYPE proposal_scene_variant_status;
                DROP TYPE proposal_scene_variant_type;
                """);

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:account_status", "ACTIVE,INACTIVE,SUSPENDED")
                .Annotation("Npgsql:Enum:customization_status", "SUBMITTED,DESIGN_REVIEWING,WAITING_FOR_DESIGN_APPROVAL,DESIGN_REVISION_REQUESTED,PRODUCTION_REVIEWING,NOT_FEASIBLE,ACCEPTED,REJECTED_BY_CUSTOMER,CANCELLED")
                .Annotation("Npgsql:Enum:file_status", "ACTIVE,ARCHIVED")
                .Annotation("Npgsql:Enum:file_type", "SPACE_IMAGE,FLOOR_PLAN,REFERENCE_IMAGE,BRAND_ASSET,CAD_FILE,PDF_DRAWING,MEASUREMENT_REPORT,LIDAR_SCAN,MODEL_3D,TEXTURE,PRODUCT_PREVIEW,PROPOSAL_PREVIEW,PROPOSAL_FILE,QUOTATION_FILE,ORDER_DOCUMENT,PRODUCTION_FILE,DELIVERY_PHOTO,DELIVERY_NOTE,REVIEW_IMAGE,OTHER")
                .Annotation("Npgsql:Enum:file_visibility", "CUSTOMER_VISIBLE,STAFF_ONLY,PRIVATE")
                .Annotation("Npgsql:Enum:notification_status", "UNREAD,READ")
                .Annotation("Npgsql:Enum:order_item_status", "PENDING,IN_PRODUCTION,READY,UNAVAILABLE,DELIVERED,CANCELLED")
                .Annotation("Npgsql:Enum:order_status", "CREATED,DEPOSIT_PENDING,DEPOSIT_PAID,IN_PRODUCTION,PRODUCTION_PARTIALLY_FAILED,PRODUCTION_COMPLETED,READY_FOR_DELIVERY,DELIVERY_SCHEDULED,DELIVERING,DELIVERED,FINAL_PAYMENT_PENDING,COMPLETED,CANCELLED")
                .Annotation("Npgsql:Enum:payment_status", "PENDING,PAID,FAILED,REFUNDED,CANCELLED")
                .Annotation("Npgsql:Enum:payment_transaction_status", "PENDING,SUCCESS,FAILED,CANCELLED")
                .Annotation("Npgsql:Enum:payment_transaction_type", "CHARGE,REFUND,ADJUSTMENT")
                .Annotation("Npgsql:Enum:payment_type", "DEPOSIT,REMAINING_PAYMENT,FULL_PAYMENT,MEASUREMENT_FEE,DESIGN_FEE,CUSTOMIZATION_FEE,DELIVERY_FEE,CANCELLATION_FEE,REFUND,OTHER")
                .Annotation("Npgsql:Enum:product_status", "ACTIVE,INACTIVE,ARCHIVED")
                .Annotation("Npgsql:Enum:product_version_type", "STANDARD,CUSTOM,PROJECT_SPECIFIC")
                .Annotation("Npgsql:Enum:production_item_status", "PENDING,IN_PRODUCTION,COMPLETED,BLOCKED,CANCELLED")
                .Annotation("Npgsql:Enum:production_request_status", "PENDING_REVIEW,FEASIBLE,IN_PRODUCTION,COMPLETED,BLOCKED,CANCELLED")
                .Annotation("Npgsql:Enum:project_area_status", "DRAFT,NEED_MEASUREMENT,MEASURED,VERIFIED,DESIGNING,DESIGNED,APPROVED,CANCELLED")
                .Annotation("Npgsql:Enum:project_area_type", "STORE,FLOOR,ROOM,ZONE,OUTDOOR_AREA,OTHER")
                .Annotation("Npgsql:Enum:project_chat_message_type", "TEXT,FILE,SYSTEM")
                .Annotation("Npgsql:Enum:project_chat_status", "OPEN,CLOSED,ARCHIVED")
                .Annotation("Npgsql:Enum:project_chat_type", "SALES,DESIGNER,PRODUCTION,DELIVERY,GENERAL,INTERNAL")
                .Annotation("Npgsql:Enum:project_schedule_status", "PENDING_CONFIRMATION,CONFIRMED,COMPLETED,CANCELLED")
                .Annotation("Npgsql:Enum:project_schedule_type", "MEASUREMENT,CONSULTATION,DESIGN_REVIEW,DELIVERY,HANDOVER,OTHER")
                .Annotation("Npgsql:Enum:project_status", "SUBMITTED,IN_CONSULTATION,NEED_BASIC_INFORMATION,WAITING_FOR_DESIGNER_ASSIGNMENT,MEASUREMENT_REQUIRED,SPACE_VERIFIED,PROPOSAL_DRAFTING,WAITING_FOR_CUSTOMER_REVIEW,REVISION_REQUESTED,PROPOSAL_SELECTED,QUOTATION_SENT,QUOTATION_REVISION_REQUESTED,ORDER_CONFIRMED,IN_PRODUCTION,PRODUCTION_BLOCKED,READY_FOR_DELIVERY,DELIVERING,DELIVERED,COMPLETED,REJECTED")
                .Annotation("Npgsql:Enum:proposal_scene_type", "TWO_D,THREE_D")
                .Annotation("Npgsql:Enum:proposal_status", "DRAFT,PUBLISHED,VIEWED,SELECTED,REVISION_REQUESTED,REJECTED,ARCHIVED")
                .Annotation("Npgsql:Enum:quotation_status", "DRAFT,SENT,REVISION_REQUESTED,REVISED,ACCEPTED,REJECTED,EXPIRED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:account_status", "ACTIVE,INACTIVE,SUSPENDED")
                .OldAnnotation("Npgsql:Enum:customization_status", "SUBMITTED,DESIGN_REVIEWING,WAITING_FOR_DESIGN_APPROVAL,DESIGN_REVISION_REQUESTED,PRODUCTION_REVIEWING,NOT_FEASIBLE,ACCEPTED,REJECTED_BY_CUSTOMER,CANCELLED")
                .OldAnnotation("Npgsql:Enum:file_status", "ACTIVE,ARCHIVED")
                .OldAnnotation("Npgsql:Enum:file_type", "SPACE_IMAGE,FLOOR_PLAN,REFERENCE_IMAGE,BRAND_ASSET,CAD_FILE,PDF_DRAWING,MEASUREMENT_REPORT,LIDAR_SCAN,MODEL_3D,TEXTURE,PRODUCT_PREVIEW,PROPOSAL_PREVIEW,PROPOSAL_FILE,QUOTATION_FILE,ORDER_DOCUMENT,PRODUCTION_FILE,DELIVERY_PHOTO,DELIVERY_NOTE,REVIEW_IMAGE,OTHER")
                .OldAnnotation("Npgsql:Enum:file_visibility", "CUSTOMER_VISIBLE,STAFF_ONLY,PRIVATE")
                .OldAnnotation("Npgsql:Enum:notification_status", "UNREAD,READ")
                .OldAnnotation("Npgsql:Enum:order_item_status", "PENDING,IN_PRODUCTION,READY,UNAVAILABLE,DELIVERED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:order_status", "CREATED,DEPOSIT_PENDING,DEPOSIT_PAID,IN_PRODUCTION,PRODUCTION_PARTIALLY_FAILED,PRODUCTION_COMPLETED,READY_FOR_DELIVERY,DELIVERY_SCHEDULED,DELIVERING,DELIVERED,FINAL_PAYMENT_PENDING,COMPLETED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:payment_status", "PENDING,PAID,FAILED,REFUNDED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:payment_transaction_status", "PENDING,SUCCESS,FAILED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:payment_transaction_type", "CHARGE,REFUND,ADJUSTMENT")
                .OldAnnotation("Npgsql:Enum:payment_type", "DEPOSIT,REMAINING_PAYMENT,FULL_PAYMENT,MEASUREMENT_FEE,DESIGN_FEE,CUSTOMIZATION_FEE,DELIVERY_FEE,CANCELLATION_FEE,REFUND,OTHER")
                .OldAnnotation("Npgsql:Enum:product_status", "ACTIVE,INACTIVE,ARCHIVED")
                .OldAnnotation("Npgsql:Enum:product_version_type", "STANDARD,CUSTOM,PROJECT_SPECIFIC")
                .OldAnnotation("Npgsql:Enum:production_item_status", "PENDING,IN_PRODUCTION,COMPLETED,BLOCKED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:production_request_status", "PENDING_REVIEW,FEASIBLE,IN_PRODUCTION,COMPLETED,BLOCKED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:project_area_status", "DRAFT,NEED_MEASUREMENT,MEASURED,VERIFIED,DESIGNING,DESIGNED,APPROVED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:project_area_type", "STORE,FLOOR,ROOM,ZONE,OUTDOOR_AREA,OTHER")
                .OldAnnotation("Npgsql:Enum:project_chat_message_type", "TEXT,FILE,SYSTEM")
                .OldAnnotation("Npgsql:Enum:project_chat_status", "OPEN,CLOSED,ARCHIVED")
                .OldAnnotation("Npgsql:Enum:project_chat_type", "SALES,DESIGNER,PRODUCTION,DELIVERY,GENERAL,INTERNAL")
                .OldAnnotation("Npgsql:Enum:project_schedule_status", "PENDING_CONFIRMATION,CONFIRMED,COMPLETED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:project_schedule_type", "MEASUREMENT,CONSULTATION,DESIGN_REVIEW,DELIVERY,HANDOVER,OTHER")
                .OldAnnotation("Npgsql:Enum:project_status", "SUBMITTED,IN_CONSULTATION,NEED_BASIC_INFORMATION,WAITING_FOR_DESIGNER_ASSIGNMENT,MEASUREMENT_REQUIRED,SPACE_VERIFIED,PROPOSAL_DRAFTING,WAITING_FOR_CUSTOMER_REVIEW,REVISION_REQUESTED,PROPOSAL_SELECTED,QUOTATION_SENT,QUOTATION_REVISION_REQUESTED,ORDER_CONFIRMED,IN_PRODUCTION,PRODUCTION_BLOCKED,READY_FOR_DELIVERY,DELIVERING,DELIVERED,COMPLETED,REJECTED")
                .Annotation("Npgsql:Enum:proposal_scene_variant_status", "DRAFT,SUBMITTED,ACCEPTED,REJECTED,APPLIED")
                .Annotation("Npgsql:Enum:proposal_scene_variant_type", "CUSTOMER_SUGGESTION,DESIGNER_REVISION")
                .OldAnnotation("Npgsql:Enum:proposal_scene_type", "TWO_D,THREE_D")
                .OldAnnotation("Npgsql:Enum:proposal_scene_variant_status", "DRAFT,SUBMITTED,ACCEPTED,REJECTED,APPLIED")
                .OldAnnotation("Npgsql:Enum:proposal_scene_variant_type", "CUSTOMER_SUGGESTION,DESIGNER_REVISION")
                .OldAnnotation("Npgsql:Enum:proposal_status", "DRAFT,PUBLISHED,VIEWED,SELECTED,REVISION_REQUESTED,REJECTED,ARCHIVED")
                .OldAnnotation("Npgsql:Enum:quotation_status", "DRAFT,SENT,REVISION_REQUESTED,REVISED,ACCEPTED,REJECTED,EXPIRED,CANCELLED");

            migrationBuilder.AddColumn<string>(
                name: "finish",
                table: "proposal_items",
                type: "varchar(100)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payment_method",
                table: "payments",
                type: "varchar(50)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "transaction_reference",
                table: "payments",
                type: "varchar(150)",
                nullable: true);
        }
    }
}
