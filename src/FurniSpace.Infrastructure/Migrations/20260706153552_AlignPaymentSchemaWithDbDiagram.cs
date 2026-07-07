using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurniSpace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AlignPaymentSchemaWithDbDiagram : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_payments_order_id",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "IX_payments_project_id",
                table: "payments");

            migrationBuilder.Sql(
                """
                CREATE TYPE payment_provider AS ENUM (
                    'PAYOS',
                    'SEPAY',
                    'CASH',
                    'MANUAL_BANK_TRANSFER',
                    'OTHER'
                );

                CREATE TYPE payment_method AS ENUM (
                    'PAYMENT_LINK',
                    'QR_CODE',
                    'BANK_TRANSFER',
                    'CASH',
                    'OTHER'
                );

                ALTER TABLE payments ALTER COLUMN status DROP DEFAULT;
                ALTER TYPE payment_status RENAME TO payment_status_old;
                CREATE TYPE payment_status AS ENUM (
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
                    ALTER COLUMN status TYPE payment_status
                    USING status::text::payment_status;
                ALTER TABLE payments ALTER COLUMN status SET DEFAULT 'PENDING'::payment_status;
                DROP TYPE payment_status_old;

                ALTER TABLE payments ALTER COLUMN payment_type DROP DEFAULT;
                ALTER TYPE payment_type RENAME TO payment_type_old;
                CREATE TYPE payment_type AS ENUM (
                    'PROJECT_START_FEE',
                    'DEPOSIT',
                    'REMAINING_PAYMENT',
                    'FULL_PAYMENT',
                    'REFUND',
                    'OTHER'
                );
                ALTER TABLE payments
                    ALTER COLUMN payment_type TYPE payment_type
                    USING payment_type::text::payment_type;
                ALTER TABLE payments ALTER COLUMN payment_type SET DEFAULT 'OTHER'::payment_type;
                DROP TYPE payment_type_old;

                ALTER TABLE payment_transactions DROP COLUMN payment_method;
                ALTER TABLE payment_transactions ADD COLUMN payment_method payment_method;
                ALTER TABLE payment_transactions ADD COLUMN payment_provider payment_provider;
                ALTER TABLE payment_transactions ADD COLUMN provider_reference_code varchar(255);
                """);

            migrationBuilder.DropColumn(
                name: "due_date",
                table: "payments");

            migrationBuilder.AddColumn<DateTime>(
                name: "expired_at",
                table: "payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payment_code",
                table: "payments",
                type: "varchar(100)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "idx_payments_order_type",
                table: "payments",
                columns: new[] { "order_id", "payment_type" });

            migrationBuilder.CreateIndex(
                name: "idx_payments_project_time",
                table: "payments",
                columns: new[] { "project_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "uq_payments_payment_code",
                table: "payments",
                column: "payment_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_payment_transactions_provider_reference_code",
                table: "payment_transactions",
                column: "provider_reference_code");

            migrationBuilder.CreateIndex(
                name: "uq_payment_transactions_provider_txn",
                table: "payment_transactions",
                columns: new[] { "payment_provider", "provider_transaction_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_payments_order_type",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "idx_payments_project_time",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "uq_payments_payment_code",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "idx_payment_transactions_provider_reference_code",
                table: "payment_transactions");

            migrationBuilder.DropIndex(
                name: "uq_payment_transactions_provider_txn",
                table: "payment_transactions");

            migrationBuilder.DropColumn(
                name: "expired_at",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "payment_code",
                table: "payments");

            migrationBuilder.AddColumn<DateOnly>(
                name: "due_date",
                table: "payments",
                type: "date",
                nullable: true);

            migrationBuilder.Sql(
                """
                ALTER TABLE payment_transactions DROP COLUMN provider_reference_code;
                ALTER TABLE payment_transactions DROP COLUMN payment_provider;
                ALTER TABLE payment_transactions DROP COLUMN payment_method;
                ALTER TABLE payment_transactions ADD COLUMN payment_method varchar(50);

                ALTER TABLE payments ALTER COLUMN payment_type DROP DEFAULT;
                ALTER TYPE payment_type RENAME TO payment_type_old;
                CREATE TYPE payment_type AS ENUM (
                    'DEPOSIT',
                    'REMAINING_PAYMENT',
                    'FULL_PAYMENT',
                    'MEASUREMENT_FEE',
                    'DESIGN_FEE',
                    'CUSTOMIZATION_FEE',
                    'DELIVERY_FEE',
                    'CANCELLATION_FEE',
                    'REFUND',
                    'OTHER'
                );
                ALTER TABLE payments
                    ALTER COLUMN payment_type TYPE payment_type
                    USING payment_type::text::payment_type;
                ALTER TABLE payments ALTER COLUMN payment_type SET DEFAULT 'OTHER'::payment_type;
                DROP TYPE payment_type_old;

                ALTER TABLE payments ALTER COLUMN status DROP DEFAULT;
                ALTER TYPE payment_status RENAME TO payment_status_old;
                CREATE TYPE payment_status AS ENUM (
                    'PENDING',
                    'PROCESSING',
                    'PAID',
                    'PARTIALLY_PAID',
                    'FAILED',
                    'CANCELLED',
                    'REFUNDED'
                );
                ALTER TABLE payments
                    ALTER COLUMN status TYPE payment_status
                    USING status::text::payment_status;
                ALTER TABLE payments ALTER COLUMN status SET DEFAULT 'PENDING'::payment_status;
                DROP TYPE payment_status_old;

                DROP TYPE payment_method;
                DROP TYPE payment_provider;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_payments_order_id",
                table: "payments",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "IX_payments_project_id",
                table: "payments",
                column: "project_id");
        }
    }
}
