using Microsoft.EntityFrameworkCore;

namespace FurniSpace.Infrastructure.Data;

public static class DataSeeder
{
    private const string SeedPasswordHash = "AQAAAAIAAYagAAAAEAECAwQFBgcICQoLDA0ODxDAH0b1OrxnAM4eksEmeLkcwosb1PNke5hkU3/Rat3JOA==";
    private const string InvalidSeedPasswordHash = "AQAAAAMAAYagAAAAEAECAwQFBgcICQoLDA0ODxDAH0b1OrxnAM4eksEmeLkcwosb1PNke5hkU3/Rat3JOA==";

    public static async Task SeedAsync(AppDbContext dbContext, CancellationToken cancellationToken = default)
    {
        await SeedRolesAsync(dbContext, cancellationToken);
        await SeedAccountsAsync(dbContext, cancellationToken);
        await SeedCategoriesAsync(dbContext, cancellationToken);
        await SeedProductsAsync(dbContext, cancellationToken);
        await SeedProductVersionsAsync(dbContext, cancellationToken);
    }

    private static Task<int> SeedRolesAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        return dbContext.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO roles (role_id, role_name, description, created_at, updated_at)
            VALUES
                ('11111111-1111-1111-1111-111111111111', 'ADMIN', 'System administrator', now(), now()),
                ('22222222-2222-2222-2222-222222222222', 'SALES', 'Sales consultant', now(), now()),
                ('33333333-3333-3333-3333-333333333333', 'DESIGNER', 'Interior designer', now(), now()),
                ('44444444-4444-4444-4444-444444444444', 'CUSTOMER', 'Customer account', now(), now()),
                ('55555555-5555-5555-5555-555555555555', 'PRODUCTION', 'Production account', now(), now())
            ON CONFLICT (role_name) DO NOTHING;
            """,
            cancellationToken);
    }

    private static Task<int> SeedAccountsAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        return dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO accounts (account_id, role_id, email, password_hash, full_name, phone, status, created_at, updated_at)
            VALUES
                ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', '11111111-1111-1111-1111-111111111111', 'admin@furnispace.local', {SeedPasswordHash}, 'FurniSpace Admin', '0900000001', 'ACTIVE'::account_status, now(), now()),
                ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', '22222222-2222-2222-2222-222222222222', 'sales@furnispace.local', {SeedPasswordHash}, 'Sales Consultant', '0900000002', 'ACTIVE'::account_status, now(), now()),
                ('cccccccc-cccc-cccc-cccc-cccccccccccc', '33333333-3333-3333-3333-333333333333', 'designer@furnispace.local', {SeedPasswordHash}, 'Design Specialist', '0900000003', 'ACTIVE'::account_status, now(), now()),
                ('dddddddd-dddd-dddd-dddd-dddddddddddd', '44444444-4444-4444-4444-444444444444', 'customer@furnispace.local', {SeedPasswordHash}, 'Demo Customer', '0900000004', 'ACTIVE'::account_status, now(), now()),
                ('eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee', '55555555-5555-5555-5555-555555555555', 'production@furnispace.local', {SeedPasswordHash}, 'Demo Production', '0900000005', 'ACTIVE'::account_status, now(), now())
            ON CONFLICT (email) DO UPDATE
            SET password_hash = EXCLUDED.password_hash,
                updated_at = now()
            WHERE accounts.password_hash IN ('seed-password-hash', {InvalidSeedPasswordHash});
            """,
            cancellationToken);
    }

    private static Task<int> SeedCategoriesAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        return dbContext.Database.ExecuteSqlRawAsync(
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

    private static Task<int> SeedProductsAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        return dbContext.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO products (product_id, category_id, product_code, product_name, description, status, created_at, updated_at)
            VALUES
                ('20000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001', 'SOFA-LUX-001', 'Luxe Modular Sofa', 'Custom modular sofa for living spaces', 'ACTIVE'::product_status, now(), now()),
                ('20000000-0000-0000-0000-000000000002', '10000000-0000-0000-0000-000000000002', 'WARD-STD-001', 'Sliding Door Wardrobe', 'Built-in wardrobe with sliding doors', 'ACTIVE'::product_status, now(), now()),
                ('20000000-0000-0000-0000-000000000003', '10000000-0000-0000-0000-000000000003', 'KITCH-CAB-001', 'Modern Kitchen Cabinet', 'Upper and lower kitchen cabinet set', 'ACTIVE'::product_status, now(), now()),
                ('20000000-0000-0000-0000-000000000004', '10000000-0000-0000-0000-000000000004', 'DESK-OAK-001', 'Oak Work Desk', 'Minimal office desk with drawer module', 'ACTIVE'::product_status, now(), now())
            ON CONFLICT (product_code) DO NOTHING;
            """,
            cancellationToken);
    }

    private static Task<int> SeedProductVersionsAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        return dbContext.Database.ExecuteSqlRawAsync(
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
}
