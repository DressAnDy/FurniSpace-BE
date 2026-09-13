using FurniSpace.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurniSpace.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260702102000_AddAccountListFilterSortIndex")]
    public partial class AddAccountListFilterSortIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "idx_accounts_active_list_sort",
                table: "accounts",
                columns: new[] { "created_at", "email" },
                descending: new[] { true, false },
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "idx_accounts_active_status_list_sort",
                table: "accounts",
                columns: new[] { "status", "created_at", "email" },
                descending: new[] { false, true, false },
                filter: "deleted_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_accounts_active_status_list_sort",
                table: "accounts");

            migrationBuilder.DropIndex(
                name: "idx_accounts_active_list_sort",
                table: "accounts");
        }
    }
}
