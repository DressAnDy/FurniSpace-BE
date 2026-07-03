using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurniSpace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProposalItemRoomPlannerMapping : Migration
    {
        private static readonly string[] ProposalItemSceneObjectIndexColumns = ["scene_id", "scene_object_id"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_proposal_items_scene_id",
                table: "proposal_items");

            migrationBuilder.AddColumn<bool>(
                name: "is_customized",
                table: "proposal_items",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "scene_object_id",
                table: "proposal_items",
                type: "character varying(100)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_proposal_items_scene_object",
                table: "proposal_items",
                columns: ProposalItemSceneObjectIndexColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_proposal_items_scene_object",
                table: "proposal_items");

            migrationBuilder.DropColumn(
                name: "is_customized",
                table: "proposal_items");

            migrationBuilder.DropColumn(
                name: "scene_object_id",
                table: "proposal_items");

            migrationBuilder.CreateIndex(
                name: "IX_proposal_items_scene_id",
                table: "proposal_items",
                column: "scene_id");
        }
    }
}
