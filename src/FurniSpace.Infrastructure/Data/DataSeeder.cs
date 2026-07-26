using Microsoft.EntityFrameworkCore;

namespace FurniSpace.Infrastructure.Data;

public static class DataSeeder
{
    internal delegate Task<int> RawSeedExecutor(string sql, CancellationToken cancellationToken);
    internal delegate Task<int> InterpolatedSeedExecutor(FormattableString sql, CancellationToken cancellationToken);

    private const string SeedPasswordHash = "AQAAAAIAAYagAAAAEAECAwQFBgcICQoLDA0ODxDAH0b1OrxnAM4eksEmeLkcwosb1PNke5hkU3/Rat3JOA==";
    private const string InvalidSeedPasswordHash = "AQAAAAMAAYagAAAAEAECAwQFBgcICQoLDA0ODxDAH0b1OrxnAM4eksEmeLkcwosb1PNke5hkU3/Rat3JOA==";

    public static Task SeedAsync(AppDbContext dbContext, CancellationToken cancellationToken = default)
    {
        return SeedAsync(
            (sql, token) => dbContext.Database.ExecuteSqlRawAsync(sql, token),
            (sql, token) => dbContext.Database.ExecuteSqlInterpolatedAsync(sql, token),
            cancellationToken);
    }

    internal static async Task SeedAsync(
        RawSeedExecutor executeRawAsync,
        InterpolatedSeedExecutor executeInterpolatedAsync,
        CancellationToken cancellationToken = default)
    {
        await SeedRolesAsync(executeRawAsync, cancellationToken);
        await SeedAccountsAsync(executeInterpolatedAsync, cancellationToken);
        await SeedBusinessTypesAsync(executeRawAsync, cancellationToken);
        await SeedCategoriesAsync(executeRawAsync, cancellationToken);
        await SeedProductsAsync(executeRawAsync, cancellationToken);
        await SeedProductVersionsAsync(executeRawAsync, cancellationToken);
        await SeedProjectsAsync(executeRawAsync, cancellationToken);
        await SeedProjectAreasAsync(executeRawAsync, cancellationToken);
        await SeedProjectSchedulesAsync(executeRawAsync, cancellationToken);
        await SeedFilesAsync(executeRawAsync, cancellationToken);
        await SeedFileLinksAsync(executeRawAsync, cancellationToken);
        await SeedProjectChatsAsync(executeRawAsync, cancellationToken);
        await SeedProjectChatMessagesAsync(executeRawAsync, cancellationToken);
        await SeedProposalsAsync(executeRawAsync, cancellationToken);
        await SeedProposalScenesAsync(executeRawAsync, cancellationToken);
        await SeedProposalItemsAsync(executeRawAsync, cancellationToken);
        await SeedProposalSceneVariantsAsync(executeRawAsync, cancellationToken);
        await SeedCustomizationRequestsAsync(executeRawAsync, cancellationToken);
        await SeedQuotationsAsync(executeRawAsync, cancellationToken);
        await SeedQuotationItemsAsync(executeRawAsync, cancellationToken);
        await SeedOrdersAsync(executeRawAsync, cancellationToken);
        await SeedOrderItemsAsync(executeRawAsync, cancellationToken);
        await SeedProductionRequestsAsync(executeRawAsync, cancellationToken);
        await SeedProductionItemsAsync(executeRawAsync, cancellationToken);
        await SeedNotificationsAsync(executeRawAsync, cancellationToken);
        await SeedProjectReviewsAsync(executeRawAsync, cancellationToken);
    }

    private static Task<int> SeedRolesAsync(RawSeedExecutor executeRawAsync, CancellationToken cancellationToken)
    {
        return executeRawAsync(
            """
            INSERT INTO roles (role_id, role_name, description, created_at, updated_at)
            VALUES
                ('11111111-1111-1111-1111-111111111111', 'ADMIN', 'System administrator', now(), now()),
                ('22222222-2222-2222-2222-222222222222', 'SALES', 'Sales consultant', now(), now()),
                ('33333333-3333-3333-3333-333333333333', 'DESIGNER', 'Interior designer', now(), now()),
                ('44444444-4444-4444-4444-444444444444', 'CUSTOMER', 'Customer account', now(), now()),
                ('55555555-5555-5555-5555-555555555555', 'PRODUCTION', 'Production staff', now(), now())
            ON CONFLICT (role_name) DO NOTHING;
            """,
            cancellationToken);
    }

    private static Task<int> SeedAccountsAsync(InterpolatedSeedExecutor executeInterpolatedAsync, CancellationToken cancellationToken)
    {
        return executeInterpolatedAsync(
            $"""
            INSERT INTO accounts (account_id, role_id, email, password_hash, full_name, phone, status, created_at, updated_at)
            VALUES
                ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', '11111111-1111-1111-1111-111111111111', 'admin@furnispace.local', {SeedPasswordHash}, 'FurniSpace Admin', '0900000001', 'ACTIVE'::account_status, now(), now()),
                ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', '22222222-2222-2222-2222-222222222222', 'sales@furnispace.local', {SeedPasswordHash}, 'Sales Consultant', '0900000002', 'ACTIVE'::account_status, now(), now()),
                ('cccccccc-cccc-cccc-cccc-cccccccccccc', '33333333-3333-3333-3333-333333333333', 'designer@furnispace.local', {SeedPasswordHash}, 'Design Specialist', '0900000003', 'ACTIVE'::account_status, now(), now()),
                ('dddddddd-dddd-dddd-dddd-dddddddddddd', '44444444-4444-4444-4444-444444444444', 'customer@furnispace.local', {SeedPasswordHash}, 'Demo Customer', '0900000004', 'ACTIVE'::account_status, now(), now()),
                ('eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee', '55555555-5555-5555-5555-555555555555', 'production@furnispace.local', {SeedPasswordHash}, 'Production Staff', '0900000005', 'ACTIVE'::account_status, now(), now())
            ON CONFLICT (email) DO UPDATE
            SET password_hash = EXCLUDED.password_hash,
                updated_at = now()
            WHERE accounts.password_hash IN ('seed-password-hash', {InvalidSeedPasswordHash});
            """,
            cancellationToken);
    }

    private static Task<int> SeedBusinessTypesAsync(RawSeedExecutor executeRawAsync, CancellationToken cancellationToken)
    {
        return executeRawAsync(
            """
            INSERT INTO business_types (id, code, name, description, status, created_at, updated_at)
            VALUES
                (1, 'CAFE', 'Quan ca phe', 'Khong gian kinh doanh do uong va phuc vu khach.', true, now(), now()),
                (2, 'RESTAURANT', 'Nha hang', 'Khong gian phuc vu an uong tai cho.', true, now(), now()),
                (3, 'SPA', 'Spa', 'Khong gian cham soc suc khoe va thu gian.', true, now(), now()),
                (4, 'BEAUTY_STORE', 'Cua hang lam dep', 'Cua hang ban san pham cham soc sac dep.', true, now(), now()),
                (5, 'FASHION_STORE', 'Cua hang thoi trang', 'Khong gian trung bay va ban san pham thoi trang.', true, now(), now()),
                (6, 'SHOWROOM', 'Showroom', 'Khong gian trung bay san pham va tu van khach hang.', true, now(), now()),
                (7, 'CONVENIENCE_STORE', 'Cua hang tien loi', 'Cua hang ban le hang hoa thiet yeu va tieu dung nhanh.', true, now(), now()),
                (8, 'RETAIL_KIOSK', 'Kiosk ban le', 'Diem ban le nho gon trong trung tam thuong mai hoac khu cong cong.', true, now(), now()),
                (9, 'OTHER', 'Khac', 'Loai hinh kinh doanh khac.', true, now(), now())
            ON CONFLICT (code) DO NOTHING;

            SELECT setval(
                pg_get_serial_sequence('business_types', 'id'),
                GREATEST(
                    COALESCE((SELECT MAX(id) FROM business_types), 1),
                    9
                ),
                true
            );
            """,
            cancellationToken);
    }

