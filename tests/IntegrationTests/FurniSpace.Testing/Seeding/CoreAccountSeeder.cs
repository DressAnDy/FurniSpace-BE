using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.Testing.Seeding;

public static class CoreRoles
{
    public const string Admin = "ADMIN";
    public const string Sales = "SALES";
    public const string Designer = "DESIGNER";
    public const string Customer = "CUSTOMER";
    public const string Production = "PRODUCTION";
}

public sealed record SeededAccount(
    Guid AccountId,
    Guid RoleId,
    string RoleName,
    string Email,
    string FullName);

public static class CoreAccountSeeder
{
    public static readonly DateTime FixedTimestamp = new(2026, 1, 15, 8, 0, 0, DateTimeKind.Utc);

    public static async Task<IReadOnlyDictionary<string, Role>> EnsureRolesAsync(
        AppDbContext context,
        CancellationToken cancellationToken = default,
        params string[] roleNames)
    {
        var names = roleNames.Length == 0
            ? [CoreRoles.Admin, CoreRoles.Sales, CoreRoles.Designer, CoreRoles.Customer, CoreRoles.Production]
            : roleNames.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        var existing = await context.RoleSet
            .Where(role => names.Contains(role.RoleName))
            .ToListAsync(cancellationToken);

        var roles = existing.ToDictionary(role => role.RoleName, StringComparer.OrdinalIgnoreCase);
        foreach (var name in names)
        {
            if (roles.ContainsKey(name))
            {
                continue;
            }

            var role = CreateRole(name);
            context.RoleSet.Add(role);
            roles[name] = role;
        }

        return roles;
    }

    public static Role CreateRole(string roleName, DateTime? createdAt = null) =>
        new()
        {
            RoleId = Guid.NewGuid(),
            RoleName = roleName,
            CreatedAt = createdAt ?? FixedTimestamp
        };

    public static Account CreateAccount(
        Guid roleId,
        string email,
        string fullName,
        DateTime? createdAt = null) =>
        new()
        {
            AccountId = Guid.NewGuid(),
            RoleId = roleId,
            Email = email,
            PasswordHash = "integration-test-password-hash",
            FullName = fullName,
            Status = AccountStatus.ACTIVE,
            CreatedAt = createdAt ?? FixedTimestamp
        };

    public static async Task<SeededAccount> SeedAccountAsync(
        AppDbContext context,
        string roleName,
        string? email = null,
        string? fullName = null,
        CancellationToken cancellationToken = default)
    {
        var roles = await EnsureRolesAsync(context, cancellationToken, roleName);
        var role = roles[roleName];
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var account = CreateAccount(
            role.RoleId,
            email ?? $"{roleName.ToLowerInvariant()}-{suffix}@integration.test",
            fullName ?? $"{roleName} User");
        context.AccountSet.Add(account);
        await context.SaveChangesAsync(cancellationToken);

        return new SeededAccount(
            account.AccountId,
            role.RoleId,
            role.RoleName,
            account.Email,
            account.FullName);
    }
}
