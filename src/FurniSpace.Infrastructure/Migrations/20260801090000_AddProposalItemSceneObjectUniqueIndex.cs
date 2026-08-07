using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurniSpace.Infrastructure.Migrations;

public partial class AddProposalItemSceneObjectUniqueIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "idx_proposal_items_scene_object",
            table: "proposal_items");

        migrationBuilder.CreateIndex(
            name: "uq_proposal_items_scene_object",
            table: "proposal_items",
            columns: ["scene_id", "scene_object_id"],
            unique: true,
            filter: "scene_id IS NOT NULL AND scene_object_id IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "uq_proposal_items_scene_object",
            table: "proposal_items");

        migrationBuilder.CreateIndex(
            name: "idx_proposal_items_scene_object",
            table: "proposal_items",
            columns: ["scene_id", "scene_object_id"]);
    }
}
