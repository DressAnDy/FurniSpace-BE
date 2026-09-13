using FurniSpace.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurniSpace.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260808140000_AddProductVersionDefaultTaxRate")]
public partial class AddProductVersionDefaultTaxRate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "default_tax_rate",
            table: "product_versions",
            type: "numeric(7,4)",
            nullable: true);

        migrationBuilder.Sql(
            """
            ALTER TABLE product_versions
            ADD CONSTRAINT ck_product_versions_default_tax_rate_range
            CHECK (default_tax_rate IS NULL OR (default_tax_rate >= 0 AND default_tax_rate <= 100));
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE product_versions
            DROP CONSTRAINT IF EXISTS ck_product_versions_default_tax_rate_range;
            """);

        migrationBuilder.DropColumn(
            name: "default_tax_rate",
            table: "product_versions");
    }
}
