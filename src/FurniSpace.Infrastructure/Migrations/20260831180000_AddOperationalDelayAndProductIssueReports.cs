using System;
using FurniSpace.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurniSpace.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260831180000_AddOperationalDelayAndProductIssueReports")]
public partial class AddOperationalDelayAndProductIssueReports : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'operational_delay_phase') THEN
                    CREATE TYPE operational_delay_phase AS ENUM ('PRODUCTION', 'DELIVERY');
                END IF;
            END $$;
            """);

        migrationBuilder.Sql("""
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'operational_delay_state') THEN
                    CREATE TYPE operational_delay_state AS ENUM ('AT_RISK', 'OVERDUE');
                END IF;
            END $$;
            """);

        migrationBuilder.Sql("""
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'delivery_product_issue_type') THEN
                    CREATE TYPE delivery_product_issue_type AS ENUM (
                        'DAMAGED',
                        'WRONG_ITEM',
                        'WRONG_SPECIFICATION',
                        'MISSING_PART',
                        'QUALITY_DEFECT',
                        'INSTALLATION_ISSUE',
                        'QUANTITY_MISMATCH',
                        'OTHER'
                    );
                END IF;
            END $$;
            """);

        migrationBuilder.Sql("""
            ALTER TYPE file_type ADD VALUE IF NOT EXISTS 'PRODUCT_ISSUE_EVIDENCE' AFTER 'DELIVERY_NOTE';
            """);

        migrationBuilder.CreateTable(
            name: "operational_delay_reports",
            columns: table => new
            {
                operational_delay_report_id = table.Column<Guid>(type: "uuid", nullable: false),
                project_id = table.Column<Guid>(type: "uuid", nullable: false),
                report_phase = table.Column<string>(type: "operational_delay_phase", nullable: false),
                production_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                order_id = table.Column<Guid>(type: "uuid", nullable: true),
                delivery_id = table.Column<Guid>(type: "uuid", nullable: true),
                deadline_snapshot = table.Column<DateOnly>(type: "date", nullable: false),
                delay_state = table.Column<string>(type: "operational_delay_state", nullable: false),
                reason_code = table.Column<string>(type: "varchar(100)", nullable: true),
                reason_detail = table.Column<string>(type: "text", nullable: false),
                reported_by = table.Column<Guid>(type: "uuid", nullable: false),
                reported_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_operational_delay_reports", x => x.operational_delay_report_id);
                table.ForeignKey(
                    name: "FK_operational_delay_reports_accounts_reported_by",
                    column: x => x.reported_by,
                    principalTable: "accounts",
                    principalColumn: "account_id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_operational_delay_reports_deliveries_delivery_id",
                    column: x => x.delivery_id,
                    principalTable: "deliveries",
                    principalColumn: "delivery_id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_operational_delay_reports_orders_order_id",
                    column: x => x.order_id,
                    principalTable: "orders",
                    principalColumn: "order_id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_operational_delay_reports_production_requests_production_request_id",
                    column: x => x.production_request_id,
                    principalTable: "production_requests",
                    principalColumn: "production_request_id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_operational_delay_reports_projects_project_id",
                    column: x => x.project_id,
                    principalTable: "projects",
                    principalColumn: "project_id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "delivery_product_issue_reports",
            columns: table => new
            {
                delivery_product_issue_report_id = table.Column<Guid>(type: "uuid", nullable: false),
                project_id = table.Column<Guid>(type: "uuid", nullable: false),
                order_id = table.Column<Guid>(type: "uuid", nullable: false),
                order_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                delivery_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                issue_type = table.Column<string>(type: "delivery_product_issue_type", nullable: false),
                description = table.Column<string>(type: "text", nullable: false),
                affected_quantity = table.Column<int>(type: "integer", nullable: true),
                reported_by = table.Column<Guid>(type: "uuid", nullable: false),
                reported_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_delivery_product_issue_reports", x => x.delivery_product_issue_report_id);
                table.ForeignKey(
                    name: "FK_delivery_product_issue_reports_accounts_reported_by",
                    column: x => x.reported_by,
                    principalTable: "accounts",
                    principalColumn: "account_id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_delivery_product_issue_reports_delivery_items_delivery_item_id",
                    column: x => x.delivery_item_id,
                    principalTable: "delivery_items",
                    principalColumn: "delivery_item_id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_delivery_product_issue_reports_order_items_order_item_id",
                    column: x => x.order_item_id,
                    principalTable: "order_items",
                    principalColumn: "order_item_id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_delivery_product_issue_reports_orders_order_id",
                    column: x => x.order_id,
                    principalTable: "orders",
                    principalColumn: "order_id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_delivery_product_issue_reports_projects_project_id",
                    column: x => x.project_id,
                    principalTable: "projects",
                    principalColumn: "project_id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "idx_operational_delay_reports_project_phase",
            table: "operational_delay_reports",
            columns: new[] { "project_id", "report_phase" });

        migrationBuilder.CreateIndex(
            name: "idx_operational_delay_reports_production_request",
            table: "operational_delay_reports",
            column: "production_request_id");

        migrationBuilder.CreateIndex(
            name: "idx_operational_delay_reports_order",
            table: "operational_delay_reports",
            column: "order_id");

        migrationBuilder.CreateIndex(
            name: "idx_operational_delay_reports_reported_at",
            table: "operational_delay_reports",
            column: "reported_at");

        migrationBuilder.CreateIndex(
            name: "idx_delivery_product_issue_reports_project",
            table: "delivery_product_issue_reports",
            column: "project_id");

        migrationBuilder.CreateIndex(
            name: "idx_delivery_product_issue_reports_order",
            table: "delivery_product_issue_reports",
            column: "order_id");

        migrationBuilder.CreateIndex(
            name: "idx_delivery_product_issue_reports_order_item",
            table: "delivery_product_issue_reports",
            column: "order_item_id");

        migrationBuilder.CreateIndex(
            name: "idx_delivery_product_issue_reports_delivery_item",
            table: "delivery_product_issue_reports",
            column: "delivery_item_id");

        migrationBuilder.CreateIndex(
            name: "idx_delivery_product_issue_reports_reported_at",
            table: "delivery_product_issue_reports",
            column: "reported_at");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "delivery_product_issue_reports");
        migrationBuilder.DropTable(name: "operational_delay_reports");
    }
}
