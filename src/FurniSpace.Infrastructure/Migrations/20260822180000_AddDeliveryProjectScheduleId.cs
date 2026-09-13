using System;
using FurniSpace.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurniSpace.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260822180000_AddDeliveryProjectScheduleId")]
public partial class AddDeliveryProjectScheduleId : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "project_schedule_id",
            table: "deliveries",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "idx_deliveries_project_schedule_id",
            table: "deliveries",
            column: "project_schedule_id");

        migrationBuilder.CreateIndex(
            name: "ux_deliveries_project_schedule_id",
            table: "deliveries",
            column: "project_schedule_id",
            unique: true,
            filter: "project_schedule_id IS NOT NULL");

        migrationBuilder.AddForeignKey(
            name: "FK_deliveries_project_schedules_project_schedule_id",
            table: "deliveries",
            column: "project_schedule_id",
            principalTable: "project_schedules",
            principalColumn: "schedule_id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_deliveries_project_schedules_project_schedule_id",
            table: "deliveries");

        migrationBuilder.DropIndex(
            name: "ux_deliveries_project_schedule_id",
            table: "deliveries");

        migrationBuilder.DropIndex(
            name: "idx_deliveries_project_schedule_id",
            table: "deliveries");

        migrationBuilder.DropColumn(
            name: "project_schedule_id",
            table: "deliveries");
    }
}
