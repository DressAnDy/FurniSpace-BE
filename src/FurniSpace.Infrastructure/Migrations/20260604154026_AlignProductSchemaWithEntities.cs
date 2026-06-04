using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurniSpace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AlignProductSchemaWithEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_product_versions_accounts_created_by",
                table: "product_versions");

            migrationBuilder.DropForeignKey(
                name: "FK_product_versions_projects_project_id",
                table: "product_versions");

            migrationBuilder.DropForeignKey(
                name: "FK_products_accounts_created_by",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_product_versions_created_by",
                table: "product_versions");

            migrationBuilder.DropIndex(
                name: "IX_product_versions_project_id",
                table: "product_versions");

            migrationBuilder.DropIndex(
                name: "IX_products_created_by",
                table: "products");

            migrationBuilder.Sql("ALTER TABLE categories ALTER COLUMN status DROP DEFAULT;");
            migrationBuilder.Sql("ALTER TABLE categories ALTER COLUMN status TYPE varchar(30) USING status::text;");
            migrationBuilder.Sql("ALTER TABLE categories ALTER COLUMN status SET DEFAULT 'ACTIVE';");

            migrationBuilder.AddColumn<string>(
                name: "product_type",
                table: "products",
                type: "varchar(30)",
                nullable: true,
                defaultValue: "SINGLE");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "products");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "products");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "product_versions");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "product_versions");

            migrationBuilder.DropColumn(
                name: "finish",
                table: "product_versions");

            migrationBuilder.DropColumn(
                name: "production_note",
                table: "product_versions");

            migrationBuilder.DropColumn(
                name: "project_id",
                table: "product_versions");

            migrationBuilder.DropColumn(
                name: "technical_note",
                table: "product_versions");

            migrationBuilder.Sql("UPDATE product_versions SET version_code = product_version_id::text WHERE version_code IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "version_code",
                table: "product_versions",
                type: "varchar(50)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldNullable: true);

            migrationBuilder.Sql("ALTER TABLE product_versions ALTER COLUMN status DROP DEFAULT;");
            migrationBuilder.Sql("ALTER TABLE product_versions ALTER COLUMN status TYPE product_status USING status::text::product_status;");
            migrationBuilder.Sql("ALTER TABLE product_versions ALTER COLUMN status SET DEFAULT 'ACTIVE'::product_status;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE product_versions ALTER COLUMN status DROP DEFAULT;");
            migrationBuilder.Sql("ALTER TABLE product_versions ALTER COLUMN status TYPE product_version_status USING status::text::product_version_status;");
            migrationBuilder.Sql("ALTER TABLE product_versions ALTER COLUMN status SET DEFAULT 'ACTIVE'::product_version_status;");

            migrationBuilder.AlterColumn<string>(
                name: "version_code",
                table: "product_versions",
                type: "varchar(50)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(50)");

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                table: "products",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "products",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "product_type",
                table: "products");

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                table: "product_versions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "product_versions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "finish",
                table: "product_versions",
                type: "varchar(100)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "production_note",
                table: "product_versions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "project_id",
                table: "product_versions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "technical_note",
                table: "product_versions",
                type: "text",
                nullable: true);

            migrationBuilder.Sql("ALTER TABLE categories ALTER COLUMN status DROP DEFAULT;");
            migrationBuilder.Sql("ALTER TABLE categories ALTER COLUMN status TYPE product_status USING status::text::product_status;");
            migrationBuilder.Sql("ALTER TABLE categories ALTER COLUMN status SET DEFAULT 'ACTIVE'::product_status;");

            migrationBuilder.CreateIndex(
                name: "IX_products_created_by",
                table: "products",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_product_versions_created_by",
                table: "product_versions",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_product_versions_project_id",
                table: "product_versions",
                column: "project_id");

            migrationBuilder.AddForeignKey(
                name: "FK_products_accounts_created_by",
                table: "products",
                column: "created_by",
                principalTable: "accounts",
                principalColumn: "account_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_product_versions_accounts_created_by",
                table: "product_versions",
                column: "created_by",
                principalTable: "accounts",
                principalColumn: "account_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_product_versions_projects_project_id",
                table: "product_versions",
                column: "project_id",
                principalTable: "projects",
                principalColumn: "project_id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
