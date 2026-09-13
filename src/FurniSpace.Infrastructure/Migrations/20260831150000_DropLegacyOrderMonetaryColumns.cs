using FurniSpace.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurniSpace.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260831150000_DropLegacyOrderMonetaryColumns")]
public partial class DropLegacyOrderMonetaryColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "original_total_amount",
            table: "orders");

        migrationBuilder.DropColumn(
            name: "item_adjustment_amount",
            table: "orders");

        migrationBuilder.DropColumn(
            name: "additional_discount_amount",
            table: "orders");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "original_total_amount",
            table: "orders",
            type: "numeric(12,2)",
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "item_adjustment_amount",
            table: "orders",
            type: "numeric(12,2)",
            nullable: true,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "additional_discount_amount",
            table: "orders",
            type: "numeric(12,2)",
            nullable: true,
            defaultValue: 0m);
    }
}
