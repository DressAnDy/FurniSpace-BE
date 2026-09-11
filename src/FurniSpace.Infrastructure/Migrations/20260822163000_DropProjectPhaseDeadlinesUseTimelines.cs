using System;
using FurniSpace.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurniSpace.Infrastructure.Migrations;

[DbContext(typeof(FurniSpace.Infrastructure.Data.AppDbContext))]
[Migration("20260822163000_DropProjectPhaseDeadlinesUseTimelines")]
public partial class DropProjectPhaseDeadlinesUseTimelines : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            INSERT INTO project_phase_timelines (
                project_phase_timeline_id,
                project_id,
                phase,
                due_date,
                started_at,
                completed_at,
                created_by,
                updated_by,
                created_at,
                updated_at)
            SELECT
                d.project_phase_deadline_id,
                d.project_id,
                d.phase,
                d.due_date,
                NULL,
                d.completed_at,
                d.created_by,
                d.updated_by,
                d.created_at,
                d.updated_at
            FROM project_phase_deadlines d
            WHERE NOT EXISTS (
                SELECT 1
                FROM project_phase_timelines t
                WHERE t.project_id = d.project_id
                  AND t.phase = d.phase);
            """);

        migrationBuilder.DropTable(
            name: "project_phase_deadlines");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "project_phase_deadlines",
            columns: table => new
            {
                project_phase_deadline_id = table.Column<Guid>(type: "uuid", nullable: false),
                completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                created_by = table.Column<Guid>(type: "uuid", nullable: false),
                due_date = table.Column<DateOnly>(type: "date", nullable: false),
                phase = table.Column<string>(type: "text", nullable: false),
                project_id = table.Column<Guid>(type: "uuid", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_by = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_project_phase_deadlines", x => x.project_phase_deadline_id);
                table.ForeignKey(
                    name: "FK_project_phase_deadlines_accounts_created_by",
                    column: x => x.created_by,
                    principalTable: "accounts",
                    principalColumn: "account_id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_project_phase_deadlines_accounts_updated_by",
                    column: x => x.updated_by,
                    principalTable: "accounts",
                    principalColumn: "account_id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_project_phase_deadlines_projects_project_id",
                    column: x => x.project_id,
                    principalTable: "projects",
                    principalColumn: "project_id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "idx_project_phase_deadlines_due_date",
            table: "project_phase_deadlines",
            column: "due_date");

        migrationBuilder.CreateIndex(
            name: "idx_project_phase_deadlines_project",
            table: "project_phase_deadlines",
            column: "project_id");

        migrationBuilder.CreateIndex(
            name: "IX_project_phase_deadlines_created_by",
            table: "project_phase_deadlines",
            column: "created_by");

        migrationBuilder.CreateIndex(
            name: "IX_project_phase_deadlines_updated_by",
            table: "project_phase_deadlines",
            column: "updated_by");

        migrationBuilder.CreateIndex(
            name: "uq_project_phase_deadlines_project_phase",
            table: "project_phase_deadlines",
            columns: new[] { "project_id", "phase" },
            unique: true);
    }
}
