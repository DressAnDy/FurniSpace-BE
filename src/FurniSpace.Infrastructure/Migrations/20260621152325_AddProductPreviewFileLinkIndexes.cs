using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurniSpace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductPreviewFileLinkIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                WITH ranked AS (
                    SELECT
                        fl.file_link_id,
                        ROW_NUMBER() OVER (
                            PARTITION BY fl.reference_id
                            ORDER BY
                                CASE
                                    WHEN fl.display_order IS NULL OR fl.display_order <= 0 THEN 2147483647
                                    ELSE fl.display_order
                                END,
                                fl.created_at NULLS LAST,
                                fl.file_link_id
                        ) AS new_display_order
                    FROM file_links fl
                    INNER JOIN files sf
                        ON sf.file_id = fl.file_id
                        AND sf.status = 'ACTIVE'::file_status
                    WHERE fl.reference_type = 'PRODUCT'
                      AND fl.file_type = 'PRODUCT_PREVIEW'::file_type
                )
                UPDATE file_links fl
                SET display_order = ranked.new_display_order
                FROM ranked
                WHERE fl.file_link_id = ranked.file_link_id;
                """);

            migrationBuilder.CreateIndex(
                name: "idx_file_links_reference_type_display_order",
                table: "file_links",
                columns: new[] { "reference_type", "reference_id", "file_type", "display_order" });

            migrationBuilder.CreateIndex(
                name: "uq_file_links_product_preview_display_order",
                table: "file_links",
                columns: new[] { "reference_type", "reference_id", "display_order" },
                unique: true,
                filter: "reference_type = 'PRODUCT' AND file_type = 'PRODUCT_PREVIEW'::file_type AND display_order > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_file_links_reference_type_display_order",
                table: "file_links");

            migrationBuilder.DropIndex(
                name: "uq_file_links_product_preview_display_order",
                table: "file_links");
        }
    }
}
