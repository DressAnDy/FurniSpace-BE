using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurniSpace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AlignPaymentSchemaWithMainOrderFlowM2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE payments
                SET status = 'PAID'::payment_status
                WHERE status::text = 'PARTIALLY_PAID'
                  AND paid_amount >= amount;

                UPDATE payments
                SET status = 'PENDING'::payment_status
                WHERE status::text IN ('PARTIALLY_PAID', 'FAILED');
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_customization_requests_accounts_sales_review_by",
                table: "customization_requests");

            migrationBuilder.DropIndex(
                name: "IX_customization_requests_sales_review_by",
                table: "customization_requests");

            migrationBuilder.DropColumn(
                name: "paid_amount",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "remaining_amount",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "sales_review_by",
                table: "customization_requests");

            migrationBuilder.Sql(
                """
                DROP INDEX IF EXISTS uq_payments_active_order_type;

                ALTER TABLE payments ALTER COLUMN status DROP DEFAULT;

                CREATE TYPE payment_status_new AS ENUM (
                    'PENDING',
                    'PROCESSING',
                    'PAID',
                    'CANCELLED',
                    'EXPIRED',
                    'REFUNDED'
                );

                ALTER TABLE payments
                ALTER COLUMN status TYPE payment_status_new
                USING status::text::payment_status_new;

                DROP TYPE payment_status;
                ALTER TYPE payment_status_new RENAME TO payment_status;
                ALTER TABLE payments ALTER COLUMN status SET DEFAULT 'PENDING'::payment_status;

                CREATE UNIQUE INDEX uq_payments_active_order_type
                ON payments(order_id, payment_type)
                WHERE order_id IS NOT NULL
                  AND status IN ('PENDING', 'PROCESSING');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.AddColumn<Guid>(
                name: "sales_review_by",
                table: "customization_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_customization_requests_sales_review_by",
                table: "customization_requests",
                column: "sales_review_by");

            migrationBuilder.AddForeignKey(
                name: "FK_customization_requests_accounts_sales_review_by",
                table: "customization_requests",
                column: "sales_review_by",
                principalTable: "accounts",
                principalColumn: "account_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(
                """
                ALTER TABLE payments ALTER COLUMN status DROP DEFAULT;

                CREATE TYPE payment_status_old AS ENUM (
                    'PENDING',
                    'PROCESSING',
                    'PARTIALLY_PAID',
                    'PAID',
                    'FAILED',
                    'CANCELLED',
                    'EXPIRED',
                    'REFUNDED'
                );

                ALTER TABLE payments
                ALTER COLUMN status TYPE payment_status_old
                USING status::text::payment_status_old;

                DROP TYPE payment_status;
                ALTER TYPE payment_status_old RENAME TO payment_status;
                ALTER TABLE payments ALTER COLUMN status SET DEFAULT 'PENDING'::payment_status;
                """);
        }
    }
}