    private static Task<int> SeedCategoriesAsync(RawSeedExecutor executeRawAsync, CancellationToken cancellationToken)
    {
        return executeRawAsync(
            """
            INSERT INTO categories (category_id, category_name, description, status, created_at, updated_at)
            VALUES
                ('10000000-0000-0000-0000-000000000001', 'Living Room', 'Sofas, shelves, and media furniture', 'ACTIVE'::product_status, now(), now()),
                ('10000000-0000-0000-0000-000000000002', 'Bedroom', 'Beds, wardrobes, and nightstands', 'ACTIVE'::product_status, now(), now()),
                ('10000000-0000-0000-0000-000000000003', 'Kitchen', 'Kitchen cabinets and storage', 'ACTIVE'::product_status, now(), now()),
                ('10000000-0000-0000-0000-000000000004', 'Office', 'Desks, cabinets, and work furniture', 'ACTIVE'::product_status, now(), now())
            ON CONFLICT (category_id) DO NOTHING;
            """,
            cancellationToken);
    }

    private static Task<int> SeedProductsAsync(RawSeedExecutor executeRawAsync, CancellationToken cancellationToken)
    {
        return executeRawAsync(
            """
            INSERT INTO products (product_id, category_id, business_type_ids, product_code, product_name, description, status, created_at, updated_at)
            VALUES
                ('20000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001', ARRAY[1, 2, 6]::integer[], 'SOFA-LUX-001', 'Luxe Modular Sofa', 'Custom modular sofa for living spaces', 'ACTIVE'::product_status, now(), now()),
                ('20000000-0000-0000-0000-000000000002', '10000000-0000-0000-0000-000000000002', ARRAY[5, 6, 9]::integer[], 'WARD-STD-001', 'Sliding Door Wardrobe', 'Built-in wardrobe with sliding doors', 'ACTIVE'::product_status, now(), now()),
                ('20000000-0000-0000-0000-000000000003', '10000000-0000-0000-0000-000000000003', ARRAY[1, 2, 7]::integer[], 'KITCH-CAB-001', 'Modern Kitchen Cabinet', 'Upper and lower kitchen cabinet set', 'ACTIVE'::product_status, now(), now()),
                ('20000000-0000-0000-0000-000000000004', '10000000-0000-0000-0000-000000000004', ARRAY[3, 4, 5, 6]::integer[], 'DESK-OAK-001', 'Oak Work Desk', 'Minimal office desk with drawer module', 'ACTIVE'::product_status, now(), now())
            ON CONFLICT (product_code) DO UPDATE
            SET business_type_ids = COALESCE(products.business_type_ids, EXCLUDED.business_type_ids),
                updated_at = CASE
                    WHEN products.business_type_ids IS NULL THEN now()
                    ELSE products.updated_at
                END;
            """,
            cancellationToken);
    }

    private static Task<int> SeedProductVersionsAsync(RawSeedExecutor executeRawAsync, CancellationToken cancellationToken)
    {
        return executeRawAsync(
            """
            INSERT INTO product_versions (
                product_version_id, product_id, version_code, version_name, version_type,
                material, color, width, height, depth, estimated_price,
                is_default, is_public, is_project_specific, status, created_at, updated_at
            )
            VALUES
                ('30000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000001', 'SOFA-LUX-001-A', 'Luxe Modular Sofa - Linen', 'STANDARD'::product_version_type, 'Solid wood frame, linen upholstery', 'Warm Gray', 280.00, 82.00, 95.00, 18500000.00, true, true, false, 'ACTIVE'::product_status, now(), now()),
                ('30000000-0000-0000-0000-000000000002', '20000000-0000-0000-0000-000000000002', 'WARD-STD-001-A', 'Sliding Door Wardrobe - White Oak', 'STANDARD'::product_version_type, 'MDF, laminate', 'White Oak', 240.00, 260.00, 60.00, 22500000.00, true, true, false, 'ACTIVE'::product_status, now(), now()),
                ('30000000-0000-0000-0000-000000000003', '20000000-0000-0000-0000-000000000003', 'KITCH-CAB-001-A', 'Modern Kitchen Cabinet - Gloss White', 'STANDARD'::product_version_type, 'Plywood, acrylic surface', 'Gloss White', 360.00, 220.00, 65.00, 42000000.00, true, true, false, 'ACTIVE'::product_status, now(), now()),
                ('30000000-0000-0000-0000-000000000004', '20000000-0000-0000-0000-000000000004', 'DESK-OAK-001-A', 'Oak Work Desk - Natural', 'STANDARD'::product_version_type, 'Oak veneer, powder-coated steel', 'Natural Oak', 160.00, 75.00, 70.00, 8900000.00, true, true, false, 'ACTIVE'::product_status, now(), now())
            ON CONFLICT (version_code) DO NOTHING;
            """,
            cancellationToken);
    }

    private static Task<int> SeedProjectsAsync(RawSeedExecutor executeRawAsync, CancellationToken cancellationToken)
    {
        return executeRawAsync(
            """
            INSERT INTO projects (
                project_id, customer_id, assigned_sales_id, assigned_designer_id, project_code,
                project_name, business_type, project_address, business_purpose, furniture_requirement,
                description, total_area_sqm, number_of_floors, budget_min, budget_max,
                target_completion_date, status, submitted_at, sales_assigned_at, designer_assigned_at,
                created_at, updated_at
            )
            VALUES
                ('40000000-0000-0000-0000-000000000001', 'dddddddd-dddd-dddd-dddd-dddddddddddd', null, null, 'PRJ-SEED-0001', 'Seed Cafe Request', 'Cafe', 'District 1, Ho Chi Minh City', 'Open a warm minimalist cafe.', 'Cashier counter, tables, chairs, display shelf, warm lighting.', 'Submitted request waiting for review.', 80.00, 1, 150000000.00, 250000000.00, DATE '2026-09-15', 'SUBMITTED'::project_status, now() - INTERVAL '10 days', null, null, now() - INTERVAL '10 days', now() - INTERVAL '10 days'),
                ('40000000-0000-0000-0000-000000000002', 'dddddddd-dddd-dddd-dddd-dddddddddddd', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', null, 'PRJ-SEED-0002', 'Seed Fashion Store Consultation', 'Fashion Retail', 'District 3, Ho Chi Minh City', 'Renovate a boutique fashion store.', 'Display racks, checkout counter, changing rooms, accent lighting.', 'Accepted by sales and in consultation.', 120.00, 2, 220000000.00, 380000000.00, DATE '2026-10-01', 'IN_CONSULTATION'::project_status, now() - INTERVAL '9 days', now() - INTERVAL '8 days', null, now() - INTERVAL '9 days', now() - INTERVAL '8 days'),
                ('40000000-0000-0000-0000-000000000003', 'dddddddd-dddd-dddd-dddd-dddddddddddd', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 'cccccccc-cccc-cccc-cccc-cccccccccccc', 'PRJ-SEED-0003', 'Seed Office Proposal Consulting', 'Office', 'Thu Duc City, Ho Chi Minh City', 'Set up a compact office workspace.', 'Work desks, storage cabinets, meeting table, acoustic partitions.', 'Multiple proposals available for customer review.', 180.00, 1, 300000000.00, 520000000.00, DATE '2026-11-05', 'PROPOSAL_CONSULTING'::project_status, now() - INTERVAL '8 days', now() - INTERVAL '7 days', now() - INTERVAL '6 days', now() - INTERVAL '8 days', now() - INTERVAL '2 days'),
                ('40000000-0000-0000-0000-000000000004', 'dddddddd-dddd-dddd-dddd-dddddddddddd', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 'cccccccc-cccc-cccc-cccc-cccccccccccc', 'PRJ-SEED-0004', 'Seed Premium Cafe Selected Proposal', 'Cafe', 'District 7, Ho Chi Minh City', 'Launch a premium cafe flagship.', 'Premium counter, booth seating, decorative lighting, wall shelving.', 'Customer selected a proposal and quotation is being prepared.', 150.00, 1, 350000000.00, 700000000.00, DATE '2026-11-20', 'PROPOSAL_SELECTED'::project_status, now() - INTERVAL '12 days', now() - INTERVAL '11 days', now() - INTERVAL '9 days', now() - INTERVAL '12 days', now() - INTERVAL '1 day'),
                ('40000000-0000-0000-0000-000000000005', 'dddddddd-dddd-dddd-dddd-dddddddddddd', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 'cccccccc-cccc-cccc-cccc-cccccccccccc', 'PRJ-SEED-0005', 'Seed Restaurant Order Confirmed', 'Restaurant', 'Binh Thanh District, Ho Chi Minh City', 'Prepare a compact dining restaurant.', 'Dining tables, service counter, divider shelves, kitchen pass lighting.', 'Quotation accepted and deposit is pending.', 210.00, 2, 450000000.00, 850000000.00, DATE '2026-12-05', 'ORDER_CONFIRMED'::project_status, now() - INTERVAL '20 days', now() - INTERVAL '19 days', now() - INTERVAL '15 days', now() - INTERVAL '20 days', now()),
                ('40000000-0000-0000-0000-000000000006', 'dddddddd-dddd-dddd-dddd-dddddddddddd', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 'cccccccc-cccc-cccc-cccc-cccccccccccc', 'PRJ-SEED-0006', 'Seed Showroom In Production', 'Showroom', 'Tan Binh District, Ho Chi Minh City', 'Create a modern product showroom.', 'Display islands, consultation tables, wall displays, reception desk.', 'Deposit paid and production has started.', 260.00, 1, 650000000.00, 1200000000.00, DATE '2026-12-18', 'IN_PRODUCTION'::project_status, now() - INTERVAL '30 days', now() - INTERVAL '29 days', now() - INTERVAL '24 days', now() - INTERVAL '30 days', now()),
                ('40000000-0000-0000-0000-000000000007', 'dddddddd-dddd-dddd-dddd-dddddddddddd', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 'cccccccc-cccc-cccc-cccc-cccccccccccc', 'PRJ-SEED-0007', 'Seed Completed Office Delivery', 'Office', 'District 5, Ho Chi Minh City', 'Complete office furniture installation.', 'Delivered desks, cabinets, meeting table and reception furniture.', 'Completed project for review and payment testing.', 95.00, 1, 180000000.00, 320000000.00, DATE '2026-08-30', 'COMPLETED'::project_status, now() - INTERVAL '45 days', now() - INTERVAL '44 days', now() - INTERVAL '40 days', now() - INTERVAL '45 days', now())
            ON CONFLICT (project_code) DO NOTHING;
            """,
            cancellationToken);
    }

