using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurniSpace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductPreviewFileLinkIndexes : Migration
    {
        private static readonly string[] CatalogPreviewOrderIndexColumns =
            ["reference_type", "reference_id", "file_type", "display_order"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                WITH ranked AS (
                    SELECT
                        fl.file_link_id,
                        ROW_NUMBER() OVER (
                            PARTITION BY fl.reference_type, fl.reference_id
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
                    WHERE fl.reference_type IN ('PRODUCT', 'PRODUCT_VERSION')
                      AND fl.file_type = 'PRODUCT_PREVIEW'::file_type
                )
                UPDATE file_links fl
                SET
                    display_order = ranked.new_display_order,
                    is_primary = ranked.new_display_order = 1
                FROM ranked
                WHERE fl.file_link_id = ranked.file_link_id;
                """);

            migrationBuilder.CreateIndex(
                name: "idx_file_links_reference_type_order",
                table: "file_links",
                columns: CatalogPreviewOrderIndexColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_file_links_reference_type_order",
                table: "file_links");
        }
    }
}
