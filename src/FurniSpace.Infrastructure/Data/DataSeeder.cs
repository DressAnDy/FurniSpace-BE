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
}
