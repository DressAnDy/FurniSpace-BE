using FurniSpace.Application.DTOs.Products;
using FurniSpace.Infrastructure.Repositories.IRepository;

namespace FurniSpace.Application.Common.Catalog;

public static class CatalogFileLoader
{
    private const bool CustomerVisibleOnly = true;

    public static async Task<List<CatalogFileDto>> LoadCustomerVisibleListAsync(
        IProjectFileRepository files,
        string referenceType,
        IReadOnlyList<Guid> referenceIds,
        CancellationToken cancellationToken = default)
    {
        if (referenceIds.Count == 0)
        {
            return [];
        }

        var catalogFiles = await files.GetCatalogFilesByReferencesAsync(
            referenceType,
            referenceIds,
            CustomerVisibleOnly,
            cancellationToken);

        return CatalogFileMapper.ToList(catalogFiles, CustomerVisibleOnly);
    }
}
