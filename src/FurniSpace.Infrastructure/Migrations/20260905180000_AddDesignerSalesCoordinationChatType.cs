using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurniSpace.Infrastructure.Migrations;

public partial class AddDesignerSalesCoordinationChatType : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1
                    FROM pg_enum e
                    JOIN pg_type t ON e.enumtypid = t.oid
                    WHERE t.typname = 'project_chat_type'
                      AND e.enumlabel = 'DESIGNER_SALES') THEN
                    ALTER TYPE project_chat_type ADD VALUE 'DESIGNER_SALES';
                END IF;
            END $$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // PostgreSQL does not support removing enum values safely.
    }
}
