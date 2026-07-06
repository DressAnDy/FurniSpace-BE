using System;
using FurniSpace.Domain.Enums;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurniSpace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AlignCustomizationQuotationSchemaWithDbDiagram : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE customization_requests ALTER COLUMN status DROP DEFAULT;

                CREATE TYPE customization_status_new AS ENUM (
                    'SUBMITTED',
                    'DESIGN_REVIEWING',
                    'PRODUCTION_REVIEWING',
                    'WAITING_FOR_CUSTOMER_FINAL_APPROVAL',
                    'NOT_FEASIBLE',
                    'ACCEPTED',
                    'REJECTED_BY_CUSTOMER',
                    'CANCELLED'
                );

                ALTER TABLE customization_requests
                ALTER COLUMN status TYPE customization_status_new
                USING (
                    CASE status::text
                        WHEN 'WAITING_FOR_DESIGN_APPROVAL' THEN 'WAITING_FOR_CUSTOMER_FINAL_APPROVAL'
                        WHEN 'DESIGN_REVISION_REQUESTED' THEN 'WAITING_FOR_CUSTOMER_FINAL_APPROVAL'
                        ELSE status::text
                    END
                )::customization_status_new;

                DROP TYPE customization_status;
                ALTER TYPE customization_status_new RENAME TO customization_status;
                ALTER TABLE customization_requests ALTER COLUMN status SET DEFAULT 'SUBMITTED'::customization_status;
                """);

            migrationBuilder.DropColumn(
                name: "customization_fee",
                table: "quotations");

            migrationBuilder.DropColumn(
                name: "delivery_fee",
                table: "quotations");

            migrationBuilder.DropColumn(
                name: "service_fee",
                table: "quotations");

            migrationBuilder.RenameColumn(
                name: "customization_fee",
                table: "quotation_items",
                newName: "customization_additional_cost");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:account_status", "ACTIVE,INACTIVE,SUSPENDED")
                .Annotation("Npgsql:Enum:customization_status", "SUBMITTED,DESIGN_REVIEWING,PRODUCTION_REVIEWING,WAITING_FOR_CUSTOMER_FINAL_APPROVAL,NOT_FEASIBLE,ACCEPTED,REJECTED_BY_CUSTOMER,CANCELLED")
                .Annotation("Npgsql:Enum:file_status", "ACTIVE,ARCHIVED")
                .Annotation("Npgsql:Enum:file_type", "SPACE_IMAGE,FLOOR_PLAN,REFERENCE_IMAGE,BRAND_ASSET,CAD_FILE,PDF_DRAWING,MEASUREMENT_REPORT,LIDAR_SCAN,MODEL_3D,TEXTURE,PRODUCT_PREVIEW,PROPOSAL_PREVIEW,PROPOSAL_FILE,QUOTATION_FILE,ORDER_DOCUMENT,PRODUCTION_FILE,DELIVERY_PHOTO,DELIVERY_NOTE,REVIEW_IMAGE,OTHER")
                .Annotation("Npgsql:Enum:file_visibility", "CUSTOMER_VISIBLE,STAFF_ONLY,PRIVATE")
                .Annotation("Npgsql:Enum:notification_status", "UNREAD,READ")
                .Annotation("Npgsql:Enum:order_item_status", "PENDING,IN_PRODUCTION,READY,UNAVAILABLE,DELIVERED,CANCELLED")
                .Annotation("Npgsql:Enum:order_status", "CREATED,DEPOSIT_PENDING,DEPOSIT_PAID,IN_PRODUCTION,PRODUCTION_PARTIALLY_FAILED,PRODUCTION_COMPLETED,READY_FOR_DELIVERY,DELIVERY_SCHEDULED,DELIVERING,DELIVERED,FINAL_PAYMENT_PENDING,COMPLETED,CANCELLED")
                .Annotation("Npgsql:Enum:payment_status", "PENDING,PROCESSING,PAID,PARTIALLY_PAID,FAILED,CANCELLED,REFUNDED")
                .Annotation("Npgsql:Enum:payment_transaction_status", "PENDING,SUCCESS,FAILED,CANCELLED")
                .Annotation("Npgsql:Enum:payment_transaction_type", "CHARGE,REFUND,ADJUSTMENT")
                .Annotation("Npgsql:Enum:payment_type", "DEPOSIT,REMAINING_PAYMENT,FULL_PAYMENT,MEASUREMENT_FEE,DESIGN_FEE,CUSTOMIZATION_FEE,DELIVERY_FEE,CANCELLATION_FEE,REFUND,OTHER")
                .Annotation("Npgsql:Enum:product_status", "ACTIVE,INACTIVE,ARCHIVED")
                .Annotation("Npgsql:Enum:product_version_type", "STANDARD,CUSTOM,PROJECT_SPECIFIC")
                .Annotation("Npgsql:Enum:production_item_status", "PENDING,IN_PRODUCTION,COMPLETED,BLOCKED,CANCELLED")
                .Annotation("Npgsql:Enum:production_request_status", "PENDING_REVIEW,FEASIBLE,IN_PRODUCTION,COMPLETED,BLOCKED,CANCELLED")
                .Annotation("Npgsql:Enum:project_area_status", "DRAFT,NEED_MEASUREMENT,MEASURED,VERIFIED,CANCELLED")
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
                .Annotation("Npgsql:Enum:quotation_item_type", "PRODUCT_ITEM,MANUAL_ITEM")
                .Annotation("Npgsql:Enum:quotation_status", "DRAFT,SENT,REVISION_REQUESTED,REVISED,ACCEPTED,REJECTED,EXPIRED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:account_status", "ACTIVE,INACTIVE,SUSPENDED")
                .OldAnnotation("Npgsql:Enum:customization_status", "SUBMITTED,DESIGN_REVIEWING,PRODUCTION_REVIEWING,WAITING_FOR_CUSTOMER_FINAL_APPROVAL,NOT_FEASIBLE,ACCEPTED,REJECTED_BY_CUSTOMER,CANCELLED")
                .OldAnnotation("Npgsql:Enum:file_status", "ACTIVE,ARCHIVED")
                .OldAnnotation("Npgsql:Enum:file_type", "SPACE_IMAGE,FLOOR_PLAN,REFERENCE_IMAGE,BRAND_ASSET,CAD_FILE,PDF_DRAWING,MEASUREMENT_REPORT,LIDAR_SCAN,MODEL_3D,TEXTURE,PRODUCT_PREVIEW,PROPOSAL_PREVIEW,PROPOSAL_FILE,QUOTATION_FILE,ORDER_DOCUMENT,PRODUCTION_FILE,DELIVERY_PHOTO,DELIVERY_NOTE,REVIEW_IMAGE,OTHER")
                .OldAnnotation("Npgsql:Enum:file_visibility", "CUSTOMER_VISIBLE,STAFF_ONLY,PRIVATE")
                .OldAnnotation("Npgsql:Enum:notification_status", "UNREAD,READ")
                .OldAnnotation("Npgsql:Enum:order_item_status", "PENDING,IN_PRODUCTION,READY,UNAVAILABLE,DELIVERED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:order_status", "CREATED,DEPOSIT_PENDING,DEPOSIT_PAID,IN_PRODUCTION,PRODUCTION_PARTIALLY_FAILED,PRODUCTION_COMPLETED,READY_FOR_DELIVERY,DELIVERY_SCHEDULED,DELIVERING,DELIVERED,FINAL_PAYMENT_PENDING,COMPLETED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:payment_status", "PENDING,PROCESSING,PAID,PARTIALLY_PAID,FAILED,CANCELLED,REFUNDED")
                .OldAnnotation("Npgsql:Enum:payment_transaction_status", "PENDING,SUCCESS,FAILED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:payment_transaction_type", "CHARGE,REFUND,ADJUSTMENT")
                .OldAnnotation("Npgsql:Enum:payment_type", "DEPOSIT,REMAINING_PAYMENT,FULL_PAYMENT,MEASUREMENT_FEE,DESIGN_FEE,CUSTOMIZATION_FEE,DELIVERY_FEE,CANCELLATION_FEE,REFUND,OTHER")
                .OldAnnotation("Npgsql:Enum:product_status", "ACTIVE,INACTIVE,ARCHIVED")
                .OldAnnotation("Npgsql:Enum:product_version_type", "STANDARD,CUSTOM,PROJECT_SPECIFIC")
                .OldAnnotation("Npgsql:Enum:production_item_status", "PENDING,IN_PRODUCTION,COMPLETED,BLOCKED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:production_request_status", "PENDING_REVIEW,FEASIBLE,IN_PRODUCTION,COMPLETED,BLOCKED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:project_area_status", "DRAFT,NEED_MEASUREMENT,MEASURED,VERIFIED,CANCELLED")
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

            migrationBuilder.AddColumn<string>(
                name: "reject_reason",
                table: "quotations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "revision_reason",
                table: "quotations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "customization_note",
                table: "quotation_items",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "quotation_items",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_customized",
                table: "quotation_items",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "item_name",
                table: "quotation_items",
                type: "varchar(150)",
                nullable: true);

            migrationBuilder.AddColumn<QuotationItemType>(
                name: "item_type",
                table: "quotation_items",
                type: "quotation_item_type",
                nullable: true,
                defaultValueSql: "'PRODUCT_ITEM'::quotation_item_type");

            migrationBuilder.AlterColumn<Guid>(
                name: "proposal_item_id",
                table: "customization_requests",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "proposal_id",
                table: "customization_requests",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "additional_cost_reason",
                table: "customization_requests",
                type: "text",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_customization_requests_additional_cost_reason",
                table: "customization_requests",
                sql: "estimated_additional_cost IS NULL OR estimated_additional_cost <= 0 OR additional_cost_reason IS NOT NULL AND btrim(additional_cost_reason) <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_customization_requests_additional_cost_reason",
                table: "customization_requests");

            migrationBuilder.Sql(
                """
                ALTER TABLE customization_requests ALTER COLUMN status DROP DEFAULT;

                CREATE TYPE customization_status_old AS ENUM (
                    'SUBMITTED',
                    'DESIGN_REVIEWING',
                    'WAITING_FOR_DESIGN_APPROVAL',
                    'DESIGN_REVISION_REQUESTED',
                    'PRODUCTION_REVIEWING',
                    'NOT_FEASIBLE',
                    'ACCEPTED',
                    'REJECTED_BY_CUSTOMER',
                    'CANCELLED'
                );

                ALTER TABLE customization_requests
                ALTER COLUMN status TYPE customization_status_old
                USING (
                    CASE status::text
                        WHEN 'WAITING_FOR_CUSTOMER_FINAL_APPROVAL' THEN 'WAITING_FOR_DESIGN_APPROVAL'
                        ELSE status::text
                    END
                )::customization_status_old;

                DROP TYPE customization_status;
                ALTER TYPE customization_status_old RENAME TO customization_status;
                ALTER TABLE customization_requests ALTER COLUMN status SET DEFAULT 'SUBMITTED'::customization_status;
                """);

            migrationBuilder.DropColumn(
                name: "reject_reason",
                table: "quotations");

            migrationBuilder.DropColumn(
                name: "revision_reason",
                table: "quotations");

            migrationBuilder.DropColumn(
                name: "customization_note",
                table: "quotation_items");

            migrationBuilder.DropColumn(
                name: "description",
                table: "quotation_items");

            migrationBuilder.DropColumn(
                name: "is_customized",
                table: "quotation_items");

            migrationBuilder.DropColumn(
                name: "item_name",
                table: "quotation_items");

            migrationBuilder.DropColumn(
                name: "item_type",
                table: "quotation_items");

            migrationBuilder.DropColumn(
                name: "additional_cost_reason",
                table: "customization_requests");

            migrationBuilder.RenameColumn(
                name: "customization_additional_cost",
                table: "quotation_items",
                newName: "customization_fee");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:account_status", "ACTIVE,INACTIVE,SUSPENDED")
                .Annotation("Npgsql:Enum:customization_status", "SUBMITTED,DESIGN_REVIEWING,WAITING_FOR_DESIGN_APPROVAL,DESIGN_REVISION_REQUESTED,PRODUCTION_REVIEWING,NOT_FEASIBLE,ACCEPTED,REJECTED_BY_CUSTOMER,CANCELLED")
                .Annotation("Npgsql:Enum:file_status", "ACTIVE,ARCHIVED")
                .Annotation("Npgsql:Enum:file_type", "SPACE_IMAGE,FLOOR_PLAN,REFERENCE_IMAGE,BRAND_ASSET,CAD_FILE,PDF_DRAWING,MEASUREMENT_REPORT,LIDAR_SCAN,MODEL_3D,TEXTURE,PRODUCT_PREVIEW,PROPOSAL_PREVIEW,PROPOSAL_FILE,QUOTATION_FILE,ORDER_DOCUMENT,PRODUCTION_FILE,DELIVERY_PHOTO,DELIVERY_NOTE,REVIEW_IMAGE,OTHER")
                .Annotation("Npgsql:Enum:file_visibility", "CUSTOMER_VISIBLE,STAFF_ONLY,PRIVATE")
                .Annotation("Npgsql:Enum:notification_status", "UNREAD,READ")
                .Annotation("Npgsql:Enum:order_item_status", "PENDING,IN_PRODUCTION,READY,UNAVAILABLE,DELIVERED,CANCELLED")
                .Annotation("Npgsql:Enum:order_status", "CREATED,DEPOSIT_PENDING,DEPOSIT_PAID,IN_PRODUCTION,PRODUCTION_PARTIALLY_FAILED,PRODUCTION_COMPLETED,READY_FOR_DELIVERY,DELIVERY_SCHEDULED,DELIVERING,DELIVERED,FINAL_PAYMENT_PENDING,COMPLETED,CANCELLED")
                .Annotation("Npgsql:Enum:payment_status", "PENDING,PROCESSING,PAID,PARTIALLY_PAID,FAILED,CANCELLED,REFUNDED")
                .Annotation("Npgsql:Enum:payment_transaction_status", "PENDING,SUCCESS,FAILED,CANCELLED")
                .Annotation("Npgsql:Enum:payment_transaction_type", "CHARGE,REFUND,ADJUSTMENT")
                .Annotation("Npgsql:Enum:payment_type", "DEPOSIT,REMAINING_PAYMENT,FULL_PAYMENT,MEASUREMENT_FEE,DESIGN_FEE,CUSTOMIZATION_FEE,DELIVERY_FEE,CANCELLATION_FEE,REFUND,OTHER")
                .Annotation("Npgsql:Enum:product_status", "ACTIVE,INACTIVE,ARCHIVED")
                .Annotation("Npgsql:Enum:product_version_type", "STANDARD,CUSTOM,PROJECT_SPECIFIC")
                .Annotation("Npgsql:Enum:production_item_status", "PENDING,IN_PRODUCTION,COMPLETED,BLOCKED,CANCELLED")
                .Annotation("Npgsql:Enum:production_request_status", "PENDING_REVIEW,FEASIBLE,IN_PRODUCTION,COMPLETED,BLOCKED,CANCELLED")
                .Annotation("Npgsql:Enum:project_area_status", "DRAFT,NEED_MEASUREMENT,MEASURED,VERIFIED,CANCELLED")
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
                .OldAnnotation("Npgsql:Enum:payment_status", "PENDING,PROCESSING,PAID,PARTIALLY_PAID,FAILED,CANCELLED,REFUNDED")
                .OldAnnotation("Npgsql:Enum:payment_transaction_status", "PENDING,SUCCESS,FAILED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:payment_transaction_type", "CHARGE,REFUND,ADJUSTMENT")
                .OldAnnotation("Npgsql:Enum:payment_type", "DEPOSIT,REMAINING_PAYMENT,FULL_PAYMENT,MEASUREMENT_FEE,DESIGN_FEE,CUSTOMIZATION_FEE,DELIVERY_FEE,CANCELLATION_FEE,REFUND,OTHER")
                .OldAnnotation("Npgsql:Enum:product_status", "ACTIVE,INACTIVE,ARCHIVED")
                .OldAnnotation("Npgsql:Enum:product_version_type", "STANDARD,CUSTOM,PROJECT_SPECIFIC")
                .OldAnnotation("Npgsql:Enum:production_item_status", "PENDING,IN_PRODUCTION,COMPLETED,BLOCKED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:production_request_status", "PENDING_REVIEW,FEASIBLE,IN_PRODUCTION,COMPLETED,BLOCKED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:project_area_status", "DRAFT,NEED_MEASUREMENT,MEASURED,VERIFIED,CANCELLED")
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
                .OldAnnotation("Npgsql:Enum:quotation_item_type", "PRODUCT_ITEM,MANUAL_ITEM")
                .OldAnnotation("Npgsql:Enum:quotation_status", "DRAFT,SENT,REVISION_REQUESTED,REVISED,ACCEPTED,REJECTED,EXPIRED,CANCELLED");

            migrationBuilder.AddColumn<decimal>(
                name: "customization_fee",
                table: "quotations",
                type: "numeric(12,2)",
                nullable: true,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "delivery_fee",
                table: "quotations",
                type: "numeric(12,2)",
                nullable: true,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "service_fee",
                table: "quotations",
                type: "numeric(12,2)",
                nullable: true,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<Guid>(
                name: "proposal_item_id",
                table: "customization_requests",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "proposal_id",
                table: "customization_requests",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");
        }
    }
}
