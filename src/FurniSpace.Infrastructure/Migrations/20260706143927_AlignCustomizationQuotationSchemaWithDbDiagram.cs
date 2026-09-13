using System;
using FurniSpace.Domain.Enums;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurniSpace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AlignCustomizationQuotationSchemaWithDbDiagram : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE customization_requests ALTER COLUMN status DROP DEFAULT;

                CREATE TYPE customization_status_new AS ENUM (
                    'SUBMITTED',
                    'DESIGN_REVIEWING',
                    'PRODUCTION_REVIEWING',
                    'WAITING_FOR_CUSTOMER_FINAL_APPROVAL',
                    'NOT_FEASIBLE',
                    'ACCEPTED',
                    'REJECTED_BY_CUSTOMER',
                    'CANCELLED'
                );

                ALTER TABLE customization_requests
                ALTER COLUMN status TYPE customization_status_new
                USING (
                    CASE status::text
                        WHEN 'WAITING_FOR_DESIGN_APPROVAL' THEN 'WAITING_FOR_CUSTOMER_FINAL_APPROVAL'
                        WHEN 'DESIGN_REVISION_REQUESTED' THEN 'WAITING_FOR_CUSTOMER_FINAL_APPROVAL'
                        ELSE status::text
                    END
                )::customization_status_new;

                DROP TYPE customization_status;
                ALTER TYPE customization_status_new RENAME TO customization_status;
                ALTER TABLE customization_requests ALTER COLUMN status SET DEFAULT 'SUBMITTED'::customization_status;
                """);

            migrationBuilder.DropColumn(
                name: "customization_fee",
                table: "quotations");

            migrationBuilder.DropColumn(
                name: "delivery_fee",
                table: "quotations");

            migrationBuilder.DropColumn(
                name: "service_fee",
                table: "quotations");

            migrationBuilder.RenameColumn(
                name: "customization_fee",
                table: "quotation_items",
                newName: "customization_additional_cost");

            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    CREATE TYPE quotation_item_type AS ENUM ('PRODUCT_ITEM', 'MANUAL_ITEM');
                EXCEPTION
                    WHEN duplicate_object THEN NULL;
                END $$;
                """);

            migrationBuilder.AddColumn<string>(
                name: "reject_reason",
                table: "quotations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "revision_reason",
                table: "quotations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "customization_note",
                table: "quotation_items",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "quotation_items",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_customized",
                table: "quotation_items",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "item_name",
                table: "quotation_items",
                type: "varchar(150)",
                nullable: true);

            migrationBuilder.AddColumn<QuotationItemType>(
                name: "item_type",
                table: "quotation_items",
                type: "quotation_item_type",
                nullable: true,
                defaultValueSql: "'PRODUCT_ITEM'::quotation_item_type");

            migrationBuilder.AlterColumn<Guid>(
                name: "proposal_item_id",
                table: "customization_requests",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "proposal_id",
                table: "customization_requests",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "additional_cost_reason",
                table: "customization_requests",
                type: "text",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_customization_requests_additional_cost_reason",
                table: "customization_requests",
                sql: "estimated_additional_cost IS NULL OR estimated_additional_cost <= 0 OR additional_cost_reason IS NOT NULL AND btrim(additional_cost_reason) <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_customization_requests_additional_cost_reason",
                table: "customization_requests");

            migrationBuilder.Sql(
                """
                ALTER TABLE customization_requests ALTER COLUMN status DROP DEFAULT;

                CREATE TYPE customization_status_old AS ENUM (
                    'SUBMITTED',
                    'DESIGN_REVIEWING',
                    'WAITING_FOR_DESIGN_APPROVAL',
                    'DESIGN_REVISION_REQUESTED',
                    'PRODUCTION_REVIEWING',
                    'NOT_FEASIBLE',
                    'ACCEPTED',
                    'REJECTED_BY_CUSTOMER',
                    'CANCELLED'
                );

                ALTER TABLE customization_requests
                ALTER COLUMN status TYPE customization_status_old
                USING (
                    CASE status::text
                        WHEN 'WAITING_FOR_CUSTOMER_FINAL_APPROVAL' THEN 'WAITING_FOR_DESIGN_APPROVAL'
                        ELSE status::text
                    END
                )::customization_status_old;

                DROP TYPE customization_status;
                ALTER TYPE customization_status_old RENAME TO customization_status;
                ALTER TABLE customization_requests ALTER COLUMN status SET DEFAULT 'SUBMITTED'::customization_status;
                """);

            migrationBuilder.DropColumn(
                name: "reject_reason",
                table: "quotations");

            migrationBuilder.DropColumn(
                name: "revision_reason",
                table: "quotations");

            migrationBuilder.DropColumn(
                name: "customization_note",
                table: "quotation_items");

            migrationBuilder.DropColumn(
                name: "description",
                table: "quotation_items");

            migrationBuilder.DropColumn(
                name: "is_customized",
                table: "quotation_items");

            migrationBuilder.DropColumn(
                name: "item_name",
                table: "quotation_items");

            migrationBuilder.DropColumn(
                name: "item_type",
                table: "quotation_items");

            migrationBuilder.DropColumn(
                name: "additional_cost_reason",
                table: "customization_requests");

            migrationBuilder.RenameColumn(
                name: "customization_additional_cost",
                table: "quotation_items",
                newName: "customization_fee");

            migrationBuilder.Sql("DROP TYPE IF EXISTS quotation_item_type;");

            migrationBuilder.AddColumn<decimal>(
                name: "customization_fee",
                table: "quotations",
                type: "numeric(12,2)",
                nullable: true,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "delivery_fee",
                table: "quotations",
                type: "numeric(12,2)",
                nullable: true,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "service_fee",
                table: "quotations",
                type: "numeric(12,2)",
                nullable: true,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<Guid>(
                name: "proposal_item_id",
                table: "customization_requests",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "proposal_id",
                table: "customization_requests",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");
        }
    }
}
