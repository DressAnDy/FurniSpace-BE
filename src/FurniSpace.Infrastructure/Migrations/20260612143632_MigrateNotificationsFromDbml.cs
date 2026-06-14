using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurniSpace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MigrateNotificationsFromDbml : Migration
    {
        private static readonly string[] ReceiverCreatedIndexColumns = ["receiver_id", "created_at"];
        private static readonly string[] ReceiverReadIndexColumns = ["receiver_id", "is_read"];
        private static readonly string[] ReferenceIndexColumns = ["reference_type", "reference_id"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_notifications_receiver_id",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "status",
                table: "notifications");

            migrationBuilder.RenameIndex(
                name: "IX_notifications_project_id",
                table: "notifications",
                newName: "idx_notifications_project_id");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "notifications",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "notifications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_read",
                table: "notifications",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "reference_id",
                table: "notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reference_type",
                table: "notifications",
                type: "varchar(50)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_notifications_receiver_created",
                table: "notifications",
                columns: ReceiverCreatedIndexColumns);

            migrationBuilder.CreateIndex(
                name: "idx_notifications_receiver_read",
                table: "notifications",
                columns: ReceiverReadIndexColumns);

            migrationBuilder.CreateIndex(
                name: "idx_notifications_reference",
                table: "notifications",
                columns: ReferenceIndexColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_notifications_receiver_created",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "idx_notifications_receiver_read",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "idx_notifications_reference",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "is_read",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "reference_id",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "reference_type",
                table: "notifications");

            migrationBuilder.RenameIndex(
                name: "idx_notifications_project_id",
                table: "notifications",
                newName: "IX_notifications_project_id");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "notifications",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<int>(
                name: "status",
                table: "notifications",
                type: "notification_status",
                nullable: true,
                defaultValueSql: "'UNREAD'::notification_status");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_receiver_id",
                table: "notifications",
                column: "receiver_id");
        }
    }
}
