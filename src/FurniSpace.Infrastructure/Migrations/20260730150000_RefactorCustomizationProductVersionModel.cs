using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurniSpace.Infrastructure.Migrations;

/// <inheritdoc />
public partial class RefactorCustomizationProductVersionModel : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // CUSTOM-BE-01: add source product_version_id on customization_requests
        migrationBuilder.AddColumn<Guid>(
            name: "product_version_id",
            table: "customization_requests",
            type: "uuid",
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE customization_requests AS cr
            SET product_version_id = pi.product_version_id
            FROM proposal_items AS pi
            WHERE cr.proposal_item_id = pi.proposal_item_id
              AND pi.product_version_id IS NOT NULL;
            """);

        migrationBuilder.Sql(
            """
            DO $$
            DECLARE
                orphan_count integer;
            BEGIN
                SELECT COUNT(*) INTO orphan_count
                FROM customization_requests
                WHERE product_version_id IS NULL;

                IF orphan_count > 0 THEN
                    RAISE EXCEPTION
                        'CUSTOM-BE-01 migration failed: % customization_requests row(s) could not resolve source product_version_id from proposal_items. Reconcile data before retrying.',
                        orphan_count;
                END IF;
            END $$;
            """);

        migrationBuilder.AlterColumn<Guid>(
            name: "product_version_id",
            table: "customization_requests",
            type: "uuid",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uuid",
            oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_customization_requests_product_version_id",
            table: "customization_requests",
            column: "product_version_id");

        migrationBuilder.CreateIndex(
            name: "idx_customization_requests_project_proposal_product_version",
            table: "customization_requests",
            columns: new[] { "project_id", "proposal_id", "product_version_id" });

        migrationBuilder.AddForeignKey(
            name: "FK_customization_requests_product_versions_product_version_id",
            table: "customization_requests",
            column: "product_version_id",
            principalTable: "product_versions",
            principalColumn: "product_version_id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.DropForeignKey(
            name: "FK_customization_requests_proposal_items_proposal_item_id",
            table: "customization_requests");

        migrationBuilder.DropIndex(
            name: "IX_customization_requests_proposal_item_id",
            table: "customization_requests");

        migrationBuilder.DropColumn(
            name: "proposal_item_id",
            table: "customization_requests");

        // CUSTOM-BE-02: remove approved_product_version_id from proposal_items
        migrationBuilder.Sql(
            """
            DO $$
            DECLARE
                approved_count integer;
            BEGIN
                SELECT COUNT(*) INTO approved_count
                FROM proposal_items
                WHERE approved_product_version_id IS NOT NULL;

                IF approved_count > 0 THEN
                    RAISE EXCEPTION
                        'CUSTOM-BE-02 migration failed: % proposal_items row(s) still have approved_product_version_id. Reset or reconcile non-production data before dropping the column (no silent conversion).',
                        approved_count;
                END IF;
            END $$;
            """);

        migrationBuilder.DropForeignKey(
            name: "FK_proposal_items_product_versions_approved_product_version_id",
            table: "proposal_items");

        migrationBuilder.DropIndex(
            name: "IX_proposal_items_approved_product_version_id",
            table: "proposal_items");

        migrationBuilder.DropColumn(
            name: "approved_product_version_id",
            table: "proposal_items");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "approved_product_version_id",
            table: "proposal_items",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_proposal_items_approved_product_version_id",
            table: "proposal_items",
            column: "approved_product_version_id");

        migrationBuilder.AddForeignKey(
            name: "FK_proposal_items_product_versions_approved_product_version_id",
            table: "proposal_items",
            column: "approved_product_version_id",
            principalTable: "product_versions",
            principalColumn: "product_version_id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddColumn<Guid>(
            name: "proposal_item_id",
            table: "customization_requests",
            type: "uuid",
            nullable: true);

        migrationBuilder.Sql(
            """
            -- Best-effort reverse: restore proposal_item_id when a unique active match exists.
            -- Rows that cannot be restored remain null and fail the NOT NULL alter below.
            UPDATE customization_requests AS cr
            SET proposal_item_id = matched.proposal_item_id
            FROM (
                SELECT DISTINCT ON (pi.proposal_id, pi.product_version_id)
                    pi.proposal_item_id,
                    pi.proposal_id,
                    pi.product_version_id
                FROM proposal_items AS pi
                WHERE pi.product_version_id IS NOT NULL
                ORDER BY pi.proposal_id, pi.product_version_id, pi.created_at DESC NULLS LAST
            ) AS matched
            WHERE cr.proposal_id = matched.proposal_id
              AND cr.product_version_id = matched.product_version_id
              AND cr.proposal_item_id IS NULL;
            """);

        migrationBuilder.Sql(
            """
            DO $$
            DECLARE
                orphan_count integer;
            BEGIN
                SELECT COUNT(*) INTO orphan_count
                FROM customization_requests
                WHERE proposal_item_id IS NULL;

                IF orphan_count > 0 THEN
                    RAISE EXCEPTION
                        'CUSTOM-BE-01 rollback failed: % customization_requests row(s) could not restore proposal_item_id.',
                        orphan_count;
                END IF;
            END $$;
            """);

        migrationBuilder.AlterColumn<Guid>(
            name: "proposal_item_id",
            table: "customization_requests",
            type: "uuid",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uuid",
            oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_customization_requests_proposal_item_id",
            table: "customization_requests",
            column: "proposal_item_id");

        migrationBuilder.AddForeignKey(
            name: "FK_customization_requests_proposal_items_proposal_item_id",
            table: "customization_requests",
            column: "proposal_item_id",
            principalTable: "proposal_items",
            principalColumn: "proposal_item_id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.DropForeignKey(
            name: "FK_customization_requests_product_versions_product_version_id",
            table: "customization_requests");

        migrationBuilder.DropIndex(
            name: "idx_customization_requests_project_proposal_product_version",
            table: "customization_requests");

        migrationBuilder.DropIndex(
            name: "IX_customization_requests_product_version_id",
            table: "customization_requests");

        migrationBuilder.DropColumn(
            name: "product_version_id",
            table: "customization_requests");
    }
}
