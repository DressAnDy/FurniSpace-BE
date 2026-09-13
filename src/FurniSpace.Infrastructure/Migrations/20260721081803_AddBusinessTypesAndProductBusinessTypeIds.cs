using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FurniSpace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessTypesAndProductBusinessTypeIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int[]>(
                name: "business_type_ids",
                table: "products",
                type: "integer[]",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "business_types",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "varchar(50)", nullable: false),
                    name = table.Column<string>(type: "varchar(150)", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_business_types", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_products_business_type_ids",
                table: "products",
                column: "business_type_ids")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_business_types_code",
                table: "business_types",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_business_types_status",
                table: "business_types",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "business_types");

            migrationBuilder.DropIndex(
                name: "idx_products_business_type_ids",
                table: "products");

            migrationBuilder.DropColumn(
                name: "business_type_ids",
                table: "products");
        }
    }
}