    private static Task<int> SeedProjectAreasAsync(RawSeedExecutor executeRawAsync, CancellationToken cancellationToken)
    {
        return executeRawAsync(
            """
            INSERT INTO project_areas (
                project_area_id, project_id, area_name, area_type, floor_number, description,
                area_sqm, width, length, height, current_condition, requirement_note,
                status, created_by, created_at, updated_at
            )
            VALUES
                ('41000000-0000-0000-0000-000000000001', '40000000-0000-0000-0000-000000000003', 'Open Office Area', 'ZONE'::project_area_type, 1, 'Main working zone.', 120.00, 12.00, 10.00, 3.20, 'Empty shell office.', 'Need workstations and storage.', 'VERIFIED'::project_area_status, 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', now() - INTERVAL '7 days', now() - INTERVAL '6 days'),
                ('41000000-0000-0000-0000-000000000002', '40000000-0000-0000-0000-000000000004', 'Cafe Service Area', 'ZONE'::project_area_type, 1, 'Counter and service line.', 65.00, 10.00, 6.50, 3.40, 'Renovated floor with MEP ready.', 'Premium counter and display lighting.', 'MEASURED'::project_area_status, 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', now() - INTERVAL '10 days', now() - INTERVAL '8 days'),
                ('41000000-0000-0000-0000-000000000003', '40000000-0000-0000-0000-000000000006', 'Showroom Main Hall', 'ROOM'::project_area_type, 1, 'Main display hall.', 180.00, 18.00, 10.00, 4.00, 'Concrete floor, painted walls.', 'Display islands and reception desk.', 'VERIFIED'::project_area_status, 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', now() - INTERVAL '25 days', now() - INTERVAL '24 days')
            ON CONFLICT (project_area_id) DO NOTHING;
            """,
            cancellationToken);
    }

    private static Task<int> SeedProjectSchedulesAsync(RawSeedExecutor executeRawAsync, CancellationToken cancellationToken)
    {
        return executeRawAsync(
            """
            INSERT INTO project_schedules (
                schedule_id, project_id, project_area_id, schedule_type, title, description,
                created_by, assigned_staff_id, scheduled_start, scheduled_end, location,
                status, customer_note, internal_note, created_at, updated_at
            )
            VALUES
                ('42000000-0000-0000-0000-000000000001', '40000000-0000-0000-0000-000000000003', '41000000-0000-0000-0000-000000000001', 'MEASUREMENT'::project_schedule_type, 'Office measurement visit', 'Measure the open office area.', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 'cccccccc-cccc-cccc-cccc-cccccccccccc', now() + INTERVAL '2 days', now() + INTERVAL '2 days 2 hours', 'Thu Duc City office', 'CONFIRMED'::project_schedule_status, 'Security requires visitor list.', 'Bring laser measurement kit.', now() - INTERVAL '5 days', now() - INTERVAL '4 days'),
                ('42000000-0000-0000-0000-000000000002', '40000000-0000-0000-0000-000000000004', '41000000-0000-0000-0000-000000000002', 'DESIGN_REVIEW'::project_schedule_type, 'Premium cafe design review', 'Review selected cafe layout.', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 'cccccccc-cccc-cccc-cccc-cccccccccccc', now() + INTERVAL '4 days', now() + INTERVAL '4 days 2 hours', 'District 7 cafe site', 'PENDING_CONFIRMATION'::project_schedule_status, 'Prefer afternoon slot.', 'Prepare material samples.', now() - INTERVAL '4 days', now() - INTERVAL '4 days'),
                ('42000000-0000-0000-0000-000000000003', '40000000-0000-0000-0000-000000000006', '41000000-0000-0000-0000-000000000003', 'HANDOVER'::project_schedule_type, 'Showroom production checkpoint', 'Check installed furniture progress.', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee', now() + INTERVAL '7 days', now() + INTERVAL '7 days 3 hours', 'Tan Binh showroom', 'CONFIRMED'::project_schedule_status, null, 'Coordinate with production team.', now() - INTERVAL '3 days', now() - INTERVAL '3 days')
            ON CONFLICT (schedule_id) DO NOTHING;
            """,
            cancellationToken);
    }

    private static Task<int> SeedFilesAsync(RawSeedExecutor executeRawAsync, CancellationToken cancellationToken)
    {
        return executeRawAsync(
            """
            INSERT INTO files (
                file_id, uploaded_by, original_file_name, stored_file_name, file_url,
                storage_path, mime_type, file_extension, file_size_bytes, checksum,
                status, uploaded_at
            )
            VALUES
                ('91000000-0000-0000-0000-000000000001', 'cccccccc-cccc-cccc-cccc-cccccccccccc', 'office-measurement.pdf', 'seed-office-measurement.pdf', '/seed/files/office-measurement.pdf', 'seed/measurements/office-measurement.pdf', 'application/pdf', '.pdf', 120000, 'seed-checksum-001', 'ACTIVE'::file_status, now() - INTERVAL '6 days'),
                ('91000000-0000-0000-0000-000000000002', 'cccccccc-cccc-cccc-cccc-cccccccccccc', 'premium-cafe-preview.png', 'seed-premium-cafe-preview.png', '/seed/files/premium-cafe-preview.png', 'seed/proposals/premium-cafe-preview.png', 'image/png', '.png', 240000, 'seed-checksum-002', 'ACTIVE'::file_status, now() - INTERVAL '5 days'),
                ('91000000-0000-0000-0000-000000000003', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 'showroom-quotation.pdf', 'seed-showroom-quotation.pdf', '/seed/files/showroom-quotation.pdf', 'seed/quotations/showroom-quotation.pdf', 'application/pdf', '.pdf', 180000, 'seed-checksum-003', 'ACTIVE'::file_status, now() - INTERVAL '4 days')
            ON CONFLICT (storage_path) DO NOTHING;
            """,
            cancellationToken);
    }

