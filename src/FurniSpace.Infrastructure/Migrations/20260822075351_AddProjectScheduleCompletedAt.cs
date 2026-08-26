using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurniSpace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectScheduleCompletedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "completed_at",
                table: "project_schedules",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "completed_at",
                table: "project_schedules");
        }
    }
}
