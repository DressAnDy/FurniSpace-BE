using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurniSpace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectSpecificProductVersionCheckConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "ck_product_versions_project_specific",
                table: "product_versions",
                sql: "(version_type = 'PROJECT_SPECIFIC'::product_version_type AND project_id IS NOT NULL AND is_project_specific = TRUE AND is_public = FALSE AND is_default = FALSE) OR version_type <> 'PROJECT_SPECIFIC'::product_version_type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_product_versions_project_specific",
                table: "product_versions");
        }
    }
}