    private static Task<int> SeedFileLinksAsync(RawSeedExecutor executeRawAsync, CancellationToken cancellationToken)
    {
        return executeRawAsync(
            """
            INSERT INTO file_links (
                file_link_id, file_id, reference_type, reference_id, file_type,
                visibility, is_primary, display_order, description, created_by, created_at
            )
            VALUES
                ('92000000-0000-0000-0000-000000000001', '91000000-0000-0000-0000-000000000001', 'PROJECT', '40000000-0000-0000-0000-000000000003', 'MEASUREMENT_REPORT'::file_type, 'STAFF_ONLY'::file_visibility, true, 1, 'Measurement file for seed office project.', 'cccccccc-cccc-cccc-cccc-cccccccccccc', now() - INTERVAL '6 days'),
                ('92000000-0000-0000-0000-000000000002', '91000000-0000-0000-0000-000000000002', 'PROPOSAL', '50000000-0000-0000-0000-000000000004', 'PROPOSAL_PREVIEW'::file_type, 'CUSTOMER_VISIBLE'::file_visibility, true, 1, 'Preview image for premium cafe proposal.', 'cccccccc-cccc-cccc-cccc-cccccccccccc', now() - INTERVAL '5 days'),
                ('92000000-0000-0000-0000-000000000003', '91000000-0000-0000-0000-000000000003', 'QUOTATION', '60000000-0000-0000-0000-000000000005', 'QUOTATION_FILE'::file_type, 'CUSTOMER_VISIBLE'::file_visibility, true, 1, 'Quotation attachment for showroom project.', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', now() - INTERVAL '4 days')
            ON CONFLICT (file_id, reference_type, reference_id, file_type) DO NOTHING;
            """,
            cancellationToken);
    }

    private static Task<int> SeedProjectChatsAsync(RawSeedExecutor executeRawAsync, CancellationToken cancellationToken)
    {
        return executeRawAsync(
            """
            INSERT INTO project_chats (chat_id, project_id, chat_type, staff_id, title, status, created_at, closed_at)
            VALUES
                ('43000000-0000-0000-0000-000000000001', '40000000-0000-0000-0000-000000000003', 'SALES'::project_chat_type, 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 'Sales Consultation', 'OPEN'::project_chat_status, now() - INTERVAL '7 days', null),
                ('43000000-0000-0000-0000-000000000002', '40000000-0000-0000-0000-000000000003', 'DESIGNER'::project_chat_type, 'cccccccc-cccc-cccc-cccc-cccccccccccc', 'Design Discussion', 'OPEN'::project_chat_status, now() - INTERVAL '6 days', null),
                ('43000000-0000-0000-0000-000000000003', '40000000-0000-0000-0000-000000000006', 'PRODUCTION'::project_chat_type, 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee', 'Production Coordination', 'OPEN'::project_chat_status, now() - INTERVAL '3 days', null)
            ON CONFLICT (chat_id) DO NOTHING;
            """,
            cancellationToken);
    }

    private static Task<int> SeedProjectChatMessagesAsync(RawSeedExecutor executeRawAsync, CancellationToken cancellationToken)
    {
        return executeRawAsync(
            """
            INSERT INTO project_chat_messages (
                message_id, chat_id, sender_id, message_type, content, attachment_file_id,
                created_at, edited_at, deleted_at, read_at
            )
            VALUES
                ('43100000-0000-0000-0000-000000000001', '43000000-0000-0000-0000-000000000001', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 'TEXT'::project_chat_message_type, 'We have prepared three office layout options for your review.', null, now() - INTERVAL '6 days', null, null, now() - INTERVAL '6 days'),
                ('43100000-0000-0000-0000-000000000002', '43000000-0000-0000-0000-000000000002', 'cccccccc-cccc-cccc-cccc-cccccccccccc', 'FILE'::project_chat_message_type, 'Attached the measurement report for reference.', '91000000-0000-0000-0000-000000000001', now() - INTERVAL '5 days', null, null, null),
                ('43100000-0000-0000-0000-000000000003', '43000000-0000-0000-0000-000000000003', 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee', 'TEXT'::project_chat_message_type, 'Production material check is in progress.', null, now() - INTERVAL '2 days', null, null, null)
            ON CONFLICT (message_id) DO NOTHING;
            """,
            cancellationToken);
    }

    private static Task<int> SeedProposalsAsync(RawSeedExecutor executeRawAsync, CancellationToken cancellationToken)
    {
        return executeRawAsync(
            """
            INSERT INTO proposals (
                proposal_id, project_id, parent_proposal_id, proposal_name, description,
                design_concept, version_no, estimated_price, status, created_by,
                published_at, selected_at, rejected_at, created_at, updated_at
            )
            VALUES
                ('50000000-0000-0000-0000-000000000001', '40000000-0000-0000-0000-000000000003', null, 'Basic Office Layout', 'Lean office setup with core workstations.', 'Functional compact office design.', 1, 310000000.00, 'DRAFT'::proposal_status, 'cccccccc-cccc-cccc-cccc-cccccccccccc', null, null, null, now() - INTERVAL '6 days', now() - INTERVAL '6 days'),
                ('50000000-0000-0000-0000-000000000002', '40000000-0000-0000-0000-000000000003', null, 'Standard Office Layout', 'Balanced design with meeting and storage areas.', 'Warm productive workspace.', 2, 420000000.00, 'PUBLISHED'::proposal_status, 'cccccccc-cccc-cccc-cccc-cccccccccccc', now() - INTERVAL '4 days', null, null, now() - INTERVAL '5 days', now() - INTERVAL '4 days'),
                ('50000000-0000-0000-0000-000000000003', '40000000-0000-0000-0000-000000000003', null, 'Premium Office Layout', 'Premium version with reception and acoustic partitions.', 'Modern executive workspace.', 3, 510000000.00, 'PUBLISHED'::proposal_status, 'cccccccc-cccc-cccc-cccc-cccccccccccc', now() - INTERVAL '3 days', null, null, now() - INTERVAL '4 days', now() - INTERVAL '3 days'),
                ('50000000-0000-0000-0000-000000000004', '40000000-0000-0000-0000-000000000004', null, 'Premium Cafe Layout', 'Selected premium cafe layout.', 'Warm minimal flagship cafe.', 1, 620000000.00, 'SELECTED'::proposal_status, 'cccccccc-cccc-cccc-cccc-cccccccccccc', now() - INTERVAL '6 days', now() - INTERVAL '2 days', null, now() - INTERVAL '7 days', now() - INTERVAL '2 days'),
                ('50000000-0000-0000-0000-000000000005', '40000000-0000-0000-0000-000000000005', null, 'Restaurant Fit-out Proposal', 'Accepted restaurant furniture proposal.', 'Efficient restaurant service flow.', 1, 760000000.00, 'SELECTED'::proposal_status, 'cccccccc-cccc-cccc-cccc-cccccccccccc', now() - INTERVAL '12 days', now() - INTERVAL '8 days', null, now() - INTERVAL '13 days', now() - INTERVAL '8 days'),
                ('50000000-0000-0000-0000-000000000006', '40000000-0000-0000-0000-000000000006', null, 'Showroom Production Proposal', 'Selected showroom production proposal.', 'Flexible display zones and premium reception.', 1, 1080000000.00, 'SELECTED'::proposal_status, 'cccccccc-cccc-cccc-cccc-cccccccccccc', now() - INTERVAL '20 days', now() - INTERVAL '18 days', null, now() - INTERVAL '21 days', now() - INTERVAL '18 days'),
                ('50000000-0000-0000-0000-000000000007', '40000000-0000-0000-0000-000000000007', null, 'Completed Office Proposal', 'Delivered office furniture proposal.', 'Clean practical office setup.', 1, 290000000.00, 'SELECTED'::proposal_status, 'cccccccc-cccc-cccc-cccc-cccccccccccc', now() - INTERVAL '35 days', now() - INTERVAL '30 days', null, now() - INTERVAL '36 days', now() - INTERVAL '30 days')
            ON CONFLICT (proposal_id) DO NOTHING;
            """,
            cancellationToken);
    }

