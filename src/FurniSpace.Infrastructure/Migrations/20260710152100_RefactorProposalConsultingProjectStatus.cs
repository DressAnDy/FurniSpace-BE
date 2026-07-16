using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurniSpace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RefactorProposalConsultingProjectStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                    'PRODUCTION_BLOCKED',
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
                        WHEN 'PROPOSAL_DRAFTING' THEN 'PROPOSAL_CONSULTING'
                        WHEN 'WAITING_FOR_CUSTOMER_REVIEW' THEN 'PROPOSAL_CONSULTING'
                        WHEN 'REVISION_REQUESTED' THEN 'PROPOSAL_CONSULTING'
                        ELSE status::text
                    END
                )::project_status_new;

                DROP TYPE project_status;
                ALTER TYPE project_status_new RENAME TO project_status;
                ALTER TABLE projects ALTER COLUMN status SET DEFAULT 'SUBMITTED'::project_status;
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE proposals ALTER COLUMN status DROP DEFAULT;

                CREATE TYPE proposal_status_new AS ENUM (
                    'DRAFT',
                    'PUBLISHED',
                    'SELECTED',
                    'REVISION_REQUESTED',
                    'REJECTED',
                    'ARCHIVED'
                );

                ALTER TABLE proposals
                ALTER COLUMN status TYPE proposal_status_new
                USING (
                    CASE status::text
                        WHEN 'VIEWED' THEN 'PUBLISHED'
                        ELSE status::text
                    END
                )::proposal_status_new;

                DROP TYPE proposal_status;
                ALTER TYPE proposal_status_new RENAME TO proposal_status;
                ALTER TABLE proposals ALTER COLUMN status SET DEFAULT 'DRAFT'::proposal_status;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
                    'PROPOSAL_DRAFTING',
                    'WAITING_FOR_CUSTOMER_REVIEW',
                    'REVISION_REQUESTED',
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
                USING (
                    CASE status::text
                        WHEN 'PROPOSAL_CONSULTING' THEN 'PROPOSAL_DRAFTING'
                        ELSE status::text
                    END
                )::project_status_old;

                DROP TYPE project_status;
                ALTER TYPE project_status_old RENAME TO project_status;
                ALTER TABLE projects ALTER COLUMN status SET DEFAULT 'SUBMITTED'::project_status;
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE proposals ALTER COLUMN status DROP DEFAULT;

                CREATE TYPE proposal_status_old AS ENUM (
                    'DRAFT',
                    'PUBLISHED',
                    'VIEWED',
                    'SELECTED',
                    'REVISION_REQUESTED',
                    'REJECTED',
                    'ARCHIVED'
                );

                ALTER TABLE proposals
                ALTER COLUMN status TYPE proposal_status_old
                USING status::text::proposal_status_old;

                DROP TYPE proposal_status;
                ALTER TYPE proposal_status_old RENAME TO proposal_status;
                ALTER TABLE proposals ALTER COLUMN status SET DEFAULT 'DRAFT'::proposal_status;
                """);
        }
    }
}
