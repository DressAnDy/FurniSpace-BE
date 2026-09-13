using System;
using FurniSpace.Domain.Enums;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurniSpace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FileStorageDbmlAlignment : Migration
    {
        private static readonly string[] FileLinkReferenceIndexColumns = ["reference_type", "reference_id"];
        private static readonly string[] FileLinkUniqueReferenceColumns = ["file_id", "reference_type", "reference_id", "file_type"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "file_name",
                table: "files",
                newName: "stored_file_name");

            migrationBuilder.RenameIndex(
                name: "IX_files_uploaded_by",
                table: "files",
                newName: "idx_files_uploaded_by");

            migrationBuilder.RenameIndex(
                name: "IX_file_links_file_id",
                table: "file_links",
                newName: "idx_file_links_file_id");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "files");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "files");

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
                .OldAnnotation("Npgsql:Enum:file_type", "SPACE_IMAGE,FLOOR_PLAN,REFERENCE_IMAGE,BRAND_ASSET,CAD_FILE,PDF_DRAWING,MEASUREMENT_REPORT,LIDAR_SCAN,MODEL_3D,TEXTURE,PRODUCT_PREVIEW,PROPOSAL_PREVIEW,PROPOSAL_FILE,QUOTATION_FILE,ORDER_DOCUMENT,PRODUCTION_FILE,DELIVERY_PHOTO,DELIVERY_NOTE,REVIEW_IMAGE,OTHER")
                .OldAnnotation("Npgsql:Enum:file_visibility", "CUSTOMER_VISIBLE,STAFF_ONLY,PRIVATE")
                .OldAnnotation("Npgsql:Enum:notification_status", "UNREAD,READ")
                .OldAnnotation("Npgsql:Enum:order_item_status", "PENDING,IN_PRODUCTION,READY,UNAVAILABLE,DELIVERED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:order_status", "CREATED,DEPOSIT_PENDING,DEPOSIT_PAID,IN_PRODUCTION,PRODUCTION_PARTIALLY_FAILED,PRODUCTION_COMPLETED,READY_FOR_DELIVERY,DELIVERY_SCHEDULED,DELIVERING,DELIVERED,FINAL_PAYMENT_PENDING,COMPLETED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:payment_status", "PENDING,PAID,FAILED,REFUNDED,CANCELLED")
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
                .OldAnnotation("Npgsql:Enum:proposal_status", "DRAFT,PUBLISHED,VIEWED,SELECTED,REVISION_REQUESTED,REJECTED,ARCHIVED")
                .OldAnnotation("Npgsql:Enum:quotation_status", "DRAFT,SENT,REVISION_REQUESTED,REVISED,ACCEPTED,REJECTED,EXPIRED,CANCELLED");

            migrationBuilder.AlterColumn<Guid>(
                name: "uploaded_by",
                table: "files",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "mime_type",
                table: "files",
                type: "varchar(100)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(100)",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "file_size_bytes",
                table: "files",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "archived_at",
                table: "files",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "checksum",
                table: "files",
                type: "varchar(255)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "file_extension",
                table: "files",
                type: "varchar(20)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "original_file_name",
                table: "files",
                type: "varchar(255)",
                nullable: false);

            migrationBuilder.AddColumn<FileStatus>(
                name: "status",
                table: "files",
                type: "file_status",
                nullable: true,
                defaultValueSql: "'ACTIVE'::file_status");

            migrationBuilder.AddColumn<string>(
                name: "storage_path",
                table: "files",
                type: "text",
                nullable: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "uploaded_at",
                table: "files",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AlterColumn<FileVisibility>(
                name: "visibility",
                table: "file_links",
                type: "file_visibility",
                nullable: true,
                defaultValueSql: "'CUSTOMER_VISIBLE'::file_visibility",
                oldClrType: typeof(FileVisibility),
                oldType: "file_visibility",
                oldNullable: true,
                oldDefaultValueSql: "'STAFF_ONLY'::file_visibility");

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                table: "file_links",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_files_status",
                table: "files",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "uq_files_storage_path",
                table: "files",
                column: "storage_path",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_file_links_reference",
                table: "file_links",
                columns: FileLinkReferenceIndexColumns);

            migrationBuilder.CreateIndex(
                name: "IX_file_links_created_by",
                table: "file_links",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "uq_file_links_unique_reference",
                table: "file_links",
                columns: FileLinkUniqueReferenceColumns,
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_file_links_accounts_created_by",
                table: "file_links",
                column: "created_by",
                principalTable: "accounts",
                principalColumn: "account_id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_file_links_accounts_created_by",
                table: "file_links");

            migrationBuilder.DropIndex(
                name: "idx_files_status",
                table: "files");

            migrationBuilder.DropIndex(
                name: "uq_files_storage_path",
                table: "files");

            migrationBuilder.DropIndex(
                name: "idx_file_links_reference",
                table: "file_links");

            migrationBuilder.DropIndex(
                name: "IX_file_links_created_by",
                table: "file_links");

            migrationBuilder.DropIndex(
                name: "uq_file_links_unique_reference",
                table: "file_links");

            migrationBuilder.DropColumn(
                name: "checksum",
                table: "files");

            migrationBuilder.DropColumn(
                name: "archived_at",
                table: "files");

            migrationBuilder.DropColumn(
                name: "file_extension",
                table: "files");

            migrationBuilder.DropColumn(
                name: "original_file_name",
                table: "files");

            migrationBuilder.DropColumn(
                name: "status",
                table: "files");

            migrationBuilder.DropColumn(
                name: "storage_path",
                table: "files");

            migrationBuilder.DropColumn(
                name: "uploaded_at",
                table: "files");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "file_links");

            migrationBuilder.RenameColumn(
                name: "stored_file_name",
                table: "files",
                newName: "file_name");

            migrationBuilder.RenameIndex(
                name: "idx_files_uploaded_by",
                table: "files",
                newName: "IX_files_uploaded_by");

            migrationBuilder.RenameIndex(
                name: "idx_file_links_file_id",
                table: "file_links",
                newName: "IX_file_links_file_id");

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "files",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "files",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:account_status", "ACTIVE,INACTIVE,SUSPENDED")
                .Annotation("Npgsql:Enum:customization_status", "SUBMITTED,DESIGN_REVIEWING,WAITING_FOR_DESIGN_APPROVAL,DESIGN_REVISION_REQUESTED,PRODUCTION_REVIEWING,NOT_FEASIBLE,ACCEPTED,REJECTED_BY_CUSTOMER,CANCELLED")
                .Annotation("Npgsql:Enum:file_type", "SPACE_IMAGE,FLOOR_PLAN,REFERENCE_IMAGE,BRAND_ASSET,CAD_FILE,PDF_DRAWING,MEASUREMENT_REPORT,LIDAR_SCAN,MODEL_3D,TEXTURE,PRODUCT_PREVIEW,PROPOSAL_PREVIEW,PROPOSAL_FILE,QUOTATION_FILE,ORDER_DOCUMENT,PRODUCTION_FILE,DELIVERY_PHOTO,DELIVERY_NOTE,REVIEW_IMAGE,OTHER")
                .Annotation("Npgsql:Enum:file_visibility", "CUSTOMER_VISIBLE,STAFF_ONLY,PRIVATE")
                .Annotation("Npgsql:Enum:notification_status", "UNREAD,READ")
                .Annotation("Npgsql:Enum:order_item_status", "PENDING,IN_PRODUCTION,READY,UNAVAILABLE,DELIVERED,CANCELLED")
                .Annotation("Npgsql:Enum:order_status", "CREATED,DEPOSIT_PENDING,DEPOSIT_PAID,IN_PRODUCTION,PRODUCTION_PARTIALLY_FAILED,PRODUCTION_COMPLETED,READY_FOR_DELIVERY,DELIVERY_SCHEDULED,DELIVERING,DELIVERED,FINAL_PAYMENT_PENDING,COMPLETED,CANCELLED")
                .Annotation("Npgsql:Enum:payment_status", "PENDING,PAID,FAILED,REFUNDED,CANCELLED")
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
                .OldAnnotation("Npgsql:Enum:proposal_status", "DRAFT,PUBLISHED,VIEWED,SELECTED,REVISION_REQUESTED,REJECTED,ARCHIVED")
                .OldAnnotation("Npgsql:Enum:quotation_status", "DRAFT,SENT,REVISION_REQUESTED,REVISED,ACCEPTED,REJECTED,EXPIRED,CANCELLED");

            migrationBuilder.AlterColumn<Guid>(
                name: "uploaded_by",
                table: "files",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "mime_type",
                table: "files",
                type: "varchar(100)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(100)");

            migrationBuilder.AlterColumn<long>(
                name: "file_size_bytes",
                table: "files",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<FileVisibility>(
                name: "visibility",
                table: "file_links",
                type: "file_visibility",
                nullable: true,
                defaultValueSql: "'STAFF_ONLY'::file_visibility",
                oldClrType: typeof(FileVisibility),
                oldType: "file_visibility",
                oldNullable: true,
                oldDefaultValueSql: "'CUSTOMER_VISIBLE'::file_visibility");
        }
    }
}
