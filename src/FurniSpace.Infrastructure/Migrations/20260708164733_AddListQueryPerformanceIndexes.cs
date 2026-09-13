using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurniSpace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddListQueryPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_proposals_project_id",
                table: "proposals");

            migrationBuilder.DropIndex(
                name: "IX_proposal_scenes_proposal_id",
                table: "proposal_scenes");

            migrationBuilder.DropIndex(
                name: "IX_proposal_items_proposal_id",
                table: "proposal_items");

            migrationBuilder.DropIndex(
                name: "IX_projects_assigned_designer_id",
                table: "projects");

            migrationBuilder.DropIndex(
                name: "IX_projects_assigned_sales_id",
                table: "projects");

            migrationBuilder.DropIndex(
                name: "IX_projects_customer_id",
                table: "projects");

            migrationBuilder.DropIndex(
                name: "IX_project_schedules_assigned_staff_id",
                table: "project_schedules");

            migrationBuilder.DropIndex(
                name: "IX_project_schedules_project_id",
                table: "project_schedules");

            migrationBuilder.DropIndex(
                name: "IX_project_chats_project_id",
                table: "project_chats");

            migrationBuilder.DropIndex(
                name: "IX_project_chat_messages_chat_id",
                table: "project_chat_messages");

            migrationBuilder.DropIndex(
                name: "IX_products_category_id",
                table: "products");

            migrationBuilder.CreateIndex(
                name: "idx_proposals_project_list_sort",
                table: "proposals",
                columns: new[] { "project_id", "version_no", "created_at", "proposal_id" },
                descending: new[] { false, true, true, true });

            migrationBuilder.CreateIndex(
                name: "idx_proposal_scenes_proposal_list_sort",
                table: "proposal_scenes",
                columns: new[] { "proposal_id", "version_no", "created_at", "scene_id" });

            migrationBuilder.CreateIndex(
                name: "idx_proposal_items_proposal_list_sort",
                table: "proposal_items",
                columns: new[] { "proposal_id", "item_name" });

            migrationBuilder.CreateIndex(
                name: "idx_projects_customer_list_sort",
                table: "projects",
                columns: new[] { "customer_id", "submitted_at", "project_id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "idx_projects_designer_list_sort",
                table: "projects",
                columns: new[] { "assigned_designer_id", "submitted_at", "project_id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "idx_projects_sales_list_sort",
                table: "projects",
                columns: new[] { "assigned_sales_id", "submitted_at", "project_id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "idx_projects_status_list_sort",
                table: "projects",
                columns: new[] { "status", "submitted_at", "project_id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "idx_project_schedules_project_sort",
                table: "project_schedules",
                columns: new[] { "project_id", "scheduled_start" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "idx_project_schedules_staff_sort",
                table: "project_schedules",
                columns: new[] { "assigned_staff_id", "scheduled_start" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "idx_project_chats_project_list_sort",
                table: "project_chats",
                columns: new[] { "project_id", "created_at", "chat_id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "idx_chat_messages_chat_list_sort",
                table: "project_chat_messages",
                columns: new[] { "chat_id", "created_at", "message_id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "idx_products_category_list_sort",
                table: "products",
                columns: new[] { "category_id", "created_at", "product_name" },
                descending: new[] { false, true, false });

            migrationBuilder.CreateIndex(
                name: "idx_products_list_sort",
                table: "products",
                columns: new[] { "created_at", "product_name" },
                descending: new[] { true, false });

            migrationBuilder.CreateIndex(
                name: "idx_categories_list_sort",
                table: "categories",
                columns: new[] { "category_name", "category_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_proposals_project_list_sort",
                table: "proposals");

            migrationBuilder.DropIndex(
                name: "idx_proposal_scenes_proposal_list_sort",
                table: "proposal_scenes");

            migrationBuilder.DropIndex(
                name: "idx_proposal_items_proposal_list_sort",
                table: "proposal_items");

            migrationBuilder.DropIndex(
                name: "idx_projects_customer_list_sort",
                table: "projects");

            migrationBuilder.DropIndex(
                name: "idx_projects_designer_list_sort",
                table: "projects");

            migrationBuilder.DropIndex(
                name: "idx_projects_sales_list_sort",
                table: "projects");

            migrationBuilder.DropIndex(
                name: "idx_projects_status_list_sort",
                table: "projects");

            migrationBuilder.DropIndex(
                name: "idx_project_schedules_project_sort",
                table: "project_schedules");

            migrationBuilder.DropIndex(
                name: "idx_project_schedules_staff_sort",
                table: "project_schedules");

            migrationBuilder.DropIndex(
                name: "idx_project_chats_project_list_sort",
                table: "project_chats");

            migrationBuilder.DropIndex(
                name: "idx_chat_messages_chat_list_sort",
                table: "project_chat_messages");

            migrationBuilder.DropIndex(
                name: "idx_products_category_list_sort",
                table: "products");

            migrationBuilder.DropIndex(
                name: "idx_products_list_sort",
                table: "products");

            migrationBuilder.DropIndex(
                name: "idx_categories_list_sort",
                table: "categories");

            migrationBuilder.CreateIndex(
                name: "IX_proposals_project_id",
                table: "proposals",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_proposal_scenes_proposal_id",
                table: "proposal_scenes",
                column: "proposal_id");

            migrationBuilder.CreateIndex(
                name: "IX_proposal_items_proposal_id",
                table: "proposal_items",
                column: "proposal_id");

            migrationBuilder.CreateIndex(
                name: "IX_projects_assigned_designer_id",
                table: "projects",
                column: "assigned_designer_id");

            migrationBuilder.CreateIndex(
                name: "IX_projects_assigned_sales_id",
                table: "projects",
                column: "assigned_sales_id");

            migrationBuilder.CreateIndex(
                name: "IX_projects_customer_id",
                table: "projects",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_schedules_assigned_staff_id",
                table: "project_schedules",
                column: "assigned_staff_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_schedules_project_id",
                table: "project_schedules",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_chats_project_id",
                table: "project_chats",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_chat_messages_chat_id",
                table: "project_chat_messages",
                column: "chat_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_category_id",
                table: "products",
                column: "category_id");
        }
    }
}
