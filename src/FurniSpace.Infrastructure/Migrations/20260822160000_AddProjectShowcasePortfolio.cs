using System;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurniSpace.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260822160000_AddProjectShowcasePortfolio")]
public partial class AddProjectShowcasePortfolio : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterDatabase()
            .Annotation("Npgsql:Enum:project_showcase_media_type", "BEFORE,AFTER,FINAL,DETAIL,OTHER")
            .Annotation("Npgsql:Enum:project_showcase_status", "DRAFT,PENDING_REVIEW,PUBLISHED,ARCHIVED");

        migrationBuilder.Sql("""
            ALTER TYPE file_type ADD VALUE IF NOT EXISTS 'PORTFOLIO_IMAGE' AFTER 'REVIEW_IMAGE';
            """);

        migrationBuilder.AddColumn<bool>(
            name: "allow_public_display",
            table: "project_reviews",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<DateTime>(
            name: "public_display_consent_at",
            table: "project_reviews",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "project_showcases",
            columns: table => new
            {
                project_showcase_id = table.Column<Guid>(type: "uuid", nullable: false),
                project_id = table.Column<Guid>(type: "uuid", nullable: false),
                featured_review_id = table.Column<Guid>(type: "uuid", nullable: true),
                title = table.Column<string>(type: "varchar(150)", nullable: false),
                slug = table.Column<string>(type: "varchar(255)", nullable: false),
                summary = table.Column<string>(type: "text", nullable: true),
                description = table.Column<string>(type: "text", nullable: true),
                status = table.Column<ProjectShowcaseStatus>(type: "project_showcase_status", nullable: false, defaultValueSql: "'DRAFT'::project_showcase_status"),
                created_by = table.Column<Guid>(type: "uuid", nullable: true),
                approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                published_by = table.Column<Guid>(type: "uuid", nullable: true),
                approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                archived_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_project_showcases", x => x.project_showcase_id);
                table.ForeignKey(
                    name: "FK_project_showcases_accounts_approved_by",
                    column: x => x.approved_by,
                    principalTable: "accounts",
                    principalColumn: "account_id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_project_showcases_accounts_created_by",
                    column: x => x.created_by,
                    principalTable: "accounts",
                    principalColumn: "account_id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_project_showcases_accounts_published_by",
                    column: x => x.published_by,
                    principalTable: "accounts",
                    principalColumn: "account_id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_project_showcases_project_reviews_featured_review_id",
                    column: x => x.featured_review_id,
                    principalTable: "project_reviews",
                    principalColumn: "review_id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_project_showcases_projects_project_id",
                    column: x => x.project_id,
                    principalTable: "projects",
                    principalColumn: "project_id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "project_showcase_media",
            columns: table => new
            {
                project_showcase_media_id = table.Column<Guid>(type: "uuid", nullable: false),
                project_showcase_id = table.Column<Guid>(type: "uuid", nullable: false),
                file_id = table.Column<Guid>(type: "uuid", nullable: false),
                media_type = table.Column<ProjectShowcaseMediaType>(type: "project_showcase_media_type", nullable: false),
                title = table.Column<string>(type: "varchar(150)", nullable: true),
                caption = table.Column<string>(type: "text", nullable: true),
                is_cover = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                display_order = table.Column<int>(type: "integer", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_project_showcase_media", x => x.project_showcase_media_id);
                table.ForeignKey(
                    name: "FK_project_showcase_media_files_file_id",
                    column: x => x.file_id,
                    principalTable: "files",
                    principalColumn: "file_id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_project_showcase_media_project_showcases_project_showcase_id",
                    column: x => x.project_showcase_id,
                    principalTable: "project_showcases",
                    principalColumn: "project_showcase_id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_project_showcase_media_file_id",
            table: "project_showcase_media",
            column: "file_id");

        migrationBuilder.CreateIndex(
            name: "IX_project_showcase_media_project_showcase_id_display_order",
            table: "project_showcase_media",
            columns: new[] { "project_showcase_id", "display_order" });

        migrationBuilder.CreateIndex(
            name: "IX_project_showcase_media_project_showcase_id_file_id",
            table: "project_showcase_media",
            columns: new[] { "project_showcase_id", "file_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_project_showcases_approved_by",
            table: "project_showcases",
            column: "approved_by");

        migrationBuilder.CreateIndex(
            name: "IX_project_showcases_created_by",
            table: "project_showcases",
            column: "created_by");

        migrationBuilder.CreateIndex(
            name: "IX_project_showcases_featured_review_id",
            table: "project_showcases",
            column: "featured_review_id");

        migrationBuilder.CreateIndex(
            name: "IX_project_showcases_project_id",
            table: "project_showcases",
            column: "project_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_project_showcases_published_by",
            table: "project_showcases",
            column: "published_by");

        migrationBuilder.CreateIndex(
            name: "IX_project_showcases_slug",
            table: "project_showcases",
            column: "slug",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_project_showcases_status",
            table: "project_showcases",
            column: "status");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "project_showcase_media");
        migrationBuilder.DropTable(name: "project_showcases");

        migrationBuilder.DropColumn(
            name: "allow_public_display",
            table: "project_reviews");

        migrationBuilder.DropColumn(
            name: "public_display_consent_at",
            table: "project_reviews");
    }
}
