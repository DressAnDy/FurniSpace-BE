using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurniSpace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectAreaActiveFloorNumberUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "uq_project_active_floor_number",
                table: "project_areas",
                columns: new[] { "project_id", "floor_number" },
                unique: true,
                filter: "area_type = 'FLOOR' AND status <> 'CANCELLED'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_project_active_floor_number",
                table: "project_areas");
        }
    }
}
