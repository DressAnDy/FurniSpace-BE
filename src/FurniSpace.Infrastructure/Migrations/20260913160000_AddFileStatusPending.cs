using FurniSpace.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurniSpace.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260913160000_AddFileStatusPending")]
public partial class AddFileStatusPending : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1
                    FROM pg_enum
                    WHERE enumlabel = 'PENDING'
                      AND enumtypid = 'file_status'::regtype) THEN
                    ALTER TYPE file_status ADD VALUE 'PENDING';
                END IF;
            END $$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // PostgreSQL does not support removing enum values safely.
    }
}
