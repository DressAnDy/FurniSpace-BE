using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurniSpace.Infrastructure.Migrations;

public partial class AddAdminFinancialDashboardIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "idx_fin_orders_project_confirmed",
            table: "orders",
            columns: new[] { "project_id", "confirmed_at", "order_id" });

        migrationBuilder.CreateIndex(
            name: "idx_fin_orders_receivable_status_confirmed",
            table: "orders",
            columns: new[] { "status", "confirmed_at", "project_id" },
            filter: "remaining_amount > 0");

        migrationBuilder.CreateIndex(
            name: "idx_fin_payment_transactions_failed_reporting",
            table: "payment_transactions",
            columns: new[] { "status", "created_at", "currency" },
            filter: "status = 'FAILED'");

        migrationBuilder.CreateIndex(
            name: "idx_fin_payment_transactions_payment_failed_time",
            table: "payment_transactions",
            columns: new[] { "payment_id", "status", "created_at" },
            filter: "status = 'FAILED'");

        migrationBuilder.CreateIndex(
            name: "idx_fin_payments_active_obligations",
            table: "payments",
            columns: new[] { "status", "expired_at", "created_at", "payment_type", "order_id" },
            filter: "status IN ('PENDING', 'PROCESSING')");

        migrationBuilder.CreateIndex(
            name: "idx_fin_payments_paid_reporting",
            table: "payments",
            columns: new[] { "status", "paid_at", "payment_type", "currency" },
            filter: "status = 'PAID' AND paid_at IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "idx_fin_orders_project_confirmed",
            table: "orders");

        migrationBuilder.DropIndex(
            name: "idx_fin_orders_receivable_status_confirmed",
            table: "orders");

        migrationBuilder.DropIndex(
            name: "idx_fin_payment_transactions_failed_reporting",
            table: "payment_transactions");

        migrationBuilder.DropIndex(
            name: "idx_fin_payment_transactions_payment_failed_time",
            table: "payment_transactions");

        migrationBuilder.DropIndex(
            name: "idx_fin_payments_active_obligations",
            table: "payments");

        migrationBuilder.DropIndex(
            name: "idx_fin_payments_paid_reporting",
            table: "payments");
    }
}