    private static Task<int> SeedProposalScenesAsync(RawSeedExecutor executeRawAsync, CancellationToken cancellationToken)
    {
        return executeRawAsync(
            """
            INSERT INTO proposal_scenes (
                scene_id, proposal_id, project_area_id, scene_name, scene_type, mongo_scene_id,
                preview_file_id, version_no, is_active, created_by, created_at, updated_at
            )
            VALUES
                ('51000000-0000-0000-0000-000000000001', '50000000-0000-0000-0000-000000000002', '41000000-0000-0000-0000-000000000001', 'Standard Office 3D Scene', 'THREE_D'::proposal_scene_type, 'mongo-seed-office-standard', null, 1, true, 'cccccccc-cccc-cccc-cccc-cccccccccccc', now() - INTERVAL '4 days', now() - INTERVAL '4 days'),
                ('51000000-0000-0000-0000-000000000002', '50000000-0000-0000-0000-000000000004', '41000000-0000-0000-0000-000000000002', 'Premium Cafe 3D Scene', 'THREE_D'::proposal_scene_type, 'mongo-seed-cafe-premium', '91000000-0000-0000-0000-000000000002', 1, true, 'cccccccc-cccc-cccc-cccc-cccccccccccc', now() - INTERVAL '5 days', now() - INTERVAL '3 days'),
                ('51000000-0000-0000-0000-000000000003', '50000000-0000-0000-0000-000000000006', '41000000-0000-0000-0000-000000000003', 'Showroom Main Hall Scene', 'THREE_D'::proposal_scene_type, 'mongo-seed-showroom-main', null, 1, true, 'cccccccc-cccc-cccc-cccc-cccccccccccc', now() - INTERVAL '19 days', now() - INTERVAL '18 days')
            ON CONFLICT (scene_id) DO NOTHING;
            """,
            cancellationToken);
    }

    private static Task<int> SeedProposalItemsAsync(RawSeedExecutor executeRawAsync, CancellationToken cancellationToken)
    {
        return executeRawAsync(
            """
            INSERT INTO proposal_items (
                proposal_item_id, proposal_id, scene_id, scene_object_id, project_area_id,
                product_version_id, approved_product_version_id, item_name, item_type,
                quantity, width, height, depth, material, color, is_customized,
                unit_price_snapshot, total_price_snapshot, note, created_at, updated_at
            )
            VALUES
                ('52000000-0000-0000-0000-000000000001', '50000000-0000-0000-0000-000000000002', '51000000-0000-0000-0000-000000000001', 'seed-office-desk-001', '41000000-0000-0000-0000-000000000001', '30000000-0000-0000-0000-000000000004', null, 'Oak Work Desk Package', 'PRODUCT', 12, 160.00, 75.00, 70.00, 'Oak veneer, powder-coated steel', 'Natural Oak', false, 8900000.00, 106800000.00, 'Twelve desk package for open office.', now() - INTERVAL '4 days', now() - INTERVAL '4 days'),
                ('52000000-0000-0000-0000-000000000002', '50000000-0000-0000-0000-000000000004', '51000000-0000-0000-0000-000000000002', 'seed-cafe-counter-001', '41000000-0000-0000-0000-000000000002', '30000000-0000-0000-0000-000000000003', null, 'Premium Service Counter', 'PRODUCT', 1, 360.00, 220.00, 65.00, 'Plywood, acrylic surface', 'Gloss White', true, 42000000.00, 52000000.00, 'Customized counter finish for premium cafe.', now() - INTERVAL '5 days', now() - INTERVAL '2 days'),
                ('52000000-0000-0000-0000-000000000003', '50000000-0000-0000-0000-000000000006', '51000000-0000-0000-0000-000000000003', 'seed-showroom-sofa-001', '41000000-0000-0000-0000-000000000003', '30000000-0000-0000-0000-000000000001', null, 'Showroom Modular Sofa', 'PRODUCT', 2, 280.00, 82.00, 95.00, 'Solid wood frame, linen upholstery', 'Warm Gray', false, 18500000.00, 37000000.00, 'Reception sofa set.', now() - INTERVAL '18 days', now() - INTERVAL '18 days'),
                ('52000000-0000-0000-0000-000000000004', '50000000-0000-0000-0000-000000000005', null, null, null, '30000000-0000-0000-0000-000000000002', null, 'Restaurant Storage Wardrobe', 'PRODUCT', 3, 240.00, 260.00, 60.00, 'MDF, laminate', 'White Oak', false, 22500000.00, 67500000.00, 'Back-office storage for restaurant.', now() - INTERVAL '12 days', now() - INTERVAL '8 days'),
                ('52000000-0000-0000-0000-000000000005', '50000000-0000-0000-0000-000000000007', null, null, null, '30000000-0000-0000-0000-000000000004', null, 'Delivered Office Desk Set', 'PRODUCT', 8, 160.00, 75.00, 70.00, 'Oak veneer, powder-coated steel', 'Natural Oak', false, 8900000.00, 71200000.00, 'Delivered desk set.', now() - INTERVAL '30 days', now() - INTERVAL '25 days')
            ON CONFLICT (proposal_item_id) DO NOTHING;
            """,
            cancellationToken);
    }

    private static Task<int> SeedProposalSceneVariantsAsync(RawSeedExecutor executeRawAsync, CancellationToken cancellationToken)
    {
        return executeRawAsync(
            """
            INSERT INTO proposal_scene_variants (
                variant_id, proposal_id, scene_id, created_by, variant_type, status,
                mongo_variant_scene_id, note, submitted_at, reviewed_by, reviewed_at,
                review_note, applied_at, applied_by, created_at, updated_at
            )
            VALUES
                ('53000000-0000-0000-0000-000000000001', '50000000-0000-0000-0000-000000000002', '51000000-0000-0000-0000-000000000001', 'dddddddd-dddd-dddd-dddd-dddddddddddd', 'CUSTOMER_SUGGESTION'::proposal_scene_variant_type, 'SUBMITTED'::proposal_scene_variant_status, 'mongo-seed-office-variant-001', 'Move meeting table closer to window.', now() - INTERVAL '3 days', null, null, null, null, null, now() - INTERVAL '3 days', now() - INTERVAL '3 days'),
                ('53000000-0000-0000-0000-000000000002', '50000000-0000-0000-0000-000000000004', '51000000-0000-0000-0000-000000000002', 'dddddddd-dddd-dddd-dddd-dddddddddddd', 'CUSTOMER_SUGGESTION'::proposal_scene_variant_type, 'ACCEPTED'::proposal_scene_variant_status, 'mongo-seed-cafe-variant-001', 'Use darker walnut tone for counter.', now() - INTERVAL '4 days', 'cccccccc-cccc-cccc-cccc-cccccccccccc', now() - INTERVAL '3 days', 'Accepted material tone change.', now() - INTERVAL '3 days', 'cccccccc-cccc-cccc-cccc-cccccccccccc', now() - INTERVAL '4 days', now() - INTERVAL '3 days'),
                ('53000000-0000-0000-0000-000000000003', '50000000-0000-0000-0000-000000000006', '51000000-0000-0000-0000-000000000003', 'cccccccc-cccc-cccc-cccc-cccccccccccc', 'DESIGNER_REVISION'::proposal_scene_variant_type, 'APPLIED'::proposal_scene_variant_status, 'mongo-seed-showroom-variant-001', 'Applied wider walking path near display island.', now() - INTERVAL '17 days', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', now() - INTERVAL '16 days', 'Looks good for production.', now() - INTERVAL '16 days', 'cccccccc-cccc-cccc-cccc-cccccccccccc', now() - INTERVAL '17 days', now() - INTERVAL '16 days')
            ON CONFLICT (variant_id) DO NOTHING;
            """,
            cancellationToken);
    }

