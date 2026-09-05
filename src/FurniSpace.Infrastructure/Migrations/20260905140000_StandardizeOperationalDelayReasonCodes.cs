using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurniSpace.Infrastructure.Migrations;

public partial class StandardizeOperationalDelayReasonCodes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'production_delay_reason_code') THEN
                    CREATE TYPE production_delay_reason_code AS ENUM (
                        'MATERIAL_DELAY',
                        'TECHNICAL_ISSUE',
                        'CUSTOMIZATION_ISSUE',
                        'CAPACITY_CONSTRAINT',
                        'QUALITY_REWORK',
                        'DEPENDENCY_DELAY',
                        'OTHER'
                    );
                END IF;
            END $$;
            """);

        migrationBuilder.Sql("""
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'delivery_delay_reason_code') THEN
                    CREATE TYPE delivery_delay_reason_code AS ENUM (
                        'CUSTOMER_RESCHEDULE',
                        'VEHICLE_ISSUE',
                        'PRODUCT_NOT_READY',
                        'SITE_NOT_READY',
                        'STAFF_UNAVAILABLE',
                        'WEATHER',
                        'ACCESS_RESTRICTION',
                        'OTHER'
                    );
                END IF;
            END $$;
            """);

        migrationBuilder.AddColumn<string>(
            name: "production_reason_code",
            table: "operational_delay_reports",
            type: "production_delay_reason_code",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "delivery_reason_code",
            table: "operational_delay_reports",
            type: "delivery_delay_reason_code",
            nullable: true);

        migrationBuilder.Sql("""
            UPDATE operational_delay_reports
            SET production_reason_code = CASE UPPER(BTRIM(COALESCE(reason_code, '')))
                    WHEN 'MATERIAL_DELAY' THEN 'MATERIAL_DELAY'::production_delay_reason_code
                    WHEN 'TECHNICAL_ISSUE' THEN 'TECHNICAL_ISSUE'::production_delay_reason_code
                    WHEN 'CUSTOMIZATION_ISSUE' THEN 'CUSTOMIZATION_ISSUE'::production_delay_reason_code
                    WHEN 'CAPACITY_CONSTRAINT' THEN 'CAPACITY_CONSTRAINT'::production_delay_reason_code
                    WHEN 'QUALITY_REWORK' THEN 'QUALITY_REWORK'::production_delay_reason_code
                    WHEN 'DEPENDENCY_DELAY' THEN 'DEPENDENCY_DELAY'::production_delay_reason_code
                    WHEN 'OTHER' THEN 'OTHER'::production_delay_reason_code
                    ELSE 'OTHER'::production_delay_reason_code
                END,
                reason_detail = CASE
                    WHEN reason_code IS NOT NULL
                         AND BTRIM(reason_code) <> ''
                         AND UPPER(BTRIM(reason_code)) NOT IN (
                             'MATERIAL_DELAY',
                             'TECHNICAL_ISSUE',
                             'CUSTOMIZATION_ISSUE',
                             'CAPACITY_CONSTRAINT',
                             'QUALITY_REWORK',
                             'DEPENDENCY_DELAY',
                             'OTHER')
                    THEN CASE
                        WHEN reason_detail IS NULL OR BTRIM(reason_detail) = '' THEN 'Legacy reason: ' || BTRIM(reason_code)
                        WHEN reason_detail NOT ILIKE '%' || BTRIM(reason_code) || '%' THEN BTRIM(reason_code) || ' — ' || reason_detail
                        ELSE reason_detail
                    END
                    ELSE reason_detail
                END
            WHERE report_phase = 'PRODUCTION'::operational_delay_phase;
            """);

        migrationBuilder.Sql("""
            UPDATE operational_delay_reports
            SET delivery_reason_code = CASE UPPER(BTRIM(COALESCE(reason_code, '')))
                    WHEN 'CUSTOMER_RESCHEDULE' THEN 'CUSTOMER_RESCHEDULE'::delivery_delay_reason_code
                    WHEN 'VEHICLE_ISSUE' THEN 'VEHICLE_ISSUE'::delivery_delay_reason_code
                    WHEN 'PRODUCT_NOT_READY' THEN 'PRODUCT_NOT_READY'::delivery_delay_reason_code
                    WHEN 'SITE_NOT_READY' THEN 'SITE_NOT_READY'::delivery_delay_reason_code
                    WHEN 'STAFF_UNAVAILABLE' THEN 'STAFF_UNAVAILABLE'::delivery_delay_reason_code
                    WHEN 'WEATHER' THEN 'WEATHER'::delivery_delay_reason_code
                    WHEN 'ACCESS_RESTRICTION' THEN 'ACCESS_RESTRICTION'::delivery_delay_reason_code
                    WHEN 'OTHER' THEN 'OTHER'::delivery_delay_reason_code
                    ELSE 'OTHER'::delivery_delay_reason_code
                END,
                reason_detail = CASE
                    WHEN reason_code IS NOT NULL
                         AND BTRIM(reason_code) <> ''
                         AND UPPER(BTRIM(reason_code)) NOT IN (
                             'CUSTOMER_RESCHEDULE',
                             'VEHICLE_ISSUE',
                             'PRODUCT_NOT_READY',
                             'SITE_NOT_READY',
                             'STAFF_UNAVAILABLE',
                             'WEATHER',
                             'ACCESS_RESTRICTION',
                             'OTHER')
                    THEN CASE
                        WHEN reason_detail IS NULL OR BTRIM(reason_detail) = '' THEN 'Legacy reason: ' || BTRIM(reason_code)
                        WHEN reason_detail NOT ILIKE '%' || BTRIM(reason_code) || '%' THEN BTRIM(reason_code) || ' — ' || reason_detail
                        ELSE reason_detail
                    END
                    ELSE reason_detail
                END
            WHERE report_phase = 'DELIVERY'::operational_delay_phase;
            """);

        migrationBuilder.DropColumn(
            name: "reason_code",
            table: "operational_delay_reports");

        migrationBuilder.AddCheckConstraint(
            name: "ck_operational_delay_reports_phase_reason",
            table: "operational_delay_reports",
            sql: "(report_phase = 'PRODUCTION'::operational_delay_phase AND production_reason_code IS NOT NULL AND delivery_reason_code IS NULL) OR (report_phase = 'DELIVERY'::operational_delay_phase AND delivery_reason_code IS NOT NULL AND production_reason_code IS NULL)");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_operational_delay_reports_phase_reason",
            table: "operational_delay_reports");

        migrationBuilder.AddColumn<string>(
            name: "reason_code",
            table: "operational_delay_reports",
            type: "varchar(100)",
            nullable: true);

        migrationBuilder.Sql("""
            UPDATE operational_delay_reports
            SET reason_code = production_reason_code::text
            WHERE report_phase = 'PRODUCTION'::operational_delay_phase
              AND production_reason_code IS NOT NULL;
            """);

        migrationBuilder.Sql("""
            UPDATE operational_delay_reports
            SET reason_code = delivery_reason_code::text
            WHERE report_phase = 'DELIVERY'::operational_delay_phase
              AND delivery_reason_code IS NOT NULL;
            """);

        migrationBuilder.DropColumn(
            name: "production_reason_code",
            table: "operational_delay_reports");

        migrationBuilder.DropColumn(
            name: "delivery_reason_code",
            table: "operational_delay_reports");

        migrationBuilder.Sql("DROP TYPE IF EXISTS production_delay_reason_code;");
        migrationBuilder.Sql("DROP TYPE IF EXISTS delivery_delay_reason_code;");
    }
}
