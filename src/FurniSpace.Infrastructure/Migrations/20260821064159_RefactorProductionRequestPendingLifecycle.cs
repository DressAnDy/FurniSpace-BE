using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurniSpace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RefactorProductionRequestPendingLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE production_requests ALTER COLUMN status DROP DEFAULT;

                ALTER TYPE production_request_status RENAME TO production_request_status_old;

                CREATE TYPE production_request_status AS ENUM (
                    'PENDING',
                    'IN_PRODUCTION',
                    'COMPLETED',
                    'CANCELLED'
                );

                ALTER TABLE production_requests
                ALTER COLUMN status TYPE production_request_status
                USING (
                    CASE status::text
                        WHEN 'PENDING_REVIEW' THEN 'PENDING'
                        WHEN 'FEASIBLE' THEN 'PENDING'
                        ELSE status::text
                    END
                )::production_request_status;

                ALTER TABLE production_requests ALTER COLUMN status SET DEFAULT 'PENDING'::production_request_status;

                DROP TYPE production_request_status_old;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE production_requests ALTER COLUMN status DROP DEFAULT;

                ALTER TYPE production_request_status RENAME TO production_request_status_new;

                CREATE TYPE production_request_status AS ENUM (
                    'PENDING_REVIEW',
                    'FEASIBLE',
                    'IN_PRODUCTION',
                    'COMPLETED',
                    'CANCELLED'
                );

                ALTER TABLE production_requests
                ALTER COLUMN status TYPE production_request_status
                USING (
                    CASE status::text
                        WHEN 'PENDING' THEN 'PENDING_REVIEW'
                        ELSE status::text
                    END
                )::production_request_status;

                ALTER TABLE production_requests ALTER COLUMN status SET DEFAULT 'PENDING_REVIEW'::production_request_status;

                DROP TYPE production_request_status_new;
                """);
        }
    }
}
