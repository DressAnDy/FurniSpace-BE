using FurniSpace.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurniSpace.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260815143000_RefactorOrderItemDeliveryForSingleFullDelivery")]
public partial class RefactorOrderItemDeliveryForSingleFullDelivery : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_order_items_accounts_last_delivered_by",
            table: "order_items");

        migrationBuilder.DropIndex(
            name: "IX_order_items_last_delivered_by",
            table: "order_items");

        migrationBuilder.DropColumn(
            name: "customer_confirmed_at",
            table: "order_items");

        migrationBuilder.DropColumn(
            name: "delivered_quantity",
            table: "order_items");

        migrationBuilder.RenameColumn(
            name: "last_delivered_at",
            table: "order_items",
            newName: "delivered_at");

        migrationBuilder.RenameColumn(
            name: "last_delivered_by",
            table: "order_items",
            newName: "delivered_by");

        migrationBuilder.CreateIndex(
            name: "IX_order_items_delivered_by",
            table: "order_items",
            column: "delivered_by");

        migrationBuilder.AddForeignKey(
            name: "FK_order_items_accounts_delivered_by",
            table: "order_items",
            column: "delivered_by",
            principalTable: "accounts",
            principalColumn: "account_id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_order_items_accounts_delivered_by",
            table: "order_items");

        migrationBuilder.DropIndex(
            name: "IX_order_items_delivered_by",
            table: "order_items");

        migrationBuilder.RenameColumn(
            name: "delivered_at",
            table: "order_items",
            newName: "last_delivered_at");

        migrationBuilder.RenameColumn(
            name: "delivered_by",
            table: "order_items",
            newName: "last_delivered_by");

        migrationBuilder.AddColumn<DateTime>(
            name: "customer_confirmed_at",
            table: "order_items",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "delivered_quantity",
            table: "order_items",
            type: "integer",
            nullable: true,
            defaultValue: 0);

        migrationBuilder.CreateIndex(
            name: "IX_order_items_last_delivered_by",
            table: "order_items",
            column: "last_delivered_by");

        migrationBuilder.AddForeignKey(
            name: "FK_order_items_accounts_last_delivered_by",
            table: "order_items",
            column: "last_delivered_by",
            principalTable: "accounts",
            principalColumn: "account_id",
            onDelete: ReferentialAction.Restrict);
    }
}