    private static Task<int> SeedCustomizationRequestsAsync(RawSeedExecutor executeRawAsync, CancellationToken cancellationToken)
    {
        return executeRawAsync(
            """
            INSERT INTO customization_requests (
                customization_request_id, project_id, proposal_id, proposal_item_id, requested_by_customer_id,
                request_title, request_description, requested_width, requested_height, requested_depth,
                requested_material, requested_color, requested_change_note, designer_id, designer_spec_note,
                production_review_by, feasibility_note, estimated_production_days, estimated_additional_cost,
                additional_cost_reason, material_available, production_risk_note,
                approved_product_version_id, status, customer_accepted_at, customer_rejected_at, created_at, updated_at
            )
            VALUES
                ('54000000-0000-0000-0000-000000000001', '40000000-0000-0000-0000-000000000003', '50000000-0000-0000-0000-000000000002', '52000000-0000-0000-0000-000000000001', 'dddddddd-dddd-dddd-dddd-dddddddddddd', 'Desk cable grommet update', 'Add cable grommets to every work desk.', null, null, null, null, null, 'Need cleaner cable routing.', 'cccccccc-cccc-cccc-cccc-cccccccccccc', 'Add two grommets per desk.', null, null, null, null, null, null, null, null, 'DESIGN_REVIEWING'::customization_status, null, null, now() - INTERVAL '3 days', now() - INTERVAL '2 days'),
                ('54000000-0000-0000-0000-000000000002', '40000000-0000-0000-0000-000000000004', '50000000-0000-0000-0000-000000000004', '52000000-0000-0000-0000-000000000002', 'dddddddd-dddd-dddd-dddd-dddddddddddd', 'Dark walnut counter finish', 'Change service counter color to dark walnut.', 380.00, 220.00, 70.00, 'Plywood with walnut veneer', 'Dark Walnut', 'Match premium cafe brand tone.', 'cccccccc-cccc-cccc-cccc-cccccccccccc', 'Designer approved revised finish.', 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee', 'Feasible with available veneer.', 7, 10000000.00, 'Additional veneer and finishing labor.', true, 'Low risk.', null, 'WAITING_FOR_CUSTOMER_FINAL_APPROVAL'::customization_status, null, null, now() - INTERVAL '4 days', now() - INTERVAL '1 day'),
                ('54000000-0000-0000-0000-000000000003', '40000000-0000-0000-0000-000000000006', '50000000-0000-0000-0000-000000000006', '52000000-0000-0000-0000-000000000003', 'dddddddd-dddd-dddd-dddd-dddddddddddd', 'Premium sofa fabric upgrade', 'Upgrade showroom sofa fabric to stain-resistant linen.', null, null, null, 'Stain-resistant linen', 'Warm Gray', 'Need durable fabric for heavy showroom use.', 'cccccccc-cccc-cccc-cccc-cccccccccccc', 'Approved for production review.', 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee', 'Feasible but needs supplier confirmation.', 10, 6000000.00, 'Premium fabric surcharge.', true, 'Supplier lead time may vary.', null, 'ACCEPTED'::customization_status, now() - INTERVAL '10 days', null, now() - INTERVAL '15 days', now() - INTERVAL '10 days')
            ON CONFLICT (customization_request_id) DO NOTHING;
            """,
            cancellationToken);
    }

    private static Task<int> SeedQuotationsAsync(RawSeedExecutor executeRawAsync, CancellationToken cancellationToken)
    {
        return executeRawAsync(
            """
            INSERT INTO quotations (
                quotation_id, project_id, proposal_id, quotation_code, version_no,
                subtotal_amount, discount_amount, tax_amount, total_amount, status,
                valid_until, customer_note, sales_note, revision_reason, reject_reason,
                created_by, sent_at, accepted_at, rejected_at, created_at, updated_at
            )
            VALUES
                ('60000000-0000-0000-0000-000000000001', '40000000-0000-0000-0000-000000000004', '50000000-0000-0000-0000-000000000004', 'QTN-SEED-0001', 1, 620000000.00, 0.00, 0.00, 620000000.00, 'DRAFT'::quotation_status, DATE '2026-12-31', null, 'Draft quotation for selected cafe proposal.', null, null, 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', null, null, null, now() - INTERVAL '2 days', now() - INTERVAL '2 days'),
                ('60000000-0000-0000-0000-000000000002', '40000000-0000-0000-0000-000000000003', '50000000-0000-0000-0000-000000000002', 'QTN-SEED-0002', 1, 420000000.00, 5000000.00, 0.00, 415000000.00, 'SENT'::quotation_status, DATE '2026-12-20', 'Please review the office quotation.', 'Sent to customer for office project.', null, null, 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', now() - INTERVAL '1 day', null, null, now() - INTERVAL '2 days', now() - INTERVAL '1 day'),
                ('60000000-0000-0000-0000-000000000003', '40000000-0000-0000-0000-000000000005', '50000000-0000-0000-0000-000000000005', 'QTN-SEED-0003', 1, 760000000.00, 0.00, 0.00, 760000000.00, 'ACCEPTED'::quotation_status, DATE '2026-12-15', null, 'Accepted restaurant quotation.', null, null, 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', now() - INTERVAL '8 days', now() - INTERVAL '7 days', null, now() - INTERVAL '9 days', now() - INTERVAL '7 days'),
                ('60000000-0000-0000-0000-000000000004', '40000000-0000-0000-0000-000000000006', '50000000-0000-0000-0000-000000000006', 'QTN-SEED-0004', 1, 1080000000.00, 30000000.00, 0.00, 1050000000.00, 'ACCEPTED'::quotation_status, DATE '2026-12-18', null, 'Accepted showroom quotation.', null, null, 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', now() - INTERVAL '18 days', now() - INTERVAL '17 days', null, now() - INTERVAL '19 days', now() - INTERVAL '17 days'),
                ('60000000-0000-0000-0000-000000000005', '40000000-0000-0000-0000-000000000007', '50000000-0000-0000-0000-000000000007', 'QTN-SEED-0005', 1, 290000000.00, 0.00, 0.00, 290000000.00, 'ACCEPTED'::quotation_status, DATE '2026-10-30', null, 'Accepted completed office quotation.', null, null, 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', now() - INTERVAL '28 days', now() - INTERVAL '27 days', null, now() - INTERVAL '29 days', now() - INTERVAL '27 days')
            ON CONFLICT (quotation_code) DO NOTHING;
            """,
            cancellationToken);
    }

