using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurniSpace.Infrastructure.Migrations;

public partial class AddProposalSceneAreasMultiAreaMapping : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TYPE proposal_scene_type ADD VALUE IF NOT EXISTS 'ROOM_PLANNER';");
        migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pgcrypto;");

        migrationBuilder.CreateTable(
            name: "proposal_scene_areas",
            columns: table => new
            {
                proposal_scene_area_id = table.Column<Guid>(type: "uuid", nullable: false),
                scene_id = table.Column<Guid>(type: "uuid", nullable: false),
                project_area_id = table.Column<Guid>(type: "uuid", nullable: false),
                sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_proposal_scene_areas", x => x.proposal_scene_area_id);
                table.ForeignKey(
                    name: "FK_proposal_scene_areas_project_areas_project_area_id",
                    column: x => x.project_area_id,
                    principalTable: "project_areas",
                    principalColumn: "project_area_id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_proposal_scene_areas_proposal_scenes_scene_id",
                    column: x => x.scene_id,
                    principalTable: "proposal_scenes",
                    principalColumn: "scene_id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "idx_proposal_scene_areas_project_area_id",
            table: "proposal_scene_areas",
            column: "project_area_id");

        migrationBuilder.CreateIndex(
            name: "idx_proposal_scene_areas_scene_id",
            table: "proposal_scene_areas",
            column: "scene_id");

        migrationBuilder.CreateIndex(
            name: "uq_proposal_scene_areas_scene_project_area",
            table: "proposal_scene_areas",
            columns: new[] { "scene_id", "project_area_id" },
            unique: true);

        migrationBuilder.Sql(
            """
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1
                    FROM proposal_scenes ps
                    JOIN proposals p ON p.proposal_id = ps.proposal_id
                    JOIN project_areas pa ON pa.project_area_id = ps.project_area_id
                    WHERE ps.project_area_id IS NOT NULL
                      AND p.project_id <> pa.project_id
                ) THEN
                    RAISE EXCEPTION 'SCENE_AREA_PROJECT_MISMATCH: proposal_scenes.project_area_id contains cross-project mappings.';
                END IF;
            END $$;
            """);

        migrationBuilder.Sql(
            """
            INSERT INTO proposal_scene_areas (
                proposal_scene_area_id, scene_id, project_area_id, sort_order, created_at
            )
            SELECT gen_random_uuid(), scene_id, project_area_id, 0, COALESCE(created_at, NOW())
            FROM proposal_scenes
            WHERE project_area_id IS NOT NULL
            ON CONFLICT (scene_id, project_area_id) DO NOTHING;
            """);

        migrationBuilder.DropForeignKey(
            name: "FK_proposal_scenes_project_areas_project_area_id",
            table: "proposal_scenes");

        migrationBuilder.DropIndex(
            name: "IX_proposal_scenes_project_area_id",
            table: "proposal_scenes");

        migrationBuilder.DropColumn(
            name: "project_area_id",
            table: "proposal_scenes");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "project_area_id",
            table: "proposal_scenes",
            type: "uuid",
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE proposal_scenes ps
            SET project_area_id = area.project_area_id
            FROM (
                SELECT DISTINCT ON (scene_id) scene_id, project_area_id
                FROM proposal_scene_areas
                ORDER BY scene_id, sort_order, project_area_id
            ) area
            WHERE ps.scene_id = area.scene_id;
            """);

        migrationBuilder.CreateIndex(
            name: "IX_proposal_scenes_project_area_id",
            table: "proposal_scenes",
            column: "project_area_id");

        migrationBuilder.AddForeignKey(
            name: "FK_proposal_scenes_project_areas_project_area_id",
            table: "proposal_scenes",
            column: "project_area_id",
            principalTable: "project_areas",
            principalColumn: "project_area_id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.DropTable(
            name: "proposal_scene_areas");
    }
}
