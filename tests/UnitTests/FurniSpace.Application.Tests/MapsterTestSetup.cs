#nullable enable

using System.Runtime.CompilerServices;
using FurniSpace.Application.Mappings;
using Mapster;

namespace FurniSpace.Application.Tests;

internal static class MapsterTestSetup
{
    private static readonly object Sync = new();
    private static bool _configured;

    [ModuleInitializer]
    internal static void Initialize()
    {
        EnsureConfigured();
    }

    internal static void EnsureConfigured()
    {
        if (_configured)
        {
            return;
        }

        lock (Sync)
        {
            if (_configured)
            {
                return;
            }

            TypeAdapterConfig.GlobalSettings.Scan(typeof(AccountMappingConfig).Assembly);
            _configured = true;
        }
    }
}