    private static Task<int> SeedQuotationItemsAsync(RawSeedExecutor executeRawAsync, CancellationToken cancellationToken)
    {
        return executeRawAsync(
            """
            INSERT INTO quotation_items (
                quotation_item_id, quotation_id, item_type, proposal_item_id, product_version_id,
                product_name_snapshot, product_version_name_snapshot, product_version_code_snapshot,
                item_name, description, quantity, unit_price, customization_additional_cost,
                discount_amount, subtotal_amount, is_customized, customization_note, note
            )
            VALUES
                ('61000000-0000-0000-0000-000000000001', '60000000-0000-0000-0000-000000000001', 'PRODUCT_ITEM'::quotation_item_type, '52000000-0000-0000-0000-000000000002', '30000000-0000-0000-0000-000000000003', 'Modern Kitchen Cabinet', 'Modern Kitchen Cabinet - Gloss White', 'KITCH-CAB-001-A', 'Premium Service Counter', 'Cafe counter product item.', 1, 42000000.00, 10000000.00, 0.00, 52000000.00, true, 'Dark walnut customization included.', null),
                ('61000000-0000-0000-0000-000000000002', '60000000-0000-0000-0000-000000000001', 'MANUAL_ITEM'::quotation_item_type, null, null, null, null, null, 'Decorative lighting package', 'Manual lighting and installation charge.', 1, 88000000.00, 0.00, 0.00, 88000000.00, false, null, 'Manual item for cafe ambience.'),
                ('61000000-0000-0000-0000-000000000003', '60000000-0000-0000-0000-000000000002', 'PRODUCT_ITEM'::quotation_item_type, '52000000-0000-0000-0000-000000000001', '30000000-0000-0000-0000-000000000004', 'Oak Work Desk', 'Oak Work Desk - Natural', 'DESK-OAK-001-A', 'Oak Work Desk Package', 'Office desks quotation item.', 12, 8900000.00, 0.00, 5000000.00, 101800000.00, false, null, null),
                ('61000000-0000-0000-0000-000000000004', '60000000-0000-0000-0000-000000000003', 'PRODUCT_ITEM'::quotation_item_type, '52000000-0000-0000-0000-000000000004', '30000000-0000-0000-0000-000000000002', 'Sliding Door Wardrobe', 'Sliding Door Wardrobe - White Oak', 'WARD-STD-001-A', 'Restaurant Storage Wardrobe', 'Storage furniture for restaurant.', 3, 22500000.00, 0.00, 0.00, 67500000.00, false, null, null),
                ('61000000-0000-0000-0000-000000000005', '60000000-0000-0000-0000-000000000003', 'MANUAL_ITEM'::quotation_item_type, null, null, null, null, null, 'Restaurant installation labor', 'Installation and site finishing.', 1, 692500000.00, 0.00, 0.00, 692500000.00, false, null, 'Manual labor and finishing package.'),
                ('61000000-0000-0000-0000-000000000006', '60000000-0000-0000-0000-000000000004', 'PRODUCT_ITEM'::quotation_item_type, '52000000-0000-0000-0000-000000000003', '30000000-0000-0000-0000-000000000001', 'Luxe Modular Sofa', 'Luxe Modular Sofa - Linen', 'SOFA-LUX-001-A', 'Showroom Modular Sofa', 'Showroom reception sofa set.', 2, 18500000.00, 6000000.00, 0.00, 43000000.00, true, 'Premium fabric upgrade.', null),
                ('61000000-0000-0000-0000-000000000007', '60000000-0000-0000-0000-000000000004', 'MANUAL_ITEM'::quotation_item_type, null, null, null, null, null, 'Showroom display island package', 'Custom display islands and installation.', 1, 1007000000.00, 0.00, 30000000.00, 977000000.00, false, null, null),
                ('61000000-0000-0000-0000-000000000008', '60000000-0000-0000-0000-000000000005', 'PRODUCT_ITEM'::quotation_item_type, '52000000-0000-0000-0000-000000000005', '30000000-0000-0000-0000-000000000004', 'Oak Work Desk', 'Oak Work Desk - Natural', 'DESK-OAK-001-A', 'Delivered Office Desk Set', 'Completed office desk package.', 8, 8900000.00, 0.00, 0.00, 71200000.00, false, null, null)
            ON CONFLICT (quotation_item_id) DO NOTHING;
            """,
            cancellationToken);
    }

    private static Task<int> SeedOrdersAsync(RawSeedExecutor executeRawAsync, CancellationToken cancellationToken)
    {
        return executeRawAsync(
            """
            INSERT INTO orders (
                order_id, project_id, proposal_id, quotation_id, order_code, customer_id, sales_id,
                original_total_amount, item_adjustment_amount, additional_discount_amount, final_total_amount,
                deposit_amount, paid_amount, remaining_amount, status, delivery_address, receiver_name,
                receiver_phone, delivery_note, customer_delivery_note, confirmed_by, confirmed_at,
                created_at, updated_at
            )
            VALUES
                ('70000000-0000-0000-0000-000000000001', '40000000-0000-0000-0000-000000000005', '50000000-0000-0000-0000-000000000005', '60000000-0000-0000-0000-000000000003', 'ORD-SEED-0001', 'dddddddd-dddd-dddd-dddd-dddddddddddd', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 760000000.00, 0.00, 0.00, 760000000.00, 228000000.00, 0.00, 760000000.00, 'DEPOSIT_PENDING'::order_status, 'Binh Thanh District, Ho Chi Minh City', 'Nguyen Cafe Owner', '0911000001', 'Deliver after site floor completion.', null, 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', now() - INTERVAL '7 days', now() - INTERVAL '7 days', now() - INTERVAL '7 days'),
                ('70000000-0000-0000-0000-000000000002', '40000000-0000-0000-0000-000000000006', '50000000-0000-0000-0000-000000000006', '60000000-0000-0000-0000-000000000004', 'ORD-SEED-0002', 'dddddddd-dddd-dddd-dddd-dddddddddddd', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 1050000000.00, 0.00, 0.00, 1050000000.00, 315000000.00, 315000000.00, 735000000.00, 'DEPOSIT_PAID'::order_status, 'Tan Binh District, Ho Chi Minh City', 'Tran Fashion Owner', '0911000002', 'Coordinate delivery by display zones.', null, 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', now() - INTERVAL '17 days', now() - INTERVAL '17 days', now() - INTERVAL '1 day'),
                ('70000000-0000-0000-0000-000000000003', '40000000-0000-0000-0000-000000000007', '50000000-0000-0000-0000-000000000007', '60000000-0000-0000-0000-000000000005', 'ORD-SEED-0003', 'dddddddd-dddd-dddd-dddd-dddddddddddd', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 290000000.00, 0.00, 0.00, 290000000.00, 87000000.00, 290000000.00, 0.00, 'IN_PRODUCTION'::order_status, 'District 5, Ho Chi Minh City', 'Le Office Manager', '0911000003', 'Completed seed order kept for history.', 'All delivered items confirmed.', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', now() - INTERVAL '27 days', now() - INTERVAL '27 days', now() - INTERVAL '1 day')
            ON CONFLICT (order_code) DO NOTHING;
            """,
            cancellationToken);
    }

    private static Task<int> SeedOrderItemsAsync(RawSeedExecutor executeRawAsync, CancellationToken cancellationToken)
    {
        return executeRawAsync(
            """
            INSERT INTO order_items (
                order_item_id, order_id, quotation_item_id, product_version_id, product_name_snapshot,
                product_version_name_snapshot, product_version_code_snapshot, quantity, delivered_quantity,
                status, unit_price, customization_fee, discount_amount, subtotal_amount, adjustment_amount,
                unavailable_reason, production_note, delivery_note, last_delivered_at, last_delivered_by,
                customer_confirmed_at
            )
            VALUES
                ('71000000-0000-0000-0000-000000000001', '70000000-0000-0000-0000-000000000001', '61000000-0000-0000-0000-000000000004', '30000000-0000-0000-0000-000000000002', 'Sliding Door Wardrobe', 'Sliding Door Wardrobe - White Oak', 'WARD-STD-001-A', 3, 0, 'PENDING'::order_item_status, 22500000.00, 0.00, 0.00, 67500000.00, 0.00, null, 'Pending production start.', null, null, null, null),
                ('71000000-0000-0000-0000-000000000002', '70000000-0000-0000-0000-000000000002', '61000000-0000-0000-0000-000000000006', '30000000-0000-0000-0000-000000000001', 'Luxe Modular Sofa', 'Luxe Modular Sofa - Linen', 'SOFA-LUX-001-A', 2, 0, 'IN_PRODUCTION'::order_item_status, 18500000.00, 6000000.00, 0.00, 43000000.00, 0.00, null, 'Premium fabric in cutting stage.', null, null, null, null),
                ('71000000-0000-0000-0000-000000000003', '70000000-0000-0000-0000-000000000003', '61000000-0000-0000-0000-000000000008', '30000000-0000-0000-0000-000000000004', 'Oak Work Desk', 'Oak Work Desk - Natural', 'DESK-OAK-001-A', 8, 8, 'DELIVERED'::order_item_status, 8900000.00, 0.00, 0.00, 71200000.00, 0.00, null, 'Completed.', 'Delivered and installed.', now() - INTERVAL '2 days', 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee', now() - INTERVAL '1 day')
            ON CONFLICT (order_item_id) DO NOTHING;
            """,
            cancellationToken);
    }

