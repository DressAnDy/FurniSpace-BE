using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurniSpace.Infrastructure.Migrations;

/// <inheritdoc />
public partial class RefactorHeaderVatProductOnlyModel : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            -- Remove legacy manual tax/fee lines before header VAT migration
            DELETE FROM quotation_items WHERE item_type = 'MANUAL_ITEM'::quotation_item_type;

            -- Merge customization add-on into unit price where present
            UPDATE quotation_items
            SET unit_price = unit_price + COALESCE(customization_unit_additional_cost, 0),
                customization_unit_additional_cost = 0
            WHERE COALESCE(customization_unit_additional_cost, 0) <> 0;

            UPDATE order_items
            SET unit_price = unit_price + COALESCE(customization_unit_additional_cost, 0),
                customization_unit_additional_cost = 0
            WHERE COALESCE(customization_unit_additional_cost, 0) <> 0;

            -- Recalculate quotation item pre-VAT totals
            UPDATE quotation_items
            SET gross_amount = GREATEST(COALESCE(quantity, 1) * COALESCE(unit_price, 0), 0),
                discount_amount = COALESCE(discount_amount, 0);

            UPDATE quotation_items
            SET total_amount = GREATEST(gross_amount - discount_amount, 0);

            -- Add header VAT columns on quotations
            ALTER TABLE quotations
                ADD COLUMN IF NOT EXISTS pre_vat_amount numeric(14,2) NOT NULL DEFAULT 0,
                ADD COLUMN IF NOT EXISTS vat_rate numeric(7,4) NOT NULL DEFAULT 0.0800,
                ADD COLUMN IF NOT EXISTS vat_amount numeric(14,2) NOT NULL DEFAULT 0;

            UPDATE quotations
            SET pre_vat_amount = GREATEST(COALESCE(subtotal_amount, 0) - COALESCE(total_discount_amount, 0), 0),
                vat_rate = 0.0800,
                vat_amount = ROUND(GREATEST(COALESCE(subtotal_amount, 0) - COALESCE(total_discount_amount, 0), 0) * 0.0800, 2),
                total_amount = GREATEST(COALESCE(subtotal_amount, 0) - COALESCE(total_discount_amount, 0), 0)
                    + ROUND(GREATEST(COALESCE(subtotal_amount, 0) - COALESCE(total_discount_amount, 0), 0) * 0.0800, 2);

            ALTER TABLE quotations DROP COLUMN IF EXISTS taxable_amount;

            -- Add header VAT snapshot on orders
            ALTER TABLE orders
                ADD COLUMN IF NOT EXISTS vat_rate numeric(7,4) NOT NULL DEFAULT 0.0800,
                ADD COLUMN IF NOT EXISTS vat_amount numeric(14,2) NOT NULL DEFAULT 0;

            UPDATE orders AS o
            SET vat_rate = COALESCE(q.vat_rate, 0.0800),
                vat_amount = COALESCE(q.vat_amount, 0)
            FROM quotations AS q
            WHERE q.quotation_id = o.quotation_id;

            -- Recalculate order item pre-VAT subtotals
            UPDATE order_items
            SET subtotal_amount = GREATEST(COALESCE(quantity, 1) * COALESCE(unit_price, 0) - COALESCE(discount_amount, 0), 0);

            -- Drop enum-backed defaults before removing columns
            ALTER TABLE quotation_items ALTER COLUMN item_type DROP DEFAULT;
            ALTER TABLE order_items ALTER COLUMN item_type DROP DEFAULT;

            -- Drop obsolete quotation item columns
            ALTER TABLE quotation_items DROP COLUMN IF EXISTS item_type;
            ALTER TABLE quotation_items DROP COLUMN IF EXISTS customization_unit_additional_cost;
            ALTER TABLE quotation_items DROP COLUMN IF EXISTS taxable_amount;
            ALTER TABLE quotation_items DROP COLUMN IF EXISTS tax_rate;
            ALTER TABLE quotation_items DROP COLUMN IF EXISTS tax_amount;
            ALTER TABLE quotation_items DROP COLUMN IF EXISTS subtotal_amount;

            ALTER TABLE quotation_items
                ADD COLUMN IF NOT EXISTS created_at timestamp with time zone,
                ADD COLUMN IF NOT EXISTS updated_at timestamp with time zone;

            UPDATE quotation_items
            SET created_at = COALESCE(created_at, NOW()),
                updated_at = COALESCE(updated_at, NOW());

            -- Drop obsolete order item columns
            ALTER TABLE order_items DROP COLUMN IF EXISTS item_type;
            ALTER TABLE order_items DROP COLUMN IF EXISTS customization_unit_additional_cost;
            ALTER TABLE order_items DROP COLUMN IF EXISTS gross_amount;
            ALTER TABLE order_items DROP COLUMN IF EXISTS taxable_amount;
            ALTER TABLE order_items DROP COLUMN IF EXISTS tax_rate;
            ALTER TABLE order_items DROP COLUMN IF EXISTS tax_amount;
            ALTER TABLE order_items DROP COLUMN IF EXISTS total_amount;

            ALTER TABLE order_items
                ALTER COLUMN subtotal_amount TYPE numeric(14,2),
                ALTER COLUMN subtotal_amount SET DEFAULT 0,
                ALTER COLUMN subtotal_amount SET NOT NULL;

            -- Remove product version default tax
            ALTER TABLE product_versions DROP CONSTRAINT IF EXISTS ck_product_versions_default_tax_rate_range;
            ALTER TABLE product_versions DROP COLUMN IF EXISTS default_tax_rate;

            DROP TYPE IF EXISTS quotation_item_type;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TYPE quotation_item_type AS ENUM ('PRODUCT_ITEM', 'MANUAL_ITEM');

            ALTER TABLE product_versions
                ADD COLUMN IF NOT EXISTS default_tax_rate numeric(7,4);

            ALTER TABLE product_versions
                ADD CONSTRAINT ck_product_versions_default_tax_rate_range
                CHECK (default_tax_rate IS NULL OR (default_tax_rate >= 0 AND default_tax_rate <= 100));

            ALTER TABLE order_items
                ADD COLUMN IF NOT EXISTS item_type quotation_item_type NOT NULL DEFAULT 'PRODUCT_ITEM'::quotation_item_type,
                ADD COLUMN IF NOT EXISTS customization_unit_additional_cost numeric(14,2) NOT NULL DEFAULT 0,
                ADD COLUMN IF NOT EXISTS gross_amount numeric(14,2) NOT NULL DEFAULT 0,
                ADD COLUMN IF NOT EXISTS taxable_amount numeric(14,2) NOT NULL DEFAULT 0,
                ADD COLUMN IF NOT EXISTS tax_rate numeric(7,4) NOT NULL DEFAULT 0,
                ADD COLUMN IF NOT EXISTS tax_amount numeric(14,2) NOT NULL DEFAULT 0,
                ADD COLUMN IF NOT EXISTS total_amount numeric(14,2) NOT NULL DEFAULT 0;

            ALTER TABLE quotation_items
                ADD COLUMN IF NOT EXISTS item_type quotation_item_type NOT NULL DEFAULT 'PRODUCT_ITEM'::quotation_item_type,
                ADD COLUMN IF NOT EXISTS customization_unit_additional_cost numeric(14,2) NOT NULL DEFAULT 0,
                ADD COLUMN IF NOT EXISTS taxable_amount numeric(14,2) NOT NULL DEFAULT 0,
                ADD COLUMN IF NOT EXISTS tax_rate numeric(7,4) NOT NULL DEFAULT 0,
                ADD COLUMN IF NOT EXISTS tax_amount numeric(14,2) NOT NULL DEFAULT 0,
                ADD COLUMN IF NOT EXISTS subtotal_amount numeric(14,2) NOT NULL DEFAULT 0;

            ALTER TABLE quotation_items
                DROP COLUMN IF EXISTS created_at,
                DROP COLUMN IF EXISTS updated_at;

            ALTER TABLE orders
                DROP COLUMN IF EXISTS vat_rate,
                DROP COLUMN IF EXISTS vat_amount;

            ALTER TABLE quotations
                ADD COLUMN IF NOT EXISTS taxable_amount numeric(14,2) NOT NULL DEFAULT 0;

            ALTER TABLE quotations
                DROP COLUMN IF EXISTS pre_vat_amount,
                DROP COLUMN IF EXISTS vat_rate,
                DROP COLUMN IF EXISTS vat_amount;
            """);
    }
}
