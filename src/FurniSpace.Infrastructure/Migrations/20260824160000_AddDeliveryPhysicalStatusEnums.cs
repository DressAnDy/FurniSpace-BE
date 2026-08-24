using FurniSpace.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurniSpace.Infrastructure.Migrations;

/// <summary>
/// SCRUM-534 (Story 9): order_item_status + PHYSICALLY_DELIVERED
/// SCRUM-535 (Story 10): order_status + AWAITING_CUSTOMER_CONFIRMATION
/// Aligns PostgreSQL enums with support-docs/furnispace_db.dbml target flow.
/// Stories 3, 11, 12, 13, 14, 15, 538 require no schema change.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260824160000_AddDeliveryPhysicalStatusEnums")]
public partial class AddDeliveryPhysicalStatusEnums : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TYPE order_item_status ADD VALUE IF NOT EXISTS 'PHYSICALLY_DELIVERED' AFTER 'PARTIALLY_DELIVERED';
            """);

        migrationBuilder.Sql(
            """
            ALTER TYPE order_status ADD VALUE IF NOT EXISTS 'AWAITING_CUSTOMER_CONFIRMATION' AFTER 'DELIVERING';
            """);

        migrationBuilder.Sql(
            """
            UPDATE order_items oi
            SET status = 'PHYSICALLY_DELIVERED'::order_item_status
            FROM orders o
            WHERE oi.order_id = o.order_id
              AND o.customer_confirmed_delivery_at IS NULL
              AND oi.status IN ('READY'::order_item_status, 'PARTIALLY_DELIVERED'::order_item_status)
              AND oi.delivered_quantity >= oi.quantity;
            """);

        migrationBuilder.Sql(
            """
            UPDATE orders o
            SET status = 'AWAITING_CUSTOMER_CONFIRMATION'::order_status
            WHERE o.status = 'DELIVERING'::order_status
              AND o.customer_confirmed_delivery_at IS NULL
              AND NOT EXISTS (
                  SELECT 1
                  FROM deliveries d
                  WHERE d.order_id = o.order_id
                    AND d.status = 'IN_PROGRESS'::delivery_status)
              AND NOT EXISTS (
                  SELECT 1
                  FROM project_schedules ps
                  WHERE ps.project_id = o.project_id
                    AND ps.schedule_type = 'DELIVERY'::project_schedule_type
                    AND ps.status = 'CONFIRMED'::project_schedule_status)
              AND NOT EXISTS (
                  SELECT 1
                  FROM order_items oi
                  WHERE oi.order_id = o.order_id
                    AND oi.status NOT IN (
                        'UNAVAILABLE'::order_item_status,
                        'CANCELLED'::order_item_status)
                    AND (
                        oi.delivered_quantity < oi.quantity
                        OR oi.status NOT IN (
                            'PHYSICALLY_DELIVERED'::order_item_status,
                            'DELIVERED'::order_item_status)));
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE orders
            SET status = 'DELIVERING'::order_status
            WHERE status = 'AWAITING_CUSTOMER_CONFIRMATION'::order_status;
            """);

        migrationBuilder.Sql(
            """
            UPDATE order_items oi
            SET status = 'READY'::order_item_status
            FROM orders o
            WHERE oi.order_id = o.order_id
              AND o.customer_confirmed_delivery_at IS NULL
              AND oi.status = 'PHYSICALLY_DELIVERED'::order_item_status;
            """);
    }
}
