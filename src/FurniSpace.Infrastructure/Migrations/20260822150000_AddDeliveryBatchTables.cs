using System;
using FurniSpace.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurniSpace.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260822150000_AddDeliveryBatchTables")]
public partial class AddDeliveryBatchTables : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'delivery_status') THEN
                    CREATE TYPE delivery_status AS ENUM ('IN_PROGRESS', 'COMPLETED');
                END IF;
            END $$;
            """);

        migrationBuilder.Sql("""
            ALTER TYPE order_item_status ADD VALUE IF NOT EXISTS 'PARTIALLY_DELIVERED' AFTER 'READY';
            """);

        migrationBuilder.AddColumn<int>(
            name: "delivered_quantity",
            table: "order_items",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.CreateTable(
            name: "deliveries",
            columns: table => new
            {
                delivery_id = table.Column<Guid>(type: "uuid", nullable: false),
                order_id = table.Column<Guid>(type: "uuid", nullable: false),
                status = table.Column<string>(type: "delivery_status", nullable: true, defaultValueSql: "'IN_PROGRESS'::delivery_status"),
                created_by = table.Column<Guid>(type: "uuid", nullable: true),
                completed_by = table.Column<Guid>(type: "uuid", nullable: true),
                note = table.Column<string>(type: "text", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_deliveries", x => x.delivery_id);
                table.ForeignKey(
                    name: "FK_deliveries_accounts_completed_by",
                    column: x => x.completed_by,
                    principalTable: "accounts",
                    principalColumn: "account_id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_deliveries_accounts_created_by",
                    column: x => x.created_by,
                    principalTable: "accounts",
                    principalColumn: "account_id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_deliveries_orders_order_id",
                    column: x => x.order_id,
                    principalTable: "orders",
                    principalColumn: "order_id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "delivery_items",
            columns: table => new
            {
                delivery_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                delivery_id = table.Column<Guid>(type: "uuid", nullable: false),
                order_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                quantity = table.Column<int>(type: "integer", nullable: false),
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
            });

        migrationBuilder.CreateIndex(
            name: "idx_deliveries_order_id",
            table: "deliveries",
            column: "order_id");

        migrationBuilder.CreateIndex(
            name: "IX_deliveries_completed_by",
            table: "deliveries",
            column: "completed_by");

        migrationBuilder.CreateIndex(
            name: "IX_deliveries_created_by",
            table: "deliveries",
            column: "created_by");

        migrationBuilder.CreateIndex(
            name: "idx_delivery_items_delivery_id",
            table: "delivery_items",
            column: "delivery_id");

        migrationBuilder.CreateIndex(
            name: "idx_delivery_items_order_item_id",
            table: "delivery_items",
            column: "order_item_id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "delivery_items");

        migrationBuilder.DropTable(
            name: "deliveries");

        migrationBuilder.DropColumn(
            name: "delivered_quantity",
            table: "order_items");
    }
}
