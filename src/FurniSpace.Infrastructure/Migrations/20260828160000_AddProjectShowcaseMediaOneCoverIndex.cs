using FurniSpace.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurniSpace.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260828160000_AddProjectShowcaseMediaOneCoverIndex")]
public partial class AddProjectShowcaseMediaOneCoverIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            WITH ranked_covers AS (
                SELECT
                    project_showcase_media_id,
                    ROW_NUMBER() OVER (
                        PARTITION BY project_showcase_id
                        ORDER BY display_order, created_at, project_showcase_media_id
                    ) AS cover_rank
                FROM project_showcase_media
                WHERE is_cover = TRUE
            )
            UPDATE project_showcase_media AS media
            SET
                is_cover = FALSE,
                updated_at = NOW()
            FROM ranked_covers
            WHERE media.project_showcase_media_id = ranked_covers.project_showcase_media_id
              AND ranked_covers.cover_rank > 1;
            """);

        migrationBuilder.CreateIndex(
            name: "ux_project_showcase_media_one_cover",
            table: "project_showcase_media",
            column: "project_showcase_id",
            unique: true,
            filter: "is_cover = true");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ux_project_showcase_media_one_cover",
            table: "project_showcase_media");
    }
}
