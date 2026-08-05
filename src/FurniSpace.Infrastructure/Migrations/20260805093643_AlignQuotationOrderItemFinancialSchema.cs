using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurniSpace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AlignQuotationOrderItemFinancialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE quotations RENAME COLUMN discount_amount TO total_discount_amount;
                ALTER TABLE quotation_items RENAME COLUMN customization_additional_cost TO customization_unit_additional_cost;
                ALTER TABLE order_items RENAME COLUMN customization_fee TO customization_unit_additional_cost;

                ALTER TABLE quotations
                    ADD COLUMN currency varchar(10) NOT NULL DEFAULT 'VND',
                    ADD COLUMN taxable_amount numeric(14,2) NOT NULL DEFAULT 0;

                ALTER TABLE quotation_items
                    ADD COLUMN gross_amount numeric(14,2) NOT NULL DEFAULT 0,
                    ADD COLUMN taxable_amount numeric(14,2) NOT NULL DEFAULT 0,
                    ADD COLUMN tax_rate numeric(7,4) NOT NULL DEFAULT 0,
                    ADD COLUMN tax_amount numeric(14,2) NOT NULL DEFAULT 0,
                    ADD COLUMN total_amount numeric(14,2) NOT NULL DEFAULT 0;

                ALTER TABLE order_items
                    ADD COLUMN gross_amount numeric(14,2) NOT NULL DEFAULT 0,
                    ADD COLUMN taxable_amount numeric(14,2) NOT NULL DEFAULT 0,
                    ADD COLUMN tax_rate numeric(7,4) NOT NULL DEFAULT 0,
                    ADD COLUMN tax_amount numeric(14,2) NOT NULL DEFAULT 0,
                    ADD COLUMN total_amount numeric(14,2) NOT NULL DEFAULT 0;

                UPDATE quotations
                SET subtotal_amount = COALESCE(subtotal_amount, 0),
                    total_discount_amount = COALESCE(total_discount_amount, 0),
                    tax_amount = COALESCE(tax_amount, 0),
                    total_amount = COALESCE(total_amount, 0),
                    taxable_amount = GREATEST(COALESCE(subtotal_amount, 0) - COALESCE(total_discount_amount, 0), 0),
                    currency = COALESCE(NULLIF(currency, ''), 'VND');

                UPDATE quotation_items
                SET quantity = COALESCE(quantity, 1),
                    unit_price = COALESCE(unit_price, 0),
                    customization_unit_additional_cost = COALESCE(customization_unit_additional_cost, 0),
                    discount_amount = COALESCE(discount_amount, 0),
                    subtotal_amount = COALESCE(subtotal_amount, 0);

                UPDATE quotation_items
                SET gross_amount = GREATEST(COALESCE(subtotal_amount, 0) + COALESCE(discount_amount, 0), 0),
                    taxable_amount = GREATEST(COALESCE(subtotal_amount, 0), 0),
                    tax_rate = 0,
                    tax_amount = 0,
                    total_amount = GREATEST(COALESCE(subtotal_amount, 0), 0);

                INSERT INTO quotation_items (
                    quotation_item_id,
                    quotation_id,
                    item_type,
                    item_name,
                    description,
                    quantity,
                    unit_price,
                    customization_unit_additional_cost,
                    gross_amount,
                    discount_amount,
                    taxable_amount,
                    tax_rate,
                    tax_amount,
                    total_amount,
                    subtotal_amount,
                    is_customized,
                    customization_note
                )
                SELECT
                    gen_random_uuid(),
                    quotation_id,
                    'MANUAL_ITEM'::quotation_item_type,
                    'Legacy Tax',
                    'Legacy quotation-level tax carried forward as a manual item.',
                    1,
                    tax_amount,
                    0,
                    tax_amount,
                    0,
                    tax_amount,
                    0,
                    0,
                    tax_amount,
                    tax_amount,
                    false,
                    'Created by AlignQuotationOrderItemFinancialSchema migration.'
                FROM quotations
                WHERE tax_amount > 0;

                UPDATE quotations AS quotation
                SET subtotal_amount = totals.gross_amount,
                    taxable_amount = GREATEST(totals.gross_amount - quotation.total_discount_amount, 0),
                    tax_amount = 0,
                    total_amount = GREATEST(totals.gross_amount - quotation.total_discount_amount, 0)
                FROM (
                    SELECT quotation_id, COALESCE(SUM(gross_amount), 0) AS gross_amount
                    FROM quotation_items
                    GROUP BY quotation_id
                ) AS totals
                WHERE totals.quotation_id = quotation.quotation_id;

                UPDATE order_items
                SET quantity = COALESCE(quantity, 1),
                    unit_price = COALESCE(unit_price, 0),
                    customization_unit_additional_cost = COALESCE(customization_unit_additional_cost, 0),
                    discount_amount = COALESCE(discount_amount, 0),
                    subtotal_amount = COALESCE(subtotal_amount, 0);

                UPDATE order_items
                SET gross_amount = GREATEST(COALESCE(subtotal_amount, 0) + COALESCE(discount_amount, 0), 0),
                    taxable_amount = GREATEST(COALESCE(subtotal_amount, 0), 0),
                    tax_rate = 0,
                    tax_amount = 0,
                    total_amount = GREATEST(COALESCE(subtotal_amount, 0), 0);

                ALTER TABLE quotations
                    ALTER COLUMN subtotal_amount TYPE numeric(14,2),
                    ALTER COLUMN subtotal_amount SET DEFAULT 0,
                    ALTER COLUMN subtotal_amount SET NOT NULL,
                    ALTER COLUMN total_discount_amount TYPE numeric(14,2),
                    ALTER COLUMN total_discount_amount SET DEFAULT 0,
                    ALTER COLUMN total_discount_amount SET NOT NULL,
                    ALTER COLUMN tax_amount TYPE numeric(14,2),
                    ALTER COLUMN tax_amount SET DEFAULT 0,
                    ALTER COLUMN tax_amount SET NOT NULL,
                    ALTER COLUMN total_amount TYPE numeric(14,2),
                    ALTER COLUMN total_amount SET DEFAULT 0,
                    ALTER COLUMN total_amount SET NOT NULL;

                ALTER TABLE quotation_items
                    ALTER COLUMN quantity SET DEFAULT 1,
                    ALTER COLUMN quantity SET NOT NULL,
                    ALTER COLUMN unit_price TYPE numeric(14,2),
                    ALTER COLUMN unit_price SET DEFAULT 0,
                    ALTER COLUMN unit_price SET NOT NULL,
                    ALTER COLUMN customization_unit_additional_cost TYPE numeric(14,2),
                    ALTER COLUMN customization_unit_additional_cost SET DEFAULT 0,
                    ALTER COLUMN customization_unit_additional_cost SET NOT NULL,
                    ALTER COLUMN discount_amount TYPE numeric(14,2),
                    ALTER COLUMN discount_amount SET DEFAULT 0,
                    ALTER COLUMN discount_amount SET NOT NULL,
                    ALTER COLUMN subtotal_amount TYPE numeric(14,2),
                    ALTER COLUMN subtotal_amount SET DEFAULT 0,
                    ALTER COLUMN subtotal_amount SET NOT NULL;

                ALTER TABLE order_items
                    ALTER COLUMN quantity SET DEFAULT 1,
                    ALTER COLUMN quantity SET NOT NULL,
                    ALTER COLUMN unit_price TYPE numeric(14,2),
                    ALTER COLUMN unit_price SET DEFAULT 0,
                    ALTER COLUMN unit_price SET NOT NULL,
                    ALTER COLUMN customization_unit_additional_cost TYPE numeric(14,2),
                    ALTER COLUMN customization_unit_additional_cost SET DEFAULT 0,
                    ALTER COLUMN customization_unit_additional_cost SET NOT NULL,
                    ALTER COLUMN discount_amount TYPE numeric(14,2),
                    ALTER COLUMN discount_amount SET DEFAULT 0,
                    ALTER COLUMN discount_amount SET NOT NULL,
                    ALTER COLUMN subtotal_amount TYPE numeric(14,2),
                    ALTER COLUMN subtotal_amount SET DEFAULT 0,
                    ALTER COLUMN subtotal_amount SET NOT NULL;

                ALTER TABLE quotations
                    ADD CONSTRAINT ck_quotations_financial_amounts_non_negative
                    CHECK (
                        subtotal_amount >= 0
                        AND total_discount_amount >= 0
                        AND taxable_amount >= 0
                        AND tax_amount >= 0
                        AND total_amount >= 0
                    );

                ALTER TABLE quotation_items
                    ADD CONSTRAINT ck_quotation_items_financial_amounts_valid
                    CHECK (
                        quantity > 0
                        AND unit_price >= 0
                        AND customization_unit_additional_cost >= 0
                        AND gross_amount >= 0
                        AND discount_amount >= 0
                        AND discount_amount <= gross_amount
                        AND taxable_amount >= 0
                        AND tax_rate >= 0
                        AND tax_rate <= 100
                        AND tax_amount >= 0
                        AND total_amount >= 0
                    );

                ALTER TABLE order_items
                    ADD CONSTRAINT ck_order_items_financial_amounts_valid
                    CHECK (
                        quantity > 0
                        AND unit_price >= 0
                        AND customization_unit_additional_cost >= 0
                        AND gross_amount >= 0
                        AND discount_amount >= 0
                        AND discount_amount <= gross_amount
                        AND taxable_amount >= 0
                        AND tax_rate >= 0
                        AND tax_rate <= 100
                        AND tax_amount >= 0
                        AND total_amount >= 0
                    );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE order_items DROP CONSTRAINT IF EXISTS ck_order_items_financial_amounts_valid;
                ALTER TABLE quotation_items DROP CONSTRAINT IF EXISTS ck_quotation_items_financial_amounts_valid;
                ALTER TABLE quotations DROP CONSTRAINT IF EXISTS ck_quotations_financial_amounts_non_negative;

                DELETE FROM quotation_items
                WHERE item_type = 'MANUAL_ITEM'::quotation_item_type
                    AND item_name = 'Legacy Tax'
                    AND description = 'Legacy quotation-level tax carried forward as a manual item.'
                    AND customization_note = 'Created by AlignQuotationOrderItemFinancialSchema migration.';

                ALTER TABLE order_items
                    ALTER COLUMN quantity DROP DEFAULT,
                    ALTER COLUMN quantity DROP NOT NULL,
                    ALTER COLUMN unit_price TYPE numeric(12,2),
                    ALTER COLUMN unit_price DROP DEFAULT,
                    ALTER COLUMN unit_price DROP NOT NULL,
                    ALTER COLUMN customization_unit_additional_cost TYPE numeric(12,2),
                    ALTER COLUMN customization_unit_additional_cost DROP DEFAULT,
                    ALTER COLUMN customization_unit_additional_cost DROP NOT NULL,
                    ALTER COLUMN discount_amount TYPE numeric(12,2),
                    ALTER COLUMN discount_amount DROP DEFAULT,
                    ALTER COLUMN discount_amount DROP NOT NULL,
                    ALTER COLUMN subtotal_amount TYPE numeric(12,2),
                    ALTER COLUMN subtotal_amount DROP DEFAULT,
                    ALTER COLUMN subtotal_amount DROP NOT NULL;

                ALTER TABLE quotation_items
                    ALTER COLUMN quantity DROP DEFAULT,
                    ALTER COLUMN quantity DROP NOT NULL,
                    ALTER COLUMN unit_price TYPE numeric(12,2),
                    ALTER COLUMN unit_price DROP DEFAULT,
                    ALTER COLUMN unit_price DROP NOT NULL,
                    ALTER COLUMN customization_unit_additional_cost TYPE numeric(12,2),
                    ALTER COLUMN customization_unit_additional_cost DROP DEFAULT,
                    ALTER COLUMN customization_unit_additional_cost DROP NOT NULL,
                    ALTER COLUMN discount_amount TYPE numeric(12,2),
                    ALTER COLUMN discount_amount DROP DEFAULT,
                    ALTER COLUMN discount_amount DROP NOT NULL,
                    ALTER COLUMN subtotal_amount TYPE numeric(12,2),
                    ALTER COLUMN subtotal_amount DROP DEFAULT,
                    ALTER COLUMN subtotal_amount DROP NOT NULL;

                ALTER TABLE quotations
                    ALTER COLUMN subtotal_amount TYPE numeric(12,2),
                    ALTER COLUMN subtotal_amount DROP DEFAULT,
                    ALTER COLUMN subtotal_amount DROP NOT NULL,
                    ALTER COLUMN total_discount_amount TYPE numeric(12,2),
                    ALTER COLUMN total_discount_amount DROP DEFAULT,
                    ALTER COLUMN total_discount_amount DROP NOT NULL,
                    ALTER COLUMN tax_amount TYPE numeric(12,2),
                    ALTER COLUMN tax_amount DROP DEFAULT,
                    ALTER COLUMN tax_amount DROP NOT NULL,
                    ALTER COLUMN total_amount TYPE numeric(12,2),
                    ALTER COLUMN total_amount DROP DEFAULT,
                    ALTER COLUMN total_amount DROP NOT NULL;

                ALTER TABLE order_items
                    DROP COLUMN total_amount,
                    DROP COLUMN tax_amount,
                    DROP COLUMN tax_rate,
                    DROP COLUMN taxable_amount,
                    DROP COLUMN gross_amount;

                ALTER TABLE quotation_items
                    DROP COLUMN total_amount,
                    DROP COLUMN tax_amount,
                    DROP COLUMN tax_rate,
                    DROP COLUMN taxable_amount,
                    DROP COLUMN gross_amount;

                ALTER TABLE quotations
                    DROP COLUMN taxable_amount,
                    DROP COLUMN currency;

                ALTER TABLE order_items RENAME COLUMN customization_unit_additional_cost TO customization_fee;
                ALTER TABLE quotation_items RENAME COLUMN customization_unit_additional_cost TO customization_additional_cost;
                ALTER TABLE quotations RENAME COLUMN total_discount_amount TO discount_amount;
                """);
        }
    }
}
