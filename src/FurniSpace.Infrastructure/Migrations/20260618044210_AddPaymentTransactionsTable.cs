using System;
using FurniSpace.Domain.Enums;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurniSpace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentTransactionsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.CreateTable(
                name: "payment_transactions",
                columns: table => new
                {
                    payment_transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    transaction_code = table.Column<string>(type: "varchar(100)", nullable: false),
                    transaction_type = table.Column<PaymentTransactionType>(type: "payment_transaction_type", nullable: false, defaultValueSql: "'CHARGE'::payment_transaction_type"),
                    amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    currency = table.Column<string>(type: "varchar(10)", nullable: false, defaultValue: "VND"),
                    payment_method = table.Column<string>(type: "varchar(50)", nullable: true),
                    provider_transaction_id = table.Column<string>(type: "varchar(255)", nullable: true),
                    status = table.Column<PaymentTransactionStatus>(type: "payment_transaction_status", nullable: false, defaultValueSql: "'PENDING'::payment_transaction_status"),
                    transaction_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    confirmed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    confirmed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    failure_reason = table.Column<string>(type: "text", nullable: true),
                    raw_provider_payload = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_transactions", x => x.payment_transaction_id);
                    table.ForeignKey(
                        name: "FK_payment_transactions_accounts_confirmed_by",
                        column: x => x.confirmed_by,
                        principalTable: "accounts",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payment_transactions_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "order_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payment_transactions_payments_payment_id",
                        column: x => x.payment_id,
                        principalTable: "payments",
                        principalColumn: "payment_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payment_transactions_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "project_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "idx_payment_transactions_order_time",
                table: "payment_transactions",
                columns: new[] { "order_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "idx_payment_transactions_payment_id",
                table: "payment_transactions",
                column: "payment_id");

            migrationBuilder.CreateIndex(
                name: "idx_payment_transactions_project_time",
                table: "payment_transactions",
                columns: new[] { "project_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "idx_payment_transactions_provider_transaction_id",
                table: "payment_transactions",
                column: "provider_transaction_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_transactions_confirmed_by",
                table: "payment_transactions",
                column: "confirmed_by");

            migrationBuilder.CreateIndex(
                name: "IX_payment_transactions_transaction_code",
                table: "payment_transactions",
                column: "transaction_code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payment_transactions");

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
                .OldAnnotation("Npgsql:Enum:proposal_status", "DRAFT,PUBLISHED,VIEWED,SELECTED,REVISION_REQUESTED,REJECTED,ARCHIVED")
                .OldAnnotation("Npgsql:Enum:quotation_status", "DRAFT,SENT,REVISION_REQUESTED,REVISED,ACCEPTED,REJECTED,EXPIRED,CANCELLED");
        }
    }
}
