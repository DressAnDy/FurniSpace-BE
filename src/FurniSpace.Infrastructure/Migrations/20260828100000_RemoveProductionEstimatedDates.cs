using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurniSpace.Infrastructure.Migrations
{
    public partial class RemoveProductionEstimatedDates : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "estimated_start_date",
                table: "production_requests");

            migrationBuilder.DropColumn(
                name: "estimated_completion_date",
                table: "production_requests");

            migrationBuilder.DropColumn(
                name: "estimated_completion_date",
                table: "production_items");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "estimated_start_date",
                table: "production_requests",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "estimated_completion_date",
                table: "production_requests",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "estimated_completion_date",
                table: "production_items",
                type: "date",
                nullable: true);
        }
    }
}
