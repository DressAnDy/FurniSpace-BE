using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurniSpace.Infrastructure.Migrations;

/// <inheritdoc />
public partial class EnsureProjectScheduleCompletedAtColumn : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE project_schedules
            ADD COLUMN IF NOT EXISTS completed_at timestamp with time zone NULL;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE project_schedules
            DROP COLUMN IF EXISTS completed_at;
            """);
    }
}
