using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurniSpace.Infrastructure.Migrations;

/// <inheritdoc />
public partial class CustomizationMultiVersionModel : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pgcrypto;");

        migrationBuilder.Sql(
            """
            DO $$ BEGIN
                CREATE TYPE customization_version_status AS ENUM (
                    'DRAFT', 'REVIEWING', 'PRODUCTION_REJECTED', 'ACCEPTED', 'WITHDRAWN');
            EXCEPTION WHEN duplicate_object THEN NULL;
            END $$;
            """);

        migrationBuilder.Sql(
            """
            DO $$ BEGIN
                CREATE TYPE production_feasibility_status AS ENUM ('PENDING', 'FEASIBLE', 'NOT_FEASIBLE');
            EXCEPTION WHEN duplicate_object THEN NULL;
            END $$;
            """);

        migrationBuilder.Sql(
            """
            DO $$ BEGIN
                CREATE TYPE customization_status_new AS ENUM ('SUBMITTED', 'REVIEWING', 'ACCEPTED', 'CANCELLED');
            EXCEPTION WHEN duplicate_object THEN NULL;
            END $$;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE customization_requests
            ALTER COLUMN status DROP DEFAULT;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE customization_requests
            ALTER COLUMN status TYPE customization_status_new
            USING (
                CASE status::text
                    WHEN 'SUBMITTED' THEN 'SUBMITTED'
                    WHEN 'DESIGN_REVIEWING' THEN 'REVIEWING'
                    WHEN 'PRODUCTION_REVIEWING' THEN 'REVIEWING'
                    WHEN 'WAITING_FOR_CUSTOMER_FINAL_APPROVAL' THEN 'REVIEWING'
                    WHEN 'NOT_FEASIBLE' THEN 'REVIEWING'
                    WHEN 'ACCEPTED' THEN 'ACCEPTED'
                    WHEN 'REJECTED_BY_CUSTOMER' THEN 'CANCELLED'
                    WHEN 'CANCELLED' THEN 'CANCELLED'
                    ELSE 'SUBMITTED'
                END
            )::customization_status_new;
            """);

        migrationBuilder.Sql("DROP TYPE IF EXISTS customization_status;");
        migrationBuilder.Sql("ALTER TYPE customization_status_new RENAME TO customization_status;");
        migrationBuilder.Sql(
            """
            ALTER TABLE customization_requests
            ALTER COLUMN status SET DEFAULT 'SUBMITTED'::customization_status;
            """);

        migrationBuilder.Sql(
            """
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'customization_requests' AND column_name = 'proposal_item_id')
                THEN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'customization_requests' AND column_name = 'product_version_id')
                    THEN
                        ALTER TABLE customization_requests ADD COLUMN product_version_id uuid;
                        UPDATE customization_requests AS cr
                        SET product_version_id = pi.product_version_id
                        FROM proposal_items AS pi
                        WHERE cr.proposal_item_id = pi.proposal_item_id
                          AND pi.product_version_id IS NOT NULL;
                        ALTER TABLE customization_requests ALTER COLUMN product_version_id SET NOT NULL;
                    END IF;

                    ALTER TABLE customization_requests DROP CONSTRAINT IF EXISTS
                        "FK_customization_requests_proposal_items_proposal_item_id";
                    DROP INDEX IF EXISTS "IX_customization_requests_proposal_item_id";
                    ALTER TABLE customization_requests DROP COLUMN proposal_item_id;
                END IF;
            END $$;
            """);

        migrationBuilder.Sql(
            """
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'customization_requests' AND column_name = 'product_version_id')
                   AND NOT EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'customization_requests' AND column_name = 'source_product_version_id')
                THEN
                    ALTER TABLE customization_requests RENAME COLUMN product_version_id TO source_product_version_id;
                END IF;
            END $$;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE customization_requests DROP CONSTRAINT IF EXISTS ck_customization_requests_additional_cost_reason;
            """);

        migrationBuilder.CreateTable(
            name: "customization_request_versions",
            columns: table => new
            {
                customization_request_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                customization_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                product_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                version_no = table.Column<int>(type: "integer", nullable: false),
                created_by_designer_id = table.Column<Guid>(type: "uuid", nullable: false),
                version_title = table.Column<string>(type: "varchar(150)", nullable: true),
                designer_note = table.Column<string>(type: "text", nullable: true),
                status = table.Column<string>(type: "customization_version_status", nullable: false, defaultValueSql: "'DRAFT'::customization_version_status"),
                production_reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                feasibility_status = table.Column<string>(type: "production_feasibility_status", nullable: false, defaultValueSql: "'PENDING'::production_feasibility_status"),
                feasibility_note = table.Column<string>(type: "text", nullable: true),
                estimated_production_days = table.Column<int>(type: "integer", nullable: true),
                estimated_additional_cost = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                additional_cost_reason = table.Column<string>(type: "text", nullable: true),
                material_available = table.Column<bool>(type: "boolean", nullable: true),
                production_risk_note = table.Column<string>(type: "text", nullable: true),
                alternative_material_note = table.Column<string>(type: "text", nullable: true),
                submitted_for_review_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                production_reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                production_rejected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                accepted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                withdrawn_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_customization_request_versions", x => x.customization_request_version_id);
                table.ForeignKey(
                    name: "FK_customization_request_versions_accounts_created_by_designer_id",
                    column: x => x.created_by_designer_id,
                    principalTable: "accounts",
                    principalColumn: "account_id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_customization_request_versions_accounts_production_reviewed_by",
                    column: x => x.production_reviewed_by,
                    principalTable: "accounts",
                    principalColumn: "account_id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_customization_request_versions_customization_requests_custom~",
                    column: x => x.customization_request_id,
                    principalTable: "customization_requests",
                    principalColumn: "customization_request_id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_customization_request_versions_product_versions_product_versi~",
                    column: x => x.product_version_id,
                    principalTable: "product_versions",
                    principalColumn: "product_version_id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.Sql(
            """
            INSERT INTO customization_request_versions (
                customization_request_version_id,
                customization_request_id,
                product_version_id,
                version_no,
                created_by_designer_id,
                version_title,
                designer_note,
                status,
                production_reviewed_by,
                feasibility_status,
                feasibility_note,
                estimated_production_days,
                estimated_additional_cost,
                additional_cost_reason,
                material_available,
                production_risk_note,
                submitted_for_review_at,
                production_reviewed_at,
                production_rejected_at,
                accepted_at,
                created_at,
                updated_at
            )
            SELECT
                gen_random_uuid(),
                cr.customization_request_id,
                cr.approved_product_version_id,
                1,
                COALESCE(
                    cr.designer_id,
                    p.assigned_designer_id,
                    (SELECT a.account_id FROM accounts a JOIN roles r ON r.role_id = a.role_id WHERE r.role_name = 'ADMIN' LIMIT 1)),
                cr.request_title,
                cr.designer_spec_note,
                CASE
                    WHEN cr.status::text = 'ACCEPTED' THEN 'ACCEPTED'::customization_version_status
                    WHEN cr.status::text = 'CANCELLED' THEN 'WITHDRAWN'::customization_version_status
                    WHEN cr.production_review_by IS NOT NULL AND cr.material_available = false THEN 'PRODUCTION_REJECTED'::customization_version_status
                    WHEN cr.production_review_by IS NOT NULL AND cr.material_available = true THEN 'REVIEWING'::customization_version_status
                    ELSE 'DRAFT'::customization_version_status
                END,
                cr.production_review_by,
                CASE
                    WHEN cr.production_review_by IS NULL THEN 'PENDING'::production_feasibility_status
                    WHEN cr.material_available = true THEN 'FEASIBLE'::production_feasibility_status
                    ELSE 'NOT_FEASIBLE'::production_feasibility_status
                END,
                cr.feasibility_note,
                cr.estimated_production_days,
                cr.estimated_additional_cost,
                cr.additional_cost_reason,
                cr.material_available,
                cr.production_risk_note,
                CASE WHEN cr.production_review_by IS NOT NULL THEN cr.updated_at ELSE NULL END,
                CASE WHEN cr.production_review_by IS NOT NULL THEN cr.updated_at ELSE NULL END,
                CASE WHEN cr.material_available = false AND cr.production_review_by IS NOT NULL THEN cr.updated_at ELSE NULL END,
                cr.customer_accepted_at,
                COALESCE(cr.created_at, NOW()),
                COALESCE(cr.updated_at, NOW())
            FROM customization_requests cr
            JOIN projects p ON p.project_id = cr.project_id
            WHERE cr.approved_product_version_id IS NOT NULL
              AND EXISTS (
                  SELECT 1 FROM information_schema.columns
                  WHERE table_name = 'customization_requests' AND column_name = 'approved_product_version_id');
            """);

        migrationBuilder.AddColumn<Guid>(
            name: "accepted_request_version_id",
            table: "customization_requests",
            type: "uuid",
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE customization_requests cr
            SET accepted_request_version_id = cv.customization_request_version_id
            FROM customization_request_versions cv
            WHERE cv.customization_request_id = cr.customization_request_id
              AND cv.version_no = 1
              AND cr.status::text = 'ACCEPTED';
            """);

        migrationBuilder.CreateIndex(
            name: "IX_customization_requests_accepted_request_version_id",
            table: "customization_requests",
            column: "accepted_request_version_id");

        migrationBuilder.AddForeignKey(
            name: "FK_customization_requests_customization_request_versions_accepte~",
            table: "customization_requests",
            column: "accepted_request_version_id",
            principalTable: "customization_request_versions",
            principalColumn: "customization_request_version_id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.Sql(
            """
            ALTER TABLE customization_requests DROP CONSTRAINT IF EXISTS
                "FK_customization_requests_product_versions_approved_product_version_id";
            DROP INDEX IF EXISTS "IX_customization_requests_approved_product_version_id";
            ALTER TABLE customization_requests DROP COLUMN IF EXISTS approved_product_version_id;
            ALTER TABLE customization_requests DROP COLUMN IF EXISTS designer_id;
            ALTER TABLE customization_requests DROP COLUMN IF EXISTS designer_spec_note;
            ALTER TABLE customization_requests DROP COLUMN IF EXISTS production_review_by;
            ALTER TABLE customization_requests DROP COLUMN IF EXISTS feasibility_note;
            ALTER TABLE customization_requests DROP COLUMN IF EXISTS estimated_production_days;
            ALTER TABLE customization_requests DROP COLUMN IF EXISTS estimated_additional_cost;
            ALTER TABLE customization_requests DROP COLUMN IF EXISTS additional_cost_reason;
            ALTER TABLE customization_requests DROP COLUMN IF EXISTS material_available;
            ALTER TABLE customization_requests DROP COLUMN IF EXISTS production_risk_note;
            ALTER TABLE customization_requests DROP COLUMN IF EXISTS customer_accepted_at;
            ALTER TABLE customization_requests DROP COLUMN IF EXISTS customer_rejected_at;
            """);

        migrationBuilder.CreateIndex(
            name: "IX_customization_request_versions_created_by_designer_id",
            table: "customization_request_versions",
            column: "created_by_designer_id");

        migrationBuilder.CreateIndex(
            name: "IX_customization_request_versions_customization_request_id",
            table: "customization_request_versions",
            column: "customization_request_id");

        migrationBuilder.CreateIndex(
            name: "IX_customization_request_versions_customization_request_id_produ~",
            table: "customization_request_versions",
            columns: new[] { "customization_request_id", "product_version_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_customization_request_versions_customization_request_id_versi~",
            table: "customization_request_versions",
            columns: new[] { "customization_request_id", "version_no" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_customization_request_versions_feasibility_status",
            table: "customization_request_versions",
            column: "feasibility_status");

        migrationBuilder.CreateIndex(
            name: "IX_customization_request_versions_product_version_id",
            table: "customization_request_versions",
            column: "product_version_id");

        migrationBuilder.CreateIndex(
            name: "IX_customization_request_versions_production_reviewed_by",
            table: "customization_request_versions",
            column: "production_reviewed_by");

        migrationBuilder.CreateIndex(
            name: "IX_customization_request_versions_status",
            table: "customization_request_versions",
            column: "status");

        migrationBuilder.Sql(
            """
            DROP INDEX IF EXISTS idx_customization_requests_project_proposal_product_version;
            DROP INDEX IF EXISTS "IX_customization_requests_product_version_id";
            CREATE INDEX IF NOT EXISTS "IX_customization_requests_source_product_version_id"
                ON customization_requests (source_product_version_id);
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        throw new NotSupportedException(
            "CUSTOM-MV-01 rollback is not supported. Restore from database backup if needed.");
    }
}
