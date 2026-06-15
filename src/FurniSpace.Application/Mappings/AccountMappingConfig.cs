using FurniSpace.Application.DTOs.Accounts;
using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure.DTOs.Accounts;
using Mapster;

namespace FurniSpace.Application.Mappings;

public sealed class AccountMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Account, AccountDto>()
            .Map(destination => destination.Status, source => source.Status.HasValue ? source.Status.Value.ToString() : null);

        config.NewConfig<Account, MyProfileDto>()
            .Map(destination => destination.Status, source => source.Status.HasValue ? source.Status.Value.ToString() : string.Empty);

        config.NewConfig<AccountDetailReadModel, AccountDetailDto>()
            .Map(destination => destination.Status, source => source.Status.HasValue ? source.Status.Value.ToString() : null);

        config.NewConfig<AvailableDesignerReadModel, AvailableDesignerDto>()
            .Map(destination => destination.Status, source => source.Status.HasValue ? source.Status.Value.ToString() : null);
    }
}
