using System;
using FurniSpace.Domain.Enums;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurniSpace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectPhaseDeadlines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "project_phase_deadlines",
                columns: table => new
                {
                    project_phase_deadline_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    phase = table.Column<ProjectPhaseType>(type: "project_phase_type", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "project_phase_deadlines");
        }
    }
}
