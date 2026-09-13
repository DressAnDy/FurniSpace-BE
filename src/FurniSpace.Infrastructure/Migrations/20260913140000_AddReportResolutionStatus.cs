using FurniSpace.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurniSpace.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260913140000_AddReportResolutionStatus")]
public partial class AddReportResolutionStatus : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'report_resolution_status') THEN
                    CREATE TYPE report_resolution_status AS ENUM (
                        'OPEN',
                        'RESOLVED'
                    );
                END IF;
            END $$;
            """);

        migrationBuilder.AddColumn<string>(
            name: "status",
            table: "operational_delay_reports",
            type: "report_resolution_status",
            nullable: false,
            defaultValue: "OPEN");

        migrationBuilder.AddColumn<DateTime>(
            name: "resolved_at",
            table: "operational_delay_reports",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "resolution_note",
            table: "operational_delay_reports",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "status",
            table: "delivery_product_issue_reports",
            type: "report_resolution_status",
            nullable: false,
            defaultValue: "OPEN");

        migrationBuilder.AddColumn<DateTime>(
            name: "resolved_at",
            table: "delivery_product_issue_reports",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "resolution_note",
            table: "delivery_product_issue_reports",
            type: "text",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "status",
            table: "operational_delay_reports");

        migrationBuilder.DropColumn(
            name: "resolved_at",
            table: "operational_delay_reports");

        migrationBuilder.DropColumn(
            name: "resolution_note",
            table: "operational_delay_reports");

        migrationBuilder.DropColumn(
            name: "status",
            table: "delivery_product_issue_reports");

        migrationBuilder.DropColumn(
            name: "resolved_at",
            table: "delivery_product_issue_reports");

        migrationBuilder.DropColumn(
            name: "resolution_note",
            table: "delivery_product_issue_reports");

        migrationBuilder.Sql("DROP TYPE IF EXISTS report_resolution_status;");
    }
}