    private static Task<int> SeedProductionRequestsAsync(RawSeedExecutor executeRawAsync, CancellationToken cancellationToken)
    {
        return executeRawAsync(
            """
            INSERT INTO production_requests (
                production_request_id, production_code, project_id, order_id, assigned_to, status,
                priority, estimated_start_date, estimated_completion_date, actual_start_date,
                actual_completion_date, note, created_at, updated_at
            )
            VALUES
                ('82000000-0000-0000-0000-000000000001', 'PROD-SEED-0001', '40000000-0000-0000-0000-000000000005', '70000000-0000-0000-0000-000000000001', 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee', 'PENDING_REVIEW'::production_request_status, 'NORMAL', DATE '2026-08-01', DATE '2026-08-25', null, null, 'Pending deposit confirmation.', now() - INTERVAL '6 days', now() - INTERVAL '6 days'),
                ('82000000-0000-0000-0000-000000000002', 'PROD-SEED-0002', '40000000-0000-0000-0000-000000000006', '70000000-0000-0000-0000-000000000002', 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee', 'IN_PRODUCTION'::production_request_status, 'HIGH', DATE '2026-07-20', DATE '2026-08-20', DATE '2026-07-21', null, 'Showroom display production started.', now() - INTERVAL '14 days', now() - INTERVAL '1 day'),
                ('82000000-0000-0000-0000-000000000003', 'PROD-SEED-0003', '40000000-0000-0000-0000-000000000007', '70000000-0000-0000-0000-000000000003', 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee', 'COMPLETED'::production_request_status, 'NORMAL', DATE '2026-06-10', DATE '2026-06-25', DATE '2026-06-11', DATE '2026-06-24', 'Completed office order production.', now() - INTERVAL '25 days', now() - INTERVAL '2 days')
            ON CONFLICT (production_code) DO NOTHING;
            """,
            cancellationToken);
    }

    private static Task<int> SeedProductionItemsAsync(RawSeedExecutor executeRawAsync, CancellationToken cancellationToken)
    {
        return executeRawAsync(
            """
            INSERT INTO production_items (
                production_item_id, production_request_id, order_item_id, product_version_id,
                product_name_snapshot, product_version_name_snapshot, quantity, status,
                material_note, production_note, estimated_completion_date, completed_at
            )
            VALUES
                ('83000000-0000-0000-0000-000000000001', '82000000-0000-0000-0000-000000000001', '71000000-0000-0000-0000-000000000001', '30000000-0000-0000-0000-000000000002', 'Sliding Door Wardrobe', 'Sliding Door Wardrobe - White Oak', 3, 'PENDING'::production_item_status, 'Confirm laminate batch.', 'Waiting for production approval.', DATE '2026-08-25', null),
                ('83000000-0000-0000-0000-000000000002', '82000000-0000-0000-0000-000000000002', '71000000-0000-0000-0000-000000000002', '30000000-0000-0000-0000-000000000001', 'Luxe Modular Sofa', 'Luxe Modular Sofa - Linen', 2, 'IN_PRODUCTION'::production_item_status, 'Use stain-resistant linen.', 'Frame assembly completed.', DATE '2026-08-20', null),
                ('83000000-0000-0000-0000-000000000003', '82000000-0000-0000-0000-000000000003', '71000000-0000-0000-0000-000000000003', '30000000-0000-0000-0000-000000000004', 'Oak Work Desk', 'Oak Work Desk - Natural', 8, 'COMPLETED'::production_item_status, 'Natural oak finish.', 'Delivered to site.', DATE '2026-06-25', now() - INTERVAL '3 days')
            ON CONFLICT (production_item_id) DO NOTHING;
            """,
            cancellationToken);
    }

    private static Task<int> SeedNotificationsAsync(RawSeedExecutor executeRawAsync, CancellationToken cancellationToken)
    {
        return executeRawAsync(
            """
            INSERT INTO notifications (
                notification_id, receiver_id, project_id, title, message, notification_type,
                reference_type, reference_id, is_read, read_at, deleted_at, created_at
            )
            VALUES
                ('90000000-0000-0000-0000-000000000001', 'dddddddd-dddd-dddd-dddd-dddddddddddd', '40000000-0000-0000-0000-000000000003', 'Proposal published', 'A new proposal has been published for your office project.', 'ProposalPublished', 'PROPOSAL', '50000000-0000-0000-0000-000000000002', false, null, null, now() - INTERVAL '4 days'),
                ('90000000-0000-0000-0000-000000000002', 'cccccccc-cccc-cccc-cccc-cccccccccccc', '40000000-0000-0000-0000-000000000004', 'Designer assigned', 'You have been assigned as Designer for the premium cafe project.', 'ProjectDesignerAssigned', 'PROJECT', '40000000-0000-0000-0000-000000000004', true, now() - INTERVAL '8 days', null, now() - INTERVAL '9 days'),
                ('90000000-0000-0000-0000-000000000003', 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee', '40000000-0000-0000-0000-000000000004', 'Customization review required', 'A customization request needs production review.', 'CustomizationProductionReviewRequested', 'CUSTOMIZATION_REQUEST', '54000000-0000-0000-0000-000000000002', false, null, null, now() - INTERVAL '2 days')
            ON CONFLICT (notification_id) DO NOTHING;
            """,
            cancellationToken);
    }

    private static Task<int> SeedProjectReviewsAsync(RawSeedExecutor executeRawAsync, CancellationToken cancellationToken)
    {
        return executeRawAsync(
            """
            INSERT INTO project_reviews (
                review_id, project_id, order_id, customer_id, rating, design_quality_rating,
                service_quality_rating, delivery_rating, comment, created_at, updated_at
            )
            VALUES
                ('94000000-0000-0000-0000-000000000001', '40000000-0000-0000-0000-000000000005', '70000000-0000-0000-0000-000000000001', 'dddddddd-dddd-dddd-dddd-dddddddddddd', 4, 4, 5, 4, 'Seed restaurant workflow review placeholder.', now() - INTERVAL '1 day', now() - INTERVAL '1 day'),
                ('94000000-0000-0000-0000-000000000002', '40000000-0000-0000-0000-000000000006', '70000000-0000-0000-0000-000000000002', 'dddddddd-dddd-dddd-dddd-dddddddddddd', 5, 5, 4, 5, 'Showroom production is progressing well.', now() - INTERVAL '1 day', now() - INTERVAL '1 day'),
                ('94000000-0000-0000-0000-000000000003', '40000000-0000-0000-0000-000000000007', '70000000-0000-0000-0000-000000000003', 'dddddddd-dddd-dddd-dddd-dddddddddddd', 5, 5, 5, 5, 'Completed office installation met expectations.', now() - INTERVAL '1 day', now() - INTERVAL '1 day')
            ON CONFLICT (project_id) DO NOTHING;
            """,
            cancellationToken);
    }
}
