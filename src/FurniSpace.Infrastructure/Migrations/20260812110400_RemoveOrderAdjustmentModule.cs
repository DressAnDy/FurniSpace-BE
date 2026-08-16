using FurniSpace.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurniSpace.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260812110400_RemoveOrderAdjustmentModule")]
public partial class RemoveOrderAdjustmentModule : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "order_adjustment_items");

        migrationBuilder.DropTable(
            name: "order_adjustments");

        migrationBuilder.Sql("DROP TYPE IF EXISTS order_adjustment_item_type;");
        migrationBuilder.Sql("DROP TYPE IF EXISTS order_adjustment_status;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TYPE order_adjustment_status AS ENUM ('DRAFT', 'CONFIRMED', 'APPLIED', 'CANCELLED');
            CREATE TYPE order_adjustment_item_type AS ENUM ('UNAVAILABLE_ITEM', 'ADDITIONAL_DISCOUNT');
            """);

        migrationBuilder.CreateTable(
            name: "order_adjustments",
            columns: table => new
            {
                order_adjustment_id = table.Column<Guid>(type: "uuid", nullable: false),
                order_id = table.Column<Guid>(type: "uuid", nullable: false),
                status = table.Column<string>(type: "order_adjustment_status", nullable: false, defaultValueSql: "'DRAFT'::order_adjustment_status"),
                item_adjustment_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false, defaultValue: 0m),
                additional_discount_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false, defaultValue: 0m),
                total_adjustment_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false, defaultValue: 0m),
                reason = table.Column<string>(type: "text", nullable: false),
                internal_note = table.Column<string>(type: "text", nullable: true),
                created_by = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                confirmed_by = table.Column<Guid>(type: "uuid", nullable: true),
                confirmed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                applied_by = table.Column<Guid>(type: "uuid", nullable: true),
                applied_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                cancelled_by = table.Column<Guid>(type: "uuid", nullable: true),
                cancelled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                cancellation_reason = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_order_adjustments", x => x.order_adjustment_id);
                table.ForeignKey(
                    name: "FK_order_adjustments_accounts_applied_by",
                    column: x => x.applied_by,
                    principalTable: "accounts",
                    principalColumn: "account_id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_order_adjustments_accounts_cancelled_by",
                    column: x => x.cancelled_by,
                    principalTable: "accounts",
                    principalColumn: "account_id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_order_adjustments_accounts_confirmed_by",
                    column: x => x.confirmed_by,
                    principalTable: "accounts",
                    principalColumn: "account_id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_order_adjustments_accounts_created_by",
                    column: x => x.created_by,
                    principalTable: "accounts",
                    principalColumn: "account_id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_order_adjustments_accounts_updated_by",
                    column: x => x.updated_by,
                    principalTable: "accounts",
                    principalColumn: "account_id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_order_adjustments_orders_order_id",
                    column: x => x.order_id,
                    principalTable: "orders",
                    principalColumn: "order_id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "order_adjustment_items",
            columns: table => new
            {
                order_adjustment_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                order_adjustment_id = table.Column<Guid>(type: "uuid", nullable: false),
                order_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                adjustment_type = table.Column<string>(type: "order_adjustment_item_type", nullable: false),
                previous_item_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false, defaultValue: 0m),
                adjustment_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                reason = table.Column<string>(type: "text", nullable: false),
                created_by = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_order_adjustment_items", x => x.order_adjustment_item_id);
                table.ForeignKey(
                    name: "FK_order_adjustment_items_accounts_created_by",
                    column: x => x.created_by,
                    principalTable: "accounts",
                    principalColumn: "account_id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_order_adjustment_items_accounts_updated_by",
                    column: x => x.updated_by,
                    principalTable: "accounts",
                    principalColumn: "account_id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_order_adjustment_items_order_adjustments_order_adjustment_id",
                    column: x => x.order_adjustment_id,
                    principalTable: "order_adjustments",
                    principalColumn: "order_adjustment_id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_order_adjustment_items_order_items_order_item_id",
                    column: x => x.order_item_id,
                    principalTable: "order_items",
                    principalColumn: "order_item_id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_order_adjustment_items_created_by",
            table: "order_adjustment_items",
            column: "created_by");

        migrationBuilder.CreateIndex(
            name: "IX_order_adjustment_items_order_adjustment_id_order_item_id_ad~",
            table: "order_adjustment_items",
            columns: new[] { "order_adjustment_id", "order_item_id", "adjustment_type" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_order_adjustment_items_order_item_id",
            table: "order_adjustment_items",
            column: "order_item_id");

        migrationBuilder.CreateIndex(
            name: "IX_order_adjustment_items_updated_by",
            table: "order_adjustment_items",
            column: "updated_by");

        migrationBuilder.CreateIndex(
            name: "IX_order_adjustments_applied_by",
            table: "order_adjustments",
            column: "applied_by");

        migrationBuilder.CreateIndex(
            name: "IX_order_adjustments_cancelled_by",
            table: "order_adjustments",
            column: "cancelled_by");

        migrationBuilder.CreateIndex(
            name: "IX_order_adjustments_confirmed_by",
            table: "order_adjustments",
            column: "confirmed_by");

        migrationBuilder.CreateIndex(
            name: "IX_order_adjustments_created_by",
            table: "order_adjustments",
            column: "created_by");

        migrationBuilder.CreateIndex(
            name: "IX_order_adjustments_order_id",
            table: "order_adjustments",
            column: "order_id");

        migrationBuilder.CreateIndex(
            name: "IX_order_adjustments_updated_by",
            table: "order_adjustments",
            column: "updated_by");
    }
}
