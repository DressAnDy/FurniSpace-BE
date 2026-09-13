using FurniSpace.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurniSpace.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260822153000_AddFileTypePreview")]
public partial class AddFileTypePreview : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TYPE file_type ADD VALUE IF NOT EXISTS 'PREVIEW' AFTER 'TEXTURE';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
