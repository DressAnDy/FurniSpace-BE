using FurniSpace.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurniSpace.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260812180000_RemoveLegacyBlockedProductionStates")]
public partial class RemoveLegacyBlockedProductionStates : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE production_requests ALTER COLUMN status DROP DEFAULT;

            CREATE TYPE production_request_status_new AS ENUM (
                'PENDING_REVIEW',
                'FEASIBLE',
                'IN_PRODUCTION',
                'COMPLETED',
                'CANCELLED'
            );

            ALTER TABLE production_requests
            ALTER COLUMN status TYPE production_request_status_new
            USING (
                CASE status::text
                    WHEN 'BLOCKED' THEN 'IN_PRODUCTION'
                    ELSE status::text
                END
            )::production_request_status_new;

            DROP TYPE production_request_status;
            ALTER TYPE production_request_status_new RENAME TO production_request_status;
            ALTER TABLE production_requests ALTER COLUMN status SET DEFAULT 'PENDING_REVIEW'::production_request_status;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE production_items ALTER COLUMN status DROP DEFAULT;

            CREATE TYPE production_item_status_new AS ENUM (
                'PENDING',
                'IN_PRODUCTION',
                'COMPLETED',
                'CANCELLED'
            );

            ALTER TABLE production_items
            ALTER COLUMN status TYPE production_item_status_new
            USING (
                CASE status::text
                    WHEN 'BLOCKED' THEN 'IN_PRODUCTION'
                    ELSE status::text
                END
            )::production_item_status_new;

            DROP TYPE production_item_status;
            ALTER TYPE production_item_status_new RENAME TO production_item_status;
            ALTER TABLE production_items ALTER COLUMN status SET DEFAULT 'PENDING'::production_item_status;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE projects ALTER COLUMN status DROP DEFAULT;

            CREATE TYPE project_status_new AS ENUM (
                'SUBMITTED',
                'IN_CONSULTATION',
                'NEED_BASIC_INFORMATION',
                'WAITING_FOR_DESIGNER_ASSIGNMENT',
                'MEASUREMENT_REQUIRED',
                'SPACE_VERIFIED',
                'PROPOSAL_CONSULTING',
                'PROPOSAL_SELECTED',
                'QUOTATION_SENT',
                'QUOTATION_REVISION_REQUESTED',
                'ORDER_CONFIRMED',
                'IN_PRODUCTION',
                'READY_FOR_DELIVERY',
                'DELIVERING',
                'DELIVERED',
                'COMPLETED',
                'REJECTED'
            );

            ALTER TABLE projects
            ALTER COLUMN status TYPE project_status_new
            USING (
                CASE status::text
                    WHEN 'PRODUCTION_BLOCKED' THEN 'IN_PRODUCTION'
                    ELSE status::text
                END
            )::project_status_new;

            DROP TYPE project_status;
            ALTER TYPE project_status_new RENAME TO project_status;
            ALTER TABLE projects ALTER COLUMN status SET DEFAULT 'SUBMITTED'::project_status;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE production_requests ALTER COLUMN status DROP DEFAULT;

            CREATE TYPE production_request_status_old AS ENUM (
                'PENDING_REVIEW',
                'FEASIBLE',
                'IN_PRODUCTION',
                'COMPLETED',
                'BLOCKED',
                'CANCELLED'
            );

            ALTER TABLE production_requests
            ALTER COLUMN status TYPE production_request_status_old
            USING status::text::production_request_status_old;

            DROP TYPE production_request_status;
            ALTER TYPE production_request_status_old RENAME TO production_request_status;
            ALTER TABLE production_requests ALTER COLUMN status SET DEFAULT 'PENDING_REVIEW'::production_request_status;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE production_items ALTER COLUMN status DROP DEFAULT;

            CREATE TYPE production_item_status_old AS ENUM (
                'PENDING',
                'IN_PRODUCTION',
                'COMPLETED',
                'BLOCKED',
                'CANCELLED'
            );

            ALTER TABLE production_items
            ALTER COLUMN status TYPE production_item_status_old
            USING status::text::production_item_status_old;

            DROP TYPE production_item_status;
            ALTER TYPE production_item_status_old RENAME TO production_item_status;
            ALTER TABLE production_items ALTER COLUMN status SET DEFAULT 'PENDING'::production_item_status;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE projects ALTER COLUMN status DROP DEFAULT;

            CREATE TYPE project_status_old AS ENUM (
                'SUBMITTED',
                'IN_CONSULTATION',
                'NEED_BASIC_INFORMATION',
                'WAITING_FOR_DESIGNER_ASSIGNMENT',
                'MEASUREMENT_REQUIRED',
                'SPACE_VERIFIED',
                'PROPOSAL_CONSULTING',
                'PROPOSAL_SELECTED',
                'QUOTATION_SENT',
                'QUOTATION_REVISION_REQUESTED',
                'ORDER_CONFIRMED',
                'IN_PRODUCTION',
                'PRODUCTION_BLOCKED',
                'READY_FOR_DELIVERY',
                'DELIVERING',
                'DELIVERED',
                'COMPLETED',
                'REJECTED'
            );

            ALTER TABLE projects
            ALTER COLUMN status TYPE project_status_old
            USING status::text::project_status_old;

            DROP TYPE project_status;
            ALTER TYPE project_status_old RENAME TO project_status;
            ALTER TABLE projects ALTER COLUMN status SET DEFAULT 'SUBMITTED'::project_status;
            """);
    }
}
