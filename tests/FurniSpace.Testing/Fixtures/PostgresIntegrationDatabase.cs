using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Npgsql.NameTranslation;
using Respawn;
using Respawn.Graph;
using Testcontainers.PostgreSql;

namespace FurniSpace.Testing.Fixtures;

public sealed class PostgresIntegrationDatabase : IAsyncDisposable
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("furnispace_integration")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private NpgsqlDataSource? _dataSource;
    private Respawner? _respawner;

    public string ConnectionString => _container.GetConnectionString();
    public NpgsqlDataSource DataSource =>
        _dataSource ?? throw new InvalidOperationException("PostgreSQL integration database has not been started.");

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _container.StartAsync(cancellationToken);

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(ConnectionString);
        MapPostgresEnums(dataSourceBuilder);
        _dataSource = dataSourceBuilder.Build();

        await using (var context = CreateDbContext())
        {
            await context.Database.MigrateAsync(cancellationToken);
        }

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        _respawner = await Respawner.CreateAsync(
            connection,
            new RespawnerOptions
            {
                DbAdapter = DbAdapter.Postgres,
                TablesToIgnore = [new Table("__EFMigrationsHistory")]
            });
    }

    public AppDbContext CreateDbContext()
    {
        if (_dataSource is null)
        {
            throw new InvalidOperationException("PostgreSQL integration database has not been started.");
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(
                _dataSource,
                npgsql => npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .Options;

        return new AppDbContext(options);
    }

    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        if (_respawner is null)
        {
            throw new InvalidOperationException("PostgreSQL integration database has not been started.");
        }

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await _respawner.ResetAsync(connection);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dataSource is not null)
        {
            await _dataSource.DisposeAsync();
        }

        await _container.DisposeAsync();
    }

    private static void MapPostgresEnums(NpgsqlDataSourceBuilder builder)
    {
        var translator = new NpgsqlNullNameTranslator();

        builder.MapEnum<AccountStatus>("account_status", translator);
        builder.MapEnum<ProjectStatus>("project_status", translator);
        builder.MapEnum<ProjectAreaType>("project_area_type", translator);
        builder.MapEnum<ProjectAreaStatus>("project_area_status", translator);
        builder.MapEnum<ProjectScheduleType>("project_schedule_type", translator);
        builder.MapEnum<ProjectScheduleStatus>("project_schedule_status", translator);
        builder.MapEnum<ProposalStatus>("proposal_status", translator);
        builder.MapEnum<ProposalSceneType>("proposal_scene_type", translator);
        builder.MapEnum<ProposalSceneVariantStatus>("proposal_scene_variant_status", translator);
        builder.MapEnum<ProposalSceneVariantType>("proposal_scene_variant_type", translator);
        builder.MapEnum<CustomizationStatus>("customization_status", translator);
        builder.MapEnum<CustomizationVersionStatus>("customization_version_status", translator);
        builder.MapEnum<ProductionFeasibilityStatus>("production_feasibility_status", translator);
        builder.MapEnum<QuotationStatus>("quotation_status", translator);
        builder.MapEnum<QuotationItemType>("quotation_item_type", translator);
        builder.MapEnum<OrderStatus>("order_status", translator);
        builder.MapEnum<OrderItemStatus>("order_item_status", translator);
        builder.MapEnum<OrderAdjustmentStatus>("order_adjustment_status", translator);
        builder.MapEnum<OrderAdjustmentItemType>("order_adjustment_item_type", translator);
        builder.MapEnum<PaymentStatus>("payment_status", translator);
        builder.MapEnum<PaymentType>("payment_type", translator);
        builder.MapEnum<PaymentProvider>("payment_provider", translator);
        builder.MapEnum<PaymentMethod>("payment_method", translator);
        builder.MapEnum<PaymentTransactionType>("payment_transaction_type", translator);
        builder.MapEnum<PaymentTransactionStatus>("payment_transaction_status", translator);
        builder.MapEnum<ProductionRequestStatus>("production_request_status", translator);
        builder.MapEnum<ProductionItemStatus>("production_item_status", translator);
        builder.MapEnum<ProjectChatType>("project_chat_type", translator);
        builder.MapEnum<ProjectChatStatus>("project_chat_status", translator);
        builder.MapEnum<ProjectChatMessageType>("project_chat_message_type", translator);
        builder.MapEnum<FileStatus>("file_status", translator);
        builder.MapEnum<FileVisibility>("file_visibility", translator);
        builder.MapEnum<FileType>("file_type", translator);
        builder.MapEnum<ProductStatus>("product_status", translator);
        builder.MapEnum<ProductVersionType>("product_version_type", translator);
    }
}
